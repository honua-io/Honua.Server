// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Globalization;
using Honua.Server.Core.Metadata;
using YamlDotNet.RepresentationModel;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Styling;

/// <summary>
/// Parses YSLD (YAML SLD) into StyleDefinition
/// </summary>
public static class YsldParser
{
    /// <summary>
    /// Parse YSLD YAML string into StyleDefinition
    /// </summary>
    public static StyleDefinition Parse(string ysld, string styleId)
    {
        Guard.NotNullOrWhiteSpace(ysld);
        Guard.NotNullOrWhiteSpace(styleId);

        var yaml = new YamlStream();
        using var reader = new StringReader(ysld);
        yaml.Load(reader);

        if (yaml.Documents.Count == 0)
        {
            throw new InvalidOperationException("Invalid YSLD: document is empty");
        }

        var root = (YamlMappingNode)yaml.Documents[0].RootNode;

        var name = GetScalarValue(root, "name") ?? styleId;
        var title = GetScalarValue(root, "title") ?? name;

        var rules = ParseFeatureStyles(root);
        var geometryType = InferGeometryType(rules);

        return new StyleDefinition
        {
            Id = styleId,
            Title = title,
            Format = "ysld",
            GeometryType = geometryType,
            Rules = rules.ToArray()
        };
    }

    private static List<StyleRuleDefinition> ParseFeatureStyles(YamlMappingNode root)
    {
        var allRules = new List<StyleRuleDefinition>();

        if (!root.Children.TryGetValue(new YamlScalarNode("feature-styles"), out var featureStylesNode))
        {
            return allRules;
        }

        if (featureStylesNode is not YamlSequenceNode featureStylesSeq)
        {
            return allRules;
        }

        foreach (var featureStyleNode in featureStylesSeq.Children)
        {
            if (featureStyleNode is not YamlMappingNode featureStyleMap)
            {
                continue;
            }

            if (!featureStyleMap.Children.TryGetValue(new YamlScalarNode("rules"), out var rulesNode))
            {
                continue;
            }

            if (rulesNode is not YamlSequenceNode rulesSeq)
            {
                continue;
            }

            foreach (var ruleNode in rulesSeq.Children)
            {
                if (ruleNode is YamlMappingNode ruleMap)
                {
                    var rule = ParseRule(ruleMap);
                    if (rule != null)
                    {
                        allRules.Add(rule);
                    }
                }
            }
        }

        return allRules;
    }

    private static StyleRuleDefinition? ParseRule(YamlMappingNode ruleMap)
    {
        var name = GetScalarValue(ruleMap, "name") ?? $"rule-{Guid.NewGuid():N}";
        var filterStr = GetScalarValue(ruleMap, "filter");
        var filter = ParseFilter(filterStr);

        double? minScale = null;
        double? maxScale = null;

        if (ruleMap.Children.TryGetValue(new YamlScalarNode("scale"), out var scaleNode) && scaleNode is YamlMappingNode scaleMap)
        {
            var minStr = GetScalarValue(scaleMap, "min");
            var maxStr = GetScalarValue(scaleMap, "max");
            minScale = ParseDouble(minStr);
            maxScale = ParseDouble(maxStr);
        }

        if (!ruleMap.Children.TryGetValue(new YamlScalarNode("symbolizers"), out var symbolizersNode))
        {
            return null;
        }

        if (symbolizersNode is not YamlSequenceNode symbolizersSeq || symbolizersSeq.Children.Count == 0)
        {
            return null;
        }

        var symbolizer = ParseSymbolizer(symbolizersSeq.Children[0]);
        if (symbolizer == null)
        {
            return null;
        }

        return new StyleRuleDefinition
        {
            Id = name,
            IsDefault = string.IsNullOrWhiteSpace(filterStr),
            Filter = filter,
            MinScale = minScale,
            MaxScale = maxScale,
            Symbolizer = symbolizer
        };
    }

