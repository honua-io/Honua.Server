// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Styling;

/// <summary>
/// Validates style definitions in various formats
/// </summary>
public static class StyleValidator
{
    private static readonly XNamespace Sld = "http://www.opengis.net/sld";
    private static readonly XNamespace Ogc = "http://www.opengis.net/ogc";

    public static ValidationResult ValidateStyleDefinition(StyleDefinition style)
    {
        Guard.NotNull(style);

        var errors = new List<string>();
        var warnings = new List<string>();

        // Validate ID
        if (style.Id.IsNullOrWhiteSpace())
        {
            errors.Add("Style ID is required.");
        }
        else if (style.Id.Length > 100)
        {
            errors.Add("Style ID must not exceed 100 characters.");
        }

        // Validate format
        if (style.Format.IsNullOrWhiteSpace())
        {
            errors.Add("Style format is required.");
        }
        else
        {
            var format = style.Format.ToLowerInvariant();
            if (format != "legacy" && format != "sld" && format != "mapbox" && format != "cartocss")
            {
                errors.Add($"Unsupported style format: '{style.Format}'. Supported formats: legacy, sld, mapbox, cartocss.");
            }
        }

        // Validate geometry type
        if (style.GeometryType.IsNullOrWhiteSpace())
        {
            errors.Add("Geometry type is required.");
        }
        else
        {
            var geomType = style.GeometryType.ToLowerInvariant();
            if (geomType != "point" && geomType != "line" && geomType != "polyline" && geomType != "polygon" && geomType != "raster")
            {
                warnings.Add($"Geometry type '{style.GeometryType}' is non-standard. Expected: point, line, polygon, or raster.");
            }
        }

        // Validate renderer
        var renderer = style.Renderer?.ToLowerInvariant() ?? "simple";
        switch (renderer)
        {
            case "simple":
                if (style.Simple is null)
                {
                    errors.Add("Simple renderer requires a 'simple' symbol definition.");
                }
                else
                {
                    ValidateSimpleSymbol(style.Simple, errors, warnings);
                }
                break;

            case "uniquevalue":
            case "unique-value":
                if (style.UniqueValue is null)
                {
                    errors.Add("UniqueValue renderer requires a 'uniqueValue' configuration.");
                }
                else
                {
                    ValidateUniqueValueRenderer(style.UniqueValue, errors, warnings);
                }
                break;

            default:
                warnings.Add($"Renderer type '{style.Renderer}' may not be fully supported.");
                break;
        }

        // Validate rules
        if (style.Rules.Count > 0)
        {
            ValidateRules(style.Rules, errors, warnings);
        }

        return new ValidationResult(errors, warnings);
    }

    public static ValidationResult ValidateSldXml(string sldXml)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (sldXml.IsNullOrWhiteSpace())
        {
            errors.Add("SLD XML content is empty.");
            return new ValidationResult(errors, warnings);
        }

