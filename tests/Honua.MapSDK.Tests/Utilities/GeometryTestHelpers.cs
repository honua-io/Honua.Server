// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Tests.Utilities;

/// <summary>
/// Helper utilities for geometry testing and validation.
/// Provides methods for geometry comparison, validation, and transformation.
/// </summary>
public static class GeometryTestHelpers
{
    /// <summary>
    /// Validate GeoJSON point geometry
    /// </summary>
    public static bool IsValidPoint(object? geometry)
    {
        if (geometry == null) return false;

        var props = geometry.GetType().GetProperty("type");
        if (props?.GetValue(geometry)?.ToString() != "Point") return false;

        var coords = geometry.GetType().GetProperty("coordinates")?.GetValue(geometry);
        if (coords is not double[] coordArray) return false;

        return coordArray.Length == 2 &&
               IsValidLongitude(coordArray[0]) &&
               IsValidLatitude(coordArray[1]);
    }

    /// <summary>
    /// Validate GeoJSON polygon geometry
    /// </summary>
    public static bool IsValidPolygon(object? geometry)
    {
        if (geometry == null) return false;

        var props = geometry.GetType().GetProperty("type");
        if (props?.GetValue(geometry)?.ToString() != "Polygon") return false;

        // Note: Full validation would check ring closure, winding order, etc.
        return true;
    }

    /// <summary>
    /// Validate longitude value
    /// </summary>
    public static bool IsValidLongitude(double lon)
    {
        return lon >= -180 && lon <= 180;
    }

    /// <summary>
    /// Validate latitude value
    /// </summary>
    public static bool IsValidLatitude(double lat)
    {
        return lat >= -90 && lat <= 90;
    }

    /// <summary>
    /// Validate bounds array [west, south, east, north]
    /// </summary>
    public static bool IsValidBounds(double[]? bounds)
    {
        if (bounds == null || bounds.Length != 4) return false;

        var west = bounds[0];
        var south = bounds[1];
        var east = bounds[2];
        var north = bounds[3];

        return IsValidLongitude(west) &&
               IsValidLatitude(south) &&
               IsValidLongitude(east) &&
               IsValidLatitude(north) &&
               west < east &&
               south < north;
    }

    /// <summary>
    /// Validate zoom level
    /// </summary>
    public static bool IsValidZoom(double zoom)
    {
        return zoom >= 0 && zoom <= 22;
    }

    /// <summary>
    /// Validate bearing (rotation) value
    /// </summary>
    public static bool IsValidBearing(double bearing)
    {
        return bearing >= 0 && bearing <= 360;
    }

    /// <summary>
    /// Validate pitch (tilt) value
    /// </summary>
    public static bool IsValidPitch(double pitch)
    {
        return pitch >= 0 && pitch <= 60;
    }

    /// <summary>
    /// Calculate approximate distance between two points in meters (Haversine formula)
    /// </summary>
    public static double CalculateDistance(double lon1, double lat1, double lon2, double lat2)
    {
        const double earthRadius = 6371000; // meters

        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return earthRadius * c;
    }

    /// <summary>
    /// Calculate bounds from center and zoom level (approximate)
    /// </summary>
    public static double[] CalculateBoundsFromCenter(double[] center, double zoom, int tileSize = 256)
    {
        var lon = center[0];
        var lat = center[1];

        // Approximate degree span at this zoom level
        var scale = Math.Pow(2, zoom);
        var degreesPerPixel = 360.0 / (tileSize * scale);
        var span = degreesPerPixel * 256; // assuming viewport of 256x256

        return new[]
        {
            lon - span / 2, // west
            lat - span / 2, // south
            lon + span / 2, // east
            lat + span / 2  // north
        };
    }

    /// <summary>
    /// Check if a point is within bounds
    /// </summary>
    public static bool IsPointInBounds(double[] point, double[] bounds)
    {
        if (point.Length != 2 || bounds.Length != 4) return false;

        var lon = point[0];
        var lat = point[1];
        var west = bounds[0];
        var south = bounds[1];
        var east = bounds[2];
        var north = bounds[3];

        return lon >= west && lon <= east && lat >= south && lat <= north;
    }

    /// <summary>
    /// Calculate the center point of bounds
    /// </summary>
    public static double[] CalculateBoundsCenter(double[] bounds)
    {
        if (bounds.Length != 4)
            throw new ArgumentException("Bounds must have 4 values [west, south, east, north]");

        return new[]
        {
            (bounds[0] + bounds[2]) / 2.0, // center longitude
            (bounds[1] + bounds[3]) / 2.0  // center latitude
        };
    }

    /// <summary>
    /// Decode Google Maps encoded polyline to coordinates
    /// </summary>
    public static List<double[]> DecodePolyline(string encoded)
    {
        var poly = new List<double[]>();
        int index = 0, len = encoded.Length;
        int lat = 0, lng = 0;

        while (index < len)
        {
            int b, shift = 0, result = 0;
            do
            {
                b = encoded[index++] - 63;
                result |= (b & 0x1f) << shift;
                shift += 5;
            } while (b >= 0x20);

            int dlat = ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
            lat += dlat;

            shift = 0;
            result = 0;
            do
            {
                b = encoded[index++] - 63;
                result |= (b & 0x1f) << shift;
                shift += 5;
            } while (b >= 0x20);

            int dlng = ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
            lng += dlng;

            poly.Add(new[] { lng / 1E5, lat / 1E5 });
        }

        return poly;
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }
}
