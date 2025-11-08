// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
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
using DuckDB.NET.Data;
using Polly;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Data.DuckDB;

public sealed class DuckDBDataStoreProvider : DisposableBase, IDataStoreProvider
{
    private const int DefaultCommandTimeoutSeconds = 30;
    private const int BulkBatchSize = 1000; // DuckDB excels at bulk operations

    private static readonly GeoJsonReader GeoJsonReader = new();
    private static readonly WKTReader WktReader = new();
    private static readonly WKTWriter WktWriter = new();

    private readonly ConcurrentDictionary<string, string> _normalizedConnectionStrings = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Task<string>> _decryptionCache = new(StringComparer.Ordinal);
    private readonly ResiliencePipeline _retryPipeline;
    private readonly IConnectionStringEncryptionService? _encryptionService;

    public const string ProviderKey = "duckdb";

    public DuckDBDataStoreProvider(IConnectionStringEncryptionService? encryptionService = null)
    {
        _retryPipeline = DatabaseRetryPolicy.CreateDefaultRetryPipeline();
        _encryptionService = encryptionService;
    }

    public string Provider => ProviderKey;

    public IDataStoreCapabilities Capabilities => DuckDBDataStoreCapabilities.Instance;

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

        // Ensure spatial extension is loaded
        await EnsureSpatialExtensionAsync(connection, cancellationToken).ConfigureAwait(false);

        var builder = new DuckDBFeatureQueryBuilder(service, layer);
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

        var builder = new DuckDBFeatureQueryBuilder(service, layer);
        var definition = builder.BuildCount(normalizedQuery);

        await using var command = CreateCommand(connection, definition);
        var result = await _retryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteScalarAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
        if (result is null or DBNull)
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

        await EnsureSpatialExtensionAsync(connection, cancellationToken).ConfigureAwait(false);

        var builder = new DuckDBFeatureQueryBuilder(service, layer);
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

        var table = LayerMetadataHelper.GetTableExpression(layer, DuckDBFeatureQueryBuilder.QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        var (connection, duckDbTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource,
            transaction,
            cancellationToken).ConfigureAwait(false);

        try
        {
            await EnsureSpatialExtensionAsync(connection, cancellationToken).ConfigureAwait(false);

            var normalized = NormalizeRecord(layer, record.Attributes, includeKey: true);
            if (normalized.Columns.Count == 0)
            {
                throw new InvalidOperationException($"Create operation for layer '{layer.Id}' did not include any columns.");
            }

            var columnList = string.Join(", ", normalized.Columns);
            var parameterList = string.Join(", ", normalized.Parameters.Select((_, i) => $"${i + 1}"));

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = duckDbTransaction;
                command.CommandText = $"INSERT INTO {table} ({columnList}) VALUES ({parameterList}) RETURNING *;";
                AddParameters(command, normalized.Parameters);

                await using var reader = await _retryPipeline.ExecuteAsync(async ct =>
                    await command.ExecuteReaderAsync(ct).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);

                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var storageSrid = layer.Storage?.Srid ?? CrsHelper.Wgs84;
                    var targetSrid = CrsHelper.ParseCrs(service.Ogc.DefaultCrs);
                    return CreateFeatureRecord(reader, layer, storageSrid, targetSrid);
                }

                throw new InvalidOperationException("Failed to load feature after insert.");
            }
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

