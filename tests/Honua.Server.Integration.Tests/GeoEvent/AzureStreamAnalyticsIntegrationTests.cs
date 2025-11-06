// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Enterprise.Events.Dto;
using Xunit;

namespace Honua.Server.Integration.Tests.GeoEvent;

/// <summary>
/// Integration tests for Azure Stream Analytics webhook endpoints
/// </summary>
public class AzureStreamAnalyticsIntegrationTests : IAsyncLifetime, IClassFixture<GeoEventTestFixture>
{
    private readonly GeoEventTestFixture _fixture;
    private readonly HttpClient _client;
    private Guid _testGeofenceId;

    public AzureStreamAnalyticsIntegrationTests(GeoEventTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    public async Task InitializeAsync()
    {
        // Create a test geofence for evaluation
        _testGeofenceId = await CreateTestGeofenceAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ReceiveWebhook_ValidBatch_ShouldProcess()
    {
        // Arrange
        var batch = new AzureStreamAnalyticsBatch
        {
            Events = new List<AzureStreamAnalyticsEvent>
            {
                new AzureStreamAnalyticsEvent
                {
                    EntityId = "device-001",
                    EntityType = "iot_device",
                    Longitude = -122.4,
                    Latitude = 37.8,
                    EventTime = DateTime.UtcNow,
                    Properties = new Dictionary<string, object>
                    {
                        ["temperature"] = 72.5,
                        ["battery"] = 85
                    }
                },
                new AzureStreamAnalyticsEvent
                {
                    EntityId = "device-002",
                    EntityType = "iot_device",
                    Longitude = -122.45,
                    Latitude = 37.85,
                    EventTime = DateTime.UtcNow,
                    Properties = new Dictionary<string, object>
                    {
                        ["temperature"] = 68.2,
                        ["battery"] = 92
                    }
                }
            },
            Metadata = new AzureStreamAnalyticsMetadata
            {
                JobName = "test-job",
                OutputName = "honua-geoevent"
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/azure-sa/webhook", batch);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<AzureStreamAnalyticsResponse>();
        result.Should().NotBeNull();
        result!.ProcessedCount.Should().Be(2);
        result.FailedCount.Should().Be(0);
        result.ProcessingTimeMs.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ReceiveWebhook_EmptyBatch_ShouldReturn400()
    {
        // Arrange
        var batch = new AzureStreamAnalyticsBatch
        {
            Events = new List<AzureStreamAnalyticsEvent>()
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/azure-sa/webhook", batch);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReceiveWebhook_NullEvents_ShouldReturn400()
    {
        // Arrange
        var batch = new AzureStreamAnalyticsBatch
        {
            Events = null
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/azure-sa/webhook", batch);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReceiveWebhook_TooManyEvents_ShouldReturn400()
    {
        // Arrange - Create > 1000 events
        var events = new List<AzureStreamAnalyticsEvent>();
        for (int i = 0; i < 1001; i++)
        {
            events.Add(new AzureStreamAnalyticsEvent
            {
                EntityId = $"device-{i}",
                Longitude = -122.4,
                Latitude = 37.8,
                EventTime = DateTime.UtcNow
            });
        }

        var batch = new AzureStreamAnalyticsBatch { Events = events };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/azure-sa/webhook", batch);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("1000");
    }

    [Fact]
    public async Task ReceiveWebhook_InvalidCoordinates_ShouldHandleGracefully()
    {
        // Arrange - Mix of valid and invalid coordinates
        var batch = new AzureStreamAnalyticsBatch
        {
            Events = new List<AzureStreamAnalyticsEvent>
            {
                new AzureStreamAnalyticsEvent
                {
                    EntityId = "device-valid",
                    Longitude = -122.4,
                    Latitude = 37.8,
                    EventTime = DateTime.UtcNow
                },
                new AzureStreamAnalyticsEvent
                {
                    EntityId = "device-invalid-lon",
                    Longitude = -200.0, // Invalid
                    Latitude = 37.8,
                    EventTime = DateTime.UtcNow
                },
                new AzureStreamAnalyticsEvent
                {
                    EntityId = "device-invalid-lat",
                    Longitude = -122.4,
                    Latitude = 100.0, // Invalid
                    EventTime = DateTime.UtcNow
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/azure-sa/webhook", batch);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<AzureStreamAnalyticsResponse>();
        result.Should().NotBeNull();
        result!.ProcessedCount.Should().Be(1); // Only valid one processed
        result.FailedCount.Should().Be(2);
        result.Errors.Should().NotBeNull();
        result.Errors.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task ReceiveWebhook_MissingEntityId_ShouldHandleGracefully()
    {
        // Arrange
        var batch = new AzureStreamAnalyticsBatch
        {
            Events = new List<AzureStreamAnalyticsEvent>
            {
                new AzureStreamAnalyticsEvent
                {
                    EntityId = null!, // Missing
                    Longitude = -122.4,
                    Latitude = 37.8,
                    EventTime = DateTime.UtcNow
                },
                new AzureStreamAnalyticsEvent
                {
                    EntityId = "device-valid",
                    Longitude = -122.4,
                    Latitude = 37.8,
                    EventTime = DateTime.UtcNow
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/azure-sa/webhook", batch);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<AzureStreamAnalyticsResponse>();
        result.Should().NotBeNull();
        result!.ProcessedCount.Should().Be(1);
        result.FailedCount.Should().Be(1);
        result.Errors.Should().Contain(e => e.Contains("entity_id"));
    }

    [Fact]
    public async Task ReceiveWebhook_InsideGeofence_ShouldGenerateEvents()
    {
        // Arrange - Event inside our test geofence
        var batch = new AzureStreamAnalyticsBatch
        {
            Events = new List<AzureStreamAnalyticsEvent>
            {
                new AzureStreamAnalyticsEvent
                {
                    EntityId = "device-inside",
                    EntityType = "sensor",
                    Longitude = -122.4,
                    Latitude = 37.8,
                    EventTime = DateTime.UtcNow,
                    Properties = new Dictionary<string, object>
                    {
                        ["temp"] = 25.5
                    }
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/azure-sa/webhook", batch);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<AzureStreamAnalyticsResponse>();
        result.Should().NotBeNull();
        result!.ProcessedCount.Should().Be(1);
        result.EventsGeneratedCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ReceiveWebhook_LargeBatch_ShouldProcessEfficiently()
    {
        // Arrange - Create batch of 500 events
        var events = new List<AzureStreamAnalyticsEvent>();
        for (int i = 0; i < 500; i++)
        {
            events.Add(new AzureStreamAnalyticsEvent
            {
                EntityId = $"device-{i}",
                EntityType = "iot_device",
                Longitude = -122.4 + (i * 0.001),
                Latitude = 37.8 + (i * 0.001),
                EventTime = DateTime.UtcNow
            });
        }

        var batch = new AzureStreamAnalyticsBatch { Events = events };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/azure-sa/webhook", batch);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<AzureStreamAnalyticsResponse>();
        result.Should().NotBeNull();
        result!.ProcessedCount.Should().Be(500);
        result.FailedCount.Should().Be(0);
        result.ProcessingTimeMs.Should().BeLessThan(5000); // Should process in under 5 seconds
    }

    [Fact]
    public async Task ReceiveSingleEvent_ValidEvent_ShouldProcess()
    {
        // Arrange
        var asaEvent = new AzureStreamAnalyticsEvent
        {
            EntityId = "device-single",
            EntityType = "sensor",
            Longitude = -122.4,
            Latitude = 37.8,
            EventTime = DateTime.UtcNow,
            Properties = new Dictionary<string, object>
            {
                ["humidity"] = 65.0
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/azure-sa/webhook/single", asaEvent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<EvaluateLocationResponse>();
        result.Should().NotBeNull();
        result!.EntityId.Should().Be("device-single");
        result.ProcessingTimeMs.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ReceiveSingleEvent_NullEvent_ShouldReturn400()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/azure-sa/webhook/single", (AzureStreamAnalyticsEvent)null!);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReceiveSingleEvent_MissingEntityId_ShouldReturn400()
    {
        // Arrange
        var asaEvent = new AzureStreamAnalyticsEvent
        {
            EntityId = null!,
            Longitude = -122.4,
            Latitude = 37.8
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/azure-sa/webhook/single", asaEvent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("entity_id");
    }

    [Fact]
    public async Task ReceiveSingleEvent_InvalidCoordinates_ShouldReturn400()
    {
        // Arrange
        var asaEvent = new AzureStreamAnalyticsEvent
        {
            EntityId = "device-test",
            Longitude = -200.0, // Invalid
            Latitude = 37.8
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/azure-sa/webhook/single", asaEvent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("coordinates");
    }

    [Fact]
    public async Task ReceiveSingleEvent_DefaultEventTime_ShouldUseCurrentTime()
    {
        // Arrange - No event_time provided
        var asaEvent = new AzureStreamAnalyticsEvent
        {
            EntityId = "device-no-time",
            Longitude = -122.4,
            Latitude = 37.8,
            EventTime = null // Should default to current time
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/azure-sa/webhook/single", asaEvent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<EvaluateLocationResponse>();
        result.Should().NotBeNull();
        result!.EventTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Helper methods

    private async Task<Guid> CreateTestGeofenceAsync()
    {
        var request = new CreateGeofenceRequest
        {
            Name = "ASA Test Geofence",
            Description = "Geofence for Azure Stream Analytics tests",
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
            IsActive = true
        };

        var response = await _client.PostAsJsonAsync("/api/v1/geofences", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GeofenceResponse>();
        return result!.Id;
    }
}
