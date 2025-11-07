namespace Honua.MapSDK.Services.Geocoding;

/// <summary>
/// Represents a geocoding search result
/// </summary>
public class SearchResult
{
    /// <summary>
    /// Unique identifier for this result (from provider)
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name (formatted address)
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Latitude (WGS84)
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Longitude (WGS84)
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Bounding box [west, south, east, north] for fly-to animation
    /// </summary>
    public double[]? BoundingBox { get; set; }

    /// <summary>
    /// Type of result (city, street, building, POI, etc.)
    /// </summary>
    public string Type { get; set; } = "unknown";

    /// <summary>
    /// Category for icon display
    /// </summary>
    public SearchResultCategory Category { get; set; } = SearchResultCategory.Other;

    /// <summary>
    /// Relevance score (0.0 to 1.0)
    /// </summary>
    public double Relevance { get; set; } = 1.0;

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Street address
    /// </summary>
    public string? Street { get; set; }

    /// <summary>
    /// City name
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// State/province
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// Country name
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// Country code (ISO 3166-1 alpha-2)
    /// </summary>
    public string? CountryCode { get; set; }

    /// <summary>
    /// Postal code
    /// </summary>
    public string? PostalCode { get; set; }

    /// <summary>
    /// Provider-specific data
    /// </summary>
    public object? RawData { get; set; }
}

/// <summary>
/// Categories for search results
/// Used to determine icon display
/// </summary>
public enum SearchResultCategory
{
    Other,
    City,
    Town,
    Village,
    Street,
    Building,
    PointOfInterest,
    Park,
    Water,
    Mountain,
    Administrative,
    Transportation,
    Restaurant,
    Hotel,
    Shop
}
