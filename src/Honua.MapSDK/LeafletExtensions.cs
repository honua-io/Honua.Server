// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.MapSDK.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.MapSDK;

/// <summary>
/// Extension methods for adding Leaflet support to the MapSDK
/// </summary>
public static class LeafletExtensions
{
    /// <summary>
    /// Add Leaflet map support to the MapSDK
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureOptions">Optional configuration action</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddLeafletSupport(
        this IServiceCollection services,
        Action<LeafletConfiguration>? configureOptions = null)
    {
        // Register Leaflet configuration
        var config = new LeafletConfiguration();
        configureOptions?.Invoke(config);
        services.AddSingleton(config);

        return services;
    }
}

/// <summary>
/// Global Leaflet configuration
/// </summary>
public class LeafletConfiguration
{
    /// <summary>
    /// Default tile layer URL template
    /// </summary>
    public string DefaultTileUrl { get; set; } = "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png";

    /// <summary>
    /// Default attribution text
    /// </summary>
    public string DefaultAttribution { get; set; } = "&copy; <a href='https://www.openstreetmap.org/copyright'>OpenStreetMap</a> contributors";

    /// <summary>
    /// Leaflet library CDN URL
    /// </summary>
    public string LeafletCdnUrl { get; set; } = "https://unpkg.com/leaflet@1.9.4/dist/leaflet.js";

    /// <summary>
    /// Leaflet CSS CDN URL
    /// </summary>
    public string LeafletCssCdnUrl { get; set; } = "https://unpkg.com/leaflet@1.9.4/dist/leaflet.css";

    /// <summary>
    /// Enable marker cluster plugin
    /// </summary>
    public bool EnableMarkerCluster { get; set; } = true;

    /// <summary>
    /// Marker cluster plugin CDN URL
    /// </summary>
    public string MarkerClusterCdnUrl { get; set; } = "https://unpkg.com/leaflet.markercluster@1.5.3/dist/leaflet.markercluster.js";

    /// <summary>
    /// Marker cluster CSS CDN URL
    /// </summary>
    public string MarkerClusterCssCdnUrl { get; set; } = "https://unpkg.com/leaflet.markercluster@1.5.3/dist/MarkerCluster.Default.css";

    /// <summary>
    /// Enable drawing tools plugin
    /// </summary>
    public bool EnableDraw { get; set; } = true;

    /// <summary>
    /// Leaflet.draw plugin CDN URL
    /// </summary>
    public string DrawCdnUrl { get; set; } = "https://unpkg.com/leaflet-draw@1.0.4/dist/leaflet.draw.js";

    /// <summary>
    /// Leaflet.draw CSS CDN URL
    /// </summary>
    public string DrawCssCdnUrl { get; set; } = "https://unpkg.com/leaflet-draw@1.0.4/dist/leaflet.draw.css";

    /// <summary>
    /// Enable measure tools plugin
    /// </summary>
    public bool EnableMeasure { get; set; } = true;

    /// <summary>
    /// Leaflet.measure plugin CDN URL
    /// </summary>
    public string MeasureCdnUrl { get; set; } = "https://unpkg.com/leaflet-measure@3.1.0/dist/leaflet-measure.js";

    /// <summary>
    /// Leaflet.measure CSS CDN URL
    /// </summary>
    public string MeasureCssCdnUrl { get; set; } = "https://unpkg.com/leaflet-measure@3.1.0/dist/leaflet-measure.css";

    /// <summary>
    /// Enable fullscreen control plugin
    /// </summary>
    public bool EnableFullscreen { get; set; } = true;

    /// <summary>
    /// Leaflet.fullscreen plugin CDN URL
    /// </summary>
    public string FullscreenCdnUrl { get; set; } = "https://unpkg.com/leaflet.fullscreen@2.4.0/Control.FullScreen.js";

    /// <summary>
    /// Leaflet.fullscreen CSS CDN URL
    /// </summary>
    public string FullscreenCssCdnUrl { get; set; } = "https://unpkg.com/leaflet.fullscreen@2.4.0/Control.FullScreen.css";

    /// <summary>
    /// Default zoom level
    /// </summary>
    public double DefaultZoom { get; set; } = 2;

    /// <summary>
    /// Default center [latitude, longitude]
    /// </summary>
    public double[] DefaultCenter { get; set; } = new[] { 0.0, 0.0 };

    /// <summary>
    /// Default minimum zoom
    /// </summary>
    public double? DefaultMinZoom { get; set; }

    /// <summary>
    /// Default maximum zoom
    /// </summary>
    public double? DefaultMaxZoom { get; set; } = 18;

    /// <summary>
    /// Enable retina/high-DPI tile support
    /// </summary>
    public bool EnableRetina { get; set; } = true;

    /// <summary>
    /// Default marker cluster radius
    /// </summary>
    public int DefaultClusterRadius { get; set; } = 80;

    /// <summary>
    /// Custom tile layer configurations (for predefined basemaps)
    /// </summary>
    public Dictionary<string, LeafletTileLayer> CustomTileLayers { get; set; } = new();

