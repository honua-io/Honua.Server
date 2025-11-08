// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.MapSDK.Components.Map;

/// <summary>
/// Initialization options for MapLibre GL JS map instance.
/// </summary>
public class MapLibreInitOptions
{
    /// <summary>
    /// Container element ID or reference.
    /// </summary>
    [JsonPropertyName("container")]
    public required string Container { get; set; }

    /// <summary>
    /// Map style URL or style object.
    /// </summary>
    [JsonPropertyName("style")]
    public object? Style { get; set; }

    /// <summary>
    /// Initial map center [longitude, latitude].
    /// </summary>
    [JsonPropertyName("center")]
    public double[]? Center { get; set; }

    /// <summary>
    /// Initial zoom level (0-22).
    /// </summary>
    [JsonPropertyName("zoom")]
    public double? Zoom { get; set; }

    /// <summary>
    /// Initial bearing (rotation) in degrees.
    /// </summary>
    [JsonPropertyName("bearing")]
    public double? Bearing { get; set; }

    /// <summary>
    /// Initial pitch (tilt) in degrees (0-85).
    /// </summary>
    [JsonPropertyName("pitch")]
    public double? Pitch { get; set; }

    /// <summary>
    /// Minimum zoom level.
    /// </summary>
    [JsonPropertyName("minZoom")]
    public double? MinZoom { get; set; }

    /// <summary>
    /// Maximum zoom level.
    /// </summary>
    [JsonPropertyName("maxZoom")]
    public double? MaxZoom { get; set; }

    /// <summary>
    /// Bounding box to restrict map extent [west, south, east, north].
    /// </summary>
    [JsonPropertyName("maxBounds")]
    public double[]? MaxBounds { get; set; }

    /// <summary>
    /// Enable scroll zoom.
    /// </summary>
    [JsonPropertyName("scrollZoom")]
    public bool ScrollZoom { get; set; } = true;

    /// <summary>
    /// Enable box zoom (shift+drag).
    /// </summary>
    [JsonPropertyName("boxZoom")]
    public bool BoxZoom { get; set; } = true;

    /// <summary>
    /// Enable drag rotate (ctrl+drag or right-click+drag).
    /// </summary>
    [JsonPropertyName("dragRotate")]
    public bool DragRotate { get; set; } = true;

    /// <summary>
    /// Enable drag pan.
    /// </summary>
    [JsonPropertyName("dragPan")]
    public bool DragPan { get; set; } = true;

    /// <summary>
    /// Enable keyboard navigation.
    /// </summary>
    [JsonPropertyName("keyboard")]
    public bool Keyboard { get; set; } = true;

    /// <summary>
    /// Enable double click zoom.
    /// </summary>
    [JsonPropertyName("doubleClickZoom")]
    public bool DoubleClickZoom { get; set; } = true;

    /// <summary>
    /// Enable touch zoom rotate.
    /// </summary>
    [JsonPropertyName("touchZoomRotate")]
    public bool TouchZoomRotate { get; set; } = true;

    /// <summary>
    /// Enable touch pitch.
    /// </summary>
    [JsonPropertyName("touchPitch")]
    public bool TouchPitch { get; set; } = true;

    /// <summary>
    /// Hash navigation (update URL with map position).
    /// </summary>
    [JsonPropertyName("hash")]
    public bool Hash { get; set; } = false;

    /// <summary>
    /// Fade duration for symbols (ms).
    /// </summary>
    [JsonPropertyName("fadeDuration")]
    public int? FadeDuration { get; set; }

    /// <summary>
    /// Crosshairs cursor on map.
    /// </summary>
    [JsonPropertyName("crossSourceCache")]
    public bool CrossSourceCache { get; set; } = true;

    /// <summary>
    /// Collect GPU timing information (debug).
    /// </summary>
    [JsonPropertyName("collectResourceTiming")]
    public bool CollectResourceTiming { get; set; } = false;

    /// <summary>
    /// Render world copies at low zoom levels.
    /// </summary>
    [JsonPropertyName("renderWorldCopies")]
    public bool RenderWorldCopies { get; set; } = true;

    /// <summary>
    /// Max tile cache size.
    /// </summary>
    [JsonPropertyName("maxTileCacheSize")]
    public int? MaxTileCacheSize { get; set; }

    /// <summary>
    /// Transform request function for modifying tile requests.
    /// </summary>
    [JsonPropertyName("transformRequest")]
    public string? TransformRequest { get; set; }

    /// <summary>
    /// Locale for UI strings.
    /// </summary>
    [JsonPropertyName("locale")]
    public Dictionary<string, string>? Locale { get; set; }

