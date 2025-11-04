// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Core.Deployment;

/// <summary>
/// Deployment policy configuration for an environment
/// Controls approval requirements, deployment windows, and rollback behavior
/// </summary>
public class DeploymentPolicy
{
    /// <summary>
    /// Whether deployments to this environment require approval
    /// </summary>
    public bool RequiresApproval { get; set; } = false;

    /// <summary>
    /// Maximum time to wait for approval before timing out
    /// </summary>
    public TimeSpan ApprovalTimeout { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Allowed days of week for deployments (empty = all days allowed)
    /// Example: ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"]
    /// </summary>
    public List<string> AllowedDays { get; set; } = new();

    /// <summary>
    /// Allowed time range for deployments (null = all times allowed)
    /// </summary>
    public TimeRange? AllowedHours { get; set; }

    /// <summary>
    /// Whether to automatically rollback failed deployments
    /// </summary>
    public bool AutoRollback { get; set; } = true;

    /// <summary>
    /// Minimum risk level that requires approval (even if RequiresApproval is false)
    /// </summary>
    public RiskLevel? MinimumRiskLevelForApproval { get; set; }
}

/// <summary>
/// Time range for deployment windows
/// </summary>
public class TimeRange
{
    /// <summary>
    /// Start time (24-hour format, e.g., "09:00")
    /// </summary>
    public string Start { get; set; } = "00:00";

    /// <summary>
    /// End time (24-hour format, e.g., "17:00")
    /// </summary>
    public string End { get; set; } = "23:59";

    /// <summary>
    /// Check if the current time is within this range
    /// </summary>
    public bool IsWithinRange(DateTime dateTime)
    {
        if (!TimeSpan.TryParse(Start, out var startTime) ||
            !TimeSpan.TryParse(End, out var endTime))
        {
            return true; // Invalid range, allow deployment
        }

        var currentTime = dateTime.TimeOfDay;

        // Handle ranges that span midnight (e.g., 22:00 to 02:00)
        if (endTime < startTime)
        {
            return currentTime >= startTime || currentTime <= endTime;
        }

        return currentTime >= startTime && currentTime <= endTime;
    }
}
