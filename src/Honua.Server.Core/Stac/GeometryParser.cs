// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Honua.Server.Core.Stac;

/// <summary>
/// GeoJSON geometry types supported by STAC search.
/// </summary>
public enum GeometryType
{
    Point,
    LineString,
    Polygon,
    MultiPoint,
    MultiLineString,
    MultiPolygon,
    GeometryCollection
}

/// <summary>
/// Represents a parsed and validated GeoJSON geometry for spatial intersection queries.
/// </summary>
public sealed class ParsedGeometry
{
    public required GeometryType Type { get; init; }
    public required string GeoJson { get; init; }
    public required string Wkt { get; init; }
    public int VertexCount { get; init; }
    public double[]? BoundingBox { get; init; }
}

/// <summary>
/// Parser and validator for GeoJSON geometries used in STAC search intersects parameter.
/// Validates geometry structure, coordinates, and converts to WKT format for database queries.
/// </summary>
public static class GeometryParser
{
    private const int MaxVertices = 10000;
    private const int MaxGeometryDepth = 10;

    /// <summary>
    /// Parses and validates a GeoJSON geometry object.
    /// </summary>
    /// <param name="geometryJson">The GeoJSON geometry as a JsonNode.</param>
    /// <returns>A parsed geometry with GeoJSON and WKT representations, or null if validation fails.</returns>
    /// <exception cref="ArgumentNullException">Thrown when geometryJson is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when geometry is invalid.</exception>
    public static ParsedGeometry Parse(JsonNode geometryJson)
    {
        ArgumentNullException.ThrowIfNull(geometryJson);

        if (geometryJson is not JsonObject geoJsonObject)
        {
            throw new InvalidOperationException("Geometry must be a JSON object.");
        }

        // Validate type property exists
        if (!geoJsonObject.TryGetPropertyValue("type", out var typeNode) || typeNode is null)
        {
            throw new InvalidOperationException("Geometry must have a 'type' property.");
        }

        var typeStr = typeNode.GetValue<string>();
        if (!Enum.TryParse<GeometryType>(typeStr, ignoreCase: true, out var geometryType))
        {
            throw new InvalidOperationException($"Unsupported geometry type: {typeStr}");
        }

        // Validate coordinates exist (except for GeometryCollection)
        if (geometryType != GeometryType.GeometryCollection)
        {
            if (!geoJsonObject.TryGetPropertyValue("coordinates", out var coordinatesNode) || coordinatesNode is null)
            {
                throw new InvalidOperationException("Geometry must have a 'coordinates' property.");
            }

            // Validate and count vertices
            var vertexCount = CountAndValidateCoordinates(coordinatesNode, geometryType, 0);
            if (vertexCount > MaxVertices)
            {
                throw new InvalidOperationException($"Geometry exceeds maximum vertex count of {MaxVertices}. Found {vertexCount} vertices.");
            }

            // Convert to WKT
            var wkt = ConvertToWkt(geometryType, coordinatesNode);

            // Calculate bounding box
            var bbox = CalculateBoundingBox(coordinatesNode, geometryType);

            return new ParsedGeometry
            {
                Type = geometryType,
                GeoJson = geoJsonObject.ToJsonString(),
                Wkt = wkt,
                VertexCount = vertexCount,
                BoundingBox = bbox
            };
        }
        else
        {
            // Handle GeometryCollection
            if (!geoJsonObject.TryGetPropertyValue("geometries", out var geometriesNode) || geometriesNode is not JsonArray geometriesArray)
            {
                throw new InvalidOperationException("GeometryCollection must have a 'geometries' array property.");
            }

            if (geometriesArray.Count == 0)
            {
                throw new InvalidOperationException("GeometryCollection must contain at least one geometry.");
            }

            var totalVertices = 0;
            var wktParts = new List<string>();
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (var geom in geometriesArray)
            {
                if (geom is null)
                {
                    continue;
                }

                var parsed = Parse(geom);
                totalVertices += parsed.VertexCount;
                wktParts.Add(parsed.Wkt);

                if (parsed.BoundingBox is not null && parsed.BoundingBox.Length >= 4)
                {
                    minX = Math.Min(minX, parsed.BoundingBox[0]);
                    minY = Math.Min(minY, parsed.BoundingBox[1]);
                    maxX = Math.Max(maxX, parsed.BoundingBox[2]);
                    maxY = Math.Max(maxY, parsed.BoundingBox[3]);
                }
            }

            if (totalVertices > MaxVertices)
            {
                throw new InvalidOperationException($"GeometryCollection exceeds maximum vertex count of {MaxVertices}. Found {totalVertices} vertices.");
            }

            var collectionWkt = $"GEOMETRYCOLLECTION({string.Join(",", wktParts)})";
            var collectionBbox = minX != double.MaxValue ? new[] { minX, minY, maxX, maxY } : null;

            return new ParsedGeometry
            {
                Type = GeometryType.GeometryCollection,
                GeoJson = geoJsonObject.ToJsonString(),
                Wkt = collectionWkt,
                VertexCount = totalVertices,
                BoundingBox = collectionBbox
            };
        }
    }

