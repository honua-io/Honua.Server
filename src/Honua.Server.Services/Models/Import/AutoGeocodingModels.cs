// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Services.Services.Import;

namespace Honua.Server.Services.Models.Import;

/// <summary>
/// Request to initiate automatic geocoding on uploaded data.
/// </summary>
public sealed record AutoGeocodingRequest
{
    /// <summary>
    /// Upload session ID or dataset ID
    /// </summary>
    public required string DatasetId { get; init; }

    /// <summary>
    /// Parsed data to geocode
    /// </summary>
    public required ParsedData ParsedData { get; init; }

    /// <summary>
    /// Address configuration (auto-detected or user-specified)
    /// </summary>
    public required AddressConfiguration AddressConfiguration { get; init; }

    /// <summary>
    /// Geocoding provider to use
    /// </summary>
    public string Provider { get; init; } = "nominatim";

    /// <summary>
    /// Whether to automatically apply results to dataset
    /// </summary>
    public bool AutoApply { get; init; } = true;

    /// <summary>
    /// Maximum concurrent geocoding requests
    /// </summary>
    public int MaxConcurrentRequests { get; init; } = 10;

    /// <summary>
    /// Whether to skip rows that already have geometry
    /// </summary>
    public bool SkipExistingGeometry { get; init; } = true;

    /// <summary>
    /// Minimum confidence threshold (0-1) to accept geocoding results
    /// </summary>
    public double MinConfidenceThreshold { get; init; } = 0.5;
}

/// <summary>
/// Response containing detected address columns for user confirmation.
/// </summary>
public sealed record AddressDetectionResponse
{
    /// <summary>
    /// Dataset or upload session ID
    /// </summary>
    public required string DatasetId { get; init; }

    /// <summary>
    /// All detected address column candidates
    /// </summary>
    public required List<AddressColumnCandidate> Candidates { get; init; }

    /// <summary>
    /// Suggested configuration (best match)
    /// </summary>
    public AddressConfiguration? SuggestedConfiguration { get; init; }

    /// <summary>
    /// Total number of rows in dataset
    /// </summary>
    public int TotalRows { get; init; }

    /// <summary>
    /// Number of rows with existing geometry
    /// </summary>
    public int RowsWithGeometry { get; init; }

    /// <summary>
    /// Number of rows that would be geocoded
    /// </summary>
    public int RowsToGeocode { get; init; }

    /// <summary>
    /// Sample addresses (first 5)
    /// </summary>
    public List<string> SampleAddresses { get; init; } = new();
}

/// <summary>
/// Result of automatic geocoding operation on upload.
/// </summary>
public sealed record AutoGeocodingResult
{
    /// <summary>
    /// Dataset ID
    /// </summary>
    public required string DatasetId { get; init; }

    /// <summary>
    /// Geocoding session ID
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Overall operation status
    /// </summary>
    public AutoGeocodingStatus Status { get; init; }

    /// <summary>
    /// Statistics for the operation
    /// </summary>
    public required AutoGeocodingStatistics Statistics { get; init; }

    /// <summary>
    /// Updated parsed data with geometry
    /// </summary>
    public ParsedData? UpdatedData { get; init; }

    /// <summary>
    /// Geocoded features (with geometry added)
    /// </summary>
    public List<GeocodedFeature>? GeocodedFeatures { get; init; }

    /// <summary>
    /// Start time
    /// </summary>
    public DateTime StartTime { get; init; }

    /// <summary>
    /// End time
    /// </summary>
    public DateTime? EndTime { get; init; }

    /// <summary>
    /// Total duration
    /// </summary>
    public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;

    /// <summary>
    /// Provider used
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Address configuration used
    /// </summary>
    public required AddressConfiguration AddressConfiguration { get; init; }

    /// <summary>
    /// Error message if operation failed
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Statistics for automatic geocoding operation.
/// </summary>
public sealed record AutoGeocodingStatistics
{
    /// <summary>
    /// Total rows in dataset
    /// </summary>
    public int TotalRows { get; init; }

    /// <summary>
    /// Rows processed
    /// </summary>
    public int ProcessedRows { get; init; }

    /// <summary>
    /// Rows successfully geocoded
    /// </summary>
    public int SuccessCount { get; init; }

    /// <summary>
    /// Rows that failed geocoding
    /// </summary>
    public int FailedCount { get; init; }

    /// <summary>
    /// Rows skipped (already had geometry)
    /// </summary>
    public int SkippedCount { get; init; }

    /// <summary>
    /// Rows with ambiguous results
    /// </summary>
    public int AmbiguousCount { get; init; }

    /// <summary>
    /// Average confidence score
    /// </summary>
    public double AverageConfidence { get; init; }

    /// <summary>
    /// Average time per address (ms)
    /// </summary>
    public double AverageTimeMs { get; init; }

    /// <summary>
    /// Success rate percentage (0-100)
    /// </summary>
    public double SuccessRate => ProcessedRows > 0
        ? (double)SuccessCount / ProcessedRows * 100
        : 0;

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public double ProgressPercentage => TotalRows > 0
        ? (double)ProcessedRows / TotalRows * 100
        : 0;
}

/// <summary>
/// Progress update for automatic geocoding.
/// </summary>
public sealed record AutoGeocodingProgress
{
    /// <summary>
    /// Dataset ID
    /// </summary>
    public required string DatasetId { get; init; }

