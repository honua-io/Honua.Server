// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Intake.BackgroundServices;

/// <summary>
/// Timestamps tracking the lifecycle of a build job.
/// </summary>
public sealed record BuildTimeline
{
    /// <summary>
    /// When the job was added to the queue.
    /// </summary>
    public required DateTimeOffset EnqueuedAt { get; init; }

    /// <summary>
    /// When the job started executing (null if pending).
    /// </summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>
    /// When the job completed (null if still running).
    /// </summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>
    /// Last update timestamp (tracking modifications).
    /// </summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Calculates the duration of the build (from start to completion).
    /// Returns null if job hasn't completed.
    /// </summary>
    public TimeSpan? GetDuration()
    {
        if (StartedAt == null || CompletedAt == null)
        {
            return null;
        }

        return CompletedAt.Value - StartedAt.Value;
    }

    /// <summary>
    /// Calculates the wait time before build started.
    /// Returns null if job hasn't started.
    /// </summary>
    public TimeSpan? GetWaitTime()
    {
        if (StartedAt == null)
        {
            return null;
        }

        return StartedAt.Value - EnqueuedAt;
    }
}
