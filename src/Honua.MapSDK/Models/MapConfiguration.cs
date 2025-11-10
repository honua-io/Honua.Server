// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.MapSDK.Models;

/// <summary>
/// Complete map configuration that can be saved, loaded, and exported
/// </summary>
public class MapConfiguration
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public required string Name { get; set; }

    public string? Description { get; set; }

    public required MapSettings Settings { get; set; }

    public List<LayerConfiguration> Layers { get; set; } = new();

    public List<ControlConfiguration> Controls { get; set; } = new();

    public FilterConfiguration? Filters { get; set; }

    public Dictionary<string, object> Metadata { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string CreatedBy { get; set; } = string.Empty;
}

public class MapSettings
{
    /// <summary>
    /// Map style URL (e.g., "maplibre://honua/dark")
    /// </summary>
    public required string Style { get; set; }

    /// <summary>
    /// Initial center [longitude, latitude]
    /// </summary>
    public double[] Center { get; set; } = new[] { 0.0, 0.0 };

    /// <summary>
    /// Initial zoom level (0-22)
    /// </summary>
    public double Zoom { get; set; } = 2;

    /// <summary>
    /// Initial bearing (rotation) in degrees
    /// </summary>
    public double Bearing { get; set; } = 0;

    /// <summary>
    /// Initial pitch (tilt) in degrees (0-60)
    /// </summary>
    public double Pitch { get; set; } = 0;

    /// <summary>
    /// Projection: mercator or globe
    /// </summary>
    public string Projection { get; set; } = "mercator";

    /// <summary>
    /// Enable WebGPU rendering
    /// </summary>
    public bool EnableGPU { get; set; } = true;

    /// <summary>
    /// Bounding box to restrict map extent [west, south, east, north]
    /// </summary>
    public double[]? MaxBounds { get; set; }

    /// <summary>
    /// Min zoom level
    /// </summary>
    public double? MinZoom { get; set; }

    /// <summary>
    /// Max zoom level
    /// </summary>
    public double? MaxZoom { get; set; }
}

public class LayerConfiguration
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public required string Name { get; set; }

    public required LayerType Type { get; set; }

    /// <summary>
    /// Data source (e.g., "grpc://api.honua.io/parcels" or "wfs://server/layer")
    /// </summary>
    public required string Source { get; set; }

    /// <summary>
    /// Layer visibility
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// Layer opacity (0-1)
    /// </summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>
    /// Min zoom for layer visibility
    /// </summary>
    public double? MinZoom { get; set; }

    /// <summary>
    /// Max zoom for layer visibility
    /// </summary>
    public double? MaxZoom { get; set; }

    /// <summary>
    /// Layer styling (MapLibre style spec or simplified)
    /// </summary>
    public LayerStyle Style { get; set; } = new();

    /// <summary>
    /// Popup template (Mustache/Handlebars)
    /// </summary>
    public string? PopupTemplate { get; set; }

    /// <summary>
    /// Fields to include in features
    /// </summary>
    public string[]? Fields { get; set; }
}

public enum LayerType
{
    Vector,
    Raster,
    ThreeD,
    Heatmap,
    Cluster,
    Line,
    Fill,
    Symbol
}

public class LayerStyle
{
    /// <summary>
    /// Fill color (for polygons)
    /// Can be color string or MapLibre expression
    /// </summary>
    public object? FillColor { get; set; }

    /// <summary>
    /// Fill opacity (0-1)
    /// </summary>
    public double? FillOpacity { get; set; }

    /// <summary>
    /// Line color
    /// </summary>
    public object? LineColor { get; set; }

    /// <summary>
    /// Line width
    /// </summary>
    public double? LineWidth { get; set; }

    /// <summary>
    /// Circle radius (for points)
    /// </summary>
    public double? CircleRadius { get; set; }

    /// <summary>
    /// Circle color
    /// </summary>
    public object? CircleColor { get; set; }

    /// <summary>
    /// Extrusion height (for 3D)
    /// </summary>
    public object? ExtrusionHeight { get; set; }

    /// <summary>
    /// Heatmap settings
    /// </summary>
    public HeatmapStyleSimple? Heatmap { get; set; }
}

public class HeatmapStyleSimple
{
    public double Radius { get; set; } = 20;
    public double Intensity { get; set; } = 1.0;
    public string[] ColorRamp { get; set; } = new[] { "#0000ff", "#00ff00", "#ffff00", "#ff0000" };
}

public class ControlConfiguration
{
    public required ControlType Type { get; set; }

    public string Position { get; set; } = "top-right";

    public bool Visible { get; set; } = true;

    public Dictionary<string, object> Options { get; set; } = new();
}

public enum ControlType
{
    Navigation,
    Scale,
    Fullscreen,
    Geolocate,
    Legend,
    LayerList,
    Filter,
    Search,
    Measure,
    Draw,
    Timeline,
    BasemapGallery
}

public class FilterConfiguration
{
    public bool AllowSpatial { get; set; } = true;

    public bool AllowAttribute { get; set; } = true;

    public bool AllowTemporal { get; set; } = true;

    public List<FilterFieldDefinition> AvailableFilters { get; set; } = new();
}

public class FilterFieldDefinition
{
    public required string Field { get; set; }

    public required string Label { get; set; }

    public required FilterFieldType Type { get; set; }

    public object? DefaultValue { get; set; }

    public string[]? Options { get; set; }
}

public enum FilterFieldType
{
    Text,
    Number,
    Date,
    Select,
    MultiSelect,
    Range,
    Boolean
}
