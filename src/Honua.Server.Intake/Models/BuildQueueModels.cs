// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Honua.Server.Intake.Models;

/// <summary>
/// Status of a build job in the queue.
/// </summary>
public enum BuildJobStatus
{
    /// <summary>Queued and waiting to be processed</summary>
    Pending,

    /// <summary>Currently being built</summary>
    Building,

    /// <summary>Build completed successfully</summary>
    Success,

    /// <summary>Build failed</summary>
    Failed,

    /// <summary>Build was cancelled</summary>
    Cancelled,

    /// <summary>Build timed out</summary>
    TimedOut
}

/// <summary>
/// Priority level for build jobs.
/// </summary>
public enum BuildPriority
{
    /// <summary>Low priority (batch/scheduled builds)</summary>
    Low = 0,

    /// <summary>Normal priority (standard customer builds)</summary>
    Normal = 1,

    /// <summary>High priority (urgent customer builds)</summary>
    High = 2,

    /// <summary>Critical priority (support/emergency builds)</summary>
    Critical = 3
}

/// <summary>
/// Represents a build job in the queue.
/// </summary>
public sealed class BuildJob
{
    /// <summary>
    /// Unique identifier for the build job.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Customer identifier who requested the build.
    /// </summary>
    public string CustomerId { get; init; } = string.Empty;

    /// <summary>
    /// Customer name for notifications.
    /// </summary>
    public string CustomerName { get; init; } = string.Empty;

    /// <summary>
    /// Customer email for notifications.
    /// </summary>
    public string CustomerEmail { get; init; } = string.Empty;

    /// <summary>
    /// Path to the build manifest file.
    /// </summary>
    public string ManifestPath { get; init; } = string.Empty;

    /// <summary>
    /// Build configuration name.
    /// </summary>
    public string ConfigurationName { get; init; } = string.Empty;

    /// <summary>
    /// License tier (starter, professional, enterprise).
    /// </summary>
    public string Tier { get; init; } = string.Empty;

    /// <summary>
    /// Target architecture (linux-x64, linux-arm64, etc).
    /// </summary>
    public string Architecture { get; init; } = string.Empty;

    /// <summary>
    /// Cloud provider (aws, azure, gcp, on-premises).
    /// </summary>
    public string CloudProvider { get; init; } = string.Empty;

    /// <summary>
    /// Current status of the build job.
    /// </summary>
    public BuildJobStatus Status { get; set; } = BuildJobStatus.Pending;

    /// <summary>
    /// Priority of the build job.
    /// </summary>
    public BuildPriority Priority { get; init; } = BuildPriority.Normal;

    /// <summary>
    /// Current build progress (0-100).
    /// </summary>
    public int ProgressPercent { get; set; }

    /// <summary>
    /// Current step description.
    /// </summary>
    public string? CurrentStep { get; set; }

    /// <summary>
    /// Output path where build artifacts are stored (when complete).
    /// </summary>
    public string? OutputPath { get; set; }

    /// <summary>
    /// Container image URL (when complete).
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Download URL for standalone binary (when complete).
    /// </summary>
    public string? DownloadUrl { get; set; }

    /// <summary>
    /// Error message if build failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of retry attempts made.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// When the build was enqueued.
    /// </summary>
    public DateTimeOffset EnqueuedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When the build started.
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// When the build completed (success or failure).
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Build duration in seconds (when complete).
    /// </summary>
    public double? BuildDurationSeconds { get; set; }

    /// <summary>
    /// Additional metadata about the build.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Progress information for an in-progress build.
/// </summary>
public sealed class BuildProgress
{
    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public int ProgressPercent { get; init; }

    /// <summary>
    /// Description of current step.
    /// </summary>
    public string CurrentStep { get; init; } = string.Empty;

    /// <summary>
    /// When the progress was reported.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Result of a completed build.
/// </summary>
public sealed class BuildResult
{
    /// <summary>
    /// Indicates if the build was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Output path where artifacts are stored.
    /// </summary>
    public string? OutputPath { get; init; }

    /// <summary>
    /// Container image URL.
    /// </summary>
    public string? ImageUrl { get; init; }

    /// <summary>
    /// Download URL for standalone binary.
    /// </summary>
    public string? DownloadUrl { get; init; }

    /// <summary>
    /// Error message if build failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Build duration.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Size of built artifacts in bytes.
    /// </summary>
    public long? ArtifactSize { get; init; }

    /// <summary>
    /// Deployment instructions for the customer.
    /// </summary>
    public string? DeploymentInstructions { get; init; }
}

/// <summary>
/// Statistics about the build queue.
/// </summary>
public sealed class QueueStatistics
{
    /// <summary>
    /// Number of pending builds.
    /// </summary>
    public int PendingCount { get; init; }

    /// <summary>
    /// Number of builds currently being processed.
    /// </summary>
    public int BuildingCount { get; init; }

    /// <summary>
    /// Total builds completed today.
    /// </summary>
    public int CompletedToday { get; init; }

    /// <summary>
    /// Total builds failed today.
    /// </summary>
    public int FailedToday { get; init; }

    /// <summary>
    /// Average build time in seconds (last 24 hours).
    /// </summary>
    public double AverageBuildTimeSeconds { get; init; }

    /// <summary>
    /// Success rate percentage (last 24 hours).
    /// </summary>
    public double SuccessRate { get; init; }

    /// <summary>
    /// Oldest pending build timestamp.
    /// </summary>
    public DateTimeOffset? OldestPendingBuild { get; init; }

    /// <summary>
    /// When the statistics were computed.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Notification information for a build job.
/// </summary>
public sealed class BuildNotification
{
    /// <summary>
    /// Notification type.
    /// </summary>
    public BuildNotificationType Type { get; init; }

    /// <summary>
    /// The build job associated with this notification.
    /// </summary>
    public BuildJob Job { get; init; } = new();

    /// <summary>
    /// Build result (for completed/failed notifications).
    /// </summary>
    public BuildResult? Result { get; init; }

    /// <summary>
    /// When the notification was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Type of build notification.
/// </summary>
public enum BuildNotificationType
{
    /// <summary>Build has been queued</summary>
    Queued,

    /// <summary>Build has started</summary>
    Started,

    /// <summary>Build completed successfully</summary>
    Completed,

    /// <summary>Build failed</summary>
    Failed
}
