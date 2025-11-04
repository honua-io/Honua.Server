// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Observability;
using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Caching;

/// <summary>
/// Implementation of disk quota service with real-time disk space monitoring.
/// </summary>
public sealed class DiskQuotaService : IDiskQuotaService
{
    private readonly ILogger<DiskQuotaService> _logger;
    private readonly IRasterTileCacheMetadataStore _metadataStore;
    private readonly IRasterTileCacheProvider _cacheProvider;
    private readonly IDiskQuotaMetrics? _metrics;

    public DiskQuotaService(
        ILogger<DiskQuotaService> logger,
        IRasterTileCacheMetadataStore metadataStore,
        IRasterTileCacheProvider cacheProvider,
        DiskQuotaOptions? options = null,
        IDiskQuotaMetrics? metrics = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metadataStore = metadataStore ?? throw new ArgumentNullException(nameof(metadataStore));
        _cacheProvider = cacheProvider ?? throw new ArgumentNullException(nameof(cacheProvider));
        _metrics = metrics;
        Options = options ?? new DiskQuotaOptions();

        _logger.LogInformation(
            "DiskQuotaService initialized: MaxUsage={MaxUsage:P0}, WarningThreshold={Warning:P0}, MinFreeSpace={MinFree} bytes",
            Options.MaxDiskUsagePercent, Options.WarningThresholdPercent, Options.MinimumFreeSpaceBytes);
    }

    public DiskQuotaOptions Options { get; }

    public Task<bool> HasSufficientSpaceAsync(string path, long sizeBytes, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(path);

        if (sizeBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "Size must be non-negative");
        }

