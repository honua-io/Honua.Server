// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Globalization;
using Honua.Server.Core.Metadata;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Query;

public sealed class MetadataQueryModelBuilder
{
    private static readonly Dictionary<string, QueryDataType> TypeLookup = new(StringComparer.OrdinalIgnoreCase)
    {
        ["string"] = QueryDataType.String,
        ["text"] = QueryDataType.String,
        ["boolean"] = QueryDataType.Boolean,
        ["bool"] = QueryDataType.Boolean,
        ["int32"] = QueryDataType.Int32,
        ["integer"] = QueryDataType.Int32,
        ["int16"] = QueryDataType.Int32,
        ["int64"] = QueryDataType.Int64,
        ["long"] = QueryDataType.Int64,
        ["float"] = QueryDataType.Single,
        ["single"] = QueryDataType.Single,
        ["double"] = QueryDataType.Double,
        ["decimal"] = QueryDataType.Decimal,
        ["date"] = QueryDataType.DateTimeOffset,
        ["datetime"] = QueryDataType.DateTimeOffset,
        ["datetimenoffset"] = QueryDataType.DateTimeOffset,
        ["datetimeoffset"] = QueryDataType.DateTimeOffset,
        ["guid"] = QueryDataType.Guid,
        ["geometry"] = QueryDataType.Geometry,
        ["geography"] = QueryDataType.Geometry,
        ["json"] = QueryDataType.Json,
        ["binary"] = QueryDataType.Binary
    };

    public QueryEntityDefinition Build(MetadataSnapshot snapshot, ServiceDefinition service, LayerDefinition layer)
    {
        Guard.NotNull(snapshot);
        Guard.NotNull(service);
        Guard.NotNull(layer);

        var fields = new Dictionary<string, QueryFieldDefinition>(StringComparer.OrdinalIgnoreCase);

        void AddField(QueryFieldDefinition definition)
        {
            fields[definition.Name] = definition;
        }

        // Add ID field
        AddField(new QueryFieldDefinition
        {
            Name = layer.IdField,
            DataType = MapDataType(FindFieldDataType(layer, layer.IdField) ?? "int64"),
            Nullable = false,
            IsKey = true
        });

        // Add geometry field
        AddField(new QueryFieldDefinition
        {
            Name = layer.GeometryField,
            DataType = QueryDataType.Geometry,
            Nullable = true,
            IsGeometry = true
        });

        foreach (var field in layer.Fields)
        {
            if (string.Equals(field.Name, layer.IdField, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(field.Name, layer.GeometryField, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var dataType = MapDataType(field.DataType ?? string.Empty);
            AddField(new QueryFieldDefinition
            {
                Name = field.Name,
                DataType = dataType,
                Nullable = field.Nullable,
                IsKey = string.Equals(field.Name, layer.IdField, StringComparison.OrdinalIgnoreCase)
            });
        }

        // Make sure display field exists
        if (!string.IsNullOrWhiteSpace(layer.DisplayField) && !fields.ContainsKey(layer.DisplayField))
        {
            AddField(new QueryFieldDefinition
            {
                Name = layer.DisplayField!,
                DataType = MapDataType(FindFieldDataType(layer, layer.DisplayField!) ?? "string"),
                Nullable = true
            });
        }

        return new QueryEntityDefinition(layer.Id, layer.Title ?? layer.Id, fields);
    }

    private static string? FindFieldDataType(LayerDefinition layer, string fieldName)
    {
        foreach (var field in layer.Fields)
        {
            if (string.Equals(field.Name, fieldName, StringComparison.OrdinalIgnoreCase))
            {
                return field.DataType;
            }
        }

        return null;
    }

    private static QueryDataType MapDataType(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return QueryDataType.String;
        }

        if (TypeLookup.TryGetValue(raw, out var mapped))
        {
            return mapped;
        }

        return QueryDataType.String;
    }
}
