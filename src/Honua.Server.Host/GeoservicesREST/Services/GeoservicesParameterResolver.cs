// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Globalization;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Query;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.GeoservicesREST.Services;

/// <summary>
/// Resolves basic query parameters for Geoservices REST API requests.
/// </summary>
internal static class GeoservicesParameterResolver
{
    // Fallback defaults if configuration is not available
    private const string FallbackDefaultFormat = "json";
    private const int FallbackDefaultMaxRecordCount = 1000;

    public static (GeoservicesResponseFormat Format, bool PrettyPrint) ResolveFormat(
        IQueryCollection query)
    {
        var defaultFormat = FallbackDefaultFormat;
        var format = query.TryGetValue("f", out var formatValues) ? formatValues[^1] : defaultFormat;
        return NormalizeFormat(format);
    }

    public static int? ResolveLimit(
        IQueryCollection query,
        CatalogServiceView serviceView,
        CatalogLayerView layerView)
    {
        var layerLimit = layerView.Layer.Query.MaxRecordCount;
        var serviceLimit = serviceView.Service.Ogc.ItemLimit;
        var configDefaultLimit = (int?)null;

        var rawValue = query.TryGetValue("resultRecordCount", out var limitValues) && limitValues.Count > 0
            ? limitValues[^1]
            : null;

        var (limit, error) = QueryParameterHelper.ParseLimit(
            rawValue,
            serviceLimit,
            layerLimit,
            configDefaultLimit);

        if (error is not null)
        {
            ThrowBadRequest(error);
        }

        return limit;
    }

    public static int? ResolveOffset(IQueryCollection query)
    {
        var rawValue = query.TryGetValue("resultOffset", out var values) && values.Count > 0
            ? values[^1]
            : null;

        var (offset, error) = QueryParameterHelper.ParseOffset(rawValue);

        if (error is not null)
        {
            ThrowBadRequest(error);
        }

        return offset;
    }

    public static bool ResolveBoolean(IQueryCollection query, string key, bool defaultValue)
    {
        var rawValue = query.TryGetValue(key, out var values) && values.Count > 0
            ? values[^1]
            : null;

        var (result, error) = QueryParameterHelper.ParseBoolean(rawValue, defaultValue);

        if (error is not null)
        {
            ThrowBadRequest($"Parameter '{key}' {error}");
        }

        return result;
    }

    public static double? ResolveMapScale(IQueryCollection query)
    {
        if (!query.TryGetValue("mapExtent", out var extentValues) || extentValues.Count == 0)
        {
            return null;
        }

        if (!query.TryGetValue("imageDisplay", out var displayValues) || displayValues.Count == 0)
        {
            return null;
        }

        var extentRaw = extentValues[^1];
        if (extentRaw.IsNullOrWhiteSpace())
        {
            return null;
        }

        var extentParts = QueryParameterHelper.ParseCommaSeparatedList(extentRaw);
        if (extentParts.Count < 4 ||
            !extentParts[0].TryParseDoubleStrict(out var xmin) ||
            !extentParts[1].TryParseDoubleStrict(out var ymin) ||
            !extentParts[2].TryParseDoubleStrict(out var xmax) ||
            !extentParts[3].TryParseDoubleStrict(out var ymax))
        {
            return null;
        }

        var displayRaw = displayValues[^1];
        if (displayRaw.IsNullOrWhiteSpace())
        {
            return null;
        }

        var displayParts = QueryParameterHelper.ParseCommaSeparatedList(displayRaw);
        if (displayParts.Count < 2 ||
            !displayParts[0].TryParseDoubleStrict(out var widthPixels) ||
            !displayParts[1].TryParseDoubleStrict(out var heightPixels) ||
            widthPixels <= 0 || heightPixels <= 0)
        {
            return null;
        }

        var dpi = 96d;
        if (displayParts.Count >= 3 && displayParts[2].TryParseDoubleStrict(out var parsedDpi) && parsedDpi > 0)
        {
            dpi = parsedDpi;
        }

        var widthUnits = Math.Abs(xmax - xmin);
        var heightUnits = Math.Abs(ymax - ymin);
        if (widthUnits <= 0 || heightUnits <= 0)
        {
            return null;
        }

        var resolutionX = widthUnits / widthPixels;
        var resolutionY = heightUnits / heightPixels;
        var resolution = Math.Max(resolutionX, resolutionY);
        if (resolution <= 0)
        {
            return null;
        }

        const double InchesPerMeter = 39.37d;
        var scale = resolution * dpi * InchesPerMeter;
        return scale > 0 ? scale : null;
    }

    public static double? ResolveMaxAllowableOffset(IQueryCollection query)
    {
        if (!query.TryGetValue("maxAllowableOffset", out var values) || values.Count == 0)
        {
            return null;
        }

        var rawValue = values[^1];
        var (offset, error) = QueryParameterHelper.ParseDouble(rawValue);

        if (error is not null)
        {
            ThrowBadRequest($"maxAllowableOffset {error}, got '{rawValue}'.");
        }

        if (offset.HasValue && offset.Value < 0)
        {
            ThrowBadRequest($"maxAllowableOffset must be non-negative, got '{offset.Value}'.");
        }

        // A value of 0 means no simplification
        return offset > 0 ? offset : null;
    }

