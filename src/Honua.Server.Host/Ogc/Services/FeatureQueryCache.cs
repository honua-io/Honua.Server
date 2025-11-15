// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetTopologySuite.Features;
using StackExchange.Redis;

namespace Honua.Server.Host.Ogc.Services;

/// <summary>
/// Configuration options for feature query result caching.
/// </summary>
public sealed class FeatureQueryCacheOptions
{
    /// <summary>
    /// Enable feature query result caching.
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Time-to-live for cached feature query results in seconds.
    /// Default: 300 seconds (5 minutes)
    /// </summary>
    public int TtlSeconds { get; set; } = 300;

    /// <summary>
    /// Maximum cache size in megabytes.
    /// Used for memory estimation and monitoring.
    /// Default: 1024 MB (1 GB)
    /// </summary>
    public int MaxCacheSizeMb { get; set; } = 1024;

    /// <summary>
    /// Invalidate cache entries on write operations (POST/PUT/DELETE).
    /// Default: true
    /// </summary>
    public bool InvalidateOnWrite { get; set; } = true;

    /// <summary>
    /// Service-specific TTL overrides.
    /// Key: service ID, Value: TTL in seconds
    /// </summary>
    public Dictionary<string, int> ServiceTtlOverrides { get; set; } = new();

    /// <summary>
    /// Layer-specific TTL overrides.
    /// Key: "serviceId:layerId", Value: TTL in seconds
    /// </summary>
    public Dictionary<string, int> LayerTtlOverrides { get; set; } = new();

    /// <summary>
    /// Enable cache hit/miss metrics.
    /// Default: true
    /// </summary>
    public bool EnableMetrics { get; set; } = true;
}

/// <summary>
/// Cache service for feature query results.
/// Provides Redis-backed caching with configurable TTL and invalidation.
/// </summary>
public interface IFeatureQueryCache
{
    /// <summary>
    /// Get cached feature collection for a query.
    /// </summary>
    Task<string?> GetAsync(string cacheKey, CancellationToken ct = default);

    /// <summary>
    /// Set cached feature collection for a query.
    /// </summary>
    Task SetAsync(string cacheKey, string featuresJson, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>
    /// Invalidate cache entries for a specific service and layer.
    /// Called on write operations (POST/PUT/DELETE).
    /// </summary>
    Task InvalidateLayerAsync(string serviceId, string layerId, CancellationToken ct = default);

    /// <summary>
    /// Generate cache key for a feature query.
    /// Format: features:{service}:{layer}:{bbox}:{filter_hash}
    /// </summary>
    string GenerateCacheKey(string serviceId, string layerId, string? bbox, string? filter, Dictionary<string, string>? parameters);

    /// <summary>
    /// Get effective TTL for a service/layer combination.
    /// </summary>
    TimeSpan GetEffectiveTtl(string serviceId, string layerId);
}

/// <summary>
/// Redis-backed implementation of feature query result cache.
/// </summary>
public sealed class RedisFeatureQueryCache : IFeatureQueryCache
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly FeatureQueryCacheOptions _options;
    private readonly ILogger<RedisFeatureQueryCache> _logger;
    private readonly FeatureQueryCacheMetrics _metrics;

    private const string KeyPrefix = "features:";
    private const string InvalidationSetPrefix = "features:invalidation:";

