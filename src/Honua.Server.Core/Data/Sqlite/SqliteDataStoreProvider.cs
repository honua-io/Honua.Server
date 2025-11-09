// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Security;
using Honua.Server.Core.Utilities;
using Microsoft.Data.Sqlite;
using Polly;

using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Data.Sqlite;

public sealed class SqliteDataStoreProvider : DisposableBase, IDataStoreProvider
{
    private const int DefaultCommandTimeoutSeconds = 30;
    private const int BulkBatchSize = 500;

    private static readonly GeoJsonReader GeoJsonReader = new();
    private static readonly WKTReader WktReader = new();
    private static readonly WKTWriter WktWriter = new();

    private readonly ConcurrentDictionary<string, string> _normalizedConnectionStrings = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Task<string>> _decryptionCache = new(StringComparer.Ordinal);
    private readonly ResiliencePipeline _retryPipeline;
    private readonly IConnectionStringEncryptionService? _encryptionService;

    public const string ProviderKey = "sqlite";

    public SqliteDataStoreProvider(IConnectionStringEncryptionService? encryptionService = null)
    {
        _retryPipeline = DatabaseRetryPolicy.CreateSqliteRetryPipeline();
        _encryptionService = encryptionService;
    }

    public string Provider => ProviderKey;

    public IDataStoreCapabilities Capabilities => SqliteDataStoreCapabilities.Instance;

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

        var normalizedQuery = query ?? new FeatureQuery();
        var storageSrid = layer.Storage?.Srid ?? CrsHelper.Wgs84;
        var targetSrid = CrsHelper.ParseCrs(normalizedQuery.Crs ?? service.Ogc.DefaultCrs);

        await using var connection = CreateConnection(dataSource);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var builder = new SqliteFeatureQueryBuilder(service, layer);
        var definition = builder.BuildSelect(normalizedQuery);

        await using var command = CreateCommand(connection, definition);
        await using var reader = await _retryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return CreateFeatureRecord(reader, layer, storageSrid, targetSrid);
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

        var normalizedQuery = query ?? new FeatureQuery();
        await using var connection = CreateConnection(dataSource);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var builder = new SqliteFeatureQueryBuilder(service, layer);
        var definition = builder.BuildCount(normalizedQuery);

        await using var command = CreateCommand(connection, definition);
        var result = await _retryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteScalarAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
        if (result is null || result is DBNull)
        {
            return 0;
        }

        return Convert.ToInt64(result);
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

        var storageSrid = layer.Storage?.Srid ?? CrsHelper.Wgs84;
        var targetSrid = CrsHelper.ParseCrs(query?.Crs ?? service.Ogc.DefaultCrs);

        await using var connection = CreateConnection(dataSource);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var builder = new SqliteFeatureQueryBuilder(service, layer);
        var definition = builder.BuildById(featureId);

