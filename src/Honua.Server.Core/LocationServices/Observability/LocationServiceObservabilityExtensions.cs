// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.LocationServices.Observability;

/// <summary>
/// Extension methods for adding comprehensive observability to location services.
/// Integrates metrics, logging, health checks, and OpenTelemetry support.
/// </summary>
public static class LocationServiceObservabilityExtensions
{
    /// <summary>
    /// Adds location services with comprehensive observability (metrics, logging, health checks).
    /// This method replaces the standard AddLocationServices and adds monitoring decorators.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration containing location service settings.</param>
    /// <param name="configureOptions">Optional configuration action.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddLocationServicesWithObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<LocationServiceObservabilityOptions>? configureOptions = null)
    {
        // Parse configuration options
        var options = new LocationServiceObservabilityOptions();
        configureOptions?.Invoke(options);

        // Add the base location services first
        services.AddLocationServices(configuration);

        // Register metrics
        services.AddSingleton<LocationServiceMetrics>();

        // Add memory cache if caching is enabled and not already registered
        if (options.EnableCaching)
        {
            services.TryAddSingleton<IMemoryCache>(sp => new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = options.CacheSizeLimit
            }));
        }

        // Decorate providers with monitoring
        DecorateProvidersWithMonitoring(services, options);

        // Add health checks if enabled
        if (options.EnableHealthChecks)
        {
            AddLocationServiceHealthChecks(services, options);
        }

        return services;
    }

    /// <summary>
    /// Adds just the metrics collection for location services without decorating providers.
    /// Use this if you want to manually control when to apply monitoring decorators.
    /// </summary>
    public static IServiceCollection AddLocationServiceMetrics(this IServiceCollection services)
    {
        services.AddSingleton<LocationServiceMetrics>();
        return services;
    }

    /// <summary>
    /// Adds health checks for location service providers.
    /// </summary>
    public static IHealthChecksBuilder AddLocationServiceHealthChecks(
        this IHealthChecksBuilder builder,
        TimeSpan? timeout = null)
    {
        builder.AddCheck<LocationServicesHealthCheck>(
            "location_services",
            HealthStatus.Degraded,
            tags: new[] { "location", "external", "services" },
            timeout: timeout);

        return builder;
    }

    /// <summary>
    /// Wraps a geocoding provider with monitoring capabilities.
    /// </summary>
    public static IGeocodingProvider WithMonitoring(
        this IGeocodingProvider provider,
        IServiceProvider services,
        TimeSpan? cacheDuration = null)
    {
        var metrics = services.GetRequiredService<LocationServiceMetrics>();
        var logger = services.GetRequiredService<ILogger<MonitoredGeocodingProvider>>();
        var cache = services.GetService<IMemoryCache>();

        return new MonitoredGeocodingProvider(provider, metrics, logger, cache, cacheDuration);
    }

    /// <summary>
    /// Wraps a routing provider with monitoring capabilities.
    /// </summary>
    public static IRoutingProvider WithMonitoring(
        this IRoutingProvider provider,
        IServiceProvider services,
        TimeSpan? cacheDuration = null)
    {
        var metrics = services.GetRequiredService<LocationServiceMetrics>();
        var logger = services.GetRequiredService<ILogger<MonitoredRoutingProvider>>();
        var cache = services.GetService<IMemoryCache>();

        return new MonitoredRoutingProvider(provider, metrics, logger, cache, cacheDuration);
    }

    /// <summary>
    /// Wraps a basemap tile provider with monitoring capabilities.
    /// </summary>
    public static IBasemapTileProvider WithMonitoring(
        this IBasemapTileProvider provider,
        IServiceProvider services,
        TimeSpan? cacheDuration = null)
    {
        var metrics = services.GetRequiredService<LocationServiceMetrics>();
        var logger = services.GetRequiredService<ILogger<MonitoredBasemapTileProvider>>();
        var cache = services.GetService<IMemoryCache>();

        return new MonitoredBasemapTileProvider(provider, metrics, logger, cache, cacheDuration);
    }

    private static void DecorateProvidersWithMonitoring(
        IServiceCollection services,
        LocationServiceObservabilityOptions options)
    {
        // Decorate keyed geocoding providers
        var geocodingDescriptors = services
            .Where(d => d.ServiceType == typeof(IGeocodingProvider) && d.ServiceKey != null)
            .ToList();

        foreach (var descriptor in geocodingDescriptors)
        {
            services.Remove(descriptor);

            services.Add(ServiceDescriptor.KeyedSingleton(
                typeof(IGeocodingProvider),
                descriptor.ServiceKey,
                (sp, key) =>
                {
                    // Get the original provider
                    var factory = descriptor.KeyedImplementationFactory ??
                                  throw new InvalidOperationException("Keyed factory required");
                    var inner = (IGeocodingProvider)factory(sp, key);

                    // Wrap with monitoring
                    var metrics = sp.GetRequiredService<LocationServiceMetrics>();
                    var logger = sp.GetRequiredService<ILogger<MonitoredGeocodingProvider>>();
                    var cache = options.EnableCaching ? sp.GetService<IMemoryCache>() : null;

                    return new MonitoredGeocodingProvider(
                        inner,
                        metrics,
                        logger,
                        cache,
                        options.GeocodingCacheDuration);
                }));
        }

        // Decorate the default geocoding provider
        var defaultGeocodingDescriptor = services
            .FirstOrDefault(d => d.ServiceType == typeof(IGeocodingProvider) && d.ServiceKey == null);

        if (defaultGeocodingDescriptor != null)
        {
            services.Remove(defaultGeocodingDescriptor);

            services.AddSingleton<IGeocodingProvider>(sp =>
            {
                var factory = defaultGeocodingDescriptor.ImplementationFactory ??
                              throw new InvalidOperationException("Factory required");
                var inner = (IGeocodingProvider)factory(sp);

                var metrics = sp.GetRequiredService<LocationServiceMetrics>();
                var logger = sp.GetRequiredService<ILogger<MonitoredGeocodingProvider>>();
                var cache = options.EnableCaching ? sp.GetService<IMemoryCache>() : null;

                return new MonitoredGeocodingProvider(
                    inner,
                    metrics,
                    logger,
                    cache,
                    options.GeocodingCacheDuration);
            });
        }

        // Decorate keyed routing providers
        var routingDescriptors = services
            .Where(d => d.ServiceType == typeof(IRoutingProvider) && d.ServiceKey != null)
            .ToList();

        foreach (var descriptor in routingDescriptors)
        {
            services.Remove(descriptor);

            services.Add(ServiceDescriptor.KeyedSingleton(
                typeof(IRoutingProvider),
                descriptor.ServiceKey,
                (sp, key) =>
                {
                    var factory = descriptor.KeyedImplementationFactory ??
                                  throw new InvalidOperationException("Keyed factory required");
                    var inner = (IRoutingProvider)factory(sp, key);

                    var metrics = sp.GetRequiredService<LocationServiceMetrics>();
                    var logger = sp.GetRequiredService<ILogger<MonitoredRoutingProvider>>();
                    var cache = options.EnableCaching ? sp.GetService<IMemoryCache>() : null;

                    return new MonitoredRoutingProvider(
                        inner,
                        metrics,
                        logger,
                        cache,
                        options.RoutingCacheDuration);
                }));
        }

        // Decorate the default routing provider
        var defaultRoutingDescriptor = services
            .FirstOrDefault(d => d.ServiceType == typeof(IRoutingProvider) && d.ServiceKey == null);

        if (defaultRoutingDescriptor != null)
        {
            services.Remove(defaultRoutingDescriptor);

            services.AddSingleton<IRoutingProvider>(sp =>
            {
                var factory = defaultRoutingDescriptor.ImplementationFactory ??
                              throw new InvalidOperationException("Factory required");
                var inner = (IRoutingProvider)factory(sp);

                var metrics = sp.GetRequiredService<LocationServiceMetrics>();
                var logger = sp.GetRequiredService<ILogger<MonitoredRoutingProvider>>();
                var cache = options.EnableCaching ? sp.GetService<IMemoryCache>() : null;

                return new MonitoredRoutingProvider(
                    inner,
                    metrics,
                    logger,
                    cache,
                    options.RoutingCacheDuration);
            });
        }

        // Decorate keyed basemap providers
        var basemapDescriptors = services
            .Where(d => d.ServiceType == typeof(IBasemapTileProvider) && d.ServiceKey != null)
            .ToList();

        foreach (var descriptor in basemapDescriptors)
        {
            services.Remove(descriptor);

            services.Add(ServiceDescriptor.KeyedSingleton(
                typeof(IBasemapTileProvider),
                descriptor.ServiceKey,
                (sp, key) =>
                {
                    var factory = descriptor.KeyedImplementationFactory ??
                                  throw new InvalidOperationException("Keyed factory required");
                    var inner = (IBasemapTileProvider)factory(sp, key);

                    var metrics = sp.GetRequiredService<LocationServiceMetrics>();
                    var logger = sp.GetRequiredService<ILogger<MonitoredBasemapTileProvider>>();
                    var cache = options.EnableCaching ? sp.GetService<IMemoryCache>() : null;

                    return new MonitoredBasemapTileProvider(
                        inner,
                        metrics,
                        logger,
                        cache,
                        options.BasemapCacheDuration);
                }));
        }

        // Decorate the default basemap provider
        var defaultBasemapDescriptor = services
            .FirstOrDefault(d => d.ServiceType == typeof(IBasemapTileProvider) && d.ServiceKey == null);

        if (defaultBasemapDescriptor != null)
        {
            services.Remove(defaultBasemapDescriptor);

            services.AddSingleton<IBasemapTileProvider>(sp =>
            {
                var factory = defaultBasemapDescriptor.ImplementationFactory ??
                              throw new InvalidOperationException("Factory required");
                var inner = (IBasemapTileProvider)factory(sp);

                var metrics = sp.GetRequiredService<LocationServiceMetrics>();
                var logger = sp.GetRequiredService<ILogger<MonitoredBasemapTileProvider>>();
                var cache = options.EnableCaching ? sp.GetService<IMemoryCache>() : null;

                return new MonitoredBasemapTileProvider(
                    inner,
                    metrics,
                    logger,
                    cache,
                    options.BasemapCacheDuration);
            });
        }
    }

    private static void AddLocationServiceHealthChecks(
        IServiceCollection services,
        LocationServiceObservabilityOptions options)
    {
        // Add aggregate health check
        services.AddHealthChecks()
            .AddCheck<LocationServicesHealthCheck>(
                "location_services",
                HealthStatus.Degraded,
                tags: new[] { "location", "external", "services" },
                timeout: options.HealthCheckTimeout);
    }
}

/// <summary>
/// Configuration options for location service observability.
/// </summary>
public class LocationServiceObservabilityOptions
{
    /// <summary>
    /// Whether to enable caching of location service responses.
    /// Default: true.
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Cache size limit in number of entries.
    /// Default: 10000.
    /// </summary>
    public long CacheSizeLimit { get; set; } = 10000;

    /// <summary>
    /// Cache duration for geocoding responses.
    /// Default: 1 hour.
    /// </summary>
    public TimeSpan GeocodingCacheDuration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Cache duration for routing responses.
    /// Default: 15 minutes.
    /// </summary>
    public TimeSpan RoutingCacheDuration { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Cache duration for basemap tiles.
    /// Default: 24 hours.
    /// </summary>
    public TimeSpan BasemapCacheDuration { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Whether to enable health checks for location service providers.
    /// Default: true.
    /// </summary>
    public bool EnableHealthChecks { get; set; } = true;

    /// <summary>
    /// Timeout for health check operations.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan HealthCheckTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to enable detailed logging for all operations.
    /// Default: true.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = true;
}
