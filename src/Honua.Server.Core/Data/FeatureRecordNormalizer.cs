// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Data;

/// <summary>
/// Provides value normalization utilities for feature record attributes.
/// Handles conversion of dates, booleans, JSON, and geometry values to database-compatible formats.
/// </summary>
public static class FeatureRecordNormalizer
{
    /// <summary>
    /// Normalizes an attribute value for database storage.
    /// Handles JSON nodes/elements, dates, and geometry values.
    /// </summary>
    /// <param name="value">The value to normalize</param>
    /// <param name="isGeometry">Whether this value represents a geometry column</param>
    /// <returns>The normalized value suitable for database parameter binding</returns>
    public static object? NormalizeValue(object? value, bool isGeometry)
    {
        if (value is null)
        {
            return null;
        }

        if (isGeometry)
        {
            return NormalizeGeometryValue(value);
        }

        return value switch
        {
            JsonNode node => node.ToJsonString(),
            JsonElement element => element.ValueKind == JsonValueKind.Null ? null : element.ToString(),
            DateTimeOffset dto => dto.UtcDateTime,
            DateTime dt => dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime(),
            _ => value
        };
    }

    /// <summary>
    /// Normalizes a geometry value to a string representation (GeoJSON or WKT).
    /// </summary>
    /// <param name="value">The geometry value to normalize</param>
    /// <returns>String representation of the geometry, or null if the value is null</returns>
    public static object? NormalizeGeometryValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return value switch
        {
            JsonNode node => node.ToJsonString(),
            JsonElement element => element.ValueKind == JsonValueKind.Null ? null : element.ToString(),
            string text => text,
            _ => value.ToString()
        };
    }

    /// <summary>
    /// Normalizes a boolean value to the appropriate database representation.
    /// Different databases use different representations (true/false vs 1/0).
    /// </summary>
    /// <param name="value">The boolean value to normalize</param>
    /// <param name="useNumericBoolean">Whether to use 1/0 instead of true/false</param>
    /// <returns>The normalized boolean value</returns>
    public static object NormalizeBooleanValue(bool value, bool useNumericBoolean = false)
    {
        return useNumericBoolean ? (value ? 1 : 0) : value;
    }

    /// <summary>
    /// Normalizes a DateTime value to UTC.
    /// </summary>
    /// <param name="value">The DateTime value to normalize</param>
    /// <returns>The DateTime in UTC</returns>
    public static DateTime NormalizeDateTimeValue(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }

    /// <summary>
    /// Normalizes a DateTimeOffset value to UTC DateTime.
    /// </summary>
    /// <param name="value">The DateTimeOffset value to normalize</param>
    /// <returns>The DateTime in UTC</returns>
    public static DateTime NormalizeDateTimeOffsetValue(DateTimeOffset value)
    {
        return value.UtcDateTime;
    }

    /// <summary>
    /// Attempts to parse a geometry from GeoJSON text.
    /// </summary>
    /// <param name="text">The GeoJSON text</param>
    /// <returns>A JsonNode representing the geometry, or null if parsing fails</returns>
    public static JsonNode? ParseGeometry(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Determines if a text value looks like JSON (starts with { or [).
    /// </summary>
    /// <param name="text">The text to check</param>
    /// <returns>True if the text appears to be JSON</returns>
    public static bool LooksLikeJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        return trimmed.StartsWith("{", StringComparison.Ordinal) ||
               trimmed.StartsWith("[", StringComparison.Ordinal);
    }

    /// <summary>
    /// Normalizes null values to DBNull for database parameters.
    /// </summary>
    /// <param name="value">The value to check</param>
    /// <returns>DBNull.Value if the input is null, otherwise the original value</returns>
    public static object NormalizeNullValue(object? value)
    {
        return value ?? DBNull.Value;
    }

    /// <summary>
    /// Checks if a value is null or DBNull.
    /// </summary>
    /// <param name="value">The value to check</param>
    /// <returns>True if the value is null or DBNull</returns>
    public static bool IsNullOrDbNull(object? value)
    {
        return value is null || value is DBNull;
    }

    /// <summary>
    /// Converts a JsonElement to a .NET object based on its value kind.
    /// </summary>
    /// <param name="element">The JsonElement to convert</param>
    /// <returns>The appropriate .NET object representation</returns>
    public static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt32(out var i) => i,
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number when element.TryGetDouble(out var d) => d,
            JsonValueKind.String when element.TryGetDateTime(out var dt) => dt,
            JsonValueKind.String => element.GetString(),
            _ => element.ToString()
        };
    }

    /// <summary>
    /// Normalizes field values from a database reader, handling common type conversions.
    /// </summary>
    /// <param name="value">The value from the database reader</param>
    /// <param name="targetType">Optional target type for conversion</param>
    /// <returns>The normalized value</returns>
    public static object? NormalizeFieldValue(object? value, Type? targetType = null)
    {
        if (value is null or DBNull)
        {
            return null;
        }

        // If no target type specified, return as-is
        if (targetType == null)
        {
            return value;
        }

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        // Attempt conversion
        try
        {
            if (underlyingType == typeof(DateTime) && value is string dateString)
            {
                return DateTime.Parse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }

            if (underlyingType == typeof(bool) && value is long longValue)
            {
                return longValue != 0;
            }

            if (underlyingType == typeof(bool) && value is int intValue)
            {
                return intValue != 0;
            }

            return Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
        }
        catch
        {
            return value;
        }
    }

    /// <summary>
    /// Safely converts a database value to a string, handling null and DBNull.
    /// </summary>
    /// <param name="value">The database value</param>
    /// <param name="defaultValue">The default value to return if the input is null</param>
    /// <returns>The string representation or the default value</returns>
    public static string? SafeToString(object? value, string? defaultValue = null)
    {
        if (value is null or DBNull)
        {
            return defaultValue;
        }

        return value switch
        {
            JsonNode node => node.ToJsonString(),
            JsonElement element => element.ValueKind == JsonValueKind.Null ? defaultValue : element.ToString(),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }
}
