// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Honua.Server.Core.Validation;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Migration.GeoservicesRest;

public static class GeoservicesRestGeometryConverter
{
    private static readonly GeometryFactory GeometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory();
    private static readonly GeoJsonWriter GeoJsonWriter = new();

    public static string? ToGeoJson(JsonElement geometryElement, string? geometryType)
    {
        if (geometryElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(geometryType))
        {
            return null;
        }

        var geometry = CreateGeometry(geometryElement, geometryType);
        if (geometry is null || geometry.IsEmpty)
        {
            return null;
        }

        return GeoJsonWriter.Write(geometry);
    }

    private static Geometry? CreateGeometry(JsonElement element, string geometryType)
    {
        geometryType = geometryType.Trim();
        return geometryType switch
        {
            "esriGeometryPoint" => CreatePoint(element),
            "esriGeometryMultipoint" => CreateMultiPoint(element),
            "esriGeometryPolyline" => CreatePolyline(element),
            "esriGeometryPolygon" => CreatePolygon(element),
            _ => null
        };
    }

    private static Geometry? CreatePoint(JsonElement element)
    {
        if (!element.TryGetProperty("x", out var xProp) || !element.TryGetProperty("y", out var yProp))
        {
            return null;
        }

        var x = xProp.GetDouble();
        var y = yProp.GetDouble();
        double z = double.NaN;
        if (element.TryGetProperty("z", out var zProp) && zProp.ValueKind == JsonValueKind.Number)
        {
            z = zProp.GetDouble();
        }

        var coordinate = new Coordinate(x, y);
        if (!double.IsNaN(z))
        {
            coordinate.Z = z;
        }

        return GeometryFactory.CreatePoint(coordinate);
    }

