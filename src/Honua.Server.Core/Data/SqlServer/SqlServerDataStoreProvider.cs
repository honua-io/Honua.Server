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
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Polly;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Data.SqlServer;

public sealed class SqlServerDataStoreProvider : DisposableBase, IDataStoreProvider
{
    private const int BulkBatchSize = 1000;

    private static readonly GeoJsonReader GeoJsonReader = new();
    private static readonly WKTReader WktReader = new();
    private static readonly WKTWriter WktWriter = new();

    private readonly ConcurrentDictionary<string, GeometryColumnInfo> _geometryCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SqlConnectionStringBuilder> _connectionBuilders = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Task<string>> _decryptionCache = new(StringComparer.Ordinal);
    private readonly ResiliencePipeline _retryPipeline;
    private readonly IConnectionStringEncryptionService? _encryptionService;
    private readonly DataAccessOptions _options;

    public const string ProviderKey = "sqlserver";

    public SqlServerDataStoreProvider(
        IOptions<DataAccessOptions>? options = null,
        IConnectionStringEncryptionService? encryptionService = null)
    {
        _retryPipeline = DatabaseRetryPolicy.CreateSqlServerRetryPipeline();
        _encryptionService = encryptionService;
        _options = options?.Value ?? new DataAccessOptions();
    }

    public string Provider => ProviderKey;

    public IDataStoreCapabilities Capabilities => SqlServerDataStoreCapabilities.Instance;

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

        var geometryInfo = await ResolveGeometryInfoAsync(connection, layer, cancellationToken).ConfigureAwait(false);
        var builder = new SqlServerFeatureQueryBuilder(service, layer, storageSrid, targetSrid, geometryInfo.IsGeography);
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
        var storageSrid = layer.Storage?.Srid ?? CrsHelper.Wgs84;
        var targetSrid = CrsHelper.ParseCrs(normalizedQuery.Crs ?? service.Ogc.DefaultCrs);

        await using var connection = CreateConnection(dataSource);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var geometryInfo = await ResolveGeometryInfoAsync(connection, layer, cancellationToken).ConfigureAwait(false);
        var builder = new SqlServerFeatureQueryBuilder(service, layer, storageSrid, targetSrid, geometryInfo.IsGeography);
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

        var normalizedQuery = query ?? new FeatureQuery();
        var storageSrid = layer.Storage?.Srid ?? CrsHelper.Wgs84;
        var targetSrid = CrsHelper.ParseCrs(normalizedQuery.Crs ?? service.Ogc.DefaultCrs);

        await using var connection = CreateConnection(dataSource);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var geometryInfo = await ResolveGeometryInfoAsync(connection, layer, cancellationToken).ConfigureAwait(false);
        var builder = new SqlServerFeatureQueryBuilder(service, layer, storageSrid, targetSrid, geometryInfo.IsGeography);
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
        // BUG FIX #17: Flow transaction through CRUD operations

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(record);

