// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Honua.Server.Services.Models.BatchGeocoding;

/// <summary>
/// Request for batch geocoding operation.
/// </summary>
public sealed record BatchGeocodingRequest
{
    /// <summary>
    /// List of addresses to geocode.
    /// </summary>
    public required List<string> Addresses { get; init; }

    /// <summary>
    /// Geocoding provider to use (e.g., "nominatim", "azure-maps", "google-maps", "aws-location").
    /// </summary>
    public string Provider { get; init; } = "nominatim";

    /// <summary>
    /// Server URL for Honua.Server API.
    /// </summary>
    public required string ServerUrl { get; init; }

    /// <summary>
    /// Maximum concurrent requests (for rate limiting).
    /// </summary>
    public int MaxConcurrentRequests { get; init; } = 10;

    /// <summary>
    /// Maximum retry attempts for failed requests.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Timeout for each geocoding request in milliseconds.
    /// </summary>
    public int TimeoutMs { get; init; } = 10000;
}

/// <summary>
/// Options for batch geocoding configuration.
/// </summary>
public sealed record BatchGeocodingOptions
{
    /// <summary>
    /// Address column name in CSV (auto-detected if not specified).
    /// </summary>
    public string? AddressColumn { get; init; }

    /// <summary>
    /// Multiple columns to combine for address (e.g., ["Street", "City", "State"]).
    /// </summary>
    public List<string>? AddressColumns { get; init; }

    /// <summary>
    /// Separator for combining multiple address columns.
    /// </summary>
    public string AddressSeparator { get; init; } = ", ";

    /// <summary>
    /// Whether to skip rows with empty addresses.
    /// </summary>
    public bool SkipEmptyAddresses { get; init; } = true;

    /// <summary>
    /// Whether to include original row data in results.
    /// </summary>
    public bool IncludeOriginalData { get; init; } = true;
}

/// <summary>
/// Complete result of a batch geocoding operation.
/// </summary>
public sealed record BatchGeocodingResult
{
    /// <summary>
    /// Individual geocoding matches for each address.
    /// </summary>
    public required List<GeocodingMatch> Matches { get; init; }

    /// <summary>
    /// Statistics for the batch operation.
    /// </summary>
    public required BatchGeocodingStatistics Statistics { get; init; }

    /// <summary>
    /// Start time of the batch operation.
    /// </summary>
    public DateTime StartTime { get; init; }

    /// <summary>
    /// End time of the batch operation.
    /// </summary>
    public DateTime? EndTime { get; init; }

    /// <summary>
    /// Total duration of the batch operation.
    /// </summary>
    public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;

    /// <summary>
    /// Provider used for geocoding.
    /// </summary>
    public required string Provider { get; init; }
}

/// <summary>
/// A single geocoding match result.
/// </summary>
public sealed record GeocodingMatch
{
    /// <summary>
    /// Row index in the original CSV.
    /// </summary>
    public int RowIndex { get; init; }

    /// <summary>
    /// Original address query.
    /// </summary>
    public required string OriginalAddress { get; init; }

    /// <summary>
    /// Matched/formatted address from geocoding service.
    /// </summary>
    public string? MatchedAddress { get; init; }

    /// <summary>
    /// Latitude coordinate.
    /// </summary>
    public double? Latitude { get; init; }

    /// <summary>
    /// Longitude coordinate.
    /// </summary>
    public double? Longitude { get; init; }

    /// <summary>
    /// Match quality/confidence score.
    /// </summary>
    public MatchQuality Quality { get; init; }

    /// <summary>
    /// Geocoding status.
    /// </summary>
    public GeocodingStatus Status { get; init; }

    /// <summary>
    /// Confidence score from provider (0-1).
    /// </summary>
    public double? Confidence { get; init; }

    /// <summary>
    /// Result type (e.g., "address", "poi", "city").
    /// </summary>
    public string? ResultType { get; init; }

    /// <summary>
    /// Bounding box [west, south, east, north].
    /// </summary>
    public double[]? BoundingBox { get; init; }

    /// <summary>
    /// Number of alternative results found.
    /// </summary>
    public int AlternativesCount { get; init; }

    /// <summary>
    /// Error message if geocoding failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Number of retry attempts made.
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// Duration of geocoding request.
    /// </summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>
    /// Original row data from CSV (if IncludeOriginalData is true).
    /// </summary>
    public Dictionary<string, string>? OriginalData { get; init; }

    /// <summary>
    /// Whether this match has been manually edited.
    /// </summary>
    public bool IsEdited { get; set; }
}

/// <summary>
/// Match quality classification.
/// </summary>
public enum MatchQuality
{
    /// <summary>
    /// Exact match with high confidence (>0.9).
    /// </summary>
    Exact,

    /// <summary>
    /// High quality match (0.7-0.9).
    /// </summary>
    High,

    /// <summary>
    /// Medium quality match (0.5-0.7) or multiple results.
    /// </summary>
    Medium,

    /// <summary>
    /// Low quality match (&lt;0.5).
    /// </summary>
    Low,

    /// <summary>
    /// Geocoding failed or no results.
    /// </summary>
    Failed
}

/// <summary>
/// Geocoding operation status.
/// </summary>
public enum GeocodingStatus
{
    /// <summary>
    /// Successfully geocoded.
    /// </summary>
    Success,

    /// <summary>
    /// Multiple ambiguous results found.
    /// </summary>
    Ambiguous,

    /// <summary>
    /// No results found for address.
    /// </summary>
    NoResults,

    /// <summary>
    /// Geocoding request failed with error.
    /// </summary>
    Error,

    /// <summary>
    /// Request timed out.
    /// </summary>
    Timeout,

    /// <summary>
    /// Rate limit exceeded.
    /// </summary>
    RateLimited,

