// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Core.Data.SoftDelete;

/// <summary>
/// Configuration options for soft delete behavior across the application.
/// </summary>
public sealed class SoftDeleteOptions
{
    /// <summary>
    /// Gets or sets whether soft delete is enabled globally.
    /// When false, all delete operations perform hard deletes.
    /// Default: true (soft delete enabled)
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to automatically purge soft-deleted records after a retention period.
    /// Default: false (keep soft-deleted records indefinitely)
    /// </summary>
    public bool AutoPurgeEnabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the retention period for soft-deleted records before auto-purge.
    /// Only applicable when AutoPurgeEnabled is true.
    /// Default: 90 days
    /// </summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(90);

    /// <summary>
    /// Gets or sets whether to include soft-deleted records in queries by default.
    /// When false, queries filter out soft-deleted records unless explicitly included.
    /// Default: false (exclude soft-deleted records)
    /// </summary>
    public bool IncludeDeletedByDefault { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to create audit log entries for all delete operations.
    /// Default: true (audit all deletions)
    /// </summary>
    public bool AuditDeletions { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to create audit log entries for restore operations.
    /// Default: true (audit all restorations)
    /// </summary>
    public bool AuditRestorations { get; set; } = true;
}
