// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Utilities;

/// <summary>
/// Utility methods for working with geographic geometries and coordinates.
/// </summary>
public static class GeometryUtils
{
    private const double EarthRadiusKm = 6371.0;
    private const double DegreesToRadians = Math.PI / 180.0;
    private const double RadiansToDegrees = 180.0 / Math.PI;

    /// <summary>
    /// Represents a bounding box with min/max longitude and latitude.
    /// </summary>
    public record BoundingBox(double MinLon, double MinLat, double MaxLon, double MaxLat)
    {
        /// <summary>
        /// Gets the center point of the bounding box.
        /// </summary>
        public (double Lon, double Lat) Center => ((MinLon + MaxLon) / 2, (MinLat + MaxLat) / 2);

        /// <summary>
        /// Gets the width of the bounding box in degrees.
        /// </summary>
        public double Width => MaxLon - MinLon;

        /// <summary>
        /// Gets the height of the bounding box in degrees.
        /// </summary>
        public double Height => MaxLat - MinLat;

        /// <summary>
        /// Checks if a point is within the bounding box.
        /// </summary>
        public bool Contains(double lon, double lat) =>
            lon >= MinLon && lon <= MaxLon && lat >= MinLat && lat <= MaxLat;
    }

    /// <summary>
    /// Calculates the Haversine distance between two points in kilometers.
    /// </summary>
    /// <param name="lon1">Longitude of first point in degrees.</param>
    /// <param name="lat1">Latitude of first point in degrees.</param>
    /// <param name="lon2">Longitude of second point in degrees.</param>
    /// <param name="lat2">Latitude of second point in degrees.</param>
    /// <returns>Distance in kilometers.</returns>
    public static double CalculateDistance(double lon1, double lat1, double lon2, double lat2)
    {
        var dLat = (lat2 - lat1) * DegreesToRadians;
        var dLon = (lon2 - lon1) * DegreesToRadians;

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * DegreesToRadians) * Math.Cos(lat2 * DegreesToRadians) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    /// <summary>
    /// Calculates the bearing (direction) from point 1 to point 2 in degrees.
    /// </summary>
    /// <param name="lon1">Longitude of first point in degrees.</param>
    /// <param name="lat1">Latitude of first point in degrees.</param>
    /// <param name="lon2">Longitude of second point in degrees.</param>
    /// <param name="lat2">Latitude of second point in degrees.</param>
    /// <returns>Bearing in degrees (0-360).</returns>
    public static double CalculateBearing(double lon1, double lat1, double lon2, double lat2)
    {
        var dLon = (lon2 - lon1) * DegreesToRadians;
        var lat1Rad = lat1 * DegreesToRadians;
        var lat2Rad = lat2 * DegreesToRadians;

        var y = Math.Sin(dLon) * Math.Cos(lat2Rad);
        var x = Math.Cos(lat1Rad) * Math.Sin(lat2Rad) -
                Math.Sin(lat1Rad) * Math.Cos(lat2Rad) * Math.Cos(dLon);

        var bearing = Math.Atan2(y, x) * RadiansToDegrees;
        return (bearing + 360) % 360;
    }

    /// <summary>
    /// Calculates the destination point given a start point, bearing, and distance.
    /// </summary>
    /// <param name="lon">Starting longitude in degrees.</param>
    /// <param name="lat">Starting latitude in degrees.</param>
    /// <param name="bearingDegrees">Bearing in degrees.</param>
    /// <param name="distanceKm">Distance in kilometers.</param>
    /// <returns>Destination point (longitude, latitude).</returns>
    public static (double Lon, double Lat) CalculateDestination(double lon, double lat, double bearingDegrees, double distanceKm)
    {
        var latRad = lat * DegreesToRadians;
        var lonRad = lon * DegreesToRadians;
        var bearingRad = bearingDegrees * DegreesToRadians;
        var angularDistance = distanceKm / EarthRadiusKm;

        var lat2 = Math.Asin(
            Math.Sin(latRad) * Math.Cos(angularDistance) +
            Math.Cos(latRad) * Math.Sin(angularDistance) * Math.Cos(bearingRad)
        );

        var lon2 = lonRad + Math.Atan2(
            Math.Sin(bearingRad) * Math.Sin(angularDistance) * Math.Cos(latRad),
            Math.Cos(angularDistance) - Math.Sin(latRad) * Math.Sin(lat2)
        );

        return (lon2 * RadiansToDegrees, lat2 * RadiansToDegrees);
    }

