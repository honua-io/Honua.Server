// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Caching;
using Honua.Server.Core.LocationServices.Models;
using Honua.Server.Core.Performance;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.LocationServices.Caching;

/// <summary>
/// Decorator that adds caching to an <see cref="IGeocodingProvider"/>.
/// Uses memory cache by default, with optional distributed cache support.
/// Thread-safe implementation with cache metrics collection.
/// </summary>
public sealed class CachedGeocodingProvider : IGeocodingProvider, IDisposable
{
    private readonly IGeocodingProvider _innerProvider;
    private readonly IMemoryCache? _memoryCache;
    private readonly IDistributedCache? _distributedCache;
    private readonly LocationServiceCacheConfiguration _config;
    private readonly CacheMetricsCollector? _metricsCollector;
    private readonly ILogger<CachedGeocodingProvider> _logger;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private const string CacheNamePrefix = "location:geocoding";

    /// <summary>
    /// Initializes a new instance of the <see cref="CachedGeocodingProvider"/> class.
    /// </summary>
    public CachedGeocodingProvider(
        IGeocodingProvider innerProvider,
        LocationServiceCacheConfiguration config,
        ILogger<CachedGeocodingProvider> logger,
        IMemoryCache? memoryCache = null,
        IDistributedCache? distributedCache = null,
        CacheMetricsCollector? metricsCollector = null)
    {
        _innerProvider = innerProvider ?? throw new ArgumentNullException(nameof(innerProvider));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryCache = memoryCache;
        _distributedCache = distributedCache;
        _metricsCollector = metricsCollector;

        if (_memoryCache == null && _distributedCache == null)
        {
            _logger.LogWarning(
                "CachedGeocodingProvider initialized without any cache backend. Caching will be disabled.");
        }
    }

    /// <inheritdoc />
    public string ProviderKey => _innerProvider.ProviderKey;

    /// <inheritdoc />
    public string ProviderName => $"{_innerProvider.ProviderName} (Cached)";

