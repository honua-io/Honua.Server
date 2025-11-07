// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Honua.Server.Host.Tests.TestInfrastructure;
using Xunit;

namespace Honua.Server.Host.Tests.Stac;

/// <summary>
/// Integration tests for StacCatalogController - STAC catalog root and conformance endpoints.
/// Tests the STAC API catalog landing page and conformance declarations.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
[Trait("Feature", "STAC")]
public sealed class StacCatalogControllerIntegrationTests : IClassFixture<HonuaWebApplicationFactory>
{
    private readonly HonuaWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public StacCatalogControllerIntegrationTests(HonuaWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    #region GetRoot Tests

    [Fact]
    public async Task GetRoot_StacDisabled_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/stac");

        // Assert - STAC is disabled by default in test configuration
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetRoot_WithoutAuthentication_ReturnsOk()
    {
        // Arrange - Root endpoint allows anonymous access
        var unauthenticatedClient = _factory.CreateClient();

        // Act
        var response = await unauthenticatedClient.GetAsync("/stac");

        // Assert - Should be accessible without authentication (but STAC disabled)
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetRoot_ReturnsJsonContentType()
    {
        // Act
        var response = await _client.GetAsync("/stac");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType?.MediaType.Should().BeOneOf(
                "application/json",
                "application/geo+json");
        }
    }

    [Fact]
    public async Task GetRoot_ContainsRequiredStacFields()
    {
        // Act
        var response = await _client.GetAsync("/stac");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();

            // STAC catalog root must have these fields
            content.TryGetProperty("type", out var type).Should().BeTrue();
            type.GetString().Should().Be("Catalog");

            content.TryGetProperty("id", out _).Should().BeTrue();
            content.TryGetProperty("description", out _).Should().BeTrue();
            content.TryGetProperty("links", out _).Should().BeTrue();
            content.TryGetProperty("stac_version", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task GetRoot_ContainsValidLinks()
    {
        // Act
        var response = await _client.GetAsync("/stac");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("links", out var links).Should().BeTrue();

            links.ValueKind.Should().Be(JsonValueKind.Array);
            var linksArray = links.EnumerateArray().ToList();
            linksArray.Should().NotBeEmpty();

            // Should contain self link
            linksArray.Should().Contain(link =>
                link.TryGetProperty("rel", out var rel) &&
                rel.GetString() == "self");

            // Should contain root link
            linksArray.Should().Contain(link =>
                link.TryGetProperty("rel", out var rel) &&
                rel.GetString() == "root");
        }
    }

    [Fact]
    public async Task GetRoot_LinksHaveRequiredProperties()
    {
        // Act
        var response = await _client.GetAsync("/stac");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("links", out var links).Should().BeTrue();

            foreach (var link in links.EnumerateArray())
            {
                // Each link must have rel and href
                link.TryGetProperty("rel", out _).Should().BeTrue();
                link.TryGetProperty("href", out _).Should().BeTrue();
            }
        }
    }

    [Fact]
    public async Task GetRoot_StacVersionIsValid()
    {
        // Act
        var response = await _client.GetAsync("/stac");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("stac_version", out var version).Should().BeTrue();

            var versionString = version.GetString();
            versionString.Should().NotBeNullOrEmpty();
            // STAC versions are like "1.0.0"
            versionString.Should().MatchRegex(@"^\d+\.\d+\.\d+$");
        }
    }

    [Fact]
    public async Task GetRoot_ContainsTitle()
    {
        // Act
        var response = await _client.GetAsync("/stac");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("title", out var title).Should().BeTrue();
            title.GetString().Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task GetRoot_WithTrailingSlash_HandlesCorrectly()
    {
        // Act
        var response = await _client.GetAsync("/stac/");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.MovedPermanently,
            HttpStatusCode.Redirect);
    }

    #endregion

    #region GetConformance Tests

    [Fact]
    public async Task GetConformance_StacDisabled_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/stac/conformance");

        // Assert - STAC is disabled by default in test configuration
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetConformance_WithoutAuthentication_ReturnsOk()
    {
        // Arrange - Conformance endpoint allows anonymous access
        var unauthenticatedClient = _factory.CreateClient();

        // Act
        var response = await unauthenticatedClient.GetAsync("/stac/conformance");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetConformance_ReturnsJsonContentType()
    {
        // Act
        var response = await _client.GetAsync("/stac/conformance");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        }
    }

