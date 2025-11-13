// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.MapSDK.Models;

/// <summary>
/// Base class for Deck.gl layer definitions
/// </summary>
public abstract class DeckLayerDefinition
{
    /// <summary>
    /// Unique identifier for this layer
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Human-readable name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Layer type (scatterplot, hexagon, arc, grid, screengrid)
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Layer data (array of objects)
    /// </summary>
    [JsonPropertyName("data")]
    public List<object> Data { get; set; } = new();

    /// <summary>
    /// Whether layer is visible
    /// </summary>
    [JsonPropertyName("visible")]
    public bool Visible { get; set; } = true;

    /// <summary>
    /// Layer opacity (0-1)
    /// </summary>
    [JsonPropertyName("opacity")]
    public double Opacity { get; set; } = 0.8;

    /// <summary>
    /// Whether layer is pickable (clickable)
    /// </summary>
    [JsonPropertyName("pickable")]
    public bool Pickable { get; set; } = true;

    /// <summary>
    /// Metadata dictionary
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Scatterplot layer - renders points with variable size and color
/// </summary>
public class ScatterplotLayerDefinition : DeckLayerDefinition
{
    public ScatterplotLayerDefinition()
    {
        Type = "scatterplot";
    }

    /// <summary>
    /// Radius scale multiplier
    /// </summary>
    [JsonPropertyName("radiusScale")]
    public double RadiusScale { get; set; } = 1.0;

    /// <summary>
    /// Minimum radius in pixels
    /// </summary>
    [JsonPropertyName("radiusMinPixels")]
    public int RadiusMinPixels { get; set; } = 1;

    /// <summary>
    /// Maximum radius in pixels
    /// </summary>
    [JsonPropertyName("radiusMaxPixels")]
    public int RadiusMaxPixels { get; set; } = 100;

    /// <summary>
    /// Whether points have stroke outline
    /// </summary>
    [JsonPropertyName("stroked")]
    public bool Stroked { get; set; } = true;

    /// <summary>
    /// Whether points are filled
    /// </summary>
    [JsonPropertyName("filled")]
    public bool Filled { get; set; } = true;

    /// <summary>
    /// Minimum line width in pixels
    /// </summary>
    [JsonPropertyName("lineWidthMinPixels")]
    public int LineWidthMinPixels { get; set; } = 1;

    /// <summary>
    /// Accessor for position data (property path or function)
    /// </summary>
    [JsonPropertyName("getPosition")]
    public string? GetPosition { get; set; } = "position";

    /// <summary>
    /// Accessor for radius data
    /// </summary>
    [JsonPropertyName("getRadius")]
    public string? GetRadius { get; set; } = "radius";

    /// <summary>
    /// Accessor for fill color data
    /// </summary>
    [JsonPropertyName("getFillColor")]
    public string? GetFillColor { get; set; } = "color";

    /// <summary>
    /// Accessor for line color data
    /// </summary>
    [JsonPropertyName("getLineColor")]
    public string? GetLineColor { get; set; } = "lineColor";
}

/// <summary>
/// Hexagon layer - 3D hexagonal binning aggregation
/// </summary>
public class HexagonLayerDefinition : DeckLayerDefinition
{
    public HexagonLayerDefinition()
    {
        Type = "hexagon";
    }

    /// <summary>
    /// Hexagon radius in meters
    /// </summary>
    [JsonPropertyName("radius")]
    public double Radius { get; set; } = 1000;

    /// <summary>
    /// Whether hexagons are extruded (3D)
    /// </summary>
    [JsonPropertyName("extruded")]
    public bool Extruded { get; set; } = true;

    /// <summary>
    /// Elevation scale multiplier
    /// </summary>
    [JsonPropertyName("elevationScale")]
    public double ElevationScale { get; set; } = 4;

    /// <summary>
    /// Elevation range [min, max] in meters
    /// </summary>
    [JsonPropertyName("elevationRange")]
    public double[] ElevationRange { get; set; } = { 0, 3000 };

    /// <summary>
    /// Coverage area (0-1)
    /// </summary>
    [JsonPropertyName("coverage")]
    public double Coverage { get; set; } = 1.0;

    /// <summary>
    /// Upper percentile for color scale
    /// </summary>
    [JsonPropertyName("upperPercentile")]
    public int UpperPercentile { get; set; } = 100;

    /// <summary>
    /// Color range for elevation gradient
    /// </summary>
    [JsonPropertyName("colorRange")]
    public int[][] ColorRange { get; set; } = {
        new[] { 1, 152, 189 },
        new[] { 73, 227, 206 },
        new[] { 216, 254, 181 },
        new[] { 254, 237, 177 },
        new[] { 254, 173, 84 },
        new[] { 209, 55, 78 }
    };

    /// <summary>
    /// Accessor for position data
    /// </summary>
    [JsonPropertyName("getPosition")]
    public string? GetPosition { get; set; } = "position";

    /// <summary>
    /// Accessor for weight data (for aggregation)
    /// </summary>
    [JsonPropertyName("getWeight")]
    public string? GetWeight { get; set; }
}

/// <summary>
/// Arc layer - renders arcs between two points (origin-destination flows)
/// </summary>
public class ArcLayerDefinition : DeckLayerDefinition
{
    public ArcLayerDefinition()
    {
        Type = "arc";
    }

