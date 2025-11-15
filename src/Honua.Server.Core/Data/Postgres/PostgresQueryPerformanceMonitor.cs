// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Server.Core.Data.Postgres;

/// <summary>
/// Configuration for query performance monitoring.
/// </summary>
public sealed class QueryPerformanceMonitorOptions
{
    /// <summary>
    /// Enable slow query logging.
    /// Default: true
    /// </summary>
    public bool EnableSlowQueryLogging { get; set; } = true;

    /// <summary>
    /// Threshold for slow query logging in milliseconds.
    /// Default: 500ms
    /// </summary>
    public int SlowQueryThresholdMs { get; set; } = 500;

    /// <summary>
    /// Enable query plan analysis for slow queries.
    /// Uses PostgreSQL EXPLAIN to analyze slow queries.
    /// Default: false (requires additional database round-trip)
    /// </summary>
    public bool EnableQueryPlanAnalysis { get; set; } = false;

    /// <summary>
    /// Warn when OFFSET pagination exceeds threshold.
    /// Default: 1000
    /// </summary>
    public int OffsetWarningThreshold { get; set; } = 1000;

    /// <summary>
    /// Enable query duration histogram metrics.
    /// Default: true
    /// </summary>
    public bool EnableMetrics { get; set; } = true;
}

/// <summary>
/// Monitors PostgreSQL query performance and provides diagnostics.
/// </summary>
public sealed class PostgresQueryPerformanceMonitor
{
    private readonly QueryPerformanceMonitorOptions _options;
    private readonly ILogger<PostgresQueryPerformanceMonitor> _logger;
    private readonly ConcurrentDictionary<string, QueryMetrics> _metricsPerOperation = new();

    public PostgresQueryPerformanceMonitor(
        QueryPerformanceMonitorOptions options,
        ILogger<PostgresQueryPerformanceMonitor> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Monitor a query execution and log performance metrics.
    /// </summary>
    public async Task<T> MonitorQueryAsync<T>(
        string operationType,
        string queryDescription,
        Func<Task<T>> executeQuery,
        NpgsqlConnection? connection = null,
        string? sql = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        T result;

        try
        {
            result = await executeQuery().ConfigureAwait(false);
            sw.Stop();

            var duration = sw.Elapsed;

            // Record metrics
            if (_options.EnableMetrics)
            {
                RecordQueryMetrics(operationType, duration);
            }

            // Log slow queries
            if (_options.EnableSlowQueryLogging && duration.TotalMilliseconds >= _options.SlowQueryThresholdMs)
            {
                await LogSlowQueryAsync(operationType, queryDescription, duration, connection, sql, cancellationToken)
                    .ConfigureAwait(false);
            }

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "Query failed: {OperationType} - {Description}, Duration: {Duration}ms",
                operationType, queryDescription, sw.ElapsedMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// Check if an offset value is inefficient and log a warning if needed.
    /// </summary>
    public void CheckOffsetEfficiency(int offset, string operationType)
    {
        if (offset > _options.OffsetWarningThreshold)
        {
            _logger.LogWarning(
                "Inefficient OFFSET pagination detected in {OperationType}: OFFSET={Offset}. " +
                "Consider using cursor-based pagination for better performance. " +
                "Large OFFSET values require PostgreSQL to scan and discard {Offset} rows before returning results.",
                operationType, offset);
        }
    }

    /// <summary>
    /// Get query performance statistics for an operation type.
    /// </summary>
    public QueryMetrics? GetMetrics(string operationType)
    {
        return _metricsPerOperation.TryGetValue(operationType, out var metrics) ? metrics : null;
    }

    /// <summary>
    /// Get all query performance statistics.
    /// </summary>
    public ConcurrentDictionary<string, QueryMetrics> GetAllMetrics()
    {
        return new ConcurrentDictionary<string, QueryMetrics>(_metricsPerOperation);
    }

    /// <summary>
    /// Reset all metrics.
    /// </summary>
    public void ResetMetrics()
    {
        _metricsPerOperation.Clear();
    }

    private void RecordQueryMetrics(string operationType, TimeSpan duration)
    {
        var metrics = _metricsPerOperation.GetOrAdd(operationType, _ => new QueryMetrics());
        metrics.RecordQuery(duration);
    }

    private async Task LogSlowQueryAsync(
        string operationType,
        string description,
        TimeSpan duration,
        NpgsqlConnection? connection,
        string? sql,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Slow query detected: {OperationType} - {Description}, Duration: {Duration}ms",
            operationType, description, duration.TotalMilliseconds);

        // Optionally analyze query plan
        if (_options.EnableQueryPlanAnalysis && connection != null && !string.IsNullOrEmpty(sql))
        {
            try
            {
                await AnalyzeQueryPlanAsync(connection, sql, operationType, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to analyze query plan for slow query: {OperationType}", operationType);
            }
        }
    }

    private async Task AnalyzeQueryPlanAsync(
        NpgsqlConnection connection,
        string sql,
        string operationType,
        CancellationToken cancellationToken)
    {
        // Run EXPLAIN (ANALYZE, BUFFERS) on the slow query
        // Note: ANALYZE actually executes the query, so we use EXPLAIN only for safety
        var explainSql = $"EXPLAIN (BUFFERS, VERBOSE) {sql}";

        await using var command = connection.CreateCommand();
        command.CommandText = explainSql;
        command.CommandTimeout = 30; // Short timeout for EXPLAIN

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var plan = new System.Text.StringBuilder();

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var line = reader.GetString(0);
            plan.AppendLine(line);
        }

        _logger.LogInformation(
            "Query plan for slow {OperationType}:\n{QueryPlan}",
            operationType, plan.ToString());
    }
}

/// <summary>
/// Metrics for a specific query operation type.
/// </summary>
public sealed class QueryMetrics
{
    private long _count;
    private long _totalMs;
    private long _minMs = long.MaxValue;
    private long _maxMs;
    private readonly ConcurrentDictionary<int, long> _histogram = new();

    public long Count => Interlocked.Read(ref _count);
    public long TotalMs => Interlocked.Read(ref _totalMs);
    public long MinMs => Interlocked.Read(ref _minMs);
    public long MaxMs => Interlocked.Read(ref _maxMs);

    public double AverageMs
    {
        get
        {
            var c = Count;
            return c > 0 ? (double)TotalMs / c : 0;
        }
    }

    public void RecordQuery(TimeSpan duration)
    {
        var ms = (long)duration.TotalMilliseconds;

        Interlocked.Increment(ref _count);
        Interlocked.Add(ref _totalMs, ms);

        // Update min
        long currentMin;
        do
        {
            currentMin = Interlocked.Read(ref _minMs);
            if (ms >= currentMin) break;
        } while (Interlocked.CompareExchange(ref _minMs, ms, currentMin) != currentMin);

        // Update max
        long currentMax;
        do
        {
            currentMax = Interlocked.Read(ref _maxMs);
            if (ms <= currentMax) break;
        } while (Interlocked.CompareExchange(ref _maxMs, ms, currentMax) != currentMax);

        // Update histogram (buckets: <10ms, 10-50ms, 50-100ms, 100-500ms, 500-1000ms, >1000ms)
        var bucket = ms switch
        {
            < 10 => 10,
            < 50 => 50,
            < 100 => 100,
            < 500 => 500,
            < 1000 => 1000,
            _ => 5000
        };

        _histogram.AddOrUpdate(bucket, 1, (_, count) => count + 1);
    }

    public ConcurrentDictionary<int, long> GetHistogram()
    {
        return new ConcurrentDictionary<int, long>(_histogram);
    }
}
