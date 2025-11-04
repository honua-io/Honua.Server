// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Caching;

/// <summary>
/// Collects and reports metrics for all memory caches to monitor for memory exhaustion.
/// Provides visibility into cache sizes, hit rates, and eviction patterns.
/// </summary>
public sealed class CacheMetricsCollector : IDisposable
{
    private readonly ILogger<CacheMetricsCollector> _logger;
    private readonly CacheSizeLimitOptions _options;
    private readonly ConcurrentDictionary<string, CacheMetrics> _cacheMetrics;

    // Metrics
    private readonly Counter<long> _cacheHitsCounter;
    private readonly Counter<long> _cacheMissesCounter;
    private readonly Counter<long> _cacheEvictionsCounter;
    private readonly ObservableGauge<int> _cacheEntriesGauge;
    private readonly ObservableGauge<long> _cacheSizeBytesGauge;
    private readonly ObservableGauge<double> _cacheHitRateGauge;

    private static readonly Meter Meter = new("Honua.Server.Core.Caching", "1.0.0");

    public CacheMetricsCollector(
        ILogger<CacheMetricsCollector> logger,
        IOptions<CacheSizeLimitOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _cacheMetrics = new ConcurrentDictionary<string, CacheMetrics>(StringComparer.OrdinalIgnoreCase);

        // Initialize metrics
        _cacheHitsCounter = Meter.CreateCounter<long>(
            "honua.cache.hits",
            description: "Number of cache hits");

        _cacheMissesCounter = Meter.CreateCounter<long>(
            "honua.cache.misses",
            description: "Number of cache misses");

        _cacheEvictionsCounter = Meter.CreateCounter<long>(
            "honua.cache.evictions",
            description: "Number of cache evictions");

        _cacheEntriesGauge = Meter.CreateObservableGauge<int>(
            "honua.cache.entries",
            () => GetTotalEntries(),
            description: "Total number of cache entries across all caches");

        _cacheSizeBytesGauge = Meter.CreateObservableGauge<long>(
            "honua.cache.size_bytes",
            () => GetTotalSizeBytes(),
            description: "Total cache size in bytes across all caches");

        _cacheHitRateGauge = Meter.CreateObservableGauge<double>(
            "honua.cache.hit_rate",
            () => GetOverallHitRate(),
            description: "Overall cache hit rate (0-1)");

        _logger.LogInformation(
            "Cache metrics collector initialized. MaxSize={MaxSizeMB}MB, MaxEntries={MaxEntries}",
            _options.MaxTotalSizeMB,
            _options.MaxTotalEntries);
    }

    /// <summary>
    /// Records a cache hit for a specific cache.
    /// </summary>
    public void RecordHit(string cacheName)
    {
        if (!_options.EnableMetrics)
        {
            return;
        }

        var metrics = _cacheMetrics.GetOrAdd(cacheName, _ => new CacheMetrics(cacheName));
        Interlocked.Increment(ref metrics.Hits);

        _cacheHitsCounter.Add(1, new KeyValuePair<string, object?>("cache", cacheName));
    }

    /// <summary>
    /// Records a cache miss for a specific cache.
    /// </summary>
    public void RecordMiss(string cacheName)
    {
        if (!_options.EnableMetrics)
        {
            return;
        }

        var metrics = _cacheMetrics.GetOrAdd(cacheName, _ => new CacheMetrics(cacheName));
        Interlocked.Increment(ref metrics.Misses);

        _cacheMissesCounter.Add(1, new KeyValuePair<string, object?>("cache", cacheName));
    }

    /// <summary>
    /// Records a cache eviction for a specific cache.
    /// </summary>
    public void RecordEviction(string cacheName, EvictionReason reason)
    {
        if (!_options.EnableMetrics)
        {
            return;
        }

        var metrics = _cacheMetrics.GetOrAdd(cacheName, _ => new CacheMetrics(cacheName));
        Interlocked.Increment(ref metrics.Evictions);

        _cacheEvictionsCounter.Add(1,
            new KeyValuePair<string, object?>("cache", cacheName),
            new KeyValuePair<string, object?>("reason", reason.ToString()));

        // Log warning if eviction is due to capacity
        if (reason == EvictionReason.Capacity)
        {
            _logger.LogWarning(
                "Cache {CacheName} evicted entry due to capacity limit. Consider increasing cache size limits.",
                cacheName);
        }
    }

