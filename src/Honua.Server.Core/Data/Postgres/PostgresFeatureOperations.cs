// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Exceptions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Microsoft.Extensions.Logging;
using Npgsql;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data.Postgres;

/// <summary>
/// Handles single-record CRUD operations for PostgreSQL features.
/// </summary>
internal sealed class PostgresFeatureOperations
{
    private readonly PostgresConnectionManager _connectionManager;
    private readonly QueryBuilderPool _queryBuilderPool;
    private readonly DataAccessOptions _options;
    private readonly ILogger<PostgresFeatureOperations>? _logger;

    public PostgresFeatureOperations(
        PostgresConnectionManager connectionManager,
        QueryBuilderPool queryBuilderPool,
        DataAccessOptions options,
        ILogger<PostgresFeatureOperations>? logger = null)
    {
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _queryBuilderPool = queryBuilderPool ?? throw new ArgumentNullException(nameof(queryBuilderPool));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <summary>
    /// Helper method that extracts connection and transaction from IDataStoreTransaction or creates a new connection.
    /// Eliminates duplicated boilerplate code across all CRUD operations.
    /// </summary>
    private async Task<(NpgsqlConnection Connection, NpgsqlTransaction? Transaction, bool ShouldDispose)> GetConnectionAndTransactionAsync(
        DataSourceDefinition dataSource,
        IDataStoreTransaction? transaction,
        CancellationToken cancellationToken = default)
    {
        if (transaction is RelationalDataStoreTransaction<NpgsqlConnection, NpgsqlTransaction> pgTransaction)
        {
            // Transaction owns the connection - caller should NOT dispose it
            return (pgTransaction.Connection, pgTransaction.Transaction, false);
        }

        // No transaction provided - create new connection, caller MUST dispose it
        var connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        return (connection, null, true);
    }

    public async IAsyncEnumerable<FeatureRecord> QueryAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
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

        await using var connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var builder = _queryBuilderPool.Get(service, layer, storageSrid, targetSrid);
        PostgresQueryDefinition definition;
        try
        {
            definition = builder.BuildSelect(normalizedQuery);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to build select query for layer {LayerId} in service {ServiceId}. Storage SRID: {StorageSrid}, Target SRID: {TargetSrid}",
                layer.Id, service.Id, storageSrid, targetSrid);
            _queryBuilderPool.Return(builder, service, layer, storageSrid, targetSrid);
            throw;
        }

