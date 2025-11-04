// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Globalization;
using System.Text.Json;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Honua.Server.Host.GeoservicesREST.Services;

/// <summary>
/// Resolves spatial filter and coordinate reference system parameters for Geoservices REST API requests.
/// </summary>
internal static class GeoservicesSpatialResolver
{
    public static int ResolveTargetWkid(HttpRequest request, ServiceDefinition service, LayerDefinition layer)
    {
        var query = request.Query;
        if (query.TryGetValue("outSR", out var values) && values.Count > 0)
        {
            return ParseSpatialReference(values[^1]!);
        }

        if (request.Headers.TryGetValue("Accept-Crs", out var acceptValues))
        {
            var candidate = ParseAcceptCrsHeader(acceptValues);
            if (candidate.HasValue())
            {
                return ParseSpatialReference(candidate!);
            }
        }

        if (service.Ogc.DefaultCrs.HasValue())
        {
            return ParseSpatialReference(service.Ogc.DefaultCrs!);
        }

        if (layer.Storage?.Srid is int storage)
        {
            return storage;
        }

        if (layer.Crs.Count > 0)
        {
            return ParseSpatialReference(layer.Crs[0]);
        }

        return 4326;
    }

    public static BoundingBox? ResolveSpatialFilter(IQueryCollection query, ServiceDefinition service, LayerDefinition layer)
    {
        if (!query.TryGetValue("geometry", out var values) || values.Count == 0)
        {
            return null;
        }

        var geometryValue = values[^1];
        if (geometryValue.IsNullOrWhiteSpace())
        {
            return null;
        }

        var spatialRel = query.TryGetValue("spatialRel", out var spatialValues) && spatialValues.Count > 0
            ? spatialValues[^1]
            : "esriSpatialRelIntersects";

        if (spatialRel.IsNullOrWhiteSpace())
        {
            spatialRel = "esriSpatialRelIntersects";
        }

        EnsureSpatialRelationSupported(spatialRel);

        var geometryTypeRaw = query.TryGetValue("geometryType", out var typeValues) && typeValues.Count > 0
            ? typeValues[^1]
            : null;
        var geometryType = geometryTypeRaw.IsNullOrWhiteSpace() ? "esriGeometryEnvelope" : geometryTypeRaw;

        var sridHint = ResolveInitialSpatialReference(query, service, layer);

        SpatialFilter? filter = geometryType switch
        {
            var type when type.EqualsIgnoreCase("esriGeometryEnvelope") => ParseEnvelope(geometryValue, sridHint),
            var type when type.EqualsIgnoreCase("esriGeometryPoint") => ParsePointEnvelope(geometryValue, sridHint),
            var type when type.EqualsIgnoreCase("esriGeometryPolygon") => ParsePolygonEnvelope(geometryValue, sridHint),
            var type when type.EqualsIgnoreCase("esriGeometryPolyline") => ParsePolylineEnvelope(geometryValue, sridHint),
            var type when type.EqualsIgnoreCase("esriGeometryMultipoint") => ParseMultipointEnvelope(geometryValue, sridHint),
            _ => null
        };

        if (filter is null)
        {
            ThrowBadRequest($"Geometry type '{geometryType}' is not supported in this release.");
        }

        var resolvedFilter = filter!;
        return new BoundingBox(resolvedFilter.MinX, resolvedFilter.MinY, resolvedFilter.MaxX, resolvedFilter.MaxY, null, null, $"EPSG:{resolvedFilter.Srid}");
    }

    private static int ParseSpatialReference(string value)
    {
        var (srid, error) = QueryParameterHelper.ParseCrsToSrid(value);

        if (error is not null)
        {
            ThrowBadRequest($"Spatial reference: {error}");
        }

        return srid ?? 4326;
    }

