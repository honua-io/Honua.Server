// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using Honua.Server.Core.Caching;

namespace Honua.Server.Core.LocationServices.Caching;

/// <summary>
/// Configuration for location service caching behavior.
/// Controls cache TTLs, enable/disable per service, and cache storage backend preferences.
/// </summary>
public class LocationServiceCacheConfiguration
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "LocationServices:Caching";

    /// <summary>
    /// Enable caching for all location services.
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Enable metrics collection for cache performance.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Prefer distributed cache over memory cache when available.
    /// Useful in multi-instance deployments.
    /// </summary>
    public bool PreferDistributedCache { get; set; } = false;

    /// <summary>
    /// Geocoding cache configuration.
    /// </summary>
    public GeocodingCacheOptions Geocoding { get; set; } = new();

    /// <summary>
    /// Routing cache configuration.
    /// </summary>
    public RoutingCacheOptions Routing { get; set; } = new();

    /// <summary>
    /// Basemap tile cache configuration.
    /// </summary>
    public BasemapTileCacheOptions BasemapTiles { get; set; } = new();
}

/// <summary>
/// Cache options for geocoding services.
/// </summary>
public class GeocodingCacheOptions
{
    /// <summary>
    /// Enable caching for geocoding requests.
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Cache TTL policy for forward geocoding results.
    /// Default: 24 hours (addresses rarely change).
    /// </summary>
    public CacheTtlPolicy ForwardGeocodingTtl { get; set; } = CacheTtlPolicy.Long;

    /// <summary>
    /// Cache TTL policy for reverse geocoding results.
    /// Default: 24 hours (coordinates-to-address mappings are stable).
    /// </summary>
    public CacheTtlPolicy ReverseGeocodingTtl { get; set; } = CacheTtlPolicy.Long;

    /// <summary>
    /// Maximum number of geocoding results to cache per request.
    /// Set to 0 for unlimited. Default: 10.
    /// </summary>
    public int MaxCachedResultsPerRequest { get; set; } = 10;

    /// <summary>
    /// Cache key includes language parameter.
    /// Set to false to share cache across languages (may reduce accuracy).
    /// </summary>
    public bool CachePerLanguage { get; set; } = true;

    /// <summary>
    /// Cache key includes country code parameter.
    /// Set to false to share cache across countries (may reduce relevance).
    /// </summary>
    public bool CachePerCountryCode { get; set; } = true;
}

/// <summary>
/// Cache options for routing services.
/// </summary>
public class RoutingCacheOptions
{
    /// <summary>
    /// Enable caching for routing requests.
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Cache TTL policy for route calculations without traffic.
    /// Default: 1 hour (routes change infrequently but roads may be closed).
    /// </summary>
    public CacheTtlPolicy RouteTtl { get; set; } = CacheTtlPolicy.Medium;

    /// <summary>
    /// Cache TTL policy for traffic-aware routes.
    /// Default: 5 minutes (traffic conditions change frequently).
    /// </summary>
    public CacheTtlPolicy TrafficAwareRouteTtl { get; set; } = CacheTtlPolicy.Short;

    /// <summary>
    /// Disable caching entirely for traffic-aware routes.
    /// Useful when real-time accuracy is critical.
    /// </summary>
    public bool DisableTrafficAwareRouteCaching { get; set; } = false;

    /// <summary>
    /// Maximum number of waypoints to cache routes for.
    /// Routes with more waypoints are not cached (too specific).
    /// Set to 0 for unlimited. Default: 10.
    /// </summary>
    public int MaxCachedWaypoints { get; set; } = 10;

    /// <summary>
    /// Cache key includes language parameter.
    /// Set to false to share cache across languages.
    /// </summary>
    public bool CachePerLanguage { get; set; } = true;

    /// <summary>
    /// Cache key includes travel mode (car, truck, bicycle, pedestrian).
    /// Set to false to share cache across travel modes (not recommended).
    /// </summary>
    public bool CachePerTravelMode { get; set; } = true;

