// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Enterprise.Events.Dto;
using Honua.Server.Enterprise.Events.Services;
using Honua.Server.Enterprise.Multitenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Geometries;

namespace Honua.Server.Host.GeoEvent;

/// <summary>
/// API endpoints for real-time location evaluation and geofence event generation
/// </summary>
/// <remarks>
/// This API evaluates locations against active geofences and generates enter/exit events.
/// Use this for real-time tracking of entities (vehicles, assets, people) moving through geofences.
///
/// Performance: Target P95 latency &lt; 100ms for 1,000 geofences
/// </remarks>
[ApiController]
[Route("api/v1/geoevent")]
[Authorize] // Require authentication
[Produces("application/json")]
[Tags("GeoFencing")]
public class GeoEventController : ControllerBase
{
    private readonly IGeofenceEvaluationService _evaluationService;
    private readonly ILogger<GeoEventController> _logger;

    public GeoEventController(
        IGeofenceEvaluationService evaluationService,
        ILogger<GeoEventController> logger)
    {
        _evaluationService = evaluationService;
        _logger = logger;
    }

    /// <summary>
    /// Evaluate a single location against all active geofences
    /// </summary>
    /// <param name="request">Location evaluation request with entity ID, location coordinates, and optional metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Evaluation result with generated events (Enter/Exit), current geofences, and processing time</returns>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/v1/geoevent/evaluate
    ///     {
    ///       "entity_id": "vehicle-123",
    ///       "entity_type": "vehicle",
    ///       "location": {
    ///         "type": "Point",
    ///         "coordinates": [-122.4194, 37.7749]
    ///       },
    ///       "event_time": "2025-11-05T10:30:00Z",
    ///       "properties": {
    ///         "speed": 45.5,
    ///         "heading": 180,
    ///         "driver_id": "D-456"
    ///       }
    ///     }
    ///
    /// **Event Generation Logic:**
    /// - **Enter Event**: Generated when entity enters a geofence (was outside, now inside)
    /// - **Exit Event**: Generated when entity exits a geofence (was inside, now outside). Includes dwell_time_seconds.
    /// - **No Event**: If entity remains inside or outside the same geofences
    ///
    /// **State Tracking**: The service maintains entity state to detect enter/exit events efficiently (O(1) lookup).
    ///
    /// Coordinates must be in WGS84 (EPSG:4326) format: [longitude, latitude]
    /// </remarks>
    /// <response code="200">Location evaluated successfully. Returns events generated and current geofences.</response>
    /// <response code="400">Invalid request (e.g., invalid coordinates, missing entity_id)</response>
    /// <response code="401">Unauthorized - authentication required</response>
    [HttpPost("evaluate")]
    [ProducesResponseType(typeof(EvaluateLocationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<EvaluateLocationResponse>> EvaluateLocation(
        [FromBody] EvaluateLocationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Validate request
            if (request.Location?.Coordinates == null || request.Location.Coordinates.Length != 2)
            {
                return BadRequest(new { error = "Location must be a point with [longitude, latitude] coordinates" });
            }

            if (string.IsNullOrWhiteSpace(request.EntityId))
            {
                return BadRequest(new { error = "EntityId is required" });
            }

            var tenantId = GetTenantId();

            // Parse coordinates - GeoJSON format is [longitude, latitude]
            var longitude = request.Location.Coordinates[0];
            var latitude = request.Location.Coordinates[1];

            // Validate coordinate ranges
            if (longitude < -180 || longitude > 180)
            {
                return BadRequest(new { error = "Longitude must be between -180 and 180" });
            }

            if (latitude < -90 || latitude > 90)
            {
                return BadRequest(new { error = "Latitude must be between -90 and 90" });
            }

            // Create NetTopologySuite Point (SRID 4326 - WGS84)
            var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
            var location = geometryFactory.CreatePoint(new Coordinate(longitude, latitude));

            // Parse event time (default to now if not provided)
            var eventTime = request.EventTime ?? DateTime.UtcNow;

            // Evaluate location against geofences
            var startTime = DateTime.UtcNow;

            var result = await _evaluationService.EvaluateLocationAsync(
                request.EntityId,
                location,
                eventTime,
                request.Properties,
                request.EntityType,
                tenantId,
                cancellationToken);

            var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

            // Log performance metrics
            if (processingTime > 100)
            {
                _logger.LogWarning(
                    "Slow geofence evaluation: {ProcessingTime}ms for entity {EntityId} against {GeofenceCount} geofences",
                    processingTime,
                    request.EntityId,
                    result.CurrentGeofences.Count);
            }

            // Map to response
            var response = new EvaluateLocationResponse
            {
                EntityId = request.EntityId,
                Location = request.Location,
                EventTime = eventTime,
                EventsGenerated = result.Events.Select(e => new GeofenceEventSummary
                {
                    Id = e.Id,
                    EventType = e.EventType.ToString(),
                    GeofenceId = e.GeofenceId,
                    GeofenceName = e.GeofenceName,
                    EventTime = e.EventTime,
                    DwellTimeSeconds = e.DwellTimeSeconds
                }).ToList(),
                CurrentGeofences = result.CurrentGeofences.Select(g => new GeofenceSummary
                {
                    Id = g.Id,
                    Name = g.Name
                }).ToList(),
                ProcessingTimeMs = processingTime
            };

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid location evaluation request");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating location for entity {EntityId}", request.EntityId);
            return StatusCode(500, new { error = "Internal server error processing location" });
        }
    }

    /// <summary>
    /// Evaluate multiple locations in a batch (for high-throughput scenarios)
    /// </summary>
    /// <param name="requests">Array of location evaluation requests (max 1000 per batch)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch evaluation results with success/error counts and total processing time</returns>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/v1/geoevent/evaluate/batch
    ///     [
    ///       {
    ///         "entity_id": "vehicle-123",
    ///         "location": { "type": "Point", "coordinates": [-122.4194, 37.7749] }
    ///       },
    ///       {
    ///         "entity_id": "vehicle-456",
    ///         "location": { "type": "Point", "coordinates": [-122.4094, 37.7849] }
    ///       }
    ///     ]
    ///
    /// **Use Cases:**
    /// - Bulk location updates from IoT devices
    /// - Processing historical GPS tracks
    /// - Integration with Azure Stream Analytics (MVP Phase 2)
    ///
    /// **Limits:**
    /// - Maximum 1000 locations per batch
    /// - Individual location failures don't fail the entire batch
    /// - Returns detailed error information for each failed location
    ///
    /// **Performance Target:** 100 events/second sustained throughput
    /// </remarks>
    /// <response code="200">Batch processed. Returns individual results with success/error status for each location.</response>
    /// <response code="400">Invalid batch request (e.g., empty array, exceeds 1000 limit)</response>
    /// <response code="401">Unauthorized - authentication required</response>
    [HttpPost("evaluate/batch")]
    [ProducesResponseType(typeof(BatchEvaluateLocationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<BatchEvaluateLocationResponse>> EvaluateBatch(
        [FromBody] List<EvaluateLocationRequest> requests,
        CancellationToken cancellationToken)
    {
        try
        {
            if (requests == null || !requests.Any())
            {
                return BadRequest(new { error = "At least one location request is required" });
            }

            if (requests.Count > 1000)
            {
                return BadRequest(new { error = "Maximum 1000 locations per batch request" });
            }

            var tenantId = GetTenantId();
            var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
            var startTime = DateTime.UtcNow;

            var responses = new List<EvaluateLocationResponse>();

            foreach (var request in requests)
            {
                try
                {
                    // Validate request
                    if (request.Location?.Coordinates == null || request.Location.Coordinates.Length != 2)
                    {
                        responses.Add(new EvaluateLocationResponse
                        {
                            EntityId = request.EntityId,
                            Location = request.Location,
                            Error = "Invalid location coordinates"
                        });
                        continue;
                    }

                    var longitude = request.Location.Coordinates[0];
                    var latitude = request.Location.Coordinates[1];

                    if (longitude < -180 || longitude > 180 || latitude < -90 || latitude > 90)
                    {
                        responses.Add(new EvaluateLocationResponse
                        {
                            EntityId = request.EntityId,
                            Location = request.Location,
                            Error = "Coordinates out of valid range"
                        });
                        continue;
                    }

                    var location = geometryFactory.CreatePoint(new Coordinate(longitude, latitude));
                    var eventTime = request.EventTime ?? DateTime.UtcNow;

                    var result = await _evaluationService.EvaluateLocationAsync(
                        request.EntityId,
                        location,
                        eventTime,
                        request.Properties,
                        request.EntityType,
                        tenantId,
                        cancellationToken);

                    responses.Add(new EvaluateLocationResponse
                    {
                        EntityId = request.EntityId,
                        Location = request.Location,
                        EventTime = eventTime,
                        EventsGenerated = result.Events.Select(e => new GeofenceEventSummary
                        {
                            Id = e.Id,
                            EventType = e.EventType.ToString(),
                            GeofenceId = e.GeofenceId,
                            GeofenceName = e.GeofenceName,
                            EventTime = e.EventTime,
                            DwellTimeSeconds = e.DwellTimeSeconds
                        }).ToList(),
                        CurrentGeofences = result.CurrentGeofences.Select(g => new GeofenceSummary
                        {
                            Id = g.Id,
                            Name = g.Name
                        }).ToList()
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error evaluating location for entity {EntityId} in batch", request.EntityId);
                    responses.Add(new EvaluateLocationResponse
                    {
                        EntityId = request.EntityId,
                        Location = request.Location,
                        Error = "Processing error"
                    });
                }
            }

            var totalProcessingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

            var batchResponse = new BatchEvaluateLocationResponse
            {
                Results = responses,
                TotalProcessed = responses.Count,
                SuccessCount = responses.Count(r => string.IsNullOrEmpty(r.Error)),
                ErrorCount = responses.Count(r => !string.IsNullOrEmpty(r.Error)),
                TotalProcessingTimeMs = totalProcessingTime
            };

            _logger.LogInformation(
                "Batch evaluation completed: {TotalProcessed} locations, {SuccessCount} success, {ErrorCount} errors, {ProcessingTime}ms",
                batchResponse.TotalProcessed,
                batchResponse.SuccessCount,
                batchResponse.ErrorCount,
                totalProcessingTime);

            return Ok(batchResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing batch location evaluation");
            return StatusCode(500, new { error = "Internal server error processing batch" });
        }
    }

    /// <summary>
    /// Gets the tenant ID from the current HTTP context.
    /// Extracts tenant from TenantMiddleware context (set by subdomain or X-Tenant-Id header).
    /// Returns null for single-tenant deployments or when TenantMiddleware is not active.
    /// </summary>
    /// <returns>Tenant ID if available; null otherwise</returns>
    private string? GetTenantId()
    {
        // Extract tenant from TenantMiddleware context
        // TenantMiddleware extracts tenant from subdomain or X-Tenant-Id header
        var tenantContext = HttpContext.GetTenantContext();

        if (tenantContext != null)
        {
            _logger.LogDebug("Request executing for tenant: {TenantId}", tenantContext.TenantId);
            return tenantContext.TenantId;
        }

        // No tenant context - single-tenant mode or TenantMiddleware not active
        _logger.LogDebug("No tenant context found - operating in single-tenant mode");
        return null;
    }
}

/// <summary>
/// Response for batch location evaluation
/// </summary>
public class BatchEvaluateLocationResponse
{
    public List<EvaluateLocationResponse> Results { get; set; } = new();
    public int TotalProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public double TotalProcessingTimeMs { get; set; }
}
