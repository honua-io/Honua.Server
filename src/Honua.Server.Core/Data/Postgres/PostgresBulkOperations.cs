// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Microsoft.Extensions.Logging;
using Npgsql;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data.Postgres;

/// <summary>
/// Handles bulk insert, update, and delete operations for PostgreSQL features.
/// </summary>
internal sealed class PostgresBulkOperations
{
    private const int BulkBatchSize = 1000;

    private readonly PostgresConnectionManager _connectionManager;
    private readonly ILogger<PostgresBulkOperations> _logger;

    public PostgresBulkOperations(
        PostgresConnectionManager connectionManager,
        ILogger<PostgresBulkOperations> logger)
    {
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> BulkInsertAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<FeatureRecord> records,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(records);

        var table = PostgresRecordMapper.ResolveTableName(layer);
        var srid = layer.Storage?.Srid ?? CrsHelper.Wgs84;
        var geometryColumn = layer.Storage?.GeometryColumn ?? layer.GeometryField;

        await using var connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        // Get the first record to determine columns, but don't buffer the rest
        FeatureRecord? firstRecord = null;
        IAsyncEnumerator<FeatureRecord>? enumerator = null;

        try
        {
            enumerator = records.GetAsyncEnumerator(cancellationToken);
            if (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                firstRecord = enumerator.Current;
            }

            if (firstRecord == null)
            {
                return 0;
            }

            // Determine columns from first record
            var normalized = PostgresRecordMapper.NormalizeRecord(layer, firstRecord.Attributes, includeKey: true);
            if (normalized.Columns.Count == 0)
            {
                return 0;
            }

            var columnNames = normalized.Columns.Select(c => c.ColumnName.Trim('"')).ToList();
            var copyCommand = $"COPY {table} ({string.Join(", ", normalized.Columns.Select(c => c.ColumnName))}) FROM STDIN (FORMAT BINARY)";

            // Start transaction to ensure atomicity
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var count = 0;

                await using (var writer = await connection.BeginBinaryImportAsync(copyCommand, cancellationToken).ConfigureAwait(false))
                {
                    // Write first record
                    await WriteRecordAsync(writer, normalized, cancellationToken).ConfigureAwait(false);
                    count++;

                    // Stream remaining records directly into COPY without buffering
                    while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        var record = enumerator.Current;
                        var recordNormalized = PostgresRecordMapper.NormalizeRecord(layer, record.Attributes, includeKey: true);
                        await WriteRecordAsync(writer, recordNormalized, cancellationToken).ConfigureAwait(false);
                        count++;
                    }

                    await writer.CompleteAsync(cancellationToken).ConfigureAwait(false);
                }

                // Commit transaction only on success
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(
                    "Bulk insert completed successfully for layer {LayerId}: {Count} records inserted",
                    layer.Id,
                    count);

                return count;
            }
            catch (Exception ex)
            {
                // Rollback transaction on any failure
                _logger.LogError(
                    ex,
                    "Bulk insert failed for layer {LayerId}, rolling back transaction",
                    layer.Id);

                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);

