// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.MapSDK.Models;

/// <summary>
/// Leaflet map initialization options
/// </summary>
public class LeafletInitOptions
{
    /// <summary>
    /// Map container ID
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Tile layer URL template (e.g., "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png")
    /// </summary>
    public string TileUrl { get; set; } = "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png";

    /// <summary>
    /// Attribution text for the tile layer
    /// </summary>
    public string Attribution { get; set; } = "&copy; OpenStreetMap contributors";

    /// <summary>
    /// Initial center [latitude, longitude]
    /// </summary>
    public double[] Center { get; set; } = new[] { 0.0, 0.0 };

    /// <summary>
    /// Initial zoom level (0-22)
    /// </summary>
    public double Zoom { get; set; } = 2;

    /// <summary>
    /// Bounding box to restrict map extent [[south, west], [north, east]]
    /// </summary>
    public double[][]? MaxBounds { get; set; }

    /// <summary>
    /// Minimum zoom level
    /// </summary>
    public double? MinZoom { get; set; }

    /// <summary>
    /// Maximum zoom level
    /// </summary>
    public double? MaxZoom { get; set; }

    /// <summary>
    /// Enable fullscreen control
    /// </summary>
    public bool EnableFullscreen { get; set; } = true;

    /// <summary>
    /// Enable measure tools
    /// </summary>
    public bool EnableMeasure { get; set; } = false;

    /// <summary>
    /// Enable drawing tools
    /// </summary>
    public bool EnableDraw { get; set; } = false;

    /// <summary>
    /// Enable marker clustering
    /// </summary>
    public bool EnableMarkerCluster { get; set; } = false;

    /// <summary>
    /// Maximum cluster radius in pixels
    /// </summary>
    public int MaxClusterRadius { get; set; } = 80;

    /// <summary>
    /// Zoom control position
    /// </summary>
    public string ZoomControlPosition { get; set; } = "topright";

    /// <summary>
    /// Attribution control position
    /// </summary>
    public string AttributionControlPosition { get; set; } = "bottomright";
}

/// <summary>
/// Leaflet tile layer configuration
/// </summary>
public class LeafletTileLayer
{
    /// <summary>
    /// Layer identifier
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Tile URL template with {z}, {x}, {y} placeholders
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// Attribution text
    /// </summary>
    public string? Attribution { get; set; }

    /// <summary>
    /// Layer opacity (0-1)
    /// </summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>
    /// Z-index for layer ordering
    /// </summary>
    public int ZIndex { get; set; } = 0;

    /// <summary>
    /// Minimum zoom level for layer
    /// </summary>
    public double? MinZoom { get; set; }

    /// <summary>
    /// Maximum zoom level for layer
    /// </summary>
    public double? MaxZoom { get; set; }

    /// <summary>
    /// Subdomains for tile server
    /// </summary>
    public string[]? Subdomains { get; set; }

    /// <summary>
    /// Additional options
    /// </summary>
    public Dictionary<string, object>? Options { get; set; }
}

/// <summary>
/// Leaflet marker configuration
/// </summary>
public class LeafletMarker
{
    /// <summary>
    /// Marker identifier
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Position [latitude, longitude]
    /// </summary>
    public required double[] Position { get; set; }

    /// <summary>
    /// Marker title (tooltip)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Popup content (HTML)
    /// </summary>
    public string? PopupContent { get; set; }

    /// <summary>
    /// Icon URL
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    /// Icon size [width, height]
    /// </summary>
    public int[]? IconSize { get; set; }

    /// <summary>
    /// Icon anchor [x, y] relative to top-left
    /// </summary>
    public int[]? IconAnchor { get; set; }

    /// <summary>
    /// Popup anchor [x, y] relative to icon anchor
    /// </summary>
    public int[]? PopupAnchor { get; set; }

    /// <summary>
    /// Whether marker is draggable
    /// </summary>
    public bool Draggable { get; set; } = false;

    /// <summary>
    /// Marker opacity (0-1)
    /// </summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>
    /// Z-index offset
    /// </summary>
    public int ZIndexOffset { get; set; } = 0;

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Leaflet GeoJSON layer configuration
/// </summary>
public class LeafletGeoJsonLayer
{
    /// <summary>
    /// Layer identifier
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// GeoJSON data (as object or string)
    /// </summary>
    public required object GeoJson { get; set; }

    /// <summary>
    /// Layer style
    /// </summary>
    public LeafletLayerStyle? Style { get; set; }

    /// <summary>
    /// Point to layer function name (for custom marker rendering)
    /// </summary>
    public string? PointToLayerFunction { get; set; }

    /// <summary>
    /// On each feature function name (for popups, events)
    /// </summary>
    public string? OnEachFeatureFunction { get; set; }

    /// <summary>
    /// Whether to add to marker cluster group
    /// </summary>
    public bool UseMarkerCluster { get; set; } = false;

    /// <summary>
    /// Z-index for layer ordering
    /// </summary>
    public int ZIndex { get; set; } = 0;
}

/// <summary>
/// Leaflet WMS layer configuration
/// </summary>
public class LeafletWmsLayer
{
    /// <summary>
    /// Layer identifier
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// WMS service URL
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// WMS layers to display (comma-separated)
    /// </summary>
    public required string Layers { get; set; }

    /// <summary>
    /// Image format (e.g., "image/png")
    /// </summary>
    public string Format { get; set; } = "image/png";

