// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Security;
using Honua.Server.Core.Utilities;
using MySqlConnector;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Data.MySql;

public sealed class MySqlDataStoreProvider : RelationalDataStoreProviderBase<MySqlConnection, MySqlTransaction, MySqlCommand, MySqlDataReader>
{
    private const int BulkBatchSize = 1000;

    private readonly ConcurrentDictionary<string, Lazy<MySqlDataSource>> _dataSources = new(StringComparer.Ordinal);

    public const string ProviderKey = "mysql";

    public MySqlDataStoreProvider(IConnectionStringEncryptionService? encryptionService = null)
        : base(ProviderKey, DatabaseRetryPolicy.CreateMySqlRetryPipeline(), encryptionService)
    {
    }

    public override string Provider => ProviderKey;

    public override IDataStoreCapabilities Capabilities => MySqlDataStoreCapabilities.Instance;

    // ========================================
    // ABSTRACT METHOD IMPLEMENTATIONS
    // ========================================

    protected override MySqlConnection CreateConnectionCore(string connectionString)
    {
        var dataSource = new MySqlDataSource(connectionString);
        return dataSource.CreateConnection();
    }

    protected override string NormalizeConnectionString(string connectionString)
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

    protected override string GetProviderName() => "mysql";

    protected override QueryDefinition BuildSelectQuery(
        ServiceDefinition service,
        LayerDefinition layer,
        int storageSrid,
        int targetSrid,
        FeatureQuery query)
    {
        var builder = new MySqlFeatureQueryBuilder(service, layer, storageSrid, targetSrid);
        var definition = builder.BuildSelect(query);
        return new QueryDefinition(definition.Sql, definition.Parameters);
    }

    protected override QueryDefinition BuildCountQuery(
        ServiceDefinition service,
        LayerDefinition layer,
        int storageSrid,
        int targetSrid,
        FeatureQuery query)
    {
        var builder = new MySqlFeatureQueryBuilder(service, layer, storageSrid, targetSrid);
        var definition = builder.BuildCount(query);
        return new QueryDefinition(definition.Sql, definition.Parameters);
    }

    protected override QueryDefinition BuildByIdQuery(
        ServiceDefinition service,
        LayerDefinition layer,
        int storageSrid,
        int targetSrid,
        string featureId)
    {
        var builder = new MySqlFeatureQueryBuilder(service, layer, storageSrid, targetSrid);
        var definition = builder.BuildById(featureId);
        return new QueryDefinition(definition.Sql, definition.Parameters);
    }

    protected override QueryDefinition BuildInsertQuery(LayerDefinition layer, FeatureRecord record)
    {
        var table = LayerMetadataHelper.GetTableExpression(layer, QuoteIdentifier);
        var normalized = NormalizeRecord(layer, record.Attributes, includeKey: true);

        if (normalized.Columns.Count == 0)
        {
            throw new InvalidOperationException($"Create operation for layer '{layer.Id}' did not include any columns.");
        }

        var columnList = string.Join(", ", normalized.Columns.Select(c => c.ColumnName));
        var valueList = string.Join(", ", normalized.Columns.Select(c => BuildValueExpression(c, normalized.Srid)));
        var sql = $"INSERT INTO {table} ({columnList}) VALUES ({valueList})";

        return new QueryDefinition(sql, normalized.Parameters);
    }

    protected override QueryDefinition BuildUpdateQuery(LayerDefinition layer, string featureId, FeatureRecord record)
    {
        var table = LayerMetadataHelper.GetTableExpression(layer, QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);
        var normalized = NormalizeRecord(layer, record.Attributes, includeKey: false);

        if (normalized.Columns.Count == 0)
        {
            // Return empty update that won't affect any rows
            return new QueryDefinition($"SELECT 0", new Dictionary<string, object?>());
        }

        var assignments = string.Join(", ", normalized.Columns.Select(c => $"{c.ColumnName} = {BuildValueExpression(c, normalized.Srid)}"));
        var parameters = new Dictionary<string, object?>(normalized.Parameters, StringComparer.Ordinal)
        {
            ["@key"] = NormalizeKeyParameter(layer, featureId)
        };

        var sql = $"UPDATE {table} SET {assignments} WHERE {QuoteIdentifier(keyColumn)} = @key";
        return new QueryDefinition(sql, new ReadOnlyDictionary<string, object?>(parameters));
    }

