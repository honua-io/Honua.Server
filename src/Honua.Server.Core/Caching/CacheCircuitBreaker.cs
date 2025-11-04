// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Exceptions;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace Honua.Server.Core.Caching;

/// <summary>
/// Circuit breaker wrapper for cache operations to prevent cascading failures.
/// Provides automatic fallback and recovery for cache failures.
/// </summary>
public sealed class CacheCircuitBreaker
{
    private readonly string _cacheName;
    private readonly ILogger _logger;
    private readonly ResiliencePipeline _pipeline;

    public CacheCircuitBreaker(string cacheName, ILogger logger)
    {
        _cacheName = cacheName ?? throw new ArgumentNullException(nameof(cacheName));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pipeline = CreateCircuitBreakerPipeline();
    }

    /// <summary>
    /// Executes a cache operation with circuit breaker protection.
    /// Returns null if circuit is open or operation fails.
    /// </summary>
    public async Task<T?> ExecuteAsync<T>(Func<CancellationToken, Task<T?>> operation, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            return await _pipeline.ExecuteAsync(
                async ct => await operation(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogWarning("Circuit breaker is open for cache '{CacheName}'. Returning null.", _cacheName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache operation failed for '{CacheName}'. Returning null.", _cacheName);
            return null;
        }
    }

    /// <summary>
    /// Executes a cache write operation with circuit breaker protection.
    /// Returns true if successful, false if circuit is open or operation fails.
    /// </summary>
    public async Task<bool> ExecuteWriteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        try
        {
            await _pipeline.ExecuteAsync(
                async ct =>
                {
                    await operation(ct).ConfigureAwait(false);
                    return ValueTask.FromResult(true);
                },
                cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning("Circuit breaker is open for cache '{CacheName}'. Write operation skipped.", _cacheName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache write operation failed for '{CacheName}'.", _cacheName);
            return false;
        }
    }

    private ResiliencePipeline CreateCircuitBreakerPipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                // Circuit opens after 50% failure rate
                FailureRatio = 0.5,
                // Minimum 10 operations before circuit can open
                MinimumThroughput = 10,
                // Sample window for failure rate calculation
                SamplingDuration = TimeSpan.FromSeconds(30),
                // Circuit stays open for 30 seconds
                BreakDuration = TimeSpan.FromSeconds(30),

                // Handle cache-specific exceptions
                ShouldHandle = new PredicateBuilder()
                    .Handle<CacheUnavailableException>()
                    .Handle<CacheWriteException>()
                    .Handle<TimeoutException>()
                    .Handle<Exception>(ex =>
                        // Handle timeout and network errors
                        ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("network", StringComparison.OrdinalIgnoreCase)),

                // Log circuit state changes
                OnOpened = args =>
                {
                    _logger.LogError(
                        "Circuit breaker OPENED for cache '{CacheName}'. Failure rate: {FailureRate:P}. Break duration: {BreakDuration}s. Cache operations will be skipped.",
                        _cacheName,
                        args.Outcome.Exception != null ? 1.0 : 0.0,
                        args.BreakDuration.TotalSeconds);
                    return default;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation(
                        "Circuit breaker CLOSED for cache '{CacheName}'. Cache is healthy again.",
                        _cacheName);
                    return default;
                },
                OnHalfOpened = args =>
                {
                    _logger.LogInformation(
                        "Circuit breaker HALF-OPEN for cache '{CacheName}'. Testing if cache has recovered.",
                        _cacheName);
                    return default;
                }
            })
            .Build();
    }
}
