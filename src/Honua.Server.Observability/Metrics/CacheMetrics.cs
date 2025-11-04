// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Diagnostics.Metrics;

namespace Honua.Server.Observability.Metrics;

/// <summary>
/// Provides metrics instrumentation for cache performance.
/// </summary>
public class CacheMetrics
{
    private readonly Counter<long> _cacheLookups;
    private readonly ObservableGauge<long> _cacheEntries;
    private readonly Counter<long> _cacheSavingsSeconds;
    private readonly Histogram<double> _deduplicationRatio;
    private readonly Counter<long> _cacheEvictions;

    private long _currentCacheEntries;

    public CacheMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Honua.Cache");

        _cacheLookups = meter.CreateCounter<long>(
            "cache_lookups_total",
            description: "Total number of cache lookups");

        _cacheEntries = meter.CreateObservableGauge(
            "cache_entries_total",
            observeValue: () => _currentCacheEntries,
            description: "Current number of entries in cache");

        _cacheSavingsSeconds = meter.CreateCounter<long>(
            "cache_savings_seconds_total",
            unit: "s",
            description: "Total seconds saved by cache hits");

        _deduplicationRatio = meter.CreateHistogram<double>(
            "cache_deduplication_ratio",
            description: "Ratio of deduplicated cache entries");

        _cacheEvictions = meter.CreateCounter<long>(
            "cache_evictions_total",
            description: "Total number of cache evictions");
    }

    /// <summary>
    /// Records a cache lookup operation.
    /// </summary>
    public void RecordCacheLookup(bool hit, string tier, string architecture)
    {
        _cacheLookups.Add(1,
            new KeyValuePair<string, object?>("result", hit ? "hit" : "miss"),
            new KeyValuePair<string, object?>("tier", tier),
            new KeyValuePair<string, object?>("architecture", architecture));
    }

    /// <summary>
    /// Records time saved by a cache hit.
    /// </summary>
    public void RecordCacheSavings(TimeSpan savedTime, string tier)
    {
        _cacheSavingsSeconds.Add((long)savedTime.TotalSeconds,
            new KeyValuePair<string, object?>("tier", tier));
    }

    /// <summary>
    /// Records cache deduplication efficiency.
    /// </summary>
    public void RecordDeduplication(double ratio, string tier)
    {
        _deduplicationRatio.Record(ratio,
            new KeyValuePair<string, object?>("tier", tier));
    }

    /// <summary>
    /// Records a cache eviction event.
    /// </summary>
    public void RecordEviction(string reason, string tier)
    {
        _cacheEvictions.Add(1,
            new KeyValuePair<string, object?>("reason", reason),
            new KeyValuePair<string, object?>("tier", tier));
    }

    /// <summary>
    /// Updates the current cache entry count.
    /// </summary>
    public void UpdateCacheEntryCount(long count)
    {
        _currentCacheEntries = count;
    }
}
