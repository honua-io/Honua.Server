// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Caching;

/// <summary>
/// Stores metadata about cached tiles for quota management and statistics
/// </summary>
public interface IRasterTileCacheMetadataStore
{
    Task RecordTileAccessAsync(RasterTileCacheKey key, CancellationToken cancellationToken = default);
    Task RecordTileCreationAsync(RasterTileCacheKey key, long sizeBytes, CancellationToken cancellationToken = default);
    Task RecordTileRemovalAsync(RasterTileCacheKey key, CancellationToken cancellationToken = default);
    Task<TileCacheMetadata?> GetMetadataAsync(RasterTileCacheKey key, CancellationToken cancellationToken = default);
    Task<DatasetCacheMetadata> GetDatasetMetadataAsync(string datasetId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TileCacheMetadata>> GetAllTilesAsync(string datasetId, CancellationToken cancellationToken = default);
}

public sealed record TileCacheMetadata(
    string DatasetId,
    string TileMatrixSetId,
    int ZoomLevel,
    int TileCol,
    int TileRow,
    string Format,
    long SizeBytes,
    DateTimeOffset CreatedUtc,
    DateTimeOffset LastAccessedUtc,
    long AccessCount,
    string? Time = null);

public sealed record DatasetCacheMetadata(
    string DatasetId,
    long TotalTiles,
    long TotalSizeBytes,
    DateTimeOffset? OldestTileUtc,
    DateTimeOffset? NewestTileUtc,
    int MinZoomLevel,
    int MaxZoomLevel);
