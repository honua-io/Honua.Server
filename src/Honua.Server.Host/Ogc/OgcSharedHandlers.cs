// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
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

internal static class OgcSharedHandlers
{
    private const string CollectionIdSeparator = "::";
    internal const string ApiDefinitionFileName = "ogc-openapi.json";
    private const string DefaultTemporalReferenceSystem = "http://www.opengis.net/def/uom/ISO-8601/0/Gregorian";
    private const string HtmlMediaType = "text/html";
    internal const string HtmlContentType = HtmlMediaType + "; charset=utf-8";
    internal static readonly JsonSerializerOptions GeoJsonSerializerOptions = new(JsonSerializerDefaults.Web);
    internal static readonly string[] DefaultConformanceClasses =
    {
        "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/core",
        "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/geojson",
        "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/oas30",
        "http://www.opengis.net/spec/ogcapi-features-3/1.0/conf/search",
        "http://www.opengis.net/spec/ogcapi-features-3/1.0/conf/filter",
        "http://www.opengis.net/spec/ogcapi-features-3/1.0/conf/filter-cql2-json",
        "http://www.opengis.net/spec/ogcapi-features-3/1.0/conf/features-filter",
        "http://www.opengis.net/spec/ogcapi-features-3/1.0/conf/spatial-operators",
        "http://www.opengis.net/spec/ogcapi-features-3/1.0/conf/temporal-operators",
        // OGC API - Tiles conformance classes (spec version 1.0)
        "http://www.opengis.net/spec/ogcapi-tiles-1/1.0/conf/core",
        "http://www.opengis.net/spec/ogcapi-tiles-1/1.0/conf/tileset",
        "http://www.opengis.net/spec/ogcapi-tiles-1/1.0/conf/tilesets-list",
        "http://www.opengis.net/spec/ogcapi-tiles-1/1.0/conf/collections",
        "http://www.opengis.net/spec/ogcapi-tiles-1/1.0/conf/geodata-tilesets",
        "http://www.opengis.net/spec/ogcapi-tiles-1/1.0/conf/oas30"
    };
    private const int OverlayFetchBatchSize = 500;
    private const int OverlayFetchMaxFeatures = 10_000;

    internal sealed record CollectionSummary(
        string Id,
        string? Title,
        string? Description,
        string? ItemType,
        IReadOnlyList<string> Crs,
        string? StorageCrs);

    internal sealed record HtmlFeatureEntry(
        string CollectionId,
        string? CollectionTitle,
        FeatureComponents Components);


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

        var query = new FeatureQuery(
            Limit: effectiveLimit,
            Offset: effectiveOffset,
            Bbox: bbox,
            Temporal: timeParse.Value,
            ResultType: resultType,
            PropertyNames: propertyNames,
            SortOrders: sortOrders,
            Filter: combinedFilter,
            Crs: servedCrs);

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

    internal static async Task<IResult> ExecuteSearchAsync(
        HttpRequest request,
        IReadOnlyList<string> collections,
        IQueryCollection queryParameters,
        IFeatureContextResolver resolver,
        IFeatureRepository repository,
        IGeoPackageExporter geoPackageExporter,
        IShapefileExporter shapefileExporter,
        IFlatGeobufExporter flatGeobufExporter,
        IGeoArrowExporter geoArrowExporter,
        ICsvExporter csvExporter,
        IFeatureAttachmentOrchestrator attachmentOrchestrator,
        IMetadataRegistry metadataRegistry,
        IApiMetrics apiMetrics,
        OgcCacheHeaderService cacheHeaderService,
        Services.IOgcFeaturesAttachmentHandler attachmentHandler,
        CancellationToken cancellationToken)
    {
        static QueryCollection RemoveCollectionsParameter(IQueryCollection source)
        {
            var dictionary = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in source)
            {
                if (string.Equals(pair.Key, "collections", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                dictionary[pair.Key] = pair.Value;
            }

            return new QueryCollection(dictionary);
        }

        if (collections.Count == 0)
        {
            return CreateValidationProblem("At least one collection must be specified.", "collections");
        }

        var resolutions = new List<SearchCollectionContext>(collections.Count);
        foreach (var collectionId in collections)
        {
            var resolution = await ResolveCollectionAsync(collectionId, resolver, cancellationToken).ConfigureAwait(false);
            if (resolution.IsFailure)
            {
                return MapCollectionResolutionError(resolution.Error!, collectionId);
            }

            resolutions.Add(new SearchCollectionContext(collectionId, resolution.Value));
        }

        var (format, contentType, formatError) = ResolveResponseFormat(request, queryParameters);
        if (formatError is not null)
        {
            return formatError;
        }

        var supportsAggregation = format is OgcResponseFormat.GeoJson or OgcResponseFormat.Html;
        if (!supportsAggregation)
        {
            if (collections.Count != 1)
            {
                return CreateValidationProblem("The requested format requires a single collection.", "collections");
            }

            var sanitized = RemoveCollectionsParameter(queryParameters);
            return await OgcFeaturesHandlers.ExecuteCollectionItemsAsync(
                collections[0],
                request,
                resolver,
                repository,
                geoPackageExporter,
                shapefileExporter,
                flatGeobufExporter,
                geoArrowExporter,
                csvExporter,
                attachmentOrchestrator,
                metadataRegistry,
                apiMetrics,
                cacheHeaderService,
                attachmentHandler,
                sanitized,
                cancellationToken).ConfigureAwait(false);
        }

        var isHtml = format == OgcResponseFormat.Html;
        var preparedQueries = new List<(SearchCollectionContext Context, FeatureQuery Query, string ContentCrs)>(resolutions.Count);
        FeatureQuery? baseQuery = null;
        FeatureResultType resultType = FeatureResultType.Results;
        string? globalContentCrs = null;
        var includeCount = false;

        foreach (var context in resolutions)
        {
            var (query, contentCrs, includeCountFlag, error) = ParseItemsQuery(request, context.FeatureContext.Service, context.FeatureContext.Layer, queryParameters);
            if (error is not null)
            {
                return error;
            }

            includeCount |= includeCountFlag;

            if (baseQuery is null)
            {
                baseQuery = query;
                resultType = query.ResultType;
            }
            else if (query.ResultType != resultType)
            {
                return CreateValidationProblem("Mixed resultType values are not supported across collections.", "resultType");
            }

            if (globalContentCrs is null)
            {
                globalContentCrs = contentCrs;
            }
            else if (!string.Equals(globalContentCrs, contentCrs, StringComparison.OrdinalIgnoreCase))
            {
                return CreateValidationProblem("All collections in a search must share a common response CRS.", "crs");
            }

            preparedQueries.Add((context, query, contentCrs));
        }

        if (baseQuery is null)
        {
            baseQuery = new FeatureQuery(ResultType: FeatureResultType.Results);
        }

        if (resultType == FeatureResultType.Hits)
        {
            includeCount = true;
        }
        var needsOffsetDistribution = (baseQuery.Offset ?? 0) > 0;

        var initialLimit = baseQuery.Limit.HasValue ? Math.Max(1, (long)baseQuery.Limit.Value) : long.MaxValue;
        var initialOffset = baseQuery.Offset ?? 0;
        if (!needsOffsetDistribution)
        {
            initialOffset = 0;
        }

        if (isHtml)
        {
            var htmlEntries = new List<HtmlFeatureEntry>();

            var iterationResult = await EnumerateSearchAsync(
                preparedQueries,
                resultType,
                includeCount,
                initialLimit,
                initialOffset,
                repository,
                (context, layer, query, record, components) =>
                {
                    htmlEntries.Add(new HtmlFeatureEntry(context.CollectionId, layer.Title ?? context.CollectionId, components));
                    return ValueTask.FromResult(true);
                },
                cancellationToken).ConfigureAwait(false);

            var links = BuildSearchLinks(request, collections, baseQuery, iterationResult.NumberMatched, contentType);
            var htmlDescription = collections.Count == 1
                ? $"Collection: {collections[0]}"
                : $"Collections: {string.Join(", ", collections)}";

            var html = RenderFeatureCollectionHtml(
                "Search results",
                htmlDescription,
                htmlEntries,
                iterationResult.NumberMatched,
                iterationResult.NumberReturned,
                globalContentCrs,
                links,
                resultType == FeatureResultType.Hits);

            var htmlResult = Results.Content(html, HtmlContentType);
            return WithContentCrsHeader(htmlResult, globalContentCrs);
        }

        var geoJsonResult = Results.Stream(async stream =>
        {
            await WriteGeoJsonSearchResponseAsync(
                stream,
                request,
                collections,
                contentType,
                baseQuery,
                preparedQueries,
                includeCount,
                resultType,
                repository,
                initialLimit,
                initialOffset,
                cancellationToken).ConfigureAwait(false);
        }, contentType);

        return WithContentCrsHeader(geoJsonResult, globalContentCrs);
    }

    internal static object BuildQueryablesSchema(LayerDefinition layer)
    {
        var properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var required = new List<string>();

        var fields = FieldMetadataResolver.ResolveFields(layer, includeGeometry: false, includeIdField: true);
        foreach (var field in fields)
        {
            var schema = CreateQueryablesPropertySchema(field);
            if (schema is null)
            {
                continue;
            }

            properties[field.Name] = schema;
            if (!field.Nullable)
            {
                required.Add(field.Name);
            }
        }

        if (!properties.ContainsKey(layer.GeometryField))
        {
            properties[layer.GeometryField] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["content"] = new Dictionary<string, object>
                {
                    ["application/geo+json"] = new { }
                }
            };
        }

        var result = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["title"] = layer.Title.IsNullOrWhiteSpace() ? layer.Id : layer.Title,
            ["properties"] = properties,
            ["additionalProperties"] = false
        };

        if (required.Count > 0)
        {
            result["required"] = required;
        }

