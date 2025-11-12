// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Data.Sqlite;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;

using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    // Slow query logging threshold lowered from 1000ms to 500ms for tighter performance monitoring
    private const int SlowQueryThresholdMs = 500;

    private readonly IFeatureContextResolver _contextResolver;
    private readonly ILogger<FeatureRepository> _logger;
    private readonly QueryTimeoutOptions _timeoutOptions;
    private readonly IQueryMetrics? _queryMetrics;

    public FeatureRepository(
        IFeatureContextResolver contextResolver,
        ILogger<FeatureRepository> logger,
        IOptions<QueryTimeoutOptions> timeoutOptions,
        IQueryMetrics? queryMetrics = null)
    {
        _contextResolver = contextResolver ?? throw new ArgumentNullException(nameof(contextResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeoutOptions = timeoutOptions?.Value ?? new QueryTimeoutOptions();
        _queryMetrics = queryMetrics; // Optional to maintain backward compatibility
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

        return await ExecuteWithTimeoutAsync(
            QueryOperationType.Count,
            serviceId,
            layerId,
            async (effectiveQuery, timeoutCts) =>
            {
                var context = await ResolveContextAsync(serviceId, layerId, timeoutCts.Token).ConfigureAwait(false);
                var normalizedQuery = NormalizeQuery(context, query);

                // Apply timeout to query if not already specified
                if (!normalizedQuery.CommandTimeout.HasValue)
                {
                    normalizedQuery = normalizedQuery with { CommandTimeout = _timeoutOptions.GetTimeoutForOperation(QueryOperationType.Count) };
                }

                return await context.Provider.CountAsync(context.DataSource, context.Service, context.Layer, normalizedQuery, timeoutCts.Token);
            },
            query,
            cancellationToken).ConfigureAwait(false);
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

        _logger.LogInformation("Creating feature in {ServiceId}/{LayerId}", serviceId, layerId);

        try
        {
            var context = await ResolveContextAsync(serviceId, layerId, cancellationToken).ConfigureAwait(false);
            var result = await context.Provider.CreateAsync(context.DataSource, context.Service, context.Layer, record, transaction, cancellationToken);
            var featureId = result.Attributes.TryGetValue(context.Layer.IdField, out var pk) ? pk : "unknown";
            _logger.LogInformation("Successfully created feature {FeatureId} in {ServiceId}/{LayerId}",
                featureId, serviceId, layerId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create feature in {ServiceId}/{LayerId}", serviceId, layerId);
            throw;
        }
    }

    public async Task<FeatureRecord?> UpdateAsync(string serviceId, string layerId, string featureId, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(serviceId);
        Guard.NotNullOrWhiteSpace(layerId);
        Guard.NotNullOrWhiteSpace(featureId);
        Guard.NotNull(record);

        _logger.LogInformation("Updating feature {FeatureId} in {ServiceId}/{LayerId}", featureId, serviceId, layerId);

        try
        {
            var context = await ResolveContextAsync(serviceId, layerId, cancellationToken).ConfigureAwait(false);
            var result = await context.Provider.UpdateAsync(context.DataSource, context.Service, context.Layer, featureId, record, transaction, cancellationToken);

            if (result != null)
            {
                _logger.LogInformation("Successfully updated feature {FeatureId} in {ServiceId}/{LayerId}", featureId, serviceId, layerId);
            }
            else
            {
                _logger.LogWarning("Feature {FeatureId} not found for update in {ServiceId}/{LayerId}", featureId, serviceId, layerId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update feature {FeatureId} in {ServiceId}/{LayerId}", featureId, serviceId, layerId);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(string serviceId, string layerId, string featureId, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(serviceId);
        Guard.NotNullOrWhiteSpace(layerId);
        Guard.NotNullOrWhiteSpace(featureId);

        _logger.LogInformation("Deleting feature {FeatureId} from {ServiceId}/{LayerId}", featureId, serviceId, layerId);

        try
        {
            var context = await ResolveContextAsync(serviceId, layerId, cancellationToken).ConfigureAwait(false);
            var deleted = await context.Provider.DeleteAsync(context.DataSource, context.Service, context.Layer, featureId, transaction, cancellationToken);

            if (deleted)
            {
                _logger.LogInformation("Successfully deleted feature {FeatureId} from {ServiceId}/{LayerId}", featureId, serviceId, layerId);
            }
            else
            {
                _logger.LogWarning("Feature {FeatureId} not found for deletion in {ServiceId}/{LayerId}", featureId, serviceId, layerId);
            }

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete feature {FeatureId} from {ServiceId}/{LayerId}", featureId, serviceId, layerId);
            throw;
        }
    }

    public async Task<byte[]> GenerateMvtTileAsync(string serviceId, string layerId, int zoom, int x, int y, string? datetime = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(serviceId);
        Guard.NotNullOrWhiteSpace(layerId);

        return await ExecuteWithTimeoutAsync(
            QueryOperationType.TileGeneration,
            serviceId,
            layerId,
            async (query, timeoutCts) =>
            {
                var context = await ResolveContextAsync(serviceId, layerId, timeoutCts.Token).ConfigureAwait(false);

                // Try provider's native MVT generation with temporal filter
                var mvtBytes = await context.Provider.GenerateMvtTileAsync(
                    context.DataSource,
                    context.Service,
                    context.Layer,
                    zoom,
                    x,
                    y,
                    datetime,
                    timeoutCts.Token).ConfigureAwait(false);

                if (mvtBytes is not null)
                {
                    return mvtBytes;
                }

                // Fall back to empty tile (NTS fallback not yet implemented)
                return Array.Empty<byte>();
            },
            null,
            cancellationToken,
            $"zoom={zoom}, x={x}, y={y}").ConfigureAwait(false);
    }

    private async IAsyncEnumerable<FeatureRecord> QueryInternalAsync(
        string serviceId,
        string layerId,
        FeatureQuery? query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var timeout = _timeoutOptions.GetTimeoutForOperation(QueryOperationType.SimpleQuery);
        var warningThreshold = _timeoutOptions.GetWarningThreshold(QueryOperationType.SimpleQuery);
        var stopwatch = Stopwatch.StartNew();

        // Create linked cancellation token source combining timeout and external cancellation
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        // Start activity for distributed tracing
        using var activity = HonuaTelemetry.Database.StartActivity($"Query.{QueryOperationType.SimpleQuery}");
        activity?.SetTag("service.id", serviceId);
        activity?.SetTag("layer.id", layerId);
        activity?.SetTag("operation.type", QueryOperationType.SimpleQuery.ToString());

        // Analyze filter complexity for metrics
        var hasFilter = query?.Filter?.Expression != null;
        var hasSpatialFilter = query?.Bbox != null;
        var filterComplexity = DetermineFilterComplexity(query);

        if (_timeoutOptions.EnableDetailedLogging)
        {
            _logger.LogDebug(
                "Starting {OperationType} for {ServiceId}/{LayerId} with timeout {TimeoutSeconds}s",
                QueryOperationType.SimpleQuery,
                serviceId,
                layerId,
                timeout.TotalSeconds);
        }

        var recordCount = 0L;
        var success = false;

        try
        {
            var context = await ResolveContextAsync(serviceId, layerId, timeoutCts.Token).ConfigureAwait(false);
            var effectiveQuery = NormalizeQuery(context, query);

            // Apply timeout to query if not already specified
            if (!effectiveQuery.CommandTimeout.HasValue)
            {
                effectiveQuery = effectiveQuery with { CommandTimeout = _timeoutOptions.GetTimeoutForOperation(QueryOperationType.SimpleQuery) };
            }

            await foreach (var record in context.Provider.QueryAsync(context.DataSource, context.Service, context.Layer, effectiveQuery, timeoutCts.Token).ConfigureAwait(false))
            {
                recordCount++;
                yield return record;
            }

            stopwatch.Stop();
            success = true;

            var elapsed = stopwatch.Elapsed;
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            // Record query duration and result metrics
            _queryMetrics?.RecordQueryDuration(
                serviceId,
                layerId,
                "Repository",
                QueryOperationType.SimpleQuery.ToString(),
                elapsed,
                success);

            _queryMetrics?.RecordQueryResults(
                serviceId,
                layerId,
                "Repository",
                recordCount);

            _queryMetrics?.RecordFilterComplexity(
                serviceId,
                layerId,
                "Repository",
                hasFilter,
                hasSpatialFilter,
                filterComplexity);

            // CRITICAL: Log slow query warning if 500ms threshold exceeded
            if (elapsedMs >= SlowQueryThresholdMs)
            {
                _logger.LogWarning(
                    "SLOW QUERY: {OperationType} for {ServiceId}/{LayerId} took {ElapsedMs}ms (threshold: {ThresholdMs}ms), returned {RecordCount} records. " +
                    "Filter: {HasFilter}, Spatial: {HasSpatial}, Complexity: {Complexity}",
                    QueryOperationType.SimpleQuery,
                    serviceId,
                    layerId,
                    elapsedMs,
                    SlowQueryThresholdMs,
                    recordCount,
                    hasFilter,
                    hasSpatialFilter,
                    filterComplexity);

                _queryMetrics?.RecordSlowQuery(
                    serviceId,
                    layerId,
                    "Repository",
                    QueryOperationType.SimpleQuery.ToString(),
                    elapsed,
                    TimeSpan.FromMilliseconds(SlowQueryThresholdMs));

                activity?.SetTag("query.slow", true);
            }
            // Log timeout warning if operation approached timeout threshold
            else if (elapsed >= warningThreshold && _timeoutOptions.EnableDetailedLogging)
            {
                _logger.LogWarning(
                    "Query approaching timeout: {OperationType} for {ServiceId}/{LayerId} took {ElapsedMs}ms ({ElapsedPercent:F1}% of {TimeoutSeconds}s timeout), returned {RecordCount} records",
                    QueryOperationType.SimpleQuery,
                    serviceId,
                    layerId,
                    elapsedMs,
                    (elapsed.TotalMilliseconds / timeout.TotalMilliseconds) * 100,
                    timeout.TotalSeconds,
                    recordCount);
            }
            else if (_timeoutOptions.EnableDetailedLogging)
            {
                _logger.LogDebug(
                    "{OperationType} for {ServiceId}/{LayerId} completed in {ElapsedMs}ms, returned {RecordCount} records",
                    QueryOperationType.SimpleQuery,
                    serviceId,
                    layerId,
                    elapsedMs,
                    recordCount);
            }

            activity?.SetTag("query.record_count", recordCount);
        }
        catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred (internal cancellation), not external cancellation
            stopwatch.Stop();

            // Record timeout metrics
            _queryMetrics?.RecordQueryDuration(
                serviceId,
                layerId,
                "Repository",
                QueryOperationType.SimpleQuery.ToString(),
                stopwatch.Elapsed,
                false);

            _queryMetrics?.RecordQueryError(
                serviceId,
                layerId,
                "Repository",
                QueryOperationType.SimpleQuery.ToString(),
                "Timeout");

            _logger.LogError(
                ex,
                "QUERY TIMEOUT: {OperationType} for {ServiceId}/{LayerId} exceeded {TimeoutSeconds}s timeout after {ElapsedMs}ms, {RecordCount} records returned before timeout. " +
                "Filter: {HasFilter}, Spatial: {HasSpatial}, Complexity: {Complexity}",
                QueryOperationType.SimpleQuery,
                serviceId,
                layerId,
                timeout.TotalSeconds,
                stopwatch.ElapsedMilliseconds,
                recordCount,
                hasFilter,
                hasSpatialFilter,
                filterComplexity);

            activity?.SetStatus(ActivityStatusCode.Error, $"Query timeout after {stopwatch.ElapsedMilliseconds}ms");
            activity?.SetTag("query.record_count", recordCount);

            throw new TimeoutException(
                $"{QueryOperationType.SimpleQuery} operation for {serviceId}/{layerId} exceeded {timeout.TotalSeconds}s timeout after returning {recordCount} records. " +
                $"Consider adding filters, reducing the result set, or increasing the timeout in configuration.",
                ex);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Record error metrics
            _queryMetrics?.RecordQueryDuration(
                serviceId,
                layerId,
                "Repository",
                QueryOperationType.SimpleQuery.ToString(),
                stopwatch.Elapsed,
                false);

            _queryMetrics?.RecordQueryError(
                serviceId,
                layerId,
                "Repository",
                QueryOperationType.SimpleQuery.ToString(),
                ex.GetType().Name);

            _logger.LogError(
                ex,
                "QUERY ERROR: {OperationType} for {ServiceId}/{LayerId} failed after {ElapsedMs}ms, {RecordCount} records returned before failure. " +
                "Error: {ErrorType}",
                QueryOperationType.SimpleQuery,
                serviceId,
                layerId,
                stopwatch.ElapsedMilliseconds,
                recordCount,
                ex.GetType().Name);

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("query.record_count", recordCount);

            throw;
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

        return await ExecuteWithTimeoutAsync(
            QueryOperationType.Statistics,
            serviceId,
            layerId,
            async (effectiveQuery, timeoutCts) =>
            {
                var context = await ResolveContextAsync(serviceId, layerId, timeoutCts.Token).ConfigureAwait(false);
                var normalizedQuery = NormalizeQuery(context, filter);

                // Apply timeout to query if not already specified
                if (!normalizedQuery.CommandTimeout.HasValue)
                {
                    normalizedQuery = normalizedQuery with { CommandTimeout = _timeoutOptions.GetTimeoutForOperation(QueryOperationType.Statistics) };
                }

                return await context.Provider.QueryStatisticsAsync(
                    context.DataSource,
                    context.Service,
                    context.Layer,
                    statistics,
                    groupByFields,
                    normalizedQuery,
                    timeoutCts.Token).ConfigureAwait(false);
            },
            filter,
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

        return await ExecuteWithTimeoutAsync(
            QueryOperationType.Distinct,
            serviceId,
            layerId,
            async (effectiveQuery, timeoutCts) =>
            {
                var context = await ResolveContextAsync(serviceId, layerId, timeoutCts.Token).ConfigureAwait(false);
                var normalizedQuery = NormalizeQuery(context, filter);

                // Apply timeout to query if not already specified
                if (!normalizedQuery.CommandTimeout.HasValue)
                {
                    normalizedQuery = normalizedQuery with { CommandTimeout = _timeoutOptions.GetTimeoutForOperation(QueryOperationType.Distinct) };
                }

                return await context.Provider.QueryDistinctAsync(
                    context.DataSource,
                    context.Service,
                    context.Layer,
                    fieldNames,
                    normalizedQuery,
                    timeoutCts.Token).ConfigureAwait(false);
            },
            filter,
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

        return await ExecuteWithTimeoutAsync(
            QueryOperationType.ExtentCalculation,
            serviceId,
            layerId,
            async (effectiveQuery, timeoutCts) =>
            {
                var context = await ResolveContextAsync(serviceId, layerId, timeoutCts.Token).ConfigureAwait(false);
                var normalizedQuery = NormalizeQuery(context, filter);

                // Apply timeout to query if not already specified
                if (!normalizedQuery.CommandTimeout.HasValue)
                {
                    normalizedQuery = normalizedQuery with { CommandTimeout = _timeoutOptions.GetTimeoutForOperation(QueryOperationType.ExtentCalculation) };
                }

                return await context.Provider.QueryExtentAsync(
                    context.DataSource,
                    context.Service,
                    context.Layer,
                    normalizedQuery,
                    timeoutCts.Token).ConfigureAwait(false);
            },
            filter,
            cancellationToken).ConfigureAwait(false);
    }

    private Task<FeatureContext> ResolveContextAsync(string serviceId, string layerId, CancellationToken cancellationToken)
        => _contextResolver.ResolveAsync(serviceId, layerId, cancellationToken);

    /// <summary>
    /// Executes a query operation with configurable timeout, logging, and OpenTelemetry metrics.
    /// Provides timeout enforcement, warning logs, slow query detection (500ms threshold),
    /// detailed error messages, and performance breakdown tracking.
    /// </summary>
    /// <typeparam name="TResult">The type of result returned by the operation.</typeparam>
    /// <param name="operationType">The type of query operation being performed.</param>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="layerId">The layer identifier.</param>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="query">The optional query parameters.</param>
    /// <param name="cancellationToken">The external cancellation token.</param>
    /// <param name="additionalContext">Additional context for logging (e.g., tile coordinates).</param>
    /// <param name="endpointType">The endpoint type for metrics (e.g., "WFS", "OGC-API", "GeoServices").</param>
    /// <returns>The result of the operation.</returns>
    private async Task<TResult> ExecuteWithTimeoutAsync<TResult>(
        QueryOperationType operationType,
        string serviceId,
        string layerId,
        Func<FeatureQuery?, CancellationTokenSource, Task<TResult>> operation,
        FeatureQuery? query,
        CancellationToken cancellationToken,
        string? additionalContext = null,
        string endpointType = "Repository")
    {
        var timeout = _timeoutOptions.GetTimeoutForOperation(operationType);
        var warningThreshold = _timeoutOptions.GetWarningThreshold(operationType);
        var stopwatch = Stopwatch.StartNew();
        var success = false;

        // Create linked cancellation token source combining timeout and external cancellation
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        // Start activity for distributed tracing
        using var activity = HonuaTelemetry.Database.StartActivity($"Query.{operationType}");
        activity?.SetTag("service.id", serviceId);
        activity?.SetTag("layer.id", layerId);
        activity?.SetTag("operation.type", operationType.ToString());
        activity?.SetTag("endpoint.type", endpointType);

        // Analyze filter complexity for metrics
        var hasFilter = query?.Filter?.Expression != null;
        var hasSpatialFilter = query?.Bbox != null;
        var filterComplexity = DetermineFilterComplexity(query);

        if (_timeoutOptions.EnableDetailedLogging)
        {
            _logger.LogDebug(
                "Starting {OperationType} for {ServiceId}/{LayerId} with timeout {TimeoutSeconds}s{AdditionalContext}",
                operationType,
                serviceId,
                layerId,
                timeout.TotalSeconds,
                string.IsNullOrEmpty(additionalContext) ? string.Empty : $" ({additionalContext})");
        }

        try
        {
            var result = await operation(query, timeoutCts).ConfigureAwait(false);

            stopwatch.Stop();
            success = true;

            var elapsed = stopwatch.Elapsed;
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            // Record query duration metrics
            _queryMetrics?.RecordQueryDuration(
                serviceId,
                layerId,
                endpointType,
                operationType.ToString(),
                elapsed,
                success);

            // Record filter complexity metrics
            _queryMetrics?.RecordFilterComplexity(
                serviceId,
                layerId,
                endpointType,
                hasFilter,
                hasSpatialFilter,
                filterComplexity);

            // CRITICAL: Log slow query warning if 500ms threshold exceeded
            if (elapsedMs >= SlowQueryThresholdMs)
            {
                _logger.LogWarning(
                    "SLOW QUERY: {OperationType} for {ServiceId}/{LayerId} took {ElapsedMs}ms (threshold: {ThresholdMs}ms). " +
                    "Filter: {HasFilter}, Spatial: {HasSpatial}, Complexity: {Complexity}{AdditionalContext}",
                    operationType,
                    serviceId,
                    layerId,
                    elapsedMs,
                    SlowQueryThresholdMs,
                    hasFilter,
                    hasSpatialFilter,
                    filterComplexity,
                    string.IsNullOrEmpty(additionalContext) ? string.Empty : $" | {additionalContext}");

                // Record slow query metric
                _queryMetrics?.RecordSlowQuery(
                    serviceId,
                    layerId,
                    endpointType,
                    operationType.ToString(),
                    elapsed,
                    TimeSpan.FromMilliseconds(SlowQueryThresholdMs));

                activity?.SetTag("query.slow", true);
            }
            // Log timeout warning if operation approached timeout threshold (75% by default)
            else if (elapsed >= warningThreshold && _timeoutOptions.EnableDetailedLogging)
            {
                _logger.LogWarning(
                    "Query approaching timeout: {OperationType} for {ServiceId}/{LayerId} took {ElapsedMs}ms ({ElapsedPercent:F1}% of {TimeoutSeconds}s timeout){AdditionalContext}",
                    operationType,
                    serviceId,
                    layerId,
                    elapsedMs,
                    (elapsed.TotalMilliseconds / timeout.TotalMilliseconds) * 100,
                    timeout.TotalSeconds,
                    string.IsNullOrEmpty(additionalContext) ? string.Empty : $" ({additionalContext})");
            }
            else if (_timeoutOptions.EnableDetailedLogging)
            {
                _logger.LogDebug(
                    "{OperationType} for {ServiceId}/{LayerId} completed in {ElapsedMs}ms{AdditionalContext}",
                    operationType,
                    serviceId,
                    layerId,
                    elapsedMs,
                    string.IsNullOrEmpty(additionalContext) ? string.Empty : $" ({additionalContext})");
            }

            return result;
        }
        catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred (internal cancellation), not external cancellation
            stopwatch.Stop();

            // Record timeout metrics
            _queryMetrics?.RecordQueryDuration(
                serviceId,
                layerId,
                endpointType,
                operationType.ToString(),
                stopwatch.Elapsed,
                false);

            _queryMetrics?.RecordQueryError(
                serviceId,
                layerId,
                endpointType,
                operationType.ToString(),
                "Timeout");

            _logger.LogError(
                ex,
                "QUERY TIMEOUT: {OperationType} for {ServiceId}/{LayerId} exceeded {TimeoutSeconds}s timeout after {ElapsedMs}ms. " +
                "Filter: {HasFilter}, Spatial: {HasSpatial}, Complexity: {Complexity}{AdditionalContext}",
                operationType,
                serviceId,
                layerId,
                timeout.TotalSeconds,
                stopwatch.ElapsedMilliseconds,
                hasFilter,
                hasSpatialFilter,
                filterComplexity,
                string.IsNullOrEmpty(additionalContext) ? string.Empty : $" | {additionalContext}");

            activity?.SetStatus(ActivityStatusCode.Error, $"Query timeout after {stopwatch.ElapsedMilliseconds}ms");

            throw new TimeoutException(
                $"{operationType} operation for {serviceId}/{layerId} exceeded {timeout.TotalSeconds}s timeout. " +
                $"Consider optimizing the query, adding indexes, or increasing the timeout in configuration.",
                ex);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Record error metrics
            _queryMetrics?.RecordQueryDuration(
                serviceId,
                layerId,
                endpointType,
                operationType.ToString(),
                stopwatch.Elapsed,
                false);

            _queryMetrics?.RecordQueryError(
                serviceId,
                layerId,
                endpointType,
                operationType.ToString(),
                ex.GetType().Name);

            _logger.LogError(
                ex,
                "QUERY ERROR: {OperationType} for {ServiceId}/{LayerId} failed after {ElapsedMs}ms. " +
                "Error: {ErrorType}{AdditionalContext}",
                operationType,
                serviceId,
                layerId,
                stopwatch.ElapsedMilliseconds,
                ex.GetType().Name,
                string.IsNullOrEmpty(additionalContext) ? string.Empty : $" | {additionalContext}");

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            throw;
        }
    }

    /// <summary>
    /// Determines filter complexity based on query parameters for metrics and logging.
    /// Simple: No filter or bbox only
    /// Medium: Single attribute filter or bbox with simple attribute filter
    /// Complex: Multiple filters, complex CQL expressions, or combination of spatial and attribute filters
    /// </summary>
    private static string DetermineFilterComplexity(FeatureQuery? query)
    {
        if (query == null)
            return "simple";

        var hasFilter = query.Filter?.Expression != null;
        var hasBbox = query.Bbox != null;

        if (!hasFilter && !hasBbox)
            return "simple";

        if (hasBbox && !hasFilter)
            return "simple";

        if (hasFilter && !hasBbox)
        {
            // Analyze filter expression depth/complexity
            // This is a simplified heuristic - could be enhanced with actual expression analysis
            return "medium";
        }

        // Both spatial and attribute filters
        return "complex";
    }
}
