// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Core.Data;

/// <summary>
/// Provides common utilities for reading and converting geometry data from database result sets.
/// Consolidates geometry reading, WKB/WKT parsing, and coordinate transformation logic.
/// </summary>
public static class GeometryReader
{
    private static readonly WKTReader WktReader = new();
    private static readonly WKBReader WkbReader = new();
    private static readonly GeoJsonWriter GeoJsonWriter = new();
    private static readonly GeoJsonReader GeoJsonReaderInstance = new();

    /// <summary>
    /// Reads geometry from a data reader column at the specified ordinal.
    /// Automatically detects the format (WKB bytes, WKT string, or GeoJSON string).
    /// </summary>
    /// <param name="reader">The data reader</param>
    /// <param name="ordinal">The zero-based column ordinal containing geometry data</param>
    /// <param name="storageSrid">The SRID of the geometry as stored in the database</param>
    /// <param name="targetSrid">The target SRID for coordinate transformation</param>
    /// <returns>A JsonNode representing the geometry in GeoJSON format, or null if no geometry</returns>
    public static JsonNode? ReadGeometry(
        IDataReader reader,
        int ordinal,
        int storageSrid = CrsHelper.Wgs84,
        int targetSrid = CrsHelper.Wgs84)
    {
        ArgumentNullException.ThrowIfNull(reader);

        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetValue(ordinal);

        return value switch
        {
            byte[] wkb => ReadWkbGeometry(wkb, storageSrid, targetSrid),
            string text => ReadTextGeometry(text, storageSrid, targetSrid),
            _ => null
        };
    }

