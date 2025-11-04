// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Resilience;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace Honua.Cli.AI.Services.Processes;

/// <summary>
/// Provides retry logic with exponential backoff for process steps.
/// Helps process steps recover from transient failures automatically.
/// Uses ResiliencePolicies.CreateRetryPolicy for consistent retry behavior.
/// </summary>
public static class ProcessStepRetryHelper
{
    /// <summary>
    /// Default retry policy configuration
    /// </summary>
    public static class DefaultConfig
    {
        public const int MaxRetries = 3;
        public const int InitialDelayMs = 1000;
        public const double BackoffMultiplier = 2.0;
    }

    /// <summary>
    /// Executes an async operation with retry logic and exponential backoff.
    /// Uses centralized ResiliencePolicies builder for consistent behavior.
    /// </summary>
    /// <typeparam name="T">The return type of the operation</typeparam>
    /// <param name="operation">The operation to execute</param>
    /// <param name="logger">Logger for retry attempts</param>
    /// <param name="operationName">Name of the operation for logging</param>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 3)</param>
    /// <param name="initialDelayMs">Initial delay in milliseconds (default: 1000ms)</param>
    /// <param name="backoffMultiplier">Backoff multiplier for exponential delay (default: 2.0)</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation</param>
    /// <returns>The result of the operation</returns>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        ILogger logger,
        string operationName,
        int maxRetries = DefaultConfig.MaxRetries,
        int initialDelayMs = DefaultConfig.InitialDelayMs,
        double backoffMultiplier = DefaultConfig.BackoffMultiplier,
        CancellationToken cancellationToken = default)
    {
        var pipeline = ResiliencePolicies.CreateRetryPolicy(
            maxRetries: maxRetries,
            initialDelay: TimeSpan.FromMilliseconds(initialDelayMs),
            logger: logger,
            shouldRetry: IsRetryableException,
            useJitter: false);

        return await pipeline.ExecuteAsync(async ct => await operation(), cancellationToken);
    }

    /// <summary>
    /// Executes an async operation with retry logic and exponential backoff (void return).
    /// Uses centralized ResiliencePolicies builder for consistent behavior.
    /// </summary>
    /// <param name="operation">The operation to execute</param>
    /// <param name="logger">Logger for retry attempts</param>
    /// <param name="operationName">Name of the operation for logging</param>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 3)</param>
    /// <param name="initialDelayMs">Initial delay in milliseconds (default: 1000ms)</param>
    /// <param name="backoffMultiplier">Backoff multiplier for exponential delay (default: 2.0)</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation</param>
    public static async Task ExecuteWithRetryAsync(
        Func<Task> operation,
        ILogger logger,
        string operationName,
        int maxRetries = DefaultConfig.MaxRetries,
        int initialDelayMs = DefaultConfig.InitialDelayMs,
        double backoffMultiplier = DefaultConfig.BackoffMultiplier,
        CancellationToken cancellationToken = default)
    {
        var pipeline = ResiliencePolicies.CreateRetryPolicy(
            maxRetries: maxRetries,
            initialDelay: TimeSpan.FromMilliseconds(initialDelayMs),
            logger: logger,
            shouldRetry: IsRetryableException,
            useJitter: false);

        await pipeline.ExecuteAsync(async ct => await operation(), cancellationToken);
    }

    /// <summary>
    /// Determines if an exception is retryable (transient failure) or non-retryable (validation error).
    /// </summary>
    /// <param name="exception">The exception to check</param>
    /// <returns>True if the exception represents a transient failure that should be retried</returns>
    public static bool IsRetryableException(Exception exception)
    {
        // Retryable: Network, timeout, temporary service issues
        if (exception is HttpRequestException ||
            exception is TimeoutException ||
            exception is TaskCanceledException ||
            exception is OperationCanceledException)
        {
            return true;
        }

        // Retryable: IO exceptions (could be transient file locks, network storage issues)
        if (exception is IOException)
        {
            return true;
        }

        // Check inner exception
        if (exception.InnerException != null)
        {
            return IsRetryableException(exception.InnerException);
        }

        // Check for specific error messages that indicate transient issues
        var message = exception.Message?.ToLowerInvariant() ?? string.Empty;
        if (message.Contains("timeout") ||
            message.Contains("connection") ||
            message.Contains("network") ||
            message.Contains("unavailable") ||
            message.Contains("throttl") ||
            message.Contains("rate limit") ||
            message.Contains("too many requests"))
        {
            return true;
        }

        // Non-retryable: Validation errors, argument errors, not found, unauthorized
        // These indicate programming errors or configuration issues that won't be fixed by retrying
        if (exception is ArgumentException ||
            exception is ArgumentNullException ||
            exception is InvalidOperationException ||
            exception is NotSupportedException ||
            exception is UnauthorizedAccessException)
        {
            return false;
        }

        // Default to retryable for unknown exceptions
        // This is a conservative approach - better to retry than fail immediately
        return true;
    }
}