    protected override QueryDefinition BuildDeleteQuery(LayerDefinition layer, string featureId)
    {
        var table = LayerMetadataHelper.GetTableExpression(layer, QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);
        var sql = $"DELETE FROM {table} WHERE {QuoteIdentifier(keyColumn)} = @key";
        var parameters = new Dictionary<string, object?> { ["@key"] = NormalizeKeyParameter(layer, featureId) };
        return new QueryDefinition(sql, parameters);
    }

    protected override QueryDefinition BuildSoftDeleteQuery(LayerDefinition layer, string featureId, string? deletedBy)
    {
        var table = LayerMetadataHelper.GetTableExpression(layer, QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);
        var sql = $@"
            UPDATE {table}
            SET is_deleted = true,
                deleted_at = NOW(),
                deleted_by = @deletedBy
            WHERE {QuoteIdentifier(keyColumn)} = @key
              AND (is_deleted IS NULL OR is_deleted = false)";

        var parameters = new Dictionary<string, object?>
        {
            ["@key"] = NormalizeKeyParameter(layer, featureId),
            ["@deletedBy"] = (object?)deletedBy ?? DBNull.Value
        };

        return new QueryDefinition(sql, parameters);
    }

    protected override QueryDefinition BuildRestoreQuery(LayerDefinition layer, string featureId)
    {
        var table = LayerMetadataHelper.GetTableExpression(layer, QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);
        var sql = $@"
            UPDATE {table}
            SET is_deleted = false,
                deleted_at = NULL,
                deleted_by = NULL
            WHERE {QuoteIdentifier(keyColumn)} = @key
              AND is_deleted = true";

        var parameters = new Dictionary<string, object?> { ["@key"] = NormalizeKeyParameter(layer, featureId) };
        return new QueryDefinition(sql, parameters);
    }

    protected override QueryDefinition BuildHardDeleteQuery(LayerDefinition layer, string featureId)
    {
        var table = LayerMetadataHelper.GetTableExpression(layer, QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);
        var sql = $"DELETE FROM {table} WHERE {QuoteIdentifier(keyColumn)} = @key";
        var parameters = new Dictionary<string, object?> { ["@key"] = NormalizeKeyParameter(layer, featureId) };
        return new QueryDefinition(sql, parameters);
    }

    protected override FeatureRecord CreateFeatureRecord(
        MySqlDataReader reader,
        LayerDefinition layer,
        int storageSrid,
        int targetSrid)
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

