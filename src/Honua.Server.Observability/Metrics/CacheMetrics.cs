// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Diagnostics.Metrics;

namespace Honua.Server.Observability.Metrics;

/// <summary>
/// Provides metrics instrumentation for cache performance.
/// </summary>
public class CacheMetrics
{
    private readonly Counter<long> cacheLookups;
    private readonly ObservableGauge<long> cacheEntries;
    private readonly Counter<long> cacheSavingsSeconds;
    private readonly Histogram<double> deduplicationRatio;
    private readonly Counter<long> cacheEvictions;

    private long currentCacheEntries;

    /// <summary>
    /// Initializes a new instance of the <see cref="CacheMetrics"/> class.
    /// </summary>
    /// <param name="meterFactory">The meter factory.</param>
    public CacheMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Honua.Cache");

        this.cacheLookups = meter.CreateCounter<long>(
            "cache_lookups_total",
            description: "Total number of cache lookups");

        this.cacheEntries = meter.CreateObservableGauge(
            "cache_entries_total",
            observeValue: () => this.currentCacheEntries,
            description: "Current number of entries in cache");

        this.cacheSavingsSeconds = meter.CreateCounter<long>(
            "cache_savings_seconds_total",
            unit: "s",
            description: "Total seconds saved by cache hits");

        this.deduplicationRatio = meter.CreateHistogram<double>(
            "cache_deduplication_ratio",
            description: "Ratio of deduplicated cache entries");

        this.cacheEvictions = meter.CreateCounter<long>(
            "cache_evictions_total",
            description: "Total number of cache evictions");
    }

    /// <summary>
    /// Records a cache lookup operation.
    /// </summary>
    /// <param name="hit">Whether the cache hit.</param>
    /// <param name="tier">The tier.</param>
    /// <param name="architecture">The architecture.</param>
    public void RecordCacheLookup(bool hit, string tier, string architecture)
    {
        this.cacheLookups.Add(1,
            new KeyValuePair<string, object?>("result", hit ? "hit" : "miss"),
            new KeyValuePair<string, object?>("tier", tier),
            new KeyValuePair<string, object?>("architecture", architecture));
    }

    /// <summary>
    /// Records time saved by a cache hit.
    /// </summary>
    /// <param name="savedTime">The saved time.</param>
    /// <param name="tier">The tier.</param>
    public void RecordCacheSavings(TimeSpan savedTime, string tier)
    {
        this.cacheSavingsSeconds.Add((long)savedTime.TotalSeconds,
            new KeyValuePair<string, object?>("tier", tier));
    }

    /// <summary>
    /// Records cache deduplication efficiency.
    /// </summary>
    /// <param name="ratio">The deduplication ratio.</param>
    /// <param name="tier">The tier.</param>
    public void RecordDeduplication(double ratio, string tier)
    {
        this.deduplicationRatio.Record(ratio,
            new KeyValuePair<string, object?>("tier", tier));
    }

    /// <summary>
    /// Records a cache eviction event.
    /// </summary>
    /// <param name="reason">The eviction reason.</param>
    /// <param name="tier">The tier.</param>
    public void RecordEviction(string reason, string tier)
    {
        this.cacheEvictions.Add(1,
            new KeyValuePair<string, object?>("reason", reason),
            new KeyValuePair<string, object?>("tier", tier));
    }

    /// <summary>
    /// Updates the current cache entry count.
    /// </summary>
    /// <param name="count">The cache entry count.</param>
    public void UpdateCacheEntryCount(long count)
    {
        this.currentCacheEntries = count;
    }
}
