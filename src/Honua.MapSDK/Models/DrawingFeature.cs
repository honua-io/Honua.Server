namespace Honua.MapSDK.Models;

/// <summary>
/// Represents a drawn feature on the map with geometry, style, and measurements
/// </summary>
public class DrawingFeature
{
    /// <summary>
    /// Unique identifier for the feature
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Feature name/label
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Geometry type (Point, LineString, Polygon, Circle, Rectangle)
    /// </summary>
    public required string GeometryType { get; set; }

    /// <summary>
    /// GeoJSON geometry object
    /// </summary>
    public required object Geometry { get; set; }

    /// <summary>
    /// Feature properties
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = new();

    /// <summary>
    /// Drawing style for this feature
    /// </summary>
    public DrawingStyle? Style { get; set; }

    /// <summary>
    /// Measurements associated with this feature
    /// </summary>
    public FeatureMeasurements? Measurements { get; set; }

    /// <summary>
    /// Whether this feature is currently selected
    /// </summary>
    public bool IsSelected { get; set; }

    /// <summary>
    /// Whether this feature is visible
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Whether this feature is locked from editing
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// Date and time when feature was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date and time when feature was last modified
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    /// Optional text annotation
    /// </summary>
    public string? Annotation { get; set; }
}

/// <summary>
/// Measurements calculated for a drawn feature
/// </summary>
public class FeatureMeasurements
{
    /// <summary>
    /// Distance in meters (for lines)
    /// </summary>
    public double? Distance { get; set; }

    /// <summary>
    /// Area in square meters (for polygons)
    /// </summary>
    public double? Area { get; set; }

    /// <summary>
    /// Perimeter in meters (for polygons)
    /// </summary>
    public double? Perimeter { get; set; }

    /// <summary>
    /// Radius in meters (for circles)
    /// </summary>
    public double? Radius { get; set; }

    /// <summary>
    /// Bearing in degrees (for lines)
    /// </summary>
    public double? Bearing { get; set; }

    /// <summary>
    /// Coordinates [longitude, latitude] (for points)
    /// </summary>
    public double[]? Coordinates { get; set; }

    /// <summary>
    /// Formatted measurement string for display
    /// </summary>
    public string? DisplayText { get; set; }
}

/// <summary>
/// Drawing modes available in the component
/// </summary>
public enum DrawMode
{
    None,
    Point,
    Line,
    Polygon,
    Circle,
    Rectangle,
    Freehand,
    Text,
    Select,
    Edit
}
