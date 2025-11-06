// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Linq;
using System.Text.Json.Nodes;
using Honua.Server.Core.Validation;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Host.GeoservicesREST;

internal static class GeoservicesRESTGeometryConverter
{
    private static readonly GeoJsonReader Reader = new();

    public static JsonObject? ToGeometry(JsonNode? geoJson, string geometryType, int targetWkid)
    {
        if (geoJson is null)
        {
            return null;
        }

        Geometry? geometry;
        try
        {
            geometry = Reader.Read<Geometry>(geoJson.ToJsonString());
        }
        catch
        {
            return null;
        }

        if (geometry is null || geometry.IsEmpty)
        {
            return null;
        }

        geometry = geometry switch
        {
            GeometryCollection collection when collection.NumGeometries > 0 => collection.GetGeometryN(0),
            _ => geometry
        };

        geometry = geometry.Factory.CreateGeometry(geometry);
        geometry.SRID = targetWkid;

        return geometryType switch
        {
            "esriGeometryPoint" => CreatePoint(geometry, targetWkid),
            "esriGeometryMultipoint" => CreateMultipoint(geometry, targetWkid),
            "esriGeometryPolyline" => CreatePolyline(geometry, targetWkid),
            _ => CreatePolygon(geometry, targetWkid)
        };
    }

    private static JsonObject CreatePoint(Geometry geometry, int wkid)
    {
        var point = geometry as Point ?? geometry.Factory.CreatePoint(geometry.Coordinate);
        var obj = new JsonObject
        {
            ["x"] = point.X,
            ["y"] = point.Y,
            ["spatialReference"] = CreateSpatialReference(wkid)
        };

        if (!double.IsNaN(point.Z) && !double.IsInfinity(point.Z))
        {
            obj["z"] = point.Z;
        }

        return obj;
    }

    private static JsonObject? CreateMultipoint(Geometry geometry, int wkid)
    {
        Coordinate[] coordinates = geometry switch
        {
            MultiPoint multi => multi.Geometries.Cast<Point>().Select(point => point.Coordinate).ToArray(),
            Point single => new[] { single.Coordinate },
            _ => Array.Empty<Coordinate>()
        };

        if (coordinates.Length == 0)
        {
            return null;
        }

        var points = new JsonArray();
        foreach (var coordinate in coordinates)
        {
            points.Add(CreateCoordinateArray(coordinate));
        }

        return new JsonObject
        {
            ["points"] = points,
            ["spatialReference"] = CreateSpatialReference(wkid)
        };
    }

    private static JsonObject? CreatePolyline(Geometry geometry, int wkid)
    {
        var paths = new JsonArray();
        switch (geometry)
        {
            case LineString line:
                paths.Add(CreatePath(line));
                break;
            case MultiLineString multi:
                foreach (LineString line in multi.Geometries)
                {
                    paths.Add(CreatePath(line));
                }
                break;
            case Polygon polygon:
                paths.Add(CreatePath((LineString)polygon.ExteriorRing));
                foreach (var interior in polygon.InteriorRings)
                {
                    paths.Add(CreatePath((LineString)interior));
                }
                break;
            default:
                return null;
        }

        return new JsonObject
        {
            ["paths"] = paths,
            ["spatialReference"] = CreateSpatialReference(wkid)
        };
    }

    private static JsonObject? CreatePolygon(Geometry geometry, int wkid)
    {
        var rings = new JsonArray();
        switch (geometry)
        {
            case Polygon polygon:
                // Validate polygon before serialization
                var validationResult = GeometryValidator.ValidatePolygon(polygon);
                if (!validationResult.IsValid)
                {
                    throw new InvalidOperationException($"Cannot serialize invalid polygon: {validationResult.ErrorMessage}");
                }

                // Ensure correct GeoServices ring orientation (CW exterior, CCW holes)
                var esriPolygon = GeometryValidator.EnsureEsriOrientation(polygon);
                AddPolygonRings(rings, esriPolygon);
                break;
            case MultiPolygon multi:
                // Validate multi-polygon before serialization
                var multiValidationResult = GeometryValidator.ValidateMultiPolygon(multi);
                if (!multiValidationResult.IsValid)
                {
                    throw new InvalidOperationException($"Cannot serialize invalid multi-polygon: {multiValidationResult.ErrorMessage}");
                }

                foreach (Polygon poly in multi.Geometries)
                {
                    // Ensure correct GeoServices ring orientation for each polygon
                    var esriPoly = GeometryValidator.EnsureEsriOrientation(poly);
                    AddPolygonRings(rings, esriPoly);
                }
                break;
            default:
                return null;
        }

        if (rings.Count == 0)
        {
            return null;
        }

        return new JsonObject
        {
            ["rings"] = rings,
            ["spatialReference"] = CreateSpatialReference(wkid)
        };
    }

    private static void AddPolygonRings(JsonArray target, Polygon polygon)
    {
        target.Add(CreatePath((LineString)polygon.ExteriorRing));
        foreach (var interior in polygon.InteriorRings)
        {
            target.Add(CreatePath((LineString)interior));
        }
    }

    private static JsonArray CreatePath(LineString line)
    {
        var path = new JsonArray();
        var sequence = line.CoordinateSequence;
        for (var i = 0; i < sequence.Count; i++)
        {
            path.Add(CreateCoordinateArray(sequence.GetCoordinate(i)));
        }

        if (sequence.Count > 0)
        {
            var first = sequence.GetCoordinate(0);
            var last = sequence.GetCoordinate(sequence.Count - 1);
            if (!first.Equals2D(last))
            {
                path.Add(CreateCoordinateArray(first));
            }
        }

        return path;
    }

    private static JsonArray CreateCoordinateArray(Coordinate coordinate)
    {
        var array = new JsonArray
        {
            coordinate.X,
            coordinate.Y
        };

        if (!double.IsNaN(coordinate.Z) && !double.IsInfinity(coordinate.Z))
        {
            array.Add(coordinate.Z);
        }

        return array;
    }

    private static JsonObject CreateSpatialReference(int wkid)
    {
        return new JsonObject
        {
            ["wkid"] = wkid
        };
    }
}
