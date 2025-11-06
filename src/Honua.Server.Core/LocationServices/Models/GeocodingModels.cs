// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Honua.Server.Core.LocationServices.Models;

/// <summary>
/// Request for geocoding an address to coordinates.
/// </summary>
public sealed record GeocodingRequest
{
    /// <summary>
    /// The address or query string to geocode.
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// Optional maximum number of results to return.
    /// </summary>
    public int? MaxResults { get; init; }

    /// <summary>
    /// Optional bounding box to constrain results [west, south, east, north].
    /// </summary>
    public double[]? BoundingBox { get; init; }

    /// <summary>
    /// Optional country codes to constrain results (ISO 3166-1 alpha-2).
    /// </summary>
    public string[]? CountryCodes { get; init; }

    /// <summary>
    /// Optional bias location [longitude, latitude] to prefer nearby results.
    /// </summary>
    public double[]? BiasLocation { get; init; }

    /// <summary>
    /// Optional language code for results (ISO 639-1).
    /// </summary>
    public string? Language { get; init; }
}

/// <summary>
/// Request for reverse geocoding coordinates to an address.
/// </summary>
public sealed record ReverseGeocodingRequest
{
    /// <summary>
    /// Longitude coordinate.
    /// </summary>
    public required double Longitude { get; init; }

    /// <summary>
    /// Latitude coordinate.
    /// </summary>
    public required double Latitude { get; init; }

    /// <summary>
    /// Optional language code for results (ISO 639-1).
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// Optional result types to include (e.g., "address", "poi", "street").
    /// </summary>
    public string[]? ResultTypes { get; init; }
}

/// <summary>
/// Response from a geocoding request containing one or more results.
/// </summary>
public sealed record GeocodingResponse
{
    /// <summary>
    /// List of geocoding results ordered by relevance.
    /// </summary>
    public required IReadOnlyList<GeocodingResult> Results { get; init; }

    /// <summary>
    /// Attribution text required by the provider.
    /// </summary>
    public string? Attribution { get; init; }
}

/// <summary>
/// A single geocoding result.
/// </summary>
public sealed record GeocodingResult
{
    /// <summary>
    /// The formatted address.
    /// </summary>
    public required string FormattedAddress { get; init; }

    /// <summary>
    /// Longitude coordinate.
    /// </summary>
    public required double Longitude { get; init; }

    /// <summary>
    /// Latitude coordinate.
    /// </summary>
    public required double Latitude { get; init; }

    /// <summary>
    /// Bounding box of the result [west, south, east, north].
    /// </summary>
    public double[]? BoundingBox { get; init; }

    /// <summary>
    /// Result type (e.g., "address", "poi", "street", "city").
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// Confidence score (0-1) indicating quality of the match.
    /// </summary>
    public double? Confidence { get; init; }

    /// <summary>
    /// Structured address components.
    /// </summary>
    public AddressComponents? Components { get; init; }

    /// <summary>
    /// Additional provider-specific metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Structured address components.
/// </summary>
public sealed record AddressComponents
{
    public string? HouseNumber { get; init; }
    public string? Street { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
    public string? CountryCode { get; init; }
    public string? County { get; init; }
    public string? District { get; init; }
}
