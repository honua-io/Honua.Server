// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Enterprise.Sensors.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Honua.Server.Host.SensorThings;

/// <summary>
/// SignalR hub for real-time sensor observation streaming.
/// Enables dashboards and clients to receive live sensor data as observations are created.
/// </summary>
/// <remarks>
/// **Client Connection (JavaScript)**:
/// <code>
/// const connection = new signalR.HubConnectionBuilder()
///     .withUrl("/hubs/sensor-observations", {
///         accessTokenFactory: () => yourAuthToken
///     })
///     .build();
///
/// // Listen for observation events
/// connection.on("ObservationCreated", (observation) => {
///     console.log("New observation:", observation);
///     // observation.datastreamId: ID of the datastream
///     // observation.result: The sensor reading
///     // observation.phenomenonTime: When the observation occurred
/// });
///
/// // Connect to the hub
/// await connection.start();
///
/// // Subscribe to a specific datastream
/// await connection.invoke("SubscribeToDatastream", "datastream-id-here");
///
/// // Subscribe to all datastreams for a specific Thing (sensor device)
/// await connection.invoke("SubscribeToThing", "thing-id-here");
///
/// // Subscribe to all observations (requires admin)
/// await connection.invoke("SubscribeToAll");
/// </code>
///
/// **Client Connection (TypeScript)**:
/// <code>
/// import * as signalR from "@microsoft/signalr";
///
/// interface Observation {
///     id: string;
///     datastreamId: string;
///     result: any;
///     phenomenonTime: string;
///     resultTime?: string;
///     parameters?: Record&lt;string, any&gt;;
/// }
///
/// const connection = new signalR.HubConnectionBuilder()
///     .withUrl("/hubs/sensor-observations", {
///         accessTokenFactory: () => getAuthToken()
///     })
///     .withAutomaticReconnect()
///     .build();
///
/// connection.on("ObservationCreated", (observation: Observation) => {
///     console.log(`New reading from ${observation.datastreamId}:`, observation.result);
/// });
///
/// await connection.start();
/// await connection.invoke("SubscribeToDatastream", datastreamId);
/// </code>
///
/// **Connection Management**:
/// The hub automatically handles reconnection, but clients should implement error handling:
/// <code>
/// connection.onreconnecting(() => console.log("Reconnecting..."));
/// connection.onreconnected(() => {
///     console.log("Reconnected, re-subscribing...");
///     // Re-subscribe to datastreams after reconnection
///     connection.invoke("SubscribeToDatastream", datastreamId);
/// });
/// connection.onclose(() => console.log("Connection closed"));
/// </code>
/// </remarks>
[Authorize]
public class SensorObservationHub : Hub
{
    private readonly ILogger<SensorObservationHub> _logger;

    public SensorObservationHub(ILogger<SensorObservationHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Subscribe to observations from a specific datastream.
    /// Clients will receive real-time updates whenever new observations are added to this datastream.
    /// </summary>
    /// <param name="datastreamId">Datastream ID to subscribe to</param>
    public async Task SubscribeToDatastream(string datastreamId)
    {
        if (string.IsNullOrWhiteSpace(datastreamId))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Datastream ID is required" });
            return;
        }

        var groupName = $"datastream:{datastreamId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        _logger.LogInformation(
            "Client {ConnectionId} subscribed to datastream {DatastreamId}",
            Context.ConnectionId,
            datastreamId);

