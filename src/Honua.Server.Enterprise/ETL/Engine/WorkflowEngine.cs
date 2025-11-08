// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Enterprise.ETL.Models;
using Honua.Server.Enterprise.ETL.Nodes;
using Honua.Server.Enterprise.ETL.Progress;
using Honua.Server.Enterprise.ETL.Resilience;
using Honua.Server.Enterprise.ETL.Stores;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.ETL.Engine;

/// <summary>
/// Core workflow execution engine with DAG validation and execution
/// </summary>
public class WorkflowEngine : IWorkflowEngine
{
    private readonly IWorkflowStore _workflowStore;
    private readonly IWorkflowNodeRegistry _nodeRegistry;
    private readonly IWorkflowProgressBroadcaster? _progressBroadcaster;
    private readonly ICircuitBreakerService? _circuitBreakerService;
    private readonly IDeadLetterQueueService? _deadLetterQueueService;
    private readonly ILogger<WorkflowEngine> _logger;

    public WorkflowEngine(
        IWorkflowStore workflowStore,
        IWorkflowNodeRegistry nodeRegistry,
        ILogger<WorkflowEngine> logger,
        IWorkflowProgressBroadcaster? progressBroadcaster = null,
        ICircuitBreakerService? circuitBreakerService = null,
        IDeadLetterQueueService? deadLetterQueueService = null)
    {
        _workflowStore = workflowStore ?? throw new ArgumentNullException(nameof(workflowStore));
        _nodeRegistry = nodeRegistry ?? throw new ArgumentNullException(nameof(nodeRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _progressBroadcaster = progressBroadcaster;
        _circuitBreakerService = circuitBreakerService;
        _deadLetterQueueService = deadLetterQueueService;
    }

    public async Task<WorkflowValidationResult> ValidateAsync(
        WorkflowDefinition workflow,
        Dictionary<string, object>? parameterValues = null,
        CancellationToken cancellationToken = default)
    {
        var result = new WorkflowValidationResult { IsValid = true };

        try
        {
            // 1. Validate DAG structure
            result.DagValidation = ValidateDag(workflow);
            if (!result.DagValidation.IsValid)
            {
                result.IsValid = false;
                if (result.DagValidation.Cycles is { Count: > 0 } cycles)
                {
                    result.Errors.Add($"Workflow contains cycles: {string.Join(", ", cycles.Select(c => string.Join(" → ", c)))}");
                }
                if (result.DagValidation.MissingNodes is { Count: > 0 } missingNodes)
                {
                    result.Errors.Add($"Missing node references: {string.Join(", ", missingNodes)}");
                }
            }

            // Add warning for disconnected nodes (even if DAG is otherwise valid)
            if (result.DagValidation.DisconnectedNodes is { Count: > 0 } disconnectedNodes)
            {
                result.Warnings.Add($"Disconnected nodes: {string.Join(", ", disconnectedNodes)}");
            }

            // 2. Validate parameters
            parameterValues ??= new Dictionary<string, object>();
            foreach (var param in workflow.Parameters.Values.Where(p => p.Required))
            {
                if (!parameterValues.ContainsKey(param.Name))
                {
                    result.IsValid = false;
                    result.Errors.Add($"Required parameter '{param.Name}' is missing");
                }
            }

            // 3. Validate individual nodes
            foreach (var node in workflow.Nodes)
            {
                var nodeImpl = _nodeRegistry.GetNode(node.Type);
                if (nodeImpl == null)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Node {node.Id}: Unknown node type '{node.Type}'");
                    continue;
                }

                var nodeValidation = await nodeImpl.ValidateAsync(node, parameterValues, cancellationToken);
                if (!nodeValidation.IsValid)
                {
                    result.IsValid = false;
                    result.NodeErrors[node.Id] = nodeValidation.Errors;
                    result.Errors.AddRange(nodeValidation.Errors.Select(e => $"Node {node.Id}: {e}"));
                }

                if (nodeValidation.Warnings.Any())
                {
                    result.Warnings.AddRange(nodeValidation.Warnings.Select(w => $"Node {node.Id}: {w}"));
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating workflow {WorkflowId}", workflow.Id);
            return WorkflowValidationResult.Failure($"Validation error: {ex.Message}");
        }
    }

    public async Task<WorkflowEstimate> EstimateAsync(
        WorkflowDefinition workflow,
        Dictionary<string, object>? parameterValues = null,
        CancellationToken cancellationToken = default)
    {
        var estimate = new WorkflowEstimate();
        parameterValues ??= new Dictionary<string, object>();

        try
        {
            // Get execution order
            var dagValidation = ValidateDag(workflow);
            if (!dagValidation.IsValid || dagValidation.ExecutionOrder == null)
            {
                _logger.LogWarning("Cannot estimate workflow {WorkflowId}: invalid DAG", workflow.Id);
                return estimate;
            }

            // Estimate each node
            long totalDuration = 0;
            long peakMemory = 0;
            decimal totalCost = 0;

            foreach (var nodeId in dagValidation.ExecutionOrder)
            {
                var node = workflow.Nodes.FirstOrDefault(n => n.Id == nodeId);
                if (node == null) continue;

                var nodeImpl = _nodeRegistry.GetNode(node.Type);
                if (nodeImpl == null) continue;

                var nodeEstimate = await nodeImpl.EstimateAsync(node, parameterValues, cancellationToken);
                estimate.NodeEstimates[nodeId] = nodeEstimate;

                totalDuration += nodeEstimate.EstimatedDurationSeconds;
                peakMemory = Math.Max(peakMemory, nodeEstimate.EstimatedMemoryMB);
                totalCost += nodeEstimate.EstimatedCostUsd ?? 0;
            }

            estimate.TotalDurationSeconds = totalDuration;
            estimate.PeakMemoryMB = peakMemory;
            estimate.TotalCostUsd = totalCost > 0 ? totalCost : null;
            estimate.CriticalPath = dagValidation.ExecutionOrder;

            return estimate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error estimating workflow {WorkflowId}", workflow.Id);
            return estimate;
        }
    }

    public async Task<WorkflowRun> ExecuteAsync(
        WorkflowDefinition workflow,
        WorkflowExecutionOptions options,
        CancellationToken cancellationToken = default)
    {
        var runId = options.RunId ?? Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();

        var run = new WorkflowRun
        {
            Id = runId,
            WorkflowId = workflow.Id,
            TenantId = options.TenantId,
            Status = WorkflowRunStatus.Pending,
            TriggeredBy = options.UserId,
            TriggerType = options.TriggerType,
            ParameterValues = options.ParameterValues,
            CreatedAt = DateTimeOffset.UtcNow
        };

        try
        {
            _logger.LogInformation("Starting workflow execution {RunId} for workflow {WorkflowId}", runId, workflow.Id);

            // Persist run (use CancellationToken.None to ensure run is created even if token is cancelled)
            await _workflowStore.CreateRunAsync(run, CancellationToken.None);

            // Check for cancellation after creating run record
            cancellationToken.ThrowIfCancellationRequested();

            // Validate workflow
            var validation = await ValidateAsync(workflow, options.ParameterValues, cancellationToken);
            if (!validation.IsValid)
            {
                run.Status = WorkflowRunStatus.Failed;
                run.ErrorMessage = $"Workflow validation failed: {string.Join("; ", validation.Errors)}";
                await _workflowStore.UpdateRunAsync(run, cancellationToken);
                return run;
            }

            run.Status = WorkflowRunStatus.Running;
            run.StartedAt = DateTimeOffset.UtcNow;

            // Initialize workflow state (for nodes like OutputNode to store data)
            run.State = new Dictionary<string, object>();

            await _workflowStore.UpdateRunAsync(run, cancellationToken);

            // Broadcast workflow started
            if (_progressBroadcaster != null)
            {
                await _progressBroadcaster.BroadcastWorkflowStartedAsync(runId, new WorkflowStartedMetadata
                {
                    WorkflowId = workflow.Id,
                    WorkflowName = workflow.Metadata.Name,
                    TenantId = options.TenantId,
                    UserId = options.UserId,
                    TotalNodes = workflow.Nodes.Count,
                    StartedAt = run.StartedAt.Value,
                    Parameters = options.ParameterValues
                });
            }

            // Execute DAG in topological order
            var executionOrder = validation.DagValidation!.ExecutionOrder!;
            var nodeResults = new Dictionary<string, NodeExecutionResult>();

            int completedNodes = 0;
            foreach (var nodeId in executionOrder)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var node = workflow.Nodes.First(n => n.Id == nodeId);
                var nodeImpl = _nodeRegistry.GetNode(node.Type);
                if (nodeImpl == null)
                {
                    throw new InvalidOperationException($"Node type '{node.Type}' not found");
                }

                // Create node run
                var nodeRun = new NodeRun
                {
                    WorkflowRunId = runId,
                    NodeId = nodeId,
                    NodeType = node.Type,
                    Status = NodeRunStatus.Running,
                    StartedAt = DateTimeOffset.UtcNow
                };
                run.NodeRuns.Add(nodeRun);

                _logger.LogInformation("Executing node {NodeId} ({NodeType}) in run {RunId}", nodeId, node.Type, runId);

                // Broadcast node started
                if (_progressBroadcaster != null)
                {
                    await _progressBroadcaster.BroadcastNodeStartedAsync(
                        runId,
                        nodeId,
                        node.Name ?? nodeId,
                        node.Type);
                }

                // Report progress
                ReportProgress(options.ProgressCallback, run.Id, completedNodes, executionOrder.Count, nodeId, 0,
                    $"Executing {nodeImpl.DisplayName}...");

                // Get inputs from upstream nodes
                var inputs = GetNodeInputs(workflow, nodeId, nodeResults);

                // Check circuit breaker before executing
                if (_circuitBreakerService != null)
                {
                    var isOpen = await _circuitBreakerService.IsOpenAsync(node.Type);
                    if (isOpen)
                    {
                        var error = $"Circuit breaker is open for node type '{node.Type}' - too many recent failures";
                        _logger.LogWarning(error);

                        nodeRun.CompletedAt = DateTimeOffset.UtcNow;
                        nodeRun.Status = NodeRunStatus.Failed;
                        nodeRun.ErrorMessage = error;

                        if (!options.ContinueOnError)
                        {
                            run.Status = WorkflowRunStatus.Failed;
                            run.ErrorMessage = error;
                            run.CompletedAt = DateTimeOffset.UtcNow;
                            await _workflowStore.UpdateRunAsync(run, cancellationToken);
                            return run;
                        }

                        continue;
                    }
                }

                // Execute node
                var nodeStopwatch = Stopwatch.StartNew();
                var context = new NodeExecutionContext
                {
                    WorkflowRunId = runId,
                    NodeRunId = nodeRun.Id,
                    NodeDefinition = node,
                    Parameters = options.ParameterValues ?? new Dictionary<string, object>(),
                    Inputs = inputs,
                    TenantId = options.TenantId,
                    UserId = options.UserId,
                    State = run.State ?? new Dictionary<string, object>(),
                    ProgressCallback = new Progress<NodeProgress>(async p =>
                    {
                        ReportProgress(options.ProgressCallback, run.Id, completedNodes, executionOrder.Count,
                            nodeId, p.Percentage, p.Message);

                        // Broadcast node progress via SignalR
                        if (_progressBroadcaster != null)
                        {
                            await _progressBroadcaster.BroadcastNodeProgressAsync(
                                runId,
                                nodeId,
                                p.Percentage,
                                p.Message,
                                p.FeaturesProcessed,
                                p.TotalFeatures);
                        }
                    })
                };

                NodeExecutionResult result;
                try
                {
                    result = await nodeImpl.ExecuteAsync(context, cancellationToken);
                    nodeStopwatch.Stop();

                    // Record success with circuit breaker
                    if (result.Success && _circuitBreakerService != null)
                    {
                        await _circuitBreakerService.RecordSuccessAsync(node.Type);
                    }
                }
                catch (Exception ex)
                {
                    nodeStopwatch.Stop();
                    _logger.LogError(ex, "Unhandled exception in node {NodeId}", nodeId);

                    // Record failure with circuit breaker
                    if (_circuitBreakerService != null)
                    {
                        await _circuitBreakerService.RecordFailureAsync(node.Type, ex);
                    }

                    result = NodeExecutionResult.Fail(ex.Message, ex.StackTrace);
                }

                // Update node run
                nodeRun.CompletedAt = DateTimeOffset.UtcNow;
                nodeRun.DurationMs = nodeStopwatch.ElapsedMilliseconds;
                nodeRun.FeaturesProcessed = result.FeaturesProcessed;
                nodeRun.Status = result.Success ? NodeRunStatus.Completed : NodeRunStatus.Failed;
                nodeRun.ErrorMessage = result.ErrorMessage;
                nodeRun.Output = result.Data;

                if (!result.Success)
                {
                    _logger.LogError("Node {NodeId} failed: {Error}", nodeId, result.ErrorMessage);

                    // Record failure with circuit breaker
                    if (_circuitBreakerService != null && result.ErrorMessage != null)
                    {
                        await _circuitBreakerService.RecordFailureAsync(
                            node.Type,
                            new InvalidOperationException(result.ErrorMessage));
                    }

                    // Broadcast node failed
                    if (_progressBroadcaster != null)
                    {
                        await _progressBroadcaster.BroadcastNodeFailedAsync(
                            runId,
                            nodeId,
                            result.ErrorMessage ?? "Unknown error");
                    }

                    if (!options.ContinueOnError)
                    {
                        run.Status = WorkflowRunStatus.Failed;
                        run.ErrorMessage = $"Node {nodeId} failed: {result.ErrorMessage}";
                        run.CompletedAt = DateTimeOffset.UtcNow;
                        await _workflowStore.UpdateRunAsync(run, cancellationToken);

                        // Add to dead letter queue
                        if (_deadLetterQueueService != null)
                        {
                            try
                            {
                                var workflowError = new WorkflowError
                                {
                                    WorkflowRunId = runId,
                                    NodeId = nodeId,
                                    NodeType = node.Type,
                                    Category = ErrorCategorizer.CategorizeByMessage(result.ErrorMessage ?? ""),
                                    Message = result.ErrorMessage ?? "Unknown error",
                                    StackTrace = result.ErrorDetails,
                                    Suggestion = ErrorCategorizer.GetSuggestion(
                                        ErrorCategorizer.CategorizeByMessage(result.ErrorMessage ?? ""))
                                };

                                await _deadLetterQueueService.AddAsync(run, workflowError, cancellationToken);
                            }
                            catch (Exception dlqEx)
                            {
                                _logger.LogError(dlqEx, "Failed to add workflow to dead letter queue");
                            }
                        }

                        // Broadcast workflow failed
                        if (_progressBroadcaster != null)
                        {
                            await _progressBroadcaster.BroadcastWorkflowFailedAsync(runId, run.ErrorMessage);
                        }

                        return run;
                    }
                }
                else
                {
                    nodeResults[nodeId] = result;
                    completedNodes++;

                    // Broadcast node completed
                    if (_progressBroadcaster != null)
                    {
                        await _progressBroadcaster.BroadcastNodeCompletedAsync(
                            runId,
                            nodeId,
                            new NodeCompletedResult
                            {
                                DurationMs = nodeStopwatch.ElapsedMilliseconds,
                                FeaturesProcessed = result.FeaturesProcessed,
                                BytesRead = null,
                                BytesWritten = null
                            });
                    }
                }

                await _workflowStore.UpdateRunAsync(run, cancellationToken);
            }

            // Workflow completed successfully
            stopwatch.Stop();
            run.Status = WorkflowRunStatus.Completed;
            run.CompletedAt = DateTimeOffset.UtcNow;
            await _workflowStore.UpdateRunAsync(run, cancellationToken);

            _logger.LogInformation(
                "Workflow execution {RunId} completed in {DurationMs}ms",
                runId,
                stopwatch.ElapsedMilliseconds);

            ReportProgress(options.ProgressCallback, run.Id, executionOrder.Count, executionOrder.Count, null, 100,
                "Workflow completed successfully");

            // Broadcast workflow completed
            if (_progressBroadcaster != null)
            {
                await _progressBroadcaster.BroadcastWorkflowCompletedAsync(runId, new WorkflowCompletedSummary
                {
                    CompletedAt = run.CompletedAt.Value,
                    TotalDurationMs = stopwatch.ElapsedMilliseconds,
                    NodesCompleted = completedNodes,
                    TotalNodes = executionOrder.Count,
                    TotalFeaturesProcessed = run.NodeRuns.Sum(n => n.FeaturesProcessed),
                    TotalBytesRead = null,
                    TotalBytesWritten = null
                });
            }

            return run;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Workflow execution {RunId} was cancelled", runId);
            run.Status = WorkflowRunStatus.Cancelled;
            run.CompletedAt = DateTimeOffset.UtcNow;

            // Use CancellationToken.None to ensure we can update the run status even if cancelled
            await _workflowStore.UpdateRunAsync(run, CancellationToken.None);

            // Broadcast workflow cancelled
            if (_progressBroadcaster != null)
            {
                await _progressBroadcaster.BroadcastWorkflowCancelledAsync(runId, "Workflow was cancelled by user or system");
            }

            return run;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Workflow execution {RunId} failed", runId);
            run.Status = WorkflowRunStatus.Failed;
            run.ErrorMessage = ex.Message;
            run.ErrorStack = ex.StackTrace;
            run.CompletedAt = DateTimeOffset.UtcNow;
            await _workflowStore.UpdateRunAsync(run, cancellationToken);

            // Broadcast workflow failed
            if (_progressBroadcaster != null)
            {
                await _progressBroadcaster.BroadcastWorkflowFailedAsync(runId, ex.Message);
            }

            return run;
        }
    }

    public async Task CancelAsync(Guid workflowRunId, CancellationToken cancellationToken = default)
    {
        var run = await _workflowStore.GetRunAsync(workflowRunId, cancellationToken);
        if (run == null)
        {
            throw new InvalidOperationException($"Workflow run {workflowRunId} not found");
        }

        if (run.Status != WorkflowRunStatus.Running)
        {
            _logger.LogWarning("Cannot cancel workflow run {RunId} in status {Status}", workflowRunId, run.Status);
            return;
        }

        run.Status = WorkflowRunStatus.Cancelled;
        run.CompletedAt = DateTimeOffset.UtcNow;
        await _workflowStore.UpdateRunAsync(run, cancellationToken);

        _logger.LogInformation("Cancelled workflow run {RunId}", workflowRunId);
    }

    public Task<WorkflowRun?> GetRunStatusAsync(Guid workflowRunId, CancellationToken cancellationToken = default)
    {
        return _workflowStore.GetRunAsync(workflowRunId, cancellationToken);
    }

    /// <summary>
    /// Validates DAG structure (cycles, connectivity, topological sort)
    /// </summary>
    private DagValidationResult ValidateDag(WorkflowDefinition workflow)
    {
        var result = new DagValidationResult { IsValid = true };

        // Build adjacency list
        var adjacency = new Dictionary<string, List<string>>();
        var inDegree = new Dictionary<string, int>();

        foreach (var node in workflow.Nodes)
        {
            adjacency[node.Id] = new List<string>();
            inDegree[node.Id] = 0;
        }

        // Check for missing node references
        var missingNodes = new List<string>();
        foreach (var edge in workflow.Edges)
        {
            if (!adjacency.ContainsKey(edge.From))
            {
                missingNodes.Add(edge.From);
            }
            if (!adjacency.ContainsKey(edge.To))
            {
                missingNodes.Add(edge.To);
            }

            if (adjacency.ContainsKey(edge.From) && adjacency.ContainsKey(edge.To))
            {
                adjacency[edge.From].Add(edge.To);
                inDegree[edge.To]++;
            }
        }

        if (missingNodes.Any())
        {
            result.IsValid = false;
            result.MissingNodes = missingNodes.Distinct().ToList();
            return result;
        }

        // Topological sort (Kahn's algorithm) - also detects cycles
        var queue = new Queue<string>(inDegree.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key));
        var executionOrder = new List<string>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            executionOrder.Add(current);

            foreach (var neighbor in adjacency[current])
            {
                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0)
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        // If not all nodes processed, there's a cycle
        if (executionOrder.Count != workflow.Nodes.Count)
        {
            result.IsValid = false;
            var cycleNodes = inDegree.Where(kvp => kvp.Value > 0).Select(kvp => kvp.Key).ToList();
            result.Cycles = new List<List<string>> { cycleNodes };
        }

        result.ExecutionOrder = executionOrder;

        // Check for disconnected nodes (warning, not error)
        var disconnectedNodes = workflow.Nodes
            .Where(n => !workflow.Edges.Any(e => e.From == n.Id || e.To == n.Id))
            .Select(n => n.Id)
            .ToList();

        if (disconnectedNodes.Any())
        {
            result.DisconnectedNodes = disconnectedNodes;
        }

        return result;
    }

    /// <summary>
    /// Gets inputs for a node from upstream node results
    /// </summary>
    private Dictionary<string, NodeExecutionResult> GetNodeInputs(
        WorkflowDefinition workflow,
        string nodeId,
        Dictionary<string, NodeExecutionResult> nodeResults)
    {
        var inputs = new Dictionary<string, NodeExecutionResult>();

        var incomingEdges = workflow.Edges.Where(e => e.To == nodeId);
        foreach (var edge in incomingEdges)
        {
            if (nodeResults.TryGetValue(edge.From, out var result))
            {
                inputs[edge.From] = result;
            }
        }

        return inputs;
    }

    /// <summary>
    /// Reports workflow progress
    /// </summary>
    private void ReportProgress(
        IProgress<WorkflowProgress>? progressCallback,
        Guid runId,
        int completedNodes,
        int totalNodes,
        string? currentNodeId,
        int? nodeProgress,
        string? message)
    {
        progressCallback?.Report(new WorkflowProgress
        {
            WorkflowRunId = runId,
            ProgressPercent = totalNodes > 0 ? (completedNodes * 100 / totalNodes) : 0,
            Message = message,
            NodesCompleted = completedNodes,
            TotalNodes = totalNodes,
            CurrentNodeId = currentNodeId,
            CurrentNodeProgress = nodeProgress
        });
    }
}
