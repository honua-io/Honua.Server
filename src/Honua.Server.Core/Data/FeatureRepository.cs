// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data.Sqlite;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data;

/// <summary>
/// Repository for querying and managing feature records across data sources.
/// Provides CRUD operations, spatial queries, statistics, and tile generation for geospatial features.
/// </summary>
public interface IFeatureRepository
{
    /// <summary>
    /// Queries features asynchronously from the specified service and layer.
    /// Returns an async enumerable stream for efficient processing of large result sets.
    /// </summary>
    /// <param name="serviceId">The unique service identifier.</param>
    /// <param name="layerId">The unique layer identifier within the service.</param>
    /// <param name="query">Optional query parameters for filtering, sorting, and field selection.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>An async enumerable stream of feature records.</returns>
    IAsyncEnumerable<FeatureRecord> QueryAsync(string serviceId, string layerId, FeatureQuery? query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts features matching the specified query criteria.
    /// </summary>
    /// <param name="serviceId">The unique service identifier.</param>
    /// <param name="layerId">The unique layer identifier within the service.</param>
    /// <param name="query">Optional query parameters for filtering.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The total count of features matching the query.</returns>
    Task<long> CountAsync(string serviceId, string layerId, FeatureQuery? query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single feature by its identifier.
    /// </summary>
    /// <param name="serviceId">The unique service identifier.</param>
    /// <param name="layerId">The unique layer identifier within the service.</param>
    /// <param name="featureId">The unique feature identifier.</param>
    /// <param name="query">Optional query parameters for field selection.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The feature record if found, null otherwise.</returns>
    Task<FeatureRecord?> GetAsync(string serviceId, string layerId, string featureId, FeatureQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new feature record in the specified layer.
    /// </summary>
    /// <param name="serviceId">The unique service identifier.</param>
    /// <param name="layerId">The unique layer identifier within the service.</param>
    /// <param name="record">The feature record to create.</param>
    /// <param name="transaction">Optional transaction for atomicity.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The created feature record with generated identifier and server-side values.</returns>
    Task<FeatureRecord> CreateAsync(string serviceId, string layerId, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing feature record.
    /// </summary>
    /// <param name="serviceId">The unique service identifier.</param>
    /// <param name="layerId">The unique layer identifier within the service.</param>
    /// <param name="featureId">The unique feature identifier to update.</param>
    /// <param name="record">The updated feature record data.</param>
    /// <param name="transaction">Optional transaction for atomicity.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The updated feature record if successful, null if feature not found.</returns>
    Task<FeatureRecord?> UpdateAsync(string serviceId, string layerId, string featureId, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a feature by its identifier.
    /// </summary>
    /// <param name="serviceId">The unique service identifier.</param>
    /// <param name="layerId">The unique layer identifier within the service.</param>
    /// <param name="featureId">The unique feature identifier to delete.</param>
    /// <param name="transaction">Optional transaction for atomicity.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True if the feature was deleted, false if not found.</returns>
    Task<bool> DeleteAsync(string serviceId, string layerId, string featureId, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a Mapbox Vector Tile (MVT) for the specified tile coordinates.
    /// </summary>
    /// <param name="serviceId">The unique service identifier.</param>
    /// <param name="layerId">The unique layer identifier within the service.</param>
    /// <param name="zoom">The tile zoom level.</param>
    /// <param name="x">The tile X coordinate.</param>
    /// <param name="y">The tile Y coordinate.</param>
    /// <param name="datetime">Optional temporal filter in ISO 8601 format.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The MVT tile as a byte array in Protobuf format.</returns>
    Task<byte[]> GenerateMvtTileAsync(string serviceId, string layerId, int zoom, int x, int y, string? datetime = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes statistical aggregations (min, max, avg, sum, count) on numeric fields.
    /// </summary>
    /// <param name="serviceId">The unique service identifier.</param>
    /// <param name="layerId">The unique layer identifier within the service.</param>
    /// <param name="statistics">The list of statistics to compute.</param>
    /// <param name="groupByFields">Optional fields to group results by.</param>
    /// <param name="filter">Optional query filter to apply before aggregation.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>List of statistics results, one per group (or single result if no grouping).</returns>
    Task<IReadOnlyList<StatisticsResult>> QueryStatisticsAsync(string serviceId, string layerId, IReadOnlyList<StatisticDefinition> statistics, IReadOnlyList<string>? groupByFields, FeatureQuery? filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves distinct values for specified fields.
    /// Useful for populating filter dropdowns and understanding data distribution.
    /// </summary>
    /// <param name="serviceId">The unique service identifier.</param>
    /// <param name="layerId">The unique layer identifier within the service.</param>
    /// <param name="fieldNames">The fields to get distinct values for.</param>
    /// <param name="filter">Optional query filter to apply before computing distinct values.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>List of distinct results, one per field.</returns>
    Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(string serviceId, string layerId, IReadOnlyList<string> fieldNames, FeatureQuery? filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the spatial extent (bounding box) of features matching the filter.
    /// </summary>
    /// <param name="serviceId">The unique service identifier.</param>
    /// <param name="layerId">The unique layer identifier within the service.</param>
    /// <param name="filter">Optional query filter to apply before computing extent.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The bounding box of all matching features, or null if no features found.</returns>
    Task<BoundingBox?> QueryExtentAsync(string serviceId, string layerId, FeatureQuery? filter, CancellationToken cancellationToken = default);
}

public sealed class FeatureRepository : IFeatureRepository
{
    private readonly IFeatureContextResolver _contextResolver;

    public FeatureRepository(IFeatureContextResolver contextResolver)
    {
        _contextResolver = contextResolver ?? throw new ArgumentNullException(nameof(contextResolver));
    }

    public IAsyncEnumerable<FeatureRecord> QueryAsync(string serviceId, string layerId, FeatureQuery? query, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(serviceId);
        Guard.NotNullOrWhiteSpace(layerId);
        return QueryInternalAsync(serviceId, layerId, query, cancellationToken);
    }

    public async Task<long> CountAsync(string serviceId, string layerId, FeatureQuery? query, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(serviceId);
        Guard.NotNullOrWhiteSpace(layerId);
        var context = await ResolveContextAsync(serviceId, layerId, cancellationToken).ConfigureAwait(false);
        var effectiveQuery = NormalizeQuery(context, query);
        return await context.Provider.CountAsync(context.DataSource, context.Service, context.Layer, effectiveQuery, cancellationToken);
    }

    public async Task<FeatureRecord?> GetAsync(string serviceId, string layerId, string featureId, FeatureQuery? query = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(serviceId);
        Guard.NotNullOrWhiteSpace(layerId);
        Guard.NotNullOrWhiteSpace(featureId);
        var context = await ResolveContextAsync(serviceId, layerId, cancellationToken).ConfigureAwait(false);
        var normalizedQuery = NormalizeQuery(context, query);
        return await context.Provider.GetAsync(context.DataSource, context.Service, context.Layer, featureId, normalizedQuery, cancellationToken);
    }

    public async Task<FeatureRecord> CreateAsync(string serviceId, string layerId, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(serviceId);
        Guard.NotNullOrWhiteSpace(layerId);
        Guard.NotNull(record);
        var context = await ResolveContextAsync(serviceId, layerId, cancellationToken).ConfigureAwait(false);
        return await context.Provider.CreateAsync(context.DataSource, context.Service, context.Layer, record, transaction, cancellationToken);
    }

    public async Task<FeatureRecord?> UpdateAsync(string serviceId, string layerId, string featureId, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(serviceId);
        Guard.NotNullOrWhiteSpace(layerId);
        Guard.NotNullOrWhiteSpace(featureId);
        Guard.NotNull(record);
        var context = await ResolveContextAsync(serviceId, layerId, cancellationToken).ConfigureAwait(false);
        return await context.Provider.UpdateAsync(context.DataSource, context.Service, context.Layer, featureId, record, transaction, cancellationToken);
    }

    public async Task<bool> DeleteAsync(string serviceId, string layerId, string featureId, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(serviceId);
        Guard.NotNullOrWhiteSpace(layerId);
        Guard.NotNullOrWhiteSpace(featureId);
        var context = await ResolveContextAsync(serviceId, layerId, cancellationToken).ConfigureAwait(false);
        return await context.Provider.DeleteAsync(context.DataSource, context.Service, context.Layer, featureId, transaction, cancellationToken);
    }

    public async Task<byte[]> GenerateMvtTileAsync(string serviceId, string layerId, int zoom, int x, int y, string? datetime = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(serviceId);
        Guard.NotNullOrWhiteSpace(layerId);
        var context = await ResolveContextAsync(serviceId, layerId, cancellationToken).ConfigureAwait(false);

        // Try provider's native MVT generation with temporal filter
        var mvtBytes = await context.Provider.GenerateMvtTileAsync(context.DataSource, context.Service, context.Layer, zoom, x, y, datetime, cancellationToken).ConfigureAwait(false);
        if (mvtBytes is not null)
        {
            return mvtBytes;
        }

        // Fall back to empty tile (NTS fallback not yet implemented)
        return Array.Empty<byte>();
    }

    private async IAsyncEnumerable<FeatureRecord> QueryInternalAsync(
        string serviceId,
        string layerId,
        FeatureQuery? query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var context = await ResolveContextAsync(serviceId, layerId, cancellationToken).ConfigureAwait(false);
        var effectiveQuery = NormalizeQuery(context, query);

        await foreach (var record in context.Provider.QueryAsync(context.DataSource, context.Service, context.Layer, effectiveQuery, cancellationToken).ConfigureAwait(false))
        {
            yield return record;
        }
    }

    private FeatureQuery NormalizeQuery(FeatureContext context, FeatureQuery? query)
    {
        var effectiveQuery = query ?? new FeatureQuery();
        var normalizedTargetCrs = ResolveTargetCrs(context, effectiveQuery.Crs);
        if (!string.IsNullOrWhiteSpace(normalizedTargetCrs) &&
            !string.Equals(normalizedTargetCrs, effectiveQuery.Crs, StringComparison.OrdinalIgnoreCase))
        {
            effectiveQuery = effectiveQuery with { Crs = normalizedTargetCrs };
        }

        effectiveQuery = NormalizeBoundingBox(context, effectiveQuery, normalizedTargetCrs);
        effectiveQuery = ApplyAutoFilter(context.Layer, effectiveQuery);

        var needsEntityDefinition = (effectiveQuery.SortOrders?.Count ?? 0) > 0 || effectiveQuery.Filter?.Expression is not null;
        if (!needsEntityDefinition)
        {
            return effectiveQuery;
        }

        if (effectiveQuery.EntityDefinition is not null)
        {
            return effectiveQuery;
        }

        var builder = new MetadataQueryModelBuilder();
        var entityDefinition = builder.Build(context.Snapshot, context.Service, context.Layer);
        return effectiveQuery with { EntityDefinition = entityDefinition };
    }

    private static FeatureQuery ApplyAutoFilter(LayerDefinition layer, FeatureQuery query)
    {
        var autoFilter = layer.Query.AutoFilter?.Expression;
        if (autoFilter?.Expression is null)
        {
            return query;
        }

        var combined = CombineFilters(autoFilter, query.Filter);
        if (ReferenceEquals(combined, query.Filter))
        {
            return query;
        }

        return query with { Filter = combined };
    }

    private static QueryFilter? CombineFilters(QueryFilter autoFilter, QueryFilter? userFilter)
    {
        if (autoFilter.Expression is null)
        {
            return userFilter;
        }

        if (userFilter?.Expression is null)
        {
            return autoFilter;
        }

        var combinedExpression = new QueryBinaryExpression(autoFilter.Expression, QueryBinaryOperator.And, userFilter.Expression);
        return new QueryFilter(combinedExpression);
    }

    private static FeatureQuery NormalizeBoundingBox(FeatureContext context, FeatureQuery query, string? targetCrs)
    {
        if (query.Bbox is null)
        {
            return query;
        }

        var bbox = query.Bbox;
        var normalizedBboxCrs = CrsHelper.NormalizeIdentifier(bbox.Crs ?? targetCrs);
        var storageSrid = ResolveStorageSrid(context.Layer);
        var storageCrs = CrsHelper.NormalizeIdentifier($"EPSG:{storageSrid}");

        var shouldPreproject = string.Equals(
            context.Provider.Provider,
            SqliteDataStoreProvider.ProviderKey,
            StringComparison.OrdinalIgnoreCase);

        if (!shouldPreproject)
        {
            if (!string.Equals(bbox.Crs, normalizedBboxCrs, StringComparison.OrdinalIgnoreCase))
            {
                bbox = bbox with { Crs = normalizedBboxCrs };
                query = query with { Bbox = bbox };
            }

            return query;
        }

        var bboxSrid = CrsHelper.ParseCrs(normalizedBboxCrs);
        if (bboxSrid == storageSrid)
        {
            if (!string.Equals(bbox.Crs, storageCrs, StringComparison.OrdinalIgnoreCase))
            {
                bbox = bbox with { Crs = storageCrs };
                query = query with { Bbox = bbox };
            }

            return query;
        }

        var transformed = CrsTransform.TransformEnvelope(bbox.MinX, bbox.MinY, bbox.MaxX, bbox.MaxY, bboxSrid, storageSrid);
        var projected = new BoundingBox(
            transformed.MinX,
            transformed.MinY,
            transformed.MaxX,
            transformed.MaxY,
            bbox.MinZ,
            bbox.MaxZ,
            storageCrs);

        return query with { Bbox = projected };
    }

    private static int ResolveStorageSrid(LayerDefinition layer)
    {
        if (layer.Storage?.Srid is int srid && srid > 0)
        {
            return srid;
        }

        if (!string.IsNullOrWhiteSpace(layer.Storage?.Crs))
        {
            return CrsHelper.ParseCrs(layer.Storage!.Crs);
        }

        if (layer.Crs.Count > 0)
        {
            return CrsHelper.ParseCrs(layer.Crs[0]);
        }

        return CrsHelper.Wgs84;
    }

    private static string ResolveTargetCrs(FeatureContext context, string? requestedCrs)
    {
        if (!string.IsNullOrWhiteSpace(requestedCrs))
        {
            return CrsHelper.NormalizeIdentifier(requestedCrs);
        }

        if (!string.IsNullOrWhiteSpace(context.Service.Ogc.DefaultCrs))
        {
            return CrsHelper.NormalizeIdentifier(context.Service.Ogc.DefaultCrs);
        }

        if (context.Layer.Crs.Count > 0)
        {
            return CrsHelper.NormalizeIdentifier(context.Layer.Crs[0]);
        }

        if (!string.IsNullOrWhiteSpace(context.Layer.Storage?.Crs))
        {
            return CrsHelper.NormalizeIdentifier(context.Layer.Storage!.Crs);
        }

        if (context.Layer.Storage?.Srid is int srid && srid > 0)
        {
            return CrsHelper.NormalizeIdentifier($"EPSG:{srid}");
        }

        return CrsHelper.DefaultCrsIdentifier;
    }

    public async Task<IReadOnlyList<StatisticsResult>> QueryStatisticsAsync(
        string serviceId,
        string layerId,
        IReadOnlyList<StatisticDefinition> statistics,
        IReadOnlyList<string>? groupByFields,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(serviceId);
        Guard.NotNullOrWhiteSpace(layerId);
        Guard.NotNull(statistics);
        var context = await ResolveContextAsync(serviceId, layerId, cancellationToken).ConfigureAwait(false);
        var effectiveQuery = NormalizeQuery(context, filter);
        return await context.Provider.QueryStatisticsAsync(
            context.DataSource,
            context.Service,
            context.Layer,
            statistics,
            groupByFields,
            effectiveQuery,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(
        string serviceId,
        string layerId,
        IReadOnlyList<string> fieldNames,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(serviceId);
        Guard.NotNullOrWhiteSpace(layerId);
        Guard.NotNull(fieldNames);
        var context = await ResolveContextAsync(serviceId, layerId, cancellationToken).ConfigureAwait(false);
        var effectiveQuery = NormalizeQuery(context, filter);
        return await context.Provider.QueryDistinctAsync(
            context.DataSource,
            context.Service,
            context.Layer,
            fieldNames,
            effectiveQuery,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<BoundingBox?> QueryExtentAsync(
        string serviceId,
        string layerId,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(serviceId);
        Guard.NotNullOrWhiteSpace(layerId);
        var context = await ResolveContextAsync(serviceId, layerId, cancellationToken).ConfigureAwait(false);
        var effectiveQuery = NormalizeQuery(context, filter);
        return await context.Provider.QueryExtentAsync(
            context.DataSource,
            context.Service,
            context.Layer,
            effectiveQuery,
            cancellationToken).ConfigureAwait(false);
    }

    private Task<FeatureContext> ResolveContextAsync(string serviceId, string layerId, CancellationToken cancellationToken)
        => _contextResolver.ResolveAsync(serviceId, layerId, cancellationToken);
}