    /// <summary>
    /// Whether to request transparent images
    /// </summary>
    public bool Transparent { get; set; } = true;

    /// <summary>
    /// WMS version
    /// </summary>
    public string Version { get; set; } = "1.1.1";

    /// <summary>
    /// Coordinate reference system
    /// </summary>
    public string Crs { get; set; } = "EPSG:3857";

    /// <summary>
    /// Layer opacity (0-1)
    /// </summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>
    /// Z-index for layer ordering
    /// </summary>
    public int ZIndex { get; set; } = 0;

    /// <summary>
    /// Additional WMS parameters
    /// </summary>
    public Dictionary<string, object>? AdditionalParams { get; set; }
}

/// <summary>
/// Leaflet layer style configuration
/// </summary>
public class LeafletLayerStyle
{
    /// <summary>
    /// Stroke color
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Stroke width in pixels
    /// </summary>
    public double? Weight { get; set; }

    /// <summary>
    /// Stroke opacity (0-1)
    /// </summary>
    public double? Opacity { get; set; }

    /// <summary>
    /// Fill color
    /// </summary>
    public string? FillColor { get; set; }

    /// <summary>
    /// Fill opacity (0-1)
    /// </summary>
    public double? FillOpacity { get; set; }

    /// <summary>
    /// Line cap style
    /// </summary>
    public string? LineCap { get; set; }

    /// <summary>
    /// Line join style
    /// </summary>
    public string? LineJoin { get; set; }

    /// <summary>
    /// Dash pattern
    /// </summary>
    public string? DashArray { get; set; }

    /// <summary>
    /// Dash offset
    /// </summary>
    public string? DashOffset { get; set; }

    /// <summary>
    /// Circle marker radius (for point features)
    /// </summary>
    public double? Radius { get; set; }

    /// <summary>
    /// Additional style properties
    /// </summary>
    public Dictionary<string, object>? AdditionalProperties { get; set; }
}

/// <summary>
/// Leaflet drawing tools configuration
/// </summary>
public class LeafletDrawOptions
{
    /// <summary>
    /// Enable polygon drawing
    /// </summary>
    public bool EnablePolygon { get; set; } = true;

    /// <summary>
    /// Enable polyline drawing
    /// </summary>
    public bool EnablePolyline { get; set; } = true;

    /// <summary>
    /// Enable rectangle drawing
    /// </summary>
    public bool EnableRectangle { get; set; } = true;

    /// <summary>
    /// Enable circle drawing
    /// </summary>
    public bool EnableCircle { get; set; } = true;

    /// <summary>
    /// Enable marker placement
    /// </summary>
    public bool EnableMarker { get; set; } = true;

    /// <summary>
    /// Enable circle marker placement
    /// </summary>
    public bool EnableCircleMarker { get; set; } = false;

    /// <summary>
    /// Enable editing
    /// </summary>
    public bool EnableEdit { get; set; } = true;

    /// <summary>
    /// Enable removal
    /// </summary>
    public bool EnableRemove { get; set; } = true;

    /// <summary>
    /// Drawing shape color
    /// </summary>
    public string ShapeColor { get; set; } = "#3388ff";

    /// <summary>
    /// Control position
    /// </summary>
    public string Position { get; set; } = "topright";
}

/// <summary>
/// Leaflet measure tools configuration
/// </summary>
public class LeafletMeasureOptions
{
    /// <summary>
    /// Primary length unit
    /// </summary>
    public string PrimaryLengthUnit { get; set; } = "meters";

    /// <summary>
    /// Secondary length unit
    /// </summary>
    public string SecondaryLengthUnit { get; set; } = "kilometers";

    /// <summary>
    /// Primary area unit
    /// </summary>
    public string PrimaryAreaUnit { get; set; } = "sqmeters";

    /// <summary>
    /// Secondary area unit
    /// </summary>
    public string SecondaryAreaUnit { get; set; } = "hectares";

    /// <summary>
    /// Active measurement color
    /// </summary>
    public string ActiveColor { get; set; } = "#db4a29";

    /// <summary>
    /// Completed measurement color
    /// </summary>
    public string CompletedColor { get; set; } = "#9b2d14";

    /// <summary>
    /// Control position
    /// </summary>
    public string Position { get; set; } = "topright";
}

/// <summary>
/// Leaflet popup configuration
/// </summary>
public class LeafletPopupOptions
{
    /// <summary>
    /// Maximum width in pixels
    /// </summary>
    public int? MaxWidth { get; set; }

    /// <summary>
    /// Minimum width in pixels
    /// </summary>
    public int? MinWidth { get; set; }

    /// <summary>
    /// Maximum height in pixels
    /// </summary>
    public int? MaxHeight { get; set; }

    /// <summary>
    /// Auto pan map when popup opens
    /// </summary>
    public bool AutoPan { get; set; } = true;

    /// <summary>
    /// Keep popup in view when panning
    /// </summary>
    public bool KeepInView { get; set; } = true;

    /// <summary>
    /// Close button
    /// </summary>
    public bool CloseButton { get; set; } = true;

    /// <summary>
    /// Auto close on map click
    /// </summary>
    public bool AutoClose { get; set; } = true;

    /// <summary>
    /// Close on escape key
    /// </summary>
    public bool CloseOnEscapeKey { get; set; } = true;

    /// <summary>
    /// CSS class name
    /// </summary>
    public string? ClassName { get; set; }
}