        try
        {
            var doc = XDocument.Parse(sldXml);
            var root = doc.Root;

            if (root is null)
            {
                errors.Add("Invalid SLD XML: no root element.");
                return new ValidationResult(errors, warnings);
            }

            // Check for SLD namespace
            if (root.Name.Namespace != Sld && !root.Name.LocalName.Equals("StyledLayerDescriptor", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Invalid SLD root element: expected StyledLayerDescriptor, found {root.Name.LocalName}.");
            }

            // Check version attribute
            var version = root.Attribute("version")?.Value;
            if (version.IsNullOrWhiteSpace())
            {
                warnings.Add("SLD version attribute is missing.");
            }
            else if (version != "1.0.0" && version != "1.1.0")
            {
                warnings.Add($"SLD version '{version}' may not be fully supported. Recommended: 1.0.0 or 1.1.0.");
            }

            // Check for at least one NamedLayer or UserLayer
            var namedLayers = root.Descendants(Sld + "NamedLayer").ToList();
            var userLayers = root.Descendants(Sld + "UserLayer").ToList();

            if (namedLayers.Count == 0 && userLayers.Count == 0)
            {
                errors.Add("SLD must contain at least one NamedLayer or UserLayer.");
            }

            // Check for FeatureTypeStyle
            var featureTypeStyles = root.Descendants(Sld + "FeatureTypeStyle").ToList();
            if (featureTypeStyles.Count == 0)
            {
                errors.Add("SLD must contain at least one FeatureTypeStyle.");
            }

            // Check for at least one Rule
            var rules = root.Descendants(Sld + "Rule").ToList();
            if (rules.Count == 0)
            {
                errors.Add("SLD must contain at least one Rule.");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to parse SLD XML: {ex.Message}");
        }

        return new ValidationResult(errors, warnings);
    }

    public static ValidationResult ValidateMapboxStyle(string mapboxJson)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (mapboxJson.IsNullOrWhiteSpace())
        {
            errors.Add("Mapbox style JSON content is empty.");
            return new ValidationResult(errors, warnings);
        }

        try
        {
            using var doc = JsonDocument.Parse(mapboxJson);
            var root = doc.RootElement;

            // Check version
            if (!root.TryGetProperty("version", out var versionElement))
            {
                errors.Add("Mapbox style must include a 'version' property.");
            }
            else if (versionElement.ValueKind == JsonValueKind.Number)
            {
                var version = versionElement.GetInt32();
                if (version != 8)
                {
                    warnings.Add($"Mapbox style version {version} may not be fully supported. Expected: 8.");
                }
            }

            // Check for layers
            if (!root.TryGetProperty("layers", out var layersElement))
            {
                errors.Add("Mapbox style must include a 'layers' array.");
            }
            else if (layersElement.ValueKind != JsonValueKind.Array)
            {
                errors.Add("Mapbox style 'layers' must be an array.");
            }
            else if (layersElement.GetArrayLength() == 0)
            {
                warnings.Add("Mapbox style 'layers' array is empty.");
            }

            // Check for sources (optional but recommended)
            if (!root.TryGetProperty("sources", out var sourcesElement))
            {
                warnings.Add("Mapbox style does not include 'sources'. This may be intentional for reference styles.");
            }
            else if (sourcesElement.ValueKind != JsonValueKind.Object)
            {
                warnings.Add("Mapbox style 'sources' should be an object.");
            }

            // Check for name
            if (!root.TryGetProperty("name", out _))
            {
                warnings.Add("Mapbox style does not include a 'name' property.");
            }
        }
        catch (JsonException ex)
        {
            errors.Add($"Failed to parse Mapbox style JSON: {ex.Message}");
        }

        return new ValidationResult(errors, warnings);
    }

    public static ValidationResult ValidateCartoCSS(string cartoCSS)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (cartoCSS.IsNullOrWhiteSpace())
        {
            errors.Add("CartoCSS content is empty.");
            return new ValidationResult(errors, warnings);
        }

        // Basic CartoCSS validation
        if (!cartoCSS.Contains("{") || !cartoCSS.Contains("}"))
        {
            errors.Add("CartoCSS must contain at least one rule block with curly braces.");
        }

        // Check for common CartoCSS properties
        var hasProperties = cartoCSS.Contains("marker-") ||
                           cartoCSS.Contains("line-") ||
                           cartoCSS.Contains("polygon-") ||
                           cartoCSS.Contains("text-") ||
                           cartoCSS.Contains("shield-");

        if (!hasProperties)
        {
            warnings.Add("CartoCSS does not appear to contain standard styling properties (marker-, line-, polygon-, text-, shield-).");
        }

