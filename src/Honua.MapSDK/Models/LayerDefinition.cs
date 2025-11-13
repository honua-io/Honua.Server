// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Models;

/// <summary>
/// Base class for all layer definitions
/// </summary>
public abstract class LayerDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public required string Name { get; set; }

    public string? Description { get; set; }

    public required string Type { get; init; }

    public required string SourceId { get; set; }

    public bool Visible { get; set; } = true;

    public double Opacity { get; set; } = 1.0;

    public double? MinZoom { get; set; }

    public double? MaxZoom { get; set; }

    public int ZIndex { get; set; } = 0;

    public string? GroupId { get; set; }

    public Dictionary<string, object> Metadata { get; set; } = new();

    public string? Attribution { get; set; }

    public string? Legend { get; set; }

    public bool Interactive { get; set; } = true;

    public string? PopupTemplate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Vector layer (MVT/Mapbox Vector Tiles)
/// </summary>
public class VectorLayer : LayerDefinition
{
    public VectorLayer()
    {
        Type = "vector";
    }

    /// <summary>
    /// Source layer name within the vector tiles
    /// </summary>
    public string? SourceLayer { get; set; }

    /// <summary>
    /// Filter expression (MapLibre filter spec)
    /// </summary>
    public object? Filter { get; set; }

    /// <summary>
    /// Paint properties (MapLibre paint spec)
    /// </summary>
    public Dictionary<string, object> Paint { get; set; } = new();

    /// <summary>
    /// Layout properties (MapLibre layout spec)
    /// </summary>
    public Dictionary<string, object> Layout { get; set; } = new();
}

/// <summary>
/// Raster layer (PNG/JPEG tiles)
/// </summary>
public class RasterLayer : LayerDefinition
{
    public RasterLayer()
    {
        Type = "raster";
    }

    /// <summary>
    /// Raster brightness min (-1 to 1)
    /// </summary>
    public double BrightnessMin { get; set; } = 0;

    /// <summary>
    /// Raster brightness max (-1 to 1)
    /// </summary>
    public double BrightnessMax { get; set; } = 1;

    /// <summary>
    /// Raster contrast (-1 to 1)
    /// </summary>
    public double Contrast { get; set; } = 0;

    /// <summary>
    /// Raster saturation (-1 to 1)
    /// </summary>
    public double Saturation { get; set; } = 0;

    /// <summary>
    /// Raster hue rotation (0 to 360 degrees)
    /// </summary>
    public double HueRotate { get; set; } = 0;

    /// <summary>
    /// Resampling method: "linear" or "nearest"
    /// </summary>
    public string Resampling { get; set; } = "linear";
}

/// <summary>
/// WMS layer (Web Map Service)
/// </summary>
public class WmsLayer : LayerDefinition
{
    public WmsLayer()
    {
        Type = "raster";
    }

    /// <summary>
    /// WMS-specific styling parameters
    /// </summary>
    public string? Styles { get; set; }

    /// <summary>
    /// Time parameter for temporal WMS
    /// </summary>
    public string? Time { get; set; }

    /// <summary>
    /// Elevation parameter
    /// </summary>
    public string? Elevation { get; set; }

    /// <summary>
    /// Additional WMS parameters
    /// </summary>
    public Dictionary<string, string> Parameters { get; set; } = new();
}

/// <summary>
/// WFS layer (Web Feature Service)
/// </summary>
public class WfsLayer : LayerDefinition
{
    public WfsLayer()
    {
        Type = "geojson";
    }

    /// <summary>
    /// Geometry type: "point", "line", "polygon"
    /// </summary>
    public string GeometryType { get; set; } = "point";

    /// <summary>
    /// Style for features
    /// </summary>
    public FeatureStyle Style { get; set; } = new();

    /// <summary>
    /// Fields to display in popup
    /// </summary>
    public string[]? PopupFields { get; set; }

    /// <summary>
    /// Label field
    /// </summary>
    public string? LabelField { get; set; }

    /// <summary>
    /// Enable clustering for point features
    /// </summary>
    public bool EnableClustering { get; set; } = false;
}

/// <summary>
/// GeoJSON layer (client-side vector data)
/// </summary>
public class GeoJsonLayer : LayerDefinition
{
    public GeoJsonLayer()
    {
        Type = "geojson";
    }

    /// <summary>
    /// Geometry type
    /// </summary>
    public string GeometryType { get; set; } = "point";

    /// <summary>
    /// Style configuration
    /// </summary>
    public FeatureStyle Style { get; set; } = new();

    /// <summary>
    /// Fields to show in popup
    /// </summary>
    public string[]? PopupFields { get; set; }

    /// <summary>
    /// Label configuration
    /// </summary>
    public LabelStyle? Labels { get; set; }

    /// <summary>
    /// Enable clustering for points
    /// </summary>
    public bool EnableClustering { get; set; } = false;

    /// <summary>
    /// Cluster style
    /// </summary>
    public LayerClusterStyle? ClusterStyle { get; set; }
}

/// <summary>
/// Image layer (static georeferenced image)
/// </summary>
public class ImageLayer : LayerDefinition
{
    public ImageLayer()
    {
        Type = "image";
    }

