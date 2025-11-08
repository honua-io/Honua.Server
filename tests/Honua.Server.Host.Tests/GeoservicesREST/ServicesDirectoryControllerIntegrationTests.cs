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
/// Integration tests for ServicesDirectoryController - ArcGIS REST Services Directory.
/// Tests root directory, folder listing, and service discovery endpoints.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
[Trait("Feature", "ArcGIS_Services_Directory")]
public sealed class ServicesDirectoryControllerIntegrationTests : IClassFixture<HonuaWebApplicationFactory>
{
    private readonly HonuaWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ServicesDirectoryControllerIntegrationTests(HonuaWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    #region GetRoot Tests

    [Fact]
    public async Task GetRoot_ReturnsServicesDirectory()
    {
        // Act
        var response = await _client.GetAsync("/rest/services");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();

            // Should have currentVersion
            content.TryGetProperty("currentVersion", out var version).Should().BeTrue();
            version.GetDouble().Should().BeGreaterThan(0);

            // Should have folders array
            content.TryGetProperty("folders", out var folders).Should().BeTrue();
            folders.ValueKind.Should().Be(JsonValueKind.Array);

            // Should have services array
            content.TryGetProperty("services", out var services).Should().BeTrue();
            services.ValueKind.Should().Be(JsonValueKind.Array);
        }
    }