    private static SpatialFilter ParseEnvelope(string geometry, int sridHint)
    {
        var trimmed = geometry.Trim();
        var srid = sridHint > 0 ? sridHint : 4326;

        if (trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            using var document = JsonDocument.Parse(trimmed);
            var root = document.RootElement;
            srid = ResolveSpatialReference(root, srid);

            var minX = ReadDoubleProperty(root, "xmin");
            var minY = ReadDoubleProperty(root, "ymin");
            var maxX = ReadDoubleProperty(root, "xmax");
            var maxY = ReadDoubleProperty(root, "ymax");

            return new SpatialFilter(
                Math.Min(minX, maxX),
                Math.Min(minY, maxY),
                Math.Max(minX, maxX),
                Math.Max(minY, maxY),
                srid);
        }

        var parts = QueryParsingHelpers.ParseCsv(trimmed);
        if (parts.Count < 4)
        {
            ThrowBadRequest("geometry must contain xmin,ymin,xmax,ymax values.");
        }

        var xmin = ParseDouble(parts[0], "geometry");
        var ymin = ParseDouble(parts[1], "geometry");
        var xmax = ParseDouble(parts[2], "geometry");
        var ymax = ParseDouble(parts[3], "geometry");

        return new SpatialFilter(
            Math.Min(xmin, xmax),
            Math.Min(ymin, ymax),
            Math.Max(xmin, xmax),
            Math.Max(ymin, ymax),
            srid);
    }

    private static SpatialFilter ParsePointEnvelope(string geometry, int sridHint)
    {
        var trimmed = geometry.Trim();
        var srid = sridHint > 0 ? sridHint : 4326;

        if (trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            using var document = JsonDocument.Parse(trimmed);
            var root = document.RootElement;
            srid = ResolveSpatialReference(root, srid);

            var x = ReadDoubleProperty(root, "x");
            var y = ReadDoubleProperty(root, "y");

            return new SpatialFilter(x, y, x, y, srid);
        }

        var parts = QueryParsingHelpers.ParseCsv(trimmed);
        if (parts.Count < 2)
        {
            ThrowBadRequest("geometry must contain x,y values.");
        }

        var xCoord = ParseDouble(parts[0], "geometry");
        var yCoord = ParseDouble(parts[1], "geometry");

        return new SpatialFilter(xCoord, yCoord, xCoord, yCoord, srid);
    }

    private static SpatialFilter ParsePolygonEnvelope(string geometry, int sridHint)
    {
        var trimmed = geometry.Trim();
        var srid = sridHint > 0 ? sridHint : 4326;

        if (!trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            ThrowBadRequest("Polygon geometry must be in JSON format with 'rings' array.");
        }

        using var document = JsonDocument.Parse(trimmed);
        var root = document.RootElement;
        srid = ResolveSpatialReference(root, srid);

        if (!root.TryGetProperty("rings", out var rings) || rings.ValueKind != JsonValueKind.Array)
        {
            ThrowBadRequest("Polygon geometry must contain a 'rings' array.");
        }

        return ComputeBoundingBoxFromCoordinateArrays(rings, "rings", srid);
    }

    private static SpatialFilter ParsePolylineEnvelope(string geometry, int sridHint)
    {
        var trimmed = geometry.Trim();
        var srid = sridHint > 0 ? sridHint : 4326;

        if (!trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            ThrowBadRequest("Polyline geometry must be in JSON format with 'paths' array.");
        }

        using var document = JsonDocument.Parse(trimmed);
        var root = document.RootElement;
        srid = ResolveSpatialReference(root, srid);

        if (!root.TryGetProperty("paths", out var paths) || paths.ValueKind != JsonValueKind.Array)
        {
            ThrowBadRequest("Polyline geometry must contain a 'paths' array.");
        }

        return ComputeBoundingBoxFromCoordinateArrays(paths, "paths", srid);
    }

