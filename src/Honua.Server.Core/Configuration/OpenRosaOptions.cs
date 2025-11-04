// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Configuration options for OpenRosa/ODK integration.
/// </summary>
public sealed class OpenRosaOptions
{
    /// <summary>
    /// Whether OpenRosa API endpoints are enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Base URL for OpenRosa endpoints (e.g., "https://honua.example.com/openrosa").
    /// Used by ODK Collect for form downloads and submissions.
    /// </summary>
    [Required]
    [RegularExpression(@"^/[\w\-/]*$", ErrorMessage = "BaseUrl must start with '/' and contain only alphanumeric characters, hyphens, and forward slashes")]
    public string BaseUrl { get; set; } = "/openrosa";

    /// <summary>
    /// HTTP Digest authentication configuration.
    /// </summary>
    [Required]
    public DigestAuthOptions DigestAuth { get; set; } = new();

    /// <summary>
    /// Maximum submission size in MB (including attachments).
    /// </summary>
    [Range(1, 1000, ErrorMessage = "MaxSubmissionSizeMB must be between 1 and 1000")]
    public int MaxSubmissionSizeMB { get; set; } = 50;

    /// <summary>
    /// Allowed media types for attachments (e.g., ["image/jpeg", "image/png"]).
    /// </summary>
    public IReadOnlyList<string> AllowedMediaTypes { get; set; } = new[]
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "audio/mp3",
        "audio/aac",
        "video/mp4"
    };

    /// <summary>
    /// Number of days to retain staged submissions before archival.
    /// </summary>
    [Range(1, 3650, ErrorMessage = "StagingRetentionDays must be between 1 and 3650 (10 years)")]
    public int StagingRetentionDays { get; set; } = 90;

    /// <summary>
    /// Automatically archive rejected submissions.
    /// </summary>
    public bool AutoArchiveRejected { get; set; } = true;
}

/// <summary>
/// HTTP Digest authentication options (required by OpenRosa spec).
/// </summary>
public sealed class DigestAuthOptions
{
    /// <summary>
    /// Whether HTTP Digest authentication is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Authentication realm displayed to ODK Collect users.
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Realm must be between 1 and 100 characters")]
    public string Realm { get; set; } = "Honua OpenRosa";

    /// <summary>
    /// Nonce lifetime in minutes (for replay protection).
    /// </summary>
    [Range(1, 60, ErrorMessage = "NonceLifetimeMinutes must be between 1 and 60")]
    public int NonceLifetimeMinutes { get; set; } = 5;
}
