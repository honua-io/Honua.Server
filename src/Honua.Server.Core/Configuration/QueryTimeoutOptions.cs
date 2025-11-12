// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Configuration options for database query timeouts across different operation types.
/// Provides hierarchical timeout configuration: global defaults with per-operation overrides.
/// </summary>
/// <remarks>
/// Query timeouts prevent long-running database operations from blocking resources and degrading
/// overall system performance. Different operation types have different expected execution times:
/// - Simple queries: Fast record retrieval (15 seconds default)
/// - Statistics/Aggregations: Complex analytical queries (60 seconds default)
/// - Tile generation: Spatial tile rendering (10 seconds default)
/// - Extent calculations: Bounding box computation (30 seconds default)
/// - Count operations: Record counting (30 seconds default)
/// - Distinct queries: Unique value retrieval (30 seconds default)
///
/// Timeouts are enforced using CancellationTokenSource.CancelAfter and will throw
/// OperationCanceledException when exceeded, with detailed logging for troubleshooting.
/// </remarks>
public sealed class QueryTimeoutOptions
{
    /// <summary>
    /// Global default timeout for all query operations when no specific timeout is configured.
    /// Default: 30 seconds.
    /// This acts as a safety net for operations without explicit timeout configuration.
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Timeout for simple feature queries (QueryAsync, GetAsync).
    /// These are typically fast indexed lookups with filters and spatial predicates.
    /// Default: 15 seconds.
    /// </summary>
    /// <remarks>
    /// Increase this if you have:
    /// - Large tables (>10M records) with complex spatial queries
    /// - Slow storage (network-attached storage, distributed databases)
    /// - Complex CQL filters requiring full table scans
    /// </remarks>
    public TimeSpan SimpleQueryTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Timeout for statistics and aggregation queries (QueryStatisticsAsync).
    /// These operations perform SUM, AVG, MIN, MAX, COUNT with optional GROUP BY.
    /// Default: 60 seconds (1 minute).
    /// </summary>
    /// <remarks>
    /// Statistics queries are computationally expensive and may require:
    /// - Full table scans for unindexed fields
    /// - Large sort operations for GROUP BY
    /// - Multiple passes over data for percentile calculations
    ///
    /// Consider increasing to 120-300 seconds for:
    /// - Very large datasets (>100M records)
    /// - Complex multi-field groupings
    /// - Analytical workloads on cold data
    /// </remarks>
    public TimeSpan StatisticsTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Timeout for vector tile generation (GenerateMvtTileAsync).
    /// Tiles should render quickly to support interactive map panning.
    /// Default: 10 seconds.
    /// </summary>
    /// <remarks>
    /// Tile generation is time-sensitive for user experience:
    /// - 10 seconds allows for complex vector simplification
    /// - Cache misses trigger on-demand rendering
    /// - Slow tiles indicate missing spatial indexes or excessive feature density
    ///
    /// If tiles consistently timeout:
    /// 1. Verify spatial indexes exist on geometry columns
    /// 2. Consider pre-generating tiles for high-traffic zoom levels
    /// 3. Apply feature density limits (maxAllowableOffset)
    /// 4. Use tile-based caching strategies
    /// </remarks>
    public TimeSpan TileGenerationTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Timeout for spatial extent calculations (QueryExtentAsync).
    /// Computes bounding box using database-level ST_Extent aggregation.
    /// Default: 30 seconds.
    /// </summary>
    /// <remarks>
    /// Extent calculations are moderately expensive:
    /// - PostgreSQL: Uses ST_Extent aggregate (optimized with spatial index)
    /// - SQL Server: Uses geometry::EnvelopeAggregate()
    /// - Requires scanning all matching geometries
    ///
    /// Increase timeout for:
    /// - Large polygon/multipolygon layers (parcels, boundaries)
    /// - Tables with >10M features
    /// - Complex filters without spatial indexes
    /// </remarks>
    public TimeSpan ExtentCalculationTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Timeout for count operations (CountAsync).
    /// Count queries may require full table scans for complex filters.
    /// Default: 30 seconds.
    /// </summary>
    /// <remarks>
    /// Count performance depends on filter complexity:
    /// - Simple counts with spatial index: Sub-second
    /// - Complex CQL filters: May require sequential scan
    /// - Very large tables (>50M records): Consider approximate counts
    ///
    /// Optimization strategies:
    /// - Use table statistics for approximate counts on large datasets
    /// - Create partial indexes for frequently filtered subsets
    /// - Avoid counting when not required (OGC API Features numberMatched)
    /// </remarks>
    public TimeSpan CountTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Timeout for distinct value queries (QueryDistinctAsync).
    /// Retrieves unique values for fields, useful for filter dropdowns.
    /// Default: 30 seconds.
    /// </summary>
    /// <remarks>
    /// DISTINCT queries performance characteristics:
    /// - Indexed fields: Fast (uses index scan)
    /// - Non-indexed fields: Requires full table scan + sort
    /// - High cardinality fields: Memory-intensive
    ///
    /// Best practices:
    /// - Exclude geometry and large BLOB/TEXT columns
    /// - Create indexes on frequently queried distinct fields
    /// - Limit DISTINCT to categorical fields (status, type, category)
    /// - Consider materialized views for expensive distinct queries
    /// </remarks>
    public TimeSpan DistinctTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Timeout warning threshold as a percentage of the configured timeout.
    /// Logs warnings when queries exceed this threshold to identify slow operations.
    /// Default: 0.75 (75% of timeout).
    /// </summary>
    /// <remarks>
    /// Warning threshold helps identify operations approaching timeout:
    /// - 75% threshold: Query at 22.5s of 30s timeout triggers warning
    /// - Provides early visibility into performance degradation
    /// - Enables proactive optimization before timeouts occur
    ///
    /// Set to 0 to disable timeout warnings (not recommended).
    /// Set to 0.5 for more aggressive warning (50% threshold).
    /// </remarks>
    public double TimeoutWarningThreshold { get; set; } = 0.75;

