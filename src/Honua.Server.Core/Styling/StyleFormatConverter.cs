// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Honua.Server.Core.Metadata;
using SharpKml.Base;
using SharpKml.Dom;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Styling;

public static class StyleFormatConverter
{
    private static readonly XNamespace Sld = "http://www.opengis.net/sld";
    private static readonly XNamespace Ogc = "http://www.opengis.net/ogc";

    public static string CreateSld(StyleDefinition style, string layerName, string? geometryType = null)
    {
        Guard.NotNull(style);
        Guard.NotNullOrWhiteSpace(layerName);

        var resolvedGeometry = NormalizeGeometryType(geometryType ?? style.GeometryType);
        var rules = ResolveRules(style);

        var featureTypeStyle = new XElement(Sld + "FeatureTypeStyle");
        foreach (var rule in rules)
        {
            var ruleNode = new XElement(Sld + "Rule");

            if (rule.Id.HasValue())
            {
                ruleNode.Add(new XElement(Sld + "Name", rule.Id));
            }

            if (rule.Filter is { Field: { } field, Value: { } value })
            {
                ruleNode.Add(new XElement(Ogc + "Filter",
                    new XElement(Ogc + "PropertyIsEqualTo",
                        new XElement(Ogc + "PropertyName", field),
                        new XElement(Ogc + "Literal", value))));
            }

            if (rule.MinScale is double minScale)
            {
                ruleNode.Add(new XElement(Sld + "MinScaleDenominator", minScale.ToString(CultureInfo.InvariantCulture)));
            }

            if (rule.MaxScale is double maxScale)
            {
                ruleNode.Add(new XElement(Sld + "MaxScaleDenominator", maxScale.ToString(CultureInfo.InvariantCulture)));
            }

            var symbol = rule.Symbolizer;
            switch (resolvedGeometry)
            {
                case "point":
                    ruleNode.Add(CreatePointSymbolizer(symbol));
                    break;
                case "line":
                    ruleNode.Add(CreateLineSymbolizer(symbol));
                    break;
                case "raster":
                    ruleNode.Add(CreateRasterSymbolizer(symbol));
                    break;
                default:
                    ruleNode.Add(CreatePolygonSymbolizer(symbol));
                    break;
            }

            featureTypeStyle.Add(ruleNode);
        }

        var document = new XDocument(
            new XElement(Sld + "StyledLayerDescriptor",
                new XAttribute("version", "1.0.0"),
                new XAttribute(XNamespace.Xmlns + "sld", Sld),
                new XAttribute(XNamespace.Xmlns + "ogc", Ogc),
                new XElement(Sld + "NamedLayer",
                    new XElement(Sld + "Name", layerName),
                    new XElement(Sld + "UserStyle",
                        new XElement(Sld + "Name", style.Id),
                        new XElement(Sld + "Title", style.Title ?? style.Id),
                        featureTypeStyle))));

        return document.ToString(SaveOptions.DisableFormatting);
    }

    public static JsonObject CreateEsriDrawingInfo(StyleDefinition style, string geometryType)
    {
        Guard.NotNull(style);

        var normalizedGeometry = NormalizeGeometryType(geometryType.IsNullOrWhiteSpace() ? style.GeometryType : geometryType);
        var renderer = style.UniqueValue is not null
            ? CreateUniqueValueRenderer(style.UniqueValue, normalizedGeometry)
            : CreateSimpleRenderer(ResolveSimpleSymbol(style) ?? new SimpleStyleDefinition(), normalizedGeometry);

        return new JsonObject
        {
            ["renderer"] = renderer
        };
    }

    public static Style? CreateKmlStyle(StyleDefinition style, string styleId, string geometryType)
    {
        Guard.NotNull(style);
        Guard.NotNullOrWhiteSpace(styleId);

        var symbol = ResolveSimpleSymbol(style);
        if (symbol is null)
        {
            return null;
        }

        var normalizedGeometry = NormalizeGeometryType(geometryType.IsNullOrWhiteSpace() ? style.GeometryType : geometryType);
        var fill = ParseColor(symbol.FillColor, DefaultFill);
        fill = fill with { A = ResolveAlpha(symbol.Opacity, fill.A) };

        var stroke = ParseColor(symbol.StrokeColor, DefaultStroke);
        stroke = stroke with { A = ResolveAlpha(symbol.Opacity, stroke.A) };

        var kmlStyle = new Style { Id = styleId };

        switch (normalizedGeometry)
        {
            case "point":
                kmlStyle.Icon = new IconStyle
                {
                    Color = Color32.Parse(fill.ToKml()),
                    Scale = symbol.Size is double size ? Math.Max(size / 16d, 0.1d) : 1d,
                    Icon = symbol.IconHref.IsNullOrWhiteSpace() ? null : new IconStyle.IconLink(new Uri(symbol.IconHref, UriKind.RelativeOrAbsolute))
                };
                break;
            case "line":
                kmlStyle.Line = new LineStyle
                {
                    Color = Color32.Parse(stroke.ToKml()),
                    Width = symbol.StrokeWidth ?? 2d
                };
                break;
            default:
                kmlStyle.Polygon = new PolygonStyle
                {
                    Color = Color32.Parse(fill.ToKml()),
                    Fill = true,
                    Outline = true
                };
                kmlStyle.Line = new LineStyle
                {
                    Color = Color32.Parse(stroke.ToKml()),
                    Width = symbol.StrokeWidth ?? 1.5d
                };
                break;
        }

        return kmlStyle;
    }