    /// <summary>
    /// Updates the current entry count for a specific cache.
    /// </summary>
    public void UpdateEntryCount(string cacheName, int count)
    {
        if (!_options.EnableMetrics)
        {
            return;
        }

        var metrics = _cacheMetrics.GetOrAdd(cacheName, _ => new CacheMetrics(cacheName));
        metrics.EntryCount = count;
    }

    /// <summary>
    /// Updates the current size in bytes for a specific cache.
    /// </summary>
    public void UpdateSizeBytes(string cacheName, long sizeBytes)
    {
        if (!_options.EnableMetrics)
        {
            return;
        }

        var metrics = _cacheMetrics.GetOrAdd(cacheName, _ => new CacheMetrics(cacheName));
        metrics.SizeBytes = sizeBytes;
    }

    /// <summary>
    /// Gets statistics for a specific cache.
    /// </summary>
    public CacheStatisticsSnapshot? GetCacheStatistics(string cacheName)
    {
        if (!_cacheMetrics.TryGetValue(cacheName, out var metrics))
        {
            return null;
        }

        return new CacheStatisticsSnapshot
        {
            CacheName = cacheName,
            Hits = Interlocked.Read(ref metrics.Hits),
            Misses = Interlocked.Read(ref metrics.Misses),
            Evictions = Interlocked.Read(ref metrics.Evictions),
            EntryCount = metrics.EntryCount,
            SizeBytes = metrics.SizeBytes,
            HitRate = metrics.GetHitRate()
        };
    }

    /// <summary>
    /// Gets statistics for all caches.
    /// </summary>
    public CacheStatisticsSnapshot GetOverallStatistics()
    {
        long totalHits = 0;
        long totalMisses = 0;
        long totalEvictions = 0;
        int totalEntries = 0;
        long totalSize = 0;

        foreach (var metrics in _cacheMetrics.Values)
        {
            totalHits += Interlocked.Read(ref metrics.Hits);
            totalMisses += Interlocked.Read(ref metrics.Misses);
            totalEvictions += Interlocked.Read(ref metrics.Evictions);
            totalEntries += metrics.EntryCount;
            totalSize += metrics.SizeBytes;
        }

        return new CacheStatisticsSnapshot
        {
            CacheName = "Overall",
            Hits = totalHits,
            Misses = totalMisses,
            Evictions = totalEvictions,
            EntryCount = totalEntries,
            SizeBytes = totalSize,
            HitRate = (totalHits + totalMisses) > 0 ? (double)totalHits / (totalHits + totalMisses) : 0
        };
    }

    private int GetTotalEntries()
    {
        int total = 0;
        foreach (var metrics in _cacheMetrics.Values)
        {
            total += metrics.EntryCount;
        }
        return total;
    }

    private long GetTotalSizeBytes()
    {
        long total = 0;
        foreach (var metrics in _cacheMetrics.Values)
        {
            total += metrics.SizeBytes;
        }
        return total;
    }

    private double GetOverallHitRate()
    {
        long totalHits = 0;
        long totalMisses = 0;

        foreach (var metrics in _cacheMetrics.Values)
        {
            totalHits += Interlocked.Read(ref metrics.Hits);
            totalMisses += Interlocked.Read(ref metrics.Misses);
        }

        return (totalHits + totalMisses) > 0 ? (double)totalHits / (totalHits + totalMisses) : 0;
    }

    public void Dispose()
    {
        Meter.Dispose();
    }

    private sealed class CacheMetrics
    {
        public string CacheName { get; }
        public long Hits;
        public long Misses;
        public long Evictions;
        public int EntryCount;
        public long SizeBytes;

        public CacheMetrics(string cacheName)
        {
            CacheName = cacheName;
        }

        public double GetHitRate()
        {
            var hits = Interlocked.Read(ref Hits);
            var misses = Interlocked.Read(ref Misses);
            return (hits + misses) > 0 ? (double)hits / (hits + misses) : 0;
        }
    }
}

/// <summary>
/// Snapshot of cache statistics at a point in time.
/// </summary>
public sealed class CacheStatisticsSnapshot
{
    public string CacheName { get; init; } = string.Empty;
    public long Hits { get; init; }
    public long Misses { get; init; }
    public long Evictions { get; init; }
    public int EntryCount { get; init; }
    public long SizeBytes { get; init; }
    public double HitRate { get; init; }

    public override string ToString()
    {
        return $"{CacheName}: {EntryCount} entries, {SizeBytes / 1024.0:F1} KB, " +
               $"Hit Rate: {HitRate:P2}, Hits: {Hits}, Misses: {Misses}, Evictions: {Evictions}";
    }
}
