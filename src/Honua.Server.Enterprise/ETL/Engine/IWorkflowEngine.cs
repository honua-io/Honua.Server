// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Enterprise.ETL.Models;

namespace Honua.Server.Enterprise.ETL.Engine;

/// <summary>
/// Core workflow execution engine
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>
    /// Validates a workflow definition (checks DAG, node parameters, etc.)
    /// </summary>
    Task<WorkflowValidationResult> ValidateAsync(
        WorkflowDefinition workflow,
        Dictionary<string, object>? parameterValues = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Estimates resource usage for a workflow execution
    /// </summary>
    Task<WorkflowEstimate> EstimateAsync(
        WorkflowDefinition workflow,
        Dictionary<string, object>? parameterValues = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a workflow
    /// </summary>
    Task<WorkflowRun> ExecuteAsync(
        WorkflowDefinition workflow,
        WorkflowExecutionOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a running workflow
    /// </summary>
    Task CancelAsync(Guid workflowRunId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of a workflow run
    /// </summary>
    Task<WorkflowRun?> GetRunStatusAsync(Guid workflowRunId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for workflow execution
/// </summary>
public class WorkflowExecutionOptions
{
    /// <summary>
    /// Workflow run ID (for tracking)
    /// </summary>
    public Guid? RunId { get; set; }

    /// <summary>
    /// Tenant ID
    /// </summary>
    public required Guid TenantId { get; set; }

    /// <summary>
    /// User ID
    /// </summary>
    public required Guid UserId { get; set; }

    /// <summary>
    /// Parameter values for this execution
    /// </summary>
    public Dictionary<string, object>? ParameterValues { get; set; }

    /// <summary>
    /// How the workflow was triggered
    /// </summary>
    public WorkflowTriggerType TriggerType { get; set; } = WorkflowTriggerType.Manual;

    /// <summary>
    /// Progress callback
    /// </summary>
    public IProgress<WorkflowProgress>? ProgressCallback { get; set; }

    /// <summary>
    /// Overall timeout for workflow (default: 30 minutes)
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Whether to continue execution if a node fails (default: false)
    /// </summary>
    public bool ContinueOnError { get; set; } = false;
}

/// <summary>
/// Workflow validation result
/// </summary>
public class WorkflowValidationResult
{
    /// <summary>
    /// Whether validation passed
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Validation errors
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Validation warnings
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// DAG validation (cycles, disconnected nodes, etc.)
    /// </summary>
    public DagValidationResult? DagValidation { get; set; }

    /// <summary>
    /// Per-node validation results
    /// </summary>
    public Dictionary<string, List<string>> NodeErrors { get; set; } = new();

    public static WorkflowValidationResult Success() => new() { IsValid = true };

    public static WorkflowValidationResult Failure(params string[] errors) => new()
    {
        IsValid = false,
        Errors = new List<string>(errors)
    };
}

/// <summary>
/// DAG structure validation result
/// </summary>
public class DagValidationResult
{
    /// <summary>
    /// Whether DAG is valid (acyclic, connected, etc.)
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Topological order of nodes (for execution)
    /// </summary>
    public List<string>? ExecutionOrder { get; set; }

    /// <summary>
    /// Detected cycles (if any)
    /// </summary>
    public List<List<string>>? Cycles { get; set; }

    /// <summary>
    /// Disconnected nodes (if any)
    /// </summary>
    public List<string>? DisconnectedNodes { get; set; }

    /// <summary>
    /// Missing node references
    /// </summary>
    public List<string>? MissingNodes { get; set; }
}

/// <summary>
/// Workflow resource estimate
/// </summary>
public class WorkflowEstimate
{
    /// <summary>
    /// Total estimated duration in seconds
    /// </summary>
    public long TotalDurationSeconds { get; set; }

    /// <summary>
    /// Peak memory usage in MB
    /// </summary>
    public long PeakMemoryMB { get; set; }

    /// <summary>
    /// Total estimated cost in USD
    /// </summary>
    public decimal? TotalCostUsd { get; set; }

    /// <summary>
    /// Per-node estimates
    /// </summary>
    public Dictionary<string, NodeEstimate> NodeEstimates { get; set; } = new();

    /// <summary>
    /// Estimated critical path (longest sequential path)
    /// </summary>
    public List<string>? CriticalPath { get; set; }
}

/// <summary>
/// Workflow progress information
/// </summary>
public class WorkflowProgress
{
    /// <summary>
    /// Workflow run ID
    /// </summary>
    public Guid WorkflowRunId { get; set; }

    /// <summary>
    /// Overall progress percentage (0-100)
    /// </summary>
    public int ProgressPercent { get; set; }

    /// <summary>
    /// Current status message
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Number of nodes completed
    /// </summary>
    public int NodesCompleted { get; set; }

    /// <summary>
    /// Total number of nodes
    /// </summary>
    public int TotalNodes { get; set; }

    /// <summary>
    /// Currently executing node ID
    /// </summary>
    public string? CurrentNodeId { get; set; }

    /// <summary>
    /// Current node progress (0-100)
    /// </summary>
    public int? CurrentNodeProgress { get; set; }
}
