using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Metadata;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Hosting;

/// <summary>
/// Integration tests for ArcGIS REST API FeatureServer endpoints.
/// Tests feature queries, editing capabilities, relationships, and export formats.
/// </summary>
[Collection("EndpointTests")]
[Trait("Category", "Integration")]
[Trait("Feature", "GeoservicesREST")]
[Trait("Server", "FeatureServer")]
public class GeoservicesFeatureServerTests : IClassFixture<GeoservicesLeafletFixture>
{
    private readonly GeoservicesLeafletFixture _fixture;

    public GeoservicesFeatureServerTests(GeoservicesLeafletFixture fixture)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
    }

    [Fact]
    public async Task ServicesDirectory_ShouldIncludeImageServerEntry()
    {
        using var document = await GetJsonAsync("/rest/services?f=json");
        var services = document.RootElement.GetProperty("services").EnumerateArray().ToArray();
        var featureServer = services.First(entry =>
        {
            var type = entry.GetProperty("type").GetString();
            var name = entry.GetProperty("name").GetString();
            return string.Equals(type, "FeatureServer", StringComparison.OrdinalIgnoreCase)
                && name != null
                && name.EndsWith("/roads", StringComparison.OrdinalIgnoreCase);
        });

        featureServer.TryGetProperty("url", out var featureUrl).Should().BeTrue();
        featureUrl.GetString().Should().EndWith("/rest/services/transportation/roads/FeatureServer");
    }

    [Fact]
    public async Task FeatureServiceLayerMetadata_ShouldIncludeDrawingInfo()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/FeatureServer/0?f=json");
        var root = document.RootElement;
        root.TryGetProperty("drawingInfo", out var drawingInfo).Should().BeTrue();
        drawingInfo.GetProperty("renderer").GetProperty("type").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task FeatureServiceQuery_ShouldRespectScaleRange()
    {
        using var document = await GetJsonAsync(
            "/rest/services/transportation/roads/FeatureServer/0/query?f=json&where=1=1&outFields=*&mapExtent=-123,45,-122,46&imageDisplay=400,400,96");

        var root = document.RootElement;
        root.GetProperty("features").GetArrayLength().Should().Be(0);
        root.GetProperty("exceededTransferLimit").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task FeatureServiceMetadata_ShouldExposeLayers()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/FeatureServer?f=json");
        var root = document.RootElement;
        root.GetProperty("layers").EnumerateArray().Should().HaveCount(2);
        root.GetProperty("serviceDescription").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task FeatureServiceMetadata_ShouldExposeRelationshipCapability()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/FeatureServer?f=json");
        var root = document.RootElement;
        root.GetProperty("supportsRelationshipsResource").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task FeatureServiceMetadata_ShouldAdvertiseEditingCapabilities()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/FeatureServer?f=json");
        var root = document.RootElement;
        var capabilities = root.GetProperty("capabilities").GetString();
        capabilities.Should().NotBeNull();
        capabilities!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Should().Contain(new[] { "Create", "Delete", "Query", "Update", "Editing" });

        root.GetProperty("allowGeometryUpdates").GetBoolean().Should().BeTrue();
        root.GetProperty("hasStaticData").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task LayerMetadata_ShouldExposeFields()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/FeatureServer/0?f=json");
        var root = document.RootElement;
        root.GetProperty("geometryType").GetString().Should().Be("esriGeometryPolyline");
        root.GetProperty("fields").EnumerateArray().Should().Contain(field =>
            field.GetProperty("type").GetString() == "esriFieldTypeOID" && field.GetProperty("name").GetString() == "road_id");
        root.GetProperty("fields").EnumerateArray().Should().Contain(field =>
            field.GetProperty("name").GetString() == "name" &&
            field.GetProperty("type").GetString() == "esriFieldTypeString");
    }

    [Fact]
    public async Task LayerMetadata_ShouldExposeRelationships()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/FeatureServer/0?f=json");
        var relationships = document.RootElement.GetProperty("relationships").EnumerateArray().ToArray();
        relationships.Should().NotBeEmpty();
        var first = relationships[0];
        first.GetProperty("cardinality").GetString().Should().Be("esriRelCardinalityOneToMany");
        first.GetProperty("relatedTableId").GetInt32().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task LayerMetadata_ShouldExposeEditingFlags()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/FeatureServer/0?f=json");
        var root = document.RootElement;

        root.GetProperty("objectIdField").GetString().Should().Be("road_id");
        root.TryGetProperty("globalIdField", out var globalIdProperty).Should().BeTrue();
        if (globalIdProperty.ValueKind == JsonValueKind.String)
        {
            globalIdProperty.GetString().Should().NotBeNullOrWhiteSpace();
        }

        var capabilities = root.GetProperty("capabilities").GetString();
        capabilities.Should().NotBeNull();
        capabilities!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Should().Contain(new[] { "Create", "Delete", "Query", "Update", "Editing" });

        root.GetProperty("allowGeometryUpdates").GetBoolean().Should().BeTrue();
        root.GetProperty("supportsRollbackOnFailureParameter").GetBoolean().Should().BeTrue();
        root.GetProperty("hasAttachments").GetBoolean().Should().BeFalse();
    }


    [Fact]
    public async Task LayerMetadata_ShouldExposeTimeInfo()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/FeatureServer/0?f=json");
        var root = document.RootElement;
        root.TryGetProperty("timeInfo", out var timeInfoProperty).Should().BeTrue("timeInfo should be emitted for time-enabled layers");

        var timeInfo = timeInfoProperty;
        timeInfo.GetProperty("startTimeField").GetString().Should().Be("observed_at");
        if (timeInfo.TryGetProperty("endTimeField", out var endField))
        {
            endField.ValueKind.Should().Be(JsonValueKind.Null);
        }

        var extent = timeInfo.GetProperty("timeExtent").EnumerateArray()
            .Select(element => element.ValueKind == JsonValueKind.Null ? (long?)null : element.GetInt64())
            .ToArray();

        extent.Should().HaveCount(2);
        extent[0].Should().Be(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds());
        extent[1].Should().Be(new DateTimeOffset(2024, 12, 31, 23, 59, 59, TimeSpan.Zero).ToUnixTimeMilliseconds());

        var timeReference = timeInfo.GetProperty("timeReference");
        timeReference.GetProperty("timeZone").GetString().Should().Be("UTC");
        timeReference.GetProperty("respectsDaylightSaving").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Query_ShouldReturnAllFeatures()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/FeatureServer/0/query?f=json&where=1=1&outFields=*&returnGeometry=false");
        var features = document.RootElement.GetProperty("features").EnumerateArray().ToArray();
        features.Should().HaveCount(3);
        features.Select(feature => feature.GetProperty("attributes").GetProperty("road_id").GetInt32())
            .Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public async Task Query_Filter_ShouldReturnOpenRoads()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/FeatureServer/0/query?f=json&where=status%20=%20%27open%27&outFields=status,road_id&returnGeometry=false");
        var features = document.RootElement.GetProperty("features").EnumerateArray().ToArray();
        features.Should().HaveCount(2);
        features.Select(feature => feature.GetProperty("attributes").GetProperty("status").GetString())
            .Should().OnlyContain(status => status == "open");
    }

    [Fact]
    public async Task Query_Bbox_ShouldRestrictByEnvelope()
    {
        var geometry = Uri.EscapeDataString("{\"xmin\":-122.60,\"ymin\":45.50,\"xmax\":-122.44,\"ymax\":45.58,\"spatialReference\":{\"wkid\":4326}}");
        using var document = await GetJsonAsync($"/rest/services/transportation/roads/FeatureServer/0/query?f=json&where=1=1&geometry={geometry}&geometryType=esriGeometryEnvelope&inSR=4326&spatialRel=esriSpatialRelIntersects&outFields=road_id&returnGeometry=false");
        var ids = document.RootElement.GetProperty("features").EnumerateArray()
            .Select(feature => feature.GetProperty("attributes").GetProperty("road_id").GetInt32())
            .ToArray();
        ids.Should().Contain(1);
        ids.Should().NotContain(2);
    }

    [Fact]
    public async Task Query_Limit_ShouldApplyPaging()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/FeatureServer/0/query?f=json&where=1=1&outFields=road_id&returnGeometry=false&resultRecordCount=1&resultOffset=1");
        var ids = document.RootElement.GetProperty("features").EnumerateArray()
            .Select(feature => feature.GetProperty("attributes").GetProperty("road_id").GetInt32())
            .ToArray();
        ids.Should().BeEquivalentTo(new[] { 2 });

        document.RootElement.TryGetProperty("exceededTransferLimit", out var exceededProperty).Should().BeTrue();
        exceededProperty.GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Query_Count_ShouldReturnTotal()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/FeatureServer/0/query?f=json&where=1=1&returnCountOnly=true");
        document.RootElement.GetProperty("count").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task Query_Ids_ShouldReturnObjectIds()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/FeatureServer/0/query?f=json&where=1=1&returnIdsOnly=true");
        document.RootElement.GetProperty("objectIds").EnumerateArray().Select(element => element.GetInt32())
            .Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public async Task Query_Ids_ShouldReportExceededTransferLimitWhenTruncated()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/FeatureServer/0/query?f=json&where=1=1&returnIdsOnly=true&resultRecordCount=2");
        var root = document.RootElement;

        var ids = root.GetProperty("objectIds").EnumerateArray()
            .Select(element => element.GetInt32())
            .ToArray();

        ids.Should().HaveCount(2);
        root.TryGetProperty("exceededTransferLimit", out var exceededProperty).Should().BeTrue();
        exceededProperty.GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Query_KmlFormat_ShouldReturnKmlDocument()
    {
        var client = _fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/rest/services/transportation/roads/FeatureServer/0/query?f=kml&where=1=1");
        response.EnsureSuccessStatusCode();

        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.google-earth.kml+xml");

        var disposition = response.Content.Headers.ContentDisposition?.ToString();
        disposition.Should().NotBeNull();
        disposition!.Should().Contain("attachment");
        disposition.Should().Contain("roads__roads-primary.kml");

        var payload = await response.Content.ReadAsStringAsync();
        payload.Should().Contain("<kml");
        payload.Should().Contain("<Placemark");
        payload.Should().Contain("Sunset Highway");
    }

    [Fact]
    public async Task Query_GeometryPrecision_ShouldRoundCoordinates()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/FeatureServer/0/query?f=json&where=road_id=1&outFields=road_id&geometryPrecision=1");
        var feature = document.RootElement.GetProperty("features").EnumerateArray().Single();
        var geometry = feature.GetProperty("geometry");

        var firstPoint = geometry.GetProperty("paths")
            .EnumerateArray().First()
            .EnumerateArray().First()
            .EnumerateArray().Select(element => element.GetDouble()).ToArray();

        var secondPoint = geometry.GetProperty("paths")
            .EnumerateArray().First()
            .EnumerateArray().Skip(1).First()
            .EnumerateArray().Select(element => element.GetDouble()).ToArray();

        firstPoint.Should().HaveCount(2);
        secondPoint.Should().HaveCount(2);

        Math.Abs(firstPoint[0] - Math.Round(firstPoint[0], 1)).Should().BeLessThan(1e-9);
        Math.Abs(firstPoint[1] - Math.Round(firstPoint[1], 1)).Should().BeLessThan(1e-9);
        Math.Abs(secondPoint[0] - Math.Round(secondPoint[0], 1)).Should().BeLessThan(1e-9);
        Math.Abs(secondPoint[1] - Math.Round(secondPoint[1], 1)).Should().BeLessThan(1e-9);
    }

    [Fact]
    public async Task Query_ReturnDistinctValues_ShouldDeduplicate()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/FeatureServer/0/query?f=json&where=1=1&outFields=status&returnDistinctValues=true&returnGeometry=false");
        var features = document.RootElement.GetProperty("features").EnumerateArray().ToArray();
        features.Should().HaveCount(2);

        var statuses = features
            .Select(feature => feature.GetProperty("attributes").GetProperty("status").GetString())
            .ToArray();

        statuses.Should().BeEquivalentTo(new[] { "open", "planned" });
    }

    [Fact]
    public async Task Query_OutStatistics_ShouldReturnAggregatedValues()
    {
        var statisticsPayload = Uri.EscapeDataString("[{\"statisticType\":\"sum\",\"onStatisticField\":\"road_id\",\"outStatisticFieldName\":\"totalRoadId\"},{\"statisticType\":\"count\",\"onStatisticField\":\"road_id\",\"outStatisticFieldName\":\"countRoads\"}]");
        var url = $"/rest/services/transportation/roads/FeatureServer/0/query?f=json&where=1=1&groupByFieldsForStatistics=status&outFields=status&outStatistics={statisticsPayload}&returnGeometry=false";
        using var document = await GetJsonAsync(url);

        var features = document.RootElement.GetProperty("features").EnumerateArray()
            .Select(element => element.GetProperty("attributes"))
            .ToDictionary(
                attr => attr.GetProperty("status").GetString()!,
                attr => new
                {
                    Total = attr.TryGetProperty("totalRoadId", out var totalProperty) ? totalProperty.GetDouble() : 0d,
                    Count = attr.TryGetProperty("countRoads", out var countProperty) ? countProperty.GetInt32() : 0
                });

        features.Should().ContainKey("open");
        features.Should().ContainKey("planned");

        features["open"].Total.Should().BeApproximately(4d, 1e-6);
        features["open"].Count.Should().Be(2);

        features["planned"].Total.Should().BeApproximately(2d, 1e-6);
        features["planned"].Count.Should().Be(1);
    }

    [Fact]
    public async Task QueryRelatedRecords_ShouldReturnInspections()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/FeatureServer/0/queryRelatedRecords?relationshipId=1&objectIds=1&outFields=inspection_id,inspector,status,road_id&returnGeometry=false");
        var root = document.RootElement;

        var groups = root.GetProperty("relatedRecordGroups").EnumerateArray().ToArray();
        groups.Should().HaveCount(1);

        var group = groups[0];
        group.GetProperty("objectId").GetInt32().Should().Be(1);

        var relatedRecords = group.GetProperty("relatedRecords").EnumerateArray().ToArray();
        relatedRecords.Should().HaveCount(2);

        var firstRecord = relatedRecords[0].GetProperty("attributes");
        firstRecord.GetProperty("road_id").GetInt32().Should().Be(1);
        firstRecord.GetProperty("inspector").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Query_OrderByFields_ShouldSortDescending()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/FeatureServer/0/query?f=json&where=1=1&outFields=road_id&orderByFields=road_id%20DESC&returnGeometry=false");
        var ids = document.RootElement.GetProperty("features").EnumerateArray()
            .Select(feature => feature.GetProperty("attributes").GetProperty("road_id").GetInt32())
            .ToArray();
        ids.Should().Equal(3, 2, 1);
    }

    [Fact]
    public async Task Query_Where_Like_ShouldMatchName()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/FeatureServer/0/query?f=json&where=name%20LIKE%20'%25Harbor%25'&outFields=name&returnGeometry=false");
        var names = document.RootElement.GetProperty("features").EnumerateArray()
            .Select(feature => feature.GetProperty("attributes").GetProperty("name").GetString())
            .ToArray();
        names.Should().Contain("Harbor Drive");
    }

    [Fact]
    public async Task Query_Where_In_ShouldMatchIds()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/FeatureServer/0/query?f=json&where=road_id%20IN%20(1,3)&outFields=road_id&returnGeometry=false");
        var ids = document.RootElement.GetProperty("features").EnumerateArray()
            .Select(feature => feature.GetProperty("attributes").GetProperty("road_id").GetInt32())
            .OrderBy(id => id)
            .ToArray();
        ids.Should().Equal(1, 3);
    }

    [Fact]
    public async Task Query_Where_Between_ShouldMatchObservedAt()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/FeatureServer/0/query?f=json&where=observed_at%20BETWEEN%20'2024-01-01'%20AND%20'2024-02-15'&outFields=road_id&returnGeometry=false");
        var ids = document.RootElement.GetProperty("features").EnumerateArray()
            .Select(feature => feature.GetProperty("attributes").GetProperty("road_id").GetInt32())
            .OrderBy(id => id)
            .ToArray();
        ids.Should().Equal(1, 2);
    }

    [Fact]
    public async Task Query_TimeParameter_ShouldHonorTemporalWindow()
    {
        var start = new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var end = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        using var document = await GetJsonAsync($"/rest/services/transportation/roads/FeatureServer/0/query?f=json&where=1=1&outFields=road_id&returnGeometry=false&time={start},{end}");
        var ids = document.RootElement.GetProperty("features").EnumerateArray()
            .Select(feature => feature.GetProperty("attributes").GetProperty("road_id").GetInt32())
            .OrderBy(id => id)
            .ToArray();

        ids.Should().Equal(2);
    }

    [Fact]
    public async Task Query_ReturnExtentOnly_ShouldReturnExtent()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/FeatureServer/0/query?f=json&where=1=1&returnExtentOnly=true");
        var root = document.RootElement;
        root.TryGetProperty("extent", out var extentElement).Should().BeTrue();
        extentElement.GetProperty("xmin").GetDouble().Should().BeApproximately(-122.56, 1e-6);
        extentElement.GetProperty("ymin").GetDouble().Should().BeApproximately(45.51, 1e-6);
        extentElement.GetProperty("xmax").GetDouble().Should().BeApproximately(-122.40, 1e-6);
        extentElement.GetProperty("ymax").GetDouble().Should().BeApproximately(45.62, 1e-6);

        root.TryGetProperty("spatialReference", out var spatialRef).Should().BeTrue();
        spatialRef.GetProperty("wkid").GetInt32().Should().Be(4326);
        root.TryGetProperty("count", out var countElement).Should().BeTrue();
        countElement.GetInt64().Should().Be(3);
    }

    [Fact]
    public async Task Query_KmzFormat_ShouldReturnArchive()
    {
        var client = _fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/rest/services/transportation/roads/FeatureServer/0/query?f=kmz&where=1=1");
        response.EnsureSuccessStatusCode();

        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.google-earth.kmz");

        var disposition = response.Content.Headers.ContentDisposition?.ToString();
        disposition.Should().NotBeNull();
        disposition!.Should().Contain("attachment");
        disposition.Should().Contain("roads__roads-primary.kmz");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var archiveStream = new MemoryStream(bytes);
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: false);

        archive.Entries.Should().HaveCount(1);
        var entry = archive.GetEntry("roads__roads-primary.kml");
        entry.Should().NotBeNull();

        using var reader = new StreamReader(entry!.Open(), Encoding.UTF8);
        var payload = reader.ReadToEnd();
        payload.Should().Contain("<Placemark");
        payload.Should().Contain("Sunset Highway");
    }

    [Fact]
    public async Task GenerateRenderer_ShouldReturnRendererPayload()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/FeatureServer/0/generateRenderer?f=json");
        var root = document.RootElement;
        root.TryGetProperty("renderer", out var renderer).Should().BeTrue();
        renderer.GetProperty("type").GetString().Should().NotBeNullOrWhiteSpace();
    }


    [Fact]
    public async Task ReturnUpdates_ShouldProvideBaselineResponse()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/FeatureServer/returnUpdates?f=json&layers=0");
        var root = document.RootElement;

        var layers = root.GetProperty("layers").EnumerateArray().ToArray();
        layers.Should().HaveCount(1);

        var layer = layers[0];
        layer.GetProperty("id").GetInt32().Should().Be(0);
        layer.GetProperty("adds").EnumerateArray().Should().BeEmpty();
        layer.GetProperty("updates").EnumerateArray().Should().BeEmpty();
        layer.GetProperty("deletes").EnumerateArray().Should().BeEmpty();
        layer.GetProperty("exceededTransferLimit").GetBoolean().Should().BeFalse();

        root.GetProperty("revisionInfo").GetProperty("lastChange").GetInt64().Should().BeGreaterThan(0);
        root.GetProperty("latestTimestamp").GetInt64().Should().BeGreaterThan(0);
    }

    private async Task<JsonDocument> GetJsonAsync(string path)
    {
        var client = _fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync(path);
        if (!response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new System.Net.Http.HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase}). Body: {payload}");
        }
        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
    }
}
