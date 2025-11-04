// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Npgsql;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data.Postgres;

/// <summary>
/// Provides metrics for monitoring Npgsql connection pool performance using OpenTelemetry.
/// </summary>
public sealed class PostgresConnectionPoolMetrics : IDisposable
{
    private static readonly Meter Meter = new("Honua.Server.Core.Data.Postgres", "1.0.0");
    private static readonly ActivitySource ActivitySource = new("Honua.Server.Core.Data.Postgres", "1.0.0");

    private readonly ObservableGauge<int> _activeConnectionsGauge;
    private readonly ObservableGauge<int> _idleConnectionsGauge;
    private readonly ObservableGauge<int> _totalConnectionsGauge;
    private readonly Counter<long> _connectionFailuresCounter;
    private readonly Counter<long> _connectionOpenedCounter;
    private readonly Counter<long> _connectionClosedCounter;
    private readonly Histogram<double> _poolWaitTimeHistogram;
    private readonly Histogram<double> _connectionLifetimeHistogram;
    private readonly Dictionary<string, NpgsqlDataSource> _dataSources;
    private readonly Dictionary<string, ConnectionPoolStats> _poolStats;
    private bool _disposed;

    public PostgresConnectionPoolMetrics(Dictionary<string, NpgsqlDataSource> dataSources)
    {
        _dataSources = dataSources ?? throw new ArgumentNullException(nameof(dataSources));
        _poolStats = new Dictionary<string, ConnectionPoolStats>();

        // Observable gauges for current pool state
        _activeConnectionsGauge = Meter.CreateObservableGauge(
            "postgres.pool.connections.active",
            GetTotalActiveConnections,
            unit: "connections",
            description: "Current number of active connections in the pool");

        _idleConnectionsGauge = Meter.CreateObservableGauge(
            "postgres.pool.connections.idle",
            GetTotalIdleConnections,
            unit: "connections",
            description: "Current number of idle connections in the pool");

        _totalConnectionsGauge = Meter.CreateObservableGauge(
            "postgres.pool.connections.total",
            GetTotalConnections,
            unit: "connections",
            description: "Total number of connections in the pool");

        // Counters for connection lifecycle events
        _connectionOpenedCounter = Meter.CreateCounter<long>(
            "postgres.pool.connections.opened",
            unit: "connections",
            description: "Total number of connections opened");

        _connectionClosedCounter = Meter.CreateCounter<long>(
            "postgres.pool.connections.closed",
            unit: "connections",
            description: "Total number of connections closed");

        _connectionFailuresCounter = Meter.CreateCounter<long>(
            "postgres.pool.connections.failures",
            unit: "failures",
            description: "Total number of connection failures");

        // Histograms for timing metrics
        _poolWaitTimeHistogram = Meter.CreateHistogram<double>(
            "postgres.pool.wait.duration",
            unit: "ms",
            description: "Time spent waiting for a connection from the pool");

        _connectionLifetimeHistogram = Meter.CreateHistogram<double>(
            "postgres.pool.connection.lifetime",
            unit: "ms",
            description: "Lifetime of pooled connections");
    }

    /// <summary>
    /// Records a connection failure with OpenTelemetry tracing.
    /// </summary>
    public void RecordConnectionFailure(string connectionString, Exception exception)
    {
        var tags = new TagList
        {
            { "error.type", exception.GetType().Name },
            { "error.message", exception.Message },
            { "connection.masked", MaskConnectionString(connectionString) }
        };
        _connectionFailuresCounter.Add(1, tags);

        // Record activity for distributed tracing
        using var activity = ActivitySource.StartActivity("postgres.connection.failure", ActivityKind.Client);
        activity?.SetTag("error.type", exception.GetType().Name);
        activity?.SetTag("error.message", exception.Message);
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
    }

    /// <summary>
    /// Records a connection being opened.
    /// </summary>
    public void RecordConnectionOpened(string connectionString)
    {
        var masked = MaskConnectionString(connectionString);
        var tags = new TagList { { "connection.masked", masked } };
        _connectionOpenedCounter.Add(1, tags);

        lock (_poolStats)
        {
            if (!_poolStats.ContainsKey(masked))
            {
                _poolStats[masked] = new ConnectionPoolStats();
            }
            _poolStats[masked].ActiveConnections++;
        }
    }

    /// <summary>
    /// Records a connection being closed.
    /// </summary>
    public void RecordConnectionClosed(string connectionString, TimeSpan lifetime)
    {
        var masked = MaskConnectionString(connectionString);
        var tags = new TagList { { "connection.masked", masked } };
        _connectionClosedCounter.Add(1, tags);
        _connectionLifetimeHistogram.Record(lifetime.TotalMilliseconds, tags);

        lock (_poolStats)
        {
            if (_poolStats.TryGetValue(masked, out var stats) && stats.ActiveConnections > 0)
            {
                stats.ActiveConnections--;
            }
        }
    }

    /// <summary>
    /// Records the time spent waiting for a connection from the pool.
    /// </summary>
    public void RecordPoolWaitTime(string connectionString, TimeSpan waitTime)
    {
        var masked = MaskConnectionString(connectionString);
        var tags = new TagList { { "connection.masked", masked } };
        _poolWaitTimeHistogram.Record(waitTime.TotalMilliseconds, tags);

        // Record activity for distributed tracing
        using var activity = ActivitySource.StartActivity("postgres.pool.wait", ActivityKind.Client);
        activity?.SetTag("connection.masked", masked);
        activity?.SetTag("wait_time_ms", waitTime.TotalMilliseconds);
    }

    /// <summary>
    /// Creates a stopwatch to measure pool wait time.
    /// </summary>
    public Stopwatch CreateWaitTimeStopwatch() => Stopwatch.StartNew();

    private int GetTotalActiveConnections()
    {
        lock (_poolStats)
        {
            var total = 0;
            foreach (var stats in _poolStats.Values)
            {
                total += stats.ActiveConnections;
            }
            return total;
        }
    }

    private int GetTotalIdleConnections()
    {
        lock (_poolStats)
        {
            var total = 0;
            foreach (var stats in _poolStats.Values)
            {
                total += stats.IdleConnections;
            }
            return total;
        }
    }

    private int GetTotalConnections()
    {
        lock (_poolStats)
        {
            var total = 0;
            foreach (var stats in _poolStats.Values)
            {
                total += stats.ActiveConnections + stats.IdleConnections;
            }
            return total;
        }
    }

    private static string MaskConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "unknown";
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            return $"{builder.Host}:{builder.Port}/{builder.Database}";
        }
        catch
        {
            return "malformed";
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Meter.Dispose();
        ActivitySource.Dispose();
    }

    private sealed class ConnectionPoolStats
    {
        public int ActiveConnections { get; set; }
        public int IdleConnections { get; set; }
    }
}
