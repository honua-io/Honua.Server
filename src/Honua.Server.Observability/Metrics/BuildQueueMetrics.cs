// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Diagnostics.Metrics;

namespace Honua.Server.Observability.Metrics;

/// <summary>
/// Provides metrics instrumentation for build queue operations.
/// </summary>
public class BuildQueueMetrics
{
    private readonly Counter<long> buildsEnqueued;
    private readonly ObservableGauge<int> buildsInQueue;
    private readonly Histogram<double> buildDuration;
    private readonly Histogram<double> queueWaitTime;
    private readonly Counter<long> buildSuccess;
    private readonly Counter<long> buildFailure;

    private int currentQueueDepth;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildQueueMetrics"/> class.
    /// </summary>
    /// <param name="meterFactory">The meter factory.</param>
    public BuildQueueMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Honua.BuildQueue");

        this.buildsEnqueued = meter.CreateCounter<long>(
            "builds_enqueued_total",
            description: "Total number of builds enqueued");

        this.buildsInQueue = meter.CreateObservableGauge(
            "builds_in_queue",
            observeValue: () => this.currentQueueDepth,
            description: "Current number of builds in queue");

        this.buildDuration = meter.CreateHistogram<double>(
            "build_duration_seconds",
            unit: "s",
            description: "Build processing duration in seconds");

        this.queueWaitTime = meter.CreateHistogram<double>(
            "build_queue_wait_time_seconds",
            unit: "s",
            description: "Time builds spend waiting in queue");

        this.buildSuccess = meter.CreateCounter<long>(
            "build_success_total",
            description: "Total successful builds");

        this.buildFailure = meter.CreateCounter<long>(
            "build_failure_total",
            description: "Total failed builds");
    }

    /// <summary>
    /// Records a build being enqueued.
    /// </summary>
    /// <param name="tier">The build tier.</param>
    /// <param name="architecture">The architecture.</param>
    public void RecordBuildEnqueued(string tier, string architecture)
    {
        this.buildsEnqueued.Add(1,
            new KeyValuePair<string, object?>("tier", tier),
            new KeyValuePair<string, object?>("architecture", architecture));
    }

    /// <summary>
    /// Records a build completion with result and duration.
    /// </summary>
    /// <param name="tier">The build tier.</param>
    /// <param name="success">Whether the build succeeded.</param>
    /// <param name="fromCache">Whether the build was served from cache.</param>
    /// <param name="duration">The build duration.</param>
    /// <param name="errorType">The error type if failed.</param>
    public void RecordBuildCompleted(string tier, bool success, bool fromCache, TimeSpan duration, string? errorType = null)
    {
        if (success)
        {
            this.buildSuccess.Add(1,
                new KeyValuePair<string, object?>("tier", tier),
                new KeyValuePair<string, object?>("from_cache", fromCache.ToString()));
        }
        else
        {
            this.buildFailure.Add(1,
                new KeyValuePair<string, object?>("tier", tier),
                new KeyValuePair<string, object?>("error_type", errorType ?? "unknown"));
        }

        this.buildDuration.Record(duration.TotalSeconds,
            new KeyValuePair<string, object?>("tier", tier),
            new KeyValuePair<string, object?>("success", success.ToString()));
    }

    /// <summary>
    /// Records the time a build spent waiting in queue.
    /// </summary>
    /// <param name="tier">The build tier.</param>
    /// <param name="waitTime">The wait time.</param>
    public void RecordQueueWaitTime(string tier, TimeSpan waitTime)
    {
        this.queueWaitTime.Record(waitTime.TotalSeconds,
            new KeyValuePair<string, object?>("tier", tier));
    }

    /// <summary>
    /// Updates the current queue depth gauge.
    /// </summary>
    /// <param name="count">The current queue depth.</param>
    public void UpdateQueueDepth(int count)
    {
        this.currentQueueDepth = count;
    }
}
