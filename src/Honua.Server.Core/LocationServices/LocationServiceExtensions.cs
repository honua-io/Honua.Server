// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using Honua.Server.Core.LocationServices.Providers.AzureMaps;
using Honua.Server.Core.LocationServices.Providers.OpenStreetMap;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.LocationServices;

/// <summary>
/// Extension methods for configuring location services.
/// </summary>
public static class LocationServiceExtensions
{
    /// <summary>
    /// Adds location services (geocoding, routing, basemap tiles) to the service collection.
    /// </summary>
    public static IServiceCollection AddLocationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        var config = new LocationServiceConfiguration();
        configuration.GetSection(LocationServiceConfiguration.SectionName).Bind(config);
        services.AddSingleton(config);

        // Add HttpClient for providers
        services.AddHttpClient();

        // Register geocoding providers
        RegisterGeocodingProviders(services, config);

        // Register routing providers
        RegisterRoutingProviders(services, config);

        // Register basemap tile providers
        RegisterBasemapTileProviders(services, config);

        return services;
    }

    private static void RegisterGeocodingProviders(
        IServiceCollection services,
        LocationServiceConfiguration config)
    {
        // Azure Maps Geocoding
        if (config.AzureMaps?.SubscriptionKey != null)
        {
            services.AddKeyedSingleton<IGeocodingProvider>("azure-maps", (sp, key) =>
            {
                var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
                var logger = sp.GetRequiredService<ILogger<AzureMapsGeocodingProvider>>();
                return new AzureMapsGeocodingProvider(
                    httpClient,
                    config.AzureMaps.SubscriptionKey,
                    logger);
            });
        }

        // Nominatim Geocoding (OpenStreetMap)
        services.AddKeyedSingleton<IGeocodingProvider>("nominatim", (sp, key) =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
            var logger = sp.GetRequiredService<ILogger<NominatimGeocodingProvider>>();
            return new NominatimGeocodingProvider(
                httpClient,
                logger,
                config.Nominatim?.BaseUrl,
                config.Nominatim?.UserAgent);
        });

        // Register default provider
        services.AddSingleton<IGeocodingProvider>(sp =>
        {
            var providerKey = config.GeocodingProvider.ToLowerInvariant();
            return sp.GetRequiredKeyedService<IGeocodingProvider>(providerKey);
        });
    }

    private static void RegisterRoutingProviders(
        IServiceCollection services,
        LocationServiceConfiguration config)
    {
        // Azure Maps Routing
        if (config.AzureMaps?.SubscriptionKey != null)
        {
            services.AddKeyedSingleton<IRoutingProvider>("azure-maps", (sp, key) =>
            {
                var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
                var logger = sp.GetRequiredService<ILogger<AzureMapsRoutingProvider>>();
                return new AzureMapsRoutingProvider(
                    httpClient,
                    config.AzureMaps.SubscriptionKey,
                    logger);
            });
        }

        // OSRM Routing
        services.AddKeyedSingleton<IRoutingProvider>("osrm", (sp, key) =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
            var logger = sp.GetRequiredService<ILogger<OsrmRoutingProvider>>();
            return new OsrmRoutingProvider(
                httpClient,
                logger,
                config.Osrm?.BaseUrl);
        });

        // Register default provider
        services.AddSingleton<IRoutingProvider>(sp =>
        {
            var providerKey = config.RoutingProvider.ToLowerInvariant();
            return sp.GetRequiredKeyedService<IRoutingProvider>(providerKey);
        });
    }

    private static void RegisterBasemapTileProviders(
        IServiceCollection services,
        LocationServiceConfiguration config)
    {
        // Azure Maps Basemap
        if (config.AzureMaps?.SubscriptionKey != null)
        {
            services.AddKeyedSingleton<IBasemapTileProvider>("azure-maps", (sp, key) =>
            {
                var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
                var logger = sp.GetRequiredService<ILogger<AzureMapsBasemapTileProvider>>();
                return new AzureMapsBasemapTileProvider(
                    httpClient,
                    config.AzureMaps.SubscriptionKey,
                    logger);
            });
        }

        // OpenStreetMap Tiles
        services.AddKeyedSingleton<IBasemapTileProvider>("openstreetmap", (sp, key) =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
            var logger = sp.GetRequiredService<ILogger<OsmBasemapTileProvider>>();
            return new OsmBasemapTileProvider(
                httpClient,
                logger,
                config.OsmTiles?.UserAgent);
        });

        // Register default provider
        services.AddSingleton<IBasemapTileProvider>(sp =>
        {
            var providerKey = config.BasemapTileProvider.ToLowerInvariant();
            return sp.GetRequiredKeyedService<IBasemapTileProvider>(providerKey);
        });
    }

    /// <summary>
    /// Gets a geocoding provider by key.
    /// </summary>
    public static IGeocodingProvider GetGeocodingProvider(
        this IServiceProvider services,
        string providerKey)
    {
        return services.GetRequiredKeyedService<IGeocodingProvider>(providerKey.ToLowerInvariant());
    }

    /// <summary>
    /// Gets a routing provider by key.
    /// </summary>
    public static IRoutingProvider GetRoutingProvider(
        this IServiceProvider services,
        string providerKey)
    {
        return services.GetRequiredKeyedService<IRoutingProvider>(providerKey.ToLowerInvariant());
    }

    /// <summary>
    /// Gets a basemap tile provider by key.
    /// </summary>
    public static IBasemapTileProvider GetBasemapTileProvider(
        this IServiceProvider services,
        string providerKey)
    {
        return services.GetRequiredKeyedService<IBasemapTileProvider>(providerKey.ToLowerInvariant());
    }
}