    /// <inheritdoc />
    public async Task<GeocodingResponse> GeocodeAsync(
        GeocodingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_config.EnableCaching || !_config.Geocoding.EnableCaching)
        {
            return await _innerProvider.GeocodeAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var cacheKey = BuildGeocodingCacheKey(request);

        // Try to get from cache
        var cachedResponse = await GetFromCacheAsync<GeocodingResponse>(cacheKey, cancellationToken)
            .ConfigureAwait(false);

        if (cachedResponse != null)
        {
            _metricsCollector?.RecordHit(CacheNamePrefix);
            _logger.LogDebug("Cache hit for geocoding query: {Query}", request.Query);
            return cachedResponse;
        }

        _metricsCollector?.RecordMiss(CacheNamePrefix);
        _logger.LogDebug("Cache miss for geocoding query: {Query}", request.Query);

        // Get from provider
        var response = await _innerProvider.GeocodeAsync(request, cancellationToken).ConfigureAwait(false);

        // Cache the response
        if (response.Results.Count > 0)
        {
            var ttl = _config.GetGeocodingTtl(isReverseGeocoding: false);
            await SetCacheAsync(cacheKey, response, ttl, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Cached geocoding response for query: {Query} (TTL: {Ttl})", request.Query, ttl);
        }

        return response;
    }

    /// <inheritdoc />
    public async Task<GeocodingResponse> ReverseGeocodeAsync(
        ReverseGeocodingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_config.EnableCaching || !_config.Geocoding.EnableCaching)
        {
            return await _innerProvider.ReverseGeocodeAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var cacheKey = BuildReverseGeocodingCacheKey(request);

        // Try to get from cache
        var cachedResponse = await GetFromCacheAsync<GeocodingResponse>(cacheKey, cancellationToken)
            .ConfigureAwait(false);

        if (cachedResponse != null)
        {
            _metricsCollector?.RecordHit(CacheNamePrefix);
            _logger.LogDebug(
                "Cache hit for reverse geocoding: {Lat},{Lon}",
                request.Latitude,
                request.Longitude);
            return cachedResponse;
        }

        _metricsCollector?.RecordMiss(CacheNamePrefix);
        _logger.LogDebug(
            "Cache miss for reverse geocoding: {Lat},{Lon}",
            request.Latitude,
            request.Longitude);

        // Get from provider
        var response = await _innerProvider.ReverseGeocodeAsync(request, cancellationToken)
            .ConfigureAwait(false);

        // Cache the response
        if (response.Results.Count > 0)
        {
            var ttl = _config.GetGeocodingTtl(isReverseGeocoding: true);
            await SetCacheAsync(cacheKey, response, ttl, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug(
                "Cached reverse geocoding response for: {Lat},{Lon} (TTL: {Ttl})",
                request.Latitude,
                request.Longitude,
                ttl);
        }

        return response;
    }

    /// <inheritdoc />
    public Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default)
    {
        // Pass through - don't cache connectivity tests
        return _innerProvider.TestConnectivityAsync(cancellationToken);
    }

    /// <summary>
    /// Builds a cache key for forward geocoding requests.
    /// Format: "geocode:{provider}:{query}:{language}:{countryCodes}"
    /// </summary>
    private string BuildGeocodingCacheKey(GeocodingRequest request)
    {
        var keyBuilder = new CacheKeyBuilder.ForCustomPrefix("geocode")
            .WithComponent(_innerProvider.ProviderKey)
            .WithComponent(request.Query);

        if (_config.Geocoding.CachePerLanguage && !string.IsNullOrWhiteSpace(request.Language))
        {
            keyBuilder.WithComponent(request.Language);
        }

        if (_config.Geocoding.CachePerCountryCode && request.CountryCodes?.Length > 0)
        {
            var countryCodes = string.Join(",", request.CountryCodes);
            keyBuilder.WithComponent(countryCodes);
        }

        // Hash additional options to keep key length manageable
        if (request.BoundingBox != null || request.BiasLocation != null || request.MaxResults != null)
        {
            keyBuilder.WithObjectHash(new
            {
                request.BoundingBox,
                request.BiasLocation,
                request.MaxResults
            });
        }

        return keyBuilder.Build();
    }

    /// <summary>
    /// Builds a cache key for reverse geocoding requests.
    /// Format: "reverse:{provider}:{lat}:{lon}:{language}"
    /// </summary>
    private string BuildReverseGeocodingCacheKey(ReverseGeocodingRequest request)
    {
        // Round coordinates to 6 decimal places (~0.1m precision) for better cache hit rate
        var lat = Math.Round(request.Latitude, 6);
        var lon = Math.Round(request.Longitude, 6);

        var keyBuilder = new CacheKeyBuilder.ForCustomPrefix("reverse")
            .WithComponent(_innerProvider.ProviderKey)
            .WithComponent(lat.ToString("F6", System.Globalization.CultureInfo.InvariantCulture))
            .WithComponent(lon.ToString("F6", System.Globalization.CultureInfo.InvariantCulture));

        if (_config.Geocoding.CachePerLanguage && !string.IsNullOrWhiteSpace(request.Language))
        {
            keyBuilder.WithComponent(request.Language);
        }

        // Hash result types if specified
        if (request.ResultTypes?.Length > 0)
        {
            var resultTypes = string.Join(",", request.ResultTypes);
            keyBuilder.WithComponent(resultTypes);
        }

        return keyBuilder.Build();
    }

    /// <summary>
    /// Gets a value from cache (memory or distributed).
    /// </summary>
    private async Task<T?> GetFromCacheAsync<T>(string key, CancellationToken cancellationToken)
        where T : class
    {
        // Try memory cache first
        if (_memoryCache != null && !_config.PreferDistributedCache)
        {
            if (_memoryCache.TryGetValue(key, out T? value) && value != null)
            {
                return value;
            }
        }

        // Try distributed cache
        if (_distributedCache != null)
        {
            try
            {
                var bytes = await _distributedCache.GetAsync(key, cancellationToken).ConfigureAwait(false);
                if (bytes != null && bytes.Length > 0)
                {
                    var value = JsonSerializer.Deserialize<T>(bytes, JsonSerializerOptionsRegistry.Web);

                    // Populate memory cache if available
                    if (value != null && _memoryCache != null)
                    {
                        var options = CacheTtlPolicy.Short.ToMemoryCacheOptions(); // Short TTL for L1 cache
                        _memoryCache.Set(key, value, options);
                    }

                    return value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve geocoding response from distributed cache for key: {Key}", key);
            }
        }

        return null;
    }

    /// <summary>
    /// Sets a value in cache (memory and/or distributed).
    /// </summary>
    private async Task SetCacheAsync<T>(
        string key,
        T value,
        TimeSpan ttl,
        CancellationToken cancellationToken)
        where T : class
    {
        if (ttl <= TimeSpan.Zero)
        {
            return; // Don't cache if TTL is zero or negative
        }

        // Set in memory cache
        if (_memoryCache != null)
        {
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl,
                Priority = Microsoft.Extensions.Caching.Memory.CacheItemPriority.Normal
            };
            _memoryCache.Set(key, value, options);
        }

        // Set in distributed cache
        if (_distributedCache != null)
        {
            try
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonSerializerOptionsRegistry.Web);
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl
                };
                await _distributedCache.SetAsync(key, bytes, options, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache geocoding response in distributed cache for key: {Key}", key);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cacheLock.Dispose();
    }
}

/// <summary>
/// Helper class to build custom prefix cache keys.
/// </summary>
internal static class CacheKeyBuilder
{
    internal class ForCustomPrefix
    {
        private readonly System.Text.StringBuilder _builder;

        public ForCustomPrefix(string prefix)
        {
            _builder = new System.Text.StringBuilder(prefix);
        }

        public ForCustomPrefix WithComponent(string component)
        {
            var sanitized = CacheKeyNormalizer.SanitizeForRedis(component);
            _builder.Append(':').Append(sanitized);
            return this;
        }

        public ForCustomPrefix WithObjectHash<T>(T obj)
        {
            if (obj != null)
            {
                var json = JsonSerializer.Serialize(obj, JsonSerializerOptionsRegistry.Web);
                var hash = CacheKeyNormalizer.GenerateCompactHash(json);
                _builder.Append(':').Append(hash);
            }
            return this;
        }

        public string Build()
        {
            return CacheKeyNormalizer.Normalize(_builder.ToString());
        }
    }
}
