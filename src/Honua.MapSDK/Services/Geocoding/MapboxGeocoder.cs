using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Honua.MapSDK.Services.Geocoding;

/// <summary>
/// Geocoding provider using Mapbox Geocoding API
/// Requires API key: https://docs.mapbox.com/api/search/geocoding/
/// </summary>
public class MapboxGeocoder : IGeocoder
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MapboxGeocoder>? _logger;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    /// <summary>
    /// Provider name
    /// </summary>
    public string ProviderName => "Mapbox";

    /// <summary>
    /// Creates a new Mapbox geocoder
    /// </summary>
    /// <param name="httpClient">HTTP client for API requests</param>
    /// <param name="apiKey">Mapbox API key (required)</param>
    /// <param name="logger">Optional logger</param>
    /// <param name="baseUrl">Optional custom Mapbox API URL</param>
    public MapboxGeocoder(
        HttpClient httpClient,
        string apiKey,
        ILogger<MapboxGeocoder>? logger = null,
        string baseUrl = "https://api.mapbox.com/geocoding/v5/mapbox.places")
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("Mapbox API key is required", nameof(apiKey));
        }

        _httpClient = httpClient;
        _apiKey = apiKey;
        _logger = logger;
        _baseUrl = baseUrl.TrimEnd('/');
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

        try
        {
            var url = $"{_baseUrl}/{Uri.EscapeDataString(query)}.json?access_token={_apiKey}&limit={limit}&types=country,region,postcode,district,place,locality,neighborhood,address,poi";

            _logger?.LogDebug("Mapbox search: {Query}", query);

            var response = await _httpClient.GetFromJsonAsync<MapboxResponse>(url, cancellationToken);

            if (response?.Features == null)
            {
                return new List<SearchResult>();
            }

            return response.Features.Select(MapToSearchResult).ToList();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error searching Mapbox for '{Query}'", query);
            throw new GeocodingException($"Failed to search for '{query}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Reverse geocode a coordinate to get address
    /// </summary>
    public async Task<SearchResult?> ReverseGeocodeAsync(double lat, double lon, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_baseUrl}/{lon},{lat}.json?access_token={_apiKey}&types=country,region,postcode,district,place,locality,neighborhood,address,poi";

            _logger?.LogDebug("Mapbox reverse: {Lat}, {Lon}", lat, lon);

            var response = await _httpClient.GetFromJsonAsync<MapboxResponse>(url, cancellationToken);

            if (response?.Features == null || response.Features.Length == 0)
            {
                return null;
            }

            return MapToSearchResult(response.Features[0]);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error reverse geocoding ({Lat}, {Lon})", lat, lon);
            throw new GeocodingException($"Failed to reverse geocode ({lat}, {lon}): {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Map Mapbox feature to SearchResult
    /// </summary>
    private SearchResult MapToSearchResult(MapboxFeature feature)
    {
        var coordinates = feature.Center ?? feature.Geometry?.Coordinates;
        var lon = coordinates?[0] ?? 0;
        var lat = coordinates?[1] ?? 0;

        var category = DetermineCategory(feature.PlaceType?.FirstOrDefault());

        // Extract address components from context
        string? city = null;
        string? state = null;
        string? country = null;
        string? countryCode = null;
        string? postalCode = null;

        if (feature.Context != null)
        {
            foreach (var context in feature.Context)
            {
                if (context.Id?.StartsWith("place.") == true)
                    city = context.Text;
                else if (context.Id?.StartsWith("region.") == true)
                    state = context.Text;
                else if (context.Id?.StartsWith("country.") == true)
                {
                    country = context.Text;
                    countryCode = context.ShortCode;
                }
                else if (context.Id?.StartsWith("postcode.") == true)
                    postalCode = context.Text;
            }
        }

        // If this IS the place/city, don't put it in the city field
        if (feature.PlaceType?.Contains("place") == true)
        {
            city = null;
        }

        return new SearchResult
        {
            Id = feature.Id ?? Guid.NewGuid().ToString(),
            DisplayName = feature.PlaceName ?? feature.Text ?? "Unknown",
            Latitude = lat,
            Longitude = lon,
            BoundingBox = feature.BoundingBox,
            Type = feature.PlaceType?.FirstOrDefault() ?? "unknown",
            Category = category,
            Relevance = feature.Relevance ?? 0.5,
            Street = feature.Address != null ? $"{feature.Address} {feature.Text}" : null,
            City = city,
            State = state,
            Country = country,
            CountryCode = countryCode?.ToUpperInvariant(),
            PostalCode = postalCode,
            RawData = feature,
            Metadata = new Dictionary<string, string>
            {
                { "mapbox_id", feature.Id ?? "" },
                { "place_type", string.Join(",", feature.PlaceType ?? Array.Empty<string>()) }
            }
        };
    }

    /// <summary>
    /// Determine search result category from Mapbox place type
    /// </summary>
    private SearchResultCategory DetermineCategory(string? placeType)
    {
        if (placeType == null) return SearchResultCategory.Other;

        return placeType.ToLowerInvariant() switch
        {
            "place" => SearchResultCategory.City,
            "locality" => SearchResultCategory.Town,
            "neighborhood" => SearchResultCategory.Town,
            "address" => SearchResultCategory.Street,
            "poi" => SearchResultCategory.PointOfInterest,
            "poi.landmark" => SearchResultCategory.PointOfInterest,
            "country" => SearchResultCategory.Administrative,
            "region" => SearchResultCategory.Administrative,
            "district" => SearchResultCategory.Administrative,
            _ => SearchResultCategory.Other
        };
    }

    #region Mapbox JSON Models

    private class MapboxResponse
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("query")]
        public object[]? Query { get; set; }

        [JsonPropertyName("features")]
        public MapboxFeature[]? Features { get; set; }
    }

    private class MapboxFeature
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("place_type")]
        public string[]? PlaceType { get; set; }

        [JsonPropertyName("relevance")]
        public double? Relevance { get; set; }

        [JsonPropertyName("properties")]
        public MapboxProperties? Properties { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("place_name")]
        public string? PlaceName { get; set; }

        [JsonPropertyName("center")]
        public double[]? Center { get; set; }

        [JsonPropertyName("geometry")]
        public MapboxGeometry? Geometry { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("context")]
        public MapboxContext[]? Context { get; set; }

        [JsonPropertyName("bbox")]
        public double[]? BoundingBox { get; set; }
    }

    private class MapboxGeometry
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("coordinates")]
        public double[]? Coordinates { get; set; }
    }

    private class MapboxProperties
    {
        [JsonPropertyName("accuracy")]
        public string? Accuracy { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }
    }

    private class MapboxContext
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("short_code")]
        public string? ShortCode { get; set; }
    }

    #endregion
}
