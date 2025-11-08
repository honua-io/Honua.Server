using Honua.MapSDK.Configuration;
using Honua.MapSDK.Components.Map;
using Honua.MapSDK.Core;
using Honua.MapSDK.Logging;
using Honua.MapSDK.Services;
using Honua.MapSDK.Services.DataLoading;
using Honua.MapSDK.Services.Drawing;
using Honua.MapSDK.Services.Performance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.MapSDK;

/// <summary>
/// Extension methods for registering MapSDK services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Honua MapSDK services with default configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureMapLibre">Optional configuration for MapLibre support.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaMapSDK(
        this IServiceCollection services,
        Action<MapLibreOptions>? configureMapLibre = null)
    {
        return services.AddHonuaMapSDK(options => { }, configureMapLibre);
    }

    /// <summary>
    /// Registers all Honua MapSDK services with custom configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action for MapSDK options.</param>
    /// <param name="configureMapLibre">Optional configuration for MapLibre support.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddHonuaMapSDK(options => {
    ///     options.EnablePerformanceMonitoring = true;
    ///     options.Cache.MaxSizeMB = 100;
    ///     options.LogLevel = LogLevel.Debug;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddHonuaMapSDK(
        this IServiceCollection services,
        Action<MapSdkOptions> configure,
        Action<MapLibreOptions>? configureMapLibre = null)
    {
        // Create and configure options
        var options = new MapSdkOptions();
        configure(options);

        // Register options as singleton
        services.AddSingleton(options);

        // Core services
        services.AddScoped<ComponentBus>();
        services.AddScoped<IMapConfigurationService, MapConfigurationService>();

        // Data loading services
        services.AddSingleton(sp => new DataCache(options.Cache));
        services.AddHttpClient<DataLoader>();
        services.AddScoped<DataLoader>();
        services.AddScoped<StreamingLoader>();

        // Register layer management service
        services.AddScoped<ILayerManager, LayerManager>();

        // Register geocoding search service
        services.AddScoped<GeocodingSearchService>();

        // Register drawing services
        services.AddScoped<IDrawingManager, DrawingManager>();
        services.AddScoped<MeasurementManager>();

        // Logging and monitoring
        services.AddScoped<MapSdkLogger>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<MapSdkLogger>>();
            return new MapSdkLogger(logger, options.EnablePerformanceMonitoring);
        });

        services.AddScoped<PerformanceMonitor>(sp =>
        {
            var logger = sp.GetRequiredService<MapSdkLogger>();
            return new PerformanceMonitor(logger, options.EnablePerformanceMonitoring);
        });

        // Keyboard shortcuts (if enabled)
        if (options.Accessibility.EnableKeyboardShortcuts)
        {
            services.AddScoped<KeyboardShortcuts>();
        }

        // Register MapLibre support if configured
        if (configureMapLibre != null)
        {
            services.AddMapLibreSupport(configureMapLibre);
        }

        return services;
    }

    /// <summary>
    /// Adds MapSDK data loading services only (for scenarios where you only need data loading).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action for cache options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaMapSDKDataLoading(
        this IServiceCollection services,
        Action<CacheOptions>? configure = null)
    {
        var cacheOptions = new CacheOptions();
        configure?.Invoke(cacheOptions);

        services.AddSingleton(sp => new DataCache(cacheOptions));
        services.AddHttpClient<DataLoader>();
        services.AddScoped<DataLoader>();
        services.AddScoped<StreamingLoader>();

        return services;
    }
}
