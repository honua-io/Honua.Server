// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Enterprise.ETL.Models;

namespace Honua.Server.Enterprise.ETL.Resilience;

/// <summary>
/// Service for managing failed workflows (Dead Letter Queue)
/// </summary>
public interface IDeadLetterQueueService
{
    /// <summary>
    /// Add a failed workflow to the queue
    /// </summary>
    Task<FailedWorkflow> AddAsync(
        WorkflowRun run,
        WorkflowError error,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a failed workflow by ID
    /// </summary>
    Task<FailedWorkflow?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// List failed workflows with filtering
    /// </summary>
    Task<PagedResult<FailedWorkflow>> ListAsync(
        FailedWorkflowFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retry a failed workflow
    /// </summary>
    Task<WorkflowRun> RetryAsync(
        Guid failedWorkflowId,
        RetryOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk retry failed workflows
    /// </summary>
    Task<BulkRetryResult> BulkRetryAsync(
        List<Guid> failedWorkflowIds,
        RetryOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a failed workflow as abandoned
    /// </summary>
    Task AbandonAsync(
        Guid failedWorkflowId,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assign a failed workflow to a user
    /// </summary>
    Task AssignAsync(
        Guid failedWorkflowId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get error statistics
    /// </summary>
    Task<ErrorStatistics> GetStatisticsAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find related failures (same error pattern)
    /// </summary>
    Task<List<FailedWorkflow>> FindRelatedFailuresAsync(
        Guid failedWorkflowId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clean up old resolved/abandoned failures
    /// </summary>
    Task<int> CleanupOldFailuresAsync(
        int retentionDays = 30,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Filter for querying failed workflows
/// </summary>
public class FailedWorkflowFilter
{
    public Guid? TenantId { get; set; }
    public Guid? WorkflowId { get; set; }
    public FailedWorkflowStatus? Status { get; set; }
    public ErrorCategory? ErrorCategory { get; set; }
    public DateTimeOffset? FromDate { get; set; }
    public DateTimeOffset? ToDate { get; set; }
    public Guid? AssignedTo { get; set; }
    public string? SearchText { get; set; }
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 50;
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = true;
}

/// <summary>
/// Paged result
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
    public bool HasMore => Skip + Items.Count < TotalCount;
}

/// <summary>
/// Result of bulk retry operation
/// </summary>
public class BulkRetryResult
{
    public int TotalRequested { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public List<BulkRetryItemResult> Results { get; set; } = new();
}

public class BulkRetryItemResult
{
    public Guid FailedWorkflowId { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? NewRunId { get; set; }
}

/// <summary>
/// Error statistics
/// </summary>
public class ErrorStatistics
{
    public DateTimeOffset FromDate { get; set; }
    public DateTimeOffset ToDate { get; set; }
    public int TotalFailures { get; set; }
    public int PendingFailures { get; set; }
    public int ResolvedFailures { get; set; }
    public int AbandonedFailures { get; set; }
    public Dictionary<ErrorCategory, int> FailuresByCategory { get; set; } = new();
    public Dictionary<string, int> FailuresByNodeType { get; set; } = new();
    public Dictionary<string, int> FailuresByWorkflow { get; set; } = new();
    public List<TopError> TopErrors { get; set; } = new();
    public double AverageRetryCount { get; set; }
    public double SuccessfulRetryRate { get; set; }
}

public class TopError
{
    public string ErrorMessage { get; set; } = string.Empty;
    public ErrorCategory Category { get; set; }
    public int Count { get; set; }
    public DateTimeOffset? LastOccurred { get; set; }
}