    /// <summary>
    /// Calculates a bounding box from a collection of coordinates.
    /// </summary>
    /// <param name="coordinates">Collection of (longitude, latitude) tuples.</param>
    /// <returns>Bounding box encompassing all coordinates.</returns>
    public static BoundingBox CalculateBoundingBox(IEnumerable<(double Lon, double Lat)> coordinates)
    {
        var coordList = coordinates.ToList();
        if (!coordList.Any())
            throw new ArgumentException("Coordinates collection cannot be empty", nameof(coordinates));

        var minLon = coordList.Min(c => c.Lon);
        var maxLon = coordList.Max(c => c.Lon);
        var minLat = coordList.Min(c => c.Lat);
        var maxLat = coordList.Max(c => c.Lat);

        return new BoundingBox(minLon, minLat, maxLon, maxLat);
    }

    /// <summary>
    /// Expands a bounding box by a percentage.
    /// </summary>
    /// <param name="bbox">The bounding box to expand.</param>
    /// <param name="percent">Expansion percentage (e.g., 0.1 for 10%).</param>
    /// <returns>Expanded bounding box.</returns>
    public static BoundingBox ExpandBoundingBox(BoundingBox bbox, double percent)
    {
        var lonExpansion = bbox.Width * percent / 2;
        var latExpansion = bbox.Height * percent / 2;

        return new BoundingBox(
            bbox.MinLon - lonExpansion,
            bbox.MinLat - latExpansion,
            bbox.MaxLon + lonExpansion,
            bbox.MaxLat + latExpansion
        );
    }

    /// <summary>
    /// Tests if a point is inside a polygon using the ray-casting algorithm.
    /// </summary>
    /// <param name="lon">Point longitude.</param>
    /// <param name="lat">Point latitude.</param>
    /// <param name="polygon">Polygon vertices as (longitude, latitude) tuples.</param>
    /// <returns>True if the point is inside the polygon; otherwise, false.</returns>
    public static bool IsPointInPolygon(double lon, double lat, IList<(double Lon, double Lat)> polygon)
    {
        if (polygon.Count < 3)
            throw new ArgumentException("Polygon must have at least 3 vertices", nameof(polygon));

        var inside = false;
        var j = polygon.Count - 1;

        for (var i = 0; i < polygon.Count; i++)
        {
            if ((polygon[i].Lat > lat) != (polygon[j].Lat > lat) &&
                lon < (polygon[j].Lon - polygon[i].Lon) * (lat - polygon[i].Lat) / (polygon[j].Lat - polygon[i].Lat) + polygon[i].Lon)
            {
                inside = !inside;
            }
            j = i;
        }

        return inside;
    }

    /// <summary>
    /// Simplifies a line using the Douglas-Peucker algorithm.
    /// </summary>
    /// <param name="points">Line points as (longitude, latitude) tuples.</param>
    /// <param name="epsilon">Simplification tolerance in degrees.</param>
    /// <returns>Simplified line.</returns>
    public static List<(double Lon, double Lat)> SimplifyLine(IList<(double Lon, double Lat)> points, double epsilon)
    {
        if (points.Count < 3)
            return points.ToList();

        var dmax = 0.0;
        var index = 0;

        for (var i = 1; i < points.Count - 1; i++)
        {
            var d = PerpendicularDistance(points[i], points[0], points[^1]);
            if (d > dmax)
            {
                index = i;
                dmax = d;
            }
        }

        if (dmax > epsilon)
        {
            var left = SimplifyLine(points.Take(index + 1).ToList(), epsilon);
            var right = SimplifyLine(points.Skip(index).ToList(), epsilon);

            return left.Take(left.Count - 1).Concat(right).ToList();
        }
        else
        {
            return new List<(double Lon, double Lat)> { points[0], points[^1] };
        }
    }

