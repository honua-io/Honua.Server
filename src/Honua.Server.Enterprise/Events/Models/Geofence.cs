// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using NetTopologySuite.Geometries;

namespace Honua.Server.Enterprise.Events.Models;

/// <summary>
/// Represents a geofence boundary for spatial event detection
/// </summary>
public class Geofence
{
    /// <summary>
    /// Unique identifier for the geofence
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Display name for the geofence
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Polygon geometry (SRID 4326 - WGS84)
    /// </summary>
    public Polygon Geometry { get; set; } = null!;

    /// <summary>
    /// Additional metadata as JSON
    /// </summary>
    public Dictionary<string, object>? Properties { get; set; }

    /// <summary>
    /// Event types to generate for this geofence
    /// </summary>
    public GeofenceEventTypes EnabledEventTypes { get; set; } = GeofenceEventTypes.Enter | GeofenceEventTypes.Exit;

    /// <summary>
    /// Whether this geofence is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional tenant ID for multi-tenancy
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// When the geofence was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the geofence was last modified
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Who created the geofence
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Who last modified the geofence
    /// </summary>
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Types of geofence events that can be generated
/// </summary>
[Flags]
public enum GeofenceEventTypes
{
    /// <summary>
    /// No events
    /// </summary>
    None = 0,

    /// <summary>
    /// Entity enters geofence
    /// </summary>
    Enter = 1,

    /// <summary>
    /// Entity exits geofence
    /// </summary>
    Exit = 2,

    /// <summary>
    /// Entity dwells inside geofence for specified duration (Phase 2)
    /// </summary>
    Dwell = 4,

    /// <summary>
    /// Entity approaches within buffer distance (Phase 2)
    /// </summary>
    Approach = 8
}
