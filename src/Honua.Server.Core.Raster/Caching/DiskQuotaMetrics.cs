// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics.Metrics;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Caching;

/// <summary>
/// Interface for disk quota metrics.
/// </summary>
public interface IDiskQuotaMetrics
{
    /// <summary>
    /// Records a disk quota check operation.
    /// </summary>
    void RecordQuotaCheck(string path, bool hasSpace, double projectedUsage);

    /// <summary>
    /// Records disk usage statistics.
    /// </summary>
    void RecordDiskUsage(string path, long totalBytes, long freeBytes, long usedBytes, double usagePercent);

    /// <summary>
    /// Records a cleanup operation.
    /// </summary>
    void RecordCleanup(string path, int filesRemoved, long bytesFreed, TimeSpan duration);

    /// <summary>
    /// Records a quota error.
    /// </summary>
    void RecordQuotaError(string path, string operation);

    /// <summary>
    /// Records a pre-write check result.
    /// </summary>
    void RecordPreWriteCheck(string datasetId, bool allowed, string reason);
}

/// <summary>
/// OpenTelemetry metrics for disk quota monitoring and enforcement.
/// </summary>
public sealed class DiskQuotaMetrics : IDiskQuotaMetrics, IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _quotaChecks;
    private readonly Counter<long> _quotaRejections;
    private readonly Counter<long> _cleanupOperations;
    private readonly Counter<long> _filesRemoved;
    private readonly Counter<long> _bytesFreed;
    private readonly Counter<long> _quotaErrors;
    private readonly Histogram<double> _diskUsagePercent;
    private readonly Histogram<double> _cleanupDuration;
    private readonly ObservableGauge<long> _diskFreeSpace;
    private readonly ObservableGauge<double> _diskUsageGauge;

    // Cache for last known disk status to avoid excessive I/O in observable callbacks
    private long _lastKnownFreeBytes;
    private double _lastKnownUsagePercent;

    public DiskQuotaMetrics()
    {
        _meter = new Meter("Honua.Server.DiskQuota", "1.0.0");

        _quotaChecks = _meter.CreateCounter<long>(
            "honua.disk_quota.checks",
            unit: "{check}",
            description: "Number of disk quota checks performed");

        _quotaRejections = _meter.CreateCounter<long>(
            "honua.disk_quota.rejections",
            unit: "{rejection}",
            description: "Number of writes rejected due to insufficient disk space");

        _cleanupOperations = _meter.CreateCounter<long>(
            "honua.disk_quota.cleanups",
            unit: "{cleanup}",
            description: "Number of cleanup operations executed");

        _filesRemoved = _meter.CreateCounter<long>(
            "honua.disk_quota.files_removed",
            unit: "{file}",
            description: "Number of files removed during cleanup");

        _bytesFreed = _meter.CreateCounter<long>(
            "honua.disk_quota.bytes_freed",
            unit: "bytes",
            description: "Total bytes freed during cleanup operations");

        _quotaErrors = _meter.CreateCounter<long>(
            "honua.disk_quota.errors",
            unit: "{error}",
            description: "Number of disk quota operation errors");

        _diskUsagePercent = _meter.CreateHistogram<double>(
            "honua.disk_quota.usage_percent",
            unit: "%",
            description: "Disk usage percentage distribution");

        _cleanupDuration = _meter.CreateHistogram<double>(
            "honua.disk_quota.cleanup_duration",
            unit: "ms",
            description: "Duration of cleanup operations");

        _diskFreeSpace = _meter.CreateObservableGauge<long>(
            "honua.disk_quota.free_bytes",
            () => _lastKnownFreeBytes,
            unit: "bytes",
            description: "Available free disk space");

        _diskUsageGauge = _meter.CreateObservableGauge<double>(
            "honua.disk_quota.usage_current",
            () => _lastKnownUsagePercent,
            unit: "%",
            description: "Current disk usage percentage");
    }

    public void RecordQuotaCheck(string path, bool hasSpace, double projectedUsage)
    {
        _quotaChecks.Add(1,
            new("disk.path", NormalizePath(path)),
            new("has_space", hasSpace),
            new("usage.bucket", GetUsageBucket(projectedUsage)));

        if (!hasSpace)
        {
            _quotaRejections.Add(1,
                new("disk.path", NormalizePath(path)),
                new("reason", "insufficient_space"));
        }

        _diskUsagePercent.Record(projectedUsage * 100,
            new("disk.path", NormalizePath(path)),
            new("check.type", "pre_write"));
    }

    public void RecordDiskUsage(string path, long totalBytes, long freeBytes, long usedBytes, double usagePercent)
    {
        _lastKnownFreeBytes = freeBytes;
        _lastKnownUsagePercent = usagePercent * 100;

        _diskUsagePercent.Record(usagePercent * 100,
            new("disk.path", NormalizePath(path)),
            new("check.type", "status"),
            new("usage.bucket", GetUsageBucket(usagePercent)));
    }

    public void RecordCleanup(string path, int filesRemoved, long bytesFreed, TimeSpan duration)
    {
        _cleanupOperations.Add(1,
            new KeyValuePair<string, object?>("disk.path", NormalizePath(path)));

        _filesRemoved.Add(filesRemoved,
            new("disk.path", NormalizePath(path)),
            new("cleanup.scale", GetCleanupScale(filesRemoved)));

        _bytesFreed.Add(bytesFreed,
            new("disk.path", NormalizePath(path)),
            new("size.bucket", GetSizeBucket(bytesFreed)));

        _cleanupDuration.Record(duration.TotalMilliseconds,
            new("disk.path", NormalizePath(path)),
            new("duration.bucket", GetDurationBucket(duration)));
    }

    public void RecordQuotaError(string path, string operation)
    {
        _quotaErrors.Add(1,
            new("disk.path", NormalizePath(path)),
            new("operation", NormalizeOperation(operation)),
            new("error.type", "disk_quota_error"));
    }

    public void RecordPreWriteCheck(string datasetId, bool allowed, string reason)
    {
        _quotaChecks.Add(1,
            new("dataset.id", datasetId ?? "unknown"),
            new("allowed", allowed),
            new("reason", NormalizeReason(reason)));

        if (!allowed)
        {
            _quotaRejections.Add(1,
                new("dataset.id", datasetId ?? "unknown"),
                new("reason", NormalizeReason(reason)));
        }
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "unknown";

        // Extract just the drive or mount point to avoid high cardinality
        try
        {
            var root = System.IO.Path.GetPathRoot(path);
            return string.IsNullOrWhiteSpace(root) ? "unknown" : root;
        }
        catch
        {
            return "unknown";
        }
    }

    private static string NormalizeOperation(string? operation)
    {
        if (string.IsNullOrWhiteSpace(operation))
            return "unknown";

        return operation.ToLowerInvariant() switch
        {
            "check_space" or "check" => "check",
            "get_status" or "status" => "status",
            "cleanup" or "clean" => "cleanup",
            _ => operation.ToLowerInvariant()
        };
    }

    private static string NormalizeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return "unknown";

        return reason.ToLowerInvariant() switch
        {
            var r when r.Contains("quota") => "quota_exceeded",
            var r when r.Contains("space") => "insufficient_space",
            var r when r.Contains("threshold") => "threshold_exceeded",
            _ => reason.ToLowerInvariant()
        };
    }

    private static string GetUsageBucket(double usagePercent)
    {
        return usagePercent switch
        {
            < 0.5 => "low",         // < 50%
            < 0.7 => "normal",      // 50-70%
            < 0.8 => "elevated",    // 70-80%
            < 0.9 => "high",        // 80-90%
            < 0.95 => "critical",   // 90-95%
            _ => "emergency"        // >= 95%
        };
    }

    private static string GetSizeBucket(long bytes)
    {
        return bytes switch
        {
            < 1024L * 1024 => "tiny",                    // < 1 MB
            < 10L * 1024 * 1024 => "small",              // < 10 MB
            < 100L * 1024 * 1024 => "medium",            // < 100 MB
            < 1024L * 1024 * 1024 => "large",            // < 1 GB
            < 10L * 1024 * 1024 * 1024 => "very_large",  // < 10 GB
            _ => "huge"                                   // >= 10 GB
        };
    }

    private static string GetCleanupScale(int filesRemoved)
    {
        return filesRemoved switch
        {
            0 => "none",
            < 10 => "minimal",
            < 100 => "moderate",
            < 1000 => "significant",
            _ => "extensive"
        };
    }

    private static string GetDurationBucket(TimeSpan duration)
    {
        return duration.TotalMilliseconds switch
        {
            < 100 => "fast",
            < 1000 => "normal",
            < 5000 => "slow",
            _ => "very_slow"
        };
    }
}
