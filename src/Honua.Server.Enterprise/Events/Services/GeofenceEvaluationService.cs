using System.Diagnostics;
using Honua.Server.Enterprise.Events.Models;
using Honua.Server.Enterprise.Events.Repositories;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace Honua.Server.Enterprise.Events.Services;

/// <summary>
/// Service for evaluating locations against geofences
/// </summary>
public class GeofenceEvaluationService : IGeofenceEvaluationService
{
    private readonly IGeofenceRepository _geofenceRepository;
    private readonly IEntityStateRepository _entityStateRepository;
    private readonly IGeofenceEventRepository _eventRepository;
    private readonly IGeofenceToAlertBridgeService? _alertBridgeService;
    private readonly ILogger<GeofenceEvaluationService> _logger;

    public GeofenceEvaluationService(
        IGeofenceRepository geofenceRepository,
        IEntityStateRepository entityStateRepository,
        IGeofenceEventRepository eventRepository,
        ILogger<GeofenceEvaluationService> logger,
        IGeofenceToAlertBridgeService? alertBridgeService = null)
    {
        _geofenceRepository = geofenceRepository ?? throw new ArgumentNullException(nameof(geofenceRepository));
        _entityStateRepository = entityStateRepository ?? throw new ArgumentNullException(nameof(entityStateRepository));
        _eventRepository = eventRepository ?? throw new ArgumentNullException(nameof(eventRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _alertBridgeService = alertBridgeService; // Optional - allows gradual rollout
    }

    public async Task<GeofenceEvaluationResult> EvaluateLocationAsync(
        string entityId,
        Point location,
        DateTime eventTime,
        Dictionary<string, object>? properties = null,
        string? entityType = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 1. Find all active geofences that contain this point
            var containingGeofences = await _geofenceRepository.FindGeofencesAtPointAsync(
                location.X, // longitude
                location.Y, // latitude
                tenantId,
                cancellationToken) ?? new List<Geofence>();

            _logger.LogDebug(
                "Entity {EntityId} at ({Lon}, {Lat}) is inside {Count} geofences",
                entityId,
                location.X,
                location.Y,
                containingGeofences.Count);

            // 2. Get current states for this entity
            var currentStates = await _entityStateRepository.GetEntityStatesAsync(
                entityId,
                tenantId,
                cancellationToken) ?? new List<EntityGeofenceState>();

            var currentGeofenceIds = currentStates
                .Where(s => s.IsInside)
                .Select(s => s.GeofenceId)
                .ToHashSet();

            var containingGeofenceIds = containingGeofences
                .Select(g => g.Id)
                .ToHashSet();

            // 3. Detect enter events (in new geofence, not in old state)
            var enterGeofences = containingGeofences
                .Where(g => !currentGeofenceIds.Contains(g.Id))
                .ToList();

            // 4. Detect exit events (in old state, not in new geofence)
            var exitGeofenceIds = currentGeofenceIds
                .Except(containingGeofenceIds)
                .ToList();

            // 5. Generate events
            var events = new List<GeofenceEvent>();

            // Generate ENTER events
            foreach (var geofence in enterGeofences)
            {
                if (!geofence.EnabledEventTypes.HasFlag(GeofenceEventTypes.Enter))
                {
                    continue;
                }

                var enterEvent = new GeofenceEvent
                {
                    Id = Guid.NewGuid(),
                    EventType = GeofenceEventType.Enter,
                    EventTime = eventTime,
                    GeofenceId = geofence.Id,
                    GeofenceName = geofence.Name,
                    EntityId = entityId,
                    EntityType = entityType,
                    Location = location,
                    Properties = properties,
                    TenantId = tenantId,
                    ProcessedAt = DateTime.UtcNow
                };

                events.Add(enterEvent);

                // Update state: entity is now inside
                await _entityStateRepository.UpsertStateAsync(
                    new EntityGeofenceState
                    {
                        EntityId = entityId,
                        GeofenceId = geofence.Id,
                        IsInside = true,
                        EnteredAt = eventTime,
                        LastUpdated = DateTime.UtcNow,
                        TenantId = tenantId
                    },
                    cancellationToken);

                _logger.LogInformation(
                    "Entity {EntityId} ENTERED geofence {GeofenceName} ({GeofenceId})",
                    entityId,
                    geofence.Name,
                    geofence.Id);
            }

            // Generate EXIT events
            // Batch query all exit geofences to avoid N+1 problem
            var exitGeofences = exitGeofenceIds.Any()
                ? await _geofenceRepository.GetByIdsAsync(exitGeofenceIds, tenantId, cancellationToken) ?? new List<Geofence>()
                : new List<Geofence>();

            var exitGeofenceMap = exitGeofences.ToDictionary(g => g.Id);

            foreach (var exitGeofenceId in exitGeofenceIds)
            {
                var previousState = currentStates.FirstOrDefault(s => s.GeofenceId == exitGeofenceId);
                if (previousState == null)
                {
                    continue;
                }

                // Get geofence details from batch query result
                if (!exitGeofenceMap.TryGetValue(exitGeofenceId, out var geofence) ||
                    !geofence.EnabledEventTypes.HasFlag(GeofenceEventTypes.Exit))
                {
                    continue;
                }

                // Calculate dwell time
                int? dwellTimeSeconds = null;
                if (previousState.EnteredAt.HasValue)
                {
                    var dwellTime = eventTime - previousState.EnteredAt.Value;
                    dwellTimeSeconds = (int)dwellTime.TotalSeconds;
                }

                var exitEvent = new GeofenceEvent
                {
                    Id = Guid.NewGuid(),
                    EventType = GeofenceEventType.Exit,
                    EventTime = eventTime,
                    GeofenceId = geofence.Id,
                    GeofenceName = geofence.Name,
                    EntityId = entityId,
                    EntityType = entityType,
                    Location = location,
                    Properties = properties,
                    DwellTimeSeconds = dwellTimeSeconds,
                    TenantId = tenantId,
                    ProcessedAt = DateTime.UtcNow
                };

                events.Add(exitEvent);

                // Update state: entity is no longer inside
                await _entityStateRepository.UpsertStateAsync(
                    new EntityGeofenceState
                    {
                        EntityId = entityId,
                        GeofenceId = geofence.Id,
                        IsInside = false,
                        EnteredAt = null,
                        LastUpdated = DateTime.UtcNow,
                        TenantId = tenantId
                    },
                    cancellationToken);

                _logger.LogInformation(
                    "Entity {EntityId} EXITED geofence {GeofenceName} ({GeofenceId}) after {DwellSeconds}s",
                    entityId,
                    geofence.Name,
                    geofence.Id,
                    dwellTimeSeconds ?? 0);
            }

            // Update states for geofences entity is still inside (refresh timestamp)
            foreach (var geofence in containingGeofences.Where(g => currentGeofenceIds.Contains(g.Id)))
            {
                var existingState = currentStates.FirstOrDefault(s => s.GeofenceId == geofence.Id);
                if (existingState != null)
                {
                    existingState.LastUpdated = DateTime.UtcNow;
                    await _entityStateRepository.UpsertStateAsync(existingState, cancellationToken);
                }
            }

            // 6. Persist events
            if (events.Any())
            {
                await _eventRepository.CreateBatchAsync(events, cancellationToken);

                // 7. Process events for alert generation (if bridge service is configured)
                if (_alertBridgeService != null)
                {
                    foreach (var geofenceEvent in events)
                    {
                        try
                        {
                            // Process each event asynchronously for alert generation
                            // We don't await here to avoid blocking the evaluation response
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await _alertBridgeService.ProcessGeofenceEventAsync(geofenceEvent, CancellationToken.None);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error processing geofence event {EventId} for alerts", geofenceEvent.Id);
                                }
                            }, CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to queue alert processing for event {EventId}", geofenceEvent.Id);
                        }
                    }
                }
            }

            stopwatch.Stop();

            return new GeofenceEvaluationResult
            {
                EntityId = entityId,
                Events = events,
                CurrentGeofences = containingGeofences,
                ProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                GeofencesEvaluated = containingGeofences.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error evaluating location for entity {EntityId}",
                entityId);
            throw;
        }
    }
}
