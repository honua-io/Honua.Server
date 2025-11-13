// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Host.Services;

/// <summary>
/// Default implementation of capabilities cache using IMemoryCache.
/// </summary>
/// <remarks>
/// <para>
/// This implementation provides high-performance in-memory caching of OGC capabilities documents
/// with automatic TTL-based expiration and size-based eviction. It significantly improves
/// GetCapabilities response times by eliminating redundant XML generation and metadata lookups.
/// </para>
/// <para>
/// <strong>Implementation Details:</strong>
/// <list type="bullet">
/// <item>Uses IMemoryCache for fast in-process caching</item>
/// <item>Configurable TTL (default 10 minutes as per requirements)</item>
/// <item>Size limit of 100 entries to prevent unbounded growth</item>
/// <item>Comprehensive metrics tracking (hits, misses, evictions)</item>
/// <item>Thread-safe concurrent operations</item>
/// </list>
/// </para>
/// <para>
/// <strong>Cache Invalidation:</strong>
/// The cache automatically invalidates entries on:
/// <list type="bullet">
/// <item>TTL expiration (configurable, default 10 minutes)</item>
/// <item>Service metadata updates via InvalidateService()</item>
/// <item>Global metadata reload via InvalidateAll()</item>
/// <item>Size-based eviction when cache is full</item>
/// </list>
/// </para>
/// </remarks>
public sealed class CapabilitiesCache : ICapabilitiesCache
{
    private readonly IMemoryCache cache;
    private readonly ILogger<CapabilitiesCache> logger;
    private readonly CapabilitiesCacheOptions options;
    private readonly ConcurrentDictionary<string, byte> _cacheKeys;
    private readonly Counter<long> hitCounter;
    private readonly Counter<long> missCounter;
    private readonly Counter<long> evictionsCounter;
    private readonly ObservableGauge<int> entriesGauge;
    private readonly Histogram<double> documentSizeHistogram;
    private long _hits;
    private long _misses;
    private long _evictions;

    /// <summary>
    /// Meter for capabilities cache metrics.
    /// </summary>
    private static readonly Meter _meter = new("Honua.Capabilities", "1.0.0");

    /// <summary>
    /// Cache key prefix for all capabilities entries.
    /// </summary>
    private const string CacheKeyPrefix = "capabilities:";

    public CapabilitiesCache(
        IMemoryCache cache,
        ILogger<CapabilitiesCache> logger,
        IOptions<CapabilitiesCacheOptions> options)
    {
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.cacheKeys = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        // Initialize metrics
        this.hitCounter = this.meter.CreateCounter<long>(
            "honua.capabilities.cache.hits",
            description: "Number of capabilities cache hits");

        this.missCounter = this.meter.CreateCounter<long>(
            "honua.capabilities.cache.misses",
            description: "Number of capabilities cache misses");

        this.evictionsCounter = this.meter.CreateCounter<long>(
            "honua.capabilities.cache.evictions",
            description: "Number of capabilities cache evictions");

        this.entriesGauge = this.meter.CreateObservableGauge<int>(
            "honua.capabilities.cache.entries",
            () => this.cacheKeys.Count,
            description: "Number of cached capabilities documents");

        this.documentSizeHistogram = this.meter.CreateHistogram<double>(
            "honua.capabilities.document.size",
            unit: "bytes",
            description: "Size of cached capabilities documents");

        this.logger.LogInformation(
            "Capabilities cache initialized with TTL={TtlMinutes}min, MaxEntries={MaxEntries}",
            this.options.CacheDurationMinutes,
            this.options.MaxCachedDocuments);
    }

    /// <inheritdoc />
    public bool TryGetCapabilities(
        string serviceType,
        string serviceId,
        string version,
        string? acceptLanguage,
        out string? cachedDocument)
    {
        if (!this.options.EnableCaching)
        {
            cachedDocument = null;
            return false;
        }

        var cacheKey = BuildCacheKey(serviceType, serviceId, version, acceptLanguage);

        if (this.cache.TryGetValue(cacheKey, out cachedDocument))
        {
            Interlocked.Increment(ref _hits);
            this.hitCounter.Add(1,
                new KeyValuePair<string, object?>("service_type", serviceType),
                new KeyValuePair<string, object?>("service_id", serviceId),
                new KeyValuePair<string, object?>("version", version));

            this.logger.LogDebug(
                "Capabilities cache HIT: {ServiceType}/{ServiceId}/{Version}/{Language}",
                serviceType, serviceId, version, acceptLanguage ?? "default");
            return true;
        }

        Interlocked.Increment(ref _misses);
        this.missCounter.Add(1,
            new KeyValuePair<string, object?>("service_type", serviceType),
            new KeyValuePair<string, object?>("service_id", serviceId),
            new KeyValuePair<string, object?>("version", version));

        this.logger.LogDebug(
            "Capabilities cache MISS: {ServiceType}/{ServiceId}/{Version}/{Language}",
            serviceType, serviceId, version, acceptLanguage ?? "default");

        cachedDocument = null;
        return false;
    }

