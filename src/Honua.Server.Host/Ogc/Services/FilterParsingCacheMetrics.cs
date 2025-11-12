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
    private readonly Meter _meter;
    private readonly Counter<long> _cacheHitsCounter;
    private readonly Counter<long> _cacheMissesCounter;
    private readonly Counter<long> _evictionsCounter;
    private readonly Histogram<double> _parseTimeHistogram;
    private readonly Histogram<long> _cacheEntrySizeHistogram;

    // In-memory counters for summary statistics
    private long _totalHits;
    private long _totalMisses;
    private long _totalParseTimeMs;
    private long _totalEvictions;
    private long _totalEvictedBytes;

    public FilterParsingCacheMetrics()
    {
        // Create a meter for filter parsing cache metrics
        _meter = new Meter("Honua.Server.FilterParsingCache", "1.0");

        // Counter for cache hits
        _cacheHitsCounter = _meter.CreateCounter<long>(
            "honua.filter_cache.hits",
            unit: "{hit}",
            description: "Number of filter parsing cache hits");

        // Counter for cache misses
        _cacheMissesCounter = _meter.CreateCounter<long>(
            "honua.filter_cache.misses",
            unit: "{miss}",
            description: "Number of filter parsing cache misses");

        // Counter for cache evictions
        _evictionsCounter = _meter.CreateCounter<long>(
            "honua.filter_cache.evictions",
            unit: "{eviction}",
            description: "Number of filter cache evictions");

        // Histogram for parse time (milliseconds)
        _parseTimeHistogram = _meter.CreateHistogram<double>(
            "honua.filter_cache.parse_time",
            unit: "ms",
            description: "Time spent parsing filters (cache misses only)");

        // Histogram for cache entry size (bytes)
        _cacheEntrySizeHistogram = _meter.CreateHistogram<long>(
            "honua.filter_cache.entry_size",
            unit: "bytes",
            description: "Estimated size of cached filter entries");

        // Observable gauge for cache hit rate
        _meter.CreateObservableGauge(
            "honua.filter_cache.hit_rate",
            () => ComputeHitRate(),
            unit: "{ratio}",
            description: "Filter cache hit rate (hits / total requests)");

        // Observable gauge for total parse time saved
        _meter.CreateObservableGauge(
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

        _cacheHitsCounter.Add(1, tags);
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

        _cacheMissesCounter.Add(1, tags);
        _parseTimeHistogram.Record(parseTimeMs, tags);
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

        _evictionsCounter.Add(1, tags);
        _cacheEntrySizeHistogram.Record(sizeBytes, tags);
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
        _meter.Dispose();
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
