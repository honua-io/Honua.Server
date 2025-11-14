// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Honua.Server.Core.GeometryValidation;
using Honua.Server.Core.Query;
using NetTopologySuite.Geometries;

namespace Honua.Server.Host.Wfs.Filters;

/// <summary>
/// Parses GML 3.2 geometry elements to NetTopologySuite Geometry objects.
/// Supports Point, LineString, Polygon, and Envelope geometries commonly used in WFS filters.
/// Includes DOS protection via geometry complexity validation.
/// </summary>
internal static class GmlGeometryParser
{
    private const string GmlNamespace = "http://www.opengis.net/gml/3.2";
    private static readonly GeometryFactory DefaultFactory = new();
    private static GeometryComplexityValidator? _complexityValidator;

    /// <summary>
    /// Sets the geometry complexity validator for DOS protection.
    /// Should be called during application startup with configured options.
    /// </summary>
    public static void SetComplexityValidator(GeometryComplexityValidator? validator)
    {
        _complexityValidator = validator;
    }

    /// <summary>
    /// Parses a GML geometry element and returns a QueryGeometryValue.
    /// Validates geometry complexity to prevent DOS attacks.
    /// </summary>
    /// <param name="element">The GML geometry element</param>
    /// <param name="filterCrs">Optional CRS from the filter</param>
    /// <returns>A QueryGeometryValue containing WKT and SRID</returns>
    /// <exception cref="GeometryComplexityException">Thrown when geometry exceeds complexity limits</exception>
    public static QueryGeometryValue Parse(XElement element, string? filterCrs)
    {
        ArgumentNullException.ThrowIfNull(element);

        NetTopologySuite.Geometries.Geometry geometry = element.Name.LocalName switch
        {
            "Point" => ParsePoint(element),
            "LineString" => ParseLineString(element),
            "Polygon" => ParsePolygon(element),
            "Envelope" => ParseEnvelope(element),
            "MultiPoint" => ParseMultiPoint(element),
            "MultiLineString" => ParseMultiLineString(element),
            "MultiPolygon" => ParseMultiPolygon(element),
            _ => throw new NotSupportedException($"GML geometry type '{element.Name.LocalName}' is not supported.")
        };

        // Parse SRID from srsName attribute
        var srsName = element.Attribute("srsName")?.Value ?? filterCrs;
        var srid = ParseSrsName(srsName);
        if (srid.HasValue)
        {
            geometry.SRID = srid.Value;
        }

        // SECURITY: Validate geometry complexity to prevent DOS attacks
        // This check prevents attackers from submitting geometries with millions of vertices
        // that would exhaust CPU and memory during validation, indexing, and rendering
        _complexityValidator?.Validate(geometry);

        return new QueryGeometryValue(geometry.ToText(), geometry.SRID);
    }

    /// <summary>
    /// Parses a GML Point element.
    /// </summary>
    private static Point ParsePoint(XElement element)
    {
        // GML Point can have either gml:pos or gml:coordinates
        var posElement = element.Element(XName.Get("pos", GmlNamespace));
        if (posElement != null)
        {
            var coords = ParsePosList(posElement.Value, 1);
            return DefaultFactory.CreatePoint(coords[0]);
        }

        var coordinatesElement = element.Element(XName.Get("coordinates", GmlNamespace));
        if (coordinatesElement != null)
        {
            var coords = ParseCoordinates(coordinatesElement.Value);
            if (coords.Length == 0)
            {
                throw new InvalidOperationException("Point requires at least one coordinate.");
            }
            return DefaultFactory.CreatePoint(coords[0]);
        }

        throw new InvalidOperationException("Point element requires gml:pos or gml:coordinates.");
    }

    /// <summary>
    /// Parses a GML LineString element.
    /// </summary>
    private static LineString ParseLineString(XElement element)
    {
        var posListElement = element.Element(XName.Get("posList", GmlNamespace));
        if (posListElement != null)
        {
            var coords = ParsePosList(posListElement.Value);
            return DefaultFactory.CreateLineString(coords);
        }

        var coordinatesElement = element.Element(XName.Get("coordinates", GmlNamespace));
        if (coordinatesElement != null)
        {
            var coords = ParseCoordinates(coordinatesElement.Value);
            return DefaultFactory.CreateLineString(coords);
        }

        throw new InvalidOperationException("LineString element requires gml:posList or gml:coordinates.");
    }

