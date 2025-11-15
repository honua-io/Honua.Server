// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.Geoprocessing;

/// <summary>
/// OpenTelemetry metrics for geoprocessing operations and fire-and-forget background tasks.
/// Tracks job processing, alert delivery, progress updates, and SLA compliance.
/// </summary>
public interface IGeoprocessingMetrics
{
    // Job processing metrics
    void RecordJobStarted(string processId, int priority, string tier);
    void RecordJobCompleted(string processId, int priority, string tier, TimeSpan duration, long featuresProcessed);
    void RecordJobFailed(string processId, int priority, string tier, TimeSpan duration, string errorType, bool isTransient);
    void RecordJobTimeout(string processId, int priority, string tier, TimeSpan duration);
    void RecordJobRetry(string processId, int priority, int retryCount, string errorType);

    // SLA metrics
    void RecordSlaCompliance(string processId, int priority, TimeSpan queueWait, bool breached);
    void RecordSlaBreach(string processId, int priority, TimeSpan queueWait, TimeSpan slaThreshold);

    // Fire-and-forget alert delivery metrics
    void RecordAlertAttempt(string alertType, string severity);
    void RecordAlertSuccess(string alertType, string severity, TimeSpan duration);
    void RecordAlertFailure(string alertType, string severity, string errorType);

    // Fire-and-forget progress update metrics
    void RecordProgressUpdateAttempt(string processId);
    void RecordProgressUpdateSuccess(string processId, int progressPercent);
    void RecordProgressUpdateFailure(string processId, string errorType);
    void RecordProgressUpdateThrottled(string processId, string throttleReason);

    // Background task metrics
    void RecordBackgroundTaskStarted(string taskType);
    void RecordBackgroundTaskCompleted(string taskType, TimeSpan duration);
    void RecordBackgroundTaskFailed(string taskType, string errorType);

    // Concurrency and resource metrics
    void RecordActiveJobCount(int count);
    void RecordQueueDepth(int depth);
}

/// <summary>
/// Implementation of geoprocessing metrics using OpenTelemetry.
/// Provides comprehensive observability for geoprocessing operations and fire-and-forget patterns.
/// </summary>
public sealed class GeoprocessingMetrics : IGeoprocessingMetrics, IDisposable
{
    private readonly Meter _meter;
    private readonly ILogger<GeoprocessingMetrics>? _logger;

    // Job processing metrics
    private readonly Counter<long> _jobStartedCounter;
    private readonly Counter<long> _jobCompletedCounter;
    private readonly Counter<long> _jobFailedCounter;
    private readonly Counter<long> _jobTimeoutCounter;
    private readonly Counter<long> _jobRetryCounter;
    private readonly Histogram<double> _jobDuration;
    private readonly Histogram<long> _featuresProcessed;

    // SLA metrics
    private readonly Histogram<double> _queueWaitTime;
    private readonly Counter<long> _slaComplianceCounter;
    private readonly Counter<long> _slaBreachCounter;

    // Alert delivery metrics (fire-and-forget)
    private readonly Counter<long> _alertAttemptCounter;
    private readonly Counter<long> _alertSuccessCounter;
    private readonly Counter<long> _alertFailureCounter;
    private readonly Histogram<double> _alertDuration;

    // Progress update metrics (fire-and-forget)
    private readonly Counter<long> _progressUpdateAttemptCounter;
    private readonly Counter<long> _progressUpdateSuccessCounter;
    private readonly Counter<long> _progressUpdateFailureCounter;
    private readonly Counter<long> _progressUpdateThrottledCounter;

    // Background task metrics
    private readonly Counter<long> _backgroundTaskStartedCounter;
    private readonly Counter<long> _backgroundTaskCompletedCounter;
    private readonly Counter<long> _backgroundTaskFailedCounter;
    private readonly Histogram<double> _backgroundTaskDuration;

