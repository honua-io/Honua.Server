// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Globalization;
using System.Text.RegularExpressions;
using Honua.Server.Core.Metadata;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Styling;

/// <summary>
/// Parses GeoServer-style CSS into StyleDefinition
/// </summary>
public static partial class CssParser
{
    /// <summary>
    /// Parse CSS string into StyleDefinition
    /// </summary>
    public static StyleDefinition Parse(string css, string styleId)
    {
        Guard.NotNullOrWhiteSpace(css);
        Guard.NotNullOrWhiteSpace(styleId);

        var rules = ParseRules(css);
        var geometryType = InferGeometryType(rules);

        return new StyleDefinition
        {
            Id = styleId,
            Title = styleId,
            Format = "css",
            GeometryType = geometryType,
            Rules = rules.ToArray()
        };
    }

    private static List<StyleRuleDefinition> ParseRules(string css)
    {
        var rules = new List<StyleRuleDefinition>();

        // Match CSS rule blocks: selector { declarations }
        var ruleMatches = RuleBlockRegex().Matches(css);
        var ruleIndex = 0;

        foreach (Match match in ruleMatches)
        {
            var selector = match.Groups[1].Value.Trim();
            var declarations = match.Groups[2].Value.Trim();

            var rule = ParseRule(selector, declarations, ruleIndex++);
            if (rule != null)
            {
                rules.Add(rule);
            }
        }

        // If no rules found, return a default rule
        if (rules.Count == 0)
        {
            rules.Add(new StyleRuleDefinition
            {
                Id = "default",
                IsDefault = true,
                Symbolizer = new SimpleStyleDefinition()
            });
        }

        return rules;
    }

    private static StyleRuleDefinition? ParseRule(string selector, string declarations, int index)
    {
        // Parse selector for filters and scale constraints
        var (filter, minScale, maxScale) = ParseSelector(selector);

        // Parse CSS declarations into symbolizer properties
        var symbolizer = ParseDeclarations(declarations);

        var ruleId = $"rule-{index + 1}";
        var isDefault = selector == "*" || string.IsNullOrWhiteSpace(selector);

        return new StyleRuleDefinition
        {
            Id = ruleId,
            IsDefault = isDefault,
            Filter = filter,
            MinScale = minScale,
            MaxScale = maxScale,
            Symbolizer = symbolizer
        };
    }

    private static (RuleFilterDefinition?, double?, double?) ParseSelector(string selector)
    {
        RuleFilterDefinition? filter = null;
        double? minScale = null;
        double? maxScale = null;

        if (string.IsNullOrWhiteSpace(selector) || selector == "*")
        {
            return (null, null, null);
        }

        // Parse attribute filter: [field = 'value'] or [field='value']
        var filterMatch = AttributeFilterRegex().Match(selector);
        if (filterMatch.Success)
        {
            var field = filterMatch.Groups[1].Value.Trim();
            var value = filterMatch.Groups[2].Value.Trim().Trim('\'', '"');
            filter = new RuleFilterDefinition(field, value);
        }

        // Parse scale constraints: [@scale > minScale] or [@scale < maxScale]
        var scaleMatches = ScaleFilterRegex().Matches(selector);
        foreach (Match scaleMatch in scaleMatches)
        {
            var op = scaleMatch.Groups[1].Value.Trim();
            var scaleValue = scaleMatch.Groups[2].Value.Trim();

            if (double.TryParse(scaleValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale))
            {
                if (op == ">")
                {
                    minScale = scale;
                }
                else if (op == "<")
                {
                    maxScale = scale;
                }
            }
        }

        return (filter, minScale, maxScale);
    }

    private static SimpleStyleDefinition ParseDeclarations(string declarations)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Split by semicolon and parse each declaration
        var declarationMatches = DeclarationRegex().Matches(declarations);
        foreach (Match match in declarationMatches)
        {
            var property = match.Groups[1].Value.Trim();
            var value = match.Groups[2].Value.Trim();
            properties[property] = value;
        }

        var markValue = GetPropertyValue(properties, "mark");
        var iconHref = markValue != null ? ExtractUrl(markValue) : null;

        return new SimpleStyleDefinition
        {
            SymbolType = iconHref == null ? "shape" : "icon",
            FillColor = GetPropertyValue(properties, "fill"),
            StrokeColor = GetPropertyValue(properties, "stroke"),
            StrokeWidth = ParseDouble(GetPropertyValue(properties, "stroke-width")),
            Size = ParseDouble(GetPropertyValue(properties, "mark-size") ?? GetPropertyValue(properties, "size")),
            Opacity = ParseDouble(GetPropertyValue(properties, "fill-opacity") ?? GetPropertyValue(properties, "opacity")),
            IconHref = iconHref
        };
    }

    private static string? GetPropertyValue(Dictionary<string, string> properties, string key)
    {
        return properties.TryGetValue(key, out var value) ? value : null;
    }

    private static string? ExtractUrl(string value)
    {
        // Extract URL from url('...') or url("...")
        var match = UrlExtractionRegex().Match(value);
        if (match.Success)
        {
            var url = match.Groups[1].Value;
            // Remove quotes if present
            return url.Trim('\'', '"');
        }
        return value.Trim('\'', '"');
    }

    private static double? ParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // Handle units like "10px" by stripping non-numeric suffix
        var cleanValue = StripUnitsRegex().Replace(value.Trim(), "$1");

        return double.TryParse(cleanValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private static string InferGeometryType(List<StyleRuleDefinition> rules)
    {
        if (rules.Count == 0)
        {
            return "polygon";
        }

        // Check if any rule has mark/icon (point) or only stroke (line)
        var hasIcon = rules.Any(r => !string.IsNullOrWhiteSpace(r.Symbolizer?.IconHref));
        var hasFill = rules.Any(r => !string.IsNullOrWhiteSpace(r.Symbolizer?.FillColor));
        var hasStroke = rules.Any(r => !string.IsNullOrWhiteSpace(r.Symbolizer?.StrokeColor));

        if (hasIcon)
        {
            return "point";
        }

        if (hasStroke && !hasFill)
        {
            return "line";
        }

        return "polygon";
    }

    // Regex patterns using C# 11+ source generators for performance
    [GeneratedRegex(@"([^{]+)\{([^}]+)\}", RegexOptions.Multiline)]
    private static partial Regex RuleBlockRegex();

    [GeneratedRegex(@"\[(\w+)\s*=\s*['""]?([^'""]+)['""]?\]")]
    private static partial Regex AttributeFilterRegex();

    [GeneratedRegex(@"\[@scale\s*([<>])\s*([\d.]+)\]")]
    private static partial Regex ScaleFilterRegex();

    [GeneratedRegex(@"([\w-]+)\s*:\s*([^;]+);?")]
    private static partial Regex DeclarationRegex();

    [GeneratedRegex(@"^([\d.]+)(?:px|pt|em)?$")]
    private static partial Regex StripUnitsRegex();

    [GeneratedRegex(@"^url\(([^)]+)\)$")]
    private static partial Regex UrlExtractionRegex();
}
