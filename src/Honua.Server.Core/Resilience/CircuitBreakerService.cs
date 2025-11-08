// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Data.Common;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Honua.Server.Core.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Honua.Server.Core.Resilience;

/// <summary>
/// Service interface for managing circuit breaker policies across different dependency types.
/// Provides pre-configured policies for databases, external APIs, and cloud storage.
/// </summary>
public interface ICircuitBreakerService
{
    /// <summary>
    /// Gets a circuit breaker policy for database operations.
    /// Combines retry logic with circuit breaker protection.
    /// </summary>
    /// <returns>A resilience pipeline with retry and circuit breaker.</returns>
    ResiliencePipeline GetDatabasePolicy();

    /// <summary>
    /// Gets a circuit breaker policy for external API calls (HTTP).
    /// Combines retry logic with circuit breaker protection.
    /// </summary>
    /// <returns>A resilience pipeline with retry and circuit breaker.</returns>
    ResiliencePipeline GetExternalApiPolicy();

    /// <summary>
    /// Gets a circuit breaker policy for cloud storage operations (S3, Azure Blob, GCS).
    /// Combines retry logic with circuit breaker protection.
    /// </summary>
    /// <returns>A resilience pipeline with retry and circuit breaker.</returns>
    ResiliencePipeline GetStoragePolicy();

    /// <summary>
    /// Gets the current state of a circuit breaker.
    /// </summary>
    /// <param name="serviceName">The service name (database, externalapi, storage).</param>
    /// <returns>The current circuit state.</returns>
    Observability.CircuitState GetCircuitState(string serviceName);
}

/// <summary>
/// Implementation of circuit breaker service using Polly.
/// Provides resilience policies that combine retry, circuit breaker, and timeout strategies.
/// </summary>
public class CircuitBreakerService : ICircuitBreakerService
{
    private readonly CircuitBreakerOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<CircuitBreakerService> _logger;
    private readonly ICircuitBreakerMetrics _metrics;

    // Cached pipelines - created once and reused
    // We cache the non-generic version and create typed wrappers on demand
    private readonly Lazy<ResiliencePipeline> _databasePipeline;
    private readonly Lazy<ResiliencePipeline> _externalApiPipeline;
    private readonly Lazy<ResiliencePipeline> _storagePipeline;

    // Track circuit states for health checks
    private Observability.CircuitState _databaseState = Observability.CircuitState.Closed;
    private Observability.CircuitState _externalApiState = Observability.CircuitState.Closed;
    private Observability.CircuitState _storageState = Observability.CircuitState.Closed;

