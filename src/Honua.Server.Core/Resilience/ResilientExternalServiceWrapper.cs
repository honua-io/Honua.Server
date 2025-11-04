// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Exceptions;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace Honua.Server.Core.Resilience;

/// <summary>
/// Resilient wrapper for external HTTP services (S3, Azure Blob, GCS, etc.)
/// Provides circuit breaker, retry, and fallback mechanisms.
/// </summary>
public sealed class ResilientExternalServiceWrapper
{
    private readonly string _serviceName;
    private readonly ILogger _logger;
    private readonly ResiliencePipeline _pipeline;
    private readonly ResilientServiceExecutor _executor;

    public ResilientExternalServiceWrapper(
        string serviceName,
        ILogger logger,
        ResiliencePipeline? customPipeline = null)
    {
        _serviceName = serviceName ?? throw new ArgumentNullException(nameof(serviceName));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pipeline = customPipeline ?? CreateDefaultPipeline();
        _executor = new ResilientServiceExecutor(logger);
    }

    /// <summary>
    /// Executes an HTTP operation with circuit breaker and retry logic.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _pipeline.ExecuteAsync(
                async ct => await operation(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
        }
        catch (BrokenCircuitException)
        {
            throw new CircuitBreakerOpenException(_serviceName, TimeSpan.FromSeconds(30));
        }
        catch (TimeoutException ex)
        {
            throw new ServiceTimeoutException(_serviceName, TimeSpan.FromSeconds(30), ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ServiceUnavailableException(_serviceName, "HTTP request failed", ex);
        }
    }

    /// <summary>
    /// Executes an operation with fallback to alternative service.
    /// </summary>
    public async Task<FallbackResult<T>> ExecuteWithAlternativeAsync<T>(
        Func<CancellationToken, Task<T>> primary,
        Func<Exception, CancellationToken, Task<T>> alternative,
        CancellationToken cancellationToken = default)
    {
        return await _executor.ExecuteWithFallbackAsync(
            async ct =>
            {
                try
                {
                    return await ExecuteAsync(primary, ct);
                }
                catch (Exception ex)
                {
                    // Wrap in transient exception for fallback logic
                    throw new ServiceUnavailableException(_serviceName, "Primary service failed", ex);
                }
            },
            alternative,
            $"{_serviceName}.ExecuteWithAlternative",
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes an operation with default value fallback on failure.
    /// </summary>
    public async Task<FallbackResult<T>> ExecuteWithDefaultAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        T defaultValue,
        CancellationToken cancellationToken = default)
    {
        return await _executor.ExecuteWithDefaultAsync(
            async ct =>
            {
                try
                {
                    return await ExecuteAsync(operation, ct);
                }
                catch (Exception ex)
                {
                    // Wrap in transient exception for fallback logic
                    throw new ServiceUnavailableException(_serviceName, "Service failed", ex);
                }
            },
            defaultValue,
            $"{_serviceName}.ExecuteWithDefault",
            cancellationToken).ConfigureAwait(false);
    }

    private ResiliencePipeline CreateDefaultPipeline()
    {
        return new ResiliencePipelineBuilder()
            // Circuit Breaker: Open after 50% failure rate
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
                    .Handle<ServiceUnavailableException>(),
                OnOpened = args =>
                {
                    _logger.LogError("Circuit breaker OPENED for {ServiceName}", _serviceName);
                    return default;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("Circuit breaker CLOSED for {ServiceName}", _serviceName);
                    return default;
                },
                OnHalfOpened = args =>
                {
                    _logger.LogInformation("Circuit breaker HALF-OPEN for {ServiceName}", _serviceName);
                    return default;
                }
            })
            // Retry: 3 attempts with exponential backoff
            .AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = Polly.DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>(),
                OnRetry = args =>
                {
                    _logger.LogWarning("Retrying {ServiceName} (attempt {Attempt})",
                        _serviceName, args.AttemptNumber + 1);
                    return ValueTask.CompletedTask;
                }
            })
            // Timeout: 30 seconds per request
            .AddTimeout(new Polly.Timeout.TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(30),
                OnTimeout = args =>
                {
                    _logger.LogWarning("{ServiceName} request timed out after {Timeout}s",
                        _serviceName, args.Timeout.TotalSeconds);
                    return default;
                }
            })
            .Build();
    }
}
