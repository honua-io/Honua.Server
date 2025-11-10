// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Core.Exceptions;

/// <summary>
/// Exception thrown when a tenant exceeds their resource allocation limits.
/// This prevents noisy neighbor problems in multi-tenant scenarios.
/// </summary>
public class TenantResourceLimitExceededException : HonuaException
{
    /// <summary>
    /// Gets the tenant ID that exceeded the limit.
    /// </summary>
    public string TenantId { get; }

    /// <summary>
    /// Gets the maximum parallelization allowed for the tenant.
    /// </summary>
    public int MaxParallelization { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantResourceLimitExceededException"/> class.
    /// </summary>
    /// <param name="tenantId">The tenant ID that exceeded the limit.</param>
    public TenantResourceLimitExceededException(string tenantId)
        : base(
            $"Resource limit exceeded for tenant '{tenantId}'. Too many concurrent operations in progress.",
            "TENANT_RESOURCE_LIMIT_EXCEEDED")
    {
        TenantId = tenantId;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantResourceLimitExceededException"/> class.
    /// </summary>
    /// <param name="tenantId">The tenant ID that exceeded the limit.</param>
    /// <param name="maxParallelization">The maximum parallelization allowed.</param>
    public TenantResourceLimitExceededException(string tenantId, int maxParallelization)
        : base(
            $"Resource limit exceeded for tenant '{tenantId}'. Maximum {maxParallelization} concurrent operations allowed.",
            "TENANT_RESOURCE_LIMIT_EXCEEDED")
    {
        TenantId = tenantId;
        MaxParallelization = maxParallelization;
    }
}

/// <summary>
/// Exception thrown when memory usage exceeds the configured threshold.
/// This protects against out-of-memory conditions.
/// </summary>
public class MemoryThresholdExceededException : HonuaException
{
    /// <summary>
    /// Gets the current memory usage in bytes.
    /// </summary>
    public long CurrentMemoryBytes { get; }

    /// <summary>
    /// Gets the memory threshold in bytes.
    /// </summary>
    public long ThresholdBytes { get; }

    /// <summary>
    /// Gets the current memory usage in megabytes for easier reading.
    /// </summary>
    public long CurrentMemoryMB => CurrentMemoryBytes / 1_048_576;

    /// <summary>
    /// Gets the memory threshold in megabytes for easier reading.
    /// </summary>
    public long ThresholdMB => ThresholdBytes / 1_048_576;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryThresholdExceededException"/> class.
    /// </summary>
    /// <param name="currentMemory">The current memory usage in bytes.</param>
    /// <param name="threshold">The memory threshold in bytes.</param>
    public MemoryThresholdExceededException(long currentMemory, long threshold)
        : base(
            $"Memory threshold exceeded: {currentMemory / 1_048_576}MB > {threshold / 1_048_576}MB. Operation rejected to prevent OOM.",
            "MEMORY_THRESHOLD_EXCEEDED")
    {
        CurrentMemoryBytes = currentMemory;
        ThresholdBytes = threshold;
    }
}

/// <summary>
/// Exception thrown when a bulkhead rejects an operation due to capacity limits.
/// This indicates that the maximum parallelization and queue size have been reached.
/// </summary>
public class BulkheadRejectedException : HonuaException
{
    /// <summary>
    /// Gets the name of the bulkhead that rejected the operation.
    /// </summary>
    public string BulkheadName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BulkheadRejectedException"/> class.
    /// </summary>
    /// <param name="bulkheadName">The name of the bulkhead (e.g., "Database", "ExternalApi").</param>
    public BulkheadRejectedException(string bulkheadName)
        : base(
            $"Bulkhead '{bulkheadName}' rejected the operation due to capacity limits. All execution slots and queue positions are occupied.",
            "BULKHEAD_REJECTED")
    {
        BulkheadName = bulkheadName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BulkheadRejectedException"/> class.
    /// </summary>
    /// <param name="bulkheadName">The name of the bulkhead.</param>
    /// <param name="innerException">The inner exception from Polly.</param>
    public BulkheadRejectedException(string bulkheadName, Exception innerException)
        : base(
            $"Bulkhead '{bulkheadName}' rejected the operation due to capacity limits.",
            "BULKHEAD_REJECTED",
            innerException)
    {
        BulkheadName = bulkheadName;
    }
}
