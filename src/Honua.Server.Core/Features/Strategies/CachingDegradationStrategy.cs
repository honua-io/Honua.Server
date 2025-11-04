// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Features.Strategies;

/// <summary>
/// Adaptive caching service that falls back from distributed to in-memory cache.
/// </summary>
public sealed class AdaptiveCacheService
{
    private readonly IDistributedCache? _distributedCache;
    private readonly IMemoryCache _memoryCache;
    private readonly IFeatureManagementService _featureManagement;
    private readonly ILogger<AdaptiveCacheService> _logger;

    public AdaptiveCacheService(
        IMemoryCache memoryCache,
        IFeatureManagementService featureManagement,
        ILogger<AdaptiveCacheService> logger,
        IDistributedCache? distributedCache = null)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _featureManagement = featureManagement ?? throw new ArgumentNullException(nameof(featureManagement));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _distributedCache = distributedCache;
    }

    /// <summary>
    /// Gets a value from cache, using appropriate cache based on feature status.
    /// </summary>
    public async Task<byte[]?> GetAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var useDistributed = await ShouldUseDistributedCacheAsync(cancellationToken);

        if (useDistributed && _distributedCache != null)
        {
            try
            {
                return await _distributedCache.GetAsync(key, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Distributed cache get failed for key {Key}, falling back to memory cache",
                    key);

                // Fall through to memory cache
            }
        }

        // Use memory cache
        return _memoryCache.Get<byte[]>(key);
    }

    /// <summary>
    /// Sets a value in cache, using appropriate cache based on feature status.
    /// </summary>
    public async Task SetAsync(
        string key,
        byte[] value,
        DistributedCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var useDistributed = await ShouldUseDistributedCacheAsync(cancellationToken);

        if (useDistributed && _distributedCache != null)
        {
            try
            {
                await _distributedCache.SetAsync(key, value, options ?? new(), cancellationToken);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Distributed cache set failed for key {Key}, falling back to memory cache",
                    key);

                // Fall through to memory cache
            }
        }

        // Use memory cache - convert DistributedCacheEntryOptions to MemoryCacheEntryOptions
        var builder = new CacheOptionsBuilder();

        if (options != null)
        {
            if (options.AbsoluteExpiration.HasValue)
            {
                builder.WithAbsoluteExpiration(options.AbsoluteExpiration.Value);
            }
            else if (options.AbsoluteExpirationRelativeToNow.HasValue)
            {
                builder.WithAbsoluteExpiration(options.AbsoluteExpirationRelativeToNow.Value);
            }

            if (options.SlidingExpiration.HasValue)
            {
                builder.WithSlidingExpiration(options.SlidingExpiration.Value);
            }
        }

        _memoryCache.Set(key, value, builder.BuildMemory());
    }

    /// <summary>
    /// Removes a value from cache.
    /// </summary>
    public async Task RemoveAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var useDistributed = await ShouldUseDistributedCacheAsync(cancellationToken);

        if (useDistributed && _distributedCache != null)
        {
            try
            {
                await _distributedCache.RemoveAsync(key, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Distributed cache remove failed for key {Key}",
                    key);
            }
        }

        _memoryCache.Remove(key);
    }

    private async Task<bool> ShouldUseDistributedCacheAsync(CancellationToken cancellationToken)
    {
        if (_distributedCache == null)
        {
            return false;
        }

        var status = await _featureManagement.GetFeatureStatusAsync(
            "AdvancedCaching",
            cancellationToken);

        // Use distributed cache only if healthy
        return status.IsAvailable && !status.IsDegraded;
    }
}

/// <summary>
/// Typed cache service with automatic serialization.
/// </summary>
public sealed class AdaptiveTypedCacheService<T> where T : class
{
    private readonly AdaptiveCacheService _cacheService;
    private readonly ILogger<AdaptiveTypedCacheService<T>> _logger;

    public AdaptiveTypedCacheService(
        AdaptiveCacheService cacheService,
        ILogger<AdaptiveTypedCacheService<T>> logger)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<T?> GetAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var bytes = await _cacheService.GetAsync(key, cancellationToken);
        if (bytes == null)
        {
            return null;
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<T>(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize cached value for key {Key}", key);
            return null;
        }
    }

    public async Task SetAsync(
        string key,
        T value,
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value);

            var builder = new CacheOptionsBuilder();

            if (absoluteExpiration.HasValue)
            {
                builder.WithAbsoluteExpiration(absoluteExpiration.Value);
            }

            if (slidingExpiration.HasValue)
            {
                builder.WithSlidingExpiration(slidingExpiration.Value);
            }

            await _cacheService.SetAsync(key, bytes, builder.BuildDistributed(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to serialize and cache value for key {Key}", key);
        }
    }

    public Task RemoveAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        return _cacheService.RemoveAsync(key, cancellationToken);
    }
}
