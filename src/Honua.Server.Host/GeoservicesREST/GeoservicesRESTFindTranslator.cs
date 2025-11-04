// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.GeoservicesREST.Services;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.GeoservicesREST;

internal static class GeoservicesRESTFindTranslator
{
    private const int DefaultMaxRecordCount = 1000;

    public static bool TryParse(
        HttpRequest request,
        CatalogServiceView serviceView,
        out GeoservicesRESTFindContext context,
        out IActionResult? error)
    {
        Guard.NotNull(request);
        Guard.NotNull(serviceView);

        try
        {
            var query = request.Query;

            if (!query.TryGetValue("searchText", out var searchValues) || searchValues.Count == 0 || searchValues[^1].IsNullOrWhiteSpace())
            {
                context = default!;
                error = GeoservicesRESTErrorHelper.BadRequest("Parameter 'searchText' is required.");
                return false;
            }

            var searchText = searchValues[^1]!.Trim();
            var contains = ResolveBoolean(query, "contains", defaultValue: true);
            var returnGeometry = ResolveBoolean(query, "returnGeometry", defaultValue: true);
            var maxRecordCount = ResolveInt(query, "maxRecordCount", DefaultMaxRecordCount, allowZero: false);

            var layerIds = ResolveLayerSelection(query, serviceView, out error);
            if (error is not null)
            {
                context = default!;
                return false;
            }

            var searchFields = ResolveSearchFields(query, serviceView);
            if (searchFields.Count == 0)
            {
                context = default!;
                error = GeoservicesRESTErrorHelper.BadRequest("No searchable fields were found for this request.");
                return false;
            }

            var targetCrs = ResolveTargetCrs(query, serviceView);

            context = new GeoservicesRESTFindContext(
                searchText,
                contains,
                returnGeometry,
                maxRecordCount,
                targetCrs,
                new ReadOnlyCollection<int>(layerIds.ToList()),
                new ReadOnlyCollection<string>(searchFields.ToList()));

            error = null;
            return true;
        }
        catch (GeoservicesRESTQueryException ex)
        {
            context = default!;
            error = GeoservicesRESTErrorHelper.BadRequest(ex.Message);
            return false;
        }
    }

    public static QueryExpression? BuildFindExpression(GeoservicesRESTFindContext context, LayerDefinition layer)
    {
        Guard.NotNull(layer);

        var comparisons = new List<QueryExpression>();
        foreach (var field in context.SearchFields)
        {
            if (!ContainsField(layer, field))
            {
                continue;
            }

            var normalized = GeoservicesFieldResolver.NormalizeFieldName(field, layer);
            var pattern = context.SearchText.Replace('*', '%');
            if (context.Contains)
            {
                if (!pattern.Contains('%'))
                {
                    pattern = "%" + pattern + "%";
                }
            }

            var constant = new QueryConstant(pattern);
            var function = new QueryFunctionExpression("like", new QueryExpression[]
            {
                new QueryFieldReference(normalized),
                constant
            });

            comparisons.Add(function);
        }

        if (comparisons.Count == 0)
        {
            return null;
        }

        QueryExpression expression = comparisons[0];
        for (var i = 1; i < comparisons.Count; i++)
        {
            expression = new QueryBinaryExpression(expression, QueryBinaryOperator.Or, comparisons[i]);
        }

        return expression;
    }

    private static bool ContainsField(LayerDefinition layer, string field)
    {
        if (layer.IdField.EqualsIgnoreCase(field))
        {
            return true;
        }

        return layer.Fields.Any(candidate => candidate.Name.EqualsIgnoreCase(field));
    }

    private static IReadOnlyList<string> ResolveSearchFields(IQueryCollection query, CatalogServiceView serviceView)
    {
        if (query.TryGetValue("searchFields", out var values) && values.Count > 0 && values[^1].HasValue())
        {
            var tokens = QueryParsingHelpers.ParseCsv(values[^1]);

            if (tokens.Count > 0)
            {
                return tokens;
            }
        }

        var stringFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in serviceView.Layers)
        {
            foreach (var field in layer.Layer.Fields)
            {
                if (field.DataType.EqualsIgnoreCase("string"))
                {
                    stringFields.Add(field.Name);
                }
            }
        }

