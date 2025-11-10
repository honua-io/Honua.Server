// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Models;

/// <summary>
/// Represents a basemap/background layer with metadata and style information
/// </summary>
public class Basemap
{
    /// <summary>
    /// Unique identifier for the basemap
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Display name of the basemap
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Category: Streets, Satellite, Terrain, Specialty
    /// </summary>
    public required string Category { get; set; }

    /// <summary>
    /// URL to thumbnail preview image
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// MapLibre style URL or style JSON
    /// </summary>
    public required string StyleUrl { get; set; }

    /// <summary>
    /// Whether this basemap requires an API key
    /// </summary>
    public bool RequiresApiKey { get; set; }

    /// <summary>
    /// Provider name (e.g., OpenStreetMap, Mapbox, ESRI)
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Description of the basemap
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Default opacity (0-1)
    /// </summary>
    public double DefaultOpacity { get; set; } = 1.0;

    /// <summary>
    /// Attribution text
    /// </summary>
    public string? Attribution { get; set; }

    /// <summary>
    /// Min zoom level
    /// </summary>
    public double? MinZoom { get; set; }

    /// <summary>
    /// Max zoom level
    /// </summary>
    public double? MaxZoom { get; set; }

    /// <summary>
    /// Additional metadata (tags, keywords, etc.)
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Whether this is a custom user-added basemap
    /// </summary>
    public bool IsCustom { get; set; }

    /// <summary>
    /// Sort order within category
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Whether this basemap is premium/requires subscription
    /// </summary>
    public bool IsPremium { get; set; }

    /// <summary>
    /// Icon name for the basemap category
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Tags for search/filtering
    /// </summary>
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Basemap categories
/// </summary>
public static class BasemapCategories
{
    public const string Streets = "Streets";
    public const string Satellite = "Satellite";
    public const string Terrain = "Terrain";
    public const string Specialty = "Specialty";
    public const string Custom = "Custom";

    public static readonly string[] All = new[]
    {
        Streets,
        Satellite,
        Terrain,
        Specialty,
        Custom
    };
}

/// <summary>
/// User preferences for basemap gallery
/// </summary>
public class BasemapGallerySettings
{
    /// <summary>
    /// Currently active basemap ID
    /// </summary>
    public string? ActiveBasemapId { get; set; }

    /// <summary>
    /// Favorite basemap IDs
    /// </summary>
    public List<string> Favorites { get; set; } = new();

    /// <summary>
    /// Recently used basemap IDs
    /// </summary>
    public List<string> RecentlyUsed { get; set; } = new();

    /// <summary>
    /// Default category to show
    /// </summary>
    public string DefaultCategory { get; set; } = BasemapCategories.Streets;

    /// <summary>
    /// Gallery layout mode
    /// </summary>
    public string Layout { get; set; } = "grid";

    /// <summary>
    /// Maximum number of recently used to track
    /// </summary>
    public int MaxRecentlyUsed { get; set; } = 5;
}
