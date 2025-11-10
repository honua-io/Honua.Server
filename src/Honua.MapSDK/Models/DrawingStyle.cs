// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Models;

/// <summary>
/// Style configuration for drawn features
/// </summary>
public class DrawingStyle
{
    /// <summary>
    /// Stroke (line) color in hex format (e.g., "#FF0000")
    /// </summary>
    public string StrokeColor { get; set; } = "#3B82F6";

    /// <summary>
    /// Stroke width in pixels
    /// </summary>
    public double StrokeWidth { get; set; } = 2.0;

    /// <summary>
    /// Stroke opacity (0.0 to 1.0)
    /// </summary>
    public double StrokeOpacity { get; set; } = 1.0;

    /// <summary>
    /// Stroke dash pattern (e.g., [5, 5] for dashed line)
    /// </summary>
    public double[]? StrokeDashArray { get; set; }

    /// <summary>
    /// Line cap style (butt, round, square)
    /// </summary>
    public string StrokeLineCap { get; set; } = "round";

    /// <summary>
    /// Line join style (bevel, round, miter)
    /// </summary>
    public string StrokeLineJoin { get; set; } = "round";

    /// <summary>
    /// Fill color in hex format
    /// </summary>
    public string FillColor { get; set; } = "#3B82F6";

    /// <summary>
    /// Fill opacity (0.0 to 1.0)
    /// </summary>
    public double FillOpacity { get; set; } = 0.2;

    /// <summary>
    /// Fill pattern (solid, diagonal, cross, etc.)
    /// </summary>
    public string? FillPattern { get; set; }

    /// <summary>
    /// Point marker icon (for point features)
    /// </summary>
    public string? MarkerIcon { get; set; }

    /// <summary>
    /// Marker size in pixels
    /// </summary>
    public double MarkerSize { get; set; } = 10.0;

    /// <summary>
    /// Marker color
    /// </summary>
    public string MarkerColor { get; set; } = "#3B82F6";

    /// <summary>
    /// Text label
    /// </summary>
    public string? LabelText { get; set; }

    /// <summary>
    /// Label font family
    /// </summary>
    public string LabelFont { get; set; } = "Arial, sans-serif";

    /// <summary>
    /// Label font size in pixels
    /// </summary>
    public double LabelSize { get; set; } = 12.0;

    /// <summary>
    /// Label color
    /// </summary>
    public string LabelColor { get; set; } = "#000000";

    /// <summary>
    /// Label halo color (outline)
    /// </summary>
    public string LabelHaloColor { get; set; } = "#FFFFFF";

    /// <summary>
    /// Label halo width
    /// </summary>
    public double LabelHaloWidth { get; set; } = 2.0;

    /// <summary>
    /// Label placement (point, line, line-center)
    /// </summary>
    public string LabelPlacement { get; set; } = "point";

    /// <summary>
    /// Label offset [x, y] in pixels
    /// </summary>
    public double[]? LabelOffset { get; set; }

    /// <summary>
    /// Show measurement labels on features
    /// </summary>
    public bool ShowMeasurements { get; set; } = true;

    /// <summary>
    /// Measurement label color
    /// </summary>
    public string MeasurementColor { get; set; } = "#1F2937";

    /// <summary>
    /// Measurement label background color
    /// </summary>
    public string MeasurementBackgroundColor { get; set; } = "#FFFFFF";

    /// <summary>
    /// Measurement label background opacity
    /// </summary>
    public double MeasurementBackgroundOpacity { get; set; } = 0.8;

    /// <summary>
    /// Clone this style configuration
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
            MarkerIcon = MarkerIcon,
            MarkerSize = MarkerSize,
            MarkerColor = MarkerColor,
            LabelText = LabelText,
            LabelFont = LabelFont,
            LabelSize = LabelSize,
            LabelColor = LabelColor,
            LabelHaloColor = LabelHaloColor,
            LabelHaloWidth = LabelHaloWidth,
            LabelPlacement = LabelPlacement,
            LabelOffset = LabelOffset?.ToArray(),
            ShowMeasurements = ShowMeasurements,
            MeasurementColor = MeasurementColor,
            MeasurementBackgroundColor = MeasurementBackgroundColor,
            MeasurementBackgroundOpacity = MeasurementBackgroundOpacity
        };
    }
}

/// <summary>
/// Predefined style presets for common use cases
/// </summary>
public static class DrawingStylePresets
{
    /// <summary>
    /// Default blue style
    /// </summary>
    public static DrawingStyle Default => new()
    {
        StrokeColor = "#3B82F6",
        StrokeWidth = 2.0,
        FillColor = "#3B82F6",
        FillOpacity = 0.2
    };

    /// <summary>
    /// Red style for important features
    /// </summary>
    public static DrawingStyle Important => new()
    {
        StrokeColor = "#EF4444",
        StrokeWidth = 3.0,
        FillColor = "#EF4444",
        FillOpacity = 0.25
    };

    /// <summary>
    /// Green style for success/safe areas
    /// </summary>
    public static DrawingStyle Success => new()
    {
        StrokeColor = "#10B981",
        StrokeWidth = 2.0,
        FillColor = "#10B981",
        FillOpacity = 0.2
    };

    /// <summary>
    /// Yellow style for warnings
    /// </summary>
    public static DrawingStyle Warning => new()
    {
        StrokeColor = "#F59E0B",
        StrokeWidth = 2.5,
        FillColor = "#F59E0B",
        FillOpacity = 0.25
    };

    /// <summary>
    /// Dashed line style
    /// </summary>
    public static DrawingStyle Dashed => new()
    {
        StrokeColor = "#6B7280",
        StrokeWidth = 2.0,
        StrokeDashArray = new[] { 5.0, 5.0 },
        FillColor = "#6B7280",
        FillOpacity = 0.15
    };

    /// <summary>
    /// Measurement style (thin, precise)
    /// </summary>
    public static DrawingStyle Measurement => new()
    {
        StrokeColor = "#8B5CF6",
        StrokeWidth = 1.5,
        FillColor = "#8B5CF6",
        FillOpacity = 0.1,
        ShowMeasurements = true
    };

    /// <summary>
    /// Highlight style for selected features
    /// </summary>
    public static DrawingStyle Highlight => new()
    {
        StrokeColor = "#FBBF24",
        StrokeWidth = 4.0,
        FillColor = "#FBBF24",
        FillOpacity = 0.3
    };

    /// <summary>
    /// Active drawing style (while drawing)
    /// </summary>
    public static DrawingStyle Active => new()
    {
        StrokeColor = "#06B6D4",
        StrokeWidth = 2.5,
        FillColor = "#06B6D4",
        FillOpacity = 0.25,
        StrokeDashArray = new[] { 3.0, 3.0 }
    };
}
