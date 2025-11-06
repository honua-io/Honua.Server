using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Honua.MapSDK.Services.Geocoding;

/// <summary>
/// Geocoding provider using OpenStreetMap Nominatim
/// Free service with usage policy: max 1 request per second
/// https://operations.osmfoundation.org/policies/nominatim/
/// </summary>
public class NominatimGeocoder : IGeocoder
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NominatimGeocoder>? _logger;
    private readonly string _userAgent;
    private readonly string _baseUrl;
    private DateTime _lastRequest = DateTime.MinValue;
    private readonly SemaphoreSlim _rateLimiter = new(1, 1);

    /// <summary>
    /// Provider name
    /// </summary>
    public string ProviderName => "Nominatim (OpenStreetMap)";

    /// <summary>
    /// Creates a new Nominatim geocoder
    /// </summary>
    /// <param name="httpClient">HTTP client for API requests</param>
    /// <param name="userAgent">User agent string (required by Nominatim)</param>
    /// <param name="logger">Optional logger</param>
    /// <param name="baseUrl">Optional custom Nominatim server URL</param>
    public NominatimGeocoder(
        HttpClient httpClient,
        string userAgent = "Honua.MapSDK/1.0",
        ILogger<NominatimGeocoder>? logger = null,
        string baseUrl = "https://nominatim.openstreetmap.org")
    {
        _httpClient = httpClient;
        _userAgent = userAgent;
        _logger = logger;
        _baseUrl = baseUrl.TrimEnd('/');

        // Set user agent header (required by Nominatim)
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", _userAgent);
        }
    }

    /// <summary>
    /// Search for locations matching the query
    /// </summary>
    public async Task<List<SearchResult>> SearchAsync(string query, int limit = 10, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<SearchResult>();
        }

        await EnforceRateLimit(cancellationToken);

        try
        {
            var url = $"{_baseUrl}/search?q={Uri.EscapeDataString(query)}&format=json&limit={limit}&addressdetails=1";

            _logger?.LogDebug("Nominatim search: {Query}", query);

            var response = await _httpClient.GetFromJsonAsync<List<NominatimSearchResult>>(url, cancellationToken);

            if (response == null)
            {
                return new List<SearchResult>();
            }

            return response.Select(MapToSearchResult).ToList();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error searching Nominatim for '{Query}'", query);
            throw new GeocodingException($"Failed to search for '{query}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Reverse geocode a coordinate to get address
    /// </summary>
    public async Task<SearchResult?> ReverseGeocodeAsync(double lat, double lon, CancellationToken cancellationToken = default)
    {
        await EnforceRateLimit(cancellationToken);

        try
        {
            var url = $"{_baseUrl}/reverse?lat={lat}&lon={lon}&format=json&addressdetails=1";

            _logger?.LogDebug("Nominatim reverse: {Lat}, {Lon}", lat, lon);

            var response = await _httpClient.GetFromJsonAsync<NominatimSearchResult>(url, cancellationToken);

            if (response == null)
            {
                return null;
            }

            return MapToSearchResult(response);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error reverse geocoding ({Lat}, {Lon})", lat, lon);
            throw new GeocodingException($"Failed to reverse geocode ({lat}, {lon}): {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Enforce Nominatim rate limit (1 request per second)
    /// </summary>
    private async Task EnforceRateLimit(CancellationToken cancellationToken)
    {
        await _rateLimiter.WaitAsync(cancellationToken);
        try
        {
            var timeSinceLastRequest = DateTime.UtcNow - _lastRequest;
            if (timeSinceLastRequest.TotalMilliseconds < 1000)
            {
                var delay = 1000 - (int)timeSinceLastRequest.TotalMilliseconds;
                await Task.Delay(delay, cancellationToken);
            }
            _lastRequest = DateTime.UtcNow;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    /// <summary>
    /// Map Nominatim result to SearchResult
    /// </summary>
    private SearchResult MapToSearchResult(NominatimSearchResult result)
    {
        var category = DetermineCategory(result.Type, result.Class);

        return new SearchResult
        {
            Id = result.PlaceId?.ToString() ?? result.OsmId?.ToString() ?? Guid.NewGuid().ToString(),
            DisplayName = result.DisplayName ?? "Unknown",
            Latitude = double.Parse(result.Lat ?? "0"),
            Longitude = double.Parse(result.Lon ?? "0"),
            BoundingBox = result.BoundingBox != null && result.BoundingBox.Length >= 4
                ? new[] {
                    double.Parse(result.BoundingBox[2]), // west
                    double.Parse(result.BoundingBox[0]), // south
                    double.Parse(result.BoundingBox[3]), // east
                    double.Parse(result.BoundingBox[1])  // north
                }
                : null,
            Type = result.Type ?? "unknown",
            Category = category,
            Relevance = result.Importance ?? 0.5,
            Street = result.Address?.Road ?? result.Address?.Street,
            City = result.Address?.City ?? result.Address?.Town ?? result.Address?.Village,
            State = result.Address?.State,
            Country = result.Address?.Country,
            CountryCode = result.Address?.CountryCode?.ToUpperInvariant(),
            PostalCode = result.Address?.Postcode,
            RawData = result,
            Metadata = new Dictionary<string, string>
            {
                { "osm_type", result.OsmType ?? "" },
                { "osm_id", result.OsmId?.ToString() ?? "" },
                { "class", result.Class ?? "" },
                { "type", result.Type ?? "" }
            }
        };
    }

    /// <summary>
    /// Determine search result category from Nominatim type and class
    /// </summary>
    private SearchResultCategory DetermineCategory(string? type, string? osmClass)
    {
        if (type == null) return SearchResultCategory.Other;

        return type.ToLowerInvariant() switch
        {
            "city" => SearchResultCategory.City,
            "town" => SearchResultCategory.Town,
            "village" => SearchResultCategory.Village,
            "residential" => SearchResultCategory.Street,
            "road" => SearchResultCategory.Street,
            "building" => SearchResultCategory.Building,
            "house" => SearchResultCategory.Building,
            "tourism" => SearchResultCategory.PointOfInterest,
            "park" => SearchResultCategory.Park,
            "water" => SearchResultCategory.Water,
            "peak" => SearchResultCategory.Mountain,
            "administrative" => SearchResultCategory.Administrative,
            "railway" => SearchResultCategory.Transportation,
            "station" => SearchResultCategory.Transportation,
            "restaurant" => SearchResultCategory.Restaurant,
            "hotel" => SearchResultCategory.Hotel,
            "shop" => SearchResultCategory.Shop,
            _ => osmClass?.ToLowerInvariant() switch
            {
                "place" => SearchResultCategory.City,
                "highway" => SearchResultCategory.Street,
                "building" => SearchResultCategory.Building,
                "amenity" => SearchResultCategory.PointOfInterest,
                "leisure" => SearchResultCategory.Park,
                _ => SearchResultCategory.Other
            }
        };
    }

    #region Nominatim JSON Models

    private class NominatimSearchResult
    {
        [JsonPropertyName("place_id")]
        public long? PlaceId { get; set; }

        [JsonPropertyName("osm_type")]
        public string? OsmType { get; set; }

        [JsonPropertyName("osm_id")]
        public long? OsmId { get; set; }

        [JsonPropertyName("lat")]
        public string? Lat { get; set; }

        [JsonPropertyName("lon")]
        public string? Lon { get; set; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("class")]
        public string? Class { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("importance")]
        public double? Importance { get; set; }

        [JsonPropertyName("boundingbox")]
        public string[]? BoundingBox { get; set; }

        [JsonPropertyName("address")]
        public NominatimAddress? Address { get; set; }
    }

    private class NominatimAddress
    {
        [JsonPropertyName("road")]
        public string? Road { get; set; }

        [JsonPropertyName("street")]
        public string? Street { get; set; }

        [JsonPropertyName("house_number")]
        public string? HouseNumber { get; set; }

        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("town")]
        public string? Town { get; set; }

        [JsonPropertyName("village")]
        public string? Village { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("country_code")]
        public string? CountryCode { get; set; }

        [JsonPropertyName("postcode")]
        public string? Postcode { get; set; }
    }

    #endregion
}

/// <summary>
/// Exception thrown when geocoding operations fail
/// </summary>
public class GeocodingException : Exception
{
    public GeocodingException(string message) : base(message) { }
    public GeocodingException(string message, Exception innerException) : base(message, innerException) { }
}
