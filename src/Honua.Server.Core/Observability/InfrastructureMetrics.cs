// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;

namespace Honua.Server.Core.Observability;

/// <summary>
/// OpenTelemetry metrics for infrastructure health and resource utilization.
/// Tracks memory, GC, thread pool, and connection pool statistics.
/// </summary>
public interface IInfrastructureMetrics
{
    void RecordMemoryUsage(long workingSetBytes, long gcHeapBytes);
    void RecordGarbageCollection(int generation, TimeSpan duration, long freedBytes);
    void RecordThreadPoolStats(int availableWorkerThreads, int availableIoThreads, int queuedWorkItems);
    void RecordHttpConnectionPoolStats(string poolName, int activeConnections, int idleConnections);
    void RecordDatabaseConnectionPoolStats(string poolName, int activeConnections, int idleConnections, int waitingRequests);
}

/// <summary>
/// Implementation of infrastructure metrics using OpenTelemetry.
/// Includes observable gauges for runtime metrics.
/// </summary>
public sealed class InfrastructureMetrics : IInfrastructureMetrics, IDisposable
{
    private readonly Meter _meter;
    private readonly Process _currentProcess;

    // Counters
    private readonly Counter<long> _garbageCollections;
    private readonly Counter<long> _gcFreedBytes;

    // Histograms
    private readonly Histogram<double> _gcDuration;

    // Observable gauges - these are automatically called by the metrics system
    private readonly ObservableGauge<long> _memoryWorkingSet;
    private readonly ObservableGauge<long> _memoryGcHeap;
    private readonly ObservableGauge<long> _memoryPrivateBytes;
    private readonly ObservableGauge<long> _threadPoolWorkerThreads;
    private readonly ObservableGauge<long> _threadPoolIoThreads;
    private readonly ObservableGauge<long> _threadPoolQueueLength;
    private readonly ObservableGauge<long> _threadCount;
    private readonly ObservableGauge<double> _cpuUsagePercent;

    public InfrastructureMetrics()
    {
        _meter = new Meter("Honua.Server.Infrastructure", "1.0.0");
        _currentProcess = Process.GetCurrentProcess();

        // Create counters
        _garbageCollections = _meter.CreateCounter<long>(
            "honua.infrastructure.gc_collections",
            unit: "{collection}",
            description: "Number of garbage collections by generation");

        _gcFreedBytes = _meter.CreateCounter<long>(
            "honua.infrastructure.gc_freed_bytes",
            unit: "bytes",
            description: "Bytes freed by garbage collection");

        // Create histograms
        _gcDuration = _meter.CreateHistogram<double>(
            "honua.infrastructure.gc_duration",
            unit: "ms",
            description: "Garbage collection pause duration");

        // Create observable gauges for automatic metric collection
        _memoryWorkingSet = _meter.CreateObservableGauge(
            "honua.infrastructure.memory_working_set",
            () => _currentProcess.WorkingSet64,
            unit: "bytes",
            description: "Process working set size");

        _memoryGcHeap = _meter.CreateObservableGauge(
            "honua.infrastructure.memory_gc_heap",
            () => GC.GetTotalMemory(forceFullCollection: false),
            unit: "bytes",
            description: "GC heap size");

        _memoryPrivateBytes = _meter.CreateObservableGauge(
            "honua.infrastructure.memory_private_bytes",
            () => _currentProcess.PrivateMemorySize64,
            unit: "bytes",
            description: "Process private memory size");

        _threadPoolWorkerThreads = _meter.CreateObservableGauge<long>(
            "honua.infrastructure.threadpool_worker_threads",
            GetAvailableWorkerThreads,
            unit: "{thread}",
            description: "Available worker threads in thread pool");

        _threadPoolIoThreads = _meter.CreateObservableGauge<long>(
            "honua.infrastructure.threadpool_io_threads",
            GetAvailableIoThreads,
            unit: "{thread}",
            description: "Available I/O threads in thread pool");

        _threadPoolQueueLength = _meter.CreateObservableGauge<long>(
            "honua.infrastructure.threadpool_queue_length",
            GetThreadPoolQueueLength,
            unit: "{item}",
            description: "Number of items queued in thread pool");

        _threadCount = _meter.CreateObservableGauge<long>(
            "honua.infrastructure.thread_count",
            () => (long)_currentProcess.Threads.Count,
            unit: "{thread}",
            description: "Total number of threads in the process");

        _cpuUsagePercent = _meter.CreateObservableGauge(
            "honua.infrastructure.cpu_usage_percent",
            GetCpuUsagePercent,
            unit: "%",
            description: "CPU usage percentage");
    }

    public void RecordMemoryUsage(long workingSetBytes, long gcHeapBytes)
    {
        // Memory is now tracked via observable gauges automatically
        // This method is kept for backward compatibility or manual recording
    }

