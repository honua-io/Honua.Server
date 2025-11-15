// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Enterprise.Events.Models;
using Honua.Server.Enterprise.Events.Queue.Models;
using Honua.Server.Enterprise.Events.Queue.Repositories;
using Honua.Server.Enterprise.Multitenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.GeoEvent;

/// <summary>
/// API endpoints for replaying and querying geofence events
/// </summary>
[ApiController]
[Route("api/v1/geoevent/replay")]
[Authorize]
[Produces("application/json")]
[Tags("GeoFencing")]
public class GeoEventReplayController : ControllerBase
{
    private readonly IGeofenceEventQueueRepository _queueRepository;
    private readonly ILogger<GeoEventReplayController> _logger;

    public GeoEventReplayController(
        IGeofenceEventQueueRepository queueRepository,
        ILogger<GeoEventReplayController> logger)
    {
        _queueRepository = queueRepository ?? throw new ArgumentNullException(nameof(queueRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Replay geofence events for time-travel queries
    /// </summary>
    /// <param name="request">Replay request parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of geofence events matching the criteria</returns>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/v1/geoevent/replay
    ///     {
    ///       "entity_id": "vehicle-123",
    ///       "start_time": "2025-11-14T00:00:00Z",
    ///       "end_time": "2025-11-14T23:59:59Z",
    ///       "event_types": ["enter", "exit"]
    ///     }
    ///
    /// Use Cases:
    /// - Reconstruct entity movement timeline
    /// - Audit trail for compliance
    /// - Debug geofencing behavior
    /// - Analytics and reporting
    /// </remarks>
    /// <response code="200">Events retrieved successfully</response>
    /// <response code="400">Invalid request parameters</response>
    /// <response code="401">Unauthorized - authentication required</response>
    [HttpPost]
    [ProducesResponseType(typeof(EventReplayResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<EventReplayResponse>> ReplayEvents(
        [FromBody] ReplayEventsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = GetTenantId();

            // Validate time range
            if (request.StartTime >= request.EndTime)
            {
                return this.BadRequest(new { error = "Start time must be before end time" });
            }

            var timeSpan = request.EndTime - request.StartTime;
            if (timeSpan.TotalDays > 90)
            {
                return this.BadRequest(new { error = "Time range cannot exceed 90 days" });
            }

            // Build repository request
            var replayRequest = new EventReplayRequest
            {
                EntityId = request.EntityId,
                GeofenceId = request.GeofenceId,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                EventTypes = request.EventTypes?.Select(t => Enum.Parse<GeofenceEventType>(t, ignoreCase: true)).ToList(),
                TenantId = tenantId
            };

            var startTime = DateTime.UtcNow;
            var events = await _queueRepository.ReplayEventsAsync(replayRequest, cancellationToken);
            var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

            var response = new EventReplayResponse
            {
                Events = events.Select(e => new ReplayedEvent
                {
                    Id = e.Id,
                    EventType = e.EventType.ToString(),
                    EventTime = e.EventTime,
                    GeofenceId = e.GeofenceId,
                    GeofenceName = e.GeofenceName,
                    EntityId = e.EntityId,
                    EntityType = e.EntityType,
                    Location = new LocationDto
                    {
                        Type = "Point",
                        Coordinates = new[] { e.Location.X, e.Location.Y }
                    },
                    Properties = e.Properties,
                    DwellTimeSeconds = e.DwellTimeSeconds
                }).ToList(),
                TotalEvents = events.Count,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                ProcessingTimeMs = processingTime
            };

            _logger.LogInformation(
                "Replayed {Count} geofence events for entity {EntityId} from {StartTime} to {EndTime}",
                events.Count,
                request.EntityId,
                request.StartTime,
                request.EndTime);

            return this.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error replaying geofence events");
            return this.StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get queue metrics for monitoring
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Queue metrics</returns>
    /// <response code="200">Metrics retrieved successfully</response>
    /// <response code="401">Unauthorized - authentication required</response>
    [HttpGet("metrics")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(QueueMetrics), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<QueueMetrics>> GetQueueMetrics(CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = GetTenantId();
            var metrics = await _queueRepository.GetQueueMetricsAsync(tenantId, cancellationToken);

            return this.Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving queue metrics");
            return this.StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get dead letter queue items
    /// </summary>
    /// <param name="limit">Maximum number of items to retrieve</param>
    /// <param name="offset">Offset for pagination</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of dead letter queue items</returns>
    /// <response code="200">Dead letter queue items retrieved successfully</response>
    /// <response code="401">Unauthorized - authentication required</response>
    [HttpGet("deadletter")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(DeadLetterQueueResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DeadLetterQueueResponse>> GetDeadLetterQueue(
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantId();
            var items = await _queueRepository.GetDeadLetterQueueAsync(limit, offset, tenantId, cancellationToken);

            var response = new DeadLetterQueueResponse
            {
                Items = items.Select(item => new DeadLetterQueueItemDto
                {
                    Id = item.Id,
                    GeofenceEventId = item.GeofenceEventId,
                    AttemptCount = item.AttemptCount,
                    MaxAttempts = item.MaxAttempts,
                    LastError = item.LastError,
                    CreatedAt = item.CreatedAt,
                    UpdatedAt = item.UpdatedAt
                }).ToList(),
                TotalItems = items.Count,
                Limit = limit,
                Offset = offset
            };

            return this.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dead letter queue");
            return this.StatusCode(500, new { error = "Internal server error" });
        }
    }

    private string? GetTenantId()
    {
        var tenantContext = HttpContext.GetTenantContext();
        return tenantContext?.TenantId;
    }
}

// DTOs for API responses

public class ReplayEventsRequest
{
    public string? EntityId { get; set; }
    public Guid? GeofenceId { get; set; }
    public DateTime StartTime { get; set; } = DateTime.UtcNow.AddHours(-24);
    public DateTime EndTime { get; set; } = DateTime.UtcNow;
    public List<string>? EventTypes { get; set; }
}

public class EventReplayResponse
{
    public List<ReplayedEvent> Events { get; set; } = new();
    public int TotalEvents { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double ProcessingTimeMs { get; set; }
}

public class ReplayedEvent
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime EventTime { get; set; }
    public Guid GeofenceId { get; set; }
    public string GeofenceName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public LocationDto Location { get; set; } = new();
    public Dictionary<string, object>? Properties { get; set; }
    public int? DwellTimeSeconds { get; set; }
}

public class LocationDto
{
    public string Type { get; set; } = "Point";
    public double[] Coordinates { get; set; } = Array.Empty<double>();
}

public class DeadLetterQueueResponse
{
    public List<DeadLetterQueueItemDto> Items { get; set; } = new();
    public int TotalItems { get; set; }
    public int Limit { get; set; }
    public int Offset { get; set; }
}

public class DeadLetterQueueItemDto
{
    public Guid Id { get; set; }
    public Guid GeofenceEventId { get; set; }
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