    private static Geometry? CreateMultiPoint(JsonElement element)
    {
        if (!element.TryGetProperty("points", out var pointsProp) || pointsProp.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var points = new List<Point>();
        foreach (var item in pointsProp.EnumerateArray())
        {
            var coordinate = ReadCoordinate(item);
            if (coordinate is null)
            {
                continue;
            }

            points.Add(GeometryFactory.CreatePoint(coordinate));
        }

        return points.Count switch
        {
            0 => null,
            1 => points[0],
            _ => GeometryFactory.CreateMultiPoint(points.ToArray())
        };
    }

    private static Geometry? CreatePolyline(JsonElement element)
    {
        if (!element.TryGetProperty("paths", out var pathsProp) || pathsProp.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var lines = new List<LineString>();
        foreach (var path in pathsProp.EnumerateArray())
        {
            var sequence = ReadCoordinateSequence(path);
            if (sequence is null || sequence.Count < 2)
            {
                continue;
            }

            lines.Add(GeometryFactory.CreateLineString(sequence));
        }

        return lines.Count switch
        {
            0 => null,
            1 => lines[0],
            _ => GeometryFactory.CreateMultiLineString(lines.ToArray())
        };
    }

    private static Geometry? CreatePolygon(JsonElement element)
    {
        if (!element.TryGetProperty("rings", out var ringsProp) || ringsProp.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var shells = new List<LinearRing>();
        var holes = new List<LinearRing>();
        foreach (var ringElement in ringsProp.EnumerateArray())
        {
            var sequence = ReadCoordinateSequence(ringElement, ensureClosed: true);
            if (sequence is null || sequence.Count < 4)
            {
                continue;
            }

            var ring = GeometryFactory.CreateLinearRing(sequence);
            if (ring.IsEmpty)
            {
                continue;
            }

            if (IsCounterClockwise(ring))
            {
                holes.Add(ring);
            }
            else
            {
                shells.Add(ring);
            }
        }

        if (shells.Count == 0 && holes.Count > 0)
        {
            shells.AddRange(holes);
            holes.Clear();
        }

        if (shells.Count == 0)
        {
            return null;
        }

        var polygonBuilders = shells
            .Select(shell => new PolygonBuilder(shell))
            .ToList();

        foreach (var hole in holes)
        {
            var assigned = false;
            foreach (var builder in polygonBuilders)
            {
                if (builder.Shell.EnvelopeInternal.Contains(hole.EnvelopeInternal))
                {
                    builder.Holes.Add(hole);
                    assigned = true;
                    break;
                }
            }

            if (!assigned)
            {
                polygonBuilders[0].Holes.Add(hole);
            }
        }

        if (polygonBuilders.Count == 1)
        {
            var builder = polygonBuilders[0];
            var polygon = GeometryFactory.CreatePolygon(builder.Shell, builder.Holes.ToArray());

            // Validate polygon geometry
            var validationResult = GeometryValidator.ValidatePolygon(polygon);
            if (!validationResult.IsValid)
            {
                throw new InvalidOperationException($"Invalid polygon geometry from GeoServices: {validationResult.ErrorMessage}");
            }

            return polygon;
        }

        var polygons = polygonBuilders
            .Select(builder => GeometryFactory.CreatePolygon(builder.Shell, builder.Holes.ToArray()))
            .ToArray();

        var multiPolygon = GeometryFactory.CreateMultiPolygon(polygons);

        // Validate multi-polygon geometry
        var multiValidationResult = GeometryValidator.ValidateMultiPolygon(multiPolygon);
        if (!multiValidationResult.IsValid)
        {
            throw new InvalidOperationException($"Invalid multi-polygon geometry from GeoServices: {multiValidationResult.ErrorMessage}");
        }

        return multiPolygon;
    }

    private static CoordinateSequence? ReadCoordinateSequence(JsonElement element, bool ensureClosed = false)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var coordinates = new List<Coordinate>();
        foreach (var coordinate in element.EnumerateArray())
        {
            var item = ReadCoordinate(coordinate);
            if (item is not null)
            {
                coordinates.Add(item);
            }
        }

        if (coordinates.Count == 0)
        {
            return null;
        }

        if (ensureClosed && !coordinates[0].Equals2D(coordinates[^1]))
        {
            coordinates.Add(coordinates[0]);
        }

        return GeometryFactory.CoordinateSequenceFactory.Create(coordinates.ToArray());
    }

    private static Coordinate? ReadCoordinate(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            var enumerator = element.EnumerateArray();
            if (!enumerator.MoveNext())
            {
                return null;
            }

            var x = enumerator.Current.GetDouble();
            double y = double.NaN;
            double z = double.NaN;
            if (enumerator.MoveNext())
            {
                y = enumerator.Current.GetDouble();
            }

            if (enumerator.MoveNext())
            {
                z = enumerator.Current.ValueKind == JsonValueKind.Number ? enumerator.Current.GetDouble() : double.NaN;
            }

            var coordinate = new Coordinate(x, y);
            if (!double.IsNaN(z))
            {
                coordinate.Z = z;
            }

            return coordinate;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            if (!element.TryGetProperty("x", out var xProp) || !element.TryGetProperty("y", out var yProp))
            {
                return null;
            }

            var x = xProp.GetDouble();
            var y = yProp.GetDouble();
            double z = double.NaN;
            if (element.TryGetProperty("z", out var zProp) && zProp.ValueKind == JsonValueKind.Number)
            {
                z = zProp.GetDouble();
            }

            var coordinate = new Coordinate(x, y);
            if (!double.IsNaN(z))
            {
                coordinate.Z = z;
            }

            return coordinate;
        }

        return null;
    }

    /// <summary>
    /// Determines if a linear ring is counter-clockwise using NetTopologySuite's robust algorithm.
    /// In GeoJSON and OGC standards, exterior rings must be CCW and holes must be CW.
    /// Uses the signed area method (shoelace formula): CCW rings have negative signed area
    /// in standard coordinate systems where Y increases upward.
    /// </summary>
    private static bool IsCounterClockwise(LinearRing ring)
    {
        // Use NetTopologySuite's robust and well-tested implementation
        // rather than a custom shoelace implementation which can be error-prone
        return Orientation.IsCCW(ring.CoordinateSequence);
    }

    private sealed class PolygonBuilder
    {
        public PolygonBuilder(LinearRing shell)
        {
            Shell = shell ?? throw new ArgumentNullException(nameof(shell));
        }

        public LinearRing Shell { get; }

        public List<LinearRing> Holes { get; } = new();
    }
}
