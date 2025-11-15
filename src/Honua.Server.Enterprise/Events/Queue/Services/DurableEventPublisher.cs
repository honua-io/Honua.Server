// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Enterprise.Events.Models;
using Honua.Server.Enterprise.Events.Queue.Repositories;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.Events.Queue.Services;

/// <summary>
/// Durable event publisher implementation
/// </summary>
public class DurableEventPublisher : IDurableEventPublisher
{
    private readonly IGeofenceEventQueueRepository _queueRepository;
    private readonly ILogger<DurableEventPublisher> _logger;

    public DurableEventPublisher(
        IGeofenceEventQueueRepository queueRepository,
        ILogger<DurableEventPublisher> logger)
    {
        _queueRepository = queueRepository ?? throw new ArgumentNullException(nameof(queueRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> PublishEventAsync(
        GeofenceEvent geofenceEvent,
        Geofence geofence,
        List<string>? deliveryTargets = null,
        int priority = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var queueId = await _queueRepository.EnqueueEventAsync(
                geofenceEvent,
                deliveryTargets,
                priority,
                cancellationToken);

            _logger.LogInformation(
                "Published geofence event {EventId} ({EventType}) for entity {EntityId} to durable queue (QueueId: {QueueId})",
                geofenceEvent.Id,
                geofenceEvent.EventType,
                geofenceEvent.EntityId,
                queueId);

            return queueId;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish geofence event {EventId} to durable queue",
                geofenceEvent.Id);
            throw;
        }
    }

    public async Task<List<Guid>> PublishEventsAsync(
        List<(GeofenceEvent Event, Geofence Geofence)> events,
        List<string>? deliveryTargets = null,
        int priority = 0,
        CancellationToken cancellationToken = default)
    {
        var queueIds = new List<Guid>();

        foreach (var (geofenceEvent, geofence) in events)
        {
            try
            {
                var queueId = await PublishEventAsync(
                    geofenceEvent,
                    geofence,
                    deliveryTargets,
                    priority,
                    cancellationToken);

                queueIds.Add(queueId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to publish event {EventId} in batch, continuing with remaining events",
                    geofenceEvent.Id);
            }
        }

        _logger.LogInformation(
            "Published {SuccessCount}/{TotalCount} geofence events to durable queue",
            queueIds.Count,
            events.Count);

        return queueIds;
    }
}
