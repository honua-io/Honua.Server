// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;

namespace Honua.Server.Integration.Tests.GeoETL.Utilities;

/// <summary>
/// Helper class for generating test features
/// </summary>
public static class FeatureGenerator
{
    private static readonly GeometryFactory _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);

    public static IFeature CreatePointFeature(double lon, double lat, Dictionary<string, object>? attributes = null)
    {
        var point = _geometryFactory.CreatePoint(new Coordinate(lon, lat));
        var attributesTable = new AttributesTable(attributes ?? new Dictionary<string, object>());
        return new Feature(point, attributesTable);
    }

    public static List<IFeature> CreatePointFeatures(int count, double startLon = -122.0, double startLat = 37.0)
    {
        var features = new List<IFeature>();
        for (int i = 0; i < count; i++)
        {
            var attributes = new Dictionary<string, object>
            {
                ["id"] = i,
                ["name"] = $"Point {i}",
                ["value"] = i * 10
            };
            features.Add(CreatePointFeature(startLon + (i * 0.01), startLat + (i * 0.01), attributes));
        }
        return features;
    }

    public static IFeature CreatePolygonFeature(double minX, double minY, double maxX, double maxY, Dictionary<string, object>? attributes = null)
    {
        var coordinates = new[]
        {
            new Coordinate(minX, minY),
            new Coordinate(maxX, minY),
            new Coordinate(maxX, maxY),
            new Coordinate(minX, maxY),
            new Coordinate(minX, minY)
        };

        var polygon = _geometryFactory.CreatePolygon(coordinates);
        var attributesTable = new AttributesTable(attributes ?? new Dictionary<string, object>());
        return new Feature(polygon, attributesTable);
    }

    public static List<IFeature> CreatePolygonFeatures(int count, double startX = -122.0, double startY = 37.0, double size = 0.01)
    {
        var features = new List<IFeature>();
        for (int i = 0; i < count; i++)
        {
            var offsetX = i * size * 2;
            var offsetY = i * size * 2;
            var attributes = new Dictionary<string, object>
            {
                ["id"] = i,
                ["name"] = $"Polygon {i}",
                ["area"] = size * size
            };
            features.Add(CreatePolygonFeature(
                startX + offsetX,
                startY + offsetY,
                startX + offsetX + size,
                startY + offsetY + size,
                attributes
            ));
        }
        return features;
    }

    public static IFeature CreateLineStringFeature(Coordinate[] coordinates, Dictionary<string, object>? attributes = null)
    {
        var lineString = _geometryFactory.CreateLineString(coordinates);
        var attributesTable = new AttributesTable(attributes ?? new Dictionary<string, object>());
        return new Feature(lineString, attributesTable);
    }

    public static List<IFeature> CreateLineStringFeatures(int count, double startX = -122.0, double startY = 37.0)
    {
        var features = new List<IFeature>();
        for (int i = 0; i < count; i++)
        {
            var coordinates = new[]
            {
                new Coordinate(startX + (i * 0.01), startY),
                new Coordinate(startX + (i * 0.01) + 0.01, startY + 0.01),
                new Coordinate(startX + (i * 0.01) + 0.02, startY)
            };

            var attributes = new Dictionary<string, object>
            {
                ["id"] = i,
                ["name"] = $"Line {i}",
                ["length"] = 0.02
            };

            features.Add(CreateLineStringFeature(coordinates, attributes));
        }
        return features;
    }

    public static string CreateGeoJsonFeatureCollection(IEnumerable<IFeature> features)
    {
        var featureStrings = new List<string>();
        foreach (var feature in features)
        {
            var geometry = feature.Geometry;
            var geomType = geometry.GeometryType;
            var coords = GeometryToGeoJsonCoordinates(geometry);

            var properties = new List<string>();
            if (feature.Attributes != null)
            {
                foreach (var name in feature.Attributes.GetNames())
                {
                    var value = feature.Attributes[name];
                    var valueStr = value switch
                    {
                        string s => $"\"{s}\"",
                        _ => value?.ToString() ?? "null"
                    };
                    properties.Add($"\"{name}\": {valueStr}");
                }
            }

            var propertiesJson = properties.Count > 0 ? string.Join(", ", properties) : "";

            featureStrings.Add($@"
            {{
                ""type"": ""Feature"",
                ""geometry"": {{
                    ""type"": ""{geomType}"",
                    ""coordinates"": {coords}
                }},
                ""properties"": {{ {propertiesJson} }}
            }}");
        }

        return $@"{{
            ""type"": ""FeatureCollection"",
            ""features"": [{string.Join(",", featureStrings)}]
        }}";
    }

    private static string GeometryToGeoJsonCoordinates(Geometry geometry)
    {
        return geometry switch
        {
            Point p => $"[{p.X}, {p.Y}]",
            LineString ls => $"[{string.Join(", ", Array.ConvertAll(ls.Coordinates, c => $"[{c.X}, {c.Y}]"))}]",
            Polygon poly => $"[[{string.Join(", ", Array.ConvertAll(poly.ExteriorRing.Coordinates, c => $"[{c.X}, {c.Y}]"))}]]",
            _ => "[]"
        };
    }

    public static string CreateGeoJsonFromPoints(int count)
    {
        var features = CreatePointFeatures(count);
        return CreateGeoJsonFeatureCollection(features);
    }

    public static string CreateGeoJsonFromPolygons(int count)
    {
        var features = CreatePolygonFeatures(count);
        return CreateGeoJsonFeatureCollection(features);
    }

    public static string CreateGeoJsonFromLineStrings(int count)
    {
        var features = CreateLineStringFeatures(count);
        return CreateGeoJsonFeatureCollection(features);
    }
}
