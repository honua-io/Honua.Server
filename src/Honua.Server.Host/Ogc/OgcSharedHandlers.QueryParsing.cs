// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

// This file contains query parameter parsing methods for OGC API requests.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Data;
using Honua.Server.Core.Editing;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Export;
using Honua.Server.Core.Results;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Styling;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Raster;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Host.Ogc;

internal static partial class OgcSharedHandlers
{
    internal static (FeatureQuery Query, string ContentCrs, bool IncludeCount, IResult? Error) ParseItemsQuery(HttpRequest request, ServiceDefinition service, LayerDefinition layer, IQueryCollection? overrideQuery = null)
    {
        var allowedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "limit",
            "offset",
            "bbox",
            "bbox-crs",
            "datetime",
            "resultType",
            "properties",
            "crs",
            "count",
            "f",
            "filter",
            "filter-lang",
            "filter-crs",
            "ids",
            "sortby"
        };

        var queryCollection = overrideQuery ?? request.Query;

        if (overrideQuery is not null)
        {
            allowedKeys.Add("collections");
        }

        // Allow SQL view parameters if layer has SQL view
        if (layer.SqlView?.Parameters != null)
        {
            foreach (var param in layer.SqlView.Parameters)
            {
                allowedKeys.Add(param.Name);
            }
        }

        foreach (var key in queryCollection.Keys)
        {
            if (!allowedKeys.Contains(key))
            {
                return (default!, string.Empty, false, CreateValidationProblem($"Unknown query parameter '{key}'.", key));
            }
        }

        // Parse limit using shared helper (handles clamping automatically)
        var serviceLimit = service.Ogc.ItemLimit;
        var layerLimit = layer.Query?.MaxRecordCount;
        var (limitValue, limitError) = QueryParameterHelper.ParseLimit(
            queryCollection["limit"].ToString(),
            serviceLimit,
            layerLimit,
            fallback: 10); // OGC API default page size is 10
        if (limitError is not null)
        {
            return (default!, string.Empty, false, CreateValidationProblem(limitError, "limit"));
        }
        // Fallback should ensure non-null, but add safety check
        if (!limitValue.HasValue)
        {
            return (default!, string.Empty, false, CreateValidationProblem("Limit parameter is required.", "limit"));
        }

        // Parse offset using shared helper
        var offsetRaw = queryCollection["offset"].ToString();
        var (offsetValue, offsetError) = QueryParameterHelper.ParseOffset(offsetRaw);
        if (offsetError is not null)
        {
            return (default!, string.Empty, false, CreateValidationProblem(offsetError, "offset"));
        }

        var bboxParse = ParseBoundingBox(queryCollection["bbox"]);
        if (bboxParse.Error is not null)
        {
            return (default!, string.Empty, false, bboxParse.Error);
        }

        var timeParse = ParseTemporal(queryCollection["datetime"]);
        if (timeParse.Error is not null)
        {
            return (default!, string.Empty, false, timeParse.Error);
        }

        var resultTypeParse = ParseResultType(queryCollection["resultType"]);
        if (resultTypeParse.Error is not null)
        {
            return (default!, string.Empty, false, resultTypeParse.Error);
        }
        var resultType = resultTypeParse.Value;

        var (sortOrdersExplicit, sortError) = ParseSortOrders(queryCollection["sortby"], layer);
        if (sortError is not null)
        {
            return (default!, string.Empty, false, sortError);
        }

        var supportedCrs = ResolveSupportedCrs(service, layer);
        var defaultCrs = DetermineDefaultCrs(service, supportedCrs);

        var (acceptCrs, acceptCrsError) = ResolveAcceptCrs(request, supportedCrs);
        if (acceptCrsError is not null)
        {
            return (default!, string.Empty, false, acceptCrsError);
        }

        var filterLangRaw = queryCollection["filter-lang"].ToString();
        string? filterLangNormalized = null;
        if (filterLangRaw.HasValue())
        {
            filterLangNormalized = filterLangRaw.Trim().ToLowerInvariant();
            if (filterLangNormalized != "cql-text" &&
                filterLangNormalized != "cql2-json")
            {
                return (default!, string.Empty, false, CreateValidationProblem($"filter-lang '{filterLangRaw}' is not supported. Supported values: cql-text, cql2-json.", "filter-lang"));
            }
        }

