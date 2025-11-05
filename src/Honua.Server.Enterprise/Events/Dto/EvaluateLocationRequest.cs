using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Honua.Server.Enterprise.Events.Dto;

/// <summary>
/// Request to evaluate a location against geofences
/// </summary>
public class EvaluateLocationRequest
{
    /// <summary>
    /// Entity identifier
    /// </summary>
    [Required]
    [StringLength(255)]
    [JsonPropertyName("entity_id")]
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// Optional entity type
    /// </summary>
    [StringLength(100)]
    [JsonPropertyName("entity_type")]
    public string? EntityType { get; set; }

    /// <summary>
    /// Location to evaluate (GeoJSON Point)
    /// </summary>
    [Required]
    [JsonPropertyName("location")]
    public GeoJsonPoint Location { get; set; } = null!;

    /// <summary>
    /// When the event occurred (defaults to current time)
    /// </summary>
    [JsonPropertyName("event_time")]
    public DateTime? EventTime { get; set; }

    /// <summary>
    /// Additional properties from the source event
    /// </summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, object>? Properties { get; set; }
}

/// <summary>
/// GeoJSON Point geometry
/// </summary>
public class GeoJsonPoint
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "Point";

    /// <summary>
    /// Coordinates [longitude, latitude]
    /// </summary>
    [JsonPropertyName("coordinates")]
    [Required]
    [MinLength(2)]
    [MaxLength(2)]
    public double[] Coordinates { get; set; } = null!;
}

/// <summary>
/// Response from location evaluation
/// </summary>
public class EvaluateLocationResponse
{
    /// <summary>
    /// Entity ID that was evaluated
    /// </summary>
    [JsonPropertyName("entity_id")]
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// Events that were generated
    /// </summary>
    [JsonPropertyName("events")]
    public List<GeofenceEventResponse> Events { get; set; } = new();

    /// <summary>
    /// Current geofences the entity is inside
    /// </summary>
    [JsonPropertyName("current_geofences")]
    public List<Guid> CurrentGeofences { get; set; } = new();

    /// <summary>
    /// Processing time in milliseconds
    /// </summary>
    [JsonPropertyName("processing_time_ms")]
    public double ProcessingTimeMs { get; set; }
}

/// <summary>
/// Geofence event in API response
/// </summary>
public class GeofenceEventResponse
{
    [JsonPropertyName("event_id")]
    public Guid EventId { get; set; }

    [JsonPropertyName("event_type")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("event_time")]
    public DateTime EventTime { get; set; }

    [JsonPropertyName("geofence_id")]
    public Guid GeofenceId { get; set; }

    [JsonPropertyName("geofence_name")]
    public string GeofenceName { get; set; } = string.Empty;

    [JsonPropertyName("location")]
    public GeoJsonPoint Location { get; set; } = null!;

    [JsonPropertyName("properties")]
    public Dictionary<string, object>? Properties { get; set; }

    [JsonPropertyName("dwell_time_seconds")]
    public int? DwellTimeSeconds { get; set; }
}
