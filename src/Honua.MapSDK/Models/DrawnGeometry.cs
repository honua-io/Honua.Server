using System.Text.Json.Serialization;
using Honua.MapSDK.Services.Drawing;
using ServicesDrawingStyle = Honua.MapSDK.Services.Drawing.DrawingStyle;

namespace Honua.MapSDK.Models;

/// <summary>
/// Represents a geometry drawn on the map
/// </summary>
public class DrawnGeometry
{
    /// <summary>
    /// Unique identifier for the geometry
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Geometry type
    /// </summary>
    public required GeometryType Type { get; set; }

    /// <summary>
    /// GeoJSON geometry object
    /// </summary>
    public required GeoJsonGeometry Geometry { get; set; }

    /// <summary>
    /// Display name
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Custom properties
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = new();

    /// <summary>
    /// Visual style
    /// </summary>
    public required ServicesDrawingStyle Style { get; set; }

    /// <summary>
    /// When the geometry was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the geometry was last modified
    /// </summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who created the geometry
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Whether the geometry is selected
    /// </summary>
    [JsonIgnore]
    public bool IsSelected { get; set; }

    /// <summary>
    /// Whether the geometry is being edited
    /// </summary>
    [JsonIgnore]
    public bool IsEditing { get; set; }

    /// <summary>
    /// Metadata for internal use
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Convert to GeoJSON Feature
    /// </summary>
    public GeoJsonFeature ToGeoJsonFeature()
    {
        return new GeoJsonFeature
        {
            Type = "Feature",
            Id = Id,
            Geometry = Geometry,
            Properties = new Dictionary<string, object>(Properties)
            {
                ["name"] = Name ?? "",
                ["description"] = Description ?? "",
                ["type"] = Type.ToString(),
                ["createdAt"] = CreatedAt,
                ["modifiedAt"] = ModifiedAt,
                ["createdBy"] = CreatedBy ?? ""
            }
        };
    }
}

/// <summary>
/// Geometry types supported by the drawing system
/// </summary>
public enum GeometryType
{
    Point,
    LineString,
    Polygon,
    MultiPoint,
    MultiLineString,
    MultiPolygon,
    Circle // Special case, stored as Point with radius property
}

/// <summary>
/// GeoJSON geometry object
/// </summary>
public class GeoJsonGeometry
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("coordinates")]
    public required object Coordinates { get; set; }

    /// <summary>
    /// For Circle geometry type - radius in meters
    /// </summary>
    [JsonPropertyName("radius")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Radius { get; set; }
}

/// <summary>
/// GeoJSON Feature
/// </summary>
public class GeoJsonFeature
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    [JsonPropertyName("geometry")]
    public required GeoJsonGeometry Geometry { get; set; }

    [JsonPropertyName("properties")]
    public Dictionary<string, object> Properties { get; set; } = new();
}

/// <summary>
/// GeoJSON FeatureCollection
/// </summary>
public class GeoJsonFeatureCollection
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "FeatureCollection";

    [JsonPropertyName("features")]
    public List<GeoJsonFeature> Features { get; set; } = new();
}
