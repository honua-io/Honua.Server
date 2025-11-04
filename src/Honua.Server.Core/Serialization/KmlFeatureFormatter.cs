// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Styling;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using SharpKml.Base;
using SharpKml.Dom;
using SharpKml.Engine;

using KmlInnerBoundary = SharpKml.Dom.InnerBoundary;
using KmlLinearRing = SharpKml.Dom.LinearRing;
using KmlLineString = SharpKml.Dom.LineString;
using KmlOuterBoundary = SharpKml.Dom.OuterBoundary;
using KmlPoint = SharpKml.Dom.Point;
using KmlPolygon = SharpKml.Dom.Polygon;
using NtsCoordinateSequence = NetTopologySuite.Geometries.CoordinateSequence;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;
using NtsGeometryCollection = NetTopologySuite.Geometries.GeometryCollection;
using NtsLinearRing = NetTopologySuite.Geometries.LinearRing;
using NtsLineString = NetTopologySuite.Geometries.LineString;
using NtsMultiLineString = NetTopologySuite.Geometries.MultiLineString;
using NtsMultiPoint = NetTopologySuite.Geometries.MultiPoint;
using NtsMultiPolygon = NetTopologySuite.Geometries.MultiPolygon;
using NtsPoint = NetTopologySuite.Geometries.Point;
using NtsPolygon = NetTopologySuite.Geometries.Polygon;
using Honua.Server.Core.Extensions;
namespace Honua.Server.Core.Serialization;

public static class KmlFeatureFormatter
{
    private static readonly GeoJsonReader GeoJsonReader = new();

    public static string WriteFeatureCollection(
        string collectionId,
        LayerDefinition layer,
        IEnumerable<KmlFeatureContent> features,
        long numberMatched,
        long numberReturned,
        StyleDefinition? style = null)
    {
        Guard.NotNull(layer);
        Guard.NotNull(features);

        var document = new Document
        {
            Name = layer.Title.IsNullOrWhiteSpace() ? collectionId : layer.Title,
            Open = true
        };

        if (layer.Description.HasValue())
        {
            document.Description = new Description { Text = layer.Description };
        }

        var extendedData = new ExtendedData();
        AddDataValue(extendedData, "collectionId", collectionId);
        AddDataValue(extendedData, "numberMatched", numberMatched.ToString(CultureInfo.InvariantCulture));
        AddDataValue(extendedData, "numberReturned", numberReturned.ToString(CultureInfo.InvariantCulture));
        document.ExtendedData = extendedData;

        string? styleUrl = null;
        if (style is not null)
        {
            var kmlStyle = StyleFormatConverter.CreateKmlStyle(style, BuildStyleId(style.Id), layer.GeometryType ?? style.GeometryType);
            if (kmlStyle is not null)
            {
                document.AddStyle(kmlStyle);
                styleUrl = "#" + kmlStyle.Id;
            }
        }

        foreach (var feature in features)
        {
            document.AddFeature(CreatePlacemark(layer, feature, styleUrl));
        }

        var kml = new Kml { Feature = document };
        return SerializeKml(kml);
    }

    public static string WriteSingleFeature(
        string collectionId,
        LayerDefinition layer,
        KmlFeatureContent feature,
        StyleDefinition? style = null)
    {
        Guard.NotNull(layer);
        Guard.NotNull(feature);

        Style? inlineStyle = null;
        string? styleUrl = null;
        if (style is not null)
        {
            inlineStyle = StyleFormatConverter.CreateKmlStyle(style, BuildStyleId(style.Id), layer.GeometryType ?? style.GeometryType);
            if (inlineStyle is not null)
            {
                styleUrl = "#" + inlineStyle.Id;
            }
        }

        var placemark = CreatePlacemark(layer, feature, styleUrl);
        if (inlineStyle is not null)
        {
            placemark.AddStyle(inlineStyle);
        }

        var kml = new Kml { Feature = placemark };
        return SerializeKml(kml);
    }

    private static Placemark CreatePlacemark(LayerDefinition layer, KmlFeatureContent feature, string? styleUrl)
    {
        var placemark = new Placemark
        {
            Id = BuildXmlId(feature.Id),
            Name = feature.Name.IsNullOrWhiteSpace() ? feature.Id : feature.Name
        };

        if (styleUrl.HasValue())
        {
            placemark.StyleUrl = new Uri(styleUrl, UriKind.Relative);
        }

        if (layer.Description.HasValue())
        {
            placemark.Description = new Description { Text = layer.Description };
        }

        var geometry = ConvertGeometry(feature.Geometry);
        if (geometry is not null)
        {
            placemark.Geometry = geometry;
        }

        if (feature.Properties.Count > 0)
        {
            var extendedData = new ExtendedData();
            foreach (var pair in feature.Properties.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (pair.Key.IsNullOrWhiteSpace())
                {
                    continue;
                }

                var value = FormatValue(pair.Value);
                if (value is null)
                {
                    continue;
                }

                AddDataValue(extendedData, pair.Key, value);
            }

            if (extendedData.Data.Count > 0)
            {
                placemark.ExtendedData = extendedData;
            }
        }

        return placemark;
    }

