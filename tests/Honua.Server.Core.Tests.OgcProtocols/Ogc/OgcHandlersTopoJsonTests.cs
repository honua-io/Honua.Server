using Microsoft.Extensions.Logging.Abstractions;
﻿using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Export;
using Honua.Server.Core.Query;
using Honua.Server.Core.Raster.Export;
using Honua.Server.Host.Ogc;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Ogc;

[Collection("UnitTests")]
[Trait("Category", "Unit")]
[Trait("Feature", "OGC")]
[Trait("Speed", "Fast")]
public class OgcHandlersTopoJsonTests
{
    private static readonly IFlatGeobufExporter FlatGeobufExporter = new FlatGeobufExporter();
    private static readonly IGeoArrowExporter GeoArrowExporter = new GeoArrowExporter();

    [Fact]
    public async Task Items_WithTopoJsonFormat_ShouldReturnTopology()
    {
        var registry = OgcTestUtilities.CreateRegistry();
        var resolver = OgcTestUtilities.CreateResolver(registry);
        var repository = OgcTestUtilities.CreateRepository();
        var geoPackageExporter = new GeoPackageExporter(NullLogger<GeoPackageExporter>.Instance);
        var shapefileExporter = OgcTestUtilities.CreateShapefileExporterStub();
        var csvExporter = OgcTestUtilities.CreateCsvExporter();
        var context = OgcTestUtilities.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/items",
            "f=topojson");

        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            resolver,
            repository,
            geoPackageExporter,
            shapefileExporter,
            FlatGeobufExporter,
            GeoArrowExporter,
            csvExporter,
            OgcTestUtilities.CreateAttachmentOrchestratorStub(),
            registry,
            OgcTestUtilities.CreateApiMetrics(),
            OgcTestUtilities.CreateCacheHeaderService(),
            OgcTestUtilities.CreateOgcFeaturesAttachmentHandlerStub(),
            new Core.Elevation.DefaultElevationService(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Be("application/topo+json");
        context.Response.Headers["Content-Crs"].ToString().Should().Be("<http://www.opengis.net/def/crs/OGC/1.3/CRS84>");

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        var payload = reader.ReadToEnd();

        var document = JsonNode.Parse(payload)?.AsObject();
        document.Should().NotBeNull();
        document!["type"]!.GetValue<string>().Should().Be("Topology");
        document["title"]!.GetValue<string>().Should().Be("Primary Roads");

        var objects = document["objects"]!.AsObject();
        objects.ContainsKey("roads::roads-primary").Should().BeTrue();
        var collection = objects["roads::roads-primary"]!.AsObject();
        collection["type"]!.GetValue<string>().Should().Be("GeometryCollection");

        var geometries = collection["geometries"]!.AsArray();
        geometries.Should().HaveCount(2);
        geometries[0]!["type"]!.GetValue<string>().Should().Be("LineString");
        geometries[0]!["properties"]!["name"]!.GetValue<string>().Should().Be("First");
        geometries[0]!["properties"]!["title"]!.GetValue<string>().Should().Be("1");

        var arcs = document["arcs"]!.AsArray();
        arcs.Should().HaveCount(2);

        var meta = document["meta"]!.AsObject();
        meta["numberMatched"]!.GetValue<long>().Should().Be(2);
        meta["numberReturned"]!.GetValue<long>().Should().Be(2);

        var bbox = document["bbox"]!.AsArray();
        bbox.Should().HaveCount(4);
        bbox[0]!.GetValue<double>().Should().BeApproximately(-122.41, 1e-6);
        bbox[1]!.GetValue<double>().Should().BeApproximately(45.6, 1e-6);
        bbox[2]!.GetValue<double>().Should().BeApproximately(-122.39, 1e-6);
        bbox[3]!.GetValue<double>().Should().BeApproximately(45.62, 1e-6);
    }

    [Fact]
    public async Task Item_WithTopoJsonFormat_ShouldReturnTopology()
    {
        var registry = OgcTestUtilities.CreateRegistry();
        var resolver = OgcTestUtilities.CreateResolver(registry);
        var repository = OgcTestUtilities.CreateRepository();
        var context = OgcTestUtilities.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/items/1",
            "f=topojson");

