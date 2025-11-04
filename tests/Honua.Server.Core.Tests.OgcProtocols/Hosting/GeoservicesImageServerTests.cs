using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Hosting;

/// <summary>
/// Integration tests for ArcGIS REST API ImageServer endpoints.
/// Tests raster operations, image export, histograms, pixel sampling, and raster attributes.
/// </summary>
[Collection("EndpointTests")]
[Trait("Category", "Integration")]
[Trait("Feature", "GeoservicesREST")]
[Trait("Server", "ImageServer")]
public class GeoservicesImageServerTests : IClassFixture<GeoservicesLeafletFixture>
{
    private readonly GeoservicesLeafletFixture _fixture;

    public GeoservicesImageServerTests(GeoservicesLeafletFixture fixture)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
    }

    [Fact]
    public async Task ImageServiceMetadata_ShouldExposeDatasets()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/ImageServer?f=json");
        var root = document.RootElement;
        root.GetProperty("capabilities").GetString().Should().Contain("Image");
        var datasets = root.GetProperty("datasets").EnumerateArray().ToArray();
        datasets.Should().HaveCount(2);
        datasets.Select(dataset => dataset.GetProperty("id").GetString())
            .Should().BeEquivalentTo(new[] { "roads-imagery", "roads-imagery-alt" });
        datasets.Should().Contain(dataset =>
            dataset.GetProperty("id").GetString() == "roads-imagery-alt"
            && dataset.GetProperty("defaultStyleId").GetString() == "infrared");
    }

    [Fact]
    public async Task ImageServiceExport_ShouldReturnPngImage()
    {
        var client = _fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/rest/services/transportation/roads/ImageServer/exportImage?f=image&bbox=-122.6,45.5,-122.3,45.7&size=256,256&format=png");
        response.EnsureSuccessStatusCode();

        response.Content.Headers.ContentType?.MediaType.Should().Be("image/png");
        response.Content.Headers.ContentLength.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ImageServiceExport_InvalidStyle_ShouldReturnBadRequest()
    {
        var client = _fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/rest/services/transportation/roads/ImageServer/exportImage?f=image&bbox=-122.6,45.5,-122.3,45.7&size=256,256&format=png&styleId=unknown");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ImageServiceLegend_ShouldReturnEntries()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/ImageServer/legend?f=json");
        var layers = document.RootElement.GetProperty("layers").EnumerateArray().ToArray();
        layers.Should().HaveCount(2);
        foreach (var layer in layers)
        {
            var legend = layer.GetProperty("legend").EnumerateArray().ToArray();
            legend.Should().NotBeEmpty();
            legend[0].GetProperty("imageData").GetString().Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task ImageServiceGetSamples_ShouldReturnValues()
    {
        var geometry = Uri.EscapeDataString("{\"x\":-122.5,\"y\":45.55,\"spatialReference\":{\"wkid\":4326}}");
        using var document = await GetJsonAsync($"/rest/services/transportation/roads/ImageServer/getSamples?f=json&geometry={geometry}&geometryType=esriGeometryPoint&sr=4326");
        var samples = document.RootElement.GetProperty("samples").EnumerateArray().ToArray();
        samples.Should().NotBeEmpty();
        samples[0].GetProperty("value").EnumerateArray().Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task ImageServiceIdentify_ShouldReturnSample()
    {
        var geometry = Uri.EscapeDataString("{\"x\":-122.5,\"y\":45.55,\"spatialReference\":{\"wkid\":4326}}");
        using var document = await GetJsonAsync($"/rest/services/transportation/roads/ImageServer/identify?f=json&geometry={geometry}&geometryType=esriGeometryPoint&sr=4326");
        var items = document.RootElement.GetProperty("catalogItems").EnumerateArray().ToArray();
        items.Should().NotBeEmpty();
        items[0].GetProperty("sample").GetProperty("value").EnumerateArray().Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task ImageServiceComputeHistograms_ShouldReturnBands()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/ImageServer/computeHistograms?f=json&bbox=-122.6,45.5,-122.3,45.7&size=128,128");
        var histograms = document.RootElement.GetProperty("histograms").EnumerateArray().ToArray();
        histograms.Should().NotBeEmpty();
        var bands = histograms[0].GetProperty("bands").EnumerateArray().ToArray();
        bands.Should().HaveCountGreaterThan(0);
        bands[0].GetProperty("counts").EnumerateArray().Sum(element => element.GetInt32()).Should().BeGreaterThan(0);
        bands[0].TryGetProperty("description", out var bandDescription).Should().BeTrue();
        bandDescription.ValueKind.Should().BeOneOf(JsonValueKind.String, JsonValueKind.Null);
    }

    [Fact]
    public async Task ImageServiceComputeHistograms_WithBins_ShouldHonorRequest()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/ImageServer/computeHistograms?f=json&bins=16");
        var histograms = document.RootElement.GetProperty("histograms").EnumerateArray().ToArray();
        histograms.Should().NotBeEmpty();
        var counts = histograms[0].GetProperty("bands").EnumerateArray().First().GetProperty("counts").EnumerateArray().ToArray();
        counts.Should().HaveCount(16);
        histograms[0].GetProperty("bands").EnumerateArray().First().TryGetProperty("description", out var bandDescription).Should().BeTrue();
        bandDescription.ValueKind.Should().BeOneOf(JsonValueKind.String, JsonValueKind.Null);
    }

    [Fact]
    public async Task ImageServiceComputeHistograms_InvalidBins_ShouldReturnBadRequest()
    {
        var client = _fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/rest/services/transportation/roads/ImageServer/computeHistograms?f=json&bins=0");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ImageServiceGetRasterAttributes_ShouldReturnRows()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/ImageServer/getRasterAttributes?f=json");
        document.RootElement.TryGetProperty("fields", out var fieldsElement).Should().BeTrue(document.RootElement.ToString());
        var fields = fieldsElement.EnumerateArray().ToArray();
        fields.Should().Contain(field => field.GetProperty("name").GetString() == "Value");
        document.RootElement.TryGetProperty("features", out var featuresElement).Should().BeTrue(document.RootElement.ToString());
        var features = featuresElement.EnumerateArray().ToArray();
        features.Should().NotBeEmpty();
        var firstAttributes = features[0].GetProperty("attributes");
        firstAttributes.GetProperty("OBJECTID").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ImageServiceGetSamples_WithStyleAndTargetCrs_ShouldReflectRequest()
    {
        var geometry = Uri.EscapeDataString("{\"x\":-122.5,\"y\":45.55,\"spatialReference\":{\"wkid\":4326}}");
        using var document = await GetJsonAsync($"/rest/services/transportation/roads/ImageServer/getSamples?f=json&geometry={geometry}&geometryType=esriGeometryPoint&sr=3857&rasterId=roads-imagery-alt&styleId=infrared");
        var samples = document.RootElement.GetProperty("samples").EnumerateArray().ToArray();
        samples.Should().HaveCountGreaterThan(0);
        samples[0].GetProperty("location").GetProperty("spatialReference").GetProperty("wkid").GetInt32()
            .Should().Be(3857);
    }

    [Fact]
    public async Task ImageServiceGetSamples_InvalidStyle_ShouldReturnBadRequest()
    {
        var geometry = Uri.EscapeDataString("{\"x\":-122.5,\"y\":45.55,\"spatialReference\":{\"wkid\":4326}}");
        var client = _fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync($"/rest/services/transportation/roads/ImageServer/getSamples?f=json&geometry={geometry}&geometryType=esriGeometryPoint&styleId=unknown");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ImageServiceGetRasterInfo_ShouldReturnBandStatistics()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/ImageServer/getRasterInfo?f=json");
        document.RootElement.GetProperty("datasetId").GetString().Should().Be("roads-imagery");
        document.RootElement.GetProperty("bandCount").GetInt32().Should().BeGreaterThan(0);
        document.RootElement.GetProperty("pixelSizeX").GetDouble().Should().BeGreaterThan(0);
        Math.Abs(document.RootElement.GetProperty("pixelSizeY").GetDouble()).Should().BeGreaterThan(0);
        var spatialReference = document.RootElement.GetProperty("spatialReference");
        spatialReference.GetProperty("wkid").GetInt32().Should().BeGreaterThan(0);
        var extent = document.RootElement.GetProperty("extent");
        extent.GetProperty("xmin").GetDouble().Should().BeLessThan(extent.GetProperty("xmax").GetDouble());
        var bands = document.RootElement.GetProperty("bands").EnumerateArray().ToArray();
        bands.Should().NotBeEmpty();
        bands[0].GetProperty("minimum").GetDouble().Should().BeGreaterThanOrEqualTo(0);
        bands[0].TryGetProperty("noDataValue", out var noDataElement).Should().BeTrue();
        noDataElement.ValueKind.Should().BeOneOf(JsonValueKind.Number, JsonValueKind.Null);
        bands[0].TryGetProperty("description", out var bandDescription).Should().BeTrue();
        bandDescription.ValueKind.Should().BeOneOf(JsonValueKind.String, JsonValueKind.Null);
    }

    [Fact]
    public async Task ImageServiceGetPixelHistograms_ShouldReturnCounts()
    {
        var polygon = Uri.EscapeDataString("{\"rings\":[[[-122.60,45.50],[-122.30,45.50],[-122.30,45.70],[-122.60,45.70],[-122.60,45.50]]],\"spatialReference\":{\"wkid\":4326}}");
        using var document = await GetJsonAsync($"/rest/services/transportation/roads/ImageServer/getPixelHistograms?f=json&geometry={polygon}&geometryType=esriGeometryPolygon&bins=32&size=96,96");
        var histograms = document.RootElement.GetProperty("histograms").EnumerateArray().ToArray();
        histograms.Should().NotBeEmpty();
        var counts = histograms[0].GetProperty("bands").EnumerateArray().First().GetProperty("counts").EnumerateArray().ToArray();
        counts.Should().HaveCount(32);
        counts.Sum(element => element.GetInt32()).Should().BeGreaterThan(0);
        histograms[0].GetProperty("bands").EnumerateArray().First().TryGetProperty("description", out var windowDescription).Should().BeTrue();
        windowDescription.ValueKind.Should().BeOneOf(JsonValueKind.String, JsonValueKind.Null);
    }

    [Fact]
    public async Task ImageServiceMetadata_ShouldExposeRasterInfo()
    {
        using var document = await GetJsonAsync("/rest/services/transportation/roads/ImageServer?f=json");
        var root = document.RootElement;

        root.GetProperty("supportedImageFormatTypes").GetString()
            .Should().Contain("PNG");

        root.GetProperty("rasters").EnumerateArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task ImageServiceExport_ShouldReturnImage()
    {
        var client = _fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/rest/services/transportation/roads/ImageServer/exportImage?f=image&bbox=-122.6,45.5,-122.3,45.7&size=256,256&format=png");
        response.EnsureSuccessStatusCode();

        response.Content.Headers.ContentType?.MediaType.Should().Be("image/png");
        response.Content.Headers.ContentLength.Should().BeGreaterThan(0);
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
