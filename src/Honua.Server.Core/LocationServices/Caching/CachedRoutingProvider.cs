// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
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
/// Decorator that adds caching to an <see cref="IRoutingProvider"/>.
/// Uses shorter TTL for traffic-aware routes and supports disabling cache for real-time routing.
/// Thread-safe implementation with cache metrics collection.
/// </summary>
public sealed class CachedRoutingProvider : IRoutingProvider, IDisposable
{
    private readonly IRoutingProvider _innerProvider;
    private readonly IMemoryCache? _memoryCache;
    private readonly IDistributedCache? _distributedCache;
    private readonly LocationServiceCacheConfiguration _config;
    private readonly CacheMetricsCollector? _metricsCollector;
    private readonly ILogger<CachedRoutingProvider> _logger;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private const string CacheNamePrefix = "location:routing";

    /// <summary>
    /// Initializes a new instance of the <see cref="CachedRoutingProvider"/> class.
    /// </summary>
    public CachedRoutingProvider(
        IRoutingProvider innerProvider,
        LocationServiceCacheConfiguration config,
        ILogger<CachedRoutingProvider> logger,
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
                "CachedRoutingProvider initialized without any cache backend. Caching will be disabled.");
        }
    }

    /// <inheritdoc />
    public string ProviderKey => _innerProvider.ProviderKey;

    /// <inheritdoc />
    public string ProviderName => $"{_innerProvider.ProviderName} (Cached)";

    /// <inheritdoc />
    public async Task<RoutingResponse> CalculateRouteAsync(
        RoutingRequest request,
        CancellationToken cancellationToken = default)
    {
        var isTrafficAware = request.UseTraffic || request.DepartureTime.HasValue;
        var waypointCount = request.Waypoints.Count;

        // Check if we should cache this route
        if (!_config.ShouldCacheRoute(isTrafficAware, waypointCount))
        {
            _logger.LogDebug(
                "Route caching disabled for this request (TrafficAware: {IsTrafficAware}, Waypoints: {Count})",
                isTrafficAware,
                waypointCount);
            return await _innerProvider.CalculateRouteAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var cacheKey = BuildRoutingCacheKey(request);

        // Try to get from cache
        var cachedResponse = await GetFromCacheAsync<RoutingResponse>(cacheKey, cancellationToken)
            .ConfigureAwait(false);

        if (cachedResponse != null)
        {
            _metricsCollector?.RecordHit(CacheNamePrefix);
            _logger.LogDebug(
                "Cache hit for route: {WaypointCount} waypoints, mode: {TravelMode}",
                waypointCount,
                request.TravelMode);
            return cachedResponse;
        }

        _metricsCollector?.RecordMiss(CacheNamePrefix);
        _logger.LogDebug(
            "Cache miss for route: {WaypointCount} waypoints, mode: {TravelMode}",
            waypointCount,
            request.TravelMode);

        // Get from provider
        var response = await _innerProvider.CalculateRouteAsync(request, cancellationToken)
            .ConfigureAwait(false);

        // Cache the response
        if (response.Routes.Count > 0)
        {
            var ttl = _config.GetRoutingTtl(isTrafficAware);
            if (ttl > TimeSpan.Zero)
            {
                await SetCacheAsync(cacheKey, response, ttl, cancellationToken).ConfigureAwait(false);
                _logger.LogDebug(
                    "Cached routing response for {WaypointCount} waypoints (TTL: {Ttl})",
                    waypointCount,
                    ttl);
            }
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
    /// Builds a cache key for routing requests.
    /// Format: "route:{provider}:{waypointsHash}:{mode}:{optionsHash}"
    /// </summary>
    private string BuildRoutingCacheKey(RoutingRequest request)
    {
        var keyBuilder = new CacheKeyBuilder.ForCustomPrefix("route")
            .WithComponent(_innerProvider.ProviderKey);

        // Hash waypoints to keep key manageable
        var waypointsJson = JsonSerializer.Serialize(request.Waypoints, JsonSerializerOptionsRegistry.Web);
        var waypointsHash = CacheKeyNormalizer.GenerateCompactHash(waypointsJson);
        keyBuilder.WithComponent(waypointsHash);

        // Include travel mode if configured
        if (_config.Routing.CachePerTravelMode)
        {
            keyBuilder.WithComponent(request.TravelMode);
        }

        // Include language if configured
        if (_config.Routing.CachePerLanguage && !string.IsNullOrWhiteSpace(request.Language))
        {
            keyBuilder.WithComponent(request.Language);
        }

        // Include route options if configured
        if (_config.Routing.CachePerRouteOptions)
        {
            keyBuilder.WithObjectHash(new
            {
                request.AvoidTolls,
                request.AvoidHighways,
                request.AvoidFerries,
                request.UseTraffic,
                request.UnitSystem
            });
        }

        // Include vehicle specs if provided
        if (request.Vehicle != null)
        {
            keyBuilder.WithObjectHash(request.Vehicle);
        }

        // For traffic-aware routes, include departure time rounded to nearest 5 minutes
        if (request.UseTraffic && request.DepartureTime.HasValue)
        {
            var roundedTime = RoundToNearestFiveMinutes(request.DepartureTime.Value);
            keyBuilder.WithComponent(roundedTime.ToUnixTimeSeconds().ToString());
        }

        return keyBuilder.Build();
    }

    /// <summary>
    /// Rounds a timestamp to the nearest 5 minutes for better cache hit rate.
    /// </summary>
    private static DateTimeOffset RoundToNearestFiveMinutes(DateTimeOffset timestamp)
    {
        var ticks = timestamp.Ticks;
        var fiveMinuteTicks = TimeSpan.FromMinutes(5).Ticks;
        var roundedTicks = (ticks / fiveMinuteTicks) * fiveMinuteTicks;
        return new DateTimeOffset(roundedTicks, timestamp.Offset);
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
                        var options = CacheTtlPolicy.VeryShort.ToMemoryCacheOptions(); // Very short TTL for L1 cache
                        _memoryCache.Set(key, value, options);
                    }

                    return value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve routing response from distributed cache for key: {Key}", key);
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
                _logger.LogWarning(ex, "Failed to cache routing response in distributed cache for key: {Key}", key);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cacheLock.Dispose();
    }
}
