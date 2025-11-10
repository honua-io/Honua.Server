using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FlatGeobuf.NTS;
using Honua.Server.Host.Ogc;
using Microsoft.AspNetCore.Http;
using NetTopologySuite.Geometries;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Ogc;

[Collection("UnitTests")]
[Trait("Category", "Unit")]
[Trait("Feature", "OGC")]
[Trait("Speed", "Fast")]
public class OgcHandlersFlatGeobufTests : IClassFixture<OgcHandlerTestFixture>
{
    private readonly OgcHandlerTestFixture _fixture;

    public OgcHandlersFlatGeobufTests(OgcHandlerTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Items_WithFlatGeobufFormat_ShouldReturnArchive()
    {
        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items", "f=flatgeobuf");

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
            OgcTestUtilities.CreateOgcFeaturesAttachmentHandlerStub(),
            new Core.Elevation.DefaultElevationService(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Be("application/vnd.flatgeobuf");

        context.Response.Body.Length.Should().BeGreaterThan(0);

        // Validate FlatGeobuf binary format
        context.Response.Body.Position = 0;

        // Validate magic bytes (FlatGeobuf signature)
        var header = new byte[8];
        var bytesRead = await context.Response.Body.ReadAsync(header, 0, 8);
        bytesRead.Should().Be(8, "Should be able to read FlatGeobuf header");

        // FlatGeobuf magic bytes: 0x66, 0x67, 0x62 (fgb) followed by version
        header[0].Should().Be(0x66, "First magic byte should be 'f'");
        header[1].Should().Be(0x67, "Second magic byte should be 'g'");
        header[2].Should().Be(0x62, "Third magic byte should be 'b'");

        // Validate can deserialize to features
        context.Response.Body.Position = 0;
        var features = FeatureCollectionConversions.Deserialize(context.Response.Body);

        features.Should().NotBeNull("FlatGeobuf stream should deserialize to feature collection");
        features!.Count().Should().BeGreaterThan(0, "Feature collection must contain at least one feature");

        // Validate first feature structure
        var firstFeature = features.First();
        firstFeature.Should().NotBeNull();
        firstFeature.Geometry.Should().NotBeNull("Feature must have geometry");
        firstFeature.Geometry.Should().BeOfType<LineString>("Roads should have LineString geometry");

        var lineString = (LineString)firstFeature.Geometry;
        lineString.Coordinates.Should().NotBeEmpty("LineString must have coordinates");
        lineString.Coordinates.Length.Should().BeGreaterThan(1, "LineString must have at least 2 points");

        // Validate coordinates are valid
        foreach (var coord in lineString.Coordinates.Take(2))
        {
            coord.Should().NotBeNull();
            coord.X.Should().NotBe(0, "Longitude should be set");
            coord.Y.Should().NotBe(0, "Latitude should be set");
        }

        // Validate feature has attributes
        firstFeature.Attributes.Should().NotBeNull("Feature must have attributes");
        firstFeature.Attributes.Count.Should().BeGreaterThan(0, "Feature should have at least one attribute");

        // Check for expected attribute (name field from test data)
        firstFeature.Attributes.Exists("name").Should().BeTrue("Feature should have 'name' attribute");
        var nameValue = firstFeature.Attributes["name"];
        nameValue.Should().NotBeNull("Name attribute should have a value");
    }
}