        await Clients.Caller.SendAsync("Subscribed", new
        {
            type = "datastream",
            id = datastreamId,
            message = $"Subscribed to observations from datastream {datastreamId}"
        });
    }

    /// <summary>
    /// Unsubscribe from observations from a specific datastream.
    /// </summary>
    /// <param name="datastreamId">Datastream ID to unsubscribe from</param>
    public async Task UnsubscribeFromDatastream(string datastreamId)
    {
        var groupName = $"datastream:{datastreamId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        _logger.LogInformation(
            "Client {ConnectionId} unsubscribed from datastream {DatastreamId}",
            Context.ConnectionId,
            datastreamId);

        await Clients.Caller.SendAsync("Unsubscribed", new
        {
            type = "datastream",
            id = datastreamId
        });
    }

    /// <summary>
    /// Subscribe to observations from all datastreams associated with a specific Thing (sensor device).
    /// This is useful for monitoring all sensors on a particular device or location.
    /// </summary>
    /// <param name="thingId">Thing ID to subscribe to</param>
    public async Task SubscribeToThing(string thingId)
    {
        if (string.IsNullOrWhiteSpace(thingId))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Thing ID is required" });
            return;
        }

        var groupName = $"thing:{thingId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        _logger.LogInformation(
            "Client {ConnectionId} subscribed to thing {ThingId}",
            Context.ConnectionId,
            thingId);

        await Clients.Caller.SendAsync("Subscribed", new
        {
            type = "thing",
            id = thingId,
            message = $"Subscribed to all observations from thing {thingId}"
        });
    }

    /// <summary>
    /// Unsubscribe from observations from a specific Thing.
    /// </summary>
    /// <param name="thingId">Thing ID to unsubscribe from</param>
    public async Task UnsubscribeFromThing(string thingId)
    {
        var groupName = $"thing:{thingId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        _logger.LogInformation(
            "Client {ConnectionId} unsubscribed from thing {ThingId}",
            Context.ConnectionId,
            thingId);

        await Clients.Caller.SendAsync("Unsubscribed", new
        {
            type = "thing",
            id = thingId
        });
    }

    /// <summary>
    /// Subscribe to a specific sensor (all datastreams using this sensor).
    /// </summary>
    /// <param name="sensorId">Sensor ID to subscribe to</param>
    public async Task SubscribeToSensor(string sensorId)
    {
        if (string.IsNullOrWhiteSpace(sensorId))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Sensor ID is required" });
            return;
        }

        var groupName = $"sensor:{sensorId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        _logger.LogInformation(
            "Client {ConnectionId} subscribed to sensor {SensorId}",
            Context.ConnectionId,
            sensorId);

        await Clients.Caller.SendAsync("Subscribed", new
        {
            type = "sensor",
            id = sensorId,
            message = $"Subscribed to observations from sensor {sensorId}"
        });
    }

    /// <summary>
    /// Unsubscribe from a specific sensor.
    /// </summary>
    /// <param name="sensorId">Sensor ID to unsubscribe from</param>
    public async Task UnsubscribeFromSensor(string sensorId)
    {
        var groupName = $"sensor:{sensorId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        _logger.LogInformation(
            "Client {ConnectionId} unsubscribed from sensor {SensorId}",
            Context.ConnectionId,
            sensorId);

        await Clients.Caller.SendAsync("Unsubscribed", new
        {
            type = "sensor",
            id = sensorId
        });
    }

    /// <summary>
    /// Subscribe to all sensor observations (requires admin permission).
    /// Use with caution in high-volume scenarios.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public async Task SubscribeToAll()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "all-observations");

        _logger.LogInformation(
            "Client {ConnectionId} subscribed to all observations (admin)",
            Context.ConnectionId);

        await Clients.Caller.SendAsync("Subscribed", new
        {
            type = "all",
            message = "Subscribed to all sensor observations"
        });
    }

    /// <summary>
    /// Unsubscribe from all observations.
    /// </summary>
    public async Task UnsubscribeFromAll()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "all-observations");

        _logger.LogInformation(
            "Client {ConnectionId} unsubscribed from all observations",
            Context.ConnectionId);

        await Clients.Caller.SendAsync("Unsubscribed", new
        {
            type = "all"
        });
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation(
            "Client {ConnectionId} connected to SensorObservation hub. User: {User}",
            Context.ConnectionId,
            Context.User?.Identity?.Name ?? "Anonymous");

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(
                exception,
                "Client {ConnectionId} disconnected from SensorObservation hub with error",
                Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation(
                "Client {ConnectionId} disconnected from SensorObservation hub",
                Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