    /// <summary>
    /// Parses a GeoJSON geometry from a JSON string.
    /// </summary>
    public static ParsedGeometry Parse(string geometryJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(geometryJson);

        try
        {
            var node = JsonNode.Parse(geometryJson);
            if (node is null)
            {
                throw new InvalidOperationException("Failed to parse geometry JSON.");
            }
            return Parse(node);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Invalid JSON in geometry.", ex);
        }
    }

    private static int CountAndValidateCoordinates(JsonNode coordinatesNode, GeometryType type, int depth)
    {
        if (depth > MaxGeometryDepth)
        {
            throw new InvalidOperationException($"Geometry nesting exceeds maximum depth of {MaxGeometryDepth}.");
        }

        return type switch
        {
            GeometryType.Point => ValidateAndCountPoint(coordinatesNode),
            GeometryType.LineString => ValidateAndCountLineString(coordinatesNode),
            GeometryType.Polygon => ValidateAndCountPolygon(coordinatesNode),
            GeometryType.MultiPoint => ValidateAndCountMultiPoint(coordinatesNode),
            GeometryType.MultiLineString => ValidateAndCountMultiLineString(coordinatesNode),
            GeometryType.MultiPolygon => ValidateAndCountMultiPolygon(coordinatesNode),
            _ => throw new InvalidOperationException($"Unsupported geometry type: {type}")
        };
    }

    private static int ValidateAndCountPoint(JsonNode coordinatesNode)
    {
        if (coordinatesNode is not JsonArray coordArray)
        {
            throw new InvalidOperationException("Point coordinates must be an array.");
        }

        if (coordArray.Count < 2 || coordArray.Count > 3)
        {
            throw new InvalidOperationException("Point coordinates must have 2 or 3 elements (longitude, latitude, optional altitude).");
        }

        ValidateCoordinate(coordArray[0]?.GetValue<double>() ?? throw new InvalidOperationException("Invalid longitude"),
                          coordArray[1]?.GetValue<double>() ?? throw new InvalidOperationException("Invalid latitude"));

        return 1;
    }

    private static int ValidateAndCountLineString(JsonNode coordinatesNode)
    {
        if (coordinatesNode is not JsonArray coordArray)
        {
            throw new InvalidOperationException("LineString coordinates must be an array.");
        }

        if (coordArray.Count < 2)
        {
            throw new InvalidOperationException("LineString must have at least 2 positions.");
        }

        foreach (var position in coordArray)
        {
            if (position is null)
            {
                throw new InvalidOperationException("LineString position cannot be null.");
            }
            ValidateAndCountPoint(position);
        }

        return coordArray.Count;
    }

