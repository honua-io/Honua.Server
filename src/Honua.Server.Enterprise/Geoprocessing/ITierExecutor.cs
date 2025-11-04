// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Enterprise.Geoprocessing;

/// <summary>
/// Tier execution coordinator - Routes operations to appropriate execution tier
/// Supports adaptive fallback between NTS, PostGIS, and Cloud Batch
/// </summary>
public interface ITierExecutor
{
    /// <summary>
    /// Executes a process on the specified tier
    /// Falls back to higher tiers if the current tier fails or is unavailable
    /// </summary>
    Task<ProcessResult> ExecuteAsync(
        ProcessRun run,
        ProcessDefinition process,
        ProcessExecutionTier tier,
        IProgress<ProcessProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Selects the optimal tier for a process based on inputs and thresholds
    /// </summary>
    Task<ProcessExecutionTier> SelectTierAsync(
        ProcessDefinition process,
        ProcessExecutionRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if a specific tier is available for execution
    /// </summary>
    Task<bool> IsTierAvailableAsync(ProcessExecutionTier tier, CancellationToken ct = default);

    /// <summary>
    /// Gets tier capacity and health status
    /// </summary>
    Task<TierStatus> GetTierStatusAsync(ProcessExecutionTier tier, CancellationToken ct = default);
}

/// <summary>
/// Executor for Tier 1 (NetTopologySuite) - In-process operations
/// Fast, simple vector operations that complete in &lt;100ms
/// </summary>
public interface INtsExecutor
{
    /// <summary>
    /// Executes a process using NetTopologySuite
    /// </summary>
    Task<ProcessResult> ExecuteAsync(
        ProcessRun run,
        ProcessDefinition process,
        IProgress<ProcessProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Validates if a process can be executed with NTS
    /// </summary>
    Task<bool> CanExecuteAsync(ProcessDefinition process, ProcessExecutionRequest request, CancellationToken ct = default);
}

/// <summary>
/// Executor for Tier 2 (PostGIS) - Database server-side operations
/// Medium complexity operations that complete in 1-10 seconds
/// </summary>
public interface IPostGisExecutor
{
    /// <summary>
    /// Executes a process using PostGIS stored procedures
    /// </summary>
    Task<ProcessResult> ExecuteAsync(
        ProcessRun run,
        ProcessDefinition process,
        IProgress<ProcessProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Validates if a process can be executed with PostGIS
    /// </summary>
    Task<bool> CanExecuteAsync(ProcessDefinition process, ProcessExecutionRequest request, CancellationToken ct = default);
}

/// <summary>
/// Executor for Tier 3 (Cloud Batch) - AWS/Azure/GCP batch services
/// Complex, long-running operations (10s-30min) with GPU support
/// </summary>
public interface ICloudBatchExecutor
{
    /// <summary>
    /// Submits a process to cloud batch service
    /// Returns immediately with job ID, execution happens asynchronously
    /// </summary>
    Task<ProcessResult> SubmitAsync(
        ProcessRun run,
        ProcessDefinition process,
        IProgress<ProcessProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets status of a cloud batch job
    /// </summary>
    Task<CloudBatchJobStatus> GetJobStatusAsync(string cloudJobId, CancellationToken ct = default);

    /// <summary>
    /// Cancels a running cloud batch job
    /// </summary>
    Task<bool> CancelJobAsync(string cloudJobId, CancellationToken ct = default);

    /// <summary>
    /// Validates if a process can be executed with cloud batch
    /// </summary>
    Task<bool> CanExecuteAsync(ProcessDefinition process, ProcessExecutionRequest request, CancellationToken ct = default);
}

/// <summary>
/// Process execution progress
/// </summary>
public class ProcessProgress
{
    /// <summary>Progress percentage (0-100)</summary>
    public int Percent { get; init; }

    /// <summary>Progress message</summary>
    public string? Message { get; init; }

    /// <summary>Features processed so far</summary>
    public long? FeaturesProcessed { get; init; }

    /// <summary>Total features to process</summary>
    public long? TotalFeatures { get; init; }

    /// <summary>Current stage/phase</summary>
    public string? Stage { get; init; }

    /// <summary>Additional metadata</summary>
    public object? Metadata { get; init; }
}

/// <summary>
/// Tier status and health
/// </summary>
public class TierStatus
{
    /// <summary>Tier identifier</summary>
    public ProcessExecutionTier Tier { get; init; }

    /// <summary>Whether tier is available</summary>
    public bool Available { get; init; }

    /// <summary>Current queue depth</summary>
    public int QueueDepth { get; init; }

    /// <summary>Active/running jobs</summary>
    public int ActiveJobs { get; init; }

    /// <summary>Available capacity (0-100%)</summary>
    public int CapacityPercent { get; init; }

    /// <summary>Average queue wait time (seconds)</summary>
    public double AverageWaitSeconds { get; init; }

    /// <summary>Health status message</summary>
    public string? HealthMessage { get; init; }

    /// <summary>Last health check timestamp</summary>
    public DateTimeOffset LastCheckAt { get; init; }
}

/// <summary>
/// Cloud batch job status
/// </summary>
public record CloudBatchJobStatus
{
    /// <summary>Cloud provider job ID</summary>
    public required string CloudJobId { get; init; }

    /// <summary>Honua job ID</summary>
    public required string HonuaJobId { get; init; }

    /// <summary>Status (pending, running, completed, failed)</summary>
    public required string Status { get; init; }

    /// <summary>Progress percentage</summary>
    public int? Progress { get; init; }

    /// <summary>Progress message</summary>
    public string? Message { get; init; }

    /// <summary>When job started</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>When job completed</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Exit code (if completed)</summary>
    public int? ExitCode { get; init; }

    /// <summary>Error message (if failed)</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Log stream URL</summary>
    public string? LogUrl { get; init; }

    /// <summary>Output URL (if completed successfully)</summary>
    public string? OutputUrl { get; init; }

    /// <summary>Resource usage</summary>
    public CloudBatchResourceUsage? ResourceUsage { get; init; }
}

/// <summary>
/// Cloud batch resource usage metrics
/// </summary>
public class CloudBatchResourceUsage
{
    /// <summary>CPU utilization (0-100%)</summary>
    public double? CpuPercent { get; init; }

    /// <summary>Memory utilization (0-100%)</summary>
    public double? MemoryPercent { get; init; }

    /// <summary>Memory used in MB</summary>
    public long? MemoryUsedMB { get; init; }

    /// <summary>GPU utilization (0-100%, if applicable)</summary>
    public double? GpuPercent { get; init; }

    /// <summary>Duration in seconds</summary>
    public double? DurationSeconds { get; init; }

    /// <summary>Compute cost estimate</summary>
    public decimal? EstimatedCost { get; init; }
}

/// <summary>
/// Exception thrown when tier execution fails
/// </summary>
public class TierExecutionException : Exception
{
    public ProcessExecutionTier Tier { get; }
    public string ProcessId { get; }

    public TierExecutionException(ProcessExecutionTier tier, string processId, string message)
        : base(message)
    {
        Tier = tier;
        ProcessId = processId;
    }

    public TierExecutionException(ProcessExecutionTier tier, string processId, string message, Exception innerException)
        : base(message, innerException)
    {
        Tier = tier;
        ProcessId = processId;
    }
}

/// <summary>
/// Exception thrown when tier is unavailable or at capacity
/// </summary>
public class TierUnavailableException : Exception
{
    public ProcessExecutionTier Tier { get; }

    public TierUnavailableException(ProcessExecutionTier tier, string message)
        : base(message)
    {
        Tier = tier;
    }

    public TierUnavailableException(ProcessExecutionTier tier, string message, Exception innerException)
        : base(message, innerException)
    {
        Tier = tier;
    }
}
