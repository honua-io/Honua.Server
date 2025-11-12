// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Enterprise.Licensing;
using Honua.Server.Enterprise.Licensing.Models;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Features;

/// <summary>
/// Service for checking feature flags based on license tier and permissions.
/// </summary>
public interface ILicenseFeatureFlagService
{
    /// <summary>
    /// Checks if a specific feature is enabled for the current license.
    /// </summary>
    Task<bool> IsFeatureEnabledAsync(string featureName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current license information including all feature flags.
    /// </summary>
    Task<LicenseFeatureFlags?> GetFeatureFlagsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the current license tier meets or exceeds the required tier.
    /// </summary>
    Task<bool> HasTierAccessAsync(LicenseTier requiredTier, CancellationToken cancellationToken = default);
}

/// <summary>
/// Simplified feature flag representation for UI consumption.
/// </summary>
public sealed class LicenseFeatureFlags
{
    /// <summary>Current license tier</summary>
    public LicenseTier Tier { get; set; }

    /// <summary>Whether the license is currently valid</summary>
    public bool IsValid { get; set; }

    /// <summary>Days until expiration (0 if expired)</summary>
    public int DaysUntilExpiration { get; set; }

    // Standard features
    public bool AdvancedAnalytics { get; set; }
    public bool CloudIntegrations { get; set; }
    public bool StacCatalog { get; set; }
    public bool RasterProcessing { get; set; }
    public bool VectorTiles { get; set; }
    public bool PrioritySupport { get; set; }

    // Enterprise features
    public bool GeoEtl { get; set; }
    public bool Versioning { get; set; }
    public bool OracleSupport { get; set; }
    public bool ElasticsearchSupport { get; set; }

    // Quota information
    public int MaxUsers { get; set; }
    public int MaxCollections { get; set; }
    public int MaxApiRequestsPerDay { get; set; }
    public int MaxStorageGb { get; set; }

    /// <summary>
    /// Creates feature flags from license information.
    /// </summary>
    public static LicenseFeatureFlags FromLicenseInfo(LicenseInfo license)
    {
        return new LicenseFeatureFlags
        {
            Tier = license.Tier,
            IsValid = license.IsValid(),
            DaysUntilExpiration = license.DaysUntilExpiration(),
            AdvancedAnalytics = license.Features.AdvancedAnalytics,
            CloudIntegrations = license.Features.CloudIntegrations,
            StacCatalog = license.Features.StacCatalog,
            RasterProcessing = license.Features.RasterProcessing,
            VectorTiles = license.Features.VectorTiles,
            PrioritySupport = license.Features.PrioritySupport,
            GeoEtl = license.Features.GeoEtl,
            Versioning = license.Features.Versioning,
            OracleSupport = license.Features.OracleSupport,
            ElasticsearchSupport = license.Features.ElasticsearchSupport,
            MaxUsers = license.Features.MaxUsers,
            MaxCollections = license.Features.MaxCollections,
            MaxApiRequestsPerDay = license.Features.MaxApiRequestsPerDay,
            MaxStorageGb = license.Features.MaxStorageGb
        };
    }

    /// <summary>
    /// Returns default (free tier) feature flags when no license is available.
    /// </summary>
    public static LicenseFeatureFlags GetDefault()
    {
        var freeFeatures = LicenseFeatures.GetDefaultForTier(LicenseTier.Free);
        return new LicenseFeatureFlags
        {
            Tier = LicenseTier.Free,
            IsValid = true,
            DaysUntilExpiration = int.MaxValue,
            AdvancedAnalytics = freeFeatures.AdvancedAnalytics,
            CloudIntegrations = freeFeatures.CloudIntegrations,
            StacCatalog = freeFeatures.StacCatalog,
            RasterProcessing = freeFeatures.RasterProcessing,
            VectorTiles = freeFeatures.VectorTiles,
            PrioritySupport = freeFeatures.PrioritySupport,
            GeoEtl = freeFeatures.GeoEtl,
            Versioning = freeFeatures.Versioning,
            OracleSupport = freeFeatures.OracleSupport,
            ElasticsearchSupport = freeFeatures.ElasticsearchSupport,
            MaxUsers = freeFeatures.MaxUsers,
            MaxCollections = freeFeatures.MaxCollections,
            MaxApiRequestsPerDay = freeFeatures.MaxApiRequestsPerDay,
            MaxStorageGb = freeFeatures.MaxStorageGb
        };
    }
}

/// <summary>
/// Implementation of license-based feature flag service.
/// </summary>
public sealed class LicenseFeatureFlagService : ILicenseFeatureFlagService
{
    private readonly ILicenseStore _licenseStore;
    private readonly ILogger<LicenseFeatureFlagService> _logger;
    private LicenseFeatureFlags? _cachedFlags;
    private DateTimeOffset _cacheExpiration;
    private readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public LicenseFeatureFlagService(
        ILicenseStore licenseStore,
        ILogger<LicenseFeatureFlagService> logger)
    {
        _licenseStore = licenseStore ?? throw new System.ArgumentNullException(nameof(licenseStore));
        _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
    }

