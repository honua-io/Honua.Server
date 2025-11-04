// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Exceptions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Caching.Resilience;

/// <summary>
/// Retry policy for cache invalidation operations with exponential backoff.
/// Ensures cache invalidation failures are retried before propagating to callers.
/// </summary>
public class CacheInvalidationRetryPolicy
{
    private readonly IOptionsMonitor<CacheInvalidationOptions> _options;
    private readonly ILogger<CacheInvalidationRetryPolicy> _logger;

    public CacheInvalidationRetryPolicy(
        IOptionsMonitor<CacheInvalidationOptions> options,
        ILogger<CacheInvalidationRetryPolicy> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes cache invalidation with retry logic and exponential backoff.
    /// </summary>
    /// <param name="cache">The distributed cache instance.</param>
    /// <param name="cacheKey">The cache key to invalidate.</param>
    /// <param name="cacheName">The name of the cache for logging/metrics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="CacheInvalidationException">Thrown when all retry attempts fail.</exception>
    public async Task InvalidateWithRetryAsync(
        IDistributedCache cache,
        string cacheKey,
        string cacheName,
        CancellationToken cancellationToken = default)
    {
        if (cache == null)
        {
            throw new ArgumentNullException(nameof(cache));
        }

        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            throw new ArgumentException("Cache key cannot be null or empty.", nameof(cacheKey));
        }

        var options = _options.CurrentValue;
        var maxAttempts = Math.Max(1, options.RetryCount + 1); // Initial attempt + retries
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                // Create timeout for this operation
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(options.OperationTimeout);

                if (options.EnableDetailedLogging)
                {
                    _logger.LogDebug(
                        "Invalidating cache '{CacheName}' key '{CacheKey}' (attempt {Attempt}/{MaxAttempts})",
                        cacheName,
                        cacheKey,
                        attempt,
                        maxAttempts);
                }

                await cache.RemoveAsync(cacheKey, timeoutCts.Token).ConfigureAwait(false);

                // Success - log if it took retries
                if (attempt > 1)
                {
                    _logger.LogInformation(
                        "Cache invalidation succeeded on attempt {Attempt}/{MaxAttempts} for '{CacheName}' key '{CacheKey}'",
                        attempt,
                        maxAttempts,
                        cacheName,
                        cacheKey);
                }

                return; // Success!
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout - retry
                lastException = ex;

                if (options.EnableDetailedLogging)
                {
                    _logger.LogWarning(
                        "Cache invalidation timed out on attempt {Attempt}/{MaxAttempts} for '{CacheName}' key '{CacheKey}'",
                        attempt,
                        maxAttempts,
                        cacheName,
                        cacheKey);
                }
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (options.EnableDetailedLogging)
                {
                    _logger.LogWarning(
                        ex,
                        "Cache invalidation failed on attempt {Attempt}/{MaxAttempts} for '{CacheName}' key '{CacheKey}': {Error}",
                        attempt,
                        maxAttempts,
                        cacheName,
                        cacheKey,
                        ex.Message);
                }
            }

            // If not the last attempt, wait with exponential backoff
            if (attempt < maxAttempts)
            {
                var delay = options.GetRetryDelay(attempt);

                if (options.EnableDetailedLogging)
                {
                    _logger.LogDebug(
                        "Retrying cache invalidation after {DelayMs}ms delay (attempt {NextAttempt}/{MaxAttempts})",
                        delay.TotalMilliseconds,
                        attempt + 1,
                        maxAttempts);
                }

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        // All attempts failed - throw exception
        var message = lastException?.Message ?? "Unknown error";
        _logger.LogError(
            lastException,
            "CRITICAL: Cache invalidation failed after {MaxAttempts} attempts for '{CacheName}' key '{CacheKey}'. " +
            "Cache-database inconsistency detected. Error: {Error}",
            maxAttempts,
            cacheName,
            cacheKey,
            message);

        throw new CacheInvalidationException(
            cacheName,
            cacheKey,
            $"Failed after {maxAttempts} attempts: {message}",
            lastException!,
            maxAttempts);
    }

    /// <summary>
    /// Executes cache set operation with short TTL as fallback strategy.
    /// Used when invalidation fails but we want to reduce the stale data window.
    /// </summary>
    public async Task<bool> TrySetShortTtlAsync(
        IDistributedCache cache,
        string cacheKey,
        byte[] value,
        string cacheName,
        CancellationToken cancellationToken = default)
    {
        if (cache == null || string.IsNullOrWhiteSpace(cacheKey) || value == null)
        {
            return false;
        }

        try
        {
            var options = _options.CurrentValue;
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = options.ShortTtl
            };

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(options.OperationTimeout);

            await cache.SetAsync(cacheKey, value, cacheOptions, timeoutCts.Token).ConfigureAwait(false);

            _logger.LogInformation(
                "Applied short TTL ({TtlSeconds}s) to cache '{CacheName}' key '{CacheKey}' as fallback strategy",
                options.ShortTtl.TotalSeconds,
                cacheName,
                cacheKey);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to apply short TTL fallback for cache '{CacheName}' key '{CacheKey}'",
                cacheName,
                cacheKey);

            return false;
        }
    }
}
