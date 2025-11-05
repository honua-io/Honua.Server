using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using NetTopologySuite.Geometries;

namespace Honua.Server.Enterprise.Events.Dto;

/// <summary>
/// Request to create a new geofence
/// </summary>
public class CreateGeofenceRequest
{
    /// <summary>
    /// Display name for the geofence
    /// </summary>
    [Required]
    [StringLength(255, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description
    /// </summary>
    [StringLength(2000)]
    public string? Description { get; set; }

    /// <summary>
    /// GeoJSON geometry (must be a Polygon)
    /// </summary>
    [Required]
    public GeoJsonGeometry Geometry { get; set; } = null!;

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object>? Properties { get; set; }

    /// <summary>
    /// Event types to enable (default: Enter | Exit)
    /// </summary>
    public string[]? EnabledEventTypes { get; set; }

    /// <summary>
    /// Whether the geofence is active (default: true)
    /// </summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// GeoJSON geometry wrapper for API requests
/// </summary>
public class GeoJsonGeometry
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "Polygon";

    [JsonPropertyName("coordinates")]
    public double[][][] Coordinates { get; set; } = null!;
}
