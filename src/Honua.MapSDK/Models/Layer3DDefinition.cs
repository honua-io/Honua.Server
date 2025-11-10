// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Models;

/// <summary>
/// Extended layer definition for 3D geometries with Deck.gl rendering support.
/// Provides configuration for 3D visualization, elevation handling, and rendering options.
/// </summary>
public class Layer3DDefinition : GeoJsonLayer
{
    /// <summary>
    /// Indicates whether the layer contains 3D geometries with Z coordinates.
    /// </summary>
    public bool HasZ { get; set; } = true;

    /// <summary>
    /// Indicates whether the layer contains geometries with M (measure) coordinates.
    /// </summary>
    public bool HasM { get; set; } = false;

    /// <summary>
    /// Rendering engine to use for this layer.
    /// "deck.gl" for 3D rendering, "maplibre" for standard 2D rendering.
    /// </summary>
    public string RenderEngine { get; set; } = "deck.gl";

    /// <summary>
    /// 3D rendering options specific to Deck.gl.
    /// </summary>
    public Deck3DOptions DeckOptions { get; set; } = new();

    /// <summary>
    /// Elevation configuration for 3D features.
    /// </summary>
    public ElevationConfig Elevation { get; set; } = new();

    /// <summary>
    /// Camera configuration for optimal 3D viewing.
    /// </summary>
    public Camera3DConfig? Camera3D { get; set; }

    /// <summary>
    /// Minimum Z (elevation) value in the dataset (meters).
    /// Used for performance optimization and data validation.
    /// </summary>
    public double? ZMin { get; set; }

    /// <summary>
    /// Maximum Z (elevation) value in the dataset (meters).
    /// </summary>
    public double? ZMax { get; set; }

    /// <summary>
    /// Constructor that sets appropriate defaults for 3D layers.
    /// </summary>
    public Layer3DDefinition()
    {
        Type = "geojson-3d";
    }
}

/// <summary>
/// Deck.gl-specific rendering options for 3D layers.
/// </summary>
public class Deck3DOptions
{
    /// <summary>
    /// Enable extrusion for polygon features (3D buildings, etc.)
    /// </summary>
    public bool Extruded { get; set; } = true;

    /// <summary>
    /// Show wireframe for extruded features.
    /// </summary>
    public bool Wireframe { get; set; } = false;

    /// <summary>
    /// Fill color for 3D features [R, G, B, A].
    /// Can be overridden per-feature using expressions.
    /// </summary>
    public int[]? FillColor { get; set; } = new[] { 160, 160, 180, 200 };

    /// <summary>
    /// Line color for feature outlines [R, G, B, A].
    /// </summary>
    public int[]? LineColor { get; set; } = new[] { 80, 80, 80, 255 };

    /// <summary>
    /// Line width in pixels.
    /// </summary>
    public double LineWidth { get; set; } = 1;

    /// <summary>
    /// Point radius for point features (pixels).
    /// </summary>
    public double PointRadius { get; set; } = 5;

    /// <summary>
    /// Enable feature picking (click/hover interactions).
    /// </summary>
    public bool Pickable { get; set; } = true;

    /// <summary>
    /// Auto-highlight features on hover.
    /// </summary>
    public bool AutoHighlight { get; set; } = true;

    /// <summary>
    /// Highlight color [R, G, B, A].
    /// </summary>
    public int[]? HighlightColor { get; set; } = new[] { 255, 255, 0, 100 };

    /// <summary>
    /// Material properties for 3D lighting.
    /// </summary>
    public MaterialProperties? Material { get; set; }

    /// <summary>
    /// Expression to get elevation value from feature properties.
    /// Example: "feature.properties.elevation"
    /// </summary>
    public string? ElevationExpression { get; set; }

    /// <summary>
    /// Expression to get height (extrusion) value from feature properties.
    /// Example: "feature.properties.building_height"
    /// </summary>
    public string? HeightExpression { get; set; }

    /// <summary>
    /// Enable GPU-based aggregation for large point datasets.
    /// </summary>
    public bool GpuAggregation { get; set; } = false;
}

/// <summary>
/// Elevation configuration for 3D features.
/// </summary>
public class ElevationConfig
{
    /// <summary>
    /// Property name containing elevation values.
    /// If null, Z coordinate from geometry is used.
    /// </summary>
    public string? PropertyName { get; set; }

