// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Data.Common;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using Honua.Server.Core.Observability;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Hedging;
using Polly.Retry;
using Polly.Timeout;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Resilience;

/// <summary>
/// Resilience policies for external dependencies using Polly.
/// Provides circuit breakers, retries, and timeouts for HTTP clients and database operations.
/// </summary>
public static class ResiliencePolicies
{
    /// <summary>
    /// Creates a resilience pipeline for HTTP clients accessing cloud storage (S3, Azure Blob).
    /// Includes retry, circuit breaker, and timeout policies.
    /// </summary>
    public static ResiliencePipeline<HttpResponseMessage> CreateCloudStoragePolicy(ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Resilience.CloudStorage");

        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            // Timeout: 30 seconds per request
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(30),
                OnTimeout = args =>
                {
                    logger.LogWarning("Cloud storage request timed out after {Timeout}s", args.Timeout.TotalSeconds);
                    return default;
                }
            })
            // Retry: 3 attempts with exponential backoff
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(r => r.StatusCode >= HttpStatusCode.InternalServerError) // 5xx errors
                    .HandleResult(r => r.StatusCode == HttpStatusCode.RequestTimeout)      // 408
                    .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)     // 429
                    // Handle transient network exceptions
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>()
                    .Handle<SocketException>()
                    .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested) // Timeout, not user cancellation
                    .Handle<OperationCanceledException>(ex => ex.InnerException is TimeoutException),
                OnRetry = args =>
                {
                    var statusCode = args.Outcome.Result?.StatusCode.ToString() ?? "N/A";
                    var exceptionType = args.Outcome.Exception?.GetType().Name ?? "None";
                    logger.LogWarning(args.Outcome.Exception,
                        "Retrying cloud storage request (attempt {Attempt}). Status: {Status}, Exception: {Exception}",
                        args.AttemptNumber + 1,
                        statusCode,
                        exceptionType);
                    return default;
                }
            })
            // Circuit Breaker: Open after 5 consecutive failures, half-open after 30 seconds
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 10,
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(r => r.StatusCode >= HttpStatusCode.InternalServerError)
                    // Handle transient network exceptions in circuit breaker
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>()
                    .Handle<SocketException>()
                    .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
                    .Handle<OperationCanceledException>(ex => ex.InnerException is TimeoutException),
                OnOpened = args =>
                {
                    logger.LogError("Cloud storage circuit breaker OPENED. Too many failures.");
                    return default;
                },
                OnClosed = args =>
                {
                    logger.LogInformation("Cloud storage circuit breaker CLOSED. Service recovered.");
                    return default;
                },
                OnHalfOpened = args =>
                {
                    logger.LogInformation("Cloud storage circuit breaker HALF-OPEN. Testing if service recovered.");
                    return default;
                }
            })
            .Build();
    }

    /// <summary>
    /// Creates a resilience pipeline for HTTP clients accessing external APIs (STAC, migration sources).
    /// More lenient than cloud storage - allows for slower responses.
    /// </summary>
    public static ResiliencePipeline<HttpResponseMessage> CreateExternalApiPolicy(ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Resilience.ExternalApi");

        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            // Timeout: 60 seconds per request (external APIs may be slow)
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(60),
                OnTimeout = args =>
                {
                    logger.LogWarning("External API request timed out after {Timeout}s", args.Timeout.TotalSeconds);
                    return default;
                }
            })
            // Retry: 2 attempts with exponential backoff
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(r => r.StatusCode >= HttpStatusCode.InternalServerError)
                    .HandleResult(r => r.StatusCode == HttpStatusCode.RequestTimeout)
                    .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
                    // Handle transient network exceptions
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>()
                    .Handle<SocketException>()
                    .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
                    .Handle<OperationCanceledException>(ex => ex.InnerException is TimeoutException),
                OnRetry = args =>
                {
                    var statusCode = args.Outcome.Result?.StatusCode.ToString() ?? "N/A";
                    var exceptionType = args.Outcome.Exception?.GetType().Name ?? "None";
                    logger.LogWarning(args.Outcome.Exception,
                        "Retrying external API request (attempt {Attempt}). Status: {Status}, Exception: {Exception}",
                        args.AttemptNumber + 1,
                        statusCode,
                        exceptionType);
                    return default;
                }
            })
            .Build();
    }

    /// <summary>
    /// Creates a resilience pipeline for database operations.
    /// Handles transient errors like connection timeouts and deadlocks.
    /// </summary>
    public static ResiliencePipeline CreateDatabasePolicy(ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Resilience.Database");

        return new ResiliencePipelineBuilder()
            // Timeout: 30 seconds per query
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(30),
                OnTimeout = args =>
                {
                    logger.LogWarning("Database query timed out after {Timeout}s", args.Timeout.TotalSeconds);
                    return default;
                }
            })
            // Retry: 3 attempts with exponential backoff for transient errors
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(100),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder()
                    .Handle<TimeoutException>()
                    .Handle<DbException>(IsTransientDatabaseException),
                OnRetry = args =>
                {
                    logger.LogWarning(args.Outcome.Exception,
                        "Retrying database operation (attempt {Attempt})",
                        args.AttemptNumber + 1);
                    return default;
                }
            })
            .Build();
    }

    /// <summary>
    /// Creates a simple timeout policy for fast operations.
    /// </summary>
    public static ResiliencePipeline CreateFastOperationPolicy(TimeSpan timeout)
    {
        return new ResiliencePipelineBuilder()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = timeout
            })
            .Build();
    }

    /// <summary>
    /// Creates a hedging resilience pipeline for latency-sensitive HTTP read operations.
    /// Hedging sends parallel requests and uses the first successful response to reduce tail latency.
    /// </summary>
    /// <param name="options">Hedging configuration options</param>
    /// <param name="logger">Logger for hedging events</param>
    /// <param name="metrics">Optional metrics collector for hedging statistics</param>
    /// <returns>Configured hedging pipeline for HTTP responses</returns>
    public static ResiliencePipeline<HttpResponseMessage> CreateHttpHedgingPipeline(
        HedgingOptions options,
        ILogger logger,
        ICircuitBreakerMetrics? metrics = null)
    {
        Guard.NotNull(options);
        Guard.NotNull(logger);

        // Validate options
        options.Validate();

        if (!options.Enabled)
        {
            // Return a simple timeout-only pipeline if hedging is disabled
            return new ResiliencePipelineBuilder<HttpResponseMessage>()
                .AddTimeout(TimeSpan.FromSeconds(options.TimeoutSeconds))
                .Build();
        }

        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            // Hedging strategy: Send parallel requests, use first successful response
            .AddHedging(new HedgingStrategyOptions<HttpResponseMessage>
            {
                // Maximum number of hedged attempts (including primary)
                MaxHedgedAttempts = options.MaxHedgedAttempts,

                // Delay before sending hedged request (allows primary to complete if fast)
                Delay = TimeSpan.FromMilliseconds(options.DelayMilliseconds),

                // Determine which outcomes should trigger hedging
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    // Handle transient network exceptions
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
                    .Handle<TimeoutRejectedException>()
                    .Handle<SocketException>()
                    // Handle HTTP error responses (5xx server errors, 408 timeout, 429 rate limit)
                    .HandleResult(response =>
                        response.StatusCode >= HttpStatusCode.InternalServerError || // 5xx
                        response.StatusCode == HttpStatusCode.RequestTimeout ||      // 408
                        response.StatusCode == HttpStatusCode.TooManyRequests),      // 429

                // Log hedging events
                OnHedging = args =>
                {
                    var attemptNumber = args.AttemptNumber;

                    logger.LogWarning(
                        "Hedging HTTP request (attempt {AttemptNumber}) due to slow response or failure",
                        attemptNumber);

                    // Record metrics if available
                    metrics?.RecordHedgingAttempt(attemptNumber, "N/A", "None");

                    return default;
                }
            })
            // Overall timeout for entire hedging operation (all attempts)
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
                OnTimeout = args =>
                {
                    logger.LogWarning(
                        "Hedging operation timed out after {Timeout}s (all attempts failed or exceeded timeout)",
                        args.Timeout.TotalSeconds);

                    metrics?.RecordHedgingTimeout(args.Timeout.TotalSeconds);

                    return default;
                }
            })
            .Build();
    }

    /// <summary>
    /// Creates a hedging resilience pipeline with default options for quick setup.
    /// Uses standard configuration: 2 max attempts, 50ms delay, 5s timeout.
    /// </summary>
    /// <param name="loggerFactory">Logger factory for creating hedging logger</param>
    /// <param name="metrics">Optional metrics collector</param>
    /// <returns>Configured hedging pipeline with defaults</returns>
    public static ResiliencePipeline<HttpResponseMessage> CreateHttpHedgingPipeline(
        ILoggerFactory loggerFactory,
        ICircuitBreakerMetrics? metrics = null)
    {
        var logger = loggerFactory.CreateLogger("Resilience.Hedging");
        var options = new HedgingOptions(); // Use defaults

        return CreateHttpHedgingPipeline(options, logger, metrics);
    }

    /// <summary>
    /// Determines if a database exception is transient and should be retried.
    /// Made internal to allow reuse by CircuitBreakerService.
    /// </summary>
    internal static bool IsTransientDatabaseException(DbException exception)
    {
        if (exception == null)
        {
            return false;
        }

        if (TryGetStringProperty(exception, "SqlState", out var sqlState) &&
            sqlState.HasValue())
        {
            if (sqlState is "40001" or "40P01" or "55P03" or "57014" or "08006" or "08003" or "08001")
            {
                return true;
            }
        }

        if (TryGetIntProperty(exception, "Number", out var number))
        {
            if (number is 4060 or 10928 or 10929 or 40197 or 40501 or 40540 or 40544 or 40549 or 40550 or 40551 or 40552 or 40613 or 49918 or 49919 or 49920 or 1205 or 1213)
            {
                return true;
            }
        }

        if (exception.InnerException is DbException inner && IsTransientDatabaseException(inner))
        {
            return true;
        }

        return exception.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
               exception.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase) ||
               exception.Message.Contains("temporarily unavailable", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetStringProperty(DbException exception, string propertyName, out string? value)
    {
        value = null;
        var property = exception.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (property?.PropertyType == typeof(string))
        {
            value = property.GetValue(exception) as string;
            return true;
        }

        return false;
    }

    private static bool TryGetIntProperty(DbException exception, string propertyName, out int value)
    {
        value = default;
        var property = exception.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (property is null)
        {
            return false;
        }

        var raw = property.GetValue(exception);
        if (raw is int intValue)
        {
            value = intValue;
            return true;
        }

        return false;
    }

    // ========================================================================
    // BUILDER METHODS FOR COMMON RESILIENCE SCENARIOS
    // ========================================================================

    /// <summary>
    /// Creates a resilience pipeline for HTTP client requests with retry and exponential backoff.
    /// Handles transient HTTP errors (5xx, 408, 429) and network exceptions.
    /// </summary>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 3)</param>
    /// <param name="initialDelay">Initial delay before first retry (default: 500ms)</param>
    /// <param name="timeout">Optional timeout per request (default: 30s)</param>
    /// <param name="logger">Optional logger for retry events</param>
    /// <returns>Configured resilience pipeline for HTTP operations</returns>
    public static ResiliencePipeline<HttpResponseMessage> CreateHttpRetryPolicy(
        int maxRetries = 3,
        TimeSpan? initialDelay = null,
        TimeSpan? timeout = null,
        ILogger? logger = null)
    {
        var delay = initialDelay ?? TimeSpan.FromMilliseconds(500);
        var timeoutValue = timeout ?? TimeSpan.FromSeconds(30);

        var builder = new ResiliencePipelineBuilder<HttpResponseMessage>();

        // Add timeout if specified
        if (timeoutValue > TimeSpan.Zero)
        {
            builder.AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = timeoutValue,
                OnTimeout = args =>
                {
                    logger?.LogWarning("HTTP request timed out after {Timeout}s", args.Timeout.TotalSeconds);
                    return default;
                }
            });
        }

        // Add retry with exponential backoff
        builder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
        {
            MaxRetryAttempts = maxRetries,
            Delay = delay,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .HandleResult(r => r.StatusCode >= HttpStatusCode.InternalServerError) // 5xx errors
                .HandleResult(r => r.StatusCode == HttpStatusCode.RequestTimeout)      // 408
                .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)     // 429
                .Handle<HttpRequestException>()
                .Handle<TimeoutRejectedException>()
                .Handle<SocketException>()
                .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
                .Handle<OperationCanceledException>(ex => ex.InnerException is TimeoutException),
            OnRetry = args =>
            {
                var statusCode = args.Outcome.Result?.StatusCode.ToString() ?? "N/A";
                var exceptionType = args.Outcome.Exception?.GetType().Name ?? "None";
                logger?.LogWarning(args.Outcome.Exception,
                    "Retrying HTTP request (attempt {Attempt}/{MaxAttempts}). Status: {Status}, Exception: {Exception}",
                    args.AttemptNumber + 1,
                    maxRetries,
                    statusCode,
                    exceptionType);
                return default;
            }
        });

        return builder.Build();
    }

    /// <summary>
    /// Creates a resilience pipeline for database operations with retry logic for transient exceptions.
    /// Handles database-specific transient errors like deadlocks, timeouts, and connection failures.
    /// </summary>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 3)</param>
    /// <param name="initialDelay">Initial delay before first retry (default: 100ms)</param>
    /// <param name="logger">Optional logger for retry events</param>
    /// <param name="retryableExceptions">Optional additional exception types to retry</param>
    /// <returns>Configured resilience pipeline for database operations</returns>
    public static ResiliencePipeline CreateDatabaseRetryPolicy(
        int maxRetries = 3,
        TimeSpan? initialDelay = null,
        ILogger? logger = null,
        params Type[] retryableExceptions)
    {
        var delay = initialDelay ?? TimeSpan.FromMilliseconds(100);

        var predicateBuilder = new PredicateBuilder()
            .Handle<TimeoutException>()
            .Handle<DbException>(IsTransientDatabaseException);

        // Note: Custom exception types via params not supported in new Polly API
        // Use the shouldRetry func parameter in CreateRetryPolicy instead

        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetries,
                Delay = delay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = predicateBuilder,
                OnRetry = args =>
                {
                    logger?.LogWarning(args.Outcome.Exception,
                        "Retrying database operation (attempt {Attempt}/{MaxAttempts}). Exception: {Exception}",
                        args.AttemptNumber + 1,
                        maxRetries,
                        args.Outcome.Exception?.GetType().Name ?? "Unknown");
                    return default;
                }
            })
            .Build();
    }

    /// <summary>
    /// Creates a resilience pipeline for external service calls with retry, circuit breaker, and timeout.
    /// Provides comprehensive resilience for external dependencies (APIs, cloud services, etc.).
    /// </summary>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 3)</param>
    /// <param name="circuitBreakerThreshold">Failure ratio to open circuit (default: 0.5 = 50%)</param>
    /// <param name="circuitBreakerDuration">How long circuit stays open (default: 30 seconds)</param>
    /// <param name="timeout">Optional timeout per request (default: 30 seconds)</param>
    /// <param name="logger">Optional logger for events</param>
    /// <returns>Configured resilience pipeline with retry, circuit breaker, and timeout</returns>
    public static ResiliencePipeline CreateExternalServicePolicy(
        int maxRetries = 3,
        double circuitBreakerThreshold = 0.5,
        TimeSpan? circuitBreakerDuration = null,
        TimeSpan? timeout = null,
        ILogger? logger = null)
    {
        var breakDuration = circuitBreakerDuration ?? TimeSpan.FromSeconds(30);
        var timeoutValue = timeout ?? TimeSpan.FromSeconds(30);

        var builder = new ResiliencePipelineBuilder();

        // Add timeout if specified
        if (timeoutValue > TimeSpan.Zero)
        {
            builder.AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = timeoutValue,
                OnTimeout = args =>
                {
                    logger?.LogWarning("External service request timed out after {Timeout}s", args.Timeout.TotalSeconds);
                    return default;
                }
            });
        }

        // Add retry with exponential backoff
        builder.AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = maxRetries,
            Delay = TimeSpan.FromMilliseconds(500),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = new PredicateBuilder()
                .Handle<HttpRequestException>()
                .Handle<TimeoutRejectedException>()
                .Handle<TimeoutException>()
                .Handle<SocketException>()
                .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
                .Handle<OperationCanceledException>(ex => ex.InnerException is TimeoutException),
            OnRetry = args =>
            {
                logger?.LogWarning(args.Outcome.Exception,
                    "Retrying external service call (attempt {Attempt}/{MaxAttempts})",
                    args.AttemptNumber + 1,
                    maxRetries);
                return default;
            }
        });

        // Add circuit breaker
        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = circuitBreakerThreshold,
            SamplingDuration = TimeSpan.FromSeconds(30),
            MinimumThroughput = 10,
            BreakDuration = breakDuration,
            ShouldHandle = new PredicateBuilder()
                .Handle<HttpRequestException>()
                .Handle<TimeoutRejectedException>()
                .Handle<TimeoutException>()
                .Handle<SocketException>()
                .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
                .Handle<OperationCanceledException>(ex => ex.InnerException is TimeoutException),
            OnOpened = args =>
            {
                logger?.LogError("External service circuit breaker OPENED. Too many failures.");
                return default;
            },
            OnClosed = args =>
            {
                logger?.LogInformation("External service circuit breaker CLOSED. Service recovered.");
                return default;
            },
            OnHalfOpened = args =>
            {
                logger?.LogInformation("External service circuit breaker HALF-OPEN. Testing recovery.");
                return default;
            }
        });

        return builder.Build();
    }

    /// <summary>
    /// Creates a resilience pipeline for LLM/AI service calls with rate limit handling.
    /// Handles HTTP 429 rate limit responses with intelligent retry delays based on Retry-After headers.
    /// Uses exponential backoff with jitter for other transient failures.
    /// </summary>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 5)</param>
    /// <param name="maxDelay">Maximum delay between retries (default: 60 seconds)</param>
    /// <param name="timeout">Optional timeout per request (default: 120 seconds for LLM calls)</param>
    /// <param name="logger">Optional logger for rate limit events</param>
    /// <returns>Configured resilience pipeline for LLM service calls</returns>
    public static ResiliencePipeline<HttpResponseMessage> CreateLlmRetryPolicy(
        int maxRetries = 5,
        TimeSpan? maxDelay = null,
        TimeSpan? timeout = null,
        ILogger? logger = null)
    {
        var maxDelayValue = maxDelay ?? TimeSpan.FromSeconds(60);
        var timeoutValue = timeout ?? TimeSpan.FromSeconds(120);

        var builder = new ResiliencePipelineBuilder<HttpResponseMessage>();

        // Add timeout if specified
        if (timeoutValue > TimeSpan.Zero)
        {
            builder.AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = timeoutValue,
                OnTimeout = args =>
                {
                    logger?.LogWarning("LLM request timed out after {Timeout}s", args.Timeout.TotalSeconds);
                    return default;
                }
            });
        }

        // Add retry with custom delay generator for rate limiting
        builder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
        {
            MaxRetryAttempts = maxRetries,
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)     // 429 rate limit
                .HandleResult(r => r.StatusCode >= HttpStatusCode.InternalServerError) // 5xx errors
                .HandleResult(r => r.StatusCode == HttpStatusCode.RequestTimeout)      // 408
                .Handle<HttpRequestException>()
                .Handle<TimeoutRejectedException>()
                .Handle<SocketException>()
                .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested),
            DelayGenerator = args =>
            {
                // For HTTP 429, try to respect Retry-After header
                if (args.Outcome.Result?.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var delay = GetRetryAfterDelay(args.Outcome.Result, args.AttemptNumber, maxDelayValue);
                    logger?.LogWarning(
                        "Rate limit (HTTP 429) from LLM provider. Attempt {Attempt}/{MaxAttempts}. Waiting {DelaySeconds}s",
                        args.AttemptNumber + 1,
                        maxRetries,
                        delay.TotalSeconds);
                    return new ValueTask<TimeSpan?>(delay);
                }

                // For other errors, use exponential backoff
                var exponentialDelay = TimeSpan.FromSeconds(Math.Min(
                    Math.Pow(2, args.AttemptNumber),
                    maxDelayValue.TotalSeconds));

                logger?.LogWarning(
                    "Retrying LLM request. Attempt {Attempt}/{MaxAttempts}. Waiting {DelaySeconds}s",
                    args.AttemptNumber + 1,
                    maxRetries,
                    exponentialDelay.TotalSeconds);

                return new ValueTask<TimeSpan?>(exponentialDelay);
            }
        });

        return builder.Build();
    }

    /// <summary>
    /// Creates a resilience pipeline for general operations with simple retry logic.
    /// Uses exponential backoff and handles common transient exceptions.
    /// </summary>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 3)</param>
    /// <param name="initialDelay">Initial delay before first retry (default: 1 second)</param>
    /// <param name="logger">Optional logger for retry events</param>
    /// <param name="shouldRetry">Optional predicate to determine if exception should be retried</param>
    /// <param name="useJitter">Whether to apply jitter to retry delays (default: true)</param>
    /// <returns>Configured resilience pipeline with retry logic</returns>
    public static ResiliencePipeline CreateRetryPolicy(
        int maxRetries = 3,
        TimeSpan? initialDelay = null,
        ILogger? logger = null,
        Func<Exception, bool>? shouldRetry = null,
        bool useJitter = true)
    {
        var delay = initialDelay ?? TimeSpan.FromSeconds(1);

        var predicateBuilder = new PredicateBuilder()
            .Handle<HttpRequestException>()
            .Handle<TimeoutException>()
            .Handle<TaskCanceledException>()
            .Handle<OperationCanceledException>()
            .Handle<IOException>();

        // Add custom retry predicate if provided
        if (shouldRetry != null)
        {
            predicateBuilder.Handle<Exception>(shouldRetry);
        }

        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetries,
                Delay = delay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = useJitter,
                ShouldHandle = predicateBuilder,
                OnRetry = args =>
                {
                    logger?.LogWarning(args.Outcome.Exception,
                        "Retrying operation (attempt {Attempt}/{MaxAttempts}). Exception: {Exception}",
                        args.AttemptNumber + 1,
                        maxRetries,
                        args.Outcome.Exception?.GetType().Name ?? "Unknown");
                    return default;
                }
            })
            .Build();
    }

    /// <summary>
    /// Extracts retry delay from HTTP 429 response's Retry-After header.
    /// Falls back to exponential backoff if header is not present or invalid.
    /// </summary>
    private static TimeSpan GetRetryAfterDelay(
        HttpResponseMessage response,
        int attemptNumber,
        TimeSpan maxDelay)
    {
        // Try to get Retry-After header
        if (response.Headers.TryGetValues("Retry-After", out var retryAfterValues))
        {
            var retryAfter = retryAfterValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(retryAfter))
            {
                // Try parsing as seconds (integer)
                if (int.TryParse(retryAfter, out var seconds))
                {
                    return TimeSpan.FromSeconds(Math.Min(seconds, maxDelay.TotalSeconds));
                }

                // Try parsing as HTTP date
                if (DateTimeOffset.TryParse(retryAfter, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var retryDate))
                {
                    var delayUntil = retryDate - DateTimeOffset.UtcNow;
                    if (delayUntil > TimeSpan.Zero)
                    {
                        return TimeSpan.FromSeconds(Math.Min(delayUntil.TotalSeconds, maxDelay.TotalSeconds));
                    }
                }
            }
        }

        // Fallback to exponential backoff: 1s, 2s, 4s, 8s, etc.
        var baseDelay = Math.Pow(2, attemptNumber);
        return TimeSpan.FromSeconds(Math.Min(baseDelay, maxDelay.TotalSeconds));
    }
}