    // Resource metrics
    private readonly ObservableGauge<int> _activeJobGauge;
    private readonly ObservableGauge<int> _queueDepthGauge;
    private int _currentActiveJobs;
    private int _currentQueueDepth;

    public GeoprocessingMetrics(ILogger<GeoprocessingMetrics>? logger = null)
    {
        _logger = logger;
        _meter = new Meter("Honua.Server.Geoprocessing", "1.0.0");

        // Job processing metrics
        _jobStartedCounter = _meter.CreateCounter<long>(
            "honua.geoprocessing.jobs.started",
            unit: "{job}",
            description: "Number of geoprocessing jobs started");

        _jobCompletedCounter = _meter.CreateCounter<long>(
            "honua.geoprocessing.jobs.completed",
            unit: "{job}",
            description: "Number of geoprocessing jobs completed successfully");

        _jobFailedCounter = _meter.CreateCounter<long>(
            "honua.geoprocessing.jobs.failed",
            unit: "{job}",
            description: "Number of geoprocessing jobs failed");

        _jobTimeoutCounter = _meter.CreateCounter<long>(
            "honua.geoprocessing.jobs.timeout",
            unit: "{job}",
            description: "Number of geoprocessing jobs that timed out");

        _jobRetryCounter = _meter.CreateCounter<long>(
            "honua.geoprocessing.jobs.retries",
            unit: "{retry}",
            description: "Number of job retry attempts");

        _jobDuration = _meter.CreateHistogram<double>(
            "honua.geoprocessing.job.duration",
            unit: "ms",
            description: "Job execution duration in milliseconds");

        _featuresProcessed = _meter.CreateHistogram<long>(
            "honua.geoprocessing.job.features_processed",
            unit: "{feature}",
            description: "Number of features processed per job");

        // SLA metrics
        _queueWaitTime = _meter.CreateHistogram<double>(
            "honua.geoprocessing.job.queue_wait",
            unit: "ms",
            description: "Time jobs spend waiting in queue before execution");

        _slaComplianceCounter = _meter.CreateCounter<long>(
            "honua.geoprocessing.sla.compliance",
            unit: "{event}",
            description: "SLA compliance events (1 = compliant, 0 = breach)");

        _slaBreachCounter = _meter.CreateCounter<long>(
            "honua.geoprocessing.sla.breaches",
            unit: "{breach}",
            description: "Number of SLA breaches by severity");

        // Alert delivery metrics
        _alertAttemptCounter = _meter.CreateCounter<long>(
            "honua.geoprocessing.alerts.attempts",
            unit: "{attempt}",
            description: "Number of alert delivery attempts (fire-and-forget)");

        _alertSuccessCounter = _meter.CreateCounter<long>(
            "honua.geoprocessing.alerts.success",
            unit: "{alert}",
            description: "Number of successful alert deliveries");

        _alertFailureCounter = _meter.CreateCounter<long>(
            "honua.geoprocessing.alerts.failures",
            unit: "{failure}",
            description: "Number of failed alert deliveries");

        _alertDuration = _meter.CreateHistogram<double>(
            "honua.geoprocessing.alert.duration",
            unit: "ms",
            description: "Alert delivery duration in milliseconds");

        // Progress update metrics
        _progressUpdateAttemptCounter = _meter.CreateCounter<long>(
            "honua.geoprocessing.progress.attempts",
            unit: "{attempt}",
            description: "Number of progress update attempts (fire-and-forget)");

        _progressUpdateSuccessCounter = _meter.CreateCounter<long>(
            "honua.geoprocessing.progress.success",
            unit: "{update}",
            description: "Number of successful progress updates");

        _progressUpdateFailureCounter = _meter.CreateCounter<long>(
            "honua.geoprocessing.progress.failures",
            unit: "{failure}",
            description: "Number of failed progress updates");

        _progressUpdateThrottledCounter = _meter.CreateCounter<long>(
            "honua.geoprocessing.progress.throttled",
            unit: "{throttle}",
            description: "Number of throttled progress updates");

        // Background task metrics
        _backgroundTaskStartedCounter = _meter.CreateCounter<long>(
            "honua.geoprocessing.background_tasks.started",
            unit: "{task}",
            description: "Number of background tasks started");

        _backgroundTaskCompletedCounter = _meter.CreateCounter<long>(
            "honua.geoprocessing.background_tasks.completed",
            unit: "{task}",
            description: "Number of background tasks completed");

        _backgroundTaskFailedCounter = _meter.CreateCounter<long>(
            "honua.geoprocessing.background_tasks.failed",
            unit: "{task}",
            description: "Number of background tasks failed");

        _backgroundTaskDuration = _meter.CreateHistogram<double>(
            "honua.geoprocessing.background_task.duration",
            unit: "ms",
            description: "Background task duration in milliseconds");

        // Resource metrics (observable gauges)
        _activeJobGauge = _meter.CreateObservableGauge<int>(
            "honua.geoprocessing.jobs.active",
            () => _currentActiveJobs,
            unit: "{job}",
            description: "Number of currently active geoprocessing jobs");

        _queueDepthGauge = _meter.CreateObservableGauge<int>(
            "honua.geoprocessing.queue.depth",
            () => _currentQueueDepth,
            unit: "{job}",
            description: "Number of jobs waiting in queue");
    }