        var table = LayerMetadataHelper.GetTableExpression(layer, DuckDBFeatureQueryBuilder.QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        var (connection, duckDbTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
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

            var assignmentBuilder = new StringBuilder();
            for (var i = 0; i < normalized.Columns.Count; i++)
            {
                if (assignmentBuilder.Length > 0)
                {
                    assignmentBuilder.Append(", ");
                }

                assignmentBuilder.Append(normalized.Columns[i]);
                assignmentBuilder.Append(" = $");
                assignmentBuilder.Append(i + 1);
            }

            var assignments = assignmentBuilder.ToString();
            var parameters = normalized.Parameters.ToList();
            parameters.Add(new KeyValuePair<string, object?>("key", featureId));

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = duckDbTransaction;
                command.CommandText = $"UPDATE {table} SET {assignments} WHERE {DuckDBFeatureQueryBuilder.QuoteIdentifier(keyColumn)} = ${parameters.Count}";
                AddParameters(command, parameters);
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

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNullOrWhiteSpace(featureId);

        var table = LayerMetadataHelper.GetTableExpression(layer, DuckDBFeatureQueryBuilder.QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        var (connection, duckDbTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource,
            transaction,
            cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = duckDbTransaction;
            command.CommandText = $"DELETE FROM {table} WHERE {DuckDBFeatureQueryBuilder.QuoteIdentifier(keyColumn)} = $1";
            command.Parameters.Add(new DuckDBParameter(featureId));
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

        var table = LayerMetadataHelper.GetTableExpression(layer, DuckDBFeatureQueryBuilder.QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        var (connection, duckDbTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource,
            transaction,
            cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = duckDbTransaction;
            command.CommandText = $@"
                UPDATE {table}
                SET is_deleted = 1,
                    deleted_at = CURRENT_TIMESTAMP,
                    deleted_by = $1
                WHERE {DuckDBFeatureQueryBuilder.QuoteIdentifier(keyColumn)} = $2
                  AND (is_deleted IS NULL OR is_deleted = 0)";
            command.Parameters.Add(new DuckDBParameter(deletedBy ?? (object)DBNull.Value));
            command.Parameters.Add(new DuckDBParameter(featureId));
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

        var table = LayerMetadataHelper.GetTableExpression(layer, DuckDBFeatureQueryBuilder.QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        var (connection, duckDbTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource,
            transaction,
            cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = duckDbTransaction;
            command.CommandText = $@"
                UPDATE {table}
                SET is_deleted = 0,
                    deleted_at = NULL,
                    deleted_by = NULL
                WHERE {DuckDBFeatureQueryBuilder.QuoteIdentifier(keyColumn)} = $1
                  AND is_deleted = 1";
            command.Parameters.Add(new DuckDBParameter(featureId));
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

        var table = LayerMetadataHelper.GetTableExpression(layer, DuckDBFeatureQueryBuilder.QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        var (connection, duckDbTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource,
            transaction,
            cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = duckDbTransaction;
            command.CommandText = $"DELETE FROM {table} WHERE {DuckDBFeatureQueryBuilder.QuoteIdentifier(keyColumn)} = $1";
            command.Parameters.Add(new DuckDBParameter(featureId));
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

        await EnsureSpatialExtensionAsync(connection, cancellationToken).ConfigureAwait(false);

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
        DuckDBConnection connection,
        LayerDefinition layer,
        List<FeatureRecord> batch,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return 0;
        }

        var table = LayerMetadataHelper.GetTableExpression(layer, DuckDBFeatureQueryBuilder.QuoteIdentifier);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

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
                var parameterList = string.Join(", ", normalized.Parameters.Select((_, i) => $"${i + 1}"));

                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = $"INSERT INTO {table} ({columnList}) VALUES ({parameterList})";
                AddParameters(command, normalized.Parameters);
                await _retryPipeline.ExecuteAsync(async ct =>
                    await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
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
        DuckDBConnection connection,
        LayerDefinition layer,
        List<KeyValuePair<string, FeatureRecord>> batch,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return 0;
        }

        var table = LayerMetadataHelper.GetTableExpression(layer, DuckDBFeatureQueryBuilder.QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var count = 0;
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
                    assignmentBuilder.Append(" = $");
                    assignmentBuilder.Append(i + 1);
                }

                var parameters = normalized.Parameters.ToList();
                parameters.Add(new KeyValuePair<string, object?>("key", update.Key));

                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = $"UPDATE {table} SET {assignmentBuilder} WHERE {DuckDBFeatureQueryBuilder.QuoteIdentifier(keyColumn)} = ${parameters.Count}";
                AddParameters(command, parameters);
                var affected = await _retryPipeline.ExecuteAsync(async ct =>
                    await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
                count += affected;
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return count;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<int> ExecuteDeleteBatchAsync(
        DuckDBConnection connection,
        LayerDefinition layer,
        List<string> batch,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return 0;
        }

        var table = LayerMetadataHelper.GetTableExpression(layer, DuckDBFeatureQueryBuilder.QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var parameterPlaceholders = string.Join(", ", batch.Select((_, i) => $"${i + 1}"));

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"DELETE FROM {table} WHERE {DuckDBFeatureQueryBuilder.QuoteIdentifier(keyColumn)} IN ({parameterPlaceholders})";

            foreach (var id in batch)
            {
                command.Parameters.Add(new DuckDBParameter(id));
            }

            var affected = await _retryPipeline.ExecuteAsync(async ct =>
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return affected;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
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
        // DuckDB does not support native MVT generation
        return Task.FromResult<byte[]?>(null);
    }

    public async Task<IDataStoreTransaction?> BeginTransactionAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        DuckDBConnection? connection = null;
        try
        {
            connection = CreateConnection(dataSource);
            await _retryPipeline.ExecuteAsync(async ct =>
                await connection.OpenAsync(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            return new RelationalDataStoreTransaction<DuckDBConnection, DuckDBTransaction>(connection, transaction);
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

        var builder = new DuckDBFeatureQueryBuilder(service, layer);
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

        var builder = new DuckDBFeatureQueryBuilder(service, layer);
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

        await EnsureSpatialExtensionAsync(connection, cancellationToken).ConfigureAwait(false);

        var builder = new DuckDBFeatureQueryBuilder(service, layer);
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

    private async Task<(DuckDBConnection Connection, DuckDBTransaction? Transaction, bool ShouldDispose)> GetConnectionAndTransactionAsync(
        DataSourceDefinition dataSource,
        IDataStoreTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (transaction is RelationalDataStoreTransaction<DuckDBConnection, DuckDBTransaction> duckDbDataStoreTransaction)
        {
            return (duckDbDataStoreTransaction.Connection, duckDbDataStoreTransaction.Transaction, false);
        }

        var connection = CreateConnection(dataSource);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        return (connection, null, true);
    }

    private DuckDBConnection CreateConnection(DataSourceDefinition dataSource)
    {
        SqlExceptionHelper.ValidateConnectionString(dataSource.ConnectionString, dataSource.Id);

        var connectionString = _normalizedConnectionStrings.GetOrAdd(
            dataSource.ConnectionString,
            NormalizeConnectionString);

        return new DuckDBConnection(connectionString);
    }

    private static string NormalizeConnectionString(string connectionString)
    {
        ConnectionStringValidator.Validate(connectionString, ProviderKey);

        // DuckDB connection string is simple: just a file path or :memory:
        // Examples: "DataSource=:memory:" or "DataSource=/path/to/database.db"
        if (!connectionString.Contains("DataSource=", StringComparison.OrdinalIgnoreCase) &&
            !connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            // If just a path is provided, wrap it in DataSource=
            return $"DataSource={connectionString}";
        }

        return connectionString;
    }

    private static async Task EnsureSpatialExtensionAsync(DuckDBConnection connection, CancellationToken cancellationToken)
    {
        // Load the spatial extension for geometry support
        await using var command = connection.CreateCommand();
        command.CommandText = "INSTALL spatial; LOAD spatial;";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static DuckDBCommand CreateCommand(DuckDBConnection connection, DuckDBQueryDefinition definition)
    {
        var command = connection.CreateCommand();
        command.CommandText = definition.Sql;
        command.CommandTimeout = DefaultCommandTimeoutSeconds;
        AddParameters(command, definition.Parameters);
        return command;
    }

    private static FeatureRecord CreateFeatureRecord(IDataReader reader, LayerDefinition layer, int storageSrid, int targetSrid)
    {
        var skipColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            layer.GeometryField
        };

        string? geometryText = null;

        // Extract geometry as WKT from DuckDB spatial extension
        for (var index = 0; index < reader.FieldCount; index++)
        {
            var columnName = reader.GetName(index);
            if (string.Equals(columnName, layer.GeometryField, StringComparison.OrdinalIgnoreCase))
            {
                if (!reader.IsDBNull(index))
                {
                    var value = reader.GetValue(index);
                    // DuckDB spatial extension returns geometry as WKB bytes or can be converted to WKT
                    geometryText = value?.ToString();
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
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);
        var geometryColumn = LayerMetadataHelper.GetGeometryColumn(layer);

        foreach (var pair in attributes)
        {
            if (!includeKey && string.Equals(pair.Key, keyColumn, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var columnName = DuckDBFeatureQueryBuilder.QuoteIdentifier(pair.Key);
            var value = NormalizeValue(pair.Key, pair.Value, geometryColumn);
            columns.Add(columnName);
            parameters.Add(new KeyValuePair<string, object?>(pair.Key, value));
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
        catch (Exception ex)
        {
            // Log geometry conversion failures in debug builds
            Debug.WriteLine($"Failed to convert geometry value: {ex.Message}");
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
        catch (Exception ex)
        {
            // Log WKT parsing failures in debug builds
            Debug.WriteLine($"Failed to parse geometry text as WKT: {ex.Message}");
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

    private static void AddParameters(DuckDBCommand command, IEnumerable<KeyValuePair<string, object?>> parameters)
    {
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(new DuckDBParameter(parameter.Value ?? DBNull.Value));
        }
    }

    protected override void DisposeCore()
    {
        _normalizedConnectionStrings.Clear();
        _decryptionCache.Clear();
    }

    private sealed record NormalizedRecord(IReadOnlyList<string> Columns, IReadOnlyList<KeyValuePair<string, object?>> Parameters);
}
