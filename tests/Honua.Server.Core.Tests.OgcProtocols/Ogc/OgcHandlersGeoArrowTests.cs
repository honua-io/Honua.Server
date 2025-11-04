using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Apache.Arrow;
using Apache.Arrow.Ipc;
using Apache.Arrow.Types;
using FluentAssertions;
using Honua.Server.Host.Ogc;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Ogc;

[Collection("UnitTests")]
[Trait("Category", "Unit")]
[Trait("Feature", "OGC")]
[Trait("Speed", "Fast")]
public class OgcHandlersGeoArrowTests : IClassFixture<OgcHandlerTestFixture>
{
    private readonly OgcHandlerTestFixture _fixture;

    public OgcHandlersGeoArrowTests(OgcHandlerTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Items_WithGeoArrowFormat_ShouldReturnStream()
    {
        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items", "f=geoarrow");

        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            _fixture.Resolver,
            _fixture.Repository,
            _fixture.GeoPackageExporter,
            _fixture.ShapefileExporter,
            _fixture.FlatGeobufExporter,
            _fixture.GeoArrowExporter,
            _fixture.CsvExporter,
            _fixture.AttachmentOrchestrator,
            _fixture.Registry,
            _fixture.ApiMetrics,
            _fixture.CacheHeaderService,
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Be("application/vnd.apache.arrow.stream");

        context.Response.Body.Length.Should().BeGreaterThan(0);

        // Validate Apache Arrow IPC stream format
        context.Response.Body.Position = 0;

        // Read Arrow stream using ArrowStreamReader
        using var arrowReader = new ArrowStreamReader(context.Response.Body);

        // Validate schema
        var schema = arrowReader.Schema;
        schema.Should().NotBeNull("Arrow stream must have a schema");
        schema.FieldsList.Should().NotBeEmpty("Schema must have at least one field");

        // Validate geometry field exists in schema
        var geometryField = schema.FieldsList.FirstOrDefault(f =>
            f.Name.Equals("geometry", StringComparison.OrdinalIgnoreCase) ||
            f.Name.Equals("geom", StringComparison.OrdinalIgnoreCase));
        geometryField.Should().NotBeNull("Schema must contain a geometry field");

        // Validate name field exists (from test data)
        var nameField = schema.FieldsList.FirstOrDefault(f =>
            f.Name.Equals("name", StringComparison.OrdinalIgnoreCase));
        nameField.Should().NotBeNull("Schema must contain a 'name' field");

        // Read record batches
        var recordBatches = new System.Collections.Generic.List<RecordBatch>();
        while (true)
        {
            var batch = arrowReader.ReadNextRecordBatch();
            if (batch == null)
            {
                break;
            }
            recordBatches.Add(batch);
        }

        recordBatches.Should().NotBeEmpty("Arrow stream must contain at least one record batch");

        // Validate total row count
        var totalRows = recordBatches.Sum(b => b.Length);
        totalRows.Should().BeGreaterThan(0, "Record batches must contain at least one row");

        // Validate first record batch structure
        var firstBatch = recordBatches[0];
        firstBatch.Should().NotBeNull();
        firstBatch.Length.Should().BeGreaterThan(0, "First record batch must have rows");
        firstBatch.ColumnCount.Should().Be(schema.FieldsList.Count, "Batch columns must match schema fields");

        // Validate columns are not null
        for (int i = 0; i < firstBatch.ColumnCount; i++)
        {
            var column = firstBatch.Column(i);
            column.Should().NotBeNull($"Column {i} ({schema.GetFieldByIndex(i).Name}) should not be null");
        }

        // Validate geometry column has data
        var geomColumnIndex = schema.GetFieldIndex(geometryField.Name);
        var geometryColumn = firstBatch.Column(geomColumnIndex);
        geometryColumn.Should().NotBeNull("Geometry column must exist");
        geometryColumn.Length.Should().BeGreaterThan(0, "Geometry column must have data");

        // Validate name column has data
        var nameColumnIndex = schema.GetFieldIndex(nameField.Name);
        var nameColumn = firstBatch.Column(nameColumnIndex);
        nameColumn.Should().NotBeNull("Name column must exist");
        nameColumn.Length.Should().BeGreaterThan(0, "Name column must have data");

        // Clean up record batches
        foreach (var batch in recordBatches)
        {
            batch.Dispose();
        }
    }
}
