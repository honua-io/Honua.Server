// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Features;

/// <summary>
/// Adaptive service that provides feature-specific degradation logic.
/// Works in conjunction with FeatureManagementService to apply degradation strategies.
/// </summary>
public sealed class AdaptiveFeatureService
{
    private readonly IFeatureManagementService _featureManagement;
    private readonly ILogger<AdaptiveFeatureService> _logger;

    public AdaptiveFeatureService(
        IFeatureManagementService featureManagement,
        ILogger<AdaptiveFeatureService> logger)
    {
        _featureManagement = featureManagement ?? throw new ArgumentNullException(nameof(featureManagement));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Checks if advanced caching is available, falling back to in-memory if not.
    /// </summary>
    public async Task<CachingMode> GetCachingModeAsync(CancellationToken cancellationToken = default)
    {
        var isAvailable = await _featureManagement.IsFeatureAvailableAsync(
            "AdvancedCaching",
            cancellationToken);

        if (!isAvailable)
        {
            _logger.LogDebug("Advanced caching unavailable, using in-memory fallback");
            return CachingMode.InMemory;
        }

        var status = await _featureManagement.GetFeatureStatusAsync(
            "AdvancedCaching",
            cancellationToken);

        if (status.IsDegraded && status.ActiveDegradation == DegradationType.Fallback)
        {
            _logger.LogDebug("Advanced caching degraded, using in-memory fallback");
            return CachingMode.InMemory;
        }

        return CachingMode.Distributed;
    }

    /// <summary>
    /// Checks if AI features are available.
    /// </summary>
    public async Task<bool> IsAIAvailableAsync(CancellationToken cancellationToken = default)
    {
        var isAvailable = await _featureManagement.IsFeatureAvailableAsync(
            "AIConsultant",
            cancellationToken);

        if (!isAvailable)
        {
            _logger.LogDebug("AI features unavailable");
        }

        return isAvailable;
    }

    /// <summary>
    /// Gets the recommended tile resolution based on raster processing availability.
    /// </summary>
    public async Task<TileResolution> GetRecommendedTileResolutionAsync(
        TileResolution requested,
        CancellationToken cancellationToken = default)
    {
        var status = await _featureManagement.GetFeatureStatusAsync(
            "AdvancedRasterProcessing",
            cancellationToken);

        // If degraded with quality reduction, return lower resolution
        if (status.IsDegraded && status.ActiveDegradation == DegradationType.ReduceQuality)
        {
            var reducedResolution = requested switch
            {
                TileResolution.High => TileResolution.Medium,
                TileResolution.Medium => TileResolution.Low,
                TileResolution.Low => TileResolution.Low,
                _ => TileResolution.Medium
            };

            _logger.LogDebug(
                "Raster processing degraded, reducing tile resolution from {Requested} to {Actual}",
                requested,
                reducedResolution);

            return reducedResolution;
        }

        return requested;
    }

    /// <summary>
    /// Gets the appropriate search strategy based on search feature availability.
    /// </summary>
    public async Task<SearchStrategy> GetSearchStrategyAsync(CancellationToken cancellationToken = default)
    {
        var status = await _featureManagement.GetFeatureStatusAsync(
            "Search",
            cancellationToken);

        if (!status.IsAvailable)
        {
            _logger.LogDebug("Search indexing unavailable, using database fallback");
            return SearchStrategy.DatabaseScan;
        }

        if (status.IsDegraded && status.ActiveDegradation == DegradationType.ReduceFunctionality)
        {
            _logger.LogDebug("Search indexing degraded, using basic search");
            return SearchStrategy.BasicIndex;
        }

        return SearchStrategy.FullTextSearch;
    }

    /// <summary>
    /// Checks if STAC catalog is available, determining metadata strategy.
    /// </summary>
    public async Task<MetadataStrategy> GetMetadataStrategyAsync(CancellationToken cancellationToken = default)
    {
        var status = await _featureManagement.GetFeatureStatusAsync(
            "StacCatalog",
            cancellationToken);

        if (!status.IsAvailable)
        {
            _logger.LogDebug("STAC catalog unavailable, using basic metadata");
            return MetadataStrategy.BasicMetadata;
        }

        if (status.IsDegraded)
        {
            _logger.LogDebug("STAC catalog degraded, serving cached STAC items");
            return MetadataStrategy.CachedStac;
        }

        return MetadataStrategy.FullStac;
    }

    /// <summary>
    /// Checks if real-time metrics are available.
    /// </summary>
    public async Task<MetricsMode> GetMetricsModeAsync(CancellationToken cancellationToken = default)
    {
        var status = await _featureManagement.GetFeatureStatusAsync(
            "RealTimeMetrics",
            cancellationToken);

        if (!status.IsAvailable)
        {
            _logger.LogDebug("Real-time metrics unavailable, using log-only mode");
            return MetricsMode.LogOnly;
        }

        if (status.IsDegraded && status.ActiveDegradation == DegradationType.Fallback)
        {
            _logger.LogDebug("Real-time metrics degraded, using in-memory counters");
            return MetricsMode.InMemory;
        }

        return MetricsMode.RealTime;
    }

    /// <summary>
    /// Gets recommended rate limit multiplier based on system health.
    /// </summary>
    public async Task<double> GetRateLimitMultiplierAsync(CancellationToken cancellationToken = default)
    {
        var allStatuses = await _featureManagement.GetAllFeatureStatusesAsync(cancellationToken);

        var degradedCount = 0;
        var totalCount = 0;

        foreach (var status in allStatuses.Values)
        {
            if (status.State == FeatureDegradationState.Disabled)
            {
                continue; // Don't count disabled features
            }

            totalCount++;
            if (status.IsDegraded || !status.IsAvailable)
            {
                degradedCount++;
            }
        }

        if (totalCount == 0)
        {
            return 1.0;
        }

        var degradationRatio = (double)degradedCount / totalCount;

        // More aggressive rate limiting as more features degrade
        var multiplier = degradationRatio switch
        {
            >= 0.5 => 0.25, // 50%+ degraded: 75% rate limit reduction
            >= 0.3 => 0.5,  // 30%+ degraded: 50% rate limit reduction
            >= 0.1 => 0.75, // 10%+ degraded: 25% rate limit reduction
            _ => 1.0        // < 10% degraded: no change
        };

        if (multiplier < 1.0)
        {
            _logger.LogWarning(
                "Rate limiting increased due to system degradation. Multiplier: {Multiplier:F2}, Degraded: {DegradedCount}/{TotalCount}",
                multiplier,
                degradedCount,
                totalCount);
        }

        return multiplier;
    }
}

/// <summary>
/// Caching mode for the application.
/// </summary>
public enum CachingMode
{
    /// <summary>
    /// In-memory caching (per instance).
    /// </summary>
    InMemory,