    /// <summary>
    /// Base elevation to add to all features (meters).
    /// </summary>
    public double BaseElevation { get; set; } = 0;

    /// <summary>
    /// Vertical exaggeration factor.
    /// 1.0 = true elevation, 2.0 = double height, etc.
    /// </summary>
    public double VerticalExaggeration { get; set; } = 1.0;

    /// <summary>
    /// Elevation units in source data.
    /// "meters", "feet", "units" (data units)
    /// </summary>
    public string Units { get; set; } = "meters";

    /// <summary>
    /// Clamp elevation values to this range.
    /// Useful for filtering outliers.
    /// </summary>
    public double[]? ClampRange { get; set; }

    /// <summary>
    /// Use terrain elevation for features without Z coordinates.
    /// </summary>
    public bool UseTerrainElevation { get; set; } = false;
}

/// <summary>
/// Camera configuration optimized for 3D viewing.
/// </summary>
public class Camera3DConfig
{
    /// <summary>
    /// Default pitch angle in degrees (0-85).
    /// 0 = top-down, 60 = oblique view.
    /// </summary>
    public double Pitch { get; set; } = 45;

    /// <summary>
    /// Default bearing (rotation) in degrees (0-360).
    /// </summary>
    public double Bearing { get; set; } = 0;

    /// <summary>
    /// Default zoom level for the layer.
    /// </summary>
    public double? Zoom { get; set; }

    /// <summary>
    /// Auto-adjust camera to fit layer bounds.
    /// </summary>
    public bool AutoFit { get; set; } = false;

    /// <summary>
    /// Center point [longitude, latitude] for camera.
    /// </summary>
    public double[]? Center { get; set; }

    /// <summary>
    /// Padding around bounds when auto-fitting (pixels).
    /// </summary>
    public int FitPadding { get; set; } = 50;
}

/// <summary>
/// Material properties for 3D lighting and appearance.
/// </summary>
public class MaterialProperties
{
    /// <summary>
    /// Ambient light factor (0-1).
    /// </summary>
    public double Ambient { get; set; } = 0.35;

    /// <summary>
    /// Diffuse light factor (0-1).
    /// </summary>
    public double Diffuse { get; set; } = 0.6;

    /// <summary>
    /// Specular light factor (0-1).
    /// </summary>
    public double Specular { get; set; } = 0.8;

    /// <summary>
    /// Shininess factor (1-128).
    /// Higher values = smaller, sharper highlights.
    /// </summary>
    public double Shininess { get; set; } = 32;
}

/// <summary>
/// Options for 3D point cloud layers (millions of points).
/// </summary>
public class PointCloud3DLayer : Layer3DDefinition
{
    /// <summary>
    /// Point size in pixels.
    /// </summary>
    public double PointSize { get; set; } = 2;

    /// <summary>
    /// Color by property name (e.g., "intensity", "classification").
    /// </summary>
    public string? ColorByProperty { get; set; }

    /// <summary>
    /// Color ramp for gradient coloring.
    /// </summary>
    public ColorRamp? ColorRamp { get; set; }

    /// <summary>
    /// Enable Level of Detail (LOD) for performance.
    /// </summary>
    public bool EnableLOD { get; set; } = true;

    /// <summary>
    /// Maximum number of points to render at once.
    /// </summary>
    public int MaxPoints { get; set; } = 1_000_000;

    /// <summary>
    /// Constructor for point cloud layers.
    /// </summary>
    public PointCloud3DLayer()
    {
        Type = "pointcloud-3d";
        GeometryType = "point";
    }
}

/// <summary>
/// Color ramp definition for gradient coloring.
/// </summary>
public class ColorRamp
{
    /// <summary>
    /// Minimum value for color mapping.
    /// </summary>
    public double Min { get; set; }

    /// <summary>
    /// Maximum value for color mapping.
    /// </summary>
    public double Max { get; set; }

    /// <summary>
    /// Color stops [value, color] pairs.
    /// Color format: [R, G, B] or [R, G, B, A].
    /// </summary>
    public List<ColorStop> Stops { get; set; } = new();
}

/// <summary>
/// Color stop for gradient mapping.
/// </summary>
public class ColorStop
{
    /// <summary>
    /// Value at this stop.
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// Color at this stop [R, G, B, A].
    /// </summary>
    public required int[] Color { get; set; }
}
