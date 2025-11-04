using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Raster;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Hosting;

/// <summary>
/// Integration tests for ArcGIS REST API MapServer endpoints.
/// Tests map rendering, layer queries, identify operations, and legend generation.
/// </summary>
[Collection("EndpointTests")]
[Trait("Category", "Integration")]
[Trait("Feature", "GeoservicesREST")]
[Trait("Server", "MapServer")]
public class GeoservicesMapServerTests : IClassFixture<GeoservicesLeafletFixture>
{
    private readonly GeoservicesLeafletFixture _fixture;

    public GeoservicesMapServerTests(GeoservicesLeafletFixture fixture)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
    }

    [Fact]
    public async Task MapServiceMetadata_ShouldExposeLayers()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/MapServer?f=json");
        var root = document.RootElement;
        root.GetProperty("layers").EnumerateArray().Should().HaveCount(2);
        root.GetProperty("serviceDescription").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("capabilities").GetString().Should().Contain("Map");
        root.GetProperty("supportedImageFormatTypes").GetString().Should().Be("PNG32,PNG24,PNG,JPG");
    }

    [Fact]
    public async Task ServicesDirectory_ShouldIncludeMapServerEntry()
    {
        using var document = await GetJsonAsync("/rest/services?f=json");
        var services = document.RootElement.GetProperty("services").EnumerateArray().ToArray();

        var mapServer = services.First(entry =>
        {
            var type = entry.GetProperty("type").GetString();
            var name = entry.GetProperty("name").GetString();
            return string.Equals(type, "MapServer", StringComparison.OrdinalIgnoreCase)
                && name != null
                && name.EndsWith("/roads", StringComparison.OrdinalIgnoreCase);
        });

        mapServer.TryGetProperty("url", out var mapUrl).Should().BeTrue();
        mapUrl.GetString().Should().EndWith("/rest/services/transportation/roads/MapServer");

        var imageServer = services.First(entry =>
        {
            var type = entry.GetProperty("type").GetString();
            var name = entry.GetProperty("name").GetString();
            return string.Equals(type, "ImageServer", StringComparison.OrdinalIgnoreCase)
                && name != null
                && name.EndsWith("/roads", StringComparison.OrdinalIgnoreCase);
        });

        imageServer.TryGetProperty("url", out var imageUrl).Should().BeTrue();
        imageUrl.GetString().Should().EndWith("/rest/services/transportation/roads/ImageServer");
    }

    [Fact]
    public async Task MapServiceLayerMetadata_ShouldExposeFields()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/MapServer/0?f=json");
        var root = document.RootElement;
        root.GetProperty("geometryType").GetString().Should().Be("esriGeometryPolyline");
        root.GetProperty("fields").EnumerateArray().Should().Contain(field =>
            field.GetProperty("name").GetString() == "road_id" && field.GetProperty("type").GetString() == "esriFieldTypeOID");
    }

    [Fact]
    public async Task MapServiceQuery_ShouldReturnFeatures()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/MapServer/0/query?f=json&where=1=1&outFields=road_id&returnGeometry=false");
        var features = document.RootElement.GetProperty("features").EnumerateArray().ToArray();
        features.Should().NotBeEmpty();
        features[0].GetProperty("attributes").GetProperty("road_id").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MapServiceLayers_ShouldReturnLayerCollection()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/MapServer/layers?f=json");
        var root = document.RootElement;
        var layers = root.GetProperty("layers").EnumerateArray().ToArray();
        layers.Should().HaveCount(2);

        var primary = layers.Single(layer => layer.GetProperty("id").GetString() == "0");
        primary.GetProperty("fields").EnumerateArray().Should().NotBeEmpty();

        var inspections = layers.Single(layer => layer.GetProperty("id").GetString() == "1");
        inspections.GetProperty("fields").EnumerateArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task MapServiceExport_ShouldReturnPngImage()
    {
        var registry = _fixture.Services.GetRequiredService<IRasterDatasetRegistry>();
        var datasets = await registry.GetByServiceAsync("roads");
        datasets.Should().NotBeEmpty("expected raster dataset to be registered for roads service");

        var client = _fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/rest/services/transportation/roads/MapServer/export?f=image&bbox=-122.6,45.5,-122.3,45.7&size=256,256&format=png");
        if (!response.IsSuccessStatusCode)
        {
            var errorPayload = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Export failed with status {(int)response.StatusCode}: {errorPayload}");
        }

        response.Content.Headers.ContentType?.MediaType.Should().Be("image/png");
        response.Content.Headers.ContentLength.Should().BeGreaterThan(0);

        using var stream = await response.Content.ReadAsStreamAsync();
        stream.Should().NotBeNull();
        stream.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MapServiceExport_Jpeg_ShouldReturnJpegImage()
    {
        var client = _fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/rest/services/transportation/roads/MapServer/export?f=image&bbox=-122.6,45.5,-122.3,45.7&size=256,256&format=jpg");
        response.EnsureSuccessStatusCode();

        response.Content.Headers.ContentType?.MediaType.Should().Be("image/jpeg");
        response.Content.Headers.ContentLength.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MapServiceExport_StyleId_ShouldAffectImage()
    {
        var client = _fixture.CreateAuthenticatedClient();

        async Task<byte[]> ExportAsync(string query)
        {
            var response = await client.GetAsync($"/rest/services/transportation/roads/MapServer/export?f=image&bbox=-122.6,45.5,-122.3,45.7&size=256,256{query}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }

        var defaultBytes = await ExportAsync("&format=png");
        var styledBytes = await ExportAsync("&format=png&styleId=infrared");

        styledBytes.Should().NotEqual(defaultBytes);
    }

    [Fact]
    public async Task MapServiceLegend_ShouldReturnEntries()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/MapServer/legend?f=json");
        var layers = document.RootElement.GetProperty("layers").EnumerateArray().ToArray();
        layers.Should().NotBeEmpty();

        var legend = layers[0].GetProperty("legend").EnumerateArray().ToArray();
        legend.Should().NotBeEmpty();

        var entry = legend[0];
        entry.GetProperty("imageData").GetString().Should().NotBeNullOrWhiteSpace();
        entry.GetProperty("contentType").GetString().Should().Be("image/png");
    }

    [Fact]
    public async Task MapServiceExport_InvalidStyle_ShouldReturnBadRequest()
    {
        var client = _fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/rest/services/transportation/roads/MapServer/export?f=image&bbox=-122.6,45.5,-122.3,45.7&size=256,256&format=png&styleId=unknown");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MapServiceIdentify_ShouldReturnResults()
    {
        var envelope = Uri.EscapeDataString("{\"xmin\":-122.60,\"ymin\":45.50,\"xmax\":-122.44,\"ymax\":45.58,\"spatialReference\":{\"wkid\":4326}}");
        var client = _fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync($"/rest/services/transportation/roads/MapServer/identify?f=json&geometry={envelope}&geometryType=esriGeometryEnvelope&layers=all:0");
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var results = document.RootElement.GetProperty("results").EnumerateArray().ToArray();
        results.Should().NotBeEmpty();
        var first = results.First();
        first.GetProperty("layerId").GetInt32().Should().Be(0);
        first.GetProperty("attributes").EnumerateObject().Should().Contain(p => p.Name == "road_id");
    }

    [Fact]
    public async Task MapServiceFind_ShouldReturnMatches()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/MapServer/find?f=json&searchText=Sunset&searchFields=name&layers=0&returnGeometry=false");
        var results = document.RootElement.GetProperty("results").EnumerateArray().ToArray();
        results.Should().HaveCount(1);
        var first = results[0];
        first.GetProperty("layerId").GetInt32().Should().Be(0);
        first.GetProperty("foundFieldName").GetString().Should().Be("name");
        first.GetProperty("value").GetString().Should().ContainEquivalentOf("Sunset");
    }

    [Fact]
    public async Task MapServiceFind_VisibleLayers_ShouldRespectKeyword()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/MapServer/find?f=json&searchText=Sunset&searchFields=name&layers=visible&returnGeometry=false");
        var results = document.RootElement.GetProperty("results").EnumerateArray().ToArray();
        results.Should().NotBeEmpty();
        results.Should().Contain(result => result.GetProperty("layerId").GetInt32() == 0);
    }

    [Fact]
    public async Task MapServiceFind_TopLayers_ShouldReturnTopLayer()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/MapServer/find?f=json&searchText=Sunset&searchFields=name&layers=top&returnGeometry=false");
        var results = document.RootElement.GetProperty("results").EnumerateArray().ToArray();
        results.Should().NotBeEmpty();
        results.Select(result => result.GetProperty("layerId").GetInt32())
            .Should().OnlyContain(id => id == 1);
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
