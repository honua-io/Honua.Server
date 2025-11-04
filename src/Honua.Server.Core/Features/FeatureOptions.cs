// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Core.Features;

/// <summary>
/// Configuration options for a feature with degradation support.
/// </summary>
public sealed class FeatureOptions
{
    /// <summary>
    /// Whether the feature is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether the feature is required for the application to function.
    /// If true, the application will not start if the feature fails health checks.
    /// If false, the feature will be disabled gracefully when unhealthy.
    /// </summary>
    public bool Required { get; set; } = false;

    /// <summary>
    /// Minimum health check score (0-100) required to keep the feature enabled.
    /// Below this threshold, the feature will be degraded.
    /// </summary>
    public int MinHealthScore { get; set; } = 50;

    /// <summary>
    /// Time in seconds before attempting to re-enable a degraded feature.
    /// </summary>
    public int RecoveryCheckInterval { get; set; } = 60;

    /// <summary>
    /// Optional degradation strategy to use when the feature is unhealthy.
    /// </summary>
    public DegradationStrategy? Strategy { get; set; }
}

/// <summary>
/// Defines how a feature should degrade when unhealthy.
/// </summary>
public sealed class DegradationStrategy
{
    /// <summary>
    /// Type of degradation to apply.
    /// </summary>
    public DegradationType Type { get; set; } = DegradationType.Disable;

    /// <summary>
    /// Fallback feature to use when this feature is degraded.
    /// </summary>
    public string? FallbackFeature { get; set; }

    /// <summary>
    /// Custom message to include in responses when degraded.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Whether to reduce quality instead of disabling entirely.
    /// </summary>
    public bool ReduceQuality { get; set; } = false;

    /// <summary>
    /// Quality reduction factor (0.0-1.0) when ReduceQuality is true.
    /// </summary>
    public double QualityReduction { get; set; } = 0.5;
}

/// <summary>
/// Types of feature degradation.
/// </summary>
public enum DegradationType
{
    /// <summary>
    /// Disable the feature entirely.
    /// </summary>
    Disable = 0,

    /// <summary>
    /// Reduce quality (e.g., lower resolution tiles, cached data).
    /// </summary>
    ReduceQuality = 1,

    /// <summary>
    /// Reduce functionality (e.g., disable advanced features, use simpler algorithms).
    /// </summary>
    ReduceFunctionality = 2,

    /// <summary>
    /// Reduce performance (e.g., more aggressive rate limiting, disable caching).
    /// </summary>
    ReducePerformance = 3,

    /// <summary>
    /// Use fallback implementation.
    /// </summary>
    Fallback = 4
}

/// <summary>
/// Feature flags configuration section.
/// </summary>
public sealed class FeatureFlagsOptions
{
    /// <summary>
    /// Configuration section name in appsettings.
    /// </summary>
    public const string SectionName = "Features";

    /// <summary>
    /// AI consultant features (chat, recommendations, deployments).
    /// </summary>
    public FeatureOptions AIConsultant { get; set; } = new();

    /// <summary>
    /// Advanced caching (Redis-based distributed caching).
    /// </summary>
    public FeatureOptions AdvancedCaching { get; set; } = new();

    /// <summary>
    /// Search and indexing capabilities.
    /// </summary>
    public FeatureOptions Search { get; set; } = new();

    /// <summary>
    /// Real-time metrics collection and export.
    /// </summary>
    public FeatureOptions RealTimeMetrics { get; set; } = new();

    /// <summary>
    /// STAC catalog features.
    /// </summary>
    public FeatureOptions StacCatalog { get; set; } = new();

    /// <summary>
    /// Advanced raster processing (COG, Zarr).
    /// </summary>
    public FeatureOptions AdvancedRasterProcessing { get; set; } = new();

    /// <summary>
    /// Vector tile generation and caching.
    /// </summary>
    public FeatureOptions VectorTiles { get; set; } = new();

    /// <summary>
    /// Analytics and usage tracking.
    /// </summary>
    public FeatureOptions Analytics { get; set; } = new();

    /// <summary>
    /// External storage providers (S3, Azure Blob, GCS).
    /// </summary>
    public FeatureOptions ExternalStorage { get; set; } = new();

    /// <summary>
    /// OpenID Connect authentication.
    /// </summary>
    public FeatureOptions OidcAuthentication { get; set; } = new();
}
