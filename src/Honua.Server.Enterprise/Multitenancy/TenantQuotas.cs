// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Enterprise.Multitenancy;

/// <summary>
/// Defines resource quotas and limits for a tenant
/// </summary>
public class TenantQuotas
{
    /// <summary>
    /// Maximum storage in bytes (null = unlimited)
    /// </summary>
    public long? MaxStorageBytes { get; init; }

    /// <summary>
    /// Maximum number of datasets
    /// </summary>
    public int? MaxDatasets { get; init; }

    /// <summary>
    /// Maximum number of API requests per month
    /// </summary>
    public long? MaxApiRequestsPerMonth { get; init; }

    /// <summary>
    /// Maximum number of concurrent requests
    /// </summary>
    public int MaxConcurrentRequests { get; init; } = 10;

    /// <summary>
    /// Maximum raster processing time in minutes per month
    /// </summary>
    public int? MaxRasterProcessingMinutesPerMonth { get; init; }

    /// <summary>
    /// Maximum vector processing requests per month
    /// </summary>
    public long? MaxVectorProcessingRequestsPerMonth { get; init; }

    /// <summary>
    /// Maximum builds per month
    /// </summary>
    public int? MaxBuildsPerMonth { get; init; }

    /// <summary>
    /// Maximum export size in bytes
    /// </summary>
    public long? MaxExportSizeBytes { get; init; }

    /// <summary>
    /// Rate limit - requests per minute
    /// </summary>
    public int RateLimitPerMinute { get; init; } = 60;

    /// <summary>
    /// Creates default quotas for trial tier
    /// </summary>
    public static TenantQuotas Trial() => new()
    {
        MaxStorageBytes = 1024L * 1024 * 1024 * 5, // 5 GB
        MaxDatasets = 10,
        MaxApiRequestsPerMonth = 10_000,
        MaxConcurrentRequests = 5,
        MaxRasterProcessingMinutesPerMonth = 60,
        MaxVectorProcessingRequestsPerMonth = 1_000,
        MaxBuildsPerMonth = 10,
        MaxExportSizeBytes = 1024L * 1024 * 100, // 100 MB
        RateLimitPerMinute = 30
    };

    /// <summary>
    /// Creates default quotas for core tier
    /// </summary>
    public static TenantQuotas Core() => new()
    {
        MaxStorageBytes = 1024L * 1024 * 1024 * 50, // 50 GB
        MaxDatasets = 100,
        MaxApiRequestsPerMonth = 100_000,
        MaxConcurrentRequests = 10,
        MaxRasterProcessingMinutesPerMonth = 300,
        MaxVectorProcessingRequestsPerMonth = 10_000,
        MaxBuildsPerMonth = 100,
        MaxExportSizeBytes = 1024L * 1024 * 500, // 500 MB
        RateLimitPerMinute = 60
    };

    /// <summary>
    /// Creates default quotas for pro tier
    /// </summary>
    public static TenantQuotas Pro() => new()
    {
        MaxStorageBytes = 1024L * 1024 * 1024 * 500, // 500 GB
        MaxDatasets = 1000,
        MaxApiRequestsPerMonth = 1_000_000,
        MaxConcurrentRequests = 50,
        MaxRasterProcessingMinutesPerMonth = 3000,
        MaxVectorProcessingRequestsPerMonth = 100_000,
        MaxBuildsPerMonth = 1000,
        MaxExportSizeBytes = 1024L * 1024 * 1024 * 2, // 2 GB
        RateLimitPerMinute = 300
    };

    /// <summary>
    /// Creates unlimited quotas for enterprise tier
    /// </summary>
    public static TenantQuotas Enterprise() => new()
    {
        MaxStorageBytes = null, // Unlimited
        MaxDatasets = null,
        MaxApiRequestsPerMonth = null,
        MaxConcurrentRequests = 100,
        MaxRasterProcessingMinutesPerMonth = null,
        MaxVectorProcessingRequestsPerMonth = null,
        MaxBuildsPerMonth = null,
        MaxExportSizeBytes = null,
        RateLimitPerMinute = 1000
    };

    /// <summary>
    /// Creates quotas from tier name
    /// </summary>
    public static TenantQuotas FromTier(string? tier) => tier?.ToLowerInvariant() switch
    {
        "trial" => Trial(),
        "core" => Core(),
        "pro" => Pro(),
        "enterprise" => Enterprise(),
        "asp" => Enterprise(),
        _ => Trial()
    };
}
