using Honua.MapSDK.Components.Map;
using Honua.MapSDK.Core;
using Honua.MapSDK.Services;
using Honua.MapSDK.Services.Drawing;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.MapSDK;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register Honua MapSDK services
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureMapLibre">Optional configuration for MapLibre support.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaMapSDK(
        this IServiceCollection services,
        Action<MapLibreOptions>? configureMapLibre = null)
    {
        // Register ComponentBus as scoped (one per Blazor circuit)
        services.AddScoped<ComponentBus>();

        // Register configuration service
        services.AddScoped<IMapConfigurationService, MapConfigurationService>();

        // Register layer management service
        services.AddScoped<ILayerManager, LayerManager>();

        // Register geocoding search service
        services.AddScoped<GeocodingSearchService>();

        // Register drawing services
        services.AddScoped<IDrawingManager, DrawingManager>();
        services.AddScoped<MeasurementManager>();

        // Register MapLibre support if configured
        if (configureMapLibre != null)
        {
            services.AddMapLibreSupport(configureMapLibre);
        }

        return services;
    }
}
