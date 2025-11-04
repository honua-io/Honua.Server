// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel.DataAnnotations;

namespace Honua.Server.Core.Caching;

/// <summary>
/// Global cache size limit configuration to prevent memory exhaustion.
/// Applied to all IMemoryCache instances to prevent OutOfMemoryException.
/// </summary>
public sealed class CacheSizeLimitOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "honua:caching";

    /// <summary>
    /// Maximum total size for all memory caches in megabytes.
    /// Default: 100 MB to prevent memory exhaustion.
    /// Set to 0 for unlimited (not recommended in production).
    /// </summary>
    /// <remarks>
    /// This limit is shared across all IMemoryCache instances.
    /// Individual caches (WFS schemas, authorization, etc.) count against this limit.
    /// When limit is reached, LRU eviction occurs automatically.
    /// </remarks>
    [Range(0, 10_000)]
    public int MaxTotalSizeMB { get; set; } = 100;

    /// <summary>
    /// Maximum total entry count for all memory caches.
    /// Default: 10,000 entries to prevent unbounded growth.
    /// Set to 0 for unlimited (not recommended in production).
    /// </summary>
    /// <remarks>
    /// This is a global limit across all cache types.
    /// Protects against scenarios where many small entries consume memory.
    /// </remarks>
    [Range(0, 1_000_000)]
    public int MaxTotalEntries { get; set; } = 10_000;

    /// <summary>
    /// Enable automatic cache compaction when memory pressure is detected.
    /// Default: true.
    /// </summary>
    /// <remarks>
    /// When enabled, IMemoryCache automatically evicts entries under memory pressure.
    /// Recommended for production to prevent OOM under load.
    /// </remarks>
    public bool EnableAutoCompaction { get; set; } = true;

    /// <summary>
    /// Frequency of cache expiration scans in minutes.
    /// Default: 1 minute for responsive eviction.
    /// </summary>
    /// <remarks>
    /// Lower values = more responsive eviction but higher CPU overhead.
    /// Higher values = less CPU but slower eviction response.
    /// </remarks>
    [Range(0.5, 60)]
    public double ExpirationScanFrequencyMinutes { get; set; } = 1.0;

    /// <summary>
    /// Enable detailed cache metrics collection.
    /// Default: true for monitoring cache health.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Compaction percentage when size limit is reached (0.0-1.0).
    /// Default: 0.25 (compact 25% of entries).
    /// </summary>
    /// <remarks>
    /// Higher values = more aggressive eviction, more free space.
    /// Lower values = less aggressive, but may reach limit again quickly.
    /// </remarks>
    [Range(0.1, 0.5)]
    public double CompactionPercentage { get; set; } = 0.25;

    /// <summary>
    /// Gets the maximum total size in bytes.
    /// </summary>
    public long MaxTotalSizeBytes => MaxTotalSizeMB * 1024L * 1024L;

    /// <summary>
    /// Gets the expiration scan frequency as TimeSpan.
    /// </summary>
    public TimeSpan ExpirationScanFrequency => TimeSpan.FromMinutes(ExpirationScanFrequencyMinutes);

    /// <summary>
    /// Validates the configuration values.
    /// </summary>
    public void Validate()
    {
        if (MaxTotalSizeMB < 0)
        {
            throw new InvalidOperationException("MaxTotalSizeMB must be non-negative.");
        }

        if (MaxTotalEntries < 0)
        {
            throw new InvalidOperationException("MaxTotalEntries must be non-negative.");
        }

        if (ExpirationScanFrequencyMinutes <= 0)
        {
            throw new InvalidOperationException("ExpirationScanFrequencyMinutes must be positive.");
        }

        if (CompactionPercentage <= 0 || CompactionPercentage > 1.0)
        {
            throw new InvalidOperationException("CompactionPercentage must be between 0.1 and 0.5.");
        }
    }
}