        var (normalizedFilterCrs, filterCrsError) = QueryParameterHelper.ParseCrs(
            queryCollection["filter-crs"].ToString(),
            supportedCrs,
            defaultCrs: null);
        if (filterCrsError is not null)
        {
            return (default!, string.Empty, false, CreateValidationProblem(filterCrsError, "filter-crs"));
        }

        QueryFilter? combinedFilter = null;
        var filterValues = queryCollection["filter"];
        var rawFilter = filterValues.ToString();
        if (rawFilter.HasValue())
        {
            var treatAsJsonFilter = string.Equals(filterLangNormalized, "cql2-json", StringComparison.Ordinal) ||
                                    (filterLangNormalized is null && LooksLikeJson(rawFilter));

            try
            {
                if (treatAsJsonFilter)
                {
                    combinedFilter = Cql2JsonParser.Parse(rawFilter, layer, normalizedFilterCrs);
                    filterLangNormalized ??= "cql2-json";
                }
                else
                {
                    combinedFilter = CqlFilterParser.Parse(rawFilter, layer);
                }
            }
            catch (Exception ex)
            {
                return (default!, string.Empty, false, CreateValidationProblem($"Invalid filter expression. {ex.Message}", "filter"));
            }
        }
        else if (string.Equals(filterLangNormalized, "cql2-json", StringComparison.Ordinal))
        {
            return (default!, string.Empty, false, CreateValidationProblem("filter parameter is required when filter-lang=cql2-json.", "filter"));
        }

        var rawIds = ParseList(queryCollection["ids"]);
        if (rawIds.Count > 0)
        {
            var (idsFilter, idsError) = BuildIdsFilter(layer, rawIds);
            if (idsError is not null)
            {
                return (default!, string.Empty, false, idsError);
            }

            combinedFilter = CombineFilters(combinedFilter, idsFilter);
        }

        var requestedCrsRaw = queryCollection["crs"].ToString();
        if (requestedCrsRaw.IsNullOrWhiteSpace() && acceptCrs.HasValue())
        {
            requestedCrsRaw = acceptCrs;
        }

        var (servedCrsCandidate, servedCrsError) = QueryParameterHelper.ParseCrs(
            requestedCrsRaw,
            supportedCrs,
            defaultCrs);
        if (servedCrsError is not null)
        {
            return (default!, string.Empty, false, CreateValidationProblem(servedCrsError, "crs"));
        }

        var servedCrs = servedCrsCandidate ?? defaultCrs;

        var storageCrs = DetermineStorageCrs(layer);
        var (bboxCrsCandidate, bboxCrsError) = QueryParameterHelper.ParseCrs(
            queryCollection["bbox-crs"].ToString(),
            supportedCrs,
            storageCrs);
        if (bboxCrsError is not null)
        {
            return (default!, string.Empty, false, CreateValidationProblem(bboxCrsError, "bbox-crs"));
        }

        var bboxCrs = bboxCrsCandidate ?? storageCrs;

        // QueryParameterHelper already clamped limit to service/layer max
        var effectiveLimit = limitValue.Value; // Already validated as non-null above
        var effectiveOffset = offsetValue ?? 0;
        var rawProperties = ParseList(queryCollection["properties"]);
        IReadOnlyList<string>? propertyNames = rawProperties.Count == 0 ? null : rawProperties;

        var bbox = bboxParse.Value;
        if (bbox is not null)
        {
            bbox = bbox with { Crs = bboxCrs };
        }

        IReadOnlyList<FeatureSortOrder>? sortOrders = sortOrdersExplicit;
        if (sortOrders is null && layer.IdField.HasValue())
        {
            sortOrders = new[] { new FeatureSortOrder(layer.IdField, FeatureSortDirection.Ascending) };
        }

        // Extract SQL view parameters if layer has SQL view
        var sqlViewParameters = ExtractSqlViewParameters(layer, queryCollection);

