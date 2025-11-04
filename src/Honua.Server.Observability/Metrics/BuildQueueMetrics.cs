// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Diagnostics.Metrics;

namespace Honua.Server.Observability.Metrics;

/// <summary>
/// Provides metrics instrumentation for build queue operations.
/// </summary>
public class BuildQueueMetrics
{
    private readonly Counter<long> _buildsEnqueued;
    private readonly ObservableGauge<int> _buildsInQueue;
    private readonly Histogram<double> _buildDuration;
    private readonly Histogram<double> _queueWaitTime;
    private readonly Counter<long> _buildSuccess;
    private readonly Counter<long> _buildFailure;

    private int _currentQueueDepth;

    public BuildQueueMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Honua.BuildQueue");

        _buildsEnqueued = meter.CreateCounter<long>(
            "builds_enqueued_total",
            description: "Total number of builds enqueued");

        _buildsInQueue = meter.CreateObservableGauge(
            "builds_in_queue",
            observeValue: () => _currentQueueDepth,
            description: "Current number of builds in queue");

        _buildDuration = meter.CreateHistogram<double>(
            "build_duration_seconds",
            unit: "s",
            description: "Build processing duration in seconds");

        _queueWaitTime = meter.CreateHistogram<double>(
            "build_queue_wait_time_seconds",
            unit: "s",
            description: "Time builds spend waiting in queue");

        _buildSuccess = meter.CreateCounter<long>(
            "build_success_total",
            description: "Total successful builds");

        _buildFailure = meter.CreateCounter<long>(
            "build_failure_total",
            description: "Total failed builds");
    }

    /// <summary>
    /// Records a build being enqueued.
    /// </summary>
    public void RecordBuildEnqueued(string tier, string architecture)
    {
        _buildsEnqueued.Add(1,
            new KeyValuePair<string, object?>("tier", tier),
            new KeyValuePair<string, object?>("architecture", architecture));
    }

    /// <summary>
    /// Records a build completion with result and duration.
    /// </summary>
    public void RecordBuildCompleted(string tier, bool success, bool fromCache, TimeSpan duration, string? errorType = null)
    {
        if (success)
        {
            _buildSuccess.Add(1,
                new KeyValuePair<string, object?>("tier", tier),
                new KeyValuePair<string, object?>("from_cache", fromCache.ToString()));
        }
        else
        {
            _buildFailure.Add(1,
                new KeyValuePair<string, object?>("tier", tier),
                new KeyValuePair<string, object?>("error_type", errorType ?? "unknown"));
        }

        _buildDuration.Record(duration.TotalSeconds,
            new KeyValuePair<string, object?>("tier", tier),
            new KeyValuePair<string, object?>("success", success.ToString()));
    }

    /// <summary>
    /// Records the time a build spent waiting in queue.
    /// </summary>
    public void RecordQueueWaitTime(string tier, TimeSpan waitTime)
    {
        _queueWaitTime.Record(waitTime.TotalSeconds,
            new KeyValuePair<string, object?>("tier", tier));
    }

    /// <summary>
    /// Updates the current queue depth gauge.
    /// </summary>
    public void UpdateQueueDepth(int count)
    {
        _currentQueueDepth = count;
    }
}
