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

    public CancellationToken Token => this.cts.Token;

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
                    this.Request.DatasetIds,
                    this.Request.TileMatrixSetId,
                    this.Request.TileSize,
                    this.Request.Transparent,
                    this.Request.Format,
                    this.Request.Overwrite,
                    _tilesCompleted,
                    _tilesTotal);
            }
        }
    }

    public void MarkStarted(string stage)
    {
        lock (_syncRoot)
        {
            this.status = RasterTilePreseedJobStatus.Running;
            this.stage = stage;
            this.message = null;
            this.progress = _tilesTotal > 0 ? Math.Min(1d, (double)_tilesCompleted / _tilesTotal) : 0d;
        }
    }

    public void SetTotalTiles(long totalTiles)
    {
        lock (_syncRoot)
        {
            this.tilesTotal = totalTiles;
            this.progress = _tilesTotal > 0 ? Math.Min(1d, (double)_tilesCompleted / _tilesTotal) : 0d;
        }
    }

    public void IncrementTiles(string stage)
    {
        lock (_syncRoot)
        {
            this.tilesCompleted = checked(_tilesCompleted + 1);
            this.stage = stage;
            this.progress = _tilesTotal > 0 ? Math.Min(1d, (double)_tilesCompleted / _tilesTotal) : 0d;
        }
    }

    public void UpdateStage(string stage)
    {
        lock (_syncRoot)
        {
            this.stage = stage;
        }
    }

    public void MarkCompleted(string message)
    {
        lock (_syncRoot)
        {
            this.status = RasterTilePreseedJobStatus.Completed;
            this.stage = "Completed";
            this.message = message;
            this.progress = 1d;
            this.completedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    public void MarkFailed(string message)
    {
        lock (_syncRoot)
        {
            this.status = RasterTilePreseedJobStatus.Failed;
            this.stage = "Failed";
            this.message = message;
            this.completedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    public void MarkCancelled(string message)
    {
        lock (_syncRoot)
        {
            this.status = RasterTilePreseedJobStatus.Cancelled;
            this.stage = "Cancelled";
            this.message = message;
            this.completedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    public void RequestCancellation(string? reason = null)
    {
        lock (_syncRoot)
        {
            if (this.cts.IsCancellationRequested)
            {
                return;
            }

            this.message = string.IsNullOrWhiteSpace(reason) ? "Cancellation requested." : reason;
            this.cts.Cancel();
        }
    }

    public void Dispose()
    {
        this.cts.Dispose();
    }
}
