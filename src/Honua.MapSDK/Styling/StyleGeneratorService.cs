// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Honua.Server.Core.Metadata;

namespace Honua.MapSDK.Styling;

/// <summary>
/// Intelligently generates professional map styles based on data characteristics
/// </summary>
public class StyleGeneratorService
{
    private readonly DataAnalyzer _dataAnalyzer = new();

    /// <summary>
    /// Generate a style for a layer automatically
    /// </summary>
    public GeneratedStyle GenerateStyle(StyleGenerationRequest request)
    {
        var style = new GeneratedStyle
        {
            StyleId = request.StyleId ?? $"auto-style-{Guid.NewGuid():N}",
            Title = request.Title ?? "Auto-Generated Style",
            GeometryType = NormalizeGeometryType(request.GeometryType)
        };

        // If field is specified, analyze it for data-driven styling
        if (!string.IsNullOrEmpty(request.FieldName) && request.FieldValues != null)
        {
            var analysis = _dataAnalyzer.AnalyzeField(request.FieldValues, request.FieldName);
            style.FieldAnalysis = analysis;

            // Generate data-driven style
            var styleDefinition = GenerateDataDrivenStyle(request, analysis);
            style.StyleDefinition = styleDefinition;
            style.MapLibreStyle = ConvertToMapLibreStyle(styleDefinition, request);
        }
        else
        {
            // Generate simple style
            var styleDefinition = GenerateSimpleStyle(request);
            style.StyleDefinition = styleDefinition;
            style.MapLibreStyle = ConvertToMapLibreStyle(styleDefinition, request);
        }

        // Add clustering/heatmap recommendations for points
        if (request.GeometryType?.ToLowerInvariant() == "point" && request.Coordinates != null)
        {
            var geometryAnalysis = _dataAnalyzer.AnalyzeGeometryDistribution(request.Coordinates);
            style.GeometryAnalysis = geometryAnalysis;

            if (geometryAnalysis.ShouldUseHeatmap)
            {
                style.Recommendations.Add("Consider using a heatmap for better visualization of high-density point data");
            }
            else if (geometryAnalysis.ShouldCluster)
            {
                style.Recommendations.Add("Consider enabling point clustering for better performance");
            }
        }

        return style;
    }

    /// <summary>
    /// Generate data-driven style based on field analysis
    /// </summary>
    private StyleDefinition GenerateDataDrivenStyle(StyleGenerationRequest request, FieldAnalysisResult analysis)
    {
        var geometryType = NormalizeGeometryType(request.GeometryType);

        // For categorical data, use unique value renderer
        if (analysis.IsCategorical && analysis.CategoryCounts != null)
        {
            return GenerateCategoricalStyle(request, analysis, geometryType);
        }

        // For numeric data, use graduated/choropleth renderer
        if (analysis.DataType == DataType.Numeric)
        {
            return GenerateChoroplethStyle(request, analysis, geometryType);
        }

        // For temporal data, use temporal styling
        if (analysis.DataType == DataType.DateTime)
        {
            return GenerateTemporalStyle(request, analysis, geometryType);
        }

        // Fallback to simple style
        return GenerateSimpleStyle(request);
    }

    /// <summary>
    /// Generate categorical/unique value style
    /// </summary>
    private StyleDefinition GenerateCategoricalStyle(StyleGenerationRequest request, FieldAnalysisResult analysis, string geometryType)
    {
        var categories = analysis.CategoryCounts!
            .OrderByDescending(c => c.Value)
            .Take(Math.Min(analysis.SuggestedClasses, 12))
            .ToList();

        var paletteName = request.ColorPalette ?? analysis.GetRecommendedPalette();
        var colors = CartographicPalettes.GetPalette(paletteName, categories.Count);

        var classes = new List<UniqueValueStyleClassDefinition>();
        for (int i = 0; i < categories.Count; i++)
        {
            var category = categories[i];
            var color = colors[i % colors.Length];

            classes.Add(new UniqueValueStyleClassDefinition
            {
                Value = category.Key,
                Symbol = CreateSymbol(geometryType, color, request.Opacity ?? 0.8)
            });
        }

        return new StyleDefinition
        {
            Id = request.StyleId ?? $"categorical-{Guid.NewGuid():N}",
            Title = request.Title ?? $"Categorical: {analysis.FieldName}",
            Renderer = "uniqueValue",
            GeometryType = geometryType,
            Format = "legacy",
            UniqueValue = new UniqueValueStyleDefinition
            {
                Field = analysis.FieldName,
                Classes = classes,
                DefaultSymbol = CreateSymbol(geometryType, "#CCCCCC", 0.5)
            }
        };
    }

