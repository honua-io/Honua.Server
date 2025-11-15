// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Honua.Server.Enterprise.Events.Queue.Repositories;
using Honua.Server.Enterprise.Events.Repositories;
using Honua.Server.Enterprise.Events.CEP.Services;
using Honua.Server.Host.GeoEvent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Enterprise.Events.Queue.Services;

/// <summary>
/// Background service that consumes events from the durable queue and delivers them to SignalR
/// </summary>
public class GeofenceEventQueueConsumerService : BackgroundService
{
    private readonly IGeofenceEventQueueRepository _queueRepository;
    private readonly IGeofenceEventRepository _eventRepository;
    private readonly IGeofenceRepository _geofenceRepository;
    private readonly IHubContext<GeoEventHub> _hubContext;
    private readonly IPatternMatchingEngine? _patternMatchingEngine;
    private readonly ILogger<GeofenceEventQueueConsumerService> _logger;
    private readonly GeoEventQueueOptions _options;

    public GeofenceEventQueueConsumerService(
        IGeofenceEventQueueRepository queueRepository,
        IGeofenceEventRepository eventRepository,
        IGeofenceRepository geofenceRepository,
        IHubContext<GeoEventHub> hubContext,
        IOptions<GeoEventQueueOptions> options,
        ILogger<GeofenceEventQueueConsumerService> logger,
        IPatternMatchingEngine? patternMatchingEngine = null)
    {
        _queueRepository = queueRepository ?? throw new ArgumentNullException(nameof(queueRepository));
        _eventRepository = eventRepository ?? throw new ArgumentNullException(nameof(eventRepository));
        _geofenceRepository = geofenceRepository ?? throw new ArgumentNullException(nameof(geofenceRepository));
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _patternMatchingEngine = patternMatchingEngine; // Optional CEP integration
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Geofence Event Queue Consumer Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessQueueAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event queue");
            }

