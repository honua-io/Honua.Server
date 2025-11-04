// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Configuration options for cache invalidation resilience.
/// Controls retry behavior, consistency strategies, and health check parameters.
/// </summary>
public sealed class CacheInvalidationOptions
{
    /// <summary>
    /// Number of retry attempts for failed cache invalidations.
    /// Default: 3 attempts.
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Initial delay for retry attempts in milliseconds.
    /// Subsequent retries use exponential backoff: delay * 2^attempt.
    /// Default: 100ms (100ms, 200ms, 400ms for 3 retries).
    /// </summary>
    public int RetryDelayMs { get; set; } = 100;

    /// <summary>
    /// Maximum delay between retry attempts in milliseconds.
    /// Prevents exponential backoff from growing too large.
    /// Default: 5000ms (5 seconds).
    /// </summary>
    public int MaxRetryDelayMs { get; set; } = 5000;

    /// <summary>
    /// Strategy for handling cache invalidation failures.
    /// </summary>
    public CacheInvalidationStrategy Strategy { get; set; } = CacheInvalidationStrategy.Strict;

    /// <summary>
    /// Number of random entries to sample for cache consistency health checks.
    /// Default: 100 entries.
    /// </summary>
    public int HealthCheckSampleSize { get; set; } = 100;

    /// <summary>
    /// Maximum acceptable drift percentage for cache-database consistency.
    /// Health check reports degraded if drift exceeds this threshold.
    /// Default: 1.0% (1% of sampled entries can be inconsistent).
    /// </summary>
    public double MaxDriftPercentage { get; set; } = 1.0;

    /// <summary>
    /// TTL to use when cache invalidation fails and ShortTTL strategy is active.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan ShortTtl { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Enable detailed logging for cache invalidation operations.
    /// Logs each retry attempt and failure details.
    /// Default: true.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = true;

    /// <summary>
    /// Enable metrics collection for cache invalidation operations.
    /// Tracks success/failure rates, retry counts, and drift detection.
    /// Default: true.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Timeout for cache invalidation operations.
    /// Default: 10 seconds.
    /// </summary>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets the calculated retry delay for a specific attempt using exponential backoff.
    /// </summary>
    /// <param name="attemptNumber">The retry attempt number (1-based).</param>
    /// <returns>The delay duration for this attempt.</returns>
    public TimeSpan GetRetryDelay(int attemptNumber)
    {
        if (attemptNumber <= 0)
        {
            return TimeSpan.Zero;
        }

        // Exponential backoff: delay * 2^(attempt-1)
        var delayMs = RetryDelayMs * Math.Pow(2, attemptNumber - 1);
        var cappedDelayMs = Math.Min(delayMs, MaxRetryDelayMs);
        return TimeSpan.FromMilliseconds(cappedDelayMs);
    }
}

/// <summary>
/// Strategy for handling cache invalidation failures.
/// </summary>
public enum CacheInvalidationStrategy
{
    /// <summary>
    /// Strict consistency: Fail the write transaction if cache invalidation fails.
    /// Ensures cache-database consistency but may reduce availability.
    /// Use for critical data where stale cache is unacceptable.
    /// </summary>
    Strict,

    /// <summary>
    /// Eventual consistency: Queue failed invalidations for retry in background.
    /// Allows write to succeed even if cache invalidation fails.
    /// Cache may serve stale data temporarily until retry succeeds.
    /// Use for high availability scenarios where brief inconsistency is acceptable.
    /// </summary>
    Eventual,

    /// <summary>
    /// Short TTL: Set a very short TTL on entries that failed to invalidate.
    /// Reduces the window of stale data without failing the write.
    /// Use as a middle ground between Strict and Eventual.
    /// </summary>
    ShortTTL
}
