// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Enterprise.Events.Models;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.Events.Notifications;

/// <summary>
/// Service that orchestrates multiple geofence event notifiers
/// </summary>
public class GeofenceEventNotificationService : IGeofenceEventNotificationService
{
    private readonly IEnumerable<IGeofenceEventNotifier> _notifiers;
    private readonly ILogger<GeofenceEventNotificationService> _logger;

    public GeofenceEventNotificationService(
        IEnumerable<IGeofenceEventNotifier> notifiers,
        ILogger<GeofenceEventNotificationService> logger)
    {
        _notifiers = notifiers ?? throw new ArgumentNullException(nameof(notifiers));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task NotifyAllAsync(
        GeofenceEvent geofenceEvent,
        Geofence geofence,
        CancellationToken cancellationToken = default)
    {
        var enabledNotifiers = _notifiers.Where(n => n.IsEnabled).ToList();

        if (!enabledNotifiers.Any())
        {
            _logger.LogDebug("No enabled notifiers configured for event {EventId}", geofenceEvent.Id);
            return;
        }

        _logger.LogDebug(
            "Sending event {EventId} to {NotifierCount} enabled notifiers",
            geofenceEvent.Id,
            enabledNotifiers.Count);

        var tasks = enabledNotifiers.Select(notifier =>
            NotifyWithLogging(notifier, geofenceEvent, geofence, cancellationToken));

        // Execute all notifiers in parallel, don't fail if one fails
        await Task.WhenAll(tasks);
    }

    public async Task NotifyAllBatchAsync(
        List<(GeofenceEvent Event, Geofence Geofence)> events,
        CancellationToken cancellationToken = default)
    {
        if (!events.Any())
        {
            return;
        }

        var enabledNotifiers = _notifiers.Where(n => n.IsEnabled).ToList();

        if (!enabledNotifiers.Any())
        {
            _logger.LogDebug("No enabled notifiers configured for batch of {EventCount} events", events.Count);
            return;
        }

        _logger.LogDebug(
            "Sending batch of {EventCount} events to {NotifierCount} enabled notifiers",
            events.Count,
            enabledNotifiers.Count);

        var tasks = enabledNotifiers.Select(notifier =>
            NotifyBatchWithLogging(notifier, events, cancellationToken));

        await Task.WhenAll(tasks);
    }

    public IEnumerable<IGeofenceEventNotifier> GetNotifiers()
    {
        return _notifiers;
    }

    private async Task NotifyWithLogging(
        IGeofenceEventNotifier notifier,
        GeofenceEvent geofenceEvent,
        Geofence geofence,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug(
                "Notifying via {NotifierName} for event {EventId}",
                notifier.Name,
                geofenceEvent.Id);

            await notifier.NotifyAsync(geofenceEvent, geofence, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error in {NotifierName} notifier for event {EventId}",
                notifier.Name,
                geofenceEvent.Id);
        }
    }

    private async Task NotifyBatchWithLogging(
        IGeofenceEventNotifier notifier,
        List<(GeofenceEvent Event, Geofence Geofence)> events,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug(
                "Notifying via {NotifierName} for batch of {EventCount} events",
                notifier.Name,
                events.Count);

            await notifier.NotifyBatchAsync(events, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error in {NotifierName} notifier for batch notification",
                notifier.Name);
        }
    }
}
