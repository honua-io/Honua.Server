// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Cloud.EventGrid.Models;
using Honua.Server.Core.Cloud.EventGrid.Services;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Cloud.EventGrid.Hooks;

/// <summary>
/// Publishes GeoEvent API events to Event Grid.
/// </summary>
public class GeoEventPublisher : IGeoEventPublisher
{
    private readonly IEventGridPublisher _publisher;
    private readonly ILogger<GeoEventPublisher> _logger;

    public GeoEventPublisher(
        IEventGridPublisher publisher,
        ILogger<GeoEventPublisher> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task PublishGeofenceEnteredAsync(
        string entityId,
        string geofenceId,
        string geofenceName,
        double longitude,
        double latitude,
        Dictionary<string, object?>? properties = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cloudEvent = new HonuaCloudEventBuilder()
                .WithId(Guid.NewGuid().ToString())
                .WithSource($"honua.io/geoevent/entities/{entityId}")
                .WithType(HonuaEventTypes.GeofenceEntered)
                .WithSubject(geofenceId)
                .WithTenantId(tenantId)
                .WithBoundingBox(new[] { longitude, latitude, longitude, latitude })
                .WithData(new
                {
                    entity_id = entityId,
                    geofence_id = geofenceId,
                    geofence_name = geofenceName,
                    location = new
                    {
                        type = "Point",
                        coordinates = new[] { longitude, latitude }
                    },
                    properties
                })
                .Build();

            await _publisher.PublishAsync(cloudEvent, cancellationToken);

            _logger.LogDebug("Published geofence entered event: Entity={Entity}, Geofence={Geofence}",
                entityId, geofenceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing geofence entered event: Entity={Entity}, Geofence={Geofence}",
                entityId, geofenceId);
        }
    }

    public async Task PublishGeofenceExitedAsync(
        string entityId,
        string geofenceId,
        string geofenceName,
        double longitude,
        double latitude,
        double? dwellTimeSeconds = null,
        Dictionary<string, object?>? properties = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cloudEvent = new HonuaCloudEventBuilder()
                .WithId(Guid.NewGuid().ToString())
                .WithSource($"honua.io/geoevent/entities/{entityId}")
                .WithType(HonuaEventTypes.GeofenceExited)
                .WithSubject(geofenceId)
                .WithTenantId(tenantId)
                .WithBoundingBox(new[] { longitude, latitude, longitude, latitude })
                .WithData(new
                {
                    entity_id = entityId,
                    geofence_id = geofenceId,
                    geofence_name = geofenceName,
                    location = new
                    {
                        type = "Point",
                        coordinates = new[] { longitude, latitude }
                    },
                    dwell_time_seconds = dwellTimeSeconds,
                    properties
                })
                .Build();

            await _publisher.PublishAsync(cloudEvent, cancellationToken);

            _logger.LogDebug("Published geofence exited event: Entity={Entity}, Geofence={Geofence}, DwellTime={DwellTime}s",
                entityId, geofenceId, dwellTimeSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing geofence exited event: Entity={Entity}, Geofence={Geofence}",
                entityId, geofenceId);
        }
    }

    public async Task PublishGeofenceAlertAsync(
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
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cloudEvent = new HonuaCloudEventBuilder()
                .WithId(Guid.NewGuid().ToString())
                .WithSource($"honua.io/geoevent/entities/{entityId}")
                .WithType(HonuaEventTypes.GeofenceAlert)
                .WithSubject(geofenceId)
                .WithTenantId(tenantId)
                .WithSeverity(severity)
                .WithBoundingBox(new[] { longitude, latitude, longitude, latitude })
                .WithData(new
                {
                    entity_id = entityId,
                    geofence_id = geofenceId,
                    geofence_name = geofenceName,
                    alert_type = alertType,
                    severity,
                    message,
                    location = new
                    {
                        type = "Point",
                        coordinates = new[] { longitude, latitude }
                    },
                    properties
                })
                .Build();

            await _publisher.PublishAsync(cloudEvent, cancellationToken);

            _logger.LogWarning("Published geofence alert event: Entity={Entity}, Geofence={Geofence}, Severity={Severity}",
                entityId, geofenceId, severity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing geofence alert event: Entity={Entity}, Geofence={Geofence}",
                entityId, geofenceId);
        }
    }

    public async Task PublishLocationEvaluatedAsync(
        string entityId,
        double longitude,
        double latitude,
        IEnumerable<string> currentGeofences,
        int eventsGenerated,
        Dictionary<string, object?>? properties = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cloudEvent = new HonuaCloudEventBuilder()
                .WithId(Guid.NewGuid().ToString())
                .WithSource($"honua.io/geoevent/entities/{entityId}")
                .WithType(HonuaEventTypes.LocationEvaluated)
                .WithSubject(entityId)
                .WithTenantId(tenantId)
                .WithBoundingBox(new[] { longitude, latitude, longitude, latitude })
                .WithData(new
                {
                    entity_id = entityId,
                    location = new
                    {
                        type = "Point",
                        coordinates = new[] { longitude, latitude }
                    },
                    current_geofences = currentGeofences.ToList(),
                    events_generated = eventsGenerated,
                    properties
                })
                .Build();

            await _publisher.PublishAsync(cloudEvent, cancellationToken);

            _logger.LogTrace("Published location evaluated event: Entity={Entity}, Geofences={GeofenceCount}",
                entityId, currentGeofences.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing location evaluated event: Entity={Entity}", entityId);
        }
    }
}