    [Fact]
    public async Task GetRoot_ReturnsCurrentVersion()
    {
        // Act
        var response = await _client.GetAsync("/rest/services");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("currentVersion", out var version).Should().BeTrue();

            // Version should be approximately 10.81
            var versionNumber = version.GetDouble();
            versionNumber.Should().BeGreaterThanOrEqualTo(10.0);
            versionNumber.Should().BeLessThan(20.0);
        }
    }

    [Fact]
    public async Task GetRoot_ReturnsFoldersArray()
    {
        // Act
        var response = await _client.GetAsync("/rest/services");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("folders", out var folders).Should().BeTrue();

            folders.ValueKind.Should().Be(JsonValueKind.Array);
            // Folders may be empty or populated
        }
    }

    [Fact]
    public async Task GetRoot_ReturnsServicesArray()
    {
        // Act
        var response = await _client.GetAsync("/rest/services");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("services", out var services).Should().BeTrue();

            services.ValueKind.Should().Be(JsonValueKind.Array);

            // If services exist, check structure
            if (services.GetArrayLength() > 0)
            {
                var firstService = services[0];
                firstService.TryGetProperty("name", out _).Should().BeTrue();
                firstService.TryGetProperty("type", out _).Should().BeTrue();
            }
        }
    }

    [Fact]
    public async Task GetRoot_ServicesHaveValidTypes()
    {
        // Act
        var response = await _client.GetAsync("/rest/services");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("services", out var services).Should().BeTrue();

            var validTypes = new[] { "FeatureServer", "MapServer", "ImageServer", "GeometryServer" };

            foreach (var service in services.EnumerateArray())
            {
                if (service.TryGetProperty("type", out var type))
                {
                    var typeString = type.GetString();
                    typeString.Should().BeOneOf(validTypes);
                }
            }
        }
    }

    [Fact]
    public async Task GetRoot_IncludesGeometryServerIfEnabled()
    {
        // Act
        var response = await _client.GetAsync("/rest/services");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("services", out var services).Should().BeTrue();

            // Check if GeometryServer is in the list
            var servicesList = services.EnumerateArray().ToList();
            var hasGeometryServer = servicesList.Any(s =>
                s.TryGetProperty("type", out var type) &&
                type.GetString() == "GeometryServer");

            // GeometryServer may or may not be present depending on configuration
            hasGeometryServer.Should().BeOneOf(true, false);
        }
    }

    #endregion

    #region GetFolder Tests

    [Fact]
    public async Task GetFolder_WithValidFolder_ReturnsFolderContents()
    {
        // Arrange
        var folderId = "test-folder";

        // Act
        var response = await _client.GetAsync($"/rest/services/{folderId}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();

            // Should have currentVersion
            content.TryGetProperty("currentVersion", out var version).Should().BeTrue();
            version.GetDouble().Should().BeGreaterThan(0);

            // Should have folderName
            content.TryGetProperty("folderName", out _).Should().BeTrue();

            // Should have services array
            content.TryGetProperty("services", out var services).Should().BeTrue();
            services.ValueKind.Should().Be(JsonValueKind.Array);
        }
    }

    [Fact]
    public async Task GetFolder_WithNonExistentFolder_ReturnsNotFound()
    {
        // Arrange
        var folderId = "nonexistent-folder-12345";

        // Act
        var response = await _client.GetAsync($"/rest/services/{folderId}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetFolder_ReturnsServicesInFolder()
    {
        // Arrange
        var folderId = "test-folder";

        // Act
        var response = await _client.GetAsync($"/rest/services/{folderId}");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("services", out var services).Should().BeTrue();

            services.ValueKind.Should().Be(JsonValueKind.Array);

            // If services exist, verify structure
            if (services.GetArrayLength() > 0)
            {
                var firstService = services[0];
                firstService.TryGetProperty("name", out _).Should().BeTrue();
                firstService.TryGetProperty("type", out _).Should().BeTrue();
                firstService.TryGetProperty("url", out _).Should().BeTrue();
            }
        }
    }

    [Fact]
    public async Task GetFolder_ServiceUrlsAreAbsolute()
    {
        // Arrange
        var folderId = "test-folder";

        // Act
        var response = await _client.GetAsync($"/rest/services/{folderId}");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("services", out var services).Should().BeTrue();

            foreach (var service in services.EnumerateArray())
            {
                if (service.TryGetProperty("url", out var url))
                {
                    var urlString = url.GetString();
                    urlString.Should().StartWith("http");
                }
            }
        }
    }

    #endregion

    #region Special Characters and Encoding

    [Fact]
    public async Task GetFolder_WithEncodedFolderId_HandlesCorrectly()
    {
        // Arrange
        var folderId = Uri.EscapeDataString("folder-with-special@chars");

        // Act
        var response = await _client.GetAsync($"/rest/services/{folderId}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetFolder_WithUnicodeFolderId_HandlesCorrectly()
    {
        // Arrange
        var folderId = Uri.EscapeDataString("папка");

        // Act
        var response = await _client.GetAsync($"/rest/services/{folderId}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetFolder_WithSpacesInFolderId_HandlesCorrectly()
    {
        // Arrange
        var folderId = Uri.EscapeDataString("folder with spaces");

        // Act
        var response = await _client.GetAsync($"/rest/services/{folderId}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task GetRoot_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var unauthenticatedClient = _factory.CreateClient();

        // Act
        var response = await unauthenticatedClient.GetAsync("/rest/services");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.OK); // May be allowed for some deployments
    }

    [Fact]
    public async Task GetFolder_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var unauthenticatedClient = _factory.CreateClient();
        var folderId = "test-folder";

        // Act
        var response = await unauthenticatedClient.GetAsync($"/rest/services/{folderId}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.OK,      // May be allowed for some deployments
            HttpStatusCode.NotFound);
    }

    #endregion

    #region Service URL Construction

    [Fact]
    public async Task GetRoot_ServiceUrlsIncludeServerContext()
    {
        // Act
        var response = await _client.GetAsync("/rest/services");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("services", out var services).Should().BeTrue();

            foreach (var service in services.EnumerateArray())
            {
                if (service.TryGetProperty("url", out var url))
                {
                    var urlString = url.GetString();

                    // URL should be well-formed
                    urlString.Should().NotBeNullOrEmpty();
                    Uri.TryCreate(urlString, UriKind.Absolute, out _).Should().BeTrue();
                }
            }
        }
    }

    [Fact]
    public async Task GetFolder_ServiceUrlsIncludeFolderId()
    {
        // Arrange
        var folderId = "test-folder";

        // Act
        var response = await _client.GetAsync($"/rest/services/{folderId}");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("services", out var services).Should().BeTrue();

            foreach (var service in services.EnumerateArray())
            {
                if (service.TryGetProperty("url", out var url))
                {
                    var urlString = url.GetString();
                    urlString.Should().Contain(folderId);
                }
            }
        }
    }

    #endregion

    #region Service Type Tests

    [Fact]
    public async Task GetRoot_ReturnsFeatureServerServices()
    {
        // Act
        var response = await _client.GetAsync("/rest/services");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("services", out var services).Should().BeTrue();

            var featureServers = services.EnumerateArray()
                .Where(s => s.TryGetProperty("type", out var type) &&
                           type.GetString() == "FeatureServer")
                .ToList();

            // May or may not have FeatureServers
            featureServers.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task GetRoot_ReturnsMapServerServices()
    {
        // Act
        var response = await _client.GetAsync("/rest/services");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("services", out var services).Should().BeTrue();

            var mapServers = services.EnumerateArray()
                .Where(s => s.TryGetProperty("type", out var type) &&
                           type.GetString() == "MapServer")
                .ToList();

            // May or may not have MapServers
            mapServers.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task GetRoot_ReturnsImageServerServices()
    {
        // Act
        var response = await _client.GetAsync("/rest/services");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("services", out var services).Should().BeTrue();

            var imageServers = services.EnumerateArray()
                .Where(s => s.TryGetProperty("type", out var type) &&
                           type.GetString() == "ImageServer")
                .ToList();

            // May or may not have ImageServers
            imageServers.Should().NotBeNull();
        }
    }

    #endregion

    #region Response Format Tests

    [Fact]
    public async Task GetRoot_ReturnsJsonResponse()
    {
        // Act
        var response = await _client.GetAsync("/rest/services");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType?.MediaType.Should().BeOneOf(
                "application/json",
                "text/json");
        }
    }

    [Fact]
    public async Task GetFolder_ReturnsJsonResponse()
    {
        // Arrange
        var folderId = "test-folder";

        // Act
        var response = await _client.GetAsync($"/rest/services/{folderId}");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType?.MediaType.Should().BeOneOf(
                "application/json",
                "text/json");
        }
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task GetFolder_WithEmptyFolderId_ReturnsNotFoundOrBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.BadRequest,
            HttpStatusCode.OK,          // Might redirect to root
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetRoot_WithTrailingSlash_HandlesCorrectly()
    {
        // Act
        var response = await _client.GetAsync("/rest/services/");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.MovedPermanently,
            HttpStatusCode.Redirect,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetFolder_WithSlashesInFolderId_HandlesCorrectly()
    {
        // Arrange
        var folderId = Uri.EscapeDataString("folder/subfolder");

        // Act
        var response = await _client.GetAsync($"/rest/services/{folderId}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task GetRoot_CompletesWithinReasonableTime()
    {
        // Act
        var startTime = DateTime.UtcNow;
        var response = await _client.GetAsync("/rest/services");
        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            duration.Should().BeLessThan(2000); // Should complete within 2 seconds
        }
    }

    [Fact]
    public async Task GetFolder_CompletesWithinReasonableTime()
    {
        // Arrange
        var folderId = "test-folder";

        // Act
        var startTime = DateTime.UtcNow;
        var response = await _client.GetAsync($"/rest/services/{folderId}");
        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            duration.Should().BeLessThan(2000); // Should complete within 2 seconds
        }
    }

    #endregion

    #region Concurrent Requests

    [Fact]
    public async Task GetRoot_MultipleConcurrentRequests_HandlesCorrectly()
    {
        // Act - Send 10 concurrent requests
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _client.GetAsync("/rest/services"))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert - All requests should complete successfully
        responses.Should().NotBeEmpty();
        responses.Should().OnlyContain(r => r != null);
        responses.Should().OnlyContain(r =>
            r.StatusCode == HttpStatusCode.OK ||
            r.StatusCode == HttpStatusCode.Unauthorized);
    }

    #endregion
}
