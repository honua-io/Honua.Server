// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Honua.Server.Enterprise.ETL.Resilience;

/// <summary>
/// Represents a workflow that failed and is stored in the dead letter queue
/// </summary>
public class FailedWorkflow
{
    /// <summary>
    /// Failed workflow identifier
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Workflow run ID
    /// </summary>
    public Guid WorkflowRunId { get; set; }

    /// <summary>
    /// Workflow definition ID
    /// </summary>
    public Guid WorkflowId { get; set; }

    /// <summary>
    /// Workflow name
    /// </summary>
    public string? WorkflowName { get; set; }

    /// <summary>
    /// Tenant ID
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Node ID where failure occurred
    /// </summary>
    public string? FailedNodeId { get; set; }

    /// <summary>
    /// Node type where failure occurred
    /// </summary>
    public string? FailedNodeType { get; set; }

    /// <summary>
    /// When workflow failed
    /// </summary>
    public DateTimeOffset FailedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Error category
    /// </summary>
    public ErrorCategory ErrorCategory { get; set; }

    /// <summary>
    /// Error message
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Full error details (JSON)
    /// </summary>
    public string? ErrorDetailsJson { get; set; }

    /// <summary>
    /// Number of retry attempts
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Last retry attempt time
    /// </summary>
    public DateTimeOffset? LastRetryAt { get; set; }

    /// <summary>
    /// Status of this failed workflow
    /// </summary>
    public FailedWorkflowStatus Status { get; set; } = FailedWorkflowStatus.Pending;

    /// <summary>
    /// User assigned to investigate/fix
    /// </summary>
    public Guid? AssignedTo { get; set; }

    /// <summary>
    /// Resolution notes
    /// </summary>
    public string? ResolutionNotes { get; set; }

    /// <summary>
    /// When resolved
    /// </summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>
    /// Related failed workflow IDs (same error pattern)
    /// </summary>
    public List<Guid>? RelatedFailures { get; set; }

    /// <summary>
    /// Tags for categorization
    /// </summary>
    public List<string>? Tags { get; set; }

    /// <summary>
    /// Priority level
    /// </summary>
    public FailedWorkflowPriority Priority { get; set; } = FailedWorkflowPriority.Medium;
}

/// <summary>
/// Status of failed workflow
/// </summary>
public enum FailedWorkflowStatus
{
    /// <summary>
    /// Waiting for review or retry
    /// </summary>
    Pending,

    /// <summary>
    /// Currently being retried
    /// </summary>
    Retrying,

    /// <summary>
    /// Being investigated
    /// </summary>
    Investigating,

    /// <summary>
    /// Successfully resolved (retry succeeded or manually fixed)
    /// </summary>
    Resolved,

    /// <summary>
    /// Abandoned (won't retry, accepted failure)
    /// </summary>
    Abandoned
}

/// <summary>
/// Priority level for failed workflows
/// </summary>
public enum FailedWorkflowPriority
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Options for retrying a failed workflow
/// </summary>
public class RetryOptions
{
    /// <summary>
    /// Retry from the beginning or from failed node
    /// </summary>
    public RetryFromPoint FromPoint { get; set; } = RetryFromPoint.FailedNode;

    /// <summary>
    /// Override parameter values for retry
    /// </summary>
    public Dictionary<string, object>? ParameterOverrides { get; set; }

    /// <summary>
    /// Force retry even if circuit breaker is open
    /// </summary>
    public bool ForceRetry { get; set; } = false;

    /// <summary>
    /// User initiating the retry
    /// </summary>
    public Guid? UserId { get; set; }
}

/// <summary>
/// Where to retry from
/// </summary>
public enum RetryFromPoint
{
    /// <summary>
    /// Restart entire workflow
    /// </summary>
    Beginning,

    /// <summary>
    /// Continue from the failed node
    /// </summary>
    FailedNode
}
