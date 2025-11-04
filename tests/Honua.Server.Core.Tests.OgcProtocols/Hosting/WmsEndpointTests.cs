using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Hosting;

[Collection("EndpointTests")]
[Trait("Category", "Integration")]
public sealed class WmsEndpointTests : IClassFixture<HonuaWebApplicationFactory>
{
    private static readonly XNamespace Wms = "http://www.opengis.net/wms";
    private readonly HonuaWebApplicationFactory _factory;

    public WmsEndpointTests(HonuaWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetCapabilities_ShouldExposeRasterDataset()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/wms?service=WMS&request=GetCapabilities");

        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/xml");

        await using var contentStream = await response.Content.ReadAsStreamAsync();
        var document = XDocument.Load(contentStream);

        var layerNames = document
            .Descendants(Wms + "Layer")
            .Elements(Wms + "Name")
            .Select(element => element.Value)
            .ToArray();

        layerNames.Should().Contain("roads:roads-imagery");
    }

    [Fact]
    public async Task GetMap_ShouldReturnPngImage()
    {
        var client = _factory.CreateAuthenticatedClient();

        var url = "/wms?service=WMS&request=GetMap&layers=roads:roads-imagery&bbox=-122.6,45.5,-122.3,45.7&width=256&height=256&format=image/png&crs=EPSG:4326";
        var response = await client.GetAsync(url);

        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeNull();
        bytes.Length.Should().BeGreaterThan(0);
        bytes[0].Should().Be(0x89);
        bytes[1].Should().Be(0x50);
        bytes[2].Should().Be(0x4E);
        bytes[3].Should().Be(0x47);
    }

    [Fact]
    public async Task GetMap_ShouldSupportMultipleLayers()
    {
        var client = _factory.CreateAuthenticatedClient();

        const string url = "/wms?service=WMS&request=GetMap&layers=roads:roads-imagery,roads:roads-imagery-alt&styles=natural-color,infrared&bbox=-122.6,45.5,-122.3,45.7&width=256&height=256&format=image/png&transparent=true&crs=EPSG:4326";
        var response = await client.GetAsync(url);

        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeNull();
        bytes.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetMap_ShouldReturnServiceException_WhenLayersMissing()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/wms?service=WMS&request=GetMap&bbox=0,0,1,1&width=128&height=128");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.ogc.se_xml");

        var payload = await response.Content.ReadAsStringAsync();
        payload.Should().Contain("ServiceException");
    }

    [Fact]
    public async Task GetFeatureInfo_ShouldReturnJsonFeaturePayload()
    {
        var client = _factory.CreateAuthenticatedClient();

        const string url = "/wms?service=WMS&request=GetFeatureInfo&layers=roads:roads-imagery&query_layers=roads:roads-imagery&bbox=-122.6,45.5,-122.3,45.7&width=256&height=256&i=128&j=128&info_format=application/json&feature_count=2&crs=EPSG:4326";
        var response = await client.GetAsync(url);
        var payload = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"Expected success but received {(int)response.StatusCode}: {payload}");
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(payload));
        using var document = await JsonDocument.ParseAsync(stream);

        var root = document.RootElement;
        root.GetProperty("type").GetString().Should().Be("FeatureInfo");
        root.GetProperty("coordinate").GetProperty("crs").GetString().Should().Be("EPSG:4326");

        var features = root.GetProperty("features");
        features.ValueKind.Should().Be(JsonValueKind.Array);
        features.GetArrayLength().Should().BeGreaterThan(0);

        var feature = features[0];
        feature.ValueKind.Should().Be(JsonValueKind.Object);
        feature.EnumerateObject().Select(p => p.Name).Should().Contain("road_id");
    }

    [Fact]
    public async Task GetFeatureInfo_ShouldSupportPlainTextFormat()
    {
        var client = _factory.CreateAuthenticatedClient();

        const string url = "/wms?service=WMS&request=GetFeatureInfo&layers=roads:roads-imagery&query_layers=roads:roads-imagery&bbox=-122.6,45.5,-122.3,45.7&width=256&height=256&i=128&j=128&info_format=text/plain&crs=EPSG:4326";
        var response = await client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"Expected success but received {(int)response.StatusCode}: {content}");
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");

        content.Should().Contain("Dataset: roads-imagery");
        content.Should().Contain("Coordinate (EPSG:4326)");
    }

    [Fact]
    public async Task GetFeatureInfo_ShouldSupportGeoJsonFormat()
    {
        var client = _factory.CreateAuthenticatedClient();

        const string url = "/wms?service=WMS&request=GetFeatureInfo&layers=roads:roads-imagery&query_layers=roads:roads-imagery&bbox=-122.6,45.5,-122.3,45.7&width=256&height=256&i=128&j=128&info_format=application/geo+json&crs=EPSG:4326";
        var response = await client.GetAsync(url);
        var payload = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"Expected success but received {(int)response.StatusCode}: {payload}");
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/geo+json");

        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(payload));
        using var document = await JsonDocument.ParseAsync(stream);

        var root = document.RootElement;
        root.GetProperty("type").GetString().Should().Be("FeatureCollection");
        root.GetProperty("coordinate").GetProperty("crs").GetString().Should().Be("EPSG:4326");
    }

    [Fact]
    public async Task GetFeatureInfo_ShouldSupportXmlFormat()
    {
        var client = _factory.CreateAuthenticatedClient();

        const string url = "/wms?service=WMS&request=GetFeatureInfo&layers=roads:roads-imagery&query_layers=roads:roads-imagery&bbox=-122.6,45.5,-122.3,45.7&width=256&height=256&i=128&j=128&info_format=application/xml&crs=EPSG:4326";
        var response = await client.GetAsync(url);
        var payload = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"Expected success but received {(int)response.StatusCode}: {payload}");
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/xml");

        var document = XDocument.Parse(payload);
        document.Root!.Name.LocalName.Should().Be("FeatureInfo");
        document.Root.Attribute("dataset")!.Value.Should().Be("roads-imagery");
        document.Root.Element("Coordinate")!.Attribute("crs").Should().BeNull();
        document.Root.Attribute("crs")!.Value.Should().Be("EPSG:4326");
    }

    [Fact]
    public async Task GetFeatureInfo_ShouldSupportHtmlFormat()
    {
        var client = _factory.CreateAuthenticatedClient();

        const string url = "/wms?service=WMS&request=GetFeatureInfo&layers=roads:roads-imagery&query_layers=roads:roads-imagery&bbox=-122.6,45.5,-122.3,45.7&width=256&height=256&i=128&j=128&info_format=text/html&crs=EPSG:4326";
        var response = await client.GetAsync(url);
        var payload = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"Expected success but received {(int)response.StatusCode}: {payload}");
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");

        payload.Should().Contain("Feature Info");
        payload.Should().Contain("Dataset:");
        payload.Should().Contain("Coordinate (EPSG:4326)");
    }
}
