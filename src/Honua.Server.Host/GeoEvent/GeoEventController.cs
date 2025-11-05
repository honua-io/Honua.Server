using Honua.Server.Enterprise.Events.Dto;
using Honua.Server.Enterprise.Events.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Geometries;

namespace Honua.Server.Host.GeoEvent;

/// <summary>
/// API endpoints for geofence event processing
/// </summary>
[ApiController]
[Route("api/v1/geoevent")]
[Authorize] // Require authentication
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
    /// Evaluate a location against all active geofences
    /// </summary>
    /// <param name="request">Location evaluation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Evaluation result with generated events</returns>
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
                request.EntityType,
                request.Properties,
                request.SensorThingsObservationId,
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
    /// <param name="requests">List of location evaluation requests</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of evaluation results</returns>
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
                        request.EntityType,
                        request.Properties,
                        request.SensorThingsObservationId,
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

    private string? GetTenantId()
    {
        // TODO: Extract tenant ID from claims or context
        // For now, return null (single-tenant mode)
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