    /// <summary>
    /// Reads geometry from a WKB (Well-Known Binary) byte array.
    /// </summary>
    /// <param name="wkb">The WKB byte array</param>
    /// <param name="storageSrid">The SRID of the geometry as stored in the database</param>
    /// <param name="targetSrid">The target SRID for coordinate transformation</param>
    /// <returns>A JsonNode representing the geometry in GeoJSON format, or null if parsing fails</returns>
    public static JsonNode? ReadWkbGeometry(
        byte[] wkb,
        int storageSrid = CrsHelper.Wgs84,
        int targetSrid = CrsHelper.Wgs84)
    {
        if (wkb == null || wkb.Length == 0)
        {
            return null;
        }

        try
        {
            var geometry = WkbReader.Read(wkb);
            geometry.SRID = storageSrid;

            return TransformAndSerializeGeometry(geometry, storageSrid, targetSrid);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Reads geometry from a WKT (Well-Known Text) string.
    /// </summary>
    /// <param name="wkt">The WKT string</param>
    /// <param name="storageSrid">The SRID of the geometry as stored in the database</param>
    /// <param name="targetSrid">The target SRID for coordinate transformation</param>
    /// <returns>A JsonNode representing the geometry in GeoJSON format, or null if parsing fails</returns>
    public static JsonNode? ReadWktGeometry(
        string? wkt,
        int storageSrid = CrsHelper.Wgs84,
        int targetSrid = CrsHelper.Wgs84)
    {
        if (string.IsNullOrWhiteSpace(wkt))
        {
            return null;
        }

        try
        {
            var geometry = WktReader.Read(wkt);
            geometry.SRID = storageSrid;

            return TransformAndSerializeGeometry(geometry, storageSrid, targetSrid);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Reads geometry from a text string that may be WKT or GeoJSON.
    /// Automatically detects the format based on the content.
    /// </summary>
    /// <param name="text">The text string containing geometry data</param>
    /// <param name="storageSrid">The SRID of the geometry as stored in the database</param>
    /// <param name="targetSrid">The target SRID for coordinate transformation</param>
    /// <returns>A JsonNode representing the geometry in GeoJSON format, or null if parsing fails</returns>
    public static JsonNode? ReadTextGeometry(
        string? text,
        int storageSrid = CrsHelper.Wgs84,
        int targetSrid = CrsHelper.Wgs84)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        // Check if the text looks like JSON (GeoJSON)
        if (FeatureRecordNormalizer.LooksLikeJson(text))
        {
            return ReadGeoJsonGeometry(text, storageSrid, targetSrid);
        }

        // Otherwise treat as WKT
        return ReadWktGeometry(text, storageSrid, targetSrid);
    }

    /// <summary>
    /// Reads geometry from a GeoJSON string.
    /// </summary>
    /// <param name="geoJson">The GeoJSON string</param>
    /// <param name="storageSrid">The SRID of the geometry as stored in the database</param>
    /// <param name="targetSrid">The target SRID for coordinate transformation</param>
    /// <returns>A JsonNode representing the geometry in GeoJSON format, or null if parsing fails</returns>
    public static JsonNode? ReadGeoJsonGeometry(
        string? geoJson,
        int storageSrid = CrsHelper.Wgs84,
        int targetSrid = CrsHelper.Wgs84)
    {
        if (string.IsNullOrWhiteSpace(geoJson))
        {
            return null;
        }

        try
        {
            var geometry = GeoJsonReaderInstance.Read<Geometry>(geoJson);
            geometry.SRID = storageSrid;

            // If no transformation needed and input is already valid JSON, return parsed node
            if (storageSrid == targetSrid)
            {
                return JsonNode.Parse(geoJson);
            }

            return TransformAndSerializeGeometry(geometry, storageSrid, targetSrid);
        }
        catch
        {
            // Fallback: try to parse as JSON even if geometry parsing failed
            try
            {
                return JsonNode.Parse(geoJson);
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Transforms a geometry from one SRID to another and serializes to GeoJSON.
    /// </summary>
    /// <param name="geometry">The geometry to transform</param>
    /// <param name="sourceSrid">The source SRID</param>
    /// <param name="targetSrid">The target SRID</param>
    /// <returns>A JsonNode representing the transformed geometry in GeoJSON format</returns>
    public static JsonNode? TransformAndSerializeGeometry(
        Geometry? geometry,
        int sourceSrid,
        int targetSrid)
    {
        if (geometry is null)
        {
            return null;
        }

        try
        {
            // Apply coordinate transformation if needed
            var transformed = CrsTransform.TransformGeometry(geometry, sourceSrid, targetSrid);

            // Serialize to GeoJSON
            var json = GeoJsonWriter.Write(transformed);
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            // Fallback to WKT if GeoJSON serialization fails
            return JsonValue.Create(geometry.ToText());
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Attempts to find the ordinal (column index) of a geometry field in the reader.
    /// </summary>
    /// <param name="reader">The data reader</param>
    /// <param name="geometryFieldName">The name of the geometry field to find</param>
    /// <returns>The ordinal of the geometry field, or -1 if not found</returns>
    public static int TryGetGeometryOrdinal(IDataReader reader, string? geometryFieldName)
    {
        return FeatureRecordReader.TryGetGeometryOrdinal(reader, geometryFieldName);
    }

    /// <summary>
    /// Parses a geometry string (WKT or GeoJSON) into a NetTopologySuite Geometry object.
    /// </summary>
    /// <param name="text">The text to parse</param>
    /// <returns>A Geometry object, or null if parsing fails</returns>
    public static Geometry? ParseGeometry(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return FeatureRecordNormalizer.LooksLikeJson(text)
                ? GeoJsonReaderInstance.Read<Geometry>(text)
                : WktReader.Read(text);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Converts a geometry to a GeoJSON JsonNode without coordinate transformation.
    /// </summary>
    /// <param name="geometry">The geometry to serialize</param>
    /// <returns>A JsonNode representing the geometry in GeoJSON format, or null if serialization fails</returns>
    public static JsonNode? SerializeGeometry(Geometry? geometry)
    {
        if (geometry is null)
        {
            return null;
        }

        try
        {
            var json = GeoJsonWriter.Write(geometry);
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return JsonValue.Create(geometry.ToText());
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Checks if a text string looks like it could be GeoJSON.
    /// </summary>
    /// <param name="text">The text to check</param>
    /// <returns>True if the text appears to be JSON-formatted</returns>
    public static bool LooksLikeJson(string? text)
    {
        return FeatureRecordNormalizer.LooksLikeJson(text);
    }

    /// <summary>
    /// Creates a GeoJSON reader instance.
    /// Useful for providers that need to customize geometry reading.
    /// </summary>
    /// <returns>A new GeoJsonReader instance</returns>
    public static GeoJsonReader CreateGeoJsonReader()
    {
        return new GeoJsonReader();
    }

    /// <summary>
    /// Creates a WKT reader instance.
    /// Useful for providers that need to customize geometry reading.
    /// </summary>
    /// <returns>A new WKTReader instance</returns>
    public static WKTReader CreateWktReader()
    {
        return new WKTReader();
    }

    /// <summary>
    /// Creates a WKB reader instance.
    /// Useful for providers that need to customize geometry reading.
    /// </summary>
    /// <returns>A new WKBReader instance</returns>
    public static WKBReader CreateWkbReader()
    {
        return new WKBReader();
    }

    /// <summary>
    /// Attempts to parse a bounding box extent from a GeoJSON geometry string.
    /// Useful for parsing ST_Extent results that return a polygon envelope.
    /// </summary>
    /// <param name="geoJson">The GeoJSON string representing the extent polygon</param>
    /// <param name="crs">The CRS identifier for the bounding box</param>
    /// <returns>A BoundingBox if parsing succeeds, otherwise null</returns>
    public static BoundingBox? TryParseExtentFromGeoJson(string? geoJson, string? crs)
    {
        if (string.IsNullOrWhiteSpace(geoJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(geoJson);
            var root = document.RootElement;

            if (!root.TryGetProperty("coordinates", out var coordinatesElement))
            {
                return null;
            }

            if (coordinatesElement.ValueKind != JsonValueKind.Array || coordinatesElement.GetArrayLength() == 0)
            {
                return null;
            }

            var ring = coordinatesElement.EnumerateArray().FirstOrDefault();
            if (ring.ValueKind != JsonValueKind.Array || ring.GetArrayLength() == 0)
            {
                return null;
            }

            double minX = double.PositiveInfinity;
            double minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity;
            double maxY = double.NegativeInfinity;

            foreach (var coordinate in ring.EnumerateArray())
            {
                if (coordinate.ValueKind != JsonValueKind.Array || coordinate.GetArrayLength() < 2)
                {
                    continue;
                }

                var x = coordinate[0].GetDouble();
                var y = coordinate[1].GetDouble();

                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }

            if (double.IsInfinity(minX) || double.IsInfinity(minY) || double.IsInfinity(maxX) || double.IsInfinity(maxY))
            {
                return null;
            }

            return new BoundingBox(minX, minY, maxX, maxY, Crs: crs);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