    /// <inheritdoc />
    public Task SetCapabilitiesAsync(
        string serviceType,
        string serviceId,
        string version,
        string? acceptLanguage,
        string document,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serviceType))
        {
            throw new ArgumentException("Service type cannot be null or whitespace.", nameof(serviceType));
        }

        if (string.IsNullOrWhiteSpace(serviceId))
        {
            throw new ArgumentException("Service ID cannot be null or whitespace.", nameof(serviceId));
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("Version cannot be null or whitespace.", nameof(version));
        }

        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (!this.options.EnableCaching)
        {
            this.logger.LogTrace("Capabilities caching is disabled, skipping cache storage");
            return Task.CompletedTask;
        }

        var cacheKey = BuildCacheKey(serviceType, serviceId, version, acceptLanguage);

        // Check if we're at the cache size limit
        if (this.options.MaxCachedDocuments > 0 && this.cacheKeys.Count >= this.options.MaxCachedDocuments)
        {
            this.logger.LogWarning(
                "Capabilities cache has reached maximum size limit of {MaxDocuments}. " +
                "Entry will be cached but may be evicted immediately. Consider increasing MaxCachedDocuments.",
                this.options.MaxCachedDocuments);
        }

        // Track document size for metrics
        var documentSize = System.Text.Encoding.UTF8.GetByteCount(document);
        this.documentSizeHistogram.Record(documentSize,
            new KeyValuePair<string, object?>("service_type", serviceType));

        // Build cache options with configured TTL
        var cacheOptions = new CacheOptionsBuilder()
            .WithAbsoluteExpiration(TimeSpan.FromMinutes(this.options.CacheDurationMinutes))
            .WithPriority(CacheItemPriority.Normal)
            .WithSize(1) // Each document counts as 1 entry toward size limit
            .BuildMemory();

        // Register eviction callback to track cache keys and metrics
        cacheOptions.RegisterPostEvictionCallback((key, value, reason, state) =>
        {
            this.cacheKeys.TryRemove(key.ToString()!, out _);
            Interlocked.Increment(ref _evictions);
            this.evictionsCounter.Add(1,
                new KeyValuePair<string, object?>("service_type", serviceType),
                new KeyValuePair<string, object?>("service_id", serviceId),
                new KeyValuePair<string, object?>("reason", reason.ToString()));

            this.logger.LogDebug(
                "Capabilities cache entry evicted for {ServiceType}/{ServiceId}/{Version}, reason: {Reason}",
                serviceType, serviceId, version, reason);

            // Warn if eviction is due to capacity limits
            if (reason == EvictionReason.Capacity)
            {
                this.logger.LogWarning(
                    "Capabilities cache evicted entry for {ServiceType}/{ServiceId} due to capacity limit. " +
                    "Current entries: {CurrentEntries}, Max: {MaxDocuments}. Consider increasing cache limits.",
                    serviceType, serviceId, this.cacheKeys.Count, this.options.MaxCachedDocuments);
            }
        });

        this.cache.Set(cacheKey, document, cacheOptions);
        this.cacheKeys.TryAdd(cacheKey, 0);

        this.logger.LogDebug(
            "Capabilities cached for {ServiceType}/{ServiceId}/{Version}/{Language} " +
            "(size={Size} bytes, TTL={TtlMinutes}min)",
            serviceType, serviceId, version, acceptLanguage ?? "default",
            documentSize, this.options.CacheDurationMinutes);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void InvalidateService(string serviceType, string? serviceId = null)
    {
        if (string.IsNullOrWhiteSpace(serviceType))
        {
            return;
        }

        var pattern = serviceId == null
            ? $"{CacheKeyPrefix}{serviceType.ToLowerInvariant()}:"
            : $"{CacheKeyPrefix}{serviceType.ToLowerInvariant()}:{serviceId.ToLowerInvariant()}:";

        var keysToRemove = this.cacheKeys.Keys
            .Where(k => k.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in keysToRemove)
        {
            this.cache.Remove(key);
            this.cacheKeys.TryRemove(key, out _);
        }

        if (keysToRemove.Count > 0)
        {
            this.logger.LogInformation(
                "Invalidated {Count} capabilities cache entries for {ServiceType}/{ServiceId}",
                keysToRemove.Count,
                serviceType,
                serviceId ?? "*");
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

        this.logger.LogInformation(
            "Invalidated all {Count} capabilities cache entries",
            count);
    }

    /// <inheritdoc />
    public CapabilitiesCacheStatistics GetStatistics()
    {
        var hits = Interlocked.Read(ref _hits);
        var misses = Interlocked.Read(ref _misses);

        return new CapabilitiesCacheStatistics
        {
            Hits = hits,
            Misses = misses,
            Evictions = Interlocked.Read(ref _evictions),
            EntryCount = this.cacheKeys.Count,
            MaxEntries = this.options.MaxCachedDocuments,
            HitRate = (hits + misses) > 0 ? (double)hits / (hits + misses) : 0
        };
    }

    /// <summary>
    /// Builds a cache key for a capabilities document.
    /// Format: "capabilities:{service_type}:{service_id}:{version}:{language}"
    /// </summary>
    private static string BuildCacheKey(
        string serviceType,
        string serviceId,
        string version,
        string? acceptLanguage)
    {
        var language = string.IsNullOrWhiteSpace(acceptLanguage)
            ? "default"
            : acceptLanguage.ToLowerInvariant();

        return $"{CacheKeyPrefix}{serviceType.ToLowerInvariant()}:{serviceId.ToLowerInvariant()}:" +
               $"{version.ToLowerInvariant()}:{language}";
    }
}
