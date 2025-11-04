using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Host;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Hosting;

[Collection("EndpointTests")]
[Trait("Category", "Integration")]
public sealed class CatalogEndpointTests : IClassFixture<HonuaWebApplicationFactory>
{
    private readonly HonuaWebApplicationFactory _factory;

    public CatalogEndpointTests(HonuaWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ApiCatalog_ShouldReturnDiscoveryRecords()
    {
        var client = await CreateClientWithFreshMetadataAsync();
        var response = await client.GetAsync("/api/catalog");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("count").GetInt32().Should().BeGreaterThan(0);

        var record = payload.GetProperty("records").EnumerateArray().First();
        record.GetProperty("id").GetString().Should().Be("roads:roads-primary");
        record.GetProperty("links").EnumerateArray().Should().Contain(l =>
            l.GetProperty("rel").GetString() == "self");
    }

    [Fact]
    public async Task ApiCatalog_Record_ShouldExposeExtent()
    {
        var client = await CreateClientWithFreshMetadataAsync();
        var record = await client.GetFromJsonAsync<JsonElement>("/api/catalog/roads/roads-primary");

        record.GetProperty("extent").GetProperty("spatial").GetProperty("bbox").EnumerateArray().Should().NotBeEmpty();
        record.GetProperty("links").EnumerateArray().Should().Contain(l =>
            l.GetProperty("rel").GetString() == "esri:service");
    }

    [Fact]
    public async Task GeoservicesRestDirectory_ShouldListFoldersAndServices()
    {
        var client = await CreateClientWithFreshMetadataAsync();
        var root = await client.GetFromJsonAsync<JsonElement>("/rest/services");

        root.GetProperty("folders").EnumerateArray().Should().Contain(f => f.GetString() == "transportation");
        root.GetProperty("services").EnumerateArray().Should().Contain(s =>
            s.GetProperty("type").GetString() == "FeatureServer");
        root.GetProperty("services").EnumerateArray().Should().Contain(s =>
            s.GetProperty("type").GetString() == "ImageServer");
    }

    [Fact]
    public async Task GeoservicesRestFeatureService_ShouldExposeLayers()
    {
        var client = await CreateClientWithFreshMetadataAsync();
        var json = await client.GetFromJsonAsync<JsonElement>("/rest/services/transportation/roads/FeatureServer");

        json.GetProperty("layers").EnumerateArray().Should().Contain(layer =>
            layer.GetProperty("name").GetString() == "Primary Roads" &&
            layer.GetProperty("geometryType").GetString() == "esriGeometryPolyline");
    }

    [Fact]
    public async Task GeoservicesRestImageServer_ShouldExposeDatasets()
    {
        var client = await CreateClientWithFreshMetadataAsync();
        var json = await client.GetFromJsonAsync<JsonElement>("/rest/services/transportation/roads/ImageServer");

        json.GetProperty("capabilities").GetString().Should().Contain("Image");
        json.GetProperty("datasets").EnumerateArray().Should().Contain(dataset =>
            dataset.GetProperty("id").GetString() == "roads-imagery");
    }

    [Fact]
    public async Task HtmlCatalog_ShouldRenderIndexPage()
    {
        var client = await CreateClientWithFreshMetadataAsync();
        var response = await client.GetAsync("/catalog");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Primary Roads");
    }

    [Fact]
    public async Task HtmlCatalog_Detail_ShouldRenderRecord()
    {
        var client = await CreateClientWithFreshMetadataAsync();
        var response = await client.GetAsync("/catalog/roads/roads-primary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Primary roads for routing");
    }

    private async Task<HttpClient> CreateClientWithFreshMetadataAsync()
    {
        File.WriteAllText(_factory.MetadataPath, HonuaWebApplicationFactory.SampleMetadata);

        using (var scope = _factory.Services.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<IMetadataRegistry>();
            await registry.ReloadAsync();
        }

        return _factory.CreateAuthenticatedClient();
    }

}
