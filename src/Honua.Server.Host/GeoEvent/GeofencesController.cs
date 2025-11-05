using Honua.Server.Enterprise.Events.Dto;
using Honua.Server.Enterprise.Events.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.GeoEvent;

/// <summary>
/// API endpoints for geofence management (CRUD operations)
/// </summary>
/// <remarks>
/// Geofences are geographic boundaries that trigger events when entities enter or exit them.
/// This API allows you to create, read, update, and delete geofences.
///
/// For location evaluation and event generation, see the GeoEvent controller.
/// </remarks>
[ApiController]
[Route("api/v1/geofences")]
[Authorize] // Require authentication
[Produces("application/json")]
[Tags("GeoFencing")]
public class GeofencesController : ControllerBase
{
    private readonly IGeofenceManagementService _managementService;
    private readonly ILogger<GeofencesController> _logger;

    public GeofencesController(
        IGeofenceManagementService managementService,
        ILogger<GeofencesController> logger)
    {
        _managementService = managementService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new geofence
    /// </summary>
    /// <param name="request">Geofence details including name, geometry (GeoJSON Polygon), and event types</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created geofence with assigned ID</returns>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/v1/geofences
    ///     {
    ///       "name": "Downtown District",
    ///       "description": "Main downtown area",
    ///       "geometry": {
    ///         "type": "Polygon",
    ///         "coordinates": [[
    ///           [-122.4194, 37.7749],
    ///           [-122.4094, 37.7749],
    ///           [-122.4094, 37.7849],
    ///           [-122.4194, 37.7849],
    ///           [-122.4194, 37.7749]
    ///         ]]
    ///       },
    ///       "enabled_event_types": ["Enter", "Exit"],
    ///       "is_active": true,
    ///       "properties": {
    ///         "zone_type": "restricted",
    ///         "priority": "high"
    ///       }
    ///     }
    ///
    /// Enabled event types: Enter, Exit, Dwell (Phase 2), Approach (Phase 2)
    /// Geometry must be a valid GeoJSON Polygon with WGS84 coordinates (EPSG:4326)
    /// </remarks>
    /// <response code="201">Geofence created successfully. Location header contains URL to retrieve it.</response>
    /// <response code="400">Invalid request (e.g., invalid geometry, missing required fields)</response>
    /// <response code="401">Unauthorized - authentication required</response>
    [HttpPost]
    [ProducesResponseType(typeof(GeofenceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<GeofenceResponse>> CreateGeofence(
        [FromBody] CreateGeofenceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = GetTenantId();
            var createdBy = User.Identity?.Name;

            var geofence = await _managementService.CreateGeofenceAsync(
                request,
                createdBy,
                tenantId,
                cancellationToken);

            var response = MapToResponse(geofence);

            return CreatedAtAction(
                nameof(GetGeofence),
                new { id = geofence.Id },
                response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid geofence request");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get geofence by ID
    /// </summary>
    /// <param name="id">Geofence ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Geofence details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(GeofenceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<GeofenceResponse>> GetGeofence(
        Guid id,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();

        var geofence = await _managementService.GetGeofenceAsync(
            id,
            tenantId,
            cancellationToken);

        if (geofence == null)
        {
            return NotFound();
        }

        return Ok(MapToResponse(geofence));
    }

    /// <summary>
    /// List geofences with pagination and filtering
    /// </summary>
    /// <param name="isActive">Filter by active status (optional: true/false/null for all)</param>
    /// <param name="limit">Maximum number of results (1-1000, default 100)</param>
    /// <param name="offset">Number of records to skip for pagination (default 0)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of geofences with total count</returns>
    /// <remarks>
    /// Examples:
    /// - GET /api/v1/geofences - Get first 100 geofences
    /// - GET /api/v1/geofences?limit=50&amp;offset=100 - Get geofences 101-150
    /// - GET /api/v1/geofences?is_active=true - Get only active geofences
    /// </remarks>
    /// <response code="200">Successfully retrieved geofences</response>
    /// <response code="400">Invalid pagination parameters</response>
    /// <response code="401">Unauthorized - authentication required</response>
    [HttpGet]
    [ProducesResponseType(typeof(GeofenceListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<GeofenceListResponse>> ListGeofences(
        [FromQuery] bool? isActive = null,
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        // Validate pagination parameters
        if (limit < 1 || limit > 1000)
        {
            return BadRequest(new { error = "Limit must be between 1 and 1000" });
        }

        if (offset < 0)
        {
            return BadRequest(new { error = "Offset must be >= 0" });
        }

        var result = await _managementService.ListGeofencesAsync(
            isActive,
            tenantId,
            limit,
            offset,
            cancellationToken);

        var response = new GeofenceListResponse
        {
            Geofences = result.Geofences.Select(MapToResponse).ToList(),
            TotalCount = result.TotalCount,
            Limit = result.Limit,
            Offset = result.Offset
        };

        return Ok(response);
    }

    /// <summary>
    /// Update geofence
    /// </summary>
    /// <param name="id">Geofence ID</param>
    /// <param name="request">Updated geofence details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateGeofence(
        Guid id,
        [FromBody] CreateGeofenceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = GetTenantId();
            var updatedBy = User.Identity?.Name;

            var updated = await _managementService.UpdateGeofenceAsync(
                id,
                request,
                updatedBy,
                tenantId,
                cancellationToken);

            if (!updated)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid geofence update request");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete geofence
    /// </summary>
    /// <param name="id">Geofence ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteGeofence(
        Guid id,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();

        var deleted = await _managementService.DeleteGeofenceAsync(
            id,
            tenantId,
            cancellationToken);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    private string? GetTenantId()
    {
        // TODO: Extract tenant ID from claims or context
        // For now, return null (single-tenant mode)
        return null;
    }

    private GeofenceResponse MapToResponse(Enterprise.Events.Models.Geofence geofence)
    {
        // Convert NetTopologySuite Polygon to GeoJSON
        var coordinates = geofence.Geometry.ExteriorRing.Coordinates
            .Select(c => new[] { c.X, c.Y })
            .ToArray();

        var eventTypes = new List<string>();
        if (geofence.EnabledEventTypes.HasFlag(Enterprise.Events.Models.GeofenceEventTypes.Enter))
            eventTypes.Add("Enter");
        if (geofence.EnabledEventTypes.HasFlag(Enterprise.Events.Models.GeofenceEventTypes.Exit))
            eventTypes.Add("Exit");
        if (geofence.EnabledEventTypes.HasFlag(Enterprise.Events.Models.GeofenceEventTypes.Dwell))
            eventTypes.Add("Dwell");
        if (geofence.EnabledEventTypes.HasFlag(Enterprise.Events.Models.GeofenceEventTypes.Approach))
            eventTypes.Add("Approach");

        return new GeofenceResponse
        {
            Id = geofence.Id,
            Name = geofence.Name,
            Description = geofence.Description,
            Geometry = new GeoJsonGeometry
            {
                Type = "Polygon",
                Coordinates = new[] { coordinates }
            },
            Properties = geofence.Properties,
            EnabledEventTypes = eventTypes.ToArray(),
            IsActive = geofence.IsActive,
            CreatedAt = geofence.CreatedAt,
            UpdatedAt = geofence.UpdatedAt,
            CreatedBy = geofence.CreatedBy,
            UpdatedBy = geofence.UpdatedBy
        };
    }
}

/// <summary>
/// Response for geofence list
/// </summary>
public class GeofenceListResponse
{
    public List<GeofenceResponse> Geofences { get; set; } = new();
    public int TotalCount { get; set; }
    public int Limit { get; set; }
    public int Offset { get; set; }
}