    private static RuleFilterDefinition? ParseFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return null;
        }

        var parts = filter.Split('=', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2)
        {
            var field = parts[0].Trim();
            var value = parts[1].Trim().Trim('\'', '"');
            return new RuleFilterDefinition(field, value);
        }

        return null;
    }

    private static SimpleStyleDefinition? ParseSymbolizer(YamlNode symbolizerNode)
    {
        if (symbolizerNode is not YamlMappingNode symbolizerMap)
        {
            return null;
        }

        // Check for each symbolizer type
        if (symbolizerMap.Children.TryGetValue(new YamlScalarNode("point"), out var pointNode) && pointNode is YamlMappingNode pointMap)
        {
            return ParsePointSymbolizer(pointMap);
        }

        if (symbolizerMap.Children.TryGetValue(new YamlScalarNode("line"), out var lineNode) && lineNode is YamlMappingNode lineMap)
        {
            return ParseLineSymbolizer(lineMap);
        }

        if (symbolizerMap.Children.TryGetValue(new YamlScalarNode("polygon"), out var polygonNode) && polygonNode is YamlMappingNode polygonMap)
        {
            return ParsePolygonSymbolizer(polygonMap);
        }

        if (symbolizerMap.Children.TryGetValue(new YamlScalarNode("raster"), out var rasterNode) && rasterNode is YamlMappingNode rasterMap)
        {
            return ParseRasterSymbolizer(rasterMap);
        }

        return null;
    }

    private static SimpleStyleDefinition ParsePointSymbolizer(YamlMappingNode pointMap)
    {
        var size = ParseDouble(GetScalarValue(pointMap, "size"));

        // Check for symbols array
        if (pointMap.Children.TryGetValue(new YamlScalarNode("symbols"), out var symbolsNode) && symbolsNode is YamlSequenceNode symbolsSeq && symbolsSeq.Children.Count > 0)
        {
            if (symbolsSeq.Children[0] is YamlMappingNode symbolMap)
            {
                // Check for external-graphic
                if (symbolMap.Children.TryGetValue(new YamlScalarNode("external-graphic"), out var egNode) && egNode is YamlMappingNode egMap)
                {
                    var url = GetScalarValue(egMap, "url");
                    return new SimpleStyleDefinition
                    {
                        SymbolType = "icon",
                        IconHref = url,
                        Size = size
                    };
                }

                // Check for mark
                if (symbolMap.Children.TryGetValue(new YamlScalarNode("mark"), out var markNode) && markNode is YamlMappingNode markMap)
                {
                    var fillColor = GetColorFromNode(markMap, "fill");
                    var fillOpacity = GetOpacityFromNode(markMap, "fill");
                    var strokeColor = GetColorFromNode(markMap, "stroke");
                    var strokeWidth = GetWidthFromNode(markMap, "stroke");

                    return new SimpleStyleDefinition
                    {
                        SymbolType = "shape",
                        FillColor = fillColor,
                        StrokeColor = strokeColor,
                        StrokeWidth = strokeWidth,
                        Size = size,
                        Opacity = fillOpacity
                    };
                }
            }
        }

        // Check for graphic
        if (pointMap.Children.TryGetValue(new YamlScalarNode("graphic"), out var graphicNode) && graphicNode is YamlMappingNode graphicMap)
        {
            var graphicSize = ParseDouble(GetScalarValue(graphicMap, "size")) ?? size;

            if (graphicMap.Children.TryGetValue(new YamlScalarNode("symbols"), out var gSymbolsNode) && gSymbolsNode is YamlSequenceNode gSymbolsSeq && gSymbolsSeq.Children.Count > 0)
            {
                if (gSymbolsSeq.Children[0] is YamlMappingNode gSymbolMap)
                {
                    if (gSymbolMap.Children.TryGetValue(new YamlScalarNode("mark"), out var gMarkNode) && gMarkNode is YamlMappingNode gMarkMap)
                    {
                        var fillColor = GetColorFromNode(gMarkMap, "fill");
                        var fillOpacity = GetOpacityFromNode(gMarkMap, "fill");
                        var strokeColor = GetColorFromNode(gMarkMap, "stroke");
                        var strokeWidth = GetWidthFromNode(gMarkMap, "stroke");

                        return new SimpleStyleDefinition
                        {
                            SymbolType = "shape",
                            FillColor = fillColor,
                            StrokeColor = strokeColor,
                            StrokeWidth = strokeWidth,
                            Size = graphicSize,
                            Opacity = fillOpacity
                        };
                    }
                }
            }
        }

        return new SimpleStyleDefinition
        {
            SymbolType = "shape",
            Size = size
        };
    }

    private static SimpleStyleDefinition ParseLineSymbolizer(YamlMappingNode lineMap)
    {
        var strokeColor = GetColorFromNode(lineMap, "stroke");
        var strokeWidth = GetWidthFromNode(lineMap, "stroke");
        var strokeOpacity = GetOpacityFromNode(lineMap, "stroke");

        return new SimpleStyleDefinition
        {
            SymbolType = "shape",
            StrokeColor = strokeColor,
            StrokeWidth = strokeWidth,
            Opacity = strokeOpacity
        };
    }

    private static SimpleStyleDefinition ParsePolygonSymbolizer(YamlMappingNode polygonMap)
    {
        var fillColor = GetColorFromNode(polygonMap, "fill");
        var fillOpacity = GetOpacityFromNode(polygonMap, "fill");
        var strokeColor = GetColorFromNode(polygonMap, "stroke");
        var strokeWidth = GetWidthFromNode(polygonMap, "stroke");

        return new SimpleStyleDefinition
        {
            SymbolType = "shape",
            FillColor = fillColor,
            StrokeColor = strokeColor,
            StrokeWidth = strokeWidth,
            Opacity = fillOpacity
        };
    }

    private static SimpleStyleDefinition ParseRasterSymbolizer(YamlMappingNode rasterMap)
    {
        var opacity = ParseDouble(GetScalarValue(rasterMap, "opacity"));

        return new SimpleStyleDefinition
        {
            SymbolType = "raster",
            Opacity = opacity
        };
    }

    private static string? GetScalarValue(YamlMappingNode map, string key)
    {
        if (map.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlScalarNode scalar)
        {
            return scalar.Value;
        }
        return null;
    }

    private static string? GetColorFromNode(YamlMappingNode parentMap, string nodeName)
    {
        if (parentMap.Children.TryGetValue(new YamlScalarNode(nodeName), out var node) && node is YamlMappingNode map)
        {
            return GetScalarValue(map, "color");
        }
        return null;
    }

    private static double? GetOpacityFromNode(YamlMappingNode parentMap, string nodeName)
    {
        if (parentMap.Children.TryGetValue(new YamlScalarNode(nodeName), out var node) && node is YamlMappingNode map)
        {
            return ParseDouble(GetScalarValue(map, "opacity"));
        }
        return null;
    }

    private static double? GetWidthFromNode(YamlMappingNode parentMap, string nodeName)
    {
        if (parentMap.Children.TryGetValue(new YamlScalarNode(nodeName), out var node) && node is YamlMappingNode map)
        {
            return ParseDouble(GetScalarValue(map, "width"));
        }
        return null;
    }

    private static double? ParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private static string InferGeometryType(List<StyleRuleDefinition> rules)
    {
        if (rules.Count == 0)
        {
            return "polygon";
        }

        var symbolizer = rules[0].Symbolizer;
        if (symbolizer == null)
        {
            return "polygon";
        }

        // Check if it's a point based on size property (points have size, polygons/lines don't)
        if (symbolizer.Size.HasValue)
        {
            return "point";
        }

        var symbolType = symbolizer.SymbolType?.ToLowerInvariant();
        return symbolType switch
        {
            "icon" => "point",
            "shape" when !string.IsNullOrWhiteSpace(symbolizer.IconHref) => "point",
            "raster" => "raster",
            "shape" when string.IsNullOrWhiteSpace(symbolizer.FillColor) && !string.IsNullOrWhiteSpace(symbolizer.StrokeColor) => "line",
            _ => "polygon"
        };
    }
}