    private static SpatialFilter ParseMultipointEnvelope(string geometry, int sridHint)
    {
        var trimmed = geometry.Trim();
        var srid = sridHint > 0 ? sridHint : 4326;

        if (!trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            ThrowBadRequest("Multipoint geometry must be in JSON format with 'points' array.");
        }

        using var document = JsonDocument.Parse(trimmed);
        var root = document.RootElement;
        srid = ResolveSpatialReference(root, srid);

        if (!root.TryGetProperty("points", out var points) || points.ValueKind != JsonValueKind.Array)
        {
            ThrowBadRequest("Multipoint geometry must contain a 'points' array.");
        }

        if (points.GetArrayLength() == 0)
        {
            ThrowBadRequest("Multipoint geometry must contain at least one point.");
        }

        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;

        foreach (var point in points.EnumerateArray())
        {
            if (point.ValueKind != JsonValueKind.Array || point.GetArrayLength() < 2)
            {
                ThrowBadRequest("Each point in multipoint geometry must be a coordinate pair [x, y].");
            }

            var coords = point.EnumerateArray().ToArray();
            double x, y;
            if (!coords[0].TryGetDouble(out x) || !coords[1].TryGetDouble(out y))
            {
                ThrowBadRequest("Point coordinates must be numeric.");
                continue; // Never reached, but helps compiler
            }

            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }

        return new SpatialFilter(minX, minY, maxX, maxY, srid);
    }

    private static SpatialFilter ComputeBoundingBoxFromCoordinateArrays(JsonElement arrayOfArrays, string arrayName, int srid)
    {
        if (arrayOfArrays.GetArrayLength() == 0)
        {
            ThrowBadRequest($"Geometry {arrayName} must contain at least one element.");
        }

        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;

        foreach (var subArray in arrayOfArrays.EnumerateArray())
        {
            if (subArray.ValueKind != JsonValueKind.Array)
            {
                ThrowBadRequest($"Each element in {arrayName} must be an array of coordinate pairs.");
            }

            foreach (var coordinate in subArray.EnumerateArray())
            {
                if (coordinate.ValueKind != JsonValueKind.Array || coordinate.GetArrayLength() < 2)
                {
                    ThrowBadRequest($"Each coordinate in {arrayName} must be a pair [x, y].");
                }

                var coords = coordinate.EnumerateArray().ToArray();
                double x, y;
                if (!coords[0].TryGetDouble(out x) || !coords[1].TryGetDouble(out y))
                {
                    ThrowBadRequest($"Coordinates in {arrayName} must be numeric.");
                    continue; // Never reached, but helps compiler
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        return new SpatialFilter(minX, minY, maxX, maxY, srid);
    }

    private static int ResolveSpatialReference(JsonElement element, int fallback)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("spatialReference", out var srElement))
        {
            var srid = TryParseSpatialReference(srElement);
            if (srid.HasValue)
            {
                return srid.Value;
            }
        }

        return fallback;
    }

    private static int ResolveInitialSpatialReference(IQueryCollection query, ServiceDefinition service, LayerDefinition layer)
    {
        if (query.TryGetValue("inSR", out var values) && values.Count > 0)
        {
            return ParseSpatialReference(values[^1]!);
        }

        if (layer.Storage?.Srid is int storage && storage > 0)
        {
            return storage;
        }

        if (service.Ogc.DefaultCrs.HasValue())
        {
            return ParseSpatialReference(service.Ogc.DefaultCrs!);
        }

        var firstLayerCrs = layer.Crs.FirstOrDefault(static crs => crs.HasValue());
        if (firstLayerCrs.HasValue())
        {
            return ParseSpatialReference(firstLayerCrs!);
        }

        return 4326;
    }

    private static int? TryParseSpatialReference(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (element.TryGetProperty("wkid", out var wkidElement) && wkidElement.TryGetInt32(out var wkid))
                {
                    return wkid;
                }

                if (element.TryGetProperty("latestWkid", out var latestElement) && latestElement.TryGetInt32(out var latest))
                {
                    return latest;
                }

                if (element.TryGetProperty("wkt", out var wktElement))
                {
                    return TryParseSpatialReference(wktElement.GetString());
                }

                break;
            case JsonValueKind.Number:
                if (element.TryGetInt32(out var numeric))
                {
                    return numeric;
                }

                break;
            case JsonValueKind.String:
                return TryParseSpatialReference(element.GetString());
        }

        return null;
    }

