using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Hosting;

[Collection("EndpointTests")]
[Trait("Category", "Integration")]
public sealed class RecordsEndpointTests : IClassFixture<HonuaWebApplicationFactory>
{
    private readonly HonuaWebApplicationFactory _factory;

    public RecordsEndpointTests(HonuaWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Landing_ShouldReturnConformanceAndLinks()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/records");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var conformsTo = document.RootElement.GetProperty("conformsTo");
        conformsTo.EnumerateArray().Should().Contain(element => element.GetString() == "http://www.opengis.net/spec/ogcapi-records-1/1.0/conf/core");

        var links = document.RootElement.GetProperty("links");
        links.EnumerateArray().Should().Contain(link => link.GetProperty("rel").GetString() == "collections");
    }

    [Fact]
    public async Task Collections_ShouldReturnGroup()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/records/collections");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var collections = document.RootElement.GetProperty("collections");
        collections.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Items_ShouldHonorLimit()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/records/collections/transportation/items?limit=1");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("numberReturned").GetInt32().Should().Be(1);
        document.RootElement.GetProperty("items").EnumerateArray().Should().HaveCount(1);
        document.RootElement.GetProperty("links").EnumerateArray().Should().Contain(link => link.GetProperty("rel").GetString() == "self");
    }

    [Fact]
    public async Task Item_ShouldReturnSingleRecord()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/records/collections/transportation/items/roads%3Aroads-primary");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("id").GetString().Should().Be("roads:roads-primary");
        document.RootElement.GetProperty("type").GetString().Should().Be("Record");
    }

    [Fact]
    public async Task Items_ShouldReturnNotFound_ForMissingCollection()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/records/collections/unknown/items");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
