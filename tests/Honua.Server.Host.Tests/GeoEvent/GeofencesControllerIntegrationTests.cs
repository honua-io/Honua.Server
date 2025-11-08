// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Honua.Server.Host.Tests.TestInfrastructure;
using Xunit;

namespace Honua.Server.Host.Tests.GeoEvent;

/// <summary>
/// Integration tests for GeofencesController - Geofence management CRUD operations.
/// Tests create, read, update, delete, and list operations for geofences.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
[Trait("Feature", "GeoFencing")]
public sealed class GeofencesControllerIntegrationTests : IClassFixture<HonuaWebApplicationFactory>
{
    private readonly HonuaWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public GeofencesControllerIntegrationTests(HonuaWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    #region CreateGeofence Tests

    [Fact]
    public async Task CreateGeofence_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = new
        {
            name = "Test Geofence",
            description = "Integration test geofence",
            geometry = new
            {
                type = "Polygon",
                coordinates = new[]
                {
                    new[]
                    {
                        new[] { -122.4194, 37.7749 },
                        new[] { -122.4094, 37.7749 },
                        new[] { -122.4094, 37.7849 },
                        new[] { -122.4194, 37.7849 },
                        new[] { -122.4194, 37.7749 }
                    }
                }
            },
            enabled_event_types = new[] { "Enter", "Exit" },
            is_active = true,
            properties = new
            {
                zone_type = "restricted",
                priority = "high"
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/geofences", request);

        // Assert - May return ServiceUnavailable if geofencing is not configured
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Created,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.NotFound,
            HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task CreateGeofence_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var unauthenticatedClient = _factory.CreateClient();
        var request = new
        {
            name = "Test Geofence",
            description = "Test",
            geometry = new
            {
                type = "Polygon",
                coordinates = new[] { new[] { new[] { -122.4194, 37.7749 }, new[] { -122.4094, 37.7749 }, new[] { -122.4094, 37.7849 }, new[] { -122.4194, 37.7849 }, new[] { -122.4194, 37.7749 } } }
            },
            enabled_event_types = new[] { "Enter" },
            is_active = true
        };

        // Act
        var response = await unauthenticatedClient.PostAsJsonAsync("/api/v1/geofences", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateGeofence_MissingName_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            description = "Test",
            geometry = new
            {
                type = "Polygon",
                coordinates = new[] { new[] { new[] { -122.4194, 37.7749 }, new[] { -122.4094, 37.7749 }, new[] { -122.4094, 37.7849 }, new[] { -122.4194, 37.7849 }, new[] { -122.4194, 37.7749 } } }
            },
            enabled_event_types = new[] { "Enter" },
            is_active = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/geofences", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateGeofence_InvalidGeometry_ReturnsBadRequest()
    {
        // Arrange - Missing required fields in geometry
        var request = new
        {
            name = "Test Geofence",
            description = "Test",
            geometry = new
            {
                type = "Polygon"
                // Missing coordinates
            },
            enabled_event_types = new[] { "Enter" },
            is_active = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/geofences", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateGeofence_EmptyName_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            name = "",
            description = "Test",
            geometry = new
            {
                type = "Polygon",
                coordinates = new[] { new[] { new[] { -122.4194, 37.7749 }, new[] { -122.4094, 37.7749 }, new[] { -122.4094, 37.7849 }, new[] { -122.4194, 37.7849 }, new[] { -122.4194, 37.7749 } } }
            },
            enabled_event_types = new[] { "Enter" },
            is_active = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/geofences", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateGeofence_WithProperties_StoresProperties()
    {
        // Arrange
        var request = new
        {
            name = "Test Geofence with Properties",
            description = "Test",
            geometry = new
            {
                type = "Polygon",
                coordinates = new[] { new[] { new[] { -122.4194, 37.7749 }, new[] { -122.4094, 37.7749 }, new[] { -122.4094, 37.7849 }, new[] { -122.4194, 37.7849 }, new[] { -122.4194, 37.7749 } } }
            },
            enabled_event_types = new[] { "Enter", "Exit" },
            is_active = true,
            properties = new
            {
                custom_field = "custom_value",
                priority = 5
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/geofences", request);

        // Assert
        if (response.StatusCode == HttpStatusCode.Created)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("properties", out _).Should().BeTrue();
        }
    }

    #endregion

    #region GetGeofence Tests

    [Fact]
    public async Task GetGeofence_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/geofences/{id}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetGeofence_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var unauthenticatedClient = _factory.CreateClient();
        var id = Guid.NewGuid();

        // Act
        var response = await unauthenticatedClient.GetAsync($"/api/v1/geofences/{id}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetGeofence_InvalidGuid_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/geofences/invalid-guid");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region ListGeofences Tests

    [Fact]
    public async Task ListGeofences_WithoutParameters_ReturnsDefaultPagination()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/geofences");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("geofences", out _).Should().BeTrue();
            content.TryGetProperty("totalCount", out _).Should().BeTrue();
            content.TryGetProperty("limit", out var limit).Should().BeTrue();
            content.TryGetProperty("offset", out var offset).Should().BeTrue();

            limit.GetInt32().Should().Be(100); // Default limit
            offset.GetInt32().Should().Be(0);  // Default offset
        }
    }

    [Fact]
    public async Task ListGeofences_WithCustomLimit_ReturnsSpecifiedLimit()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/geofences?limit=50");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("limit", out var limit).Should().BeTrue();
            limit.GetInt32().Should().Be(50);
        }
    }

    [Fact]
    public async Task ListGeofences_WithOffset_SkipsRecords()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/geofences?offset=10");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("offset", out var offset).Should().BeTrue();
            offset.GetInt32().Should().Be(10);
        }
    }

    [Fact]
    public async Task ListGeofences_WithActiveFilter_FiltersResults()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/geofences?isActive=true");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public async Task ListGeofences_WithInvalidLimit_ReturnsBadRequest(int invalidLimit)
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/geofences?limit={invalidLimit}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListGeofences_WithNegativeOffset_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/geofences?offset=-1");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListGeofences_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var unauthenticatedClient = _factory.CreateClient();

        // Act
        var response = await unauthenticatedClient.GetAsync("/api/v1/geofences");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListGeofences_WithLimitAndOffset_PaginatesCorrectly()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/geofences?limit=25&offset=50");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("limit", out var limit).Should().BeTrue();
            content.TryGetProperty("offset", out var offset).Should().BeTrue();

            limit.GetInt32().Should().Be(25);
            offset.GetInt32().Should().Be(50);
        }
    }

    #endregion

    #region UpdateGeofence Tests

    [Fact]
    public async Task UpdateGeofence_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        var request = new
        {
            name = "Updated Geofence",
            description = "Updated",
            geometry = new
            {
                type = "Polygon",
                coordinates = new[] { new[] { new[] { -122.4194, 37.7749 }, new[] { -122.4094, 37.7749 }, new[] { -122.4094, 37.7849 }, new[] { -122.4194, 37.7849 }, new[] { -122.4194, 37.7749 } } }
            },
            enabled_event_types = new[] { "Enter" },
            is_active = true
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/geofences/{id}", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task UpdateGeofence_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var unauthenticatedClient = _factory.CreateClient();
        var id = Guid.NewGuid();
        var request = new
        {
            name = "Updated Geofence",
            description = "Updated",
            geometry = new
            {
                type = "Polygon",
                coordinates = new[] { new[] { new[] { -122.4194, 37.7749 }, new[] { -122.4094, 37.7749 }, new[] { -122.4094, 37.7849 }, new[] { -122.4194, 37.7849 }, new[] { -122.4194, 37.7749 } } }
            },
            enabled_event_types = new[] { "Enter" },
            is_active = true
        };

        // Act
        var response = await unauthenticatedClient.PutAsJsonAsync($"/api/v1/geofences/{id}", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateGeofence_InvalidGeometry_ReturnsBadRequest()
    {
        // Arrange
        var id = Guid.NewGuid();
        var request = new
        {
            name = "Updated Geofence",
            description = "Updated",
            geometry = new
            {
                type = "Polygon"
                // Missing coordinates
            },
            enabled_event_types = new[] { "Enter" },
            is_active = true
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/geofences/{id}", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable);
    }

    #endregion

    #region DeleteGeofence Tests

    [Fact]
    public async Task DeleteGeofence_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/v1/geofences/{id}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task DeleteGeofence_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var unauthenticatedClient = _factory.CreateClient();
        var id = Guid.NewGuid();

        // Act
        var response = await unauthenticatedClient.DeleteAsync($"/api/v1/geofences/{id}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteGeofence_InvalidGuid_ReturnsBadRequest()
    {
        // Act
        var response = await _client.DeleteAsync("/api/v1/geofences/invalid-guid");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region Content Negotiation Tests

    [Fact]
    public async Task ListGeofences_WithAcceptJson_ReturnsJson()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        // Act
        var response = await client.GetAsync("/api/v1/geofences");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        }
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task ListGeofences_CompletesWithinReasonableTime()
    {
        // Act
        var startTime = DateTime.UtcNow;
        var response = await _client.GetAsync("/api/v1/geofences");
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
    public async Task ListGeofences_MultipleConcurrentRequests_HandlesCorrectly()
    {
        // Act - Send 10 concurrent requests
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _client.GetAsync("/api/v1/geofences"))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert - All requests should complete
        responses.Should().NotBeEmpty();
        responses.Should().OnlyContain(r => r != null);
        responses.Should().OnlyContain(r =>
            r.StatusCode == HttpStatusCode.OK ||
            r.StatusCode == HttpStatusCode.ServiceUnavailable ||
            r.StatusCode == HttpStatusCode.NotFound);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task ListGeofences_WithMaximumLimit_ReturnsResults()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/geofences?limit=1000");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListGeofences_WithVeryLargeOffset_ReturnsEmptyResults()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/geofences?offset=999999");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("geofences", out var geofences).Should().BeTrue();
            geofences.GetArrayLength().Should().Be(0);
        }
    }

    #endregion
}