    public void RecordGarbageCollection(int generation, TimeSpan duration, long freedBytes)
    {
        _garbageCollections.Add(1,
            new("gc.generation", generation.ToString()),
            new("gc.type", GetGcType(generation)));

        _gcDuration.Record(duration.TotalMilliseconds,
            new("gc.generation", generation.ToString()),
            new("gc.type", GetGcType(generation)),
            new("duration.bucket", GetGcDurationBucket(duration)));

        if (freedBytes > 0)
        {
            _gcFreedBytes.Add(freedBytes,
                new KeyValuePair<string, object?>[] { new("gc.generation", generation.ToString()) });
        }
    }

    public void RecordThreadPoolStats(int availableWorkerThreads, int availableIoThreads, int queuedWorkItems)
    {
        // Thread pool stats are now tracked via observable gauges automatically
        // This method is kept for backward compatibility or manual recording
    }

    public void RecordHttpConnectionPoolStats(string poolName, int activeConnections, int idleConnections)
    {
        // These could be exposed as observable gauges if we have access to the connection pool objects
        // For now, this is a placeholder for future implementation
    }

    public void RecordDatabaseConnectionPoolStats(string poolName, int activeConnections, int idleConnections, int waitingRequests)
    {
        // These are tracked separately in PostgresConnectionPoolMetrics
        // This method is kept for other database types
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    private static long GetAvailableWorkerThreads()
    {
        ThreadPool.GetAvailableThreads(out var workerThreads, out _);
        return workerThreads;
    }

    private static long GetAvailableIoThreads()
    {
        ThreadPool.GetAvailableThreads(out _, out var ioThreads);
        return ioThreads;
    }

    private static long GetThreadPoolQueueLength()
    {
        return ThreadPool.PendingWorkItemCount;
    }

    private double GetCpuUsagePercent()
    {
        try
        {
            // This is a simplified CPU usage calculation
            // For more accurate CPU usage, you would need to track over time
            var totalProcessorTime = _currentProcess.TotalProcessorTime.TotalMilliseconds;
            var processUptime = (DateTime.UtcNow - _currentProcess.StartTime.ToUniversalTime()).TotalMilliseconds;

            if (processUptime > 0)
            {
                var cpuUsage = (totalProcessorTime / (Environment.ProcessorCount * processUptime)) * 100;
                return Math.Min(cpuUsage, 100.0); // Cap at 100%
            }

            return 0;
        }
        catch
        {
            return 0;
        }
    }

    private static string GetGcType(int generation)
    {
        return generation switch
        {
            0 => "gen0",
            1 => "gen1",
            2 => "gen2",
            _ => "blocking"
        };
    }

    private static string GetGcDurationBucket(TimeSpan duration)
    {
        var ms = duration.TotalMilliseconds;
        return ms switch
        {
            < 10 => "fast",
            < 50 => "normal",
            < 100 => "slow",
            < 500 => "very_slow",
            _ => "critical"
        };
    }
}

/// <summary>
/// Extension class for registering infrastructure metrics with more detailed .NET runtime metrics.
/// </summary>
public static class InfrastructureMetricsExtensions
{
    /// <summary>
    /// Creates a comprehensive set of infrastructure metrics including GC, memory, and thread pool metrics.
    /// </summary>
    public static void RegisterRuntimeMetrics(this Meter meter)
    {
        // GC collection counts by generation
        meter.CreateObservableCounter(
            "honua.infrastructure.gc_collection_count",
            () => new Measurement<long>[]
            {
                new Measurement<long>(GC.CollectionCount(0), new KeyValuePair<string, object?>[] { new("generation", "0") }),
                new Measurement<long>(GC.CollectionCount(1), new KeyValuePair<string, object?>[] { new("generation", "1") }),
                new Measurement<long>(GC.CollectionCount(2), new KeyValuePair<string, object?>[] { new("generation", "2") })
            },
            unit: "{collection}",
            description: "Total GC collection count by generation");

        // Thread pool configuration
        ThreadPool.GetMaxThreads(out var maxWorkerThreads, out var maxIoThreads);
        meter.CreateObservableGauge(
            "honua.infrastructure.threadpool_max_threads",
            () => new Measurement<int>[]
            {
                new Measurement<int>(maxWorkerThreads, new KeyValuePair<string, object?>[] { new("thread.type", "worker") }),
                new Measurement<int>(maxIoThreads, new KeyValuePair<string, object?>[] { new("thread.type", "io") })
            },
            unit: "{thread}",
            description: "Maximum thread pool threads");

        ThreadPool.GetMinThreads(out var minWorkerThreads, out var minIoThreads);
        meter.CreateObservableGauge(
            "honua.infrastructure.threadpool_min_threads",
            () => new Measurement<int>[]
            {
                new Measurement<int>(minWorkerThreads, new KeyValuePair<string, object?>[] { new("thread.type", "worker") }),
                new Measurement<int>(minIoThreads, new KeyValuePair<string, object?>[] { new("thread.type", "io") })
            },
            unit: "{thread}",
            description: "Minimum thread pool threads");
    }
}
