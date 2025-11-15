// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Metadata;

public sealed record DataSourceDefinition
{
    public required string Id { get; init; }
    public required string Provider { get; init; }
    public required string ConnectionString { get; init; }

    /// <summary>
    /// Indicates whether this data source is read-only (e.g., read replica).
    /// Read-only data sources are used for query operations but not for writes.
    /// </summary>
    public bool ReadOnly { get; init; } = false;

    /// <summary>
    /// Optional maximum replication lag in seconds for read replicas.
    /// If specified, this replica will be skipped if lag exceeds this threshold.
    /// </summary>
    public int? MaxReplicationLagSeconds { get; init; }

    /// <summary>
    /// Optional health check query to verify data source availability.
    /// Defaults to a provider-specific lightweight query if not specified.
    /// </summary>
    public string? HealthCheckQuery { get; init; }
}
