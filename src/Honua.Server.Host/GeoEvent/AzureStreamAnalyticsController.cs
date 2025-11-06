using Honua.Server.Enterprise.Events.Dto;
using Honua.Server.Enterprise.Events.Services;
using Honua.Server.Enterprise.Multitenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Geometries;

namespace Honua.Server.Host.GeoEvent;

/// <summary>
/// Webhook receiver for Azure Stream Analytics geospatial output
/// </summary>
/// <remarks>
/// This endpoint receives location events from Azure Stream Analytics jobs
/// and evaluates them against active geofences.
///
/// **Configuration in Azure Stream Analytics:**
///
/// 1. Add HTTP Output:
///    - Output alias: honua-geoevent
///    - URL: https://your-server/api/v1/azure-sa/webhook
///    - Authentication: Bearer token
///    - Batch size: 100-1000 events
///
/// 2. Query example:
/// <code>
/// SELECT
///     deviceId as entity_id,
///     'iot_device' as entity_type,
///     location.lon as longitude,
///     location.lat as latitude,
///     EventEnqueuedUtcTime as event_time,
///     temperature,
///     speed
/// INTO [honua-geoevent]
/// FROM [iothub-input]
/// WHERE location.lon IS NOT NULL AND location.lat IS NOT NULL
/// </code>
///
/// **Performance**: Designed to handle batches of up to 1,000 events from Stream Analytics.
/// </remarks>
[ApiController]
[Route("api/v1/azure-sa")]
[Authorize]
[Produces("application/json")]
[Tags("Azure Integration")]
public class AzureStreamAnalyticsController : ControllerBase
{
    private readonly IGeofenceEvaluationService _evaluationService;
    private readonly ILogger<AzureStreamAnalyticsController> _logger;

    public AzureStreamAnalyticsController(
        IGeofenceEvaluationService evaluationService,
        ILogger<AzureStreamAnalyticsController> logger)
    {
        _evaluationService = evaluationService;
        _logger = logger;
    }

