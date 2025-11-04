// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Linq;
using Oracle.ManagedDataAccess.Client;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Security;
using Polly;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Enterprise.Data.Oracle;

/// <summary>
/// Oracle Spatial data provider using Oracle.ManagedDataAccess.Core with SDO_GEOMETRY.
/// Inherits from RelationalDataStoreProviderBase for standardized connection and transaction management.
/// </summary>
public sealed class OracleDataStoreProvider : RelationalDataStoreProviderBase<OracleConnection, OracleTransaction, OracleCommand>
{
    public const string ProviderKey = "oracle";

    public OracleDataStoreProvider()
        : this(null)
    {
    }

    public OracleDataStoreProvider(IConnectionStringEncryptionService? encryptionService)
        : base(ProviderKey, DatabaseRetryPolicy.CreateOracleRetryPipeline(), encryptionService)
    {
    }

    public override string Provider => ProviderKey;

    public override IDataStoreCapabilities Capabilities => OracleDataStoreCapabilities.Instance;

    protected override OracleConnection CreateConnectionCore(string connectionString)
    {
        return new OracleConnection(connectionString);
    }

    protected override string NormalizeConnectionString(string connectionString)
    {
        // SECURITY: Validate connection string to prevent SQL injection
        ConnectionStringValidator.Validate(connectionString, nameof(connectionString));

        var builder = new OracleConnectionStringBuilder(connectionString);

        // Configure connection pooling (Oracle uses different parameters than other DBs)
        if (!builder.ContainsKey("Pooling"))
            builder.Pooling = true;

        if (!builder.ContainsKey("Min Pool Size"))
            builder.MinPoolSize = 2;

        if (!builder.ContainsKey("Max Pool Size"))
            builder.MaxPoolSize = 50;

        if (!builder.ContainsKey("Connection Lifetime"))
            builder.ConnectionLifeTime = 600; // 10 minutes

        if (!builder.ContainsKey("Connection Timeout"))
            builder.ConnectionTimeout = 15;

        // Set application name for monitoring
        if (!builder.ContainsKey("Application Name"))
            builder["Application Name"] = "HonuaIO";

        return builder.ConnectionString;
    }

    protected override string GetConnectivityTestQuery() => "SELECT 1 FROM DUAL";