    private static IReadOnlyList<StyleRuleDefinition> ResolveRules(StyleDefinition style)
    {
        if (style.Rules.Count > 0)
        {
            return style.Rules;
        }

        if (string.Equals(style.Renderer, "uniqueValue", StringComparison.OrdinalIgnoreCase) && style.UniqueValue is not null)
        {
            var generated = BuildUniqueValueRules(style.UniqueValue);
            if (generated.Count > 0)
            {
                return generated;
            }
        }

        var symbol = ResolveSimpleSymbol(style) ?? new SimpleStyleDefinition();
        return new[]
        {
            new StyleRuleDefinition
            {
                Id = "default",
                IsDefault = true,
                Symbolizer = symbol
            }
        };
    }

    private static IReadOnlyList<StyleRuleDefinition> BuildUniqueValueRules(UniqueValueStyleDefinition uniqueValue)
    {
        var rules = new List<StyleRuleDefinition>();

        if (uniqueValue.DefaultSymbol is not null)
        {
            rules.Add(new StyleRuleDefinition
            {
                Id = "default",
                IsDefault = true,
                Symbolizer = uniqueValue.DefaultSymbol
            });
        }

        for (var index = 0; index < uniqueValue.Classes.Count; index++)
        {
            var valueClass = uniqueValue.Classes[index];
            if (valueClass is null)
            {
                continue;
            }

            var classId = valueClass.Value.IsNullOrWhiteSpace()
                ? $"class-{index + 1}"
                : valueClass.Value;

            rules.Add(new StyleRuleDefinition
            {
                Id = classId,
                Filter = uniqueValue.Field.IsNullOrWhiteSpace()
                    ? null
                    : new RuleFilterDefinition(uniqueValue.Field, valueClass.Value),
                Symbolizer = valueClass.Symbol
            });
        }

        return rules;
    }

    private static SimpleStyleDefinition? ResolveSimpleSymbol(StyleDefinition style)
    {
        if (style.Rules.Count > 0)
        {
            return style.Rules.FirstOrDefault(rule => rule.IsDefault)?.Symbolizer
                   ?? style.Rules[0].Symbolizer;
        }

        if (string.Equals(style.Renderer, "uniqueValue", StringComparison.OrdinalIgnoreCase) && style.UniqueValue is not null)
        {
            return style.UniqueValue.DefaultSymbol ?? style.UniqueValue.Classes.FirstOrDefault()?.Symbol;
        }

        return style.Simple;
    }

    private static string NormalizeGeometryType(string? geometryType)
    {
        if (geometryType.IsNullOrWhiteSpace())
        {
            return "polygon";
        }

        return geometryType.Trim().ToLowerInvariant() switch
        {
            "point" => "point",
            "line" or "polyline" => "line",
            "raster" => "raster",
            _ => "polygon"
        };
    }

    private static JsonObject CreateSimpleRenderer(SimpleStyleDefinition symbol, string geometryType)
    {
        return new JsonObject
        {
            ["type"] = "simple",
            ["symbol"] = CreateSymbol(symbol, geometryType)
        };
    }

    private static JsonObject CreateUniqueValueRenderer(UniqueValueStyleDefinition uniqueValue, string geometryType)
    {
        var renderer = new JsonObject
        {
            ["type"] = "uniqueValue",
            ["field1"] = uniqueValue.Field
        };

        if (uniqueValue.DefaultSymbol is not null)
        {
            renderer["defaultSymbol"] = CreateSymbol(uniqueValue.DefaultSymbol, geometryType);
        }

        var infos = new JsonArray();
        foreach (var valueClass in uniqueValue.Classes)
        {
            infos.Add(new JsonObject
            {
                ["value"] = valueClass.Value,
                ["symbol"] = CreateSymbol(valueClass.Symbol, geometryType)
            });
        }

        renderer["uniqueValueInfos"] = infos;
        return renderer;
    }

