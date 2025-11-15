// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data;

/// <summary>
/// Configuration options for data access operations.
/// </summary>
public sealed class DataAccessOptions
{
    /// <summary>
    /// Default command timeout for standard queries in seconds.
    /// Default: 30 seconds
    /// </summary>
    public int DefaultCommandTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Timeout for long-running analytical queries in seconds.
    /// Default: 300 seconds (5 minutes)
    /// </summary>
    public int LongRunningQueryTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Timeout for bulk operations (import, export, batch updates) in seconds.
    /// Default: 600 seconds (10 minutes)
    /// </summary>
    public int BulkOperationTimeoutSeconds { get; set; } = 600;

    /// <summary>
    /// Timeout for database transactions in seconds.
    /// Default: 120 seconds (2 minutes)
    /// </summary>
    public int TransactionTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Timeout for health check connectivity tests in seconds.
    /// Default: 5 seconds
    /// </summary>
    public int HealthCheckTimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Connection string encryption key identifier.
    /// If set, connection strings will be encrypted at rest.
    /// </summary>
    public string? EncryptionKeyId { get; set; }

    /// <summary>
    /// Enable connection pooling metrics collection.
    /// Default: true
    /// </summary>
    public bool EnablePoolingMetrics { get; set; } = true;

    /// <summary>
    /// SQL Server connection pool settings.
    /// </summary>
    public SqlServerPoolOptions SqlServer { get; set; } = new();

    /// <summary>
    /// PostgreSQL connection pool settings.
    /// </summary>
    public PostgresPoolOptions Postgres { get; set; } = new();

    /// <summary>
    /// MySQL connection pool settings.
    /// </summary>
    public MySqlPoolOptions MySql { get; set; } = new();

    /// <summary>
    /// SQLite connection settings.
    /// </summary>
    public SqlitePoolOptions Sqlite { get; set; } = new();

    /// <summary>
    /// Optimistic locking configuration for concurrent updates.
    /// </summary>
    public OptimisticLockingOptions OptimisticLocking { get; set; } = new();
}

/// <summary>
/// SQL Server connection pool configuration.
/// </summary>
public sealed class SqlServerPoolOptions
{
    /// <summary>
    /// Enable connection pooling.
    /// Default: true
    /// </summary>
    public bool Pooling { get; set; } = true;

    /// <summary>
    /// Minimum pool size.
    /// Default: 2
    /// </summary>
    public int MinPoolSize { get; set; } = 2;

    /// <summary>
    /// Maximum pool size.
    /// Default: 50
    /// </summary>
    public int MaxPoolSize { get; set; } = 50;

    /// <summary>
    /// Connection lifetime in seconds.
    /// Default: 600 (10 minutes)
    /// </summary>
    public int ConnectionLifetime { get; set; } = 600;

    /// <summary>
    /// Connection timeout in seconds.
    /// Default: 15
    /// </summary>
    public int ConnectTimeout { get; set; } = 15;

    /// <summary>
    /// Application name for connection tracking.
    /// Default: "Honua.Server"
    /// </summary>
    public string ApplicationName { get; set; } = "Honua.Server";
}

/// <summary>
/// PostgreSQL connection pool configuration.
/// </summary>
public sealed class PostgresPoolOptions
{
    /// <summary>
    /// Enable connection pooling.
    /// Default: true
    /// </summary>
    public bool Pooling { get; set; } = true;

    /// <summary>
    /// Minimum pool size.
    /// Default: 2
    /// </summary>
    public int MinPoolSize { get; set; } = 2;

    /// <summary>
    /// Maximum pool size.
    /// Default: 50
    /// </summary>
    public int MaxPoolSize { get; set; } = 50;

    /// <summary>
    /// Enable automatic pool size scaling based on CPU core count.
    /// When enabled, MaxPoolSize is calculated as: ProcessorCount * ScaleFactor
    /// Default: false (use explicit MaxPoolSize)
    /// </summary>
    public bool AutoScale { get; set; } = false;

    /// <summary>
    /// Connections per CPU core when auto-scaling is enabled.
    /// Typical values: 10-20 for web servers, 5-10 for background workers
    /// Default: 15
    /// </summary>
    public int ScaleFactor { get; set; } = 15;

    /// <summary>
    /// Connection lifetime in seconds.
    /// Default: 600 (10 minutes)
    /// </summary>
    public int ConnectionLifetime { get; set; } = 600;

