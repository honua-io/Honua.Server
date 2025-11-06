// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.LocationServices.Models;

namespace Honua.MapSDK.Models;

/// <summary>
/// Enhanced geocoding result for search UI with additional display metadata.
/// </summary>
public sealed record SearchResult
{
    /// <summary>
    /// The underlying geocoding result.
    /// </summary>
    public required GeocodingResult Result { get; init; }

    /// <summary>
    /// Relevance score (0-100) for ranking results.
    /// Computed from confidence, distance, and other factors.
    /// </summary>
    public double RelevanceScore { get; init; }

    /// <summary>
    /// Distance in meters from the map center or bias point.
    /// Null if no bias location was used.
    /// </summary>
    public double? DistanceFromCenter { get; init; }

    /// <summary>
    /// Display-friendly formatted address (may be truncated).
    /// </summary>
    public string DisplayAddress { get; init; } = string.Empty;

    /// <summary>
    /// Secondary text for display (e.g., city, state, country).
    /// </summary>
    public string? SecondaryText { get; init; }

    /// <summary>
    /// Icon identifier for the result type (e.g., "pin", "building", "city").
    /// </summary>
    public string Icon { get; init; } = "pin";

    /// <summary>
    /// Provider key that returned this result.
    /// </summary>
    public string? ProviderKey { get; init; }

    /// <summary>
    /// Timestamp when this result was retrieved.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a SearchResult from a GeocodingResult.
    /// </summary>
    public static SearchResult FromGeocodingResult(
        GeocodingResult result,
        string? providerKey = null,
        double[]? biasLocation = null)
    {
        var displayAddress = result.FormattedAddress;
        string? secondaryText = null;

        // Extract secondary text from components if available
        if (result.Components != null)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(result.Components.City))
                parts.Add(result.Components.City);
            if (!string.IsNullOrEmpty(result.Components.State))
                parts.Add(result.Components.State);
            if (!string.IsNullOrEmpty(result.Components.Country))
                parts.Add(result.Components.Country);

            if (parts.Count > 0)
            {
                secondaryText = string.Join(", ", parts);
            }
        }

        // Determine icon based on result type
        var icon = result.Type?.ToLowerInvariant() switch
        {
            "poi" or "point_of_interest" => "place",
            "address" => "home",
            "street" or "road" => "route",
            "city" or "municipality" => "location_city",
            "state" or "region" => "public",
            "country" => "flag",
            "postal_code" or "postcode" => "markunread_mailbox",
            _ => "place"
        };

        // Calculate distance from bias location if provided
        double? distanceFromCenter = null;
        if (biasLocation?.Length == 2)
        {
            distanceFromCenter = CalculateDistance(
                biasLocation[1], biasLocation[0],
                result.Latitude, result.Longitude);
        }

        // Calculate relevance score
        var relevanceScore = CalculateRelevanceScore(
            result.Confidence ?? 0.5,
            distanceFromCenter);

        return new SearchResult
        {
            Result = result,
            RelevanceScore = relevanceScore,
            DistanceFromCenter = distanceFromCenter,
            DisplayAddress = displayAddress,
            SecondaryText = secondaryText,
            Icon = icon,
            ProviderKey = providerKey
        };
    }

    /// <summary>
    /// Calculates relevance score from confidence and distance.
    /// </summary>
    private static double CalculateRelevanceScore(double confidence, double? distanceMeters)
    {
        // Base score from confidence (0-1 mapped to 0-70)
        var score = confidence * 70.0;

        // Distance penalty (up to -30 points for very far results)
        if (distanceMeters.HasValue)
        {
            // Normalize distance: 0m = no penalty, 10km = -15 points, 100km+ = -30 points
            var distanceKm = distanceMeters.Value / 1000.0;
            var distancePenalty = Math.Min(30.0, distanceKm / 3.33);
            score -= distancePenalty;
        }

        return Math.Max(0, Math.Min(100, score));
    }

    /// <summary>
    /// Calculates distance in meters between two lat/lon points using Haversine formula.
    /// </summary>
    private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMeters = 6371000.0;

        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return earthRadiusMeters * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    /// <summary>
    /// Formats distance for display.
    /// </summary>
    public string GetDistanceDisplay()
    {
        if (!DistanceFromCenter.HasValue)
            return string.Empty;

        var meters = DistanceFromCenter.Value;
        if (meters < 1000)
            return $"{meters:F0}m";

        if (meters < 10000)
            return $"{meters / 1000.0:F1}km";

        return $"{meters / 1000.0:F0}km";
    }
}
