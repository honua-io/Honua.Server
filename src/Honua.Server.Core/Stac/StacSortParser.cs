// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Honua.Server.Core.Stac;

/// <summary>
/// Parser for STAC Sort Extension sortby parameters.
/// Supports both GET (comma-separated string with +/- prefixes) and POST (JSON array) formats.
/// </summary>
public static class StacSortParser
{
    /// <summary>
    /// Allowed sortable fields at the item level.
    /// </summary>
    private static readonly HashSet<string> AllowedItemFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "id",
        "collection",
        "datetime",
        "created",
        "updated"
    };

    /// <summary>
    /// Allowed property fields that can be sorted (common STAC properties).
    /// </summary>
    private static readonly HashSet<string> AllowedPropertyFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "cloud_cover",
        "eo:cloud_cover",
        "gsd",
        "platform",
        "instruments",
        "constellation",
        "mission",
        "providers",
        "license",
        "created",
        "updated",
        "start_datetime",
        "end_datetime"
    };

    /// <summary>
    /// Parses a GET-style sortby parameter (comma-separated with +/- prefixes).
    /// </summary>
    /// <param name="sortby">The sortby query parameter (e.g., "-datetime,+id").</param>
    /// <returns>A tuple containing the parsed sort fields or an error message.</returns>
    public static (IReadOnlyList<StacSortField>? SortFields, string? Error) ParseGetSortBy(string? sortby)
    {
        if (string.IsNullOrWhiteSpace(sortby))
        {
            return (null, null);
        }

        var fields = new List<StacSortField>();
        var parts = sortby.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            return (null, null);
        }

        if (parts.Length > 10)
        {
            return (null, "Maximum of 10 sort fields allowed");
        }

        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part))
            {
                continue;
            }

            var fieldName = part;
            var direction = StacSortDirection.Ascending;

            // Check for direction prefix
            if (part.StartsWith('-'))
            {
                direction = StacSortDirection.Descending;
                fieldName = part.Substring(1);
            }
            else if (part.StartsWith('+'))
            {
                direction = StacSortDirection.Ascending;
                fieldName = part.Substring(1);
            }

            if (string.IsNullOrWhiteSpace(fieldName))
            {
                return (null, $"Invalid sort field: '{part}'");
            }

            // Validate field name
            var validationError = ValidateFieldName(fieldName);
            if (validationError != null)
            {
                return (null, validationError);
            }

            fields.Add(new StacSortField
            {
                Field = fieldName,
                Direction = direction
            });
        }

        return fields.Count > 0 ? (fields, null) : (null, null);
    }

    /// <summary>
    /// Validates a collection of sort fields from POST request.
    /// </summary>
    /// <param name="sortFields">The sort fields from the POST body.</param>
    /// <returns>An error message if validation fails, otherwise null.</returns>
    public static string? ValidatePostSortBy(IReadOnlyList<StacSortField>? sortFields)
    {
        if (sortFields == null || sortFields.Count == 0)
        {
            return null;
        }

        if (sortFields.Count > 10)
        {
            return "Maximum of 10 sort fields allowed";
        }

        foreach (var field in sortFields)
        {
            if (string.IsNullOrWhiteSpace(field.Field))
            {
                return "Sort field name cannot be empty";
            }

            var validationError = ValidateFieldName(field.Field);
            if (validationError != null)
            {
                return validationError;
            }
        }

        return null;
    }

    /// <summary>
    /// Validates a field name to ensure it's allowed and safe.
    /// </summary>
    /// <param name="fieldName">The field name to validate.</param>
    /// <returns>An error message if validation fails, otherwise null.</returns>
    private static string? ValidateFieldName(string fieldName)
    {
        if (fieldName.Length > 100)
        {
            return $"Sort field name exceeds maximum length of 100 characters: '{fieldName}'";
        }

        // Check for SQL injection attempts
        if (ContainsDangerousCharacters(fieldName))
        {
            return $"Sort field name contains invalid characters: '{fieldName}'";
        }

        // Normalize field name for validation
        var normalizedField = fieldName.ToLowerInvariant();

        // Check if it's a top-level item field
        if (AllowedItemFields.Contains(normalizedField))
        {
            return null;
        }

        // Check if it's a property field (with or without "properties." prefix)
        if (normalizedField.StartsWith("properties."))
        {
            var propertyName = normalizedField.Substring("properties.".Length);
            if (AllowedPropertyFields.Contains(propertyName))
            {
                return null;
            }
            return $"Property field '{propertyName}' is not sortable. Allowed properties: {string.Join(", ", AllowedPropertyFields)}";
        }

        // Check if it's a property field without prefix
        if (AllowedPropertyFields.Contains(normalizedField))
        {
            return null;
        }

        return $"Field '{fieldName}' is not sortable. Allowed fields: {string.Join(", ", AllowedItemFields)} and properties: {string.Join(", ", AllowedPropertyFields)}";
    }

    /// <summary>
    /// Checks if a field name contains characters that could be used for SQL injection.
    /// </summary>
    /// <param name="fieldName">The field name to check.</param>
    /// <returns>True if the field name contains dangerous characters, otherwise false.</returns>
    private static bool ContainsDangerousCharacters(string fieldName)
    {
        foreach (var ch in fieldName)
        {
            // Allow alphanumeric, underscore, dot, colon (for STAC extensions like "eo:cloud_cover"), and hyphen
            if (!char.IsLetterOrDigit(ch) && ch != '_' && ch != '.' && ch != ':' && ch != '-')
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Gets the default sort order for STAC searches (by collection_id, then id for stable pagination).
    /// </summary>
    /// <returns>The default sort fields.</returns>
    public static IReadOnlyList<StacSortField> GetDefaultSortFields()
    {
        return new[]
        {
            new StacSortField { Field = "collection", Direction = StacSortDirection.Ascending },
            new StacSortField { Field = "id", Direction = StacSortDirection.Ascending }
        };
    }
}
