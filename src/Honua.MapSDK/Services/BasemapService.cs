using Honua.MapSDK.Models;

namespace Honua.MapSDK.Services;

/// <summary>
/// Service for managing basemaps, including built-in and custom basemaps
/// </summary>
public class BasemapService
{
    private readonly List<Basemap> _basemaps = new();
    private BasemapGallerySettings _settings = new();

    public BasemapService()
    {
        InitializeBuiltInBasemaps();
    }

    /// <summary>
    /// Get all available basemaps
    /// </summary>
    public List<Basemap> GetBasemaps()
    {
        return _basemaps.OrderBy(b => b.SortOrder).ToList();
    }

    /// <summary>
    /// Get basemaps by category
    /// </summary>
    public List<Basemap> GetBasemapsByCategory(string category)
    {
        return _basemaps
            .Where(b => b.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .OrderBy(b => b.SortOrder)
            .ToList();
    }

    /// <summary>
    /// Get a basemap by ID
    /// </summary>
    public Basemap? GetBasemap(string id)
    {
        return _basemaps.FirstOrDefault(b => b.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Search basemaps by name or tags
    /// </summary>
    public List<Basemap> SearchBasemaps(string query)
    {
        var lowerQuery = query.ToLowerInvariant();
        return _basemaps
            .Where(b =>
                b.Name.ToLowerInvariant().Contains(lowerQuery) ||
                b.Tags.Any(t => t.ToLowerInvariant().Contains(lowerQuery)) ||
                (b.Description != null && b.Description.ToLowerInvariant().Contains(lowerQuery))
            )
            .OrderBy(b => b.SortOrder)
            .ToList();
    }

    /// <summary>
    /// Add a custom basemap
    /// </summary>
    public void AddCustomBasemap(Basemap basemap)
    {
        basemap.IsCustom = true;
        basemap.Category = BasemapCategories.Custom;
        _basemaps.Add(basemap);
    }

    /// <summary>
    /// Remove a custom basemap
    /// </summary>
    public bool RemoveCustomBasemap(string id)
    {
        var basemap = _basemaps.FirstOrDefault(b => b.Id == id && b.IsCustom);
        if (basemap != null)
        {
            _basemaps.Remove(basemap);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Get all categories
    /// </summary>
    public string[] GetCategories()
    {
        return _basemaps
            .Select(b => b.Category)
            .Distinct()
            .OrderBy(c => Array.IndexOf(BasemapCategories.All, c))
            .ToArray();
    }

    /// <summary>
    /// Get or set user settings
    /// </summary>
    public BasemapGallerySettings Settings
    {
        get => _settings;
        set => _settings = value;
    }

    /// <summary>
    /// Mark a basemap as recently used
    /// </summary>
    public void MarkAsRecentlyUsed(string basemapId)
    {
        _settings.RecentlyUsed.Remove(basemapId);
        _settings.RecentlyUsed.Insert(0, basemapId);

        if (_settings.RecentlyUsed.Count > _settings.MaxRecentlyUsed)
        {
            _settings.RecentlyUsed = _settings.RecentlyUsed.Take(_settings.MaxRecentlyUsed).ToList();
        }
    }

    /// <summary>
    /// Toggle favorite status
    /// </summary>
    public void ToggleFavorite(string basemapId)
    {
        if (_settings.Favorites.Contains(basemapId))
        {
            _settings.Favorites.Remove(basemapId);
        }
        else
        {
            _settings.Favorites.Add(basemapId);
        }
    }

    /// <summary>
    /// Get favorite basemaps
    /// </summary>
    public List<Basemap> GetFavorites()
    {
        return _basemaps
            .Where(b => _settings.Favorites.Contains(b.Id))
            .OrderBy(b => _settings.Favorites.IndexOf(b.Id))
            .ToList();
    }

    /// <summary>
    /// Get recently used basemaps
    /// </summary>
    public List<Basemap> GetRecentlyUsed()
    {
        return _basemaps
            .Where(b => _settings.RecentlyUsed.Contains(b.Id))
            .OrderBy(b => _settings.RecentlyUsed.IndexOf(b.Id))
            .ToList();
    }

    /// <summary>
    /// Initialize built-in basemaps
    /// </summary>
    private void InitializeBuiltInBasemaps()
    {
        // Streets Category
        _basemaps.AddRange(new[]
        {
            new Basemap
            {
                Id = "osm-standard",
                Name = "OpenStreetMap",
                Category = BasemapCategories.Streets,
                StyleUrl = "https://demotiles.maplibre.org/style.json",
                ThumbnailUrl = "/_content/Honua.MapSDK/basemap-thumbnails/osm-standard.png",
                Provider = "OpenStreetMap",
                Description = "Standard OpenStreetMap basemap with bright colors",
                Attribution = "© OpenStreetMap contributors",
                MinZoom = 0,
                MaxZoom = 22,
                SortOrder = 1,
                Tags = new List<string> { "street", "default", "osm", "bright" }
            },
            new Basemap
            {
                Id = "carto-positron",
                Name = "Carto Positron",
                Category = BasemapCategories.Streets,
                StyleUrl = "https://basemaps.cartocdn.com/gl/positron-gl-style/style.json",
                ThumbnailUrl = "/_content/Honua.MapSDK/basemap-thumbnails/carto-positron.png",
                Provider = "Carto",
                Description = "Light, minimalist basemap perfect for data visualization",
                Attribution = "© CARTO, © OpenStreetMap contributors",
                MinZoom = 0,
                MaxZoom = 22,
                SortOrder = 2,
                Tags = new List<string> { "light", "minimal", "clean", "data-viz" }
            },
            new Basemap
            {
                Id = "carto-dark-matter",
                Name = "Carto Dark Matter",
                Category = BasemapCategories.Streets,
                StyleUrl = "https://basemaps.cartocdn.com/gl/dark-matter-gl-style/style.json",
                ThumbnailUrl = "/_content/Honua.MapSDK/basemap-thumbnails/carto-dark-matter.png",
                Provider = "Carto",
                Description = "Dark theme basemap for stunning visualizations",
                Attribution = "© CARTO, © OpenStreetMap contributors",
                MinZoom = 0,
                MaxZoom = 22,
                SortOrder = 3,
                Tags = new List<string> { "dark", "night", "data-viz", "minimal" }
            },
            new Basemap
            {
                Id = "osm-liberty",
                Name = "OSM Liberty",
                Category = BasemapCategories.Streets,
                StyleUrl = "https://tiles.openfreemap.org/styles/liberty",
                ThumbnailUrl = "/_content/Honua.MapSDK/basemap-thumbnails/osm-liberty.png",
                Provider = "OpenMapTiles",
                Description = "Classic OpenStreetMap style with detailed streets",
                Attribution = "© OpenMapTiles, © OpenStreetMap contributors",
                MinZoom = 0,
                MaxZoom = 22,
                SortOrder = 4,
                Tags = new List<string> { "street", "detailed", "classic" }
            }
        });

        // Satellite Category
        _basemaps.AddRange(new[]
        {
            new Basemap
            {
                Id = "esri-world-imagery",
                Name = "ESRI World Imagery",
                Category = BasemapCategories.Satellite,
                StyleUrl = "https://basemaps-api.arcgis.com/arcgis/rest/services/styles/ArcGIS:Imagery",
                ThumbnailUrl = "/_content/Honua.MapSDK/basemap-thumbnails/esri-world-imagery.png",
                Provider = "Esri",
                Description = "High-resolution satellite imagery from multiple sources",
                Attribution = "© Esri, Maxar, Earthstar Geographics",
                RequiresApiKey = false,
                MinZoom = 0,
                MaxZoom = 19,
                SortOrder = 1,
                Tags = new List<string> { "satellite", "aerial", "imagery", "photo" }
            },
            new Basemap
            {
                Id = "mapbox-satellite",
                Name = "Mapbox Satellite",
                Category = BasemapCategories.Satellite,
                StyleUrl = "mapbox://styles/mapbox/satellite-v9",
                ThumbnailUrl = "/_content/Honua.MapSDK/basemap-thumbnails/mapbox-satellite.png",
                Provider = "Mapbox",
                Description = "Global satellite and aerial imagery",
                Attribution = "© Mapbox, © Maxar",
                RequiresApiKey = true,
                MinZoom = 0,
                MaxZoom = 22,
                SortOrder = 2,
                IsPremium = true,
                Tags = new List<string> { "satellite", "aerial", "imagery" }
            },
            new Basemap
            {
                Id = "satellite-streets",
                Name = "Satellite Streets",
                Category = BasemapCategories.Satellite,
                StyleUrl = "mapbox://styles/mapbox/satellite-streets-v12",
                ThumbnailUrl = "/_content/Honua.MapSDK/basemap-thumbnails/satellite-streets.png",
                Provider = "Mapbox",
                Description = "Satellite imagery with street labels and borders",
                Attribution = "© Mapbox, © Maxar, © OpenStreetMap",
                RequiresApiKey = true,
                MinZoom = 0,
                MaxZoom = 22,
                SortOrder = 3,
                IsPremium = true,
                Tags = new List<string> { "satellite", "hybrid", "labels" }
            }
        });

        // Terrain Category
        _basemaps.AddRange(new[]
        {
            new Basemap
            {
                Id = "opentopomap",
                Name = "OpenTopoMap",
                Category = BasemapCategories.Terrain,
                StyleUrl = "https://tile.opentopomap.org/{z}/{x}/{y}.png",
                ThumbnailUrl = "/_content/Honua.MapSDK/basemap-thumbnails/opentopomap.png",
                Provider = "OpenTopoMap",
                Description = "Topographic map with contour lines and terrain shading",
                Attribution = "© OpenTopoMap contributors",
                MinZoom = 0,
                MaxZoom = 17,
                SortOrder = 1,
                Tags = new List<string> { "topo", "topographic", "contour", "terrain", "hiking" }
            },
            new Basemap
            {
                Id = "stamen-terrain",
                Name = "Stamen Terrain",
                Category = BasemapCategories.Terrain,
                StyleUrl = "https://tiles.stadiamaps.com/styles/stamen_terrain.json",
                ThumbnailUrl = "/_content/Honua.MapSDK/basemap-thumbnails/stamen-terrain.png",
                Provider = "Stamen / Stadia Maps",
                Description = "Beautiful terrain map with hill shading",
                Attribution = "© Stamen Design, © Stadia Maps, © OpenStreetMap",
                MinZoom = 0,
                MaxZoom = 18,
                SortOrder = 2,
                Tags = new List<string> { "terrain", "hillshade", "relief" }
            },
            new Basemap
            {
                Id = "maptiler-outdoor",
                Name = "MapTiler Outdoor",
                Category = BasemapCategories.Terrain,
                StyleUrl = "https://api.maptiler.com/maps/outdoor-v2/style.json?key=get_your_own_key",
                ThumbnailUrl = "/_content/Honua.MapSDK/basemap-thumbnails/maptiler-outdoor.png",
                Provider = "MapTiler",
                Description = "Detailed outdoor map for hiking and recreation",
                Attribution = "© MapTiler, © OpenStreetMap contributors",
                RequiresApiKey = true,
                MinZoom = 0,
                MaxZoom = 22,
                SortOrder = 3,
                Tags = new List<string> { "outdoor", "hiking", "trails", "terrain" }
            },
            new Basemap
            {
                Id = "esri-world-terrain",
                Name = "ESRI World Terrain",
                Category = BasemapCategories.Terrain,
                StyleUrl = "https://basemaps-api.arcgis.com/arcgis/rest/services/styles/ArcGIS:Terrain",
                ThumbnailUrl = "/_content/Honua.MapSDK/basemap-thumbnails/esri-world-terrain.png",
                Provider = "Esri",
                Description = "Physical terrain map with elevation shading",
                Attribution = "© Esri, USGS, NOAA",
                MinZoom = 0,
                MaxZoom = 13,
                SortOrder = 4,
                Tags = new List<string> { "terrain", "elevation", "physical" }
            }
        });

        // Specialty Category
        _basemaps.AddRange(new[]
        {
            new Basemap
            {
                Id = "stamen-watercolor",
                Name = "Watercolor",
                Category = BasemapCategories.Specialty,
                StyleUrl = "https://tiles.stadiamaps.com/styles/stamen_watercolor.json",
                ThumbnailUrl = "/_content/Honua.MapSDK/basemap-thumbnails/stamen-watercolor.png",
                Provider = "Stamen / Stadia Maps",
                Description = "Artistic watercolor-style map",
                Attribution = "© Stamen Design, © Stadia Maps, © OpenStreetMap",
                MinZoom = 0,
                MaxZoom = 18,
                SortOrder = 1,
                Tags = new List<string> { "artistic", "watercolor", "creative", "presentation" }
            },
            new Basemap
            {
                Id = "stamen-toner",
                Name = "Black & White",
                Category = BasemapCategories.Specialty,
                StyleUrl = "https://tiles.stadiamaps.com/styles/stamen_toner.json",
                ThumbnailUrl = "/_content/Honua.MapSDK/basemap-thumbnails/stamen-toner.png",
                Provider = "Stamen / Stadia Maps",
                Description = "High contrast black and white map",
                Attribution = "© Stamen Design, © Stadia Maps, © OpenStreetMap",
                MinZoom = 0,
                MaxZoom = 20,
                SortOrder = 2,
                Tags = new List<string> { "black-white", "monochrome", "contrast", "print" }
            },
            new Basemap
            {
                Id = "mapbox-outdoors",
                Name = "Blueprint",
                Category = BasemapCategories.Specialty,
                StyleUrl = "mapbox://styles/mapbox/outdoors-v12",
                ThumbnailUrl = "/_content/Honua.MapSDK/basemap-thumbnails/blueprint.png",
                Provider = "Mapbox",
                Description = "Blueprint-style technical map",
                Attribution = "© Mapbox, © OpenStreetMap",
                RequiresApiKey = true,
                MinZoom = 0,
                MaxZoom = 22,
                SortOrder = 3,
                IsPremium = true,
                Tags = new List<string> { "blueprint", "technical", "engineering" }
            },
            new Basemap
            {
                Id = "vintage",
                Name = "Vintage",
                Category = BasemapCategories.Specialty,
                StyleUrl = "https://tiles.stadiamaps.com/styles/alidade_smooth.json",
                ThumbnailUrl = "/_content/Honua.MapSDK/basemap-thumbnails/vintage.png",
                Provider = "Stadia Maps",
                Description = "Vintage-style map with muted colors",
                Attribution = "© Stadia Maps, © OpenStreetMap",
                MinZoom = 0,
                MaxZoom = 20,
                SortOrder = 4,
                Tags = new List<string> { "vintage", "retro", "classic", "sepia" }
            }
        });
    }
}
