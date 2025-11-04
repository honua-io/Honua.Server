// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Stac;

/// <summary>
/// Parses STAC API Fields Extension parameters from GET and POST requests.
/// </summary>
/// <remarks>
/// The Fields Extension allows clients to request specific fields be included or excluded
/// from STAC Item responses.
///
/// GET syntax: ?fields=id,properties.datetime or ?fields=-geometry,-assets
/// POST syntax: { "fields": { "include": ["id", "geometry"], "exclude": [] } }
///
/// Reference: https://github.com/stac-api-extensions/fields
/// </remarks>
public static class FieldsParser
{
    /// <summary>
    /// Parses a comma-separated fields string from a GET request.
    /// Supports both include (positive) and exclude (negative with '-' prefix) syntax.
    /// </summary>
    /// <param name="fieldsString">Comma-separated field names, with optional '-' prefix for exclusions.</param>
    /// <returns>A FieldsSpecification with parsed include/exclude sets.</returns>
    /// <example>
    /// "id,properties.datetime" -> Include: ["id", "properties.datetime"]
    /// "-geometry,-assets" -> Exclude: ["geometry", "assets"]
    /// "id,properties,-assets.preview" -> Include: ["id", "properties"], Exclude: ["assets.preview"]
    /// </example>
    public static FieldsSpecification ParseGetFields(string? fieldsString)
    {
        if (fieldsString.IsNullOrWhiteSpace())
        {
            return new FieldsSpecification();
        }

        var includes = new HashSet<string>(StringComparer.Ordinal);
        var excludes = new HashSet<string>(StringComparer.Ordinal);

        // Split by comma and trim whitespace
        var fields = fieldsString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var field in fields)
        {
            if (field.IsNullOrWhiteSpace())
            {
                continue;
            }

            // Check if this is an exclude (starts with '-')
            if (field.StartsWith('-'))
            {
                var fieldName = field.Substring(1).Trim();
                if (fieldName.HasValue())
                {
                    excludes.Add(fieldName);
                }
            }
            else
            {
                includes.Add(field.Trim());
            }
        }

        // Return specification based on what was parsed
        return new FieldsSpecification
        {
            Include = includes.Count > 0 ? includes : null,
            Exclude = excludes.Count > 0 ? excludes : null
        };
    }

    /// <summary>
    /// Parses a FieldsSpecification from a POST request body.
    /// Validates that include/exclude are not both specified with conflicting values.
    /// </summary>
    /// <param name="include">Array of field names to include.</param>
    /// <param name="exclude">Array of field names to exclude.</param>
    /// <returns>A FieldsSpecification with normalized include/exclude sets.</returns>
    public static FieldsSpecification ParsePostFields(IReadOnlyList<string>? include, IReadOnlyList<string>? exclude)
    {
        HashSet<string>? includeSet = null;
        HashSet<string>? excludeSet = null;

        if (include is not null && include.Count > 0)
        {
            includeSet = new HashSet<string>(include.Where(f => f.HasValue()), StringComparer.Ordinal);
        }

        if (exclude is not null && exclude.Count > 0)
        {
            excludeSet = new HashSet<string>(exclude.Where(f => f.HasValue()), StringComparer.Ordinal);
        }

        return new FieldsSpecification
        {
            Include = includeSet is not null && includeSet.Count > 0 ? includeSet : null,
            Exclude = excludeSet is not null && excludeSet.Count > 0 ? excludeSet : null
        };
    }

    /// <summary>
    /// Normalizes a field name to ensure consistent formatting.
    /// </summary>
    /// <param name="field">The field name to normalize.</param>
    /// <returns>The normalized field name.</returns>
    internal static string NormalizeFieldName(string field)
    {
        if (field.IsNullOrWhiteSpace())
        {
            return string.Empty;
        }

        // Remove any leading/trailing whitespace and dots
        return field.Trim().Trim('.');
    }

    /// <summary>
    /// Validates a field name to ensure it doesn't contain invalid characters.
    /// </summary>
    /// <param name="field">The field name to validate.</param>
    /// <returns>True if the field name is valid, false otherwise.</returns>
    internal static bool IsValidFieldName(string field)
    {
        if (field.IsNullOrWhiteSpace())
        {
            return false;
        }

        // Field names should only contain alphanumeric characters, dots, underscores, and hyphens
        foreach (var ch in field)
        {
            if (!char.IsLetterOrDigit(ch) && ch != '.' && ch != '_' && ch != '-' && ch != ':')
            {
                return false;
            }
        }

        return true;
    }
}