    /// <summary>
    /// Parses a GML Polygon element.
    /// </summary>
    private static Polygon ParsePolygon(XElement element)
    {
        var exteriorElement = element.Element(XName.Get("exterior", GmlNamespace));
        if (exteriorElement == null)
        {
            throw new InvalidOperationException("Polygon element requires gml:exterior.");
        }

        var linearRingElement = exteriorElement.Element(XName.Get("LinearRing", GmlNamespace));
        if (linearRingElement == null)
        {
            throw new InvalidOperationException("Polygon exterior requires gml:LinearRing.");
        }

        var shell = ParseLinearRing(linearRingElement);

        // Parse interior rings (holes)
        var holes = element.Elements(XName.Get("interior", GmlNamespace))
            .Select(interiorElement =>
            {
                var ringElement = interiorElement.Element(XName.Get("LinearRing", GmlNamespace));
                if (ringElement == null)
                {
                    throw new InvalidOperationException("Polygon interior requires gml:LinearRing.");
                }
                return ParseLinearRing(ringElement);
            })
            .ToArray();

        return DefaultFactory.CreatePolygon(shell, holes);
    }

    /// <summary>
    /// Parses a GML LinearRing element.
    /// </summary>
    private static LinearRing ParseLinearRing(XElement element)
    {
        var posListElement = element.Element(XName.Get("posList", GmlNamespace));
        if (posListElement != null)
        {
            var coords = ParsePosList(posListElement.Value);
            return DefaultFactory.CreateLinearRing(coords);
        }

        var coordinatesElement = element.Element(XName.Get("coordinates", GmlNamespace));
        if (coordinatesElement != null)
        {
            var coords = ParseCoordinates(coordinatesElement.Value);
            return DefaultFactory.CreateLinearRing(coords);
        }

        throw new InvalidOperationException("LinearRing element requires gml:posList or gml:coordinates.");
    }

    /// <summary>
    /// Parses a GML Envelope element (used for BBOX).
    /// </summary>
    private static Polygon ParseEnvelope(XElement element)
    {
        var lowerCorner = element.Element(XName.Get("lowerCorner", GmlNamespace))?.Value;
        var upperCorner = element.Element(XName.Get("upperCorner", GmlNamespace))?.Value;

        if (lowerCorner == null || upperCorner == null)
        {
            throw new InvalidOperationException("Envelope requires gml:lowerCorner and gml:upperCorner.");
        }

        var lowerCoords = ParseCoordinatePair(lowerCorner);
        var upperCoords = ParseCoordinatePair(upperCorner);

        // Create a rectangular polygon from the envelope
        var envelope = new Envelope(
            lowerCoords.X, upperCoords.X,  // minX, maxX
            lowerCoords.Y, upperCoords.Y); // minY, maxY

        return (Polygon)DefaultFactory.ToGeometry(envelope);
    }

    /// <summary>
    /// Parses a GML MultiPoint element.
    /// </summary>
    private static MultiPoint ParseMultiPoint(XElement element)
    {
        var pointMembers = element.Elements(XName.Get("pointMember", GmlNamespace))
            .Select(member => member.Element(XName.Get("Point", GmlNamespace)))
            .Where(point => point != null)
            .Select(point => ParsePoint(point!))
            .ToArray();

        if (pointMembers.Length == 0)
        {
            throw new InvalidOperationException("MultiPoint requires at least one pointMember.");
        }

        return DefaultFactory.CreateMultiPoint(pointMembers);
    }

