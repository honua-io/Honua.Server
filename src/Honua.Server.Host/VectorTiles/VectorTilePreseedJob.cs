// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.VectorTiles;

/// <summary>
/// Represents an active vector tile preseed job
/// </summary>
public sealed class VectorTilePreseedJob
{
    private long _tilesProcessed;
    private long _tilesTotal;

    public VectorTilePreseedJob(Guid jobId, VectorTilePreseedRequest request)
    {
        JobId = jobId;
        Request = Guard.NotNull(request);
        Status = VectorTilePreseedJobStatus.Queued;
        CreatedUtc = DateTimeOffset.UtcNow;
    }

    public Guid JobId { get; }
    public VectorTilePreseedRequest Request { get; }
    public VectorTilePreseedJobStatus Status { get; private set; }
    public DateTimeOffset CreatedUtc { get; }
    public DateTimeOffset? StartedUtc { get; private set; }
    public DateTimeOffset? CompletedUtc { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string Stage { get; private set; } = "Queued";
    public CancellationTokenSource? CancellationTokenSource { get; private set; }

    public long TilesProcessed => Interlocked.Read(ref _tilesProcessed);
    public long TilesTotal => Interlocked.Read(ref _tilesTotal);
    public double Progress => TilesTotal > 0 ? (double)TilesProcessed / TilesTotal : 0.0;

    public void MarkStarted(long totalTiles)
    {
        Status = VectorTilePreseedJobStatus.Running;
        StartedUtc = DateTimeOffset.UtcNow;
        Interlocked.Exchange(ref _tilesTotal, totalTiles);
        Interlocked.Exchange(ref _tilesProcessed, 0);
        CancellationTokenSource = new CancellationTokenSource();
        Stage = "Generating tiles";
    }

    public void IncrementProgress()
    {
        Interlocked.Increment(ref _tilesProcessed);
    }

    public void UpdateStage(string stage)
    {
        Stage = stage ?? "Processing";
    }

    public void MarkCompleted()
    {
        Status = VectorTilePreseedJobStatus.Completed;
        CompletedUtc = DateTimeOffset.UtcNow;
        Stage = "Completed";
        CancellationTokenSource?.Dispose();
        CancellationTokenSource = null;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = VectorTilePreseedJobStatus.Failed;
        CompletedUtc = DateTimeOffset.UtcNow;
        ErrorMessage = errorMessage;
        Stage = "Failed";
        CancellationTokenSource?.Dispose();
        CancellationTokenSource = null;
    }

    public void MarkCancelled()
    {
        Status = VectorTilePreseedJobStatus.Cancelled;
        CompletedUtc = DateTimeOffset.UtcNow;
        Stage = "Cancelled";
        CancellationTokenSource?.Cancel();
        CancellationTokenSource?.Dispose();
        CancellationTokenSource = null;
    }

    public bool TryCancel()
    {
        if (Status is VectorTilePreseedJobStatus.Completed or VectorTilePreseedJobStatus.Failed or VectorTilePreseedJobStatus.Cancelled)
        {
            return false;
        }

        MarkCancelled();
        return true;
    }
}