    /// <summary>
    /// Arc width in pixels
    /// </summary>
    [JsonPropertyName("getWidth")]
    public int GetWidth { get; set; } = 5;

    /// <summary>
    /// Accessor for source position [lng, lat]
    /// </summary>
    [JsonPropertyName("getSourcePosition")]
    public string? GetSourcePosition { get; set; } = "sourcePosition";

    /// <summary>
    /// Accessor for target position [lng, lat]
    /// </summary>
    [JsonPropertyName("getTargetPosition")]
    public string? GetTargetPosition { get; set; } = "targetPosition";

    /// <summary>
    /// Accessor for source color [r, g, b]
    /// </summary>
    [JsonPropertyName("getSourceColor")]
    public string? GetSourceColor { get; set; } = "sourceColor";

    /// <summary>
    /// Accessor for target color [r, g, b]
    /// </summary>
    [JsonPropertyName("getTargetColor")]
    public string? GetTargetColor { get; set; } = "targetColor";

    /// <summary>
    /// Accessor for arc tilt angle
    /// </summary>
    [JsonPropertyName("getTilt")]
    public string? GetTilt { get; set; }

    /// <summary>
    /// Accessor for arc height (curvature)
    /// </summary>
    [JsonPropertyName("getHeight")]
    public string? GetHeight { get; set; }
}

/// <summary>
/// Grid layer - rectangular grid aggregation
/// </summary>
public class GridLayerDefinition : DeckLayerDefinition
{
    public GridLayerDefinition()
    {
        Type = "grid";
    }

    /// <summary>
    /// Cell size in meters
    /// </summary>
    [JsonPropertyName("cellSize")]
    public double CellSize { get; set; } = 1000;

    /// <summary>
    /// Whether cells are extruded (3D)
    /// </summary>
    [JsonPropertyName("extruded")]
    public bool Extruded { get; set; } = true;

    /// <summary>
    /// Elevation scale multiplier
    /// </summary>
    [JsonPropertyName("elevationScale")]
    public double ElevationScale { get; set; } = 4;

    /// <summary>
    /// Elevation range [min, max] in meters
    /// </summary>
    [JsonPropertyName("elevationRange")]
    public double[] ElevationRange { get; set; } = { 0, 3000 };

    /// <summary>
    /// Coverage area (0-1)
    /// </summary>
    [JsonPropertyName("coverage")]
    public double Coverage { get; set; } = 1.0;

    /// <summary>
    /// Upper percentile for color scale
    /// </summary>
    [JsonPropertyName("upperPercentile")]
    public int UpperPercentile { get; set; } = 100;

    /// <summary>
    /// Color range for elevation gradient
    /// </summary>
    [JsonPropertyName("colorRange")]
    public int[][] ColorRange { get; set; } = {
        new[] { 1, 152, 189 },
        new[] { 73, 227, 206 },
        new[] { 216, 254, 181 },
        new[] { 254, 237, 177 },
        new[] { 254, 173, 84 },
        new[] { 209, 55, 78 }
    };

    /// <summary>
    /// Accessor for position data
    /// </summary>
    [JsonPropertyName("getPosition")]
    public string? GetPosition { get; set; } = "position";

    /// <summary>
    /// Accessor for weight data (for aggregation)
    /// </summary>
    [JsonPropertyName("getWeight")]
    public string? GetWeight { get; set; }
}

/// <summary>
/// Screen grid layer - screen-space grid aggregation (heatmap)
/// </summary>
public class ScreenGridLayerDefinition : DeckLayerDefinition
{
    public ScreenGridLayerDefinition()
    {
        Type = "screengrid";
    }

    /// <summary>
    /// Cell size in pixels
    /// </summary>
    [JsonPropertyName("cellSizePixels")]
    public int CellSizePixels { get; set; } = 50;

    /// <summary>
    /// Color range for heatmap gradient
    /// </summary>
    [JsonPropertyName("colorRange")]
    public int[][] ColorRange { get; set; } = {
        new[] { 0, 25, 0, 25 },
        new[] { 0, 85, 0, 85 },
        new[] { 0, 127, 0, 127 },
        new[] { 0, 170, 0, 170 },
        new[] { 0, 190, 0, 190 },
        new[] { 0, 255, 0, 255 }
    };

    /// <summary>
    /// Accessor for position data
    /// </summary>
    [JsonPropertyName("getPosition")]
    public string? GetPosition { get; set; } = "position";

    /// <summary>
    /// Accessor for weight data (for aggregation)
    /// </summary>
    [JsonPropertyName("getWeight")]
    public string? GetWeight { get; set; }
}

/// <summary>
/// Deck.gl layer click event data
/// </summary>
public class DeckLayerClickEventArgs
{
    public required string LayerId { get; set; }
    public required object ClickedObject { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public double[] Coordinate { get; set; } = Array.Empty<double>();
}

/// <summary>
/// Deck.gl layer hover event data
/// </summary>
public class DeckLayerHoverEventArgs
{
    public required string LayerId { get; set; }
    public object? HoveredObject { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}
