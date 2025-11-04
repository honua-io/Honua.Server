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
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Security;
using Honua.Server.Core.Utilities;
using MySqlConnector;
using Polly;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Data.MySql;

public sealed class MySqlDataStoreProvider : DisposableBase, IDataStoreProvider
{
    private const int DefaultCommandTimeoutSeconds = 30;
    private const int BulkBatchSize = 1000;

    private readonly ConcurrentDictionary<string, Lazy<MySqlDataSource>> _dataSources = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Task<string>> _decryptionCache = new(StringComparer.Ordinal);
    private readonly ResiliencePipeline _retryPipeline;
    private readonly IConnectionStringEncryptionService? _encryptionService;

    public const string ProviderKey = "mysql";

    public MySqlDataStoreProvider(IConnectionStringEncryptionService? encryptionService = null)
    {
        _retryPipeline = DatabaseRetryPolicy.CreateMySqlRetryPipeline();
        _encryptionService = encryptionService;
    }

    public string Provider => ProviderKey;

    public IDataStoreCapabilities Capabilities => MySqlDataStoreCapabilities.Instance;

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

        await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var builder = CreateQueryBuilder(service, layer, storageSrid, targetSrid);
        var definition = builder.BuildSelect(normalizedQuery);

        await using var command = CreateCommand(connection, definition);
        await using var reader = await _retryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return CreateFeatureRecord(reader, layer);
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
        var storageSrid = layer.Storage?.Srid ?? CrsHelper.Wgs84;
        var targetSrid = CrsHelper.ParseCrs(normalizedQuery.Crs ?? service.Ogc.DefaultCrs);

        await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var builder = CreateQueryBuilder(service, layer, storageSrid, targetSrid);
        var definition = builder.BuildCount(normalizedQuery);

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

        await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var builder = CreateQueryBuilder(service, layer, storageSrid, targetSrid);
        var definition = builder.BuildById(featureId);

        await using var command = CreateCommand(connection, definition);
        await using var reader = await _retryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return CreateFeatureRecord(reader, layer);
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
        // BUG FIX #16: Flow transaction through CRUD operations

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(record);

