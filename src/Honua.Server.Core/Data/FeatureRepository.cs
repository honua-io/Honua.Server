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

public interface IFeatureRepository
{
    IAsyncEnumerable<FeatureRecord> QueryAsync(string serviceId, string layerId, FeatureQuery? query, CancellationToken cancellationToken = default);
    Task<long> CountAsync(string serviceId, string layerId, FeatureQuery? query, CancellationToken cancellationToken = default);
    Task<FeatureRecord?> GetAsync(string serviceId, string layerId, string featureId, FeatureQuery? query = null, CancellationToken cancellationToken = default);
    Task<FeatureRecord> CreateAsync(string serviceId, string layerId, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task<FeatureRecord?> UpdateAsync(string serviceId, string layerId, string featureId, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string serviceId, string layerId, string featureId, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateMvtTileAsync(string serviceId, string layerId, int zoom, int x, int y, string? datetime = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StatisticsResult>> QueryStatisticsAsync(string serviceId, string layerId, IReadOnlyList<StatisticDefinition> statistics, IReadOnlyList<string>? groupByFields, FeatureQuery? filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(string serviceId, string layerId, IReadOnlyList<string> fieldNames, FeatureQuery? filter, CancellationToken cancellationToken = default);
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
        return QueryInternalAsync(serviceId, layerId, query, cancellationToken);
    }

    public async Task<long> CountAsync(string serviceId, string layerId, FeatureQuery? query, CancellationToken cancellationToken = default)
    {
        var context = await ResolveContextAsync(serviceId, layerId, cancellationToken).ConfigureAwait(false);
        var effectiveQuery = NormalizeQuery(context, query);
        return await context.Provider.CountAsync(context.DataSource, context.Service, context.Layer, effectiveQuery, cancellationToken);
    }

    public async Task<FeatureRecord?> GetAsync(string serviceId, string layerId, string featureId, FeatureQuery? query = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(featureId);
        var context = await ResolveContextAsync(serviceId, layerId, cancellationToken).ConfigureAwait(false);
        var normalizedQuery = NormalizeQuery(context, query);
        return await context.Provider.GetAsync(context.DataSource, context.Service, context.Layer, featureId, normalizedQuery, cancellationToken);
    }

    public async Task<FeatureRecord> CreateAsync(string serviceId, string layerId, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(record);
        var context = await ResolveContextAsync(serviceId, layerId, cancellationToken).ConfigureAwait(false);
        return await context.Provider.CreateAsync(context.DataSource, context.Service, context.Layer, record, transaction, cancellationToken);
    }

    public async Task<FeatureRecord?> UpdateAsync(string serviceId, string layerId, string featureId, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(featureId);
        Guard.NotNull(record);
        var context = await ResolveContextAsync(serviceId, layerId, cancellationToken).ConfigureAwait(false);
        return await context.Provider.UpdateAsync(context.DataSource, context.Service, context.Layer, featureId, record, transaction, cancellationToken);
    }

    public async Task<bool> DeleteAsync(string serviceId, string layerId, string featureId, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(featureId);
        var context = await ResolveContextAsync(serviceId, layerId, cancellationToken).ConfigureAwait(false);
        return await context.Provider.DeleteAsync(context.DataSource, context.Service, context.Layer, featureId, transaction, cancellationToken);
    }

    public async Task<byte[]> GenerateMvtTileAsync(string serviceId, string layerId, int zoom, int x, int y, string? datetime = null, CancellationToken cancellationToken = default)
    {
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