        var query = new FeatureQuery(
            Limit: effectiveLimit,
            Offset: effectiveOffset,
            Bbox: bbox,
            Temporal: timeParse.Value,
            ResultType: resultType,
            PropertyNames: propertyNames,
            SortOrders: sortOrders,
            Filter: combinedFilter,
            Crs: servedCrs,
            SqlViewParameters: sqlViewParameters);

        var (includeCount, countError) = QueryParameterHelper.ParseBoolean(
            queryCollection["count"].ToString(),
            defaultValue: resultType == FeatureResultType.Hits);
        // Note: Boolean parsing errors are not critical, just use default
        if (resultType == FeatureResultType.Hits)
        {
            includeCount = true;
        }

        return (query, servedCrs, includeCount, null);
    }

    private static QueryFilter? CombineFilters(QueryFilter? first, QueryFilter? second)
    {
        if (first?.Expression is null)
        {
            return second;
        }

        if (second?.Expression is null)
        {
            return first;
        }

        var combined = new QueryBinaryExpression(first.Expression, QueryBinaryOperator.And, second.Expression);
        return new QueryFilter(combined);
    }

    private static (QueryFilter? Filter, IResult? Error) BuildIdsFilter(LayerDefinition layer, IReadOnlyList<string> ids)
    {
        if (ids.Count == 0)
        {
            return (null, null);
        }

        // Limit IDs to prevent unbounded OR expressions that hammer the database
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

    internal static (IReadOnlyList<FeatureSortOrder>? SortOrders, IResult? Error) ParseSortOrders(string? raw, LayerDefinition layer)
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
                    _ => direction
                };

                if (!suffix.Equals("a", StringComparison.OrdinalIgnoreCase) &&
                    !suffix.Equals("asc", StringComparison.OrdinalIgnoreCase) &&
                    !suffix.Equals("ascending", StringComparison.OrdinalIgnoreCase) &&
                    !suffix.Equals("+", StringComparison.OrdinalIgnoreCase) &&
                    !suffix.Equals("d", StringComparison.OrdinalIgnoreCase) &&
                    !suffix.Equals("desc", StringComparison.OrdinalIgnoreCase) &&
                    !suffix.Equals("descending", StringComparison.OrdinalIgnoreCase) &&
                    !suffix.Equals("-", StringComparison.OrdinalIgnoreCase))
                {
                    return (null, CreateValidationProblem($"Unsupported sort direction '{suffix}'.", "sortby"));
                }
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

    private static (OgcResponseFormat Format, IResult? Error) ParseFormat(string? raw)
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
            "application/vnd.google-earth.kml+xml" => (OgcResponseFormat.Kml, null),
            "kmz" => (OgcResponseFormat.Kmz, null),
            "application/vnd.google-earth.kmz" => (OgcResponseFormat.Kmz, null),
            "topojson" => (OgcResponseFormat.TopoJson, null),
            "application/topo+json" => (OgcResponseFormat.TopoJson, null),
            "flatgeobuf" => (OgcResponseFormat.FlatGeobuf, null),
            "fgb" => (OgcResponseFormat.FlatGeobuf, null),
            "application/vnd.flatgeobuf" => (OgcResponseFormat.FlatGeobuf, null),
            "geoarrow" => (OgcResponseFormat.GeoArrow, null),
            "arrow" => (OgcResponseFormat.GeoArrow, null),
            "application/vnd.apache.arrow.stream" => (OgcResponseFormat.GeoArrow, null),
            "application/vnd.apache.arrow.file" => (OgcResponseFormat.GeoArrow, null),
            "geopkg" => (OgcResponseFormat.GeoPackage, null),
            "geopackage" => (OgcResponseFormat.GeoPackage, null),
            "application/geopackage+sqlite3" => (OgcResponseFormat.GeoPackage, null),
            "shapefile" => (OgcResponseFormat.Shapefile, null),
            "shp" => (OgcResponseFormat.Shapefile, null),
            "application/x-esri-shapefile" => (OgcResponseFormat.Shapefile, null),
            "jsonld" => (OgcResponseFormat.JsonLd, null),
            "json-ld" => (OgcResponseFormat.JsonLd, null),
            "application/ld+json" => (OgcResponseFormat.JsonLd, null),
            "geojson-t" => (OgcResponseFormat.GeoJsonT, null),
            "geojsont" => (OgcResponseFormat.GeoJsonT, null),
            "application/geo+json-t" => (OgcResponseFormat.GeoJsonT, null),
            "csv" => (OgcResponseFormat.Csv, null),
            "text/csv" => (OgcResponseFormat.Csv, null),
            "wkt" => (OgcResponseFormat.Wkt, null),
            "text/wkt" => (OgcResponseFormat.Wkt, null),
            "application/wkt" => (OgcResponseFormat.Wkt, null),
            "wkb" => (OgcResponseFormat.Wkb, null),
            "application/wkb" => (OgcResponseFormat.Wkb, null),
            "application/vnd.ogc.wkb" => (OgcResponseFormat.Wkb, null),
            _ => (default, CreateValidationProblem($"Unsupported format '{raw}'.", "f"))
        };
    }

    internal static (OgcResponseFormat Format, string ContentType, IResult? Error) ResolveResponseFormat(HttpRequest request, IQueryCollection? queryOverrides = null)
    {
        var formatParameter = queryOverrides?["f"].ToString();
        if (formatParameter.IsNullOrWhiteSpace())
        {
            formatParameter = request.Query["f"].ToString();
        }
        if (formatParameter.HasValue())
        {
            var (format, error) = ParseFormat(formatParameter);
            if (error is not null)
            {
                return (default, string.Empty, error);
            }

            return (format, GetMimeType(format), null);
        }

        if (request.Headers.TryGetValue(HeaderNames.Accept, out var acceptValues) && acceptValues.Count > 0)
        {
            if (MediaTypeHeaderValue.TryParseList(acceptValues, out var parsedAccepts))
            {
                // Use lazy evaluation - no need to materialize with ToList() when only iterating
                foreach (var media in parsedAccepts
                    .OrderByDescending(value => value.Quality ?? 1.0))
                {
                    var mediaType = media.MediaType.ToString();
                    if (mediaType.IsNullOrWhiteSpace())
                    {
                        continue;
                    }

                    if (TryMapMediaType(mediaType!, out var mappedFormat))
                    {
                        return (mappedFormat, GetMimeType(mappedFormat), null);
                    }

                    if (string.Equals(mediaType, "*/*", StringComparison.Ordinal))
                    {
                        return (OgcResponseFormat.GeoJson, GetMimeType(OgcResponseFormat.GeoJson), null);
                    }
                }

                // If no Accept header media types matched, fall back to default format (GeoJSON)
                // This is more lenient than returning 406 and aligns with OGC best practices
            }
        }

        return (OgcResponseFormat.GeoJson, GetMimeType(OgcResponseFormat.GeoJson), null);
    }

    private static bool TryMapMediaType(string mediaType, out OgcResponseFormat format)
    {
        format = mediaType.ToLowerInvariant() switch
        {
            "application/geo+json" or "application/json" or "application/vnd.geo+json" => OgcResponseFormat.GeoJson,
            "text/html" or "application/xhtml+xml" => OgcResponseFormat.Html,
            "application/vnd.google-earth.kml+xml" => OgcResponseFormat.Kml,
            "application/vnd.google-earth.kmz" => OgcResponseFormat.Kmz,
            "application/topo+json" => OgcResponseFormat.TopoJson,
            "application/vnd.flatgeobuf" => OgcResponseFormat.FlatGeobuf,
            "application/vnd.apache.arrow.stream" or "application/vnd.apache.arrow.file" => OgcResponseFormat.GeoArrow,
            "application/geopackage+sqlite3" or "application/vnd.sqlite3" => OgcResponseFormat.GeoPackage,
            "application/ld+json" => OgcResponseFormat.JsonLd,
            "application/geo+json-t" => OgcResponseFormat.GeoJsonT,
            "application/zip" or "application/x-esri-shapefile" => OgcResponseFormat.Shapefile,
            "text/csv" => OgcResponseFormat.Csv,
            _ => (OgcResponseFormat)0
        };

        return format != 0;
    }

    internal static (string? Value, IResult? Error) ResolveAcceptCrs(HttpRequest request, IReadOnlyCollection<string> supported)
    {
        if (!request.Headers.TryGetValue("Accept-Crs", out var headerValues) || headerValues.Count == 0)
        {
            return (null, null);
        }

        var candidates = new List<(string Crs, double Quality)>();
        foreach (var header in headerValues)
        {
            if (header.IsNullOrWhiteSpace())
            {
                continue;
            }

            foreach (var token in QueryParsingHelpers.ParseCsv(header))
            {
                var semicolonIndex = token.IndexOf(';');
                var crsToken = semicolonIndex >= 0 ? token[..semicolonIndex] : token;
                var quality = 1.0;

                if (semicolonIndex >= 0)
                {
                    var parameters = token[(semicolonIndex + 1)..]
                        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                    foreach (var parameter in parameters)
                    {
                        var parts = parameter.Split('=', 2, StringSplitOptions.TrimEntries);
                        if (parts.Length == 2 && string.Equals(parts[0], "q", StringComparison.OrdinalIgnoreCase) &&
                            parts[1].TryParseDoubleStrict(out var parsedQ))
                        {
                            quality = parsedQ;
                        }
                    }
                }

                candidates.Add((CrsHelper.NormalizeIdentifier(crsToken), quality));
            }
        }

        if (candidates.Count == 0)
        {
            return (null, null);
        }

        foreach (var candidate in candidates
                     .OrderByDescending(item => item.Quality)
                     .ThenBy(item => item.Crs, StringComparer.OrdinalIgnoreCase))
        {
            if (supported.Any(value => string.Equals(value, candidate.Crs, StringComparison.OrdinalIgnoreCase)))
            {
                return (candidate.Crs, null);
            }
        }

        return (null, Results.StatusCode(StatusCodes.Status406NotAcceptable));
    }
    internal static (string Value, IResult? Error) ResolveContentCrs(string? requestedCrs, ServiceDefinition service, LayerDefinition layer)
    {
        var supported = ResolveSupportedCrs(service, layer);
        var defaultCrs = DetermineDefaultCrs(service, supported);

        if (requestedCrs.IsNullOrWhiteSpace())
        {
            return (defaultCrs, null);
        }

        var normalizedRequested = CrsHelper.NormalizeIdentifier(requestedCrs);
        var match = supported.FirstOrDefault(crs => string.Equals(crs, normalizedRequested, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            var supportedList = string.Join(", ", supported);
            return (string.Empty, CreateValidationProblem($"Requested CRS '{requestedCrs}' is not supported. Supported CRS values: {supportedList}.", "crs"));
        }

        return (match, null);
    }

    internal static IReadOnlyList<string> ResolveSupportedCrs(ServiceDefinition service, LayerDefinition layer)
    {
        var supported = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddValue(string? value)
        {
            if (value.IsNullOrWhiteSpace())
            {
                return;
            }

            var normalized = CrsHelper.NormalizeIdentifier(value);
            if (seen.Add(normalized))
            {
                supported.Add(normalized);
            }
        }

        foreach (var crs in layer.Crs)
        {
            AddValue(crs);
        }

        AddValue(service.Ogc.DefaultCrs);

        foreach (var crs in service.Ogc.AdditionalCrs)
        {
            AddValue(crs);
        }

        if (supported.Count == 0)
        {
            AddValue(CrsHelper.DefaultCrsIdentifier);
        }

        // Add CRS84H (3D support) if layer has Z coordinates
        if (layer.HasZ || (layer.Storage?.HasZ ?? false))
        {
            AddValue("CRS84H");
        }

        return supported;
    }

    internal static string DetermineDefaultCrs(ServiceDefinition service, IReadOnlyList<string> supported)
    {
        if (supported.Count == 0)
        {
            return CrsHelper.DefaultCrsIdentifier;
        }

        if (service.Ogc.DefaultCrs.HasValue())
        {
            var normalizedDefault = CrsHelper.NormalizeIdentifier(service.Ogc.DefaultCrs);
            var match = supported.FirstOrDefault(crs => string.Equals(crs, normalizedDefault, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return supported[0];
    }

    internal static string DetermineStorageCrs(LayerDefinition layer)
    {
        if (layer.Storage?.Srid is int srid && srid > 0)
        {
            return CrsHelper.NormalizeIdentifier($"EPSG:{srid}");
        }

        if (layer.Crs.Count > 0)
        {
            return CrsHelper.NormalizeIdentifier(layer.Crs[0]);
        }

        return CrsHelper.DefaultCrsIdentifier;
    }

    private static (BoundingBox? Value, IResult? Error) ParseBoundingBox(string? raw)
    {
        // Note: bbox-crs is parsed separately in ParseItemsQuery and set on the BoundingBox later
        var (bbox, error) = QueryParameterHelper.ParseBoundingBox(raw, crs: null);
        if (error is not null)
        {
            return (null, CreateValidationProblem(error, "bbox"));
        }

        return (bbox, null);
    }

    private static (TemporalInterval? Value, IResult? Error) ParseTemporal(string? raw)
    {
        var (interval, error) = QueryParameterHelper.ParseTemporalRange(raw);
        if (error is not null)
        {
            return (null, CreateValidationProblem(error, "datetime"));
        }

        return (interval, null);
    }

    private static (FeatureResultType Value, IResult? Error) ParseResultType(string? raw)
    {
        var (resultType, error) = QueryParameterHelper.ParseResultType(raw, FeatureResultType.Results);
        if (error is not null)
        {
            return (FeatureResultType.Results, CreateValidationProblem(error, "resultType"));
        }

        return (resultType, null);
    }

    private static IReadOnlyList<string> ParseList(string? raw)
    {
        if (raw.IsNullOrWhiteSpace())
        {
            return Array.Empty<string>();
        }

        var values = QueryParsingHelpers.ParseCsv(raw);
        return values.Count == 0 ? Array.Empty<string>() : values;
    }

    internal static IReadOnlyList<string> BuildDefaultCrs(ServiceDefinition service)
    {
        if (service.Ogc.DefaultCrs.HasValue())
        {
            return new[] { CrsHelper.NormalizeIdentifier(service.Ogc.DefaultCrs!) };
        }

        return new[] { CrsHelper.DefaultCrsIdentifier };
    }

    internal static bool LooksLikeJson(string? value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return false;
        }

        var trimmed = value.TrimStart();
        return trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal);
    }

    private static bool HasIfMatch(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(HeaderNames.IfMatch, out var values))
        {
            return false;
        }

        foreach (var value in values)
        {
            if (value.HasValue())
            {
                return true;
            }
        }

        return false;
    }

    private static bool PreferReturnMinimal(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Prefer", out var values))
        {
            return false;
        }

        foreach (var header in values)
        {
            if (header.IsNullOrWhiteSpace())
            {
                continue;
            }

            var tokens = QueryParsingHelpers.ParseCsv(header);
            if (tokens.Any(token => string.Equals(token, "return=minimal", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts SQL view parameters from the query string based on layer configuration.
    /// </summary>
    private static IReadOnlyDictionary<string, string> ExtractSqlViewParameters(
        LayerDefinition layer,
        IQueryCollection query)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Only extract if layer has SQL view defined
        if (layer.SqlView?.Parameters == null || layer.SqlView.Parameters.Count == 0)
        {
            return parameters;
        }

        // Extract each defined parameter from query string
        foreach (var param in layer.SqlView.Parameters)
        {
            var value = query[param.Name].ToString();
            if (value.HasValue())
            {
                parameters[param.Name] = value;
            }
        }

        return parameters;
    }

    private static IResult ApplyPreferenceApplied(IResult result, string value)
        => WithResponseHeader(result, "Preference-Applied", value);

    internal static IReadOnlyList<string> ParseCollectionsParameter(StringValues values)
    {
        if (values.Count == 0)
        {
            return Array.Empty<string>();
        }

        var collectionIds = new List<string>();
        foreach (var entry in values)
        {
            if (entry.IsNullOrWhiteSpace())
            {
                continue;
            }

            var tokens = QueryParsingHelpers.ParseCsv(entry);
            foreach (var token in tokens)
            {
                collectionIds.Add(token);
            }
        }

        if (collectionIds.Count == 0)
        {
            return Array.Empty<string>();
        }

        return collectionIds
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