    /// <summary>
    /// Generate choropleth/graduated color style for numeric data
    /// </summary>
    private StyleDefinition GenerateChoroplethStyle(StyleGenerationRequest request, FieldAnalysisResult analysis, string geometryType)
    {
        var numericValues = request.FieldValues!
            .Select(v => Convert.ToDouble(v))
            .Where(v => !double.IsNaN(v) && !double.IsInfinity(v))
            .OrderBy(v => v)
            .ToList();

        // Determine classification method
        var method = request.ClassificationMethod ?? ClassificationStrategy.GetRecommendedMethod(numericValues);

        // Determine number of classes
        var classCount = analysis.SuggestedClasses > 0 ? analysis.SuggestedClasses : 7;
        if (request.ClassCount.HasValue)
        {
            classCount = Math.Clamp(request.ClassCount.Value, 3, 12);
        }

        // Classify data
        var breaks = ClassificationStrategy.Classify(numericValues, classCount, method);

        // Get color palette
        var paletteName = request.ColorPalette ?? analysis.GetRecommendedPalette();
        var colors = CartographicPalettes.GetPalette(paletteName, classCount);

        // Create graduated rules
        var rules = new List<StyleRuleDefinition>();
        var allBreaks = new List<double> { numericValues.First() };
        allBreaks.AddRange(breaks);
        allBreaks.Add(numericValues.Last() + 0.001); // Ensure last value is included

        for (int i = 0; i < classCount; i++)
        {
            var minValue = allBreaks[i];
            var maxValue = allBreaks[i + 1];
            var color = colors[i];

            var label = i == 0
                ? $"≤ {maxValue:N2}"
                : i == classCount - 1
                    ? $"> {minValue:N2}"
                    : $"{minValue:N2} - {maxValue:N2}";

            rules.Add(new StyleRuleDefinition
            {
                Id = $"class-{i}",
                Label = label,
                Symbolizer = CreateSymbol(geometryType, color, request.Opacity ?? 0.7)
            });
        }

        return new StyleDefinition
        {
            Id = request.StyleId ?? $"choropleth-{Guid.NewGuid():N}",
            Title = request.Title ?? $"Choropleth: {analysis.FieldName}",
            Renderer = "classBreaks",
            GeometryType = geometryType,
            Format = "legacy",
            Rules = rules
        };
    }

    /// <summary>
    /// Generate temporal style for time-series data
    /// </summary>
    private StyleDefinition GenerateTemporalStyle(StyleGenerationRequest request, FieldAnalysisResult analysis, string geometryType)
    {
        var dates = request.FieldValues!
            .Select(v =>
            {
                if (v is DateTime dt) return dt;
                if (v is DateTimeOffset dto) return dto.DateTime;
                if (v is string s && DateTime.TryParse(s, out var parsed)) return parsed;
                return DateTime.MinValue;
            })
            .Where(d => d != DateTime.MinValue)
            .OrderBy(d => d)
            .ToList();

        var paletteName = request.ColorPalette ?? "YlGnBu";
        var classCount = Math.Min(analysis.SuggestedClasses, 10);
        var colors = CartographicPalettes.GetPalette(paletteName, classCount);

        // Create temporal breaks
        var timeSpan = dates.Last() - dates.First();
        var intervalTicks = timeSpan.Ticks / classCount;

        var rules = new List<StyleRuleDefinition>();
        for (int i = 0; i < classCount; i++)
        {
            var startDate = dates.First().AddTicks(intervalTicks * i);
            var endDate = dates.First().AddTicks(intervalTicks * (i + 1));
            var color = colors[i];

            rules.Add(new StyleRuleDefinition
            {
                Id = $"temporal-{i}",
                Label = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
                Symbolizer = CreateSymbol(geometryType, color, request.Opacity ?? 0.7)
            });
        }

        return new StyleDefinition
        {
            Id = request.StyleId ?? $"temporal-{Guid.NewGuid():N}",
            Title = request.Title ?? $"Temporal: {analysis.FieldName}",
            Renderer = "temporal",
            GeometryType = geometryType,
            Format = "legacy",
            Rules = rules
        };
    }

    /// <summary>
    /// Generate simple style without data-driven symbology
    /// </summary>
    private StyleDefinition GenerateSimpleStyle(StyleGenerationRequest request)
    {
        var geometryType = NormalizeGeometryType(request.GeometryType);
        var color = request.BaseColor ?? GetDefaultColorForGeometry(geometryType);

        return new StyleDefinition
        {
            Id = request.StyleId ?? $"simple-{Guid.NewGuid():N}",
            Title = request.Title ?? "Simple Style",
            Renderer = "simple",
            GeometryType = geometryType,
            Format = "legacy",
            Simple = CreateSymbol(geometryType, color, request.Opacity ?? 0.7)
        };
    }

