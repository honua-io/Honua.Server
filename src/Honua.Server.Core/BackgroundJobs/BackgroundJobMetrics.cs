// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics.Metrics;

namespace Honua.Server.Core.BackgroundJobs;

/// <summary>
/// Metrics for background job processing.
/// Integrates with OpenTelemetry for monitoring and alerting.
/// </summary>
public sealed class BackgroundJobMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _jobsEnqueued;
    private readonly Counter<long> _jobsCompleted;
    private readonly Counter<long> _jobsFailed;
    private readonly Counter<long> _jobsRetried;
    private readonly Histogram<double> _jobDuration;
    private readonly Histogram<double> _queueWaitTime;
    private readonly ObservableGauge<long> _queueDepth;
    private Func<long>? _queueDepthProvider;

    public BackgroundJobMetrics()
    {
        _meter = new Meter("Honua.BackgroundJobs", "1.0.0");

        // Counters
        _jobsEnqueued = _meter.CreateCounter<long>(
            "background_jobs.enqueued",
            unit: "{job}",
            description: "Total number of jobs enqueued for background processing");

        _jobsCompleted = _meter.CreateCounter<long>(
            "background_jobs.completed",
            unit: "{job}",
            description: "Total number of jobs completed successfully");

        _jobsFailed = _meter.CreateCounter<long>(
            "background_jobs.failed",
            unit: "{job}",
            description: "Total number of jobs that failed after all retries");

        _jobsRetried = _meter.CreateCounter<long>(
            "background_jobs.retried",
            unit: "{job}",
            description: "Total number of job retry attempts");

        // Histograms
        _jobDuration = _meter.CreateHistogram<double>(
            "background_jobs.duration",
            unit: "s",
            description: "Duration of job processing in seconds");

        _queueWaitTime = _meter.CreateHistogram<double>(
            "background_jobs.queue_wait_time",
            unit: "s",
            description: "Time jobs spend waiting in queue before processing");

        // Gauge (observable)
        _queueDepth = _meter.CreateObservableGauge<long>(
            "background_jobs.queue_depth",
            observeValue: () => _queueDepthProvider?.Invoke() ?? 0,
            unit: "{job}",
            description: "Current number of pending jobs in queue");
    }

    /// <summary>
    /// Sets the queue depth provider function for the gauge metric.
    /// </summary>
    /// <param name="provider">Function that returns current queue depth</param>
    public void SetQueueDepthProvider(Func<long> provider)
    {
        _queueDepthProvider = provider;
    }

    /// <summary>
    /// Records a job enqueue event.
    /// </summary>
    /// <param name="jobType">Type of job</param>
    /// <param name="priority">Job priority</param>
    public void RecordJobEnqueued(string jobType, int priority)
    {
        _jobsEnqueued.Add(1, new("job.type", jobType), new("job.priority", priority));
    }

    /// <summary>
    /// Records a successful job completion.
    /// </summary>
    /// <param name="jobType">Type of job</param>
    /// <param name="duration">Processing duration</param>
    /// <param name="queueWait">Time spent waiting in queue</param>
    public void RecordJobCompleted(string jobType, TimeSpan duration, TimeSpan queueWait)
    {
        _jobsCompleted.Add(1, new("job.type", jobType));
        _jobDuration.Record(duration.TotalSeconds, new("job.type", jobType), new("status", "completed"));
        _queueWaitTime.Record(queueWait.TotalSeconds, new("job.type", jobType));
    }

    /// <summary>
    /// Records a job failure.
    /// </summary>
    /// <param name="jobType">Type of job</param>
    /// <param name="errorType">Type/category of error</param>
    /// <param name="isTransient">Whether error is transient (retriable)</param>
    /// <param name="duration">Processing duration before failure</param>
    public void RecordJobFailed(string jobType, string errorType, bool isTransient, TimeSpan duration)
    {
        _jobsFailed.Add(
            1,
            new("job.type", jobType),
            new("error.type", errorType),
            new("error.transient", isTransient));

        _jobDuration.Record(
            duration.TotalSeconds,
            new("job.type", jobType),
            new("status", "failed"));
    }

    /// <summary>
    /// Records a job retry attempt.
    /// </summary>
    /// <param name="jobType">Type of job</param>
    /// <param name="retryCount">Current retry attempt number</param>
    /// <param name="errorType">Type/category of error that caused retry</param>
    public void RecordJobRetry(string jobType, int retryCount, string errorType)
    {
        _jobsRetried.Add(
            1,
            new("job.type", jobType),
            new("retry.count", retryCount),
            new("error.type", errorType));
    }

    public void Dispose()
    {
        _meter?.Dispose();
    }
}

/// <summary>
/// Extension methods for BackgroundJobMetrics
/// </summary>
public static class BackgroundJobMetricsExtensions
{
    /// <summary>
    /// Gets the error type name from an exception.
    /// </summary>
    public static string GetErrorType(this Exception exception)
    {
        return exception?.GetType().Name ?? "Unknown";
    }
}