    /// <summary>
    /// Enable detailed logging for timeout events and warnings.
    /// Logs include operation type, elapsed time, and query details.
    /// Default: true.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = true;

    /// <summary>
    /// Enable metrics collection for query timeouts.
    /// Tracks timeout frequency, operation types, and execution times.
    /// Default: true.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Gets the appropriate timeout for a specific operation type.
    /// </summary>
    /// <param name="operationType">The type of database operation being performed.</param>
    /// <returns>The configured timeout for the operation type.</returns>
    public TimeSpan GetTimeoutForOperation(QueryOperationType operationType)
    {
        return operationType switch
        {
            QueryOperationType.SimpleQuery => SimpleQueryTimeout,
            QueryOperationType.Statistics => StatisticsTimeout,
            QueryOperationType.TileGeneration => TileGenerationTimeout,
            QueryOperationType.ExtentCalculation => ExtentCalculationTimeout,
            QueryOperationType.Count => CountTimeout,
            QueryOperationType.Distinct => DistinctTimeout,
            _ => DefaultTimeout
        };
    }

    /// <summary>
    /// Calculates the warning threshold time for a given operation type.
    /// Returns the elapsed time at which a warning should be logged.
    /// </summary>
    /// <param name="operationType">The type of database operation.</param>
    /// <returns>The threshold time for logging warnings.</returns>
    public TimeSpan GetWarningThreshold(QueryOperationType operationType)
    {
        var timeout = GetTimeoutForOperation(operationType);
        return TimeSpan.FromMilliseconds(timeout.TotalMilliseconds * TimeoutWarningThreshold);
    }
}

/// <summary>
/// Types of database query operations with distinct performance characteristics.
/// Used to apply appropriate timeout configuration based on expected execution time.
/// </summary>
public enum QueryOperationType
{
    /// <summary>
    /// Simple feature queries: QueryAsync, GetAsync.
    /// Fast indexed lookups with filters and spatial predicates.
    /// </summary>
    SimpleQuery,

    /// <summary>
    /// Statistical aggregations: SUM, AVG, MIN, MAX, COUNT with GROUP BY.
    /// Computationally expensive, may require full table scans.
    /// </summary>
    Statistics,

    /// <summary>
    /// Vector tile generation: MVT (Mapbox Vector Tile) rendering.
    /// Time-sensitive for interactive map panning, should be fast.
    /// </summary>
    TileGeneration,

    /// <summary>
    /// Spatial extent calculation: Bounding box computation using ST_Extent.
    /// Moderately expensive, requires scanning all geometries.
    /// </summary>
    ExtentCalculation,

    /// <summary>
    /// Count operations: Record counting with filters.
    /// Performance depends on filter complexity and indexes.
    /// </summary>
    Count,

    /// <summary>
    /// Distinct value queries: Unique value retrieval for fields.
    /// Performance depends on cardinality and indexing.
    /// </summary>
    Distinct
}