    private static JsonObject CreateSymbol(SimpleStyleDefinition symbol, string geometryType)
    {
        return geometryType switch
        {
            "point" => CreatePointSymbol(symbol),
            "line" => CreateLineSymbol(symbol),
            _ => CreatePolygonSymbol(symbol)
        };
    }

    private static JsonObject CreatePointSymbol(SimpleStyleDefinition symbol)
    {
        var fill = ParseColor(symbol.FillColor, DefaultFill);
        fill = fill with { A = ResolveAlpha(symbol.Opacity, fill.A) };

        var stroke = ParseColor(symbol.StrokeColor, DefaultStroke);
        stroke = stroke with { A = ResolveAlpha(symbol.Opacity, stroke.A) };

        return new JsonObject
        {
            ["type"] = "esriSMS",
            ["style"] = "esriSMSCircle",
            ["color"] = CreateColorArray(fill),
            ["size"] = symbol.Size ?? 12d,
            ["outline"] = new JsonObject
            {
                ["type"] = "esriSLS",
                ["style"] = "esriSLSSolid",
                ["color"] = CreateColorArray(stroke),
                ["width"] = symbol.StrokeWidth ?? 1.5d
            }
        };
    }

    private static JsonObject CreateLineSymbol(SimpleStyleDefinition symbol)
    {
        var stroke = ParseColor(symbol.StrokeColor, DefaultStroke);
        stroke = stroke with { A = ResolveAlpha(symbol.Opacity, stroke.A) };

        return new JsonObject
        {
            ["type"] = "esriSLS",
            ["style"] = "esriSLSSolid",
            ["color"] = CreateColorArray(stroke),
            ["width"] = symbol.StrokeWidth ?? 2d
        };
    }

    private static JsonObject CreatePolygonSymbol(SimpleStyleDefinition symbol)
    {
        var fill = ParseColor(symbol.FillColor, DefaultFill);
        fill = fill with { A = ResolveAlpha(symbol.Opacity, fill.A) };

        var stroke = ParseColor(symbol.StrokeColor, DefaultStroke);
        stroke = stroke with { A = ResolveAlpha(symbol.Opacity, stroke.A) };

        return new JsonObject
        {
            ["type"] = "esriSFS",
            ["style"] = "esriSFSSolid",
            ["color"] = CreateColorArray(fill),
            ["outline"] = new JsonObject
            {
                ["type"] = "esriSLS",
                ["style"] = "esriSLSSolid",
                ["color"] = CreateColorArray(stroke),
                ["width"] = symbol.StrokeWidth ?? 1.5d
            }
        };
    }

    private static XElement CreatePolygonSymbolizer(SimpleStyleDefinition symbol)
    {
        var fill = ParseColor(symbol.FillColor, DefaultFill);
        fill = fill with { A = ResolveAlpha(symbol.Opacity, fill.A) };

        var stroke = ParseColor(symbol.StrokeColor, DefaultStroke);
        stroke = stroke with { A = ResolveAlpha(symbol.Opacity, stroke.A) };

        return new XElement(Sld + "PolygonSymbolizer",
            new XElement(Sld + "Fill",
                new XElement(Sld + "CssParameter", new XAttribute("name", "fill"), fill.ToRgbHex()),
                new XElement(Sld + "CssParameter", new XAttribute("name", "fill-opacity"), fill.ToOpacityString())),
            new XElement(Sld + "Stroke",
                new XElement(Sld + "CssParameter", new XAttribute("name", "stroke"), stroke.ToRgbHex()),
                new XElement(Sld + "CssParameter", new XAttribute("name", "stroke-width"), (symbol.StrokeWidth ?? 1.5d).ToString(CultureInfo.InvariantCulture)),
                new XElement(Sld + "CssParameter", new XAttribute("name", "stroke-opacity"), stroke.ToOpacityString())));
    }

    private static XElement CreateLineSymbolizer(SimpleStyleDefinition symbol)
    {
        var stroke = ParseColor(symbol.StrokeColor, DefaultStroke);
        stroke = stroke with { A = ResolveAlpha(symbol.Opacity, stroke.A) };

        return new XElement(Sld + "LineSymbolizer",
            new XElement(Sld + "Stroke",
                new XElement(Sld + "CssParameter", new XAttribute("name", "stroke"), stroke.ToRgbHex()),
                new XElement(Sld + "CssParameter", new XAttribute("name", "stroke-width"), (symbol.StrokeWidth ?? 2d).ToString(CultureInfo.InvariantCulture)),
                new XElement(Sld + "CssParameter", new XAttribute("name", "stroke-opacity"), stroke.ToOpacityString())));
    }