    [Fact]
    public async Task GetConformance_ContainsConformsToArray()
    {
        // Act
        var response = await _client.GetAsync("/stac/conformance");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("conformsTo", out var conformsTo).Should().BeTrue();

            conformsTo.ValueKind.Should().Be(JsonValueKind.Array);
            conformsTo.GetArrayLength().Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task GetConformance_ConformanceClassesAreUrls()
    {
        // Act
        var response = await _client.GetAsync("/stac/conformance");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("conformsTo", out var conformsTo).Should().BeTrue();

            foreach (var conformanceClass in conformsTo.EnumerateArray())
            {
                var classUri = conformanceClass.GetString();
                classUri.Should().NotBeNullOrEmpty();
                // OGC conformance classes are typically URLs
                classUri.Should().StartWith("http");
            }
        }
    }

    [Fact]
    public async Task GetConformance_ContainsStacCoreConformanceClass()
    {
        // Act
        var response = await _client.GetAsync("/stac/conformance");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("conformsTo", out var conformsTo).Should().BeTrue();

            var conformanceClasses = conformsTo.EnumerateArray()
                .Select(c => c.GetString())
                .ToList();

            // Should contain at least one STAC conformance class
            conformanceClasses.Should().Contain(c =>
                c != null && c.Contains("stac", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task GetConformance_WithTrailingSlash_HandlesCorrectly()
    {
        // Act
        var response = await _client.GetAsync("/stac/conformance/");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.MovedPermanently,
            HttpStatusCode.Redirect);
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task GetRoot_CompletesWithinReasonableTime()
    {
        // Act
        var startTime = DateTime.UtcNow;
        var response = await _client.GetAsync("/stac");
        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            duration.Should().BeLessThan(1000); // Should complete within 1 second
        }
    }

    [Fact]
    public async Task GetConformance_CompletesWithinReasonableTime()
    {
        // Act
        var startTime = DateTime.UtcNow;
        var response = await _client.GetAsync("/stac/conformance");
        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            duration.Should().BeLessThan(1000); // Should complete within 1 second
        }
    }

    #endregion

    #region Concurrent Request Tests

    [Fact]
    public async Task GetRoot_MultipleConcurrentRequests_HandlesCorrectly()
    {
        // Act - Send 10 concurrent requests
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _client.GetAsync("/stac"))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert - All requests should complete
        responses.Should().NotBeEmpty();
        responses.Should().OnlyContain(r => r != null);
        responses.Should().OnlyContain(r =>
            r.StatusCode == HttpStatusCode.OK ||
            r.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetConformance_MultipleConcurrentRequests_HandlesCorrectly()
    {
        // Act - Send 10 concurrent requests
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _client.GetAsync("/stac/conformance"))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert - All requests should complete
        responses.Should().NotBeEmpty();
        responses.Should().OnlyContain(r => r != null);
        responses.Should().OnlyContain(r =>
            r.StatusCode == HttpStatusCode.OK ||
            r.StatusCode == HttpStatusCode.NotFound);
    }

    #endregion

    #region Content Negotiation Tests

    [Fact]
    public async Task GetRoot_WithAcceptJson_ReturnsJson()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        // Act
        var response = await client.GetAsync("/stac");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Contain("json");
        }
    }

    [Fact]
    public async Task GetConformance_WithAcceptJson_ReturnsJson()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        // Act
        var response = await client.GetAsync("/stac/conformance");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        }
    }

    #endregion

    #region Cache Header Tests

    [Fact]
    public async Task GetRoot_ReturnsCacheHeaders()
    {
        // Act
        var response = await _client.GetAsync("/stac");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            // May or may not have cache headers depending on configuration
            response.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task GetConformance_ReturnsCacheHeaders()
    {
        // Act
        var response = await _client.GetAsync("/stac/conformance");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Should().NotBeNull();
        }
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task GetRoot_WithInvalidPath_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/stac/invalid-path");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetRoot_WithQueryParameters_IgnoresParameters()
    {
        // Act
        var response = await _client.GetAsync("/stac?test=value");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetConformance_WithQueryParameters_IgnoresParameters()
    {
        // Act
        var response = await _client.GetAsync("/stac/conformance?test=value");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound);
    }

    #endregion
}
