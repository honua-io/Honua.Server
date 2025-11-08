// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.ETL.Performance;

/// <summary>
/// Implementation of performance metrics using .NET Meters and counters
/// </summary>
public class PerformanceMetrics : IPerformanceMetrics
{
    private readonly Meter _meter;
    private readonly ILogger<PerformanceMetrics> _logger;

    // Counters
    private readonly Counter<long> _workflowsStarted;
    private readonly Counter<long> _workflowsCompleted;
    private readonly Counter<long> _workflowsFailed;
    private readonly Counter<long> _workflowsCancelled;
    private readonly Counter<long> _nodesExecuted;
    private readonly Counter<long> _nodesFailed;
    private readonly Counter<long> _featuresProcessed;
    private readonly Counter<long> _databaseQueries;
    private readonly Counter<long> _cacheHits;
    private readonly Counter<long> _cacheMisses;

    // Histograms
    private readonly Histogram<double> _workflowDuration;
    private readonly Histogram<double> _nodeDuration;
    private readonly Histogram<double> _databaseQueryDuration;
    private readonly Histogram<double> _throughput;

    // Gauges (using ObservableGauge)
    private readonly ConcurrentDictionary<string, int> _queueDepths = new();
    private readonly ConcurrentDictionary<Guid, long> _memoryUsage = new();

    public PerformanceMetrics(ILogger<PerformanceMetrics> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _meter = new Meter("Honua.GeoETL", "1.0.0");

        // Initialize counters
        _workflowsStarted = _meter.CreateCounter<long>(
            "geoetl.workflows.started",
            description: "Number of workflows started");

        _workflowsCompleted = _meter.CreateCounter<long>(
            "geoetl.workflows.completed",
            description: "Number of workflows completed successfully");

        _workflowsFailed = _meter.CreateCounter<long>(
            "geoetl.workflows.failed",
            description: "Number of workflows failed");

        _workflowsCancelled = _meter.CreateCounter<long>(
            "geoetl.workflows.cancelled",
            description: "Number of workflows cancelled");

        _nodesExecuted = _meter.CreateCounter<long>(
            "geoetl.nodes.executed",
            description: "Number of nodes executed");

        _nodesFailed = _meter.CreateCounter<long>(
            "geoetl.nodes.failed",
            description: "Number of nodes failed");

        _featuresProcessed = _meter.CreateCounter<long>(
            "geoetl.features.processed",
            description: "Total features processed");

        _databaseQueries = _meter.CreateCounter<long>(
            "geoetl.database.queries",
            description: "Number of database queries executed");

        _cacheHits = _meter.CreateCounter<long>(
            "geoetl.cache.hits",
            description: "Number of cache hits");

        _cacheMisses = _meter.CreateCounter<long>(
            "geoetl.cache.misses",
            description: "Number of cache misses");

        // Initialize histograms
        _workflowDuration = _meter.CreateHistogram<double>(
            "geoetl.workflow.duration",
            unit: "ms",
            description: "Workflow execution duration in milliseconds");

        _nodeDuration = _meter.CreateHistogram<double>(
            "geoetl.node.duration",
            unit: "ms",
            description: "Node execution duration in milliseconds");

        _databaseQueryDuration = _meter.CreateHistogram<double>(
            "geoetl.database.duration",
            unit: "ms",
            description: "Database query duration in milliseconds");

        _throughput = _meter.CreateHistogram<double>(
            "geoetl.throughput",
            unit: "features/s",
            description: "Features processed per second");

        // Initialize observable gauges
        _meter.CreateObservableGauge(
            "geoetl.queue.depth",
            () => GetQueueDepthMeasurements(),
            description: "Current queue depth");

        _meter.CreateObservableGauge(
            "geoetl.memory.usage",
            () => GetMemoryUsageMeasurements(),
            unit: "bytes",
            description: "Current memory usage per workflow");
    }

    public void RecordWorkflowStarted(Guid workflowRunId)
    {
        _workflowsStarted.Add(1, new KeyValuePair<string, object?>("workflow_run_id", workflowRunId.ToString()));
        _logger.LogDebug("Workflow {WorkflowRunId} started", workflowRunId);
    }

    public void RecordWorkflowCompleted(Guid workflowRunId, TimeSpan duration, int nodesCompleted)
    {
        _workflowsCompleted.Add(1, new KeyValuePair<string, object?>("workflow_run_id", workflowRunId.ToString()));
        _workflowDuration.Record(duration.TotalMilliseconds,
            new KeyValuePair<string, object?>("workflow_run_id", workflowRunId.ToString()),
            new KeyValuePair<string, object?>("status", "completed"));

        _logger.LogInformation(
            "Workflow {WorkflowRunId} completed in {DurationMs}ms with {NodesCompleted} nodes",
            workflowRunId,
            duration.TotalMilliseconds,
            nodesCompleted);

        // Clean up memory tracking
        _memoryUsage.TryRemove(workflowRunId, out _);
    }

    public void RecordWorkflowFailed(Guid workflowRunId, TimeSpan duration)
    {
        _workflowsFailed.Add(1, new KeyValuePair<string, object?>("workflow_run_id", workflowRunId.ToString()));
        _workflowDuration.Record(duration.TotalMilliseconds,
            new KeyValuePair<string, object?>("workflow_run_id", workflowRunId.ToString()),
            new KeyValuePair<string, object?>("status", "failed"));

        _logger.LogWarning("Workflow {WorkflowRunId} failed after {DurationMs}ms", workflowRunId, duration.TotalMilliseconds);

        // Clean up memory tracking
        _memoryUsage.TryRemove(workflowRunId, out _);
    }