        var result = await OgcFeaturesHandlers.GetCollectionItem(
            "roads::roads-primary",
            "1",
            context.Request,
            resolver,
            repository,
            OgcTestUtilities.CreateAttachmentOrchestratorStub(),
            registry,
            OgcTestUtilities.CreateCacheHeaderService(),
            OgcTestUtilities.CreateOgcFeaturesAttachmentHandlerStub(),
            OgcTestUtilities.CreateOgcFeaturesEditingHandlerStub(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Be("application/topo+json");
        context.Response.Headers["Content-Crs"].ToString().Should().Be("<http://www.opengis.net/def/crs/OGC/1.3/CRS84>");

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        var payload = reader.ReadToEnd();

        var document = JsonNode.Parse(payload)?.AsObject();
        document.Should().NotBeNull();
        document!["type"]!.GetValue<string>().Should().Be("Topology");

        var objects = document["objects"]!.AsObject();
        objects.ContainsKey("roads::roads-primary").Should().BeTrue();
        var collection = objects["roads::roads-primary"]!.AsObject();

        var geometries = collection["geometries"]!.AsArray();
        geometries.Should().HaveCount(1);
        geometries[0]!["type"]!.GetValue<string>().Should().Be("LineString");
        geometries[0]!["id"]!.GetValue<string>().Should().Be("1");
        geometries[0]!["properties"]!["name"]!.GetValue<string>().Should().Be("First");

        var arcs = document["arcs"]!.AsArray();
        arcs.Should().HaveCount(1);
    }

    [Fact]
    public async Task Items_WithTopoJsonFormat_ShouldConvertWktGeometries()
    {
        var registry = OgcTestUtilities.CreateRegistry();
        var resolver = OgcTestUtilities.CreateResolver(registry);
        var repository = new WktFeatureRepository();
        var geoPackageExporter = new GeoPackageExporter(NullLogger<GeoPackageExporter>.Instance);
        var shapefileExporter = OgcTestUtilities.CreateShapefileExporterStub();
        var csvExporter = OgcTestUtilities.CreateCsvExporter();
        var context = OgcTestUtilities.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/items",
            "f=topojson");

        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            resolver,
            repository,
            geoPackageExporter,
            shapefileExporter,
            FlatGeobufExporter,
            GeoArrowExporter,
            csvExporter,
            OgcTestUtilities.CreateAttachmentOrchestratorStub(),
            registry,
            OgcTestUtilities.CreateApiMetrics(),
            OgcTestUtilities.CreateCacheHeaderService(),
            OgcTestUtilities.CreateOgcFeaturesAttachmentHandlerStub(),
            new Core.Elevation.DefaultElevationService(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Be("application/topo+json");
        context.Response.Headers["Content-Crs"].ToString().Should().Be("<http://www.opengis.net/def/crs/OGC/1.3/CRS84>");

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        var payload = reader.ReadToEnd();

        var document = JsonNode.Parse(payload)!.AsObject();
        var arcs = document["arcs"]!.AsArray();
        arcs.Should().NotBeEmpty();

        var firstPoint = arcs[0]!.AsArray()[0]!.AsArray();
        firstPoint[0]!.GetValue<double>().Should().BeApproximately(-122.4, 1e-6);
        firstPoint[1]!.GetValue<double>().Should().BeApproximately(45.6, 1e-6);

        var secondPoint = arcs[0]!.AsArray()[1]!.AsArray();
        secondPoint[0]!.GetValue<double>().Should().BeApproximately(-122.35, 1e-6);
        secondPoint[1]!.GetValue<double>().Should().BeApproximately(45.65, 1e-6);

        var geometry = document["objects"]!["roads::roads-primary"]!["geometries"]![0]!.AsObject();
        geometry["type"]!.GetValue<string>().Should().Be("LineString");
        var arcReferences = geometry["arcs"]!.AsArray();
        arcReferences.Should().HaveCount(1);
        arcReferences[0]!.GetValue<int>().Should().Be(0);

        var meta = document["meta"]!.AsObject();
        meta["numberMatched"]!.GetValue<long>().Should().Be(1);
        meta["numberReturned"]!.GetValue<long>().Should().Be(1);
    }

    private sealed class WktFeatureRepository : IFeatureRepository
    {
        private readonly List<FeatureRecord> _records = new()
        {
            new FeatureRecord(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["road_id"] = 1,
                ["name"] = "Segment",
                ["geom"] = "LINESTRING(-122.4 45.6, -122.35 45.65)"
            })
        };

        public Task<FeatureRecord> CreateAsync(string serviceId, string layerId, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> DeleteAsync(string serviceId, string layerId, string featureId, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FeatureRecord?> GetAsync(string serviceId, string layerId, string featureId, FeatureQuery? query = null, CancellationToken cancellationToken = default)
        {
            if (!int.TryParse(featureId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                return Task.FromResult<FeatureRecord?>(null);
            }

            var match = _records.Find(record => Convert.ToInt32(record.Attributes["road_id"], CultureInfo.InvariantCulture) == id);
            return Task.FromResult(match);
        }

        public async IAsyncEnumerable<FeatureRecord> QueryAsync(string serviceId, string layerId, FeatureQuery? query, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var record in _records)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return record;
            }

            await Task.CompletedTask;
        }

        public Task<FeatureRecord?> UpdateAsync(string serviceId, string layerId, string featureId, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<long> CountAsync(string serviceId, string layerId, FeatureQuery? query, CancellationToken cancellationToken = default)
            => Task.FromResult((long)_records.Count);

        public Task<byte[]> GenerateMvtTileAsync(string serviceId, string layerId, int zoom, int x, int y, string? datetime = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<byte>());

        public Task<IReadOnlyList<StatisticsResult>> QueryStatisticsAsync(string serviceId, string layerId, IReadOnlyList<StatisticDefinition> statistics, IReadOnlyList<string>? groupByFields, FeatureQuery? filter, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StatisticsResult>>(Array.Empty<StatisticsResult>());

        public Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(string serviceId, string layerId, IReadOnlyList<string> fieldNames, FeatureQuery? filter, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DistinctResult>>(Array.Empty<DistinctResult>());

        public Task<BoundingBox?> QueryExtentAsync(string serviceId, string layerId, FeatureQuery? filter, CancellationToken cancellationToken = default)
            => Task.FromResult<BoundingBox?>(null);
    }

}
