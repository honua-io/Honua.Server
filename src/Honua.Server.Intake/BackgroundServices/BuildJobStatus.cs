// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Intake.BackgroundServices;

/// <summary>
/// Current status and position of a build job in the queue.
/// </summary>
public sealed record JobStatusInfo
{
    /// <summary>
    /// Current job status (e.g., "pending", "building", "completed", "failed").
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Queue priority (higher = earlier execution).
    /// Range: 0-100 (lower numbers = higher priority).
    /// </summary>
    public required int Priority { get; init; }

    /// <summary>
    /// Number of retry attempts for this job.
    /// Incremented when job fails and is retried.
    /// </summary>
    public required int RetryCount { get; init; }
}
