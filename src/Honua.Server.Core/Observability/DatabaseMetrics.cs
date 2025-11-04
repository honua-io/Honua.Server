// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics.Metrics;

namespace Honua.Server.Core.Observability;

/// <summary>
/// OpenTelemetry metrics for database operations.
/// Tracks query performance, connection health, and transaction metrics.
/// </summary>
public interface IDatabaseMetrics
{
    void RecordQueryExecution(string queryType, string? tableName, TimeSpan duration, bool success);
    void RecordConnectionAcquired(string connectionString, TimeSpan waitTime);
    void RecordConnectionError(string connectionString, string errorType);
    void RecordTransactionCommitted(TimeSpan duration);
    void RecordTransactionRolledBack(string? reason);
    void RecordBulkOperation(string operationType, int recordCount, TimeSpan duration);
    void RecordSlowQuery(string queryType, string? tableName, TimeSpan duration, string? queryHint = null);
}

/// <summary>
/// Implementation of database metrics using OpenTelemetry.
/// </summary>
public sealed class DatabaseMetrics : IDatabaseMetrics, IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _queryCounter;
    private readonly Histogram<double> _queryDuration;
    private readonly Counter<long> _slowQueryCounter;
    private readonly Histogram<double> _connectionWaitTime;
    private readonly Counter<long> _connectionErrors;
    private readonly Counter<long> _transactionCommits;
    private readonly Counter<long> _transactionRollbacks;
    private readonly Histogram<double> _transactionDuration;
    private readonly Counter<long> _bulkOperations;
    private readonly Histogram<double> _bulkOperationDuration;

    public DatabaseMetrics()
    {
        _meter = new Meter("Honua.Server.Database", "1.0.0");

        _queryCounter = _meter.CreateCounter<long>(
            "honua.database.queries",
            unit: "{query}",
            description: "Number of database queries executed by type and table");

        _queryDuration = _meter.CreateHistogram<double>(
            "honua.database.query_duration",
            unit: "ms",
            description: "Database query execution duration");

        _slowQueryCounter = _meter.CreateCounter<long>(
            "honua.database.slow_queries",
            unit: "{query}",
            description: "Number of slow queries (>1s) by type and table");

        _connectionWaitTime = _meter.CreateHistogram<double>(
            "honua.database.connection_wait_time",
            unit: "ms",
            description: "Time spent waiting for a database connection");

        _connectionErrors = _meter.CreateCounter<long>(
            "honua.database.connection_errors",
            unit: "{error}",
            description: "Number of database connection errors by type");

        _transactionCommits = _meter.CreateCounter<long>(
            "honua.database.transaction_commits",
            unit: "{transaction}",
            description: "Number of committed transactions");

        _transactionRollbacks = _meter.CreateCounter<long>(
            "honua.database.transaction_rollbacks",
            unit: "{transaction}",
            description: "Number of rolled back transactions");

        _transactionDuration = _meter.CreateHistogram<double>(
            "honua.database.transaction_duration",
            unit: "ms",
            description: "Transaction execution duration");

        _bulkOperations = _meter.CreateCounter<long>(
            "honua.database.bulk_operations",
            unit: "{operation}",
            description: "Number of bulk operations by type");

        _bulkOperationDuration = _meter.CreateHistogram<double>(
            "honua.database.bulk_operation_duration",
            unit: "ms",
            description: "Bulk operation execution duration");
    }

    public void RecordQueryExecution(string queryType, string? tableName, TimeSpan duration, bool success)
    {
        _queryCounter.Add(1,
            new("query.type", NormalizeQueryType(queryType)),
            new("table.name", Normalize(tableName)),
            new("success", success.ToString()));

        _queryDuration.Record(duration.TotalMilliseconds,
            new("query.type", NormalizeQueryType(queryType)),
            new("table.name", Normalize(tableName)),
            new("success", success.ToString()));

        // Track slow queries (>1 second)
        if (duration.TotalMilliseconds > 1000)
        {
            RecordSlowQuery(queryType, tableName, duration);
        }
    }

    public void RecordSlowQuery(string queryType, string? tableName, TimeSpan duration, string? queryHint = null)
    {
        _slowQueryCounter.Add(1,
            new("query.type", NormalizeQueryType(queryType)),
            new("table.name", Normalize(tableName)),
            new("duration_bucket", GetDurationBucket(duration)),
            new("query.hint", Normalize(queryHint)));
    }

    public void RecordConnectionAcquired(string connectionString, TimeSpan waitTime)
    {
        _connectionWaitTime.Record(waitTime.TotalMilliseconds,
            new KeyValuePair<string, object?>[] { new("connection.pool", MaskConnectionString(connectionString)) });
    }

    public void RecordConnectionError(string connectionString, string errorType)
    {
        _connectionErrors.Add(1,
            new("connection.pool", MaskConnectionString(connectionString)),
            new("error.type", Normalize(errorType)));
    }

    public void RecordTransactionCommitted(TimeSpan duration)
    {
        _transactionCommits.Add(1);
        _transactionDuration.Record(duration.TotalMilliseconds,
            new KeyValuePair<string, object?>[] { new("transaction.status", "committed") });
    }

    public void RecordTransactionRolledBack(string? reason)
    {
        _transactionRollbacks.Add(1,
            new KeyValuePair<string, object?>[] { new("rollback.reason", Normalize(reason)) });

        // Record duration as well for rollback scenarios
        // This helps identify if rollbacks are happening quickly or after long operations
    }

    public void RecordBulkOperation(string operationType, int recordCount, TimeSpan duration)
    {
        _bulkOperations.Add(1,
            new("operation.type", Normalize(operationType)),
            new("record.count.bucket", GetRecordCountBucket(recordCount)));

        _bulkOperationDuration.Record(duration.TotalMilliseconds,
            new("operation.type", Normalize(operationType)),
            new("record.count.bucket", GetRecordCountBucket(recordCount)));
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? "unknown" : value;

    private static string NormalizeQueryType(string? queryType)
    {
        if (string.IsNullOrWhiteSpace(queryType))
            return "unknown";

        return queryType.ToUpperInvariant() switch
        {
            "SELECT" => "select",
            "INSERT" => "insert",
            "UPDATE" => "update",
            "DELETE" => "delete",
            "MERGE" => "merge",
            "BULK_INSERT" => "bulk_insert",
            "BULK_UPDATE" => "bulk_update",
            _ => queryType.ToLowerInvariant()
        };
    }

    private static string MaskConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return "unknown";

        // Extract just the host/database for grouping
        // This is a simple implementation - adjust based on your connection string format
        try
        {
            if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
            {
                // PostgreSQL format
                var parts = connectionString.Split(';');
                var host = "unknown";
                var database = "unknown";

                foreach (var part in parts)
                {
                    if (part.StartsWith("Host=", StringComparison.OrdinalIgnoreCase))
                        host = part.Substring(5);
                    else if (part.StartsWith("Database=", StringComparison.OrdinalIgnoreCase))
                        database = part.Substring(9);
                }

                return $"{host}/{database}";
            }
            else if (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
            {
                // SQLite format
                return "sqlite";
            }

            return "unknown";
        }
        catch
        {
            return "malformed";
        }
    }

    private static string GetDurationBucket(TimeSpan duration)
    {
        var ms = duration.TotalMilliseconds;
        return ms switch
        {
            < 100 => "fast",
            < 1000 => "medium",
            < 5000 => "slow",
            < 10000 => "very_slow",
            _ => "critical"
        };
    }

    private static string GetRecordCountBucket(int count)
    {
        return count switch
        {
            < 10 => "tiny",
            < 100 => "small",
            < 1000 => "medium",
            < 10000 => "large",
            _ => "very_large"
        };
    }
}
