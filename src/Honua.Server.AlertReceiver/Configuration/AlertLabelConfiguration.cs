// <copyright file="AlertLabelConfiguration.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

namespace Honua.Server.AlertReceiver.Configuration;

/// <summary>
/// Configuration options for alert label validation and allowlist management.
/// </summary>
/// <remarks>
/// This configuration defines the set of known safe label keys that can be used in alerts.
/// Label keys not in this list will still be allowed but may be flagged for monitoring.
/// The allowlist approach helps identify unexpected or potentially malicious labels.
/// </remarks>
public sealed class AlertLabelConfiguration
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "AlertValidation:Labels";

    /// <summary>
    /// List of known safe label keys that are commonly used in the organization.
    /// Default: Standard monitoring and observability labels.
    /// </summary>
    /// <remarks>
    /// This list is used as an allowlist for label validation. Labels not in this list
    /// are still accepted but may be logged for security monitoring purposes.
    ///
    /// To customize this list for your organization:
    /// 1. Add your custom label keys to the appsettings.json file
    /// 2. Include labels from your monitoring tools (Prometheus, Grafana, etc.)
    /// 3. Include labels from your cloud providers (AWS, Azure, GCP)
    /// 4. Review and update regularly as new services are added
    ///
    /// All label keys must follow the validation rules:
    /// - Alphanumeric characters, underscore, hyphen, and dot only
    /// - Cannot start or end with dot or hyphen
    /// - Maximum 256 characters
    /// </remarks>
    public List<string> KnownSafeLabels { get; set; } = new()
    {
        // Severity and Priority
        "severity",
        "priority",

        // Environment and Infrastructure
        "environment",
        "service",
        "host",
        "hostname",
        "instance",
        "region",
        "zone",
        "cluster",
        "namespace",
        "pod",
        "container",
        "node",

        // Ownership and Team
        "team",
        "owner",

        // Application Information
        "component",
        "version",
        "release",
        "build",
        "commit",
        "branch",

        // Job and Task Management
        "job",
        "task",

        // Alert Metadata
        "alert_type",
        "alertname",

        // Metrics and Thresholds
        "metric",
        "threshold",
        "duration",

        // Source and Classification
        "source",
        "category",
        "subcategory",

        // Service Naming
        "application",
        "app",
        "service_name",

        // HTTP and API
        "endpoint",
        "method",
        "status_code",

        // Error Handling
        "error_type",
        "error_code",
    };

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    /// <returns>True if valid, false otherwise.</returns>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (this.KnownSafeLabels == null)
        {
            errors.Add($"{SectionName}:KnownSafeLabels cannot be null");
            return false;
        }

        if (this.KnownSafeLabels.Count == 0)
        {
            errors.Add($"{SectionName}:KnownSafeLabels should contain at least one label (defaults will be used if empty)");
        }

        // Validate each label key follows the required format
        var labelKeyPattern = new System.Text.RegularExpressions.Regex(
            @"^[a-zA-Z0-9_.-]+$",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        var seenLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var label in this.KnownSafeLabels)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                errors.Add($"{SectionName}:KnownSafeLabels contains empty or null values");
                continue;
            }

            if (label.Length > 256)
            {
                errors.Add($"{SectionName}:KnownSafeLabels contains label '{TruncateForDisplay(label, 50)}' that exceeds 256 character limit");
                continue;
            }

            if (!labelKeyPattern.IsMatch(label))
            {
                errors.Add($"{SectionName}:KnownSafeLabels contains label '{label}' with invalid characters. Only alphanumeric, underscore, hyphen, and dot are allowed");
                continue;
            }

            if (label.StartsWith('.') || label.StartsWith('-') || label.EndsWith('.') || label.EndsWith('-'))
            {
                errors.Add($"{SectionName}:KnownSafeLabels contains label '{label}' that starts or ends with dot or hyphen");
                continue;
            }

            // Check for duplicates (case-insensitive)
            if (!seenLabels.Add(label))
            {
                errors.Add($"{SectionName}:KnownSafeLabels contains duplicate label '{label}' (case-insensitive comparison)");
            }
        }

        return errors.Count == 0;
    }

    /// <summary>
    /// Truncates a string for display in error messages.
    /// </summary>
    private static string TruncateForDisplay(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value.Substring(0, maxLength) + "...";
    }
}
