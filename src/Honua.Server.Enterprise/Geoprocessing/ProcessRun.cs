// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Honua.Server.Enterprise.Geoprocessing;

/// <summary>
/// ProcessRun - Single source of truth for geoprocessing job tracking
/// Used for scheduling, billing, provenance, and audit
/// </summary>
public class ProcessRun
{
    /// <summary>Unique job identifier</summary>
    public required string JobId { get; init; }

    /// <summary>Process identifier (e.g., "buffer", "intersection")</summary>
    public required string ProcessId { get; init; }

    /// <summary>Tenant ID</summary>
    public required Guid TenantId { get; init; }

    /// <summary>User ID</summary>
    public required Guid UserId { get; init; }

    /// <summary>User email</summary>
    public string? UserEmail { get; init; }

    // ==================== STATUS & TIMING ====================

    /// <summary>Current status</summary>
    public ProcessRunStatus Status { get; set; } = ProcessRunStatus.Pending;

    /// <summary>When job was created/submitted</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>When job started executing</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>When job completed (success or failure)</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Total duration in milliseconds</summary>
    public long? DurationMs { get; set; }

    /// <summary>Queue wait time in milliseconds</summary>
    public long? QueueWaitMs { get; set; }

    // ==================== EXECUTION ====================

    /// <summary>Execution tier used (NTS, PostGIS, CloudBatch)</summary>
    public ProcessExecutionTier? ExecutedTier { get; set; }

    /// <summary>Worker/instance ID that processed this job</summary>
    public string? WorkerId { get; set; }

    /// <summary>Cloud batch job ID (if Tier 3)</summary>
    public string? CloudBatchJobId { get; set; }

    /// <summary>Priority (1-10, higher = more urgent)</summary>
    public int Priority { get; set; } = 5;

    /// <summary>Progress percentage (0-100)</summary>
    public int Progress { get; set; } = 0;

    /// <summary>Progress message</summary>
    public string? ProgressMessage { get; set; }

    // ==================== INPUTS & OUTPUTS ====================

    /// <summary>Input parameters (serialized JSON)</summary>
    public required Dictionary<string, object> Inputs { get; init; }

    /// <summary>Output result (serialized JSON)</summary>
    public Dictionary<string, object>? Output { get; set; }

    /// <summary>Response format (geojson, shapefile, geoparquet, etc.)</summary>
    public string ResponseFormat { get; init; } = "geojson";

    /// <summary>Output URL (if stored in blob storage)</summary>
    public string? OutputUrl { get; set; }

    /// <summary>Output size in bytes</summary>
    public long? OutputSizeBytes { get; set; }

    // ==================== ERROR HANDLING ====================

    /// <summary>Error message (if failed)</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Error details/stack trace</summary>
    public string? ErrorDetails { get; set; }

    /// <summary>Retry count</summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>Max retries allowed</summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>Cancellation reason</summary>
    public string? CancellationReason { get; set; }

    // ==================== RESOURCE USAGE & BILLING ====================

    /// <summary>Peak memory usage in MB</summary>
    public long? PeakMemoryMB { get; set; }

    /// <summary>CPU time in milliseconds</summary>
    public long? CpuTimeMs { get; set; }

    /// <summary>Number of features processed</summary>
    public long? FeaturesProcessed { get; set; }

    /// <summary>Input data size in MB</summary>
    public decimal? InputSizeMB { get; set; }

    /// <summary>Compute cost in units (for billing)</summary>
    public decimal? ComputeCost { get; set; }

    /// <summary>Storage cost in units (for output storage)</summary>
    public decimal? StorageCost { get; set; }

    /// <summary>Total cost in units</summary>
    public decimal? TotalCost { get; set; }

    // ==================== PROVENANCE & AUDIT ====================

    /// <summary>IP address of requester</summary>
    public string? IpAddress { get; init; }

    /// <summary>User agent</summary>
    public string? UserAgent { get; init; }

    /// <summary>API surface used (GeoservicesREST, OGC, Internal)</summary>
    public string ApiSurface { get; init; } = "OGC";