    /// <summary>
    /// Parses a GML MultiLineString element.
    /// </summary>
    private static MultiLineString ParseMultiLineString(XElement element)
    {
        var lineStringMembers = element.Elements(XName.Get("lineStringMember", GmlNamespace))
            .Select(member => member.Element(XName.Get("LineString", GmlNamespace)))
            .Where(line => line != null)
            .Select(line => ParseLineString(line!))
            .ToArray();

        if (lineStringMembers.Length == 0)
        {
            throw new InvalidOperationException("MultiLineString requires at least one lineStringMember.");
        }

        return DefaultFactory.CreateMultiLineString(lineStringMembers);
    }

    /// <summary>
    /// Parses a GML MultiPolygon element.
    /// </summary>
    private static MultiPolygon ParseMultiPolygon(XElement element)
    {
        var polygonMembers = element.Elements(XName.Get("polygonMember", GmlNamespace))
            .Select(member => member.Element(XName.Get("Polygon", GmlNamespace)))
            .Where(polygon => polygon != null)
            .Select(polygon => ParsePolygon(polygon!))
            .ToArray();

        if (polygonMembers.Length == 0)
        {
            throw new InvalidOperationException("MultiPolygon requires at least one polygonMember.");
        }

        return DefaultFactory.CreateMultiPolygon(polygonMembers);
    }

    /// <summary>
    /// Parses a space-separated list of coordinates from gml:posList.
    /// </summary>
    private static Coordinate[] ParsePosList(string posList, int? expectedCount = null)
    {
        var values = posList.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture))
            .ToArray();

        if (values.Length % 2 != 0)
        {
            throw new InvalidOperationException("posList must contain an even number of coordinate values (x y pairs).");
        }

        var coords = new Coordinate[values.Length / 2];
        for (var i = 0; i < coords.Length; i++)
        {
            coords[i] = new Coordinate(values[i * 2], values[i * 2 + 1]);
        }

        if (expectedCount.HasValue && coords.Length != expectedCount.Value)
        {
            throw new InvalidOperationException($"Expected {expectedCount.Value} coordinate(s), but found {coords.Length}.");
        }

        return coords;
    }

    /// <summary>
    /// Parses a comma-separated list of coordinates from gml:coordinates (GML 2 style).
    /// </summary>
    private static Coordinate[] ParseCoordinates(string coordinates)
    {
        var tuples = coordinates.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var coords = new Coordinate[tuples.Length];

        for (var i = 0; i < tuples.Length; i++)
        {
            var parts = tuples[i].Split(',');
            if (parts.Length < 2)
            {
                throw new InvalidOperationException("Each coordinate tuple must contain at least x,y values.");
            }

            var x = double.Parse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture);
            var y = double.Parse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture);
            coords[i] = new Coordinate(x, y);
        }

        return coords;
    }

    /// <summary>
    /// Parses a single coordinate pair from space-separated values.
    /// </summary>
    private static Coordinate ParseCoordinatePair(string value)
    {
        var parts = value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            throw new InvalidOperationException("Coordinate pair requires at least two values (x y).");
        }

        var x = double.Parse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture);
        var y = double.Parse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture);
        return new Coordinate(x, y);
    }

    /// <summary>
    /// Parses an SRID from a CRS URN or URL.
    /// </summary>
    private static int? ParseSrsName(string? srsName)
    {
        if (string.IsNullOrWhiteSpace(srsName))
        {
            return null;
        }

        // Handle URN format: urn:ogc:def:crs:EPSG::4326
        if (srsName.StartsWith("urn:ogc:def:crs:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = srsName.Split(':');
            if (parts.Length >= 7 && int.TryParse(parts[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var srid))
            {
                return srid;
            }
        }

        // Handle URL format: http://www.opengis.net/def/crs/EPSG/0/4326
        if (srsName.Contains("/EPSG/", StringComparison.OrdinalIgnoreCase))
        {
            var parts = srsName.Split('/');
            if (int.TryParse(parts[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var srid))
            {
                return srid;
            }
        }

        // Handle simple EPSG:4326 format
        if (srsName.StartsWith("EPSG:", StringComparison.OrdinalIgnoreCase))
        {
            var code = srsName.Substring(5);
            if (int.TryParse(code, NumberStyles.Integer, CultureInfo.InvariantCulture, out var srid))
            {
                return srid;
            }
        }

        return null;
    }
}
