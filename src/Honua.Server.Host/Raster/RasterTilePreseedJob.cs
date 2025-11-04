// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Threading;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Raster;

internal sealed class RasterTilePreseedJob : IDisposable
{
    private readonly object _syncRoot = new();
    private readonly CancellationTokenSource _cts = new();
    private RasterTilePreseedJobStatus _status = RasterTilePreseedJobStatus.Queued;
    private double _progress;
    private string _stage = "Queued";
    private string? _message;
    private long _tilesCompleted;
    private long _tilesTotal;
    private DateTimeOffset? _completedAtUtc;

    public RasterTilePreseedJob(RasterTilePreseedRequest request)
    {
        Request = Guard.NotNull(request);
        JobId = Guid.NewGuid();
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid JobId { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public RasterTilePreseedRequest Request { get; }

    public CancellationToken Token => _cts.Token;

    public RasterTilePreseedJobSnapshot Snapshot
    {
        get
        {
            lock (_syncRoot)
            {
                return new RasterTilePreseedJobSnapshot(
                    JobId,
                    _status,
                    _progress,
                    _stage,
                    _message,
                    CreatedAtUtc,
                    _completedAtUtc,
                    Request.DatasetIds,
                    Request.TileMatrixSetId,
                    Request.TileSize,
                    Request.Transparent,
                    Request.Format,
                    Request.Overwrite,
                    _tilesCompleted,
                    _tilesTotal);
            }
        }
    }

    public void MarkStarted(string stage)
    {
        lock (_syncRoot)
        {
            _status = RasterTilePreseedJobStatus.Running;
            _stage = stage;
            _message = null;
            _progress = _tilesTotal > 0 ? Math.Min(1d, (double)_tilesCompleted / _tilesTotal) : 0d;
        }
    }

    public void SetTotalTiles(long totalTiles)
    {
        lock (_syncRoot)
        {
            _tilesTotal = totalTiles;
            _progress = _tilesTotal > 0 ? Math.Min(1d, (double)_tilesCompleted / _tilesTotal) : 0d;
        }
    }

    public void IncrementTiles(string stage)
    {
        lock (_syncRoot)
        {
            _tilesCompleted = checked(_tilesCompleted + 1);
            _stage = stage;
            _progress = _tilesTotal > 0 ? Math.Min(1d, (double)_tilesCompleted / _tilesTotal) : 0d;
        }
    }

    public void UpdateStage(string stage)
    {
        lock (_syncRoot)
        {
            _stage = stage;
        }
    }

    public void MarkCompleted(string message)
    {
        lock (_syncRoot)
        {
            _status = RasterTilePreseedJobStatus.Completed;
            _stage = "Completed";
            _message = message;
            _progress = 1d;
            _completedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    public void MarkFailed(string message)
    {
        lock (_syncRoot)
        {
            _status = RasterTilePreseedJobStatus.Failed;
            _stage = "Failed";
            _message = message;
            _completedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    public void MarkCancelled(string message)
    {
        lock (_syncRoot)
        {
            _status = RasterTilePreseedJobStatus.Cancelled;
            _stage = "Cancelled";
            _message = message;
            _completedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    public void RequestCancellation(string? reason = null)
    {
        lock (_syncRoot)
        {
            if (_cts.IsCancellationRequested)
            {
                return;
            }

            _message = string.IsNullOrWhiteSpace(reason) ? "Cancellation requested." : reason;
            _cts.Cancel();
        }
    }

    public void Dispose()
    {
        _cts.Dispose();
    }
}