    public override async IAsyncEnumerable<FeatureRecord> QueryAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);

        await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await RetryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var builder = new OracleFeatureQueryBuilder(layer);
        var (sql, parameters) = builder.BuildSelect(query ?? new FeatureQuery());

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        SqlParameterHelper.AddParameters(command, parameters);

        await using var reader = await RetryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteReaderAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return CreateFeatureRecord(reader, layer);
        }
    }

    public override async Task<long> CountAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? query,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);

        await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await RetryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var builder = new OracleFeatureQueryBuilder(layer);
        var (sql, parameters) = builder.BuildCount(query ?? new FeatureQuery());

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        SqlParameterHelper.AddParameters(command, parameters);

        var result = await RetryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteScalarAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        return result is null or DBNull ? 0L : Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    public override async Task<FeatureRecord?> GetAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        FeatureQuery? query,
        CancellationToken cancellationToken = default)
    {

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNullOrWhiteSpace(featureId);

        await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await RetryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var builder = new OracleFeatureQueryBuilder(layer);
        var (sql, parameters) = builder.BuildById(featureId);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        SqlParameterHelper.AddParameters(command, parameters);

        await using var reader = await RetryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteReaderAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return CreateFeatureRecord(reader, layer);
        }

        return null;
    }

    public override async Task<FeatureRecord> CreateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureRecord record,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(record);

        var (connection, oracleTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource, transaction, cancellationToken).ConfigureAwait(false);

        try
        {
            var builder = new OracleFeatureQueryBuilder(layer);
            var (sql, parameters) = builder.BuildInsert(record);

            await using var command = connection.CreateCommand();
            command.Transaction = oracleTransaction;
            command.CommandText = sql;
            SqlParameterHelper.AddParameters(command, parameters);

            await RetryPipeline.ExecuteAsync(async ct =>
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            // Fetch the inserted record
            var idValue = record.Attributes.TryGetValue(layer.IdField, out var id) ? id : null;
            if (idValue != null)
            {
                var inserted = await GetAsync(dataSource, service, layer, idValue.ToString()!, null, cancellationToken).ConfigureAwait(false);
                return inserted ?? record;
            }

            return record;
        }
        finally
        {
            if (shouldDisposeConnection)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public override async Task<FeatureRecord?> UpdateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        FeatureRecord record,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNullOrWhiteSpace(featureId);
        Guard.NotNull(record);

        var (connection, oracleTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource, transaction, cancellationToken).ConfigureAwait(false);

        try
        {
            var builder = new OracleFeatureQueryBuilder(layer);
            var (sql, parameters) = builder.BuildUpdate(featureId, record);

            await using var command = connection.CreateCommand();
            command.Transaction = oracleTransaction;
            command.CommandText = sql;
            SqlParameterHelper.AddParameters(command, parameters);

            var rowsAffected = await RetryPipeline.ExecuteAsync(async ct =>
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            if (rowsAffected == 0)
            {
                return null;
            }

            return await GetAsync(dataSource, service, layer, featureId, null, cancellationToken);
        }
        finally
        {
            if (shouldDisposeConnection)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public override async Task<bool> DeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNullOrWhiteSpace(featureId);

        var (connection, oracleTransaction, shouldDisposeConnection) = await GetConnectionAndTransactionAsync(
            dataSource, transaction, cancellationToken).ConfigureAwait(false);

        try
        {
            var builder = new OracleFeatureQueryBuilder(layer);
            var (sql, parameters) = builder.BuildDelete(featureId);

            await using var command = connection.CreateCommand();
            command.Transaction = oracleTransaction;
            command.CommandText = sql;
            SqlParameterHelper.AddParameters(command, parameters);

            var rowsAffected = await RetryPipeline.ExecuteAsync(async ct =>
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            return rowsAffected > 0;
        }
        finally
        {
            if (shouldDisposeConnection)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Inserts multiple features using Oracle array binding for optimal performance.
    /// Processes records in batches of 1000 using array binding, which can be 10-50x faster than row-by-row inserts.
    /// </summary>
    public override async Task<int> BulkInsertAsync(
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

        const int batchSize = 1000; // Oracle handles 1000+ rows efficiently with array binding
        var batch = new List<FeatureRecord>(batchSize);
        var totalCount = 0;

        await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await RetryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        await using var transaction = (OracleTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await foreach (var record in records.WithCancellation(cancellationToken))
            {
                batch.Add(record);

                if (batch.Count >= batchSize)
                {
                    totalCount += await ExecuteArrayInsertAsync(connection, transaction, layer, batch, cancellationToken).ConfigureAwait(false);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                totalCount += await ExecuteArrayInsertAsync(connection, transaction, layer, batch, cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// Updates multiple features using Oracle array binding for optimal performance.
    /// Processes records in batches of 1000 using array binding for significant performance gains.
    /// </summary>
    public override async Task<int> BulkUpdateAsync(
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

        const int batchSize = 1000; // Oracle handles 1000+ rows efficiently with array binding
        var batch = new List<KeyValuePair<string, FeatureRecord>>(batchSize);
        var totalCount = 0;

        await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await RetryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        await using var transaction = (OracleTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await foreach (var kvp in records.WithCancellation(cancellationToken))
            {
                batch.Add(kvp);

                if (batch.Count >= batchSize)
                {
                    totalCount += await ExecuteArrayUpdateAsync(connection, transaction, layer, batch, cancellationToken).ConfigureAwait(false);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                totalCount += await ExecuteArrayUpdateAsync(connection, transaction, layer, batch, cancellationToken).ConfigureAwait(false);
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

    public override async Task<int> BulkDeleteAsync(
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
        const int batchSize = 1000;
        var batch = new List<string>(batchSize);

        await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await RetryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        await foreach (var id in featureIds.WithCancellation(cancellationToken))
        {
            batch.Add(id);

            if (batch.Count >= batchSize)
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

    public override Task<byte[]?> GenerateMvtTileAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        int zoom,
        int x,
        int y,
        string? datetime = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<byte[]?>(null);
    }

    /// <summary>
    /// Maps a .NET object value to the appropriate OracleDbType based on its runtime type.
    /// This ensures correct parameter type mapping for optimal query performance and data integrity.
    /// </summary>
    /// <param name="value">The value to map (can be null or DBNull)</param>
    /// <returns>The corresponding OracleDbType for the value's type</returns>
    private static OracleDbType MapToOracleDbType(object? value)
    {
        if (value is null or DBNull)
            return OracleDbType.Varchar2;

        return value switch
        {
            int => OracleDbType.Int32,
            long => OracleDbType.Int64,
            short => OracleDbType.Int16,
            decimal => OracleDbType.Decimal,
            double => OracleDbType.Double,
            float => OracleDbType.Single,
            DateTime => OracleDbType.Date,
            DateTimeOffset => OracleDbType.TimeStampTZ,
            bool => OracleDbType.Byte,
            byte => OracleDbType.Byte,
            byte[] => OracleDbType.Blob,
            _ => OracleDbType.Varchar2
        };
    }

    /// <summary>
    /// Executes array-based insert using Oracle array binding for maximum performance.
    /// Array binding allows sending multiple rows in a single database round trip.
    /// </summary>
    private async Task<int> ExecuteArrayInsertAsync(
        OracleConnection connection,
        OracleTransaction transaction,
        LayerDefinition layer,
        List<FeatureRecord> batch,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
            return 0;

        // Build column list from first record
        var columns = batch[0].Attributes.Keys
            .Where(k => !string.Equals(k, "GEOJSON", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var builder = new OracleFeatureQueryBuilder(layer);
        var table = builder.GetTableExpression();
        var columnList = string.Join(", ", columns.Select(QuoteIdentifier));
        var paramList = string.Join(", ", columns.Select((_, i) => $":p{i}"));

        var sql = $"INSERT INTO {table} ({columnList}) VALUES ({paramList})";

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ArrayBindCount = batch.Count; // Enable array binding

        // Create parameters as arrays
        for (int colIdx = 0; colIdx < columns.Count; colIdx++)
        {
            var column = columns[colIdx];
            var values = batch.Select(r => r.Attributes.TryGetValue(column, out var v) ? v : DBNull.Value).ToArray();

            var param = new OracleParameter($"p{colIdx}", MapToOracleDbType(values.FirstOrDefault()))
            {
                Value = values
            };
            command.Parameters.Add(param);
        }

        var rowsAffected = await RetryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        return rowsAffected;
    }

    /// <summary>
    /// Executes array-based update using Oracle array binding for maximum performance.
    /// Array binding allows sending multiple rows in a single database round trip.
    /// </summary>
    private async Task<int> ExecuteArrayUpdateAsync(
        OracleConnection connection,
        OracleTransaction transaction,
        LayerDefinition layer,
        List<KeyValuePair<string, FeatureRecord>> batch,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
            return 0;

        var builder = new OracleFeatureQueryBuilder(layer);
        var table = builder.GetTableExpression();
        var primaryKey = layer.IdField;

        // Build column list from first record (excluding primary key and GEOJSON)
        var columns = batch[0].Value.Attributes.Keys
            .Where(k => !string.Equals(k, "GEOJSON", StringComparison.OrdinalIgnoreCase))
            .Where(k => !string.Equals(k, primaryKey, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (columns.Count == 0)
            return 0; // Nothing to update

        var setClauses = string.Join(", ", columns.Select((c, i) => $"{QuoteIdentifier(c)} = :p{i}"));
        var sql = $"UPDATE {table} SET {setClauses} WHERE {QuoteIdentifier(primaryKey)} = :pid";

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ArrayBindCount = batch.Count; // Enable array binding

        // Create parameters as arrays for SET clause
        for (int colIdx = 0; colIdx < columns.Count; colIdx++)
        {
            var column = columns[colIdx];
            var values = batch.Select(kvp => kvp.Value.Attributes.TryGetValue(column, out var v) ? v : DBNull.Value).ToArray();

            var param = new OracleParameter($"p{colIdx}", MapToOracleDbType(values.FirstOrDefault()))
            {
                Value = values
            };
            command.Parameters.Add(param);
        }

        // Create parameter array for primary key (WHERE clause)
        var idValues = batch.Select(kvp => (object)kvp.Key).ToArray();
        var idParam = new OracleParameter("pid", MapToOracleDbType(idValues.FirstOrDefault()))
        {
            Value = idValues
        };
        command.Parameters.Add(idParam);

        var rowsAffected = await RetryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        return rowsAffected;
    }

    /// <summary>
    /// Executes a batch delete using parameterized IN clause for safe and efficient deletion.
    /// </summary>
    private async Task<int> ExecuteBatchDeleteAsync(
        OracleConnection connection,
        LayerDefinition layer,
        List<string> batch,
        CancellationToken cancellationToken)
    {
        var builder = new OracleFeatureQueryBuilder(layer);
        var idList = string.Join(", ", batch.Select((_, i) => $":id{i}"));
        var tableName = layer.Storage?.Table ?? layer.Id;
        var sql = $"DELETE FROM {QuoteIdentifier(tableName)} WHERE {QuoteIdentifier(layer.IdField)} IN ({idList})";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        for (var i = 0; i < batch.Count; i++)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = $":id{i}";
            parameter.Value = batch[i];
            command.Parameters.Add(parameter);
        }

        return await RetryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }

    private FeatureRecord CreateFeatureRecord(IDataReader reader, LayerDefinition layer)
    {
        // Use FeatureRecordReader with custom geometry extraction for Oracle SDO_UTIL.TO_GEOJSON
        var skipColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "GEOJSON" };

        return FeatureRecordReader.ReadFeatureRecordWithCustomGeometry(
            reader,
            layer,
            layer.GeometryField,
            skipColumns,
            () =>
            {
                // Extract geometry from GEOJSON column if present
                var geoJsonOrdinal = FeatureRecordReader.TryGetGeometryOrdinal(reader, "GEOJSON");
                if (geoJsonOrdinal >= 0 && !reader.IsDBNull(geoJsonOrdinal))
                {
                    var geoJson = reader.GetString(geoJsonOrdinal);
                    return FeatureRecordNormalizer.ParseGeometry(geoJson);
                }
                return null;
            });
    }


    /// <summary>
    /// Quotes an Oracle identifier using double quotes, with SQL injection protection.
    /// Validates the identifier for length, valid characters, and potential injection attacks before quoting.
    /// Handles qualified names (e.g., schema.table) by quoting each part individually.
    /// </summary>
    /// <param name="identifier">The identifier to validate and quote (table, column, or schema name)</param>
    /// <returns>The safely quoted identifier for use in Oracle SQL</returns>
    /// <exception cref="ArgumentException">Thrown if the identifier is invalid or potentially malicious</exception>
    private static string QuoteIdentifier(string identifier)
    {
        Honua.Server.Core.Security.SqlIdentifierValidator.ValidateIdentifier(identifier, nameof(identifier));

        // Split on dots to handle qualified names (schema.table, database.schema.table)
        var parts = identifier.Split('.', StringSplitOptions.RemoveEmptyEntries);

        // Quote each part individually
        for (var i = 0; i < parts.Length; i++)
        {
            var unquoted = parts[i].Trim('"'); // Remove existing quotes if any
            parts[i] = $"\"{unquoted.Replace("\"", "\"\"")}\"";
        }

        return string.Join('.', parts);
    }


    public override async Task<IReadOnlyList<StatisticsResult>> QueryStatisticsAsync(
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

        Task<IReadOnlyList<StatisticsResult>> ExecuteFallbackAsync() =>
            FallbackAggregationHelper.ComputeStatisticsAsync(
                query => QueryAsync(dataSource, service, layer, query, cancellationToken),
                filter,
                statistics,
                groupByFields,
                cancellationToken);

        if (!Capabilities.SupportsServerSideGeometryOperations)
        {
            return await ExecuteFallbackAsync().ConfigureAwait(false);
        }

        try
        {
            await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
            await RetryPipeline.ExecuteAsync(async ct =>
                await connection.OpenAsync(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            var builder = new OracleFeatureQueryBuilder(layer);
            var query = NormalizeAnalyticsQuery(filter);
            var (sql, parameters) = builder.BuildStatistics(query, statistics, groupByFields);

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            SqlParameterHelper.AddParameters(command, parameters);

            await using var reader = await RetryPipeline.ExecuteAsync(async ct =>
                await command.ExecuteReaderAsync(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            var groupFieldSet = groupByFields is { Count: > 0 }
                ? new HashSet<string>(groupByFields, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var statisticAliases = new HashSet<string>(
                statistics.Select(s => s.OutputName ?? $"{s.Type}_{s.FieldName}"),
                StringComparer.OrdinalIgnoreCase);

            var results = new List<StatisticsResult>();

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var groupValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                var statisticValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

                for (var index = 0; index < reader.FieldCount; index++)
                {
                    var columnName = reader.GetName(index);
                    var value = reader.IsDBNull(index) ? null : reader.GetValue(index);

                    if (groupFieldSet.Contains(columnName))
                    {
                        groupValues[columnName] = value;
                    }
                    else if (statisticAliases.Contains(columnName))
                    {
                        statisticValues[columnName] = value;
                    }
                    else if (groupFieldSet.Count > 0)
                    {
                        groupValues[columnName] = value;
                    }
                    else
                    {
                        statisticValues[columnName] = value;
                    }
                }

                results.Add(new StatisticsResult(
                    new ReadOnlyDictionary<string, object?>(groupValues),
                    new ReadOnlyDictionary<string, object?>(statisticValues)));
            }

            return results;
        }
        catch (Exception ex) when (IsSpatialCapabilityError(ex))
        {
            return await ExecuteFallbackAsync().ConfigureAwait(false);
        }
    }

    public override async Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(
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

        Task<IReadOnlyList<DistinctResult>> ExecuteFallbackAsync() =>
            FallbackAggregationHelper.ComputeDistinctAsync(
                query => QueryAsync(dataSource, service, layer, query, cancellationToken),
                filter,
                fieldNames,
                cancellationToken);

        if (!Capabilities.SupportsServerSideGeometryOperations)
        {
            return await ExecuteFallbackAsync().ConfigureAwait(false);
        }

        try
        {
            await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
            await RetryPipeline.ExecuteAsync(async ct =>
                await connection.OpenAsync(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            var builder = new OracleFeatureQueryBuilder(layer);
            var query = NormalizeAnalyticsQuery(filter);
            var (sql, parameters) = builder.BuildDistinct(query, fieldNames);

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            SqlParameterHelper.AddParameters(command, parameters);

            await using var reader = await RetryPipeline.ExecuteAsync(async ct =>
                await command.ExecuteReaderAsync(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            var results = new List<DistinctResult>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

                for (var index = 0; index < reader.FieldCount; index++)
                {
                    var columnName = reader.GetName(index);
                    values[columnName] = reader.IsDBNull(index) ? null : reader.GetValue(index);
                }

                results.Add(new DistinctResult(new ReadOnlyDictionary<string, object?>(values)));
            }

            return results;
        }
        catch (Exception ex) when (IsSpatialCapabilityError(ex))
        {
            return await ExecuteFallbackAsync().ConfigureAwait(false);
        }
    }

    public override async Task<BoundingBox?> QueryExtentAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(service);

        Task<BoundingBox?> ExecuteFallbackAsync() =>
            FallbackAggregationHelper.ComputeExtentAsync(
                query => QueryAsync(dataSource, service, layer, query, cancellationToken),
                filter,
                service,
                layer,
                cancellationToken);

        if (!Capabilities.SupportsServerSideGeometryOperations)
        {
            return await ExecuteFallbackAsync().ConfigureAwait(false);
        }

        try
        {
            await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
            await RetryPipeline.ExecuteAsync(async ct =>
                await connection.OpenAsync(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            var builder = new OracleFeatureQueryBuilder(layer);
            var query = NormalizeAnalyticsQuery(filter);
            var (sql, parameters) = builder.BuildExtent(query);

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            SqlParameterHelper.AddParameters(command, parameters);

            await using var reader = await RetryPipeline.ExecuteAsync(async ct =>
                await command.ExecuteReaderAsync(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var minX = FeatureRecordReader.GetFieldValue<double?>(reader, 0);
            var minY = FeatureRecordReader.GetFieldValue<double?>(reader, 1);
            var maxX = FeatureRecordReader.GetFieldValue<double?>(reader, 2);
            var maxY = FeatureRecordReader.GetFieldValue<double?>(reader, 3);

            if (!minX.HasValue || !minY.HasValue || !maxX.HasValue || !maxY.HasValue)
            {
                return null;
            }

            var targetCrs = query.Crs ?? service.Ogc.DefaultCrs ?? "EPSG:4326";
            return new BoundingBox(minX.Value, minY.Value, maxX.Value, maxY.Value, Crs: targetCrs);
        }
        catch (Exception ex) when (IsSpatialCapabilityError(ex))
        {
            return await ExecuteFallbackAsync().ConfigureAwait(false);
        }
    }

    private static bool IsSpatialCapabilityError(Exception exception)
    {
        if (exception is null)
        {
            return false;
        }

        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (ContainsSpatialCapabilityIndicator(current.Message))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsSpatialCapabilityIndicator(string? message)
    {
        if (message.IsNullOrWhiteSpace())
        {
            return false;
        }

        if (message.Contains("ORA-13249", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!message.Contains("SDO", StringComparison.OrdinalIgnoreCase) &&
            !message.Contains("MDSYS", StringComparison.OrdinalIgnoreCase) &&
            !message.Contains("SPATIAL", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var triggers = new[]
        {
            "does not exist",
            "not installed",
            "not enabled",
            "not supported",
            "insufficient privileges",
            "permission denied",
            "feature is disabled",
            "feature not enabled",
            "cannot execute",
            "invalid identifier"
        };

        return triggers.Any(trigger => message.Contains(trigger, StringComparison.OrdinalIgnoreCase));
    }


    private static FeatureQuery NormalizeAnalyticsQuery(FeatureQuery? query)
    {
        var baseQuery = query ?? new FeatureQuery();
        return baseQuery with
        {
            Limit = null,
            Offset = null,
            PropertyNames = null,
            SortOrders = null,
            ResultType = FeatureResultType.Results
        };
    }


    public override Task<bool> SoftDeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        string? deletedBy,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            $"Soft delete is not supported by the {nameof(OracleDataStoreProvider)}. " +
            "Check IDataStoreCapabilities.SupportsSoftDelete before calling this method.");
    }

    public override Task<bool> RestoreAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            $"Restore is not supported by the {nameof(OracleDataStoreProvider)}. " +
            "Check IDataStoreCapabilities.SupportsSoftDelete before calling this method.");
    }

    public override Task<bool> HardDeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        string? deletedBy,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            $"Hard delete is not supported by the {nameof(OracleDataStoreProvider)}. " +
            "Use the standard DeleteAsync method for permanent deletion.");
    }

}
