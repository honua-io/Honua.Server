// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using NetTopologySuite.Geometries;

namespace Honua.Server.Integration.Tests.Helpers;

/// <summary>
/// Helper methods for creating test geometries.
/// </summary>
public static class GeometryHelper
{
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);

    /// <summary>
    /// Creates a point geometry at the specified coordinates.
    /// </summary>
    public static Point CreatePoint(double longitude, double latitude)
    {
        return Factory.CreatePoint(new Coordinate(longitude, latitude));
    }

    /// <summary>
    /// Creates a polygon from a bounding box [minX, minY, maxX, maxY].
    /// </summary>
    public static Polygon CreatePolygonFromBbox(double minX, double minY, double maxX, double maxY)
    {
        var coordinates = new[]
        {
            new Coordinate(minX, minY),
            new Coordinate(maxX, minY),
            new Coordinate(maxX, maxY),
            new Coordinate(minX, maxY),
            new Coordinate(minX, minY)
        };
        return Factory.CreatePolygon(coordinates);
    }

    /// <summary>
    /// Creates a line string from a series of coordinates.
    /// </summary>
    public static LineString CreateLineString(params (double longitude, double latitude)[] points)
    {
        var coordinates = points.Select(p => new Coordinate(p.longitude, p.latitude)).ToArray();
        return Factory.CreateLineString(coordinates);
    }

    /// <summary>
    /// Creates a multi-point geometry from a series of coordinates.
    /// </summary>
    public static MultiPoint CreateMultiPoint(params (double longitude, double latitude)[] points)
    {
        var pointGeometries = points.Select(p => CreatePoint(p.longitude, p.latitude)).ToArray();
        return Factory.CreateMultiPoint(pointGeometries);
    }

    /// <summary>
    /// Creates a buffer around a point with the specified radius (in degrees).
    /// </summary>
    public static Polygon CreateBuffer(double longitude, double latitude, double radiusDegrees)
    {
        var point = CreatePoint(longitude, latitude);
        return (Polygon)point.Buffer(radiusDegrees);
    }

    /// <summary>
    /// Checks if two geometries are spatially equal within a tolerance.
    /// </summary>
    public static bool AreGeometriesEqual(Geometry geometry1, Geometry geometry2, double tolerance = 0.0001)
    {
        return geometry1.EqualsExact(geometry2, tolerance);
    }

    /// <summary>
    /// Gets the WKT (Well-Known Text) representation of a geometry.
    /// </summary>
    public static string ToWkt(Geometry geometry)
    {
        return geometry.ToText();
    }

    /// <summary>
    /// Creates a geometry from WKT (Well-Known Text).
    /// </summary>
    public static Geometry FromWkt(string wkt)
    {
        var reader = new NetTopologySuite.IO.WKTReader(Factory);
        return reader.Read(wkt);
    }
}
