// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Security;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data.Postgres;

/// <summary>
/// PostgreSQL/PostGIS data store provider implementation.
/// Orchestrates specialized services for connection management, feature operations, and vector tile generation.
/// </summary>
public sealed class PostgresDataStoreProvider : IDataStoreProvider, IDisposable, IAsyncDisposable
{
    private readonly PostgresConnectionManager _connectionManager;
    private readonly PostgresFeatureOperations _featureOperations;
    private readonly PostgresBulkOperations _bulkOperations;
    private readonly PostgresVectorTileGenerator _vectorTileGenerator;
    private readonly PostgresConnectionPoolMetrics _metrics;
    private readonly QueryBuilderPool _queryBuilderPool;
    private readonly DataAccessOptions _options;
    private bool _disposed;

    public const string ProviderKey = "postgis";

    public PostgresDataStoreProvider(
        IOptions<DataAccessOptions>? options = null,
        QueryBuilderPool? queryBuilderPool = null,
        IConnectionStringEncryptionService? encryptionService = null,
        ILoggerFactory? loggerFactory = null,
        IMemoryCache? memoryCache = null)
    {
        _options = options?.Value ?? new DataAccessOptions();

        // Create metrics and query builder pool
        var dataSourcesDict = new Dictionary<string, Npgsql.NpgsqlDataSource>();
        _metrics = new PostgresConnectionPoolMetrics(dataSourcesDict);
        _queryBuilderPool = queryBuilderPool ?? new QueryBuilderPool();

        // Create specialized services with logging
        var bulkOpsLogger = loggerFactory?.CreateLogger<PostgresBulkOperations>()
            ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PostgresBulkOperations>.Instance;
        var featureOpsLogger = loggerFactory?.CreateLogger<PostgresFeatureOperations>();

        // Use provided memory cache or create a new one for connection string decryption caching
        var cache = memoryCache ?? new MemoryCache(new MemoryCacheOptions());

        _connectionManager = new PostgresConnectionManager(_metrics, cache, encryptionService);
        _featureOperations = new PostgresFeatureOperations(_connectionManager, _queryBuilderPool, _options, featureOpsLogger);
        _bulkOperations = new PostgresBulkOperations(_connectionManager, bulkOpsLogger);
        _vectorTileGenerator = new PostgresVectorTileGenerator(_connectionManager);
    }

    public string Provider => ProviderKey;

    public IDataStoreCapabilities Capabilities => PostgresDataStoreCapabilities.Instance;

    /// <summary>
    /// Warms the query builder pool for a specific service and layer.
    /// This pre-allocates builders to reduce latency during query execution.
    /// </summary>
    public void WarmQueryBuilderCache(
        ServiceDefinition service,
        LayerDefinition layer,
        int storageSrid,
        int targetSrid,
        int count = 5)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _queryBuilderPool.WarmCache(service, layer, storageSrid, targetSrid, count);
    }

    /// <summary>
    /// Gets statistics about the query builder pool.
    /// </summary>
    public PoolStatistics GetQueryBuilderPoolStatistics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _queryBuilderPool.GetStatistics();
    }

    public IAsyncEnumerable<FeatureRecord> QueryAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? query,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _featureOperations.QueryAsync(dataSource, service, layer, query, cancellationToken);
    }

    public Task<long> CountAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? query,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _featureOperations.CountAsync(dataSource, service, layer, query, cancellationToken);
    }

    public Task<FeatureRecord?> GetAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        FeatureQuery? query,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _featureOperations.GetAsync(dataSource, service, layer, featureId, query, cancellationToken);
    }

    public Task<FeatureRecord> CreateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureRecord record,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _featureOperations.CreateAsync(dataSource, service, layer, record, transaction, cancellationToken);
    }

    public Task<FeatureRecord?> UpdateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        FeatureRecord record,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _featureOperations.UpdateAsync(dataSource, service, layer, featureId, record, transaction, cancellationToken);
    }

    public Task<bool> DeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _featureOperations.DeleteAsync(dataSource, service, layer, featureId, transaction, cancellationToken);
    }

    public Task<bool> SoftDeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        string? deletedBy,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _featureOperations.SoftDeleteAsync(dataSource, service, layer, featureId, deletedBy, transaction, cancellationToken);
    }

    public Task<bool> RestoreAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _featureOperations.RestoreAsync(dataSource, service, layer, featureId, transaction, cancellationToken);
    }

    public Task<bool> HardDeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        string? deletedBy,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _featureOperations.HardDeleteAsync(dataSource, service, layer, featureId, deletedBy, transaction, cancellationToken);
    }

    public Task<int> BulkInsertAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<FeatureRecord> records,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _bulkOperations.BulkInsertAsync(dataSource, service, layer, records, cancellationToken);
    }

    public Task<int> BulkUpdateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<KeyValuePair<string, FeatureRecord>> updates,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _bulkOperations.BulkUpdateAsync(dataSource, service, layer, updates, cancellationToken);
    }

    public Task<int> BulkDeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<string> featureIds,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _bulkOperations.BulkDeleteAsync(dataSource, service, layer, featureIds, cancellationToken);
    }

    public Task<byte[]?> GenerateMvtTileAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        int zoom,
        int x,
        int y,
        string? datetime = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _vectorTileGenerator.GenerateMvtTileAsync(dataSource, service, layer, zoom, x, y, datetime, cancellationToken);
    }

    public Task<IReadOnlyList<StatisticsResult>> QueryStatisticsAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IReadOnlyList<StatisticDefinition> statistics,
        IReadOnlyList<string>? groupByFields,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _featureOperations.QueryStatisticsAsync(dataSource, service, layer, statistics, groupByFields, filter, cancellationToken);
    }

    public Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IReadOnlyList<string> fieldNames,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _featureOperations.QueryDistinctAsync(dataSource, service, layer, fieldNames, filter, cancellationToken);
    }

    public Task<BoundingBox?> QueryExtentAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _featureOperations.QueryExtentAsync(dataSource, service, layer, filter, cancellationToken);
    }

    public async Task<IDataStoreTransaction?> BeginTransactionAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        NpgsqlConnection? connection = null;
        try
        {
            connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            // Use REPEATABLE READ isolation level for government data integrity
            // This prevents non-repeatable reads and phantom reads during transaction execution
            // READ COMMITTED (default) is insufficient for critical data operations where
            // multiple queries within a transaction must see a consistent snapshot
            // See: https://www.postgresql.org/docs/current/transaction-iso.html
            var transaction = await connection.BeginTransactionAsync(
                System.Data.IsolationLevel.RepeatableRead,
                cancellationToken).ConfigureAwait(false);

            return new RelationalDataStoreTransaction<NpgsqlConnection, NpgsqlTransaction>(connection, transaction);
        }
        catch
        {
            // CRITICAL: Dispose connection on any failure to prevent connection pool exhaustion
            // Without this, each failed transaction permanently leaks a connection
            if (connection != null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
            throw;
        }
    }

    public async Task TestConnectivityAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _featureOperations.TestConnectivityAsync(dataSource, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    private async ValueTask DisposeAsyncCore()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Dispose services asynchronously
        await _connectionManager.DisposeAsync().ConfigureAwait(false);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed && disposing)
        {
            return;
        }

        if (disposing)
        {
            _disposed = true;
            _connectionManager?.Dispose();
            _metrics?.Dispose();
            _queryBuilderPool?.Dispose();
        }
    }
}

