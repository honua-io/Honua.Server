// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Honua.Server.Host.Ogc.Services;

/// <summary>
/// Tracks metrics for filter parsing cache performance.
/// Provides insights into cache hit rate, parse time saved, and memory usage.
/// </summary>
public sealed class FilterParsingCacheMetrics : IDisposable
{
    private readonly Meter meter;
    private readonly Counter<long> cacheHitsCounter;
    private readonly Counter<long> cacheMissesCounter;
    private readonly Counter<long> evictionsCounter;
    private readonly Histogram<double> parseTimeHistogram;
    private readonly Histogram<long> cacheEntrySizeHistogram;

    // In-memory counters for summary statistics
    private long _totalHits;
    private long _totalMisses;
    private long _totalParseTimeMs;
    private long _totalEvictions;
    private long _totalEvictedBytes;

    public FilterParsingCacheMetrics()
    {
        // Create a meter for filter parsing cache metrics
        this.meter = new Meter("Honua.Server.FilterParsingCache", "1.0");

        // Counter for cache hits
        this.cacheHitsCounter = this.meter.CreateCounter<long>(
            "honua.filter_cache.hits",
            unit: "{hit}",
            description: "Number of filter parsing cache hits");

        // Counter for cache misses
        this.cacheMissesCounter = this.meter.CreateCounter<long>(
            "honua.filter_cache.misses",
            unit: "{miss}",
            description: "Number of filter parsing cache misses");

        // Counter for cache evictions
        this.evictionsCounter = this.meter.CreateCounter<long>(
            "honua.filter_cache.evictions",
            unit: "{eviction}",
            description: "Number of filter cache evictions");

        // Histogram for parse time (milliseconds)
        this.parseTimeHistogram = this.meter.CreateHistogram<double>(
            "honua.filter_cache.parse_time",
            unit: "ms",
            description: "Time spent parsing filters (cache misses only)");

        // Histogram for cache entry size (bytes)
        this.cacheEntrySizeHistogram = this.meter.CreateHistogram<long>(
            "honua.filter_cache.entry_size",
            unit: "bytes",
            description: "Estimated size of cached filter entries");

        // Observable gauge for cache hit rate
        this.meter.CreateObservableGauge(
            "honua.filter_cache.hit_rate",
            () => ComputeHitRate(),
            unit: "{ratio}",
            description: "Filter cache hit rate (hits / total requests)");

        // Observable gauge for total parse time saved
        this.meter.CreateObservableGauge(
            "honua.filter_cache.time_saved_ms",
            () => _totalParseTimeMs,
            unit: "ms",
            description: "Cumulative parse time saved by cache hits (estimated)");
    }

    /// <summary>
    /// Records a cache hit.
    /// </summary>
    public void RecordCacheHit(string serviceId, string layerId, string filterLanguage)
    {
        System.Threading.Interlocked.Increment(ref _totalHits);

        var tags = new TagList
        {
            { "service_id", serviceId },
            { "layer_id", layerId },
            { "filter_language", filterLanguage },
            { "result", "hit" }
        };

        this.cacheHitsCounter.Add(1, tags);
    }

    /// <summary>
    /// Records a cache miss and the time spent parsing.
    /// </summary>
    public void RecordCacheMiss(string serviceId, string layerId, string filterLanguage, long parseTimeMs)
    {
        System.Threading.Interlocked.Increment(ref _totalMisses);
        System.Threading.Interlocked.Add(ref _totalParseTimeMs, parseTimeMs);

        var tags = new TagList
        {
            { "service_id", serviceId },
            { "layer_id", layerId },
            { "filter_language", filterLanguage },
            { "result", "miss" }
        };

        this.cacheMissesCounter.Add(1, tags);
        this.parseTimeHistogram.Record(parseTimeMs, tags);
    }

    /// <summary>
    /// Records a cache eviction.
    /// </summary>
    public void RecordEviction(string reason, long sizeBytes)
    {
        System.Threading.Interlocked.Increment(ref _totalEvictions);
        System.Threading.Interlocked.Add(ref _totalEvictedBytes, sizeBytes);

        var tags = new TagList
        {
            { "reason", reason }
        };

        this.evictionsCounter.Add(1, tags);
        this.cacheEntrySizeHistogram.Record(sizeBytes, tags);
    }

    /// <summary>
    /// Computes the current cache hit rate as a percentage.
    /// </summary>
    private double ComputeHitRate()
    {
        var hits = System.Threading.Interlocked.Read(ref _totalHits);
        var misses = System.Threading.Interlocked.Read(ref _totalMisses);
        var total = hits + misses;

        return total == 0 ? 0.0 : (double)hits / total;
    }

    /// <summary>
    /// Gets summary statistics for monitoring and logging.
    /// </summary>
    public FilterCacheStatistics GetStatistics()
    {
        var hits = System.Threading.Interlocked.Read(ref _totalHits);
        var misses = System.Threading.Interlocked.Read(ref _totalMisses);
        var parseTimeMs = System.Threading.Interlocked.Read(ref _totalParseTimeMs);
        var evictions = System.Threading.Interlocked.Read(ref _totalEvictions);
        var evictedBytes = System.Threading.Interlocked.Read(ref _totalEvictedBytes);

        return new FilterCacheStatistics
        {
            TotalHits = hits,
            TotalMisses = misses,
            HitRate = ComputeHitRate(),
            TotalParseTimeMs = parseTimeMs,
            TotalEvictions = evictions,
            TotalEvictedBytes = evictedBytes
        };
    }

    public void Dispose()
    {
        this.meter.Dispose();
    }
}

/// <summary>
/// Summary statistics for the filter parsing cache.
/// </summary>
public sealed record FilterCacheStatistics
{
    public long TotalHits { get; init; }
    public long TotalMisses { get; init; }
    public double HitRate { get; init; }
    public long TotalParseTimeMs { get; init; }
    public long TotalEvictions { get; init; }
    public long TotalEvictedBytes { get; init; }
    public long TotalRequests => TotalHits + TotalMisses;
}
