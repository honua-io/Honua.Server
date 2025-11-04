// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Snowflake.Data.Client;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Enterprise.Data.Snowflake;

/// <summary>
/// Handles bulk insert, update, and delete operations for Snowflake features.
/// Optimized for Snowflake's batching capabilities (250-500 record batches).
/// </summary>
internal sealed class SnowflakeBulkOperations
{
    private const int InsertBatchSize = 250; // Snowflake performs well with 250-500 record batches
    private const int UpdateBatchSize = 250;
    private const int DeleteBatchSize = 1000;

    private readonly SnowflakeConnectionManager _connectionManager;

    public SnowflakeBulkOperations(SnowflakeConnectionManager connectionManager)
    {
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
    }

    /// <summary>
    /// Performs bulk inserts using batched multi-row INSERT statements.
    /// This is significantly faster than individual INSERT statements by reducing round trips.
    /// </summary>
    /// <param name="dataSource">Data source containing connection information</param>
    /// <param name="service">Service definition</param>
    /// <param name="layer">Layer definition with schema information</param>
    /// <param name="records">Records to insert</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Total number of rows inserted</returns>
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

        var count = 0;
        var batch = new List<FeatureRecord>(InsertBatchSize);

        await using var connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await foreach (var record in records.WithCancellation(cancellationToken))
        {
            batch.Add(record);

            if (batch.Count >= InsertBatchSize)
            {
                count += await ExecuteBatchInsertAsync(connection, layer, batch, cancellationToken).ConfigureAwait(false);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            count += await ExecuteBatchInsertAsync(connection, layer, batch, cancellationToken).ConfigureAwait(false);
        }

        return count;
    }

    /// <summary>
    /// Performs bulk updates using batched UPDATE FROM VALUES statements.
    /// This is 10-50x faster than individual UPDATE statements by reducing round trips.
    /// </summary>
    /// <param name="dataSource">Data source containing connection information</param>
    /// <param name="service">Service definition</param>
    /// <param name="layer">Layer definition with schema information</param>
    /// <param name="records">Key-value pairs of feature IDs and updated records</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Total number of rows updated</returns>
    public async Task<int> BulkUpdateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<KeyValuePair<string, FeatureRecord>> records,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(records);

        var batch = new List<KeyValuePair<string, FeatureRecord>>(UpdateBatchSize);
        var totalCount = 0;

        await using var connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var txn = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var transaction = (SnowflakeDbTransaction)txn;

