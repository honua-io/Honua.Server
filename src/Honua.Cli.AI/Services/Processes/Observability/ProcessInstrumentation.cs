// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.Services.Processes.Observability;

/// <summary>
/// OpenTelemetry instrumentation for Process Framework steps.
/// Provides distributed tracing, structured logging, and metrics for process execution.
/// </summary>
public sealed class ProcessInstrumentation
{
    private readonly ActivitySource _activitySource;
    private readonly ProcessFrameworkMetrics _metrics;
    private readonly ILogger<ProcessInstrumentation> _logger;

    public ProcessInstrumentation(
        ProcessFrameworkMetrics metrics,
        ILogger<ProcessInstrumentation> logger)
    {
        _activitySource = new ActivitySource("ProcessFramework", "1.0.0");
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Starts a trace for process execution.
    /// </summary>
    public Activity? StartProcessTrace(string processId, string workflowType, object? inputData = null)
    {
        var activity = _activitySource.StartActivity(
            $"Process.{workflowType}",
            ActivityKind.Internal);

        if (activity != null)
        {
            activity.SetTag("process.id", processId);
            activity.SetTag("process.workflow_type", workflowType);
            activity.SetTag("process.start_time", DateTimeOffset.UtcNow.ToString("o"));

            if (inputData != null)
            {
                activity.SetTag("process.input_type", inputData.GetType().Name);
            }
        }

        _logger.LogInformation(
            "Starting process trace: {ProcessId} ({WorkflowType})",
            processId, workflowType);

        return activity;
    }

    /// <summary>
    /// Completes a process trace with success status.
    /// </summary>
    public void CompleteProcessTrace(Activity? activity, string processId, int stepsExecuted, TimeSpan duration)
    {
        if (activity == null) return;

        activity.SetTag("process.status", "completed");
        activity.SetTag("process.steps_executed", stepsExecuted);
        activity.SetTag("process.duration_ms", duration.TotalMilliseconds);
        activity.SetTag("process.end_time", DateTimeOffset.UtcNow.ToString("o"));

        _logger.LogInformation(
            "Process {ProcessId} completed successfully. Steps: {Steps}, Duration: {Duration}ms",
            processId, stepsExecuted, duration.TotalMilliseconds);

        activity.Dispose();
    }

    /// <summary>
    /// Fails a process trace with error information.
    /// </summary>
    public void FailProcessTrace(Activity? activity, string processId, Exception exception, string? errorReason = null)
    {
        if (activity == null) return;

        activity.SetTag("process.status", "failed");
        activity.SetTag("error.type", exception.GetType().Name);
        activity.SetTag("error.message", exception.Message);

        if (errorReason != null)
        {
            activity.SetTag("error.reason", errorReason);
        }

        activity.SetTag("process.end_time", DateTimeOffset.UtcNow.ToString("o"));
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);

        _logger.LogError(
            exception,
            "Process {ProcessId} failed: {ErrorReason}",
            processId, errorReason ?? exception.Message);

        activity.Dispose();
    }

    /// <summary>
    /// Starts a trace for step execution.
    /// </summary>
    public Activity? StartStepTrace(string processId, string stepName, string workflowType, object? inputData = null)
    {
        var activity = _activitySource.StartActivity(
            $"Step.{stepName}",
            ActivityKind.Internal);

        if (activity != null)
        {
            activity.SetTag("process.id", processId);
            activity.SetTag("step.name", stepName);
            activity.SetTag("step.workflow_type", workflowType);
            activity.SetTag("step.start_time", DateTimeOffset.UtcNow.ToString("o"));

            if (inputData != null)
            {
                activity.SetTag("step.input_type", inputData.GetType().Name);
            }
        }

        _logger.LogDebug(
            "Starting step trace: {StepName} for process {ProcessId}",
            stepName, processId);

        return activity;
    }

    /// <summary>
    /// Completes a step trace with success status.
    /// </summary>
    public void CompleteStepTrace(Activity? activity, string processId, string stepName, TimeSpan duration, object? outputData = null)
    {
        if (activity == null) return;

        activity.SetTag("step.status", "completed");
        activity.SetTag("step.duration_ms", duration.TotalMilliseconds);
        activity.SetTag("step.end_time", DateTimeOffset.UtcNow.ToString("o"));

        if (outputData != null)
        {
            activity.SetTag("step.output_type", outputData.GetType().Name);
        }

        _logger.LogDebug(
            "Step {StepName} in process {ProcessId} completed in {Duration}ms",
            stepName, processId, duration.TotalMilliseconds);

        activity.Dispose();
    }

    /// <summary>
    /// Fails a step trace with error information.
    /// </summary>
    public void FailStepTrace(Activity? activity, string processId, string stepName, Exception exception, string? errorReason = null)
    {
        if (activity == null) return;

        activity.SetTag("step.status", "failed");
        activity.SetTag("error.type", exception.GetType().Name);
        activity.SetTag("error.message", exception.Message);

        if (errorReason != null)
        {
            activity.SetTag("error.reason", errorReason);
        }

        activity.SetTag("step.end_time", DateTimeOffset.UtcNow.ToString("o"));
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);

        _logger.LogWarning(
            exception,
            "Step {StepName} in process {ProcessId} failed: {ErrorReason}",
            stepName, processId, errorReason ?? exception.Message);

