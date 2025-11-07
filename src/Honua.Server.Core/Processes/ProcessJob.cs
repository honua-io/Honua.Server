// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;

namespace Honua.Server.Core.Processes;

/// <summary>
/// Represents an executing process job.
/// </summary>
public sealed class ProcessJob : IDisposable
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _cts = new();

    private JobStatus _status = JobStatus.Accepted;
    private string? _message;
    private DateTimeOffset? _startedAtUtc;
    private DateTimeOffset? _finishedAtUtc;
    private DateTimeOffset? _updatedAtUtc;
    private int _progress;
    private Dictionary<string, object>? _results;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessJob"/> class.
    /// </summary>
    /// <param name="jobId">The unique identifier for this job.</param>
    /// <param name="processId">The identifier of the process being executed.</param>
    /// <param name="inputs">The input parameters for the process execution, or null if no inputs are required.</param>
    /// <param name="createdAtUtc">The UTC timestamp when the job was created. If null, uses the current UTC time.</param>
    public ProcessJob(
        string jobId,
        string processId,
        Dictionary<string, object>? inputs,
        DateTimeOffset? createdAtUtc = null)
    {
        JobId = jobId;
        ProcessId = processId;
        Inputs = inputs;
        CreatedAtUtc = createdAtUtc ?? DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets the unique identifier for this job.
    /// </summary>
    public string JobId { get; }

    /// <summary>
    /// Gets the identifier of the process being executed.
    /// </summary>
    public string ProcessId { get; }

    /// <summary>
    /// Gets the input parameters for the process execution.
    /// </summary>
    public Dictionary<string, object>? Inputs { get; }

    /// <summary>
    /// Gets the UTC timestamp when the job was created.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>
    /// Gets the cancellation token for this job. The token is signaled when cancellation is requested.
    /// </summary>
    public CancellationToken Token => _cts.Token;

    /// <summary>
    /// Gets a value indicating whether the job is in a terminal state (Successful, Failed, or Dismissed).
    /// Terminal jobs cannot transition to other states.
    /// </summary>
    public bool IsTerminal
    {
        get
        {
            lock (_gate)
            {
                return _status is JobStatus.Successful or JobStatus.Failed or JobStatus.Dismissed;
            }
        }
    }

    /// <summary>
    /// Gets the current status information for this job.
    /// </summary>
    /// <returns>A <see cref="StatusInfo"/> object containing the job's current state, timestamps, and progress.</returns>
    public StatusInfo GetStatus()
    {
        lock (_gate)
        {
            return new StatusInfo
            {
                JobId = JobId,
                ProcessId = ProcessId,
                Status = _status,
                Message = _message,
                Created = CreatedAtUtc,
                Started = _startedAtUtc,
                Finished = _finishedAtUtc,
                Updated = _updatedAtUtc,
                Progress = _progress > 0 ? _progress : null
            };
        }
    }

    /// <summary>
    /// Gets the execution results for this job.
    /// </summary>
    /// <returns>A dictionary containing the output results from the process execution, or null if no results are available yet.</returns>
    public Dictionary<string, object>? GetResults()
    {
        lock (_gate)
        {
            return _results;
        }
    }

    /// <summary>
    /// Marks the job as started and transitions it to the Running state.
    /// Sets the started timestamp if not already set.
    /// </summary>
    public void MarkStarted()
    {
        lock (_gate)
        {
            if (_startedAtUtc is null)
            {
                _startedAtUtc = DateTimeOffset.UtcNow;
            }
            _status = JobStatus.Running;
            _updatedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Updates the job's progress and optionally sets a status message.
    /// </summary>
    /// <param name="progress">The progress percentage (0-100). Values outside this range will be clamped.</param>
    /// <param name="message">An optional status message describing the current operation.</param>
    public void UpdateProgress(int progress, string? message = null)
    {
        lock (_gate)
        {
            _progress = Math.Clamp(progress, 0, 100);
            if (message is not null)
            {
                _message = message;
            }
            _updatedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Marks the job as successfully completed and stores the execution results.
    /// Transitions the job to the Successful terminal state and sets progress to 100%.
    /// </summary>
    /// <param name="results">The output results from the process execution.</param>
    public void MarkCompleted(Dictionary<string, object> results)
    {
        lock (_gate)
        {
            _finishedAtUtc = DateTimeOffset.UtcNow;
            _updatedAtUtc = _finishedAtUtc;
            _status = JobStatus.Successful;
            _progress = 100;
            _results = results;
        }
    }

    /// <summary>
    /// Marks the job as failed with an error message.
    /// Transitions the job to the Failed terminal state.
    /// </summary>
    /// <param name="message">The error message describing why the job failed.</param>
    public void MarkFailed(string message)
    {
        lock (_gate)
        {
            _finishedAtUtc = DateTimeOffset.UtcNow;
            _updatedAtUtc = _finishedAtUtc;
            _status = JobStatus.Failed;
            _message = message;
        }
    }

    public void MarkDismissed(string? message = null)
    {
        lock (_gate)
        {
            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }

            _finishedAtUtc = DateTimeOffset.UtcNow;
            _updatedAtUtc = _finishedAtUtc;
            _status = JobStatus.Dismissed;
            if (message is not null)
            {
                _message = message;
            }
        }
    }

    public void RequestCancellation()
    {
        lock (_gate)
        {
            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
        }
    }

    public void Dispose()
    {
        _cts.Dispose();
    }
}
