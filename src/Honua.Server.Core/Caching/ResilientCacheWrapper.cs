// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Caching;

/// <summary>
/// Resilient wrapper for distributed cache that provides circuit breaker protection
/// and graceful degradation when cache is unavailable.
/// </summary>
public sealed class ResilientCacheWrapper : IDistributedCache
{
    private readonly IDistributedCache _innerCache;
    private readonly CacheCircuitBreaker _circuitBreaker;
    private readonly ILogger<ResilientCacheWrapper> _logger;

    public ResilientCacheWrapper(
        IDistributedCache innerCache,
        ILogger<ResilientCacheWrapper> logger,
        string cacheName = "DistributedCache")
    {
        _innerCache = innerCache ?? throw new ArgumentNullException(nameof(innerCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _circuitBreaker = new CacheCircuitBreaker(cacheName, logger);
    }

    public byte[]? Get(string key)
    {
        throw new NotSupportedException("Synchronous cache operations are not supported. Use GetAsync instead to avoid deadlocks.");
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cache key cannot be null or empty.", nameof(key));
        }

        return await _circuitBreaker.ExecuteAsync(
            async ct =>
            {
                try
                {
                    return await _innerCache.GetAsync(key, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get cache key '{Key}'. Returning null.", key);
                    return null;
                }
            },
            token).ConfigureAwait(false);
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        throw new NotSupportedException("Synchronous cache operations are not supported. Use SetAsync instead to avoid deadlocks.");
    }

    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cache key cannot be null or empty.", nameof(key));
        }

        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var success = await _circuitBreaker.ExecuteWriteAsync(
            async ct =>
            {
                try
                {
                    await _innerCache.SetAsync(key, value, options, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to set cache key '{Key}'.", key);
                    throw;
                }
            },
            token).ConfigureAwait(false);

        if (!success)
        {
            _logger.LogDebug("Cache set operation skipped for key '{Key}' due to circuit breaker or failure.", key);
        }
    }

    public void Refresh(string key)
    {
        throw new NotSupportedException("Synchronous cache operations are not supported. Use RefreshAsync instead to avoid deadlocks.");
    }

    public async Task RefreshAsync(string key, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cache key cannot be null or empty.", nameof(key));
        }

        await _circuitBreaker.ExecuteWriteAsync(
            async ct =>
            {
                try
                {
                    await _innerCache.RefreshAsync(key, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to refresh cache key '{Key}'.", key);
                    throw;
                }
            },
            token).ConfigureAwait(false);
    }

    public void Remove(string key)
    {
        throw new NotSupportedException("Synchronous cache operations are not supported. Use RemoveAsync instead to avoid deadlocks.");
    }

    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cache key cannot be null or empty.", nameof(key));
        }

        await _circuitBreaker.ExecuteWriteAsync(
            async ct =>
            {
                try
                {
                    await _innerCache.RemoveAsync(key, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to remove cache key '{Key}'.", key);
                    throw;
                }
            },
            token).ConfigureAwait(false);
    }
}