    /// <summary>
    /// Calculates the perpendicular distance from a point to a line.
    /// </summary>
    private static double PerpendicularDistance((double Lon, double Lat) point, (double Lon, double Lat) lineStart, (double Lon, double Lat) lineEnd)
    {
        var dx = lineEnd.Lon - lineStart.Lon;
        var dy = lineEnd.Lat - lineStart.Lat;

        if (dx == 0 && dy == 0)
            return Math.Sqrt(Math.Pow(point.Lon - lineStart.Lon, 2) + Math.Pow(point.Lat - lineStart.Lat, 2));

        var t = ((point.Lon - lineStart.Lon) * dx + (point.Lat - lineStart.Lat) * dy) / (dx * dx + dy * dy);
        t = Math.Max(0, Math.Min(1, t));

        var projectionX = lineStart.Lon + t * dx;
        var projectionY = lineStart.Lat + t * dy;

        return Math.Sqrt(Math.Pow(point.Lon - projectionX, 2) + Math.Pow(point.Lat - projectionY, 2));
    }

    /// <summary>
    /// Creates a buffer (circle) around a point.
    /// </summary>
    /// <param name="lon">Center longitude.</param>
    /// <param name="lat">Center latitude.</param>
    /// <param name="radiusKm">Buffer radius in kilometers.</param>
    /// <param name="segments">Number of segments (higher = smoother circle).</param>
    /// <returns>Buffer polygon as (longitude, latitude) tuples.</returns>
    public static List<(double Lon, double Lat)> CreateBuffer(double lon, double lat, double radiusKm, int segments = 32)
    {
        var buffer = new List<(double Lon, double Lat)>();

        for (var i = 0; i < segments; i++)
        {
            var bearing = (360.0 / segments) * i;
            var point = CalculateDestination(lon, lat, bearing, radiusKm);
            buffer.Add(point);
        }

        // Close the polygon
        buffer.Add(buffer[0]);

        return buffer;
    }

    /// <summary>
    /// Converts Web Mercator coordinates to WGS84 (longitude, latitude).
    /// </summary>
    /// <param name="x">X coordinate in Web Mercator.</param>
    /// <param name="y">Y coordinate in Web Mercator.</param>
    /// <returns>WGS84 coordinates (longitude, latitude).</returns>
    public static (double Lon, double Lat) WebMercatorToWgs84(double x, double y)
    {
        var lon = x / 20037508.34 * 180;
        var lat = y / 20037508.34 * 180;
        lat = 180 / Math.PI * (2 * Math.Atan(Math.Exp(lat * Math.PI / 180)) - Math.PI / 2);
        return (lon, lat);
    }

    /// <summary>
    /// Converts WGS84 (longitude, latitude) to Web Mercator coordinates.
    /// </summary>
    /// <param name="lon">Longitude in degrees.</param>
    /// <param name="lat">Latitude in degrees.</param>
    /// <returns>Web Mercator coordinates (x, y).</returns>
    public static (double X, double Y) Wgs84ToWebMercator(double lon, double lat)
    {
        var x = lon * 20037508.34 / 180;
        var y = Math.Log(Math.Tan((90 + lat) * Math.PI / 360)) / (Math.PI / 180);
        y = y * 20037508.34 / 180;
        return (x, y);
    }

    /// <summary>
    /// Calculates the area of a polygon in square kilometers.
    /// </summary>
    /// <param name="polygon">Polygon vertices as (longitude, latitude) tuples.</param>
    /// <returns>Area in square kilometers.</returns>
    public static double CalculatePolygonArea(IList<(double Lon, double Lat)> polygon)
    {
        if (polygon.Count < 3)
            return 0;

        var area = 0.0;
        var j = polygon.Count - 1;

        for (var i = 0; i < polygon.Count; i++)
        {
            var p1 = polygon[i];
            var p2 = polygon[j];
            area += (p2.Lon - p1.Lon) * (p2.Lat + p1.Lat);
            j = i;
        }

        // Convert to square kilometers (approximate)
        return Math.Abs(area) * 12364.0; // Roughly 111km per degree * 111km per degree
    }

    /// <summary>
    /// Validates if coordinates are valid WGS84 coordinates.
    /// </summary>
    /// <param name="lon">Longitude in degrees.</param>
    /// <param name="lat">Latitude in degrees.</param>
    /// <returns>True if valid; otherwise, false.</returns>
    public static bool IsValidCoordinate(double lon, double lat)
    {
        return lon >= -180 && lon <= 180 && lat >= -90 && lat <= 90;
    }
}
