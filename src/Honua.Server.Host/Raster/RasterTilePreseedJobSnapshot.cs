// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Honua.Server.Host.Raster;

public sealed record RasterTilePreseedJobSnapshot
{
    public RasterTilePreseedJobSnapshot(
        Guid jobId,
        RasterTilePreseedJobStatus status,
        double progress,
        string stage,
        string? message,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? completedAtUtc,
        IReadOnlyList<string> datasetIds,
        string tileMatrixSetId,
        int tileSize,
        bool transparent,
        string format,
        bool overwrite,
        long tilesCompleted,
        long tilesTotal)
    {
        JobId = jobId;
        Status = status;
        Progress = progress;
        Stage = string.IsNullOrWhiteSpace(stage) ? "Queued" : stage;
        Message = message;
        CreatedAtUtc = createdAtUtc;
        CompletedAtUtc = completedAtUtc;
        DatasetIds = datasetIds is List<string> or string[]? datasetIds
            : new ReadOnlyCollection<string>(datasetIds.ToArray());
        TileMatrixSetId = tileMatrixSetId;
        TileSize = tileSize;
        Transparent = transparent;
        Format = format;
        Overwrite = overwrite;
        TilesCompleted = tilesCompleted;
        TilesTotal = tilesTotal;
    }

    public Guid JobId { get; }
    public RasterTilePreseedJobStatus Status { get; }
    public double Progress { get; }
    public string Stage { get; }
    public string? Message { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset? CompletedAtUtc { get; }
    public IReadOnlyList<string> DatasetIds { get; }
    public string TileMatrixSetId { get; }
    public int TileSize { get; }
    public bool Transparent { get; }
    public string Format { get; }
    public bool Overwrite { get; }
    public long TilesCompleted { get; }
    public long TilesTotal { get; }
}