        return result;
    }

    private static object? CreateQueryablesPropertySchema(FieldDefinition field)
    {
        var kind = (field.DataType ?? field.StorageType ?? "string").Trim().ToLowerInvariant();

        if (string.Equals(kind, "geometry", StringComparison.OrdinalIgnoreCase))
        {
            return new Dictionary<string, object>
            {
                ["type"] = "object",
                ["content"] = new Dictionary<string, object>
                {
                    ["application/geo+json"] = new { }
                }
            };
        }

        var schema = new Dictionary<string, object>();

        switch (kind)
        {
            case "int":
            case "integer":
            case "int16":
            case "int32":
            case "short":
            case "smallint":
                schema["type"] = "integer";
                break;
            case "int64":
            case "long":
            case "bigint":
                schema["type"] = "integer";
                schema["format"] = "int64";
                break;
            case "double":
            case "float":
            case "single":
            case "real":
            case "decimal":
            case "numeric":
                schema["type"] = "number";
                break;
            case "date":
            case "datetime":
            case "datetimeoffset":
            case "time":
                schema["type"] = "string";
                schema["format"] = "date-time";
                break;
            case "bool":
            case "boolean":
                schema["type"] = "boolean";
                break;
            case "uuid":
            case "guid":
            case "uniqueidentifier":
                schema["type"] = "string";
                schema["format"] = "uuid";
                break;
            default:
                schema["type"] = "string";
                break;
        }

        if (field.MaxLength.HasValue && field.MaxLength.Value > 0 && schema.TryGetValue("type", out var typeValue) &&
            string.Equals(typeValue as string, "string", StringComparison.OrdinalIgnoreCase))
        {
            schema["maxLength"] = field.MaxLength.Value;
        }

        return schema;
    }

    internal static List<OgcLink> BuildCollectionLinks(HttpRequest request, ServiceDefinition service, LayerDefinition layer, string collectionId)
    {
        var links = new List<OgcLink>(layer.Links.Select(ToLink));
        links.AddRange(new[]
        {
            BuildLink(request, $"/ogc/collections/{collectionId}", "self", "application/json", layer.Title),
            BuildLink(request, $"/ogc/collections/{collectionId}/items", "items", "application/geo+json", $"Items for {layer.Title}"),
            BuildLink(request, $"/ogc/collections/{collectionId}/items", "alternate", "application/vnd.google-earth.kml+xml", $"Items for {layer.Title} (KML)", null, new Dictionary<string, string?> { ["f"] = "kml" }),
            BuildLink(request, $"/ogc/collections/{collectionId}/items", "alternate", "application/vnd.google-earth.kmz", $"Items for {layer.Title} (KMZ)", null, new Dictionary<string, string?> { ["f"] = "kmz" }),
            BuildLink(request, $"/ogc/collections/{collectionId}/items", "alternate", "application/geopackage+sqlite3", $"Items for {layer.Title} (GeoPackage)", null, new Dictionary<string, string?> { ["f"] = "geopackage" }),
            BuildLink(request, $"/ogc/collections/{collectionId}/queryables", "queryables", "application/json", $"Queryables for {layer.Title}")
        });

        if (layer.DefaultStyleId.HasValue())
        {
            links.Add(BuildLink(request, $"/ogc/collections/{collectionId}/styles/{layer.DefaultStyleId}", "stylesheet", "application/vnd.ogc.sld+xml", $"Default style for {layer.Title}"));
        }

        foreach (var styleId in layer.StyleIds)
        {
            if (!string.Equals(styleId, layer.DefaultStyleId, StringComparison.OrdinalIgnoreCase))
            {
                links.Add(BuildLink(request, $"/ogc/collections/{collectionId}/styles/{styleId}", "stylesheet", "application/vnd.ogc.sld+xml", $"Style '{styleId}'"));
            }
        }

        return links;
    }

    private static void AppendStyleMetadata(IDictionary<string, object?> target, LayerDefinition layer)
    {
        if (target is null)
        {
            return;
        }

        if (layer.DefaultStyleId.HasValue())
        {
            target["honua:defaultStyleId"] = layer.DefaultStyleId;
        }

        var styleIds = BuildOrderedStyleIds(layer);
        if (styleIds.Count > 0)
        {
            target["honua:styleIds"] = styleIds;
        }

        if (layer.MinScale is double minScale)
        {
            target["honua:minScale"] = minScale;
        }

        if (layer.MaxScale is double maxScale)
        {
            target["honua:maxScale"] = maxScale;
        }
    }

    internal static IReadOnlyList<string> BuildOrderedStyleIds(LayerDefinition layer)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<string>();

        if (layer.DefaultStyleId.HasValue() && seen.Add(layer.DefaultStyleId))
        {
            results.Add(layer.DefaultStyleId);
        }

        foreach (var styleId in layer.StyleIds)
        {
            if (styleId.HasValue() && seen.Add(styleId))
            {
                results.Add(styleId);
            }
        }

        return results.Count == 0 ? Array.Empty<string>() : results;
    }

    internal static bool WantsHtml(HttpRequest request)
    {
        if (request.Query.TryGetValue("f", out var formatValues))
        {
            var formatValue = formatValues.ToString();
            if (string.Equals(formatValue, "html", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (formatValue.HasValue())
            {
                return false;
            }
        }

        if (request.Headers.TryGetValue(HeaderNames.Accept, out var acceptValues) &&
            MediaTypeHeaderValue.TryParseList(acceptValues, out var parsedAccepts))
        {
            var ordered = parsedAccepts
                .OrderByDescending(value => value.Quality ?? 1.0)
                .ToList();

            foreach (var media in ordered)
            {
                var mediaType = media.MediaType.ToString();
                if (mediaType.IsNullOrWhiteSpace())
                {
                    continue;
                }

                if (string.Equals(mediaType, HtmlMediaType, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(mediaType, "application/xhtml+xml", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!string.Equals(mediaType, "*/*", StringComparison.Ordinal))
                {
                    return false;
                }
            }
        }

        return false;
    }

    internal static string RenderLandingHtml(HttpRequest request, MetadataSnapshot snapshot, IReadOnlyList<OgcLink> links)
    {
        return RenderHtmlDocument(snapshot.Catalog.Title ?? "OGC API", body =>
        {
            body.Append("<h1>").Append(HtmlEncode(snapshot.Catalog.Title ?? snapshot.Catalog.Id)).AppendLine("</h1>");
            if (snapshot.Catalog.Description.HasValue())
            {
                body.Append("<p>").Append(HtmlEncode(snapshot.Catalog.Description)).AppendLine("</p>");
            }

            body.Append("<p><strong>Catalog ID:</strong> ")
                .Append(HtmlEncode(snapshot.Catalog.Id))
                .AppendLine("</p>");

            AppendLinksHtml(body, links);

            if (snapshot.Services.Count > 0)
            {
                body.AppendLine("<h2>Services</h2>");
                body.AppendLine("<ul>");
                foreach (var service in snapshot.Services.OrderBy(s => s.Title ?? s.Id, StringComparer.OrdinalIgnoreCase))
                {
                    body.Append("  <li><strong>")
                        .Append(HtmlEncode(service.Title ?? service.Id))
                        .Append("</strong> (")
                        .Append(HtmlEncode(service.Id))
                        .Append(")<br/><span class=\"meta\">")
                        .Append(HtmlEncode(service.ServiceType ?? ""))
                        .AppendLine("</span></li>");
                }

                body.AppendLine("</ul>");
            }
        });
    }

    internal static string RenderCollectionsHtml(HttpRequest request, MetadataSnapshot snapshot, IReadOnlyList<CollectionSummary> collections)
    {
        return RenderHtmlDocument("Collections", body =>
        {
            body.Append("<h1>Collections</h1>");
            body.Append("<p><a href=\"")
                .Append(HtmlEncode(BuildHref(request, "/ogc", null, null)))
                .AppendLine("\">Back to landing</a></p>");

            if (collections.Count == 0)
            {
                body.AppendLine("<p>No collections are published.</p>");
                return;
            }

            body.AppendLine("<table><thead><tr><th>ID</th><th>Title</th><th>Description</th><th>Item Type</th><th>CRS</th></tr></thead><tbody>");
            foreach (var collection in collections)
            {
                body.Append("<tr><td>")
                    .Append(HtmlEncode(collection.Id))
                    .Append("</td><td>")
                    .Append(HtmlEncode(collection.Title ?? collection.Id))
                    .Append("</td><td>")
                    .Append(HtmlEncode(collection.Description))
                    .Append("</td><td>")
                    .Append(HtmlEncode(collection.ItemType))
                    .Append("</td><td>")
                    .Append(HtmlEncode(string.Join(", ", collection.Crs)))
                    .AppendLine("</td></tr>");
            }

            body.AppendLine("</tbody></table>");
        });
    }

    internal static string RenderCollectionHtml(
        HttpRequest request,
        ServiceDefinition service,
        LayerDefinition layer,
        string collectionId,
        IReadOnlyList<string> crs,
        IReadOnlyList<OgcLink> links)
    {
        return RenderHtmlDocument(layer.Title ?? collectionId, body =>
        {
            body.Append("<h1>")
                .Append(HtmlEncode(layer.Title ?? collectionId))
                .AppendLine("</h1>");

            if (layer.Description.HasValue())
            {
                body.Append("<p>")
                    .Append(HtmlEncode(layer.Description))
                    .AppendLine("</p>");
            }

            body.AppendLine("<table><tbody>");
            AppendMetadataRow(body, "Collection ID", collectionId);
            AppendMetadataRow(body, "Service", service.Title ?? service.Id);
            AppendMetadataRow(body, "Item Type", layer.ItemType);
            AppendMetadataRow(body, "Storage CRS", DetermineStorageCrs(layer));
            AppendMetadataRow(body, "Supported CRS", string.Join(", ", crs));
            AppendMetadataRow(body, "Default Style", layer.DefaultStyleId);
            AppendMetadataRow(body, "Styles", string.Join(", ", BuildOrderedStyleIds(layer)));
            body.AppendLine("</tbody></table>");

            AppendLinksHtml(body, links);

            body.Append("<p><a href=\"")
                .Append(HtmlEncode(BuildHref(request, "/ogc/collections", null, null)))
                .AppendLine("\">Back to collections</a></p>");
        });
    }

    internal static string RenderFeatureCollectionHtml(
        string title,
        string? subtitle,
        IReadOnlyList<HtmlFeatureEntry> features,
        long? numberMatched,
        long numberReturned,
        string? contentCrs,
        IReadOnlyList<OgcLink> links,
        bool hitsOnly)
    {
        return RenderHtmlDocument(title, body =>
        {
            body.Append("<h1>").Append(HtmlEncode(title)).AppendLine("</h1>");
            if (subtitle.HasValue())
            {
                body.Append("<p>").Append(HtmlEncode(subtitle)).AppendLine("</p>");
            }

            var matchedDisplay = numberMatched.HasValue
                ? numberMatched.Value.ToString(CultureInfo.InvariantCulture)
                : "unknown";

            body.Append("<p><strong>Number matched:</strong> ")
                .Append(HtmlEncode(matchedDisplay))
                .Append(" &nbsp; <strong>Number returned:</strong> ")
                .Append(HtmlEncode(numberReturned.ToString(CultureInfo.InvariantCulture)))
                .AppendLine("</p>");

            if (contentCrs.HasValue())
            {
                body.Append("<p><strong>Content CRS:</strong> ")
                    .Append(HtmlEncode(contentCrs))
                    .AppendLine("</p>");
            }

            AppendLinksHtml(body, links);

            if (hitsOnly)
            {
                body.AppendLine("<p>Result type is <code>hits</code>; no features are returned.</p>");
                return;
            }

            if (features.Count == 0)
            {
                body.AppendLine("<p>No features found.</p>");
                return;
            }

            foreach (var entry in features)
            {
                var displayName = entry.Components.DisplayName ?? entry.Components.FeatureId;
                body.Append("<details open><summary>")
                    .Append(HtmlEncode(displayName ?? entry.Components.FeatureId))
                    .Append("</summary>");

                if (entry.CollectionTitle.HasValue())
                {
                    body.Append("<p><strong>Collection:</strong> ")
                        .Append(HtmlEncode(entry.CollectionTitle))
                        .AppendLine("</p>");
                }

                AppendFeaturePropertiesTable(body, entry.Components.Properties);
                AppendGeometrySection(body, entry.Components.Geometry);
                body.AppendLine("</details>");
            }
        });
    }

    private static async Task WriteGeoJsonSearchResponseAsync(
        Stream outputStream,
        HttpRequest request,
        IReadOnlyList<string> collections,
        string contentType,
        FeatureQuery baseQuery,
        IReadOnlyList<(SearchCollectionContext Context, FeatureQuery Query, string ContentCrs)> preparedQueries,
        bool includeCount,
        FeatureResultType resultType,
        IFeatureRepository repository,
        long initialLimit,
        long initialOffset,
        CancellationToken cancellationToken)
    {
        await using var writer = new Utf8JsonWriter(outputStream, new JsonWriterOptions { SkipValidation = false });

        writer.WriteStartObject();
        writer.WriteString("type", "FeatureCollection");
        writer.WriteString("timeStamp", DateTimeOffset.UtcNow);
        writer.WritePropertyName("features");
        writer.WriteStartArray();
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

        var iterationResult = await EnumerateSearchAsync(
            preparedQueries,
            resultType,
            includeCount,
            initialLimit,
            initialOffset,
            repository,
            async (context, layer, query, record, components) =>
            {
                var feature = ToFeature(request, context.CollectionId, layer, record, query, components);
                JsonSerializer.Serialize(writer, feature, GeoJsonSerializerOptions);

                if (writer.BytesPending > 8192)
                {
                    await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                return true;
            },
            cancellationToken).ConfigureAwait(false);

        writer.WriteEndArray();

        var matched = iterationResult.NumberMatched ?? iterationResult.NumberReturned;
        writer.WriteNumber("numberMatched", matched);
        writer.WriteNumber("numberReturned", iterationResult.NumberReturned);

        var links = BuildSearchLinks(request, collections, baseQuery, iterationResult.NumberMatched, contentType);
        writer.WritePropertyName("links");
        JsonSerializer.Serialize(writer, links, GeoJsonSerializerOptions);

        writer.WriteEndObject();
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<SearchIterationResult> EnumerateSearchAsync(
        IReadOnlyList<(SearchCollectionContext Context, FeatureQuery Query, string ContentCrs)> preparedQueries,
        FeatureResultType resultType,
        bool includeCount,
        long initialLimit,
        long initialOffset,
        IFeatureRepository repository,
        Func<SearchCollectionContext, LayerDefinition, FeatureQuery, FeatureRecord, FeatureComponents, ValueTask<bool>> onFeature,
        CancellationToken cancellationToken)
    {
        long? numberMatchedTotal = includeCount ? 0L : null;
        long numberReturnedTotal = 0;
        var remainingLimit = initialLimit;
        var remainingOffset = initialOffset;
        var enforceLimit = remainingLimit != long.MaxValue;

        if (resultType == FeatureResultType.Hits)
        {
            foreach (var prepared in preparedQueries)
            {
                var service = prepared.Context.FeatureContext.Service;
                var layer = prepared.Context.FeatureContext.Layer;
                var query = prepared.Query;
                var matched = await repository.CountAsync(service.Id, layer.Id, query, cancellationToken).ConfigureAwait(false);
                numberMatchedTotal += matched;
            }

            return new SearchIterationResult(numberMatchedTotal, 0);
        }

        foreach (var prepared in preparedQueries)
        {
            var service = prepared.Context.FeatureContext.Service;
            var layer = prepared.Context.FeatureContext.Layer;
            var query = prepared.Query;

            if (includeCount)
            {
                var matched = await repository.CountAsync(service.Id, layer.Id, query, cancellationToken).ConfigureAwait(false);
                numberMatchedTotal += matched;

                long skip = Math.Min(remainingOffset, matched);
                remainingOffset -= skip;

                var available = matched - skip;
                if (available <= 0)
                {
                    continue;
                }

                var allowed = enforceLimit ? Math.Min(available, remainingLimit) : available;
                if (allowed <= 0)
                {
                    continue;
                }

                var adjustedQuery = query with
                {
                    Offset = (int)Math.Min(skip, int.MaxValue),
                    Limit = (int)Math.Min(allowed, int.MaxValue)
                };

                await foreach (var record in repository.QueryAsync(service.Id, layer.Id, adjustedQuery, cancellationToken).ConfigureAwait(false))
                {
                    var components = FeatureComponentBuilder.BuildComponents(layer, record, adjustedQuery);
                    var shouldContinue = await onFeature(prepared.Context, layer, adjustedQuery, record, components).ConfigureAwait(false);
                    numberReturnedTotal++;

                    if (enforceLimit)
                    {
                        remainingLimit--;
                        if (remainingLimit <= 0)
                        {
                            return new SearchIterationResult(numberMatchedTotal, numberReturnedTotal);
                        }
                    }

                    if (!shouldContinue)
                    {
                        return new SearchIterationResult(numberMatchedTotal, numberReturnedTotal);
                    }
                }

                continue;
            }

            var streamingQuery = query with
            {
                Offset = null,
                Limit = query.Limit
            };

            await foreach (var record in repository.QueryAsync(service.Id, layer.Id, streamingQuery, cancellationToken).ConfigureAwait(false))
            {
                if (remainingOffset > 0)
                {
                    remainingOffset--;
                    continue;
                }

                var components = FeatureComponentBuilder.BuildComponents(layer, record, streamingQuery);
                var shouldContinue = await onFeature(prepared.Context, layer, streamingQuery, record, components).ConfigureAwait(false);
                numberReturnedTotal++;

                if (enforceLimit)
                {
                    remainingLimit--;
                    if (remainingLimit <= 0)
                    {
                        return new SearchIterationResult(numberMatchedTotal, numberReturnedTotal);
                    }
                }

                if (!shouldContinue)
                {
                    return new SearchIterationResult(numberMatchedTotal, numberReturnedTotal);
                }
            }
        }

        return new SearchIterationResult(numberMatchedTotal, numberReturnedTotal);
    }

    private sealed record SearchIterationResult(long? NumberMatched, long NumberReturned);

    internal static string RenderFeatureHtml(
        string title,
        string? description,
        HtmlFeatureEntry entry,
        string? contentCrs,
        IReadOnlyList<OgcLink> links)
    {
        return RenderHtmlDocument(title, body =>
        {
            body.Append("<h1>").Append(HtmlEncode(title)).AppendLine("</h1>");
            if (description.HasValue())
            {
                body.Append("<p>").Append(HtmlEncode(description)).AppendLine("</p>");
            }

            AppendFeaturePropertiesTable(body, entry.Components.Properties);
            AppendGeometrySection(body, entry.Components.Geometry);

            if (contentCrs.HasValue())
            {
                body.Append("<p><strong>Content CRS:</strong> ")
                    .Append(HtmlEncode(contentCrs))
                    .AppendLine("</p>");
            }

            AppendLinksHtml(body, links);
        });
    }

    private static void AppendLinksHtml(StringBuilder builder, IReadOnlyList<OgcLink> links)
    {
        if (links.Count == 0)
        {
            return;
        }

        builder.AppendLine("<h2>Links</h2>");
        builder.AppendLine("<ul>");
        foreach (var link in links)
        {
            builder.Append("  <li><a href=\"")
                .Append(HtmlEncode(link.Href))
                .Append("\">")
                .Append(HtmlEncode(link.Title ?? link.Rel))
                .Append("</a> <span class=\"meta\">(")
                .Append(HtmlEncode(link.Rel))
                .Append(")")
                .Append(link.Type.IsNullOrWhiteSpace() ? string.Empty : $", {HtmlEncode(link.Type)}")
                .AppendLine("</span></li>");
        }
        builder.AppendLine("</ul>");
    }

    private static void AppendFeaturePropertiesTable(StringBuilder builder, IReadOnlyDictionary<string, object?> properties)
    {
        builder.AppendLine("<h3>Properties</h3>");
        builder.AppendLine("<table><tbody>");
        foreach (var pair in properties.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("<tr><th>")
                .Append(HtmlEncode(pair.Key))
                .Append("</th><td>")
                .Append(HtmlEncode(FormatPropertyValue(pair.Value)))
                .AppendLine("</td></tr>");
        }
        builder.AppendLine("</tbody></table>");
    }

    private static void AppendGeometrySection(StringBuilder builder, object? geometry)
    {
        var geometryText = FormatGeometryValue(geometry);
        if (geometryText.IsNullOrWhiteSpace())
        {
            return;
        }

        builder.AppendLine("<h3>Geometry</h3>");
        builder.Append("<pre>")
            .Append(HtmlEncode(geometryText))
            .AppendLine("</pre>");
    }

    private static void AppendMetadataRow(StringBuilder builder, string label, string? value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return;
        }

        builder.Append("<tr><th>")
            .Append(HtmlEncode(label))
            .Append("</th><td>")
            .Append(HtmlEncode(value))
            .AppendLine("</td></tr>");
    }

    private static string RenderHtmlDocument(string title, Action<StringBuilder> bodyWriter)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"en\">");
        builder.AppendLine("<head>");
        builder.AppendLine("<meta charset=\"utf-8\" />");
        builder.Append("<title>").Append(HtmlEncode(title)).AppendLine("</title>");
        builder.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;margin:1.5rem;}table{border-collapse:collapse;margin-bottom:1rem;}th,td{border:1px solid #ccc;padding:0.35rem 0.6rem;text-align:left;}th{background:#f5f5f5;}details{margin-bottom:1rem;}summary{font-weight:600;}code{font-family:Consolas,Menlo,monospace;}pre{background:#f5f5f5;padding:0.75rem;overflow:auto;}ul{list-style:disc;margin-left:1.5rem;} .meta{color:#555;font-size:0.9em;}</style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        bodyWriter(builder);
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");
        return builder.ToString();
    }

    private static string HtmlEncode(string? value)
        => WebUtility.HtmlEncode(value ?? string.Empty);

    internal static string FormatPropertyValue(object? value)
    {
        switch (value)
        {
            case null:
                return string.Empty;
            case string text:
                return text;
            case bool b:
                return b ? "true" : "false";
            case JsonNode node:
                return node.ToJsonString(HtmlJsonOptions);
            case JsonElement element:
                return element.ValueKind == JsonValueKind.String
                    ? element.GetString() ?? string.Empty
                    : element.GetRawText();
            case byte[] bytes:
                return $"[binary: {bytes.Length} bytes]";
            case IEnumerable enumerable when value is not string:
                try
                {
                    return JsonSerializer.Serialize(enumerable, HtmlJsonOptions);
                }
                catch
                {
                    return value.ToString() ?? string.Empty;
                }
            case IFormattable formattable:
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            default:
                try
                {
                    return JsonSerializer.Serialize(value, HtmlJsonOptions);
                }
                catch
                {
                    return value.ToString() ?? string.Empty;
                }
        }
    }

    internal static string? FormatGeometryValue(object? geometry)
    {
        return geometry switch
        {
            null => null,
            JsonNode node => node.ToJsonString(HtmlJsonOptions),
            JsonElement element => element.GetRawText(),
            string text => text,
            _ => FormatPropertyValue(geometry)
        };
    }

    private static readonly JsonSerializerOptions HtmlJsonOptions = new(JsonSerializerDefaults.Web);

    internal static List<OgcLink> BuildItemsLinks(HttpRequest request, string collectionId, FeatureQuery query, long? numberMatched, OgcResponseFormat format, string contentType)
    {
        var basePath = $"/ogc/collections/{collectionId}/items";
        var links = new List<OgcLink>
        {
            BuildLink(request, basePath, "self", contentType, "This page", query),
            BuildLink(request, $"/ogc/collections/{collectionId}", "collection", "application/json", "Collection"),
            BuildLink(request, basePath, "alternate", "application/vnd.google-earth.kml+xml", "KML items", query, new Dictionary<string, string?> { ["f"] = "kml" }),
            BuildLink(request, basePath, "alternate", "application/vnd.google-earth.kmz", "KMZ items", query, new Dictionary<string, string?> { ["f"] = "kmz" }),
            BuildLink(request, basePath, "alternate", "application/topo+json", "TopoJSON items", query, new Dictionary<string, string?> { ["f"] = "topojson" }),
            BuildLink(request, basePath, "alternate", "application/geopackage+sqlite3", "GeoPackage items", query, new Dictionary<string, string?> { ["f"] = "geopackage" }),
            BuildLink(request, basePath, "alternate", "application/zip", "Shapefile items", query, new Dictionary<string, string?> { ["f"] = "shapefile" }),
            BuildLink(request, basePath, "alternate", "application/vnd.flatgeobuf", "FlatGeobuf items", query, new Dictionary<string, string?> { ["f"] = "flatgeobuf" }),
            BuildLink(request, basePath, "alternate", "application/vnd.apache.arrow.stream", "GeoArrow items", query, new Dictionary<string, string?> { ["f"] = "geoarrow" }),
            BuildLink(request, basePath, "alternate", "text/csv", "CSV items", query, new Dictionary<string, string?> { ["f"] = "csv" }),
            BuildLink(request, basePath, "alternate", "application/ld+json", "JSON-LD items", query, new Dictionary<string, string?> { ["f"] = "jsonld" }),
            BuildLink(request, basePath, "alternate", "application/geo+json-t", "GeoJSON-T items", query, new Dictionary<string, string?> { ["f"] = "geojson-t" })
        };

        var limit = query.Limit ?? 0;
        var offset = query.Offset ?? 0;

        if (query.ResultType != FeatureResultType.Hits)
        {
            // Use PaginationHelper for next/prev offset calculations
            if (limit > 0 && numberMatched.HasValue)
            {
                // BUG FIX #12: Clamp remaining to non-negative to prevent ArgumentOutOfRangeException
                // when offset exceeds numberMatched (out-of-range pagination should return empty page)
                var remaining = (int)Math.Max(0, numberMatched.Value - offset);
                var nextOffset = PaginationHelper.CalculateNextOffset(offset, limit, Math.Min(remaining, limit));
                if (nextOffset.HasValue && nextOffset.Value < numberMatched.Value)
                {
                    var nextParameters = new Dictionary<string, string?>
                    {
                        ["offset"] = nextOffset.Value.ToString(CultureInfo.InvariantCulture),
                        ["limit"] = limit.ToString(CultureInfo.InvariantCulture)
                    };
                    links.Add(BuildLink(request, basePath, "next", contentType, "Next page", query, nextParameters));
                }
            }

            if (limit > 0 && PaginationHelper.HasPrevPage(offset))
            {
                var prevOffset = PaginationHelper.CalculatePrevOffset(offset, limit)!.Value;
                var prevParameters = new Dictionary<string, string?>
                {
                    ["offset"] = prevOffset.ToString(CultureInfo.InvariantCulture),
                    ["limit"] = limit.ToString(CultureInfo.InvariantCulture)
                };
                links.Add(BuildLink(request, basePath, "prev", contentType, "Previous page", query, prevParameters));
            }
        }

        return links;
    }

internal static List<OgcLink> BuildSearchLinks(HttpRequest request, IReadOnlyList<string> collections, FeatureQuery query, long? numberMatched, string contentType)
    {
        var overrides = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["collections"] = string.Join(',', collections)
        };

        var links = new List<OgcLink>
        {
            BuildLink(request, "/ogc/search", "self", contentType, "This page", query, overrides),
            BuildLink(request, "/ogc/collections", "data", "application/json", "Collections")
        };

        if (query.ResultType != FeatureResultType.Hits)
        {
            var limit = query.Limit ?? 0;
            var offset = query.Offset ?? 0;

            // Use PaginationHelper for next/prev offset calculations
            if (limit > 0 && numberMatched.HasValue)
            {
                // BUG FIX #13: Clamp remaining to non-negative to prevent ArgumentOutOfRangeException
                // when offset exceeds numberMatched in /ogc/search (should return empty page, not 500)
                var remaining = (int)Math.Max(0, numberMatched.Value - offset);
                var nextOffset = PaginationHelper.CalculateNextOffset(offset, limit, Math.Min(remaining, limit));
                if (nextOffset.HasValue && nextOffset.Value < numberMatched.Value)
                {
                    var nextOverrides = new Dictionary<string, string?>(overrides)
                    {
                        ["offset"] = nextOffset.Value.ToString(CultureInfo.InvariantCulture),
                        ["limit"] = limit.ToString(CultureInfo.InvariantCulture)
                    };
                    links.Add(BuildLink(request, "/ogc/search", "next", contentType, "Next page", query, nextOverrides));
                }
            }

            if (limit > 0 && PaginationHelper.HasPrevPage(offset))
            {
                var prevOffset = PaginationHelper.CalculatePrevOffset(offset, limit)!.Value;
                var prevOverrides = new Dictionary<string, string?>(overrides)
                {
                    ["offset"] = prevOffset.ToString(CultureInfo.InvariantCulture),
                    ["limit"] = limit.ToString(CultureInfo.InvariantCulture)
                };
                links.Add(BuildLink(request, "/ogc/search", "prev", contentType, "Previous page", query, prevOverrides));
            }
        }

        return links;
    }
    internal static string GetMimeType(OgcResponseFormat format)
        => format switch
        {
            OgcResponseFormat.Html => HtmlMediaType,
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
    internal static IResult WithResponseHeader(IResult result, string headerName, string headerValue)
        => new HeaderResult(result, headerName, headerValue);

    internal static string FormatContentCrs(string? value)
        => value.IsNullOrWhiteSpace() ? string.Empty : $"<{value}>";

    /// <summary>
    /// Adds a Content-Crs header to the result with proper formatting.
    /// This consolidates the common pattern of calling WithResponseHeader + FormatContentCrs.
    /// </summary>
    internal static IResult WithContentCrsHeader(IResult result, string? contentCrs)
        => WithResponseHeader(result, "Content-Crs", FormatContentCrs(contentCrs));

    internal static int ResolveTileSize(HttpRequest request)
    {
        if (request.Query.TryGetValue("tileSize", out var sizeValues)
            && sizeValues.ToString().TryParseInt(out var parsed)
            && parsed > 0
            && parsed <= 2048)
        {
            return parsed;
        }

        return 256;
    }

    internal static string ResolveTileFormat(HttpRequest request)
    {
        string? raw = null;
        if (request.Query.TryGetValue("format", out var formatValues))
        {
            raw = formatValues.ToString();
        }
        else if (request.Query.TryGetValue("f", out var alternateValues))
        {
            raw = alternateValues.ToString();
        }

        return RasterFormatHelper.Normalize(raw);
    }

    internal static IReadOnlyList<object> BuildTileMatrixSetLinks(HttpRequest request, string collectionId, string tilesetId)
    {
        return new object[]
        {
            new
            {
                tileMatrixSet = OgcTileMatrixHelper.WorldCrs84QuadId,
                tileMatrixSetUri = OgcTileMatrixHelper.WorldCrs84QuadUri,
                crs = OgcTileMatrixHelper.WorldCrs84QuadCrs,
                href = BuildHref(request, $"/ogc/collections/{collectionId}/tiles/{tilesetId}/{OgcTileMatrixHelper.WorldCrs84QuadId}", null, null)
            },
            new
            {
                tileMatrixSet = OgcTileMatrixHelper.WorldWebMercatorQuadId,
                tileMatrixSetUri = OgcTileMatrixHelper.WorldWebMercatorQuadUri,
                crs = OgcTileMatrixHelper.WorldWebMercatorQuadCrs,
                href = BuildHref(request, $"/ogc/collections/{collectionId}/tiles/{tilesetId}/{OgcTileMatrixHelper.WorldWebMercatorQuadId}", null, null)
            }
        };
    }

    internal static object BuildTileMatrixSetSummary(HttpRequest request, string id, string uri, string crs)
    {
        return new
        {
            id,
            title = id,
            tileMatrixSetUri = uri,
            crs,
            links = new[]
            {
                BuildLink(request, $"/ogc/tileMatrixSets/{id}", "self", "application/json", $"{id} definition")
            }
        };
    }

    internal static bool DatasetMatchesCollection(RasterDatasetDefinition dataset, ServiceDefinition service, LayerDefinition layer)
    {
        if (dataset.ServiceId.IsNullOrWhiteSpace() || !string.Equals(dataset.ServiceId, service.Id, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (dataset.LayerId.HasValue() && !string.Equals(dataset.LayerId, layer.Id, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    internal static (string Id, string Uri, string Crs)? NormalizeTileMatrixSet(string tileMatrixSetId)
    {
        if (OgcTileMatrixHelper.IsWorldCrs84Quad(tileMatrixSetId))
        {
            return (OgcTileMatrixHelper.WorldCrs84QuadId, OgcTileMatrixHelper.WorldCrs84QuadUri, OgcTileMatrixHelper.WorldCrs84QuadCrs);
        }

        if (OgcTileMatrixHelper.IsWorldWebMercatorQuad(tileMatrixSetId))
        {
            return (OgcTileMatrixHelper.WorldWebMercatorQuadId, OgcTileMatrixHelper.WorldWebMercatorQuadUri, OgcTileMatrixHelper.WorldWebMercatorQuadCrs);
        }

        return null;
    }

    internal static bool TryResolveStyle(RasterDatasetDefinition dataset, string? requestedStyleId, out string styleId, out string? unresolvedStyle)
    {
        var (success, resolvedStyleId, error) = StyleResolutionHelper.TryResolveRasterStyleId(dataset, requestedStyleId);
        styleId = resolvedStyleId ?? string.Empty;
        unresolvedStyle = success ? null : requestedStyleId;
        return success;
    }

    private sealed record SearchCollectionContext(string CollectionId, FeatureContext FeatureContext);

    internal static async Task<StyleDefinition?> ResolveStyleDefinitionAsync(
        RasterDatasetDefinition dataset,
        string? requestedStyleId,
        IMetadataRegistry metadataRegistry,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(dataset);
        Guard.NotNull(metadataRegistry);

        var snapshot = await metadataRegistry.GetInitializedSnapshotAsync(cancellationToken).ConfigureAwait(false);

        return StyleResolutionHelper.ResolveStyleForRaster(snapshot, dataset, requestedStyleId);
    }

    internal static async Task<StyleDefinition?> ResolveStyleDefinitionAsync(
        string? styleId,
        LayerDefinition layer,
        IMetadataRegistry metadataRegistry,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(layer);
        Guard.NotNull(metadataRegistry);

        var snapshot = await metadataRegistry.GetInitializedSnapshotAsync(cancellationToken).ConfigureAwait(false);

        return StyleResolutionHelper.ResolveStyleForLayer(snapshot, layer, styleId);
    }

internal static bool RequiresVectorOverlay(StyleDefinition? style)
{
    if (style?.GeometryType is not { } geometryType)
    {
        return false;
    }

    return !geometryType.Equals("raster", StringComparison.OrdinalIgnoreCase);
}

internal static async Task<IReadOnlyList<Geometry>> CollectVectorGeometriesAsync(
    RasterDatasetDefinition dataset,
    double[] bbox,
    IMetadataRegistry metadataRegistry,
    IFeatureRepository repository,
    CancellationToken cancellationToken)
{
    Guard.NotNull(metadataRegistry);

    await metadataRegistry.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
    var snapshot = await metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

    return await CollectVectorGeometriesAsync(dataset, bbox, snapshot, repository, cancellationToken).ConfigureAwait(false);
}

internal static async Task<IReadOnlyList<Geometry>> CollectVectorGeometriesAsync(
    RasterDatasetDefinition dataset,
    double[] bbox,
    MetadataSnapshot snapshot,
    IFeatureRepository repository,
    CancellationToken cancellationToken)
{
    if (bbox.Length < 4)
    {
        return Array.Empty<Geometry>();
    }

    if (dataset.ServiceId.IsNullOrWhiteSpace())
    {
        return Array.Empty<Geometry>();
    }

    var service = snapshot.Services.FirstOrDefault(s =>
        string.Equals(s.Id, dataset.ServiceId, StringComparison.OrdinalIgnoreCase));
    if (service is null || service.Layers.IsNullOrEmpty())
    {
        return Array.Empty<Geometry>();
    }

    LayerDefinition? targetLayer = null;
    if (dataset.LayerId.HasValue())
    {
        targetLayer = service.Layers.FirstOrDefault(l =>
            string.Equals(l.Id, dataset.LayerId, StringComparison.OrdinalIgnoreCase));
    }

    targetLayer ??= service.Layers[0];
    if (targetLayer is null)
    {
        return Array.Empty<Geometry>();
    }

    var queryBbox = new BoundingBox(bbox[0], bbox[1], bbox[2], bbox[3]);
    var targetCrs = dataset.Crs.FirstOrDefault() ?? service.Ogc.DefaultCrs;
    var geometries = new List<Geometry>();
    var reader = new GeoJsonReader();
    var offset = 0;
    var reachedLimit = false;

    while (!reachedLimit)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var pagedQuery = new FeatureQuery(
            Limit: OverlayFetchBatchSize,
            Offset: offset,
            Bbox: queryBbox,
            ResultType: FeatureResultType.Results,
            Crs: targetCrs);

        var batchCount = 0;

        await foreach (var record in repository.QueryAsync(service.Id, targetLayer.Id, pagedQuery, cancellationToken).ConfigureAwait(false))
        {
            batchCount++;

            try
            {
                var components = FeatureComponentBuilder.BuildComponents(targetLayer, record, pagedQuery);
                if (components.GeometryNode is null)
                {
                    continue;
                }

                var geometry = reader.Read<Geometry>(components.GeometryNode.ToJsonString());
                if (geometry is not null && !geometry.IsEmpty)
                {
                    geometries.Add(geometry);
                    if (geometries.Count >= OverlayFetchMaxFeatures)
                    {
                        reachedLimit = true;
                        break;
                    }
                }
            }
            catch
            {
                // Ignore invalid geometries.
            }
        }

        if (reachedLimit || batchCount < OverlayFetchBatchSize)
        {
            break;
        }

        offset += OverlayFetchBatchSize;
    }

    return geometries.Count == 0
        ? Array.Empty<Geometry>()
        : new ReadOnlyCollection<Geometry>(geometries);
}

    internal static async Task<IResult> RenderVectorTileAsync(
        ServiceDefinition service,
        LayerDefinition layer,
        RasterDatasetDefinition dataset,
        double[] bbox,
        int zoom,
        int tileRow,
        int tileCol,
        string? datetime,
        IFeatureRepository repository,
        CancellationToken cancellationToken)
    {
        if (dataset.ServiceId.IsNullOrWhiteSpace() || dataset.LayerId.IsNullOrWhiteSpace())
        {
            return Results.File(Array.Empty<byte>(), "application/vnd.mapbox-vector-tile");
        }

        // BUG FIX #7: Check if provider supports MVT generation and return 501 if not
        var mvtBytes = await repository.GenerateMvtTileAsync(dataset.ServiceId, dataset.LayerId, zoom, tileCol, tileRow, datetime, cancellationToken).ConfigureAwait(false);
        if (mvtBytes is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status501NotImplemented,
                title: "Vector tiles not supported",
                detail: $"The data provider for dataset '{dataset.Id}' does not support native MVT tile generation.");
        }

        return Results.File(mvtBytes, "application/vnd.mapbox-vector-tile");
    }

    internal static OgcLink BuildLink(HttpRequest request, string relativePath, string rel, string type, string? title, FeatureQuery? query = null, IDictionary<string, string?>? overrides = null)
    {
        var href = BuildHref(request, relativePath, query, overrides);
        return new OgcLink(href, rel, type, title);
    }

    internal static OgcLink ToLink(LinkDefinition link)
    {
        return new OgcLink(
            link.Href,
            link.Rel.IsNullOrWhiteSpace() ? "related" : link.Rel,
            link.Type,
            link.Title
        );
    }

    /// <summary>
    /// Builds an HREF with query parameters using RequestLinkHelper for consistent URL generation.
    /// Respects proxy headers (X-Forwarded-Proto, X-Forwarded-Host) and handles query parameter merging.
    /// Automatically detects and preserves the API version prefix (/v1, /v2, etc.) from the incoming request path.
    /// </summary>
    internal static string BuildHref(HttpRequest request, string relativePath, FeatureQuery? query, IDictionary<string, string?>? overrides)
    {
        // BUG FIX: Preserve API version prefix in OGC links
        // OGC endpoints are available at both /ogc and /v1/ogc
        // We need to detect which one was used and preserve it in generated links
        var requestPath = request.Path.Value ?? string.Empty;
        var versionedPath = relativePath;

        // Check if the request came through a versioned endpoint (e.g., /v1/ogc)
        if (requestPath.StartsWith("/v", StringComparison.OrdinalIgnoreCase))
        {
            var firstSegmentEnd = requestPath.IndexOf('/', 1);
            if (firstSegmentEnd > 0)
            {
                var versionPrefix = requestPath.Substring(0, firstSegmentEnd); // e.g., "/v1"

                // Only add version prefix if relativePath doesn't already have it
                if (!relativePath.StartsWith(versionPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    versionedPath = versionPrefix + relativePath;
                }
            }
        }

        var queryParameters = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (query is not null)
        {
            if (query.Limit.HasValue)
            {
                queryParameters["limit"] = query.Limit.Value.ToString(CultureInfo.InvariantCulture);
            }
            if (query.Offset.HasValue)
            {
                queryParameters["offset"] = query.Offset.Value.ToString(CultureInfo.InvariantCulture);
            }
            if (query.Crs.HasValue())
            {
                queryParameters["crs"] = query.Crs;
            }

            // BUG FIX #14: Pagination links drop bbox/temporal filters
            // BUG FIX #15: CQL filter context is lost in OGC links
            // Carry forward all active filters (bbox, datetime, filter, property selections) when constructing navigation links
            if (query.Bbox is not null)
            {
                var bbox = query.Bbox;
                var bboxValue = bbox.MinZ.HasValue && bbox.MaxZ.HasValue
                    ? $"{bbox.MinX:G17},{bbox.MinY:G17},{bbox.MinZ:G17},{bbox.MaxX:G17},{bbox.MaxY:G17},{bbox.MaxZ:G17}"
                    : $"{bbox.MinX:G17},{bbox.MinY:G17},{bbox.MaxX:G17},{bbox.MaxY:G17}";
                queryParameters["bbox"] = bboxValue;

                if (bbox.Crs.HasValue())
                {
                    queryParameters["bbox-crs"] = bbox.Crs;
                }
            }

            if (query.Temporal is not null)
            {
                var temporal = query.Temporal;
                var start = temporal.Start?.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) ?? "..";
                var end = temporal.End?.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) ?? "..";
                queryParameters["datetime"] = $"{start}/{end}";
            }

            // BUG FIX #15: Preserve CQL filter context in pagination links
            // Note: We only preserve the filter expression string.  The original filter-lang and filter-crs
            // are not stored in FeatureQuery (only the parsed QueryExpression), so they cannot be reconstructed here.
            // Callers should pass filter-lang and filter-crs via overrides parameter if they need to be preserved.
            if (query.Filter is not null)
            {
                // Serialize the parsed filter expression back to string
                // This won't be identical to the original filter string, but preserves the predicate logic
                var filterStr = query.Filter.Expression?.ToString();
                if (!string.IsNullOrWhiteSpace(filterStr))
                {
                    queryParameters["filter"] = filterStr;
                }
            }

            // BUG FIX #15: Preserve property selections in pagination links
            if (query.PropertyNames is not null && query.PropertyNames.Count > 0)
            {
                queryParameters["properties"] = string.Join(',', query.PropertyNames);
            }

            if (query.ResultType == FeatureResultType.Hits)
            {
                queryParameters["resultType"] = "hits";
            }
        }

        // Apply overrides (null value = remove parameter)
        if (overrides is not null)
        {
            foreach (var kvp in overrides)
            {
                if (kvp.Value is null)
                {
                    queryParameters.Remove(kvp.Key);
                }
                else
                {
                    queryParameters[kvp.Key] = kvp.Value;
                }
            }
        }

        // Use RequestLinkHelper for consistent URL generation with proxy header support
        // Use versionedPath to preserve the version prefix from the incoming request
        return request.BuildAbsoluteUrl(versionedPath, queryParameters);
    }

    private static string? BuildAttachmentHref(
        HttpRequest request,
        ServiceDefinition service,
        LayerDefinition layer,
        string collectionId,
        FeatureComponents components,
        AttachmentDescriptor descriptor)
    {
        if (descriptor is null)
        {
            return null;
        }

        var featureId = descriptor.FeatureId.HasValue()
            ? descriptor.FeatureId
            : components.FeatureId;

        if (featureId.HasValue() && descriptor.AttachmentObjectId > 0)
        {
            var layerIndex = ResolveLayerIndex(service, layer);
            if (layerIndex >= 0 &&
                long.TryParse(featureId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var objectId))
            {
                return AttachmentUrlBuilder.BuildGeoServicesUrl(
                    request,
                    service.FolderId,
                    service.Id,
                    layerIndex,
                    objectId,
                    descriptor.AttachmentObjectId,
                    includeRootFolder: true);
            }
        }

        if (featureId.IsNullOrWhiteSpace() || descriptor.AttachmentId.IsNullOrWhiteSpace())
        {
            return null;
        }

        return AttachmentUrlBuilder.BuildOgcUrl(request, collectionId, featureId!, descriptor.AttachmentId);
    }

    internal static object? ConvertExtent(LayerExtentDefinition? extent)
    {
        if (extent is null)
        {
            return null;
        }

        object? spatial = null;
        if (extent.Bbox.Count > 0 || extent.Crs.HasValue())
        {
            spatial = new
            {
                bbox = extent.Bbox,
                crs = extent.Crs.IsNullOrWhiteSpace()
                    ? CrsHelper.DefaultCrsIdentifier
                    : CrsHelper.NormalizeIdentifier(extent.Crs)
            };
        }

        var hasIntervals = extent.Temporal.Count > 0;
        var intervals = hasIntervals
            ? extent.Temporal
                .Select(t => new[] { t.Start?.ToString("O"), t.End?.ToString("O") })
                .ToArray()
            : Array.Empty<string?[]>();

        object? temporal = null;
        if (hasIntervals || extent.TemporalReferenceSystem.HasValue())
        {
            temporal = new
            {
                interval = intervals,
                trs = extent.TemporalReferenceSystem.IsNullOrWhiteSpace()
                    ? DefaultTemporalReferenceSystem
                    : extent.TemporalReferenceSystem
            };
        }

        if (spatial is null && temporal is null)
        {
            return null;
        }

        return new
        {
            spatial,
            temporal
        };
    }

    internal static object ToFeature(
        HttpRequest request,
        string collectionId,
        LayerDefinition layer,
        FeatureRecord record,
        FeatureQuery query,
        FeatureComponents? componentsOverride = null,
        IReadOnlyList<OgcLink>? additionalLinks = null)
    {
        var components = componentsOverride ?? FeatureComponentBuilder.BuildComponents(layer, record, query);

        var links = BuildFeatureLinks(request, collectionId, layer, components, additionalLinks);

        var properties = new Dictionary<string, object?>(components.Properties, StringComparer.OrdinalIgnoreCase);
        AppendStyleMetadata(properties, layer);

        return new
        {
            type = "Feature",
            id = components.RawId,
            geometry = components.Geometry,
            properties,
            links
        };
    }

    internal static IReadOnlyList<OgcLink> BuildFeatureLinks(
        HttpRequest request,
        string collectionId,
        LayerDefinition layer,
        FeatureComponents components,
        IReadOnlyList<OgcLink>? additionalLinks)
    {
        var links = new List<OgcLink>();
        if (components.FeatureId.HasValue())
        {
            links.Add(BuildLink(request, $"/ogc/collections/{collectionId}/items/{components.FeatureId}", "self", "application/geo+json", $"Feature {components.FeatureId}"));
        }

        links.Add(BuildLink(request, $"/ogc/collections/{collectionId}", "collection", "application/json", layer.Title));

        if (additionalLinks is not null)
        {
            links.AddRange(additionalLinks);
        }

        return links;
    }

    internal static bool ShouldExposeAttachmentLinks(ServiceDefinition service, LayerDefinition layer)
    {
        // Allow attachment links for both root-level and folder-based services
        // The FolderId check was preventing root collections from exposing attachment links
        return layer.Attachments.Enabled
            && layer.Attachments.ExposeOgcLinks;
    }

    internal static int ResolveLayerIndex(ServiceDefinition service, LayerDefinition layer)
    {
        if (service.Layers is null)
        {
            return -1;
        }

        for (var index = 0; index < service.Layers.Count; index++)
        {
            if (string.Equals(service.Layers[index].Id, layer.Id, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    internal static Task<IReadOnlyList<OgcLink>> CreateAttachmentLinksAsync(
        HttpRequest request,
        ServiceDefinition service,
        LayerDefinition layer,
        string collectionId,
        FeatureComponents components,
        IFeatureAttachmentOrchestrator attachmentOrchestrator,
        CancellationToken cancellationToken)
        => CreateAttachmentLinksCoreAsync(
            request,
            service,
            layer,
            collectionId,
            components,
            attachmentOrchestrator,
            preloadedDescriptors: null,
            cancellationToken);

    internal static Task<IReadOnlyList<OgcLink>> CreateAttachmentLinksAsync(
        HttpRequest request,
        ServiceDefinition service,
        LayerDefinition layer,
        string collectionId,
        FeatureComponents components,
        IFeatureAttachmentOrchestrator attachmentOrchestrator,
        IReadOnlyList<AttachmentDescriptor> preloadedDescriptors,
        CancellationToken cancellationToken)
        => CreateAttachmentLinksCoreAsync(
            request,
            service,
            layer,
            collectionId,
            components,
            attachmentOrchestrator,
            preloadedDescriptors,
            cancellationToken);

    private static async Task<IReadOnlyList<OgcLink>> CreateAttachmentLinksCoreAsync(
        HttpRequest request,
        ServiceDefinition service,
        LayerDefinition layer,
        string collectionId,
        FeatureComponents components,
        IFeatureAttachmentOrchestrator attachmentOrchestrator,
        IReadOnlyList<AttachmentDescriptor>? preloadedDescriptors,
        CancellationToken cancellationToken)
    {
        if (components.FeatureId.IsNullOrWhiteSpace())
        {
            return Array.Empty<OgcLink>();
        }

        IReadOnlyList<AttachmentDescriptor>? descriptors = preloadedDescriptors;
        if (descriptors is null)
        {
            descriptors = await attachmentOrchestrator
                .ListAsync(service.Id, layer.Id, components.FeatureId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (descriptors.Count == 0)
        {
            return Array.Empty<OgcLink>();
        }

        var links = new List<OgcLink>(descriptors.Count);
        foreach (var descriptor in descriptors)
        {
            var href = BuildAttachmentHref(request, service, layer, collectionId, components, descriptor);
            if (href is null)
            {
                continue;
            }

            var title = descriptor.Name.IsNullOrWhiteSpace()
                ? $"Attachment {descriptor.AttachmentObjectId}"
                : descriptor.Name;
            var type = descriptor.MimeType.IsNullOrWhiteSpace()
                ? "application/octet-stream"
                : descriptor.MimeType;
            links.Add(new OgcLink(href, "enclosure", type, title));
        }

        return links.Count == 0 ? Array.Empty<OgcLink>() : links;
    }

    internal static async Task<JsonDocument?> ParseJsonDocumentAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        Guard.NotNull(request);

        // BUG FIX #31: SECURITY - DoS prevention for GeoJSON upload endpoints
        // Previous implementation buffered entire request body without size validation,
        // allowing attackers to exhaust memory with multi-GB JSON payloads.
        //
        // Security measures:
        // 1. Check Content-Length before buffering (fail fast)
        // 2. Configurable maximum size (default 100 MB)
        // 3. Return HTTP 413 Payload Too Large for oversized requests
        // 4. Prevent memory exhaustion before Kestrel's limits

        const long DefaultMaxSizeBytes = 100 * 1024 * 1024; // 100 MB
        var maxSize = DefaultMaxSizeBytes;

        // Try to get configured limit from request services (if available)
        var config = request.HttpContext.RequestServices
            .GetService(typeof(Honua.Server.Core.Configuration.HonuaConfiguration))
            as Honua.Server.Core.Configuration.HonuaConfiguration;

        if (config?.Services?.OgcApi?.MaxFeatureUploadSizeBytes > 0)
        {
            maxSize = config.Services.OgcApi.MaxFeatureUploadSizeBytes;
        }

        // Check Content-Length header before buffering
        if (request.ContentLength.HasValue && request.ContentLength.Value > maxSize)
        {
            throw new InvalidOperationException(
                $"Request body size ({request.ContentLength.Value:N0} bytes) exceeds maximum allowed size " +
                $"({maxSize:N0} bytes). To upload larger files, increase OgcApi.MaxFeatureUploadSizeBytes in configuration.");
        }

        request.EnableBuffering(maxSize);
        request.Body.Seek(0, SeekOrigin.Begin);

        try
        {
            // Additional safety: limit how much we'll actually read from the stream
            // This protects against cases where Content-Length is missing or incorrect
            var options = new JsonDocumentOptions
            {
                MaxDepth = 256 // Prevent deeply nested JSON attacks
            };

            return await JsonDocument.ParseAsync(request.Body, options, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            request.Body.Seek(0, SeekOrigin.Begin);
        }
    }

    internal static IEnumerable<JsonElement> EnumerateGeoJsonFeatures(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("type", out var typeElement) &&
                string.Equals(typeElement.GetString(), "FeatureCollection", StringComparison.OrdinalIgnoreCase) &&
                root.TryGetProperty("features", out var featuresElement) &&
                featuresElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var feature in featuresElement.EnumerateArray())
                {
                    yield return feature;
                }

                yield break;
            }

            yield return root;
            yield break;
        }

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in root.EnumerateArray())
            {
                yield return element;
            }
        }
    }

    internal static Dictionary<string, object?> ReadGeoJsonAttributes(JsonElement featureElement, LayerDefinition layer, bool removeId, out string? featureId)
    {
        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        featureId = null;

        if (featureElement.ValueKind != JsonValueKind.Object)
        {
            return attributes;
        }

        if (featureElement.TryGetProperty("properties", out var propertiesElement) && propertiesElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in propertiesElement.EnumerateObject())
            {
                attributes[property.Name] = ConvertJsonElement(property.Value);
            }
        }

        if (featureElement.TryGetProperty("geometry", out var geometryElement))
        {
            if (geometryElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                attributes[layer.GeometryField] = null;
            }
            else if (geometryElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                attributes[layer.GeometryField] = JsonNode.Parse(geometryElement.GetRawText());
            }
        }

        if (featureElement.TryGetProperty("id", out var idElement))
        {
            featureId = ConvertJsonElementToString(idElement);
        }

        if (attributes.TryGetValue(layer.IdField, out var attributeId) && attributeId is not null)
        {
            featureId ??= Convert.ToString(attributeId, CultureInfo.InvariantCulture);
            if (removeId)
            {
                attributes.Remove(layer.IdField);
            }
        }

        return attributes;
    }

    internal static IResult CreateEditFailureProblem(FeatureEditError? error, int statusCode)
    {
        if (error is null)
        {
            return Results.Problem("Feature edit failed.", statusCode: statusCode, title: "Feature edit failed");
        }

        var details = error.Details is not null && error.Details.Count > 0
            ? string.Join(",", error.Details.Select(pair => pair.Key.IsNullOrWhiteSpace() ? pair.Value : $"{pair.Key}:{pair.Value}"))
            : null;

        if (details is null)
        {
            return Results.Problem(detail: error.Message, statusCode: statusCode, title: "Feature edit failed");
        }

        var extensions = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["details"] = details
        };

        return Results.Problem(detail: error.Message, statusCode: statusCode, title: "Feature edit failed", extensions: extensions);
    }

    internal static FeatureEditBatch CreateFeatureEditBatch(
        IReadOnlyList<FeatureEditCommand> commands,
        HttpRequest request)
    {
        return new FeatureEditBatch(
            commands,
            rollbackOnFailure: true,
            clientReference: null,
            isAuthenticated: request.HttpContext.User?.Identity?.IsAuthenticated ?? false,
            userRoles: UserIdentityHelper.ExtractUserRoles(request.HttpContext.User));
    }

    internal static async Task<List<(string? FeatureId, object Payload, string? Etag)>> FetchCreatedFeaturesWithETags(
        IFeatureRepository repository,
        FeatureContext context,
        LayerDefinition layer,
        string collectionId,
        FeatureEditBatchResult editResult,
        List<string?> fallbackIds,
        FeatureQuery featureQuery,
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var created = new List<(string? FeatureId, object Payload, string? Etag)>();

        for (var index = 0; index < editResult.Results.Count; index++)
        {
            var result = editResult.Results[index];
            var featureId = result.FeatureId ?? fallbackIds.ElementAtOrDefault(index);
            if (featureId.IsNullOrWhiteSpace())
            {
                continue;
            }

            var record = await repository.GetAsync(context.Service.Id, layer.Id, featureId!, featureQuery, cancellationToken).ConfigureAwait(false);
            if (record is null)
            {
                continue;
            }

            var payload = ToFeature(request, collectionId, layer, record, featureQuery);
            var etag = ComputeFeatureEtag(layer, record);
            created.Add((featureId, payload, etag));
        }

        // If no features were fetched successfully, return minimal response with IDs only
        if (created.Count == 0)
        {
            foreach (var result in editResult.Results)
            {
                created.Add((result.FeatureId, new { id = result.FeatureId }, null));
            }
        }

        return created;
    }

    internal static IResult BuildMutationResponse(
        List<(string? FeatureId, object Payload, string? Etag)> createdFeatures,
        string collectionId,
        bool singleItemMode)
    {
        if (singleItemMode && createdFeatures.Count == 1)
        {
            var (featureId, payload, etag) = createdFeatures[0];
            var location = featureId.IsNullOrWhiteSpace()
                ? null
                : $"/ogc/collections/{collectionId}/items/{featureId}";

            var response = Results.Created(location, payload);
            if (etag.HasValue())
            {
                response = WithResponseHeader(response, HeaderNames.ETag, etag);
            }

            return response;
        }

        var collectionResponse = new
        {
            type = "FeatureCollection",
            features = createdFeatures.Select(entry => entry.Payload)
        };

        return Results.Created($"/ogc/collections/{collectionId}/items", collectionResponse);
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return JsonElementConverter.ToObjectWithJsonNode(element);
    }

    private static string? ConvertJsonElementToString(JsonElement element)
    {
        return JsonElementConverter.ToString(element);
    }

    internal static bool ValidateIfMatch(HttpRequest request, LayerDefinition layer, FeatureRecord record, out string currentEtag)
    {
        currentEtag = ComputeFeatureEtag(layer, record);

        if (!request.Headers.TryGetValue(HeaderNames.IfMatch, out var headerValues) || headerValues.Count == 0)
        {
            return true;
        }

        var normalizedCurrent = NormalizeEtagValue(currentEtag);
        foreach (var rawValue in headerValues.SelectMany(value =>
                     value is not null ? QueryParsingHelpers.ParseCsv(value) : Array.Empty<string>()))
        {
            var normalizedRequested = NormalizeEtagValue(rawValue);
            if (string.Equals(normalizedRequested, "*", StringComparison.Ordinal) ||
                string.Equals(normalizedRequested, normalizedCurrent, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeEtagValue(string? etag)
    {
        if (etag.IsNullOrWhiteSpace())
        {
            return string.Empty;
        }

        var trimmed = etag.Trim();
        if (trimmed.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[2..].Trim();
        }

        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            trimmed = trimmed[1..^1];
        }

        return trimmed;
    }

    internal static string ComputeFeatureEtag(LayerDefinition layer, FeatureRecord record)
    {
        var ordered = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in record.Attributes)
        {
            ordered[pair.Key] = pair.Value;
        }

        string json;
        try
        {
            json = JsonSerializer.Serialize(ordered, JsonSerializerOptionsRegistry.Web);
        }
        catch (NotSupportedException)
        {
            json = JsonSerializer.Serialize(ordered, RuntimeSerializerOptions);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return $"W/\"{Convert.ToHexString(hash)}\"";
    }

    private static readonly JsonSerializerOptions RuntimeSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        MaxDepth = 64,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    internal enum OgcResponseFormat
    {
        GeoJson,
        Html,
        Kml,
        Kmz,
        TopoJson,
        FlatGeobuf,
        GeoArrow,
        GeoPackage,
        Shapefile,
        Csv,
        JsonLd,
        GeoJsonT,
        Wkt,
        Wkb
    }

    private sealed class HeaderResult : IResult
    {
        private readonly IResult _inner;
        private readonly string _headerName;
        private readonly string _headerValue;

        public HeaderResult(IResult inner, string headerName, string headerValue)
        {
            _inner = Guard.NotNull(inner);
            _headerName = Guard.NotNull(headerName);
            _headerValue = Guard.NotNull(headerValue);
        }

        public Task ExecuteAsync(HttpContext httpContext)
        {
            Guard.NotNull(httpContext);

            if (_headerValue.HasValue())
            {
                httpContext.Response.Headers[_headerName] = _headerValue;
            }

            return _inner.ExecuteAsync(httpContext);
        }
    }

    private static string BuildDownloadFileName(string collectionId, string? featureId, OgcResponseFormat format)
    {
        var baseName = FileNameHelper.SanitizeSegment(collectionId);
        if (featureId.HasValue())
        {
            baseName = $"{baseName}-{FileNameHelper.SanitizeSegment(featureId)}";
        }

        var extension = format == OgcResponseFormat.Kmz ? "kmz" : "kml";
        return $"{baseName}.{extension}";
    }

    internal static double[] ResolveBounds(LayerDefinition layer, RasterDatasetDefinition? dataset)
    {
        if (dataset?.Extent?.Bbox is { Count: > 0 })
        {
            var candidate = dataset.Extent.Bbox[0];
            if (candidate.Length >= 4)
            {
                return new[] { candidate[0], candidate[1], candidate[2], candidate[3] };
            }
        }

        if (layer.Extent?.Bbox is { Count: > 0 })
        {
            var candidate = layer.Extent.Bbox[0];
            if (candidate.Length >= 4)
            {
                return new[] { candidate[0], candidate[1], candidate[2], candidate[3] };
            }
        }

        return new[] { -180d, -90d, 180d, 90d };
    }

    private static string BuildArchiveEntryName(string collectionId, string? featureId)
    {
        var baseName = FileNameHelper.SanitizeSegment(collectionId);
        if (featureId.HasValue())
        {
            baseName = $"{baseName}-{FileNameHelper.SanitizeSegment(featureId)}";
        }

        return $"{baseName}.kml";
    }

    internal static async Task<Result<FeatureContext>> ResolveCollectionAsync(
        string collectionId,
        IFeatureContextResolver resolver,
        CancellationToken cancellationToken)
    {
        if (!TryParseCollectionId(collectionId, out var serviceId, out var layerId))
        {
            return Result<FeatureContext>.Failure(Error.NotFound($"Collection '{collectionId}' was not found."));
        }

        // Security: Validate inputs to prevent path traversal and injection attacks
        if (ContainsDangerousCharacters(serviceId) || ContainsDangerousCharacters(layerId))
        {
            return Result<FeatureContext>.Failure(Error.NotFound($"Collection '{collectionId}' was not found."));
        }

        FeatureContext context;
        try
        {
            context = await resolver.ResolveAsync(serviceId, layerId, cancellationToken).ConfigureAwait(false);
        }
        catch (KeyNotFoundException ex)
        {
            var message = ex.Message.IsNullOrWhiteSpace()
                ? $"Collection '{collectionId}' was not found."
                : ex.Message;
            return Result<FeatureContext>.Failure(Error.NotFound(message));
        }
        catch (InvalidOperationException ex)
        {
            return Result<FeatureContext>.Failure(Error.NotFound($"Collection '{collectionId}' was not found."));
        }
        catch (Exception)
        {
            // Catch all other exceptions and return NotFound to avoid exposing internal errors
            return Result<FeatureContext>.Failure(Error.NotFound($"Collection '{collectionId}' was not found."));
        }

        if (context == null)
        {
            return Result<FeatureContext>.Failure(Error.NotFound($"Collection '{collectionId}' was not found."));
        }

        if (!context.Service.Enabled || !context.Service.Ogc.CollectionsEnabled)
        {
            return Result<FeatureContext>.Failure(Error.NotFound($"Collection '{collectionId}' is not available."));
        }

        return Result<FeatureContext>.Success(context);
    }

    private static bool ContainsDangerousCharacters(string value)
    {
        if (value.IsNullOrEmpty())
        {
            return false;
        }

        // Check for path traversal attempts
        if (value.Contains("..") || value.Contains("/") || value.Contains("\\"))
        {
            return true;
        }

        // Check for SQL injection attempts
        if (value.Contains("'") || value.Contains("--") || value.Contains(";"))
        {
            return true;
        }

        // Check for XML/HTML injection
        if (value.Contains("<") || value.Contains(">"))
        {
            return true;
        }

        return false;
    }

    private static bool TryParseCollectionId(string collectionId, out string serviceId, out string layerId)
    {
        serviceId = string.Empty;
        layerId = string.Empty;

        if (collectionId.IsNullOrWhiteSpace())
        {
            return false;
        }

        var parts = collectionId.Split(CollectionIdSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        serviceId = parts[0];
        layerId = parts[1];
        return true;
    }

    internal static IResult MapCollectionResolutionError(Error error, string collectionId)
    {
        return error.Code switch
        {
            "not_found" => CreateNotFoundProblem(error.Message ?? $"Collection '{collectionId}' was not found."),
            "invalid" => Results.Problem(error.Message ?? "Collection resolution failed.", statusCode: StatusCodes.Status500InternalServerError, title: "Collection resolution failed"),
            _ => Results.Problem(error.Message ?? "Collection resolution failed.", statusCode: StatusCodes.Status500InternalServerError, title: "Collection resolution failed")
        };
    }

    /// <summary>
    /// Resolves a collection and returns either the context or an error result.
    /// This consolidates the common pattern of calling ResolveCollectionAsync and mapping errors.
    /// </summary>
    /// <returns>
    /// A tuple containing either (FeatureContext, null) on success or (null, IResult) on failure.
    /// </returns>
    internal static async Task<(FeatureContext? Context, IResult? Error)> TryResolveCollectionAsync(
        string collectionId,
        IFeatureContextResolver resolver,
        CancellationToken cancellationToken)
    {
        var resolution = await ResolveCollectionAsync(collectionId, resolver, cancellationToken).ConfigureAwait(false);
        if (resolution.IsFailure)
        {
            return (null, MapCollectionResolutionError(resolution.Error!, collectionId));
        }

        return (resolution.Value, null);
    }

    internal static string BuildCollectionId(ServiceDefinition service, LayerDefinition layer)
        => $"{service.Id}{CollectionIdSeparator}{layer.Id}";

    internal static IResult CreateValidationProblem(string detail, string parameter)
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid request parameter",
            Detail = detail,
            Extensions = { ["parameter"] = parameter }
        };

        return Results.Problem(problemDetails.Detail, statusCode: problemDetails.Status, title: problemDetails.Title, extensions: problemDetails.Extensions);
    }

    internal static IResult CreateNotFoundProblem(string detail)
    {
        return Results.Problem(detail, statusCode: StatusCodes.Status404NotFound, title: "Not Found");
    }
}
