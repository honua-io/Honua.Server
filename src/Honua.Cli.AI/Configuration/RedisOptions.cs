// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Cli.AI.Configuration;

/// <summary>
/// Configuration options for Redis connection.
/// </summary>
public class RedisOptions
{
    /// <summary>
    /// The Redis connection string.
    /// Format: "host:port,password=xxx,ssl=true,abortConnect=false"
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Whether Redis is enabled. If false, falls back to in-memory storage.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Key prefix for all process state keys in Redis.
    /// Default: "honua:process:"
    /// </summary>
    public string KeyPrefix { get; set; } = "honua:process:";

    /// <summary>
    /// Time-to-live for process entries in seconds.
    /// Default: 86400 (24 hours)
    /// </summary>
    public int TtlSeconds { get; set; } = 86400;

    /// <summary>
    /// Whether to validate the connection string on startup.
    /// </summary>
    public bool ValidateConnectionOnStartup { get; set; } = true;

    /// <summary>
    /// Connection timeout in milliseconds.
    /// </summary>
    public int ConnectTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Sync timeout in milliseconds.
    /// </summary>
    public int SyncTimeoutMs { get; set; } = 1000;
}
