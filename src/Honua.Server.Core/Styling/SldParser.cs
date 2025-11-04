// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Globalization;
using System.Xml.Linq;
using Honua.Server.Core.Metadata;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Styling;

/// <summary>
/// Parses OGC Styled Layer Descriptor (SLD) XML into StyleDefinition
/// </summary>
public static class SldParser
{
    private static readonly XNamespace Sld = "http://www.opengis.net/sld";
    private static readonly XNamespace Ogc = "http://www.opengis.net/ogc";
    private static readonly XNamespace Se = "http://www.opengis.net/se";

    /// <summary>
    /// Parse SLD XML string into StyleDefinition
    /// </summary>
    public static StyleDefinition Parse(string sldXml, string styleId)
    {
        Guard.NotNullOrWhiteSpace(sldXml);
        Guard.NotNullOrWhiteSpace(styleId);

        var doc = XDocument.Parse(sldXml);
        var root = doc.Root;

        if (root == null)
        {
            throw new InvalidOperationException("Invalid SLD: no root element");
        }

        // Support both SLD 1.0 and SE namespaces
        var userStyle = root.Descendants(Sld + "UserStyle").FirstOrDefault()
            ?? root.Descendants(Se + "FeatureTypeStyle").FirstOrDefault()?.Parent;

        if (userStyle == null)
        {
            throw new InvalidOperationException("Invalid SLD: no UserStyle element found");
        }

        var title = userStyle.Element(Sld + "Title")?.Value
            ?? userStyle.Element(Se + "Name")?.Value
            ?? styleId;

        var featureTypeStyle = userStyle.Element(Sld + "FeatureTypeStyle")
            ?? userStyle.Element(Se + "FeatureTypeStyle");

        if (featureTypeStyle == null)
        {
            throw new InvalidOperationException("Invalid SLD: no FeatureTypeStyle element found");
        }

        var rules = ParseRules(featureTypeStyle);
        var geometryType = InferGeometryType(rules);

        return new StyleDefinition
        {
            Id = styleId,
            Title = title,
            Format = "sld",
            GeometryType = geometryType,
            Rules = rules.ToArray()
        };
    }

    private static List<StyleRuleDefinition> ParseRules(XElement featureTypeStyle)
    {
        var rules = new List<StyleRuleDefinition>();
        var ruleElements = featureTypeStyle.Elements(Sld + "Rule")
            .Concat(featureTypeStyle.Elements(Se + "Rule"));

        foreach (var ruleElement in ruleElements)
        {
            var rule = ParseRule(ruleElement);
            if (rule != null)
            {
                rules.Add(rule);
            }
        }

        return rules;
    }

    private static StyleRuleDefinition? ParseRule(XElement ruleElement)
    {
        var name = ruleElement.Element(Sld + "Name")?.Value
            ?? ruleElement.Element(Se + "Name")?.Value
            ?? $"rule-{Guid.NewGuid():N}";

        var filter = ParseFilter(ruleElement);
        var minScale = ParseScale(ruleElement.Element(Sld + "MinScaleDenominator") ?? ruleElement.Element(Se + "MinScaleDenominator"));
        var maxScale = ParseScale(ruleElement.Element(Sld + "MaxScaleDenominator") ?? ruleElement.Element(Se + "MaxScaleDenominator"));

        var symbolizer = ParseSymbolizer(ruleElement);
        if (symbolizer == null)
        {
            return null;
        }

        return new StyleRuleDefinition
        {
            Id = name,
            Filter = filter,
            MinScale = minScale,
            MaxScale = maxScale,
            Symbolizer = symbolizer
        };
    }