        await using var command = CreateCommand(connection, definition);
        await using var reader = await _retryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return CreateFeatureRecord(reader, layer, storageSrid, targetSrid);
        }

        return null;
    }

    public async Task<FeatureRecord> CreateAsync(
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

        var table = LayerMetadataHelper.GetTableExpression(layer, SqliteFeatureQueryBuilder.QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        // Use base class helper to extract connection and transaction - eliminates 15 lines of boilerplate
        var (connection, sqliteTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource,
            transaction,
            cancellationToken).ConfigureAwait(false);

        try
        {
            var normalized = NormalizeRecord(layer, record.Attributes, includeKey: true);
            if (normalized.Columns.Count == 0)
            {
                throw new InvalidOperationException($"Create operation for layer '{layer.Id}' did not include any columns.");
            }

            var columnList = string.Join(", ", normalized.Columns);
            var parameterList = string.Join(", ", normalized.Parameters.Select(parameter => parameter.Key));

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = sqliteTransaction;
                command.CommandText = $"insert into {table} ({columnList}) values ({parameterList});";
                AddParameters(command, normalized.Parameters);
                await _retryPipeline.ExecuteAsync(async ct =>
                    await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
            }

            var keyValue = TryResolveKey(record.Attributes, keyColumn);
            if (keyValue is null)
            {
                await using var lastIdCommand = connection.CreateCommand();
                lastIdCommand.Transaction = sqliteTransaction;
                lastIdCommand.CommandText = "select last_insert_rowid();";
                var scalar = await _retryPipeline.ExecuteAsync(async ct =>
                    await lastIdCommand.ExecuteScalarAsync(ct).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
                keyValue = Convert.ToString(scalar, CultureInfo.InvariantCulture) ?? string.Empty;
            }
            var created = await GetAsync(dataSource, service, layer, keyValue, null, cancellationToken).ConfigureAwait(false);
            if (created is null)
            {
                throw new InvalidOperationException("Failed to load feature after insert.");
            }

            return created;
        }
        finally
        {
            if (shouldDisposeConnection && connection != null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task<FeatureRecord?> UpdateAsync(
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

        var table = LayerMetadataHelper.GetTableExpression(layer, SqliteFeatureQueryBuilder.QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        // Use helper to extract connection and transaction - eliminates 15 lines of boilerplate
        var (connection, sqliteTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource,
            transaction,
            cancellationToken).ConfigureAwait(false);

        try
        {
            var normalized = NormalizeRecord(layer, record.Attributes, includeKey: false);
            if (normalized.Columns.Count == 0)
            {
                return await GetAsync(dataSource, service, layer, featureId, null, cancellationToken).ConfigureAwait(false);
            }

            var assignmentBuilder = new StringBuilder();
            for (var i = 0; i < normalized.Columns.Count; i++)
            {
                if (assignmentBuilder.Length > 0)
                {
                    assignmentBuilder.Append(", ");
                }

                assignmentBuilder.Append(normalized.Columns[i]);
                assignmentBuilder.Append(" = ");
                assignmentBuilder.Append(normalized.Parameters[i].Key);
            }

            var assignments = assignmentBuilder.ToString();
            var parameters = new List<KeyValuePair<string, object?>>(normalized.Parameters)
            {
                new("@key", featureId)
            };

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = sqliteTransaction;
                command.CommandText = $"update {table} set {assignments} where {SqliteFeatureQueryBuilder.QuoteIdentifier(keyColumn)} = @key";
                AddParameters(command, parameters);
                var affected = await _retryPipeline.ExecuteAsync(async ct =>
                    await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
                if (affected == 0)
                {
                    return null;
                }
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

    public async Task<bool> DeleteAsync(
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

        var table = LayerMetadataHelper.GetTableExpression(layer, SqliteFeatureQueryBuilder.QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        // Use helper to extract connection and transaction - eliminates 15 lines of boilerplate
        var (connection, sqliteTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource,
            transaction,
            cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = sqliteTransaction;
            command.CommandText = $"delete from {table} where {SqliteFeatureQueryBuilder.QuoteIdentifier(keyColumn)} = @key";
            command.Parameters.AddWithValue("@key", featureId);
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

    public async Task<bool> SoftDeleteAsync(
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

        var table = LayerMetadataHelper.GetTableExpression(layer, SqliteFeatureQueryBuilder.QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        // Use helper to extract connection and transaction - eliminates 15 lines of boilerplate
        var (connection, sqliteTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource,
            transaction,
            cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = sqliteTransaction;
            command.CommandText = $@"
                UPDATE {table}
                SET is_deleted = 1,
                    deleted_at = datetime('now'),
                    deleted_by = @deletedBy
                WHERE {SqliteFeatureQueryBuilder.QuoteIdentifier(keyColumn)} = @key
                  AND (is_deleted IS NULL OR is_deleted = 0)";
            command.Parameters.AddWithValue("@key", featureId);
            command.Parameters.AddWithValue("@deletedBy", (object?)deletedBy ?? DBNull.Value);
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

    public async Task<bool> RestoreAsync(
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

        var table = LayerMetadataHelper.GetTableExpression(layer, SqliteFeatureQueryBuilder.QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        // Use helper to extract connection and transaction - eliminates 15 lines of boilerplate
        var (connection, sqliteTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource,
            transaction,
            cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = sqliteTransaction;
            command.CommandText = $@"
                UPDATE {table}
                SET is_deleted = 0,
                    deleted_at = NULL,
                    deleted_by = NULL
                WHERE {SqliteFeatureQueryBuilder.QuoteIdentifier(keyColumn)} = @key
                  AND is_deleted = 1";
            command.Parameters.AddWithValue("@key", featureId);
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

    public async Task<bool> HardDeleteAsync(
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

        var table = LayerMetadataHelper.GetTableExpression(layer, SqliteFeatureQueryBuilder.QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        // Use helper to extract connection and transaction - eliminates 15 lines of boilerplate
        var (connection, sqliteTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource,
            transaction,
            cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = sqliteTransaction;
            command.CommandText = $"DELETE FROM {table} WHERE {SqliteFeatureQueryBuilder.QuoteIdentifier(keyColumn)} = @key";
            command.Parameters.AddWithValue("@key", featureId);
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

    public async Task<int> BulkInsertAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<FeatureRecord> records,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(records);

        await using var connection = CreateConnection(dataSource);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var count = 0;
        var batch = new List<FeatureRecord>();

        await foreach (var record in records.WithCancellation(cancellationToken))
        {
            batch.Add(record);

            if (batch.Count >= BulkBatchSize)
            {
                count += await ExecuteInsertBatchAsync(connection, layer, batch, cancellationToken).ConfigureAwait(false);
                batch.Clear();
            }
        }

        // Process remaining items
        if (batch.Count > 0)
        {
            count += await ExecuteInsertBatchAsync(connection, layer, batch, cancellationToken).ConfigureAwait(false);
        }

        return count;
    }

    public async Task<int> BulkUpdateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<KeyValuePair<string, FeatureRecord>> updates,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(updates);

        await using var connection = CreateConnection(dataSource);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var count = 0;
        var batch = new List<KeyValuePair<string, FeatureRecord>>();

        await foreach (var update in updates.WithCancellation(cancellationToken))
        {
            batch.Add(update);

            if (batch.Count >= BulkBatchSize)
            {
                count += await ExecuteUpdateBatchAsync(connection, layer, batch, cancellationToken).ConfigureAwait(false);
                batch.Clear();
            }
        }

        // Process remaining items
        if (batch.Count > 0)
        {
            count += await ExecuteUpdateBatchAsync(connection, layer, batch, cancellationToken).ConfigureAwait(false);
        }

        return count;
    }

    public async Task<int> BulkDeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<string> featureIds,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(featureIds);

        await using var connection = CreateConnection(dataSource);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var count = 0;
        var batch = new List<string>();

        await foreach (var id in featureIds.WithCancellation(cancellationToken))
        {
            batch.Add(id);

            if (batch.Count >= BulkBatchSize)
            {
                count += await ExecuteDeleteBatchAsync(connection, layer, batch, cancellationToken).ConfigureAwait(false);
                batch.Clear();
            }
        }

        // Process remaining items
        if (batch.Count > 0)
        {
            count += await ExecuteDeleteBatchAsync(connection, layer, batch, cancellationToken).ConfigureAwait(false);
        }

        return count;
    }

    private async Task<int> ExecuteInsertBatchAsync(
        SqliteConnection connection,
        LayerDefinition layer,
        List<FeatureRecord> batch,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return 0;
        }

        var table = LayerMetadataHelper.GetTableExpression(layer, SqliteFeatureQueryBuilder.QuoteIdentifier);

        // Use a transaction for batch efficiency
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            foreach (var record in batch)
            {
                var normalized = NormalizeRecord(layer, record.Attributes, includeKey: true);
                if (normalized.Columns.Count == 0)
                {
                    continue;
                }

                var columnList = string.Join(", ", normalized.Columns);
                var parameterList = string.Join(", ", normalized.Parameters.Select(p => p.Key));

                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = $"INSERT INTO {table} ({columnList}) VALUES ({parameterList})";
                AddParameters(command, normalized.Parameters);

                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return batch.Count;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<int> ExecuteUpdateBatchAsync(
        SqliteConnection connection,
        LayerDefinition layer,
        List<KeyValuePair<string, FeatureRecord>> batch,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return 0;
        }

        var table = LayerMetadataHelper.GetTableExpression(layer, SqliteFeatureQueryBuilder.QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        // Use a transaction for batch efficiency
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var affected = 0;

            foreach (var update in batch)
            {
                var normalized = NormalizeRecord(layer, update.Value.Attributes, includeKey: false);
                if (normalized.Columns.Count == 0)
                {
                    continue;
                }

                var assignmentBuilder = new StringBuilder();
                for (var i = 0; i < normalized.Columns.Count; i++)
                {
                    if (assignmentBuilder.Length > 0)
                    {
                        assignmentBuilder.Append(", ");
                    }

                    assignmentBuilder.Append(normalized.Columns[i]);
                    assignmentBuilder.Append(" = ");
                    assignmentBuilder.Append(normalized.Parameters[i].Key);
                }

                var assignments = assignmentBuilder.ToString();
                var parameters = new List<KeyValuePair<string, object?>>(normalized.Parameters)
                {
                    new("@key", update.Key)
                };

                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = $"UPDATE {table} SET {assignments} WHERE {SqliteFeatureQueryBuilder.QuoteIdentifier(keyColumn)} = @key";
                AddParameters(command, parameters);

                var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                affected += rowsAffected;
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return affected;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<int> ExecuteDeleteBatchAsync(
        SqliteConnection connection,
        LayerDefinition layer,
        List<string> batch,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return 0;
        }

        var table = LayerMetadataHelper.GetTableExpression(layer, SqliteFeatureQueryBuilder.QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        // Build IN clause with parameters
        var parameters = new List<KeyValuePair<string, object?>>();
        var parameterNames = new List<string>();

        for (var i = 0; i < batch.Count; i++)
        {
            var paramName = $"@id{i}";
            parameterNames.Add(paramName);
            parameters.Add(new KeyValuePair<string, object?>(paramName, batch[i]));
        }

        var inClause = string.Join(", ", parameterNames);

        await using var command = connection.CreateCommand();
        command.CommandText = $"DELETE FROM {table} WHERE {SqliteFeatureQueryBuilder.QuoteIdentifier(keyColumn)} IN ({inClause})";
        AddParameters(command, parameters);

        var affected = await _retryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        return affected;
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
        // SQLite does not support native MVT generation
        return Task.FromResult<byte[]?>(null);
    }

    public async Task<IDataStoreTransaction?> BeginTransactionAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        SqliteConnection? connection = null;
        try
        {
            connection = CreateConnection(dataSource);
            await _retryPipeline.ExecuteAsync(async ct =>
                await connection.OpenAsync(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            // Use Serializable isolation level for data integrity
            // SQLite supports DEFERRED, IMMEDIATE, and EXCLUSIVE transactions
            // Using IMMEDIATE to acquire write lock immediately and prevent deadlocks
            var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable,
                cancellationToken).ConfigureAwait(false);

            return new RelationalDataStoreTransaction<SqliteConnection, SqliteTransaction>(connection, transaction);
        }
        catch
        {
            // CRITICAL: Dispose connection on any failure to prevent connection leaks
            if (connection != null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
            throw;
        }
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
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(statistics);

        if (statistics.Count == 0)
        {
            return Array.Empty<StatisticsResult>();
        }

        var normalizedQuery = filter ?? new FeatureQuery();

        await using var connection = CreateConnection(dataSource);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var builder = new SqliteFeatureQueryBuilder(service, layer);
        var definition = builder.BuildStatistics(normalizedQuery, statistics, groupByFields);

        await using var command = CreateCommand(connection, definition);
        await using var reader = await _retryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var results = new List<StatisticsResult>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var ordinal = 0;
            var groupValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            if (groupByFields is { Count: > 0 })
            {
                foreach (var field in groupByFields)
                {
                    groupValues[field] = reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);
                    ordinal++;
                }
            }

            var statisticValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var statistic in statistics)
            {
                var key = statistic.OutputName ?? $"{statistic.Type}_{statistic.FieldName}";
                statisticValues[key] = reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);
                ordinal++;
            }

            results.Add(new StatisticsResult(groupValues, statisticValues));
        }

        return results;
    }

    public async Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IReadOnlyList<string> fieldNames,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(fieldNames);

        if (fieldNames.Count == 0)
        {
            return Array.Empty<DistinctResult>();
        }

        var normalizedQuery = filter ?? new FeatureQuery();

        await using var connection = CreateConnection(dataSource);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var builder = new SqliteFeatureQueryBuilder(service, layer);
        var definition = builder.BuildDistinct(normalizedQuery, fieldNames);

        await using var command = CreateCommand(connection, definition);
        await using var reader = await _retryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var results = new List<DistinctResult>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < fieldNames.Count; index++)
            {
                values[fieldNames[index]] = reader.IsDBNull(index) ? null : reader.GetValue(index);
            }

            results.Add(new DistinctResult(values));
        }

        return results;
    }

    public async Task<BoundingBox?> QueryExtentAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(layer);

        var normalizedQuery = filter ?? new FeatureQuery();

        await using var connection = CreateConnection(dataSource);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var builder = new SqliteFeatureQueryBuilder(service, layer);
        var definition = builder.BuildExtent(normalizedQuery);

        await using var command = CreateCommand(connection, definition);
        await using var reader = await _retryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        if (reader.IsDBNull(0) || reader.IsDBNull(1) || reader.IsDBNull(2) || reader.IsDBNull(3))
        {
            return null;
        }

        var minX = Convert.ToDouble(reader.GetValue(0), CultureInfo.InvariantCulture);
        var minY = Convert.ToDouble(reader.GetValue(1), CultureInfo.InvariantCulture);
        var maxX = Convert.ToDouble(reader.GetValue(2), CultureInfo.InvariantCulture);
        var maxY = Convert.ToDouble(reader.GetValue(3), CultureInfo.InvariantCulture);

        var crs = service.Ogc.DefaultCrs.HasValue()
            ? service.Ogc.DefaultCrs!
            : (layer.Crs.FirstOrDefault() ?? "EPSG:4326");
        var normalizedCrs = CrsHelper.NormalizeIdentifier(crs);

        return new BoundingBox(minX, minY, maxX, maxY, null, null, normalizedCrs);
    }

    public async Task TestConnectivityAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        SqlExceptionHelper.ValidateConnectionStringForHealthCheck(dataSource.ConnectionString, dataSource.Id);

        await using var connection = CreateConnection(dataSource);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        command.CommandTimeout = 5; // 5 second timeout for health checks

        await _retryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteScalarAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Extracts connection and transaction from an IDataStoreTransaction or creates a new connection.
    /// This eliminates 15+ lines of boilerplate code in each CRUD method.
    /// This pattern should be promoted to RelationalDataStoreProviderBase to eliminate duplication across all providers.
    /// </summary>
    private async Task<(SqliteConnection Connection, SqliteTransaction? Transaction, bool ShouldDispose)> GetConnectionAndTransactionAsync(
        DataSourceDefinition dataSource,
        IDataStoreTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (transaction is RelationalDataStoreTransaction<SqliteConnection, SqliteTransaction> sqliteDataStoreTransaction)
        {
            // Transaction owns the connection - caller should NOT dispose it
            return (sqliteDataStoreTransaction.Connection, sqliteDataStoreTransaction.Transaction, false);
        }

        // No transaction provided - create new connection, open it, caller MUST dispose it
        var connection = CreateConnection(dataSource);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        return (connection, null, true);
    }

    private SqliteConnection CreateConnection(DataSourceDefinition dataSource)
    {
        SqlExceptionHelper.ValidateConnectionString(dataSource.ConnectionString, dataSource.Id);

        // Decrypt connection string if encryption service is available (synchronous version not available)
        var connectionString = _normalizedConnectionStrings.GetOrAdd(
            dataSource.ConnectionString,
            NormalizeConnectionString);

        return new SqliteConnection(connectionString);
    }

    private async Task<string> DecryptConnectionStringAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        if (_encryptionService == null)
        {
            return connectionString;
        }

        return await _decryptionCache.GetOrAdd(connectionString,
            async cs => await _encryptionService.DecryptAsync(cs, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
    }

    private static string NormalizeConnectionString(string connectionString)
    {
        // SECURITY: Validate connection string for SQL injection and malformed input
        ConnectionStringValidator.Validate(connectionString, ProviderKey);

        // Try to build SQLite connection string as-is first
        try
        {
            var builder = new SqliteConnectionStringBuilder(connectionString)
            {
                DefaultTimeout = DefaultCommandTimeoutSeconds
            };
            return builder.ConnectionString;
        }
        catch (ArgumentException)
        {
            // Connection string contains unsupported SQLite keywords (e.g., from SQL Server/MySQL)
            // Filter out database-specific keywords and retry
            var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
            var supportedParts = new List<string>();

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.IsNullOrEmpty())
                    continue;

                // Skip known unsupported keywords (keeping Data Source and other SQLite keywords)
                // NOTE: Password= and Pwd= are intentionally NOT filtered out as they are valid for encrypted SQLite databases
                if (trimmed.StartsWith("Version=", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("TrustServerCertificate=", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Encrypt=", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("MultipleActiveResultSets=", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Server=", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Port=", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("User ID=", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("User Id=", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Uid=", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Database=", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Initial Catalog=", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Integrated Security=", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Pooling=", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Min Pool Size=", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Max Pool Size=", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Connection Timeout=", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Connect Timeout=", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Application Name=", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                supportedParts.Add(trimmed);
            }

            var filteredConnectionString = string.Join(";", supportedParts);

            try
            {
                var builder = new SqliteConnectionStringBuilder(filteredConnectionString)
                {
                    DefaultTimeout = DefaultCommandTimeoutSeconds
                };
                return builder.ConnectionString;
            }
            catch
            {
                throw new InvalidOperationException("SQLite connection string is invalid. Review honua configuration and ensure a valid Data Source is provided.");
            }
        }
    }

    private static SqliteCommand CreateCommand(SqliteConnection connection, SqliteQueryDefinition definition)
    {
        var command = connection.CreateCommand();
        command.CommandText = definition.Sql;
        command.CommandTimeout = DefaultCommandTimeoutSeconds;
        AddParameters(command, definition.Parameters);
        return command;
    }

    private static FeatureRecord CreateFeatureRecord(SqliteDataReader reader, LayerDefinition layer, int storageSrid, int targetSrid)
    {
        // SQLite stores geometry as text (WKT or GeoJSON)
        var skipColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            layer.GeometryField
        };

        string? geometryText = null;

        // Extract geometry text from the geometry column
        for (var index = 0; index < reader.FieldCount; index++)
        {
            var columnName = reader.GetName(index);
            if (string.Equals(columnName, layer.GeometryField, StringComparison.OrdinalIgnoreCase))
            {
                if (!reader.IsDBNull(index))
                {
                    geometryText = reader.GetString(index);
                }
                break;
            }
        }

        return FeatureRecordReader.ReadFeatureRecordWithCustomGeometry(
            reader,
            layer,
            layer.GeometryField,
            skipColumns,
            () => GeometryReader.ReadTextGeometry(geometryText, storageSrid, targetSrid));
    }


    private static NormalizedRecord NormalizeRecord(LayerDefinition layer, IReadOnlyDictionary<string, object?> attributes, bool includeKey)
    {
        var columns = new List<string>();
        var parameters = new List<KeyValuePair<string, object?>>();
        var parameterCounters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var ordinal = 0;
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);
        var geometryColumn = LayerMetadataHelper.GetGeometryColumn(layer);

        foreach (var pair in attributes)
        {
            if (!includeKey && string.Equals(pair.Key, keyColumn, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var columnName = SqliteFeatureQueryBuilder.QuoteIdentifier(pair.Key);
            var parameterName = CreateParameterName(pair.Key, parameterCounters, ordinal++);
            var value = NormalizeValue(pair.Key, pair.Value, geometryColumn);
            columns.Add(columnName);
            parameters.Add(new KeyValuePair<string, object?>(parameterName, value));
        }

        return new NormalizedRecord(columns, parameters);
    }

    private static object? NormalizeGeometryValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            return value switch
            {
                JsonNode node when node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var textValue) => NormalizeGeometryText(textValue),
                JsonNode node => NormalizeGeometryText(node.ToJsonString()),
                JsonElement element when element.ValueKind == JsonValueKind.Null => null,
                JsonElement element when element.ValueKind == JsonValueKind.String => NormalizeGeometryText(element.GetString()),
                JsonElement element => NormalizeGeometryText(element.GetRawText()),
                _ => NormalizeGeometryText(value.ToString())
            };
        }
        catch (Exception)
        {
            return value.ToString();
        }
    }

    private static string? NormalizeGeometryText(string? text)
    {
        if (text.IsNullOrWhiteSpace())
        {
            return null;
        }

        try
        {
            Geometry geometry;
            if (FeatureRecordNormalizer.LooksLikeJson(text))
            {
                geometry = GeoJsonReader.Read<Geometry>(text);
            }
            else
            {
                geometry = WktReader.Read(text);
            }

            return WktWriter.Write(geometry);
        }
        catch (Exception)
        {
            return text;
        }
    }


    private static object? NormalizeValue(string columnName, object? value, string geometryColumn)
    {
        if (value is null)
        {
            return null;
        }

        var isGeometry = string.Equals(columnName, geometryColumn, StringComparison.OrdinalIgnoreCase);
        if (isGeometry)
        {
            return NormalizeGeometryValue(value);
        }

        return FeatureRecordNormalizer.NormalizeValue(value, false);
    }

    private static string? TryResolveKey(IReadOnlyDictionary<string, object?> attributes, string keyColumn)
    {
        return SqlParameterHelper.TryResolveKey(attributes, keyColumn);
    }

    private static void AddParameters(SqliteCommand command, IEnumerable<KeyValuePair<string, object?>> parameters)
    {
        SqlParameterHelper.AddParameters(command, parameters);
    }

    protected override void DisposeCore()
    {
        _normalizedConnectionStrings.Clear();
        _decryptionCache.Clear();
    }

    private static string CreateParameterName(string columnName, IDictionary<string, int> counters, int ordinal)
    {
        return SqlParameterHelper.CreateUniqueParameterName(columnName, counters, ordinal);
    }

    private sealed record NormalizedRecord(IReadOnlyList<string> Columns, IReadOnlyList<KeyValuePair<string, object?>> Parameters);
}

