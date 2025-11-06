namespace Honua.MapSDK.Services.Drawing;

/// <summary>
/// Configuration for styling drawn geometries
/// </summary>
public class DrawingStyle
{
    /// <summary>
    /// Stroke (outline) color in hex format (#RRGGBB or #RRGGBBAA)
    /// </summary>
    public string StrokeColor { get; set; } = "#3388ff";

    /// <summary>
    /// Stroke width in pixels
    /// </summary>
    public double StrokeWidth { get; set; } = 3;

    /// <summary>
    /// Stroke opacity (0-1)
    /// </summary>
    public double StrokeOpacity { get; set; } = 1.0;

    /// <summary>
    /// Stroke dash pattern (e.g., [5, 5] for dashed line)
    /// null or empty for solid line
    /// </summary>
    public double[]? StrokeDashArray { get; set; }

    /// <summary>
    /// Line cap style: butt, round, square
    /// </summary>
    public string StrokeLineCap { get; set; } = "round";

    /// <summary>
    /// Line join style: miter, round, bevel
    /// </summary>
    public string StrokeLineJoin { get; set; } = "round";

    /// <summary>
    /// Fill color in hex format (#RRGGBB or #RRGGBBAA)
    /// </summary>
    public string FillColor { get; set; } = "#3388ff";

    /// <summary>
    /// Fill opacity (0-1)
    /// </summary>
    public double FillOpacity { get; set; } = 0.2;

    /// <summary>
    /// Fill pattern (solid, diagonal-stripes, dots, etc.)
    /// </summary>
    public FillPattern FillPattern { get; set; } = FillPattern.Solid;

    /// <summary>
    /// Vertex marker configuration
    /// </summary>
    public VertexMarkerStyle VertexMarker { get; set; } = new();

    /// <summary>
    /// Style when geometry is hovered
    /// </summary>
    public HoverStyle? HoverStyle { get; set; }

    /// <summary>
    /// Style when geometry is selected
    /// </summary>
    public SelectedStyle? SelectedStyle { get; set; }

    /// <summary>
    /// Z-index for rendering order (higher = on top)
    /// </summary>
    public int ZIndex { get; set; } = 0;

    /// <summary>
    /// Whether to show measurements while drawing
    /// </summary>
    public bool ShowMeasurements { get; set; } = true;

    /// <summary>
    /// Whether to show tooltips
    /// </summary>
    public bool ShowTooltips { get; set; } = true;

    /// <summary>
    /// Create a copy of this style
    /// </summary>
    public DrawingStyle Clone()
    {
        return new DrawingStyle
        {
            StrokeColor = StrokeColor,
            StrokeWidth = StrokeWidth,
            StrokeOpacity = StrokeOpacity,
            StrokeDashArray = StrokeDashArray?.ToArray(),
            StrokeLineCap = StrokeLineCap,
            StrokeLineJoin = StrokeLineJoin,
            FillColor = FillColor,
            FillOpacity = FillOpacity,
            FillPattern = FillPattern,
            VertexMarker = VertexMarker.Clone(),
            HoverStyle = HoverStyle?.Clone(),
            SelectedStyle = SelectedStyle?.Clone(),
            ZIndex = ZIndex,
            ShowMeasurements = ShowMeasurements,
            ShowTooltips = ShowTooltips
        };
    }

    /// <summary>
    /// Default styles for different geometry types
    /// </summary>
    public static class Defaults
    {
        public static DrawingStyle Point => new()
        {
            StrokeColor = "#3388ff",
            StrokeWidth = 2,
            FillColor = "#3388ff",
            FillOpacity = 0.8,
            VertexMarker = new VertexMarkerStyle
            {
                Shape = MarkerShape.Circle,
                Size = 8,
                Color = "#3388ff",
                StrokeColor = "#ffffff",
                StrokeWidth = 2
            }
        };

        public static DrawingStyle Line => new()
        {
            StrokeColor = "#3388ff",
            StrokeWidth = 3,
            FillOpacity = 0
        };

