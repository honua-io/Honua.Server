// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel.DataAnnotations;

namespace Honua.Server.Core.Coordination;

/// <summary>
/// Configuration options for distributed leader election.
/// </summary>
public sealed class LeaderElectionOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "LeaderElection";

    /// <summary>
    /// Duration of the leadership lease before automatic expiry.
    /// </summary>
    /// <remarks>
    /// Default: 30 seconds.
    /// This is the maximum time a leader can hold the lock without renewal.
    /// If the leader crashes or becomes unresponsive, leadership will automatically
    /// expire after this duration, allowing another instance to take over.
    ///
    /// Recommended values:
    /// - Production: 30-60 seconds (balances failover speed with stability)
    /// - Development: 10-15 seconds (faster failover for testing)
    ///
    /// Must be greater than RenewalInterval to prevent premature expiry.
    /// Typical ratio: LeaseDuration = 3 × RenewalInterval
    /// </remarks>
    [Range(5, 300)]
    public int LeaseDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Interval at which the leader renews its lease.
    /// </summary>
    /// <remarks>
    /// Default: 10 seconds.
    /// The leader must renew before LeaseDuration expires.
    ///
    /// Recommended values:
    /// - Production: LeaseDuration / 3 (provides 2 retry opportunities)
    /// - High-load: LeaseDuration / 4 (more frequent renewals for stability)
    ///
    /// Lower values provide better availability but increase Redis load.
    /// Must be less than LeaseDuration to prevent leadership loss.
    /// </remarks>
    [Range(1, 100)]
    public int RenewalIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Resource name for leader election (identifies what is being coordinated).
    /// </summary>
    /// <remarks>
    /// Default: "honua-server"
    /// This identifies the resource being coordinated across instances.
    /// Multiple resources can have different leaders (e.g., "build-processor", "notification-sender").
    ///
    /// Examples:
    /// - "honua-server" - general server leadership
    /// - "build-queue-processor" - build processing coordination
    /// - "cleanup-job" - cleanup task coordination
    /// </remarks>
    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    public string ResourceName { get; set; } = "honua-server";

    /// <summary>
    /// Redis key prefix for leader election locks.
    /// </summary>
    /// <remarks>
    /// Default: "honua:leader:"
    /// All leader election keys in Redis will use this prefix.
    /// Helps organize keys and prevent collisions with other Redis usage.
    ///
    /// Full key format: {KeyPrefix}{ResourceName}
    /// Example: "honua:leader:build-queue-processor"
    /// </remarks>
    [Required]
    [MinLength(1)]
    public string KeyPrefix { get; set; } = "honua:leader:";

    /// <summary>
    /// Whether to enable detailed logging for leader election events.
    /// </summary>
    /// <remarks>
    /// Default: false (enabled in Development environment)
    /// When true, logs all leadership acquisition, renewal, and release events.
    /// Useful for debugging leader election issues in production.
    ///
    /// Logged events:
    /// - Leadership acquisition (Info)
    /// - Leadership renewal (Debug)
    /// - Leadership release (Info)
    /// - Leadership loss (Warning)
    /// - Redis errors (Error)
    /// </remarks>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if configuration is invalid.</exception>
    public void Validate()
    {
        if (LeaseDurationSeconds <= 0)
            throw new InvalidOperationException($"{nameof(LeaseDurationSeconds)} must be greater than 0");

        if (RenewalIntervalSeconds <= 0)
            throw new InvalidOperationException($"{nameof(RenewalIntervalSeconds)} must be greater than 0");

        if (RenewalIntervalSeconds >= LeaseDurationSeconds)
            throw new InvalidOperationException(
                $"{nameof(RenewalIntervalSeconds)} ({RenewalIntervalSeconds}s) must be less than " +
                $"{nameof(LeaseDurationSeconds)} ({LeaseDurationSeconds}s) to prevent leadership loss");

        if (RenewalIntervalSeconds > LeaseDurationSeconds / 2)
            throw new InvalidOperationException(
                $"{nameof(RenewalIntervalSeconds)} ({RenewalIntervalSeconds}s) should be at most half of " +
                $"{nameof(LeaseDurationSeconds)} ({LeaseDurationSeconds}s) for reliable operation. " +
                $"Recommended: {LeaseDurationSeconds / 3}s or less");

        if (string.IsNullOrWhiteSpace(ResourceName))
            throw new InvalidOperationException($"{nameof(ResourceName)} cannot be null or whitespace");

        if (string.IsNullOrWhiteSpace(KeyPrefix))
            throw new InvalidOperationException($"{nameof(KeyPrefix)} cannot be null or whitespace");
    }

    /// <summary>
    /// Gets the lease duration as a TimeSpan.
    /// </summary>
    public TimeSpan LeaseDuration => TimeSpan.FromSeconds(LeaseDurationSeconds);

    /// <summary>
    /// Gets the renewal interval as a TimeSpan.
    /// </summary>
    public TimeSpan RenewalInterval => TimeSpan.FromSeconds(RenewalIntervalSeconds);

    /// <summary>
    /// Gets the full Redis key for the configured resource.
    /// </summary>
    public string GetRedisKey() => $"{KeyPrefix}{ResourceName}";
}
