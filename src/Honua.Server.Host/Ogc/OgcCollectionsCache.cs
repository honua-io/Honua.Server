// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Caching;
using Honua.Server.Host.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Default implementation of OGC API collections cache using IMemoryCache.
/// </summary>
/// <remarks>
/// <para>
/// This cache implementation addresses the performance opportunity identified in
/// PERFORMANCE_OPTIMIZATION_OPPORTUNITIES.md where OGC API collections list responses
/// are regenerated on every request. By caching the responses, we eliminate redundant
/// metadata queries and improve response times.
/// </para>
/// <para>
/// <strong>Cache Key Strategy:</strong>
/// Keys are formatted as "ogc:collections:{service_id}:{format}:{accept_language}" to provide:
/// <list type="bullet">
/// <item>Namespace isolation from other cached data</item>
/// <item>Service-specific lookups for targeted invalidation</item>
/// <item>Format-specific caching (JSON and HTML are cached separately)</item>
/// <item>Language-specific caching for i18n support</item>
/// </list>
/// </para>
/// <para>
/// <strong>Invalidation Scenarios:</strong>
/// <list type="bullet">
/// <item>Service configuration modifications (layer add/remove/modify)</item>
/// <item>Layer metadata updates</item>
/// <item>Administrative metadata reloads</item>
/// <item>TTL expiration (configurable, default 10 minutes)</item>
/// </list>
/// </para>
/// <para>
/// <strong>Metrics:</strong>
/// Cache operations are instrumented with hit/miss/invalidation counters and entry count gauges
/// for operational visibility via the honua.ogc.* metric namespace.
/// </para>
/// </remarks>
public sealed class OgcCollectionsCache : IOgcCollectionsCache
{
    private readonly IMemoryCache cache;
    private readonly ILogger<OgcCollectionsCache> logger;
    private readonly OgcApiOptions options;
    private readonly ConcurrentDictionary<string, byte> _cacheKeys;
    private readonly Counter<long> hitCounter;
    private readonly Counter<long> missCounter;
    private readonly Counter<long> invalidationsCounter;
    private readonly Counter<long> evictionsCounter;
    private readonly ObservableGauge<int> entriesGauge;
    private long _hits;
    private long _misses;
    private long _invalidations;
    private long _evictions;

    /// <summary>
    /// Cache key prefix for all OGC collections entries.
    /// </summary>
    private const string CacheKeyPrefix = "ogc:collections:";

    /// <summary>
    /// Default TTL for collections cache entries in seconds (10 minutes).
    /// </summary>
    private const int DefaultCacheDurationSeconds = 600;

    /// <summary>
    /// Default maximum number of cached collections entries.
    /// </summary>
    private const int DefaultMaxCachedCollections = 500;

    public OgcCollectionsCache(
        IMemoryCache cache,
        ILogger<OgcCollectionsCache> logger,
        IOptionsMonitor<OgcApiOptions> options)
    {
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.options = options?.CurrentValue ?? throw new ArgumentNullException(nameof(options));
        this.cacheKeys = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        // Initialize metrics
        this.hitCounter = OgcMetrics.CollectionsCacheHits;
        this.missCounter = OgcMetrics.CollectionsCacheMisses;
        this.invalidationsCounter = OgcMetrics.CollectionsCacheInvalidations;

        this.evictionsCounter = OgcMetrics.Meter.CreateCounter<long>(
            "honua.ogc.collections_cache.evictions",
            description: "Number of OGC API collections cache evictions");

        this.entriesGauge = OgcMetrics.Meter.CreateObservableGauge<int>(
            "honua.ogc.collections_cache.entries",
            () => this.cacheKeys.Count,
            description: "Number of cached OGC API collections entries");
    }

