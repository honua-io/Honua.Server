using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Data;
using Honua.Server.Core.Export;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Host.Ogc;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Ogc;

[Collection("UnitTests")]
[Trait("Category", "Unit")]
[Trait("Feature", "OGC")]
[Trait("Speed", "Fast")]
public class OgcHandlersGeoJsonTests : IClassFixture<OgcHandlerTestFixture>
{
    private readonly OgcHandlerTestFixture _fixture;

    public OgcHandlersGeoJsonTests(OgcHandlerTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Items_WithGeoJsonFormat_ShouldReturnFeatureCollection()
    {
        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items", "f=geojson&limit=2");

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
        context.Response.ContentType.Should().Be("application/geo+json");
        context.Response.Headers["Content-Crs"].ToString().Should().Be("<http://www.opengis.net/def/crs/OGC/1.3/CRS84>");

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var payload = reader.ReadToEnd();
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "geojson_items_payload.json"), payload);
        using var document = JsonDocument.Parse(payload);

        var root = document.RootElement;
        root.GetProperty("type").GetString().Should().Be("FeatureCollection");
        root.GetProperty("numberMatched").GetInt64().Should().Be(2);
        root.GetProperty("numberReturned").GetInt32().Should().Be(2);
        root.GetProperty("defaultStyle").GetString().Should().Be("primary-roads-line");
        var collectionStyles = root.GetProperty("styleIds").EnumerateArray().Select(element => element.GetString()).ToArray();
        collectionStyles.Should().Contain("primary-roads-line");

        var features = root.GetProperty("features");
        features.GetArrayLength().Should().Be(2);

        var featureArray = features.EnumerateArray().ToList();
        foreach (var feature in featureArray)
        {
            feature.GetProperty("type").GetString().Should().Be("Feature");
            feature.GetProperty("geometry").Should().NotBeNull();
            var properties = feature.GetProperty("properties");
            properties.TryGetProperty("name", out _).Should().BeTrue();
            properties.GetProperty("honua:defaultStyleId").GetString().Should().Be("primary-roads-line");
            var featureStyleIds = properties.GetProperty("honua:styleIds").EnumerateArray().Select(element => element.GetString()).ToArray();
            featureStyleIds.Should().Contain("primary-roads-line");
        }

        // Validate first feature's geometry in detail
        var firstFeature = featureArray[0];
        var geometry = firstFeature.GetProperty("geometry");
        geometry.GetProperty("type").GetString().Should().Be("LineString");

        var coordinates = geometry.GetProperty("coordinates").EnumerateArray().ToList();
        coordinates.Should().HaveCountGreaterThan(1, "LineString must have at least 2 coordinate points");

        // Validate coordinate structure (should be [lon, lat] arrays)
        foreach (var coord in coordinates.Take(2))
        {
            var coordArray = coord.EnumerateArray().ToList();
            coordArray.Should().HaveCountGreaterThanOrEqualTo(2, "Each coordinate must have at least [lon, lat]");

            // Validate coordinate values are numbers
            var lon = coordArray[0].GetDouble();
            var lat = coordArray[1].GetDouble();
            lon.Should().NotBe(0, "Longitude should be set");
            lat.Should().NotBe(0, "Latitude should be set");
        }

