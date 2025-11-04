// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Threading;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Import;

public sealed class DataIngestionJob : IDisposable
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _cts = new();

    private DataIngestionJobStatus _status = DataIngestionJobStatus.Queued;
    private string _stage = "Queued";
    private double _progress;
    private string? _message;
    private DateTimeOffset? _startedAtUtc;
    private DateTimeOffset? _completedAtUtc;

    public DataIngestionJob(
        string serviceId,
        string layerId,
        string? sourceFileName,
        Guid? jobId = null,
        DateTimeOffset? createdAtUtc = null)
    {
        JobId = jobId ?? Guid.NewGuid();
        ServiceId = serviceId;
        LayerId = layerId;
        SourceFileName = sourceFileName;
        CreatedAtUtc = createdAtUtc ?? DateTimeOffset.UtcNow;
    }

    public Guid JobId { get; }
    public string ServiceId { get; }
    public string LayerId { get; }
    public string? SourceFileName { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public CancellationToken Token => _cts.Token;

    public bool IsTerminal
    {
        get
        {
            lock (_gate)
            {
                return _status is DataIngestionJobStatus.Completed or DataIngestionJobStatus.Failed or DataIngestionJobStatus.Cancelled;
            }
        }
    }

    public DataIngestionJobSnapshot Snapshot
    {
        get
        {
            lock (_gate)
            {
                return new DataIngestionJobSnapshot(
                    JobId,
                    ServiceId,
                    LayerId,
                    SourceFileName,
                    _status,
                    _stage,
                    _progress,
                    _message,
                    CreatedAtUtc,
                    _startedAtUtc,
                    _completedAtUtc);
            }
        }
    }

    public void MarkStarted(string stage)
    {
        lock (_gate)
        {
            if (_startedAtUtc is null)
            {
                _startedAtUtc = DateTimeOffset.UtcNow;
            }

            UpdateState(DataIngestionJobStatus.Validating, stage, _progress, _message);
        }
    }

    public void UpdateProgress(DataIngestionJobStatus status, string stage, double progress, string? message = null)
    {
        lock (_gate)
        {
            UpdateState(status, stage, progress, message);
        }
    }

    public void ReportProgress(long processed, long total, string stage)
    {
        double progress;
        if (total <= 0)
        {
            progress = 0d;
        }
        else
        {
            progress = Math.Clamp((double)processed / total, 0d, 1d);
        }

        UpdateProgress(DataIngestionJobStatus.Importing, stage, progress);
    }

    public void MarkCompleted(string stage)
    {
        lock (_gate)
        {
            _completedAtUtc = DateTimeOffset.UtcNow;
            UpdateState(DataIngestionJobStatus.Completed, stage, 1d);
        }
    }

    public void MarkFailed(string stage, string message)
    {
        lock (_gate)
        {
            _completedAtUtc = DateTimeOffset.UtcNow;
            UpdateState(DataIngestionJobStatus.Failed, stage, _progress, message);
        }
    }

    public void MarkCancelled(string stage, string? message)
    {
        lock (_gate)
        {
            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }

            _completedAtUtc = DateTimeOffset.UtcNow;
            UpdateState(DataIngestionJobStatus.Cancelled, stage, _progress, message);
        }
    }

    public void RequestCancellation(string? reason)
    {
        lock (_gate)
        {
            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                _message = reason;
            }
        }
    }

    public void Dispose()
    {
        _cts.Dispose();
    }

    private void UpdateState(DataIngestionJobStatus status, string stage, double progress, string? message = null)
    {
        _status = status;
        _stage = stage;
        _progress = Math.Clamp(progress, 0d, 1d);
        if (message.HasValue())
        {
            _message = message;
        }
    }
}