    /// <summary>
    /// Connection timeout in seconds.
    /// Default: 15
    /// </summary>
    public int Timeout { get; set; } = 15;

    /// <summary>
    /// Application name for connection tracking.
    /// Default: "Honua.Server"
    /// </summary>
    public string ApplicationName { get; set; } = "Honua.Server";

    /// <summary>
    /// Get effective maximum pool size considering auto-scaling configuration.
    /// </summary>
    public int GetEffectiveMaxSize()
    {
        if (!AutoScale)
        {
            return MaxPoolSize;
        }

        var cpuCount = Environment.ProcessorCount;
        var scaledSize = cpuCount * ScaleFactor;

        // Ensure we stay within reasonable bounds
        return Math.Max(10, Math.Min(scaledSize, 500));
    }
}

/// <summary>
/// MySQL connection pool configuration.
/// </summary>
public sealed class MySqlPoolOptions
{
    /// <summary>
    /// Enable connection pooling.
    /// Default: true
    /// </summary>
    public bool Pooling { get; set; } = true;

    /// <summary>
    /// Minimum pool size.
    /// Default: 2
    /// </summary>
    public int MinimumPoolSize { get; set; } = 2;

    /// <summary>
    /// Maximum pool size.
    /// Default: 50
    /// </summary>
    public int MaximumPoolSize { get; set; } = 50;

    /// <summary>
    /// Connection lifetime in seconds.
    /// Default: 600 (10 minutes)
    /// </summary>
    public int ConnectionLifeTime { get; set; } = 600;

    /// <summary>
    /// Connection timeout in seconds.
    /// Default: 15
    /// </summary>
    public int ConnectionTimeout { get; set; } = 15;

    /// <summary>
    /// Application name for connection tracking.
    /// Default: "Honua.Server"
    /// </summary>
    public string ApplicationName { get; set; } = "Honua.Server";
}

/// <summary>
/// SQLite connection configuration.
/// </summary>
public sealed class SqlitePoolOptions
{
    /// <summary>
    /// Enable connection pooling.
    /// Default: true
    /// </summary>
    public bool Pooling { get; set; } = true;

    /// <summary>
    /// Enable Write-Ahead Logging mode for better concurrency.
    /// Default: true
    /// </summary>
    public bool EnableWalMode { get; set; } = true;

    /// <summary>
    /// Default timeout in seconds.
    /// Default: 30
    /// </summary>
    public int DefaultTimeout { get; set; } = 30;

    /// <summary>
    /// Cache mode (Shared, Private, Default).
    /// Default: Shared
    /// </summary>
    public string CacheMode { get; set; } = "Shared";
}

/// <summary>
/// Optimistic locking configuration for concurrent updates.
/// </summary>
public sealed class OptimisticLockingOptions
{
    /// <summary>
    /// Enable optimistic locking for feature updates.
    /// When enabled, updates will check version/timestamp and throw ConcurrencyException on conflicts.
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Strategy for handling missing version in update requests when optimistic locking is enabled.
    /// - Strict: Require version on all updates (throw exception if missing)
    /// - Lenient: Allow updates without version (skip concurrency check)
    /// Default: Lenient (for backward compatibility)
    /// </summary>
    public VersionRequirementMode VersionRequirement { get; set; } = VersionRequirementMode.Lenient;

    /// <summary>
    /// Version column name in database tables.
    /// Default: "row_version"
    /// </summary>
    public string VersionColumnName { get; set; } = "row_version";

    /// <summary>
    /// Include version information in API responses (as ETag header and in response body).
    /// Default: true
    /// </summary>
    public bool IncludeVersionInResponses { get; set; } = true;

    /// <summary>
    /// Maximum number of retry attempts for transient concurrency conflicts.
    /// Default: 0 (no automatic retries)
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 0;

    /// <summary>
    /// Base delay in milliseconds between retry attempts (uses exponential backoff).
    /// Default: 100ms
    /// </summary>
    public int RetryDelayMilliseconds { get; set; } = 100;
}

/// <summary>
/// Defines how strictly version information is required for updates.
/// </summary>
public enum VersionRequirementMode
{
    /// <summary>
    /// Version is optional on updates. If not provided, concurrency check is skipped.
    /// Recommended for backward compatibility with existing clients.
    /// </summary>
    Lenient,

    /// <summary>
    /// Version is required on all update operations. Updates without version will fail.
    /// Recommended for new applications requiring strong consistency guarantees.
    /// </summary>
    Strict
}
