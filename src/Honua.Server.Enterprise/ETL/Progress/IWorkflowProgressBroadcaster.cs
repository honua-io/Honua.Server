// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Honua.Server.Enterprise.ETL.Progress;

/// <summary>
/// Interface for broadcasting workflow execution progress to clients in real-time
/// </summary>
public interface IWorkflowProgressBroadcaster
{
    /// <summary>
    /// Broadcast that a workflow has started
    /// </summary>
    Task BroadcastWorkflowStartedAsync(Guid runId, WorkflowStartedMetadata metadata);

    /// <summary>
    /// Broadcast that a node has started executing
    /// </summary>
    Task BroadcastNodeStartedAsync(Guid runId, string nodeId, string nodeName, string nodeType);

    /// <summary>
    /// Broadcast progress update for a node
    /// </summary>
    Task BroadcastNodeProgressAsync(Guid runId, string nodeId, int percent, string? message, long? featuresProcessed = null, long? totalFeatures = null);

    /// <summary>
    /// Broadcast that a node has completed successfully
    /// </summary>
    Task BroadcastNodeCompletedAsync(Guid runId, string nodeId, NodeCompletedResult result);

    /// <summary>
    /// Broadcast that a node has failed
    /// </summary>
    Task BroadcastNodeFailedAsync(Guid runId, string nodeId, string error);

    /// <summary>
    /// Broadcast that the workflow has completed successfully
    /// </summary>
    Task BroadcastWorkflowCompletedAsync(Guid runId, WorkflowCompletedSummary summary);

    /// <summary>
    /// Broadcast that the workflow has failed
    /// </summary>
    Task BroadcastWorkflowFailedAsync(Guid runId, string error);

    /// <summary>
    /// Broadcast that the workflow was cancelled
    /// </summary>
    Task BroadcastWorkflowCancelledAsync(Guid runId, string reason);
}

/// <summary>
/// Metadata sent when a workflow starts
/// </summary>
public class WorkflowStartedMetadata
{
    public required Guid WorkflowId { get; init; }
    public required string WorkflowName { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public required int TotalNodes { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public Dictionary<string, object>? Parameters { get; init; }
}

/// <summary>
/// Result data sent when a node completes
/// </summary>
public class NodeCompletedResult
{
    public required long DurationMs { get; init; }
    public long? FeaturesProcessed { get; init; }
    public long? BytesRead { get; init; }
    public long? BytesWritten { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Summary data sent when a workflow completes
/// </summary>
public class WorkflowCompletedSummary
{
    public required DateTimeOffset CompletedAt { get; init; }
    public required long TotalDurationMs { get; init; }
    public required int NodesCompleted { get; init; }
    public required int TotalNodes { get; init; }
    public long? TotalFeaturesProcessed { get; init; }
    public long? TotalBytesRead { get; init; }
    public long? TotalBytesWritten { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}