    /// <summary>
    /// Cache key includes route options (avoid tolls, highways, ferries).
    /// Set to false to ignore options in cache key (may return incorrect routes).
    /// </summary>
    public bool CachePerRouteOptions { get; set; } = true;
}

/// <summary>
/// Cache options for basemap tile services.
/// </summary>
public class BasemapTileCacheOptions
{
    /// <summary>
    /// Enable caching for basemap tiles.
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Cache TTL policy for tiles.
    /// Default: 7 days (tiles rarely change, aggressive caching reduces API costs).
    /// </summary>
    public CacheTtlPolicy TileTtl { get; set; } = CacheTtlPolicy.VeryLong;

    /// <summary>
    /// Use distributed cache for tiles (recommended for large binary data).
    /// When false, uses memory cache (may exhaust memory).
    /// </summary>
    public bool UseDistributedCache { get; set; } = true;

    /// <summary>
    /// Maximum tile size in bytes to cache.
    /// Tiles larger than this are not cached.
    /// Default: 512 KB.
    /// </summary>
    public int MaxTileSizeBytes { get; set; } = 512 * 1024;

    /// <summary>
    /// Cache key includes language parameter for labeled tiles.
    /// Set to false to share cache across languages.
    /// </summary>
    public bool CachePerLanguage { get; set; } = true;

    /// <summary>
    /// Cache key includes scale factor (@1x, @2x).
    /// Set to false to share cache across scales (not recommended).
    /// </summary>
    public bool CachePerScale { get; set; } = true;

    /// <summary>
    /// Cache key includes image format (png, jpg, webp).
    /// Set to false to share cache across formats (not recommended).
    /// </summary>
    public bool CachePerImageFormat { get; set; } = true;
}

/// <summary>
/// Extension methods for LocationServiceCacheConfiguration.
/// </summary>
public static class LocationServiceCacheConfigurationExtensions
{
    /// <summary>
    /// Gets the effective TTL for a geocoding operation.
    /// </summary>
    public static TimeSpan GetGeocodingTtl(this LocationServiceCacheConfiguration config, bool isReverseGeocoding)
    {
        var policy = isReverseGeocoding
            ? config.Geocoding.ReverseGeocodingTtl
            : config.Geocoding.ForwardGeocodingTtl;
        return policy.ToTimeSpan();
    }

    /// <summary>
    /// Gets the effective TTL for a routing operation.
    /// </summary>
    public static TimeSpan GetRoutingTtl(this LocationServiceCacheConfiguration config, bool isTrafficAware)
    {
        if (isTrafficAware && config.Routing.DisableTrafficAwareRouteCaching)
        {
            return TimeSpan.Zero; // Don't cache
        }

        var policy = isTrafficAware
            ? config.Routing.TrafficAwareRouteTtl
            : config.Routing.RouteTtl;
        return policy.ToTimeSpan();
    }

    /// <summary>
    /// Gets the effective TTL for a basemap tile.
    /// </summary>
    public static TimeSpan GetTileTtl(this LocationServiceCacheConfiguration config)
    {
        return config.BasemapTiles.TileTtl.ToTimeSpan();
    }

    /// <summary>
    /// Determines if a route should be cached based on configuration.
    /// </summary>
    public static bool ShouldCacheRoute(
        this LocationServiceCacheConfiguration config,
        bool isTrafficAware,
        int waypointCount)
    {
        if (!config.EnableCaching || !config.Routing.EnableCaching)
        {
            return false;
        }

        if (isTrafficAware && config.Routing.DisableTrafficAwareRouteCaching)
        {
            return false;
        }

        if (config.Routing.MaxCachedWaypoints > 0 && waypointCount > config.Routing.MaxCachedWaypoints)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Determines if a tile should be cached based on configuration.
    /// </summary>
    public static bool ShouldCacheTile(this LocationServiceCacheConfiguration config, int tileSizeBytes)
    {
        if (!config.EnableCaching || !config.BasemapTiles.EnableCaching)
        {
            return false;
        }

        if (config.BasemapTiles.MaxTileSizeBytes > 0 && tileSizeBytes > config.BasemapTiles.MaxTileSizeBytes)
        {
            return false;
        }

        return true;
    }
}