    public RedisFeatureQueryCache(
        IConnectionMultiplexer redis,
        IOptions<FeatureQueryCacheOptions> options,
        ILogger<RedisFeatureQueryCache> logger,
        FeatureQueryCacheMetrics metrics)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _database = _redis.GetDatabase();
    }

    public async Task<string?> GetAsync(string cacheKey, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return null;
        }

        try
        {
            var value = await _database.StringGetAsync(cacheKey).ConfigureAwait(false);

            if (value.HasValue)
            {
                _metrics.RecordCacheHit();
                _logger.LogDebug("Cache hit for key: {CacheKey}", cacheKey);
                return value.ToString();
            }

            _metrics.RecordCacheMiss();
            _logger.LogDebug("Cache miss for key: {CacheKey}", cacheKey);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving from cache: {CacheKey}", cacheKey);
            _metrics.RecordCacheError();
            return null;
        }
    }

    public async Task SetAsync(string cacheKey, string featuresJson, TimeSpan ttl, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return;
        }

        try
        {
            await _database.StringSetAsync(cacheKey, featuresJson, ttl).ConfigureAwait(false);

            // Track this key for invalidation
            var layerKey = ExtractLayerKeyFromCacheKey(cacheKey);
            if (!string.IsNullOrEmpty(layerKey))
            {
                var invalidationSet = $"{InvalidationSetPrefix}{layerKey}";
                await _database.SetAddAsync(invalidationSet, cacheKey).ConfigureAwait(false);
                // Set expiration on invalidation set (TTL + buffer for cleanup)
                await _database.KeyExpireAsync(invalidationSet, ttl.Add(TimeSpan.FromHours(1))).ConfigureAwait(false);
            }

            _metrics.RecordCacheSet(featuresJson.Length);
            _logger.LogDebug("Cached result for key: {CacheKey}, TTL: {Ttl}", cacheKey, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache: {CacheKey}", cacheKey);
            _metrics.RecordCacheError();
        }
    }

    public async Task InvalidateLayerAsync(string serviceId, string layerId, CancellationToken ct = default)
    {
        if (!_options.Enabled || !_options.InvalidateOnWrite)
        {
            return;
        }

        try
        {
            var layerKey = $"{serviceId}:{layerId}";
            var invalidationSet = $"{InvalidationSetPrefix}{layerKey}";

            // Get all cache keys for this layer
            var cacheKeys = await _database.SetMembersAsync(invalidationSet).ConfigureAwait(false);

            if (cacheKeys.Length > 0)
            {
                // Delete all cached entries for this layer
                var keys = new RedisKey[cacheKeys.Length];
                for (int i = 0; i < cacheKeys.Length; i++)
                {
                    keys[i] = cacheKeys[i].ToString();
                }

                await _database.KeyDeleteAsync(keys).ConfigureAwait(false);
                _metrics.RecordCacheInvalidation(cacheKeys.Length);

                _logger.LogInformation(
                    "Invalidated {Count} cache entries for layer {ServiceId}/{LayerId}",
                    cacheKeys.Length, serviceId, layerId);
            }

            // Clean up the invalidation set
            await _database.KeyDeleteAsync(invalidationSet).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating cache for layer {ServiceId}/{LayerId}", serviceId, layerId);
            _metrics.RecordCacheError();
        }
    }

    public string GenerateCacheKey(
        string serviceId,
        string layerId,
        string? bbox,
        string? filter,
        Dictionary<string, string>? parameters)
    {
        // Format: features:{service}:{layer}:{bbox}:{filter_hash}:{params_hash}
        var keyBuilder = new StringBuilder();
        keyBuilder.Append(KeyPrefix);
        keyBuilder.Append(serviceId);
        keyBuilder.Append(':');
        keyBuilder.Append(layerId);
        keyBuilder.Append(':');

        // Include bbox in key
        if (!string.IsNullOrEmpty(bbox))
        {
            keyBuilder.Append(bbox);
        }
        else
        {
            keyBuilder.Append("no-bbox");
        }
        keyBuilder.Append(':');

        // Hash filter for compact key
        if (!string.IsNullOrEmpty(filter))
        {
            keyBuilder.Append(ComputeHash(filter));
        }
        else
        {
            keyBuilder.Append("no-filter");
        }
        keyBuilder.Append(':');

        // Hash parameters for compact key
        if (parameters != null && parameters.Count > 0)
        {
            var paramsJson = JsonSerializer.Serialize(parameters, new JsonSerializerOptions { WriteIndented = false });
            keyBuilder.Append(ComputeHash(paramsJson));
        }
        else
        {
            keyBuilder.Append("no-params");
        }

        return keyBuilder.ToString();
    }

    public TimeSpan GetEffectiveTtl(string serviceId, string layerId)
    {
        // Check layer-specific override
        var layerKey = $"{serviceId}:{layerId}";
        if (_options.LayerTtlOverrides.TryGetValue(layerKey, out var layerTtl))
        {
            return TimeSpan.FromSeconds(layerTtl);
        }

        // Check service-specific override
        if (_options.ServiceTtlOverrides.TryGetValue(serviceId, out var serviceTtl))
        {
            return TimeSpan.FromSeconds(serviceTtl);
        }

        // Use default TTL
        return TimeSpan.FromSeconds(_options.TtlSeconds);
    }

    private static string ComputeHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(bytes);
        // Take first 8 bytes (64 bits) for compact hash
        return Convert.ToHexString(hashBytes.AsSpan(0, 8));
    }

    private static string ExtractLayerKeyFromCacheKey(string cacheKey)
    {
        // Extract "serviceId:layerId" from "features:serviceId:layerId:..."
        if (cacheKey.StartsWith(KeyPrefix))
        {
            var remaining = cacheKey.Substring(KeyPrefix.Length);
            var parts = remaining.Split(':', 3);
            if (parts.Length >= 2)
            {
                return $"{parts[0]}:{parts[1]}";
            }
        }
        return string.Empty;
    }
}

/// <summary>
/// In-memory fallback cache for scenarios without Redis.
/// NOT recommended for production multi-instance deployments.
/// </summary>
public sealed class InMemoryFeatureQueryCache : IFeatureQueryCache
{
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _memoryCache;
    private readonly FeatureQueryCacheOptions _options;
    private readonly ILogger<InMemoryFeatureQueryCache> _logger;
    private readonly FeatureQueryCacheMetrics _metrics;

    public InMemoryFeatureQueryCache(
        Microsoft.Extensions.Caching.Memory.IMemoryCache memoryCache,
        IOptions<FeatureQueryCacheOptions> options,
        ILogger<InMemoryFeatureQueryCache> logger,
        FeatureQueryCacheMetrics metrics)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    public Task<string?> GetAsync(string cacheKey, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return Task.FromResult<string?>(null);
        }

        if (_memoryCache.TryGetValue<string>(cacheKey, out var value))
        {
            _metrics.RecordCacheHit();
            _logger.LogDebug("Cache hit for key: {CacheKey}", cacheKey);
            return Task.FromResult<string?>(value);
        }