        return stringFields.Count == 0
            ? Array.Empty<string>()
            : stringFields.ToArray();
    }

    private static string ResolveTargetCrs(IQueryCollection query, CatalogServiceView serviceView)
    {
        if (query.TryGetValue("sr", out var values) && values.Count > 0 && values[^1].HasValue())
        {
            return values[^1]!;
        }

        var defaultCrs = serviceView.Service.Ogc.DefaultCrs;
        if (defaultCrs.HasValue())
        {
            return defaultCrs!;
        }

        return "EPSG:4326";
    }

    private static bool ResolveBoolean(IQueryCollection query, string key, bool defaultValue)
    {
        if (!query.TryGetValue(key, out var values) || values.Count == 0)
        {
            return defaultValue;
        }

        var raw = values[^1];
        if (bool.TryParse(raw, out var parsed))
        {
            return parsed;
        }

        return raw switch
        {
            "1" => true,
            "0" => false,
            _ => defaultValue
        };
    }

    private static int ResolveInt(IQueryCollection query, string key, int defaultValue, bool allowZero)
    {
        if (!query.TryGetValue(key, out var values) || values.Count == 0)
        {
            return defaultValue;
        }

        if (!values[^1].TryParseInt(out var parsed) || parsed < 0)
        {
            throw new GeoservicesRESTQueryException($"Parameter '{key}' must be a non-negative integer.");
        }

        if (!allowZero && parsed == 0)
        {
            return defaultValue;
        }

        return parsed;
    }

    private static IReadOnlyList<int> ResolveLayerSelection(IQueryCollection query, CatalogServiceView serviceView, out IActionResult? error)
    {
        var layerIds = new List<int>();

        if (!query.TryGetValue("layers", out var layerValues) || layerValues.Count == 0)
        {
            for (var i = 0; i < serviceView.Layers.Count; i++)
            {
                layerIds.Add(i);
            }

            error = null;
            return layerIds;
        }

        var raw = layerValues[^1];
        if (raw.IsNullOrWhiteSpace())
        {
            return AllLayers(serviceView, out error);
        }

        var trimmed = raw.Trim();
        var option = trimmed;
        string? idSegment = null;

        var colonIndex = trimmed.IndexOf(':');
        if (colonIndex >= 0)
        {
            option = trimmed[..colonIndex].Trim();
            idSegment = trimmed[(colonIndex + 1)..].Trim();
        }

        switch (option.ToLowerInvariant())
        {
            case "all":
                return idSegment.IsNullOrEmpty()
                    ? AllLayers(serviceView, out error)
                    : ParseLayerIds(idSegment, serviceView, out error);
            case "visible":
                return ResolveVisibleLayers(idSegment, serviceView, out error);
            case "top":
                return ResolveTopLayers(idSegment, serviceView, out error);
            default:
                return ParseLayerIds(trimmed, serviceView, out error);
        }
    }

    private static IReadOnlyList<int> ResolveVisibleLayers(string? idSegment, CatalogServiceView serviceView, out IActionResult? error)
    {
        var visible = AllLayers(serviceView, out _);
        if (visible.Count == 0)
        {
            error = null;
            return visible;
        }

        if (idSegment.IsNullOrEmpty())
        {
            error = null;
            return visible;
        }

        var requested = ParseLayerIds(idSegment, serviceView, out error);
        if (error is not null)
        {
            return Array.Empty<int>();
        }

        if (requested.Count == 0)
        {
            error = null;
            return requested;
        }

        foreach (var id in requested)
        {
            if (!visible.Contains(id))
            {
                error = GeoservicesRESTErrorHelper.BadRequest($"Layer id '{id}' is not visible.");
                return Array.Empty<int>();
            }
        }

        error = null;
        return requested;
    }

    private static IReadOnlyList<int> ResolveTopLayers(string? idSegment, CatalogServiceView serviceView, out IActionResult? error)
    {
        if (!idSegment.IsNullOrEmpty())
        {
            return ParseLayerIds(idSegment, serviceView, out error);
        }

        if (serviceView.Layers.Count == 0)
        {
            error = null;
            return Array.Empty<int>();
        }

        error = null;
        return new List<int> { serviceView.Layers.Count - 1 };
    }

    private static IReadOnlyList<int> AllLayers(CatalogServiceView serviceView, out IActionResult? error)
    {
        var ids = new List<int>(serviceView.Layers.Count);
        for (var i = 0; i < serviceView.Layers.Count; i++)
        {
            ids.Add(i);
        }

        error = null;
        return ids;
    }

    private static IReadOnlyList<int> ParseLayerIds(string? segment, CatalogServiceView serviceView, out IActionResult? error)
    {
        var layerIds = new List<int>();
        if (segment.IsNullOrWhiteSpace())
        {
            error = null;
            return layerIds;
        }

        var tokens = QueryParsingHelpers.ParseCsv(segment);
        foreach (var token in tokens)
        {
            if (!token.TryParseInt(out var parsed) || parsed < 0 || parsed >= serviceView.Layers.Count)
            {
                error = GeoservicesRESTErrorHelper.BadRequest($"Layer id '{token}' is not valid.");
                return Array.Empty<int>();
            }

            if (!layerIds.Contains(parsed))
            {
                layerIds.Add(parsed);
            }
        }

        error = null;
        return layerIds;
    }
}

internal sealed record GeoservicesRESTFindContext(
    string SearchText,
    bool Contains,
    bool ReturnGeometry,
    int MaxRecordCount,
    string TargetCrs,
    IReadOnlyList<int> LayerIds,
    IReadOnlyList<string> SearchFields);
