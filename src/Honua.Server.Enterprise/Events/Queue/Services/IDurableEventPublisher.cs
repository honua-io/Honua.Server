// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Enterprise.Events.Models;

namespace Honua.Server.Enterprise.Events.Queue.Services;

/// <summary>
/// Durable event publisher with guaranteed delivery
/// </summary>
public interface IDurableEventPublisher
{
    /// <summary>
    /// Publish a geofence event with guaranteed delivery
    /// </summary>
    /// <param name="geofenceEvent">The geofence event to publish</param>
    /// <param name="geofence">The geofence that triggered the event</param>
    /// <param name="deliveryTargets">Delivery targets (signalr, servicebus, etc.)</param>
    /// <param name="priority">Priority (higher = more urgent)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Queue item ID</returns>
    Task<Guid> PublishEventAsync(
        GeofenceEvent geofenceEvent,
        Geofence geofence,
        List<string>? deliveryTargets = null,
        int priority = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish multiple geofence events in batch
    /// </summary>
    /// <param name="events">List of geofence events with their geofences</param>
    /// <param name="deliveryTargets">Delivery targets (signalr, servicebus, etc.)</param>
    /// <param name="priority">Priority (higher = more urgent)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of queue item IDs</returns>
    Task<List<Guid>> PublishEventsAsync(
        List<(GeofenceEvent Event, Geofence Geofence)> events,
        List<string>? deliveryTargets = null,
        int priority = 0,
        CancellationToken cancellationToken = default);
}
