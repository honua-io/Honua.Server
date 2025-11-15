// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Intake.BackgroundServices;

/// <summary>
/// Performance metrics and analytics for a build job.
/// </summary>
public sealed record BuildMetadata
{
    /// <summary>
    /// Total build execution time in seconds (from start to completion).
    /// Null if job hasn't completed.
    /// </summary>
    public double? BuildDurationSeconds { get; init; }

    /// <summary>
    /// Calculates throughput metric (builds per hour).
    /// </summary>
    /// <param name="successfulBuilds">Number of successful builds (default: 1).</param>
    /// <returns>Builds per hour, or null if duration is unavailable.</returns>
    public double? GetThroughput(int successfulBuilds = 1)
    {
        if (BuildDurationSeconds == null || BuildDurationSeconds == 0)
        {
            return null;
        }

        var hoursPerBuild = BuildDurationSeconds.Value / 3600.0;
        return 1.0 / hoursPerBuild * successfulBuilds;
    }
}
