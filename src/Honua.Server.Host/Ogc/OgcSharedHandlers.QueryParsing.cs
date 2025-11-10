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
            "sortby",
            "include3D"
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

        // Parse include3D parameter for 3D coordinate support
        var (include3D, include3DError) = QueryParameterHelper.ParseBoolean(
            queryCollection["include3D"].ToString(),
            defaultValue: false);
        // Note: Boolean parsing errors are not critical, just use default

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
            Include3D: include3D);

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

    internal static bool LooksLikeJson(string? value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return false;
        }

        var trimmed = value.TrimStart();
        return trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal);
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