    /// <inheritdoc />
    public bool TryGetCollections(
        string? serviceId,
        string format,
        string? acceptLanguage,
        out OgcCollectionsCacheEntry? response)
    {
        if (!IsEnabled())
        {
            response = null;
            return false;
        }

        var cacheKey = BuildCacheKey(serviceId, format, acceptLanguage);

        if (this.cache.TryGetValue(cacheKey, out response))
        {
            Interlocked.Increment(ref _hits);
            this.hitCounter.Add(1,
                new KeyValuePair<string, object?>("service_id", serviceId ?? "all"),
                new KeyValuePair<string, object?>("format", format),
                new KeyValuePair<string, object?>("language", acceptLanguage ?? "default"));

            this.logger.LogDebug(
                "OGC collections cache hit for service: {ServiceId}, format: {Format}, language: {Language}",
                serviceId ?? "all",
                format,
                acceptLanguage ?? "default");

            return true;
        }

        Interlocked.Increment(ref _misses);
        this.missCounter.Add(1,
            new KeyValuePair<string, object?>("service_id", serviceId ?? "all"),
            new KeyValuePair<string, object?>("format", format),
            new KeyValuePair<string, object?>("language", acceptLanguage ?? "default"));

        this.logger.LogDebug(
            "OGC collections cache miss for service: {ServiceId}, format: {Format}, language: {Language}",
            serviceId ?? "all",
            format,
            acceptLanguage ?? "default");

        return false;
    }

    /// <inheritdoc />
    public Task SetCollectionsAsync(
        string? serviceId,
        string format,
        string? acceptLanguage,
        string content,
        string contentType,
        string etag,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content cannot be null or whitespace.", nameof(content));
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Content type cannot be null or whitespace.", nameof(contentType));
        }

        if (string.IsNullOrWhiteSpace(etag))
        {
            throw new ArgumentException("ETag cannot be null or whitespace.", nameof(etag));
        }

        if (!IsEnabled())
        {
            this.logger.LogTrace("OGC collections caching is disabled, skipping cache storage");
            return Task.CompletedTask;
        }

        var cacheKey = BuildCacheKey(serviceId, format, acceptLanguage);
        var maxEntries = GetMaxCachedCollections();

        // Check if we're at the cache size limit
        if (maxEntries > 0 && this.cacheKeys.Count >= maxEntries)
        {
            this.logger.LogWarning(
                "OGC collections cache has reached maximum size limit of {MaxEntries}. " +
                "Entry will be cached but may be evicted immediately. Consider increasing the cache limit.",
                maxEntries);
        }

        var entry = new OgcCollectionsCacheEntry
        {
            Content = content,
            ContentType = contentType,
            ETag = etag,
            CachedAt = DateTimeOffset.UtcNow
        };

        // Build cache options with configurable TTL
        var cacheDuration = GetCacheDurationSeconds();
        var cacheOptions = new CacheOptionsBuilder()
            .WithAbsoluteExpiration(TimeSpan.FromSeconds(cacheDuration))
            .WithPriority(CacheItemPriority.Normal)
            .WithSize(1) // Each entry counts as 1 toward global limit
            .BuildMemory();

        // Register eviction callback to track cache keys and metrics
        cacheOptions.RegisterPostEvictionCallback((key, value, reason, state) =>
        {
            this.cacheKeys.TryRemove(key.ToString()!, out _);
            Interlocked.Increment(ref _evictions);
            this.evictionsCounter.Add(1,
                new KeyValuePair<string, object?>("service_id", serviceId ?? "all"),
                new KeyValuePair<string, object?>("format", format),
                new KeyValuePair<string, object?>("reason", reason.ToString()));

            this.logger.LogDebug(
                "OGC collections cache entry evicted for service {ServiceId}, format {Format}, reason: {Reason}. " +
                "Total evictions: {TotalEvictions}",
                serviceId ?? "all",
                format,
                reason,
                Interlocked.Read(ref _evictions));

            // Warn if eviction is due to capacity limits
            if (reason == EvictionReason.Capacity)
            {
                this.logger.LogWarning(
                    "OGC collections cache evicted entry for {ServiceId} due to capacity limit. " +
                    "Current entries: {CurrentEntries}, Max: {MaxEntries}. Consider increasing cache limits.",
                    serviceId ?? "all",
                    this.cacheKeys.Count,
                    maxEntries);
            }
        });

        this.cache.Set(cacheKey, entry, cacheOptions);
        this.cacheKeys.TryAdd(cacheKey, 0);