    public void RecordWorkflowCancelled(Guid workflowRunId, TimeSpan duration)
    {
        _workflowsCancelled.Add(1, new KeyValuePair<string, object?>("workflow_run_id", workflowRunId.ToString()));
        _workflowDuration.Record(duration.TotalMilliseconds,
            new KeyValuePair<string, object?>("workflow_run_id", workflowRunId.ToString()),
            new KeyValuePair<string, object?>("status", "cancelled"));

        _logger.LogWarning("Workflow {WorkflowRunId} cancelled after {DurationMs}ms", workflowRunId, duration.TotalMilliseconds);

        // Clean up memory tracking
        _memoryUsage.TryRemove(workflowRunId, out _);
    }

    public void RecordNodeStarted(Guid workflowRunId, string nodeId, string nodeType)
    {
        _logger.LogDebug("Node {NodeId} ({NodeType}) started in workflow {WorkflowRunId}", nodeId, nodeType, workflowRunId);
    }

    public void RecordNodeCompleted(Guid workflowRunId, string nodeId, string nodeType, TimeSpan duration, long featuresProcessed)
    {
        _nodesExecuted.Add(1,
            new("workflow_run_id", workflowRunId.ToString()),
            new("node_id", nodeId),
            new("node_type", nodeType));

        _nodeDuration.Record(duration.TotalMilliseconds,
            new("node_type", nodeType),
            new("status", "completed"));

        if (featuresProcessed > 0)
        {
            _featuresProcessed.Add(featuresProcessed,
                new KeyValuePair<string, object?>("node_type", nodeType));
        }

        _logger.LogInformation(
            "Node {NodeId} ({NodeType}) completed in {DurationMs}ms, processed {FeaturesProcessed} features",
            nodeId,
            nodeType,
            duration.TotalMilliseconds,
            featuresProcessed);
    }

    public void RecordNodeFailed(Guid workflowRunId, string nodeId, string nodeType, TimeSpan duration)
    {
        _nodesFailed.Add(1,
            new("workflow_run_id", workflowRunId.ToString()),
            new("node_id", nodeId),
            new("node_type", nodeType));

        _nodeDuration.Record(duration.TotalMilliseconds,
            new("node_type", nodeType),
            new("status", "failed"));

        _logger.LogWarning(
            "Node {NodeId} ({NodeType}) failed after {DurationMs}ms",
            nodeId,
            nodeType,
            duration.TotalMilliseconds);
    }

    public void RecordDatabaseQuery(string queryType, TimeSpan duration, bool success)
    {
        _databaseQueries.Add(1,
            new("query_type", queryType),
            new("success", success.ToString()));

        _databaseQueryDuration.Record(duration.TotalMilliseconds,
            new("query_type", queryType),
            new("success", success.ToString()));

        if (duration.TotalMilliseconds > 1000)
        {
            _logger.LogWarning(
                "Slow database query: {QueryType} took {DurationMs}ms",
                queryType,
                duration.TotalMilliseconds);
        }
    }

    public void RecordCacheHit(string cacheType, string key)
    {
        _cacheHits.Add(1, new KeyValuePair<string, object?>("cache_type", cacheType));
        _logger.LogTrace("Cache hit: {CacheType}:{Key}", cacheType, key);
    }

    public void RecordCacheMiss(string cacheType, string key)
    {
        _cacheMisses.Add(1, new KeyValuePair<string, object?>("cache_type", cacheType));
        _logger.LogTrace("Cache miss: {CacheType}:{Key}", cacheType, key);
    }

    public void RecordThroughput(Guid workflowRunId, string nodeId, double featuresPerSecond)
    {
        _throughput.Record(featuresPerSecond,
            new("workflow_run_id", workflowRunId.ToString()),
            new("node_id", nodeId));

        _logger.LogDebug(
            "Node {NodeId} throughput: {FeaturesPerSecond:F2} features/s",
            nodeId,
            featuresPerSecond);
    }

    public void RecordMemoryUsage(Guid workflowRunId, long bytesAllocated)
    {
        _memoryUsage.AddOrUpdate(workflowRunId, bytesAllocated, (_, __) => bytesAllocated);
    }

    public void RecordQueueDepth(string queueName, int depth)
    {
        _queueDepths.AddOrUpdate(queueName, depth, (_, __) => depth);
    }

    private System.Collections.Generic.IEnumerable<Measurement<int>> GetQueueDepthMeasurements()
    {
        foreach (var kvp in _queueDepths)
        {
            yield return new Measurement<int>(kvp.Value, new KeyValuePair<string, object?>[] { new("queue_name", kvp.Key) });
        }
    }

    private System.Collections.Generic.IEnumerable<Measurement<long>> GetMemoryUsageMeasurements()
    {
        foreach (var kvp in _memoryUsage)
        {
            yield return new Measurement<long>(kvp.Value, new KeyValuePair<string, object?>[] { new("workflow_run_id", kvp.Key.ToString()) });
        }
    }
}
