// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;

namespace Honua.Server.Core.Data.Query;

/// <summary>
/// Helper class to optimize database queries and prevent N+1 query problems.
/// </summary>
public static class QueryOptimizationHelper
{
    /// <summary>
    /// Batch load related entities to prevent N+1 queries.
    /// Instead of loading one item at a time in a loop, batch load all at once.
    /// </summary>
    /// <example>
    /// BEFORE (N+1 problem):
    /// foreach (var item in stacItems) {
    ///     var collection = await GetCollectionAsync(item.CollectionId);
    ///     // Process item with collection
    /// }
    ///
    /// AFTER (optimized):
    /// var collectionIds = stacItems.Select(i => i.CollectionId).Distinct();
    /// var collections = await BatchLoadCollectionsAsync(collectionIds);
    /// foreach (var item in stacItems) {
    ///     var collection = collections[item.CollectionId];
    ///     // Process item with collection
    /// }
    /// </example>
    public static async Task<Dictionary<TKey, TValue>> BatchLoadAsync<TKey, TValue>(
        IEnumerable<TKey> keys,
        Func<IEnumerable<TKey>, CancellationToken, Task<IEnumerable<TValue>>> loader,
        Func<TValue, TKey> keySelector,
        CancellationToken cancellationToken = default)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(keySelector);

        var distinctKeys = keys.Distinct().ToList();
        if (distinctKeys.Count == 0)
        {
            return new Dictionary<TKey, TValue>();
        }

