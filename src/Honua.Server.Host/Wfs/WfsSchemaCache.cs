// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Honua.Server.Core.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Host.Wfs;

/// <summary>
/// Default implementation of WFS schema cache using IMemoryCache.
/// </summary>
/// <remarks>
/// <para>
/// This cache implementation addresses the performance issue identified in code review
/// where WFS DescribeFeatureType operations repeatedly query database metadata for
/// schema information. By caching the generated XML Schema documents, we eliminate
/// redundant metadata queries and improve response times for GetFeature operations
/// that reference schemas.
/// </para>
/// <para>
/// <strong>Cache Key Strategy:</strong>
/// Keys are formatted as "wfs:schema:{collectionId}" to provide:
/// <list type="bullet">
/// <item>Namespace isolation from other cached data</item>
/// <item>Direct collection-based lookups without iteration</item>
/// <item>Simple prefix-based invalidation patterns</item>
/// </list>
/// </para>
/// <para>
/// <strong>Invalidation Scenarios:</strong>
/// <list type="bullet">
/// <item>Collection schema modifications (field add/remove/rename)</item>
/// <item>Collection deletion</item>
/// <item>Administrative metadata reloads</item>
/// <item>TTL expiration (configurable, default 24 hours)</item>
/// </list>
/// </para>
/// <para>
/// <strong>Metrics:</strong>
/// Cache operations are instrumented with hit/miss counters and entry count gauges
/// for operational visibility via the standard honua.wfs.* metric namespace.
/// </para>
/// </remarks>
public sealed class WfsSchemaCache : IWfsSchemaCache
{
    private readonly IMemoryCache cache;
    private readonly ILogger<WfsSchemaCache> logger;
    private readonly WfsOptions options;
    private readonly ConcurrentDictionary<string, byte> _cacheKeys;
    private readonly Counter<long> hitCounter;
    private readonly Counter<long> missCounter;
    private readonly Counter<long> evictionsCounter;
    private readonly ObservableGauge<int> entriesGauge;
    private long _hits;
    private long _misses;
    private long _evictions;

    /// <summary>
    /// Cache key prefix for all WFS schema entries.
    /// </summary>
    private const string CacheKeyPrefix = "wfs:schema:";

    public WfsSchemaCache(
        IMemoryCache cache,
        ILogger<WfsSchemaCache> logger,
        IOptionsMonitor<WfsOptions> options)
    {
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.options = options?.CurrentValue ?? throw new ArgumentNullException(nameof(options));
        this.cacheKeys = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        // Initialize metrics
        this.hitCounter = WfsMetrics.SchemaCacheHits;

        this.missCounter = WfsMetrics.SchemaCacheMisses;

        this.evictionsCounter = WfsMetrics.Meter.CreateCounter<long>(
            "honua.wfs.schema_cache.evictions",
            description: "Number of WFS schema cache evictions");

        this.entriesGauge = WfsMetrics.Meter.CreateObservableGauge<int>(
            "honua.wfs.schema_cache.entries",
            () => this.cacheKeys.Count,
            description: "Number of cached WFS schemas");
    }

    /// <inheritdoc />
    public bool TryGetSchema(string collectionId, out XDocument? schema)
    {
        if (string.IsNullOrWhiteSpace(collectionId))
        {
            schema = null;
            return false;
        }

        if (!this.options.EnableSchemaCaching || this.options.DescribeFeatureTypeCacheDuration <= 0)
        {
            schema = null;
            return false;
        }

        var cacheKey = BuildCacheKey(collectionId);

        if (this.cache.TryGetValue(cacheKey, out schema))
        {
            Interlocked.Increment(ref _hits);
            this.hitCounter.Add(1, new KeyValuePair<string, object?>("collection_id", collectionId));
            this.logger.LogDebug("WFS schema cache hit for collection: {CollectionId}", collectionId);
            return true;
        }

        Interlocked.Increment(ref _misses);
        this.missCounter.Add(1, new KeyValuePair<string, object?>("collection_id", collectionId));
        this.logger.LogDebug("WFS schema cache miss for collection: {CollectionId}", collectionId);
        return false;
    }

    /// <inheritdoc />
    public Task SetSchemaAsync(string collectionId, XDocument schema, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(collectionId))
        {
            throw new ArgumentException("Collection ID cannot be null or whitespace.", nameof(collectionId));
        }

        if (schema is null)
        {
            throw new ArgumentNullException(nameof(schema));
        }

        if (!this.options.EnableSchemaCaching || this.options.DescribeFeatureTypeCacheDuration <= 0)
        {
            this.logger.LogTrace("WFS schema caching is disabled, skipping cache storage");
            return Task.CompletedTask;
        }

        var cacheKey = BuildCacheKey(collectionId);

