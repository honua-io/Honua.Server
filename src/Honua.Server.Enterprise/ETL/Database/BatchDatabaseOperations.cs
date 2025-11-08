// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Features;
using NetTopologySuite.IO;
using Npgsql;
using Honua.Server.Enterprise.ETL.Performance;

namespace Honua.Server.Enterprise.ETL.Database;

/// <summary>
/// Optimized database operations with batching and connection pooling
/// </summary>
public class BatchDatabaseOperations
{
    private readonly ILogger<BatchDatabaseOperations> _logger;
    private readonly IPerformanceMetrics? _metrics;
    private readonly int _batchSize;

    public BatchDatabaseOperations(
        ILogger<BatchDatabaseOperations> logger,
        IPerformanceMetrics? metrics = null,
        int batchSize = 1000)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics;
        _batchSize = batchSize;
    }

    /// <summary>
    /// Batch insert features using PostgreSQL COPY command for optimal performance
    /// </summary>
    public async Task<long> BulkInsertFeaturesAsync(
        string connectionString,
        string tableName,
        IEnumerable<IFeature> features,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        long totalInserted = 0;

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var featureList = features.ToList();
            if (!featureList.Any())
            {
                return 0;
            }

            // Get column information from first feature
            var firstFeature = featureList[0];
            var columns = GetFeatureColumns(firstFeature);

            // Use COPY for bulk insert
            var copyCommand = $"COPY {tableName} ({string.Join(", ", columns.Select(c => $"\"{c.Name}\""))}) FROM STDIN (FORMAT BINARY)";

            await using var writer = await connection.BeginBinaryImportAsync(copyCommand, cancellationToken);

            foreach (var feature in featureList)
            {
                await writer.StartRowAsync(cancellationToken);

                foreach (var column in columns)
                {
                    var value = GetFeatureValue(feature, column);
                    await writer.WriteAsync(value, column.Type, cancellationToken);
                }

                totalInserted++;

                if (totalInserted % 1000 == 0)
                {
                    _logger.LogDebug("Inserted {Count} features", totalInserted);
                }
            }

            await writer.CompleteAsync(cancellationToken);

            stopwatch.Stop();
            _metrics?.RecordDatabaseQuery("bulk_insert", stopwatch.Elapsed, true);

            _logger.LogInformation(
                "Bulk inserted {Count} features into {Table} in {DurationMs}ms",
                totalInserted,
                tableName,
                stopwatch.ElapsedMilliseconds);

            return totalInserted;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics?.RecordDatabaseQuery("bulk_insert", stopwatch.Elapsed, false);
            _logger.LogError(ex, "Error during bulk insert into {Table}", tableName);
            throw;
        }
    }

    /// <summary>
    /// Batch insert features using regular INSERT statements (fallback)
    /// </summary>
    public async Task<long> BatchInsertFeaturesAsync(
        string connectionString,
        string tableName,
        IEnumerable<IFeature> features,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        long totalInserted = 0;

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var featureList = features.ToList();
            if (!featureList.Any())
            {
                return 0;
            }

            // Get column information
            var firstFeature = featureList[0];
            var columns = GetFeatureColumns(firstFeature);

            // Process in batches
            for (int i = 0; i < featureList.Count; i += _batchSize)
            {
                var batch = featureList.Skip(i).Take(_batchSize).ToList();

                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

                var sql = BuildBatchInsertSql(tableName, columns, batch.Count);
                await using var command = new NpgsqlCommand(sql, connection, transaction);

                int paramIndex = 0;
                foreach (var feature in batch)
                {
                    foreach (var column in columns)
                    {
                        var value = GetFeatureValue(feature, column);
                        command.Parameters.AddWithValue($"@p{paramIndex++}", value ?? DBNull.Value);
                    }
                }

                totalInserted += await command.ExecuteNonQueryAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogDebug("Inserted batch {BatchNumber}, total: {TotalInserted}", i / _batchSize + 1, totalInserted);
            }

            stopwatch.Stop();
            _metrics?.RecordDatabaseQuery("batch_insert", stopwatch.Elapsed, true);

            _logger.LogInformation(
                "Batch inserted {Count} features into {Table} in {DurationMs}ms",
                totalInserted,
                tableName,
                stopwatch.ElapsedMilliseconds);

            return totalInserted;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics?.RecordDatabaseQuery("batch_insert", stopwatch.Elapsed, false);
            _logger.LogError(ex, "Error during batch insert into {Table}", tableName);
            throw;
        }
    }

    /// <summary>
    /// Batch update features
    /// </summary>
    public async Task<long> BatchUpdateFeaturesAsync(
        string connectionString,
        string tableName,
        IEnumerable<IFeature> features,
        string keyColumn,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        long totalUpdated = 0;

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var featureList = features.ToList();
            if (!featureList.Any())
            {
                return 0;
            }

            // Get column information
            var firstFeature = featureList[0];
            var columns = GetFeatureColumns(firstFeature).Where(c => c.Name != keyColumn).ToList();

            // Process in batches
            for (int i = 0; i < featureList.Count; i += _batchSize)
            {
                var batch = featureList.Skip(i).Take(_batchSize).ToList();

                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

                foreach (var feature in batch)
                {
                    var sql = BuildUpdateSql(tableName, columns, keyColumn);
                    await using var command = new NpgsqlCommand(sql, connection, transaction);

                    foreach (var column in columns)
                    {
                        var value = GetFeatureValue(feature, column);
                        command.Parameters.AddWithValue($"@{column.Name}", value ?? DBNull.Value);
                    }

                    var keyValue = feature.Attributes[keyColumn];
                    command.Parameters.AddWithValue($"@{keyColumn}", keyValue ?? DBNull.Value);

                    totalUpdated += await command.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);

                _logger.LogDebug("Updated batch {BatchNumber}, total: {TotalUpdated}", i / _batchSize + 1, totalUpdated);
            }

            stopwatch.Stop();
            _metrics?.RecordDatabaseQuery("batch_update", stopwatch.Elapsed, true);

            _logger.LogInformation(
                "Batch updated {Count} features in {Table} in {DurationMs}ms",
                totalUpdated,
                tableName,
                stopwatch.ElapsedMilliseconds);

            return totalUpdated;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics?.RecordDatabaseQuery("batch_update", stopwatch.Elapsed, false);
            _logger.LogError(ex, "Error during batch update in {Table}", tableName);
            throw;
        }
    }

    /// <summary>
    /// Execute query with prepared statement for better performance
    /// </summary>
    public async Task<List<IFeature>> ExecuteQueryWithPreparedStatementAsync(
        string connectionString,
        string sql,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand(sql, connection);
            await command.PrepareAsync(cancellationToken);

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value);
                }
            }

            var features = new List<IFeature>();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var wktReader = new WKTReader();

            while (await reader.ReadAsync(cancellationToken))
            {
                var attributes = new AttributesTable();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var name = reader.GetName(i);
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    attributes.Add(name, value);
                }

                // Look for geometry column (usually named 'geom' or 'geometry')
                // GetOrdinal throws IndexOutOfRangeException if column doesn't exist
                try
                {
                    var geomColumnIndex = reader.GetOrdinal("geom");
                    if (!reader.IsDBNull(geomColumnIndex))
                    {
                        var geomWkt = reader.GetString(geomColumnIndex);
                        var geometry = wktReader.Read(geomWkt);
                        features.Add(new Feature(geometry, attributes));
                    }
                    else
                    {
                        features.Add(new Feature(null, attributes));
                    }
                }
                catch (IndexOutOfRangeException)
                {
                    // Geometry column doesn't exist in this result set
                    features.Add(new Feature(null, attributes));
                }
            }

            stopwatch.Stop();
            _metrics?.RecordDatabaseQuery("query", stopwatch.Elapsed, true);

            _logger.LogDebug(
                "Executed query and retrieved {Count} features in {DurationMs}ms",
                features.Count,
                stopwatch.ElapsedMilliseconds);

            return features;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics?.RecordDatabaseQuery("query", stopwatch.Elapsed, false);
            _logger.LogError(ex, "Error executing query");
            throw;
        }
    }

    private List<ColumnInfo> GetFeatureColumns(IFeature feature)
    {
        var columns = new List<ColumnInfo>();

        // Add geometry column if present
        if (feature.Geometry != null)
        {
            columns.Add(new ColumnInfo { Name = "geom", Type = NpgsqlTypes.NpgsqlDbType.Geometry });
        }

        // Add attribute columns
        foreach (var attrName in feature.Attributes.GetNames())
        {
            var value = feature.Attributes[attrName];
            var dbType = GetDbType(value);
            columns.Add(new ColumnInfo { Name = attrName, Type = dbType });
        }

        return columns;
    }

    private object? GetFeatureValue(IFeature feature, ColumnInfo column)
    {
        if (column.Name == "geom")
        {
            return feature.Geometry;
        }

        return feature.Attributes[column.Name];
    }

    private NpgsqlTypes.NpgsqlDbType GetDbType(object? value)
    {
        return value switch
        {
            int => NpgsqlTypes.NpgsqlDbType.Integer,
            long => NpgsqlTypes.NpgsqlDbType.Bigint,
            double => NpgsqlTypes.NpgsqlDbType.Double,
            decimal => NpgsqlTypes.NpgsqlDbType.Numeric,
            bool => NpgsqlTypes.NpgsqlDbType.Boolean,
            DateTime => NpgsqlTypes.NpgsqlDbType.Timestamp,
            DateTimeOffset => NpgsqlTypes.NpgsqlDbType.TimestampTz,
            _ => NpgsqlTypes.NpgsqlDbType.Text
        };
    }

    private string BuildBatchInsertSql(string tableName, List<ColumnInfo> columns, int batchSize)
    {
        var sql = new StringBuilder();
        sql.Append($"INSERT INTO {tableName} (");
        sql.Append(string.Join(", ", columns.Select(c => $"\"{c.Name}\"")));
        sql.Append(") VALUES ");

        var valueSets = new List<string>();
        int paramIndex = 0;

        for (int i = 0; i < batchSize; i++)
        {
            var values = columns.Select(_ => $"@p{paramIndex++}");
            valueSets.Add($"({string.Join(", ", values)})");
        }

        sql.Append(string.Join(", ", valueSets));
        return sql.ToString();
    }

    private string BuildUpdateSql(string tableName, List<ColumnInfo> columns, string keyColumn)
    {
        var sql = new StringBuilder();
        sql.Append($"UPDATE {tableName} SET ");
        sql.Append(string.Join(", ", columns.Select(c => $"\"{c.Name}\" = @{c.Name}")));
        sql.Append($" WHERE \"{keyColumn}\" = @{keyColumn}");
        return sql.ToString();
    }

    private class ColumnInfo
    {
        public required string Name { get; set; }
        public NpgsqlTypes.NpgsqlDbType Type { get; set; }
    }
}

/// <summary>
/// Database performance optimization settings
/// </summary>
public class DatabaseOptimizationOptions
{
    /// <summary>
    /// Default batch size for batch operations
    /// </summary>
    public int DefaultBatchSize { get; set; } = 1000;

    /// <summary>
    /// Command timeout in seconds
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Whether to use connection pooling
    /// </summary>
    public bool UseConnectionPooling { get; set; } = true;

    /// <summary>
    /// Minimum pool size
    /// </summary>
    public int MinPoolSize { get; set; } = 5;

    /// <summary>
    /// Maximum pool size
    /// </summary>
    public int MaxPoolSize { get; set; } = 100;

    /// <summary>
    /// Connection idle lifetime in seconds
    /// </summary>
    public int ConnectionIdleLifetimeSeconds { get; set; } = 300;

    /// <summary>
    /// Whether to use prepared statements
    /// </summary>
    public bool UsePreparedStatements { get; set; } = true;
}