        return new ValidationResult(errors, warnings);
    }

    private static void ValidateSimpleSymbol(SimpleStyleDefinition symbol, List<string> errors, List<string> warnings)
    {
        if (symbol.FillColor.IsNullOrWhiteSpace() && symbol.StrokeColor.IsNullOrWhiteSpace() && symbol.IconHref.IsNullOrWhiteSpace())
        {
            warnings.Add("Simple symbol has no fill color, stroke color, or icon. It may not render visibly.");
        }

        if (symbol.FillColor.HasValue() && !IsValidColor(symbol.FillColor))
        {
            warnings.Add($"Fill color '{symbol.FillColor}' may not be a valid color format. Expected hex format: #RRGGBB or #RRGGBBAA.");
        }

        if (symbol.StrokeColor.HasValue() && !IsValidColor(symbol.StrokeColor))
        {
            warnings.Add($"Stroke color '{symbol.StrokeColor}' may not be a valid color format. Expected hex format: #RRGGBB or #RRGGBBAA.");
        }

        if (symbol.StrokeWidth is < 0 or > 100)
        {
            warnings.Add($"Stroke width {symbol.StrokeWidth} is unusual. Expected range: 0-100.");
        }

        if (symbol.Size is < 0 or > 500)
        {
            warnings.Add($"Symbol size {symbol.Size} is unusual. Expected range: 0-500.");
        }

        if (symbol.Opacity is < 0 or > 1)
        {
            errors.Add($"Opacity {symbol.Opacity} is invalid. Expected range: 0.0-1.0.");
        }
    }

    private static void ValidateUniqueValueRenderer(UniqueValueStyleDefinition uniqueValue, List<string> errors, List<string> warnings)
    {
        if (uniqueValue.Field.IsNullOrWhiteSpace())
        {
            errors.Add("UniqueValue renderer must specify a field name.");
        }

        if (uniqueValue.Classes.Count == 0)
        {
            errors.Add("UniqueValue renderer must have at least one class.");
        }

        var seenValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var valueClass in uniqueValue.Classes)
        {
            if (valueClass.Value.IsNullOrWhiteSpace())
            {
                errors.Add("UniqueValue class must have a value.");
            }
            else if (!seenValues.Add(valueClass.Value))
            {
                warnings.Add($"Duplicate class value '{valueClass.Value}' in UniqueValue renderer.");
            }

            if (valueClass.Symbol is not null)
            {
                ValidateSimpleSymbol(valueClass.Symbol, errors, warnings);
            }
        }

        if (uniqueValue.DefaultSymbol is not null)
        {
            ValidateSimpleSymbol(uniqueValue.DefaultSymbol, errors, warnings);
        }
    }

    private static void ValidateRules(IReadOnlyList<StyleRuleDefinition> rules, List<string> errors, List<string> warnings)
    {
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in rules)
        {
            if (rule.Id.IsNullOrWhiteSpace())
            {
                errors.Add("Rule must have an ID.");
            }
            else if (!seenIds.Add(rule.Id))
            {
                errors.Add($"Duplicate rule ID '{rule.Id}'.");
            }

            if (rule.MinScale is double minScale && rule.MaxScale is double maxScale && minScale < maxScale)
            {
                errors.Add($"Rule '{rule.Id}' has minScale ({minScale}) less than maxScale ({maxScale}). In scale denominators, min should be >= max.");
            }

            if (rule.Symbolizer is not null)
            {
                ValidateSimpleSymbol(rule.Symbolizer, errors, warnings);
            }
        }
    }

    private static bool IsValidColor(string color)
    {
        if (color.IsNullOrWhiteSpace())
        {
            return false;
        }

        var trimmed = color.Trim();
        if (!trimmed.StartsWith('#'))
        {
            return false;
        }

        var hex = trimmed[1..];
        return hex.Length is 6 or 8 && hex.All(c => char.IsAsciiHexDigit(c));
    }
}

/// <summary>
/// Result of style validation
/// </summary>
public sealed record ValidationResult
{
    public ValidationResult(IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
    {
        Errors = errors ?? Array.Empty<string>();
        Warnings = warnings ?? Array.Empty<string>();
    }

    public IReadOnlyList<string> Errors { get; }
    public IReadOnlyList<string> Warnings { get; }

    public bool IsValid => Errors.Count == 0;

    public string GetSummary()
    {
        if (IsValid && Warnings.Count == 0)
        {
            return "Style is valid with no issues.";
        }

        var parts = new List<string>();
        if (Errors.Count > 0)
        {
            parts.Add($"{Errors.Count} error(s)");
        }
        if (Warnings.Count > 0)
        {
            parts.Add($"{Warnings.Count} warning(s)");
        }

        return string.Join(", ", parts);
    }
}
