// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using NetTopologySuite.IO;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Serialization;

public static class FeatureComponentBuilder
{
    public static FeatureComponents BuildComponents(LayerDefinition layer, FeatureRecord record, FeatureQuery query)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(query);

        record.Attributes.TryGetValue(layer.GeometryField, out var geometryRaw);
        record.Attributes.TryGetValue(layer.IdField, out var idValue);

        var primaryKey = layer.Storage?.PrimaryKey;
        if (primaryKey.HasValue() &&
            !string.Equals(primaryKey, layer.IdField, StringComparison.OrdinalIgnoreCase))
        {
            if (idValue is null &&
                record.Attributes.TryGetValue(primaryKey, out var primaryValue) &&
                primaryValue is not null)
            {
                idValue = primaryValue;
            }
        }

        var properties = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in record.Attributes)
        {
            if (string.Equals(pair.Key, layer.GeometryField, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pair.Key, layer.IdField, StringComparison.OrdinalIgnoreCase) ||
                (primaryKey.HasValue() && string.Equals(pair.Key, primaryKey, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            properties[pair.Key] = pair.Value;
        }

        if (query.PropertyNames is { Count: > 0 })
        {
            var allowed = new HashSet<string>(query.PropertyNames, StringComparer.OrdinalIgnoreCase);
            var keys = properties.Keys.ToList();
            foreach (var key in keys)
            {
                if (!allowed.Contains(key))
                {
                    properties.Remove(key);
                }
            }
        }

        var normalizedGeometry = NormalizeGeometry(geometryRaw);
        var geometryNode = normalizedGeometry as JsonNode ?? geometryRaw as JsonNode;

        var featureId = Convert.ToString(idValue, CultureInfo.InvariantCulture) ?? string.Empty;

        string? displayName = null;
        if (layer.DisplayField.HasValue() &&
            record.Attributes.TryGetValue(layer.DisplayField, out var displayValue))
        {
            displayName = Convert.ToString(displayValue, CultureInfo.InvariantCulture);
        }

        if (displayName.IsNullOrWhiteSpace() && featureId.HasValue())
        {
            displayName = featureId;
        }

        return new FeatureComponents(
            idValue,
            featureId,
            normalizedGeometry,
            geometryNode,
            new ReadOnlyDictionary<string, object?>(properties),
            displayName);
    }

    public static KmlFeatureContent CreateKmlContent(LayerDefinition layer, FeatureRecord record, FeatureQuery query)
    {
        var components = BuildComponents(layer, record, query);
        return new KmlFeatureContent(
            components.FeatureId,
            components.DisplayName,
            components.GeometryNode,
            components.Properties);
    }

    public static TopoJsonFeatureContent CreateTopoContent(LayerDefinition layer, FeatureRecord record, FeatureQuery query)
    {
        var components = BuildComponents(layer, record, query);
        return new TopoJsonFeatureContent(
            components.FeatureId,
            components.DisplayName,
            components.GeometryNode,
            components.Properties);
    }

    private static object? NormalizeGeometry(object? geometryValue)
    {
        switch (geometryValue)
        {
            case null:
            case DBNull:
                return null;
            case JsonNode node:
                return node;
            case JsonElement element when element.ValueKind == JsonValueKind.Null:
                return null;
            case JsonElement element:
                return TryParseGeometry(element.GetRawText());
            case JsonDocument document:
                return TryParseGeometry(document.RootElement.GetRawText());
            case QueryGeometryValue geometryValue1:
                return ConvertGeometryValue(geometryValue1) ?? (object)geometryValue1;
            case string text:
                {
                    if (text.IsNullOrWhiteSpace())
                    {
                        return null;
                    }

                    var parsed = TryParseGeometry(text);
                    if (parsed is not null)
                    {
                        return parsed;
                    }

                    return TryConvertWktToGeoJson(text, null) ?? (object)text;
                }
            default:
                return geometryValue;
        }
    }

    private static JsonNode? ConvertGeometryValue(QueryGeometryValue geometryValue)
    {
        if (geometryValue is null)
        {
            return null;
        }

        return TryConvertWktToGeoJson(geometryValue.WellKnownText, geometryValue.Srid);
    }

    private static JsonNode? TryConvertWktToGeoJson(string text, int? srid)
    {
        if (text.IsNullOrWhiteSpace())
        {
            return null;
        }

        var trimmed = text.Trim();
        int? effectiveSrid = srid;
        if (trimmed.StartsWith("SRID=", StringComparison.OrdinalIgnoreCase))
        {
            var separator = trimmed.IndexOf(';');
            if (separator > 5)
            {
                var sridText = trimmed.Substring(5, separator - 5);
                if (int.TryParse(sridText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    effectiveSrid ??= parsed;
                }

                trimmed = separator < trimmed.Length - 1 ? trimmed[(separator + 1)..] : string.Empty;
            }
        }

        if (trimmed.IsNullOrWhiteSpace())
        {
            return null;
        }

        try
        {
            var reader = new WKTReader();
            var geometry = reader.Read(trimmed);
            if (geometry is null || geometry.IsEmpty)
            {
                return null;
            }

            if (effectiveSrid.HasValue && geometry.SRID == 0)
            {
                geometry.SRID = effectiveSrid.Value;
            }

            var writer = new GeoJsonWriter();
            var json = writer.Write(geometry);
            return JsonNode.Parse(json);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static JsonNode? TryParseGeometry(string text)
    {
        if (text.IsNullOrWhiteSpace())
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
}

public sealed record FeatureComponents(
    object? RawId,
    string FeatureId,
    object? Geometry,
    JsonNode? GeometryNode,
    IReadOnlyDictionary<string, object?> Properties,
    string? DisplayName);
