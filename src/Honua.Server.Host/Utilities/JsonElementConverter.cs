// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

#nullable enable

namespace Honua.Server.Host.Utilities;

/// <summary>
/// Shared utility for converting JsonElement to native .NET types.
/// Consolidates conversion logic used across OGC, GeoServices, WFS, and OData protocols.
/// </summary>
public static class JsonElementConverter
{
    /// <summary>
    /// Converts a JsonElement to a native .NET object, recursively converting nested objects and arrays.
    /// This is the most comprehensive conversion that produces fully materialized objects.
    /// </summary>
    /// <param name="element">The JsonElement to convert.</param>
    /// <returns>A native .NET object (string, long, double, decimal, bool, Dictionary, Array, or null).</returns>
    public static object? ToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => ToNumber(element),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Object => ToObjectRecursive(element),
            JsonValueKind.Array => element.EnumerateArray().Select(ToObject).ToArray(),
            _ => element.GetRawText()
        };
    }

    /// <summary>
    /// Converts a JsonElement to a native .NET object using JsonNode for complex types.
    /// This is lighter weight than ToObject() and preserves the JSON structure without full materialization.
    /// </summary>
    /// <param name="element">The JsonElement to convert.</param>
    /// <returns>A native .NET object (string, long, double, bool, JsonNode, or null).</returns>
    public static object? ToObjectWithJsonNode(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => ToNumber(element),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Object or JsonValueKind.Array => JsonNode.Parse(element.GetRawText()),
            _ => element.GetRawText()
        };
    }

    /// <summary>
    /// Converts a JsonElement to a string representation.
    /// Numbers are converted using InvariantCulture with appropriate precision.
    /// </summary>
    /// <param name="element">The JsonElement to convert.</param>
    /// <returns>String representation or null.</returns>
    public static string? ToString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l)
                ? l.ToString(CultureInfo.InvariantCulture)
                : element.GetDouble().ToString("G17", CultureInfo.InvariantCulture),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => element.GetRawText()
        };
    }

    /// <summary>
    /// Converts a JsonElement representing a number to the most appropriate .NET numeric type.
    /// Tries Int64 first, then Double, then Decimal as fallback for high-precision values.
    /// </summary>
    private static object ToNumber(JsonElement element)
    {
        if (element.TryGetInt64(out var longValue))
        {
            return longValue;
        }

        if (element.TryGetDouble(out var doubleValue))
        {
            return doubleValue;
        }

        return element.GetDecimal();
    }

    /// <summary>
    /// Recursively converts a JsonElement object to a Dictionary with native values.
    /// </summary>
    private static Dictionary<string, object?> ToObjectRecursive(JsonElement element)
    {
        var result = new Dictionary<string, object?>();
        foreach (var property in element.EnumerateObject())
        {
            result[property.Name] = ToObject(property.Value);
        }
        return result;
    }
}
