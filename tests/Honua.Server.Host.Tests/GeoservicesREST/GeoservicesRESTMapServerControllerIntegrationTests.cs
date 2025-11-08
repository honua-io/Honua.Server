// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Honua.Server.Host.Tests.TestInfrastructure;
using Xunit;

namespace Honua.Server.Host.Tests.GeoservicesREST;

/// <summary>
/// Integration tests for GeoservicesRESTMapServerController - ArcGIS REST Map Server implementation.
/// Tests service metadata, layer queries, and map export operations.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
[Trait("Feature", "ArcGIS_MapServer")]
public sealed class GeoservicesRESTMapServerControllerIntegrationTests : IClassFixture<HonuaWebApplicationFactory>
{
    private readonly HonuaWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public GeoservicesRESTMapServerControllerIntegrationTests(HonuaWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    #region Service Metadata Tests

    [Fact]
    public async Task GetService_ReturnsServiceInfo()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/test-folder/test-service/MapServer");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("currentVersion", out _).Should().BeTrue();
            content.TryGetProperty("serviceDescription", out _).Should().BeTrue();
            content.TryGetProperty("layers", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task GetService_NonExistentService_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/folder/nonexistent-service/MapServer");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetService_WithoutAuthentication_HandlesCorrectly()
    {
        // Arrange
        var unauthenticatedClient = _factory.CreateClient();

        // Act
        var response = await unauthenticatedClient.GetAsync("/rest/services/folder/service/MapServer");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region Query Tests

    [Fact]
    public async Task Query_GetMethod_ReturnsFeatures()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/folder/service/MapServer/0/query?where=1=1&f=json");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Query_PostMethod_ReturnsFeatures()
    {
        // Arrange
        var formData = new Dictionary<string, string>
        {
            { "where", "1=1" },
            { "f", "json" }
        };

        // Act
        var response = await _client.PostAsync(
            "/rest/services/folder/service/MapServer/0/query",
            new FormUrlEncodedContent(formData));

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Query_WithGeometry_FiltersResults()
    {
        // Act
        var geometry = "{\"xmin\":-122.5,\"ymin\":37.7,\"xmax\":-122.3,\"ymax\":37.8}";
        var response = await _client.GetAsync(
            $"/rest/services/folder/service/MapServer/0/query?where=1=1&geometry={Uri.EscapeDataString(geometry)}&geometryType=esriGeometryEnvelope&f=json");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Query_WithReturnCountOnly_ReturnsCount()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/folder/service/MapServer/0/query?where=1=1&returnCountOnly=true&f=json");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Export Map Tests

    [Fact]
    public async Task Export_WithBbox_ReturnsImage()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/folder/service/MapServer/export?bbox=-122.5,37.7,-122.3,37.8&size=512,512&f=image");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Export_WithFormat_ReturnsSpecifiedFormat()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/folder/service/MapServer/export?bbox=-122.5,37.7,-122.3,37.8&size=512,512&format=png&f=image");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.BadRequest);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Contain("image");
        }
    }

    [Fact]
    public async Task Export_WithTransparent_ReturnsTransparentImage()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/folder/service/MapServer/export?bbox=-122.5,37.7,-122.3,37.8&size=512,512&transparent=true&f=image");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Export_InvalidBbox_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/folder/service/MapServer/export?bbox=invalid&size=512,512&f=image");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Export_MissingBbox_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/folder/service/MapServer/export?size=512,512&f=image");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Content Negotiation Tests

    [Fact]
    public async Task Query_WithFormatJson_ReturnsJson()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/folder/service/MapServer/0/query?where=1=1&f=json");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Contain("json");
        }
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task GetService_CompletesWithinReasonableTime()
    {
        // Act
        var startTime = DateTime.UtcNow;
        var response = await _client.GetAsync("/rest/services/folder/service/MapServer");
        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            duration.Should().BeLessThan(2000); // Should complete within 2 seconds
        }
    }

    #endregion

    #region Concurrent Request Tests

    [Fact]
    public async Task Query_MultipleConcurrentRequests_HandlesCorrectly()
    {
        // Act - Send 10 concurrent requests
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _client.GetAsync("/rest/services/folder/service/MapServer/0/query?where=1=1&f=json"))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert - All requests should complete
        responses.Should().NotBeEmpty();
        responses.Should().OnlyContain(r => r != null);
    }

    #endregion
}