    /// <summary>
    /// Distributed caching (Redis).
    /// </summary>
    Distributed
}

/// <summary>
/// Tile resolution levels.
/// </summary>
public enum TileResolution
{
    Low,
    Medium,
    High
}

/// <summary>
/// Search strategy.
/// </summary>
public enum SearchStrategy
{
    /// <summary>
    /// Full database scan (slowest, no index).
    /// </summary>
    DatabaseScan,

    /// <summary>
    /// Basic index search.
    /// </summary>
    BasicIndex,

    /// <summary>
    /// Full-text search with advanced features.
    /// </summary>
    FullTextSearch
}

/// <summary>
/// Metadata serving strategy.
/// </summary>
public enum MetadataStrategy
{
    /// <summary>
    /// Basic metadata only (no STAC).
    /// </summary>
    BasicMetadata,

    /// <summary>
    /// Cached STAC items (may be stale).
    /// </summary>
    CachedStac,

    /// <summary>
    /// Full STAC catalog with real-time updates.
    /// </summary>
    FullStac
}

/// <summary>
/// Metrics collection mode.
/// </summary>
public enum MetricsMode
{
    /// <summary>
    /// Log only (no metrics collection).
    /// </summary>
    LogOnly,

    /// <summary>
    /// In-memory counters (not exported).
    /// </summary>
    InMemory,

    /// <summary>
    /// Real-time metrics export.
    /// </summary>
    RealTime
}