    public CircuitBreakerService(
        IOptions<CircuitBreakerOptions> options,
        ILoggerFactory loggerFactory,
        ICircuitBreakerMetrics metrics)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<CircuitBreakerService>();
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));

        // Validate options
        _options.Database.Validate();
        _options.ExternalApi.Validate();
        _options.Storage.Validate();

        // Initialize lazy pipelines
        _databasePipeline = new Lazy<ResiliencePipeline>(CreateDatabasePipeline);
        _externalApiPipeline = new Lazy<ResiliencePipeline>(CreateExternalApiPipeline);
        _storagePipeline = new Lazy<ResiliencePipeline>(CreateStoragePipeline);
    }

    /// <inheritdoc/>
    public ResiliencePipeline GetDatabasePolicy()
    {
        return _databasePipeline.Value;
    }

    /// <inheritdoc/>
    public ResiliencePipeline GetExternalApiPolicy()
    {
        return _externalApiPipeline.Value;
    }

    /// <inheritdoc/>
    public ResiliencePipeline GetStoragePolicy()
    {
        return _storagePipeline.Value;
    }

    /// <inheritdoc/>
    public Observability.CircuitState GetCircuitState(string serviceName)
    {
        return serviceName?.ToLowerInvariant() switch
        {
            "database" => _databaseState,
            "externalapi" => _externalApiState,
            "storage" => _storageState,
            _ => Observability.CircuitState.Closed
        };
    }

    private ResiliencePipeline CreateDatabasePipeline()
    {
        var config = _options.Database;
        var logger = _loggerFactory.CreateLogger("Resilience.Database");

        if (!config.Enabled)
        {
            _logger.LogInformation("Database circuit breaker is disabled");
            return new ResiliencePipelineBuilder().Build();
        }

        _logger.LogInformation(
            "Creating database circuit breaker: FailureRatio={FailureRatio}, MinThroughput={MinThroughput}, BreakDuration={BreakDuration}s",
            config.FailureRatio,
            config.MinimumThroughput,
            config.BreakDuration.TotalSeconds);

        return new ResiliencePipelineBuilder()
            // Timeout: 30 seconds per database operation
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(30),
                OnTimeout = args =>
                {
                    logger.LogWarning("Database operation timed out after {Timeout}s", args.Timeout.TotalSeconds);
                    return default;
                }
            })
            // Retry: 3 attempts with exponential backoff for transient database errors
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(100),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<TimeoutException>()
                    .Handle<DbException>(ResiliencePolicies.IsTransientDatabaseException)
                    .Handle<SocketException>(),
                OnRetry = args =>
                {
                    logger.LogWarning(args.Outcome.Exception,
                        "Retrying database operation (attempt {Attempt}/{MaxAttempts}). Exception: {Exception}",
                        args.AttemptNumber + 1,
                        3,
                        args.Outcome.Exception?.GetType().Name ?? "Unknown");
                    return default;
                }
            })
            // Circuit Breaker: Prevents cascading failures
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = config.FailureRatio,
                MinimumThroughput = config.MinimumThroughput,
                SamplingDuration = config.SamplingDuration,
                BreakDuration = config.BreakDuration,
                ShouldHandle = new PredicateBuilder()
                    .Handle<TimeoutException>()
                    .Handle<DbException>(ResiliencePolicies.IsTransientDatabaseException)
                    .Handle<SocketException>(),
                OnOpened = args =>
                {
                    _databaseState = Observability.CircuitState.Open;
                    _metrics.RecordCircuitOpened("database", args.Outcome.Exception?.GetType().Name);
                    logger.LogError(
                        "Database circuit breaker OPENED. Too many failures. Circuit will remain open for {BreakDuration}s",
                        args.BreakDuration.TotalSeconds);
                    return default;
                },
                OnClosed = args =>
                {
                    _databaseState = Observability.CircuitState.Closed;
                    _metrics.RecordCircuitClosed("database");
                    logger.LogInformation("Database circuit breaker CLOSED. Service recovered.");
                    return default;
                },
                OnHalfOpened = args =>
                {
                    _databaseState = Observability.CircuitState.HalfOpen;
                    _metrics.RecordCircuitHalfOpened("database");
                    logger.LogInformation("Database circuit breaker HALF-OPEN. Testing if service recovered.");
                    return default;
                }
            })
            .Build();
    }

    private ResiliencePipeline CreateExternalApiPipeline()
    {
        var config = _options.ExternalApi;
        var logger = _loggerFactory.CreateLogger("Resilience.ExternalApi");

        if (!config.Enabled)
        {
            _logger.LogInformation("External API circuit breaker is disabled");
            return new ResiliencePipelineBuilder().Build();
        }

        _logger.LogInformation(
            "Creating external API circuit breaker: FailureRatio={FailureRatio}, MinThroughput={MinThroughput}, BreakDuration={BreakDuration}s",
            config.FailureRatio,
            config.MinimumThroughput,
            config.BreakDuration.TotalSeconds);

        return new ResiliencePipelineBuilder()
            // Timeout: 60 seconds per API call (external APIs may be slow)
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(60),
                OnTimeout = args =>
                {
                    logger.LogWarning("External API call timed out after {Timeout}s", args.Timeout.TotalSeconds);
                    return default;
                }
            })
            // Retry: 3 attempts with exponential backoff
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
                    .Handle<TimeoutRejectedException>()
                    .Handle<SocketException>()
                    .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
                    .Handle<OperationCanceledException>(ex => ex.InnerException is TimeoutException),
                OnRetry = args =>
                {
                    logger.LogWarning(args.Outcome.Exception,
                        "Retrying external API call (attempt {Attempt}/{MaxAttempts}). Exception: {Exception}",
                        args.AttemptNumber + 1,
                        3,
                        args.Outcome.Exception?.GetType().Name ?? "Unknown");
                    return default;
                }
            })
            // Circuit Breaker: Prevents cascading failures
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = config.FailureRatio,
                MinimumThroughput = config.MinimumThroughput,
                SamplingDuration = config.SamplingDuration,
                BreakDuration = config.BreakDuration,
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
                    .Handle<TimeoutRejectedException>()
                    .Handle<SocketException>()
                    .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
                    .Handle<OperationCanceledException>(ex => ex.InnerException is TimeoutException),
                OnOpened = args =>
                {
                    _externalApiState = Observability.CircuitState.Open;
                    _metrics.RecordCircuitOpened("externalapi", args.Outcome.Exception?.GetType().Name);
                    logger.LogError(
                        "External API circuit breaker OPENED. Too many failures. Circuit will remain open for {BreakDuration}s",
                        args.BreakDuration.TotalSeconds);
                    return default;
                },
                OnClosed = args =>
                {
                    _externalApiState = Observability.CircuitState.Closed;
                    _metrics.RecordCircuitClosed("externalapi");
                    logger.LogInformation("External API circuit breaker CLOSED. Service recovered.");
                    return default;
                },
                OnHalfOpened = args =>
                {
                    _externalApiState = Observability.CircuitState.HalfOpen;
                    _metrics.RecordCircuitHalfOpened("externalapi");
                    logger.LogInformation("External API circuit breaker HALF-OPEN. Testing if service recovered.");
                    return default;
                }
            })
            .Build();
    }

    private ResiliencePipeline CreateStoragePipeline()
    {
        var config = _options.Storage;
        var logger = _loggerFactory.CreateLogger("Resilience.Storage");

        if (!config.Enabled)
        {
            _logger.LogInformation("Storage circuit breaker is disabled");
            return new ResiliencePipelineBuilder().Build();
        }

        _logger.LogInformation(
            "Creating storage circuit breaker: FailureRatio={FailureRatio}, MinThroughput={MinThroughput}, BreakDuration={BreakDuration}s",
            config.FailureRatio,
            config.MinimumThroughput,
            config.BreakDuration.TotalSeconds);

        return new ResiliencePipelineBuilder()
            // Timeout: 30 seconds per storage operation
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(30),
                OnTimeout = args =>
                {
                    logger.LogWarning("Storage operation timed out after {Timeout}s", args.Timeout.TotalSeconds);
                    return default;
                }
            })
            // Retry: 3 attempts with exponential backoff
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
                    .Handle<TimeoutRejectedException>()
                    .Handle<SocketException>()
                    .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
                    .Handle<OperationCanceledException>(ex => ex.InnerException is TimeoutException)
                    // Handle cloud-specific transient exceptions
                    .Handle<Exception>(IsTransientStorageException),
                OnRetry = args =>
                {
                    logger.LogWarning(args.Outcome.Exception,
                        "Retrying storage operation (attempt {Attempt}/{MaxAttempts}). Exception: {Exception}",
                        args.AttemptNumber + 1,
                        3,
                        args.Outcome.Exception?.GetType().Name ?? "Unknown");
                    return default;
                }
            })
            // Circuit Breaker: Prevents cascading failures
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = config.FailureRatio,
                MinimumThroughput = config.MinimumThroughput,
                SamplingDuration = config.SamplingDuration,
                BreakDuration = config.BreakDuration,
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
                    .Handle<TimeoutRejectedException>()
                    .Handle<SocketException>()
                    .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
                    .Handle<OperationCanceledException>(ex => ex.InnerException is TimeoutException)
                    .Handle<Exception>(IsTransientStorageException),
                OnOpened = args =>
                {
                    _storageState = Observability.CircuitState.Open;
                    _metrics.RecordCircuitOpened("storage", args.Outcome.Exception?.GetType().Name);
                    logger.LogError(
                        "Storage circuit breaker OPENED. Too many failures. Circuit will remain open for {BreakDuration}s",
                        args.BreakDuration.TotalSeconds);
                    return default;
                },
                OnClosed = args =>
                {
                    _storageState = Observability.CircuitState.Closed;
                    _metrics.RecordCircuitClosed("storage");
                    logger.LogInformation("Storage circuit breaker CLOSED. Service recovered.");
                    return default;
                },
                OnHalfOpened = args =>
                {
                    _storageState = Observability.CircuitState.HalfOpen;
                    _metrics.RecordCircuitHalfOpened("storage");
                    logger.LogInformation("Storage circuit breaker HALF-OPEN. Testing if service recovered.");
                    return default;
                }
            })
            .Build();
    }

    /// <summary>
    /// Determines if an exception from cloud storage is transient and should be retried.
    /// </summary>
    private static bool IsTransientStorageException(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        var typeName = ex.GetType().FullName ?? string.Empty;
        var message = ex.Message?.ToLowerInvariant() ?? string.Empty;

        // AWS S3 exceptions
        if (typeName.StartsWith("Amazon.S3", StringComparison.Ordinal) ||
            typeName.StartsWith("Amazon.Runtime", StringComparison.Ordinal))
        {
            return message.Contains("timeout") ||
                   message.Contains("throttl") ||
                   message.Contains("service unavailable") ||
                   message.Contains("internal error") ||
                   message.Contains("slow down") ||
                   message.Contains("503") ||
                   message.Contains("500");
        }

        // Azure Blob exceptions
        if (typeName.StartsWith("Azure.Storage", StringComparison.Ordinal) ||
            typeName.StartsWith("Azure.RequestFailedException", StringComparison.Ordinal))
        {
            return message.Contains("timeout") ||
                   message.Contains("throttl") ||
                   message.Contains("service unavailable") ||
                   message.Contains("server busy") ||
                   message.Contains("too many requests") ||
                   message.Contains("503") ||
                   message.Contains("500");
        }

        // Google Cloud Storage exceptions
        if (typeName.StartsWith("Google.Cloud.Storage", StringComparison.Ordinal) ||
            typeName.StartsWith("Google.GoogleApiException", StringComparison.Ordinal))
        {
            return message.Contains("timeout") ||
                   message.Contains("throttl") ||
                   message.Contains("service unavailable") ||
                   message.Contains("rate limit") ||
                   message.Contains("503") ||
                   message.Contains("500");
        }

        return false;
    }
}
