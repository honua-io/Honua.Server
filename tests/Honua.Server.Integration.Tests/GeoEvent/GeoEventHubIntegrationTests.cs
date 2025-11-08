// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Enterprise.Events.Models;
using Honua.Server.Host.GeoEvent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using Xunit;

namespace Honua.Server.Integration.Tests.GeoEvent;

/// <summary>
/// Integration tests for GeoEvent SignalR Hub
/// Tests real-time event broadcasting and subscription management
/// </summary>
public class GeoEventHubIntegrationTests : IAsyncLifetime, IClassFixture<GeoEventTestFixture>
{
    private readonly GeoEventTestFixture _fixture;
    private HubConnection? _hubConnection;
    private readonly List<object> _receivedEvents = new();
    private readonly List<object> _receivedSubscriptions = new();

    public GeoEventHubIntegrationTests(GeoEventTestFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Create SignalR client connection
        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{_fixture.Server.BaseAddress}hubs/geoevent", options =>
            {
                options.HttpMessageHandlerFactory = _ => _fixture.Server.CreateHandler();
            })
            .Build();

        // Register event handlers
        _hubConnection.On<object>("GeofenceEvent", evt =>
        {
            _receivedEvents.Add(evt);
        });

        _hubConnection.On<object>("Subscribed", sub =>
        {
            _receivedSubscriptions.Add(sub);
        });