    /// <summary>
    /// Pending - not yet processed.
    /// </summary>
    Pending
}

/// <summary>
/// Statistics for batch geocoding operation.
/// </summary>
public sealed record BatchGeocodingStatistics
{
    /// <summary>
    /// Total number of addresses in batch.
    /// </summary>
    public int TotalAddresses { get; init; }

    /// <summary>
    /// Number of addresses processed so far.
    /// </summary>
    public int ProcessedCount { get; init; }

    /// <summary>
    /// Number of successful geocodes.
    /// </summary>
    public int SuccessCount { get; init; }

    /// <summary>
    /// Number of failed geocodes.
    /// </summary>
    public int FailedCount { get; init; }

    /// <summary>
    /// Number of ambiguous results (multiple matches).
    /// </summary>
    public int AmbiguousCount { get; init; }

    /// <summary>
    /// Average time per address in milliseconds.
    /// </summary>
    public double AverageTimeMs { get; init; }

    /// <summary>
    /// Estimated time remaining.
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; init; }

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public double ProgressPercentage => TotalAddresses > 0
        ? (double)ProcessedCount / TotalAddresses * 100
        : 0;

    /// <summary>
    /// Success rate percentage (0-100).
    /// </summary>
    public double SuccessRate => ProcessedCount > 0
        ? (double)SuccessCount / ProcessedCount * 100
        : 0;
}

/// <summary>
/// Progress update for batch geocoding operation.
/// </summary>
public sealed record BatchGeocodingProgress
{
    /// <summary>
    /// Current statistics.
    /// </summary>
    public required BatchGeocodingStatistics Statistics { get; init; }

    /// <summary>
    /// Current address being processed.
    /// </summary>
    public string? CurrentAddress { get; init; }

    /// <summary>
    /// Most recently completed match.
    /// </summary>
    public GeocodingMatch? LastMatch { get; init; }

    /// <summary>
    /// Recent errors (last 10).
    /// </summary>
    public List<string>? RecentErrors { get; init; }

    /// <summary>
    /// Whether the operation is paused.
    /// </summary>
    public bool IsPaused { get; init; }

    /// <summary>
    /// Whether the operation is cancelled.
    /// </summary>
    public bool IsCancelled { get; init; }
}

/// <summary>
/// CSV import configuration.
/// </summary>
public sealed record CsvImportConfiguration
{
    /// <summary>
    /// CSV delimiter character.
    /// </summary>
    public char Delimiter { get; init; } = ',';

    /// <summary>
    /// Whether CSV has header row.
    /// </summary>
    public bool HasHeader { get; init; } = true;

    /// <summary>
    /// Text encoding (e.g., "UTF-8", "ISO-8859-1").
    /// </summary>
    public string Encoding { get; init; } = "UTF-8";

    /// <summary>
    /// Quote character for CSV values.
    /// </summary>
    public char Quote { get; init; } = '"';

    /// <summary>
    /// Whether to skip empty lines.
    /// </summary>
    public bool SkipEmptyLines { get; init; } = true;

    /// <summary>
    /// Maximum number of rows to process (0 = unlimited).
    /// </summary>
    public int MaxRows { get; init; } = 0;
}

/// <summary>
/// CSV export configuration.
/// </summary>
public sealed record CsvExportConfiguration
{
    /// <summary>
    /// CSV delimiter character.
    /// </summary>
    public char Delimiter { get; init; } = ',';

    /// <summary>
    /// Whether to include original CSV columns.
    /// </summary>
    public bool IncludeOriginalColumns { get; init; } = true;

    /// <summary>
    /// Whether to include geocoding quality column.
    /// </summary>
    public bool IncludeQuality { get; init; } = true;

    /// <summary>
    /// Whether to include status column.
    /// </summary>
    public bool IncludeStatus { get; init; } = true;

    /// <summary>
    /// Whether to include confidence score.
    /// </summary>
    public bool IncludeConfidence { get; init; } = true;

    /// <summary>
    /// Whether to include matched address.
    /// </summary>
    public bool IncludeMatchedAddress { get; init; } = true;

    /// <summary>
    /// Text encoding (e.g., "UTF-8", "ISO-8859-1").
    /// </summary>
    public string Encoding { get; init; } = "UTF-8";
}

/// <summary>
/// Rate limiter configuration for geocoding providers.
/// </summary>
public sealed record RateLimiterConfiguration
{
    /// <summary>
    /// Provider key (e.g., "nominatim", "azure-maps").
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Maximum requests per time window.
    /// </summary>
    public int MaxRequests { get; init; }

    /// <summary>
    /// Time window for rate limiting.
    /// </summary>
    public TimeSpan TimeWindow { get; init; }

    /// <summary>
    /// Gets default rate limiter configuration for a provider.
    /// </summary>
    public static RateLimiterConfiguration GetDefault(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "nominatim" => new RateLimiterConfiguration
            {
                Provider = provider,
                MaxRequests = 1,
                TimeWindow = TimeSpan.FromSeconds(1)
            },
            "azure-maps" => new RateLimiterConfiguration
            {
                Provider = provider,
                MaxRequests = 50,
                TimeWindow = TimeSpan.FromSeconds(1)
            },
            "google-maps" => new RateLimiterConfiguration
            {
                Provider = provider,
                MaxRequests = 50,
                TimeWindow = TimeSpan.FromSeconds(1)
            },
            "aws-location" => new RateLimiterConfiguration
            {
                Provider = provider,
                MaxRequests = 50,
                TimeWindow = TimeSpan.FromSeconds(1)
            },
            _ => new RateLimiterConfiguration
            {
                Provider = provider,
                MaxRequests = 10,
                TimeWindow = TimeSpan.FromSeconds(1)
            }
        };
    }
}
