namespace Honua.MapSDK.Models;

/// <summary>
/// Represents a layer in the map with its metadata and state
/// </summary>
public class LayerInfo
{
    /// <summary>
    /// Unique layer identifier
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Display name of the layer
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Layer type: fill, line, circle, symbol, raster, heatmap, fill-extrusion, background
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Source ID this layer uses
    /// </summary>
    public string? SourceId { get; set; }

    /// <summary>
    /// Source layer (for vector tiles)
    /// </summary>
    public string? SourceLayer { get; set; }

    /// <summary>
    /// Layer visibility
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// Layer opacity (0-1)
    /// </summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>
    /// Whether layer is locked (cannot be edited/removed)
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// Parent group/folder ID
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>
    /// Sort order (higher renders on top)
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Feature count (if available)
    /// </summary>
    public int? FeatureCount { get; set; }

    /// <summary>
    /// Minimum zoom level
    /// </summary>
    public double? MinZoom { get; set; }

    /// <summary>
    /// Maximum zoom level
    /// </summary>
    public double? MaxZoom { get; set; }

    /// <summary>
    /// Layer extent/bounding box [west, south, east, north]
    /// </summary>
    public double[]? Extent { get; set; }

    /// <summary>
    /// Legend items for this layer
    /// </summary>
    public List<LegendItem> LegendItems { get; set; } = new();

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Custom icon for layer type
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Description/tooltip text
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Data source attribution
    /// </summary>
    public string? Attribution { get; set; }

    /// <summary>
    /// Whether layer is a basemap layer
    /// </summary>
    public bool IsBasemap { get; set; }

    /// <summary>
    /// Whether layer can be removed by user
    /// </summary>
    public bool CanRemove { get; set; } = true;

    /// <summary>
    /// Whether layer name can be edited
    /// </summary>
    public bool CanRename { get; set; } = true;
}

/// <summary>
/// Represents a layer group/folder in the layer tree
/// </summary>
public class LayerGroup
{
    /// <summary>
    /// Unique group identifier
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Group display name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Parent group ID (null for root)
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// Whether group is expanded
    /// </summary>
    public bool IsExpanded { get; set; } = true;

    /// <summary>
    /// Whether all layers in group are visible
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// Group opacity (0-1)
    /// </summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>
    /// Sort order
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Custom icon for group
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Group description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether group is locked
    /// </summary>
    public bool IsLocked { get; set; }
}

/// <summary>
/// Legend item for a layer
/// </summary>
public class LegendItem
{
    /// <summary>
    /// Label for this legend item
    /// </summary>
    public required string Label { get; set; }

    /// <summary>
    /// Color swatch (hex color)
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Image URL for complex symbols
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Symbol type: circle, square, line, polygon, icon
    /// </summary>
    public string? SymbolType { get; set; }

    /// <summary>
    /// Symbol size in pixels
    /// </summary>
    public int? Size { get; set; }

    /// <summary>
    /// Stroke/outline color
    /// </summary>
    public string? StrokeColor { get; set; }

    /// <summary>
    /// Stroke width
    /// </summary>
    public double? StrokeWidth { get; set; }
}