    private static XElement CreatePointSymbolizer(SimpleStyleDefinition symbol)
    {
        var fill = ParseColor(symbol.FillColor, DefaultFill);
        fill = fill with { A = ResolveAlpha(symbol.Opacity, fill.A) };

        var stroke = ParseColor(symbol.StrokeColor, DefaultStroke);
        stroke = stroke with { A = ResolveAlpha(symbol.Opacity, stroke.A) };

        var mark = new XElement(Sld + "Mark",
            new XElement(Sld + "WellKnownName", "circle"),
            new XElement(Sld + "Fill",
                new XElement(Sld + "CssParameter", new XAttribute("name", "fill"), fill.ToRgbHex()),
                new XElement(Sld + "CssParameter", new XAttribute("name", "fill-opacity"), fill.ToOpacityString())),
            new XElement(Sld + "Stroke",
                new XElement(Sld + "CssParameter", new XAttribute("name", "stroke"), stroke.ToRgbHex()),
                new XElement(Sld + "CssParameter", new XAttribute("name", "stroke-width"), (symbol.StrokeWidth ?? 1.5d).ToString(CultureInfo.InvariantCulture)),
                new XElement(Sld + "CssParameter", new XAttribute("name", "stroke-opacity"), stroke.ToOpacityString())));

        return new XElement(Sld + "PointSymbolizer",
            new XElement(Sld + "Graphic",
                mark,
                new XElement(Sld + "Size", (symbol.Size ?? 12d).ToString(CultureInfo.InvariantCulture))));
    }

    private static XElement CreateRasterSymbolizer(SimpleStyleDefinition symbol)
    {
        var fill = ParseColor(symbol.FillColor, DefaultFill);
        fill = fill with { A = ResolveAlpha(symbol.Opacity, fill.A) };

        var opacity = fill.ToOpacityString();
        var colorMap = new XElement(Sld + "ColorMap",
            new XElement(Sld + "ColorMapEntry",
                new XAttribute("color", fill.ToRgbHex()),
                new XAttribute("opacity", opacity),
                new XAttribute("quantity", "0")));

        return new XElement(Sld + "RasterSymbolizer",
            new XElement(Sld + "Opacity", opacity),
            colorMap);
    }

    private static JsonArray CreateColorArray(Rgba color)
    {
        return new JsonArray(
            JsonValue.Create((int)color.R),
            JsonValue.Create((int)color.G),
            JsonValue.Create((int)color.B),
            JsonValue.Create((int)color.A));
    }

    private static byte ResolveAlpha(double? overrideOpacity, byte baseAlpha)
    {
        if (overrideOpacity is null)
        {
            return baseAlpha;
        }

        var scaled = (int)Math.Clamp(Math.Round(overrideOpacity.Value * 255d), 0d, 255d);
        return (byte)scaled;
    }

