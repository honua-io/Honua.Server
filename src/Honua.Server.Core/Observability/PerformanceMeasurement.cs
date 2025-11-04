// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Observability;

/// <summary>
/// Provides utilities for measuring and logging operation performance with consistent patterns.
/// Consolidates the repeated Stopwatch + logging patterns across the codebase.
/// </summary>
/// <remarks>
/// This utility helps eliminate code duplication by providing standard patterns for:
/// <list type="bullet">
///   <item>Async operation measurement with automatic logging</item>
///   <item>Synchronous operation measurement with using blocks</item>
///   <item>Custom result callbacks for metric recording</item>
///   <item>Exception-safe timing (measures even when operation throws)</item>
/// </list>
///
/// <para><b>Examples:</b></para>
///
/// <para>Basic async measurement with logging:</para>
/// <code>
/// var result = await PerformanceMeasurement.MeasureAsync(
///     logger,
///     "LoadMetadata",
///     async () => await LoadMetadataFromDiskAsync(),
///     LogLevel.Information);
/// // Logs: "LoadMetadata completed in 245ms"
/// </code>
///
/// <para>Measurement with custom callback (e.g., for metrics):</para>
/// <code>
/// var result = await PerformanceMeasurement.MeasureAsync(
///     "QueryDatabase",
///     async () => await ExecuteQueryAsync(),
///     (duration, recordCount) =>
///     {
///         _metrics.RecordQueryDuration(duration.TotalMilliseconds);
///         _metrics.RecordQueryResults(recordCount);
///     });
/// </code>
///
/// <para>Using block for synchronous operations:</para>
/// <code>
/// using (PerformanceMeasurement.Measure(logger, "ProcessBatch"))
/// {
///     // Your code here
///     ProcessBatchOfRecords();
/// }
/// // Automatically logs duration when disposed
/// </code>
///
/// <para>Getting duration for manual handling:</para>
/// <code>
/// var (result, duration) = await PerformanceMeasurement.MeasureWithDurationAsync(
///     async () => await ComputeStatisticsAsync());
///
/// if (duration.TotalSeconds > 5)
/// {
///     logger.LogWarning("Statistics computation took {Duration}s", duration.TotalSeconds);
/// }
/// </code>
/// </remarks>
public static class PerformanceMeasurement
{
    /// <summary>
    /// Measures an async operation and logs the duration automatically.
    /// Logs even if the operation throws an exception (with failure indication).
    /// </summary>
    /// <typeparam name="T">The result type of the operation.</typeparam>
    /// <param name="logger">The logger for recording timing information. If null, no logging occurs but timing still happens.</param>
    /// <param name="operationName">Human-readable name for the operation (e.g., "LoadConfiguration").</param>
    /// <param name="operation">The async operation to measure.</param>
    /// <param name="logLevel">The log level to use (default: Debug).</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when operationName or operation is null.</exception>
    /// <remarks>
    /// Uses high-resolution Stopwatch for accurate timing.
    /// Logs format: "{operationName} completed in {duration}ms" (success)
    /// or "{operationName} failed after {duration}ms" (exception).
    /// If logger is null, the operation is still measured but no logging occurs.
    /// </remarks>
    /// <example>
    /// <code>
    /// var metadata = await PerformanceMeasurement.MeasureAsync(
    ///     _logger,
    ///     "GetMetadataSnapshot",
    ///     async () => await _registry.GetSnapshotAsync(),
    ///     LogLevel.Information);
    /// </code>
    /// </example>
    public static async Task<T> MeasureAsync<T>(
        ILogger? logger,
        string operationName,
        Func<Task<T>> operation,
        LogLevel logLevel = LogLevel.Debug)
    {
        if (string.IsNullOrWhiteSpace(operationName))
            throw new ArgumentNullException(nameof(operationName));
        if (operation is null)
            throw new ArgumentNullException(nameof(operation));

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await operation().ConfigureAwait(false);
            stopwatch.Stop();

            if (logger is not null && logger.IsEnabled(logLevel))
            {
                logger.Log(logLevel, "{OperationName} completed in {DurationMs}ms",
                    operationName, stopwatch.ElapsedMilliseconds);
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Log failure with elapsed time at Warning level (override configured level for failures)
            logger?.LogWarning(ex, "{OperationName} failed after {DurationMs}ms",
                operationName, stopwatch.ElapsedMilliseconds);

            throw;
        }
    }

