// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.ETL.Progress;

/// <summary>
/// SignalR-based implementation of workflow progress broadcaster
/// </summary>
public class SignalRWorkflowProgressBroadcaster : IWorkflowProgressBroadcaster
{
    private readonly IHubContext<GeoEtlProgressHub> _hubContext;
    private readonly ILogger<SignalRWorkflowProgressBroadcaster> _logger;

    public SignalRWorkflowProgressBroadcaster(
        IHubContext<GeoEtlProgressHub> hubContext,
        ILogger<SignalRWorkflowProgressBroadcaster> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task BroadcastWorkflowStartedAsync(Guid runId, WorkflowStartedMetadata metadata)
    {
        try
        {
            var payload = new
            {
                runId,
                workflowId = metadata.WorkflowId,
                workflowName = metadata.WorkflowName,
                tenantId = metadata.TenantId,
                userId = metadata.UserId,
                totalNodes = metadata.TotalNodes,
                startedAt = metadata.StartedAt,
                parameters = metadata.Parameters
            };

            await BroadcastToWorkflow(runId, "WorkflowStarted", payload);

            _logger.LogInformation(
                "Broadcasted WorkflowStarted event for run {RunId} (Workflow: {WorkflowName})",
                runId,
                metadata.WorkflowName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting WorkflowStarted event for run {RunId}", runId);
        }
    }

    public async Task BroadcastNodeStartedAsync(Guid runId, string nodeId, string nodeName, string nodeType)
    {
        try
        {
            var payload = new
            {
                runId,
                nodeId,
                nodeName,
                nodeType,
                startedAt = DateTimeOffset.UtcNow
            };

            await BroadcastToWorkflow(runId, "NodeStarted", payload);

            _logger.LogDebug(
                "Broadcasted NodeStarted event for node {NodeId} in run {RunId}",
                nodeId,
                runId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting NodeStarted event for node {NodeId} in run {RunId}", nodeId, runId);
        }
    }

    public async Task BroadcastNodeProgressAsync(
        Guid runId,
        string nodeId,
        int percent,
        string? message,
        long? featuresProcessed = null,
        long? totalFeatures = null)
    {
        try
        {
            var payload = new
            {
                runId,
                nodeId,
                percent,
                message,
                featuresProcessed,
                totalFeatures,
                timestamp = DateTimeOffset.UtcNow
            };

            await BroadcastToWorkflow(runId, "NodeProgress", payload);

            _logger.LogTrace(
                "Broadcasted NodeProgress event for node {NodeId} in run {RunId}: {Percent}%",
                nodeId,
                runId,
                percent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting NodeProgress event for node {NodeId} in run {RunId}", nodeId, runId);
        }
    }

    public async Task BroadcastNodeCompletedAsync(Guid runId, string nodeId, NodeCompletedResult result)
    {
        try
        {
            var payload = new
            {
                runId,
                nodeId,
                durationMs = result.DurationMs,
                featuresProcessed = result.FeaturesProcessed,
                bytesRead = result.BytesRead,
                bytesWritten = result.BytesWritten,
                metadata = result.Metadata,
                completedAt = DateTimeOffset.UtcNow
            };

            await BroadcastToWorkflow(runId, "NodeCompleted", payload);

            _logger.LogInformation(
                "Broadcasted NodeCompleted event for node {NodeId} in run {RunId} (Duration: {DurationMs}ms, Features: {FeaturesProcessed})",
                nodeId,
                runId,
                result.DurationMs,
                result.FeaturesProcessed ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting NodeCompleted event for node {NodeId} in run {RunId}", nodeId, runId);
        }
    }

    public async Task BroadcastNodeFailedAsync(Guid runId, string nodeId, string error)
    {
        try
        {
            var payload = new
            {
                runId,
                nodeId,
                error,
                failedAt = DateTimeOffset.UtcNow
            };

            await BroadcastToWorkflow(runId, "NodeFailed", payload);

            _logger.LogWarning(
                "Broadcasted NodeFailed event for node {NodeId} in run {RunId}: {Error}",
                nodeId,
                runId,
                error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting NodeFailed event for node {NodeId} in run {RunId}", nodeId, runId);
        }
    }

    public async Task BroadcastWorkflowCompletedAsync(Guid runId, WorkflowCompletedSummary summary)
    {
        try
        {
            var payload = new
            {
                runId,
                completedAt = summary.CompletedAt,
                totalDurationMs = summary.TotalDurationMs,
                nodesCompleted = summary.NodesCompleted,
                totalNodes = summary.TotalNodes,
                totalFeaturesProcessed = summary.TotalFeaturesProcessed,
                totalBytesRead = summary.TotalBytesRead,
                totalBytesWritten = summary.TotalBytesWritten,
                metadata = summary.Metadata
            };

            await BroadcastToWorkflow(runId, "WorkflowCompleted", payload);

            _logger.LogInformation(
                "Broadcasted WorkflowCompleted event for run {RunId} (Duration: {DurationMs}ms, Nodes: {NodesCompleted}/{TotalNodes}, Features: {FeaturesProcessed})",
                runId,
                summary.TotalDurationMs,
                summary.NodesCompleted,
                summary.TotalNodes,
                summary.TotalFeaturesProcessed ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting WorkflowCompleted event for run {RunId}", runId);
        }
    }

    public async Task BroadcastWorkflowFailedAsync(Guid runId, string error)
    {
        try
        {
            var payload = new
            {
                runId,
                error,
                failedAt = DateTimeOffset.UtcNow
            };

            await BroadcastToWorkflow(runId, "WorkflowFailed", payload);

            _logger.LogWarning(
                "Broadcasted WorkflowFailed event for run {RunId}: {Error}",
                runId,
                error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting WorkflowFailed event for run {RunId}", runId);
        }
    }

    public async Task BroadcastWorkflowCancelledAsync(Guid runId, string reason)
    {
        try
        {
            var payload = new
            {
                runId,
                reason,
                cancelledAt = DateTimeOffset.UtcNow
            };

            await BroadcastToWorkflow(runId, "WorkflowCancelled", payload);

            _logger.LogInformation(
                "Broadcasted WorkflowCancelled event for run {RunId}: {Reason}",
                runId,
                reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting WorkflowCancelled event for run {RunId}", runId);
        }
    }

    /// <summary>
    /// Broadcasts a message to all clients subscribed to a specific workflow run
    /// </summary>
    private async Task BroadcastToWorkflow(Guid runId, string eventName, object payload)
    {
        var groupName = GeoEtlProgressHub.GetWorkflowGroupName(runId);

        // Broadcast to workflow-specific group
        await _hubContext.Clients.Group(groupName).SendAsync(eventName, payload);

        // Also broadcast to "all workflows" group (for admin monitoring)
        await _hubContext.Clients.Group("all-workflows").SendAsync(eventName, payload);
    }
}

/// <summary>
/// Placeholder for GeoEtlProgressHub reference (actual hub is in Honua.Server.Host)
/// </summary>
internal static class GeoEtlProgressHub
{
    internal static string GetWorkflowGroupName(Guid runId) => $"workflow:{runId}";
}
