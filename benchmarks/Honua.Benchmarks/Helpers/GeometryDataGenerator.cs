// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;

namespace Honua.Benchmarks.Helpers;

/// <summary>
/// Generates test geometry data for benchmarks with controlled sizes and complexity.
/// </summary>
public static class GeometryDataGenerator
{
    private static readonly Random Random = new(42); // Fixed seed for reproducibility
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);

    /// <summary>
    /// Generates a random point within the specified bounds.
    /// </summary>
    public static Point GeneratePoint(double minX = -180, double maxX = 180, double minY = -90, double maxY = 90)
    {
        var x = minX + Random.NextDouble() * (maxX - minX);
        var y = minY + Random.NextDouble() * (maxY - minY);
        return Factory.CreatePoint(new Coordinate(x, y));
    }

    /// <summary>
    /// Generates a random polygon with the specified number of vertices.
    /// </summary>
    public static Polygon GeneratePolygon(int vertexCount, double centerX = 0, double centerY = 0, double radius = 1)
    {
        if (vertexCount < 3)
            throw new ArgumentException("Polygon must have at least 3 vertices", nameof(vertexCount));

        var coordinates = new Coordinate[vertexCount + 1]; // +1 to close the ring
        var angleStep = 2 * Math.PI / vertexCount;

        for (int i = 0; i < vertexCount; i++)
        {
            var angle = i * angleStep;
            // Add some randomness to make it less regular
            var r = radius * (0.8 + Random.NextDouble() * 0.4);
            var x = centerX + r * Math.Cos(angle);
            var y = centerY + r * Math.Sin(angle);
            coordinates[i] = new Coordinate(x, y);
        }
        coordinates[vertexCount] = coordinates[0]; // Close the ring

        return Factory.CreatePolygon(coordinates);
    }

    /// <summary>
    /// Generates a random LineString with the specified number of vertices.
    /// </summary>
    public static LineString GenerateLineString(int vertexCount, double minX = -180, double maxX = 180, double minY = -90, double maxY = 90)
    {
        if (vertexCount < 2)
            throw new ArgumentException("LineString must have at least 2 vertices", nameof(vertexCount));

        var coordinates = new Coordinate[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            var x = minX + Random.NextDouble() * (maxX - minX);
            var y = minY + Random.NextDouble() * (maxY - minY);
            coordinates[i] = new Coordinate(x, y);
        }

        return Factory.CreateLineString(coordinates);
    }

    /// <summary>
    /// Generates a MultiPolygon with the specified number of polygons.
    /// </summary>
    public static MultiPolygon GenerateMultiPolygon(int polygonCount, int verticesPerPolygon = 10, double extent = 100)
    {
        var polygons = new Polygon[polygonCount];
        for (int i = 0; i < polygonCount; i++)
        {
            var centerX = Random.NextDouble() * extent;
            var centerY = Random.NextDouble() * extent;
            polygons[i] = GeneratePolygon(verticesPerPolygon, centerX, centerY, 5);
        }
        return Factory.CreateMultiPolygon(polygons);
    }

    /// <summary>
    /// Generates a complex polygon with holes.
    /// </summary>
    public static Polygon GeneratePolygonWithHoles(int exteriorVertices, int holeCount, int verticesPerHole = 10)
    {
        var shell = Factory.CreateLinearRing(GeneratePolygon(exteriorVertices, 0, 0, 10).Coordinates);
        var holes = new LinearRing[holeCount];

        for (int i = 0; i < holeCount; i++)
        {
            var offsetX = (Random.NextDouble() - 0.5) * 5;
            var offsetY = (Random.NextDouble() - 0.5) * 5;
            holes[i] = Factory.CreateLinearRing(GeneratePolygon(verticesPerHole, offsetX, offsetY, 1).Coordinates);
        }

        return Factory.CreatePolygon(shell, holes);
    }

    /// <summary>
    /// Generates a list of geometries for bulk operations.
    /// </summary>
    public static List<Geometry> GenerateGeometryCollection(int count, Func<int, Geometry> generator)
    {
        var geometries = new List<Geometry>(count);
        for (int i = 0; i < count; i++)
        {
            geometries.Add(generator(i));
        }
        return geometries;
    }

    /// <summary>
    /// Generates a realistic GeoJSON feature-like structure.
    /// </summary>
    public static (Geometry Geometry, Dictionary<string, object> Properties) GenerateFeature(int id)
    {
        var geometry = Random.Next(0, 3) switch
        {
            0 => (Geometry)GeneratePoint(),
            1 => (Geometry)GenerateLineString(Random.Next(5, 20)),
            _ => (Geometry)GeneratePolygon(Random.Next(5, 30))
        };

        var properties = new Dictionary<string, object>
        {
            ["id"] = id,
            ["name"] = $"Feature_{id}",
            ["type"] = geometry.GeometryType,
            ["area"] = geometry.Area,
            ["length"] = geometry.Length,
            ["timestamp"] = DateTime.UtcNow.AddDays(-Random.Next(0, 365)),
            ["value"] = Random.Next(1, 1000),
            ["category"] = new[] { "A", "B", "C", "D" }[Random.Next(0, 4)]
        };

        return (geometry, properties);
    }
}
