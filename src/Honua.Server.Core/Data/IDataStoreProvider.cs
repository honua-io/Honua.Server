// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Filter;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data;

/// <summary>
/// Represents a database transaction for data store operations.
/// </summary>
public interface IDataStoreTransaction : IAsyncDisposable
{
    /// <summary>
    /// Commits the transaction.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the transaction.
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the underlying transaction object for provider-specific operations.
    /// This is typically a DbTransaction instance that can be used to enlist commands.
    /// </summary>
    object GetUnderlyingTransaction();
}

public interface IDataStoreProvider
{
    string Provider { get; }

    IDataStoreCapabilities Capabilities { get; }

    IAsyncEnumerable<FeatureRecord> QueryAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? query,
        CancellationToken cancellationToken = default);

    Task<long> CountAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? query,
        CancellationToken cancellationToken = default);

    Task<FeatureRecord?> GetAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        FeatureQuery? query,
        CancellationToken cancellationToken = default);

    Task<FeatureRecord> CreateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureRecord record,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<FeatureRecord?> UpdateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        FeatureRecord record,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a feature by marking it as deleted instead of physically removing it.
    /// Returns true if the feature was soft-deleted, false if it was not found or already deleted.
    /// </summary>
    Task<bool> SoftDeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        string? deletedBy,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a soft-deleted feature by clearing its deletion markers.
    /// Returns true if the feature was restored, false if it was not found or not deleted.
    /// </summary>
    Task<bool> RestoreAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes a feature (hard delete).
    /// This should only be used by administrators and cannot be undone.
    /// Returns true if the feature was permanently deleted.
    /// </summary>
    Task<bool> HardDeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        string? deletedBy,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts multiple features in a single optimized operation.
    /// </summary>
    /// <param name="dataSource">The data source definition.</param>
    /// <param name="service">The service definition.</param>
    /// <param name="layer">The layer definition.</param>
    /// <param name="records">Async enumerable of feature records to insert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of features inserted.</returns>
    Task<int> BulkInsertAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<FeatureRecord> records,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates multiple features in a single optimized operation.
    /// </summary>
    /// <param name="dataSource">The data source definition.</param>
    /// <param name="service">The service definition.</param>
    /// <param name="layer">The layer definition.</param>
    /// <param name="updates">Async enumerable of feature ID and record pairs to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of features updated.</returns>
    Task<int> BulkUpdateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<KeyValuePair<string, FeatureRecord>> updates,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes multiple features in a single optimized operation.
    /// </summary>
    /// <param name="dataSource">The data source definition.</param>
    /// <param name="service">The service definition.</param>
    /// <param name="layer">The layer definition.</param>
    /// <param name="featureIds">Async enumerable of feature IDs to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of features deleted.</returns>
    Task<int> BulkDeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<string> featureIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a Mapbox Vector Tile (MVT) for the specified tile coordinates.
    /// </summary>
    /// <param name="dataSource">The data source definition.</param>
    /// <param name="service">The service definition.</param>
    /// <param name="layer">The layer definition.</param>
    /// <param name="zoom">The tile zoom level.</param>
    /// <param name="x">The tile X coordinate.</param>
    /// <param name="y">The tile Y coordinate.</param>
    /// <param name="datetime">Optional datetime filter for temporal queries (ISO 8601 format).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The MVT tile bytes if the provider supports native MVT generation, or null if not supported.
    /// </returns>
    Task<byte[]?> GenerateMvtTileAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        int zoom,
        int x,
        int y,
        string? datetime = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs statistical aggregations (SUM, AVG, MIN, MAX, COUNT) at the database level.
    /// This is a critical performance optimization that replaces loading all records into memory.
    /// </summary>
    /// <param name="dataSource">The data source definition.</param>
    /// <param name="service">The service definition.</param>
    /// <param name="layer">The layer definition.</param>
    /// <param name="statistics">List of statistics to calculate.</param>
    /// <param name="groupByFields">Optional fields to group by.</param>
    /// <param name="filter">Optional filter query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Statistics results grouped by the specified fields.</returns>
    Task<IReadOnlyList<StatisticsResult>> QueryStatisticsAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IReadOnlyList<StatisticDefinition> statistics,
        IReadOnlyList<string>? groupByFields,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves distinct values for a field at the database level using SQL DISTINCT.
    /// This is a critical performance optimization that replaces loading all records into memory.
    /// </summary>
    /// <param name="dataSource">The data source definition.</param>
    /// <param name="service">The service definition.</param>
    /// <param name="layer">The layer definition.</param>
    /// <param name="fieldNames">Field names to get distinct values for.</param>
    /// <param name="filter">Optional filter query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of distinct value combinations.</returns>
    Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IReadOnlyList<string> fieldNames,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates the spatial extent (bounding box) at the database level using PostGIS ST_Extent.
    /// This is a critical performance optimization that replaces loading all geometries into memory.
    /// </summary>
    /// <param name="dataSource">The data source definition.</param>
    /// <param name="service">The service definition.</param>
    /// <param name="layer">The layer definition.</param>
    /// <param name="filter">Optional filter query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The bounding box of all geometries, or null if no geometries found.</returns>
    Task<BoundingBox?> QueryExtentAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a transaction for the specified data source.
    /// Returns null if the provider does not support transactions.
    /// </summary>
    /// <param name="dataSource">The data source definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A transaction object if supported, otherwise null.</returns>
    Task<IDataStoreTransaction?> BeginTransactionAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests database connectivity with a lightweight query.
    /// Used by health checks to verify the data source is reachable.
    /// </summary>
    /// <param name="dataSource">The data source definition containing connection information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes successfully if connectivity test passes, throws exception on failure.</returns>
    Task TestConnectivityAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a feature record with attributes and optional version tracking for optimistic concurrency control.
