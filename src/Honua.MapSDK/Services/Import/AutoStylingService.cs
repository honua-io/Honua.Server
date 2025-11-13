// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json;
using Honua.MapSDK.Models.Import;

namespace Honua.MapSDK.Services.Import;

/// <summary>
/// Automatically generates appropriate styles based on geometry type and data characteristics
/// </summary>
public class AutoStylingService
{
    private static readonly Random _random = new();

    /// <summary>
    /// Generate style for imported data
    /// </summary>
    public StyleDefinition GenerateStyle(ParsedData data, string? geometryType = null)
    {
        var detectedGeomType = geometryType ?? DetectGeometryType(data);
        var style = new StyleDefinition
        {
            GeometryType = detectedGeomType
        };

        // Generate color palette based on data characteristics
        var colorScheme = GenerateColorScheme(data);

        switch (detectedGeomType?.ToLowerInvariant())
        {
            case "point":
            case "multipoint":
                style.PointStyle = GeneratePointStyle(data, colorScheme);
                break;

            case "linestring":
            case "multilinestring":
                style.LineStyle = GenerateLineStyle(data, colorScheme);
                break;

            case "polygon":
            case "multipolygon":
                style.PolygonStyle = GeneratePolygonStyle(data, colorScheme);
                break;

            default:
                // Mixed geometry or unknown - provide all styles
                style.PointStyle = GeneratePointStyle(data, colorScheme);
                style.LineStyle = GenerateLineStyle(data, colorScheme);
                style.PolygonStyle = GeneratePolygonStyle(data, colorScheme);
                break;
        }

        // Generate popup template
        style.PopupTemplate = GeneratePopupTemplate(data);

        // Determine if clustering should be enabled
        style.EnableClustering = ShouldEnableClustering(data, detectedGeomType);

        return style;
    }

    private string? DetectGeometryType(ParsedData data)
    {
        if (!data.Features.Any()) return null;

        // Check first non-null geometry
        var firstFeature = data.Features.FirstOrDefault(f => f.Geometry != null);
        if (firstFeature?.Geometry is Dictionary<string, object> geom &&
            geom.TryGetValue("type", out var type))
        {
            return type.ToString();
        }

        return null;
    }

    private ColorScheme GenerateColorScheme(ParsedData data)
    {
        // Generate a visually pleasing color scheme
        var schemes = new[]
        {
            new ColorScheme { Primary = "#3B82F6", Secondary = "#60A5FA", Accent = "#2563EB" }, // Blue
            new ColorScheme { Primary = "#10B981", Secondary = "#34D399", Accent = "#059669" }, // Green
            new ColorScheme { Primary = "#F59E0B", Secondary = "#FBBF24", Accent = "#D97706" }, // Amber
            new ColorScheme { Primary = "#EF4444", Secondary = "#F87171", Accent = "#DC2626" }, // Red
            new ColorScheme { Primary = "#8B5CF6", Secondary = "#A78BFA", Accent = "#7C3AED" }, // Purple
            new ColorScheme { Primary = "#EC4899", Secondary = "#F472B6", Accent = "#DB2777" }, // Pink
            new ColorScheme { Primary = "#14B8A6", Secondary = "#2DD4BF", Accent = "#0D9488" }, // Teal
        };

        return schemes[_random.Next(schemes.Length)];
    }

    private PointStyleDefinition GeneratePointStyle(ParsedData data, ColorScheme colors)
    {
        var featureCount = data.Features.Count;

        return new PointStyleDefinition
        {
            Type = "circle",
            Radius = featureCount > 10000 ? 4 : featureCount > 1000 ? 5 : 6,
            FillColor = colors.Primary,
            FillOpacity = 0.8,
            StrokeColor = colors.Accent,
            StrokeWidth = 1,
            StrokeOpacity = 1.0
        };
    }

    private LineStyleDefinition GenerateLineStyle(ParsedData data, ColorScheme colors)
    {
        return new LineStyleDefinition
        {
            Color = colors.Primary,
            Width = 3,
            Opacity = 0.8,
            LineCap = "round",
            LineJoin = "round",
            DashArray = null // Solid line
        };
    }

    private PolygonStyleDefinition GeneratePolygonStyle(ParsedData data, ColorScheme colors)
    {
        return new PolygonStyleDefinition
        {
            FillColor = colors.Primary,
            FillOpacity = 0.4,
            StrokeColor = colors.Accent,
            StrokeWidth = 2,
            StrokeOpacity = 0.8
        };
    }

    private string GeneratePopupTemplate(ParsedData data)
    {
        if (!data.Fields.Any()) return "<div><strong>Feature</strong></div>";

        var template = "<div style='min-width: 200px;'>";

        // Add top 5 most interesting fields
        var interestingFields = data.Fields
            .Where(f => !f.IsLikelyLatitude && !f.IsLikelyLongitude)
            .OrderByDescending(f => f.UniqueCount)
            .Take(5)
            .ToList();

        foreach (var field in interestingFields)
        {
            var fieldName = field.DisplayName ?? field.Name;
            template += $"<div style='margin: 4px 0;'><strong>{fieldName}:</strong> {{{{{field.Name}}}}}</div>";
        }

        template += "</div>";
        return template;
    }

