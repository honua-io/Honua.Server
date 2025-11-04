// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Enterprise.Data.Elasticsearch;

public sealed partial class ElasticsearchDataStoreProvider
{
    public async IAsyncEnumerable<FeatureRecord> QueryAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);

        var effectiveQuery = query ?? new FeatureQuery();
        var connection = GetConnection(dataSource);
        var indexName = ResolveIndexName(layer, connection.Info);

        var body = BuildSearchBody(effectiveQuery, layer, includePaging: true);

        using var response = await SendAsync(
            connection.Client,
            HttpMethod.Post,
            $"{Encode(indexName)}/_search",
            body,
            cancellationToken).ConfigureAwait(false);

        if (effectiveQuery.ResultType == FeatureResultType.Hits)
        {
            yield break;
        }

        if (!response.RootElement.TryGetProperty("hits", out var hitsElement) ||
            !hitsElement.TryGetProperty("hits", out var itemsElement))
        {
            yield break;
        }

        foreach (var hit in itemsElement.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!hit.TryGetProperty("_source", out var sourceElement))
            {
                continue;
            }

            var documentId = hit.TryGetProperty("_id", out var idElement) ? idElement.GetString() : null;
            var attributes = JsonElementToDictionary(sourceElement);

            // Use LayerMetadataHelper to get primary key column
            var idField = LayerMetadataHelper.GetPrimaryKeyColumn(layer);
            if (documentId.HasValue() && !attributes.ContainsKey(idField))
            {
                attributes[idField] = documentId;
            }

            yield return new FeatureRecord(new ReadOnlyDictionary<string, object?>(attributes));
        }
    }

    public async Task<long> CountAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? query,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);

        var effectiveQuery = query ?? new FeatureQuery { ResultType = FeatureResultType.Hits };
        var connection = GetConnection(dataSource);
        var indexName = ResolveIndexName(layer, connection.Info);

        var body = BuildSearchBody(effectiveQuery with { ResultType = FeatureResultType.Hits }, layer, includePaging: false);

        using var response = await SendAsync(
            connection.Client,
            HttpMethod.Post,
            $"{Encode(indexName)}/_count",
            body.ContainsKey("query") ? new System.Text.Json.Nodes.JsonObject { ["query"] = body["query"]! } : null,
            cancellationToken).ConfigureAwait(false);

        if (response.RootElement.TryGetProperty("count", out var countElement) &&
            countElement.TryGetInt64(out var count))
        {
            return count;
        }

        // Fallback to search hits total if count property missing
        if (response.RootElement.TryGetProperty("hits", out var hitsElement) &&
            hitsElement.TryGetProperty("total", out var totalElement) &&
            totalElement.TryGetProperty("value", out var valueElement) &&
            valueElement.TryGetInt64(out count))
        {
            return count;
        }

        return 0L;
    }

    public async Task<FeatureRecord?> GetAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        FeatureQuery? query,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNullOrWhiteSpace(featureId);

        var connection = GetConnection(dataSource);
        var indexName = ResolveIndexName(layer, connection.Info);

        using var response = await SendAsync(
            connection.Client,
            HttpMethod.Get,
            $"{Encode(indexName)}/_doc/{Encode(featureId)}",
            body: null,
            cancellationToken,
            allowNotFound: true).ConfigureAwait(false);

        if (response.RootElement.TryGetProperty("found", out var foundElement) &&
            foundElement.ValueKind == JsonValueKind.False)
        {
            return null;
        }

        if (!response.RootElement.TryGetProperty("_source", out var sourceElement))
        {
            return null;
        }

        var attributes = JsonElementToDictionary(sourceElement);

        // Use LayerMetadataHelper to get primary key column
        var idField = LayerMetadataHelper.GetPrimaryKeyColumn(layer);
        if (!attributes.ContainsKey(idField))
        {
            attributes[idField] = featureId;
        }

        return new FeatureRecord(new ReadOnlyDictionary<string, object?>(attributes));
    }

    public async Task<IReadOnlyList<StatisticsResult>> QueryStatisticsAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IReadOnlyList<StatisticDefinition> statistics,
        IReadOnlyList<string>? groupByFields,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(statistics);

        if (statistics.Count == 0)
        {
            return Array.Empty<StatisticsResult>();
        }

        var effectiveQuery = filter ?? new FeatureQuery();
        var (aggregations, mappings, groupAggregationNames) = BuildStatisticsAggregations(statistics, groupByFields, layer);

        var connection = GetConnection(dataSource);
        var indexName = ResolveIndexName(layer, connection.Info);

        var body = BuildSearchBody(effectiveQuery, layer, includePaging: false);
        body["size"] = 0;
        body["track_total_hits"] = true;

        if (aggregations is not null && aggregations.Count > 0)
        {
            body["aggs"] = aggregations;
        }

        using var response = await SendAsync(
            connection.Client,
            HttpMethod.Post,
            $"{Encode(indexName)}/_search",
            body,
            cancellationToken).ConfigureAwait(false);

        if (groupByFields is { Count: > 0 })
        {
            return ParseGroupedStatistics(response.RootElement, groupByFields, groupAggregationNames, mappings);
        }

        return ParseUngroupedStatistics(response.RootElement, mappings);
    }

    public async Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IReadOnlyList<string> fieldNames,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(fieldNames);

        if (fieldNames.Count == 0)
        {
            return Array.Empty<DistinctResult>();
        }

        var effectiveQuery = filter ?? new FeatureQuery();
        var (aggregations, aggregationNames) = BuildDistinctAggregations(fieldNames);

        var connection = GetConnection(dataSource);
        var indexName = ResolveIndexName(layer, connection.Info);

        var body = BuildSearchBody(effectiveQuery, layer, includePaging: false);
        body["size"] = 0;
        body["aggs"] = aggregations;

        using var response = await SendAsync(
            connection.Client,
            HttpMethod.Post,
            $"{Encode(indexName)}/_search",
            body,
            cancellationToken).ConfigureAwait(false);

        return ParseDistinctResults(response.RootElement, fieldNames, aggregationNames);
    }

    public async Task<BoundingBox?> QueryExtentAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);

        // Use LayerMetadataHelper to get geometry column
        var geometryField = LayerMetadataHelper.GetGeometryColumn(layer);
        if (geometryField.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException($"Layer '{layer.Id}' does not define a geometry field.");
        }

        var effectiveQuery = filter ?? new FeatureQuery();
        var connection = GetConnection(dataSource);
        var indexName = ResolveIndexName(layer, connection.Info);

        var body = BuildSearchBody(effectiveQuery with { ResultType = FeatureResultType.Hits }, layer, includePaging: false);
        var aggregations = new System.Text.Json.Nodes.JsonObject
        {
            ["extent"] = new System.Text.Json.Nodes.JsonObject
            {
                ["geo_bounds"] = new System.Text.Json.Nodes.JsonObject
                {
                    ["field"] = geometryField,
                    ["wrap_longitude"] = true
                }
            }
        };
        body["size"] = 0;
        body["aggs"] = aggregations;

        using var response = await SendAsync(
            connection.Client,
            HttpMethod.Post,
            $"{Encode(indexName)}/_search",
            body,
            cancellationToken).ConfigureAwait(false);

        if (!response.RootElement.TryGetProperty("aggregations", out var aggsElement) ||
            !aggsElement.TryGetProperty("extent", out var extentElement) ||
            !extentElement.TryGetProperty("bounds", out var boundsElement))
        {
            return null;
        }

        if (!boundsElement.TryGetProperty("top_left", out var topLeft) ||
            !boundsElement.TryGetProperty("bottom_right", out var bottomRight))
        {
            return null;
        }

        if (!TryReadCoordinates(topLeft, out var topLeftLon, out var topLeftLat) ||
            !TryReadCoordinates(bottomRight, out var bottomRightLon, out var bottomRightLat))
        {
            return null;
        }

        return new BoundingBox(
            topLeftLon,
            bottomRightLat,
            bottomRightLon,
            topLeftLat,
            Crs: layer.Storage?.Crs ?? "EPSG:4326");
    }
}