        await _hubConnection.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
            await _hubConnection.DisposeAsync();
        }
    }

    [Fact]
    public async Task Connection_ShouldEstablish_Successfully()
    {
        // Assert
        _hubConnection!.State.Should().Be(HubConnectionState.Connected);
    }

    [Fact]
    public async Task SubscribeToEntity_ShouldReceiveConfirmation()
    {
        // Arrange
        var entityId = "vehicle-123";

        // Act
        await _hubConnection!.InvokeAsync("SubscribeToEntity", entityId);
        await Task.Delay(100); // Give time for confirmation

        // Assert
        _receivedSubscriptions.Should().HaveCountGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task SubscribeToGeofence_ShouldReceiveConfirmation()
    {
        // Arrange
        var geofenceId = Guid.NewGuid().ToString();

        // Act
        await _hubConnection!.InvokeAsync("SubscribeToGeofence", geofenceId);
        await Task.Delay(100);

        // Assert
        _receivedSubscriptions.Should().HaveCountGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task BroadcastEvent_ToEntitySubscribers_ShouldReceiveEvent()
    {
        // Arrange
        var entityId = "vehicle-456";
        await _hubConnection!.InvokeAsync("SubscribeToEntity", entityId);
        await Task.Delay(100);

        _receivedEvents.Clear();

        // Get the hub context to broadcast an event
        var hubContext = _fixture.Services.GetRequiredService<IHubContext<GeoEventHub>>();
        var broadcaster = new SignalRGeoEventBroadcaster(
            hubContext,
            _fixture.Services.GetRequiredService<ILogger<SignalRGeoEventBroadcaster>>());

        var geofenceEvent = CreateTestEvent(entityId);
        var geofence = CreateTestGeofence();

        // Act
        await broadcaster.BroadcastEventAsync(geofenceEvent, geofence);
        await Task.Delay(200); // Give time for broadcast

        // Assert
        _receivedEvents.Should().HaveCountGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task BroadcastEvent_ToGeofenceSubscribers_ShouldReceiveEvent()
    {
        // Arrange
        var geofenceId = Guid.NewGuid();
        await _hubConnection!.InvokeAsync("SubscribeToGeofence", geofenceId.ToString());
        await Task.Delay(100);

        _receivedEvents.Clear();

        var hubContext = _fixture.Services.GetRequiredService<IHubContext<GeoEventHub>>();
        var broadcaster = new SignalRGeoEventBroadcaster(
            hubContext,
            _fixture.Services.GetRequiredService<ILogger<SignalRGeoEventBroadcaster>>());

        var geofenceEvent = CreateTestEvent("entity-789", geofenceId);
        var geofence = CreateTestGeofence(geofenceId);

        // Act
        await broadcaster.BroadcastEventAsync(geofenceEvent, geofence);
        await Task.Delay(200);

        // Assert
        _receivedEvents.Should().HaveCountGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task UnsubscribeFromEntity_ShouldNotReceiveFurtherEvents()
    {
        // Arrange
        var entityId = "vehicle-unsubscribe";
        await _hubConnection!.InvokeAsync("SubscribeToEntity", entityId);
        await Task.Delay(100);

        // Unsubscribe
        await _hubConnection.InvokeAsync("UnsubscribeFromEntity", entityId);
        await Task.Delay(100);

        _receivedEvents.Clear();

        var hubContext = _fixture.Services.GetRequiredService<IHubContext<GeoEventHub>>();
        var broadcaster = new SignalRGeoEventBroadcaster(
            hubContext,
            _fixture.Services.GetRequiredService<ILogger<SignalRGeoEventBroadcaster>>());

        var geofenceEvent = CreateTestEvent(entityId);
        var geofence = CreateTestGeofence();

        // Act
        await broadcaster.BroadcastEventAsync(geofenceEvent, geofence);
        await Task.Delay(200);

        // Assert
        _receivedEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task UnsubscribeFromGeofence_ShouldNotReceiveFurtherEvents()
    {
        // Arrange
        var geofenceId = Guid.NewGuid();
        await _hubConnection!.InvokeAsync("SubscribeToGeofence", geofenceId.ToString());
        await Task.Delay(100);

        // Unsubscribe
        await _hubConnection.InvokeAsync("UnsubscribeFromGeofence", geofenceId.ToString());
        await Task.Delay(100);

        _receivedEvents.Clear();

        var hubContext = _fixture.Services.GetRequiredService<IHubContext<GeoEventHub>>();
        var broadcaster = new SignalRGeoEventBroadcaster(
            hubContext,
            _fixture.Services.GetRequiredService<ILogger<SignalRGeoEventBroadcaster>>());

        var geofenceEvent = CreateTestEvent("entity-test", geofenceId);
        var geofence = CreateTestGeofence(geofenceId);

        // Act
        await broadcaster.BroadcastEventAsync(geofenceEvent, geofence);
        await Task.Delay(200);

        // Assert
        _receivedEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task BroadcastEventsAsync_MultipleBatch_ShouldReceiveAll()
    {
        // Arrange
        var entityId = "vehicle-batch";
        await _hubConnection!.InvokeAsync("SubscribeToEntity", entityId);
        await Task.Delay(100);

        _receivedEvents.Clear();

        var hubContext = _fixture.Services.GetRequiredService<IHubContext<GeoEventHub>>();
        var broadcaster = new SignalRGeoEventBroadcaster(
            hubContext,
            _fixture.Services.GetRequiredService<ILogger<SignalRGeoEventBroadcaster>>());

        var events = new List<(GeofenceEvent Event, Geofence Geofence)>
        {
            (CreateTestEvent(entityId, eventType: GeofenceEventType.Enter), CreateTestGeofence()),
            (CreateTestEvent(entityId, eventType: GeofenceEventType.Exit), CreateTestGeofence()),
        };

        // Act
        await broadcaster.BroadcastEventsAsync(events);
        await Task.Delay(300);

        // Assert
        _receivedEvents.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task MultipleSubscribers_ShouldAllReceiveEvent()
    {
        // Arrange
        var entityId = "vehicle-multi";

        // Create second connection
        var connection2 = new HubConnectionBuilder()
            .WithUrl($"{_fixture.Server.BaseAddress}hubs/geoevent", options =>
            {
                options.HttpMessageHandlerFactory = _ => _fixture.Server.CreateHandler();
            })
            .Build();

        var received2 = new List<object>();
        connection2.On<object>("GeofenceEvent", evt => received2.Add(evt));

        await connection2.StartAsync();

        // Subscribe both connections
        await _hubConnection!.InvokeAsync("SubscribeToEntity", entityId);
        await connection2.InvokeAsync("SubscribeToEntity", entityId);
        await Task.Delay(100);

        _receivedEvents.Clear();

        var hubContext = _fixture.Services.GetRequiredService<IHubContext<GeoEventHub>>();
        var broadcaster = new SignalRGeoEventBroadcaster(
            hubContext,
            _fixture.Services.GetRequiredService<ILogger<SignalRGeoEventBroadcaster>>());

        var geofenceEvent = CreateTestEvent(entityId);
        var geofence = CreateTestGeofence();

        // Act
        await broadcaster.BroadcastEventAsync(geofenceEvent, geofence);
        await Task.Delay(300);

        // Assert
        _receivedEvents.Should().HaveCountGreaterOrEqualTo(1);
        received2.Should().HaveCountGreaterOrEqualTo(1);

        // Cleanup
        await connection2.StopAsync();
        await connection2.DisposeAsync();
    }

    // Helper methods

    private GeofenceEvent CreateTestEvent(
        string entityId,
        Guid? geofenceId = null,
        GeofenceEventType eventType = GeofenceEventType.Enter)
    {
        var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
        return new GeofenceEvent
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            EventTime = DateTime.UtcNow,
            GeofenceId = geofenceId ?? Guid.NewGuid(),
            GeofenceName = "Test Geofence",
            EntityId = entityId,
            EntityType = "vehicle",
            Location = geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8)),
            Properties = new Dictionary<string, object> { ["speed"] = 45.5 },
            DwellTimeSeconds = eventType == GeofenceEventType.Exit ? 300 : null
        };
    }

    private Geofence CreateTestGeofence(Guid? id = null)
    {
        var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
        var coordinates = new[]
        {
            new Coordinate(-122.5, 37.7),
            new Coordinate(-122.3, 37.7),
            new Coordinate(-122.3, 37.9),
            new Coordinate(-122.5, 37.9),
            new Coordinate(-122.5, 37.7)
        };

        return new Geofence
        {
            Id = id ?? Guid.NewGuid(),
            Name = "Test Geofence",
            Description = "Test geofence for SignalR tests",
            Geometry = geometryFactory.CreatePolygon(coordinates),
            EnabledEventTypes = GeofenceEventTypes.Enter | GeofenceEventTypes.Exit,
            IsActive = true,
            Properties = new Dictionary<string, object> { ["zone"] = "test" },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
