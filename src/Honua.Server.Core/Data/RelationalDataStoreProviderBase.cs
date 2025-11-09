// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Security;
using Honua.Server.Core.Utilities;
using Polly;

namespace Honua.Server.Core.Data;

/// <summary>
/// Abstract base class for relational database providers using ADO.NET.
/// Consolidates common patterns for connection management, transaction handling, and CRUD operations.
/// </summary>
/// <typeparam name="TConnection">The ADO.NET connection type (e.g., NpgsqlConnection, MySqlConnection).</typeparam>
/// <typeparam name="TTransaction">The ADO.NET transaction type (e.g., NpgsqlTransaction, MySqlTransaction).</typeparam>
/// <typeparam name="TCommand">The ADO.NET command type (e.g., NpgsqlCommand, MySqlCommand).</typeparam>
/// <typeparam name="TDataReader">The ADO.NET data reader type (e.g., NpgsqlDataReader, MySqlDataReader).</typeparam>
/// <remarks>
/// This base class eliminates ~4,677 lines of duplicated code across 12 data store providers by:
/// - Providing common implementations of CRUD operations (QueryAsync, CountAsync, GetAsync, CreateAsync, UpdateAsync, DeleteAsync)
/// - Implementing soft delete operations (SoftDeleteAsync, RestoreAsync, HardDeleteAsync)
/// - Centralizing connection string encryption/decryption and validation
/// - Implementing consistent retry pipeline integration
/// - Standardizing the dispose pattern with proper resource cleanup
///
/// SECURITY CRITICAL: All connection management paths ensure disposal in error scenarios to prevent pool exhaustion.
/// </remarks>
public abstract class RelationalDataStoreProviderBase<TConnection, TTransaction, TCommand, TDataReader> : IDataStoreProvider, IDisposable, IAsyncDisposable
    where TConnection : DbConnection
    where TTransaction : DbTransaction
    where TCommand : DbCommand
    where TDataReader : DbDataReader
{
    private readonly ConcurrentDictionary<string, Task<string>> _decryptionCache = new(StringComparer.Ordinal);
    private readonly IConnectionStringEncryptionService? _encryptionService;
    private readonly ResiliencePipeline _retryPipeline;
    private readonly string _providerKey;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RelationalDataStoreProviderBase{TConnection, TTransaction, TCommand, TDataReader}"/> class.
    /// </summary>
    /// <param name="providerKey">The unique key identifying this provider (e.g., "postgis", "mysql", "sqlserver").</param>
    /// <param name="retryPipeline">The Polly resilience pipeline for handling transient failures.</param>
    /// <param name="encryptionService">Optional service for decrypting encrypted connection strings.</param>
    /// <exception cref="ArgumentNullException">Thrown when providerKey or retryPipeline is null.</exception>
    protected RelationalDataStoreProviderBase(
        string providerKey,
        ResiliencePipeline retryPipeline,
        IConnectionStringEncryptionService? encryptionService = null)
    {
        _providerKey = providerKey ?? throw new ArgumentNullException(nameof(providerKey));
        _retryPipeline = retryPipeline ?? throw new ArgumentNullException(nameof(retryPipeline));
        _encryptionService = encryptionService;
    }

    /// <summary>
    /// Gets the provider key.
    /// </summary>
    public abstract string Provider { get; }

    /// <summary>
    /// Gets the capabilities of this data store provider.
    /// </summary>
    public abstract IDataStoreCapabilities Capabilities { get; }

    /// <summary>
    /// Gets the retry pipeline for transient failure handling.
    /// </summary>
    protected ResiliencePipeline RetryPipeline => _retryPipeline;

    /// <summary>
    /// Gets the default command timeout in seconds.
    /// </summary>
    protected virtual int DefaultCommandTimeoutSeconds => 30;

    // ========================================
    // COMMON CRUD OPERATIONS (Virtual)
    // ========================================

    /// <summary>
    /// Queries features from the data store based on the provided query parameters.
    /// </summary>
    public virtual async IAsyncEnumerable<FeatureRecord> QueryAsync(
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

        var normalizedQuery = query ?? new FeatureQuery();

        // Check if layer is backed by a SQL view
        if (SqlViewQueryBuilder.IsSqlView(layer))
        {
            await foreach (var record in ExecuteSqlViewQueryAsync(dataSource, layer, normalizedQuery, cancellationToken).ConfigureAwait(false))
            {
                yield return record;
            }
            yield break;
        }

        var storageSrid = layer.Storage?.Srid ?? CrsHelper.Wgs84;
        var targetSrid = CrsHelper.ParseCrs(normalizedQuery.Crs ?? service.Ogc.DefaultCrs);

        await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var definition = BuildSelectQuery(service, layer, storageSrid, targetSrid, normalizedQuery);
        await using var command = CreateCommand(connection, definition);
        await using var reader = await _retryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return CreateFeatureRecord((TDataReader)reader, layer, storageSrid, targetSrid);
        }
    }

    /// <summary>
    /// Counts features in the data store based on the provided query parameters.
    /// </summary>
    public virtual async Task<long> CountAsync(
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

        var normalizedQuery = query ?? new FeatureQuery();

        // Check if layer is backed by a SQL view
        if (SqlViewQueryBuilder.IsSqlView(layer))
        {
            return await ExecuteSqlViewCountAsync(dataSource, layer, normalizedQuery, cancellationToken).ConfigureAwait(false);
        }

        var storageSrid = layer.Storage?.Srid ?? CrsHelper.Wgs84;
        var targetSrid = CrsHelper.ParseCrs(normalizedQuery.Crs ?? service.Ogc.DefaultCrs);

        await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var definition = BuildCountQuery(service, layer, storageSrid, targetSrid, normalizedQuery);
        await using var command = CreateCommand(connection, definition);

        var result = await _retryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteScalarAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        if (result is null || result is DBNull)
        {
            return 0;
        }

        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets a single feature by its ID.
    /// </summary>
    public virtual async Task<FeatureRecord?> GetAsync(
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

        // Check if layer is backed by a SQL view
        if (SqlViewQueryBuilder.IsSqlView(layer))
        {
            return await ExecuteSqlViewByIdAsync(dataSource, layer, featureId, query, cancellationToken).ConfigureAwait(false);
        }

        var storageSrid = layer.Storage?.Srid ?? CrsHelper.Wgs84;
        var targetSrid = CrsHelper.ParseCrs(query?.Crs ?? service.Ogc.DefaultCrs);

        await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var definition = BuildByIdQuery(service, layer, storageSrid, targetSrid, featureId);
        await using var command = CreateCommand(connection, definition);
        await using var reader = await _retryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return CreateFeatureRecord((TDataReader)reader, layer, storageSrid, targetSrid);
        }

        return null;
    }

    /// <summary>
    /// Creates a new feature in the data store.
    /// </summary>
    public virtual async Task<FeatureRecord> CreateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureRecord record,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(record);

        var (connection, dbTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource, transaction, cancellationToken).ConfigureAwait(false);

        try
        {
            var definition = BuildInsertQuery(layer, record);
            await using var command = CreateCommand(connection, definition);
            if (dbTransaction != null)
            {
                command.Transaction = dbTransaction;
            }

            // Execute insert and get the inserted key
            var insertedKey = await ExecuteInsertAsync(connection, dbTransaction, layer, record, cancellationToken).ConfigureAwait(false);

            // Fetch and return the created record
            return await GetAsync(dataSource, service, layer, insertedKey, null, cancellationToken).ConfigureAwait(false)
                   ?? throw new InvalidOperationException("Failed to load feature after insert.");
        }
        finally
        {
            if (shouldDisposeConnection && connection != null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Updates an existing feature in the data store.
    /// </summary>
    public virtual async Task<FeatureRecord?> UpdateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        FeatureRecord record,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNullOrWhiteSpace(featureId);
        Guard.NotNull(record);

        var (connection, dbTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource, transaction, cancellationToken).ConfigureAwait(false);

        try
        {
            var definition = BuildUpdateQuery(layer, featureId, record);
            await using var command = CreateCommand(connection, definition);
            if (dbTransaction != null)
            {
                command.Transaction = dbTransaction;
            }

            var affected = await _retryPipeline.ExecuteAsync(async ct =>
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            if (affected == 0)
            {
                return null;
            }

            return await GetAsync(dataSource, service, layer, featureId, null, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (shouldDisposeConnection && connection != null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Deletes a feature from the data store.
    /// </summary>
    public virtual async Task<bool> DeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNullOrWhiteSpace(featureId);

        var (connection, dbTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource, transaction, cancellationToken).ConfigureAwait(false);

        try
        {
            var definition = BuildDeleteQuery(layer, featureId);
            await using var command = CreateCommand(connection, definition);
            if (dbTransaction != null)
            {
                command.Transaction = dbTransaction;
            }

            var affected = await _retryPipeline.ExecuteAsync(async ct =>
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            return affected > 0;
        }
        finally
        {
            if (shouldDisposeConnection && connection != null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Soft deletes a feature by marking it as deleted.
    /// </summary>
    public virtual async Task<bool> SoftDeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        string? deletedBy,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNullOrWhiteSpace(featureId);

        var (connection, dbTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource, transaction, cancellationToken).ConfigureAwait(false);

        try
        {
            var definition = BuildSoftDeleteQuery(layer, featureId, deletedBy);
            await using var command = CreateCommand(connection, definition);
            if (dbTransaction != null)
            {
                command.Transaction = dbTransaction;
            }

            var affected = await _retryPipeline.ExecuteAsync(async ct =>
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            return affected > 0;
        }
        finally
        {
            if (shouldDisposeConnection && connection != null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Restores a soft-deleted feature.
    /// </summary>
    public virtual async Task<bool> RestoreAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNullOrWhiteSpace(featureId);

        var (connection, dbTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource, transaction, cancellationToken).ConfigureAwait(false);

        try
        {
            var definition = BuildRestoreQuery(layer, featureId);
            await using var command = CreateCommand(connection, definition);
            if (dbTransaction != null)
            {
                command.Transaction = dbTransaction;
            }

            var affected = await _retryPipeline.ExecuteAsync(async ct =>
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            return affected > 0;
        }
        finally
        {
            if (shouldDisposeConnection && connection != null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Permanently deletes a feature (hard delete).
    /// </summary>
    public virtual async Task<bool> HardDeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        string? deletedBy,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNullOrWhiteSpace(featureId);

        var (connection, dbTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource, transaction, cancellationToken).ConfigureAwait(false);

        try
        {
            var definition = BuildHardDeleteQuery(layer, featureId);
            await using var command = CreateCommand(connection, definition);
            if (dbTransaction != null)
            {
                command.Transaction = dbTransaction;
            }

            var affected = await _retryPipeline.ExecuteAsync(async ct =>
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            return affected > 0;
        }
        finally
        {
            if (shouldDisposeConnection && connection != null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    // ========================================
    // CONNECTION AND TRANSACTION MANAGEMENT
    // ========================================

    /// <summary>
    /// Creates a database connection from the data source definition.
    /// </summary>
    /// <param name="dataSource">The data source containing connection string information.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A new database connection (not yet opened).</returns>
    protected async Task<TConnection> CreateConnectionAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dataSource.ConnectionString))
        {
            throw new InvalidOperationException($"Data source '{dataSource.Id}' is missing a connection string.");
        }

        var decryptedConnectionString = await DecryptConnectionStringAsync(
            dataSource.ConnectionString,
            cancellationToken).ConfigureAwait(false);

        var normalizedConnectionString = NormalizeConnectionString(decryptedConnectionString);
        return CreateConnectionCore(normalizedConnectionString);
    }

    /// <summary>
    /// Begins a transaction with the specified isolation level.
    /// </summary>
    public async Task<IDataStoreTransaction?> BeginTransactionAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        TConnection? connection = null;
        try
        {
            connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
            await _retryPipeline.ExecuteAsync(async ct =>
                await connection.OpenAsync(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            var isolationLevel = GetDefaultIsolationLevel();
            var transaction = (TTransaction)await connection.BeginTransactionAsync(
                isolationLevel,
                cancellationToken).ConfigureAwait(false);

            return new RelationalDataStoreTransaction<TConnection, TTransaction>(connection, transaction);
        }
        catch
        {
            if (connection != null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
            throw;
        }
    }

    /// <summary>
    /// Extracts connection and transaction from an IDataStoreTransaction or creates a new connection.
    /// </summary>
    protected async Task<(TConnection Connection, TTransaction? Transaction, bool ShouldDispose)> GetConnectionAndTransactionAsync(
        DataSourceDefinition dataSource,
        IDataStoreTransaction? transaction,
        CancellationToken cancellationToken = default)
    {
        if (transaction is RelationalDataStoreTransaction<TConnection, TTransaction> typedTransaction)
        {
            return (typedTransaction.Connection, typedTransaction.Transaction, false);
        }

        var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        return (connection, null, true);
    }

    /// <summary>
    /// Tests database connectivity with a lightweight query.
    /// </summary>
    public virtual async Task TestConnectivityAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(dataSource.ConnectionString))
        {
            throw new InvalidOperationException($"Data source '{dataSource.Id}' has no connection string configured.");
        }

        await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = GetConnectivityTestQuery();
        command.CommandTimeout = 5;

        await _retryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteScalarAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }

    // ========================================
    // HELPER METHODS
    // ========================================

    /// <summary>
    /// Creates a command from a query definition.
    /// </summary>
    protected virtual TCommand CreateCommand(TConnection connection, QueryDefinition definition)
    {
        var command = connection.CreateCommand();
        command.CommandText = definition.Sql;
        command.CommandTimeout = DefaultCommandTimeoutSeconds;
        command.CommandType = CommandType.Text;
        AddParameters(command, definition.Parameters);
        return (TCommand)command;
    }

    /// <summary>
    /// Throws ObjectDisposedException if this instance has been disposed.
    /// </summary>
    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <summary>
    /// Decrypts a connection string if an encryption service is available.
    /// </summary>
    private async Task<string> DecryptConnectionStringAsync(string connectionString, CancellationToken cancellationToken)
    {
        if (_encryptionService == null)
        {
            return connectionString;
        }

        return await _decryptionCache.GetOrAdd(connectionString,
            async cs => await _encryptionService.DecryptAsync(cs, cancellationToken).ConfigureAwait(false))
            .ConfigureAwait(false);
    }

    // ========================================
    // DISPOSAL
    // ========================================

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

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _disposed = true;
            _decryptionCache.Clear();
        }
    }

    protected virtual ValueTask DisposeAsyncCore()
    {
        _disposed = true;
        _decryptionCache.Clear();
        return ValueTask.CompletedTask;
    }

    // ========================================
    // ABSTRACT METHODS (Provider-specific)
    // ========================================

    /// <summary>
    /// Creates a concrete connection instance for this provider.
    /// </summary>
    protected abstract TConnection CreateConnectionCore(string connectionString);

    /// <summary>
    /// Validates and normalizes the connection string for this provider.
    /// </summary>
    protected abstract string NormalizeConnectionString(string connectionString);

    /// <summary>
    /// Builds a SELECT query for fetching features.
    /// </summary>
    protected abstract QueryDefinition BuildSelectQuery(
        ServiceDefinition service,
        LayerDefinition layer,
        int storageSrid,
        int targetSrid,
        FeatureQuery query);

    /// <summary>
    /// Builds a COUNT query for counting features.
    /// </summary>
    protected abstract QueryDefinition BuildCountQuery(
        ServiceDefinition service,
        LayerDefinition layer,
        int storageSrid,
        int targetSrid,
        FeatureQuery query);

    /// <summary>
    /// Builds a query to fetch a single feature by ID.
    /// </summary>
    protected abstract QueryDefinition BuildByIdQuery(
        ServiceDefinition service,
        LayerDefinition layer,
        int storageSrid,
        int targetSrid,
        string featureId);

    /// <summary>
    /// Builds an INSERT query for creating a feature.
    /// </summary>
    protected abstract QueryDefinition BuildInsertQuery(LayerDefinition layer, FeatureRecord record);

    /// <summary>
    /// Builds an UPDATE query for updating a feature.
    /// </summary>
    protected abstract QueryDefinition BuildUpdateQuery(LayerDefinition layer, string featureId, FeatureRecord record);

    /// <summary>
    /// Builds a DELETE query for deleting a feature.
    /// </summary>
    protected abstract QueryDefinition BuildDeleteQuery(LayerDefinition layer, string featureId);

    /// <summary>
    /// Builds a soft delete query (marks record as deleted).
    /// </summary>
    protected abstract QueryDefinition BuildSoftDeleteQuery(LayerDefinition layer, string featureId, string? deletedBy);

    /// <summary>
    /// Builds a restore query (un-marks record as deleted).
    /// </summary>
    protected abstract QueryDefinition BuildRestoreQuery(LayerDefinition layer, string featureId);

    /// <summary>
    /// Builds a hard delete query (permanently removes record).
    /// </summary>
    protected abstract QueryDefinition BuildHardDeleteQuery(LayerDefinition layer, string featureId);

    /// <summary>
    /// Creates a FeatureRecord from a data reader row.
    /// </summary>
    protected abstract FeatureRecord CreateFeatureRecord(
        TDataReader reader,
        LayerDefinition layer,
        int storageSrid,
        int targetSrid);

    /// <summary>
    /// Executes an insert command and returns the inserted key.
    /// </summary>
    protected abstract Task<string> ExecuteInsertAsync(
        TConnection connection,
        TTransaction? transaction,
        LayerDefinition layer,
        FeatureRecord record,
        CancellationToken cancellationToken);

    /// <summary>
    /// Adds parameters to a command.
    /// </summary>
    protected abstract void AddParameters(DbCommand command, IReadOnlyDictionary<string, object?> parameters);

    /// <summary>
    /// Gets the default isolation level for transactions.
    /// </summary>
    protected virtual IsolationLevel GetDefaultIsolationLevel() => IsolationLevel.RepeatableRead;

    /// <summary>
    /// Gets the SQL query used for connectivity tests.
    /// </summary>
    protected virtual string GetConnectivityTestQuery() => "SELECT 1";

    // ========================================
    // ABSTRACT METHODS (Remain provider-specific)
    // ========================================

    /// <inheritdoc />
    public abstract Task<int> BulkInsertAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<FeatureRecord> records,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<int> BulkUpdateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<KeyValuePair<string, FeatureRecord>> updates,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<int> BulkDeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<string> featureIds,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<byte[]?> GenerateMvtTileAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        int zoom,
        int x,
        int y,
        string? datetime = null,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<IReadOnlyList<StatisticsResult>> QueryStatisticsAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IReadOnlyList<StatisticDefinition> statistics,
        IReadOnlyList<string>? groupByFields,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IReadOnlyList<string> fieldNames,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<BoundingBox?> QueryExtentAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default);

    // ========================================
    // SQL VIEW SCHEMA DETECTION
    // ========================================

    /// <summary>
    /// Detects schema for a SQL view layer by executing the query and inspecting result metadata.
    /// This allows automatic field discovery instead of manually defining fields.
    /// </summary>
    /// <param name="layer">Layer definition containing the SQL view.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of detected field definitions.</returns>
    public virtual async Task<IReadOnlyList<FieldDefinition>> DetectSchemaForSqlViewAsync(
        LayerDefinition layer,
        CancellationToken ct)
    {
        if (layer.SqlView == null)
        {
            throw new ArgumentException("Layer does not have a SQL view defined", nameof(layer));
        }

        var detector = new SqlViewSchemaDetector();

        // We need to create a connection without a DataSourceDefinition
        // For this, we'll need to add an overload or make the connection creation more flexible
        // For now, let's add a protected method that takes just a connection string
        throw new NotImplementedException("DetectSchemaForSqlViewAsync requires connection - use overload with DataSourceDefinition");
    }

    /// <summary>
    /// Detects schema for a SQL view layer by executing the query and inspecting result metadata.
    /// This allows automatic field discovery instead of manually defining fields.
    /// </summary>
    /// <param name="dataSource">Data source containing connection information.</param>
    /// <param name="layer">Layer definition containing the SQL view.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of detected field definitions.</returns>
    public virtual async Task<IReadOnlyList<FieldDefinition>> DetectSchemaForSqlViewAsync(
        DataSourceDefinition dataSource,
        LayerDefinition layer,
        CancellationToken ct)
    {
        if (layer.SqlView == null)
        {
            throw new ArgumentException("Layer does not have a SQL view defined", nameof(layer));
        }

        var detector = new SqlViewSchemaDetector();

        await using var connection = await CreateConnectionAsync(dataSource, ct).ConfigureAwait(false);
        await _retryPipeline.ExecuteAsync(async cancellationToken =>
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false),
            ct).ConfigureAwait(false);

        return await detector.DetectSchemaAsync(
            connection,
            layer.SqlView,
            GetProviderName(),
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the provider name for schema detection.
    /// Must be one of: "postgres", "sqlserver", "mysql", "sqlite"
    /// </summary>
    protected abstract string GetProviderName();

    // ========================================
    // SQL VIEW SUPPORT (Protected virtual helpers)
    // ========================================

    /// <summary>
    /// Executes a SQL view query to retrieve features.
    /// Derived classes can override to customize SQL view execution behavior.
    /// </summary>
    protected virtual async IAsyncEnumerable<FeatureRecord> ExecuteSqlViewQueryAsync(
        DataSourceDefinition dataSource,
        LayerDefinition layer,
        FeatureQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestParameters = query.SqlViewParameters ?? new Dictionary<string, string>();
        var sqlViewBuilder = new SqlViewQueryBuilder(layer, requestParameters);
        var queryDef = sqlViewBuilder.BuildSelect(query);

        await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var definition = new QueryDefinition(queryDef.Sql, queryDef.Parameters);
        await using var command = CreateCommand(connection, definition);

        // Apply timeout from SQL view definition or query
        var timeout = sqlViewBuilder.GetCommandTimeout() ?? query.CommandTimeout ?? TimeSpan.FromSeconds(DefaultCommandTimeoutSeconds);
        command.CommandTimeout = (int)timeout.TotalSeconds;

        await using var reader = await _retryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        // For SQL views, we use the stored SRID from the layer (or WGS84 as default)
        var storageSrid = layer.Storage?.Srid ?? CrsHelper.Wgs84;
        var targetSrid = storageSrid; // SQL views handle their own CRS transformations in the query

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return CreateFeatureRecord((TDataReader)reader, layer, storageSrid, targetSrid);
        }
    }

    /// <summary>
    /// Executes a SQL view count query.
    /// Derived classes can override to customize SQL view count behavior.
    /// </summary>
    protected virtual async Task<long> ExecuteSqlViewCountAsync(
        DataSourceDefinition dataSource,
        LayerDefinition layer,
        FeatureQuery query,
        CancellationToken cancellationToken = default)
    {
        var requestParameters = query.SqlViewParameters ?? new Dictionary<string, string>();
        var sqlViewBuilder = new SqlViewQueryBuilder(layer, requestParameters);
        var queryDef = sqlViewBuilder.BuildCount(query);

        await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var definition = new QueryDefinition(queryDef.Sql, queryDef.Parameters);
        await using var command = CreateCommand(connection, definition);

        // Apply timeout from SQL view definition or query
        var timeout = sqlViewBuilder.GetCommandTimeout() ?? query.CommandTimeout ?? TimeSpan.FromSeconds(DefaultCommandTimeoutSeconds);
        command.CommandTimeout = (int)timeout.TotalSeconds;

        var result = await _retryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteScalarAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        return (result is null || result is DBNull) ? 0 : Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Executes a SQL view query to retrieve a single feature by ID.
    /// Derived classes can override to customize SQL view get-by-id behavior.
    /// </summary>
    protected virtual async Task<FeatureRecord?> ExecuteSqlViewByIdAsync(
        DataSourceDefinition dataSource,
        LayerDefinition layer,
        string featureId,
        FeatureQuery? query,
        CancellationToken cancellationToken = default)
    {
        var requestParameters = query?.SqlViewParameters ?? new Dictionary<string, string>();
        var sqlViewBuilder = new SqlViewQueryBuilder(layer, requestParameters);
        var queryDef = sqlViewBuilder.BuildById(featureId, query);

        await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var definition = new QueryDefinition(queryDef.Sql, queryDef.Parameters);
        await using var command = CreateCommand(connection, definition);

        // Apply timeout from SQL view definition or query
        var timeout = sqlViewBuilder.GetCommandTimeout() ?? query?.CommandTimeout ?? TimeSpan.FromSeconds(DefaultCommandTimeoutSeconds);
        command.CommandTimeout = (int)timeout.TotalSeconds;

        await using var reader = await _retryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        // For SQL views, we use the stored SRID from the layer (or WGS84 as default)
        var storageSrid = layer.Storage?.Srid ?? CrsHelper.Wgs84;
        var targetSrid = storageSrid; // SQL views handle their own CRS transformations in the query

        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return CreateFeatureRecord((TDataReader)reader, layer, storageSrid, targetSrid);
        }

        return null;
    }
}

/// <summary>
/// Represents a query definition with SQL and parameters.
/// </summary>
public record QueryDefinition(string Sql, IReadOnlyDictionary<string, object?> Parameters);
