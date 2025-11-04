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

    public string JobId { get; }
    public string ProcessId { get; }
    public Dictionary<string, object>? Inputs { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public CancellationToken Token => _cts.Token;

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

    public Dictionary<string, object>? GetResults()
    {
        lock (_gate)
        {
            return _results;
        }
    }

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
