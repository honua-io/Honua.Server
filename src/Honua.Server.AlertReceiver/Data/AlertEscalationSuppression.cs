// <copyright file="AlertEscalationSuppression.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

namespace Honua.Server.AlertReceiver.Data;

/// <summary>
/// Defines a maintenance window where alert escalations should be suppressed.
/// </summary>
public sealed class AlertEscalationSuppression
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Alert name patterns to suppress (glob-style).
    /// If null or empty, suppresses all alerts.
    /// </summary>
    public List<string>? AppliesToPatterns { get; set; }

    /// <summary>
    /// Severity levels to suppress.
    /// If null or empty, suppresses all severities.
    /// </summary>
    public List<string>? AppliesToSeverities { get; set; }

    /// <summary>
    /// When the suppression window starts.
    /// </summary>
    public DateTimeOffset StartsAt { get; set; }

    /// <summary>
    /// When the suppression window ends.
    /// </summary>
    public DateTimeOffset EndsAt { get; set; }

    /// <summary>
    /// Whether this suppression is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Checks if this suppression applies to the given alert at the specified time.
    /// </summary>
    public bool AppliesTo(string alertName, string severity, DateTimeOffset now)
    {
        if (!this.IsActive)
        {
            return false;
        }

        if (now < this.StartsAt || now >= this.EndsAt)
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
