namespace Honua.MapSDK.Services.Geocoding;

/// <summary>
/// Interface for geocoding providers
/// Defines methods for forward and reverse geocoding
/// </summary>
public interface IGeocoder
{
    /// <summary>
    /// Search for locations matching the query string
    /// </summary>
    /// <param name="query">The search query (e.g., "San Francisco, CA")</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>List of search results ordered by relevance</returns>
    Task<List<SearchResult>> SearchAsync(string query, int limit = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reverse geocode a coordinate to get address information
    /// </summary>
    /// <param name="lat">Latitude</param>
    /// <param name="lon">Longitude</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Search result with address information, or null if not found</returns>
    Task<SearchResult?> ReverseGeocodeAsync(double lat, double lon, CancellationToken cancellationToken = default);

    /// <summary>
    /// Name of the geocoding provider
    /// </summary>
    string ProviderName { get; }
}