    private static int ValidateAndCountPolygon(JsonNode coordinatesNode)
    {
        if (coordinatesNode is not JsonArray ringsArray)
        {
            throw new InvalidOperationException("Polygon coordinates must be an array of rings.");
        }

        if (ringsArray.Count == 0)
        {
            throw new InvalidOperationException("Polygon must have at least one ring.");
        }

        var vertexCount = 0;
        for (var i = 0; i < ringsArray.Count; i++)
        {
            var ring = ringsArray[i];
            if (ring is null)
            {
                throw new InvalidOperationException($"Polygon ring {i} cannot be null.");
            }

            if (ring is not JsonArray ringArray)
            {
                throw new InvalidOperationException($"Polygon ring {i} must be an array.");
            }

            if (ringArray.Count < 4)
            {
                throw new InvalidOperationException($"Polygon ring {i} must have at least 4 positions (closed ring).");
            }

            // Validate first and last positions match (closed ring)
            var first = ringArray[0];
            var last = ringArray[^1];
            if (first is JsonArray firstPos && last is JsonArray lastPos)
            {
                var firstLon = firstPos[0]?.GetValue<double>();
                var firstLat = firstPos[1]?.GetValue<double>();
                var lastLon = lastPos[0]?.GetValue<double>();
                var lastLat = lastPos[1]?.GetValue<double>();

                if (firstLon != lastLon || firstLat != lastLat)
                {
                    throw new InvalidOperationException($"Polygon ring {i} must be closed (first and last positions must match).");
                }
            }

            vertexCount += ringArray.Count;

            foreach (var position in ringArray)
            {
                if (position is null)
                {
                    throw new InvalidOperationException($"Polygon ring {i} position cannot be null.");
                }
                ValidateAndCountPoint(position);
            }
        }

        return vertexCount;
    }

    private static int ValidateAndCountMultiPoint(JsonNode coordinatesNode)
    {
        if (coordinatesNode is not JsonArray pointsArray)
        {
            throw new InvalidOperationException("MultiPoint coordinates must be an array.");
        }

        if (pointsArray.Count == 0)
        {
            throw new InvalidOperationException("MultiPoint must have at least one point.");
        }

        foreach (var point in pointsArray)
        {
            if (point is null)
            {
                throw new InvalidOperationException("MultiPoint position cannot be null.");
            }
            ValidateAndCountPoint(point);
        }

        return pointsArray.Count;
    }

    private static int ValidateAndCountMultiLineString(JsonNode coordinatesNode)
    {
        if (coordinatesNode is not JsonArray linesArray)
        {
            throw new InvalidOperationException("MultiLineString coordinates must be an array.");
        }

        if (linesArray.Count == 0)
        {
            throw new InvalidOperationException("MultiLineString must have at least one LineString.");
        }

        var vertexCount = 0;
        foreach (var line in linesArray)
        {
            if (line is null)
            {
                throw new InvalidOperationException("MultiLineString line cannot be null.");
            }
            vertexCount += ValidateAndCountLineString(line);
        }

        return vertexCount;
    }

    private static int ValidateAndCountMultiPolygon(JsonNode coordinatesNode)
    {
        if (coordinatesNode is not JsonArray polygonsArray)
        {
            throw new InvalidOperationException("MultiPolygon coordinates must be an array.");
        }

        if (polygonsArray.Count == 0)
        {
            throw new InvalidOperationException("MultiPolygon must have at least one Polygon.");
        }

        var vertexCount = 0;
        foreach (var polygon in polygonsArray)
        {
            if (polygon is null)
            {
                throw new InvalidOperationException("MultiPolygon polygon cannot be null.");
            }
            vertexCount += ValidateAndCountPolygon(polygon);
        }

        return vertexCount;
    }

    private static void ValidateCoordinate(double longitude, double latitude)
    {
        if (longitude < -180.0 || longitude > 180.0)
        {
            throw new InvalidOperationException($"Longitude must be between -180 and 180. Got: {longitude}");
        }

        if (latitude < -90.0 || latitude > 90.0)
        {
            throw new InvalidOperationException($"Latitude must be between -90 and 90. Got: {latitude}");
        }

        if (double.IsNaN(longitude) || double.IsInfinity(longitude))
        {
            throw new InvalidOperationException($"Longitude must be a valid number. Got: {longitude}");
        }

        if (double.IsNaN(latitude) || double.IsInfinity(latitude))
        {
            throw new InvalidOperationException($"Latitude must be a valid number. Got: {latitude}");
        }
    }

