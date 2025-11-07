// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Honua.Server.Enterprise.ETL.Performance;

namespace Honua.Server.Enterprise.ETL.Caching;

/// <summary>
/// In-memory cache implementation for workflow data
/// </summary>
public class MemoryWorkflowCache : IWorkflowCache
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryWorkflowCache> _logger;
    private readonly IPerformanceMetrics? _metrics;
    private readonly CacheOptions _options;

    public MemoryWorkflowCache(
        IMemoryCache cache,
        IOptions<CacheOptions> options,
        ILogger<MemoryWorkflowCache> logger,
        IPerformanceMetrics? metrics = null)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics;
        _options = options?.Value ?? new CacheOptions();
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        var fullKey = GetFullKey(key);

        if (_cache.TryGetValue<T>(fullKey, out var value))
        {
            _logger.LogTrace("Cache hit: {Key}", fullKey);
            _metrics?.RecordCacheHit("memory", fullKey);
            return Task.FromResult<T?>(value);
        }

        _logger.LogTrace("Cache miss: {Key}", fullKey);
        _metrics?.RecordCacheMiss("memory", fullKey);
        return Task.FromResult<T?>(null);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default) where T : class
    {
        var fullKey = GetFullKey(key);
        var actualTtl = ttl ?? TimeSpan.FromMinutes(_options.DefaultTtlMinutes);

        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(actualTtl)
            .SetSize(EstimateSize(value));

        _cache.Set(fullKey, value, cacheEntryOptions);

        _logger.LogTrace("Cache set: {Key} (TTL: {Ttl})", fullKey, actualTtl);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var fullKey = GetFullKey(key);
        _cache.Remove(fullKey);
        _logger.LogTrace("Cache removed: {Key}", fullKey);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var fullKey = GetFullKey(key);
        return Task.FromResult(_cache.TryGetValue(fullKey, out _));
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        var value = await factory();
        await SetAsync(key, value, ttl, cancellationToken);
        return value;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        // MemoryCache doesn't have a Clear method, so we'd need to track keys
        // For now, log a warning
        _logger.LogWarning("MemoryCache.ClearAsync called but not fully implemented");
        return Task.CompletedTask;
    }

    private string GetFullKey(string key)
    {
        return key.StartsWith(_options.KeyPrefix) ? key : $"{_options.KeyPrefix}{key}";
    }

    private long EstimateSize<T>(T value)
    {
        // Simple size estimation - could be improved
        if (value is string str)
        {
            return str.Length * 2; // Unicode chars
        }

        // Default to 1KB for objects
        return 1024;
    }
}
