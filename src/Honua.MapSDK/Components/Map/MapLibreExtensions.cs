// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Honua.MapSDK.Components.Map;

/// <summary>
/// Extension methods for registering MapLibre support in the service collection.
/// </summary>
public static class MapLibreExtensions
{
    /// <summary>
    /// Add MapLibre GL JS support to the application.
    /// This registers necessary services and configurations for the HonuaMapLibre component.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMapLibreSupport(
        this IServiceCollection services,
        Action<MapLibreOptions>? configure = null)
    {
        // Register MapLibre options
        var options = new MapLibreOptions();
        configure?.Invoke(options);
        services.TryAddSingleton(options);

        // Register default configuration service if not already registered
        services.TryAddScoped<IMapLibreConfigurationProvider, DefaultMapLibreConfigurationProvider>();

        return services;
    }

    /// <summary>
    /// Add MapLibre GL JS support with a custom configuration provider.
    /// </summary>
    /// <typeparam name="TProvider">Custom configuration provider type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMapLibreSupport<TProvider>(
        this IServiceCollection services)
        where TProvider : class, IMapLibreConfigurationProvider
    {
        services.TryAddScoped<IMapLibreConfigurationProvider, TProvider>();
        return services;
    }
}

/// <summary>
/// Options for MapLibre configuration.
/// </summary>
public class MapLibreOptions
{
    /// <summary>
    /// Default map style URL.
    /// </summary>
    public string DefaultStyle { get; set; } = "https://demotiles.maplibre.org/style.json";

    /// <summary>
    /// Default center position [longitude, latitude].
    /// </summary>
    public double[] DefaultCenter { get; set; } = new[] { 0.0, 0.0 };

    /// <summary>
    /// Default zoom level.
    /// </summary>
    public double DefaultZoom { get; set; } = 2.0;

    /// <summary>
    /// Enable performance optimizations.
    /// </summary>
    public bool EnablePerformanceOptimizations { get; set; } = true;

    /// <summary>
    /// Maximum tile cache size.
    /// </summary>
    public int? MaxTileCacheSize { get; set; }

    /// <summary>
    /// MapLibre GL JS version to use (defaults to latest v4).
    /// </summary>
    public string MapLibreVersion { get; set; } = "4";

    /// <summary>
    /// CDN base URL for MapLibre GL JS.
    /// </summary>
    public string CdnBaseUrl { get; set; } = "https://unpkg.com/maplibre-gl";

    /// <summary>
    /// Enable debug mode.
    /// </summary>
    public bool DebugMode { get; set; } = false;

    /// <summary>
    /// Enable accessibility features.
    /// </summary>
    public bool EnableAccessibility { get; set; } = true;

    /// <summary>
    /// Default language for map labels (ISO 639-1 code).
    /// </summary>
    public string? DefaultLanguage { get; set; }

    /// <summary>
    /// Enable hash navigation (URL updates with map position).
    /// </summary>
    public bool EnableHashNavigation { get; set; } = false;

    /// <summary>
    /// Enable cross-source collision detection for symbols.
    /// </summary>
    public bool EnableCrossSourceCollisions { get; set; } = true;

    /// <summary>
    /// Fade duration for symbols (milliseconds).
    /// </summary>
    public int FadeDuration { get; set; } = 300;

    /// <summary>
    /// Custom attribution text.
    /// </summary>
    public string? CustomAttribution { get; set; }

    /// <summary>
    /// Enable terrain (3D) support.
    /// </summary>
    public bool EnableTerrain { get; set; } = false;

    /// <summary>
    /// Terrain source URL (if terrain is enabled).
    /// </summary>
    public string? TerrainSource { get; set; }

    /// <summary>
    /// Terrain exaggeration factor.
    /// </summary>
    public double TerrainExaggeration { get; set; } = 1.0;

    /// <summary>
    /// Enable globe projection at low zoom levels.
    /// </summary>
    public bool EnableGlobeProjection { get; set; } = false;

    /// <summary>
    /// Default projection type (mercator, globe, etc.).
    /// </summary>
    public string DefaultProjection { get; set; } = "mercator";
}

/// <summary>
/// Interface for providing MapLibre configuration.
/// </summary>
public interface IMapLibreConfigurationProvider
{
    /// <summary>
    /// Get MapLibre initialization options.
    /// </summary>
    /// <returns>MapLibre initialization options.</returns>
    Task<MapLibreInitOptions> GetInitOptionsAsync();

