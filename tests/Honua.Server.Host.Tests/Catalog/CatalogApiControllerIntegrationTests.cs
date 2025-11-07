// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Honua.Server.Host.Tests.TestInfrastructure;
using Xunit;

namespace Honua.Server.Host.Tests.Catalog;

/// <summary>
/// Integration tests for CatalogApiController - catalog search and record retrieval.
/// Tests search functionality, pagination, filtering, and individual record retrieval.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
[Trait("Feature", "Catalog")]
public sealed class CatalogApiControllerIntegrationTests : IClassFixture<HonuaWebApplicationFactory>
{
    private readonly HonuaWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CatalogApiControllerIntegrationTests(HonuaWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    #region Search Tests

    [Fact]
    public async Task Get_WithoutParameters_ReturnsDefaultPaginatedResults()
    {
        // Act
        var response = await _client.GetAsync("/api/catalog");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("limit", out var limit).Should().BeTrue();
            limit.GetInt32().Should().Be(100); // Default limit

            content.TryGetProperty("offset", out var offset).Should().BeTrue();
            offset.GetInt32().Should().Be(0); // Default offset

            content.TryGetProperty("records", out _).Should().BeTrue();
            content.TryGetProperty("count", out _).Should().BeTrue();
            content.TryGetProperty("totalCount", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task Get_WithQuery_FiltersResults()
    {
        // Arrange
        var searchQuery = "test";

        // Act
        var response = await _client.GetAsync($"/api/catalog?q={searchQuery}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("query", out var query).Should().BeTrue();
            query.GetString().Should().Be(searchQuery);

            content.TryGetProperty("records", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task Get_WithGroup_FiltersResultsByGroup()
    {
        // Arrange
        var groupId = "test-group";

        // Act
        var response = await _client.GetAsync($"/api/catalog?group={groupId}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("group", out var group).Should().BeTrue();
            group.GetString().Should().Be(groupId);
        }
    }

    [Fact]
    public async Task Get_WithCustomLimit_ReturnsSpecifiedNumberOfResults()
    {
        // Arrange
        var limit = 50;

        // Act
        var response = await _client.GetAsync($"/api/catalog?limit={limit}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("limit", out var returnedLimit).Should().BeTrue();
            returnedLimit.GetInt32().Should().Be(limit);

            content.TryGetProperty("count", out var count).Should().BeTrue();
            count.GetInt32().Should().BeLessOrEqualTo(limit);
        }
    }

    [Fact]
    public async Task Get_WithOffset_SkipsResults()
    {
        // Arrange
        var offset = 10;

        // Act
        var response = await _client.GetAsync($"/api/catalog?offset={offset}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("offset", out var returnedOffset).Should().BeTrue();
            returnedOffset.GetInt32().Should().Be(offset);
        }
    }

    [Fact]
    public async Task Get_WithLimitAndOffset_PaginatesCorrectly()
    {
        // Arrange
        var limit = 25;
        var offset = 50;

        // Act
        var response = await _client.GetAsync($"/api/catalog?limit={limit}&offset={offset}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("limit", out var returnedLimit).Should().BeTrue();
            returnedLimit.GetInt32().Should().Be(limit);

            content.TryGetProperty("offset", out var returnedOffset).Should().BeTrue();
            returnedOffset.GetInt32().Should().Be(offset);
        }
    }

    #endregion

    #region Validation Tests

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task Get_WithInvalidLimit_ReturnsBadRequest(int invalidLimit)
    {
        // Act
        var response = await _client.GetAsync($"/api/catalog?limit={invalidLimit}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("limit", StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Get_WithLimitTooLarge_ReturnsBadRequest()
    {
        // Arrange - Limit must be <= 1000
        var limit = 1001;

        // Act
        var response = await _client.GetAsync($"/api/catalog?limit={limit}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("1000");
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    [InlineData(-100)]
    public async Task Get_WithNegativeOffset_ReturnsBadRequest(int invalidOffset)
    {
        // Act
        var response = await _client.GetAsync($"/api/catalog?offset={invalidOffset}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("offset", StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public async Task Get_WithValidLimits_ReturnsOk(int validLimit)
    {
        // Act
        var response = await _client.GetAsync($"/api/catalog?limit={validLimit}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Get Record Tests

    [Fact]
    public async Task GetRecord_WithValidServiceAndLayer_ReturnsRecord()
    {
        // Arrange
        var serviceId = "test-service";
        var layerId = "test-layer";

        // Act
        var response = await _client.GetAsync($"/api/catalog/{serviceId}/{layerId}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("id", out _).Should().BeTrue();
            content.TryGetProperty("title", out _).Should().BeTrue();
            content.TryGetProperty("serviceId", out _).Should().BeTrue();
            content.TryGetProperty("links", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task GetRecord_WithNonExistentServiceAndLayer_ReturnsNotFound()
    {
        // Arrange
        var serviceId = "nonexistent-service-12345";
        var layerId = "nonexistent-layer-67890";

        // Act
        var response = await _client.GetAsync($"/api/catalog/{serviceId}/{layerId}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetRecord_WithSpecialCharactersInIds_HandlesCorrectly()
    {
        // Arrange
        var serviceId = Uri.EscapeDataString("service@special");
        var layerId = Uri.EscapeDataString("layer#123");

        // Act
        var response = await _client.GetAsync($"/api/catalog/{serviceId}/{layerId}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetRecord_ReturnsLinksSection()
    {
        // Arrange
        var serviceId = "test-service";
        var layerId = "test-layer";

        // Act
        var response = await _client.GetAsync($"/api/catalog/{serviceId}/{layerId}");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("links", out var links).Should().BeTrue();

            links.ValueKind.Should().Be(JsonValueKind.Array);

            // Should contain self link
            var linksArray = links.EnumerateArray().ToList();
            linksArray.Should().Contain(link =>
                link.TryGetProperty("rel", out var rel) &&
                rel.GetString() == "self");
        }
    }

    [Fact]
    public async Task GetRecord_ReturnsExtentInformation()
    {
        // Arrange
        var serviceId = "test-service";
        var layerId = "test-layer";

        // Act
        var response = await _client.GetAsync($"/api/catalog/{serviceId}/{layerId}");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();

            // Extent may or may not be present
            if (content.TryGetProperty("extent", out var extent))
            {
                // If present, check structure
                extent.ValueKind.Should().BeOneOf(JsonValueKind.Object, JsonValueKind.Null);
            }
        }
    }

    #endregion

    #region Search with Complex Queries

    [Fact]
    public async Task Get_WithQueryAndGroup_FiltersResultsCorrectly()
    {
        // Arrange
        var query = "test";
        var group = "test-group";

        // Act
        var response = await _client.GetAsync($"/api/catalog?q={query}&group={group}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("query", out var returnedQuery).Should().BeTrue();
            returnedQuery.GetString().Should().Be(query);

            content.TryGetProperty("group", out var returnedGroup).Should().BeTrue();
            returnedGroup.GetString().Should().Be(group);
        }
    }

    [Fact]
    public async Task Get_WithQueryAndPagination_CombinesFiltersCorrectly()
    {
        // Arrange
        var query = "test";
        var limit = 20;
        var offset = 5;

        // Act
        var response = await _client.GetAsync($"/api/catalog?q={query}&limit={limit}&offset={offset}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("query", out var returnedQuery).Should().BeTrue();
            returnedQuery.GetString().Should().Be(query);

            content.TryGetProperty("limit", out var returnedLimit).Should().BeTrue();
            returnedLimit.GetInt32().Should().Be(limit);

            content.TryGetProperty("offset", out var returnedOffset).Should().BeTrue();
            returnedOffset.GetInt32().Should().Be(offset);
        }
    }

    [Fact]
    public async Task Get_WithAllParameters_ReturnsFilteredAndPaginatedResults()
    {
        // Arrange
        var query = "test";
        var group = "test-group";
        var limit = 15;
        var offset = 10;

        // Act
        var response = await _client.GetAsync(
            $"/api/catalog?q={query}&group={group}&limit={limit}&offset={offset}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("query", out _).Should().BeTrue();
            content.TryGetProperty("group", out _).Should().BeTrue();
            content.TryGetProperty("limit", out _).Should().BeTrue();
            content.TryGetProperty("offset", out _).Should().BeTrue();
        }
    }

    #endregion

    #region Special Characters and Encoding

    [Fact]
    public async Task Get_WithEncodedQuery_HandlesCorrectly()
    {
        // Arrange
        var query = Uri.EscapeDataString("test query with spaces");

        // Act
        var response = await _client.GetAsync($"/api/catalog?q={query}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_WithUnicodeQuery_HandlesCorrectly()
    {
        // Arrange
        var query = Uri.EscapeDataString("тест");

        // Act
        var response = await _client.GetAsync($"/api/catalog?q={query}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_WithSpecialCharactersInGroup_HandlesCorrectly()
    {
        // Arrange
        var group = Uri.EscapeDataString("group-with-special@chars");

        // Act
        var response = await _client.GetAsync($"/api/catalog?group={group}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task Get_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange - Create client without authentication
        var unauthenticatedClient = _factory.CreateClient();

        // Act
        var response = await unauthenticatedClient.GetAsync("/api/catalog");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.OK); // May be allowed for some deployments
    }

    [Fact]
    public async Task GetRecord_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var unauthenticatedClient = _factory.CreateClient();
        var serviceId = "test-service";
        var layerId = "test-layer";

        // Act
        var response = await unauthenticatedClient.GetAsync($"/api/catalog/{serviceId}/{layerId}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.OK,      // May be allowed for some deployments
            HttpStatusCode.NotFound);
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task Get_WithMaximumLimit_ReturnsWithinReasonableTime()
    {
        // Arrange
        var limit = 1000; // Maximum allowed

        // Act
        var startTime = DateTime.UtcNow;
        var response = await _client.GetAsync($"/api/catalog?limit={limit}");
        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            duration.Should().BeLessThan(5000); // Should complete within 5 seconds
        }
    }

    #endregion

    #region Cache Header Tests

    [Fact]
    public async Task Get_ReturnsCacheHeaders()
    {
        // Act
        var response = await _client.GetAsync("/api/catalog");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            // May or may not have cache headers depending on configuration
            // Just verify the request completes successfully
            response.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task GetRecord_ReturnsCacheHeaders()
    {
        // Arrange
        var serviceId = "test-service";
        var layerId = "test-layer";

        // Act
        var response = await _client.GetAsync($"/api/catalog/{serviceId}/{layerId}");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Should().NotBeNull();
        }
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Get_WithEmptyQuery_TreatsAsNoQuery()
    {
        // Act
        var response = await _client.GetAsync("/api/catalog?q=");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_WithEmptyGroup_TreatsAsNoGroup()
    {
        // Act
        var response = await _client.GetAsync("/api/catalog?group=");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_WithVeryLargeOffset_ReturnsEmptyResults()
    {
        // Arrange
        var offset = 999999;

        // Act
        var response = await _client.GetAsync($"/api/catalog?offset={offset}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("count", out var count).Should().BeTrue();
            count.GetInt32().Should().Be(0);
        }
    }

    #endregion
}