    #region Job Processing Metrics

    public void RecordJobStarted(string processId, int priority, string tier)
    {
        _jobStartedCounter.Add(1,
            new("process.id", Normalize(processId)),
            new("priority", GetPriorityClass(priority)),
            new("tier", Normalize(tier)));
    }

    public void RecordJobCompleted(string processId, int priority, string tier, TimeSpan duration, long featuresProcessed)
    {
        _jobCompletedCounter.Add(1,
            new("process.id", Normalize(processId)),
            new("priority", GetPriorityClass(priority)),
            new("tier", Normalize(tier)));

        _jobDuration.Record(duration.TotalMilliseconds,
            new("process.id", Normalize(processId)),
            new("priority", GetPriorityClass(priority)),
            new("tier", Normalize(tier)),
            new("outcome", "success"));

        if (featuresProcessed > 0)
        {
            _featuresProcessed.Record(featuresProcessed,
                new("process.id", Normalize(processId)),
                new("tier", Normalize(tier)));
        }
    }

    public void RecordJobFailed(string processId, int priority, string tier, TimeSpan duration, string errorType, bool isTransient)
    {
        _jobFailedCounter.Add(1,
            new("process.id", Normalize(processId)),
            new("priority", GetPriorityClass(priority)),
            new("tier", Normalize(tier)),
            new("error.type", Normalize(errorType)),
            new("error.category", isTransient ? "transient" : "permanent"));

        _jobDuration.Record(duration.TotalMilliseconds,
            new("process.id", Normalize(processId)),
            new("priority", GetPriorityClass(priority)),
            new("tier", Normalize(tier)),
            new("outcome", "failure"));

        _logger?.LogWarning(
            "Job failure recorded - Process: {ProcessId}, Priority: {Priority}, Tier: {Tier}, " +
            "Duration: {DurationMs}ms, Error: {ErrorType}, Transient: {IsTransient}",
            processId, priority, tier, duration.TotalMilliseconds, errorType, isTransient);
    }

    public void RecordJobTimeout(string processId, int priority, string tier, TimeSpan duration)
    {
        _jobTimeoutCounter.Add(1,
            new("process.id", Normalize(processId)),
            new("priority", GetPriorityClass(priority)),
            new("tier", Normalize(tier)));

        _jobDuration.Record(duration.TotalMilliseconds,
            new("process.id", Normalize(processId)),
            new("priority", GetPriorityClass(priority)),
            new("tier", Normalize(tier)),
            new("outcome", "timeout"));

        _logger?.LogWarning(
            "Job timeout recorded - Process: {ProcessId}, Priority: {Priority}, Tier: {Tier}, Duration: {DurationMs}ms",
            processId, priority, tier, duration.TotalMilliseconds);
    }

