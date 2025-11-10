// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Enterprise.Events.Models;

namespace Honua.Server.Enterprise.Events.Notifications;

/// <summary>
/// Interface for geofence event output connectors
/// </summary>
public interface IGeofenceEventNotifier
{
    /// <summary>
    /// Send notification for a geofence event
    /// </summary>
    /// <param name="geofenceEvent">The geofence event that was generated</param>
    /// <param name="geofence">The geofence that triggered the event</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task NotifyAsync(
        GeofenceEvent geofenceEvent,
        Geofence geofence,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send notifications for multiple events (batch)
    /// </summary>
    /// <param name="events">List of events with their corresponding geofences</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task NotifyBatchAsync(
        List<(GeofenceEvent Event, Geofence Geofence)> events,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifier name for logging and configuration
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether this notifier is enabled
    /// </summary>
    bool IsEnabled { get; }
}

/// <summary>
/// Orchestrates multiple event notifiers
/// </summary>
public interface IGeofenceEventNotificationService
{
    /// <summary>
    /// Send notifications through all configured notifiers
    /// </summary>
    Task NotifyAllAsync(
        GeofenceEvent geofenceEvent,
        Geofence geofence,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send batch notifications through all configured notifiers
    /// </summary>
    Task NotifyAllBatchAsync(
        List<(GeofenceEvent Event, Geofence Geofence)> events,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all registered notifiers
    /// </summary>
    IEnumerable<IGeofenceEventNotifier> GetNotifiers();
}