    private static RuleFilterDefinition? ParseFilter(XElement ruleElement)
    {
        var filter = ruleElement.Element(Ogc + "Filter");
        if (filter == null)
        {
            return null;
        }

        // Support PropertyIsEqualTo for now
        var propertyIsEqualTo = filter.Element(Ogc + "PropertyIsEqualTo");
        if (propertyIsEqualTo != null)
        {
            var propertyName = propertyIsEqualTo.Element(Ogc + "PropertyName")?.Value;
            var literal = propertyIsEqualTo.Element(Ogc + "Literal")?.Value;

            if (!string.IsNullOrWhiteSpace(propertyName) && !string.IsNullOrWhiteSpace(literal))
            {
                return new RuleFilterDefinition(propertyName, literal);
            }
        }

        return null;
    }

    private static double? ParseScale(XElement? scaleElement)
    {
        if (scaleElement == null || string.IsNullOrWhiteSpace(scaleElement.Value))
        {
            return null;
        }

        return double.TryParse(scaleElement.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale)
            ? scale
            : null;
    }

    private static SimpleStyleDefinition? ParseSymbolizer(XElement ruleElement)
    {
        // Try each symbolizer type
        var polygonSymbolizer = ruleElement.Element(Sld + "PolygonSymbolizer") ?? ruleElement.Element(Se + "PolygonSymbolizer");
        if (polygonSymbolizer != null)
        {
            return ParsePolygonSymbolizer(polygonSymbolizer);
        }

        var lineSymbolizer = ruleElement.Element(Sld + "LineSymbolizer") ?? ruleElement.Element(Se + "LineSymbolizer");
        if (lineSymbolizer != null)
        {
            return ParseLineSymbolizer(lineSymbolizer);
        }

        var pointSymbolizer = ruleElement.Element(Sld + "PointSymbolizer") ?? ruleElement.Element(Se + "PointSymbolizer");
        if (pointSymbolizer != null)
        {
            return ParsePointSymbolizer(pointSymbolizer);
        }

        var rasterSymbolizer = ruleElement.Element(Sld + "RasterSymbolizer") ?? ruleElement.Element(Se + "RasterSymbolizer");
        if (rasterSymbolizer != null)
        {
            return ParseRasterSymbolizer(rasterSymbolizer);
        }

        return null;
    }

    private static SimpleStyleDefinition ParsePolygonSymbolizer(XElement symbolizer)
    {
        var fill = symbolizer.Element(Sld + "Fill") ?? symbolizer.Element(Se + "Fill");
        var stroke = symbolizer.Element(Sld + "Stroke") ?? symbolizer.Element(Se + "Stroke");

        var fillColor = GetCssParameter(fill, "fill");
        var fillOpacity = GetCssParameter(fill, "fill-opacity");
        var strokeColor = GetCssParameter(stroke, "stroke");
        var strokeWidth = GetCssParameter(stroke, "stroke-width");
        var strokeOpacity = GetCssParameter(stroke, "stroke-opacity");

        return new SimpleStyleDefinition
        {
            SymbolType = "shape",
            FillColor = ApplyOpacity(fillColor, fillOpacity),
            StrokeColor = ApplyOpacity(strokeColor, strokeOpacity),
            StrokeWidth = ParseDouble(strokeWidth),
            Opacity = ParseDouble(fillOpacity)
        };
    }

    private static SimpleStyleDefinition ParseLineSymbolizer(XElement symbolizer)
    {
        var stroke = symbolizer.Element(Sld + "Stroke") ?? symbolizer.Element(Se + "Stroke");

        var strokeColor = GetCssParameter(stroke, "stroke");
        var strokeWidth = GetCssParameter(stroke, "stroke-width");
        var strokeOpacity = GetCssParameter(stroke, "stroke-opacity");

        return new SimpleStyleDefinition
        {
            SymbolType = "shape",
            StrokeColor = ApplyOpacity(strokeColor, strokeOpacity),
            StrokeWidth = ParseDouble(strokeWidth),
            Opacity = ParseDouble(strokeOpacity)
        };
    }