        public static DrawingStyle Polygon => new()
        {
            StrokeColor = "#3388ff",
            StrokeWidth = 3,
            FillColor = "#3388ff",
            FillOpacity = 0.2
        };

        public static DrawingStyle Circle => new()
        {
            StrokeColor = "#ff6b6b",
            StrokeWidth = 3,
            FillColor = "#ff6b6b",
            FillOpacity = 0.2
        };

        public static DrawingStyle Rectangle => new()
        {
            StrokeColor = "#4ecdc4",
            StrokeWidth = 3,
            FillColor = "#4ecdc4",
            FillOpacity = 0.2
        };
    }
}

/// <summary>
/// Configuration for vertex markers (edit handles)
/// </summary>
public class VertexMarkerStyle
{
    /// <summary>
    /// Marker shape
    /// </summary>
    public MarkerShape Shape { get; set; } = MarkerShape.Circle;

    /// <summary>
    /// Marker size in pixels
    /// </summary>
    public double Size { get; set; } = 8;

    /// <summary>
    /// Marker fill color
    /// </summary>
    public string Color { get; set; } = "#ffffff";

    /// <summary>
    /// Marker stroke color
    /// </summary>
    public string StrokeColor { get; set; } = "#3388ff";

    /// <summary>
    /// Marker stroke width
    /// </summary>
    public double StrokeWidth { get; set; } = 2;

    /// <summary>
    /// Whether to show vertex markers
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// Whether to show midpoint markers (for adding vertices)
    /// </summary>
    public bool ShowMidpoints { get; set; } = true;

    /// <summary>
    /// Midpoint marker color
    /// </summary>
    public string MidpointColor { get; set; } = "#cccccc";

    /// <summary>
    /// Midpoint marker size
    /// </summary>
    public double MidpointSize { get; set; } = 6;

    public VertexMarkerStyle Clone()
    {
        return new VertexMarkerStyle
        {
            Shape = Shape,
            Size = Size,
            Color = Color,
            StrokeColor = StrokeColor,
            StrokeWidth = StrokeWidth,
            Visible = Visible,
            ShowMidpoints = ShowMidpoints,
            MidpointColor = MidpointColor,
            MidpointSize = MidpointSize
        };
    }
}

/// <summary>
/// Style applied when geometry is hovered
/// </summary>
public class HoverStyle
{
    public string? StrokeColor { get; set; }
    public double? StrokeWidth { get; set; }
    public double? StrokeOpacity { get; set; }
    public string? FillColor { get; set; }
    public double? FillOpacity { get; set; }

    public HoverStyle Clone()
    {
        return new HoverStyle
        {
            StrokeColor = StrokeColor,
            StrokeWidth = StrokeWidth,
            StrokeOpacity = StrokeOpacity,
            FillColor = FillColor,
            FillOpacity = FillOpacity
        };
    }
}

/// <summary>
/// Style applied when geometry is selected
/// </summary>
public class SelectedStyle
{
    public string? StrokeColor { get; set; } = "#ff6b6b";
    public double? StrokeWidth { get; set; } = 4;
    public double? StrokeOpacity { get; set; }
    public string? FillColor { get; set; }
    public double? FillOpacity { get; set; } = 0.3;

    public SelectedStyle Clone()
    {
        return new SelectedStyle
        {
            StrokeColor = StrokeColor,
            StrokeWidth = StrokeWidth,
            StrokeOpacity = StrokeOpacity,
            FillColor = FillColor,
            FillOpacity = FillOpacity
        };
    }
}

/// <summary>
/// Marker shapes for vertices
/// </summary>
public enum MarkerShape
{
    Circle,
    Square,
    Triangle,
    Diamond,
    Cross,
    X
}

/// <summary>
/// Fill patterns for polygons
/// </summary>
public enum FillPattern
{
    Solid,
    DiagonalStripes,
    HorizontalStripes,
    VerticalStripes,
    Dots,
    Grid,
    Crosshatch,
    None
}
