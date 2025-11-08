// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Enterprise.ETL.Performance;

/// <summary>
/// Interface for collecting performance metrics
/// </summary>
public interface IPerformanceMetrics
{
    /// <summary>
    /// Records workflow started event
    /// </summary>
    void RecordWorkflowStarted(Guid workflowRunId);

    /// <summary>
    /// Records workflow completed event
    /// </summary>
    void RecordWorkflowCompleted(Guid workflowRunId, TimeSpan duration, int nodesCompleted);

    /// <summary>
    /// Records workflow failed event
    /// </summary>
    void RecordWorkflowFailed(Guid workflowRunId, TimeSpan duration);

    /// <summary>
    /// Records workflow cancelled event
    /// </summary>
    void RecordWorkflowCancelled(Guid workflowRunId, TimeSpan duration);

    /// <summary>
    /// Records node started event
    /// </summary>
    void RecordNodeStarted(Guid workflowRunId, string nodeId, string nodeType);

    /// <summary>
    /// Records node completed event
    /// </summary>
    void RecordNodeCompleted(Guid workflowRunId, string nodeId, string nodeType, TimeSpan duration, long featuresProcessed);

    /// <summary>
    /// Records node failed event
    /// </summary>
    void RecordNodeFailed(Guid workflowRunId, string nodeId, string nodeType, TimeSpan duration);

    /// <summary>
    /// Records database query execution
    /// </summary>
    void RecordDatabaseQuery(string queryType, TimeSpan duration, bool success);

    /// <summary>
    /// Records cache hit
    /// </summary>
    void RecordCacheHit(string cacheType, string key);

    /// <summary>
    /// Records cache miss
    /// </summary>
    void RecordCacheMiss(string cacheType, string key);

    /// <summary>
    /// Records throughput (features per second)
    /// </summary>
    void RecordThroughput(Guid workflowRunId, string nodeId, double featuresPerSecond);

    /// <summary>
    /// Records memory usage
    /// </summary>
    void RecordMemoryUsage(Guid workflowRunId, long bytesAllocated);

    /// <summary>
    /// Records queue depth
    /// </summary>
    void RecordQueueDepth(string queueName, int depth);
}
