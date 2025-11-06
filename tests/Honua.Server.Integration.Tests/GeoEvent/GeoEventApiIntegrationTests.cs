// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using FluentAssertions;
using Honua.Server.Enterprise.Events.Dto;
using Npgsql;
using Xunit;

namespace Honua.Server.Integration.Tests.GeoEvent;

/// <summary>
/// Integration tests for GeoEvent API endpoints.
/// These tests exercise the full stack from HTTP request to database.
/// </summary>
public class GeoEventApiIntegrationTests : IAsyncLifetime, IClassFixture<GeoEventTestFixture>
{
    private readonly GeoEventTestFixture _fixture;
    private readonly HttpClient _client;

    public GeoEventApiIntegrationTests(GeoEventTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateGeofence_ValidRequest_ShouldReturn201()
    {
        // Arrange
        var request = new CreateGeofenceRequest
        {
            Name = "Test Geofence",
            Description = "Integration test geofence",
            Geometry = new GeoJsonGeometry
            {
                Type = "Polygon",
                Coordinates = new[]
                {
                    new[]
                    {
                        new[] { -122.5, 37.7 },
                        new[] { -122.3, 37.7 },
                        new[] { -122.3, 37.9 },
                        new[] { -122.5, 37.9 },
                        new[] { -122.5, 37.7 }
                    }
                }
            },
            EnabledEventTypes = new[] { "Enter", "Exit" },
            IsActive = true,
            Properties = new Dictionary<string, object>
            {
                ["zone_type"] = "restricted"
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/geofences", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var result = await response.Content.ReadFromJsonAsync<GeofenceResponse>();
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test Geofence");
        result.IsActive.Should().BeTrue();
        result.EnabledEventTypes.Should().Contain("Enter");
        result.EnabledEventTypes.Should().Contain("Exit");
    }

    [Fact]
    public async Task CreateGeofence_InvalidGeometry_ShouldReturn400()
    {
        // Arrange - Invalid polygon (not enough points)
        var request = new CreateGeofenceRequest
        {
            Name = "Invalid Geofence",
            Geometry = new GeoJsonGeometry
            {
                Type = "Polygon",
                Coordinates = new[]
                {
                    new[]
                    {
                        new[] { -122.5, 37.7 },
                        new[] { -122.3, 37.7 }
                    }
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/geofences", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetGeofence_Existing_ShouldReturn200()
    {
        // Arrange - Create a geofence first
        var geofenceId = await CreateTestGeofenceAsync("Test Get Geofence");

        // Act
        var response = await _client.GetAsync($"/api/v1/geofences/{geofenceId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GeofenceResponse>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(geofenceId);
        result.Name.Should().Be("Test Get Geofence");
    }

    [Fact]
    public async Task GetGeofence_NonExistent_ShouldReturn404()
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/geofences/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListGeofences_WithPagination_ShouldReturn200()
    {
        // Arrange - Create multiple geofences
        await CreateTestGeofenceAsync("Fence 1");
        await CreateTestGeofenceAsync("Fence 2");
        await CreateTestGeofenceAsync("Fence 3");

        // Act
        var response = await _client.GetAsync("/api/v1/geofences?limit=2&offset=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GeofenceListResponse>();
        result.Should().NotBeNull();
        result!.Geofences.Should().HaveCountLessOrEqualTo(2);
    }

    [Fact]
    public async Task UpdateGeofence_Existing_ShouldReturn204()
    {
        // Arrange
        var geofenceId = await CreateTestGeofenceAsync("Original Name");

        var updateRequest = new CreateGeofenceRequest
        {
            Name = "Updated Name",
            Description = "Updated description",
            Geometry = CreateValidGeoJsonPolygon(),
            IsActive = false
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/geofences/{geofenceId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify update
        var getResponse = await _client.GetAsync($"/api/v1/geofences/{geofenceId}");
        var updated = await getResponse.Content.ReadFromJsonAsync<GeofenceResponse>();
        updated!.Name.Should().Be("Updated Name");
        updated.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteGeofence_Existing_ShouldReturn204()
    {
        // Arrange
        var geofenceId = await CreateTestGeofenceAsync("To Be Deleted");

        // Act
        var response = await _client.DeleteAsync($"/api/v1/geofences/{geofenceId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deletion
        var getResponse = await _client.GetAsync($"/api/v1/geofences/{geofenceId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EvaluateLocation_InsideGeofence_ShouldGenerateEnterEvent()
    {
        // Arrange - Create a geofence
        var geofenceId = await CreateTestGeofenceAsync("Evaluate Test",
            centerLon: -122.4, centerLat: 37.8, size: 0.2);

        var request = new EvaluateLocationRequest
        {
            EntityId = "vehicle-123",
            EntityType = "vehicle",
            Location = new GeoJsonPoint
            {
                Coordinates = new[] { -122.4, 37.8 } // Inside the geofence
            },
            Properties = new Dictionary<string, object>
            {
                ["speed"] = 45.5,
                ["heading"] = 180
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/geoevent/evaluate", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<EvaluateLocationResponse>();
        result.Should().NotBeNull();
        result!.EntityId.Should().Be("vehicle-123");
        result.EventsGenerated.Should().HaveCount(1);
        result.EventsGenerated[0].EventType.Should().Be("Enter");
        result.EventsGenerated[0].GeofenceId.Should().Be(geofenceId);
        result.CurrentGeofences.Should().ContainSingle();
        result.ProcessingTimeMs.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task EvaluateLocation_OutsideGeofence_ShouldNotGenerateEvent()
    {
        // Arrange
        await CreateTestGeofenceAsync("Far Away", centerLon: -122.4, centerLat: 37.8, size: 0.1);

        var request = new EvaluateLocationRequest
        {
            EntityId = "vehicle-456",
            Location = new GeoJsonPoint
            {
                Coordinates = new[] { -121.0, 38.5 } // Far outside
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/geoevent/evaluate", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<EvaluateLocationResponse>();
        result!.EventsGenerated.Should().BeEmpty();
        result.CurrentGeofences.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateLocation_EntryThenExit_ShouldGenerateBothEvents()
    {
        // Arrange
        var geofenceId = await CreateTestGeofenceAsync("Entry Exit Test",
            centerLon: -122.5, centerLat: 37.9, size: 0.2);

        var entityId = "vehicle-789";

        // Act 1 - Enter geofence
        var enterRequest = new EvaluateLocationRequest
        {
            EntityId = entityId,
            Location = new GeoJsonPoint { Coordinates = new[] { -122.5, 37.9 } } // Inside
        };

        var enterResponse = await _client.PostAsJsonAsync("/api/v1/geoevent/evaluate", enterRequest);
        var enterResult = await enterResponse.Content.ReadFromJsonAsync<EvaluateLocationResponse>();

        // Assert enter
        enterResult!.EventsGenerated.Should().ContainSingle();
        enterResult.EventsGenerated[0].EventType.Should().Be("Enter");

        // Act 2 - Exit geofence
        var exitRequest = new EvaluateLocationRequest
        {
            EntityId = entityId,
            Location = new GeoJsonPoint { Coordinates = new[] { -121.0, 38.5 } } // Outside
        };

        var exitResponse = await _client.PostAsJsonAsync("/api/v1/geoevent/evaluate", exitRequest);
        var exitResult = await exitResponse.Content.ReadFromJsonAsync<EvaluateLocationResponse>();

        // Assert exit
        exitResult!.EventsGenerated.Should().ContainSingle();
        exitResult.EventsGenerated[0].EventType.Should().Be("Exit");
        exitResult.EventsGenerated[0].DwellTimeSeconds.Should().BeGreaterThan(0);
        exitResult.CurrentGeofences.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateLocation_InvalidCoordinates_ShouldReturn400()
    {
        // Arrange
        var request = new EvaluateLocationRequest
        {
            EntityId = "vehicle-999",
            Location = new GeoJsonPoint
            {
                Coordinates = new[] { -200.0, 100.0 } // Invalid lon/lat
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/geoevent/evaluate", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EvaluateBatch_MultipleLocations_ShouldReturn200()
    {
        // Arrange
        var geofenceId = await CreateTestGeofenceAsync("Batch Test",
            centerLon: -122.6, centerLat: 38.0, size: 0.3);

        var requests = new List<EvaluateLocationRequest>
        {
            new EvaluateLocationRequest
            {
                EntityId = "vehicle-1",
                Location = new GeoJsonPoint { Coordinates = new[] { -122.6, 38.0 } } // Inside
            },
            new EvaluateLocationRequest
            {
                EntityId = "vehicle-2",
                Location = new GeoJsonPoint { Coordinates = new[] { -122.6, 38.0 } } // Inside
            },
            new EvaluateLocationRequest
            {
                EntityId = "vehicle-3",
                Location = new GeoJsonPoint { Coordinates = new[] { -121.0, 39.0 } } // Outside
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/geoevent/evaluate/batch", requests);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<BatchEvaluateLocationResponse>();
        result.Should().NotBeNull();
        result!.TotalProcessed.Should().Be(3);
        result.SuccessCount.Should().Be(3);
        result.ErrorCount.Should().Be(0);
        result.Results.Should().HaveCount(3);
    }

    [Fact]
    public async Task EvaluateBatch_TooManyLocations_ShouldReturn400()
    {
        // Arrange - Create > 1000 requests (exceeds limit)
        var requests = new List<EvaluateLocationRequest>();
        for (int i = 0; i < 1001; i++)
        {
            requests.Add(new EvaluateLocationRequest
            {
                EntityId = $"entity-{i}",
                Location = new GeoJsonPoint { Coordinates = new[] { -122.0, 37.0 } }
            });
        }

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/geoevent/evaluate/batch", requests);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Helper methods

    private async Task<Guid> CreateTestGeofenceAsync(
        string name,
        double centerLon = -122.4,
        double centerLat = 37.8,
        double size = 0.2)
    {
        var request = new CreateGeofenceRequest
        {
            Name = name,
            Geometry = CreateSquareGeoJsonPolygon(centerLon, centerLat, size),
            IsActive = true
        };

        var response = await _client.PostAsJsonAsync("/api/v1/geofences", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GeofenceResponse>();
        return result!.Id;
    }

    private GeoJsonGeometry CreateValidGeoJsonPolygon()
    {
        return CreateSquareGeoJsonPolygon(-122.4, 37.8, 0.2);
    }

    private GeoJsonGeometry CreateSquareGeoJsonPolygon(
        double centerLon, double centerLat, double size)
    {
        var halfSize = size / 2;
        return new GeoJsonGeometry
        {
            Type = "Polygon",
            Coordinates = new[]
            {
                new[]
                {
                    new[] { centerLon - halfSize, centerLat - halfSize },
                    new[] { centerLon + halfSize, centerLat - halfSize },
                    new[] { centerLon + halfSize, centerLat + halfSize },
                    new[] { centerLon - halfSize, centerLat + halfSize },
                    new[] { centerLon - halfSize, centerLat - halfSize }
                }
            }
        };
    }
}