        _metrics.RecordCacheMiss();
        _logger.LogDebug("Cache miss for key: {CacheKey}", cacheKey);
        return Task.FromResult<string?>(null);
    }

    public Task SetAsync(string cacheKey, string featuresJson, TimeSpan ttl, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return Task.CompletedTask;
        }

        var cacheOptions = new Microsoft.Extensions.Caching.Memory.MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
            Size = featuresJson.Length
        };

        _memoryCache.Set(cacheKey, featuresJson, cacheOptions);
        _metrics.RecordCacheSet(featuresJson.Length);
        _logger.LogDebug("Cached result for key: {CacheKey}, TTL: {Ttl}", cacheKey, ttl);

        return Task.CompletedTask;
    }

    public Task InvalidateLayerAsync(string serviceId, string layerId, CancellationToken ct = default)
    {
        // NOTE: In-memory cache doesn't support efficient layer-wide invalidation
        // Would need to track keys separately (not implemented for simplicity)
        _logger.LogWarning(
            "Layer invalidation not fully supported in InMemoryFeatureQueryCache. " +
            "Consider using RedisFeatureQueryCache for production. Layer: {ServiceId}/{LayerId}",
            serviceId, layerId);

        return Task.CompletedTask;
    }

    public string GenerateCacheKey(
        string serviceId,
        string layerId,
        string? bbox,
        string? filter,
        Dictionary<string, string>? parameters)
    {
        // Same implementation as Redis version
        var keyBuilder = new StringBuilder();
        keyBuilder.Append("features:");
        keyBuilder.Append(serviceId);
        keyBuilder.Append(':');
        keyBuilder.Append(layerId);
        keyBuilder.Append(':');

        if (!string.IsNullOrEmpty(bbox))
        {
            keyBuilder.Append(bbox);
        }
        else
        {
            keyBuilder.Append("no-bbox");
        }
        keyBuilder.Append(':');

        if (!string.IsNullOrEmpty(filter))
        {
            var bytes = Encoding.UTF8.GetBytes(filter);
            var hashBytes = SHA256.HashData(bytes);
            keyBuilder.Append(Convert.ToHexString(hashBytes.AsSpan(0, 8)));
        }
        else
        {
            keyBuilder.Append("no-filter");
        }
        keyBuilder.Append(':');

        if (parameters != null && parameters.Count > 0)
        {
            var paramsJson = JsonSerializer.Serialize(parameters);
            var bytes = Encoding.UTF8.GetBytes(paramsJson);
            var hashBytes = SHA256.HashData(bytes);
            keyBuilder.Append(Convert.ToHexString(hashBytes.AsSpan(0, 8)));
        }
        else
        {
            keyBuilder.Append("no-params");
        }

        return keyBuilder.ToString();
    }

    public TimeSpan GetEffectiveTtl(string serviceId, string layerId)
    {
        var layerKey = $"{serviceId}:{layerId}";
        if (_options.LayerTtlOverrides.TryGetValue(layerKey, out var layerTtl))
        {
            return TimeSpan.FromSeconds(layerTtl);
        }

        if (_options.ServiceTtlOverrides.TryGetValue(serviceId, out var serviceTtl))
        {
            return TimeSpan.FromSeconds(serviceTtl);
        }

        return TimeSpan.FromSeconds(_options.TtlSeconds);
    }
}

/// <summary>
/// Metrics for feature query cache operations.
/// </summary>
public sealed class FeatureQueryCacheMetrics
{
    private long _cacheHits;
    private long _cacheMisses;
    private long _cacheErrors;
    private long _cacheInvalidations;
    private long _totalBytesStored;

    public long CacheHits => Interlocked.Read(ref _cacheHits);
    public long CacheMisses => Interlocked.Read(ref _cacheMisses);
    public long CacheErrors => Interlocked.Read(ref _cacheErrors);
    public long CacheInvalidations => Interlocked.Read(ref _cacheInvalidations);
    public long TotalBytesStored => Interlocked.Read(ref _totalBytesStored);

    public double HitRate
    {
        get
        {
            var total = CacheHits + CacheMisses;
            return total > 0 ? (double)CacheHits / total : 0;
        }
    }

    public void RecordCacheHit()
    {
        Interlocked.Increment(ref _cacheHits);
    }

    public void RecordCacheMiss()
    {
        Interlocked.Increment(ref _cacheMisses);
    }

    public void RecordCacheError()
    {
        Interlocked.Increment(ref _cacheErrors);
    }

    public void RecordCacheSet(int bytes)
    {
        Interlocked.Add(ref _totalBytesStored, bytes);
    }

    public void RecordCacheInvalidation(int keyCount)
    {
        Interlocked.Add(ref _cacheInvalidations, keyCount);
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _cacheHits, 0);
        Interlocked.Exchange(ref _cacheMisses, 0);
        Interlocked.Exchange(ref _cacheErrors, 0);
        Interlocked.Exchange(ref _cacheInvalidations, 0);
        Interlocked.Exchange(ref _totalBytesStored, 0);
    }
}
