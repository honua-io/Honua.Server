// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Honua.Server.Enterprise.ETL.Models;
using Honua.Server.Enterprise.ETL.Nodes;
using Honua.Server.Enterprise.ETL.Performance;
using Honua.Server.Enterprise.ETL.Progress;
using Honua.Server.Enterprise.ETL.Stores;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.ETL.Engine;

/// <summary>
/// Parallel workflow execution engine that executes independent nodes concurrently
/// </summary>
public class ParallelWorkflowEngine : IWorkflowEngine
{
    private readonly IWorkflowStore _workflowStore;
    private readonly IWorkflowNodeRegistry _nodeRegistry;
    private readonly IWorkflowProgressBroadcaster? _progressBroadcaster;
    private readonly IPerformanceMetrics? _performanceMetrics;
    private readonly ILogger<ParallelWorkflowEngine> _logger;
    private readonly ParallelWorkflowEngineOptions _options;

    public ParallelWorkflowEngine(
        IWorkflowStore workflowStore,
        IWorkflowNodeRegistry nodeRegistry,
        ILogger<ParallelWorkflowEngine> logger,
        ParallelWorkflowEngineOptions? options = null,
        IWorkflowProgressBroadcaster? progressBroadcaster = null,
        IPerformanceMetrics? performanceMetrics = null)
    {
        _workflowStore = workflowStore ?? throw new ArgumentNullException(nameof(workflowStore));
        _nodeRegistry = nodeRegistry ?? throw new ArgumentNullException(nameof(nodeRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _progressBroadcaster = progressBroadcaster;
        _performanceMetrics = performanceMetrics;
        _options = options ?? new ParallelWorkflowEngineOptions();
    }

    public async Task<WorkflowValidationResult> ValidateAsync(
        WorkflowDefinition workflow,
        Dictionary<string, object>? parameterValues = null,
        CancellationToken cancellationToken = default)
    {
        // Use same validation logic as base engine
        var result = new WorkflowValidationResult { IsValid = true };

        try
        {
            // 1. Validate DAG structure
            result.DagValidation = ValidateDag(workflow);
            if (!result.DagValidation.IsValid)
            {
                result.IsValid = false;
                if (result.DagValidation.Cycles?.Any() == true)
                {
                    result.Errors.Add($"Workflow contains cycles: {string.Join(", ", result.DagValidation.Cycles.Select(c => string.Join(" → ", c)))}");
                }
                if (result.DagValidation.DisconnectedNodes?.Any() == true)
                {
                    result.Warnings.Add($"Disconnected nodes: {string.Join(", ", result.DagValidation.DisconnectedNodes)}");
                }
                if (result.DagValidation.MissingNodes?.Any() == true)
                {
                    result.Errors.Add($"Missing node references: {string.Join(", ", result.DagValidation.MissingNodes)}");
                }
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
            // Get parallel execution levels
            var dagValidation = ValidateDag(workflow);
            if (!dagValidation.IsValid || dagValidation.ExecutionOrder == null)
            {
                _logger.LogWarning("Cannot estimate workflow {WorkflowId}: invalid DAG", workflow.Id);
                return estimate;
            }

            var parallelLevels = AnalyzeParallelism(workflow, dagValidation.ExecutionOrder);

            // Estimate each node
            long totalDuration = 0;
            long peakMemory = 0;
            decimal totalCost = 0;

            foreach (var level in parallelLevels)
            {
                long levelMaxDuration = 0;
                long levelMemory = 0;

                foreach (var nodeId in level)
                {
                    var node = workflow.Nodes.FirstOrDefault(n => n.Id == nodeId);
                    if (node == null) continue;

                    var nodeImpl = _nodeRegistry.GetNode(node.Type);
                    if (nodeImpl == null) continue;

                    var nodeEstimate = await nodeImpl.EstimateAsync(node, parameterValues, cancellationToken);
                    estimate.NodeEstimates[nodeId] = nodeEstimate;

                    levelMaxDuration = Math.Max(levelMaxDuration, nodeEstimate.EstimatedDurationSeconds);
                    levelMemory += nodeEstimate.EstimatedMemoryMB;
                    totalCost += nodeEstimate.EstimatedCostUsd ?? 0;
                }

                totalDuration += levelMaxDuration; // Parallel execution
                peakMemory = Math.Max(peakMemory, levelMemory);
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

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (options.Timeout.HasValue)
        {
            cts.CancelAfter(options.Timeout.Value);
        }

        try
        {
            _logger.LogInformation("Starting parallel workflow execution {RunId} for workflow {WorkflowId}", runId, workflow.Id);
            _performanceMetrics?.RecordWorkflowStarted(runId);

            // Persist run
            await _workflowStore.CreateRunAsync(run, cancellationToken);

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

            // Analyze parallelism
            var executionOrder = validation.DagValidation!.ExecutionOrder!;
            var parallelLevels = AnalyzeParallelism(workflow, executionOrder);

            _logger.LogInformation("Workflow {RunId} will execute in {Levels} parallel levels", runId, parallelLevels.Count);

            // Execute levels in parallel
            var nodeResults = new ConcurrentDictionary<string, NodeExecutionResult>();
            var nodeRuns = new ConcurrentDictionary<string, NodeRun>();
            int completedNodes = 0;
            int totalNodes = workflow.Nodes.Count;

            foreach (var level in parallelLevels)
            {
                _logger.LogInformation("Executing level with {NodeCount} nodes in parallel", level.Count);

                // Execute all nodes in this level in parallel
                var levelTasks = level.Select(nodeId => ExecuteNodeAsync(
                    workflow,
                    nodeId,
                    run,
                    nodeResults,
                    nodeRuns,
                    options,
                    ref completedNodes,
                    totalNodes,
                    cts.Token
                )).ToList();

                var levelResults = await Task.WhenAll(levelTasks);

                // Check for failures
                if (levelResults.Any(r => !r.Success) && !options.ContinueOnError)
                {
                    var failedNode = levelResults.First(r => !r.Success);
                    run.Status = WorkflowRunStatus.Failed;
                    run.ErrorMessage = $"Node failed: {failedNode.ErrorMessage}";
                    run.CompletedAt = DateTimeOffset.UtcNow;
                    await _workflowStore.UpdateRunAsync(run, cancellationToken);

                    if (_progressBroadcaster != null)
                    {
                        await _progressBroadcaster.BroadcastWorkflowFailedAsync(runId, run.ErrorMessage);
                    }

                    _performanceMetrics?.RecordWorkflowFailed(runId, stopwatch.Elapsed);
                    return run;
                }
            }

            // Workflow completed successfully
            stopwatch.Stop();
            run.Status = WorkflowRunStatus.Completed;
            run.CompletedAt = DateTimeOffset.UtcNow;
            run.NodeRuns = nodeRuns.Values.ToList();
            await _workflowStore.UpdateRunAsync(run, cancellationToken);

            _logger.LogInformation(
                "Parallel workflow execution {RunId} completed in {DurationMs}ms",
                runId,
                stopwatch.ElapsedMilliseconds);

            if (_progressBroadcaster != null)
            {
                await _progressBroadcaster.BroadcastWorkflowCompletedAsync(runId, new WorkflowCompletedSummary
                {
                    CompletedAt = run.CompletedAt.Value,
                    TotalDurationMs = stopwatch.ElapsedMilliseconds,
                    NodesCompleted = completedNodes,
                    TotalNodes = totalNodes,
                    TotalFeaturesProcessed = run.NodeRuns.Sum(n => n.FeaturesProcessed),
                    TotalBytesRead = null,
                    TotalBytesWritten = null
                });
            }

            _performanceMetrics?.RecordWorkflowCompleted(runId, stopwatch.Elapsed, completedNodes);

            return run;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Workflow execution {RunId} was cancelled", runId);
            run.Status = WorkflowRunStatus.Cancelled;
            run.CompletedAt = DateTimeOffset.UtcNow;
            await _workflowStore.UpdateRunAsync(run, cancellationToken);

            if (_progressBroadcaster != null)
            {
                await _progressBroadcaster.BroadcastWorkflowCancelledAsync(runId, "Workflow was cancelled by user or system");
            }

            _performanceMetrics?.RecordWorkflowCancelled(runId, stopwatch.Elapsed);
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

            if (_progressBroadcaster != null)
            {
                await _progressBroadcaster.BroadcastWorkflowFailedAsync(runId, ex.Message);
            }

            _performanceMetrics?.RecordWorkflowFailed(runId, stopwatch.Elapsed);
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
    /// Executes a single node with proper error handling and metrics
    /// </summary>
    private async Task<NodeExecutionResult> ExecuteNodeAsync(
        WorkflowDefinition workflow,
        string nodeId,
        WorkflowRun run,
        ConcurrentDictionary<string, NodeExecutionResult> nodeResults,
        ConcurrentDictionary<string, NodeRun> nodeRuns,
        WorkflowExecutionOptions options,
        ref int completedNodes,
        int totalNodes,
        CancellationToken cancellationToken)
    {
        var node = workflow.Nodes.First(n => n.Id == nodeId);
        var nodeImpl = _nodeRegistry.GetNode(node.Type);
        if (nodeImpl == null)
        {
            return NodeExecutionResult.Fail($"Node type '{node.Type}' not found");
        }

        var nodeRun = new NodeRun
        {
            WorkflowRunId = run.Id,
            NodeId = nodeId,
            NodeType = node.Type,
            Status = NodeRunStatus.Running,
            StartedAt = DateTimeOffset.UtcNow
        };
        nodeRuns[nodeId] = nodeRun;

        _logger.LogInformation("Executing node {NodeId} ({NodeType}) in run {RunId}", nodeId, node.Type, run.Id);
        _performanceMetrics?.RecordNodeStarted(run.Id, nodeId, node.Type);

        if (_progressBroadcaster != null)
        {
            await _progressBroadcaster.BroadcastNodeStartedAsync(
                run.Id,
                nodeId,
                node.Name ?? nodeId,
                node.Type);
        }

        var nodeStopwatch = Stopwatch.StartNew();

        try
        {
            // Get inputs from upstream nodes
            var inputs = GetNodeInputs(workflow, nodeId, nodeResults);

            // Execute node
            var context = new NodeExecutionContext
            {
                WorkflowRunId = run.Id,
                NodeRunId = nodeRun.Id,
                NodeDefinition = node,
                Parameters = options.ParameterValues ?? new Dictionary<string, object>(),
                Inputs = inputs,
                TenantId = options.TenantId,
                UserId = options.UserId,
                State = run.State ?? new Dictionary<string, object>(),
                ProgressCallback = new Progress<NodeProgress>(async p =>
                {
                    if (_progressBroadcaster != null)
                    {
                        await _progressBroadcaster.BroadcastNodeProgressAsync(
                            run.Id,
                            nodeId,
                            p.Percentage,
                            p.Message,
                            p.FeaturesProcessed,
                            p.TotalFeatures);
                    }
                })
            };

            var result = await nodeImpl.ExecuteAsync(context, cancellationToken);
            nodeStopwatch.Stop();

            // Update node run
            nodeRun.CompletedAt = DateTimeOffset.UtcNow;
            nodeRun.DurationMs = nodeStopwatch.ElapsedMilliseconds;
            nodeRun.FeaturesProcessed = result.FeaturesProcessed;
            nodeRun.Status = result.Success ? NodeRunStatus.Completed : NodeRunStatus.Failed;
            nodeRun.ErrorMessage = result.ErrorMessage;
            nodeRun.Output = result.Data;

            if (result.Success)
            {
                nodeResults[nodeId] = result;
                Interlocked.Increment(ref completedNodes);

                if (_progressBroadcaster != null)
                {
                    await _progressBroadcaster.BroadcastNodeCompletedAsync(
                        run.Id,
                        nodeId,
                        new NodeCompletedResult
                        {
                            DurationMs = nodeStopwatch.ElapsedMilliseconds,
                            FeaturesProcessed = result.FeaturesProcessed,
                            BytesRead = null,
                            BytesWritten = null
                        });
                }

                _performanceMetrics?.RecordNodeCompleted(
                    run.Id,
                    nodeId,
                    node.Type,
                    nodeStopwatch.Elapsed,
                    result.FeaturesProcessed ?? 0);
            }
            else
            {
                _logger.LogError("Node {NodeId} failed: {Error}", nodeId, result.ErrorMessage);

                if (_progressBroadcaster != null)
                {
                    await _progressBroadcaster.BroadcastNodeFailedAsync(
                        run.Id,
                        nodeId,
                        result.ErrorMessage ?? "Unknown error");
                }

                _performanceMetrics?.RecordNodeFailed(run.Id, nodeId, node.Type, nodeStopwatch.Elapsed);
            }

            return result;
        }
        catch (Exception ex)
        {
            nodeStopwatch.Stop();
            _logger.LogError(ex, "Node {NodeId} threw exception", nodeId);

            nodeRun.CompletedAt = DateTimeOffset.UtcNow;
            nodeRun.DurationMs = nodeStopwatch.ElapsedMilliseconds;
            nodeRun.Status = NodeRunStatus.Failed;
            nodeRun.ErrorMessage = ex.Message;

            _performanceMetrics?.RecordNodeFailed(run.Id, nodeId, node.Type, nodeStopwatch.Elapsed);

            return NodeExecutionResult.Fail(ex.Message, ex.StackTrace);
        }
    }

    /// <summary>
    /// Analyzes workflow DAG to identify parallel execution levels
    /// </summary>
    private List<List<string>> AnalyzeParallelism(WorkflowDefinition workflow, List<string> executionOrder)
    {
        var levels = new List<List<string>>();
        var inDegree = new Dictionary<string, int>();
        var adjacency = new Dictionary<string, List<string>>();

        // Build adjacency list and in-degree map
        foreach (var node in workflow.Nodes)
        {
            adjacency[node.Id] = new List<string>();
            inDegree[node.Id] = 0;
        }

        foreach (var edge in workflow.Edges)
        {
            if (adjacency.ContainsKey(edge.From) && inDegree.ContainsKey(edge.To))
            {
                adjacency[edge.From].Add(edge.To);
                inDegree[edge.To]++;
            }
        }

        // Group nodes by execution level
        var remaining = new HashSet<string>(executionOrder);
        var currentInDegree = new Dictionary<string, int>(inDegree);

        while (remaining.Any())
        {
            // Find all nodes with no dependencies (in-degree = 0)
            var level = remaining.Where(n => currentInDegree[n] == 0).ToList();

            if (!level.Any())
            {
                // Shouldn't happen if DAG is valid, but handle gracefully
                level = remaining.Take(1).ToList();
            }

            // Apply max parallelism limit
            if (_options.MaxParallelNodes > 0 && level.Count > _options.MaxParallelNodes)
            {
                // Split into chunks respecting max parallelism
                for (int i = 0; i < level.Count; i += _options.MaxParallelNodes)
                {
                    var chunk = level.Skip(i).Take(_options.MaxParallelNodes).ToList();
                    levels.Add(chunk);

                    // Update in-degrees for next chunk
                    foreach (var nodeId in chunk)
                    {
                        remaining.Remove(nodeId);
                        foreach (var neighbor in adjacency[nodeId])
                        {
                            currentInDegree[neighbor]--;
                        }
                    }
                }
            }
            else
            {
                levels.Add(level);

                // Remove this level and update in-degrees
                foreach (var nodeId in level)
                {
                    remaining.Remove(nodeId);
                    foreach (var neighbor in adjacency[nodeId])
                    {
                        currentInDegree[neighbor]--;
                    }
                }
            }
        }

        return levels;
    }

    /// <summary>
    /// Gets inputs for a node from upstream node results
    /// </summary>
    private Dictionary<string, NodeExecutionResult> GetNodeInputs(
        WorkflowDefinition workflow,
        string nodeId,
        ConcurrentDictionary<string, NodeExecutionResult> nodeResults)
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
}

/// <summary>
/// Configuration options for parallel workflow engine
/// </summary>
public class ParallelWorkflowEngineOptions
{
    /// <summary>
    /// Maximum number of nodes to execute in parallel (default: CPU cores)
    /// Set to 0 for unlimited parallelism
    /// </summary>
    public int MaxParallelNodes { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Maximum memory per workflow in MB (for resource management)
    /// Set to 0 for no limit
    /// </summary>
    public long MaxMemoryPerWorkflowMB { get; set; } = 512;

    /// <summary>
    /// Enable resource-aware scheduling
    /// </summary>
    public bool EnableResourceAwareScheduling { get; set; } = true;
}
