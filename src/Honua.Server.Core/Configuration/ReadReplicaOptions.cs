// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Configuration options for read replica routing.
/// Controls how read operations are distributed across read replicas.
/// </summary>
public sealed class ReadReplicaOptions
{
    /// <summary>
    /// Enable read replica routing. When disabled, all operations go to primary database.
    /// Default: false (opt-in feature).
    /// </summary>
    public bool EnableReadReplicaRouting { get; set; } = false;

    /// <summary>
    /// Operations to route to read replicas (e.g., "Features", "Observations", "Tiles").
    /// If empty, all read operations are eligible for replica routing.
    /// </summary>
    public List<string> ReadReplicaOperations { get; set; } = new();

    /// <summary>
    /// Fallback to primary database if all replicas are unavailable.
    /// Default: true (ensures high availability).
    /// </summary>
    public bool FallbackToPrimary { get; set; } = true;

    /// <summary>
    /// Health check interval in seconds for replica health monitoring.
    /// Default: 30 seconds.
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum consecutive failures before marking a replica as unhealthy.
    /// Default: 3 failures.
    /// </summary>
    public int MaxConsecutiveFailures { get; set; } = 3;

    /// <summary>
    /// Time in seconds to wait before retrying an unhealthy replica.
    /// Default: 60 seconds.
    /// </summary>
    public int UnhealthyRetryIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Global maximum replication lag in seconds. Replicas exceeding this lag are skipped.
    /// Can be overridden per data source. Default: null (no lag checking).
    /// </summary>
    public int? MaxReplicationLagSeconds { get; set; }

    /// <summary>
    /// Enable detailed logging for replica routing decisions.
    /// Default: false.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;
}
