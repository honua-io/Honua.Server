// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Enterprise.Configuration;

/// <summary>
/// Configuration for trial tenant settings
/// </summary>
public class TrialConfiguration
{
    public const string SectionName = "Trial";

    /// <summary>
    /// Trial duration in days (default: 14 days)
    /// </summary>
    public int DurationDays { get; set; } = 14;

    /// <summary>
    /// Grace period after trial expires before cleanup (default: 7 days)
    /// </summary>
    public int GracePeriodDays { get; set; } = 7;

    /// <summary>
    /// Maximum number of active trial tenants (null = unlimited)
    /// </summary>
    public int? MaxActiveTrial { get; set; }

    /// <summary>
    /// Send email notification X days before expiration
    /// </summary>
    public int EmailReminderDays { get; set; } = 3;

    /// <summary>
    /// Allow trial extension (manual approval)
    /// </summary>
    public bool AllowExtension { get; set; } = true;

    /// <summary>
    /// Maximum trial extensions allowed
    /// </summary>
    public int MaxExtensions { get; set; } = 1;

    /// <summary>
    /// Extension duration in days
    /// </summary>
    public int ExtensionDurationDays { get; set; } = 7;

    /// <summary>
    /// Require credit card for trial signup
    /// </summary>
    public bool RequireCreditCard { get; set; } = false;

    /// <summary>
    /// Automatically convert to paid plan after trial
    /// </summary>
    public bool AutoConvertToPaid { get; set; } = false;

    /// <summary>
    /// Default paid tier after trial (if auto-convert enabled)
    /// </summary>
    public string DefaultPaidTier { get; set; } = "core";
}
