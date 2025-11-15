// <copyright file="AlertEscalationPolicy.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

namespace Honua.Server.AlertReceiver.Data;

/// <summary>
/// Defines a multi-level escalation policy for alerts.
/// </summary>
public sealed class AlertEscalationPolicy
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Alert name patterns this policy applies to (glob-style, e.g., "geoprocessing.*").
    /// If null or empty, applies to all alerts.
    /// </summary>
    public List<string>? AppliesToPatterns { get; set; }

    /// <summary>
    /// Severity levels this policy applies to (e.g., ["critical", "high"]).
    /// If null or empty, applies to all severities.
    /// </summary>
    public List<string>? AppliesToSeverities { get; set; }

    /// <summary>
    /// List of escalation levels with increasing urgency.
    /// </summary>
    public List<EscalationLevel> EscalationLevels { get; set; } = new();

    /// <summary>
    /// Whether alerts must be acknowledged to stop escalation.
    /// </summary>
    public bool RequiresAcknowledgment { get; set; } = true;

    /// <summary>
    /// Whether this policy is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Tenant ID for multi-tenant deployments.
    /// </summary>
    public string? TenantId { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Checks if this policy applies to the given alert.
    /// </summary>
    public bool AppliesTo(string alertName, string severity)
    {
        if (!this.IsActive)
        {
            return false;
        }

        // Check severity match
        if (this.AppliesToSeverities != null && this.AppliesToSeverities.Count > 0)
        {
            if (!this.AppliesToSeverities.Contains(severity, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Check name pattern match
        if (this.AppliesToPatterns != null && this.AppliesToPatterns.Count > 0)
        {
            var matches = this.AppliesToPatterns.Any(pattern =>
                MatchesPattern(alertName, pattern));

            if (!matches)
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesPattern(string value, string pattern)
    {
        // Simple glob-style pattern matching
        // Supports * (wildcard) and exact matches
        if (pattern == "*")
        {
            return true;
        }

        if (pattern.EndsWith(".*", StringComparison.Ordinal))
        {
            var prefix = pattern[..^2];
            return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        if (pattern.StartsWith("*.", StringComparison.Ordinal))
        {
            var suffix = pattern[2..];
            return value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        }

        return value.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Represents a single level in an escalation chain.
/// </summary>
public sealed class EscalationLevel
{
    /// <summary>
    /// Time to wait before escalating to this level.
    /// For level 0, this is typically zero (immediate).
    /// </summary>
    public TimeSpan Delay { get; set; }

    /// <summary>
    /// Notification channels to send alerts to at this level.
    /// Examples: "slack", "email", "pagerduty", "sms", "webhook".
    /// </summary>
    public List<string> NotificationChannels { get; set; } = new();

    /// <summary>
    /// Optional severity override to escalate severity at this level.
    /// Example: upgrade "warning" to "critical" at higher levels.
    /// </summary>
    public string? SeverityOverride { get; set; }

    /// <summary>
    /// Custom properties for this escalation level.
    /// Can be used for channel-specific configuration.
    /// </summary>
    public Dictionary<string, string>? CustomProperties { get; set; }

    /// <summary>
    /// Gets the effective severity for this level.
    /// </summary>
    public string GetEffectiveSeverity(string originalSeverity)
    {
        return this.SeverityOverride ?? originalSeverity;
    }
}