    public void RecordJobRetry(string processId, int priority, int retryCount, string errorType)
    {
        _jobRetryCounter.Add(1,
            new("process.id", Normalize(processId)),
            new("priority", GetPriorityClass(priority)),
            new("retry.count", retryCount.ToString()),
            new("error.type", Normalize(errorType)));
    }

    #endregion

    #region SLA Metrics

    public void RecordSlaCompliance(string processId, int priority, TimeSpan queueWait, bool breached)
    {
        _queueWaitTime.Record(queueWait.TotalMilliseconds,
            new("process.id", Normalize(processId)),
            new("priority", GetPriorityClass(priority)),
            new("sla.breached", breached.ToString().ToLowerInvariant()));

        _slaComplianceCounter.Add(1,
            new("process.id", Normalize(processId)),
            new("priority", GetPriorityClass(priority)),
            new("compliant", (!breached).ToString().ToLowerInvariant()));
    }

    public void RecordSlaBreach(string processId, int priority, TimeSpan queueWait, TimeSpan slaThreshold)
    {
        var breachFactor = queueWait.TotalMinutes / slaThreshold.TotalMinutes;
        var severity = GetSlaBreachSeverity(priority, breachFactor);

        _slaBreachCounter.Add(1,
            new("process.id", Normalize(processId)),
            new("priority", GetPriorityClass(priority)),
            new("severity", severity),
            new("breach.factor", GetBreachFactorClass(breachFactor)));

        _logger?.LogWarning(
            "SLA breach recorded - Process: {ProcessId}, Priority: {Priority}, " +
            "QueueWait: {QueueWaitMin}min, Threshold: {ThresholdMin}min, BreachFactor: {BreachFactor:F1}x, Severity: {Severity}",
            processId, priority, queueWait.TotalMinutes, slaThreshold.TotalMinutes, breachFactor, severity);
    }

    #endregion

    #region Alert Delivery Metrics (Fire-and-Forget)

    public void RecordAlertAttempt(string alertType, string severity)
    {
        _alertAttemptCounter.Add(1,
            new("alert.type", Normalize(alertType)),
            new("alert.severity", Normalize(severity)));

        _backgroundTaskStartedCounter.Add(1,
            new("task.type", "alert_delivery"));
    }

    public void RecordAlertSuccess(string alertType, string severity, TimeSpan duration)
    {
        _alertSuccessCounter.Add(1,
            new("alert.type", Normalize(alertType)),
            new("alert.severity", Normalize(severity)));

        _alertDuration.Record(duration.TotalMilliseconds,
            new("alert.type", Normalize(alertType)),
            new("alert.severity", Normalize(severity)),
            new("outcome", "success"));

        _backgroundTaskCompletedCounter.Add(1,
            new("task.type", "alert_delivery"));

        _backgroundTaskDuration.Record(duration.TotalMilliseconds,
            new("task.type", "alert_delivery"),
            new("outcome", "success"));
    }

    public void RecordAlertFailure(string alertType, string severity, string errorType)
    {
        _alertFailureCounter.Add(1,
            new("alert.type", Normalize(alertType)),
            new("alert.severity", Normalize(severity)),
            new("error.type", Normalize(errorType)));

        _backgroundTaskFailedCounter.Add(1,
            new("task.type", "alert_delivery"),
            new("error.type", Normalize(errorType)));

        _logger?.LogError(
            "Alert delivery failure - Type: {AlertType}, Severity: {Severity}, Error: {ErrorType}",
            alertType, severity, errorType);
    }

    #endregion

    #region Progress Update Metrics (Fire-and-Forget)

