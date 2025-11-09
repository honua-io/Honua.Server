using System.Diagnostics;
using Honua.MapSDK.Logging;

namespace Honua.MapSDK.Services.Performance;

/// <summary>
/// Monitors performance metrics for MapSDK components.
/// </summary>
public class PerformanceMonitor : IDisposable
{
    private readonly MapSdkLogger _logger;
    private readonly bool _enabled;
    private readonly Dictionary<string, List<long>> _measurements = new();
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PerformanceMonitor"/> class.
    /// </summary>
    /// <param name="logger">Logger for recording metrics.</param>
    /// <param name="enabled">Whether monitoring is enabled.</param>
    public PerformanceMonitor(MapSdkLogger logger, bool enabled = true)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _enabled = enabled;
    }

    /// <summary>
    /// Starts measuring an operation.
    /// </summary>
    /// <param name="operationName">Name of the operation to measure.</param>
    /// <returns>Disposable that stops measurement when disposed.</returns>
    public IDisposable? Measure(string operationName)
    {
        if (!_enabled)
            return null;

        return new PerformanceMeasurement(this, operationName);
    }

    /// <summary>
    /// Measures the execution time of an async operation.
    /// </summary>
    /// <typeparam name="T">Return type of the operation.</typeparam>
    /// <param name="operationName">Name of the operation.</param>
    /// <param name="operation">Operation to measure.</param>
    /// <returns>Result of the operation.</returns>
    public async Task<T> MeasureAsync<T>(string operationName, Func<Task<T>> operation)
    {
        if (!_enabled)
            return await operation();

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await operation();
            sw.Stop();
            RecordMeasurement(operationName, sw.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.Error($"Error in {operationName}", ex);
            throw;
        }
    }

    /// <summary>
    /// Measures the execution time and memory usage of an interop operation.
    /// Optimized for tracking Blazor-JavaScript interop performance.
    /// </summary>
    /// <typeparam name="T">Return type of the operation.</typeparam>
    /// <param name="operationName">Name of the interop operation.</param>
    /// <param name="operation">Interop operation to measure.</param>
    /// <returns>Result of the operation.</returns>
    public async Task<T> MeasureInteropAsync<T>(string operationName, Func<Task<T>> operation)
    {
        if (!_enabled)
            return await operation();

        var sw = Stopwatch.StartNew();
        var memBefore = GC.GetTotalMemory(false);

        try
        {
            var result = await operation();
            sw.Stop();

            var memAfter = GC.GetTotalMemory(false);
            var memDelta = (memAfter - memBefore) / 1024.0 / 1024.0; // MB

            RecordMeasurement(operationName, sw.ElapsedMilliseconds);

            _logger.Info(
                $"Interop {operationName}: {sw.ElapsedMilliseconds}ms, Memory: {memDelta:F2}MB"
            );

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.Error($"Interop {operationName} failed", ex);
            throw;
        }
    }

    /// <summary>
    /// Measures the execution time and memory usage of a void interop operation.
    /// Optimized for tracking Blazor-JavaScript interop performance.
    /// </summary>
    /// <param name="operationName">Name of the interop operation.</param>
    /// <param name="operation">Interop operation to measure.</param>
    public async Task MeasureInteropAsync(string operationName, Func<Task> operation)
    {
        if (!_enabled)
        {
            await operation();
            return;
        }

        var sw = Stopwatch.StartNew();
        var memBefore = GC.GetTotalMemory(false);

        try
        {
            await operation();
            sw.Stop();

            var memAfter = GC.GetTotalMemory(false);
            var memDelta = (memAfter - memBefore) / 1024.0 / 1024.0; // MB

            RecordMeasurement(operationName, sw.ElapsedMilliseconds);

            _logger.Info(
                $"Interop {operationName}: {sw.ElapsedMilliseconds}ms, Memory: {memDelta:F2}MB"
            );
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.Error($"Interop {operationName} failed", ex);
            throw;
        }
    }

    /// <summary>
    /// Measures the execution time of a synchronous operation.
    /// </summary>
    /// <typeparam name="T">Return type of the operation.</typeparam>
    /// <param name="operationName">Name of the operation.</param>
    /// <param name="operation">Operation to measure.</param>
    /// <returns>Result of the operation.</returns>
    public T Measure<T>(string operationName, Func<T> operation)
    {
        if (!_enabled)
            return operation();

        var sw = Stopwatch.StartNew();
        try
        {
            var result = operation();
            sw.Stop();
            RecordMeasurement(operationName, sw.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.Error($"Error in {operationName}", ex);
            throw;
        }
    }

    /// <summary>
    /// Records a performance measurement.
    /// </summary>
    /// <param name="operationName">Name of the operation.</param>
    /// <param name="durationMs">Duration in milliseconds.</param>
    public void RecordMeasurement(string operationName, long durationMs)
    {
        if (!_enabled)
            return;

        lock (_lock)
        {
            if (!_measurements.ContainsKey(operationName))
            {
                _measurements[operationName] = new List<long>();
            }

            _measurements[operationName].Add(durationMs);
        }

        _logger.LogComponentRender(operationName, durationMs);
    }

    /// <summary>
    /// Gets performance statistics for an operation.
    /// </summary>
    /// <param name="operationName">Name of the operation.</param>
    /// <returns>Performance statistics, or null if no measurements exist.</returns>
    public PerformanceStatistics? GetStatistics(string operationName)
    {
        lock (_lock)
        {
            if (!_measurements.TryGetValue(operationName, out var measurements) || !measurements.Any())
            {
                return null;
            }

            return CalculateStatistics(operationName, measurements);
        }
    }

    /// <summary>
    /// Gets performance statistics for all operations.
    /// </summary>
    /// <returns>Dictionary of operation name to statistics.</returns>
    public Dictionary<string, PerformanceStatistics> GetAllStatistics()
    {
        lock (_lock)
        {
            return _measurements.ToDictionary(
                kvp => kvp.Key,
                kvp => CalculateStatistics(kvp.Key, kvp.Value)
            );
        }
    }

    /// <summary>
    /// Clears all performance measurements.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _measurements.Clear();
        }
    }

    /// <summary>
    /// Calculates statistics from measurements.
    /// </summary>
    private static PerformanceStatistics CalculateStatistics(string operationName, List<long> measurements)
    {
        var sorted = measurements.OrderBy(m => m).ToList();

        return new PerformanceStatistics
        {
            OperationName = operationName,
            Count = measurements.Count,
            Min = sorted.First(),
            Max = sorted.Last(),
            Average = (long)measurements.Average(),
            Median = sorted[sorted.Count / 2],
            P95 = sorted[(int)(sorted.Count * 0.95)],
            P99 = sorted[(int)(sorted.Count * 0.99)],
            Total = measurements.Sum()
        };
    }

    /// <summary>
    /// Logs a performance report.
    /// </summary>
    public void LogReport()
    {
        var stats = GetAllStatistics();
        if (!stats.Any())
        {
            _logger.Info("No performance measurements recorded");
            return;
        }

        _logger.Info("=== Performance Report ===");
        foreach (var stat in stats.Values.OrderByDescending(s => s.Total))
        {
            _logger.Info($"{stat.OperationName}:");
            _logger.Info($"  Count: {stat.Count}");
            _logger.Info($"  Avg: {stat.Average}ms, Median: {stat.Median}ms");
            _logger.Info($"  Min: {stat.Min}ms, Max: {stat.Max}ms");
            _logger.Info($"  P95: {stat.P95}ms, P99: {stat.P99}ms");
            _logger.Info($"  Total: {stat.Total}ms");
        }
    }

    /// <summary>
    /// Disposes the performance monitor.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        if (_enabled)
        {
            LogReport();
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performance measurement helper.
    /// </summary>
    private class PerformanceMeasurement : IDisposable
    {
        private readonly PerformanceMonitor _monitor;
        private readonly string _operationName;
        private readonly Stopwatch _stopwatch;

        public PerformanceMeasurement(PerformanceMonitor monitor, string operationName)
        {
            _monitor = monitor;
            _operationName = operationName;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _monitor.RecordMeasurement(_operationName, _stopwatch.ElapsedMilliseconds);
        }
    }
}

/// <summary>
/// Performance statistics for an operation.
/// </summary>
public class PerformanceStatistics
{
    /// <summary>
    /// Gets or sets the operation name.
    /// </summary>
    public string OperationName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of measurements.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the minimum duration in milliseconds.
    /// </summary>
    public long Min { get; set; }

    /// <summary>
    /// Gets or sets the maximum duration in milliseconds.
    /// </summary>
    public long Max { get; set; }

    /// <summary>
    /// Gets or sets the average duration in milliseconds.
    /// </summary>
    public long Average { get; set; }

    /// <summary>
    /// Gets or sets the median duration in milliseconds.
    /// </summary>
    public long Median { get; set; }

    /// <summary>
    /// Gets or sets the 95th percentile duration in milliseconds.
    /// </summary>
    public long P95 { get; set; }

    /// <summary>
    /// Gets or sets the 99th percentile duration in milliseconds.
    /// </summary>
    public long P99 { get; set; }

    /// <summary>
    /// Gets or sets the total duration in milliseconds.
    /// </summary>
    public long Total { get; set; }
}
