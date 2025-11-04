// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Threading;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Migration.GeoservicesRest;

internal sealed class GeoservicesRestMigrationJob : IDisposable
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _cts = new();

    private GeoservicesRestMigrationJobStatus _status = GeoservicesRestMigrationJobStatus.Queued;
    private string _stage = "Queued";
    private double _progress;
    private string? _message;
    private DateTimeOffset? _startedAtUtc;
    private DateTimeOffset? _completedAtUtc;

    public GeoservicesRestMigrationJob(string serviceId, string dataSourceId)
    {
        JobId = Guid.NewGuid();
        ServiceId = serviceId;
        DataSourceId = dataSourceId;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid JobId { get; }

    public string ServiceId { get; }

    public string DataSourceId { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public CancellationToken Token => _cts.Token;

    public bool IsTerminal
    {
        get
        {
            lock (_gate)
            {
                return _status is GeoservicesRestMigrationJobStatus.Completed or GeoservicesRestMigrationJobStatus.Failed or GeoservicesRestMigrationJobStatus.Cancelled;
            }
        }
    }

    public GeoservicesRestMigrationJobSnapshot Snapshot
    {
        get
        {
            lock (_gate)
            {
                return new GeoservicesRestMigrationJobSnapshot(
                    JobId,
                    ServiceId,
                    DataSourceId,
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

            UpdateState(GeoservicesRestMigrationJobStatus.Initializing, stage, _progress, _message);
        }
    }

    public void UpdateProgress(GeoservicesRestMigrationJobStatus status, string stage, double progress, string? message = null)
    {
        lock (_gate)
        {
            UpdateState(status, stage, progress, message);
        }
    }

    public void MarkCompleted(string stage)
    {
        lock (_gate)
        {
            _completedAtUtc = DateTimeOffset.UtcNow;
            UpdateState(GeoservicesRestMigrationJobStatus.Completed, stage, 1d);
        }
    }

    public void MarkFailed(string stage, string message)
    {
        lock (_gate)
        {
            _completedAtUtc = DateTimeOffset.UtcNow;
            UpdateState(GeoservicesRestMigrationJobStatus.Failed, stage, _progress, message);
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
            UpdateState(GeoservicesRestMigrationJobStatus.Cancelled, stage, _progress, message);
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

    private void UpdateState(GeoservicesRestMigrationJobStatus status, string stage, double progress, string? message = null)
    {
        _status = status;
        _stage = stage;
        _progress = Math.Clamp(progress, 0d, 1d);
        if (!string.IsNullOrWhiteSpace(message))
        {
            _message = message;
        }
    }
}