        // Check if we're at the cache size limit
        if (this.options.MaxCachedSchemas > 0 && this.cacheKeys.Count >= this.options.MaxCachedSchemas)
        {
            this.logger.LogWarning(
                "WFS schema cache has reached maximum size limit of {MaxSchemas}. " +
                "Entry will be cached but may be evicted immediately. Consider increasing MaxCachedSchemas.",
                this.options.MaxCachedSchemas);
        }

        // Estimate schema size for cache accounting
        // Average schema is ~2-5 KB, use conservative estimate
        var estimatedSizeBytes = EstimateSchemaSize(schema);

        // Build cache options with configurable TTL and size
        var cacheOptions = new CacheOptionsBuilder()
            .WithAbsoluteExpiration(TimeSpan.FromSeconds(this.options.DescribeFeatureTypeCacheDuration))
            .WithPriority(CacheItemPriority.Normal)
            .WithSize(1) // Each schema counts as 1 entry toward global limit
            .BuildMemory();

        // Register eviction callback to track cache keys and metrics
        cacheOptions.RegisterPostEvictionCallback((key, value, reason, state) =>
        {
            this.cacheKeys.TryRemove(key.ToString()!, out _);
            Interlocked.Increment(ref _evictions);
            this.evictionsCounter.Add(1,
                new KeyValuePair<string, object?>("collection_id", collectionId),
                new KeyValuePair<string, object?>("reason", reason.ToString()));

            this.logger.LogDebug(
                "WFS schema cache entry evicted for collection {CollectionId}, reason: {Reason}. " +
                "Total evictions: {TotalEvictions}",
                collectionId,
                reason,
                Interlocked.Read(ref _evictions));

            // Warn if eviction is due to capacity limits
            if (reason == EvictionReason.Capacity)
            {
                this.logger.LogWarning(
                    "WFS schema cache evicted entry for {CollectionId} due to capacity limit. " +
                    "Current entries: {CurrentEntries}, Max: {MaxSchemas}. Consider increasing cache limits.",
                    collectionId,
                    this.cacheKeys.Count,
                    this.options.MaxCachedSchemas);
            }
        });

        this.cache.Set(cacheKey, schema, cacheOptions);
        this.cacheKeys.TryAdd(cacheKey, 0);

        this.logger.LogDebug(
            "WFS schema cached for collection {CollectionId} with TTL of {TtlSeconds} seconds",
            collectionId,
            this.options.DescribeFeatureTypeCacheDuration);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void InvalidateSchema(string collectionId)
    {
        if (string.IsNullOrWhiteSpace(collectionId))
        {
            return;
        }

        var cacheKey = BuildCacheKey(collectionId);
        this.cache.Remove(cacheKey);
        this.cacheKeys.TryRemove(cacheKey, out _);

        this.logger.LogInformation(
            "Invalidated WFS schema cache for collection: {CollectionId}",
            collectionId);
    }

    /// <inheritdoc />
    public void InvalidateAll()
    {
        var count = this.cacheKeys.Count;
        var keysToRemove = this.cacheKeys.Keys.ToList();

        foreach (var key in keysToRemove)
        {
            this.cache.Remove(key);
            this.cacheKeys.TryRemove(key, out _);
        }

        this.logger.LogInformation(
            "Invalidated all {Count} WFS schema cache entries",
            count);
    }

    /// <inheritdoc />
    public WfsSchemaCacheStatistics GetStatistics()
    {
        var hits = Interlocked.Read(ref _hits);
        var misses = Interlocked.Read(ref _misses);

        return new WfsSchemaCacheStatistics
        {
            Hits = hits,
            Misses = misses,
            Evictions = Interlocked.Read(ref _evictions),
            EntryCount = this.cacheKeys.Count,
            MaxEntries = this.options.MaxCachedSchemas,
            HitRate = (hits + misses) > 0 ? (double)hits / (hits + misses) : 0
        };
    }

    /// <summary>
    /// Estimates the size of a schema document for cache accounting.
    /// </summary>
    private static long EstimateSchemaSize(XDocument schema)
    {
        // Rough estimate: count elements and attributes
        // Each element ~100 bytes average, each attribute ~50 bytes
        try
        {
            var elementCount = schema.Descendants().Count();
            var attributeCount = schema.Descendants().SelectMany(e => e.Attributes()).Count();
            return (elementCount * 100L) + (attributeCount * 50L);
        }
        catch
        {
            // Fallback to conservative estimate of 5 KB per schema
            return 5 * 1024;
        }
    }

    /// <summary>
    /// Builds a cache key for a collection's schema.
    /// </summary>
    /// <param name="collectionId">The collection identifier.</param>
    /// <returns>A cache key string.</returns>
    private static string BuildCacheKey(string collectionId)
    {
        return $"{CacheKeyPrefix}{collectionId.ToLowerInvariant()}";
    }
}
