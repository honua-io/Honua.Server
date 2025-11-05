namespace Honua.Server.Enterprise.Events.Models;

/// <summary>
/// Tracks the current state of an entity relative to geofences
/// Used for detecting enter/exit events
/// </summary>
public class EntityGeofenceState
{
    /// <summary>
    /// Composite key: entity_id + geofence_id
    /// </summary>
    public string Id => $"{EntityId}:{GeofenceId}";

    /// <summary>
    /// Entity identifier
    /// </summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// Geofence identifier
    /// </summary>
    public Guid GeofenceId { get; set; }

    /// <summary>
    /// Whether the entity is currently inside the geofence
    /// </summary>
    public bool IsInside { get; set; }

    /// <summary>
    /// When the entity entered the geofence
    /// </summary>
    public DateTime? EnteredAt { get; set; }

    /// <summary>
    /// Last location update timestamp
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Tenant ID for multi-tenancy
    /// </summary>
    public string? TenantId { get; set; }
}