    /// <summary>
    /// Projection type (mercator, globe, etc.).
    /// </summary>
    [JsonPropertyName("projection")]
    public string? Projection { get; set; }
}

/// <summary>
/// MapLibre style configuration.
/// Can be a URL string or a full style object.
/// </summary>
public class MapLibreStyle
{
    /// <summary>
    /// Style version (usually 8).
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 8;

    /// <summary>
    /// Style name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Map sources.
    /// </summary>
    [JsonPropertyName("sources")]
    public Dictionary<string, MapLibreSource> Sources { get; set; } = new();

    /// <summary>
    /// Map layers.
    /// </summary>
    [JsonPropertyName("layers")]
    public List<MapLibreLayer> Layers { get; set; } = new();

    /// <summary>
    /// Sprite URL.
    /// </summary>
    [JsonPropertyName("sprite")]
    public string? Sprite { get; set; }

    /// <summary>
    /// Glyphs URL template.
    /// </summary>
    [JsonPropertyName("glyphs")]
    public string? Glyphs { get; set; }

    /// <summary>
    /// Style metadata.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// MapLibre source configuration.
/// </summary>
public class MapLibreSource
{
    /// <summary>
    /// Source type (raster, vector, geojson, image, video).
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    /// <summary>
    /// Tile URL template for raster/vector sources.
    /// </summary>
    [JsonPropertyName("tiles")]
    public string[]? Tiles { get; set; }

    /// <summary>
    /// TileJSON URL.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// Tile size in pixels.
    /// </summary>
    [JsonPropertyName("tileSize")]
    public int? TileSize { get; set; }

    /// <summary>
    /// Minimum zoom level.
    /// </summary>
    [JsonPropertyName("minzoom")]
    public int? MinZoom { get; set; }

    /// <summary>
    /// Maximum zoom level.
    /// </summary>
    [JsonPropertyName("maxzoom")]
    public int? MaxZoom { get; set; }

    /// <summary>
    /// Attribution text.
    /// </summary>
    [JsonPropertyName("attribution")]
    public string? Attribution { get; set; }

    /// <summary>
    /// Bounding box [west, south, east, north].
    /// </summary>
    [JsonPropertyName("bounds")]
    public double[]? Bounds { get; set; }

    /// <summary>
    /// GeoJSON data (for geojson sources).
    /// </summary>
    [JsonPropertyName("data")]
    public object? Data { get; set; }

    /// <summary>
    /// Scheme (xyz or tms).
    /// </summary>
    [JsonPropertyName("scheme")]
    public string? Scheme { get; set; }
}

/// <summary>
/// MapLibre layer configuration.
/// </summary>
public class MapLibreLayer
{
    /// <summary>
    /// Unique layer ID.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    /// Layer type (fill, line, symbol, circle, fill-extrusion, raster, background, heatmap, hillshade, sky).
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    /// <summary>
    /// Source ID.
    /// </summary>
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    /// <summary>
    /// Source layer (for vector tiles).
    /// </summary>
    [JsonPropertyName("source-layer")]
    public string? SourceLayer { get; set; }

    /// <summary>
    /// Minimum zoom level.
    /// </summary>
    [JsonPropertyName("minzoom")]
    public double? MinZoom { get; set; }

    /// <summary>
    /// Maximum zoom level.
    /// </summary>
    [JsonPropertyName("maxzoom")]
    public double? MaxZoom { get; set; }

    /// <summary>
    /// Filter expression.
    /// </summary>
    [JsonPropertyName("filter")]
    public object? Filter { get; set; }

    /// <summary>
    /// Layout properties.
    /// </summary>
    [JsonPropertyName("layout")]
    public Dictionary<string, object>? Layout { get; set; }

    /// <summary>
    /// Paint properties.
    /// </summary>
    [JsonPropertyName("paint")]
    public Dictionary<string, object>? Paint { get; set; }

    /// <summary>
    /// Layer metadata.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Marker configuration for MapLibre.
/// </summary>
public class MapLibreMarker
{
    /// <summary>
    /// Unique marker ID.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Marker position [longitude, latitude].
    /// </summary>
    public required double[] Position { get; set; }

    /// <summary>
    /// Marker offset [x, y] in pixels.
    /// </summary>
    public int[]? Offset { get; set; }

    /// <summary>
    /// Marker anchor point (center, top, bottom, left, right, top-left, top-right, bottom-left, bottom-right).
    /// </summary>
    public string Anchor { get; set; } = "center";

    /// <summary>
    /// Custom marker element HTML.
    /// </summary>
    public string? Element { get; set; }

