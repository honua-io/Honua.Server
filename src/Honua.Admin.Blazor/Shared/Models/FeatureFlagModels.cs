// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.Admin.Blazor.Shared.Models;

/// <summary>
/// License tier levels
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LicenseTier
{
    Free = 0,
    Professional = 1,
    Enterprise = 2
}

/// <summary>
/// Feature flag state for UI consumption
/// </summary>
public sealed class FeatureFlagState
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
    /// Checks if a specific feature is enabled (case-insensitive).
    /// </summary>
    public bool IsFeatureEnabled(string featureName)
    {
        if (string.IsNullOrWhiteSpace(featureName))
        {
            return false;
        }

        return featureName.ToLowerInvariant() switch
        {
            "advancedanalytics" or "advanced-analytics" => AdvancedAnalytics,
            "cloudintegrations" or "cloud-integrations" => CloudIntegrations,
            "staccatalog" or "stac-catalog" or "stac" => StacCatalog,
            "rasterprocessing" or "raster-processing" or "raster" => RasterProcessing,
            "vectortiles" or "vector-tiles" => VectorTiles,
            "prioritysupport" or "priority-support" => PrioritySupport,
            "geoetl" or "geo-etl" or "etl" => GeoEtl,
            "versioning" or "branching" => Versioning,
            "oracle" or "oraclesupport" or "oracle-support" => OracleSupport,
            "elasticsearch" or "elasticsearchsupport" or "elasticsearch-support" => ElasticsearchSupport,
            _ => false
        };
    }

    /// <summary>
    /// Returns default (free tier) feature flags.
    /// </summary>
    public static FeatureFlagState GetDefault()
    {
        return new FeatureFlagState
        {
            Tier = LicenseTier.Free,
            IsValid = true,
            DaysUntilExpiration = int.MaxValue,
            AdvancedAnalytics = false,
            CloudIntegrations = false,
            StacCatalog = false,
            RasterProcessing = false,
            VectorTiles = true,
            PrioritySupport = false,
            GeoEtl = false,
            Versioning = false,
            OracleSupport = false,
            ElasticsearchSupport = false,
            MaxUsers = 1,
            MaxCollections = 10,
            MaxApiRequestsPerDay = 10000,
            MaxStorageGb = 5
        };
    }
}
