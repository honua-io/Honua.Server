// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Linq;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Shared helper methods for OGC API handlers.
/// </summary>
internal static class OgcHelpers
{
    public const string CollectionIdSeparator = "::";
    public const string DefaultTemporalReferenceSystem = "http://www.opengis.net/def/uom/ISO-8601/0/Gregorian";

    public static readonly string[] DefaultConformanceClasses =
    {
        "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/core",
        "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/geojson",
        "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/oas30",
        "http://www.opengis.net/spec/ogcapi-features-3/1.0/conf/search",
        "http://www.opengis.net/spec/ogcapi-features-3/1.0/conf/filter",
        "http://www.opengis.net/spec/ogcapi-features-3/1.0/conf/filter-cql-text",
        "http://www.opengis.net/spec/ogcapi-features-3/1.0/conf/filter-cql2-json",
        "http://www.opengis.net/spec/ogcapi-features-3/1.0/conf/features-filter",
        "http://www.opengis.net/spec/ogcapi-features-3/1.0/conf/spatial-operators",
        "http://www.opengis.net/spec/ogcapi-features-3/1.0/conf/temporal-operators",
        "http://www.opengis.net/spec/ogcapi-tiles-1/1.0/conf/core"
    };

    /// <summary>
    /// Builds a simple OGC link using the RequestLinkHelper for consistent URL generation.
    /// Respects proxy headers (X-Forwarded-Proto, X-Forwarded-Host) and base paths.
    /// </summary>
    public static OgcLink BuildSimpleLink(HttpRequest request, string relativePath, string rel, string type, string? title = null)
    {
        var href = request.BuildAbsoluteUrl(relativePath);
        return new OgcLink(href, rel, type, title);
    }

    public static IReadOnlyList<string> ParseList(string? raw)
    {
        if (raw.IsNullOrWhiteSpace())
        {
            return Array.Empty<string>();
        }

        return QueryParsingHelpers.ParseCsv(raw);
    }

    public static IReadOnlyList<string> ParseCollectionsParameter(StringValues values)
    {
        if (values.Count == 0)
        {
            return Array.Empty<string>();
        }

        var collections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            if (value.IsNullOrWhiteSpace())
            {
                continue;
            }

            foreach (var segment in QueryParsingHelpers.ParseCsv(value))
            {
                collections.Add(segment);
            }
        }

        return collections.ToList();
    }

    public static bool LooksLikeJson(string? value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return false;
        }

        var trimmed = value.Trim();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }

    public static bool WantsHtml(HttpRequest request)
    {
        var accept = request.Headers.Accept.ToString();
        return accept.HasValue() &&
               accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasIfMatch(HttpRequest request)
    {
        return request.Headers.ContainsKey("If-Match");
    }

    public static bool PreferReturnMinimal(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Prefer", out var values))
        {
            return false;
        }

        foreach (var value in values)
        {
            if (value.IsNullOrWhiteSpace())
            {
                continue;
            }

            var parts = value.Split(';', StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                if (string.Equals(part, "return=minimal", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static QueryFilter? CombineFilters(QueryFilter? first, QueryFilter? second)
    {
        if (first is null)
        {
            return second;
        }

        if (second is null)
        {
            return first;
        }

        var combined = new QueryBinaryExpression(
            first.Expression!,
            QueryBinaryOperator.And,
            second.Expression!);

        return new QueryFilter(combined);
    }

    public static IReadOnlyList<string> BuildDefaultCrs(ServiceDefinition service)
    {
        var defaultCrs = service.Ogc.DefaultCrs ?? "http://www.opengis.net/def/crs/OGC/1.3/CRS84";
        return new[] { defaultCrs };
    }


    public static IReadOnlyList<string> BuildOrderedStyleIds(LayerDefinition layer)
    {
        var ordered = new List<string>();

        if (layer.DefaultStyleId.HasValue())
        {
            ordered.Add(layer.DefaultStyleId);
        }

        if (layer.StyleIds is { Count: > 0 })
        {
            foreach (var styleId in layer.StyleIds)
            {
                if (styleId.HasValue() && !ordered.Contains(styleId, StringComparer.OrdinalIgnoreCase))
                {
                    ordered.Add(styleId);
                }
            }
        }

        return ordered;
    }
}