    private static string ConvertToWkt(GeometryType type, JsonNode coordinatesNode)
    {
        return type switch
        {
            GeometryType.Point => ConvertPointToWkt(coordinatesNode),
            GeometryType.LineString => ConvertLineStringToWkt(coordinatesNode),
            GeometryType.Polygon => ConvertPolygonToWkt(coordinatesNode),
            GeometryType.MultiPoint => ConvertMultiPointToWkt(coordinatesNode),
            GeometryType.MultiLineString => ConvertMultiLineStringToWkt(coordinatesNode),
            GeometryType.MultiPolygon => ConvertMultiPolygonToWkt(coordinatesNode),
            _ => throw new InvalidOperationException($"Unsupported geometry type: {type}")
        };
    }

    private static string ConvertPointToWkt(JsonNode coordinatesNode)
    {
        if (coordinatesNode is not JsonArray coordArray || coordArray.Count < 2)
        {
            throw new InvalidOperationException("Invalid Point coordinates.");
        }

        var lon = coordArray[0]?.GetValue<double>() ?? 0;
        var lat = coordArray[1]?.GetValue<double>() ?? 0;

        return $"POINT({FormatCoordinate(lon)} {FormatCoordinate(lat)})";
    }

    private static string ConvertLineStringToWkt(JsonNode coordinatesNode)
    {
        if (coordinatesNode is not JsonArray coordArray)
        {
            throw new InvalidOperationException("Invalid LineString coordinates.");
        }

        var points = new List<string>();
        foreach (var position in coordArray)
        {
            if (position is JsonArray posArray && posArray.Count >= 2)
            {
                var lon = posArray[0]?.GetValue<double>() ?? 0;
                var lat = posArray[1]?.GetValue<double>() ?? 0;
                points.Add($"{FormatCoordinate(lon)} {FormatCoordinate(lat)}");
            }
        }

        return $"LINESTRING({string.Join(",", points)})";
    }

    private static string ConvertPolygonToWkt(JsonNode coordinatesNode)
    {
        if (coordinatesNode is not JsonArray ringsArray)
        {
            throw new InvalidOperationException("Invalid Polygon coordinates.");
        }

        var rings = new List<string>();
        foreach (var ring in ringsArray)
        {
            if (ring is JsonArray ringArray)
            {
                var points = new List<string>();
                foreach (var position in ringArray)
                {
                    if (position is JsonArray posArray && posArray.Count >= 2)
                    {
                        var lon = posArray[0]?.GetValue<double>() ?? 0;
                        var lat = posArray[1]?.GetValue<double>() ?? 0;
                        points.Add($"{FormatCoordinate(lon)} {FormatCoordinate(lat)}");
                    }
                }
                rings.Add($"({string.Join(",", points)})");
            }
        }

        return $"POLYGON({string.Join(",", rings)})";
    }

    private static string ConvertMultiPointToWkt(JsonNode coordinatesNode)
    {
        if (coordinatesNode is not JsonArray pointsArray)
        {
            throw new InvalidOperationException("Invalid MultiPoint coordinates.");
        }

        var points = new List<string>();
        foreach (var position in pointsArray)
        {
            if (position is JsonArray posArray && posArray.Count >= 2)
            {
                var lon = posArray[0]?.GetValue<double>() ?? 0;
                var lat = posArray[1]?.GetValue<double>() ?? 0;
                points.Add($"({FormatCoordinate(lon)} {FormatCoordinate(lat)})");
            }
        }

        return $"MULTIPOINT({string.Join(",", points)})";
    }

    private static string ConvertMultiLineStringToWkt(JsonNode coordinatesNode)
    {
        if (coordinatesNode is not JsonArray linesArray)
        {
            throw new InvalidOperationException("Invalid MultiLineString coordinates.");
        }

        var lines = new List<string>();
        foreach (var line in linesArray)
        {
            if (line is JsonArray lineArray)
            {
                var points = new List<string>();
                foreach (var position in lineArray)
                {
                    if (position is JsonArray posArray && posArray.Count >= 2)
                    {
                        var lon = posArray[0]?.GetValue<double>() ?? 0;
                        var lat = posArray[1]?.GetValue<double>() ?? 0;
                        points.Add($"{FormatCoordinate(lon)} {FormatCoordinate(lat)}");
                    }
                }
                lines.Add($"({string.Join(",", points)})");
            }
        }

        return $"MULTILINESTRING({string.Join(",", lines)})";
    }