/// </summary>
/// <param name="Attributes">The feature attributes as key-value pairs.</param>
/// <param name="Version">Optional version identifier for optimistic locking. Can be a timestamp, integer, or GUID depending on the provider.</param>
public sealed record FeatureRecord(
    IReadOnlyDictionary<string, object?> Attributes,
    object? Version = null);

public sealed record FeatureSortOrder(string Field, FeatureSortDirection Direction);

public sealed record FeatureQuery(
    int? Limit = null,
    int? Offset = null,  // DEPRECATED: Use Cursor instead for better performance
    string? Cursor = null,  // Keyset pagination cursor (replaces Offset for O(1) performance)
    BoundingBox? Bbox = null,
    TemporalInterval? Temporal = null,
    FeatureResultType ResultType = FeatureResultType.Results,
    IReadOnlyList<string>? PropertyNames = null,
    IReadOnlyList<FeatureSortOrder>? SortOrders = null,
    QueryFilter? Filter = null,
    QueryEntityDefinition? EntityDefinition = null,
    string? Crs = null,
    TimeSpan? CommandTimeout = null,  // Optional per-query timeout override (e.g., for slow analytical queries)
    string? HavingClause = null);  // SQL HAVING clause for filtering aggregated statistics results

public sealed record BoundingBox(
    double MinX,
    double MinY,
    double MaxX,
    double MaxY,
    double? MinZ = null,
    double? MaxZ = null,
    string? Crs = null);

public sealed record TemporalInterval(DateTimeOffset? Start, DateTimeOffset? End);

public enum FeatureResultType
{
    Results,
    Hits
}

public enum FeatureSortDirection
{
    Ascending,
    Descending
}

/// <summary>
/// Defines a statistic to calculate (e.g., SUM, AVG, MIN, MAX, COUNT).
/// </summary>
public sealed record StatisticDefinition(
    string FieldName,
    StatisticType Type,
    string? OutputName = null);

/// <summary>
/// Types of statistics that can be calculated.
/// </summary>
public enum StatisticType
{
    Count,
    Sum,
    Avg,
    Min,
    Max
}

/// <summary>
/// Result of a statistics query for a single group.
/// </summary>
public sealed record StatisticsResult(
    IReadOnlyDictionary<string, object?> GroupValues,
    IReadOnlyDictionary<string, object?> Statistics);

/// <summary>
/// Result of a distinct values query.
/// </summary>
public sealed record DistinctResult(
    IReadOnlyDictionary<string, object?> Values);