    private bool ShouldEnableClustering(ParsedData data, string? geometryType)
    {
        // Enable clustering for point geometries with many features
        if (geometryType?.ToLowerInvariant() == "point" || geometryType?.ToLowerInvariant() == "multipoint")
        {
            return data.Features.Count > 100;
        }

        return false;
    }

    /// <summary>
    /// Generate MapLibre GL style JSON
    /// </summary>
    public JsonElement GenerateMapLibreStyle(StyleDefinition style, string layerId, string sourceId)
    {
        var layers = new List<object>();

        if (style.PointStyle != null)
        {
            layers.Add(new
            {
                id = $"{layerId}-points",
                type = "circle",
                source = sourceId,
                filter = new object[] { "==", new[] { "geometry-type" }, "Point" },
                paint = new
                {
                    circle_radius = style.PointStyle.Radius,
                    circle_color = style.PointStyle.FillColor,
                    circle_opacity = style.PointStyle.FillOpacity,
                    circle_stroke_width = style.PointStyle.StrokeWidth,
                    circle_stroke_color = style.PointStyle.StrokeColor,
                    circle_stroke_opacity = style.PointStyle.StrokeOpacity
                }
            });
        }

        if (style.LineStyle != null)
        {
            layers.Add(new
            {
                id = $"{layerId}-lines",
                type = "line",
                source = sourceId,
                filter = new object[] { "==", new[] { "geometry-type" }, "LineString" },
                layout = new
                {
                    line_cap = style.LineStyle.LineCap,
                    line_join = style.LineStyle.LineJoin
                },
                paint = new
                {
                    line_color = style.LineStyle.Color,
                    line_width = style.LineStyle.Width,
                    line_opacity = style.LineStyle.Opacity,
                    line_dasharray = style.LineStyle.DashArray
                }
            });
        }

        if (style.PolygonStyle != null)
        {
            layers.Add(new
            {
                id = $"{layerId}-polygons-fill",
                type = "fill",
                source = sourceId,
                filter = new object[] { "==", new[] { "geometry-type" }, "Polygon" },
                paint = new
                {
                    fill_color = style.PolygonStyle.FillColor,
                    fill_opacity = style.PolygonStyle.FillOpacity
                }
            });

            layers.Add(new
            {
                id = $"{layerId}-polygons-outline",
                type = "line",
                source = sourceId,
                filter = new object[] { "==", new[] { "geometry-type" }, "Polygon" },
                paint = new
                {
                    line_color = style.PolygonStyle.StrokeColor,
                    line_width = style.PolygonStyle.StrokeWidth,
                    line_opacity = style.PolygonStyle.StrokeOpacity
                }
            });
        }

        var styleJson = JsonSerializer.Serialize(layers);
        return JsonDocument.Parse(styleJson).RootElement;
    }
}

/// <summary>
/// Complete style definition for a layer
/// </summary>
public class StyleDefinition
{
    public string? GeometryType { get; set; }
    public PointStyleDefinition? PointStyle { get; set; }
    public LineStyleDefinition? LineStyle { get; set; }
    public PolygonStyleDefinition? PolygonStyle { get; set; }
    public string? PopupTemplate { get; set; }
    public bool EnableClustering { get; set; }
}

/// <summary>
/// Point geometry style
/// </summary>
public class PointStyleDefinition
{
    public string Type { get; set; } = "circle";
    public double Radius { get; set; } = 6;
    public string FillColor { get; set; } = "#3B82F6";
    public double FillOpacity { get; set; } = 0.8;
    public string StrokeColor { get; set; } = "#2563EB";
    public double StrokeWidth { get; set; } = 1;
    public double StrokeOpacity { get; set; } = 1.0;
}

/// <summary>
/// Line geometry style
/// </summary>
public class LineStyleDefinition
{
    public string Color { get; set; } = "#3B82F6";
    public double Width { get; set; } = 3;
    public double Opacity { get; set; } = 0.8;
    public string LineCap { get; set; } = "round";
    public string LineJoin { get; set; } = "round";
    public double[]? DashArray { get; set; }
}

/// <summary>
/// Polygon geometry style
/// </summary>
public class PolygonStyleDefinition
{
    public string FillColor { get; set; } = "#3B82F6";
    public double FillOpacity { get; set; } = 0.4;
    public string StrokeColor { get; set; } = "#2563EB";
    public double StrokeWidth { get; set; } = 2;
    public double StrokeOpacity { get; set; } = 0.8;
}

/// <summary>
/// Color scheme for styling
/// </summary>
public class ColorScheme
{
    public required string Primary { get; set; }
    public required string Secondary { get; set; }
    public required string Accent { get; set; }
}