    private static string BuildStyleId(string rawId)
    {
        var builder = new StringBuilder();
        foreach (var ch in rawId ?? string.Empty)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? ch : '-');
        }

        return builder.Length > 0 ? builder.ToString() : "default-style";
    }

    private static string SerializeKml(Kml kml)
    {
        var file = KmlFile.Create(kml, false);
        using var stream = new MemoryStream();
        file.Save(stream);
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string? BuildXmlId(string? id)
    {
        if (id.IsNullOrWhiteSpace())
        {
            return null;
        }

        var sanitized = new StringBuilder();
        foreach (var ch in id)
        {
            sanitized.Append(char.IsLetterOrDigit(ch) ? ch : '-');
        }

        if (sanitized.Length == 0)
        {
            return null;
        }

        if (!char.IsLetter(sanitized[0]) && sanitized[0] != '_')
        {
            sanitized.Insert(0, 'f');
        }

        return sanitized.ToString();
    }

    private static void AddDataValue(ExtendedData extendedData, string name, string value)
    {
        extendedData.AddData(new SharpKml.Dom.Data
        {
            Name = name,
            Value = value
        });
    }

    private static string? FormatValue(object? value)
    {
        return value switch
        {
            null => null,
            string s when s.IsNullOrWhiteSpace() => null,
            string s => s,
            bool b => b ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            JsonNode node => node.ToJsonString(),
            _ => value?.ToString()
        };
    }

    private static SharpKml.Dom.Geometry? ConvertGeometry(JsonNode? geometryNode)
    {
        if (geometryNode is null)
        {
            return null;
        }

        var json = geometryNode.ToJsonString();
        if (json.IsNullOrWhiteSpace())
        {
            return null;
        }

        NtsGeometry ntsGeometry;
        try
        {
            ntsGeometry = GeoJsonReader.Read<NtsGeometry>(json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Unable to parse GeoJSON geometry for KML output.", ex);
        }

        if (ntsGeometry.IsEmpty)
        {
            return null;
        }

        return ConvertGeometry(ntsGeometry);
    }

    private static SharpKml.Dom.Geometry? ConvertGeometry(NtsGeometry geometry)
    {
        return geometry switch
        {
            NtsPoint point => ConvertPoint(point),
            NtsLineString lineString => ConvertLineString(lineString),
            NtsPolygon polygon => ConvertPolygon(polygon),
            NtsMultiPoint multiPoint => ConvertGeometryCollection(multiPoint),
            NtsMultiLineString multiLineString => ConvertGeometryCollection(multiLineString),
            NtsMultiPolygon multiPolygon => ConvertGeometryCollection(multiPolygon),
            NtsGeometryCollection collection => ConvertGeometryCollection(collection),
            _ => null
        };
    }

    private static SharpKml.Dom.Geometry? ConvertGeometryCollection(NtsGeometryCollection collection)
    {
        if (collection.NumGeometries == 0)
        {
            return null;
        }

        var multi = new MultipleGeometry();
        var hasGeometry = false;
        for (var i = 0; i < collection.NumGeometries; i++)
        {
            var converted = ConvertGeometry(collection.GetGeometryN(i));
            if (converted is not null)
            {
                multi.AddGeometry(converted);
                hasGeometry = true;
            }
        }

        return hasGeometry ? multi : null;
    }

    private static KmlPoint ConvertPoint(NtsPoint point)
    {
        return new KmlPoint
        {
            Coordinate = ToVector(point.CoordinateSequence, 0),
            AltitudeMode = AltitudeMode.Absolute
        };
    }

    private static KmlLineString ConvertLineString(NtsLineString lineString)
    {
        return new KmlLineString
        {
            Coordinates = ToCoordinateCollection(lineString.CoordinateSequence),
            AltitudeMode = AltitudeMode.Absolute
        };
    }

    private static KmlPolygon ConvertPolygon(NtsPolygon polygon)
    {
        var poly = new KmlPolygon
        {
            AltitudeMode = AltitudeMode.Absolute,
            OuterBoundary = new KmlOuterBoundary
            {
                LinearRing = new KmlLinearRing
                {
                    Coordinates = ToCoordinateCollection(polygon.Shell.CoordinateSequence, ensureRing: true, outerRing: true)
                }
            }
        };

        for (var i = 0; i < polygon.NumInteriorRings; i++)
        {
            var ring = (NtsLinearRing)polygon.GetInteriorRingN(i);
            poly.AddInnerBoundary(new KmlInnerBoundary
            {
                LinearRing = new KmlLinearRing
                {
                    Coordinates = ToCoordinateCollection(ring.CoordinateSequence, ensureRing: true, outerRing: false)
                }
            });
        }

        return poly;
    }

    private static CoordinateCollection ToCoordinateCollection(NtsCoordinateSequence sequence, bool ensureRing = false, bool outerRing = true)
    {
        var working = sequence;
        if (ensureRing && CoordinateSequences.IsRing(working))
        {
            var isCcw = NetTopologySuite.Algorithm.Orientation.IsCCW(working);
            var shouldBeCcw = outerRing;
            if ((shouldBeCcw && !isCcw) || (!shouldBeCcw && isCcw))
            {
                working = working.Copy();
                CoordinateSequences.Reverse(working);
            }
        }

        var collection = new CoordinateCollection();
        for (var i = 0; i < working.Count; i++)
        {
            collection.Add(ToVector(working, i));
        }

        return collection;
    }

    private static Vector ToVector(NtsCoordinateSequence sequence, int index)
    {
        var longitude = sequence.GetX(index);
        var latitude = sequence.GetY(index);
        var hasZ = sequence.Dimension >= 3;
        if (hasZ)
        {
            var altitude = sequence.GetOrdinate(index, Ordinate.Z);
            return new Vector(latitude, longitude, altitude);
        }

        return new Vector(latitude, longitude);
    }
}

public sealed record KmlFeatureContent(string? Id, string? Name, JsonNode? Geometry, IReadOnlyDictionary<string, object?> Properties);