    /// <summary>
    /// Fade duration when zooming (milliseconds)
    /// </summary>
    public int FadeDuration { get; set; } = 300;
}

/// <summary>
/// Heatmap layer
/// </summary>
public class HeatmapLayer : LayerDefinition
{
    public HeatmapLayer()
    {
        Type = "heatmap";
    }

    /// <summary>
    /// Heatmap radius in pixels
    /// </summary>
    public double Radius { get; set; } = 30;

    /// <summary>
    /// Heatmap weight expression or property
    /// </summary>
    public object Weight { get; set; } = 1;

    /// <summary>
    /// Heatmap intensity
    /// </summary>
    public double Intensity { get; set; } = 1;

    /// <summary>
    /// Color gradient stops
    /// </summary>
    public Dictionary<double, string> ColorStops { get; set; } = new()
    {
        { 0, "rgba(0, 0, 255, 0)" },
        { 0.2, "rgb(0, 255, 0)" },
        { 0.4, "rgb(255, 255, 0)" },
        { 0.6, "rgb(255, 165, 0)" },
        { 1, "rgb(255, 0, 0)" }
    };
}

/// <summary>
/// 3D extrusion layer (fill-extrusion)
/// </summary>
public class ExtrusionLayer : LayerDefinition
{
    public ExtrusionLayer()
    {
        Type = "fill-extrusion";
    }

    /// <summary>
    /// Source layer for vector tiles
    /// </summary>
    public string? SourceLayer { get; set; }

    /// <summary>
    /// Extrusion height (in meters or expression)
    /// </summary>
    public object Height { get; set; } = 10;

    /// <summary>
    /// Extrusion base height
    /// </summary>
    public object BaseHeight { get; set; } = 0;

    /// <summary>
    /// Extrusion color
    /// </summary>
    public object Color { get; set; } = "#888888";

    /// <summary>
    /// Extrusion pattern texture
    /// </summary>
    public string? Pattern { get; set; }

    /// <summary>
    /// Vertical gradient for walls
    /// </summary>
    public bool VerticalGradient { get; set; } = true;
}

/// <summary>
/// Style configuration for vector features
/// </summary>
public class FeatureStyle
{
    // Point styling
    public string? CircleColor { get; set; } = "#3388ff";
    public double? CircleRadius { get; set; } = 6;
    public double? CircleOpacity { get; set; } = 1.0;
    public string? CircleStrokeColor { get; set; } = "#ffffff";
    public double? CircleStrokeWidth { get; set; } = 2;
    public double? CircleStrokeOpacity { get; set; } = 1.0;

    // Line styling
    public string? LineColor { get; set; } = "#3388ff";
    public double? LineWidth { get; set; } = 3;
    public double? LineOpacity { get; set; } = 1.0;
    public string? LineJoin { get; set; } = "round"; // "bevel", "round", "miter"
    public string? LineCap { get; set; } = "round"; // "butt", "round", "square"
    public double[]? LineDasharray { get; set; }

    // Polygon styling
    public string? FillColor { get; set; } = "#3388ff";
    public double? FillOpacity { get; set; } = 0.5;
    public string? FillOutlineColor { get; set; } = "#3388ff";
    public string? FillPattern { get; set; }

    // Icon styling (for symbols)
    public string? IconImage { get; set; }
    public double? IconSize { get; set; } = 1;
    public double? IconOpacity { get; set; } = 1.0;
    public double[]? IconOffset { get; set; }
    public double? IconRotation { get; set; } = 0;

    // Text styling
    public string? TextColor { get; set; } = "#000000";
    public double? TextSize { get; set; } = 12;
    public string? TextFont { get; set; }
    public string? TextHaloColor { get; set; } = "#ffffff";
    public double? TextHaloWidth { get; set; } = 1;
}

/// <summary>
/// Label styling configuration
/// </summary>
public class LabelStyle
{
    public required string TextField { get; set; }
    public double Size { get; set; } = 12;
    public string Color { get; set; } = "#000000";
    public string? Font { get; set; }
    public string HaloColor { get; set; } = "#ffffff";
    public double HaloWidth { get; set; } = 2;
    public double[]? Offset { get; set; }
    public string Anchor { get; set; } = "center";
    public bool AllowOverlap { get; set; } = false;
    public double MinZoom { get; set; } = 0;
    public double MaxZoom { get; set; } = 22;
}

/// <summary>
/// Cluster styling configuration for layer definitions
/// </summary>
public class LayerClusterStyle
{
    public string CircleColor { get; set; } = "#51bbd6";
    public double CircleRadius { get; set; } = 20;
    public string TextColor { get; set; } = "#ffffff";
    public double TextSize { get; set; } = 12;

    /// <summary>
    /// Graduated colors based on point count
    /// </summary>
    public List<ClusterColorStop>? ColorStops { get; set; }
}

/// <summary>
/// Cluster color stop for graduated symbols
/// </summary>
public class ClusterColorStop
{
    public int MinCount { get; set; }
    public string Color { get; set; } = "#51bbd6";
    public double Radius { get; set; } = 20;
}
