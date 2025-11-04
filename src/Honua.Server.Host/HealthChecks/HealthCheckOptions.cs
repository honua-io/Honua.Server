// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.ComponentModel.DataAnnotations;

namespace Honua.Server.Host.HealthChecks;

/// <summary>
/// Configuration options for health checks.
/// </summary>
public class HealthCheckOptions
{
    public const string SectionName = "HealthChecks";

    /// <summary>
    /// Timeout for individual health check execution.
    /// Default: 30 seconds.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:05:00")]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Period between health check executions.
    /// Default: 10 seconds.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:01:00")]
    public TimeSpan Period { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Enable detailed health check responses with diagnostic data.
    /// Default: true.
    /// </summary>
    public bool EnableDetailedErrors { get; set; } = true;

    /// <summary>
    /// Enable database health checks.
    /// Default: true.
    /// </summary>
    public bool EnableDatabaseCheck { get; set; } = true;

    /// <summary>
    /// Enable cache health checks.
    /// Default: true.
    /// </summary>
    public bool EnableCacheCheck { get; set; } = true;

    /// <summary>
    /// Enable storage health checks.
    /// Default: true.
    /// </summary>
    public bool EnableStorageCheck { get; set; } = true;

    /// <summary>
    /// Enable health checks UI dashboard.
    /// Default: false (enable in development/staging).
    /// </summary>
    public bool EnableUI { get; set; } = false;

    /// <summary>
    /// Health checks UI path (e.g., "/healthchecks-ui").
    /// Only used if EnableUI is true.
    /// </summary>
    public string UIPath { get; set; } = "/healthchecks-ui";
}