    public void RecordProgressUpdateAttempt(string processId)
    {
        _progressUpdateAttemptCounter.Add(1,
            new("process.id", Normalize(processId)));

        _backgroundTaskStartedCounter.Add(1,
            new("task.type", "progress_update"));
    }

    public void RecordProgressUpdateSuccess(string processId, int progressPercent)
    {
        _progressUpdateSuccessCounter.Add(1,
            new("process.id", Normalize(processId)),
            new("progress.milestone", GetProgressMilestone(progressPercent)));

        _backgroundTaskCompletedCounter.Add(1,
            new("task.type", "progress_update"));
    }

    public void RecordProgressUpdateFailure(string processId, string errorType)
    {
        _progressUpdateFailureCounter.Add(1,
            new("process.id", Normalize(processId)),
            new("error.type", Normalize(errorType)));

        _backgroundTaskFailedCounter.Add(1,
            new("task.type", "progress_update"),
            new("error.type", Normalize(errorType)));

        _logger?.LogWarning(
            "Progress update failure - Process: {ProcessId}, Error: {ErrorType}",
            processId, errorType);
    }

    public void RecordProgressUpdateThrottled(string processId, string throttleReason)
    {
        _progressUpdateThrottledCounter.Add(1,
            new("process.id", Normalize(processId)),
            new("throttle.reason", Normalize(throttleReason)));
    }

    #endregion

    #region Background Task Metrics

    public void RecordBackgroundTaskStarted(string taskType)
    {
        _backgroundTaskStartedCounter.Add(1,
            new("task.type", Normalize(taskType)));
    }

    public void RecordBackgroundTaskCompleted(string taskType, TimeSpan duration)
    {
        _backgroundTaskCompletedCounter.Add(1,
            new("task.type", Normalize(taskType)));

        _backgroundTaskDuration.Record(duration.TotalMilliseconds,
            new("task.type", Normalize(taskType)),
            new("outcome", "success"));
    }

    public void RecordBackgroundTaskFailed(string taskType, string errorType)
    {
        _backgroundTaskFailedCounter.Add(1,
            new("task.type", Normalize(taskType)),
            new("error.type", Normalize(errorType)));
    }

    #endregion

    #region Resource Metrics

    public void RecordActiveJobCount(int count)
    {
        _currentActiveJobs = count;
    }

    public void RecordQueueDepth(int depth)
    {
        _currentQueueDepth = depth;
    }

    #endregion

    #region Helper Methods

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? "unknown" : value.ToLowerInvariant();

    private static string GetPriorityClass(int priority)
    {
        return priority switch
        {
            >= 9 => "critical",
            >= 7 => "high",
            >= 5 => "medium",
            >= 3 => "low",
            _ => "lowest"
        };
    }

    private static string GetSlaBreachSeverity(int priority, double breachFactor)
    {
        return (priority, breachFactor) switch
        {
            ( >= 9, >= 3.0) => "critical",
            ( >= 7, _) => "error",
            (_, >= 5.0) => "error",
            _ => "warning"
        };
    }

    private static string GetBreachFactorClass(double breachFactor)
    {
        return breachFactor switch
        {
            >= 5.0 => "5x+",
            >= 3.0 => "3x-5x",
            >= 2.0 => "2x-3x",
            >= 1.5 => "1.5x-2x",
            _ => "1x-1.5x"
        };
    }

    private static string GetProgressMilestone(int progressPercent)
    {
        return progressPercent switch
        {
            0 => "0%",
            >= 1 and < 25 => "1-24%",
            25 => "25%",
            > 25 and < 50 => "26-49%",
            50 => "50%",
            > 50 and < 75 => "51-74%",
            75 => "75%",
            > 75 and < 100 => "76-99%",
            100 => "100%",
            _ => "unknown"
        };
    }

    #endregion

    public void Dispose()
    {
        _meter.Dispose();
    }
}
