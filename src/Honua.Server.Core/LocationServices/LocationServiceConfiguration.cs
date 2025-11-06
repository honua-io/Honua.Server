// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.LocationServices;

/// <summary>
/// Configuration for location services (geocoding, routing, basemap tiles).
/// </summary>
public class LocationServiceConfiguration
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "LocationServices";

    /// <summary>
    /// Default geocoding provider ("azure-maps", "nominatim", etc.)
    /// </summary>
    public string GeocodingProvider { get; set; } = "nominatim";

    /// <summary>
    /// Default routing provider ("azure-maps", "osrm", etc.)
    /// </summary>
    public string RoutingProvider { get; set; } = "osrm";

    /// <summary>
    /// Default basemap tile provider ("azure-maps", "openstreetmap", etc.)
    /// </summary>
    public string BasemapTileProvider { get; set; } = "openstreetmap";

    /// <summary>
    /// Azure Maps configuration
    /// </summary>
    public AzureMapsConfiguration? AzureMaps { get; set; }

    /// <summary>
    /// Nominatim configuration
    /// </summary>
    public NominatimConfiguration? Nominatim { get; set; }

    /// <summary>
    /// OSRM configuration
    /// </summary>
    public OsrmConfiguration? Osrm { get; set; }

    /// <summary>
    /// OpenStreetMap tiles configuration
    /// </summary>
    public OsmTilesConfiguration? OsmTiles { get; set; }
}

/// <summary>
/// Azure Maps provider configuration
/// </summary>
public class AzureMapsConfiguration
{
    /// <summary>
    /// Azure Maps subscription key
    /// </summary>
    public required string SubscriptionKey { get; set; }

    /// <summary>
    /// Optional custom base URL (for sovereign clouds)
    /// </summary>
    public string BaseUrl { get; set; } = "https://atlas.microsoft.com";
}

/// <summary>
/// Nominatim provider configuration
/// </summary>
public class NominatimConfiguration
{
    /// <summary>
    /// Base URL for Nominatim service
    /// Default: https://nominatim.openstreetmap.org (public, rate-limited)
    /// For production, use your own Nominatim instance
    /// </summary>
    public string BaseUrl { get; set; } = "https://nominatim.openstreetmap.org";

    /// <summary>
    /// User-Agent header value (required by Nominatim)
    /// </summary>
    public string UserAgent { get; set; } = "HonuaServer/1.0";
}

/// <summary>
/// OSRM provider configuration
/// </summary>
public class OsrmConfiguration
{
    /// <summary>
    /// Base URL for OSRM service
    /// Default: https://router.project-osrm.org (public, rate-limited)
    /// For production, use your own OSRM instance
    /// </summary>
    public string BaseUrl { get; set; } = "https://router.project-osrm.org";
}

/// <summary>
/// OpenStreetMap tiles configuration
/// </summary>
public class OsmTilesConfiguration
{
    /// <summary>
    /// User-Agent header value (required by OSM tile servers)
    /// </summary>
    public string UserAgent { get; set; } = "HonuaServer/1.0";

    /// <summary>
    /// Optional custom tile server URLs
    /// </summary>
    public Dictionary<string, string>? CustomTileUrls { get; set; }
}
