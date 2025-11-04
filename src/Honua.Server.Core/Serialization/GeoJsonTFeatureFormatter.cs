// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Serialization;

/// <summary>
/// Formats features as GeoJSON-T (GeoJSON with temporal properties).
/// Supports temporal coordinates as per the GeoJSON-T specification.
/// </summary>
public static class GeoJsonTFeatureFormatter
{
    /// <summary>
    /// Adds temporal properties to a GeoJSON feature.
    /// </summary>
    public static JsonObject ToGeoJsonTFeature(
        object feature,
        string? startTimeField = null,
        string? endTimeField = null,
        string? timeField = null)
    {
        Guard.NotNull(feature);

        var featureJson = JsonSerializer.SerializeToNode(feature)?.AsObject();
        if (featureJson == null)
        {
            throw new InvalidOperationException("Failed to serialize feature to JSON");
        }

        var geoJsonT = new JsonObject();

        // Copy base GeoJSON properties
        if (featureJson.TryGetPropertyValue("type", out var typeNode))
        {
            geoJsonT["type"] = typeNode?.DeepClone();
        }

        if (featureJson.TryGetPropertyValue("id", out var idNode))
        {
            geoJsonT["id"] = idNode?.DeepClone();
        }

        // Process geometry with temporal coordinates
        if (featureJson.TryGetPropertyValue("geometry", out var geometryNode) && geometryNode is JsonObject geometryObj)
        {
            geoJsonT["geometry"] = ProcessGeometryWithTime(geometryObj);
        }

        // Process properties and extract temporal information
        if (featureJson.TryGetPropertyValue("properties", out var propsNode) && propsNode is JsonObject propsObj)
        {
            var temporalProps = new JsonObject();

            foreach (var prop in propsObj)
            {
                temporalProps[prop.Key] = prop.Value?.DeepClone();
            }

            // Add temporal properties at feature level
            var when = ExtractTemporalProperties(propsObj, startTimeField, endTimeField, timeField);
            if (when != null)
            {
                geoJsonT["when"] = when;
            }

            geoJsonT["properties"] = temporalProps;
        }

        // Copy links if present
        if (featureJson.TryGetPropertyValue("links", out var linksNode))
        {
            geoJsonT["links"] = linksNode?.DeepClone();
        }

        return geoJsonT;
    }

    /// <summary>
    /// Converts a feature collection to GeoJSON-T format.
    /// </summary>
    public static JsonObject ToGeoJsonTFeatureCollection(
        IEnumerable<object> features,
        long numberMatched,
        long numberReturned,
        string? startTimeField = null,
        string? endTimeField = null,
        string? timeField = null,
        object? links = null)
    {
        Guard.NotNull(features);

        var geoJsonT = new JsonObject
        {
            ["type"] = "FeatureCollection"
        };

        var featureArray = new JsonArray();
        foreach (var feature in features)
        {
            var geoJsonTFeature = ToGeoJsonTFeature(feature, startTimeField, endTimeField, timeField);
            featureArray.Add(geoJsonTFeature);
        }

        geoJsonT["features"] = featureArray;
        geoJsonT["numberMatched"] = numberMatched;
        geoJsonT["numberReturned"] = numberReturned;

        if (links != null)
        {
            geoJsonT["links"] = JsonSerializer.SerializeToNode(links);
        }

        return geoJsonT;
    }

    /// <summary>
    /// Serializes a GeoJSON-T object to string.
    /// </summary>
    public static string Serialize(JsonObject geoJsonT)
    {
        Guard.NotNull(geoJsonT);

        return JsonSerializer.Serialize(geoJsonT, JsonSerializerOptionsRegistry.Web);
    }

    private static JsonObject ProcessGeometryWithTime(JsonObject geometry)
    {
        // Clone the geometry
        var result = geometry.DeepClone().AsObject();

        // GeoJSON-T allows adding a 4th coordinate (time) to coordinates
        // For now, we preserve the geometry as-is, but this could be extended
        // to add temporal dimensions to coordinates if time data is embedded

        return result;
    }

    private static JsonObject? ExtractTemporalProperties(
        JsonObject properties,
        string? startTimeField,
        string? endTimeField,
        string? timeField)
    {
        var when = new JsonObject();
        bool hasTemporalData = false;

        // Try to extract start time
        if (!string.IsNullOrWhiteSpace(startTimeField) &&
            properties.TryGetPropertyValue(startTimeField, out var startNode) &&
            startNode != null)
        {
            when["start"] = startNode.DeepClone();
            hasTemporalData = true;
        }

        // Try to extract end time
        if (!string.IsNullOrWhiteSpace(endTimeField) &&
            properties.TryGetPropertyValue(endTimeField, out var endNode) &&
            endNode != null)
        {
            when["end"] = endNode.DeepClone();
            hasTemporalData = true;
        }

        // Try to extract single time instant
        if (!string.IsNullOrWhiteSpace(timeField) &&
            properties.TryGetPropertyValue(timeField, out var timeNode) &&
            timeNode != null)
        {
            when["instant"] = timeNode.DeepClone();
            hasTemporalData = true;
        }

        // Check for standard temporal property names if no field names specified
        if (!hasTemporalData)
        {
            var commonTimeFields = new[] { "datetime", "timestamp", "date", "time", "created", "modified" };
            foreach (var fieldName in commonTimeFields)
            {
                if (properties.TryGetPropertyValue(fieldName, out var timeValue) && timeValue != null)
                {
                    when["instant"] = timeValue.DeepClone();
                    hasTemporalData = true;
                    break;
                }
            }
        }

        return hasTemporalData ? when : null;
    }
}
