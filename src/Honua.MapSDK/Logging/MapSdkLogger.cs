using Microsoft.Extensions.Logging;

namespace Honua.MapSDK.Logging;

/// <summary>
/// Structured logger for MapSDK with performance tracking and tracing.
/// </summary>
public class MapSdkLogger : IDisposable
{
    private readonly ILogger<MapSdkLogger> _logger;
    private readonly bool _enablePerformanceTracking;
    private readonly Dictionary<string, PerformanceMetric> _metrics = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MapSdkLogger"/> class.
    /// </summary>
    /// <param name="logger">The underlying logger.</param>
    /// <param name="enablePerformanceTracking">Whether to enable performance tracking.</param>
    public MapSdkLogger(ILogger<MapSdkLogger> logger, bool enablePerformanceTracking = false)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _enablePerformanceTracking = enablePerformanceTracking;
    }

    /// <summary>
    /// Logs a debug message.
    /// </summary>
    /// <param name="message">Message to log.</param>
    /// <param name="args">Message arguments.</param>
    public void Debug(string message, params object[] args)
    {
        _logger.LogDebug(message, args);
    }

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    /// <param name="message">Message to log.</param>
    /// <param name="args">Message arguments.</param>
    public void Info(string message, params object[] args)
    {
        _logger.LogInformation(message, args);
    }

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    /// <param name="message">Message to log.</param>
    /// <param name="args">Message arguments.</param>
    public void Warning(string message, params object[] args)
    {
        _logger.LogWarning(message, args);
    }

    /// <summary>
    /// Logs an error message.
    /// </summary>
    /// <param name="message">Message to log.</param>
    /// <param name="exception">Exception to log.</param>
    /// <param name="args">Message arguments.</param>
    public void Error(string message, Exception? exception = null, params object[] args)
    {
        _logger.LogError(exception, message, args);
    }

    /// <summary>
    /// Logs a critical error message.
    /// </summary>
    /// <param name="message">Message to log.</param>
    /// <param name="exception">Exception to log.</param>
    /// <param name="args">Message arguments.</param>
    public void Critical(string message, Exception? exception = null, params object[] args)
    {
        _logger.LogCritical(exception, message, args);
    }

    /// <summary>
    /// Logs a ComponentBus message.
    /// </summary>
    /// <param name="messageType">Type of message.</param>
    /// <param name="sender">Sender of the message.</param>
    public void LogComponentBusMessage(string messageType, string sender)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("[ComponentBus] {MessageType} from {Sender}", messageType, sender);
        }
    }

    /// <summary>
    /// Logs a data loading operation.
    /// </summary>
    /// <param name="url">URL being loaded.</param>
    /// <param name="durationMs">Duration in milliseconds.</param>
    /// <param name="success">Whether the operation succeeded.</param>
    public void LogDataLoad(string url, long durationMs, bool success)
    {
        if (success)
        {
            _logger.LogInformation("[DataLoad] Loaded {Url} in {Duration}ms", url, durationMs);
        }
        else
        {
            _logger.LogWarning("[DataLoad] Failed to load {Url} after {Duration}ms", url, durationMs);
        }

        if (_enablePerformanceTracking)
        {
            TrackMetric("DataLoad", durationMs);
        }
    }

    /// <summary>
    /// Logs a component render operation.
    /// </summary>
    /// <param name="componentName">Name of the component.</param>
    /// <param name="durationMs">Render duration in milliseconds.</param>
    public void LogComponentRender(string componentName, long durationMs)
    {
        _logger.LogDebug("[Render] {Component} rendered in {Duration}ms", componentName, durationMs);

        if (_enablePerformanceTracking)
        {
            TrackMetric($"Render:{componentName}", durationMs);
        }
    }

    /// <summary>
    /// Logs a user action.
    /// </summary>
    /// <param name="action">Action name.</param>
    /// <param name="component">Component name.</param>
    /// <param name="details">Additional details.</param>
    public void LogUserAction(string action, string component, string? details = null)
    {
        if (details != null)
        {
            _logger.LogInformation("[UserAction] {Action} on {Component}: {Details}", action, component, details);
        }
        else
        {
            _logger.LogInformation("[UserAction] {Action} on {Component}", action, component);
        }
    }

    /// <summary>
    /// Logs a filter application.
    /// </summary>
    /// <param name="filterCount">Number of filters applied.</param>
    /// <param name="resultCount">Number of results after filtering.</param>
    /// <param name="durationMs">Filter duration in milliseconds.</param>
    public void LogFilterApplication(int filterCount, int resultCount, long durationMs)
    {
        _logger.LogInformation("[Filter] Applied {FilterCount} filters, {ResultCount} results in {Duration}ms",
            filterCount, resultCount, durationMs);

        if (_enablePerformanceTracking)
        {
            TrackMetric("FilterApplication", durationMs);
        }
    }

    /// <summary>
    /// Starts a performance measurement.
    /// </summary>
    /// <param name="operationName">Name of the operation.</param>
    /// <returns>Disposable that stops the measurement when disposed.</returns>
    public IDisposable? MeasurePerformance(string operationName)
    {
        if (!_enablePerformanceTracking)
            return null;

        return new PerformanceMeasurement(this, operationName);
    }

    /// <summary>
    /// Gets performance metrics.
    /// </summary>
    /// <returns>Dictionary of operation name to performance metric.</returns>
    public IReadOnlyDictionary<string, PerformanceMetric> GetMetrics()
    {
        return _metrics;
    }

    /// <summary>
    /// Tracks a performance metric.
    /// </summary>
    private void TrackMetric(string operationName, long durationMs)
    {
        if (!_metrics.TryGetValue(operationName, out var metric))
        {
            metric = new PerformanceMetric { OperationName = operationName };
            _metrics[operationName] = metric;
        }

        metric.Count++;
        metric.TotalMs += durationMs;
        metric.MinMs = Math.Min(metric.MinMs, durationMs);
        metric.MaxMs = Math.Max(metric.MaxMs, durationMs);
        metric.AvgMs = metric.TotalMs / metric.Count;
        metric.LastMs = durationMs;
    }

    /// <summary>
    /// Disposes the logger and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        if (_enablePerformanceTracking && _metrics.Any())
        {
            _logger.LogInformation("[Performance] Metrics summary:");
            foreach (var metric in _metrics.Values.OrderByDescending(m => m.TotalMs))
            {
                _logger.LogInformation("  {Operation}: {Count} calls, avg={AvgMs}ms, min={MinMs}ms, max={MaxMs}ms, total={TotalMs}ms",
                    metric.OperationName, metric.Count, metric.AvgMs, metric.MinMs, metric.MaxMs, metric.TotalMs);
            }
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performance measurement helper.
    /// </summary>
    private class PerformanceMeasurement : IDisposable
    {
        private readonly MapSdkLogger _logger;
        private readonly string _operationName;
        private readonly System.Diagnostics.Stopwatch _stopwatch;

        public PerformanceMeasurement(MapSdkLogger logger, string operationName)
        {
            _logger = logger;
            _operationName = operationName;
            _stopwatch = System.Diagnostics.Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _logger.TrackMetric(_operationName, _stopwatch.ElapsedMilliseconds);
        }
    }
}

/// <summary>
/// Performance metric data.
/// </summary>
public class PerformanceMetric
{
    /// <summary>
    /// Gets or sets the operation name.
    /// </summary>
    public string OperationName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of times the operation was called.
    /// </summary>
    public long Count { get; set; }

    /// <summary>
    /// Gets or sets the total duration in milliseconds.
    /// </summary>
    public long TotalMs { get; set; }

    /// <summary>
    /// Gets or sets the minimum duration in milliseconds.
    /// </summary>
    public long MinMs { get; set; } = long.MaxValue;

    /// <summary>
    /// Gets or sets the maximum duration in milliseconds.
    /// </summary>
    public long MaxMs { get; set; }

    /// <summary>
    /// Gets or sets the average duration in milliseconds.
    /// </summary>
    public long AvgMs { get; set; }

    /// <summary>
    /// Gets or sets the last measured duration in milliseconds.
    /// </summary>
    public long LastMs { get; set; }
}
