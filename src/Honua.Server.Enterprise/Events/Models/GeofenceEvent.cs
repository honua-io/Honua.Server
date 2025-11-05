using NetTopologySuite.Geometries;

namespace Honua.Server.Enterprise.Events.Models;

/// <summary>
/// Represents a geofence event (enter, exit, etc.)
/// </summary>
public class GeofenceEvent
{
    /// <summary>
    /// Unique event identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Type of event
    /// </summary>
    public GeofenceEventType EventType { get; set; }

    /// <summary>
    /// When the event occurred
    /// </summary>
    public DateTime EventTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// ID of the geofence that triggered the event
    /// </summary>
    public Guid GeofenceId { get; set; }

    /// <summary>
    /// Name of the geofence (denormalized for convenience)
    /// </summary>
    public string GeofenceName { get; set; } = string.Empty;

    /// <summary>
    /// Identifier of the entity (vehicle, person, asset, etc.)
    /// </summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// Type of entity (optional)
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Location where the event occurred (Point, SRID 4326)
    /// </summary>
    public Point Location { get; set; } = null!;

    /// <summary>
    /// Entry/exit point on geofence boundary (optional)
    /// </summary>
    public Point? BoundaryPoint { get; set; }

    /// <summary>
    /// Additional event properties from the source event
    /// </summary>
    public Dictionary<string, object>? Properties { get; set; }

    /// <summary>
    /// Dwell time in seconds (for exit events)
    /// </summary>
    public int? DwellTimeSeconds { get; set; }

    /// <summary>
    /// Optional link to SensorThings observation ID
    /// </summary>
    public Guid? SensorThingsObservationId { get; set; }

    /// <summary>
    /// Tenant ID for multi-tenancy
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// When this event was processed
    /// </summary>
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Type of geofence event
/// </summary>
public enum GeofenceEventType
{
    /// <summary>
    /// Entity entered geofence
    /// </summary>
    Enter,

    /// <summary>
    /// Entity exited geofence
    /// </summary>
    Exit,

    /// <summary>
    /// Entity has dwelled inside geofence (Phase 2)
    /// </summary>
    Dwell,

    /// <summary>
    /// Entity is approaching geofence (Phase 2)
    /// </summary>
    Approach
}