    private static Rgba ParseColor(string? value, Rgba fallback)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return fallback;
        }

        var hex = value.Trim();
        if (hex.StartsWith('#'))
        {
            hex = hex[1..];
        }

        if (hex.Length is not (6 or 8))
        {
            return fallback;
        }

        if (!byte.TryParse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) ||
            !byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) ||
            !byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return fallback;
        }

        var a = hex.Length == 8 && byte.TryParse(hex.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsedAlpha)
            ? parsedAlpha
            : (byte)255;

        return new Rgba(r, g, b, a);
    }

    private static readonly Rgba DefaultFill = new(0x4A, 0x90, 0xE2, 0xFF);
    private static readonly Rgba DefaultStroke = new(0x1F, 0x36, 0x4D, 0xFF);

    public static string CreateCss(StyleDefinition style, string? geometryType = null)
    {
        Guard.NotNull(style);

        var resolvedGeometry = NormalizeGeometryType(geometryType ?? style.GeometryType);
        var rules = ResolveRules(style);
        var sb = new StringBuilder();

        sb.AppendLine($"/* Style: {style.Title ?? style.Id} */");
        sb.AppendLine();

        foreach (var rule in rules)
        {
            AppendCssRule(sb, rule, resolvedGeometry);
        }

        return sb.ToString();
    }

    public static string CreateYsld(StyleDefinition style, string? geometryType = null)
    {
        Guard.NotNull(style);

        var resolvedGeometry = NormalizeGeometryType(geometryType ?? style.GeometryType);
        var rules = ResolveRules(style);
        var sb = new StringBuilder();

        sb.AppendLine($"name: {style.Id}");
        sb.AppendLine($"title: {style.Title ?? style.Id}");
        sb.AppendLine("feature-styles:");
        sb.AppendLine("- rules:");

        foreach (var rule in rules)
        {
            AppendYsldRule(sb, rule, resolvedGeometry);
        }

        return sb.ToString();
    }

    private static void AppendCssRule(StringBuilder sb, StyleRuleDefinition rule, string geometryType)
    {
        // Build selector
        var selector = BuildCssSelector(rule);
        sb.AppendLine($"{selector} {{");

        // Build declarations
        var symbol = rule.Symbolizer;
        if (symbol.FillColor.HasValue())
        {
            sb.AppendLine($"  fill: {symbol.FillColor};");
        }

        if (symbol.StrokeColor.HasValue())
        {
            sb.AppendLine($"  stroke: {symbol.StrokeColor};");
        }

        if (symbol.StrokeWidth is double strokeWidth)
        {
            sb.AppendLine($"  stroke-width: {strokeWidth.ToString(CultureInfo.InvariantCulture)};");
        }

        if (symbol.Size is double size)
        {
            sb.AppendLine($"  mark-size: {size.ToString(CultureInfo.InvariantCulture)};");
        }

        if (symbol.Opacity is double opacity)
        {
            sb.AppendLine($"  fill-opacity: {opacity.ToString(CultureInfo.InvariantCulture)};");
        }

        if (symbol.IconHref.HasValue())
        {
            sb.AppendLine($"  mark: url('{symbol.IconHref}');");
        }

        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static string BuildCssSelector(StyleRuleDefinition rule)
    {
        var parts = new List<string>();

        if (rule.IsDefault)
        {
            parts.Add("*");
        }

        if (rule.Filter is { Field: var field, Value: var value })
        {
            parts.Add($"[{field} = '{value}']");
        }

        if (rule.MinScale is double minScale)
        {
            parts.Add($"[@scale > {minScale.ToString(CultureInfo.InvariantCulture)}]");
        }

        if (rule.MaxScale is double maxScale)
        {
            parts.Add($"[@scale < {maxScale.ToString(CultureInfo.InvariantCulture)}]");
        }

        return parts.Count > 0 ? string.Join("", parts) : "*";
    }

    private static void AppendYsldRule(StringBuilder sb, StyleRuleDefinition rule, string geometryType)
    {
        sb.AppendLine($"  - name: {rule.Id}");

        if (rule.Filter is { Field: var field, Value: var value })
        {
            sb.AppendLine($"    filter: {field} = '{value}'");
        }

        if (rule.MinScale.HasValue || rule.MaxScale.HasValue)
        {
            sb.AppendLine("    scale:");
            if (rule.MinScale is double minScale && minScale > 0)
            {
                sb.AppendLine($"      min: {minScale.ToString(CultureInfo.InvariantCulture)}");
            }
            if (rule.MaxScale is double maxScale && maxScale > 0)
            {
                sb.AppendLine($"      max: {maxScale.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        sb.AppendLine("    symbolizers:");
        AppendYsldSymbolizer(sb, rule.Symbolizer, geometryType);
    }

    private static void AppendYsldSymbolizer(StringBuilder sb, SimpleStyleDefinition symbol, string geometryType)
    {
        switch (geometryType)
        {
            case "point":
                sb.AppendLine("    - point:");
                if (symbol.Size is double size)
                {
                    sb.AppendLine($"        size: {size.ToString(CultureInfo.InvariantCulture)}");
                }
                if (symbol.IconHref.HasValue())
                {
                    sb.AppendLine("        symbols:");
                    sb.AppendLine("        - external-graphic:");
                    sb.AppendLine($"            url: {symbol.IconHref}");
                }
                else
                {
                    sb.AppendLine("        symbols:");
                    sb.AppendLine("        - mark:");
                    sb.AppendLine("            shape: circle");
                    AppendYsldFillStroke(sb, symbol, "            ");
                }
                break;

            case "line":
                sb.AppendLine("    - line:");
                if (symbol.StrokeColor.HasValue() || symbol.StrokeWidth is not null)
                {
                    sb.AppendLine("        stroke:");
                    if (symbol.StrokeColor.HasValue())
                    {
                        sb.AppendLine($"          color: '{symbol.StrokeColor}'");
                    }
                    if (symbol.StrokeWidth is double strokeWidth)
                    {
                        sb.AppendLine($"          width: {strokeWidth.ToString(CultureInfo.InvariantCulture)}");
                    }
                }
                break;

            case "raster":
                sb.AppendLine("    - raster:");
                if (symbol.Opacity is double opacity)
                {
                    sb.AppendLine($"        opacity: {opacity.ToString(CultureInfo.InvariantCulture)}");
                }
                break;

            default: // polygon
                sb.AppendLine("    - polygon:");
                AppendYsldFillStroke(sb, symbol, "        ");
                break;
        }
    }

    private static void AppendYsldFillStroke(StringBuilder sb, SimpleStyleDefinition symbol, string indent)
    {
        if (symbol.FillColor.HasValue() || symbol.Opacity is not null)
        {
            sb.AppendLine($"{indent}fill:");
            if (symbol.FillColor.HasValue())
            {
                sb.AppendLine($"{indent}  color: '{symbol.FillColor}'");
            }
            if (symbol.Opacity is double opacity)
            {
                sb.AppendLine($"{indent}  opacity: {opacity.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        if (symbol.StrokeColor.HasValue() || symbol.StrokeWidth is not null)
        {
            sb.AppendLine($"{indent}stroke:");
            if (symbol.StrokeColor.HasValue())
            {
                sb.AppendLine($"{indent}  color: '{symbol.StrokeColor}'");
            }
            if (symbol.StrokeWidth is double strokeWidth)
            {
                sb.AppendLine($"{indent}  width: {strokeWidth.ToString(CultureInfo.InvariantCulture)}");
            }
        }
    }

    private readonly record struct Rgba(byte R, byte G, byte B, byte A)
    {
        public string ToRgbHex() => $"#{R:X2}{G:X2}{B:X2}";

        public string ToKml() => $"{A:X2}{B:X2}{G:X2}{R:X2}";

        public string ToOpacityString()
        {
            var fraction = Math.Clamp(A / 255d, 0d, 1d);
            return fraction.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }

    #region MapLibre Style Conversion

    /// <summary>
    /// Creates a MapLibre Style Specification v8 document from a Honua style definition
    /// </summary>
    public static JsonObject CreateMapLibreStyle(
        StyleDefinition style,
        string layerId,
        string sourceId,
        string? sourceLayer = null,
        string? styleName = null)
    {
        Guard.NotNull(style);
        Guard.NotNullOrWhiteSpace(layerId);
        Guard.NotNullOrWhiteSpace(sourceId);

        var mapLibreStyle = new JsonObject
        {
            ["version"] = 8,
            ["name"] = styleName ?? style.Title ?? style.Id,
            ["metadata"] = new JsonObject
            {
                ["honua:styleId"] = style.Id,
                ["honua:renderer"] = style.Renderer ?? "simple"
            }
        };

        // Create source reference (placeholder - client should populate with actual tile URL)
        var sources = new JsonObject
        {
            [sourceId] = new JsonObject
            {
                ["type"] = "vector",
                ["tiles"] = new JsonArray()
            }
        };
        mapLibreStyle["sources"] = sources;

        // Convert layers based on renderer type
        var layers = new JsonArray();
        var geometryType = NormalizeGeometryType(style.GeometryType);

        if (style.Rules.Count > 0)
        {
            // Rule-based renderer - create multiple layers
            foreach (var rule in style.Rules)
            {
                var layer = CreateMapLibreRuleLayer(
                    $"{layerId}-{rule.Id}",
                    sourceId,
                    sourceLayer,
                    rule,
                    geometryType
                );
                layers.Add(layer);
            }
        }
        else if (string.Equals(style.Renderer, "uniqueValue", StringComparison.OrdinalIgnoreCase)
                 && style.UniqueValue is not null)
        {
            // Unique value renderer - single layer with match expression
            var layer = CreateMapLibreUniqueValueLayer(
                layerId,
                sourceId,
                sourceLayer,
                style.UniqueValue,
                geometryType
            );
            layers.Add(layer);
        }
        else
        {
            // Simple renderer - single layer with solid styling
            var layer = CreateMapLibreSimpleLayer(
                layerId,
                sourceId,
                sourceLayer,
                style,
                geometryType
            );
            layers.Add(layer);
        }

        mapLibreStyle["layers"] = layers;
        return mapLibreStyle;
    }

    private static JsonObject CreateMapLibreSimpleLayer(
        string layerId,
        string sourceId,
        string? sourceLayer,
        StyleDefinition style,
        string geometryType)
    {
        var symbol = ResolveSimpleSymbol(style) ?? new SimpleStyleDefinition();
        var layerType = MapGeometryToLayerType(geometryType);

        var layer = new JsonObject
        {
            ["id"] = layerId,
            ["type"] = layerType,
            ["source"] = sourceId
        };

        if (!string.IsNullOrWhiteSpace(sourceLayer))
        {
            layer["source-layer"] = sourceLayer;
        }

        // Create paint properties based on geometry type
        layer["paint"] = CreateMapLibrePaint(symbol, layerType);

        return layer;
    }

    private static JsonObject CreateMapLibreUniqueValueLayer(
        string layerId,
        string sourceId,
        string? sourceLayer,
        UniqueValueStyleDefinition uniqueValue,
        string geometryType)
    {
        var layerType = MapGeometryToLayerType(geometryType);

        var layer = new JsonObject
        {
            ["id"] = layerId,
            ["type"] = layerType,
            ["source"] = sourceId
        };

        if (!string.IsNullOrWhiteSpace(sourceLayer))
        {
            layer["source-layer"] = sourceLayer;
        }

        var paint = new JsonObject();

        // Build match expression for fill/line/circle color
        var colorProperty = GetColorPropertyName(layerType);
        var matchExpression = new JsonArray { "match", new JsonArray { "get", uniqueValue.Field } };

        foreach (var classItem in uniqueValue.Classes)
        {
            matchExpression.Add(classItem.Value);
            var color = GetPrimaryColor(classItem.Symbol, layerType);
            matchExpression.Add(ParseColorToHex(color));
        }

        // Default color
        var defaultColor = uniqueValue.DefaultSymbol is not null
            ? GetPrimaryColor(uniqueValue.DefaultSymbol, layerType)
            : "#E0E0E0";
        matchExpression.Add(ParseColorToHex(defaultColor));

        paint[colorProperty] = matchExpression;

        // Add opacity if any class has it
        var firstSymbol = uniqueValue.Classes.FirstOrDefault()?.Symbol ?? uniqueValue.DefaultSymbol;
        if (firstSymbol?.Opacity is double opacity)
        {
            var opacityProperty = GetOpacityPropertyName(layerType);
            paint[opacityProperty] = opacity;
        }

        // Add stroke properties for fill layers
        if (layerType == "fill" && firstSymbol?.StrokeColor.HasValue() == true)
        {
            paint["fill-outline-color"] = ParseColorToHex(firstSymbol.StrokeColor);
        }

        // Add line width for line layers
        if (layerType == "line" && firstSymbol?.StrokeWidth is double lineWidth)
        {
            paint["line-width"] = lineWidth;
        }

        // Add circle properties
        if (layerType == "circle")
        {
            if (firstSymbol?.Size is double size)
            {
                paint["circle-radius"] = size / 2; // Honua size is diameter, MapLibre uses radius
            }
            if (firstSymbol?.StrokeColor.HasValue() == true)
            {
                paint["circle-stroke-color"] = ParseColorToHex(firstSymbol.StrokeColor);
            }
            if (firstSymbol?.StrokeWidth is double strokeWidth)
            {
                paint["circle-stroke-width"] = strokeWidth;
            }
        }

        layer["paint"] = paint;
        return layer;
    }

    private static JsonObject CreateMapLibreRuleLayer(
        string layerId,
        string sourceId,
        string? sourceLayer,
        StyleRuleDefinition rule,
        string geometryType)
    {
        var layerType = MapGeometryToLayerType(geometryType);

        var layer = new JsonObject
        {
            ["id"] = layerId,
            ["type"] = layerType,
            ["source"] = sourceId
        };

        if (!string.IsNullOrWhiteSpace(sourceLayer))
        {
            layer["source-layer"] = sourceLayer;
        }

        // Add filter if rule has one
        if (rule.Filter is { Field: var field, Value: var value })
        {
            layer["filter"] = new JsonArray { "==", new JsonArray { "get", field }, value };
        }

        // Add zoom constraints (scale to zoom conversion)
        if (rule.MinScale.HasValue)
        {
            layer["minzoom"] = ScaleDenominatorToZoom(rule.MinScale.Value);
        }
        if (rule.MaxScale.HasValue)
        {
            layer["maxzoom"] = ScaleDenominatorToZoom(rule.MaxScale.Value);
        }

        // Create paint properties
        layer["paint"] = CreateMapLibrePaint(rule.Symbolizer, layerType);

        return layer;
    }

    private static JsonObject CreateMapLibrePaint(
        SimpleStyleDefinition symbol,
        string layerType)
    {
        var paint = new JsonObject();

        switch (layerType)
        {
            case "fill":
                if (!string.IsNullOrWhiteSpace(symbol.FillColor))
                {
                    paint["fill-color"] = ParseColorToHex(symbol.FillColor);
                }
                if (symbol.Opacity.HasValue)
                {
                    paint["fill-opacity"] = symbol.Opacity.Value;
                }
                if (!string.IsNullOrWhiteSpace(symbol.StrokeColor))
                {
                    paint["fill-outline-color"] = ParseColorToHex(symbol.StrokeColor);
                }
                break;

            case "line":
                if (!string.IsNullOrWhiteSpace(symbol.StrokeColor))
                {
                    paint["line-color"] = ParseColorToHex(symbol.StrokeColor);
                }
                if (symbol.StrokeWidth.HasValue)
                {
                    paint["line-width"] = symbol.StrokeWidth.Value;
                }
                if (symbol.Opacity.HasValue)
                {
                    paint["line-opacity"] = symbol.Opacity.Value;
                }
                break;

            case "circle":
                if (symbol.Size.HasValue)
                {
                    paint["circle-radius"] = symbol.Size.Value / 2; // Convert diameter to radius
                }
                if (!string.IsNullOrWhiteSpace(symbol.FillColor))
                {
                    paint["circle-color"] = ParseColorToHex(symbol.FillColor);
                }
                if (symbol.Opacity.HasValue)
                {
                    paint["circle-opacity"] = symbol.Opacity.Value;
                }
                if (!string.IsNullOrWhiteSpace(symbol.StrokeColor))
                {
                    paint["circle-stroke-color"] = ParseColorToHex(symbol.StrokeColor);
                }
                if (symbol.StrokeWidth.HasValue)
                {
                    paint["circle-stroke-width"] = symbol.StrokeWidth.Value;
                }
                break;

            case "raster":
                if (symbol.Opacity.HasValue)
                {
                    paint["raster-opacity"] = symbol.Opacity.Value;
                }
                break;
        }

        return paint;
    }

    private static string MapGeometryToLayerType(string geometryType)
    {
        return geometryType switch
        {
            "point" => "circle",
            "line" => "line",
            "polygon" => "fill",
            "raster" => "raster",
            _ => "fill"
        };
    }

    private static string GetColorPropertyName(string layerType)
    {
        return layerType switch
        {
            "fill" => "fill-color",
            "line" => "line-color",
            "circle" => "circle-color",
            "raster" => "raster-color",
            _ => "fill-color"
        };
    }

    private static string GetOpacityPropertyName(string layerType)
    {
        return layerType switch
        {
            "fill" => "fill-opacity",
            "line" => "line-opacity",
            "circle" => "circle-opacity",
            "raster" => "raster-opacity",
            _ => "fill-opacity"
        };
    }

    private static string GetPrimaryColor(SimpleStyleDefinition symbol, string layerType)
    {
        return layerType switch
        {
            "line" => symbol.StrokeColor ?? "#000000",
            _ => symbol.FillColor ?? "#000000"
        };
    }

    private static string ParseColorToHex(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
            return "#000000";

        var hex = color.Trim();
        if (!hex.StartsWith('#'))
            return "#000000";

        // Remove alpha channel if present (MapLibre uses separate opacity properties)
        if (hex.Length == 9)
        {
            return hex[..7]; // Keep only #RRGGBB
        }

        return hex.Length == 7 ? hex : "#000000";
    }

    /// <summary>
    /// Converts OGC scale denominator to approximate MapLibre zoom level
    /// Based on standard web mercator tile pyramid
    /// </summary>
    private static int ScaleDenominatorToZoom(double scaleDenominator)
    {
        // Web Mercator zoom levels approximate scale denominators:
        // z0: 559,082,264  z1: 279,541,132  z2: 139,770,566  z3: 69,885,283
        // z4: 34,942,642   z5: 17,471,321   z6: 8,735,660    z7: 4,367,830
        // z8: 2,183,915    z9: 1,091,958    z10: 545,979     z11: 272,989
        // z12: 136,495     z13: 68,247      z14: 34,124      z15: 17,062
        // z16: 8,531       z17: 4,265       z18: 2,133       z19: 1,066
        // z20: 533         z21: 267         z22: 133

        if (scaleDenominator >= 279_541_132) return 0;
        if (scaleDenominator >= 139_770_566) return 1;
        if (scaleDenominator >= 69_885_283) return 2;
        if (scaleDenominator >= 34_942_642) return 3;
        if (scaleDenominator >= 17_471_321) return 4;
        if (scaleDenominator >= 8_735_660) return 5;
        if (scaleDenominator >= 4_367_830) return 6;
        if (scaleDenominator >= 2_183_915) return 7;
        if (scaleDenominator >= 1_091_958) return 8;
        if (scaleDenominator >= 545_979) return 9;
        if (scaleDenominator >= 272_989) return 10;
        if (scaleDenominator >= 136_495) return 11;
        if (scaleDenominator >= 68_247) return 12;
        if (scaleDenominator >= 34_124) return 13;
        if (scaleDenominator >= 17_062) return 14;
        if (scaleDenominator >= 8_531) return 15;
        if (scaleDenominator >= 4_265) return 16;
        if (scaleDenominator >= 2_133) return 17;
        if (scaleDenominator >= 1_066) return 18;
        if (scaleDenominator >= 533) return 19;
        if (scaleDenominator >= 267) return 20;
        if (scaleDenominator >= 133) return 21;
        return 22;
    }

    #endregion
}