    private static string ConvertMultiPolygonToWkt(JsonNode coordinatesNode)
    {
        if (coordinatesNode is not JsonArray polygonsArray)
        {
            throw new InvalidOperationException("Invalid MultiPolygon coordinates.");
        }

        var polygons = new List<string>();
        foreach (var polygon in polygonsArray)
        {
            if (polygon is JsonArray ringsArray)
            {
                var rings = new List<string>();
                foreach (var ring in ringsArray)
                {
                    if (ring is JsonArray ringArray)
                    {
                        var points = new List<string>();
                        foreach (var position in ringArray)
                        {
                            if (position is JsonArray posArray && posArray.Count >= 2)
                            {
                                var lon = posArray[0]?.GetValue<double>() ?? 0;
                                var lat = posArray[1]?.GetValue<double>() ?? 0;
                                points.Add($"{FormatCoordinate(lon)} {FormatCoordinate(lat)}");
                            }
                        }
                        rings.Add($"({string.Join(",", points)})");
                    }
                }
                polygons.Add($"({string.Join(",", rings)})");
            }
        }

        return $"MULTIPOLYGON({string.Join(",", polygons)})";
    }

    private static double[]? CalculateBoundingBox(JsonNode coordinatesNode, GeometryType type)
    {
        var allPositions = ExtractAllPositions(coordinatesNode, type);
        if (allPositions.Count == 0)
        {
            return null;
        }

        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;

        foreach (var (lon, lat) in allPositions)
        {
            minX = Math.Min(minX, lon);
            minY = Math.Min(minY, lat);
            maxX = Math.Max(maxX, lon);
            maxY = Math.Max(maxY, lat);
        }

        return new[] { minX, minY, maxX, maxY };
    }

    private static List<(double Lon, double Lat)> ExtractAllPositions(JsonNode coordinatesNode, GeometryType type)
    {
        var positions = new List<(double, double)>();

        switch (type)
        {
            case GeometryType.Point:
                if (coordinatesNode is JsonArray pointArray && pointArray.Count >= 2)
                {
                    positions.Add((pointArray[0]?.GetValue<double>() ?? 0, pointArray[1]?.GetValue<double>() ?? 0));
                }
                break;

            case GeometryType.LineString:
            case GeometryType.MultiPoint:
                if (coordinatesNode is JsonArray lineArray)
                {
                    foreach (var pos in lineArray)
                    {
                        if (pos is JsonArray posArray && posArray.Count >= 2)
                        {
                            positions.Add((posArray[0]?.GetValue<double>() ?? 0, posArray[1]?.GetValue<double>() ?? 0));
                        }
                    }
                }
                break;

            case GeometryType.Polygon:
            case GeometryType.MultiLineString:
                if (coordinatesNode is JsonArray ringsArray)
                {
                    foreach (var ring in ringsArray)
                    {
                        if (ring is JsonArray ringArray)
                        {
                            foreach (var pos in ringArray)
                            {
                                if (pos is JsonArray posArray && posArray.Count >= 2)
                                {
                                    positions.Add((posArray[0]?.GetValue<double>() ?? 0, posArray[1]?.GetValue<double>() ?? 0));
                                }
                            }
                        }
                    }
                }
                break;

            case GeometryType.MultiPolygon:
                if (coordinatesNode is JsonArray polygonsArray)
                {
                    foreach (var polygon in polygonsArray)
                    {
                        if (polygon is JsonArray polyRingsArray)
                        {
                            foreach (var ring in polyRingsArray)
                            {
                                if (ring is JsonArray ringArray)
                                {
                                    foreach (var pos in ringArray)
                                    {
                                        if (pos is JsonArray posArray && posArray.Count >= 2)
                                        {
                                            positions.Add((posArray[0]?.GetValue<double>() ?? 0, posArray[1]?.GetValue<double>() ?? 0));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                break;
        }

        return positions;
    }

    private static string FormatCoordinate(double value)
    {
        return value.ToString("G17", CultureInfo.InvariantCulture);
    }
}