    /// <summary>
    /// Session ID
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Current statistics
    /// </summary>
    public required AutoGeocodingStatistics Statistics { get; init; }

    /// <summary>
    /// Current address being processed
    /// </summary>
    public string? CurrentAddress { get; init; }

    /// <summary>
    /// Most recent geocoded feature
    /// </summary>
    public GeocodedFeature? LastGeocodedFeature { get; init; }

    /// <summary>
    /// Recent errors (last 10)
    /// </summary>
    public List<string> RecentErrors { get; init; } = new();

    /// <summary>
    /// Estimated time remaining
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; init; }

    /// <summary>
    /// Operation status
    /// </summary>
    public AutoGeocodingStatus Status { get; init; }
}

/// <summary>
/// A feature that has been geocoded with result information.
/// </summary>
public sealed record GeocodedFeature
{
    /// <summary>
    /// Original feature ID
    /// </summary>
    public required string FeatureId { get; init; }

    /// <summary>
    /// Row number in original dataset
    /// </summary>
    public int RowNumber { get; init; }

    /// <summary>
    /// Original address string
    /// </summary>
    public required string OriginalAddress { get; init; }

    /// <summary>
    /// Matched address from geocoder
    /// </summary>
    public string? MatchedAddress { get; init; }

    /// <summary>
    /// Geometry (GeoJSON format)
    /// </summary>
    public object? Geometry { get; init; }

    /// <summary>
    /// Latitude
    /// </summary>
    public double? Latitude { get; init; }

    /// <summary>
    /// Longitude
    /// </summary>
    public double? Longitude { get; init; }

    /// <summary>
    /// Confidence score (0-1)
    /// </summary>
    public double? Confidence { get; init; }

    /// <summary>
    /// Geocoding status
    /// </summary>
    public GeocodingFeatureStatus Status { get; init; }

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Original feature properties
    /// </summary>
    public Dictionary<string, object?> Properties { get; init; } = new();
}

/// <summary>
/// Status of automatic geocoding operation.
/// </summary>
public enum AutoGeocodingStatus
{
    /// <summary>
    /// Detection phase - analyzing columns
    /// </summary>
    Detecting,

    /// <summary>
    /// Waiting for user confirmation
    /// </summary>
    AwaitingConfirmation,

    /// <summary>
    /// Geocoding in progress
    /// </summary>
    InProgress,

    /// <summary>
    /// Completed successfully
    /// </summary>
    Completed,

    /// <summary>
    /// Completed with some failures
    /// </summary>
    CompletedWithErrors,

    /// <summary>
    /// Failed
    /// </summary>
    Failed,

    /// <summary>
    /// Cancelled by user
    /// </summary>
    Cancelled
}

/// <summary>
/// Status of a geocoded feature.
/// </summary>
public enum GeocodingFeatureStatus
{
    /// <summary>
    /// Successfully geocoded
    /// </summary>
    Success,

    /// <summary>
    /// Failed to geocode
    /// </summary>
    Failed,

    /// <summary>
    /// Skipped (already had geometry)
    /// </summary>
    Skipped,

    /// <summary>
    /// Ambiguous - multiple matches
    /// </summary>
    Ambiguous,

    /// <summary>
    /// Low confidence - below threshold
    /// </summary>
    LowConfidence,

    /// <summary>
    /// Pending - not yet processed
    /// </summary>
    Pending
}

/// <summary>
/// Options for automatic geocoding on upload.
/// </summary>
public sealed record AutoGeocodingOptions
{
    /// <summary>
    /// Whether automatic geocoding is enabled
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Whether to require user confirmation before geocoding
    /// </summary>
    public bool RequireConfirmation { get; init; } = true;

    /// <summary>
    /// Default geocoding provider
    /// </summary>
    public string DefaultProvider { get; init; } = "nominatim";

    /// <summary>
    /// Minimum confidence threshold
    /// </summary>
    public double MinConfidenceThreshold { get; init; } = 0.5;

    /// <summary>
    /// Maximum rows to geocode automatically (0 = unlimited)
    /// </summary>
    public int MaxRowsAutoGeocode { get; init; } = 1000;

    /// <summary>
    /// Whether to skip existing geometry
    /// </summary>
    public bool SkipExistingGeometry { get; init; } = true;

    /// <summary>
    /// Provider-specific configuration
    /// </summary>
    public Dictionary<string, string> ProviderConfig { get; init; } = new();
}

/// <summary>
/// Request to retry failed geocoding operations.
/// </summary>
public sealed record RetryGeocodingRequest
{
    /// <summary>
    /// Dataset ID
    /// </summary>
    public required string DatasetId { get; init; }

    /// <summary>
    /// Session ID from original geocoding operation
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Specific row numbers to retry (empty = all failed)
    /// </summary>
    public List<int> RowNumbers { get; init; } = new();

    /// <summary>
    /// Provider to use for retry (if different from original)
    /// </summary>
    public string? Provider { get; init; }

    /// <summary>
    /// Whether to use a different address configuration
    /// </summary>
    public AddressConfiguration? AlternativeConfiguration { get; init; }
}
