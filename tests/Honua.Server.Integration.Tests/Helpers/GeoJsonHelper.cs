// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Integration.Tests.Helpers;

/// <summary>
/// Helper methods for creating and manipulating GeoJSON test data.
/// </summary>
public static class GeoJsonHelper
{
    private static readonly GeoJsonWriter GeoJsonWriter = new();
    private static readonly GeoJsonReader GeoJsonReader = new();

    /// <summary>
    /// Converts a NetTopologySuite geometry to a GeoJSON string.
    /// </summary>
    public static string ToGeoJson(Geometry geometry)
    {
        return GeoJsonWriter.Write(geometry);
    }

    /// <summary>
    /// Parses a GeoJSON string into a NetTopologySuite geometry.
    /// </summary>
    public static Geometry FromGeoJson(string geoJson)
    {
        return GeoJsonReader.Read<Geometry>(geoJson);
    }

    /// <summary>
    /// Creates a GeoJSON Feature object with the specified geometry and properties.
    /// </summary>
    public static JsonObject CreateFeature(Geometry geometry, Dictionary<string, object>? properties = null)
    {
        var feature = new JsonObject
        {
            ["type"] = "Feature",
            ["geometry"] = JsonNode.Parse(ToGeoJson(geometry))
        };

        if (properties != null && properties.Count > 0)
        {
            var propsObject = new JsonObject();
            foreach (var (key, value) in properties)
            {
                propsObject[key] = JsonValue.Create(value);
            }
            feature["properties"] = propsObject;
        }
        else
        {
            feature["properties"] = new JsonObject();
        }

        return feature;
    }

    /// <summary>
    /// Creates a GeoJSON FeatureCollection with the specified features.
    /// </summary>
    public static JsonObject CreateFeatureCollection(params JsonObject[] features)
    {
        var collection = new JsonObject
        {
            ["type"] = "FeatureCollection",
            ["features"] = new JsonArray(features.Select(f => (JsonNode?)f).ToArray())
        };

        return collection;
    }

    /// <summary>
    /// Creates a simple Point feature for testing.
    /// </summary>
    public static JsonObject CreatePointFeature(double longitude, double latitude, Dictionary<string, object>? properties = null)
    {
        var factory = new GeometryFactory(new PrecisionModel(), 4326);
        var point = factory.CreatePoint(new Coordinate(longitude, latitude));
        return CreateFeature(point, properties);
    }

    /// <summary>
    /// Validates that a string is valid GeoJSON.
    /// </summary>
    public static bool IsValidGeoJson(string geoJson)
    {
        try
        {
            var parsed = JsonNode.Parse(geoJson);
            return parsed?["type"]?.GetValue<string>() != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Pretty-prints GeoJSON for debugging purposes.
    /// </summary>
    public static string PrettyPrint(string geoJson)
    {
        var parsed = JsonNode.Parse(geoJson);
        return JsonSerializer.Serialize(parsed, new JsonSerializerOptions { WriteIndented = true });
    }
}
