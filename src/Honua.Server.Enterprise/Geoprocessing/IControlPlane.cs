// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Enterprise.Geoprocessing;

/// <summary>
/// Control Plane - Central orchestrator for geoprocessing operations
/// Handles admission control, scheduling, and audit logging
/// </summary>
public interface IControlPlane
{
    // ==================== ADMISSION ====================
    // Policy checks, quotas, capacity reservation

    /// <summary>
    /// Evaluates whether a process execution request should be admitted
    /// Checks tenant quotas, rate limits, input size limits, and capacity
    /// </summary>
    Task<AdmissionDecision> AdmitAsync(ProcessExecutionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Gets tenant-specific policy overrides for a process
    /// </summary>
    TenantPolicyOverride GetTenantPolicyOverride(Guid tenantId, string processId);

    // ==================== SCHEDULING ====================
    // Queue management, job tracking, tier selection

    /// <summary>
    /// Enqueues an admitted job for asynchronous execution
    /// Returns ProcessRun record (source of truth for job tracking)
    /// </summary>
    Task<ProcessRun> EnqueueAsync(AdmissionDecision decision, CancellationToken ct = default);

    /// <summary>
    /// Executes an admitted job inline/synchronously
    /// Used for fast Tier 1 (NTS) operations that complete in &lt;100ms
    /// </summary>
    Task<ProcessResult> ExecuteInlineAsync(AdmissionDecision decision, CancellationToken ct = default);

    /// <summary>
    /// Gets status of a running or completed job with tenant isolation
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <param name="tenantId">Tenant ID for isolation (required)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Process run if found and belongs to tenant</returns>
    Task<ProcessRun?> GetJobStatusAsync(string jobId, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Cancels a pending or running job with tenant isolation
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <param name="tenantId">Tenant ID for isolation (required)</param>
    /// <param name="reason">Cancellation reason</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if cancelled, false if not found or already completed</returns>
    Task<bool> CancelJobAsync(string jobId, Guid tenantId, string? reason = null, CancellationToken ct = default);

    /// <summary>
    /// Lists jobs for a tenant with filtering (TenantId required in query for non-admin users)
    /// </summary>
    /// <param name="query">Query with TenantId for isolation</param>
    /// <param name="isSystemAdmin">Whether caller is system admin (can query across tenants)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Paginated list of process runs</returns>
    Task<ProcessRunQueryResult> QueryRunsAsync(ProcessRunQuery query, bool isSystemAdmin = false, CancellationToken ct = default);

    /// <summary>
    /// Dequeues the next pending job for execution (used by worker services)
    /// Atomically selects and marks job as running
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Process run if available, null if queue is empty</returns>
    Task<ProcessRun?> DequeueNextJobAsync(CancellationToken ct = default);

    /// <summary>
    /// Updates the progress of a running job
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <param name="progressPercent">Progress percentage (0-100)</param>
    /// <param name="progressMessage">Optional progress message</param>
    /// <param name="ct">Cancellation token</param>
    Task UpdateJobProgressAsync(string jobId, int progressPercent, string? progressMessage = null, CancellationToken ct = default);

    /// <summary>
    /// Requeues a job for retry after a transient failure
    /// Increments retry count and resets status to pending
    /// </summary>
    /// <param name="jobId">Job ID to requeue</param>
    /// <param name="retryCount">Current retry count</param>
    /// <param name="errorMessage">Error message from the failed attempt</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if requeued successfully, false if job not found</returns>
    Task<bool> RequeueJobForRetryAsync(string jobId, int retryCount, string errorMessage, CancellationToken ct = default);

    // ==================== AUDITING ====================
    // Provenance, cost tracking, telemetry

    /// <summary>
    /// Records successful completion of a job
    /// Updates ProcessRun with results, duration, tier used, costs
    /// </summary>
    Task RecordCompletionAsync(string jobId, ProcessResult result, ProcessExecutionTier tier, TimeSpan duration, CancellationToken ct = default);

    /// <summary>
    /// Records job failure
    /// Updates ProcessRun with error details
    /// </summary>
    Task RecordFailureAsync(string jobId, Exception error, ProcessExecutionTier tier, TimeSpan duration, CancellationToken ct = default);

    /// <summary>
    /// Gets execution statistics for a tenant
    /// </summary>
    Task<ProcessExecutionStatistics> GetStatisticsAsync(Guid? tenantId = null, DateTimeOffset? startTime = null, DateTimeOffset? endTime = null, CancellationToken ct = default);
}

/// <summary>
/// Request to execute a geoprocessing operation
/// </summary>
public class ProcessExecutionRequest
{
    /// <summary>Process identifier (e.g., "buffer", "intersection")</summary>
    public required string ProcessId { get; init; }

    /// <summary>Tenant making the request</summary>
    public required Guid TenantId { get; init; }

    /// <summary>User making the request</summary>
    public required Guid UserId { get; init; }

    /// <summary>User email for notifications</summary>
    public string? UserEmail { get; init; }

    /// <summary>Input parameters (geometry, distance, etc.)</summary>
    public required Dictionary<string, object> Inputs { get; init; }

    /// <summary>Execution mode preference (sync, async, auto)</summary>
    public ExecutionMode Mode { get; init; } = ExecutionMode.Auto;

    /// <summary>Response format (geojson, shapefile, geoparquet, etc.)</summary>
    public string ResponseFormat { get; init; } = "geojson";

    /// <summary>Preferred tier (if any)</summary>
    public ProcessExecutionTier? PreferredTier { get; init; }

    /// <summary>Job priority (1-10, higher = more urgent)</summary>
    public int Priority { get; init; } = 5;

    /// <summary>Optional webhook URL for completion notification</summary>
    public string? WebhookUrl { get; init; }

    /// <summary>Additional metadata</summary>
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Admission decision result
/// </summary>
public class AdmissionDecision
{
    /// <summary>Whether the request is admitted</summary>
    public required bool Admitted { get; set; }

    /// <summary>Original request</summary>
    public required ProcessExecutionRequest Request { get; set; }

    /// <summary>Selected execution tier</summary>
    public ProcessExecutionTier? SelectedTier { get; set; }

    /// <summary>Execution mode (inline sync vs async)</summary>
    public ExecutionMode ExecutionMode { get; set; }

    /// <summary>Estimated duration in seconds</summary>
    public int? EstimatedDurationSeconds { get; set; }

    /// <summary>Estimated cost (compute units)</summary>
    public decimal? EstimatedCost { get; set; }

    /// <summary>Denial reasons (if not admitted)</summary>
    public List<string> DenialReasons { get; set; } = new();

    /// <summary>Warnings (admitted but with caveats)</summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>Validated and normalized inputs</summary>
    public Dictionary<string, object>? ValidatedInputs { get; set; }
}

/// <summary>
/// Tenant-specific policy overrides
/// </summary>
public class TenantPolicyOverride
{
    public Guid TenantId { get; init; }
    public string ProcessId { get; init; } = string.Empty;

    /// <summary>Max concurrent jobs for this tenant+process</summary>
    public int? MaxConcurrentJobs { get; init; }

    /// <summary>Rate limit (jobs per minute)</summary>
    public int? RateLimitPerMinute { get; init; }

    /// <summary>Max input size in MB</summary>
    public int? MaxInputSizeMB { get; init; }

    /// <summary>Allowed tiers</summary>
    public List<ProcessExecutionTier>? AllowedTiers { get; init; }

    /// <summary>Force specific tier</summary>
    public ProcessExecutionTier? ForceTier { get; init; }

    /// <summary>Custom timeout in seconds</summary>
    public int? TimeoutSeconds { get; init; }
}

/// <summary>
/// Execution modes
/// </summary>
public enum ExecutionMode
{
    /// <summary>Automatically choose based on estimated duration</summary>
    Auto,

    /// <summary>Synchronous inline execution (blocks until complete)</summary>
    Sync,

    /// <summary>Asynchronous execution (returns job ID immediately)</summary>
    Async
}

/// <summary>
/// Execution tiers (based on architecture document)
/// </summary>
public enum ProcessExecutionTier
{
    /// <summary>Tier 1: NetTopologySuite (in-process, &lt;100ms, simple operations)</summary>
    NTS = 1,

    /// <summary>Tier 2: PostGIS (database server-side, 1-10s, medium complexity)</summary>
    PostGIS = 2,

    /// <summary>Tier 3: Cloud Batch (AWS/Azure/GCP, 10s-30min, complex/large jobs, GPU support)</summary>
    CloudBatch = 3
}

/// <summary>
/// Query for filtering process runs
/// </summary>
public class ProcessRunQuery
{
    public Guid? TenantId { get; init; }
    public Guid? UserId { get; init; }
    public string? ProcessId { get; init; }
    public ProcessRunStatus? Status { get; init; }
    public DateTimeOffset? StartTime { get; init; }
    public DateTimeOffset? EndTime { get; init; }
    public int Limit { get; init; } = 100;
    public int Offset { get; init; } = 0;
    public string? SortBy { get; init; } = "created_at";
    public bool SortDescending { get; init; } = true;
}

/// <summary>
/// Query result for process runs
/// </summary>
public class ProcessRunQueryResult
{
    public List<ProcessRun> Runs { get; init; } = new();
    public int TotalCount { get; init; }
    public int Offset { get; init; }
    public int Limit { get; init; }
}

/// <summary>
/// Execution statistics
/// </summary>
public class ProcessExecutionStatistics
{
    public long TotalRuns { get; init; }
    public long SuccessfulRuns { get; init; }
    public long FailedRuns { get; init; }
    public long CancelledRuns { get; init; }

    public long PendingRuns { get; init; }
    public long RunningRuns { get; init; }

    public double AverageDurationSeconds { get; init; }
    public double MedianDurationSeconds { get; init; }
    public double P95DurationSeconds { get; init; }

    public decimal TotalComputeCost { get; init; }

    public Dictionary<string, long> RunsByProcess { get; init; } = new();
    public Dictionary<ProcessExecutionTier, long> RunsByTier { get; init; } = new();
    public Dictionary<string, long> RunsByStatus { get; init; } = new();
}