    /// <summary>Client application identifier</summary>
    public string? ClientId { get; init; }

    /// <summary>Tags for categorization</summary>
    public List<string> Tags { get; init; } = new();

    /// <summary>Additional metadata</summary>
    public Dictionary<string, object>? Metadata { get; set; }

    // ==================== NOTIFICATIONS ====================

    /// <summary>Webhook URL for completion notification</summary>
    public string? WebhookUrl { get; init; }

    /// <summary>Whether to send email notification</summary>
    public bool NotifyEmail { get; init; } = false;

    /// <summary>When webhook was sent (if applicable)</summary>
    public DateTimeOffset? WebhookSentAt { get; set; }

    /// <summary>Webhook response status</summary>
    public int? WebhookResponseStatus { get; set; }

    // ==================== HELPER METHODS ====================

    /// <summary>
    /// Checks if job is in a terminal state (completed, failed, or cancelled)
    /// </summary>
    public bool IsTerminal => Status is ProcessRunStatus.Completed or ProcessRunStatus.Failed or ProcessRunStatus.Cancelled;

    /// <summary>
    /// Checks if job is still active (pending or running)
    /// </summary>
    public bool IsActive => Status is ProcessRunStatus.Pending or ProcessRunStatus.Running;

    /// <summary>
    /// Calculates actual duration (time between start and completion)
    /// </summary>
    public TimeSpan? GetDuration()
    {
        if (StartedAt.HasValue && CompletedAt.HasValue)
            return CompletedAt.Value - StartedAt.Value;
        if (StartedAt.HasValue)
            return DateTimeOffset.UtcNow - StartedAt.Value;
        return null;
    }

    /// <summary>
    /// Calculates queue wait time (time between creation and start)
    /// </summary>
    public TimeSpan? GetQueueWait()
    {
        if (StartedAt.HasValue)
            return StartedAt.Value - CreatedAt;
        return DateTimeOffset.UtcNow - CreatedAt;
    }

    /// <summary>
    /// Creates a ProcessResult from this ProcessRun
    /// </summary>
    public ProcessResult ToResult()
    {
        return new ProcessResult
        {
            JobId = JobId,
            ProcessId = ProcessId,
            Status = Status,
            Success = Status == ProcessRunStatus.Completed,
            Output = Output,
            OutputUrl = OutputUrl,
            ErrorMessage = ErrorMessage,
            ErrorDetails = ErrorDetails,
            DurationMs = DurationMs,
            FeaturesProcessed = FeaturesProcessed,
            Metadata = Metadata
        };
    }
}

/// <summary>
/// Process run status
/// </summary>
public enum ProcessRunStatus
{
    /// <summary>Job is queued, waiting to execute</summary>
    Pending,

    /// <summary>Job is currently executing</summary>
    Running,

    /// <summary>Job completed successfully</summary>
    Completed,

    /// <summary>Job failed with error</summary>
    Failed,

    /// <summary>Job was cancelled by user or system</summary>
    Cancelled,

    /// <summary>Job exceeded timeout limit</summary>
    Timeout
}

/// <summary>
/// Process execution result
/// </summary>
public class ProcessResult
{
    /// <summary>Job identifier</summary>
    public required string JobId { get; init; }

    /// <summary>Process identifier</summary>
    public required string ProcessId { get; init; }

    /// <summary>Status</summary>
    public ProcessRunStatus Status { get; init; }

    /// <summary>Whether execution was successful</summary>
    public bool Success { get; init; }

    /// <summary>Output data (GeoJSON, URLs, etc.)</summary>
    public Dictionary<string, object>? Output { get; init; }

    /// <summary>Output URL (if stored externally)</summary>
    public string? OutputUrl { get; init; }

    /// <summary>Error message (if failed)</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Error details</summary>
    public string? ErrorDetails { get; init; }

    /// <summary>Duration in milliseconds</summary>
    public long? DurationMs { get; init; }

    /// <summary>Number of features processed</summary>
    public long? FeaturesProcessed { get; init; }

    /// <summary>Additional metadata</summary>
    public Dictionary<string, object>? Metadata { get; init; }
}