    private static SimpleStyleDefinition ParsePointSymbolizer(XElement symbolizer)
    {
        var graphic = symbolizer.Element(Sld + "Graphic") ?? symbolizer.Element(Se + "Graphic");
        if (graphic == null)
        {
            return new SimpleStyleDefinition { SymbolType = "shape" };
        }

        var size = graphic.Element(Sld + "Size")?.Value ?? graphic.Element(Se + "Size")?.Value;
        var mark = graphic.Element(Sld + "Mark") ?? graphic.Element(Se + "Mark");

        if (mark != null)
        {
            var fill = mark.Element(Sld + "Fill") ?? mark.Element(Se + "Fill");
            var stroke = mark.Element(Sld + "Stroke") ?? mark.Element(Se + "Stroke");

            var fillColor = GetCssParameter(fill, "fill");
            var fillOpacity = GetCssParameter(fill, "fill-opacity");
            var strokeColor = GetCssParameter(stroke, "stroke");
            var strokeWidth = GetCssParameter(stroke, "stroke-width");

            return new SimpleStyleDefinition
            {
                SymbolType = "shape",
                FillColor = ApplyOpacity(fillColor, fillOpacity),
                StrokeColor = strokeColor,
                StrokeWidth = ParseDouble(strokeWidth),
                Size = ParseDouble(size),
                Opacity = ParseDouble(fillOpacity)
            };
        }

        // Check for external graphic (icon)
        var externalGraphic = graphic.Element(Sld + "ExternalGraphic") ?? graphic.Element(Se + "ExternalGraphic");
        if (externalGraphic != null)
        {
            var onlineResource = externalGraphic.Element(Sld + "OnlineResource")?.Attribute("href")?.Value
                ?? externalGraphic.Element(Se + "OnlineResource")?.Attribute("href")?.Value;

            return new SimpleStyleDefinition
            {
                SymbolType = "icon",
                IconHref = onlineResource,
                Size = ParseDouble(size)
            };
        }

        return new SimpleStyleDefinition
        {
            SymbolType = "shape",
            Size = ParseDouble(size)
        };
    }

    private static SimpleStyleDefinition ParseRasterSymbolizer(XElement symbolizer)
    {
        var opacity = symbolizer.Element(Sld + "Opacity")?.Value ?? symbolizer.Element(Se + "Opacity")?.Value;

        return new SimpleStyleDefinition
        {
            SymbolType = "raster",
            Opacity = ParseDouble(opacity)
        };
    }

    private static string? GetCssParameter(XElement? parent, string parameterName)
    {
        if (parent == null)
        {
            return null;
        }

        var cssParam = parent.Elements(Sld + "CssParameter")
            .Concat(parent.Elements(Se + "SvgParameter"))
            .FirstOrDefault(e => e.Attribute("name")?.Value == parameterName);

        return cssParam?.Value;
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

    private static string? ApplyOpacity(string? color, string? opacity)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return color;
        }

        if (string.IsNullOrWhiteSpace(opacity))
        {
            return color;
        }

        // Parse opacity (0.0-1.0) and convert to alpha (00-FF)
        if (!double.TryParse(opacity, NumberStyles.Float, CultureInfo.InvariantCulture, out var opacityValue))
        {
            return color;
        }

        var alpha = (int)Math.Clamp(Math.Round(opacityValue * 255), 0, 255);
        var alphaHex = alpha.ToString("X2");

        // Append alpha to hex color
        if (color.StartsWith('#') && color.Length == 7)
        {
            return $"{color}{alphaHex}";
        }

        return color;
    }

    private static string InferGeometryType(List<StyleRuleDefinition> rules)
    {
        if (rules.Count == 0)
        {
            return "polygon";
        }

        // Check first rule's symbolizer type to infer geometry
        var symbolType = rules[0].Symbolizer?.SymbolType?.ToLowerInvariant();
        return symbolType switch
        {
            "icon" or "shape" => "point",
            "raster" => "raster",
            _ => "polygon"
        };
    }
}
