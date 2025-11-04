// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Data;
using Honua.Server.Core.Query;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.GeoservicesREST.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.GeoservicesREST;

/// <summary>
/// Orchestrates the translation of Geoservices REST API query parameters into internal query objects.
/// Delegates to specialized resolver services for different aspects of the query.
/// </summary>
internal static class GeoservicesRESTQueryTranslator
{
    public static bool TryParse(
        HttpRequest request,
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        out GeoservicesRESTQueryContext context,
        out IActionResult? error,
        ILogger? logger = null)
    {
        Guard.NotNull(request);
        Guard.NotNull(serviceView);
        Guard.NotNull(layerView);

        try
        {
            var query = request.Query;

            // Resolve format and presentation
            var (format, prettyPrint) = GeoservicesParameterResolver.ResolveFormat(query);

            // Resolve pagination
            var limit = GeoservicesParameterResolver.ResolveLimit(query, serviceView, layerView);
            var offset = GeoservicesParameterResolver.ResolveOffset(query);

            // Resolve result type flags
            var returnGeometry = GeoservicesParameterResolver.ResolveBoolean(query, "returnGeometry", defaultValue: true);

            // Use QueryParameterHelper for returnCountOnly to determine result type
            var returnCountOnlyRaw = query.TryGetValue("returnCountOnly", out var returnCountOnlyValues) && returnCountOnlyValues.Count > 0
                ? returnCountOnlyValues[^1]
                : null;
            var (resultType, resultTypeError) = QueryParameterHelper.ParseResultType(
                returnCountOnlyRaw,
                FeatureResultType.Results);
            if (resultTypeError is not null)
            {
                ThrowBadRequest(resultTypeError);
            }
            var returnCountOnly = resultType == FeatureResultType.Hits;

            var returnIdsOnly = GeoservicesParameterResolver.ResolveBoolean(query, "returnIdsOnly", defaultValue: false);
            var returnExtentOnly = GeoservicesParameterResolver.ResolveBoolean(query, "returnExtentOnly", defaultValue: false);
            var returnDistinctValues = GeoservicesParameterResolver.ResolveBoolean(query, "returnDistinctValues", defaultValue: false);

            // Validate flag combinations
            ValidateQueryFlags(returnCountOnly, returnIdsOnly, returnDistinctValues, returnExtentOnly);

            // Resolve fields
            var (outFields, propertyNames, selectedFields, requestedFields) =
                GeoservicesFieldResolver.ResolveOutFields(query, layerView.Layer, returnIdsOnly);

            // Resolve spatial parameters
            var targetWkid = GeoservicesSpatialResolver.ResolveTargetWkid(request, serviceView.Service, layerView.Layer);
            var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, serviceView.Service, layerView.Layer);

            // Resolve statistics and aggregations
            var groupByFields = GeoservicesFieldResolver.ResolveGroupByFields(query, layerView.Layer);
            var statistics = GeoservicesStatisticsResolver.ResolveStatistics(query, layerView.Layer);

            // Validate statistics combinations
            ValidateStatisticsQuery(groupByFields, statistics, returnDistinctValues, returnCountOnly, returnIdsOnly, returnExtentOnly);

            // Resolve sorting
            var sortOrders = GeoservicesFieldResolver.ResolveOrderByFields(query, layerView.Layer);

            // Resolve temporal parameters
            var temporal = GeoservicesTemporalResolver.ResolveTemporalRange(query, layerView.Layer);

            // Resolve map scale
            var mapScale = GeoservicesParameterResolver.ResolveMapScale(query);

            // Resolve advanced query features BEFORE using temporal
            var historicMoment = GeoservicesParameterResolver.ResolveHistoricMoment(query);

            // Apply historicMoment to temporal filter if specified
            if (historicMoment.HasValue)
            {
                if (string.IsNullOrWhiteSpace(layerView.Layer.Storage?.TemporalColumn))
                {
                    ThrowBadRequest("historicMoment parameter requires a time-enabled layer.");
                }

                // Override the temporal filter to query at a specific point in time
                var momentOffset = new DateTimeOffset(historicMoment.Value);
                temporal = new TemporalInterval(momentOffset, momentOffset);
            }

            // Resolve geometry optimization parameters
            var maxAllowableOffset = GeoservicesParameterResolver.ResolveMaxAllowableOffset(query);
            var geometryPrecision = GeoservicesParameterResolver.ResolveGeometryPrecision(query);

            // Resolve having clause for statistics queries
            var havingClause = GeoservicesParameterResolver.ResolveHavingClause(query);

            // Validate having clause usage
            if (!string.IsNullOrWhiteSpace(havingClause) && statistics.Count == 0)
            {
                ThrowBadRequest("having clause requires outStatistics to be specified.");
            }

            // Adjust property names to include fields needed for grouping/statistics
            var effectivePropertyNames = AdjustPropertyNamesForStatistics(propertyNames, groupByFields, statistics);

            // Build filter expression (SECURITY: validates WHERE clause and objectIds)
            var filter = GeoservicesWhereParser.BuildFilter(query, layerView.Layer, request.HttpContext, logger);

            // Disable geometry for statistics/distinct queries
            if (statistics.Count > 0 || returnDistinctValues)
            {
                returnGeometry = false;
            }

            // Build the feature query
            var featureQuery = new FeatureQuery(
                Limit: limit,
                Offset: offset,
                Bbox: bbox,
                Temporal: temporal,
                ResultType: returnCountOnly ? FeatureResultType.Hits : FeatureResultType.Results,
                PropertyNames: effectivePropertyNames,
                SortOrders: sortOrders,
                Filter: filter,
                EntityDefinition: null,
                Crs: $"EPSG:{targetWkid}",
                HavingClause: havingClause);

            // Build the context
            context = new GeoservicesRESTQueryContext(
                featureQuery,
                prettyPrint,
                returnGeometry,
                returnCountOnly,
                returnIdsOnly,
                returnExtentOnly,
                selectedFields,
                targetWkid,
                format,
                outFields,
                requestedFields,
                returnDistinctValues,
                groupByFields,
                statistics,
                mapScale,
                maxAllowableOffset,
                geometryPrecision,
                historicMoment,
                havingClause);

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

    private static void ValidateQueryFlags(bool returnCountOnly, bool returnIdsOnly, bool returnDistinctValues, bool returnExtentOnly)
    {
        if (returnCountOnly && returnIdsOnly)
        {
            ThrowBadRequest("returnCountOnly and returnIdsOnly cannot both be true.");
        }
    }

    private static void ValidateStatisticsQuery(
        IReadOnlyList<string> groupByFields,
        IReadOnlyList<GeoservicesRESTStatisticDefinition> statistics,
        bool returnDistinctValues,
        bool returnCountOnly,
        bool returnIdsOnly,
        bool returnExtentOnly)
    {
        if (groupByFields.Count > 0 && statistics.Count == 0)
        {
            ThrowBadRequest("groupByFieldsForStatistics requires outStatistics.");
        }

        if (returnDistinctValues && statistics.Count > 0)
        {
            ThrowBadRequest("returnDistinctValues cannot be combined with outStatistics.");
        }

        if ((returnDistinctValues || statistics.Count > 0) && returnCountOnly)
        {
            ThrowBadRequest("returnCountOnly cannot be combined with distinct or statistical queries.");
        }

        if ((returnDistinctValues || statistics.Count > 0) && returnIdsOnly)
        {
            ThrowBadRequest("returnIdsOnly cannot be combined with distinct or statistical queries.");
        }

        if ((returnDistinctValues || statistics.Count > 0) && returnExtentOnly)
        {
            ThrowBadRequest("returnExtentOnly is not supported with distinct or statistical queries.");
        }
    }

    private static IReadOnlyList<string>? AdjustPropertyNamesForStatistics(
        IReadOnlyList<string>? propertyNames,
        IReadOnlyList<string> groupByFields,
        IReadOnlyList<GeoservicesRESTStatisticDefinition> statistics)
    {
        if (propertyNames is null)
        {
            return null;
        }

        var effectivePropertyNames = new List<string>(propertyNames);

        // Use HashSet for O(1) lookups instead of O(n) Any() calls
        var existingFieldsSet = new HashSet<string>(propertyNames, StringComparer.OrdinalIgnoreCase);

        foreach (var field in groupByFields)
        {
            if (existingFieldsSet.Add(field))
            {
                effectivePropertyNames.Add(field);
            }
        }

        foreach (var statistic in statistics)
        {
            if (!string.IsNullOrWhiteSpace(statistic.FieldName) && existingFieldsSet.Add(statistic.FieldName!))
            {
                effectivePropertyNames.Add(statistic.FieldName!);
            }
        }

        return effectivePropertyNames;
    }

    private static void ThrowBadRequest(string message)
    {
        throw new GeoservicesRESTQueryException(message);
    }
}

/// <summary>
/// Context containing all parsed query parameters for a Geoservices REST API request.
/// </summary>
public sealed record GeoservicesRESTQueryContext(
    FeatureQuery Query,
    bool PrettyPrint,
    bool ReturnGeometry,
    bool ReturnCountOnly,
    bool ReturnIdsOnly,
    bool ReturnExtentOnly,
    IReadOnlyDictionary<string, string> SelectedFields,
    int TargetWkid,
    GeoservicesResponseFormat Format,
    string OutFields,
    IReadOnlyCollection<string>? RequestedOutFields,
    bool ReturnDistinctValues,
    IReadOnlyList<string> GroupByFields,
    IReadOnlyList<GeoservicesRESTStatisticDefinition> Statistics,
    double? MapScale,
    double? MaxAllowableOffset,
    int? GeometryPrecision,
    DateTime? HistoricMoment,
    string? HavingClause);

/// <summary>
/// Supported response formats for Geoservices REST API.
/// </summary>
public enum GeoservicesResponseFormat
{
    Json,
    GeoJson,
    TopoJson,
    Kml,
    Kmz,
    Shapefile,
    Csv,
    Wkt,
    Wkb
}
