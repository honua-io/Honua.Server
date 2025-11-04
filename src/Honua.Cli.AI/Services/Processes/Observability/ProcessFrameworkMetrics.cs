// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.Services.Processes.Observability;

/// <summary>
/// Metrics collection for Process Framework workflows.
/// Tracks process execution, step durations, success/failure rates, and active process counts.
/// </summary>
public sealed class ProcessFrameworkMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly ILogger<ProcessFrameworkMetrics> _logger;

    // Counters
    private readonly Counter<long> _processStartedCounter;
    private readonly Counter<long> _processCompletedCounter;
    private readonly Counter<long> _processFailedCounter;
    private readonly Counter<long> _stepExecutedCounter;
    private readonly Counter<long> _stepFailedCounter;

    // Histograms
    private readonly Histogram<double> _processExecutionDuration;
    private readonly Histogram<double> _stepExecutionDuration;

    // Gauges (UpDownCounters)
    private readonly UpDownCounter<int> _activeProcessCount;
    private readonly UpDownCounter<int> _activeStepCount;

    // Observable Gauges
    private readonly ConcurrentDictionary<string, ProcessMetricsState> _processStates;
    private readonly ConcurrentDictionary<string, long> _workflowSuccessCounts;
    private readonly ConcurrentDictionary<string, long> _workflowFailureCounts;

    public ProcessFrameworkMetrics(ILogger<ProcessFrameworkMetrics> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _meter = new Meter("Honua.ProcessFramework", "1.0.0");

        // Initialize state tracking
        _processStates = new ConcurrentDictionary<string, ProcessMetricsState>();
        _workflowSuccessCounts = new ConcurrentDictionary<string, long>();
        _workflowFailureCounts = new ConcurrentDictionary<string, long>();

        // Create counters
        _processStartedCounter = _meter.CreateCounter<long>(
            name: "process.started",
            unit: "processes",
            description: "Number of processes started");

        _processCompletedCounter = _meter.CreateCounter<long>(
            name: "process.completed",
            unit: "processes",
            description: "Number of processes completed successfully");

        _processFailedCounter = _meter.CreateCounter<long>(
            name: "process.failed",
            unit: "processes",
            description: "Number of processes failed");

        _stepExecutedCounter = _meter.CreateCounter<long>(
            name: "process.step.executed",
            unit: "steps",
            description: "Number of process steps executed");

        _stepFailedCounter = _meter.CreateCounter<long>(
            name: "process.step.failed",
            unit: "steps",
            description: "Number of process steps failed");

        // Create histograms for durations
        _processExecutionDuration = _meter.CreateHistogram<double>(
            name: "process.execution.duration",
            unit: "ms",
            description: "Process execution duration in milliseconds");

        _stepExecutionDuration = _meter.CreateHistogram<double>(
            name: "process.step.duration",
            unit: "ms",
            description: "Step execution duration in milliseconds");

        // Create up/down counters for active counts
        _activeProcessCount = _meter.CreateUpDownCounter<int>(
            name: "process.active.count",
            unit: "processes",
            description: "Number of currently active processes");

        _activeStepCount = _meter.CreateUpDownCounter<int>(
            name: "process.step.active.count",
            unit: "steps",
            description: "Number of currently executing steps");

        // Create observable gauges - these return collections of measurements
        _meter.CreateObservableGauge(
            name: "process.workflow.success_rate",
            observeValues: CalculateSuccessRates,
            unit: "ratio",
            description: "Success rate per workflow type (0.0 to 1.0)");

        _meter.CreateObservableGauge(
            name: "process.workflow.total_executions",
            observeValues: GetTotalExecutions,
            unit: "executions",
            description: "Total executions per workflow type");
    }

    #region Process Lifecycle Metrics

    /// <summary>
    /// Records that a process has started.
    /// </summary>
    public void RecordProcessStarted(string processId, string workflowType, Dictionary<string, object?>? tags = null)
    {
        var state = new ProcessMetricsState
        {
            ProcessId = processId,
            WorkflowType = workflowType,
            StartTime = DateTimeOffset.UtcNow,
            Tags = tags ?? new Dictionary<string, object?>()
        };

        _processStates[processId] = state;

        var metricTags = new TagList
        {
            { "workflow.type", workflowType },
            { "process.id", processId }
        };

        if (tags != null)
        {
            foreach (var tag in tags)
            {
                metricTags.Add(tag.Key, tag.Value);
            }
        }

        _processStartedCounter.Add(1, metricTags);
        _activeProcessCount.Add(1, metricTags);

        _logger.LogInformation(
            "Process {ProcessId} of type {WorkflowType} started",
            processId, workflowType);
    }

    /// <summary>
    /// Records that a process has completed successfully.
    /// </summary>
    public void RecordProcessCompleted(string processId, TimeSpan? duration = null)
    {
        if (!_processStates.TryRemove(processId, out var state))
        {
            _logger.LogWarning("Attempted to complete unknown process {ProcessId}", processId);
            return;
        }

        var actualDuration = duration ?? (DateTimeOffset.UtcNow - state.StartTime);

        var tags = new TagList
        {
            { "workflow.type", state.WorkflowType },
            { "process.id", processId }
        };

        foreach (var tag in state.Tags)
        {
            tags.Add(tag.Key, tag.Value);
        }

        _processCompletedCounter.Add(1, tags);
        _activeProcessCount.Add(-1, tags);
        _processExecutionDuration.Record(actualDuration.TotalMilliseconds, tags);

        // Update workflow success count
        _workflowSuccessCounts.AddOrUpdate(state.WorkflowType, 1, (_, count) => count + 1);

        _logger.LogInformation(
            "Process {ProcessId} of type {WorkflowType} completed in {Duration}ms",
            processId, state.WorkflowType, actualDuration.TotalMilliseconds);
    }

    /// <summary>
    /// Records that a process has failed.
    /// </summary>
    public void RecordProcessFailed(string processId, string? errorReason = null, Exception? exception = null)
    {
        if (!_processStates.TryRemove(processId, out var state))
        {
            _logger.LogWarning("Attempted to fail unknown process {ProcessId}", processId);
            return;
        }

        var duration = DateTimeOffset.UtcNow - state.StartTime;

        var tags = new TagList
        {
            { "workflow.type", state.WorkflowType },
            { "process.id", processId }
        };

        if (errorReason != null)
        {
            tags.Add("error.reason", errorReason);
        }

        if (exception != null)
        {
            tags.Add("error.type", exception.GetType().Name);
        }

        foreach (var tag in state.Tags)
        {
            tags.Add(tag.Key, tag.Value);
        }

        _processFailedCounter.Add(1, tags);
        _activeProcessCount.Add(-1, tags);
        _processExecutionDuration.Record(duration.TotalMilliseconds, tags);

        // Update workflow failure count
        _workflowFailureCounts.AddOrUpdate(state.WorkflowType, 1, (_, count) => count + 1);

        _logger.LogError(
            exception,
            "Process {ProcessId} of type {WorkflowType} failed after {Duration}ms: {ErrorReason}",
            processId, state.WorkflowType, duration.TotalMilliseconds, errorReason ?? "Unknown");
    }

    #endregion

    #region Step Execution Metrics

    /// <summary>
    /// Records that a step has started executing.
    /// </summary>
    public void RecordStepStarted(string processId, string stepName)
    {
        if (!_processStates.TryGetValue(processId, out var state))
        {
            _logger.LogWarning("Step {StepName} started for unknown process {ProcessId}", stepName, processId);
            return;
        }

        var tags = new TagList
        {
            { "workflow.type", state.WorkflowType },
            { "process.id", processId },
            { "step.name", stepName }
        };

        _activeStepCount.Add(1, tags);
    }

    /// <summary>
    /// Records that a step has completed successfully.
    /// </summary>
    public void RecordStepCompleted(string processId, string stepName, TimeSpan duration)
    {
        if (!_processStates.TryGetValue(processId, out var state))
        {
            _logger.LogWarning("Step {StepName} completed for unknown process {ProcessId}", stepName, processId);
            return;
        }

        var tags = new TagList
        {
            { "workflow.type", state.WorkflowType },
            { "process.id", processId },
            { "step.name", stepName }
        };

        _stepExecutedCounter.Add(1, tags);
        _activeStepCount.Add(-1, tags);
        _stepExecutionDuration.Record(duration.TotalMilliseconds, tags);

        _logger.LogDebug(
            "Step {StepName} in process {ProcessId} completed in {Duration}ms",
            stepName, processId, duration.TotalMilliseconds);
    }

    /// <summary>
    /// Records that a step has failed.
    /// </summary>
    public void RecordStepFailed(string processId, string stepName, string? errorReason = null)
    {
        if (!_processStates.TryGetValue(processId, out var state))
        {
            _logger.LogWarning("Step {StepName} failed for unknown process {ProcessId}", stepName, processId);
            return;
        }

        var tags = new TagList
        {
            { "workflow.type", state.WorkflowType },
            { "process.id", processId },
            { "step.name", stepName }
        };

        if (errorReason != null)
        {
            tags.Add("error.reason", errorReason);
        }

        _stepFailedCounter.Add(1, tags);
        _activeStepCount.Add(-1, tags);

        _logger.LogWarning(
            "Step {StepName} in process {ProcessId} failed: {ErrorReason}",
            stepName, processId, errorReason ?? "Unknown");
    }

    #endregion

    #region Query Methods

    /// <summary>
    /// Gets the number of currently active processes.
    /// </summary>
    public int GetActiveProcessCount() => _processStates.Count;

    /// <summary>
    /// Gets active processes by workflow type.
    /// </summary>
    public Dictionary<string, int> GetActiveProcessCountByWorkflow()
    {
        var result = new Dictionary<string, int>();

        foreach (var state in _processStates.Values)
        {
            result.TryGetValue(state.WorkflowType, out var count);
            result[state.WorkflowType] = count + 1;
        }

        return result;
    }

    /// <summary>
    /// Gets success rate for a specific workflow type.
    /// </summary>
    public double GetSuccessRate(string workflowType)
    {
        var successes = _workflowSuccessCounts.GetValueOrDefault(workflowType, 0);
        var failures = _workflowFailureCounts.GetValueOrDefault(workflowType, 0);
        var total = successes + failures;

        return total == 0 ? 1.0 : (double)successes / total;
    }

    /// <summary>
    /// Gets total execution count for a specific workflow type.
    /// </summary>
    public long GetTotalExecutions(string workflowType)
    {
        var successes = _workflowSuccessCounts.GetValueOrDefault(workflowType, 0);
        var failures = _workflowFailureCounts.GetValueOrDefault(workflowType, 0);
        return successes + failures;
    }

    #endregion

    #region Private Helper Methods

    private IEnumerable<Measurement<double>> CalculateSuccessRates()
    {
        var allWorkflows = new HashSet<string>();
        allWorkflows.UnionWith(_workflowSuccessCounts.Keys);
        allWorkflows.UnionWith(_workflowFailureCounts.Keys);

        foreach (var workflow in allWorkflows)
        {
            var rate = GetSuccessRate(workflow);
            yield return new Measurement<double>(
                rate,
                new KeyValuePair<string, object?>("workflow.type", workflow));
        }
    }

    private IEnumerable<Measurement<long>> GetTotalExecutions()
    {
        var allWorkflows = new HashSet<string>();
        allWorkflows.UnionWith(_workflowSuccessCounts.Keys);
        allWorkflows.UnionWith(_workflowFailureCounts.Keys);

        foreach (var workflow in allWorkflows)
        {
            var total = GetTotalExecutions(workflow);
            yield return new Measurement<long>(
                total,
                new KeyValuePair<string, object?>("workflow.type", workflow));
        }
    }

    #endregion

    public void Dispose()
    {
        _meter?.Dispose();
    }
}

/// <summary>
/// Internal state tracking for an active process.
/// </summary>
internal class ProcessMetricsState
{
    public required string ProcessId { get; init; }
    public required string WorkflowType { get; init; }
    public DateTimeOffset StartTime { get; init; }
    public Dictionary<string, object?> Tags { get; init; } = new();
}
