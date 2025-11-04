// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using Honua.Server.Core.Validation;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Geoservices.GeometryService;

public sealed class EsriGeometrySerializer : IGeometrySerializer
{
    public IReadOnlyList<Geometry> DeserializeGeometries(JsonNode? payload, string geometryType, int srid, CancellationToken cancellationToken = default)
    {
        if (payload is null)
        {
            throw new GeometrySerializationException("geometries payload is required.");
        }

        var array = ExtractGeometriesArray(payload);
        if (array.Count == 0)
        {
            return Array.Empty<Geometry>();
        }

        var factory = CreateFactory(srid);
        var results = new List<Geometry>(array.Count);

        foreach (var node in array)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (node is null)
            {
                throw new GeometrySerializationException("Geometry entry cannot be null.");
            }

            results.Add(DeserializeGeometry(node, geometryType, factory));
        }

        return results;
    }

    public JsonObject SerializeGeometries(IReadOnlyList<Geometry> geometries, string geometryType, int srid, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(geometries);

        var array = new JsonArray();
        var hasZ = false;

        foreach (var geometry in geometries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (geometry is null)
            {
                throw new GeometrySerializationException("Geometry entry cannot be null.");
            }

            array.Add(SerializeGeometryInternal(geometry, geometryType));
            hasZ |= GeometryHasZ(geometry);
        }

        var response = new JsonObject
        {
            ["geometryType"] = geometryType,
            ["geometries"] = array,
            ["spatialReference"] = CreateSpatialReference(srid)
        };

        if (hasZ)
        {
            response["hasZ"] = true;
        }

        return response;
    }

    public JsonObject SerializeGeometry(Geometry geometry, string geometryType, int srid)
    {
        Guard.NotNull(geometry);

        var serialized = SerializeGeometryInternal(geometry, geometryType);
        serialized["spatialReference"] = CreateSpatialReference(srid);

        if (GeometryHasZ(geometry))
        {
            serialized["hasZ"] = true;
        }

        return serialized;
    }

    private static JsonArray ExtractGeometriesArray(JsonNode payload)
    {
        return payload switch
        {
            JsonArray array => array,
            JsonObject obj when obj.TryGetPropertyValue("geometries", out var node) && node is JsonArray geometries => geometries,
            _ => throw new GeometrySerializationException("geometries payload must contain a 'geometries' array.")
        };
    }

    private static Geometry DeserializeGeometry(JsonNode node, string geometryType, GeometryFactory factory)
    {
        return geometryType switch
        {
            var type when type.Equals("esriGeometryPoint", StringComparison.OrdinalIgnoreCase) => DeserializePoint(node, factory),
            var type when type.Equals("esriGeometryMultipoint", StringComparison.OrdinalIgnoreCase) => DeserializeMultipoint(node, factory),
            var type when type.Equals("esriGeometryPolyline", StringComparison.OrdinalIgnoreCase) => DeserializePolyline(node, factory),
            var type when type.Equals("esriGeometryPolygon", StringComparison.OrdinalIgnoreCase) => DeserializePolygon(node, factory),
            _ => throw new GeometrySerializationException($"Geometry type '{geometryType}' is not supported.")
        };
    }

    private static JsonObject SerializeGeometryInternal(Geometry geometry, string geometryType)
    {
        return geometryType switch
        {
            var type when type.Equals("esriGeometryPoint", StringComparison.OrdinalIgnoreCase) => SerializePoint(geometry),
            var type when type.Equals("esriGeometryMultipoint", StringComparison.OrdinalIgnoreCase) => SerializeMultipoint(geometry),
            var type when type.Equals("esriGeometryPolyline", StringComparison.OrdinalIgnoreCase) => SerializePolyline(geometry),
            var type when type.Equals("esriGeometryPolygon", StringComparison.OrdinalIgnoreCase) => SerializePolygon(geometry),
            _ => throw new GeometrySerializationException($"Geometry type '{geometryType}' is not supported.")
        };
    }

    private static GeometryFactory CreateFactory(int srid)
    {
        return new GeometryFactory(new PrecisionModel(), srid, CoordinateArraySequenceFactory.Instance);
    }

    private static Geometry DeserializePoint(JsonNode node, GeometryFactory factory)
    {
        if (node is not JsonObject obj)
        {
            throw new GeometrySerializationException("Point geometry must be an object.");
        }

        var x = ReadRequiredDouble(obj, "x");
        var y = ReadRequiredDouble(obj, "y");
        var z = ReadOptionalDouble(obj, "z", double.NaN);

        var point = factory.CreatePoint(new CoordinateZ(x, y, z));
        point.SRID = factory.SRID;
        return point;
    }

    private static Geometry DeserializeMultipoint(JsonNode node, GeometryFactory factory)
    {
        if (node is not JsonObject obj)
        {
            throw new GeometrySerializationException("Multipoint geometry must be an object.");
        }

        if (!obj.TryGetPropertyValue("points", out var pointsNode) || pointsNode is not JsonArray pointsArray)
        {
            throw new GeometrySerializationException("Multipoint geometry must contain a 'points' array.");
        }

        var coordinates = ParseCoordinateArray(pointsArray);
        if (coordinates.Length == 1)
        {
            var point = factory.CreatePoint(coordinates[0]);
            point.SRID = factory.SRID;
            return point;
        }

        var multiPoint = factory.CreateMultiPointFromCoords(coordinates);
        multiPoint.SRID = factory.SRID;
        return multiPoint;
    }

    private static Geometry DeserializePolyline(JsonNode node, GeometryFactory factory)
    {
        if (node is not JsonObject obj)
        {
            throw new GeometrySerializationException("Polyline geometry must be an object.");
        }

        if (!obj.TryGetPropertyValue("paths", out var pathsNode) || pathsNode is not JsonArray pathsArray)
        {
            throw new GeometrySerializationException("Polyline geometry must contain a 'paths' array.");
        }

        var lines = new List<LineString>(pathsArray.Count);
        foreach (var pathNode in pathsArray)
        {
            if (pathNode is not JsonArray pathArray)
            {
                throw new GeometrySerializationException("Each path must be an array of coordinates.");
            }

            var coords = ParseCoordinateArray(pathArray);
            if (coords.Length < 2)
            {
                throw new GeometrySerializationException("Path must contain at least two coordinates.");
            }

            var line = factory.CreateLineString(coords);
            line.SRID = factory.SRID;
            lines.Add(line);
        }

        if (lines.Count == 1)
        {
            return lines[0];
        }

        var multiLine = factory.CreateMultiLineString(lines.ToArray());
        multiLine.SRID = factory.SRID;
        return multiLine;
    }

    private static Geometry DeserializePolygon(JsonNode node, GeometryFactory factory)
    {
        if (node is not JsonObject obj)
        {
            throw new GeometrySerializationException("Polygon geometry must be an object.");
        }

        if (!obj.TryGetPropertyValue("rings", out var ringsNode) || ringsNode is not JsonArray ringsArray)
        {
            throw new GeometrySerializationException("Polygon geometry must contain a 'rings' array.");
        }

        var shells = new List<LinearRing>();
        var holes = new List<List<LinearRing>>();

        foreach (var ringNode in ringsArray)
        {
            if (ringNode is not JsonArray ringArray)
            {
                throw new GeometrySerializationException("Each ring must be an array of coordinates.");
            }

            var coords = ParseCoordinateArray(ringArray, ensureClosed: true);
            if (coords.Length < 4)
            {
                throw new GeometrySerializationException("Ring must contain at least four coordinates.");
            }

            var ring = factory.CreateLinearRing(coords);
            ring.SRID = factory.SRID;

            var signedArea = Area.OfRingSigned(coords);
            var isExterior = signedArea < 0d || shells.Count == 0;

            if (isExterior)
            {
                shells.Add(ring);
                holes.Add(new List<LinearRing>());
                continue;
            }

            var added = false;
            for (var i = holes.Count - 1; i >= 0; i--)
            {
                if (shells[i].EnvelopeInternal.Contains(ring.EnvelopeInternal))
                {
                    holes[i].Add(ring);
                    added = true;
                    break;
                }
            }

            if (!added)
            {
                // Fallback: associate with most recent shell.
                holes[^1].Add(ring);
            }
        }

        if (shells.Count == 0)
        {
            throw new GeometrySerializationException("Polygon payload did not include any exterior rings.");
        }

        var polygons = new List<Polygon>(shells.Count);
        for (var i = 0; i < shells.Count; i++)
        {
            var polygon = factory.CreatePolygon(shells[i], holes[i].ToArray());
            polygon.SRID = factory.SRID;

            // Validate polygon geometry according to Esri GeoServices requirements
            var validationResult = GeometryValidator.ValidatePolygon(polygon);
            if (!validationResult.IsValid)
            {
                throw new GeometrySerializationException($"Invalid polygon geometry: {validationResult.ErrorMessage}");
            }

            polygons.Add(polygon);
        }

        if (polygons.Count == 1)
        {
            return polygons[0];
        }

        var multi = factory.CreateMultiPolygon(polygons.ToArray());
        multi.SRID = factory.SRID;

        // Validate multi-polygon
        var multiValidationResult = GeometryValidator.ValidateMultiPolygon(multi);
        if (!multiValidationResult.IsValid)
        {
            throw new GeometrySerializationException($"Invalid multi-polygon geometry: {multiValidationResult.ErrorMessage}");
        }

        return multi;
    }

    private static JsonObject SerializePoint(Geometry geometry)
    {
        var point = geometry switch
        {
            Point p => p,
            MultiPoint multi when multi.NumGeometries > 0 => (Point)multi.Geometries[0],
            _ => throw new GeometrySerializationException("Expected point geometry for esriGeometryPoint serialization.")
        };

        var obj = new JsonObject
        {
            ["x"] = point.X,
            ["y"] = point.Y
        };

        if (!double.IsNaN(point.Z) && !double.IsInfinity(point.Z))
        {
            obj["z"] = point.Z;
        }

        return obj;
    }

    private static JsonObject SerializeMultipoint(Geometry geometry)
    {
        var coordinates = geometry switch
        {
            Point point => new[] { point.Coordinate },
            MultiPoint multi => multi.Coordinates,
            _ => geometry.Coordinates
        };

        var points = new JsonArray();
        foreach (var coordinate in coordinates)
        {
            points.Add(CreateCoordinateTuple(coordinate));
        }

        return new JsonObject
        {
            ["points"] = points
        };
    }

    private static JsonObject SerializePolyline(Geometry geometry)
    {
        var paths = new JsonArray();

        foreach (var line in EnumerateLineStrings(geometry))
        {
            paths.Add(CreateCoordinateCollection(line.CoordinateSequence));
        }

        return new JsonObject
        {
            ["paths"] = paths
        };
    }

    private static JsonObject SerializePolygon(Geometry geometry)
    {
        var rings = new JsonArray();

        foreach (var polygon in EnumeratePolygons(geometry))
        {
            // Validate polygon before serialization
            var validationResult = GeometryValidator.ValidatePolygon(polygon);
            if (!validationResult.IsValid)
            {
                throw new GeometrySerializationException($"Cannot serialize invalid polygon: {validationResult.ErrorMessage}");
            }

            // Ensure polygon has correct Esri orientation (CW exterior, CCW holes)
            var esriPolygon = GeometryValidator.EnsureEsriOrientation(polygon);

            rings.Add(CreateCoordinateCollection(esriPolygon.ExteriorRing.CoordinateSequence));
            foreach (var interior in esriPolygon.InteriorRings)
            {
                rings.Add(CreateCoordinateCollection(interior.CoordinateSequence));
            }
        }

        return new JsonObject
        {
            ["rings"] = rings
        };
    }

    private static JsonObject CreateSpatialReference(int srid)
    {
        return new JsonObject
        {
            ["wkid"] = srid
        };
    }

    private static double ReadRequiredDouble(JsonObject obj, string property)
    {
        if (obj.TryGetPropertyValue(property, out var value) && value is JsonValue jsonValue && jsonValue.TryGetValue<double>(out var result))
        {
            return result;
        }

        throw new GeometrySerializationException($"Property '{property}' must be provided and numeric.");
    }

    private static double ReadOptionalDouble(JsonObject obj, string property, double fallback)
    {
        if (!obj.TryGetPropertyValue(property, out var value))
        {
            return fallback;
        }

        if (value is JsonValue jsonValue && jsonValue.TryGetValue<double>(out var result))
        {
            return result;
        }

        throw new GeometrySerializationException($"Property '{property}' must be numeric if provided.");
    }

    private static Coordinate[] ParseCoordinateArray(JsonArray array, bool ensureClosed = false)
    {
        var coordinates = new Coordinate[array.Count];
        for (var i = 0; i < array.Count; i++)
        {
            if (array[i] is not JsonArray tuple)
            {
                throw new GeometrySerializationException("Coordinate entry must be an array.");
            }

            coordinates[i] = ReadCoordinate(tuple);
        }

        if (ensureClosed && coordinates.Length > 0)
        {
            var first = coordinates[0];
            var last = coordinates[^1];
            if (!first.Equals2D(last) || (double.IsNaN(first.Z) != double.IsNaN(last.Z)) || (!double.IsNaN(first.Z) && Math.Abs(first.Z - last.Z) > 1e-9))
            {
                Array.Resize(ref coordinates, coordinates.Length + 1);
                coordinates[^1] = new CoordinateZ(first.X, first.Y, first.Z);
            }
        }

        return coordinates;
    }

    private static Coordinate ReadCoordinate(JsonArray tuple)
    {
        if (tuple.Count < 2)
        {
            throw new GeometrySerializationException("Coordinate must provide at least x and y values.");
        }

        var x = ReadTupleValue(tuple[0], "x");
        var y = ReadTupleValue(tuple[1], "y");
        var z = tuple.Count >= 3 ? ReadTupleValue(tuple[2], "z") : double.NaN;

        return new CoordinateZ(x, y, z);
    }

    private static double ReadTupleValue(JsonNode? node, string component)
    {
        if (node is JsonValue value && value.TryGetValue<double>(out var result))
        {
            return result;
        }

        throw new GeometrySerializationException($"Coordinate {component} component must be numeric.");
    }

    private static JsonArray CreateCoordinateCollection(CoordinateSequence sequence)
    {
        var array = new JsonArray();
        for (var i = 0; i < sequence.Count; i++)
        {
            var coordinate = sequence.GetCoordinateCopy(i);
            array.Add(CreateCoordinateTuple(coordinate));
        }

        return array;
    }

    private static JsonArray CreateCoordinateTuple(Coordinate coordinate)
    {
        var tuple = new JsonArray
        {
            coordinate.X,
            coordinate.Y
        };

        if (!double.IsNaN(coordinate.Z) && !double.IsInfinity(coordinate.Z))
        {
            tuple.Add(coordinate.Z);
        }

        return tuple;
    }

    private static IEnumerable<LineString> EnumerateLineStrings(Geometry geometry)
    {
        switch (geometry)
        {
            case LineString line:
                yield return line;
                yield break;
            case MultiLineString multi:
                foreach (LineString line in multi.Geometries)
                {
                    yield return line;
                }
                yield break;
            case GeometryCollection collection:
                foreach (var inner in collection.Geometries)
                {
                    foreach (var line in EnumerateLineStrings(inner))
                    {
                        yield return line;
                    }
                }
                yield break;
            default:
                throw new GeometrySerializationException("Expected a line string geometry for polyline serialization.");
        }
    }

    private static IEnumerable<Polygon> EnumeratePolygons(Geometry geometry)
    {
        switch (geometry)
        {
            case Polygon polygon:
                yield return polygon;
                yield break;
            case MultiPolygon multi:
                foreach (Polygon polygon in multi.Geometries)
                {
                    yield return polygon;
                }
                yield break;
            case GeometryCollection collection:
                foreach (var inner in collection.Geometries)
                {
                    foreach (var polygon in EnumeratePolygons(inner))
                    {
                        yield return polygon;
                    }
                }
                yield break;
            default:
                throw new GeometrySerializationException("Expected a polygon geometry for esriGeometryPolygon serialization.");
        }
    }

    private static bool GeometryHasZ(Geometry geometry)
    {
        foreach (var coordinate in geometry.Coordinates)
        {
            if (!double.IsNaN(coordinate.Z) && !double.IsInfinity(coordinate.Z))
            {
                return true;
            }
        }

        return false;
    }
}