        try
        {
            await foreach (var kvp in records.WithCancellation(cancellationToken))
            {
                batch.Add(kvp);

                if (batch.Count >= UpdateBatchSize)
                {
                    totalCount += await ExecuteBatchedUpdateAsync(connection, transaction, layer, batch, cancellationToken).ConfigureAwait(false);
                    batch.Clear();
                }
            }

            // Process remaining records
            if (batch.Count > 0)
            {
                totalCount += await ExecuteBatchedUpdateAsync(connection, transaction, layer, batch, cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return totalCount;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
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

        var count = 0;
        var batch = new List<string>(DeleteBatchSize);

        await using var connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await foreach (var id in featureIds.WithCancellation(cancellationToken))
        {
            batch.Add(id);

            if (batch.Count >= DeleteBatchSize)
            {
                count += await ExecuteBatchDeleteAsync(connection, layer, batch, cancellationToken).ConfigureAwait(false);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            count += await ExecuteBatchDeleteAsync(connection, layer, batch, cancellationToken).ConfigureAwait(false);
        }

        return count;
    }

    /// <summary>
    /// Executes a batched insert using multi-row INSERT VALUES syntax.
    /// Combines multiple inserts into a single SQL statement for better performance.
    /// </summary>
    /// <param name="connection">Active Snowflake connection</param>
    /// <param name="layer">Layer definition</param>
    /// <param name="batch">Batch of records to insert</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of rows inserted</returns>
    private async Task<int> ExecuteBatchInsertAsync(
        SnowflakeDbConnection connection,
        LayerDefinition layer,
        List<FeatureRecord> batch,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return 0;
        }

        var builder = new SnowflakeFeatureQueryBuilder(layer);
        var table = builder.GetTableExpression();

        await using var txn = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var transaction = (SnowflakeDbTransaction)txn;

        try
        {
            // Determine columns from first record (excluding _geojson)
            var firstRecord = batch[0];
            var columns = firstRecord.Attributes.Keys
                .Where(k => !string.Equals(k, "_geojson", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (columns.Count == 0)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return 0;
            }

            // Build multi-row INSERT statement
            var quotedColumns = columns.Select(SnowflakeRecordMapper.QuoteIdentifier);
            var valuesList = new List<string>();
            var parameters = new Dictionary<string, object?>();

            for (int i = 0; i < batch.Count; i++)
            {
                var record = batch[i];
                var values = new List<string>();

                int fieldIndex = 0;
                foreach (var column in columns)
                {
                    var paramName = $"p{i}_{fieldIndex}";
                    values.Add($":{paramName}");
                    parameters[paramName] = record.Attributes.TryGetValue(column, out var val) ? val : null;
                    fieldIndex++;
                }

                valuesList.Add($"({string.Join(", ", values)})");
            }

            var sql = $@"
INSERT INTO {table} ({string.Join(", ", quotedColumns)})
VALUES {string.Join(", ", valuesList)}";

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;

            // Use consolidated SqlParameterHelper for parameter handling
            SqlParameterHelper.AddParameters(command, parameters);

            var count = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return count;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Executes a batched update using Snowflake's UPDATE FROM VALUES syntax.
    /// Combines multiple updates into a single SQL statement for better performance.
    /// </summary>
    /// <param name="connection">Active Snowflake connection</param>
    /// <param name="transaction">Active transaction</param>
    /// <param name="layer">Layer definition</param>
    /// <param name="batch">Batch of records to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of rows affected</returns>
    private async Task<int> ExecuteBatchedUpdateAsync(
        SnowflakeDbConnection connection,
        SnowflakeDbTransaction transaction,
        LayerDefinition layer,
        List<KeyValuePair<string, FeatureRecord>> batch,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return 0;
        }

        var builder = new SnowflakeFeatureQueryBuilder(layer);
        var table = builder.GetTableExpression();
        var keyColumn = builder.GetPrimaryKeyColumn();

        // Determine update columns from first record (excluding _geojson and primary key)
        var firstRecord = batch[0].Value;
        var allFields = firstRecord.Attributes.Keys
            .Where(k => !string.Equals(k, "_geojson", StringComparison.OrdinalIgnoreCase))
            .Where(k => !string.Equals(k, keyColumn, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (allFields.Count == 0)
        {
            return 0; // Nothing to update
        }

        // Build SET clauses
        var setClauses = new List<string>();
        foreach (var field in allFields)
        {
            setClauses.Add($"{SnowflakeRecordMapper.QuoteIdentifier(field)} = v.{SnowflakeRecordMapper.QuoteIdentifier(field)}");
        }

        // Build VALUES rows and collect parameters
        var valuesList = new List<string>();
        var parameters = new Dictionary<string, object?>();

        for (int i = 0; i < batch.Count; i++)
        {
            var (key, record) = batch[i];
            var values = new List<string> { $":k{i}" };
            parameters[$"k{i}"] = key;

            int fieldIndex = 0;
            foreach (var field in allFields)
            {
                var paramName = $"v{i}_{fieldIndex}";
                values.Add($":{paramName}");
                parameters[paramName] = record.Attributes.TryGetValue(field, out var val) ? val : null;
                fieldIndex++;
            }

            valuesList.Add($"({string.Join(", ", values)})");
        }

        // Build complete SQL statement using UPDATE FROM VALUES
        var columnList = string.Join(", ", allFields.Select(SnowflakeRecordMapper.QuoteIdentifier).Prepend(SnowflakeRecordMapper.QuoteIdentifier(keyColumn)));
        var sql = $@"
UPDATE {table} AS t
SET {string.Join(", ", setClauses)}
FROM (VALUES {string.Join(", ", valuesList)}) AS v({columnList})
WHERE t.{SnowflakeRecordMapper.QuoteIdentifier(keyColumn)} = v.{SnowflakeRecordMapper.QuoteIdentifier(keyColumn)}";

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;

        // Use consolidated SqlParameterHelper for parameter handling
        SqlParameterHelper.AddParameters(command, parameters);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<int> ExecuteBatchDeleteAsync(
        SnowflakeDbConnection connection,
        LayerDefinition layer,
        List<string> batch,
        CancellationToken cancellationToken)
    {
        var idList = string.Join(", ", batch.Select((_, i) => $":id{i}"));

        // Use QuoteIdentifier for table name and field name
        var quotedTableName = SnowflakeRecordMapper.ResolveTableName(layer);
        var quotedIdField = SnowflakeRecordMapper.QuoteIdentifier(SnowflakeRecordMapper.ResolvePrimaryKey(layer));
        var sql = $"DELETE FROM {quotedTableName} WHERE {quotedIdField} IN ({idList})";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        // Build parameters for batch delete
        var parameters = new Dictionary<string, object?>();
        for (var i = 0; i < batch.Count; i++)
        {
            parameters[$"id{i}"] = batch[i];
        }

        // Use consolidated SqlParameterHelper for parameter handling
        SqlParameterHelper.AddParameters(command, parameters);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
