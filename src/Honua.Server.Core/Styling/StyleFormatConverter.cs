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
}
