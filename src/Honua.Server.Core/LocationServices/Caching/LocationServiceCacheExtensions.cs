// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using Honua.Server.Core.Caching;
using Honua.Server.Core.LocationServices.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.LocationServices;

/// <summary>
/// Extension methods for adding location services with caching support.
/// </summary>
public static class LocationServiceCacheExtensions
{
    /// <summary>
    /// Adds location services with caching to the service collection.
    /// Wraps existing location service providers with caching decorators.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddLocationServicesWithCaching(configuration);
    /// </code>
    /// </example>
    public static IServiceCollection AddLocationServicesWithCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add base location services first
        services.AddLocationServices(configuration);

        // Bind cache configuration
        var cacheConfig = new LocationServiceCacheConfiguration();
        configuration.GetSection(LocationServiceCacheConfiguration.SectionName).Bind(cacheConfig);
        services.AddSingleton(cacheConfig);

        // Ensure memory cache is available
        services.AddMemoryCache();

        // Add cache metrics collector if enabled
        if (cacheConfig.EnableMetrics)
        {
            services.AddSingleton<CacheMetricsCollector>();
        }

        // Wrap providers with caching decorators
        WrapGeocodingProvidersWithCaching(services);
        WrapRoutingProvidersWithCaching(services);
        WrapBasemapTileProvidersWithCaching(services);