            // Wait before polling again
            await Task.Delay(TimeSpan.FromSeconds(_options.PollingIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("Geofence Event Queue Consumer Service stopped");
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        var queueItems = await _queueRepository.PollPendingEventsAsync(
            _options.BatchSize,
            tenantId: null, // Process all tenants
            cancellationToken);

        if (!queueItems.Any())
        {
            return;
        }

        _logger.LogDebug("Processing {Count} pending events from queue", queueItems.Count);

        foreach (var queueItem in queueItems)
        {
            await ProcessQueueItemAsync(queueItem, cancellationToken);
        }
    }

    private async Task ProcessQueueItemAsync(
        Queue.Models.GeofenceEventQueueItem queueItem,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Retrieve the full geofence event
            var geofenceEvent = await _eventRepository.GetByIdAsync(
                queueItem.GeofenceEventId,
                queueItem.TenantId,
                cancellationToken);

            if (geofenceEvent == null)
            {
                _logger.LogWarning(
                    "Geofence event {EventId} not found, marking queue item {QueueId} as failed",
                    queueItem.GeofenceEventId,
                    queueItem.Id);

                await _queueRepository.MarkEventFailedAsync(
                    queueItem.Id,
                    "signalr",
                    "Geofence event not found in database",
                    cancellationToken: cancellationToken);

                return;
            }

            // Retrieve the geofence
            var geofence = await _geofenceRepository.GetByIdAsync(
                geofenceEvent.GeofenceId,
                queueItem.TenantId,
                cancellationToken);

            if (geofence == null)
            {
                _logger.LogWarning(
                    "Geofence {GeofenceId} not found for event {EventId}",
                    geofenceEvent.GeofenceId,
                    geofenceEvent.Id);

                await _queueRepository.MarkEventFailedAsync(
                    queueItem.Id,
                    "signalr",
                    "Geofence not found in database",
                    cancellationToken: cancellationToken);

                return;
            }

            // Deliver to each target
            foreach (var target in queueItem.DeliveryTargets)
            {
                if (target.Equals("signalr", StringComparison.OrdinalIgnoreCase))
                {
                    await DeliverToSignalRAsync(queueItem, geofenceEvent, geofence, stopwatch, cancellationToken);
                }
                else if (target.Equals("servicebus", StringComparison.OrdinalIgnoreCase))
                {
                    // TODO: Implement Service Bus delivery in Phase 2
                    _logger.LogWarning("Service Bus delivery not yet implemented for queue item {QueueId}", queueItem.Id);
                }
                else
                {
                    _logger.LogWarning("Unknown delivery target '{Target}' for queue item {QueueId}", target, queueItem.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to process queue item {QueueId} (attempt {Attempt}/{MaxAttempts})",
                queueItem.Id,
                queueItem.AttemptCount,
                queueItem.MaxAttempts);

            await _queueRepository.MarkEventFailedAsync(
                queueItem.Id,
                "signalr",
                ex.Message,
                new Dictionary<string, object>
                {
                    { "exception_type", ex.GetType().Name },
                    { "stack_trace", ex.StackTrace ?? string.Empty }
                },
                cancellationToken);
        }
    }

    private async Task DeliverToSignalRAsync(
        Queue.Models.GeofenceEventQueueItem queueItem,
        Models.GeofenceEvent geofenceEvent,
        Models.Geofence geofence,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var payload = CreateEventPayload(geofenceEvent, geofence);
        var recipientCount = 0;

        try
        {
            // Broadcast to entity-specific subscribers
            var entityGroup = $"entity:{geofenceEvent.EntityId}";
            await _hubContext.Clients.Group(entityGroup).SendAsync("GeofenceEvent", payload, cancellationToken);
            recipientCount++;

            // Broadcast to geofence-specific subscribers
            var geofenceGroup = $"geofence:{geofence.Id}";
            await _hubContext.Clients.Group(geofenceGroup).SendAsync("GeofenceEvent", payload, cancellationToken);
            recipientCount++;

            // Broadcast to all-events subscribers
            await _hubContext.Clients.Group("all-events").SendAsync("GeofenceEvent", payload, cancellationToken);
            recipientCount++;

            stopwatch.Stop();

            await _queueRepository.MarkEventDeliveredAsync(
                queueItem.Id,
                "signalr",
                recipientCount,
                (int)stopwatch.ElapsedMilliseconds,
                new Dictionary<string, object>
                {
                    { "entity_group", entityGroup },
                    { "geofence_group", geofenceGroup }
                },
                cancellationToken);

            _logger.LogDebug(
                "Delivered event {EventId} via SignalR to {RecipientCount} groups in {LatencyMs}ms",
                geofenceEvent.Id,
                recipientCount,
                stopwatch.ElapsedMilliseconds);

            // After successful SignalR delivery, evaluate event against CEP patterns
            if (_patternMatchingEngine != null)
            {
                try
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var cepResults = await _patternMatchingEngine.EvaluateEventAsync(
                                geofenceEvent,
                                CancellationToken.None);

                            if (cepResults.Any())
                            {
                                _logger.LogDebug(
                                    "CEP evaluation for event {EventId}: {PartialMatches} partial, {CompleteMatches} complete matches",
                                    geofenceEvent.Id,
                                    cepResults.Count(r => r.MatchType == MatchType.Partial),
                                    cepResults.Count(r => r.MatchType == MatchType.Complete));
                            }
                        }
                        catch (Exception cepEx)
                        {
                            _logger.LogError(cepEx, "Error evaluating event {EventId} against CEP patterns", geofenceEvent.Id);
                        }
                    }, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to queue CEP evaluation for event {EventId}", geofenceEvent.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to deliver event {EventId} to SignalR",
                geofenceEvent.Id);

            throw;
        }
    }

    private object CreateEventPayload(Models.GeofenceEvent geofenceEvent, Models.Geofence geofence)
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

/// <summary>
/// Configuration options for the geofence event queue consumer
/// </summary>
public class GeoEventQueueOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "GeoEventQueue";

    /// <summary>
    /// Number of seconds to wait between polling for pending events
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Number of events to process in each batch
    /// </summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>
    /// Retention period for completed queue items in days
    /// </summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    /// Whether to enable Azure Service Bus delivery
    /// </summary>
    public bool EnableServiceBus { get; set; } = false;

    /// <summary>
    /// Azure Service Bus connection string
    /// </summary>
    public string? ServiceBusConnectionString { get; set; }

    /// <summary>
    /// Azure Service Bus topic name for geofence events
    /// </summary>
    public string? ServiceBusTopicName { get; set; } = "geofence-events";
}