    public static int? ResolveGeometryPrecision(IQueryCollection query)
    {
        if (!query.TryGetValue("geometryPrecision", out var values) || values.Count == 0)
        {
            return null;
        }

        var rawValue = values[^1];
        var (precision, error) = QueryParameterHelper.ParsePositiveInt(rawValue, allowZero: true);

        if (error is not null)
        {
            ThrowBadRequest($"geometryPrecision {error}, got '{rawValue}'.");
        }

        // Reasonable limit on decimal places (0-17 for double precision)
        if (precision.HasValue && precision.Value > 17)
        {
            ThrowBadRequest($"geometryPrecision must be between 0 and 17, got '{precision.Value}'.");
        }

        return precision;
    }

    public static DateTime? ResolveHistoricMoment(IQueryCollection query)
    {
        if (!query.TryGetValue("historicMoment", out var values) || values.Count == 0)
        {
            return null;
        }

        var raw = values[^1];
        if (raw.IsNullOrWhiteSpace())
        {
            return null;
        }

        // Parse Unix timestamp (milliseconds since epoch) or ISO 8601
        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliseconds))
        {
            try
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime;
            }
            catch (ArgumentOutOfRangeException)
            {
                ThrowBadRequest($"historicMoment value '{raw}' is outside the valid range for epoch milliseconds.");
            }
        }

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
        {
            return dt.ToUniversalTime();
        }

        ThrowBadRequest($"historicMoment must be a valid timestamp (epoch milliseconds) or ISO 8601 date, got '{raw}'.");
        return null;
    }

    public static string? ResolveHavingClause(IQueryCollection query)
    {
        if (!query.TryGetValue("having", out var values) || values.Count == 0)
        {
            return null;
        }

        var raw = values[^1];
        if (raw.IsNullOrWhiteSpace())
        {
            return null;
        }

        // SECURITY FIX (Bug 36): Validate and sanitize HAVING clause to prevent pathological input
        var trimmed = raw.Trim();

        // Enforce reasonable length limit (typical HAVING clauses are < 500 chars)
        if (trimmed.Length > 1000)
        {
            ThrowBadRequest($"having clause exceeds maximum length of 1000 characters, got {trimmed.Length}.");
        }

        // Validate that it contains expected SQL aggregate patterns
        // This prevents pathological regex attacks and malicious input
        if (!ContainsAggregateFunction(trimmed))
        {
            ThrowBadRequest("having clause must contain a valid aggregate function (COUNT, SUM, AVG, MIN, MAX).");
        }

        // Note: Full SQL injection protection happens in the query builder
        // which validates and sanitizes aggregate function references

        return trimmed;
    }

    private static bool ContainsAggregateFunction(string clause)
    {
        // Simple validation that the clause contains an aggregate function
        // We don't use complex regex here to avoid ReDoS attacks
        var upperClause = clause.ToUpperInvariant();
        return upperClause.Contains("COUNT(") ||
               upperClause.Contains("SUM(") ||
               upperClause.Contains("AVG(") ||
               upperClause.Contains("MIN(") ||
               upperClause.Contains("MAX(");
    }

    private static (GeoservicesResponseFormat Format, bool PrettyPrint) NormalizeFormat(string? format)
    {
        if (format.IsNullOrWhiteSpace())
        {
            return (GeoservicesResponseFormat.Json, false);
        }

        var normalized = format.Trim().ToLowerInvariant();

        return normalized switch
        {
            "json" => (GeoservicesResponseFormat.Json, false),
            "pjson" => (GeoservicesResponseFormat.Json, true),
            "geojson" => (GeoservicesResponseFormat.GeoJson, false),
            "topojson" => (GeoservicesResponseFormat.TopoJson, false),
            "kml" => (GeoservicesResponseFormat.Kml, false),
            "kmz" => (GeoservicesResponseFormat.Kmz, false),
            "shapefile" or "shp" => (GeoservicesResponseFormat.Shapefile, false),
            "csv" => (GeoservicesResponseFormat.Csv, false),
            "wkt" => (GeoservicesResponseFormat.Wkt, false),
            "wkb" => (GeoservicesResponseFormat.Wkb, false),
            _ => HandleUnsupportedFormat(format)
        };
    }

    private static (GeoservicesResponseFormat Format, bool PrettyPrint) HandleUnsupportedFormat(string? format)
    {
        ThrowBadRequest($"Format '{format}' is not supported. Supported formats are json, pjson, geojson, topojson, kml, kmz, shapefile, csv, wkt, and wkb.");
        return (GeoservicesResponseFormat.Json, false);
    }

    private static void ThrowBadRequest(string message)
    {
        throw new GeoservicesRESTQueryException(message);
    }
}
