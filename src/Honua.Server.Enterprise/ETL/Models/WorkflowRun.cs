// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Honua.Server.Enterprise.ETL.Models;

/// <summary>
/// Represents a single execution of a workflow
/// </summary>
public class WorkflowRun
{
    /// <summary>
    /// Unique run identifier
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Workflow definition ID
    /// </summary>
    public Guid WorkflowId { get; set; }

    /// <summary>
    /// Tenant ID
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Run status
    /// </summary>
    public WorkflowRunStatus Status { get; set; } = WorkflowRunStatus.Pending;

    /// <summary>
    /// When run started
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// When run completed
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// User who triggered the run
    /// </summary>
    public Guid? TriggeredBy { get; set; }

    /// <summary>
    /// How the run was triggered
    /// </summary>
    public WorkflowTriggerType TriggerType { get; set; } = WorkflowTriggerType.Manual;

    /// <summary>
    /// Parameter values for this run
    /// </summary>
    public Dictionary<string, object>? ParameterValues { get; set; }

    /// <summary>
    /// Features processed across all nodes
    /// </summary>
    public long? FeaturesProcessed { get; set; }

    /// <summary>
    /// Bytes read across all nodes
    /// </summary>
    public long? BytesRead { get; set; }

    /// <summary>
    /// Bytes written across all nodes
    /// </summary>
    public long? BytesWritten { get; set; }

    /// <summary>
    /// Peak memory usage in MB
    /// </summary>
    public int? PeakMemoryMB { get; set; }

    /// <summary>
    /// Total CPU time in milliseconds
    /// </summary>
    public long? CpuTimeMs { get; set; }

    /// <summary>
    /// Compute cost in USD
    /// </summary>
    public decimal? ComputeCostUsd { get; set; }

    /// <summary>
    /// Storage cost in USD
    /// </summary>
    public decimal? StorageCostUsd { get; set; }

    /// <summary>
    /// Output file locations or URLs
    /// </summary>
    public Dictionary<string, string>? OutputLocations { get; set; }

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Error stack trace
    /// </summary>
    public string? ErrorStack { get; set; }

    /// <summary>
    /// Input datasets used
    /// </summary>
    public List<string>? InputDatasets { get; set; }

    /// <summary>
    /// Output datasets produced
    /// </summary>
    public List<string>? OutputDatasets { get; set; }

    /// <summary>
    /// Node execution details
    /// </summary>
    public List<NodeRun> NodeRuns { get; set; } = new();

    /// <summary>
    /// Current execution state (for resume/restart)
    /// </summary>
    public Dictionary<string, object>? State { get; set; }

    /// <summary>
    /// When run was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Workflow run status
/// </summary>
public enum WorkflowRunStatus
{
    /// <summary>
    /// Run is queued, waiting to start
    /// </summary>
    Pending,

    /// <summary>
    /// Run is currently executing
    /// </summary>
    Running,

    /// <summary>
    /// Run completed successfully
    /// </summary>
    Completed,

    /// <summary>
    /// Run failed with error
    /// </summary>
    Failed,

    /// <summary>
    /// Run was cancelled by user
    /// </summary>
    Cancelled,

    /// <summary>
    /// Run timed out
    /// </summary>
    Timeout
}

/// <summary>
/// How the workflow was triggered
/// </summary>
public enum WorkflowTriggerType
{
    /// <summary>
    /// Manually triggered by user
    /// </summary>
    Manual,

    /// <summary>
    /// Triggered by schedule
    /// </summary>
    Scheduled,

    /// <summary>
    /// Triggered via API
    /// </summary>
    Api,

    /// <summary>
    /// Triggered by event (e.g., new data upload)
    /// </summary>
    Event,

    /// <summary>
    /// Triggered by another workflow
    /// </summary>
    Workflow
}

/// <summary>
/// Individual node execution within a workflow run
/// </summary>
public class NodeRun
{
    /// <summary>
    /// Unique node run identifier
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Workflow run ID
    /// </summary>
    public Guid WorkflowRunId { get; set; }

    /// <summary>
    /// Node ID from workflow definition
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Node type
    /// </summary>
    public string NodeType { get; set; } = string.Empty;

    /// <summary>
    /// Node execution status
    /// </summary>
    public NodeRunStatus Status { get; set; } = NodeRunStatus.Pending;

    /// <summary>
    /// When node started
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// When node completed
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Duration in milliseconds
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// Features processed by this node
    /// </summary>
    public long? FeaturesProcessed { get; set; }

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Link to geoprocessing run if this is a geoprocessing node
    /// </summary>
    public Guid? GeoprocessingRunId { get; set; }

    /// <summary>
    /// Node output (for passing to next nodes)
    /// </summary>
    public Dictionary<string, object>? Output { get; set; }

    /// <summary>
    /// Number of retries attempted
    /// </summary>
    public int RetryCount { get; set; } = 0;
}

/// <summary>
/// Node run status
/// </summary>
public enum NodeRunStatus
{
    /// <summary>
    /// Node is waiting to execute
    /// </summary>
    Pending,

    /// <summary>
    /// Node is currently executing
    /// </summary>
    Running,

    /// <summary>
    /// Node completed successfully
    /// </summary>
    Completed,

    /// <summary>
    /// Node failed with error
    /// </summary>
    Failed,

    /// <summary>
    /// Node was skipped (e.g., conditional)
    /// </summary>
    Skipped
}
