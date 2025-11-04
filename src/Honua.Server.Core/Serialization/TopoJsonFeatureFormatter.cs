// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Core.Serialization;

public static class TopoJsonFeatureFormatter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string WriteFeatureCollection(
        string collectionId,
        LayerDefinition layer,
        IReadOnlyList<TopoJsonFeatureContent> features,
        long numberMatched,
        long numberReturned)
    {
        if (string.IsNullOrWhiteSpace(collectionId))
        {
            throw new ArgumentException("Collection id is required.", nameof(collectionId));
        }

        Guard.NotNull(layer);
        Guard.NotNull(features);

        var builder = new TopologyBuilder(layer);
        foreach (var feature in features)
        {
            builder.AddFeature(feature);
        }

        return builder.Build(collectionId, numberMatched, numberReturned);
    }

    public static string WriteSingleFeature(
        string collectionId,
        LayerDefinition layer,
        TopoJsonFeatureContent feature)
    {
        if (string.IsNullOrWhiteSpace(collectionId))
        {
            throw new ArgumentException("Collection id is required.", nameof(collectionId));
        }

        Guard.NotNull(layer);
        Guard.NotNull(feature);

        var builder = new TopologyBuilder(layer);
        builder.AddFeature(feature);

        return builder.Build(collectionId, null, null);
    }

    private sealed class TopologyBuilder
    {
        private readonly LayerDefinition _layer;
        private readonly GeoJsonReader _reader;
        private readonly List<JsonObject> _geometries = new();
        private readonly List<JsonArray> _arcs = new();
        private readonly Envelope _envelope = new();

        public TopologyBuilder(LayerDefinition layer)
        {
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _reader = new GeoJsonReader();
        }

        public void AddFeature(TopoJsonFeatureContent feature)
        {
            Guard.NotNull(feature);

            var geometryObject = BuildGeometryObject(feature.Geometry);

            if (!string.IsNullOrWhiteSpace(feature.Id))
            {
                geometryObject["id"] = JsonValue.Create(feature.Id);
            }

            var properties = BuildProperties(feature.Properties, feature.Title);
            if (properties is not null)
            {
                geometryObject["properties"] = properties;
            }

            _geometries.Add(geometryObject);
        }

        public string Build(string collectionId, long? numberMatched, long? numberReturned)
        {
            if (string.IsNullOrWhiteSpace(collectionId))
            {
                throw new ArgumentException("Collection id is required.", nameof(collectionId));
            }

            var geometryArray = new JsonArray();
            foreach (var geometry in _geometries)
            {
                geometryArray.Add(geometry);
            }

            var arcsArray = new JsonArray();
            foreach (var arc in _arcs)
            {
                arcsArray.Add(arc);
            }

            var topologyObjects = new JsonObject
            {
                [collectionId] = new JsonObject
                {
                    ["type"] = "GeometryCollection",
                    ["geometries"] = geometryArray
                }
            };

            var topology = new JsonObject
            {
                ["type"] = "Topology",
                ["objects"] = topologyObjects,
                ["arcs"] = arcsArray
            };

            if (!_envelope.IsNull)
            {
                topology["bbox"] = new JsonArray(
                    JsonValue.Create(_envelope.MinX),
                    JsonValue.Create(_envelope.MinY),
                    JsonValue.Create(_envelope.MaxX),
                    JsonValue.Create(_envelope.MaxY));
            }

            var meta = new JsonObject();
            if (numberMatched.HasValue)
            {
                meta["numberMatched"] = JsonValue.Create(numberMatched.Value);
            }
            if (numberReturned.HasValue)
            {
                meta["numberReturned"] = JsonValue.Create(numberReturned.Value);
            }
            if (meta.Count > 0)
            {
                topology["meta"] = meta;
            }

            if (!string.IsNullOrWhiteSpace(_layer?.Title))
            {
                topology["title"] = JsonValue.Create(_layer.Title);
            }

            return topology.ToJsonString(SerializerOptions);
        }

        private JsonObject BuildGeometryObject(JsonNode? geometryNode)
        {
            if (geometryNode is null)
            {
                return CreateEmptyGeometry();
            }

            Geometry geometry;
            try
            {
                geometry = _reader.Read<Geometry>(geometryNode.ToJsonString());
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to parse feature geometry for TopoJSON conversion.", ex);
            }

            if (geometry is null || geometry.IsEmpty)
            {
                return CreateEmptyGeometry();
            }

            return BuildGeometry(geometry);
        }

        private JsonObject BuildGeometry(Geometry geometry)
        {
            return geometry switch
            {
                Point point => BuildPoint(point),
                MultiPoint multiPoint => BuildMultiPoint(multiPoint),
                LineString lineString => BuildLineString(lineString),
                MultiLineString multiLine => BuildMultiLineString(multiLine),
                Polygon polygon => BuildPolygon(polygon),
                MultiPolygon multiPolygon => BuildMultiPolygon(multiPolygon),
                GeometryCollection collection => BuildGeometryCollection(collection),
                _ => throw new InvalidOperationException($"Unsupported geometry type '{geometry.GeometryType}'.")
            };
        }

        private JsonObject BuildPoint(Point point)
        {
            var coordinates = CreateCoordinateArray(point.CoordinateSequence, 0);
            return new JsonObject
            {
                ["type"] = "Point",
                ["coordinates"] = coordinates
            };
        }

        private JsonObject BuildMultiPoint(MultiPoint multiPoint)
        {
            var coordinates = new JsonArray();
            for (var i = 0; i < multiPoint.NumGeometries; i++)
            {
                var point = (Point)multiPoint.GetGeometryN(i);
                coordinates.Add(CreateCoordinateArray(point.CoordinateSequence, 0));
            }

            return new JsonObject
            {
                ["type"] = "MultiPoint",
                ["coordinates"] = coordinates
            };
        }

        private JsonObject BuildLineString(LineString lineString)
        {
            var arcs = new JsonArray
            {
                JsonValue.Create(StoreArc(lineString))
            };

            return new JsonObject
            {
                ["type"] = "LineString",
                ["arcs"] = arcs
            };
        }

        private JsonObject BuildMultiLineString(MultiLineString multiLine)
        {
            var lines = new JsonArray();
            for (var i = 0; i < multiLine.NumGeometries; i++)
            {
                var component = (LineString)multiLine.GetGeometryN(i);
                if (component.IsEmpty)
                {
                    continue;
                }

                lines.Add(new JsonArray { JsonValue.Create(StoreArc(component)) });
            }

            return new JsonObject
            {
                ["type"] = "MultiLineString",
                ["arcs"] = lines
            };
        }

        private JsonObject BuildPolygon(Polygon polygon)
        {
            var polygonArcs = CreatePolygonArcs(polygon);
            return new JsonObject
            {
                ["type"] = "Polygon",
                ["arcs"] = polygonArcs
            };
        }

        private JsonObject BuildMultiPolygon(MultiPolygon multiPolygon)
        {
            var polygons = new JsonArray();
            for (var i = 0; i < multiPolygon.NumGeometries; i++)
            {
                var polygon = (Polygon)multiPolygon.GetGeometryN(i);
                if (polygon.IsEmpty)
                {
                    continue;
                }

                polygons.Add(CreatePolygonArcs(polygon));
            }

            return new JsonObject
            {
                ["type"] = "MultiPolygon",
                ["arcs"] = polygons
            };
        }

        private JsonObject BuildGeometryCollection(GeometryCollection collection)
        {
            var geometries = new JsonArray();
            for (var i = 0; i < collection.NumGeometries; i++)
            {
                var component = collection.GetGeometryN(i);
                geometries.Add(component.IsEmpty ? CreateEmptyGeometry() : BuildGeometry(component));
            }

            return new JsonObject
            {
                ["type"] = "GeometryCollection",
                ["geometries"] = geometries
            };
        }

        private JsonArray CreatePolygonArcs(Polygon polygon)
        {
            var polygonArcs = new JsonArray();
            if (!polygon.ExteriorRing.IsEmpty)
            {
                // Exterior ring (shell) uses positive arc index
                polygonArcs.Add(CreateRingArcs((LineString)polygon.ExteriorRing, isHole: false));
            }

            for (var i = 0; i < polygon.NumInteriorRings; i++)
            {
                var hole = (LineString)polygon.GetInteriorRingN(i);
                if (hole.IsEmpty)
                {
                    continue;
                }

                // Interior ring (hole) uses negative arc index per TopoJSON spec
                polygonArcs.Add(CreateRingArcs(hole, isHole: true));
            }

            return polygonArcs;
        }

        /// <summary>
        /// Creates arc references for a polygon ring (exterior shell or interior hole).
        /// Per TopoJSON specification (https://github.com/topojson/topojson-specification):
        /// - Exterior rings (shells) use positive arc indices
        /// - Interior rings (holes) use negative arc indices to indicate reversed orientation
        /// The negative index is calculated as -(arcIndex + 1) because arc 0 would be ambiguous.
        /// </summary>
        private JsonArray CreateRingArcs(LineString ring, bool isHole)
        {
            var arcIndex = StoreArc(ring);

            // TopoJSON spec: holes must use negative indices to indicate reversed arcs
            var arcReference = isHole ? -(arcIndex + 1) : arcIndex;

            return new JsonArray
            {
                JsonValue.Create(arcReference)
            };
        }

        private int StoreArc(LineString lineString)
        {
            var sequence = lineString.CoordinateSequence;
            if (sequence.Count < 2)
            {
                throw new InvalidOperationException("Cannot convert a line geometry with fewer than two coordinates to TopoJSON arcs.");
            }

            var arc = new JsonArray();
            for (var i = 0; i < sequence.Count; i++)
            {
                arc.Add(CreateCoordinateArray(sequence, i));
            }

            _arcs.Add(arc);
            return _arcs.Count - 1;
        }

        private JsonArray CreateCoordinateArray(CoordinateSequence sequence, int index)
        {
            var array = new JsonArray();

            var x = sequence.GetX(index);
            var y = sequence.GetY(index);
            ExpandEnvelope(x, y);

            array.Add(JsonValue.Create(x));
            array.Add(JsonValue.Create(y));

            for (var ordinate = 2; ordinate < sequence.Dimension; ordinate++)
            {
                var value = sequence.GetOrdinate(index, ordinate);
                if (double.IsNaN(value))
                {
                    continue;
                }

                array.Add(JsonValue.Create(value));
            }

            return array;
        }

        private void ExpandEnvelope(double x, double y)
        {
            _envelope.ExpandToInclude(x, y);
        }

        private static JsonObject CreateEmptyGeometry()
        {
            return new JsonObject
            {
                ["type"] = "GeometryCollection",
                ["geometries"] = new JsonArray()
            };
        }

        private static JsonObject? BuildProperties(IReadOnlyDictionary<string, object?> properties, string? title)
        {
            JsonObject? result = null;

            if (properties.Count > 0)
            {
                result = new JsonObject();
                foreach (var pair in properties)
                {
                    result[pair.Key] = ConvertToNode(pair.Value);
                }
            }

            if (!string.IsNullOrWhiteSpace(title))
            {
                result ??= new JsonObject();
                if (!result.ContainsKey("title"))
                {
                    result["title"] = JsonValue.Create(title);
                }
            }

            return result is { Count: > 0 } ? result : null;
        }

        private static JsonNode? ConvertToNode(object? value)
        {
            if (value is null || value is DBNull)
            {
                return null;
            }

            if (value is JsonNode node)
            {
                return node.DeepClone();
            }

            if (value is JsonElement element)
            {
                return JsonNode.Parse(element.GetRawText());
            }

            return JsonSerializer.SerializeToNode(value, value.GetType(), SerializerOptions);
        }
    }
}

public sealed record TopoJsonFeatureContent(
    string? Id,
    string? Title,
    JsonNode? Geometry,
    IReadOnlyDictionary<string, object?> Properties);
