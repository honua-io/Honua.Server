using Honua.Server.Enterprise.Events.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Honua.Server.Host.GeoEvent;

/// <summary>
/// SignalR hub for real-time geofence event streaming
/// </summary>
/// <remarks>
/// Clients can connect to this hub to receive real-time geofence events as they occur.
///
/// **Client Connection (JavaScript)**:
/// <code>
/// const connection = new signalR.HubConnectionBuilder()
///     .withUrl("/hubs/geoevent", {
///         accessTokenFactory: () => yourAuthToken
///     })
///     .build();
///
/// connection.on("GeofenceEvent", (event) => {
///     console.log("Geofence event received:", event);
///     // event.eventType: "Enter" or "Exit"
///     // event.entityId: ID of the entity
///     // event.geofenceName: Name of the geofence
/// });
///
/// await connection.start();
///
/// // Subscribe to specific entity
/// await connection.invoke("SubscribeToEntity", "vehicle-123");
///
/// // Subscribe to specific geofence
/// await connection.invoke("SubscribeToGeofence", "geofence-id-here");
/// </code>
/// </remarks>
[Authorize]
public class GeoEventHub : Hub
{
    private readonly ILogger<GeoEventHub> _logger;

    public GeoEventHub(ILogger<GeoEventHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Subscribe to events for a specific entity
    /// </summary>
    /// <param name="entityId">Entity ID to subscribe to</param>
    public async Task SubscribeToEntity(string entityId)
    {
        var groupName = $"entity:{entityId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        _logger.LogInformation(
            "Client {ConnectionId} subscribed to entity {EntityId}",
            Context.ConnectionId,
            entityId);

        await Clients.Caller.SendAsync("Subscribed", new
        {
            type = "entity",
            id = entityId,
            message = $"Subscribed to events for entity {entityId}"
        });
    }

    /// <summary>
    /// Unsubscribe from events for a specific entity
    /// </summary>
    public async Task UnsubscribeFromEntity(string entityId)
    {
        var groupName = $"entity:{entityId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        _logger.LogInformation(
            "Client {ConnectionId} unsubscribed from entity {EntityId}",
            Context.ConnectionId,
            entityId);
    }

    /// <summary>
    /// Subscribe to events for a specific geofence
    /// </summary>
    public async Task SubscribeToGeofence(string geofenceId)
    {
        var groupName = $"geofence:{geofenceId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        _logger.LogInformation(
            "Client {ConnectionId} subscribed to geofence {GeofenceId}",
            Context.ConnectionId,
            geofenceId);

        await Clients.Caller.SendAsync("Subscribed", new
        {
            type = "geofence",
            id = geofenceId,
            message = $"Subscribed to events for geofence {geofenceId}"
        });
    }

    /// <summary>
    /// Unsubscribe from events for a specific geofence
    /// </summary>
    public async Task UnsubscribeFromGeofence(string geofenceId)
    {
        var groupName = $"geofence:{geofenceId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        _logger.LogInformation(
            "Client {ConnectionId} unsubscribed from geofence {GeofenceId}",
            Context.ConnectionId,
            geofenceId);
    }

    /// <summary>
    /// Subscribe to all geofence events (requires admin permission)
    /// </summary>
    [Authorize(Roles = "Admin")]
    public async Task SubscribeToAll()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "all-events");

        _logger.LogInformation(
            "Client {ConnectionId} subscribed to all events",
            Context.ConnectionId);

        await Clients.Caller.SendAsync("Subscribed", new
        {
            type = "all",
            message = "Subscribed to all geofence events"
        });
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client {ConnectionId} connected to GeoEvent hub", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation(
            "Client {ConnectionId} disconnected from GeoEvent hub. Exception: {Exception}",
            Context.ConnectionId,
            exception?.Message);

        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// Service for broadcasting geofence events via SignalR
/// </summary>
public interface IGeoEventBroadcaster
{
    /// <summary>
    /// Broadcast a geofence event to subscribed clients
    /// </summary>
    Task BroadcastEventAsync(GeofenceEvent geofenceEvent, Geofence geofence);

    /// <summary>
    /// Broadcast multiple events
    /// </summary>
    Task BroadcastEventsAsync(List<(GeofenceEvent Event, Geofence Geofence)> events);
}

/// <summary>
/// Implementation of GeoEvent broadcaster using SignalR
/// </summary>
public class SignalRGeoEventBroadcaster : IGeoEventBroadcaster
{
    private readonly IHubContext<GeoEventHub> _hubContext;
    private readonly ILogger<SignalRGeoEventBroadcaster> _logger;

    public SignalRGeoEventBroadcaster(
        IHubContext<GeoEventHub> hubContext,
        ILogger<SignalRGeoEventBroadcaster> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task BroadcastEventAsync(GeofenceEvent geofenceEvent, Geofence geofence)
    {
        var payload = CreateEventPayload(geofenceEvent, geofence);

        try
        {
            // Broadcast to entity-specific subscribers
            var entityGroup = $"entity:{geofenceEvent.EntityId}";
            await _hubContext.Clients.Group(entityGroup).SendAsync("GeofenceEvent", payload);

            // Broadcast to geofence-specific subscribers
            var geofenceGroup = $"geofence:{geofence.Id}";
            await _hubContext.Clients.Group(geofenceGroup).SendAsync("GeofenceEvent", payload);

            // Broadcast to all-events subscribers
            await _hubContext.Clients.Group("all-events").SendAsync("GeofenceEvent", payload);

            _logger.LogDebug(
                "Broadcasted geofence event {EventId} ({EventType}) for entity {EntityId}",
                geofenceEvent.Id,
                geofenceEvent.EventType,
                geofenceEvent.EntityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting geofence event {EventId}", geofenceEvent.Id);
        }
    }

    public async Task BroadcastEventsAsync(List<(GeofenceEvent Event, Geofence Geofence)> events)
    {
        foreach (var (geofenceEvent, geofence) in events)
        {
            await BroadcastEventAsync(geofenceEvent, geofence);
        }
    }

    private object CreateEventPayload(GeofenceEvent geofenceEvent, Geofence geofence)
    {
        return new
        {
            eventId = geofenceEvent.Id,
            eventType = geofenceEvent.EventType.ToString(),
            eventTime = geofenceEvent.EventTime,
            entityId = geofenceEvent.EntityId,
            entityType = geofenceEvent.EntityType,
            geofenceId = geofence.Id,
            geofenceName = geofence.Name,
            geofenceProperties = geofence.Properties,
            location = new
            {
                latitude = geofenceEvent.Location.Y,
                longitude = geofenceEvent.Location.X
            },
            properties = geofenceEvent.Properties,
            dwellTimeSeconds = geofenceEvent.DwellTimeSeconds,
            tenantId = geofenceEvent.TenantId
        };
    }
}
