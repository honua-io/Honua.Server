// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Enterprise.Sensors.Models;
using Honua.Server.Host.SensorThings;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Honua.Server.Integration.Tests.SensorThings;

/// <summary>
/// Integration tests for SensorThings SignalR Hub.
/// Tests real-time observation broadcasting and subscription management.
/// </summary>
public class SensorObservationHubIntegrationTests : IAsyncLifetime, IClassFixture<SensorObservationStreamingTestFixture>
{
    private readonly SensorObservationStreamingTestFixture _fixture;
    private HubConnection? _hubConnection;
    private readonly List<object> _receivedObservations = new();
    private readonly List<object> _receivedBatches = new();
    private readonly List<object> _receivedSubscriptions = new();

    public SensorObservationHubIntegrationTests(SensorObservationStreamingTestFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Create SignalR client connection
        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{_fixture.Server.BaseAddress}hubs/sensor-observations", options =>
            {
                options.HttpMessageHandlerFactory = _ => _fixture.Server.CreateHandler();
            })
            .Build();

        // Register event handlers
        _hubConnection.On<object>("ObservationCreated", obs =>
        {
            _receivedObservations.Add(obs);
        });

        _hubConnection.On<object>("ObservationsBatch", batch =>
        {
            _receivedBatches.Add(batch);
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
    public async Task SubscribeToDatastream_ShouldReceiveConfirmation()
    {
        // Arrange
        var datastreamId = "datastream-123";

        // Act
        await _hubConnection!.InvokeAsync("SubscribeToDatastream", datastreamId);
        await Task.Delay(100); // Give time for confirmation

        // Assert
        _receivedSubscriptions.Should().HaveCountGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task SubscribeToThing_ShouldReceiveConfirmation()
    {
        // Arrange
        var thingId = "thing-456";

        // Act
        await _hubConnection!.InvokeAsync("SubscribeToThing", thingId);
        await Task.Delay(100);

        // Assert
        _receivedSubscriptions.Should().HaveCountGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task SubscribeToSensor_ShouldReceiveConfirmation()
    {
        // Arrange
        var sensorId = "sensor-789";

        // Act
        await _hubConnection!.InvokeAsync("SubscribeToSensor", sensorId);
        await Task.Delay(100);

        // Assert
        _receivedSubscriptions.Should().HaveCountGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task BroadcastObservation_ToDatastreamSubscribers_ShouldReceiveObservation()
    {
        // Arrange
        var datastreamId = "datastream-broadcast-test";
        await _hubConnection!.InvokeAsync("SubscribeToDatastream", datastreamId);
        await Task.Delay(100);

        _receivedObservations.Clear();

        // Get the hub context to broadcast an observation
        var hubContext = _fixture.Services.GetRequiredService<IHubContext<SensorObservationHub>>();
        var options = new SensorObservationStreamingOptions { Enabled = true, RateLimitingEnabled = false };
        var broadcaster = new SignalRSensorObservationBroadcaster(
            hubContext,
            _fixture.Services.GetRequiredService<ILogger<SignalRSensorObservationBroadcaster>>(),
            options);

        var observation = CreateTestObservation(datastreamId);
        var datastream = CreateTestDatastream(datastreamId);

        // Act
        await broadcaster.BroadcastObservationAsync(observation, datastream);
        await Task.Delay(200); // Give time for broadcast

        // Assert
        _receivedObservations.Should().HaveCountGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task BroadcastObservation_ToThingSubscribers_ShouldReceiveObservation()
    {
        // Arrange
        var thingId = "thing-broadcast-test";
        var datastreamId = "datastream-for-thing-test";
        await _hubConnection!.InvokeAsync("SubscribeToThing", thingId);
        await Task.Delay(100);

        _receivedObservations.Clear();

        var hubContext = _fixture.Services.GetRequiredService<IHubContext<SensorObservationHub>>();
        var options = new SensorObservationStreamingOptions { Enabled = true, RateLimitingEnabled = false };
        var broadcaster = new SignalRSensorObservationBroadcaster(
            hubContext,
            _fixture.Services.GetRequiredService<ILogger<SignalRSensorObservationBroadcaster>>(),
            options);

        var observation = CreateTestObservation(datastreamId);
        var datastream = CreateTestDatastream(datastreamId, thingId: thingId);

        // Act
        await broadcaster.BroadcastObservationAsync(observation, datastream);
        await Task.Delay(200);

        // Assert
        _receivedObservations.Should().HaveCountGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task BroadcastObservation_ToSensorSubscribers_ShouldReceiveObservation()
    {
        // Arrange
        var sensorId = "sensor-broadcast-test";
        var datastreamId = "datastream-for-sensor-test";
        await _hubConnection!.InvokeAsync("SubscribeToSensor", sensorId);
        await Task.Delay(100);

        _receivedObservations.Clear();

        var hubContext = _fixture.Services.GetRequiredService<IHubContext<SensorObservationHub>>();
        var options = new SensorObservationStreamingOptions { Enabled = true, RateLimitingEnabled = false };
        var broadcaster = new SignalRSensorObservationBroadcaster(
            hubContext,
            _fixture.Services.GetRequiredService<ILogger<SignalRSensorObservationBroadcaster>>(),
            options);

        var observation = CreateTestObservation(datastreamId);
        var datastream = CreateTestDatastream(datastreamId, sensorId: sensorId);

        // Act
        await broadcaster.BroadcastObservationAsync(observation, datastream);
        await Task.Delay(200);

        // Assert
        _receivedObservations.Should().HaveCountGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task UnsubscribeFromDatastream_ShouldNotReceiveFurtherObservations()
    {
        // Arrange
        var datastreamId = "datastream-unsubscribe-test";
        await _hubConnection!.InvokeAsync("SubscribeToDatastream", datastreamId);
        await Task.Delay(100);

        // Unsubscribe
        await _hubConnection.InvokeAsync("UnsubscribeFromDatastream", datastreamId);
        await Task.Delay(100);

        _receivedObservations.Clear();

        var hubContext = _fixture.Services.GetRequiredService<IHubContext<SensorObservationHub>>();
        var options = new SensorObservationStreamingOptions { Enabled = true, RateLimitingEnabled = false };
        var broadcaster = new SignalRSensorObservationBroadcaster(
            hubContext,
            _fixture.Services.GetRequiredService<ILogger<SignalRSensorObservationBroadcaster>>(),
            options);

        var observation = CreateTestObservation(datastreamId);
        var datastream = CreateTestDatastream(datastreamId);

        // Act
        await broadcaster.BroadcastObservationAsync(observation, datastream);
        await Task.Delay(200);

        // Assert
        _receivedObservations.Should().BeEmpty();
    }

    [Fact]
    public async Task BroadcastObservationsBatch_ShouldReceiveBatchEvent()
    {
        // Arrange
        var datastreamId = "datastream-batch-test";
        await _hubConnection!.InvokeAsync("SubscribeToDatastream", datastreamId);
        await Task.Delay(100);

        _receivedBatches.Clear();

        var hubContext = _fixture.Services.GetRequiredService<IHubContext<SensorObservationHub>>();
        var options = new SensorObservationStreamingOptions
        {
            Enabled = true,
            RateLimitingEnabled = false,
            BatchingEnabled = true,
            BatchingThreshold = 5
        };
        var broadcaster = new SignalRSensorObservationBroadcaster(
            hubContext,
            _fixture.Services.GetRequiredService<ILogger<SignalRSensorObservationBroadcaster>>(),
            options);

        var observations = Enumerable.Range(1, 10)
            .Select(i => (CreateTestObservation(datastreamId, result: i * 10.5), CreateTestDatastream(datastreamId)))
            .ToList();

        // Act
        await broadcaster.BroadcastObservationsAsync(observations);
        await Task.Delay(300);

        // Assert
        _receivedBatches.Should().HaveCountGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task MultipleSubscribers_ShouldAllReceiveObservation()
    {
        // Arrange
        var datastreamId = "datastream-multi-subscriber";

        // Create second connection
        var connection2 = new HubConnectionBuilder()
            .WithUrl($"{_fixture.Server.BaseAddress}hubs/sensor-observations", options =>
            {
                options.HttpMessageHandlerFactory = _ => _fixture.Server.CreateHandler();
            })
            .Build();

        var received2 = new List<object>();
        connection2.On<object>("ObservationCreated", obs => received2.Add(obs));

        await connection2.StartAsync();

        // Subscribe both connections
        await _hubConnection!.InvokeAsync("SubscribeToDatastream", datastreamId);
        await connection2.InvokeAsync("SubscribeToDatastream", datastreamId);
        await Task.Delay(100);

        _receivedObservations.Clear();

        var hubContext = _fixture.Services.GetRequiredService<IHubContext<SensorObservationHub>>();
        var options = new SensorObservationStreamingOptions { Enabled = true, RateLimitingEnabled = false };
        var broadcaster = new SignalRSensorObservationBroadcaster(
            hubContext,
            _fixture.Services.GetRequiredService<ILogger<SignalRSensorObservationBroadcaster>>(),
            options);

        var observation = CreateTestObservation(datastreamId);
        var datastream = CreateTestDatastream(datastreamId);

        // Act
        await broadcaster.BroadcastObservationAsync(observation, datastream);
        await Task.Delay(300);

        // Assert
        _receivedObservations.Should().HaveCountGreaterOrEqualTo(1);
        received2.Should().HaveCountGreaterOrEqualTo(1);

        // Cleanup
        await connection2.StopAsync();
        await connection2.DisposeAsync();
    }

    // Helper methods

    private Observation CreateTestObservation(string datastreamId, object? result = null)
    {
        return new Observation
        {
            Id = Guid.NewGuid().ToString(),
            DatastreamId = datastreamId,
            PhenomenonTime = DateTime.UtcNow,
            ResultTime = DateTime.UtcNow,
            Result = result ?? 23.5,
            Parameters = new Dictionary<string, object> { ["quality"] = "good" },
            ServerTimestamp = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    private Datastream CreateTestDatastream(
        string datastreamId,
        string? thingId = null,
        string? sensorId = null)
    {
        return new Datastream
        {
            Id = datastreamId,
            Name = "Test Temperature Sensor",
            Description = "Temperature sensor for testing",
            ObservationType = "http://www.opengis.net/def/observationType/OGC-OM/2.0/OM_Measurement",
            UnitOfMeasurement = new UnitOfMeasurement
            {
                Name = "Degree Celsius",
                Symbol = "°C",
                Definition = "http://www.qudt.org/qudt/owl/1.0.0/unit/Instances.html#DegreeCelsius"
            },
            ThingId = thingId ?? "thing-test",
            SensorId = sensorId ?? "sensor-test",
            ObservedPropertyId = "prop-test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
