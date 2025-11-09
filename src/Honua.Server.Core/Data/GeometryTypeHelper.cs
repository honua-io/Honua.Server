// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using NetTopologySuite.Geometries;

namespace Honua.Server.Core.Data;

/// <summary>
/// Provides utilities for working with 2D and 3D geometry types.
/// Helps differentiate between Point/PointZ, LineString/LineStringZ, etc.
/// </summary>
public static class GeometryTypeHelper
{
    /// <summary>
    /// Determines if a geometry has Z (elevation) coordinates.
    /// </summary>
    public static bool HasZCoordinate(Geometry? geometry)
    {
        if (geometry == null || geometry.IsEmpty)
        {
            return false;
        }

        // Check if geometry has Z dimension
        var coordinate = geometry.Coordinate;
        if (coordinate == null)
        {
            return false;
        }

        // NTS Coordinate.Z returns NaN if not present
        return !double.IsNaN(coordinate.Z);
    }

    /// <summary>
    /// Determines if a geometry has M (measure) coordinates.
    /// </summary>
    public static bool HasMCoordinate(Geometry? geometry)
    {
        if (geometry == null || geometry.IsEmpty)
        {
            return false;
        }

        var coordinate = geometry.Coordinate;
        if (coordinate == null)
        {
            return false;
        }

        // NTS Coordinate.M returns NaN if not present
        return !double.IsNaN(coordinate.M);
    }

    /// <summary>
    /// Gets the OGC geometry type name with appropriate Z/M suffix.
    /// Examples: "Point", "PointZ", "PointM", "PointZM"
    /// </summary>
    public static string GetOgcGeometryTypeName(Geometry? geometry)
    {
        if (geometry == null || geometry.IsEmpty)
        {
            return "Unknown";
        }

        var baseType = geometry.GeometryType;
        var hasZ = HasZCoordinate(geometry);
        var hasM = HasMCoordinate(geometry);

        if (!hasZ && !hasM)
        {
            return baseType; // "Point", "LineString", "Polygon", etc.
        }

        if (hasZ && hasM)
        {
            return baseType + "ZM";
        }

        if (hasZ)
        {
            return baseType + "Z";
        }

        return baseType + "M";
    }

    /// <summary>
    /// Gets the OGC geometry type name from a geometry type string and Z/M flags.
    /// </summary>
    public static string GetOgcGeometryTypeName(string geometryType, bool hasZ, bool hasM)
    {
        if (string.IsNullOrWhiteSpace(geometryType))
        {
            return "Unknown";
        }

        var baseType = NormalizeGeometryTypeName(geometryType);

        if (!hasZ && !hasM)
        {
            return baseType;
        }

        if (hasZ && hasM)
        {
            return baseType + "ZM";
        }

        if (hasZ)
        {
            return baseType + "Z";
        }

        return baseType + "M";
    }

    /// <summary>
    /// Normalizes geometry type names to OGC standard format.
    /// Handles variations like "multipoint" -> "MultiPoint", "polyline" -> "LineString"
    /// </summary>
    public static string NormalizeGeometryTypeName(string geometryType)
    {
        if (string.IsNullOrWhiteSpace(geometryType))
        {
            return "Unknown";
        }

        var normalized = geometryType.Trim().ToLowerInvariant();

        return normalized switch
        {
            "point" => "Point",
            "linestring" or "polyline" or "line" => "LineString",
            "polygon" => "Polygon",
            "multipoint" => "MultiPoint",
            "multilinestring" or "multipolyline" => "MultiLineString",
            "multipolygon" => "MultiPolygon",
            "geometrycollection" or "collection" => "GeometryCollection",
            _ => char.ToUpperInvariant(geometryType[0]) + geometryType.Substring(1).ToLowerInvariant()
        };
    }

    /// <summary>
    /// Gets the coordinate dimension (2, 3, or 4) based on Z and M flags.
    /// </summary>
    public static int GetCoordinateDimension(bool hasZ, bool hasM)
    {
        if (hasZ && hasM)
        {
            return 4; // XYZM
        }

        if (hasZ || hasM)
        {
            return 3; // XYZ or XYM
        }

        return 2; // XY
    }

    /// <summary>
    /// Gets the coordinate dimension from a geometry.
    /// </summary>
    public static int GetCoordinateDimension(Geometry? geometry)
    {
        if (geometry == null)
        {
            return 2;
        }

        var hasZ = HasZCoordinate(geometry);
        var hasM = HasMCoordinate(geometry);

        return GetCoordinateDimension(hasZ, hasM);
    }

    /// <summary>
    /// Determines if a geometry type string represents a 3D type.
    /// </summary>
    public static bool IsGeometryType3D(string? geometryType)
    {
        if (string.IsNullOrWhiteSpace(geometryType))
        {
            return false;
        }

        var upper = geometryType.Trim().ToUpperInvariant();
        return upper.EndsWith("Z") || upper.EndsWith("ZM") || upper.Contains("3D");
    }
}