        await using var command = PostgresRecordMapper.CreateCommand(connection, definition, normalizedQuery.CommandTimeout, _options.DefaultCommandTimeoutSeconds);
        await using var reader = await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        try
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return PostgresRecordMapper.CreateFeatureRecord(reader, layer);
            }
        }
        finally
        {
            _queryBuilderPool.Return(builder, service, layer, storageSrid, targetSrid);
        }
    }

    public async Task<long> CountAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? query,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);

        return await PerformanceMeasurement.MeasureAsync(
            "CountQuery",
            async () =>
            {
                var normalizedQuery = query ?? new FeatureQuery();

                // Check if layer is backed by a SQL view
                if (SqlViewQueryBuilder.IsSqlView(layer))
                {
                    return await ExecuteSqlViewCountAsync(dataSource, layer, normalizedQuery, cancellationToken).ConfigureAwait(false);
                }

                var storageSrid = layer.Storage?.Srid ?? CrsHelper.Wgs84;
                var targetSrid = CrsHelper.ParseCrs(normalizedQuery.Crs ?? service.Ogc.DefaultCrs);

                await using var connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
                await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
                    await connection.OpenAsync(ct).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);

                var builder = _queryBuilderPool.Get(service, layer, storageSrid, targetSrid);
                try
                {
                    var definition = builder.BuildCount(normalizedQuery);

                    await using var command = PostgresRecordMapper.CreateCommand(connection, definition, normalizedQuery.CommandTimeout, _options.DefaultCommandTimeoutSeconds);
                    var result = await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
                        await command.ExecuteScalarAsync(ct).ConfigureAwait(false),
                        cancellationToken).ConfigureAwait(false);

                    return (result is null || result is DBNull) ? 0 : Convert.ToInt64(result, CultureInfo.InvariantCulture);
                }
                finally
                {
                    _queryBuilderPool.Return(builder, service, layer, storageSrid, targetSrid);
                }
            },
            (duration, count) => _logger?.LogInformation(
                "Count query completed for layer {LayerId} in {Duration}ms: {RecordCount} records",
                layer.Id, duration.TotalMilliseconds, count))
            .ConfigureAwait(false);
    }

    public async Task<FeatureRecord?> GetAsync(
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

        // Check if layer is backed by a SQL view
        if (SqlViewQueryBuilder.IsSqlView(layer))
        {
            return await ExecuteSqlViewByIdAsync(dataSource, layer, featureId, query, cancellationToken).ConfigureAwait(false);
        }

        var storageSrid = layer.Storage?.Srid ?? CrsHelper.Wgs84;
        var targetSrid = CrsHelper.ParseCrs(query?.Crs ?? service.Ogc.DefaultCrs);

        await using var connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var builder = _queryBuilderPool.Get(service, layer, storageSrid, targetSrid);
        try
        {
            var definition = builder.BuildById(featureId);

            await using var command = PostgresRecordMapper.CreateCommand(connection, definition, null, _options.DefaultCommandTimeoutSeconds);
            await using var reader = await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
                await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return PostgresRecordMapper.CreateFeatureRecord(reader, layer);
            }

            return null;
        }
        finally
        {
            _queryBuilderPool.Return(builder, service, layer, storageSrid, targetSrid);
        }
    }

    public async Task<FeatureRecord> CreateAsync(
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

        // SECURITY: ResolveTableName validates the table identifier through SqlIdentifierValidator.ValidateAndQuotePostgres
        // to prevent SQL injection. The layer definition comes from trusted metadata configuration.
        var table = PostgresRecordMapper.ResolveTableName(layer);
        var keyColumn = PostgresRecordMapper.ResolvePrimaryKey(layer);

        var (connection, npgsqlTransaction, shouldDisposeConnection) =
            await GetConnectionAndTransactionAsync(dataSource, transaction, cancellationToken).ConfigureAwait(false);

        try
        {
            var normalized = PostgresRecordMapper.NormalizeRecord(layer, record.Attributes, includeKey: true);
            if (normalized.Columns.Count == 0)
            {
                throw new InvalidOperationException($"Create operation for layer '{layer.Id}' did not include any columns.");
            }

            // SECURITY: All column names in normalized.Columns.ColumnName are validated and quoted via QuoteIdentifier
            // in NormalizeRecord. All values are parameterized through BuildValueExpression.
            var columnList = string.Join(", ", normalized.Columns.Select(c => c.ColumnName));
            var valueList = string.Join(", ", normalized.Columns.Select(c => PostgresRecordMapper.BuildValueExpression(c, normalized.Srid)));

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = npgsqlTransaction;
                // SECURITY: SQL Injection Protection:
                // - table: Validated and quoted via ResolveTableName -> QuoteIdentifier -> ValidateAndQuotePostgres
                // - columnList: Each column validated and quoted in NormalizeRecord
                // - valueList: Contains parameterized placeholders (e.g., @p0, @p1) or ST_GeomFromGeoJSON(@p0)
                // - keyColumn: Validated and quoted via QuoteIdentifier -> ValidateAndQuotePostgres
                command.CommandText = $"insert into {table} ({columnList}) values ({valueList}) returning {PostgresRecordMapper.QuoteIdentifier(keyColumn)}";
                PostgresRecordMapper.AddParameters(command, normalized.Parameters);

                var (keyValue, created) = await PerformanceMeasurement.MeasureAsync(
                    "CreateFeature",
                    async () =>
                    {
                        var result = await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
                            await command.ExecuteScalarAsync(ct).ConfigureAwait(false),
                            cancellationToken).ConfigureAwait(false);
                        var keyValue = Convert.ToString(result, CultureInfo.InvariantCulture);
                        if (string.IsNullOrWhiteSpace(keyValue))
                        {
                            throw new InvalidOperationException("Failed to obtain key for inserted feature.");
                        }

                        var created = await GetAsync(dataSource, service, layer, keyValue, null, cancellationToken).ConfigureAwait(false)
                               ?? throw new InvalidOperationException("Failed to load feature after insert.");
                        return (keyValue, created);
                    },
                    (duration, result) => _logger?.LogInformation(
                        "Feature created for layer {LayerId} in {Duration}ms: FeatureId={FeatureId}",
                        layer.Id, duration.TotalMilliseconds, result.keyValue))
                    .ConfigureAwait(false);

                return created;
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
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNullOrWhiteSpace(featureId);
        Guard.NotNull(record);

        return await PerformanceMeasurement.MeasureAsync(
            "UpdateFeature",
            async () =>
            {
                // SECURITY: ResolveTableName validates the table identifier through SqlIdentifierValidator.ValidateAndQuotePostgres
                // to prevent SQL injection. The layer definition comes from trusted metadata configuration.
                var table = PostgresRecordMapper.ResolveTableName(layer);
                var keyColumn = PostgresRecordMapper.ResolvePrimaryKey(layer);

                var (connection, npgsqlTransaction, shouldDisposeConnection) =
                    await GetConnectionAndTransactionAsync(dataSource, transaction, cancellationToken).ConfigureAwait(false);

                try
                {
                    var normalized = PostgresRecordMapper.NormalizeRecord(layer, record.Attributes, includeKey: false);
                    if (normalized.Columns.Count == 0)
                    {
                        return await GetAsync(dataSource, service, layer, featureId, null, cancellationToken);
                    }

                    // SECURITY: All column names are validated and quoted in NormalizeRecord
                    // All values are parameterized through BuildValueExpression
                    var assignments = string.Join(", ", normalized.Columns.Select(c => $"{c.ColumnName} = {PostgresRecordMapper.BuildValueExpression(c, normalized.Srid)}"));
                    var parameters = new Dictionary<string, object?>(normalized.Parameters, StringComparer.Ordinal)
                    {
                        ["@key"] = PostgresRecordMapper.NormalizeKeyParameter(layer, featureId)
                    };

                    await using (var command = connection.CreateCommand())
                    {
                        command.Transaction = npgsqlTransaction;

                        // OPTIMISTIC CONCURRENCY CONTROL: Use row_version for detecting concurrent modifications
                        // If the record has a version and it's provided, include it in the WHERE clause
                        string whereClause;
                        if (record.Version != null)
                        {
                            // Include row_version check to detect concurrent modifications
                            whereClause = $"{PostgresRecordMapper.QuoteIdentifier(keyColumn)} = @key AND row_version = @version";
                            parameters["@version"] = record.Version;
                            // Increment row_version on successful update
                            assignments = $"{assignments}, row_version = row_version + 1";
                        }
                        else
                        {
                            // No version provided - fall back to simple WHERE clause
                            // This maintains backward compatibility but doesn't protect against concurrent updates
                            whereClause = $"{PostgresRecordMapper.QuoteIdentifier(keyColumn)} = @key";
                        }

                        // SECURITY: SQL Injection Protection:
                        // - table: Validated and quoted via ResolveTableName -> QuoteIdentifier -> ValidateAndQuotePostgres
                        // - assignments: Column names quoted, values parameterized
                        // - whereClause: keyColumn quoted, @key and @version are parameters
                        command.CommandText = $"update {table} set {assignments} where {whereClause}";
                        PostgresRecordMapper.AddParameters(command, parameters);
                        var affected = await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
                            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false),
                            cancellationToken).ConfigureAwait(false);
                        if (affected == 0)
                        {
                            // Check if the feature exists to distinguish between NotFound and concurrent modification
                            var exists = await GetAsync(dataSource, service, layer, featureId, null, cancellationToken).ConfigureAwait(false);
                            if (exists != null && record.Version != null)
                            {
                                // Feature exists but update failed with version check - concurrent modification detected
                                _logger?.LogWarning(
                                    "Concurrent modification detected for layer {LayerId}, FeatureId={FeatureId}. " +
                                    "Expected version {ExpectedVersion}, Actual version {ActualVersion}.",
                                    layer.Id, featureId, record.Version, exists.Version);
                                throw new ConcurrencyException(
                                    featureId,
                                    "Feature",
                                    record.Version,
                                    exists.Version);
                            }
                            else if (exists != null)
                            {
                                // Feature exists but update affected 0 rows without version check
                                _logger?.LogWarning("Feature update affected 0 rows for layer {LayerId}, FeatureId={FeatureId}, but feature exists. " +
                                                   "Possible constraint violation.",
                                    layer.Id, featureId);
                                throw new InvalidOperationException(
                                    $"Feature update affected 0 rows but feature exists. This may indicate a constraint violation. " +
                                    $"Layer: {layer.Id}, FeatureId: {featureId}");
                            }

                            _logger?.LogWarning("Feature update failed for layer {LayerId}, FeatureId={FeatureId}: Feature not found",
                                layer.Id, featureId);
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
            },
            (duration, result) => _logger?.LogInformation(
                "Feature updated for layer {LayerId} in {Duration}ms: FeatureId={FeatureId}",
                layer.Id, duration.TotalMilliseconds, featureId))
            .ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(
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

        return await PerformanceMeasurement.MeasureAsync(
            "DeleteFeature",
            async () =>
            {
                // SECURITY: ResolveTableName validates the table identifier through SqlIdentifierValidator.ValidateAndQuotePostgres
                // to prevent SQL injection. The layer definition comes from trusted metadata configuration.
                var table = PostgresRecordMapper.ResolveTableName(layer);
                var keyColumn = PostgresRecordMapper.ResolvePrimaryKey(layer);

                var (connection, npgsqlTransaction, shouldDisposeConnection) =
                    await GetConnectionAndTransactionAsync(dataSource, transaction, cancellationToken).ConfigureAwait(false);

                try
                {
                    await using var command = connection.CreateCommand();
                    command.Transaction = npgsqlTransaction;
                    // SECURITY: SQL Injection Protection:
                    // - table: Validated and quoted via ResolveTableName -> QuoteIdentifier -> ValidateAndQuotePostgres
                    // - keyColumn: Validated and quoted via QuoteIdentifier -> ValidateAndQuotePostgres
                    // - @key: Parameterized value
                    command.CommandText = $"delete from {table} where {PostgresRecordMapper.QuoteIdentifier(keyColumn)} = @key";
                    command.Parameters.AddWithValue("@key", PostgresRecordMapper.NormalizeKeyParameter(layer, featureId));
                    var affected = await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
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
            },
            (duration, deleted) =>
            {
                if (deleted)
                {
                    _logger?.LogInformation("Feature deleted for layer {LayerId} in {Duration}ms: FeatureId={FeatureId}",
                        layer.Id, duration.TotalMilliseconds, featureId);
                }
                else
                {
                    _logger?.LogWarning("Feature delete failed for layer {LayerId}, FeatureId={FeatureId}: Feature not found",
                        layer.Id, featureId);
                }
            })
            .ConfigureAwait(false);
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
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNullOrWhiteSpace(featureId);

        return await PerformanceMeasurement.MeasureAsync(
            "SoftDeleteFeature",
            async () =>
            {
                // SECURITY: ResolveTableName validates the table identifier through SqlIdentifierValidator.ValidateAndQuotePostgres
                // to prevent SQL injection. The layer definition comes from trusted metadata configuration.
                var table = PostgresRecordMapper.ResolveTableName(layer);
                var keyColumn = PostgresRecordMapper.ResolvePrimaryKey(layer);

                var (connection, npgsqlTransaction, shouldDisposeConnection) =
                    await GetConnectionAndTransactionAsync(dataSource, transaction, cancellationToken).ConfigureAwait(false);

                try
                {
                    await using var command = connection.CreateCommand();
                    command.Transaction = npgsqlTransaction;
                    // SECURITY: SQL Injection Protection:
                    // - table: Validated and quoted via ResolveTableName -> QuoteIdentifier -> ValidateAndQuotePostgres
                    // - keyColumn: Validated and quoted via QuoteIdentifier -> ValidateAndQuotePostgres
                    // - @key, @deletedBy: Parameterized values
                    command.CommandText = $@"
                        UPDATE {table}
                        SET is_deleted = true,
                            deleted_at = NOW(),
                            deleted_by = @deletedBy
                        WHERE {PostgresRecordMapper.QuoteIdentifier(keyColumn)} = @key
                          AND (is_deleted IS NULL OR is_deleted = false)";
                    command.Parameters.AddWithValue("@key", PostgresRecordMapper.NormalizeKeyParameter(layer, featureId));
                    command.Parameters.AddWithValue("@deletedBy", (object?)deletedBy ?? DBNull.Value);
                    var affected = await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
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
            },
            (duration, deleted) =>
            {
                if (deleted)
                {
                    _logger?.LogInformation("Feature soft-deleted for layer {LayerId} in {Duration}ms: FeatureId={FeatureId}, DeletedBy={DeletedBy}",
                        layer.Id, duration.TotalMilliseconds, featureId, deletedBy ?? "<system>");
                }
                else
                {
                    _logger?.LogWarning("Feature soft-delete failed for layer {LayerId}, FeatureId={FeatureId}: Feature not found or already deleted",
                        layer.Id, featureId);
                }
            })
            .ConfigureAwait(false);
    }

    public async Task<bool> RestoreAsync(
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

        return await PerformanceMeasurement.MeasureAsync(
            "RestoreFeature",
            async () =>
            {
                // SECURITY: ResolveTableName validates the table identifier through SqlIdentifierValidator.ValidateAndQuotePostgres
                // to prevent SQL injection. The layer definition comes from trusted metadata configuration.
                var table = PostgresRecordMapper.ResolveTableName(layer);
                var keyColumn = PostgresRecordMapper.ResolvePrimaryKey(layer);

                var (connection, npgsqlTransaction, shouldDisposeConnection) =
                    await GetConnectionAndTransactionAsync(dataSource, transaction, cancellationToken).ConfigureAwait(false);

                try
                {
                    await using var command = connection.CreateCommand();
                    command.Transaction = npgsqlTransaction;
                    // SECURITY: SQL Injection Protection:
                    // - table: Validated and quoted via ResolveTableName -> QuoteIdentifier -> ValidateAndQuotePostgres
                    // - keyColumn: Validated and quoted via QuoteIdentifier -> ValidateAndQuotePostgres
                    // - @key: Parameterized value
                    command.CommandText = $@"
                        UPDATE {table}
                        SET is_deleted = false,
                            deleted_at = NULL,
                            deleted_by = NULL
                        WHERE {PostgresRecordMapper.QuoteIdentifier(keyColumn)} = @key
                          AND is_deleted = true";
                    command.Parameters.AddWithValue("@key", PostgresRecordMapper.NormalizeKeyParameter(layer, featureId));
                    var affected = await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
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
            },
            (duration, restored) =>
            {
                if (restored)
                {
                    _logger?.LogInformation("Feature restored for layer {LayerId} in {Duration}ms: FeatureId={FeatureId}",
                        layer.Id, duration.TotalMilliseconds, featureId);
                }
                else
                {
                    _logger?.LogWarning("Feature restore failed for layer {LayerId}, FeatureId={FeatureId}: Feature not found or not deleted",
                        layer.Id, featureId);
                }
            })
            .ConfigureAwait(false);
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
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNullOrWhiteSpace(featureId);

        return await PerformanceMeasurement.MeasureAsync(
            "HardDeleteFeature",
            async () =>
            {
                // SECURITY: ResolveTableName validates the table identifier through SqlIdentifierValidator.ValidateAndQuotePostgres
                // to prevent SQL injection. The layer definition comes from trusted metadata configuration.
                var table = PostgresRecordMapper.ResolveTableName(layer);
                var keyColumn = PostgresRecordMapper.ResolvePrimaryKey(layer);

                var (connection, npgsqlTransaction, shouldDisposeConnection) =
                    await GetConnectionAndTransactionAsync(dataSource, transaction, cancellationToken).ConfigureAwait(false);

                try
                {
                    await using var command = connection.CreateCommand();
                    command.Transaction = npgsqlTransaction;
                    // SECURITY: SQL Injection Protection:
                    // - table: Validated and quoted via ResolveTableName -> QuoteIdentifier -> ValidateAndQuotePostgres
                    // - keyColumn: Validated and quoted via QuoteIdentifier -> ValidateAndQuotePostgres
                    // - @key: Parameterized value
                    command.CommandText = $"DELETE FROM {table} WHERE {PostgresRecordMapper.QuoteIdentifier(keyColumn)} = @key";
                    command.Parameters.AddWithValue("@key", PostgresRecordMapper.NormalizeKeyParameter(layer, featureId));
                    var affected = await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
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
            },
            (duration, deleted) =>
            {
                if (deleted)
                {
                    _logger?.LogWarning("Feature PERMANENTLY deleted for layer {LayerId} in {Duration}ms: FeatureId={FeatureId}, DeletedBy={DeletedBy}",
                        layer.Id, duration.TotalMilliseconds, featureId, deletedBy ?? "<system>");
                }
                else
                {
                    _logger?.LogWarning("Feature hard-delete failed for layer {LayerId}, FeatureId={FeatureId}: Feature not found",
                        layer.Id, featureId);
                }
            })
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Performs statistical aggregations at the database level using SQL GROUP BY.
    /// CRITICAL PERFORMANCE OPTIMIZATION - replaces loading all records into memory.
    /// </summary>
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

        return await PerformanceMeasurement.MeasureAsync(
            "StatisticsQuery",
            async () =>
            {
                var normalizedQuery = filter ?? new FeatureQuery();
                var storageSrid = layer.Storage?.Srid ?? CrsHelper.Wgs84;
                var targetSrid = CrsHelper.ParseCrs(normalizedQuery.Crs ?? service.Ogc.DefaultCrs);

                await using var connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
                await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
                    await connection.OpenAsync(ct).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);

                var builder = _queryBuilderPool.Get(service, layer, storageSrid, targetSrid);
                try
                {
                    var definition = builder.BuildStatistics(normalizedQuery, statistics, groupByFields);

                    await using var command = PostgresRecordMapper.CreateCommand(connection, definition, null, _options.LongRunningQueryTimeoutSeconds);
                    await using var reader = await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
                        await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct).ConfigureAwait(false),
                        cancellationToken).ConfigureAwait(false);

                    var results = new List<StatisticsResult>();
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        var groupValues = new Dictionary<string, object?>();
                        var statisticsValues = new Dictionary<string, object?>();

                        // Read group by fields first
                        var fieldIndex = 0;
                        if (groupByFields != null)
                        {
                            foreach (var groupField in groupByFields)
                            {
                                groupValues[groupField] = reader.IsDBNull(fieldIndex) ? null : reader.GetValue(fieldIndex);
                                fieldIndex++;
                            }
                        }

                        // Read statistics values
                        foreach (var stat in statistics)
                        {
                            var outputName = stat.OutputName ?? $"{stat.Type}_{stat.FieldName}";
                            statisticsValues[outputName] = reader.IsDBNull(fieldIndex) ? null : reader.GetValue(fieldIndex);
                            fieldIndex++;
                        }

                        results.Add(new StatisticsResult(groupValues, statisticsValues));
                    }

                    return results;
                }
                finally
                {
                    _queryBuilderPool.Return(builder, service, layer, storageSrid, targetSrid);
                }
            },
            (duration, results) => _logger?.LogInformation(
                "Statistics query completed for layer {LayerId} in {Duration}ms: {ResultCount} groups, {StatisticCount} statistics",
                layer.Id, duration.TotalMilliseconds, results.Count, statistics.Count))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves distinct values at the database level using SQL DISTINCT.
    /// CRITICAL PERFORMANCE OPTIMIZATION - replaces loading all records into memory.
    /// </summary>
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

        var normalizedQuery = filter ?? new FeatureQuery();
        var storageSrid = layer.Storage?.Srid ?? CrsHelper.Wgs84;
        var targetSrid = CrsHelper.ParseCrs(normalizedQuery.Crs ?? service.Ogc.DefaultCrs);

        await using var connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var builder = _queryBuilderPool.Get(service, layer, storageSrid, targetSrid);
        try
        {
            var definition = builder.BuildDistinct(normalizedQuery, fieldNames);

            await using var command = PostgresRecordMapper.CreateCommand(connection, definition, null, _options.DefaultCommandTimeoutSeconds);
            await using var reader = await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
                await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            var results = new List<DistinctResult>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var values = new Dictionary<string, object?>();
                for (var i = 0; i < fieldNames.Count; i++)
                {
                    values[fieldNames[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                results.Add(new DistinctResult(values));
            }

            return results;
        }
        finally
        {
            _queryBuilderPool.Return(builder, service, layer, storageSrid, targetSrid);
        }
    }

    /// <summary>
    /// Calculates spatial extent at the database level using PostGIS ST_Extent.
    /// CRITICAL PERFORMANCE OPTIMIZATION - replaces loading all geometries into memory.
    /// </summary>
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

        await using var connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var builder = _queryBuilderPool.Get(service, layer, storageSrid, targetSrid);
        try
        {
            var definition = builder.BuildExtent(normalizedQuery, targetSrid);

            await using var command = PostgresRecordMapper.CreateCommand(connection, definition, null, _options.DefaultCommandTimeoutSeconds);
            var result = await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
                await command.ExecuteScalarAsync(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            if (result is null || result is DBNull)
            {
                return null;
            }

            // Parse PostGIS BOX format: BOX(minx miny, maxx maxy)
            var boxString = result.ToString();
            if (string.IsNullOrWhiteSpace(boxString))
            {
                return null;
            }

            // Remove "BOX(" prefix and ")" suffix
            boxString = boxString.Replace("BOX(", "").Replace(")", "");
            var coords = boxString.Split(',');
            if (coords.Length != 2)
            {
                return null;
            }

            var minCoords = coords[0].Trim().Split(' ');
            var maxCoords = coords[1].Trim().Split(' ');
            if (minCoords.Length < 2 || maxCoords.Length < 2)
            {
                return null;
            }

            if (!double.TryParse(minCoords[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var minX) ||
                !double.TryParse(minCoords[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var minY) ||
                !double.TryParse(maxCoords[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var maxX) ||
                !double.TryParse(maxCoords[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var maxY))
            {
                return null;
            }

            return new BoundingBox(minX, minY, maxX, maxY, Crs: $"EPSG:{targetSrid}");
        }
        finally
        {
            _queryBuilderPool.Return(builder, service, layer, storageSrid, targetSrid);
        }
    }

    public async Task TestConnectivityAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(dataSource);

        if (string.IsNullOrWhiteSpace(dataSource.ConnectionString))
        {
            throw new InvalidOperationException($"Data source '{dataSource.Id}' has no connection string configured.");
        }

        await using var connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        command.CommandTimeout = _options.HealthCheckTimeoutSeconds;

        await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteScalarAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a SQL view query to retrieve features.
    /// </summary>
    private async IAsyncEnumerable<FeatureRecord> ExecuteSqlViewQueryAsync(
        DataSourceDefinition dataSource,
        LayerDefinition layer,
        FeatureQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestParameters = query.SqlViewParameters ?? new Dictionary<string, string>();
        var sqlViewBuilder = new SqlViewQueryBuilder(layer, requestParameters);
        var queryDef = sqlViewBuilder.BuildSelect(query);

        await using var connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = queryDef.Sql;

        // Apply timeout from SQL view definition or query
        var timeout = sqlViewBuilder.GetCommandTimeout() ?? query.CommandTimeout ?? TimeSpan.FromSeconds(_options.DefaultCommandTimeoutSeconds);
        command.CommandTimeout = (int)timeout.TotalSeconds;

        // Add parameters
        foreach (var param in queryDef.Parameters)
        {
            var npgsqlParam = command.CreateParameter();
            npgsqlParam.ParameterName = param.Key;
            npgsqlParam.Value = param.Value ?? DBNull.Value;
            command.Parameters.Add(npgsqlParam);
        }

        await using var reader = await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return PostgresRecordMapper.CreateFeatureRecord(reader, layer);
        }
    }

    /// <summary>
    /// Executes a SQL view count query.
    /// </summary>
    private async Task<long> ExecuteSqlViewCountAsync(
        DataSourceDefinition dataSource,
        LayerDefinition layer,
        FeatureQuery query,
        CancellationToken cancellationToken = default)
    {
        var requestParameters = query.SqlViewParameters ?? new Dictionary<string, string>();
        var sqlViewBuilder = new SqlViewQueryBuilder(layer, requestParameters);
        var queryDef = sqlViewBuilder.BuildCount(query);

        await using var connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = queryDef.Sql;

        // Apply timeout from SQL view definition or query
        var timeout = sqlViewBuilder.GetCommandTimeout() ?? query.CommandTimeout ?? TimeSpan.FromSeconds(_options.DefaultCommandTimeoutSeconds);
        command.CommandTimeout = (int)timeout.TotalSeconds;

        // Add parameters
        foreach (var param in queryDef.Parameters)
        {
            var npgsqlParam = command.CreateParameter();
            npgsqlParam.ParameterName = param.Key;
            npgsqlParam.Value = param.Value ?? DBNull.Value;
            command.Parameters.Add(npgsqlParam);
        }

        var result = await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteScalarAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        return (result is null || result is DBNull) ? 0 : Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Executes a SQL view query to retrieve a single feature by ID.
    /// </summary>
    private async Task<FeatureRecord?> ExecuteSqlViewByIdAsync(
        DataSourceDefinition dataSource,
        LayerDefinition layer,
        string featureId,
        FeatureQuery? query,
        CancellationToken cancellationToken = default)
    {
        var requestParameters = query?.SqlViewParameters ?? new Dictionary<string, string>();
        var sqlViewBuilder = new SqlViewQueryBuilder(layer, requestParameters);
        var queryDef = sqlViewBuilder.BuildById(featureId, query);

        await using var connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = queryDef.Sql;

        // Apply timeout from SQL view definition or query
        var timeout = sqlViewBuilder.GetCommandTimeout() ?? query?.CommandTimeout ?? TimeSpan.FromSeconds(_options.DefaultCommandTimeoutSeconds);
        command.CommandTimeout = (int)timeout.TotalSeconds;

        // Add parameters
        foreach (var param in queryDef.Parameters)
        {
            var npgsqlParam = command.CreateParameter();
            npgsqlParam.ParameterName = param.Key;
            npgsqlParam.Value = param.Value ?? DBNull.Value;
            command.Parameters.Add(npgsqlParam);
        }

        await using var reader = await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return PostgresRecordMapper.CreateFeatureRecord(reader, layer);
        }

        return null;
    }
}
