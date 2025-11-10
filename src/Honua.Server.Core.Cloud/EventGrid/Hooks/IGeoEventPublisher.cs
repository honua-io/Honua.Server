// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Cloud.EventGrid.Hooks;

/// <summary>
/// Publishes GeoEvent API events to Event Grid.
/// </summary>
public interface IGeoEventPublisher
{
    /// <summary>
    /// Publish a geofence entered event.
    /// </summary>
    Task PublishGeofenceEnteredAsync(
        string entityId,
        string geofenceId,
        string geofenceName,
        double longitude,
        double latitude,
        Dictionary<string, object?>? properties = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish a geofence exited event.
    /// </summary>
    Task PublishGeofenceExitedAsync(
        string entityId,
        string geofenceId,
        string geofenceName,
        double longitude,
        double latitude,
        double? dwellTimeSeconds = null,
        Dictionary<string, object?>? properties = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish a geofence alert event (custom trigger).
    /// </summary>
    Task PublishGeofenceAlertAsync(
        string entityId,
        string geofenceId,
        string geofenceName,
        string alertType,
        string severity,
        string message,
        double longitude,
        double latitude,
        Dictionary<string, object?>? properties = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish a location evaluated event (for tracking).
    /// </summary>
    Task PublishLocationEvaluatedAsync(
        string entityId,
        double longitude,
        double latitude,
        IEnumerable<string> currentGeofences,
        int eventsGenerated,
        Dictionary<string, object?>? properties = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default);
}