        try
        {
            var driveInfo = GetDriveInfo(path);
            var availableSpace = driveInfo.AvailableFreeSpace;
            var totalSpace = driveInfo.TotalSize;

            // Calculate what the usage would be after writing
            var projectedFreeSpace = availableSpace - sizeBytes;
            var projectedUsage = (double)(totalSpace - projectedFreeSpace) / totalSpace;

            // Check against maximum usage threshold
            var wouldExceedQuota = projectedUsage > Options.MaxDiskUsagePercent;

            // Also check against minimum free space
            var wouldBelowMinimum = projectedFreeSpace < Options.MinimumFreeSpaceBytes;

            var hasSufficientSpace = !wouldExceedQuota && !wouldBelowMinimum;

            if (!hasSufficientSpace)
            {
                _logger.LogWarning(
                    "Insufficient disk space for write: path={Path}, requiredBytes={Required}, " +
                    "availableBytes={Available}, projectedUsage={ProjectedUsage:P1}, maxUsage={MaxUsage:P1}",
                    path, sizeBytes, availableSpace, projectedUsage, Options.MaxDiskUsagePercent);

                _metrics?.RecordQuotaCheck(path, false, projectedUsage);
            }
            else
            {
                _metrics?.RecordQuotaCheck(path, true, projectedUsage);
            }

            return Task.FromResult(hasSufficientSpace);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking disk space for path: {Path}", path);
            _metrics?.RecordQuotaError(path, "check_space");
            // In case of error, allow the write and let it fail naturally
            return Task.FromResult(true);
        }
    }

    public Task<DiskSpaceStatus> GetDiskSpaceStatusAsync(string path, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(path);

        try
        {
            var driveInfo = GetDriveInfo(path);
            var totalBytes = driveInfo.TotalSize;
            var freeBytes = driveInfo.AvailableFreeSpace;
            var usedBytes = totalBytes - freeBytes;
            var usagePercent = (double)usedBytes / totalBytes;

            var isOverQuota = usagePercent > Options.MaxDiskUsagePercent ||
                             freeBytes < Options.MinimumFreeSpaceBytes;

            var isNearQuota = usagePercent > Options.WarningThresholdPercent &&
                             usagePercent <= Options.MaxDiskUsagePercent;

            if (isNearQuota)
            {
                _logger.LogWarning(
                    "Disk usage approaching quota: path={Path}, usage={Usage:P1}, warningThreshold={Warning:P1}",
                    path, usagePercent, Options.WarningThresholdPercent);
            }

            if (isOverQuota)
            {
                _logger.LogWarning(
                    "Disk quota exceeded: path={Path}, usage={Usage:P1}, maxUsage={MaxUsage:P1}",
                    path, usagePercent, Options.MaxDiskUsagePercent);
            }

            _metrics?.RecordDiskUsage(path, totalBytes, freeBytes, usedBytes, usagePercent);

            var status = new DiskSpaceStatus(
                Path: path,
                TotalBytes: totalBytes,
                FreeBytes: freeBytes,
                UsedBytes: usedBytes,
                UsagePercent: usagePercent,
                IsOverQuota: isOverQuota,
                IsNearQuota: isNearQuota);

            return Task.FromResult(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting disk space status for path: {Path}", path);
            _metrics?.RecordQuotaError(path, "get_status");
            throw;
        }
    }

    public async Task<DiskCleanupResult> FreeUpSpaceAsync(
        string path,
        long targetFreeBytes,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(path);

        if (targetFreeBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetFreeBytes), "Target free bytes must be non-negative");
        }

        var (result, duration) = await PerformanceMeasurement.MeasureWithDurationAsync(async () =>
        {
            var filesRemoved = 0;
            long bytesFreed = 0;

            try
            {
                _logger.LogInformation(
                    "Starting disk cleanup: path={Path}, targetFreeBytes={Target}, policy={Policy}",
                    path, targetFreeBytes, Options.EvictionPolicy);

                // Get all files in the cache directory
                var cacheFiles = GetCacheFilesForEviction(path);

                _logger.LogDebug("Found {FileCount} cache files to potentially evict", cacheFiles.Count);

                foreach (var fileInfo in cacheFiles)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        // Check if we've met our target
                        var currentStatus = await GetDiskSpaceStatusAsync(path, cancellationToken);
                        if (currentStatus.FreeBytes >= targetFreeBytes)
                        {
                            _logger.LogInformation(
                                "Target free space achieved: current={Current} bytes, target={Target} bytes",
                                currentStatus.FreeBytes, targetFreeBytes);
                            break;
                        }

                        var fileSize = fileInfo.Length;
                        fileInfo.Delete();
                        filesRemoved++;
                        bytesFreed += fileSize;

                        _logger.LogDebug("Removed cache file: {File}, size={Size} bytes", fileInfo.FullName, fileSize);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete cache file: {File}", fileInfo.FullName);
                    }
                }

                var finalStatus = await GetDiskSpaceStatusAsync(path, cancellationToken);
                var targetAchieved = finalStatus.FreeBytes >= targetFreeBytes;

                return new DiskCleanupResult(
                    Path: path,
                    FilesRemoved: filesRemoved,
                    BytesFreed: bytesFreed,
                    Duration: TimeSpan.Zero,
                    TargetAchieved: targetAchieved);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disk cleanup for path: {Path}", path);
                _metrics?.RecordQuotaError(path, "cleanup");
                throw;
            }
        });

        _logger.LogInformation(
            "Disk cleanup completed: filesRemoved={FilesRemoved}, bytesFreed={BytesFreed}, " +
            "duration={Duration}ms, targetAchieved={TargetAchieved}",
            result.FilesRemoved, result.BytesFreed, duration.TotalMilliseconds, result.TargetAchieved);

        _metrics?.RecordCleanup(path, result.FilesRemoved, result.BytesFreed, duration);

        // Update the result with actual duration
        return new DiskCleanupResult(
            Path: path,
            FilesRemoved: result.FilesRemoved,
            BytesFreed: result.BytesFreed,
            Duration: duration,
            TargetAchieved: result.TargetAchieved);
    }

    private List<FileInfo> GetCacheFilesForEviction(string path)
    {
        if (!Directory.Exists(path))
        {
            return new List<FileInfo>();
        }

        var directory = new DirectoryInfo(path);
        var files = directory.GetFiles("*", SearchOption.AllDirectories).ToList();

        // Sort files according to eviction policy
        return Options.EvictionPolicy switch
        {
            QuotaExpirationPolicy.LeastRecentlyUsed => files.OrderBy(f => f.LastAccessTime).ToList(),
            QuotaExpirationPolicy.OldestFirst => files.OrderBy(f => f.CreationTime).ToList(),
            QuotaExpirationPolicy.LeastFrequentlyUsed => files.OrderBy(f => f.LastAccessTime).ToList(),
            _ => files.OrderBy(f => f.LastAccessTime).ToList()
        };
    }

    private static DriveInfo GetDriveInfo(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var driveName = Path.GetPathRoot(fullPath) ?? throw new InvalidOperationException($"Cannot determine drive for path: {path}");
        return new DriveInfo(driveName);
    }
}