    protected override async Task<string> ExecuteInsertAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        LayerDefinition layer,
        FeatureRecord record,
        CancellationToken cancellationToken)
    {
        var table = LayerMetadataHelper.GetTableExpression(layer, QuoteIdentifier);
        var keyColumn = LayerMetadataHelper.GetPrimaryKeyColumn(layer);
        var normalized = NormalizeRecord(layer, record.Attributes, includeKey: true);

        if (normalized.Columns.Count == 0)
        {
            throw new InvalidOperationException($"Create operation for layer '{layer.Id}' did not include any columns.");
        }

        var columnList = string.Join(", ", normalized.Columns.Select(c => c.ColumnName));
        var valueList = string.Join(", ", normalized.Columns.Select(c => BuildValueExpression(c, normalized.Srid)));

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"INSERT INTO {table} ({columnList}) VALUES ({valueList})";
        AddParameters(command, normalized.Parameters);
        command.CommandTimeout = DefaultCommandTimeoutSeconds;

        await RetryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        // Get inserted key
        var keyValue = TryResolveKey(record.Attributes, keyColumn);
        if (keyValue.IsNullOrWhiteSpace())
        {
            await using var identityCommand = connection.CreateCommand();
            identityCommand.Transaction = transaction;
            identityCommand.CommandText = "SELECT LAST_INSERT_ID()";
            identityCommand.CommandTimeout = DefaultCommandTimeoutSeconds;
            var identity = await RetryPipeline.ExecuteAsync(async ct =>
                await identityCommand.ExecuteScalarAsync(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
            keyValue = Convert.ToString(identity, CultureInfo.InvariantCulture);
        }

        return keyValue ?? throw new InvalidOperationException("Failed to obtain key for inserted feature.");
    }

    protected override void AddParameters(DbCommand command, IReadOnlyDictionary<string, object?> parameters)
    {
        SqlParameterHelper.AddParameters(command, parameters);
    }

    protected override IsolationLevel GetDefaultIsolationLevel() => IsolationLevel.RepeatableRead;

    // ========================================
    // BULK OPERATIONS (Provider-specific)
    // ========================================

    public override async Task<int> BulkInsertAsync(
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
        await RetryPipeline.ExecuteAsync(async ct =>
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

    public override async Task<int> BulkUpdateAsync(
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
        await RetryPipeline.ExecuteAsync(async ct =>
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

    public override async Task<int> BulkDeleteAsync(
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
        await RetryPipeline.ExecuteAsync(async ct =>
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

        await RetryPipeline.ExecuteAsync(async ct =>
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

                var rowsAffected = await RetryPipeline.ExecuteAsync(async ct =>
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

        var affected = await RetryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        return affected;
    }

    // ========================================
    // AGGREGATION OPERATIONS (Provider-specific)
    // ========================================

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

        var normalizedQuery = filter ?? new FeatureQuery();
        var storageSrid = layer.Storage?.Srid ?? CrsHelper.Wgs84;
        var targetSrid = CrsHelper.ParseCrs(normalizedQuery.Crs ?? service.Ogc.DefaultCrs);

        await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await RetryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var builder = new MySqlFeatureQueryBuilder(service, layer, storageSrid, targetSrid);
        var definition = builder.BuildStatistics(normalizedQuery, statistics, groupByFields);

        await using var command = CreateCommand(connection, new QueryDefinition(definition.Sql, definition.Parameters));
        await using var reader = await RetryPipeline.ExecuteAsync(async ct =>
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

        var normalizedQuery = filter ?? new FeatureQuery();
        var storageSrid = layer.Storage?.Srid ?? CrsHelper.Wgs84;
        var targetSrid = CrsHelper.ParseCrs(normalizedQuery.Crs ?? service.Ogc.DefaultCrs);

        await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await RetryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var builder = new MySqlFeatureQueryBuilder(service, layer, storageSrid, targetSrid);
        var definition = builder.BuildDistinct(normalizedQuery, fieldNames);

        await using var command = CreateCommand(connection, new QueryDefinition(definition.Sql, definition.Parameters));
        await using var reader = await RetryPipeline.ExecuteAsync(async ct =>
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

        var normalizedQuery = filter ?? new FeatureQuery();
        var storageSrid = layer.Storage?.Srid ?? CrsHelper.Wgs84;
        var targetSrid = CrsHelper.ParseCrs(normalizedQuery.Crs ?? service.Ogc.DefaultCrs);

        await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await RetryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var builder = new MySqlFeatureQueryBuilder(service, layer, storageSrid, targetSrid);
        var definition = builder.BuildExtent(normalizedQuery, targetSrid);

        await using var command = CreateCommand(connection, new QueryDefinition(definition.Sql, definition.Parameters));
        await using var reader = await RetryPipeline.ExecuteAsync(async ct =>
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
        // MySQL does not support native MVT generation
        return Task.FromResult<byte[]?>(null);
    }

    // ========================================
    // HELPER METHODS
    // ========================================

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

    private static string QuoteIdentifier(string identifier) => MySqlFeatureQueryBuilder.QuoteIdentifier(identifier);

    // ========================================
    // DISPOSAL (MySqlDataSource cleanup only)
    // ========================================

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var entry in _dataSources.Values)
            {
                if (entry.IsValueCreated)
                {
                    entry.Value.Dispose();
                }
            }

            _dataSources.Clear();
        }

        base.Dispose(disposing);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        foreach (var entry in _dataSources.Values)
        {
            if (entry.IsValueCreated)
            {
                await entry.Value.DisposeAsync().ConfigureAwait(false);
            }
        }

        _dataSources.Clear();

        await base.DisposeAsyncCore().ConfigureAwait(false);
    }

    // ========================================
    // INTERNAL RECORDS
    // ========================================

    private sealed record NormalizedColumn(string ColumnName, string ParameterName, object? Value, bool IsGeometry)
    {
        public bool IsNull => Value is null || Value is DBNull;
    }

    private sealed record NormalizedRecord(IReadOnlyList<NormalizedColumn> Columns, IReadOnlyDictionary<string, object?> Parameters, int Srid);
}
