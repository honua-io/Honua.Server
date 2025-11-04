// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Carto;

internal static class CartoFieldMapper
{
    public static CartoFieldMetadata ToDatasetField(LayerDefinition layer, FieldDefinition field)
    {
        Guard.NotNull(layer);
        Guard.NotNull(field);

        var type = NormalizeFieldType(layer, field);
        return new CartoFieldMetadata(
            field.Name,
            type,
            field.Nullable,
            field.MaxLength,
            field.Precision,
            field.Scale,
            field.Alias);
    }

    public static CartoSqlFieldInfo ToSqlField(LayerDefinition layer, FieldDefinition field)
    {
        Guard.NotNull(layer);
        Guard.NotNull(field);

        var type = NormalizeFieldType(layer, field);
        var geometryType = string.Equals(field.Name, layer.GeometryField, StringComparison.OrdinalIgnoreCase)
            ? layer.GeometryType
            : null;

        return new CartoSqlFieldInfo(
            type,
            field.StorageType ?? field.DataType,
            field.Nullable,
            geometryType);
    }

    private static string NormalizeFieldType(LayerDefinition layer, FieldDefinition field)
    {
        if (string.Equals(field.Name, layer.GeometryField, StringComparison.OrdinalIgnoreCase))
        {
            return "geometry";
        }

        if (string.Equals(field.Name, layer.IdField, StringComparison.OrdinalIgnoreCase))
        {
            return "id";
        }

        var dataType = field.DataType;
        if (string.IsNullOrWhiteSpace(dataType))
        {
            return "string";
        }

        return dataType.ToLowerInvariant() switch
        {
            "int" or "integer" or "long" or "short" => "number",
            "double" or "float" or "numeric" or "decimal" => "number",
            "real" => "number",
            "smallint" => "number",
            "bigint" => "number",
            "date" or "datetime" or "timestamp" => "datetime",
            "bool" or "boolean" => "boolean",
            "uuid" => "string",
            "geometry" or "geography" => "geometry",
            _ => "string"
        };
    }

    public static IReadOnlyDictionary<string, FieldDefinition> BuildFieldLookup(LayerDefinition layer)
    {
        Guard.NotNull(layer);

        var lookup = new Dictionary<string, FieldDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in layer.Fields)
        {
            if (field is null)
            {
                continue;
            }

            lookup[field.Name] = field;
        }

        return lookup;
    }
}
