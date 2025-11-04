using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Tests.Shared;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Hosting;

[Collection("EndpointTests")]
[Trait("Category", "Integration")]
public class OgcEditingTests : IClassFixture<GeoservicesEditingFixture>
{
    private const string CollectionId = "roads::roads-primary";

    private readonly GeoservicesEditingFixture _fixture;

    public OgcEditingTests(GeoservicesEditingFixture fixture)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
    }

    [Fact]
    public async Task PostCollectionItems_ShouldCreateFeature()
    {
        _fixture.ResetRepository();
        var client = _fixture.CreateAuthenticatedClient();

        var payload = new JsonObject
        {
            ["type"] = "Feature",
            ["properties"] = new JsonObject
            {
                ["name"] = "SE Stark St",
                ["status"] = "planned"
            },
            ["geometry"] = new JsonObject
            {
                ["type"] = "LineString",
                ["coordinates"] = new JsonArray
                {
                    new JsonArray(-122.56, 45.51),
                    new JsonArray(-122.54, 45.52)
                }
            }
        };

        using var content = JsonContent.Create(payload, mediaType: new System.Net.Http.Headers.MediaTypeHeaderValue("application/geo+json"));
        var response = await client.PostAsync($"/ogc/collections/{CollectionId}/items", content);
        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created, responseBody);
        response.Headers.ETag.Should().NotBeNull();

        var repository = _fixture.GetRepository();
        repository.Features.Should().HaveCount(4);
    }

    [Fact]
    public async Task PutCollectionItem_ShouldReplaceProperties()
    {
        _fixture.ResetRepository();
        var client = _fixture.CreateAuthenticatedClient();

        var payload = new JsonObject
        {
            ["type"] = "Feature",
            ["properties"] = new JsonObject
            {
                ["road_id"] = 1,
                ["name"] = "Updated Name",
                ["status"] = "open"
            },
            ["geometry"] = new JsonObject
            {
                ["type"] = "LineString",
                ["coordinates"] = new JsonArray
                {
                    new JsonArray(-122.50, 45.50),
                    new JsonArray(-122.48, 45.51)
                }
            }
        };

        var etag = await GetEtagAsync(client, "1");

        using var content = JsonContent.Create(payload, mediaType: new System.Net.Http.Headers.MediaTypeHeaderValue("application/geo+json"));
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/geo+json");
        var request = new HttpRequestMessage(HttpMethod.Put, $"/ogc/collections/{CollectionId}/items/1")
        {
            Content = content
        };
        request.Headers.TryAddWithoutValidation(HeaderNames.IfMatch, etag);

        var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, responseBody);
        response.Headers.ETag.Should().NotBeNull();

        var repository = _fixture.GetRepository();
        repository.Features.Should().Contain(feature =>
            Convert.ToInt32(feature.Attributes["road_id"]) == 1 &&
            string.Equals(Convert.ToString(feature.Attributes["name"]), "Updated Name", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PatchCollectionItem_ShouldUpdateStatusOnly()
    {
        _fixture.ResetRepository();
        var client = _fixture.CreateAuthenticatedClient();

        var payload = new JsonObject
        {
            ["type"] = "Feature",
            ["properties"] = new JsonObject
            {
                ["road_id"] = 3,
                ["status"] = "closed"
            }
        };

        var etag = await GetEtagAsync(client, "3");

        using var content = JsonContent.Create(payload, mediaType: new System.Net.Http.Headers.MediaTypeHeaderValue("application/geo+json"));
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/ogc/collections/{CollectionId}/items/3")
        {
            Content = content
        };
        request.Headers.TryAddWithoutValidation(HeaderNames.IfMatch, etag);

        var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, responseBody);
        response.Headers.ETag.Should().NotBeNull();

        var repository = _fixture.GetRepository();
        repository.Features.Should().Contain(feature =>
            Convert.ToInt32(feature.Attributes["road_id"]) == 3 &&
            string.Equals(Convert.ToString(feature.Attributes["status"]), "closed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DeleteCollectionItem_ShouldRemoveFeature()
    {
        _fixture.ResetRepository();
        var client = _fixture.CreateAuthenticatedClient();

        var etag = await GetEtagAsync(client, "2");

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/ogc/collections/{CollectionId}/items/2");
        request.Headers.TryAddWithoutValidation(HeaderNames.IfMatch, etag);

        var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent, responseBody);

        var repository = _fixture.GetRepository();
        repository.Features.Should().NotContain(feature => Convert.ToInt32(feature.Attributes["road_id"]) == 2);
    }

    [Fact]
    public async Task PatchCollectionItem_InvalidIfMatch_ShouldReturnPreconditionFailed()
    {
        _fixture.ResetRepository();
        var client = _fixture.CreateAuthenticatedClient();

        var payload = new JsonObject
        {
            ["type"] = "Feature",
            ["properties"] = new JsonObject
            {
                ["road_id"] = 1,
                ["status"] = "planned"
            }
        };

        using var content = JsonContent.Create(payload, mediaType: new System.Net.Http.Headers.MediaTypeHeaderValue("application/geo+json"));
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/ogc/collections/{CollectionId}/items/1")
        {
            Content = content
        };
        request.Headers.TryAddWithoutValidation(HeaderNames.IfMatch, "\"invalid-etag\"");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.PreconditionFailed);
    }

    private static async Task<string> GetEtagAsync(HttpClient client, string featureId)
    {
        var response = await client.GetAsync($"/ogc/collections/{CollectionId}/items/{featureId}?f=json");
        response.EnsureSuccessStatusCode();
        return response.Headers.ETag?.Tag ?? response.Headers.ETag?.ToString() ?? throw new InvalidOperationException("Response missing ETag header.");
    }
}
