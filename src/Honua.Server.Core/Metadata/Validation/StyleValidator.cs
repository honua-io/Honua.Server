// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Metadata;

/// <summary>
/// Validates style metadata definitions including format, geometry type, rules, and renderer configuration.
/// </summary>
internal static class StyleValidator
{
    /// <summary>
    /// Validates style definitions and returns a set of style IDs.
    /// </summary>
    /// <param name="styles">The styles to validate.</param>
    /// <returns>A set of valid style IDs.</returns>
    /// <exception cref="InvalidDataException">Thrown when style validation fails.</exception>
    public static HashSet<string> ValidateAndGetIds(IReadOnlyList<StyleDefinition> styles)
    {
        var styleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var style in styles)
        {
            if (style is null)
            {
                continue;
            }

            if (style.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException("Styles must include an id.");
            }

            if (!styleIds.Add(style.Id))
            {
                throw new InvalidDataException($"Duplicate style id '{style.Id}'.");
            }

            ValidateStyleDefinition(style);
        }

        return styleIds;
    }

    /// <summary>
    /// Validates a single style definition including its format, geometry type, rules, and renderer.
    /// </summary>
    /// <param name="style">The style to validate.</param>
    /// <exception cref="InvalidDataException">Thrown when style validation fails.</exception>
    private static void ValidateStyleDefinition(StyleDefinition style)
    {
        if (style.Format.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException($"Style '{style.Id}' must specify a format.");
        }

        if (style.GeometryType.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException($"Style '{style.Id}' must specify a geometryType.");
        }

        // Validate rules
        foreach (var rule in style.Rules)
        {
            if (rule is null)
            {
                throw new InvalidDataException($"Style '{style.Id}' contains an undefined rule entry.");
            }

            if (rule.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Style '{style.Id}' contains a rule without an id.");
            }

            if (rule.Symbolizer is null)
            {
                throw new InvalidDataException($"Style '{style.Id}' rule '{rule.Id}' is missing a symbolizer definition.");
            }

            if (rule.Filter is { } filter)
            {
                if (filter.Field.IsNullOrWhiteSpace() || filter.Value.IsNullOrWhiteSpace())
                {
                    throw new InvalidDataException($"Style '{style.Id}' rule '{rule.Id}' filter must include both field and value.");
                }
            }

            if (rule.MinScale is double minScale && rule.MaxScale is double maxScale && minScale > maxScale)
            {
                throw new InvalidDataException($"Style '{style.Id}' rule '{rule.Id}' has minScale greater than maxScale.");
            }
        }

        // Validate renderer-specific configuration
        ValidateRenderer(style);
    }

    /// <summary>
    /// Validates renderer-specific configuration for a style.
    /// </summary>
    /// <param name="style">The style to validate.</param>
    /// <exception cref="InvalidDataException">Thrown when renderer validation fails.</exception>
    private static void ValidateRenderer(StyleDefinition style)
    {
        var renderer = style.Renderer?.Trim().ToLowerInvariant();

        switch (renderer)
        {
            case null or "" or "simple":
                if (style.Simple is null)
                {
                    throw new InvalidDataException($"Style '{style.Id}' with renderer 'simple' must include simple symbol details.");
                }
                break;

            case "uniquevalue":
            case "unique-value":
                if (style.UniqueValue is null)
                {
                    throw new InvalidDataException($"Style '{style.Id}' with renderer 'uniqueValue' must include unique value configuration.");
                }

                if (style.UniqueValue.Field.IsNullOrWhiteSpace())
                {
                    throw new InvalidDataException($"Style '{style.Id}' unique value renderer must specify a field.");
                }

                if (style.UniqueValue.Classes.Count == 0)
                {
                    throw new InvalidDataException($"Style '{style.Id}' unique value renderer must include at least one class.");
                }

                foreach (var valueClass in style.UniqueValue.Classes)
                {
                    if (valueClass is null || valueClass.Value.IsNullOrWhiteSpace())
                    {
                        throw new InvalidDataException($"Style '{style.Id}' unique value renderer contains a class without a value.");
                    }

                    if (valueClass.Symbol is null)
                    {
                        throw new InvalidDataException($"Style '{style.Id}' unique value renderer class '{valueClass.Value}' is missing a symbol definition.");
                    }
                }
                break;

            default:
                throw new InvalidDataException($"Style '{style.Id}' specifies unsupported renderer '{style.Renderer}'.");
        }
    }
}