        return services;
    }

    /// <summary>
    /// Adds location services with caching and custom configuration action.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="configureCaching">Action to configure caching options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddLocationServicesWithCaching(configuration, caching =>
    /// {
    ///     caching.Geocoding.ForwardGeocodingTtl = CacheTtlPolicy.VeryLong;
    ///     caching.Routing.DisableTrafficAwareRouteCaching = true;
    ///     caching.BasemapTiles.MaxTileSizeBytes = 1024 * 1024; // 1MB
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddLocationServicesWithCaching(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<LocationServiceCacheConfiguration> configureCaching)
    {
        // Add base services
        services.AddLocationServicesWithCaching(configuration);

        // Apply custom configuration
        services.Configure(configureCaching);

        return services;
    }

    /// <summary>
    /// Wraps existing geocoding providers with caching decorators.
    /// </summary>
    private static void WrapGeocodingProvidersWithCaching(IServiceCollection services)
    {
        // Decorate the default provider
        services.Decorate<IGeocodingProvider>((inner, sp) =>
        {
            var config = sp.GetRequiredService<LocationServiceCacheConfiguration>();
            if (!config.EnableCaching || !config.Geocoding.EnableCaching)
            {
                return inner; // Don't wrap if caching is disabled
            }

            var logger = sp.GetRequiredService<ILogger<CachedGeocodingProvider>>();
            var memoryCache = sp.GetService<IMemoryCache>();
            var distributedCache = sp.GetService<IDistributedCache>();
            var metricsCollector = sp.GetService<CacheMetricsCollector>();

            return new CachedGeocodingProvider(
                inner,
                config,
                logger,
                memoryCache,
                distributedCache,
                metricsCollector);
        });

        // Note: Keyed services are wrapped automatically when retrieved through the default service
    }

    /// <summary>
    /// Wraps existing routing providers with caching decorators.
    /// </summary>
    private static void WrapRoutingProvidersWithCaching(IServiceCollection services)
    {
        // Decorate the default provider
        services.Decorate<IRoutingProvider>((inner, sp) =>
        {
            var config = sp.GetRequiredService<LocationServiceCacheConfiguration>();
            if (!config.EnableCaching || !config.Routing.EnableCaching)
            {
                return inner; // Don't wrap if caching is disabled
            }

            var logger = sp.GetRequiredService<ILogger<CachedRoutingProvider>>();
            var memoryCache = sp.GetService<IMemoryCache>();
            var distributedCache = sp.GetService<IDistributedCache>();
            var metricsCollector = sp.GetService<CacheMetricsCollector>();

            return new CachedRoutingProvider(
                inner,
                config,
                logger,
                memoryCache,
                distributedCache,
                metricsCollector);
        });
    }

    /// <summary>
    /// Wraps existing basemap tile providers with caching decorators.
    /// </summary>
    private static void WrapBasemapTileProvidersWithCaching(IServiceCollection services)
    {
        // Decorate the default provider
        services.Decorate<IBasemapTileProvider>((inner, sp) =>
        {
            var config = sp.GetRequiredService<LocationServiceCacheConfiguration>();
            if (!config.EnableCaching || !config.BasemapTiles.EnableCaching)
            {
                return inner; // Don't wrap if caching is disabled
            }

            var logger = sp.GetRequiredService<ILogger<CachedBasemapTileProvider>>();
            var memoryCache = sp.GetService<IMemoryCache>();
            var distributedCache = sp.GetService<IDistributedCache>();
            var metricsCollector = sp.GetService<CacheMetricsCollector>();

            return new CachedBasemapTileProvider(
                inner,
                config,
                logger,
                memoryCache,
                distributedCache,
                metricsCollector);
        });
    }

    /// <summary>
    /// Adds a caching wrapper to a specific geocoding provider by key.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="providerKey">The provider key to wrap.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCachedGeocodingProvider(
        this IServiceCollection services,
        string providerKey)
    {
        services.AddKeyedSingleton<IGeocodingProvider>(
            $"{providerKey}-cached",
            (sp, key) =>
            {
                var inner = sp.GetRequiredKeyedService<IGeocodingProvider>(providerKey);
                var config = sp.GetRequiredService<LocationServiceCacheConfiguration>();
                var logger = sp.GetRequiredService<ILogger<CachedGeocodingProvider>>();
                var memoryCache = sp.GetService<IMemoryCache>();
                var distributedCache = sp.GetService<IDistributedCache>();
                var metricsCollector = sp.GetService<CacheMetricsCollector>();

                return new CachedGeocodingProvider(
                    inner,
                    config,
                    logger,
                    memoryCache,
                    distributedCache,
                    metricsCollector);
            });

        return services;
    }

    /// <summary>
    /// Adds a caching wrapper to a specific routing provider by key.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="providerKey">The provider key to wrap.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCachedRoutingProvider(
        this IServiceCollection services,
        string providerKey)
    {
        services.AddKeyedSingleton<IRoutingProvider>(
            $"{providerKey}-cached",
            (sp, key) =>
            {
                var inner = sp.GetRequiredKeyedService<IRoutingProvider>(providerKey);
                var config = sp.GetRequiredService<LocationServiceCacheConfiguration>();
                var logger = sp.GetRequiredService<ILogger<CachedRoutingProvider>>();
                var memoryCache = sp.GetService<IMemoryCache>();
                var distributedCache = sp.GetService<IDistributedCache>();
                var metricsCollector = sp.GetService<CacheMetricsCollector>();

                return new CachedRoutingProvider(
                    inner,
                    config,
                    logger,
                    memoryCache,
                    distributedCache,
                    metricsCollector);
            });

        return services;
    }

    /// <summary>
    /// Adds a caching wrapper to a specific basemap tile provider by key.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="providerKey">The provider key to wrap.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCachedBasemapTileProvider(
        this IServiceCollection services,
        string providerKey)
    {
        services.AddKeyedSingleton<IBasemapTileProvider>(
            $"{providerKey}-cached",
            (sp, key) =>
            {
                var inner = sp.GetRequiredKeyedService<IBasemapTileProvider>(providerKey);
                var config = sp.GetRequiredService<LocationServiceCacheConfiguration>();
                var logger = sp.GetRequiredService<ILogger<CachedBasemapTileProvider>>();
                var memoryCache = sp.GetService<IMemoryCache>();
                var distributedCache = sp.GetService<IDistributedCache>();
                var metricsCollector = sp.GetService<CacheMetricsCollector>();

                return new CachedBasemapTileProvider(
                    inner,
                    config,
                    logger,
                    memoryCache,
                    distributedCache,
                    metricsCollector);
            });

        return services;
    }

    /// <summary>
    /// Gets cache statistics for location services.
    /// </summary>
    /// <param name="services">The service provider.</param>
    /// <returns>Cache statistics snapshot, or null if metrics are not enabled.</returns>
    public static CacheStatisticsSnapshot? GetLocationServiceCacheStatistics(
        this IServiceProvider services)
    {
        var metricsCollector = services.GetService<CacheMetricsCollector>();
        return metricsCollector?.GetOverallStatistics();
    }

    /// <summary>
    /// Gets cache statistics for a specific location service.
    /// </summary>
    /// <param name="services">The service provider.</param>
    /// <param name="serviceType">The service type ("geocoding", "routing", or "tiles").</param>
    /// <returns>Cache statistics snapshot, or null if metrics are not enabled.</returns>
    public static CacheStatisticsSnapshot? GetLocationServiceCacheStatistics(
        this IServiceProvider services,
        string serviceType)
    {
        var metricsCollector = services.GetService<CacheMetricsCollector>();
        if (metricsCollector == null)
        {
            return null;
        }

        var cacheName = serviceType.ToLowerInvariant() switch
        {
            "geocoding" => "location:geocoding",
            "routing" => "location:routing",
            "tiles" => "location:tiles",
            _ => throw new ArgumentException($"Unknown service type: {serviceType}", nameof(serviceType))
        };

        return metricsCollector.GetCacheStatistics(cacheName);
    }
}