        // Validate feature properties in detail
        var props = firstFeature.GetProperty("properties");
        props.GetProperty("name").GetString().Should().NotBeNullOrEmpty("Feature name should not be empty");
    }

    [Fact]
    public async Task Item_WithGeoJsonFormat_ShouldReturnFeature()
    {
        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items/1", "f=geojson");

        var result = await OgcFeaturesHandlers.GetCollectionItem(
            "roads::roads-primary",
            "1",
            context.Request,
            _fixture.Resolver,
            _fixture.Repository,
            _fixture.AttachmentOrchestrator,
            _fixture.Registry,
            _fixture.CacheHeaderService,
            OgcTestUtilities.CreateOgcFeaturesAttachmentHandlerStub(),
            OgcTestUtilities.CreateOgcFeaturesEditingHandlerStub(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Be("application/geo+json");
        context.Response.Headers["Content-Crs"].ToString().Should().Be("<http://www.opengis.net/def/crs/OGC/1.3/CRS84>");

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var payload = reader.ReadToEnd();
        using var document = JsonDocument.Parse(payload);

        var root = document.RootElement;
        root.GetProperty("type").GetString().Should().Be("Feature");

        var idElement = root.GetProperty("id");
        idElement.ValueKind.Should().BeOneOf(JsonValueKind.String, JsonValueKind.Number);

        // Validate geometry structure
        var geometry = root.GetProperty("geometry");
        geometry.GetProperty("type").GetString().Should().Be("LineString");

        var coordinates = geometry.GetProperty("coordinates").EnumerateArray().ToList();
        coordinates.Should().HaveCountGreaterThan(1, "LineString must have at least 2 coordinate points");

        // Validate coordinate structure (should be [lon, lat] arrays)
        foreach (var coord in coordinates.Take(2))
        {
            var coordArray = coord.EnumerateArray().ToList();
            coordArray.Should().HaveCountGreaterThanOrEqualTo(2, "Each coordinate must have at least [lon, lat]");

            // Validate coordinate values are numbers
            var lon = coordArray[0].GetDouble();
            var lat = coordArray[1].GetDouble();
            lon.Should().NotBe(0, "Longitude should be set");
            lat.Should().NotBe(0, "Latitude should be set");
        }

        // Validate properties
        var properties = root.GetProperty("properties");
        properties.TryGetProperty("name", out var nameProperty).Should().BeTrue();
        nameProperty.GetString().Should().Be("First");
        properties.GetProperty("honua:defaultStyleId").GetString().Should().Be("primary-roads-line");
        var styleIds = properties.GetProperty("honua:styleIds").EnumerateArray().Select(element => element.GetString()).ToArray();
        styleIds.Should().Contain("primary-roads-line");
    }

    [Fact]
    public async Task Items_WithAttachmentExposure_ShouldEmitEnclosureLinks()
    {
        var snapshot = OgcTestUtilities.CreateSnapshot(attachmentsEnabled: true, exposeOgcLinks: true);
        var registry = _fixture.CreateRegistry(snapshot);
        var resolver = OgcTestUtilities.CreateResolver(registry);

        var descriptor = new AttachmentDescriptor
        {
            AttachmentObjectId = 7,
            AttachmentId = "att-7",
            ServiceId = "roads",
            LayerId = "roads-primary",
            FeatureId = "1",
            Name = "inspection-photo.jpg",
            MimeType = "image/jpeg",
            SizeBytes = 2048,
            ChecksumSha256 = new string('a', 64),
            StorageProvider = "memory",
            StorageKey = "attachments/att-7",
            CreatedUtc = DateTimeOffset.UtcNow,
            CreatedBy = "tester"
        };

        var attachments = new Dictionary<string, IReadOnlyList<AttachmentDescriptor>>(StringComparer.OrdinalIgnoreCase)
        {
            ["roads:roads-primary:1"] = new[] { descriptor }
        };

        var attachmentOrchestrator = _fixture.CreateAttachmentOrchestrator(attachments);
        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items", "f=geojson&limit=1");

        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            resolver,
            _fixture.Repository,
            _fixture.GeoPackageExporter,
            _fixture.ShapefileExporter,
            _fixture.FlatGeobufExporter,
            _fixture.GeoArrowExporter,
            _fixture.CsvExporter,
            attachmentOrchestrator,
            registry,
            _fixture.ApiMetrics,
            _fixture.CacheHeaderService,
            OgcTestUtilities.CreateOgcFeaturesAttachmentHandlerStub(),
            new Core.Elevation.DefaultElevationService(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        using var document = JsonDocument.Parse(reader.ReadToEnd());

        var feature = document.RootElement.GetProperty("features")[0];
        var links = feature.GetProperty("links").EnumerateArray().ToArray();
        links.Should().Contain(link => string.Equals(link.GetProperty("rel").GetString(), "enclosure", StringComparison.OrdinalIgnoreCase));

        var enclosure = links.First(link => string.Equals(link.GetProperty("rel").GetString(), "enclosure", StringComparison.OrdinalIgnoreCase));
        enclosure.GetProperty("type").GetString().Should().Be("image/jpeg");
        enclosure.GetProperty("title").GetString().Should().Be("inspection-photo.jpg");
        enclosure.GetProperty("href").GetString().Should().Be("https://localhost:5001/rest/services/root/roads/FeatureServer/0/1/attachments/7");
    }

    [Fact]
    public async Task Items_WithFilterParameter_ShouldApplyQueryFilter()
    {
        var repository = new CapturingFeatureRepository();
        var context = _fixture.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/items",
            "filter=name%20=%20'First'");

        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            _fixture.Resolver,
            repository,
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

        repository.LastQuery.Should().NotBeNull();
        var filter = repository.LastQuery!.Filter;
        filter.Should().NotBeNull();

        var comparison = filter!.Expression.Should().BeOfType<QueryBinaryExpression>().Subject;
        comparison.Operator.Should().Be(QueryBinaryOperator.Equal);

        var field = comparison.Left.Should().BeOfType<QueryFieldReference>().Subject;
        field.Name.Should().Be("name");

        var constant = comparison.Right.Should().BeOfType<QueryConstant>().Subject;
        constant.Value.Should().Be("First");
    }

    [Fact]
    public async Task Items_WithIdsParameter_ShouldCombineIdFilter()
    {
        var repository = new CapturingFeatureRepository();
        var context = _fixture.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/items",
            "ids=1,2");

        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            _fixture.Resolver,
            repository,
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

        repository.LastQuery.Should().NotBeNull();
        var filter = repository.LastQuery!.Filter;
        filter.Should().NotBeNull();

        var orExpression = filter!.Expression.Should().BeOfType<QueryBinaryExpression>().Subject;
        orExpression.Operator.Should().Be(QueryBinaryOperator.Or);

        var leftComparison = orExpression.Left.Should().BeOfType<QueryBinaryExpression>().Subject;
        leftComparison.Operator.Should().Be(QueryBinaryOperator.Equal);
        leftComparison.Right.Should().BeOfType<QueryConstant>().Subject.Value.Should().Be(1L);

        var rightComparison = orExpression.Right.Should().BeOfType<QueryBinaryExpression>().Subject;
        rightComparison.Operator.Should().Be(QueryBinaryOperator.Equal);
        rightComparison.Right.Should().BeOfType<QueryConstant>().Subject.Value.Should().Be(2L);
    }

    [Fact]
    public async Task Items_WithSortBy_ShouldApplyRequestedSortOrder()
    {
        var repository = new CapturingFeatureRepository();
        var context = _fixture.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/items",
            "sortby=-name,road_id");

        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            _fixture.Resolver,
            repository,
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

        repository.LastQuery.Should().NotBeNull();
        var sortOrders = repository.LastQuery!.SortOrders;
        sortOrders.Should().NotBeNull();
        sortOrders!.Should().BeEquivalentTo(
            new[]
            {
                new FeatureSortOrder("name", FeatureSortDirection.Descending),
                new FeatureSortOrder("road_id", FeatureSortDirection.Ascending)
            },
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Items_WithUnsupportedFilterLang_ShouldReturnBadRequest()
    {
        var context = _fixture.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/items",
            "filter-lang=cql-json");

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

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Items_WithInvalidSortField_ShouldReturnBadRequest()
    {
        var context = _fixture.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/items",
            "sortby=unknown");

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

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    private sealed class CapturingFeatureRepository : IFeatureRepository
    {
        private readonly List<FeatureRecord> _records = new()
        {
            new FeatureRecord(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["road_id"] = 1L,
                ["name"] = "First",
                ["geom"] = ParseGeometry("{\"type\":\"LineString\",\"coordinates\":[[-122.4,45.6],[-122.39,45.61]]}")
            }),
            new FeatureRecord(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["road_id"] = 2L,
                ["name"] = "Second",
                ["geom"] = ParseGeometry("{\"type\":\"LineString\",\"coordinates\":[[-122.41,45.61],[-122.4,45.62]]}")
            })
        };

        public FeatureQuery? LastQuery { get; private set; }

        public Task<FeatureRecord> CreateAsync(string serviceId, string layerId, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> DeleteAsync(string serviceId, string layerId, string featureId, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FeatureRecord?> GetAsync(string serviceId, string layerId, string featureId, FeatureQuery? query = null, CancellationToken cancellationToken = default)
        {
            if (!long.TryParse(featureId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                return Task.FromResult<FeatureRecord?>(null);
            }

            var match = _records.Find(r => Convert.ToInt64(r.Attributes["road_id"], CultureInfo.InvariantCulture) == id);
            return Task.FromResult(match);
        }

        public async IAsyncEnumerable<FeatureRecord> QueryAsync(
            string serviceId,
            string layerId,
            FeatureQuery? query,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastQuery = query;

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
        {
            LastQuery = query;
            return Task.FromResult((long)_records.Count);
        }

        public Task<byte[]> GenerateMvtTileAsync(string serviceId, string layerId, int zoom, int x, int y, string? datetime = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<byte>());

        public Task<IReadOnlyList<StatisticsResult>> QueryStatisticsAsync(string serviceId, string layerId, IReadOnlyList<StatisticDefinition> statistics, IReadOnlyList<string>? groupByFields, FeatureQuery? filter, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StatisticsResult>>(Array.Empty<StatisticsResult>());

        public Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(string serviceId, string layerId, IReadOnlyList<string> fieldNames, FeatureQuery? filter, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DistinctResult>>(Array.Empty<DistinctResult>());

        public Task<BoundingBox?> QueryExtentAsync(string serviceId, string layerId, FeatureQuery? filter, CancellationToken cancellationToken = default)
            => Task.FromResult<BoundingBox?>(null);

        private static JsonNode ParseGeometry(string json)
        {
            return JsonNode.Parse(json) ?? throw new InvalidOperationException("Invalid geometry JSON.");
        }
    }
}