    /// <summary>
    /// Receive location events from Azure Stream Analytics
    /// </summary>
    /// <param name="batch">Batch of location events from Stream Analytics</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing result with success/failure counts</returns>
    /// <remarks>
    /// Sample request from Azure Stream Analytics:
    ///
    ///     POST /api/v1/azure-sa/webhook
    ///     {
    ///       "events": [
    ///         {
    ///           "entity_id": "device-123",
    ///           "entity_type": "iot_device",
    ///           "longitude": -122.4194,
    ///           "latitude": 37.7749,
    ///           "event_time": "2025-11-05T10:30:00Z",
    ///           "properties": {
    ///             "temperature": 72.5,
    ///             "speed": 45.3,
    ///             "battery": 85
    ///           }
    ///         }
    ///       ],
    ///       "metadata": {
    ///         "job_name": "geoevent-processor",
    ///         "output_name": "honua-geoevent"
    ///       }
    ///     }
    ///
    /// **Processing**:
    /// - Each event is evaluated against all active geofences
    /// - Enter/Exit events are generated and persisted
    /// - Failures for individual events don't fail the entire batch
    ///
    /// **Authentication**: Requires Bearer token in Authorization header
    /// </remarks>
    /// <response code="200">Batch processed successfully. Returns counts of processed/failed events.</response>
    /// <response code="400">Invalid batch format or empty events array</response>
    /// <response code="401">Unauthorized - authentication required</response>
    [HttpPost("webhook")]
    [ProducesResponseType(typeof(AzureStreamAnalyticsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AzureStreamAnalyticsResponse>> ReceiveWebhook(
        [FromBody] AzureStreamAnalyticsBatch batch,
        CancellationToken cancellationToken)
    {
        if (batch?.Events == null || !batch.Events.Any())
        {
            return BadRequest(new { error = "Events array is required and cannot be empty" });
        }

        if (batch.Events.Count > 1000)
        {
            return BadRequest(new { error = "Maximum 1000 events per batch" });
        }

        var tenantId = GetTenantId();
        var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
        var startTime = DateTime.UtcNow;

        var processedCount = 0;
        var failedCount = 0;
        var eventsGeneratedCount = 0;
        var errors = new List<string>();

        _logger.LogInformation(
            "Processing Azure Stream Analytics batch: {EventCount} events from job '{JobName}'",
            batch.Events.Count,
            batch.Metadata?.JobName ?? "unknown");

        foreach (var asaEvent in batch.Events)
        {
            try
            {
                // Validate event
                if (string.IsNullOrWhiteSpace(asaEvent.EntityId))
                {
                    errors.Add($"Event missing entity_id");
                    failedCount++;
                    continue;
                }

                // Validate coordinates
                if (asaEvent.Longitude < -180 || asaEvent.Longitude > 180 ||
                    asaEvent.Latitude < -90 || asaEvent.Latitude > 90)
                {
                    errors.Add($"Invalid coordinates for entity {asaEvent.EntityId}: [{asaEvent.Longitude}, {asaEvent.Latitude}]");
                    failedCount++;
                    continue;
                }

                // Create location point
                var location = geometryFactory.CreatePoint(
                    new Coordinate(asaEvent.Longitude, asaEvent.Latitude));

                var eventTime = asaEvent.EventTime ?? DateTime.UtcNow;

                // Evaluate location against geofences
                var result = await _evaluationService.EvaluateLocationAsync(
                    asaEvent.EntityId,
                    location,
                    eventTime,
                    asaEvent.Properties,
                    asaEvent.EntityType,
                    tenantId,
                    cancellationToken);

                processedCount++;
                eventsGeneratedCount += result.Events.Count;

                if (result.Events.Any())
                {
                    _logger.LogDebug(
                        "Entity {EntityId} generated {EventCount} geofence events",
                        asaEvent.EntityId,
                        result.Events.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event for entity {EntityId}", asaEvent.EntityId);
                errors.Add($"Error processing entity {asaEvent.EntityId}: {ex.Message}");
                failedCount++;
            }
        }

        var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

        _logger.LogInformation(
            "Azure SA batch processed: {ProcessedCount} succeeded, {FailedCount} failed, {EventsGenerated} geofence events generated in {ProcessingTime}ms",
            processedCount,
            failedCount,
            eventsGeneratedCount,
            processingTime);

        var response = new AzureStreamAnalyticsResponse
        {
            ProcessedCount = processedCount,
            FailedCount = failedCount,
            EventsGeneratedCount = eventsGeneratedCount,
            ProcessingTimeMs = processingTime,
            Errors = errors.Any() ? errors : null
        };

        return Ok(response);
    }

    /// <summary>
    /// Receive single location event from Azure Stream Analytics
    /// </summary>
    /// <param name="asaEvent">Single location event</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Evaluation result</returns>
    /// <remarks>
    /// Simplified endpoint for single-event processing from Stream Analytics.
    /// For better performance with high volumes, use the batch webhook endpoint.
    /// </remarks>
    [HttpPost("webhook/single")]
    [ProducesResponseType(typeof(EvaluateLocationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<EvaluateLocationResponse>> ReceiveSingleEvent(
        [FromBody] AzureStreamAnalyticsEvent asaEvent,
        CancellationToken cancellationToken)
    {
        if (asaEvent == null)
        {
            return BadRequest(new { error = "Event is required" });
        }

        if (string.IsNullOrWhiteSpace(asaEvent.EntityId))
        {
            return BadRequest(new { error = "entity_id is required" });
        }

        // Validate coordinates
        if (asaEvent.Longitude < -180 || asaEvent.Longitude > 180 ||
            asaEvent.Latitude < -90 || asaEvent.Latitude > 90)
        {
            return BadRequest(new { error = "Invalid coordinates" });
        }

        var tenantId = GetTenantId();
        var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
        var location = geometryFactory.CreatePoint(new Coordinate(asaEvent.Longitude, asaEvent.Latitude));
        var eventTime = asaEvent.EventTime ?? DateTime.UtcNow;
        var startTime = DateTime.UtcNow;

        var result = await _evaluationService.EvaluateLocationAsync(
            asaEvent.EntityId,
            location,
            eventTime,
            asaEvent.Properties,
            asaEvent.EntityType,
            tenantId,
            cancellationToken);

        var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

        var response = new EvaluateLocationResponse
        {
            EntityId = asaEvent.EntityId,
            Location = new GeoJsonPoint
            {
                Coordinates = new[] { asaEvent.Longitude, asaEvent.Latitude }
            },
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