        var table = LayerMetadataHelper.GetTableExpression(layer, QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        // Use base class helper to extract connection and transaction - eliminates 15 lines of boilerplate
        var (connection, mySqlTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
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

            var columnList = string.Join(", ", normalized.Columns.Select(c => c.ColumnName));
            var valueList = string.Join(", ", normalized.Columns.Select(c => BuildValueExpression(c, normalized.Srid)));

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = mySqlTransaction;
                command.CommandText = $"insert into {table} ({columnList}) values ({valueList})";
                AddParameters(command, normalized.Parameters);
                command.CommandTimeout = DefaultCommandTimeoutSeconds;
                await _retryPipeline.ExecuteAsync(async ct =>
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
            }

            var keyValue = TryResolveKey(record.Attributes, keyColumn);
            if (keyValue.IsNullOrWhiteSpace())
            {
                await using var identityCommand = connection.CreateCommand();
                identityCommand.Transaction = mySqlTransaction;
                identityCommand.CommandText = "select last_insert_id()";
                identityCommand.CommandTimeout = DefaultCommandTimeoutSeconds;
                var identity = await _retryPipeline.ExecuteAsync(async ct =>
                    await identityCommand.ExecuteScalarAsync(ct).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
                keyValue = Convert.ToString(identity, CultureInfo.InvariantCulture);
            }

            if (keyValue.IsNullOrWhiteSpace())
            {
                throw new InvalidOperationException("Failed to obtain key for inserted feature.");
            }

            return await GetAsync(dataSource, service, layer, keyValue, null, cancellationToken).ConfigureAwait(false)
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
        // BUG FIX #16: Flow transaction through CRUD operations

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNullOrWhiteSpace(featureId);
        Guard.NotNull(record);

        var table = LayerMetadataHelper.GetTableExpression(layer, QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        // Use helper to extract connection and transaction - eliminates 15 lines of boilerplate
        var (connection, mySqlTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource,
            transaction,
            cancellationToken).ConfigureAwait(false);

        try
        {
            var normalized = NormalizeRecord(layer, record.Attributes, includeKey: false);
            if (normalized.Columns.Count == 0)
            {
                return await GetAsync(dataSource, service, layer, featureId, null, cancellationToken);
            }

            var assignments = string.Join(", ", normalized.Columns.Select(c => $"{c.ColumnName} = {BuildValueExpression(c, normalized.Srid)}"));
            var parameters = new Dictionary<string, object?>(normalized.Parameters, StringComparer.Ordinal)
            {
                ["@key"] = NormalizeKeyParameter(layer, featureId)
            };

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = mySqlTransaction;
                command.CommandText = $"update {table} set {assignments} where {QuoteIdentifier(keyColumn)} = @key";
                AddParameters(command, new ReadOnlyDictionary<string, object?>(parameters));
                command.CommandTimeout = DefaultCommandTimeoutSeconds;
                var affected = await _retryPipeline.ExecuteAsync(async ct =>
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
                if (affected == 0)
                {
                    return null;
                }
            }

            return await GetAsync(dataSource, service, layer, featureId, null, cancellationToken);
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
        // BUG FIX #16: Flow transaction through CRUD operations

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNullOrWhiteSpace(featureId);

        var table = LayerMetadataHelper.GetTableExpression(layer, QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        // Use helper to extract connection and transaction - eliminates 15 lines of boilerplate
        var (connection, mySqlTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource,
            transaction,
            cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = mySqlTransaction;
            command.CommandText = $"delete from {table} where {QuoteIdentifier(keyColumn)} = @key";
            command.Parameters.AddWithValue("@key", NormalizeKeyParameter(layer, featureId));
            command.CommandTimeout = DefaultCommandTimeoutSeconds;
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

        var table = LayerMetadataHelper.GetTableExpression(layer, QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        // Use helper to extract connection and transaction - eliminates 15 lines of boilerplate
        var (connection, mySqlTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource,
            transaction,
            cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = mySqlTransaction;
            command.CommandText = $@"
                UPDATE {table}
                SET is_deleted = true,
                    deleted_at = NOW(),
                    deleted_by = @deletedBy
                WHERE {QuoteIdentifier(keyColumn)} = @key
                  AND (is_deleted IS NULL OR is_deleted = false)";
            command.Parameters.AddWithValue("@key", NormalizeKeyParameter(layer, featureId));
            command.Parameters.AddWithValue("@deletedBy", (object?)deletedBy ?? DBNull.Value);
            command.CommandTimeout = DefaultCommandTimeoutSeconds;
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

        var table = LayerMetadataHelper.GetTableExpression(layer, QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        // Use helper to extract connection and transaction - eliminates 15 lines of boilerplate
        var (connection, mySqlTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource,
            transaction,
            cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = mySqlTransaction;
            command.CommandText = $@"
                UPDATE {table}
                SET is_deleted = false,
                    deleted_at = NULL,
                    deleted_by = NULL
                WHERE {QuoteIdentifier(keyColumn)} = @key
                  AND is_deleted = true";
            command.Parameters.AddWithValue("@key", NormalizeKeyParameter(layer, featureId));
            command.CommandTimeout = DefaultCommandTimeoutSeconds;
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

        var table = LayerMetadataHelper.GetTableExpression(layer, QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        // Use helper to extract connection and transaction - eliminates 15 lines of boilerplate
        var (connection, mySqlTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource,
            transaction,
            cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = mySqlTransaction;
            command.CommandText = $"DELETE FROM {table} WHERE {QuoteIdentifier(keyColumn)} = @key";
            command.Parameters.AddWithValue("@key", NormalizeKeyParameter(layer, featureId));
            command.CommandTimeout = DefaultCommandTimeoutSeconds;
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

        var table = LayerMetadataHelper.GetTableExpression(layer, QuoteIdentifier);
        var srid = layer.Storage?.Srid ?? CrsHelper.Wgs84;

        await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
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
                count += await ExecuteInsertBatchAsync(connection, table, layer, batch, srid, cancellationToken).ConfigureAwait(false);
                batch.Clear();
            }
        }

        // Process remaining items
        if (batch.Count > 0)
        {
            count += await ExecuteInsertBatchAsync(connection, table, layer, batch, srid, cancellationToken).ConfigureAwait(false);
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

        var table = LayerMetadataHelper.GetTableExpression(layer, QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);
        var srid = layer.Storage?.Srid ?? CrsHelper.Wgs84;

        await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
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
                count += await ExecuteUpdateBatchAsync(connection, table, keyColumn, layer, batch, srid, cancellationToken).ConfigureAwait(false);
                batch.Clear();
            }
        }

        // Process remaining items
        if (batch.Count > 0)
        {
            count += await ExecuteUpdateBatchAsync(connection, table, keyColumn, layer, batch, srid, cancellationToken).ConfigureAwait(false);
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

        var table = LayerMetadataHelper.GetTableExpression(layer, QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
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
                count += await ExecuteDeleteBatchAsync(connection, table, keyColumn, layer, batch, cancellationToken).ConfigureAwait(false);
                batch.Clear();
            }
        }

        // Process remaining items
        if (batch.Count > 0)
        {
            count += await ExecuteDeleteBatchAsync(connection, table, keyColumn, layer, batch, cancellationToken).ConfigureAwait(false);
        }

        return count;
    }

    private async Task<int> ExecuteInsertBatchAsync(
        MySqlConnection connection,
        string table,
        LayerDefinition layer,
        List<FeatureRecord> batch,
        int srid,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return 0;
        }

        // Build multi-row INSERT statement
        var firstNormalized = NormalizeRecord(layer, batch[0].Attributes, includeKey: true);
        if (firstNormalized.Columns.Count == 0)
        {
            return 0;
        }

        var columnList = string.Join(", ", firstNormalized.Columns.Select(c => c.ColumnName));
        var valueRows = new List<string>();
        var allParameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        var parameterIndex = 0;

        for (var i = 0; i < batch.Count; i++)
        {
            var normalized = NormalizeRecord(layer, batch[i].Attributes, includeKey: true);
            var valueList = new List<string>();

            foreach (var column in normalized.Columns)
            {
                var paramName = $"@p{parameterIndex++}";
                valueList.Add(BuildValueExpression(new NormalizedColumn(column.ColumnName, paramName, column.Value, column.IsGeometry), srid));

                if (normalized.Parameters.TryGetValue(column.ParameterName, out var value))
                {
                    allParameters[paramName] = value;
                }
            }

            valueRows.Add($"({string.Join(", ", valueList)})");
        }

        var sql = $"INSERT INTO {table} ({columnList}) VALUES {string.Join(", ", valueRows)}";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = DefaultCommandTimeoutSeconds;
        AddParameters(command, new ReadOnlyDictionary<string, object?>(allParameters));

        await _retryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        return batch.Count;
    }

    private async Task<int> ExecuteUpdateBatchAsync(
        MySqlConnection connection,
        string table,
        string keyColumn,
        LayerDefinition layer,
        List<KeyValuePair<string, FeatureRecord>> batch,
        int srid,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return 0;
        }

        // Use a transaction for the batch
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

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

                var assignments = string.Join(", ", normalized.Columns.Select(c => $"{c.ColumnName} = {BuildValueExpression(c, normalized.Srid)}"));
                var parameters = new Dictionary<string, object?>(normalized.Parameters, StringComparer.Ordinal)
                {
                    ["@key"] = NormalizeKeyParameter(layer, update.Key)
                };

                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = $"UPDATE {table} SET {assignments} WHERE {QuoteIdentifier(keyColumn)} = @key";
                command.CommandTimeout = DefaultCommandTimeoutSeconds;
                AddParameters(command, new ReadOnlyDictionary<string, object?>(parameters));

                var rowsAffected = await _retryPipeline.ExecuteAsync(async ct =>
                    await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
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
        MySqlConnection connection,
        string table,
        string keyColumn,
        LayerDefinition layer,
        List<string> batch,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return 0;
        }

        // Build IN clause with parameters
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        var parameterNames = new List<string>();

        for (var i = 0; i < batch.Count; i++)
        {
            var paramName = $"@id{i}";
            parameterNames.Add(paramName);
            parameters[paramName] = NormalizeKeyParameter(layer, batch[i]);
        }

        var inClause = string.Join(", ", parameterNames);

        await using var command = connection.CreateCommand();
        command.CommandText = $"DELETE FROM {table} WHERE {QuoteIdentifier(keyColumn)} IN ({inClause})";
        command.CommandTimeout = DefaultCommandTimeoutSeconds;
        AddParameters(command, new ReadOnlyDictionary<string, object?>(parameters));

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
        // MySQL does not support native MVT generation
        return Task.FromResult<byte[]?>(null);
    }

    public async Task<IDataStoreTransaction?> BeginTransactionAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        // BUG FIX #16: Implement transaction support for MySQL following Postgres pattern
        ThrowIfDisposed();

        MySqlConnection? connection = null;
        try
        {
            connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
            await _retryPipeline.ExecuteAsync(async ct =>
                await connection.OpenAsync(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            // Use REPEATABLE READ isolation level for government data integrity
            // This prevents non-repeatable reads and phantom reads during transaction execution
            var transaction = await connection.BeginTransactionAsync(
                System.Data.IsolationLevel.RepeatableRead,
                cancellationToken).ConfigureAwait(false);

            return new RelationalDataStoreTransaction<MySqlConnection, MySqlTransaction>(connection, transaction);
        }
        catch
        {
            // CRITICAL: Dispose connection on any failure to prevent connection pool exhaustion
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
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(statistics);

        if (statistics.Count == 0)
        {
            return Array.Empty<StatisticsResult>();
        }

        var normalizedQuery = filter ?? new FeatureQuery();
        var storageSrid = layer.Storage?.Srid ?? CrsHelper.Wgs84;
        var targetSrid = CrsHelper.ParseCrs(normalizedQuery.Crs ?? service.Ogc.DefaultCrs);

        await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var builder = CreateQueryBuilder(service, layer, storageSrid, targetSrid);
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
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(fieldNames);

        if (fieldNames.Count == 0)
        {
            return Array.Empty<DistinctResult>();
        }

        var normalizedQuery = filter ?? new FeatureQuery();
        var storageSrid = layer.Storage?.Srid ?? CrsHelper.Wgs84;
        var targetSrid = CrsHelper.ParseCrs(normalizedQuery.Crs ?? service.Ogc.DefaultCrs);

        await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var builder = CreateQueryBuilder(service, layer, storageSrid, targetSrid);
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
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);

        var normalizedQuery = filter ?? new FeatureQuery();
        var storageSrid = layer.Storage?.Srid ?? CrsHelper.Wgs84;
        var targetSrid = CrsHelper.ParseCrs(normalizedQuery.Crs ?? service.Ogc.DefaultCrs);

        await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var builder = CreateQueryBuilder(service, layer, storageSrid, targetSrid);
        var definition = builder.BuildExtent(normalizedQuery, targetSrid);

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

        var crs = CrsHelper.NormalizeIdentifier($"EPSG:{targetSrid}");
        return new BoundingBox(minX, minY, maxX, maxY, null, null, crs);
    }

    public async Task TestConnectivityAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        SqlExceptionHelper.ValidateConnectionStringForHealthCheck(dataSource.ConnectionString, dataSource.Id);

        await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        command.CommandTimeout = 5; // 5 second timeout for health checks

        await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    private FeatureRecord CreateFeatureRecord(MySqlDataReader reader, LayerDefinition layer)
    {
        // MySQL uses ST_AsGeoJSON to return geometry as GeoJSON in a special column
        var skipColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            MySqlFeatureQueryBuilder.GeoJsonColumnAlias,
            layer.GeometryField
        };

        JsonNode? geometryNode = null;

        // Extract geometry from the GeoJSON alias column if present
        for (var index = 0; index < reader.FieldCount; index++)
        {
            var columnName = reader.GetName(index);
            if (string.Equals(columnName, MySqlFeatureQueryBuilder.GeoJsonColumnAlias, StringComparison.OrdinalIgnoreCase))
            {
                if (!reader.IsDBNull(index))
                {
                    var geoJsonText = reader.GetString(index);
                    geometryNode = GeometryReader.ReadGeoJsonGeometry(geoJsonText);
                }
                break;
            }
        }

        return FeatureRecordReader.ReadFeatureRecordWithCustomGeometry(
            reader,
            layer,
            layer.GeometryField,
            skipColumns,
            () => geometryNode);
    }

    private async Task<MySqlConnection> CreateConnectionAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken)
    {
        SqlExceptionHelper.ValidateConnectionString(dataSource.ConnectionString, dataSource.Id);

        var dataSourceInstance = await GetOrCreateDataSourceAsync(dataSource, cancellationToken).ConfigureAwait(false);
        return dataSourceInstance.CreateConnection();
    }

    private async Task<MySqlDataSource> GetOrCreateDataSourceAsync(DataSourceDefinition dataSource, CancellationToken cancellationToken = default)
    {
        // BUG FIX #37: Decrypt connection string before caching/normalizing (mirroring Postgres pattern)
        var decryptedConnectionString = await DecryptConnectionStringAsync(dataSource.ConnectionString, cancellationToken).ConfigureAwait(false);
        var connectionString = NormalizeConnectionString(decryptedConnectionString);
        var lazyDataSource = _dataSources.GetOrAdd(connectionString, key => new Lazy<MySqlDataSource>(
            () => new MySqlDataSource(key),
            LazyThreadSafetyMode.ExecutionAndPublication));

        return lazyDataSource.Value;
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

        var builder = new MySqlConnectionStringBuilder(connectionString);
        if (builder.ApplicationName.IsNullOrWhiteSpace())
        {
            builder.ApplicationName = "Honua.Server";
        }

        return builder.ConnectionString;
    }

    private static MySqlCommand CreateCommand(MySqlConnection connection, MySqlQueryDefinition definition)
    {
        var command = connection.CreateCommand();
        command.CommandText = definition.Sql;
        command.CommandTimeout = DefaultCommandTimeoutSeconds;
        command.CommandType = CommandType.Text;
        AddParameters(command, definition.Parameters);
        return command;
    }

    /// <summary>
    /// Extracts connection and transaction from an IDataStoreTransaction or creates a new connection.
    /// This eliminates 15+ lines of boilerplate code in each CRUD method.
    /// </summary>
    private async Task<(MySqlConnection Connection, MySqlTransaction? Transaction, bool ShouldDispose)> GetConnectionAndTransactionAsync(
        DataSourceDefinition dataSource,
        IDataStoreTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (transaction is RelationalDataStoreTransaction<MySqlConnection, MySqlTransaction> mysqlDataStoreTransaction)
        {
            // Transaction owns the connection - caller should NOT dispose it
            return (mysqlDataStoreTransaction.Connection, mysqlDataStoreTransaction.Transaction, false);
        }

        // No transaction provided - create new connection, open it, caller MUST dispose it
        var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        return (connection, null, true);
    }

    private static MySqlFeatureQueryBuilder CreateQueryBuilder(
        ServiceDefinition service,
        LayerDefinition layer,
        int storageSrid,
        int targetSrid)
    {
        return new MySqlFeatureQueryBuilder(service, layer, storageSrid, targetSrid);
    }


    private static NormalizedRecord NormalizeRecord(LayerDefinition layer, IReadOnlyDictionary<string, object?> attributes, bool includeKey)
    {
        var srid = layer.Storage?.Srid ?? CrsHelper.Wgs84;
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);
        var geometryColumn = LayerMetadataHelper.GetGeometryColumn(layer);

        var columns = new List<NormalizedColumn>();
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        var index = 0;

        foreach (var pair in attributes)
        {
            var columnName = pair.Key;
            if (!includeKey && string.Equals(columnName, keyColumn, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var isGeometry = string.Equals(columnName, geometryColumn, StringComparison.OrdinalIgnoreCase);
            var value = NormalizeValue(pair.Value, isGeometry);
            if (isGeometry && value is null)
            {
                continue;
            }

            var parameterName = $"@p{index++}";
            columns.Add(new NormalizedColumn(MySqlFeatureQueryBuilder.QuoteIdentifier(columnName), parameterName, value, isGeometry));
            parameters[parameterName] = value ?? DBNull.Value;
        }

        return new NormalizedRecord(columns, parameters, srid);
    }

    private static object? NormalizeValue(object? value, bool isGeometry)
    {
        return FeatureRecordNormalizer.NormalizeValue(value, isGeometry);
    }

    private static string BuildValueExpression(NormalizedColumn column, int srid)
    {
        if (column.IsGeometry)
        {
            // BUG FIX #42: Pass correct arguments to ST_GeomFromGeoJSON
            // MySQL signature: ST_GeomFromGeoJSON(geojson_str, [options], [srid])
            // The second 'options' parameter controls dimension handling:
            //   1 = reject non-collection geometries (WRONG - causes WKB warnings)
            //   NULL = accept all geometry types (CORRECT)
            // For standard GeoJSON geometries (Point, LineString, Polygon), we must pass NULL
            // See: https://dev.mysql.com/doc/refman/8.0/en/spatial-geojson-functions.html
            return column.IsNull
                ? "NULL"
                : $"ST_GeomFromGeoJSON({column.ParameterName}, NULL, {srid})";
        }

        return column.ParameterName;
    }

    private static object NormalizeKeyParameter(LayerDefinition layer, string featureId)
    {
        return SqlParameterHelper.NormalizeKeyParameter(layer, featureId);
    }

    private static string? TryResolveKey(IReadOnlyDictionary<string, object?> attributes, string keyColumn)
    {
        return SqlParameterHelper.TryResolveKey(attributes, keyColumn);
    }

    private static void AddParameters(MySqlCommand command, IReadOnlyDictionary<string, object?> parameters)
    {
        SqlParameterHelper.AddParameters(command, parameters);
    }

    private static string QuoteIdentifier(string identifier) => MySqlFeatureQueryBuilder.QuoteIdentifier(identifier);

    protected override void DisposeCore()
    {
        foreach (var entry in _dataSources.Values)
        {
            if (!entry.IsValueCreated)
            {
                continue;
            }

            entry.Value.Dispose();
        }

        _dataSources.Clear();
        _decryptionCache.Clear();
    }

    protected override async ValueTask DisposeCoreAsync()
    {
        foreach (var entry in _dataSources.Values)
        {
            if (!entry.IsValueCreated)
            {
                continue;
            }

            await entry.Value.DisposeAsync().ConfigureAwait(false);
        }

        _dataSources.Clear();
        _decryptionCache.Clear();

        await base.DisposeCoreAsync().ConfigureAwait(false);
    }

    private sealed record NormalizedColumn(string ColumnName, string ParameterName, object? Value, bool IsGeometry)
    {
        public bool IsNull => Value is null || Value is DBNull;
    }

    private sealed record NormalizedRecord(IReadOnlyList<NormalizedColumn> Columns, IReadOnlyDictionary<string, object?> Parameters, int Srid);
}

