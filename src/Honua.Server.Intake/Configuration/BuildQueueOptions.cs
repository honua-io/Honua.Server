// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel.DataAnnotations;

namespace Honua.Server.Intake.Configuration;

/// <summary>
/// Configuration options for the build queue processor.
/// </summary>
public sealed class BuildQueueOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "BuildQueue";

    /// <summary>
    /// Maximum number of concurrent builds to process.
    /// Default is 2 to balance resource usage.
    /// </summary>
    [Range(1, 10)]
    public int MaxConcurrentBuilds { get; init; } = 2;

    /// <summary>
    /// Build timeout in minutes.
    /// Default is 60 minutes.
    /// </summary>
    [Range(5, 180)]
    public int BuildTimeoutMinutes { get; init; } = 60;

    /// <summary>
    /// Maximum number of retry attempts for failed builds.
    /// Default is 3.
    /// </summary>
    [Range(0, 5)]
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>
    /// Interval between polling for new builds (in seconds).
    /// Default is 5 seconds.
    /// </summary>
    [Range(1, 60)]
    public int PollIntervalSeconds { get; init; } = 5;

    /// <summary>
    /// Delay before retrying a failed build (in minutes).
    /// Default is 5 minutes.
    /// </summary>
    [Range(1, 60)]
    public int RetryDelayMinutes { get; init; } = 5;

    /// <summary>
    /// Whether to use exponential backoff for retries.
    /// Default is true.
    /// </summary>
    public bool UseExponentialBackoff { get; init; } = true;

    /// <summary>
    /// Maximum age of completed builds to keep in the queue (in days).
    /// Older builds will be archived. Default is 30 days.
    /// </summary>
    [Range(1, 365)]
    public int CompletedBuildRetentionDays { get; init; } = 30;

    /// <summary>
    /// Whether to enable build notifications.
    /// Default is true.
    /// </summary>
    public bool EnableNotifications { get; init; } = true;

    /// <summary>
    /// Database connection string for the build queue.
    /// If not specified, uses the default connection string.
    /// </summary>
    public string? ConnectionString { get; init; }

    /// <summary>
    /// Whether to enable health checks for the build queue.
    /// Default is true.
    /// </summary>
    public bool EnableHealthChecks { get; init; } = true;

    /// <summary>
    /// Workspace directory for build operations.
    /// Default is /var/honua/builds.
    /// </summary>
    [Required]
    public string WorkspaceDirectory { get; init; } = "/var/honua/builds";

    /// <summary>
    /// Output directory for completed builds.
    /// Default is /var/honua/output.
    /// </summary>
    [Required]
    public string OutputDirectory { get; init; } = "/var/honua/output";

    /// <summary>
    /// Base URL for download links in notification emails.
    /// Example: https://builds.honua.io
    /// </summary>
    public string? DownloadBaseUrl { get; init; }

    /// <summary>
    /// Whether to automatically clean up build workspace after completion.
    /// Default is true.
    /// </summary>
    public bool CleanupWorkspaceAfterBuild { get; init; } = true;

    /// <summary>
    /// Whether to enable graceful shutdown (wait for in-progress builds).
    /// Default is true.
    /// </summary>
    public bool EnableGracefulShutdown { get; init; } = true;

    /// <summary>
    /// Maximum time to wait for builds to complete during shutdown (in seconds).
    /// Default is 300 seconds (5 minutes).
    /// </summary>
    [Range(30, 1800)]
    public int GracefulShutdownTimeoutSeconds { get; init; } = 300;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {
        if (MaxConcurrentBuilds < 1 || MaxConcurrentBuilds > 10)
        {
            throw new InvalidOperationException("MaxConcurrentBuilds must be between 1 and 10");
        }

        if (BuildTimeoutMinutes < 5 || BuildTimeoutMinutes > 180)
        {
            throw new InvalidOperationException("BuildTimeoutMinutes must be between 5 and 180");
        }

        if (string.IsNullOrWhiteSpace(WorkspaceDirectory))
        {
            throw new InvalidOperationException("WorkspaceDirectory is required");
        }

        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            throw new InvalidOperationException("OutputDirectory is required");
        }

        if (PollIntervalSeconds < 1 || PollIntervalSeconds > 60)
        {
            throw new InvalidOperationException("PollIntervalSeconds must be between 1 and 60");
        }
    }
}

/// <summary>
/// Retry policy options for build operations.
/// </summary>
public sealed class BuildRetryOptions
{
    /// <summary>
    /// Initial retry delay in milliseconds.
    /// </summary>
    public int InitialDelayMs { get; init; } = 1000;

    /// <summary>
    /// Maximum retry delay in milliseconds.
    /// </summary>
    public int MaxDelayMs { get; init; } = 30000;

    /// <summary>
    /// Backoff multiplier for exponential backoff.
    /// </summary>
    public double BackoffMultiplier { get; init; } = 2.0;
}