    /// <summary>
    /// Measures an async operation (no return value) and logs the duration automatically.
    /// </summary>
    /// <param name="logger">The logger for recording timing information. If null, no logging occurs but timing still happens.</param>
    /// <param name="operationName">Human-readable name for the operation.</param>
    /// <param name="operation">The async operation to measure.</param>
    /// <param name="logLevel">The log level to use (default: Debug).</param>
    /// <exception cref="ArgumentNullException">Thrown when operationName or operation is null.</exception>
    public static async Task MeasureAsync(
        ILogger? logger,
        string operationName,
        Func<Task> operation,
        LogLevel logLevel = LogLevel.Debug)
    {
        if (string.IsNullOrWhiteSpace(operationName))
            throw new ArgumentNullException(nameof(operationName));
        if (operation is null)
            throw new ArgumentNullException(nameof(operation));

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await operation().ConfigureAwait(false);
            stopwatch.Stop();

            if (logger is not null && logger.IsEnabled(logLevel))
            {
                logger.Log(logLevel, "{OperationName} completed in {DurationMs}ms",
                    operationName, stopwatch.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger?.LogWarning(ex, "{OperationName} failed after {DurationMs}ms",
                operationName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// Measures an async operation and invokes a callback with the duration and result.
    /// Useful for recording metrics or custom logging based on both timing and result.
    /// </summary>
    /// <typeparam name="T">The result type of the operation.</typeparam>
    /// <param name="operationName">Human-readable name for the operation (for exceptions).</param>
    /// <param name="operation">The async operation to measure.</param>
    /// <param name="onCompleted">Callback invoked with duration and result on success.</param>
    /// <param name="onFailed">Optional callback invoked with duration and exception on failure.</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when operationName, operation, or onCompleted is null.</exception>
    /// <remarks>
    /// The onCompleted callback is only invoked on success.
    /// The onFailed callback (if provided) is invoked on exceptions before rethrowing.
    /// </remarks>
    /// <example>
    /// <code>
    /// var count = await PerformanceMeasurement.MeasureAsync(
    ///     "CountRecords",
    ///     async () => await CountRecordsAsync(),
    ///     (duration, result) =>
    ///     {
    ///         _metrics.RecordOperationDuration("count", duration.TotalMilliseconds);
    ///         _metrics.RecordRecordCount(result);
    ///     },
    ///     (duration, ex) =>
    ///     {
    ///         _metrics.RecordOperationError("count", ex.GetType().Name);
    ///     });
    /// </code>
    /// </example>
    public static async Task<T> MeasureAsync<T>(
        string operationName,
        Func<Task<T>> operation,
        Action<TimeSpan, T> onCompleted,
        Action<TimeSpan, Exception>? onFailed = null)
    {
        if (string.IsNullOrWhiteSpace(operationName))
            throw new ArgumentNullException(nameof(operationName));
        if (operation is null)
            throw new ArgumentNullException(nameof(operation));
        if (onCompleted is null)
            throw new ArgumentNullException(nameof(onCompleted));

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await operation().ConfigureAwait(false);
            stopwatch.Stop();

            onCompleted(stopwatch.Elapsed, result);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            onFailed?.Invoke(stopwatch.Elapsed, ex);
            throw;
        }
    }

    /// <summary>
    /// Measures an async operation (no return value) and invokes a callback with the duration.
    /// </summary>
    /// <param name="operationName">Human-readable name for the operation (for exceptions).</param>
    /// <param name="operation">The async operation to measure.</param>
    /// <param name="onCompleted">Callback invoked with duration on success.</param>
    /// <param name="onFailed">Optional callback invoked with duration and exception on failure.</param>
    /// <exception cref="ArgumentNullException">Thrown when operationName, operation, or onCompleted is null.</exception>
    public static async Task MeasureAsync(
        string operationName,
        Func<Task> operation,
        Action<TimeSpan> onCompleted,
        Action<TimeSpan, Exception>? onFailed = null)
    {
        if (string.IsNullOrWhiteSpace(operationName))
            throw new ArgumentNullException(nameof(operationName));
        if (operation is null)
            throw new ArgumentNullException(nameof(operation));
        if (onCompleted is null)
            throw new ArgumentNullException(nameof(onCompleted));

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await operation().ConfigureAwait(false);
            stopwatch.Stop();

            onCompleted(stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            onFailed?.Invoke(stopwatch.Elapsed, ex);
            throw;
        }
    }

    /// <summary>
    /// Measures an async operation and returns both the result and the elapsed duration.
    /// Useful when you need the duration for conditional logic or custom handling.
    /// </summary>
    /// <typeparam name="T">The result type of the operation.</typeparam>
    /// <param name="operation">The async operation to measure.</param>
    /// <returns>A tuple containing the operation result and the elapsed duration.</returns>
    /// <exception cref="ArgumentNullException">Thrown when operation is null.</exception>
    /// <remarks>
    /// This overload does not log automatically - caller is responsible for logging/metrics.
    /// </remarks>
    /// <example>
    /// <code>
    /// var (records, duration) = await PerformanceMeasurement.MeasureWithDurationAsync(
    ///     async () => await FetchRecordsAsync());
    ///
    /// _logger.LogInformation("Fetched {Count} records in {Duration}ms",
    ///     records.Count, duration.TotalMilliseconds);
    /// </code>
    /// </example>
    public static async Task<(T Result, TimeSpan Duration)> MeasureWithDurationAsync<T>(
        Func<Task<T>> operation)
    {
        if (operation is null)
            throw new ArgumentNullException(nameof(operation));

        var stopwatch = Stopwatch.StartNew();
        var result = await operation().ConfigureAwait(false);
        stopwatch.Stop();

        return (result, stopwatch.Elapsed);
    }

    /// <summary>
    /// Measures an async operation (no return value) and returns the elapsed duration.
    /// </summary>
    /// <param name="operation">The async operation to measure.</param>
    /// <returns>The elapsed duration.</returns>
    /// <exception cref="ArgumentNullException">Thrown when operation is null.</exception>
    public static async Task<TimeSpan> MeasureWithDurationAsync(Func<Task> operation)
    {
        if (operation is null)
            throw new ArgumentNullException(nameof(operation));

        var stopwatch = Stopwatch.StartNew();
        await operation().ConfigureAwait(false);
        stopwatch.Stop();

        return stopwatch.Elapsed;
    }

    /// <summary>
    /// Creates a disposable measurement scope for use with 'using' blocks.
    /// Automatically logs the elapsed time when disposed.
    /// </summary>
    /// <param name="logger">The logger for recording timing information. If null, no logging occurs but timing still happens.</param>
    /// <param name="operationName">Human-readable name for the operation.</param>
    /// <param name="logLevel">The log level to use (default: Debug).</param>
    /// <returns>A disposable scope that measures elapsed time.</returns>
    /// <exception cref="ArgumentNullException">Thrown when operationName is null.</exception>
    /// <remarks>
    /// Best used for synchronous code blocks or when you don't want to wrap in a lambda.
    /// Access the elapsed time via the Elapsed property before disposal for conditional logging.
    /// If logger is null, the operation is still measured but no logging occurs on disposal.
    /// </remarks>
    /// <example>
    /// <code>
    /// using (var measurement = PerformanceMeasurement.Measure(_logger, "BatchProcessing"))
    /// {
    ///     foreach (var item in items)
    ///     {
    ///         ProcessItem(item);
    ///     }
    ///
    ///     // Can check elapsed time before disposal
    ///     if (measurement.Elapsed.TotalSeconds > 10)
    ///     {
    ///         _logger.LogWarning("Batch processing is taking longer than expected");
    ///     }
    /// }
    /// // Logs: "BatchProcessing completed in 1234ms"
    /// </code>
    /// </example>
    public static PerformanceMeasurementScope Measure(
        ILogger? logger,
        string operationName,
        LogLevel logLevel = LogLevel.Debug)
    {
        if (string.IsNullOrWhiteSpace(operationName))
            throw new ArgumentNullException(nameof(operationName));

        return new PerformanceMeasurementScope(logger, operationName, logLevel);
    }

    /// <summary>
    /// Creates a disposable measurement scope that invokes a callback with the elapsed time on disposal.
    /// </summary>
    /// <param name="operationName">Human-readable name for the operation (for exceptions).</param>
    /// <param name="onCompleted">Callback invoked with elapsed time when disposed.</param>
    /// <returns>A disposable scope that measures elapsed time.</returns>
    /// <exception cref="ArgumentNullException">Thrown when operationName or onCompleted is null.</exception>
    /// <example>
    /// <code>
    /// using (PerformanceMeasurement.Measure("BulkInsert",
    ///     duration => _metrics.RecordBulkInsertDuration(duration.TotalMilliseconds)))
    /// {
    ///     await InsertBulkRecordsAsync();
    /// }
    /// </code>
    /// </example>
    public static PerformanceMeasurementScope Measure(
        string operationName,
        Action<TimeSpan> onCompleted)
    {
        if (string.IsNullOrWhiteSpace(operationName))
            throw new ArgumentNullException(nameof(operationName));
        if (onCompleted is null)
            throw new ArgumentNullException(nameof(onCompleted));

        return new PerformanceMeasurementScope(operationName, onCompleted);
    }
}

/// <summary>
/// Disposable scope for measuring operation duration with automatic logging or callbacks.
/// Use with 'using' blocks for clean, automatic measurement of code blocks.
/// </summary>
/// <remarks>
/// This class is not thread-safe. Each instance should be used on a single thread.
/// The stopwatch is started on construction and stopped on disposal.
/// </remarks>
public sealed class PerformanceMeasurementScope : IDisposable
{
    private readonly Stopwatch _stopwatch;
    private readonly ILogger? _logger;
    private readonly string _operationName;
    private readonly LogLevel _logLevel;
    private readonly Action<TimeSpan>? _onCompleted;
    private bool _disposed;

    /// <summary>
    /// Gets the elapsed time. Can be accessed before disposal for conditional logic.
    /// </summary>
    public TimeSpan Elapsed => _stopwatch.Elapsed;

    /// <summary>
    /// Gets the elapsed milliseconds as a convenience property.
    /// </summary>
    public long ElapsedMilliseconds => _stopwatch.ElapsedMilliseconds;

    // Constructor for logger-based measurement
    internal PerformanceMeasurementScope(ILogger? logger, string operationName, LogLevel logLevel)
    {
        _logger = logger;
        _operationName = operationName;
        _logLevel = logLevel;
        _stopwatch = Stopwatch.StartNew();
    }

    // Constructor for callback-based measurement
    internal PerformanceMeasurementScope(string operationName, Action<TimeSpan> onCompleted)
    {
        _operationName = operationName;
        _onCompleted = onCompleted;
        _stopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    /// Stops the measurement and logs the elapsed time (if logger was provided)
    /// or invokes the callback (if callback was provided).
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _stopwatch.Stop();

        if (_logger is not null)
        {
            if (_logger.IsEnabled(_logLevel))
            {
                _logger.Log(_logLevel, "{OperationName} completed in {DurationMs}ms",
                    _operationName, _stopwatch.ElapsedMilliseconds);
            }
        }
        else if (_onCompleted is not null)
        {
            _onCompleted(_stopwatch.Elapsed);
        }

        _disposed = true;
    }
}
