// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Core.Resilience;

/// <summary>
/// Configuration options for bulkhead isolation patterns to prevent resource exhaustion.
/// Bulkheads limit concurrent operations to specific resources (database, external APIs, per-tenant).
/// </summary>
public class BulkheadOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Resilience:Bulkhead";

    /// <summary>
    /// Maximum number of concurrent database operations.
    /// Default: 50 (typical connection pool size for production).
    /// </summary>
    public int DatabaseMaxParallelization { get; set; } = 50;

    /// <summary>
    /// Maximum number of database operations that can be queued when all slots are occupied.
    /// Default: 100 (allows burst handling without blocking indefinitely).
    /// </summary>
    public int DatabaseMaxQueuedActions { get; set; } = 100;

    /// <summary>
    /// Maximum number of concurrent external API calls.
    /// Default: 10 (conservative to avoid overwhelming external services).
    /// </summary>
    public int ExternalApiMaxParallelization { get; set; } = 10;

    /// <summary>
    /// Maximum number of external API calls that can be queued.
    /// Default: 20 (moderate queue to handle bursts without excessive memory).
    /// </summary>
    public int ExternalApiMaxQueuedActions { get; set; } = 20;

    /// <summary>
    /// Maximum number of concurrent operations per tenant.
    /// Default: 10 (prevents noisy neighbor problems in multi-tenant scenarios).
    /// </summary>
    public int PerTenantMaxParallelization { get; set; } = 10;

    /// <summary>
    /// Maximum number of operations per tenant that can be queued.
    /// Default: 20 (allows moderate bursting per tenant).
    /// </summary>
    public int PerTenantMaxQueuedActions { get; set; } = 20;

    /// <summary>
    /// Memory threshold in bytes. Operations are rejected when memory exceeds this limit.
    /// Default: 1GB (1,073,741,824 bytes) - protects against OOM conditions.
    /// </summary>
    public long MemoryThresholdBytes { get; set; } = 1_073_741_824; // 1GB

    /// <summary>
    /// Enables or disables the database bulkhead policy.
    /// Default: true.
    /// </summary>
    public bool DatabaseEnabled { get; set; } = true;

    /// <summary>
    /// Enables or disables the external API bulkhead policy.
    /// Default: true.
    /// </summary>
    public bool ExternalApiEnabled { get; set; } = true;

    /// <summary>
    /// Enables or disables per-tenant resource limiting.
    /// Default: true.
    /// </summary>
    public bool PerTenantEnabled { get; set; } = true;

    /// <summary>
    /// Enables or disables the memory-based circuit breaker.
    /// Default: true.
    /// </summary>
    public bool MemoryCircuitBreakerEnabled { get; set; } = true;

    /// <summary>
    /// Validates the configuration and throws if invalid.
    /// </summary>
    public void Validate()
    {
        if (DatabaseMaxParallelization < 1)
        {
            throw new InvalidOperationException(
                $"{nameof(DatabaseMaxParallelization)} must be at least 1.");
        }

        if (DatabaseMaxQueuedActions < 0)
        {
            throw new InvalidOperationException(
                $"{nameof(DatabaseMaxQueuedActions)} cannot be negative.");
        }

        if (ExternalApiMaxParallelization < 1)
        {
            throw new InvalidOperationException(
                $"{nameof(ExternalApiMaxParallelization)} must be at least 1.");
        }

        if (ExternalApiMaxQueuedActions < 0)
        {
            throw new InvalidOperationException(
                $"{nameof(ExternalApiMaxQueuedActions)} cannot be negative.");
        }

        if (PerTenantMaxParallelization < 1)
        {
            throw new InvalidOperationException(
                $"{nameof(PerTenantMaxParallelization)} must be at least 1.");
        }

        if (PerTenantMaxQueuedActions < 0)
        {
            throw new InvalidOperationException(
                $"{nameof(PerTenantMaxQueuedActions)} cannot be negative.");
        }

        if (MemoryThresholdBytes < 1_048_576) // Minimum 1MB
        {
            throw new InvalidOperationException(
                $"{nameof(MemoryThresholdBytes)} must be at least 1MB (1,048,576 bytes).");
        }
    }
}