        var values = await loader(distinctKeys, cancellationToken).ConfigureAwait(false);
        return values.ToDictionary(keySelector, v => v);
    }

    /// <summary>
    /// Batch load with chunking for very large key sets.
    /// Prevents oversized IN clauses that can cause SQL errors.
    /// </summary>
    /// <param name="keys">Keys to load</param>
    /// <param name="loader">Batch loader function</param>
    /// <param name="keySelector">Key extraction function</param>
    /// <param name="chunkSize">Maximum keys per batch (default 1000)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static async Task<Dictionary<TKey, TValue>> BatchLoadChunkedAsync<TKey, TValue>(
        IEnumerable<TKey> keys,
        Func<IEnumerable<TKey>, CancellationToken, Task<IEnumerable<TValue>>> loader,
        Func<TValue, TKey> keySelector,
        int chunkSize = 1000,
        CancellationToken cancellationToken = default)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(keySelector);

        if (chunkSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkSize), "Chunk size must be positive.");
        }

        var distinctKeys = keys.Distinct().ToList();
        if (distinctKeys.Count == 0)
        {
            return new Dictionary<TKey, TValue>();
        }

        var result = new Dictionary<TKey, TValue>();
        var chunks = ChunkKeys(distinctKeys, chunkSize);

        foreach (var chunk in chunks)
        {
            var values = await loader(chunk, cancellationToken).ConfigureAwait(false);
            foreach (var value in values)
            {
                var key = keySelector(value);
                result[key] = value;
            }
        }

        return result;
    }

    /// <summary>
    /// Create an optimized FeatureQuery that uses indexes effectively.
    /// </summary>
    public static FeatureQuery CreateOptimizedQuery(
        BoundingBox? bbox = null,
        IReadOnlyList<string>? propertyNames = null,
        int? limit = null,
        int? offset = null,
        string? sortBy = null)
    {
        var query = new FeatureQuery();

        // Use spatial indexes with bbox filtering
        if (bbox is not null)
        {
            query = query with { Bbox = bbox };
        }

        // Limit property selection to reduce data transfer
        if (propertyNames is not null && propertyNames.Count > 0)
        {
            query = query with { PropertyNames = propertyNames };
        }

        // Use pagination to prevent full table scans
        if (limit.HasValue)
        {
            query = query with { Limit = limit.Value };
        }

        if (offset.HasValue)
        {
            query = query with { Offset = offset.Value };
        }

        // Use indexed columns for sorting when possible
        if (!string.IsNullOrWhiteSpace(sortBy))
        {
            // Prefer indexed columns like created_at, updated_at, id
            var sortOrders = new List<FeatureSortOrder>
            {
                new FeatureSortOrder(sortBy, FeatureSortDirection.Ascending)
            };
            query = query with { SortOrders = sortOrders };
        }

        return query;
    }

    /// <summary>
    /// Optimize a spatial query to use GIST indexes effectively.
    /// </summary>
    public static string BuildOptimizedSpatialQuery(
        string tableName,
        string geometryColumn,
        BoundingBox bbox,
        int storageSrid,
        string? whereClause = null)
    {
        // Use && operator first (GIST index) then ST_Intersects for accuracy
        // This two-step approach is much faster than ST_Intersects alone
        var bboxFilter = $@"
            {geometryColumn} && ST_MakeEnvelope(
                {bbox.MinX}, {bbox.MinY},
                {bbox.MaxX}, {bbox.MaxY},
                {storageSrid}
            )
            AND ST_Intersects(
                {geometryColumn},
                ST_MakeEnvelope(
                    {bbox.MinX}, {bbox.MinY},
                    {bbox.MaxX}, {bbox.MaxY},
                    {storageSrid}
                )
            )";

        var where = string.IsNullOrWhiteSpace(whereClause)
            ? bboxFilter
            : $"{whereClause} AND {bboxFilter}";

        // Note: This is a helper method. Callers should replace * with explicit column lists
        // based on their specific table schema to avoid bandwidth waste.
        // Example: SELECT id, name, {geometryColumn}, properties FROM {tableName} WHERE {where}
        return $"SELECT * FROM {tableName} WHERE {where}";
    }

    /// <summary>
    /// Check if a query will benefit from an index hint.
    /// PostgreSQL: Use EXPLAIN to analyze query plans.
    /// </summary>
    public static bool ShouldUseIndexHint(
        string provider,
        int estimatedRows,
        bool hasSpatialFilter,
        bool hasTemporalFilter)
    {
        // For PostgreSQL, trust the query planner for most queries
        if (string.Equals(provider, "postgis", StringComparison.OrdinalIgnoreCase))
        {
            // Only hint for very specific cases
            return false;
        }

        // For SQL Server, use index hints for large spatial queries
        if (string.Equals(provider, "sqlserver", StringComparison.OrdinalIgnoreCase))
        {
            return hasSpatialFilter && estimatedRows > 10000;
        }

        // For MySQL, hint for complex queries
        if (string.Equals(provider, "mysql", StringComparison.OrdinalIgnoreCase))
        {
            return (hasSpatialFilter || hasTemporalFilter) && estimatedRows > 5000;
        }

        return false;
    }

    /// <summary>
    /// Get recommended batch size for the database provider.
    /// </summary>
    public static int GetRecommendedBatchSize(string provider)
    {
        return provider?.ToLowerInvariant() switch
        {
            "postgis" => 1000,     // PostgreSQL handles large IN clauses well
            "sqlserver" => 500,     // SQL Server has 2100 parameter limit
            "mysql" => 1000,        // MySQL handles large batches
            "sqlite" => 500,        // SQLite has variable limits
            "oracle" => 1000,       // Oracle handles large IN clauses
            _ => 500                // Conservative default
        };
    }

    private static IEnumerable<List<T>> ChunkKeys<T>(IList<T> keys, int chunkSize)
    {
        for (var i = 0; i < keys.Count; i += chunkSize)
        {
            var chunk = new List<T>(Math.Min(chunkSize, keys.Count - i));
            var endIndex = Math.Min(i + chunkSize, keys.Count);
            for (var j = i; j < endIndex; j++)
            {
                chunk.Add(keys[j]);
            }
            yield return chunk;
        }
    }

    /// <summary>
    /// Create an EXISTS subquery instead of IN for better performance on large datasets.
    /// EXISTS is typically faster because it can short-circuit.
    /// </summary>
    /// <example>
    /// BEFORE (slow for large lists):
    /// SELECT id, name, geometry FROM features WHERE id IN (1, 2, 3, ..., 10000)
    ///
    /// AFTER (faster with EXISTS):
    /// SELECT id, name, geometry FROM features f WHERE EXISTS (
    ///     SELECT 1 FROM temp_ids t WHERE t.id = f.id
    /// )
    /// </example>
    /// <remarks>
    /// Note: This helper returns SELECT * for simplicity. Callers should replace * with
    /// explicit column lists based on their table schema to reduce bandwidth usage.
    /// </remarks>
    public static string BuildExistsQuery(
        string tableName,
        string keyColumn,
        string tempTableName)
    {
        // Note: Callers should replace * with explicit column list for production use
        return $@"
            SELECT * FROM {tableName} t
            WHERE EXISTS (
                SELECT 1
                FROM {tempTableName} tmp
                WHERE tmp.id = t.{keyColumn}
            )";
    }

    /// <summary>
    /// Estimate query selectivity to help optimize index usage.
    /// High selectivity (close to 1.0) means few rows match - good for indexes.
    /// Low selectivity (close to 0.0) means many rows match - may skip index.
    /// </summary>
    public static double EstimateSelectivity(
        long totalRows,
        long matchingRows)
    {
        if (totalRows == 0)
        {
            return 0.0;
        }

        return (double)matchingRows / totalRows;
    }

    /// <summary>
    /// Determine if a full table scan would be faster than index scan.
    /// </summary>
    public static bool ShouldUseFullTableScan(
        long totalRows,
        long estimatedMatchingRows,
        double indexSelectivityThreshold = 0.25)
    {
        // If more than 25% of rows match, full scan is often faster
        var selectivity = EstimateSelectivity(totalRows, estimatedMatchingRows);
        return selectivity > indexSelectivityThreshold;
    }
}