        activity.Dispose();
    }

    /// <summary>
    /// Logs a state transition in the process.
    /// </summary>
    public void LogStateTransition(string processId, string workflowType, string fromState, string toState, string? reason = null)
    {
        using var activity = _activitySource.StartActivity("Process.StateTransition", ActivityKind.Internal);

        if (activity != null)
        {
            activity.SetTag("process.id", processId);
            activity.SetTag("process.workflow_type", workflowType);
            activity.SetTag("state.from", fromState);
            activity.SetTag("state.to", toState);

            if (reason != null)
            {
                activity.SetTag("state.transition_reason", reason);
            }
        }

        _logger.LogInformation(
            "Process {ProcessId} ({WorkflowType}) state transition: {FromState} -> {ToState}. Reason: {Reason}",
            processId, workflowType, fromState, toState, reason ?? "N/A");
    }

    /// <summary>
    /// Records a custom event in the current trace.
    /// </summary>
    public void RecordEvent(string eventName, params (string Key, object? Value)[] tags)
    {
        var activity = Activity.Current;
        if (activity == null) return;

        var tagList = new List<KeyValuePair<string, object?>>();
        foreach (var (key, value) in tags)
        {
            tagList.Add(new KeyValuePair<string, object?>(key, value));
        }

        var activityEvent = new ActivityEvent(eventName, tags: new ActivityTagsCollection(tagList));
        activity.AddEvent(activityEvent);

        _logger.LogDebug("Recorded event: {EventName}", eventName);
    }
}

/// <summary>
/// Helper class for instrumenting process step execution with automatic metrics and tracing.
/// </summary>
public sealed class StepExecutionScope : IDisposable
{
    private readonly ProcessInstrumentation _instrumentation;
    private readonly ProcessFrameworkMetrics _metrics;
    private readonly string _processId;
    private readonly string _stepName;
    private readonly string _workflowType;
    private readonly Activity? _activity;
    private readonly Stopwatch _stopwatch;
    private bool _completed;

    public StepExecutionScope(
        ProcessInstrumentation instrumentation,
        ProcessFrameworkMetrics metrics,
        string processId,
        string stepName,
        string workflowType,
        object? inputData = null)
    {
        _instrumentation = instrumentation ?? throw new ArgumentNullException(nameof(instrumentation));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _processId = processId;
        _stepName = stepName;
        _workflowType = workflowType;

        _activity = _instrumentation.StartStepTrace(processId, stepName, workflowType, inputData);
        _metrics.RecordStepStarted(processId, stepName);
        _stopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    /// Marks the step as completed successfully.
    /// </summary>
    public void Complete(object? outputData = null)
    {
        if (_completed) return;

        _stopwatch.Stop();
        _instrumentation.CompleteStepTrace(_activity, _processId, _stepName, _stopwatch.Elapsed, outputData);
        _metrics.RecordStepCompleted(_processId, _stepName, _stopwatch.Elapsed);
        _completed = true;
    }

    /// <summary>
    /// Marks the step as failed.
    /// </summary>
    public void Fail(Exception exception, string? errorReason = null)
    {
        if (_completed) return;

        _stopwatch.Stop();
        _instrumentation.FailStepTrace(_activity, _processId, _stepName, exception, errorReason);
        _metrics.RecordStepFailed(_processId, _stepName, errorReason);
        _completed = true;
    }

    public void Dispose()
    {
        if (!_completed)
        {
            // Auto-complete if not explicitly completed or failed
            Complete();
        }
    }
}

/// <summary>
/// Helper class for instrumenting process execution with automatic metrics and tracing.
/// </summary>
public sealed class ProcessExecutionScope : IDisposable
{
    private readonly ProcessInstrumentation _instrumentation;
    private readonly ProcessFrameworkMetrics _metrics;
    private readonly string _processId;
    private readonly string _workflowType;
    private readonly Activity? _activity;
    private readonly Stopwatch _stopwatch;
    private int _stepsExecuted;
    private bool _completed;

    public ProcessExecutionScope(
        ProcessInstrumentation instrumentation,
        ProcessFrameworkMetrics metrics,
        string processId,
        string workflowType,
        object? inputData = null)
    {
        _instrumentation = instrumentation ?? throw new ArgumentNullException(nameof(instrumentation));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _processId = processId;
        _workflowType = workflowType;

        _activity = _instrumentation.StartProcessTrace(processId, workflowType, inputData);
        _metrics.RecordProcessStarted(processId, workflowType);
        _stopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    /// Increments the count of executed steps.
    /// </summary>
    public void IncrementStepCount() => _stepsExecuted++;

    /// <summary>
    /// Marks the process as completed successfully.
    /// </summary>
    public void Complete()
    {
        if (_completed) return;

        _stopwatch.Stop();
        _instrumentation.CompleteProcessTrace(_activity, _processId, _stepsExecuted, _stopwatch.Elapsed);
        _metrics.RecordProcessCompleted(_processId, _stopwatch.Elapsed);
        _completed = true;
    }

    /// <summary>
    /// Marks the process as failed.
    /// </summary>
    public void Fail(Exception exception, string? errorReason = null)
    {
        if (_completed) return;

        _stopwatch.Stop();
        _instrumentation.FailProcessTrace(_activity, _processId, exception, errorReason);
        _metrics.RecordProcessFailed(_processId, errorReason, exception);
        _completed = true;
    }

    public void Dispose()
    {
        if (!_completed)
        {
            // If not explicitly completed or failed, mark as completed
            Complete();
        }
    }
}