    /// <summary>
    /// Marker color (for default marker).
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Scale factor for marker size.
    /// </summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>
    /// Rotation in degrees.
    /// </summary>
    public double Rotation { get; set; } = 0;

    /// <summary>
    /// Rotation alignment (map or viewport).
    /// </summary>
    public string RotationAlignment { get; set; } = "auto";

    /// <summary>
    /// Pitch alignment (map or viewport).
    /// </summary>
    public string PitchAlignment { get; set; } = "auto";

    /// <summary>
    /// Draggable marker.
    /// </summary>
    public bool Draggable { get; set; } = false;

    /// <summary>
    /// Popup configuration.
    /// </summary>
    public MapLibrePopup? Popup { get; set; }

    /// <summary>
    /// Custom properties.
    /// </summary>
    public Dictionary<string, object>? Properties { get; set; }
}

/// <summary>
/// Popup configuration for MapLibre.
/// </summary>
public class MapLibrePopup
{
    /// <summary>
    /// Popup HTML content.
    /// </summary>
    public string? Html { get; set; }

    /// <summary>
    /// Popup text content.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Maximum width in pixels.
    /// </summary>
    public string? MaxWidth { get; set; }

    /// <summary>
    /// Close button.
    /// </summary>
    public bool CloseButton { get; set; } = true;

    /// <summary>
    /// Close popup on click elsewhere.
    /// </summary>
    public bool CloseOnClick { get; set; } = true;

    /// <summary>
    /// Close popup on move.
    /// </summary>
    public bool CloseOnMove { get; set; } = false;

    /// <summary>
    /// Focus on popup after open.
    /// </summary>
    public bool FocusAfterOpen { get; set; } = true;

    /// <summary>
    /// Anchor position (same options as marker anchor).
    /// </summary>
    public string? Anchor { get; set; }

    /// <summary>
    /// Offset [x, y] in pixels.
    /// </summary>
    public int[]? Offset { get; set; }

    /// <summary>
    /// CSS class name.
    /// </summary>
    public string? ClassName { get; set; }
}

/// <summary>
/// Viewport state for map position and view.
/// </summary>
public class MapLibreViewport
{
    /// <summary>
    /// Center position [longitude, latitude].
    /// </summary>
    public required double[] Center { get; set; }

    /// <summary>
    /// Zoom level.
    /// </summary>
    public required double Zoom { get; set; }

    /// <summary>
    /// Bearing (rotation) in degrees.
    /// </summary>
    public double Bearing { get; set; } = 0;

    /// <summary>
    /// Pitch (tilt) in degrees.
    /// </summary>
    public double Pitch { get; set; } = 0;

    /// <summary>
    /// Bounding box [west, south, east, north].
    /// </summary>
    public double[]? Bounds { get; set; }
}

/// <summary>
/// Event arguments for map click events.
/// </summary>
public class MapClickEventArgs
{
    /// <summary>
    /// Click position [longitude, latitude].
    /// </summary>
    public required double[] LngLat { get; set; }

    /// <summary>
    /// Screen coordinates [x, y].
    /// </summary>
    public required double[] Point { get; set; }

    /// <summary>
    /// Features at click location.
    /// </summary>
    public List<MapFeature>? Features { get; set; }
}

/// <summary>
/// Event arguments for map move events.
/// </summary>
public class MapMoveEventArgs
{
    /// <summary>
    /// Current center [longitude, latitude].
    /// </summary>
    public required double[] Center { get; set; }

    /// <summary>
    /// Current zoom level.
    /// </summary>
    public required double Zoom { get; set; }

    /// <summary>
    /// Current bearing.
    /// </summary>
    public double Bearing { get; set; }

    /// <summary>
    /// Current pitch.
    /// </summary>
    public double Pitch { get; set; }
}

/// <summary>
/// Event arguments for viewport change events.
/// </summary>
public class ViewportChangeEventArgs
{
    /// <summary>
    /// New viewport state.
    /// </summary>
    public required MapLibreViewport Viewport { get; set; }

    /// <summary>
    /// Event type (move, zoom, rotate, pitch).
    /// </summary>
    public string? EventType { get; set; }
}

/// <summary>
/// Represents a map feature.
/// </summary>
public class MapFeature
{
    /// <summary>
    /// Feature ID.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Feature type (Point, LineString, Polygon, etc.).
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Source layer.
    /// </summary>
    public string? SourceLayer { get; set; }

    /// <summary>
    /// Layer ID.
    /// </summary>
    public string? Layer { get; set; }

    /// <summary>
    /// Feature properties.
    /// </summary>
    public Dictionary<string, object>? Properties { get; set; }

    /// <summary>
    /// Feature geometry.
    /// </summary>
    public object? Geometry { get; set; }
}
