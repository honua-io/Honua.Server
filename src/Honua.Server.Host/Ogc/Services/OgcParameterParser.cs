// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Honua.Server.Core.Data;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace Honua.Server.Host.Ogc.Services;

/// <summary>
/// Service for parsing OGC API request parameters including format, result type, lists, and filters.
/// </summary>
internal sealed class OgcParameterParser
{
    /// <summary>
    /// Parses the format parameter (f) from query string or Accept header.
    /// </summary>
    internal (OgcResponseFormat Format, string ContentType, IResult? Error) ResolveResponseFormat(
        HttpRequest request,
        IQueryCollection? queryOverrides = null)
    {
        var queryCollection = queryOverrides ?? request.Query;
        var formatRaw = queryCollection["f"].ToString();
        var (format, formatError) = ParseFormat(formatRaw);
        if (formatError is not null)
        {
            return (OgcResponseFormat.GeoJson, string.Empty, formatError);
        }

        // If no explicit format parameter, check Accept header
        if (formatRaw.IsNullOrWhiteSpace() && request.Headers.TryGetValue("Accept", out var acceptValues))
        {
            foreach (var acceptValue in acceptValues)
            {
                if (acceptValue.IsNullOrWhiteSpace())
                {
                    continue;
                }

                if (TryMapMediaType(acceptValue, out var mappedFormat))
                {
                    format = mappedFormat;
                    break;
                }
            }
        }

        var contentType = GetMimeType(format);
        return (format, contentType, null);
    }

    /// <summary>
    /// Parses collections parameter from query string.
    /// </summary>
    internal IReadOnlyList<string> ParseCollectionsParameter(StringValues values)
    {
        var result = new List<string>();
        foreach (var value in values)
        {
            if (value.IsNullOrWhiteSpace())
            {
                continue;
            }

            foreach (var token in QueryParsingHelpers.ParseCsv(value))
            {
                result.Add(token);
            }
        }

        return result;
    }

    /// <summary>
    /// Parses sort orders from sortby parameter.
    /// </summary>
    internal (IReadOnlyList<FeatureSortOrder>? SortOrders, IResult? Error) ParseSortOrders(string? raw, LayerDefinition layer)
    {
        if (raw.IsNullOrWhiteSpace())
        {
            return (null, null);
        }

        var tokens = QueryParsingHelpers.ParseCsv(raw);
        var orders = new List<FeatureSortOrder>();

        foreach (var token in tokens)
        {
            var trimmed = token;
            var direction = FeatureSortDirection.Ascending;

            if (trimmed.StartsWith("-", StringComparison.Ordinal))
            {
                direction = FeatureSortDirection.Descending;
                trimmed = trimmed[1..].Trim();
            }
            else if (trimmed.StartsWith("+", StringComparison.Ordinal))
            {
                trimmed = trimmed[1..].Trim();
            }

            var fieldToken = trimmed;
            var suffixIndex = trimmed.IndexOf(':');
            if (suffixIndex >= 0)
            {
                fieldToken = trimmed[..suffixIndex].Trim();
                var suffix = trimmed[(suffixIndex + 1)..].Trim();
                if (suffix.Length == 0)
                {
                    return (null, CreateValidationProblem("sortby direction segment cannot be empty.", "sortby"));
                }

                direction = suffix.ToLowerInvariant() switch
                {
                    "a" or "asc" or "ascending" or "+" => FeatureSortDirection.Ascending,
                    "d" or "desc" or "descending" or "-" => FeatureSortDirection.Descending,
                    _ => throw new InvalidOperationException($"Unsupported sort direction '{suffix}'.")
                };
            }

            if (fieldToken.IsNullOrWhiteSpace())
            {
                return (null, CreateValidationProblem("sortby field name cannot be empty.", "sortby"));
            }

            try
            {
                var (resolvedField, fieldType) = CqlFilterParserUtils.ResolveField(layer, fieldToken);
                if (string.Equals(resolvedField, layer.GeometryField, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fieldType, "geometry", StringComparison.OrdinalIgnoreCase))
                {
                    return (null, CreateValidationProblem("Geometry fields cannot be used with sortby.", "sortby"));
                }

                orders.Add(new FeatureSortOrder(resolvedField, direction));
            }
            catch (InvalidOperationException ex)
            {
                return (null, CreateValidationProblem(ex.Message, "sortby"));
            }
        }

        if (orders.Count == 0)
        {
            return (null, CreateValidationProblem("sortby parameter must specify at least one field.", "sortby"));
        }

        return (orders, null);
    }

    /// <summary>
    /// Builds an IDs filter from a list of feature identifiers.
    /// </summary>
    internal (QueryFilter? Filter, IResult? Error) BuildIdsFilter(LayerDefinition layer, IReadOnlyList<string> ids)
    {
        if (ids.Count == 0)
        {
            return (null, null);
        }

        const int MaxIds = 1000;
        if (ids.Count > MaxIds)
        {
            return (null, CreateValidationProblem($"ids parameter exceeds maximum limit of {MaxIds} identifiers.", "ids"));
        }

        QueryExpression? expression = null;
        (string FieldName, string? FieldType) resolved;

        try
        {
            resolved = CqlFilterParserUtils.ResolveField(layer, layer.IdField);
        }
        catch (Exception ex)
        {
            return (null, CreateValidationProblem(ex.Message, "ids"));
        }

        foreach (var rawId in ids)
        {
            if (rawId.IsNullOrWhiteSpace())
            {
                continue;
            }

            var typedValue = CqlFilterParserUtils.ConvertToFieldValue(resolved.FieldType, rawId);
            var comparison = new QueryBinaryExpression(
                new QueryFieldReference(resolved.FieldName),
                QueryBinaryOperator.Equal,
                new QueryConstant(typedValue));

            expression = expression is null
                ? comparison
                : new QueryBinaryExpression(expression, QueryBinaryOperator.Or, comparison);
        }

        if (expression is null)
        {
            return (null, CreateValidationProblem("ids parameter must include at least one non-empty value.", "ids"));
        }

        return (new QueryFilter(expression), null);
    }

