// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace Honua.Server.Core.Resilience;

/// <summary>
/// Provides retry logic with exponential backoff for background services and hosted services.
/// Handles transient failures gracefully without bringing down the entire service.
/// </summary>
public sealed class BackgroundServiceRetryHelper
{
    private readonly ILogger _logger;
    private readonly ResiliencePipeline _retryPipeline;

    /// <summary>
    /// Creates a new instance of the BackgroundServiceRetryHelper.
    /// </summary>
    /// <param name="logger">Logger instance for the background service.</param>
    /// <param name="maxRetries">Maximum number of retry attempts (default: unlimited for background services).</param>
    /// <param name="initialDelay">Initial delay before first retry (default: 1 second).</param>
    /// <param name="maxDelay">Maximum delay between retries (default: 5 minutes).</param>
    public BackgroundServiceRetryHelper(
        ILogger logger,
        int maxRetries = int.MaxValue,
        TimeSpan? initialDelay = null,
        TimeSpan? maxDelay = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var delay = initialDelay ?? TimeSpan.FromSeconds(1);
        var maximumDelay = maxDelay ?? TimeSpan.FromMinutes(5);

        _retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetries,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                MaxDelay = maximumDelay,
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => !(ex is OperationCanceledException)), // Don't retry on cancellation
                OnRetry = args =>
                {
                    var delay = args.RetryDelay;
                    _logger.LogWarning(
                        args.Outcome.Exception,
                        "Background service operation failed (attempt {Attempt}). Retrying after {DelaySeconds}s. Exception: {Exception}",
                        args.AttemptNumber + 1,
                        delay.TotalSeconds,
                        args.Outcome.Exception?.GetType().Name ?? "Unknown");
                    return ValueTask.CompletedTask;
                },
                DelayGenerator = args =>
                {
                    // Exponential backoff: 1s, 2s, 4s, 8s, 16s, 32s, 64s, ... up to maxDelay
                    var exponentialDelay = TimeSpan.FromSeconds(
                        Math.Min(
                            Math.Pow(2, args.AttemptNumber) * delay.TotalSeconds,
                            maximumDelay.TotalSeconds));

                    return new ValueTask<TimeSpan?>(exponentialDelay);
                }
            })
            .Build();
    }

    /// <summary>
    /// Executes a background operation with retry logic.
    /// Will keep retrying until successful or cancellation is requested.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="operationName">Name of the operation for logging.</param>
    /// <param name="cancellationToken">Cancellation token to stop retrying.</param>
    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        try
        {
            await _retryPipeline.ExecuteAsync(
                async ct =>
                {
                    _logger.LogDebug("Executing background operation '{OperationName}'", operationName);
                    await operation(ct);
                },
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Background operation '{OperationName}' completed successfully", operationName);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Background operation '{OperationName}' was cancelled", operationName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background operation '{OperationName}' failed after all retries", operationName);
            throw;
        }
    }

    /// <summary>
    /// Executes a background operation with retry logic and returns a result.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="operationName">Name of the operation for logging.</param>
    /// <param name="cancellationToken">Cancellation token to stop retrying.</param>
    /// <returns>The result of the operation.</returns>
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        try
        {
            var result = await _retryPipeline.ExecuteAsync(
                async ct =>
                {
                    _logger.LogDebug("Executing background operation '{OperationName}'", operationName);
                    return await operation(ct);
                },
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Background operation '{OperationName}' completed successfully", operationName);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Background operation '{OperationName}' was cancelled", operationName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background operation '{OperationName}' failed after all retries", operationName);
            throw;
        }
    }

    /// <summary>
    /// Executes a periodic background operation (like a polling loop) with retry logic.
    /// Each iteration is retried independently.
    /// </summary>
    /// <param name="operation">The operation to execute periodically.</param>
    /// <param name="interval">Time to wait between successful executions.</param>
    /// <param name="operationName">Name of the operation for logging.</param>
    /// <param name="cancellationToken">Cancellation token to stop the loop.</param>
    public async Task ExecutePeriodicAsync(
        Func<CancellationToken, Task> operation,
        TimeSpan interval,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        _logger.LogInformation(
            "Starting periodic background operation '{OperationName}' with interval {IntervalSeconds}s",
            operationName,
            interval.TotalSeconds);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ExecuteAsync(operation, operationName, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Periodic background operation '{OperationName}' was cancelled", operationName);
                throw;
            }
            catch (Exception ex)
            {
                // Log but continue the loop - don't let one failure bring down the background service
                _logger.LogError(
                    ex,
                    "Periodic background operation '{OperationName}' failed. Will retry after {IntervalSeconds}s",
                    operationName,
                    interval.TotalSeconds);
            }

            // Wait for the interval before next iteration
            try
            {
                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Periodic background operation '{OperationName}' was cancelled during delay", operationName);
                throw;
            }
        }
    }
}
