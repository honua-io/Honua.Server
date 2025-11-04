// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel.DataAnnotations;

namespace Honua.Server.Host.VectorTiles;

/// <summary>
/// Configuration limits for vector tile preseed operations to prevent resource exhaustion.
/// </summary>
public sealed class VectorTilePreseedLimits
{
    /// <summary>
    /// Maximum number of tiles allowed per job. Default: 100,000 tiles.
    /// </summary>
    /// <remarks>
    /// At zoom 14, this covers approximately 0.24 square degrees (about 26km x 26km at the equator).
    /// Adjust based on available resources and expected job duration.
    /// </remarks>
    [Range(1, 10_000_000, ErrorMessage = "MaxTilesPerJob must be between 1 and 10,000,000")]
    public int MaxTilesPerJob { get; set; } = 100_000;

    /// <summary>
    /// Maximum number of concurrent jobs across all users. Default: 5 jobs.
    /// </summary>
    [Range(1, 100, ErrorMessage = "MaxConcurrentJobs must be between 1 and 100")]
    public int MaxConcurrentJobs { get; set; } = 5;

    /// <summary>
    /// Maximum number of active jobs per user/service combination. Default: 3 jobs.
    /// </summary>
    [Range(1, 50, ErrorMessage = "MaxJobsPerUser must be between 1 and 50")]
    public int MaxJobsPerUser { get; set; } = 3;

    /// <summary>
    /// Maximum duration a job can run before being automatically cancelled. Default: 24 hours.
    /// </summary>
    [CustomValidation(typeof(VectorTilePreseedLimits), nameof(ValidateJobTimeout))]
    public TimeSpan JobTimeout { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Maximum zoom level allowed for preseed operations. Default: 18.
    /// </summary>
    /// <remarks>
    /// Zoom levels above 18 generate exponentially more tiles. Use with caution.
    /// </remarks>
    [Range(0, 22, ErrorMessage = "MaxZoomLevel must be between 0 and 22")]
    public int MaxZoomLevel { get; set; } = 18;

    /// <summary>
    /// Minimum time between job submissions from the same user. Default: 10 seconds.
    /// </summary>
    [CustomValidation(typeof(VectorTilePreseedLimits), nameof(ValidateRateLimitWindow))]
    public TimeSpan RateLimitWindow { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Custom validation for JobTimeout.
    /// </summary>
    public static ValidationResult? ValidateJobTimeout(TimeSpan jobTimeout, ValidationContext context)
    {
        if (jobTimeout <= TimeSpan.Zero)
        {
            return new ValidationResult("JobTimeout must be greater than zero.");
        }

        if (jobTimeout > TimeSpan.FromDays(7))
        {
            return new ValidationResult("JobTimeout must not exceed 7 days.");
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Custom validation for RateLimitWindow.
    /// </summary>
    public static ValidationResult? ValidateRateLimitWindow(TimeSpan rateLimitWindow, ValidationContext context)
    {
        if (rateLimitWindow <= TimeSpan.Zero)
        {
            return new ValidationResult("RateLimitWindow must be greater than zero.");
        }

        if (rateLimitWindow > TimeSpan.FromMinutes(10))
        {
            return new ValidationResult("RateLimitWindow must not exceed 10 minutes.");
        }

        return ValidationResult.Success;
    }
}
