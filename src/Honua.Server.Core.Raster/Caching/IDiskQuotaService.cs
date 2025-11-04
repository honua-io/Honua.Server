// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Caching;

/// <summary>
/// Service for monitoring and enforcing disk space quotas for the file system cache.
/// Provides real-time disk space monitoring and automatic cleanup when quota is exceeded.
/// </summary>
public interface IDiskQuotaService
{
    /// <summary>
    /// Checks if there is sufficient disk space available to write data of the specified size.
    /// </summary>
    /// <param name="path">The path where the data will be written</param>
    /// <param name="sizeBytes">The size of data to be written in bytes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if there is sufficient space, false otherwise</returns>
    Task<bool> HasSufficientSpaceAsync(string path, long sizeBytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current disk space status for a given path.
    /// </summary>
    /// <param name="path">The path to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current disk space status</returns>
    Task<DiskSpaceStatus> GetDiskSpaceStatusAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to free up disk space by removing cached files according to the eviction policy.
    /// </summary>
    /// <param name="path">The cache directory path</param>
    /// <param name="targetFreeBytes">The target amount of free space in bytes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the cleanup operation</returns>
    Task<DiskCleanupResult> FreeUpSpaceAsync(string path, long targetFreeBytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets or sets the disk quota configuration.
    /// </summary>
    DiskQuotaOptions Options { get; }
}

/// <summary>
/// Configuration options for disk quota management.
/// </summary>
public sealed record DiskQuotaOptions
{
    /// <summary>
    /// Maximum percentage of disk space that can be used (0.0 to 1.0).
    /// Default is 0.8 (80%).
    /// </summary>
    public double MaxDiskUsagePercent { get; init; } = 0.8;

    /// <summary>
    /// Percentage threshold at which warnings should be logged (0.0 to 1.0).
    /// Default is 0.7 (70%).
    /// </summary>
    public double WarningThresholdPercent { get; init; } = 0.7;

    /// <summary>
    /// Minimum free space in bytes that must always be available.
    /// Default is 1 GB.
    /// </summary>
    public long MinimumFreeSpaceBytes { get; init; } = 1024L * 1024 * 1024;

    /// <summary>
    /// Whether to automatically cleanup when quota is exceeded.
    /// Default is true.
    /// </summary>
    public bool EnableAutomaticCleanup { get; init; } = true;

    /// <summary>
    /// Eviction policy to use when cleaning up.
    /// Default is LeastRecentlyUsed.
    /// </summary>
    public QuotaExpirationPolicy EvictionPolicy { get; init; } = QuotaExpirationPolicy.LeastRecentlyUsed;
}

/// <summary>
/// Represents the current disk space status.
/// </summary>
public sealed record DiskSpaceStatus(
    string Path,
    long TotalBytes,
    long FreeBytes,
    long UsedBytes,
    double UsagePercent,
    bool IsOverQuota,
    bool IsNearQuota);

/// <summary>
/// Result of a disk cleanup operation.
/// </summary>
public sealed record DiskCleanupResult(
    string Path,
    int FilesRemoved,
    long BytesFreed,
    TimeSpan Duration,
    bool TargetAchieved);
