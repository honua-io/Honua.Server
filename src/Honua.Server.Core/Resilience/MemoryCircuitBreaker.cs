// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading.Tasks;
using Honua.Server.Core.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Resilience;

/// <summary>
/// Memory-based circuit breaker that rejects operations when memory usage exceeds a threshold.
/// This protects against out-of-memory conditions.
/// </summary>
public class MemoryCircuitBreaker
{
    private readonly BulkheadOptions _options;
    private readonly ILogger<MemoryCircuitBreaker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryCircuitBreaker"/> class.
    /// </summary>
    /// <param name="options">Bulkhead configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    public MemoryCircuitBreaker(
        IOptions<BulkheadOptions> options,
        ILogger<MemoryCircuitBreaker> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;

        _logger.LogInformation(
            "Memory circuit breaker initialized. Threshold: {ThresholdMB}MB",
            _options.MemoryThresholdBytes / 1_048_576);
    }

    /// <summary>
    /// Executes an operation with memory threshold protection.
    /// Checks memory usage before executing the operation.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="MemoryThresholdExceededException">
    /// Thrown when memory usage exceeds the configured threshold.
    /// </exception>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (!_options.MemoryCircuitBreakerEnabled)
        {
            // Memory circuit breaker is disabled, execute directly
            return await operation();
        }

        CheckMemoryThreshold();

        return await operation();
    }

    /// <summary>
    /// Executes a void operation with memory threshold protection.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <exception cref="MemoryThresholdExceededException">
    /// Thrown when memory usage exceeds the configured threshold.
    /// </exception>
    public async Task ExecuteAsync(Func<Task> operation)
    {
        await ExecuteAsync(async () =>
        {
            await operation();
            return 0; // Dummy return value
        });
    }

    /// <summary>
    /// Checks if the current memory usage exceeds the threshold.
    /// </summary>
    /// <exception cref="MemoryThresholdExceededException">
    /// Thrown when memory usage exceeds the configured threshold.
    /// </exception>
    private void CheckMemoryThreshold()
    {
        // Get current memory usage without forcing a full collection
        // forceFullCollection: false provides a faster, approximate value
        var memoryUsage = GC.GetTotalMemory(forceFullCollection: false);

        if (memoryUsage > _options.MemoryThresholdBytes)
        {
            _logger.LogWarning(
                "Memory threshold exceeded: {MemoryMB}MB > {ThresholdMB}MB. Rejecting operation to prevent OOM.",
                memoryUsage / 1_048_576,
                _options.MemoryThresholdBytes / 1_048_576);

            throw new MemoryThresholdExceededException(memoryUsage, _options.MemoryThresholdBytes);
        }

        // Log debug information when approaching the threshold (90%)
        var threshold90Percent = _options.MemoryThresholdBytes * 0.9;
        if (memoryUsage > threshold90Percent)
        {
            _logger.LogDebug(
                "Memory usage at {Percentage}% of threshold: {MemoryMB}MB / {ThresholdMB}MB",
                (int)((double)memoryUsage / _options.MemoryThresholdBytes * 100),
                memoryUsage / 1_048_576,
                _options.MemoryThresholdBytes / 1_048_576);
        }
    }

    /// <summary>
    /// Gets the current memory usage in bytes.
    /// </summary>
    /// <returns>Current memory usage in bytes.</returns>
    public long GetCurrentMemoryUsage()
    {
        return GC.GetTotalMemory(forceFullCollection: false);
    }

    /// <summary>
    /// Gets the current memory usage as a percentage of the threshold.
    /// </summary>
    /// <returns>Memory usage percentage (0-100+).</returns>
    public double GetMemoryUsagePercentage()
    {
        var currentMemory = GC.GetTotalMemory(forceFullCollection: false);
        return (double)currentMemory / _options.MemoryThresholdBytes * 100;
    }

    /// <summary>
    /// Checks if the current memory usage is approaching the threshold.
    /// </summary>
    /// <param name="warningPercentage">The percentage at which to warn (default: 90%).</param>
    /// <returns>True if memory usage exceeds the warning percentage.</returns>
    public bool IsApproachingThreshold(double warningPercentage = 90.0)
    {
        return GetMemoryUsagePercentage() >= warningPercentage;
    }
}
