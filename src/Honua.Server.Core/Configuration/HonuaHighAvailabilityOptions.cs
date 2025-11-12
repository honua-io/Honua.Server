// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.ComponentModel.DataAnnotations;

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Configuration options for high availability support with Redis.
/// Enables distributed configuration change notifications across multiple server instances.
/// </summary>
public sealed class HonuaHighAvailabilityOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "HighAvailability";

    /// <summary>
    /// Gets or sets whether high availability mode is enabled.
    /// When true, uses Redis for distributed configuration change notifications.
    /// When false, uses local in-process notifications.
    /// Default: false.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the Redis connection string for distributed notifications.
    /// Required when Enabled is true.
    /// Example: "localhost:6379,abortConnect=false,connectTimeout=5000"
    /// </summary>
    public string? RedisConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the Redis Pub/Sub channel name for configuration change notifications.
    /// Default: "honua:config:changes"
    /// </summary>
    [Required]
    public string ConfigurationChannel { get; set; } = "honua:config:changes";

    /// <summary>
    /// Gets or sets the connection timeout for Redis operations in milliseconds.
    /// Default: 5000ms (5 seconds).
    /// </summary>
    [Range(1000, 60000)]
    public int ConnectionTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Gets or sets whether to automatically reconnect when Redis connection is lost.
    /// Default: true.
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable detailed logging for HA operations.
    /// Default: false.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;
}