    /// <summary>
    /// Create a symbol based on geometry type
    /// </summary>
    private SimpleStyleDefinition CreateSymbol(string geometryType, string color, double opacity)
    {
        return geometryType switch
        {
            "point" => new SimpleStyleDefinition
            {
                SymbolType = "shape",
                FillColor = color,
                StrokeColor = DarkenColor(color, 0.3),
                StrokeWidth = 1.5,
                Size = 8,
                Opacity = opacity
            },
            "line" => new SimpleStyleDefinition
            {
                SymbolType = "shape",
                StrokeColor = color,
                StrokeWidth = 2.5,
                StrokeStyle = "solid",
                Opacity = opacity
            },
            "polygon" => new SimpleStyleDefinition
            {
                SymbolType = "shape",
                FillColor = color,
                StrokeColor = DarkenColor(color, 0.3),
                StrokeWidth = 1.5,
                Opacity = opacity
            },
            _ => new SimpleStyleDefinition
            {
                FillColor = color,
                Opacity = opacity
            }
        };
    }

    /// <summary>
    /// Convert Honua StyleDefinition to MapLibre GL JS style
    /// </summary>
    private JsonObject ConvertToMapLibreStyle(StyleDefinition styleDefinition, StyleGenerationRequest request)
    {
        var layerId = request.LayerId ?? "layer";
        var sourceId = request.SourceId ?? "source";
        var geometryType = NormalizeGeometryType(styleDefinition.GeometryType);
        var layerType = MapGeometryToLayerType(geometryType);

        var layer = new JsonObject
        {
            ["id"] = layerId,
            ["type"] = layerType,
            ["source"] = sourceId
        };

        // Handle different renderer types
        if (styleDefinition.Renderer == "uniqueValue" && styleDefinition.UniqueValue != null)
        {
            layer["paint"] = CreateUniqueValuePaint(styleDefinition.UniqueValue, layerType);
        }
        else if (styleDefinition.Rules.Count > 0)
        {
            layer["paint"] = CreateGraduatedPaint(styleDefinition.Rules, layerType);
        }
        else if (styleDefinition.Simple != null)
        {
            layer["paint"] = CreateSimplePaint(styleDefinition.Simple, layerType);
        }

        return new JsonObject
        {
            ["version"] = 8,
            ["layers"] = new JsonArray { layer }
        };
    }

    private JsonObject CreateUniqueValuePaint(UniqueValueStyleDefinition uniqueValue, string layerType)
    {
        var paint = new JsonObject();
        var colorProperty = GetColorPropertyName(layerType);

        // Build match expression
        var matchExpression = new JsonArray { "match", new JsonArray { "get", uniqueValue.Field } };

        foreach (var classItem in uniqueValue.Classes)
        {
            matchExpression.Add(classItem.Value);
            var color = GetPrimaryColor(classItem.Symbol, layerType);
            matchExpression.Add(color);
        }

        // Default color
        var defaultColor = uniqueValue.DefaultSymbol?.FillColor ?? "#CCCCCC";
        matchExpression.Add(defaultColor);

        paint[colorProperty] = matchExpression;

        // Add other properties
        if (layerType == "circle" && uniqueValue.Classes.Any())
        {
            var firstSymbol = uniqueValue.Classes.First().Symbol;
            if (firstSymbol.Size.HasValue)
            {
                paint["circle-radius"] = firstSymbol.Size.Value / 2;
            }
            if (firstSymbol.StrokeColor != null)
            {
                paint["circle-stroke-color"] = firstSymbol.StrokeColor;
                paint["circle-stroke-width"] = firstSymbol.StrokeWidth ?? 1;
            }
        }

        return paint;
    }

    private JsonObject CreateGraduatedPaint(IReadOnlyList<StyleRuleDefinition> rules, string layerType)
    {
        var paint = new JsonObject();
        var firstSymbol = rules.First().Symbolizer;

        // For now, use first rule's color (would need data expression for full graduated colors)
        var colorProperty = GetColorPropertyName(layerType);
        paint[colorProperty] = GetPrimaryColor(firstSymbol, layerType);

        if (layerType == "circle")
        {
            paint["circle-radius"] = firstSymbol.Size ?? 6;
            if (firstSymbol.StrokeColor != null)
            {
                paint["circle-stroke-color"] = firstSymbol.StrokeColor;
                paint["circle-stroke-width"] = firstSymbol.StrokeWidth ?? 1;
            }
        }
        else if (layerType == "line")
        {
            paint["line-width"] = firstSymbol.StrokeWidth ?? 2;
        }

        return paint;
    }