    /// <summary>
    /// Parses a comma-separated list parameter.
    /// </summary>
    internal IReadOnlyList<string> ParseList(string? raw)
    {
        if (raw.IsNullOrWhiteSpace())
        {
            return Array.Empty<string>();
        }

        var values = QueryParsingHelpers.ParseCsv(raw);
        return values.Count == 0 ? Array.Empty<string>() : values;
    }

    /// <summary>
    /// Checks if a string looks like JSON (starts with { or [).
    /// </summary>
    internal bool LooksLikeJson(string? value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return false;
        }

        var trimmed = value.TrimStart();
        return trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal);
    }

    private (OgcResponseFormat Format, IResult? Error) ParseFormat(string? raw)
    {
        if (raw.IsNullOrWhiteSpace())
        {
            return (OgcResponseFormat.GeoJson, null);
        }

        return raw.ToLowerInvariant() switch
        {
            "json" => (OgcResponseFormat.GeoJson, null),
            "geojson" => (OgcResponseFormat.GeoJson, null),
            "html" => (OgcResponseFormat.Html, null),
            "text/html" => (OgcResponseFormat.Html, null),
            "kml" => (OgcResponseFormat.Kml, null),
            "kmz" => (OgcResponseFormat.Kmz, null),
            "topojson" => (OgcResponseFormat.TopoJson, null),
            "flatgeobuf" => (OgcResponseFormat.FlatGeobuf, null),
            "fgb" => (OgcResponseFormat.FlatGeobuf, null),
            "geoarrow" => (OgcResponseFormat.GeoArrow, null),
            "arrow" => (OgcResponseFormat.GeoArrow, null),
            "geopkg" => (OgcResponseFormat.GeoPackage, null),
            "geopackage" => (OgcResponseFormat.GeoPackage, null),
            "shapefile" => (OgcResponseFormat.Shapefile, null),
            "shp" => (OgcResponseFormat.Shapefile, null),
            "jsonld" => (OgcResponseFormat.JsonLd, null),
            "json-ld" => (OgcResponseFormat.JsonLd, null),
            "csv" => (OgcResponseFormat.Csv, null),
            "geojson-t" => (OgcResponseFormat.GeoJsonT, null),
            "wkt" => (OgcResponseFormat.Wkt, null),
            "wkb" => (OgcResponseFormat.Wkb, null),
            _ => (OgcResponseFormat.GeoJson, CreateValidationProblem($"Unsupported format '{raw}'.", "f"))
        };
    }

    private bool TryMapMediaType(string mediaType, out OgcResponseFormat format)
    {
        format = mediaType.ToLowerInvariant() switch
        {
            "application/geo+json" => OgcResponseFormat.GeoJson,
            "application/json" => OgcResponseFormat.GeoJson,
            "text/html" => OgcResponseFormat.Html,
            "application/vnd.google-earth.kml+xml" => OgcResponseFormat.Kml,
            "application/vnd.google-earth.kmz" => OgcResponseFormat.Kmz,
            "application/topo+json" => OgcResponseFormat.TopoJson,
            "application/vnd.flatgeobuf" => OgcResponseFormat.FlatGeobuf,
            "application/vnd.apache.arrow.stream" => OgcResponseFormat.GeoArrow,
            "application/geopackage+sqlite3" => OgcResponseFormat.GeoPackage,
            "application/x-esri-shapefile" => OgcResponseFormat.Shapefile,
            "application/ld+json" => OgcResponseFormat.JsonLd,
            "text/csv" => OgcResponseFormat.Csv,
            _ => OgcResponseFormat.GeoJson
        };

        return !mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase) || format != OgcResponseFormat.GeoJson;
    }

    private string GetMimeType(OgcResponseFormat format) => format switch
    {
        OgcResponseFormat.Html => "text/html",
        OgcResponseFormat.Kml => "application/vnd.google-earth.kml+xml",
        OgcResponseFormat.Kmz => "application/vnd.google-earth.kmz",
        OgcResponseFormat.TopoJson => "application/topo+json",
        OgcResponseFormat.FlatGeobuf => "application/vnd.flatgeobuf",
        OgcResponseFormat.GeoArrow => "application/vnd.apache.arrow.stream",
        OgcResponseFormat.GeoPackage => "application/geopackage+sqlite3",
        OgcResponseFormat.Shapefile => "application/zip",
        OgcResponseFormat.Csv => "text/csv",
        OgcResponseFormat.JsonLd => "application/ld+json",
        OgcResponseFormat.GeoJsonT => "application/geo+json-t",
        OgcResponseFormat.Wkt => "text/wkt; charset=utf-8",
        OgcResponseFormat.Wkb => "application/wkb",
        _ => "application/geo+json"
    };

    private IResult CreateValidationProblem(string detail, string parameter)
    {
        var problem = OgcProblemDetails.Create(
            "Invalid parameter value",
            detail,
            StatusCodes.Status400BadRequest,
            parameter);
        return Results.Json(problem, statusCode: StatusCodes.Status400BadRequest);
    }
}
