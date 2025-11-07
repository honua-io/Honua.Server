using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Honua.MapSDK.Services.DataLoading;

/// <summary>
/// In-memory cache with LRU (Least Recently Used) eviction policy.
/// Provides efficient caching for GeoJSON and other data with configurable TTL.
/// </summary>
public class DataCache : IDisposable
{
    private readonly CacheOptions _options;
    private readonly ConcurrentDictionary<string, CacheEntry<object>> _cache = new();
    private readonly SemaphoreSlim _cleanupLock = new(1, 1);
    private readonly Timer _cleanupTimer;
    private long _currentSizeBytes;
    private bool _disposed;

    // Statistics
    private long _hits;
    private long _misses;
    private long _evictions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataCache"/> class.
    /// </summary>
    /// <param name="options">Cache configuration options.</param>
    public DataCache(CacheOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        // Start cleanup timer (runs every minute)
        _cleanupTimer = new Timer(
            _ => CleanupExpiredEntries(),
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1)
        );
    }

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    public CacheStatistics Statistics => new()
    {
        Hits = _hits,
        Misses = _misses,
        Evictions = _evictions,
        ItemCount = _cache.Count,
        SizeBytes = _currentSizeBytes,
        HitRate = _hits + _misses > 0 ? (double)_hits / (_hits + _misses) : 0
    };

    /// <summary>
    /// Gets a cached value by key.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <returns>The cached value, or null if not found or expired.</returns>
    public T? Get<T>(string key)
    {
        if (!_options.Enabled)
            return default;

        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));

        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.IsExpired)
            {
                // Remove expired entry
                _cache.TryRemove(key, out _);
                Interlocked.Decrement(ref _currentSizeBytes);
                Interlocked.Increment(ref _misses);
                return default;
            }

            // Update access time for LRU
            entry.LastAccessedAt = DateTime.UtcNow;
            entry.AccessCount++;

            if (_options.EnableStatistics)
                Interlocked.Increment(ref _hits);

            return (T)entry.Data;
        }

        if (_options.EnableStatistics)
            Interlocked.Increment(ref _misses);

        return default;
    }

    /// <summary>
    /// Tries to get a cached value by key.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The cached value if found.</param>
    /// <returns>True if the value was found and not expired; otherwise, false.</returns>
    public bool TryGet<T>(string key, out T? value)
    {
        value = Get<T>(key);
        return value != null;
    }

    /// <summary>
    /// Sets a value in the cache with the default TTL.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    public void Set<T>(string key, T value)
    {
        Set(key, value, TimeSpan.FromSeconds(_options.DefaultTtlSeconds));
    }

    /// <summary>
    /// Sets a value in the cache with a specific TTL.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="ttl">Time-to-live for the cached entry.</param>
    public void Set<T>(string key, T value, TimeSpan ttl)
    {
        if (!_options.Enabled)
            return;

        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));

        if (value == null)
            throw new ArgumentNullException(nameof(value));

        var now = DateTime.UtcNow;
        var sizeBytes = EstimateSize(value);

        // Check if we need to evict entries to make room
        if (_cache.Count >= _options.MaxItems ||
            _currentSizeBytes + sizeBytes > _options.MaxSizeMB * 1024 * 1024)
        {
            EvictLeastRecentlyUsed();
        }

        var entry = new CacheEntry<object>
        {
            Data = value!,
            CreatedAt = now,
            ExpiresAt = now.Add(ttl),
            LastAccessedAt = now,
            AccessCount = 0,
            SizeBytes = sizeBytes
        };

        _cache.AddOrUpdate(key, entry, (_, _) => entry);
        Interlocked.Add(ref _currentSizeBytes, sizeBytes);
    }

    /// <summary>
    /// Removes a value from the cache.
    /// </summary>
    /// <param name="key">The cache key to remove.</param>
    /// <returns>True if the key was found and removed; otherwise, false.</returns>
    public bool Remove(string key)
    {
        if (_cache.TryRemove(key, out var entry))
        {
            Interlocked.Add(ref _currentSizeBytes, -entry.SizeBytes);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Clears all entries from the cache.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
        Interlocked.Exchange(ref _currentSizeBytes, 0);
    }

    /// <summary>
    /// Gets a value from the cache or creates it if not found.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="factory">Factory function to create the value if not cached.</param>
    /// <returns>The cached or newly created value.</returns>
    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory)
    {
        return await GetOrCreateAsync(key, factory, TimeSpan.FromSeconds(_options.DefaultTtlSeconds));
    }

    /// <summary>
    /// Gets a value from the cache or creates it if not found, with a specific TTL.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="factory">Factory function to create the value if not cached.</param>
    /// <param name="ttl">Time-to-live for the cached entry.</param>
    /// <returns>The cached or newly created value.</returns>
    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl)
    {
        // Try to get from cache first
        if (TryGet<T>(key, out var cachedValue) && cachedValue != null)
        {
            return cachedValue;
        }

        // Not in cache, create it
        var value = await factory();
        Set(key, value, ttl);
        return value;
    }

    /// <summary>
    /// Evicts the least recently used entries to make room for new entries.
    /// </summary>
    private void EvictLeastRecentlyUsed()
    {
        var sortedEntries = _cache
            .OrderBy(kvp => kvp.Value.LastAccessedAt)
            .Take(_cache.Count / 4) // Remove 25% of entries
            .ToList();

        foreach (var kvp in sortedEntries)
        {
            if (_cache.TryRemove(kvp.Key, out var entry))
            {
                Interlocked.Add(ref _currentSizeBytes, -entry.SizeBytes);
                if (_options.EnableStatistics)
                    Interlocked.Increment(ref _evictions);
            }
        }
    }

    /// <summary>
    /// Removes expired entries from the cache.
    /// </summary>
    private void CleanupExpiredEntries()
    {
        if (!_cleanupLock.Wait(0))
            return; // Cleanup already in progress

        try
        {
            var expiredKeys = _cache
                .Where(kvp => kvp.Value.IsExpired)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                Remove(key);
            }
        }
        finally
        {
            _cleanupLock.Release();
        }
    }

    /// <summary>
    /// Estimates the size of a cached object in bytes.
    /// </summary>
    private static long EstimateSize<T>(T value)
    {
        try
        {
            // For strings, use actual byte size
            if (value is string str)
            {
                return Encoding.UTF8.GetByteCount(str);
            }

            // For other types, serialize to JSON and measure
            var json = JsonSerializer.Serialize(value);
            return Encoding.UTF8.GetByteCount(json);
        }
        catch
        {
            // If serialization fails, use a conservative estimate
            return 1024; // 1 KB default
        }
    }

    /// <summary>
    /// Disposes the cache and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _cleanupTimer?.Dispose();
        _cleanupLock?.Dispose();
        Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Cache statistics for monitoring and debugging.
/// </summary>
public class CacheStatistics
{
    /// <summary>
    /// Gets or sets the number of cache hits.
    /// </summary>
    public long Hits { get; set; }

    /// <summary>
    /// Gets or sets the number of cache misses.
    /// </summary>
    public long Misses { get; set; }

    /// <summary>
    /// Gets or sets the number of evicted entries.
    /// </summary>
    public long Evictions { get; set; }

    /// <summary>
    /// Gets or sets the current number of cached items.
    /// </summary>
    public int ItemCount { get; set; }

    /// <summary>
    /// Gets or sets the current cache size in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the cache hit rate (0.0 to 1.0).
    /// </summary>
    public double HitRate { get; set; }
}
