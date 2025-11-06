namespace Honua.MapSDK.Models;

/// <summary>
/// Content data for a popup display
/// </summary>
public class PopupContent
{
    /// <summary>
    /// Unique identifier of the feature
    /// </summary>
    public string FeatureId { get; set; } = string.Empty;

    /// <summary>
    /// Layer ID the feature belongs to
    /// </summary>
    public string LayerId { get; set; } = string.Empty;

    /// <summary>
    /// Feature properties/attributes
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = new();

    /// <summary>
    /// Feature geometry (GeoJSON format)
    /// </summary>
    public object? Geometry { get; set; }

    /// <summary>
    /// Coordinates where popup should be displayed [longitude, latitude]
    /// </summary>
    public double[]? Coordinates { get; set; }

    /// <summary>
    /// Template to use for rendering this popup
    /// </summary>
    public PopupTemplate? Template { get; set; }

    /// <summary>
    /// Timestamp when feature was selected
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
