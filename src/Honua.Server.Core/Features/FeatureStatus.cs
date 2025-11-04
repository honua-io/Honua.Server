// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Core.Features;

/// <summary>
/// Current status of a feature.
/// </summary>
public sealed class FeatureStatus
{
    /// <summary>
    /// Name of the feature.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Whether the feature is currently available.
    /// </summary>
    public required bool IsAvailable { get; init; }

    /// <summary>
    /// Whether the feature is currently degraded.
    /// </summary>
    public required bool IsDegraded { get; init; }

    /// <summary>
    /// Current health score (0-100).
    /// </summary>
    public required int HealthScore { get; init; }

    /// <summary>
    /// Current degradation state.
    /// </summary>
    public FeatureDegradationState State { get; init; } = FeatureDegradationState.Healthy;

    /// <summary>
    /// Active degradation type if degraded.
    /// </summary>
    public DegradationType? ActiveDegradation { get; init; }

    /// <summary>
    /// Reason for degradation if applicable.
    /// </summary>
    public string? DegradationReason { get; init; }

    /// <summary>
    /// When the feature entered the current state.
    /// </summary>
    public DateTimeOffset StateChangedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When the next recovery check will occur for degraded features.
    /// </summary>
    public DateTimeOffset? NextRecoveryCheck { get; init; }

    /// <summary>
    /// Additional metadata about the feature status.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Creates a healthy feature status.
    /// </summary>
    public static FeatureStatus Healthy(string name, int healthScore = 100)
    {
        return new FeatureStatus
        {
            Name = name,
            IsAvailable = true,
            IsDegraded = false,
            HealthScore = healthScore,
            State = FeatureDegradationState.Healthy
        };
    }

    /// <summary>
    /// Creates a degraded feature status.
    /// </summary>
    public static FeatureStatus Degraded(
        string name,
        int healthScore,
        DegradationType degradationType,
        string reason,
        DateTimeOffset? nextRecoveryCheck = null)
    {
        return new FeatureStatus
        {
            Name = name,
            IsAvailable = true,
            IsDegraded = true,
            HealthScore = healthScore,
            State = FeatureDegradationState.Degraded,
            ActiveDegradation = degradationType,
            DegradationReason = reason,
            NextRecoveryCheck = nextRecoveryCheck
        };
    }

    /// <summary>
    /// Creates a disabled feature status.
    /// </summary>
    public static FeatureStatus Disabled(string name, string reason)
    {
        return new FeatureStatus
        {
            Name = name,
            IsAvailable = false,
            IsDegraded = false,
            HealthScore = 0,
            State = FeatureDegradationState.Disabled,
            DegradationReason = reason
        };
    }

    /// <summary>
    /// Creates an unavailable feature status.
    /// </summary>
    public static FeatureStatus Unavailable(string name, string reason)
    {
        return new FeatureStatus
        {
            Name = name,
            IsAvailable = false,
            IsDegraded = false,
            HealthScore = 0,
            State = FeatureDegradationState.Unavailable,
            DegradationReason = reason
        };
    }
}

/// <summary>
/// Feature degradation states.
/// </summary>
public enum FeatureDegradationState
{
    /// <summary>
    /// Feature is fully operational.
    /// </summary>
    Healthy = 0,

    /// <summary>
    /// Feature is operating in degraded mode.
    /// </summary>
    Degraded = 1,

    /// <summary>
    /// Feature is disabled (by configuration).
    /// </summary>
    Disabled = 2,

    /// <summary>
    /// Feature is unavailable (failed health checks).
    /// </summary>
    Unavailable = 3,

    /// <summary>
    /// Feature is recovering from degradation.
    /// </summary>
    Recovering = 4
}
