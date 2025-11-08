// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Globalization;
using System.Linq;
using Honua.Server.Core.Metadata;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Query.Filter;

public static class CqlFilterParserUtils
{
    public static (string FieldName, string? FieldType) ResolveField(LayerDefinition layer, string candidate)
    {
        Guard.NotNull(layer);
        Guard.NotNullOrWhiteSpace(candidate);

        var name = candidate.Trim();
        if (name.Length == 0)
        {
            throw new InvalidOperationException("Field name cannot be empty.");
        }

        name = ExtractTerminalName(name);

        if (string.Equals(name, layer.IdField, StringComparison.OrdinalIgnoreCase))
        {
            // Use SingleOrDefault since field names are unique within a layer
            var idField = layer.Fields.SingleOrDefault(f => string.Equals(f.Name, layer.IdField, StringComparison.OrdinalIgnoreCase));
            return (layer.IdField, idField?.DataType ?? idField?.StorageType);
        }

        if (!string.IsNullOrWhiteSpace(layer.GeometryField) &&
            string.Equals(name, layer.GeometryField, StringComparison.OrdinalIgnoreCase))
        {
            return (layer.GeometryField, "geometry");
        }

        // Use SingleOrDefault since field names are unique within a layer
        var field = layer.Fields.SingleOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Field '{name}' is not defined for layer '{layer.Id}'.");

        return (field.Name, field.DataType);
    }

    public static object? ConvertToFieldValue(string? fieldType, string rawValue)
    {
        if (rawValue is null)
        {
            return null;
        }

        var normalized = fieldType?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "int" or "integer" or "int32" => int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : rawValue,
            "long" or "int64" => long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? l : rawValue,
            "double" or "float" or "decimal" => double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : rawValue,
            "datetime" or "datetimeoffset" or "date" or "time" => DateTimeOffset.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt) ? dt : rawValue,
            "bool" or "boolean" => bool.TryParse(rawValue, out var b) ? b : rawValue,
            _ => rawValue
        };
    }

    private static string ExtractTerminalName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => value,
            1 => parts[0],
            _ => parts[^1]
        };
    }
}