    private static int? TryParseSpatialReference(string? value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.StartsWithIgnoreCase("EPSG:"))
        {
            return int.TryParse(trimmed[5..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var wkid) ? wkid : null;
        }

        return int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric) ? numeric : null;
    }

    private static string? ParseAcceptCrsHeader(StringValues headerValues)
    {
        if (headerValues.Count == 0)
        {
            return null;
        }

        var bestValue = (Value: (string?)null, Weight: double.NegativeInfinity, Order: int.MaxValue);
        var order = 0;

        foreach (var header in headerValues)
        {
            if (header.IsNullOrWhiteSpace())
            {
                continue;
            }

            var entries = QueryParsingHelpers.ParseCsv(header);
            foreach (var entry in entries)
            {

                var segments = entry.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (segments.Length == 0)
                {
                    order++;
                    continue;
                }

                var value = segments[0];
                if (!value.IsNullOrEmpty() && value.Length > 1 && value[0] == '"' && value[^1] == '"')
                {
                    value = value[1..^1];
                }

                double weight = 1.0;
                for (var i = 1; i < segments.Length; i++)
                {
                    var part = segments[i];
                    if (part.StartsWithIgnoreCase("q=") &&
                        part.AsSpan(2).TryParseDouble(out var parsedWeight))
                    {
                        weight = Math.Clamp(parsedWeight, 0d, 1d);
                    }
                }

                if (weight <= 0d)
                {
                    order++;
                    continue;
                }

                if (string.Equals(value, "*", StringComparison.Ordinal))
                {
                    order++;
                    continue;
                }

                if (weight > bestValue.Weight || (Math.Abs(weight - bestValue.Weight) <= 1e-6 && order < bestValue.Order))
                {
                    bestValue = (value, weight, order);
                }

                order++;
            }
        }

        return bestValue.Value;
    }

    private static void EnsureSpatialRelationSupported(string spatialRel)
    {
        // Supported spatial relations as per ArcGIS REST API specification
        // Reference: https://developers.arcgis.com/rest/services-reference/query-feature-service-layer-.htm

        if (spatialRel.EqualsIgnoreCase("esriSpatialRelIntersects"))
        {
            return;
        }

        if (spatialRel.EqualsIgnoreCase("esriSpatialRelEnvelopeIntersects"))
        {
            return;
        }

        if (spatialRel.EqualsIgnoreCase("esriSpatialRelContains"))
        {
            return;
        }

        if (spatialRel.EqualsIgnoreCase("esriSpatialRelWithin"))
        {
            return;
        }

        if (spatialRel.EqualsIgnoreCase("esriSpatialRelTouches"))
        {
            return;
        }

        if (spatialRel.EqualsIgnoreCase("esriSpatialRelOverlaps"))
        {
            return;
        }

        if (spatialRel.EqualsIgnoreCase("esriSpatialRelCrosses"))
        {
            return;
        }

        if (spatialRel.EqualsIgnoreCase("esriSpatialRelRelation"))
        {
            // Custom DE-9IM relation string support - requires 'relationParam' parameter
            return;
        }

        ThrowBadRequest($"spatialRel '{spatialRel}' is not supported. Supported values are: esriSpatialRelIntersects, esriSpatialRelEnvelopeIntersects, esriSpatialRelContains, esriSpatialRelWithin, esriSpatialRelTouches, esriSpatialRelOverlaps, esriSpatialRelCrosses, esriSpatialRelRelation.");
    }

    private static double ReadDoubleProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            ThrowBadRequest($"geometry is missing '{propertyName}'.");
        }

        if (!property.TryGetDouble(out var value))
        {
            ThrowBadRequest($"geometry.{propertyName} must be numeric.");
        }

        return value;
    }

    private static double ParseDouble(string value, string parameter)
    {
        if (value.TryParseDouble(out var parsed))
        {
            return parsed;
        }

        ThrowBadRequest($"{parameter} contains an invalid numeric value ('{value}').");
        return 0;
    }

    private static void ThrowBadRequest(string message)
    {
        throw new GeoservicesRESTQueryException(message);
    }
}

internal sealed record SpatialFilter(double MinX, double MinY, double MaxX, double MaxY, int Srid);
