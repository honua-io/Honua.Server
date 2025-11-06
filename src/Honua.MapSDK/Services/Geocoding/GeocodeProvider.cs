namespace Honua.MapSDK.Services.Geocoding;

/// <summary>
/// Enumeration of supported geocoding providers
/// </summary>
public enum GeocodeProvider
{
    /// <summary>
    /// OpenStreetMap Nominatim (free, no API key required)
    /// Usage policy: https://operations.osmfoundation.org/policies/nominatim/
    /// </summary>
    Nominatim,

    /// <summary>
    /// Mapbox Geocoding API (requires API key)
    /// https://docs.mapbox.com/api/search/geocoding/
    /// </summary>
    Mapbox,

    /// <summary>
    /// Google Maps Geocoding API (requires API key)
    /// https://developers.google.com/maps/documentation/geocoding
    /// </summary>
    Google,

    /// <summary>
    /// Custom geocoding provider (user-implemented)
    /// </summary>
    Custom
}