        var table = LayerMetadataHelper.GetTableExpression(layer, SqlServerFeatureQueryBuilder.QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        var (connection, sqlTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource, transaction, cancellationToken).ConfigureAwait(false);

        try
        {
            var geometryInfo = await ResolveGeometryInfoAsync(connection, layer, cancellationToken).ConfigureAwait(false);
            var normalized = NormalizeRecord(layer, record.Attributes, includeKey: true, geometryInfo.IsGeography);
            if (normalized.Columns.Count == 0)
            {
                throw new InvalidOperationException($"Create operation for layer '{layer.Id}' did not include any columns.");
            }

            var columnList = string.Join(", ", normalized.Columns.Select(c => c.ColumnName));
            var valueList = string.Join(", ", normalized.Columns.Select(c => BuildValueExpression(c, normalized.Srid, normalized.IsGeography)));

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = sqlTransaction;
                var keyIdentifier = SqlServerFeatureQueryBuilder.QuoteIdentifier(keyColumn);
                command.CommandText = $"insert into {table} ({columnList}) output inserted.{keyIdentifier} values ({valueList})";
                command.CommandTimeout = _options.DefaultCommandTimeoutSeconds;
                command.CommandType = CommandType.Text;
                AddParameters(command, normalized.Parameters);

                var insertedKey = await _retryPipeline.ExecuteAsync(async ct =>
                await command.ExecuteScalarAsync(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
                var keyValue = TryResolveKey(record.Attributes, keyColumn) ?? Convert.ToString(insertedKey, CultureInfo.InvariantCulture);
                if (keyValue.IsNullOrWhiteSpace())
                {
                    throw new InvalidOperationException("Failed to obtain key for inserted feature.");
                }

                return await GetAsync(dataSource, service, layer, keyValue!, null, cancellationToken).ConfigureAwait(false)
                       ?? throw new InvalidOperationException("Failed to load feature after insert.");
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
        // BUG FIX #17: Flow transaction through CRUD operations

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNullOrWhiteSpace(featureId);
        Guard.NotNull(record);

        var table = LayerMetadataHelper.GetTableExpression(layer, SqlServerFeatureQueryBuilder.QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        var (connection, sqlTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource, transaction, cancellationToken).ConfigureAwait(false);

        try
        {
            var geometryInfo = await ResolveGeometryInfoAsync(connection, layer, cancellationToken).ConfigureAwait(false);
            var normalized = NormalizeRecord(layer, record.Attributes, includeKey: false, geometryInfo.IsGeography);
            if (normalized.Columns.Count == 0)
            {
                return await GetAsync(dataSource, service, layer, featureId, null, cancellationToken);
            }

            var assignments = string.Join(", ", normalized.Columns.Select(c => $"{c.ColumnName} = {BuildValueExpression(c, normalized.Srid, normalized.IsGeography)}"));
            var parameters = new Dictionary<string, object?>(normalized.Parameters, StringComparer.Ordinal)
            {
                ["@key"] = NormalizeKeyParameter(layer, featureId)
            };

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = sqlTransaction;
                command.CommandText = $"update {table} set {assignments} where {SqlServerFeatureQueryBuilder.QuoteIdentifier(keyColumn)} = @key";
                command.CommandTimeout = _options.DefaultCommandTimeoutSeconds;
                command.CommandType = CommandType.Text;
                AddParameters(command, new ReadOnlyDictionary<string, object?>(parameters));
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
        // BUG FIX #17: Flow transaction through CRUD operations

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNullOrWhiteSpace(featureId);

        var table = LayerMetadataHelper.GetTableExpression(layer, SqlServerFeatureQueryBuilder.QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        var (connection, sqlTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource, transaction, cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = sqlTransaction;
            command.CommandText = $"delete from {table} where {SqlServerFeatureQueryBuilder.QuoteIdentifier(keyColumn)} = @key";
            command.CommandTimeout = _options.DefaultCommandTimeoutSeconds;
            command.CommandType = CommandType.Text;
            command.Parameters.AddWithValue("@key", NormalizeKeyParameter(layer, featureId));
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

        var table = LayerMetadataHelper.GetTableExpression(layer, SqlServerFeatureQueryBuilder.QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        var (connection, sqlTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource, transaction, cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = sqlTransaction;
            command.CommandText = $@"
                UPDATE {table}
                SET is_deleted = 1,
                    deleted_at = GETUTCDATE(),
                    deleted_by = @deletedBy
                WHERE {SqlServerFeatureQueryBuilder.QuoteIdentifier(keyColumn)} = @key
                  AND (is_deleted IS NULL OR is_deleted = 0)";
            command.CommandTimeout = _options.DefaultCommandTimeoutSeconds;
            command.CommandType = CommandType.Text;
            command.Parameters.AddWithValue("@key", NormalizeKeyParameter(layer, featureId));
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

        var table = LayerMetadataHelper.GetTableExpression(layer, SqlServerFeatureQueryBuilder.QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        var (connection, sqlTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource, transaction, cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = sqlTransaction;
            command.CommandText = $@"
                UPDATE {table}
                SET is_deleted = 0,
                    deleted_at = NULL,
                    deleted_by = NULL
                WHERE {SqlServerFeatureQueryBuilder.QuoteIdentifier(keyColumn)} = @key
                  AND is_deleted = 1";
            command.CommandTimeout = _options.DefaultCommandTimeoutSeconds;
            command.CommandType = CommandType.Text;
            command.Parameters.AddWithValue("@key", NormalizeKeyParameter(layer, featureId));
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

        var table = LayerMetadataHelper.GetTableExpression(layer, SqlServerFeatureQueryBuilder.QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        var (connection, sqlTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource, transaction, cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = sqlTransaction;
            command.CommandText = $"DELETE FROM {table} WHERE {SqlServerFeatureQueryBuilder.QuoteIdentifier(keyColumn)} = @key";
            command.CommandTimeout = _options.DefaultCommandTimeoutSeconds;
            command.CommandType = CommandType.Text;
            command.Parameters.AddWithValue("@key", NormalizeKeyParameter(layer, featureId));
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

        var geometryInfo = await ResolveGeometryInfoAsync(connection, layer, cancellationToken).ConfigureAwait(false);
        var table = LayerMetadataHelper.GetTableExpression(layer, SqlServerFeatureQueryBuilder.QuoteIdentifier);

        // Collect all records into a DataTable
        var dataTable = new DataTable();
        var columnsInitialized = false;
        var count = 0;

        await foreach (var record in records.WithCancellation(cancellationToken))
        {
            if (!columnsInitialized)
            {
                // Initialize columns from first record
                var normalized = NormalizeRecord(layer, record.Attributes, includeKey: true, geometryInfo.IsGeography);
                foreach (var column in normalized.Columns)
                {
                    var columnName = column.ColumnName.Trim('[', ']');
                    var dataColumn = new DataColumn(columnName);

                    // Set appropriate data type
                    if (column.IsGeometry)
                    {
                        dataColumn.DataType = typeof(string); // WKT representation
                    }
                    else
                    {
                        dataColumn.DataType = typeof(object);
                    }

                    dataTable.Columns.Add(dataColumn);
                }
                columnsInitialized = true;

                // Add first row
                AddRowToDataTable(dataTable, normalized);
                count++;
            }
            else
            {
                var normalized = NormalizeRecord(layer, record.Attributes, includeKey: true, geometryInfo.IsGeography);
                AddRowToDataTable(dataTable, normalized);
                count++;
            }
        }

        if (count == 0)
        {
            return 0;
        }

        // Use SqlBulkCopy for efficient insertion
        using var bulkCopy = new SqlBulkCopy(connection);
        bulkCopy.DestinationTableName = table;
        bulkCopy.BatchSize = BulkBatchSize;
        bulkCopy.BulkCopyTimeout = _options.DefaultCommandTimeoutSeconds;

        // Map columns
        foreach (DataColumn column in dataTable.Columns)
        {
            bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        await bulkCopy.WriteToServerAsync(dataTable, cancellationToken).ConfigureAwait(false);

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

        var table = LayerMetadataHelper.GetTableExpression(layer, SqlServerFeatureQueryBuilder.QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        await using var connection = CreateConnection(dataSource);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var geometryInfo = await ResolveGeometryInfoAsync(connection, layer, cancellationToken).ConfigureAwait(false);

        var count = 0;
        var batch = new List<KeyValuePair<string, FeatureRecord>>();

        await foreach (var update in updates.WithCancellation(cancellationToken))
        {
            batch.Add(update);

            if (batch.Count >= BulkBatchSize)
            {
                count += await ExecuteUpdateBatchAsync(connection, table, keyColumn, layer, batch, geometryInfo, cancellationToken).ConfigureAwait(false);
                batch.Clear();
            }
        }

        // Process remaining items
        if (batch.Count > 0)
        {
            count += await ExecuteUpdateBatchAsync(connection, table, keyColumn, layer, batch, geometryInfo, cancellationToken).ConfigureAwait(false);
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

        var table = LayerMetadataHelper.GetTableExpression(layer, SqlServerFeatureQueryBuilder.QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

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

    private void AddRowToDataTable(DataTable dataTable, NormalizedRecord normalized)
    {
        var row = dataTable.NewRow();

        foreach (var column in normalized.Columns)
        {
            var columnName = column.ColumnName.Trim('[', ']');
            var value = normalized.Parameters.TryGetValue(column.ParameterName, out var paramValue) ? paramValue : DBNull.Value;

            if (value == DBNull.Value || value == null)
            {
                row[columnName] = DBNull.Value;
            }
            else
            {
                row[columnName] = value;
            }
        }

        dataTable.Rows.Add(row);
    }

    private async Task<int> ExecuteUpdateBatchAsync(
        SqlConnection connection,
        string table,
        string keyColumn,
        LayerDefinition layer,
        List<KeyValuePair<string, FeatureRecord>> batch,
        GeometryColumnInfo geometryInfo,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return 0;
        }

        // Use a transaction for the batch
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var affected = 0;

            foreach (var update in batch)
            {
                var normalized = NormalizeRecord(layer, update.Value.Attributes, includeKey: false, geometryInfo.IsGeography);
                if (normalized.Columns.Count == 0)
                {
                    continue;
                }

                var assignments = string.Join(", ", normalized.Columns.Select(c => $"{c.ColumnName} = {BuildValueExpression(c, normalized.Srid, normalized.IsGeography)}"));
                var parameters = new Dictionary<string, object?>(normalized.Parameters, StringComparer.Ordinal)
                {
                    ["@key"] = NormalizeKeyParameter(layer, update.Key)
                };

                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = $"UPDATE {table} SET {assignments} WHERE {SqlServerFeatureQueryBuilder.QuoteIdentifier(keyColumn)} = @key";
                command.CommandTimeout = _options.DefaultCommandTimeoutSeconds;
                command.CommandType = CommandType.Text;
                AddParameters(command, new ReadOnlyDictionary<string, object?>(parameters));

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
        SqlConnection connection,
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

        // BUG FIX #43: Split batches to respect SQL Server's 2,100 parameter limit
        // Large batches that exceed this limit will crash with SQL exceptions
        // We chunk to 2000 parameters to leave a safety margin
        const int maxParametersPerQuery = 2000;
        var totalAffected = 0;

        for (var offset = 0; offset < batch.Count; offset += maxParametersPerQuery)
        {
            var chunkSize = Math.Min(maxParametersPerQuery, batch.Count - offset);
            var chunk = batch.GetRange(offset, chunkSize);

            // Build IN clause with parameters for this chunk
            var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
            var parameterNames = new List<string>();

            for (var i = 0; i < chunk.Count; i++)
            {
                var paramName = $"@id{i}";
                parameterNames.Add(paramName);
                parameters[paramName] = NormalizeKeyParameter(layer, chunk[i]);
            }

            var inClause = string.Join(", ", parameterNames);

            await using var command = connection.CreateCommand();
            command.CommandText = $"DELETE FROM {table} WHERE {SqlServerFeatureQueryBuilder.QuoteIdentifier(keyColumn)} IN ({inClause})";
            command.CommandTimeout = _options.DefaultCommandTimeoutSeconds;
            command.CommandType = CommandType.Text;
            AddParameters(command, new ReadOnlyDictionary<string, object?>(parameters));

            var affected = await _retryPipeline.ExecuteAsync(async ct =>
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            totalAffected += affected;
        }

        return totalAffected;
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
        // SQL Server does not support native MVT generation
        return Task.FromResult<byte[]?>(null);
    }

    public async Task<IDataStoreTransaction?> BeginTransactionAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        // BUG FIX #17: Implement transaction support for SQL Server following Postgres pattern
        ThrowIfDisposed();

        SqlConnection? connection = null;
        try
        {
            connection = CreateConnection(dataSource);
            await _retryPipeline.ExecuteAsync(async ct =>
                await connection.OpenAsync(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            // Use REPEATABLE READ isolation level for government data integrity
            // This prevents non-repeatable reads and phantom reads during transaction execution
            var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
                System.Data.IsolationLevel.RepeatableRead,
                cancellationToken).ConfigureAwait(false);

            return new RelationalDataStoreTransaction<SqlConnection, SqlTransaction>(connection, transaction);
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

    public Task<IReadOnlyList<StatisticsResult>> QueryStatisticsAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IReadOnlyList<StatisticDefinition> statistics,
        IReadOnlyList<string>? groupByFields,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        return FallbackAggregationHelper.ComputeStatisticsAsync(
            query => QueryAsync(dataSource, service, layer, query, cancellationToken),
            filter,
            statistics,
            groupByFields,
            cancellationToken);
    }

    public Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IReadOnlyList<string> fieldNames,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        return FallbackAggregationHelper.ComputeDistinctAsync(
            query => QueryAsync(dataSource, service, layer, query, cancellationToken),
            filter,
            fieldNames,
            cancellationToken);
    }

    public Task<BoundingBox?> QueryExtentAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        return FallbackAggregationHelper.ComputeExtentAsync(
            query => QueryAsync(dataSource, service, layer, query, cancellationToken),
            filter,
            service,
            layer,
            cancellationToken);
    }

    public async Task TestConnectivityAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(dataSource);
        SqlExceptionHelper.ValidateConnectionStringForHealthCheck(dataSource.ConnectionString, dataSource.Id);

        await using var connection = CreateConnection(dataSource);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        command.CommandTimeout = 5; // 5 second timeout for health checks

        await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    private FeatureRecord CreateFeatureRecord(SqlDataReader reader, LayerDefinition layer, int storageSrid, int targetSrid)
    {
        // SQL Server uses STAsText() to return geometry as WKT in special columns
        var skipColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            SqlServerFeatureQueryBuilder.GeometryWktAlias,
            SqlServerFeatureQueryBuilder.GeometrySridAlias,
            layer.GeometryField
        };

        string? geometryText = null;
        int? geometrySrid = null;

        // Extract geometry WKT and SRID from the special columns
        for (var index = 0; index < reader.FieldCount; index++)
        {
            var columnName = reader.GetName(index);

            if (string.Equals(columnName, SqlServerFeatureQueryBuilder.GeometryWktAlias, StringComparison.OrdinalIgnoreCase))
            {
                geometryText = reader.IsDBNull(index) ? null : reader.GetString(index);
            }
            else if (string.Equals(columnName, SqlServerFeatureQueryBuilder.GeometrySridAlias, StringComparison.OrdinalIgnoreCase))
            {
                geometrySrid = reader.IsDBNull(index) ? null : reader.GetInt32(index);
            }
        }

        return FeatureRecordReader.ReadFeatureRecordWithCustomGeometry(
            reader,
            layer,
            layer.GeometryField,
            skipColumns,
            () => GeometryReader.ReadWktGeometry(geometryText, geometrySrid ?? storageSrid, targetSrid));
    }

    private SqlConnection CreateConnection(DataSourceDefinition dataSource)
    {
        SqlExceptionHelper.ValidateConnectionString(dataSource.ConnectionString, dataSource.Id);

        // BUG FIX #38: Decrypt connection string before building SqlConnectionStringBuilder
        // Note: This is a synchronous wrapper; callers should use async patterns where possible
        var decryptedConnectionString = DecryptConnectionStringAsync(dataSource.ConnectionString, CancellationToken.None).GetAwaiter().GetResult();

        var builder = _connectionBuilders.GetOrAdd(decryptedConnectionString, cs =>
        {
            // SECURITY: Validate connection string for SQL injection and malformed input
            ConnectionStringValidator.Validate(cs, ProviderKey);

            var b = new SqlConnectionStringBuilder(cs);

            // Configure connection pooling
            b.Pooling = _options.SqlServer.Pooling;
            b.MinPoolSize = _options.SqlServer.MinPoolSize;
            b.MaxPoolSize = _options.SqlServer.MaxPoolSize;
            b.ConnectTimeout = _options.SqlServer.ConnectTimeout;
            b.LoadBalanceTimeout = _options.SqlServer.ConnectionLifetime;

            // Set application name if not specified
            if (b.ApplicationName.IsNullOrWhiteSpace())
            {
                b.ApplicationName = _options.SqlServer.ApplicationName;
            }

            return b;
        });

        return new SqlConnection(builder.ConnectionString);
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

    private SqlCommand CreateCommand(SqlConnection connection, SqlServerQueryDefinition definition)
    {
        var command = connection.CreateCommand();
        command.CommandText = definition.Sql;
        command.CommandTimeout = _options.DefaultCommandTimeoutSeconds;
        command.CommandType = CommandType.Text;
        AddParameters(command, definition.Parameters);
        return command;
    }


    private NormalizedRecord NormalizeRecord(LayerDefinition layer, IReadOnlyDictionary<string, object?> attributes, bool includeKey, bool isGeography)
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

            var isGeometryColumn = string.Equals(columnName, geometryColumn, StringComparison.OrdinalIgnoreCase);
            var value = NormalizeValue(pair.Value, isGeometryColumn);
            if (isGeometryColumn && value is null)
            {
                continue;
            }

            var parameterName = $"@p{index++}";
            columns.Add(new NormalizedColumn(SqlServerFeatureQueryBuilder.QuoteIdentifier(columnName), parameterName, value, isGeometryColumn));
            parameters[parameterName] = value ?? DBNull.Value;
        }

        return new NormalizedRecord(columns, parameters, srid, isGeography);
    }

    private static object? NormalizeValue(object? value, bool isGeometry)
    {
        if (value is null)
        {
            return null;
        }

        if (isGeometry)
        {
            return value switch
            {
                JsonNode node => ConvertGeoJsonToWkt(node.ToJsonString()),
                JsonElement element => element.ValueKind == JsonValueKind.Null ? null : ConvertGeoJsonToWkt(element.ToString()),
                string text => ConvertGeoJsonToWkt(text),
                Geometry geometry => WktWriter.Write(geometry),
                _ => ConvertGeoJsonToWkt(value.ToString())
            };
        }

        // Use normalizer for non-geometry values
        return FeatureRecordNormalizer.NormalizeValue(value, false);
    }

    private static string? ConvertGeoJsonToWkt(string? text)
    {
        if (text.IsNullOrWhiteSpace())
        {
            return null;
        }

        try
        {
            var geometry = GeoJsonReader.Read<Geometry>(text);
            return geometry is null ? null : WktWriter.Write(geometry);
        }
        catch (Exception)
        {
            return text;
        }
    }

    private static string BuildValueExpression(NormalizedColumn column, int srid, bool isGeography)
    {
        if (column.IsGeometry)
        {
            if (column.IsNull)
            {
                return "NULL";
            }

            var factory = isGeography ? "geography::STGeomFromText" : "geometry::STGeomFromText";
            return $"{factory}({column.ParameterName}, {srid})";
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

    private static void AddParameters(SqlCommand command, IReadOnlyDictionary<string, object?> parameters)
    {
        SqlParameterHelper.AddParameters(command, parameters);
    }

    private async Task<(SqlConnection Connection, SqlTransaction? Transaction, bool ShouldDispose)> GetConnectionAndTransactionAsync(
        DataSourceDefinition dataSource,
        IDataStoreTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (transaction is RelationalDataStoreTransaction<SqlConnection, SqlTransaction> sqlDataStoreTransaction)
        {
            // Transaction owns the connection - caller should NOT dispose it
            return (sqlDataStoreTransaction.Connection, sqlDataStoreTransaction.Transaction, false);
        }

        // No transaction provided - create new connection, open it, caller MUST dispose it
        var connection = CreateConnection(dataSource);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        return (connection, null, true);
    }

    private async Task<GeometryColumnInfo> ResolveGeometryInfoAsync(SqlConnection connection, LayerDefinition layer, CancellationToken cancellationToken)
    {
        var table = layer.Storage?.Table ?? layer.Id;
        var geometryColumn = layer.Storage?.GeometryColumn ?? layer.GeometryField;
        var cacheKey = $"{table}|{geometryColumn}";

        if (_geometryCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var (schema, tableName) = SplitTableName(table);
        const string sql = @"select top(1) t.name from sys.columns c inner join sys.types t on c.user_type_id = t.user_type_id inner join sys.objects o on o.object_id = c.object_id inner join sys.schemas s on s.schema_id = o.schema_id where s.name = @schema and o.name = @table and c.name = @column";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = _options.DefaultCommandTimeoutSeconds;
        command.CommandType = CommandType.Text;
        command.Parameters.AddWithValue("@schema", schema);
        command.Parameters.AddWithValue("@table", tableName);
        command.Parameters.AddWithValue("@column", TrimIdentifier(geometryColumn));

        var result = await _retryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteScalarAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
        var typeName = Convert.ToString(result, CultureInfo.InvariantCulture);
        var info = new GeometryColumnInfo(string.Equals(typeName, "geography", StringComparison.OrdinalIgnoreCase));
        _geometryCache[cacheKey] = info;
        return info;
    }

    private static (string Schema, string Table) SplitTableName(string table)
    {
        var parts = table.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return ("dbo", TrimIdentifier(table));
        }

        if (parts.Length == 1)
        {
            return ("dbo", TrimIdentifier(parts[0]));
        }

        return (TrimIdentifier(parts[^2]), TrimIdentifier(parts[^1]));
    }

    private static string TrimIdentifier(string identifier)
    {
        if (identifier.IsNullOrWhiteSpace())
        {
            return identifier;
        }

        var trimmed = identifier.Trim();
        if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
        {
            trimmed = trimmed[1..^1];
        }

        return trimmed.Replace("]]", "]", StringComparison.Ordinal);
    }



    private sealed record NormalizedColumn(string ColumnName, string ParameterName, object? Value, bool IsGeometry)
    {
        public bool IsNull => Value is null || Value is DBNull;
    }

    private sealed record NormalizedRecord(IReadOnlyList<NormalizedColumn> Columns, IReadOnlyDictionary<string, object?> Parameters, int Srid, bool IsGeography);

    private sealed record GeometryColumnInfo(bool IsGeography);

    protected override void DisposeCore()
    {
        _geometryCache.Clear();
        _connectionBuilders.Clear();
        _decryptionCache.Clear();
    }
}