    /// <summary>
    /// Initialize with common basemap providers
    /// </summary>
    public void AddCommonBasemaps()
    {
        CustomTileLayers["osm"] = new LeafletTileLayer
        {
            Id = "osm",
            Url = "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png",
            Attribution = "&copy; OpenStreetMap contributors",
            Options = new Dictionary<string, object>
            {
                ["maxZoom"] = 19
            }
        };

        CustomTileLayers["osm-hot"] = new LeafletTileLayer
        {
            Id = "osm-hot",
            Url = "https://{s}.tile.openstreetmap.fr/hot/{z}/{x}/{y}.png",
            Attribution = "&copy; OpenStreetMap contributors, Tiles style by Humanitarian OpenStreetMap Team",
            Options = new Dictionary<string, object>
            {
                ["maxZoom"] = 19
            }
        };

        CustomTileLayers["cartodb-dark"] = new LeafletTileLayer
        {
            Id = "cartodb-dark",
            Url = "https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png",
            Attribution = "&copy; OpenStreetMap contributors &copy; CARTO",
            Subdomains = new[] { "a", "b", "c", "d" },
            Options = new Dictionary<string, object>
            {
                ["maxZoom"] = 19
            }
        };

        CustomTileLayers["cartodb-light"] = new LeafletTileLayer
        {
            Id = "cartodb-light",
            Url = "https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png",
            Attribution = "&copy; OpenStreetMap contributors &copy; CARTO",
            Subdomains = new[] { "a", "b", "c", "d" },
            Options = new Dictionary<string, object>
            {
                ["maxZoom"] = 19
            }
        };

        CustomTileLayers["esri-worldimagery"] = new LeafletTileLayer
        {
            Id = "esri-worldimagery",
            Url = "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}",
            Attribution = "Tiles &copy; Esri &mdash; Source: Esri, i-cubed, USDA, USGS, AEX, GeoEye, Getmapping, Aerogrid, IGN, IGP, UPR-EGP, and the GIS User Community",
            Options = new Dictionary<string, object>
            {
                ["maxZoom"] = 18
            }
        };

        CustomTileLayers["esri-worldstreetmap"] = new LeafletTileLayer
        {
            Id = "esri-worldstreetmap",
            Url = "https://server.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer/tile/{z}/{y}/{x}",
            Attribution = "Tiles &copy; Esri",
            Options = new Dictionary<string, object>
            {
                ["maxZoom"] = 18
            }
        };

        CustomTileLayers["stamen-terrain"] = new LeafletTileLayer
        {
            Id = "stamen-terrain",
            Url = "https://stamen-tiles-{s}.a.ssl.fastly.net/terrain/{z}/{x}/{y}{r}.png",
            Attribution = "Map tiles by Stamen Design, CC BY 3.0 &mdash; Map data &copy; OpenStreetMap contributors",
            Subdomains = new[] { "a", "b", "c", "d" },
            Options = new Dictionary<string, object>
            {
                ["maxZoom"] = 18
            }
        };

        CustomTileLayers["stamen-watercolor"] = new LeafletTileLayer
        {
            Id = "stamen-watercolor",
            Url = "https://stamen-tiles-{s}.a.ssl.fastly.net/watercolor/{z}/{x}/{y}.jpg",
            Attribution = "Map tiles by Stamen Design, CC BY 3.0 &mdash; Map data &copy; OpenStreetMap contributors",
            Subdomains = new[] { "a", "b", "c", "d" },
            Options = new Dictionary<string, object>
            {
                ["maxZoom"] = 16
            }
        };
    }

    /// <summary>
    /// Get HTML script/link tags for loading Leaflet and plugins
    /// </summary>
    public string GetCdnIncludes()
    {
        var includes = new List<string>();

        // Core Leaflet
        includes.Add($"<link rel=\"stylesheet\" href=\"{LeafletCssCdnUrl}\" />");
        includes.Add($"<script src=\"{LeafletCdnUrl}\"></script>");

        // Plugins
        if (EnableMarkerCluster)
        {
            includes.Add($"<link rel=\"stylesheet\" href=\"{MarkerClusterCssCdnUrl}\" />");
            includes.Add($"<script src=\"{MarkerClusterCdnUrl}\"></script>");
        }

        if (EnableDraw)
        {
            includes.Add($"<link rel=\"stylesheet\" href=\"{DrawCssCdnUrl}\" />");
            includes.Add($"<script src=\"{DrawCdnUrl}\"></script>");
        }

        if (EnableMeasure)
        {
            includes.Add($"<link rel=\"stylesheet\" href=\"{MeasureCssCdnUrl}\" />");
            includes.Add($"<script src=\"{MeasureCdnUrl}\"></script>");
        }

        if (EnableFullscreen)
        {
            includes.Add($"<link rel=\"stylesheet\" href=\"{FullscreenCssCdnUrl}\" />");
            includes.Add($"<script src=\"{FullscreenCdnUrl}\"></script>");
        }

        return string.Join("\n", includes);
    }
}