                throw new InvalidOperationException(
                    $"Bulk insert failed for layer '{layer.Id}': {ex.Message}. All changes rolled back.",
                    ex);
            }
        }
        finally
        {
            if (enumerator != null)
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task<int> BulkUpdateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<KeyValuePair<string, FeatureRecord>> updates,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(updates);

        return await PerformanceMeasurement.MeasureAsync(
            "BulkUpdate",
            async () =>
            {
                var table = PostgresRecordMapper.ResolveTableName(layer);
                var keyColumn = PostgresRecordMapper.ResolvePrimaryKey(layer);
                var srid = layer.Storage?.Srid ?? CrsHelper.Wgs84;

                await using var connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
                await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
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
            },
            (duration, count) => _logger.LogInformation(
                "Bulk update completed for layer {LayerId} in {Duration}ms: {Count} records updated",
                layer.Id, duration.TotalMilliseconds, count)
        ).ConfigureAwait(false);
    }

    public async Task<int> BulkDeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<string> featureIds,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(featureIds);

        return await PerformanceMeasurement.MeasureAsync(
            "BulkDelete",
            async () =>
            {
                var table = PostgresRecordMapper.ResolveTableName(layer);
                var keyColumn = PostgresRecordMapper.ResolvePrimaryKey(layer);

                await using var connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
                await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
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
            },
            (duration, count) => _logger.LogInformation(
                "Bulk delete completed for layer {LayerId} in {Duration}ms: {Count} records deleted",
                layer.Id, duration.TotalMilliseconds, count)
        ).ConfigureAwait(false);
    }

    private static async Task WriteRecordAsync(
        NpgsqlBinaryImporter writer,
        NormalizedRecord normalized,
        CancellationToken cancellationToken)
    {
        await writer.StartRowAsync(cancellationToken).ConfigureAwait(false);

        foreach (var column in normalized.Columns)
        {
            var value = normalized.Parameters.TryGetValue(column.ParameterName, out var paramValue) ? paramValue : DBNull.Value;

            if (value == DBNull.Value || value == null)
            {
                await writer.WriteNullAsync(cancellationToken).ConfigureAwait(false);
            }
            else if (column.IsGeometry)
            {
                // For geometry columns, we need to use special handling
                // PostgreSQL COPY BINARY mode expects raw bytes for geometry
                await writer.WriteAsync(value, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await writer.WriteAsync(value, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<int> ExecuteUpdateBatchAsync(
        NpgsqlConnection connection,
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
            // Normalize first record to determine update columns
            var firstNormalized = PostgresRecordMapper.NormalizeRecord(layer, batch[0].Value.Attributes, includeKey: false);
            if (firstNormalized.Columns.Count == 0)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return 0;
            }

            var updateColumns = firstNormalized.Columns.Select(c => c.ColumnName.Trim('"')).ToList();

            // Build single UPDATE FROM VALUES statement for entire batch
            var sql = new System.Text.StringBuilder();
            sql.Append($"UPDATE {table} AS t SET ");

            // Add SET clauses referencing the VALUES table alias
            sql.Append(string.Join(", ", updateColumns.Select(c =>
                $"{PostgresRecordMapper.QuoteIdentifier(c)} = v.{PostgresRecordMapper.QuoteIdentifier(c)}")));

            // Build VALUES clause with all records
            sql.Append(" FROM (VALUES ");

            var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);

            for (int i = 0; i < batch.Count; i++)
            {
                var update = batch[i];
                var normalized = PostgresRecordMapper.NormalizeRecord(layer, update.Value.Attributes, includeKey: false);

                // Add key parameter
                var keyParam = $"@k{i}";
                parameters[keyParam] = PostgresRecordMapper.NormalizeKeyParameter(layer, update.Key);

                sql.Append($"({keyParam}::bigint");

                // Add value parameters for each column
                foreach (var col in normalized.Columns)
                {
                    var paramName = $"@v{i}_{col.ColumnName.Trim('"')}";
                    if (normalized.Parameters.TryGetValue(col.ParameterName, out var value))
                    {
                        parameters[paramName] = value;
                    }
                    else
                    {
                        parameters[paramName] = DBNull.Value;
                    }

                    sql.Append($", {paramName}");
                }

                sql.Append(')');
                if (i < batch.Count - 1)
                {
                    sql.Append(", ");
                }
            }

            // Complete VALUES clause and add WHERE clause
            sql.Append($") AS v({PostgresRecordMapper.QuoteIdentifier(keyColumn)}, ");
            sql.Append(string.Join(", ", updateColumns.Select(c => PostgresRecordMapper.QuoteIdentifier(c))));
            sql.Append($") WHERE t.{PostgresRecordMapper.QuoteIdentifier(keyColumn)} = v.{PostgresRecordMapper.QuoteIdentifier(keyColumn)}");

            // Execute single UPDATE statement
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql.ToString();
            PostgresRecordMapper.AddParameters(command, parameters);

            var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            // Validate that all records in the batch were affected
            // This prevents silent partial failures where some IDs don't exist
            if (affected != batch.Count)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"Bulk update batch failed: expected to update {batch.Count} records but only {affected} were affected. " +
                    $"This indicates some feature IDs do not exist or were concurrently modified. Transaction rolled back.");
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return affected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bulk update batch failed for layer {LayerId}, rolling back transaction", layer.Id);
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Executes a batch of DELETE operations within a transaction.
    /// </summary>
    private async Task<int> ExecuteDeleteBatchAsync(
        NpgsqlConnection connection,
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

        // Use a transaction for the batch to ensure atomicity
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Build IN clause with parameters
            var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
            var parameterNames = new List<string>();

            for (var i = 0; i < batch.Count; i++)
            {
                var paramName = $"@id{i}";
                parameterNames.Add(paramName);
                parameters[paramName] = PostgresRecordMapper.NormalizeKeyParameter(layer, batch[i]);
            }

            var inClause = string.Join(", ", parameterNames);

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"DELETE FROM {table} WHERE {PostgresRecordMapper.QuoteIdentifier(keyColumn)} IN ({inClause})";
            PostgresRecordMapper.AddParameters(command, parameters);

            var affected = await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            // Validate that all records in the batch were deleted
            // This prevents silent partial failures where some IDs don't exist
            if (affected != batch.Count)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"Bulk delete batch failed: expected to delete {batch.Count} records but only {affected} were affected. " +
                    $"This indicates some feature IDs do not exist. Transaction rolled back.");
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return affected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bulk delete batch failed for layer {LayerId}, rolling back transaction", layer.Id);
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}
