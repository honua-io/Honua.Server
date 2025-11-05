// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// Client-side cache service for API responses with TTL support.
/// Reduces redundant API calls and improves performance.
/// </summary>
public class ClientCacheService
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly TimeSpan _defaultTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets a cached value or computes it using the factory function.
    /// </summary>
    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? ttl = null)
    {
        var effectiveTtl = ttl ?? _defaultTtl;

        // Check if cached and not expired
        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return (T)entry.Value;
            }

            // Remove expired entry
            _cache.TryRemove(key, out _);
        }

        // Compute new value
        var value = await factory();

        // Cache it
        var newEntry = new CacheEntry
        {
            Value = value!,
            ExpiresAt = DateTimeOffset.UtcNow.Add(effectiveTtl)
        };

        _cache.TryAdd(key, newEntry);

        return value;
    }

    /// <summary>
    /// Gets a cached value if available and not expired.
    /// </summary>
    public bool TryGet<T>(string key, out T? value)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt > DateTimeOffset.UtcNow)
            {
                value = (T)entry.Value;
                return true;
            }

            // Remove expired entry
            _cache.TryRemove(key, out _);
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Sets a value in the cache with optional TTL.
    /// </summary>
    public void Set<T>(string key, T value, TimeSpan? ttl = null)
    {
        var effectiveTtl = ttl ?? _defaultTtl;

        var entry = new CacheEntry
        {
            Value = value!,
            ExpiresAt = DateTimeOffset.UtcNow.Add(effectiveTtl)
        };

        _cache.AddOrUpdate(key, entry, (_, _) => entry);
    }

    /// <summary>
    /// Invalidates (removes) a cached entry.
    /// </summary>
    public void Invalidate(string key)
    {
        _cache.TryRemove(key, out _);
    }

    /// <summary>
    /// Invalidates all cache entries matching a prefix.
    /// </summary>
    public void InvalidatePrefix(string prefix)
    {
        var keysToRemove = _cache.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var key in keysToRemove)
        {
            _cache.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Clears all cached entries.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Gets the number of cached entries (including expired).
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// Removes all expired entries.
    /// </summary>
    public int RemoveExpired()
    {
        var now = DateTimeOffset.UtcNow;
        var expiredKeys = _cache.Where(kvp => kvp.Value.ExpiresAt <= now).Select(kvp => kvp.Key).ToList();

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }

        return expiredKeys.Count;
    }

    /// <summary>
    /// Gets cache statistics for monitoring.
    /// </summary>
    public CacheStats GetStats()
    {
        var now = DateTimeOffset.UtcNow;
        var entries = _cache.Values.ToList();

        return new CacheStats
        {
            TotalEntries = entries.Count,
            ExpiredEntries = entries.Count(e => e.ExpiresAt <= now),
            ActiveEntries = entries.Count(e => e.ExpiresAt > now),
            OldestExpirationTime = entries.Any() ? entries.Min(e => e.ExpiresAt) : (DateTimeOffset?)null,
            NewestExpirationTime = entries.Any() ? entries.Max(e => e.ExpiresAt) : (DateTimeOffset?)null
        };
    }

    private class CacheEntry
    {
        public object Value { get; set; } = null!;
        public DateTimeOffset ExpiresAt { get; set; }
    }
}

/// <summary>
/// Cache statistics for monitoring.
/// </summary>
public class CacheStats
{
    public int TotalEntries { get; set; }
    public int ExpiredEntries { get; set; }
    public int ActiveEntries { get; set; }
    public DateTimeOffset? OldestExpirationTime { get; set; }
    public DateTimeOffset? NewestExpirationTime { get; set; }
}

/// <summary>
/// Cache key builder for consistent cache keys.
/// </summary>
public static class CacheKeys
{
    public static string Services() => "services:all";
    public static string Service(string id) => $"services:{id}";
    public static string Layers() => "layers:all";
    public static string LayersByService(string serviceId) => $"layers:service:{serviceId}";
    public static string Layer(string id) => $"layers:{id}";
    public static string Folders() => "folders:all";
    public static string Folder(string id) => $"folders:{id}";
    public static string Styles() => "styles:all";
    public static string StylesByLayer(string layerId) => $"styles:layer:{layerId}";
    public static string Style(string id) => $"styles:{id}";
    public static string DashboardMetrics() => "dashboard:metrics";
}