    private JsonObject CreateSimplePaint(SimpleStyleDefinition symbol, string layerType)
    {
        var paint = new JsonObject();

        switch (layerType)
        {
            case "fill":
                if (symbol.FillColor != null) paint["fill-color"] = symbol.FillColor;
                if (symbol.Opacity.HasValue) paint["fill-opacity"] = symbol.Opacity.Value;
                if (symbol.StrokeColor != null) paint["fill-outline-color"] = symbol.StrokeColor;
                break;

            case "line":
                if (symbol.StrokeColor != null) paint["line-color"] = symbol.StrokeColor;
                if (symbol.StrokeWidth.HasValue) paint["line-width"] = symbol.StrokeWidth.Value;
                if (symbol.Opacity.HasValue) paint["line-opacity"] = symbol.Opacity.Value;
                break;

            case "circle":
                if (symbol.Size.HasValue) paint["circle-radius"] = symbol.Size.Value / 2;
                if (symbol.FillColor != null) paint["circle-color"] = symbol.FillColor;
                if (symbol.Opacity.HasValue) paint["circle-opacity"] = symbol.Opacity.Value;
                if (symbol.StrokeColor != null) paint["circle-stroke-color"] = symbol.StrokeColor;
                if (symbol.StrokeWidth.HasValue) paint["circle-stroke-width"] = symbol.StrokeWidth.Value;
                break;
        }

        return paint;
    }

    private string GetColorPropertyName(string layerType)
    {
        return layerType switch
        {
            "fill" => "fill-color",
            "line" => "line-color",
            "circle" => "circle-color",
            _ => "fill-color"
        };
    }

    private string GetPrimaryColor(SimpleStyleDefinition symbol, string layerType)
    {
        return layerType == "line" ? symbol.StrokeColor ?? "#000000" : symbol.FillColor ?? "#000000";
    }

    private string MapGeometryToLayerType(string geometryType)
    {
        return geometryType switch
        {
            "point" => "circle",
            "line" => "line",
            "polygon" => "fill",
            _ => "fill"
        };
    }

    private string NormalizeGeometryType(string? geometryType)
    {
        if (string.IsNullOrWhiteSpace(geometryType)) return "polygon";

        return geometryType.Trim().ToLowerInvariant() switch
        {
            "point" or "multipoint" => "point",
            "line" or "linestring" or "multilinestring" or "polyline" => "line",
            "polygon" or "multipolygon" => "polygon",
            _ => "polygon"
        };
    }

    private string GetDefaultColorForGeometry(string geometryType)
    {
        return geometryType switch
        {
            "point" => "#3B82F6",
            "line" => "#10B981",
            "polygon" => "#F59E0B",
            _ => "#3B82F6"
        };
    }

    private string DarkenColor(string hexColor, double factor)
    {
        if (!hexColor.StartsWith("#") || hexColor.Length != 7)
        {
            return hexColor;
        }

        var r = Convert.ToInt32(hexColor.Substring(1, 2), 16);
        var g = Convert.ToInt32(hexColor.Substring(3, 2), 16);
        var b = Convert.ToInt32(hexColor.Substring(5, 2), 16);

        r = (int)(r * (1 - factor));
        g = (int)(g * (1 - factor));
        b = (int)(b * (1 - factor));

        return $"#{r:X2}{g:X2}{b:X2}";
    }
}

/// <summary>
/// Request for automatic style generation
/// </summary>
public class StyleGenerationRequest
{
    public string? StyleId { get; set; }
    public string? Title { get; set; }
    public required string GeometryType { get; set; }
    public string? LayerId { get; set; }
    public string? SourceId { get; set; }

    // Data-driven styling
    public string? FieldName { get; set; }
    public IEnumerable<object?>? FieldValues { get; set; }

    // Geometry analysis
    public IEnumerable<(double x, double y)>? Coordinates { get; set; }

    // Style preferences
    public string? ColorPalette { get; set; }
    public string? BaseColor { get; set; }
    public double? Opacity { get; set; }
    public int? ClassCount { get; set; }
    public ClassificationMethod? ClassificationMethod { get; set; }
}

/// <summary>
/// Result of automatic style generation
/// </summary>
public class GeneratedStyle
{
    public required string StyleId { get; set; }
    public required string Title { get; set; }
    public required string GeometryType { get; set; }
    public required StyleDefinition StyleDefinition { get; set; }
    public required JsonObject MapLibreStyle { get; set; }

    public FieldAnalysisResult? FieldAnalysis { get; set; }
    public GeometryAnalysisResult? GeometryAnalysis { get; set; }

    public List<string> Recommendations { get; set; } = new();
}