    public async Task<bool> IsFeatureEnabledAsync(string featureName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(featureName))
        {
            return false;
        }

        var flags = await GetFeatureFlagsAsync(cancellationToken);
        if (flags == null)
        {
            return false;
        }

        // Map feature names to flags (case-insensitive)
        return featureName.ToLowerInvariant() switch
        {
            "advancedanalytics" or "advanced-analytics" => flags.AdvancedAnalytics,
            "cloudintegrations" or "cloud-integrations" => flags.CloudIntegrations,
            "staccatalog" or "stac-catalog" or "stac" => flags.StacCatalog,
            "rasterprocessing" or "raster-processing" or "raster" => flags.RasterProcessing,
            "vectortiles" or "vector-tiles" => flags.VectorTiles,
            "prioritysupport" or "priority-support" => flags.PrioritySupport,
            "geoetl" or "geo-etl" or "etl" => flags.GeoEtl,
            "versioning" or "branching" => flags.Versioning,
            "oracle" or "oraclesupport" or "oracle-support" => flags.OracleSupport,
            "elasticsearch" or "elasticsearchsupport" or "elasticsearch-support" => flags.ElasticsearchSupport,
            _ => false
        };
    }

    public async Task<LicenseFeatureFlags?> GetFeatureFlagsAsync(CancellationToken cancellationToken = default)
    {
        // Check cache first
        if (_cachedFlags != null && DateTimeOffset.UtcNow < _cacheExpiration)
        {
            return _cachedFlags;
        }

        // Acquire lock to prevent thundering herd
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check cache after acquiring lock
            if (_cachedFlags != null && DateTimeOffset.UtcNow < _cacheExpiration)
            {
                return _cachedFlags;
            }

            // Load license from store
            // Note: In a multi-tenant system, you'd pass customerId here
            // For now, we'll get the first active license (single-tenant assumption)
            var license = await _licenseStore.GetFirstActiveLicenseAsync(cancellationToken);

            if (license == null)
            {
                _logger.LogWarning("No active license found, using default (free tier) feature flags");
                _cachedFlags = LicenseFeatureFlags.GetDefault();
            }
            else if (!license.IsValid())
            {
                _logger.LogWarning(
                    "License {LicenseId} for customer {CustomerId} is invalid (status: {Status}, expired: {Expired})",
                    license.Id,
                    license.CustomerId,
                    license.Status,
                    license.IsExpired());
                _cachedFlags = LicenseFeatureFlags.GetDefault();
            }
            else
            {
                _cachedFlags = LicenseFeatureFlags.FromLicenseInfo(license);
                _logger.LogInformation(
                    "Loaded feature flags for license {LicenseId}, tier: {Tier}, expires in {Days} days",
                    license.Id,
                    license.Tier,
                    license.DaysUntilExpiration());
            }

            _cacheExpiration = DateTimeOffset.UtcNow.Add(_cacheLifetime);
            return _cachedFlags;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public async Task<bool> HasTierAccessAsync(LicenseTier requiredTier, CancellationToken cancellationToken = default)
    {
        var flags = await GetFeatureFlagsAsync(cancellationToken);
        if (flags == null || !flags.IsValid)
        {
            return false;
        }

        return flags.Tier >= requiredTier;
    }
}
