using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Hosting;

[Collection("EndpointTests")]
[Trait("Category", "Integration")]
public class OgcTileJsonEndpointTests : IClassFixture<HonuaWebApplicationFactory>
{
    private readonly HonuaWebApplicationFactory _factory;

    public OgcTileJsonEndpointTests(HonuaWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RasterTileJson_ShouldBeAccessible()
    {
        var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/ogc/collections/roads::roads-primary/tiles/roads-imagery/tilejson");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        payload.Should().NotBeNull();
        payload!.RootElement.GetProperty("tilejson").GetString().Should().Be("3.0.0");
    }

    [Fact]
    public async Task VectorTileJson_ShouldBeAccessible()
    {
        var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/ogc/collections/roads::roads-primary/tiles/roads-primary-vector/tilejson");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        payload.Should().NotBeNull();
        payload!.RootElement.GetProperty("format").GetString().Should().Be("geojson");
    }

    [Fact]
    public async Task Collection_ShouldExposeTileJsonLinks()
    {
        var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/ogc/collections/roads::roads-primary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        payload.Should().NotBeNull();
        var links = payload!.RootElement.GetProperty("links").EnumerateArray().ToArray();
        links.Should().Contain(link => link.GetProperty("rel").GetString() == "tilejson");
    }
}
