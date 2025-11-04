// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Authorization;

/// <summary>
/// Provides caching for resource authorization decisions to improve performance.
/// </summary>
public interface IResourceAuthorizationCache
{
    /// <summary>
    /// Attempts to get a cached authorization result.
    /// </summary>
    bool TryGet(string cacheKey, out ResourceAuthorizationResult result);

    /// <summary>
    /// Stores an authorization result in the cache.
    /// </summary>
    void Set(string cacheKey, ResourceAuthorizationResult result);

    /// <summary>
    /// Invalidates all cached entries for a specific resource.
    /// </summary>
    void InvalidateResource(string resourceType, string resourceId);

    /// <summary>
    /// Invalidates all cached entries.
    /// </summary>
    void InvalidateAll();

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    CacheStatistics GetStatistics();
}

/// <summary>
/// Default implementation of resource authorization cache using IMemoryCache.
/// </summary>
public sealed class ResourceAuthorizationCache : IResourceAuthorizationCache
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<ResourceAuthorizationCache> _logger;
    private readonly ResourceAuthorizationOptions _options;
    private readonly ConcurrentDictionary<string, byte> _cacheKeys;
    private long _hits;
    private long _misses;
    private long _evictions;

    public ResourceAuthorizationCache(
        IMemoryCache cache,
        ILogger<ResourceAuthorizationCache> logger,
        IOptionsMonitor<ResourceAuthorizationOptions> options)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.CurrentValue ?? throw new ArgumentNullException(nameof(options));
        _cacheKeys = new ConcurrentDictionary<string, byte>();
    }

    public bool TryGet(string cacheKey, out ResourceAuthorizationResult result)
    {
        if (_cache.TryGetValue(cacheKey, out result!))
        {
            Interlocked.Increment(ref _hits);
            _logger.LogTrace("Authorization cache hit for key: {CacheKey}", cacheKey);
            return true;
        }

        Interlocked.Increment(ref _misses);
        _logger.LogTrace("Authorization cache miss for key: {CacheKey}", cacheKey);
        return false;
    }

    public void Set(string cacheKey, ResourceAuthorizationResult result)
    {
        // Check if we're at the cache size limit
        if (_options.MaxCacheSize > 0 && _cacheKeys.Count >= _options.MaxCacheSize)
        {
            _logger.LogWarning(
                "Authorization cache has reached maximum size limit of {MaxSize}. " +
                "Entry will be cached but may be evicted immediately. Consider increasing MaxCacheSize.",
                _options.MaxCacheSize);
        }

        var cacheOptions = new CacheOptionsBuilder()
            .WithAbsoluteExpiration(TimeSpan.FromSeconds(_options.CacheDurationSeconds))
            .WithSize(1) // Each authorization result counts as 1 entry toward global limit
            .BuildMemory();

        cacheOptions.RegisterPostEvictionCallback((key, value, reason, state) =>
        {
            _cacheKeys.TryRemove(key.ToString()!, out _);
            Interlocked.Increment(ref _evictions);

            _logger.LogTrace(
                "Authorization cache entry evicted: {Key}, Reason: {Reason}. Total evictions: {TotalEvictions}",
                key,
                reason,
                Interlocked.Read(ref _evictions));

            // Warn if eviction is due to capacity limits
            if (reason == EvictionReason.Capacity)
            {
                _logger.LogWarning(
                    "Authorization cache evicted entry {Key} due to capacity limit. " +
                    "Current entries: {CurrentEntries}, Max: {MaxSize}. Consider increasing cache limits.",
                    key,
                    _cacheKeys.Count,
                    _options.MaxCacheSize);
            }
        });

        _cache.Set(cacheKey, result with { FromCache = true }, cacheOptions);
        _cacheKeys.TryAdd(cacheKey, 0);

        _logger.LogTrace("Authorization result cached with key: {CacheKey}", cacheKey);
    }

    public void InvalidateResource(string resourceType, string resourceId)
    {
        var prefix = CacheKeyGenerator.GenerateAuthorizationKeyPrefix(resourceType, resourceId);
        var keysToRemove = _cacheKeys.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();

        foreach (var key in keysToRemove)
        {
            _cache.Remove(key);
            _cacheKeys.TryRemove(key, out _);
        }

        _logger.LogInformation(
            "Invalidated {Count} authorization cache entries for resource {ResourceType}:{ResourceId}",
            keysToRemove.Count,
            resourceType,
            resourceId);
    }

    public void InvalidateAll()
    {
        var count = _cacheKeys.Count;
        foreach (var key in _cacheKeys.Keys.ToList())
        {
            _cache.Remove(key);
            _cacheKeys.TryRemove(key, out _);
        }

        _logger.LogInformation("Invalidated all {Count} authorization cache entries", count);
    }

    public CacheStatistics GetStatistics()
    {
        var hits = Interlocked.Read(ref _hits);
        var misses = Interlocked.Read(ref _misses);

        return new CacheStatistics
        {
            Hits = hits,
            Misses = misses,
            Evictions = Interlocked.Read(ref _evictions),
            EntryCount = _cacheKeys.Count,
            MaxEntries = _options.MaxCacheSize,
            HitRate = (hits + misses) > 0 ? (double)hits / (hits + misses) : 0
        };
    }

    /// <summary>
    /// Builds a cache key for an authorization check.
    /// </summary>
    public static string BuildCacheKey(string userId, string resourceType, string resourceId, string operation)
    {
        return CacheKeyGenerator.GenerateAuthorizationKey(userId, resourceType, resourceId, operation);
    }
}

/// <summary>
/// Cache statistics for monitoring.
/// </summary>
public sealed record CacheStatistics
{
    public long Hits { get; init; }
    public long Misses { get; init; }
    public long Evictions { get; init; }
    public int EntryCount { get; init; }
    public int MaxEntries { get; init; }
    public double HitRate { get; init; }
}
