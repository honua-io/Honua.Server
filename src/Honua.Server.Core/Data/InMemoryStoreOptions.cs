// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel.DataAnnotations;

namespace Honua.Server.Core.Data;

/// <summary>
/// Configuration options for in-memory stores to prevent unbounded growth.
/// Applies to ProcessJobStore, CompletedProcessJobStore, and other InMemoryStoreBase implementations.
/// </summary>
public sealed class InMemoryStoreOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "honua:inmemory";

    /// <summary>
    /// Maximum number of active jobs in ProcessJobStore.
    /// Default: 1,000 to prevent memory exhaustion from long-running jobs.
    /// Set to 0 for unlimited (not recommended in production).
    /// </summary>
    [Range(0, 100_000)]
    public int MaxActiveJobs { get; set; } = 1_000;

    /// <summary>
    /// Maximum number of completed jobs retained in CompletedProcessJobStore.
    /// Default: 10,000 for recent job history.
    /// Older jobs are automatically evicted using LRU policy.
    /// </summary>
    [Range(0, 1_000_000)]
    public int MaxCompletedJobs { get; set; } = 10_000;

    /// <summary>
    /// Maximum age for completed jobs in hours before automatic cleanup.
    /// Default: 24 hours to prevent indefinite retention.
    /// </summary>
    [Range(1, 720)]
    public int MaxCompletedJobAgeHours { get; set; } = 24;

    /// <summary>
    /// Enable automatic cleanup of expired entries.
    /// Default: true.
    /// </summary>
    public bool EnableAutoCleanup { get; set; } = true;

    /// <summary>
    /// Cleanup interval in minutes.
    /// Default: 15 minutes for regular cleanup passes.
    /// </summary>
    [Range(1, 1440)]
    public int CleanupIntervalMinutes { get; set; } = 15;

    /// <summary>
    /// Warn when store reaches this percentage of max capacity (0-100).
    /// Default: 80% to provide early warning.
    /// </summary>
    [Range(50, 100)]
    public int CapacityWarningThresholdPercent { get; set; } = 80;

    /// <summary>
    /// Gets the maximum completed job age as TimeSpan.
    /// </summary>
    public TimeSpan MaxCompletedJobAge => TimeSpan.FromHours(MaxCompletedJobAgeHours);

    /// <summary>
    /// Gets the cleanup interval as TimeSpan.
    /// </summary>
    public TimeSpan CleanupInterval => TimeSpan.FromMinutes(CleanupIntervalMinutes);

    /// <summary>
    /// Validates the configuration values.
    /// </summary>
    public void Validate()
    {
        if (MaxActiveJobs < 0)
        {
            throw new InvalidOperationException("MaxActiveJobs must be non-negative.");
        }

        if (MaxCompletedJobs < 0)
        {
            throw new InvalidOperationException("MaxCompletedJobs must be non-negative.");
        }

        if (MaxCompletedJobAgeHours <= 0)
        {
            throw new InvalidOperationException("MaxCompletedJobAgeHours must be positive.");
        }

        if (CleanupIntervalMinutes <= 0)
        {
            throw new InvalidOperationException("CleanupIntervalMinutes must be positive.");
        }

        if (CapacityWarningThresholdPercent < 50 || CapacityWarningThresholdPercent > 100)
        {
            throw new InvalidOperationException("CapacityWarningThresholdPercent must be between 50 and 100.");
        }
    }
}