    /// <summary>
    /// Get available map styles.
    /// </summary>
    /// <returns>Dictionary of style name to style URL/object.</returns>
    Task<Dictionary<string, object>> GetAvailableStylesAsync();

    /// <summary>
    /// Get style URL by identifier.
    /// </summary>
    /// <param name="styleId">Style identifier.</param>
    /// <returns>Style URL or object.</returns>
    Task<object?> GetStyleAsync(string styleId);
}

/// <summary>
/// Default implementation of MapLibre configuration provider.
/// </summary>
public class DefaultMapLibreConfigurationProvider : IMapLibreConfigurationProvider
{
    private readonly MapLibreOptions _options;

    public DefaultMapLibreConfigurationProvider(MapLibreOptions options)
    {
        _options = options;
    }

    public Task<MapLibreInitOptions> GetInitOptionsAsync()
    {
        var initOptions = new MapLibreInitOptions
        {
            Container = "map",
            Style = _options.DefaultStyle,
            Center = _options.DefaultCenter,
            Zoom = _options.DefaultZoom,
            Projection = _options.DefaultProjection,
            Hash = _options.EnableHashNavigation,
            FadeDuration = _options.FadeDuration,
            MaxTileCacheSize = _options.MaxTileCacheSize,
            CrossSourceCache = _options.EnableCrossSourceCollisions
        };

        return Task.FromResult(initOptions);
    }

    public Task<Dictionary<string, object>> GetAvailableStylesAsync()
    {
        var styles = new Dictionary<string, object>
        {
            ["demo"] = "https://demotiles.maplibre.org/style.json",
            ["default"] = _options.DefaultStyle
        };

        return Task.FromResult(styles);
    }

    public Task<object?> GetStyleAsync(string styleId)
    {
        return styleId switch
        {
            "demo" => Task.FromResult<object?>("https://demotiles.maplibre.org/style.json"),
            "default" => Task.FromResult<object?>(_options.DefaultStyle),
            _ => Task.FromResult<object?>(null)
        };
    }
}

/// <summary>
/// Builder for MapLibre configuration.
/// </summary>
public class MapLibreOptionsBuilder
{
    private readonly MapLibreOptions _options = new();

    /// <summary>
    /// Set default style.
    /// </summary>
    public MapLibreOptionsBuilder WithDefaultStyle(string styleUrl)
    {
        _options.DefaultStyle = styleUrl;
        return this;
    }

    /// <summary>
    /// Set default center.
    /// </summary>
    public MapLibreOptionsBuilder WithDefaultCenter(double longitude, double latitude)
    {
        _options.DefaultCenter = new[] { longitude, latitude };
        return this;
    }

    /// <summary>
    /// Set default zoom.
    /// </summary>
    public MapLibreOptionsBuilder WithDefaultZoom(double zoom)
    {
        _options.DefaultZoom = zoom;
        return this;
    }

    /// <summary>
    /// Enable debug mode.
    /// </summary>
    public MapLibreOptionsBuilder WithDebugMode(bool enabled = true)
    {
        _options.DebugMode = enabled;
        return this;
    }

    /// <summary>
    /// Enable terrain.
    /// </summary>
    public MapLibreOptionsBuilder WithTerrain(string sourceUrl, double exaggeration = 1.0)
    {
        _options.EnableTerrain = true;
        _options.TerrainSource = sourceUrl;
        _options.TerrainExaggeration = exaggeration;
        return this;
    }

    /// <summary>
    /// Enable globe projection.
    /// </summary>
    public MapLibreOptionsBuilder WithGlobeProjection(bool enabled = true)
    {
        _options.EnableGlobeProjection = enabled;
        _options.DefaultProjection = enabled ? "globe" : "mercator";
        return this;
    }

    /// <summary>
    /// Set default language.
    /// </summary>
    public MapLibreOptionsBuilder WithDefaultLanguage(string languageCode)
    {
        _options.DefaultLanguage = languageCode;
        return this;
    }

    /// <summary>
    /// Enable hash navigation.
    /// </summary>
    public MapLibreOptionsBuilder WithHashNavigation(bool enabled = true)
    {
        _options.EnableHashNavigation = enabled;
        return this;
    }

    /// <summary>
    /// Set tile cache size.
    /// </summary>
    public MapLibreOptionsBuilder WithTileCacheSize(int size)
    {
        _options.MaxTileCacheSize = size;
        return this;
    }

    /// <summary>
    /// Build the options.
    /// </summary>
    public MapLibreOptions Build()
    {
        return _options;
    }
}