        this.logger.LogDebug(
            "OGC collections cached for service {ServiceId}, format {Format}, language {Language} with TTL of {TtlSeconds} seconds",
            serviceId ?? "all",
            format,
            acceptLanguage ?? "default",
            cacheDuration);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void InvalidateService(string serviceId)
    {
        if (string.IsNullOrWhiteSpace(serviceId))
        {
            return;
        }

        // Remove all cache entries that match the service ID prefix
        var servicePrefix = BuildServicePrefix(serviceId);
        var keysToRemove = this.cacheKeys.Keys
            .Where(key => key.StartsWith(servicePrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var removedCount = 0;
        foreach (var key in keysToRemove)
        {
            this.cache.Remove(key);
            if (this.cacheKeys.TryRemove(key, out _))
            {
                removedCount++;
            }
        }

        if (removedCount > 0)
        {
            Interlocked.Add(ref _invalidations, removedCount);
            this.invalidationsCounter.Add(removedCount,
                new KeyValuePair<string, object?>("service_id", serviceId),
                new KeyValuePair<string, object?>("scope", "service"));

            this.logger.LogInformation(
                "Invalidated {Count} OGC collections cache entries for service: {ServiceId}",
                removedCount,
                serviceId);
        }
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

        Interlocked.Add(ref _invalidations, count);
        this.invalidationsCounter.Add(count,
            new KeyValuePair<string, object?>("scope", "all"));

        this.logger.LogInformation(
            "Invalidated all {Count} OGC collections cache entries",
            count);
    }

    /// <inheritdoc />
    public OgcCollectionsCacheStatistics GetStatistics()
    {
        var hits = Interlocked.Read(ref _hits);
        var misses = Interlocked.Read(ref _misses);

        return new OgcCollectionsCacheStatistics
        {
            Hits = hits,
            Misses = misses,
            Invalidations = Interlocked.Read(ref _invalidations),
            Evictions = Interlocked.Read(ref _evictions),
            EntryCount = this.cacheKeys.Count,
            MaxEntries = GetMaxCachedCollections(),
            HitRate = (hits + misses) > 0 ? (double)hits / (hits + misses) : 0
        };
    }

    /// <summary>
    /// Builds a cache key for a collections list response.
    /// </summary>
    /// <param name="serviceId">The service identifier, or null for all services.</param>
    /// <param name="format">The response format (json, html).</param>
    /// <param name="acceptLanguage">The Accept-Language header value.</param>
    /// <returns>A cache key string.</returns>
    private static string BuildCacheKey(string? serviceId, string format, string? acceptLanguage)
    {
        var normalizedServiceId = string.IsNullOrWhiteSpace(serviceId) ? "all" : serviceId.ToLowerInvariant();
        var normalizedFormat = format.ToLowerInvariant();
        var normalizedLanguage = string.IsNullOrWhiteSpace(acceptLanguage) ? "default" : acceptLanguage.ToLowerInvariant();

        return $"{CacheKeyPrefix}{normalizedServiceId}:{normalizedFormat}:{normalizedLanguage}";
    }

    /// <summary>
    /// Builds a service-specific cache key prefix for invalidation.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <returns>A cache key prefix string.</returns>
    private static string BuildServicePrefix(string serviceId)
    {
        var normalizedServiceId = serviceId.ToLowerInvariant();
        return $"{CacheKeyPrefix}{normalizedServiceId}:";
    }

    /// <summary>
    /// Checks if caching is enabled based on configuration.
    /// </summary>
    /// <returns>True if caching is enabled; otherwise, false.</returns>
    private bool IsEnabled()
    {
        // Check if collections caching is explicitly enabled in options
        // Default to true if not specified
        return GetCacheDurationSeconds() > 0;
    }

    /// <summary>
    /// Gets the cache duration in seconds from configuration.
    /// </summary>
    /// <returns>Cache duration in seconds.</returns>
    private int GetCacheDurationSeconds()
    {
        // Use configured value or default
        return this.options.CollectionsCacheDurationSeconds ?? DefaultCacheDurationSeconds;
    }

    /// <summary>
    /// Gets the maximum number of cached collections from configuration.
    /// </summary>
    /// <returns>Maximum number of cached entries.</returns>
    private int GetMaxCachedCollections()
    {
        // Use configured value or default
        return this.options.MaxCachedCollections ?? DefaultMaxCachedCollections;
    }
}
