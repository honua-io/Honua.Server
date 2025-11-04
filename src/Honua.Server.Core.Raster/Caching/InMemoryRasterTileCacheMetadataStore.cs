// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Data;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Raster.Caching;

/// <summary>
/// In-memory implementation of raster tile cache metadata store.
/// </summary>
/// <remarks>
/// Thread-safety: This implementation is thread-safe. Uses ConcurrentDictionary
/// with atomic AddOrUpdate operations to prevent race conditions during concurrent access.
/// </remarks>
public sealed class InMemoryRasterTileCacheMetadataStore : InMemoryStoreBase<TileCacheMetadata>, IRasterTileCacheMetadataStore
{
    /// <summary>
    /// Extracts the metadata key from a TileCacheMetadata entity.
    /// </summary>
    protected override string GetKey(TileCacheMetadata entity)
    {
        Guard.NotNull(entity);
        var baseKey = $"{entity.DatasetId}/{entity.TileMatrixSetId}/{entity.ZoomLevel}/{entity.TileCol}/{entity.TileRow}.{entity.Format}";
        return string.IsNullOrWhiteSpace(entity.Time) ? baseKey : $"{baseKey}?time={entity.Time}";
    }

    /// <summary>
    /// Records a tile access atomically.
    /// </summary>
    /// <remarks>
    /// Thread-safety: Uses UpdateAsync which delegates to ConcurrentDictionary.AddOrUpdate.
    /// The update function may be called multiple times in contention scenarios,
    /// but the final result will be consistent.
    /// </remarks>
    public async Task RecordTileAccessAsync(RasterTileCacheKey key, CancellationToken cancellationToken = default)
    {
        var metadataKey = GetMetadataKey(key);

        // Use base class UpdateAsync for atomic update
        var updated = await UpdateAsync(
            metadataKey,
            existing => existing with
            {
                LastAccessedUtc = DateTimeOffset.UtcNow,
                AccessCount = existing.AccessCount + 1
            },
            cancellationToken);

        if (updated == null)
        {
            throw new InvalidOperationException("Tile metadata not found");
        }
    }

    public Task RecordTileCreationAsync(RasterTileCacheKey key, long sizeBytes, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var metadata = new TileCacheMetadata(
            DatasetId: key.DatasetId,
            TileMatrixSetId: key.TileMatrixSetId,
            ZoomLevel: key.Zoom,
            TileCol: key.Column,
            TileRow: key.Row,
            Format: key.Format,
            SizeBytes: sizeBytes,
            CreatedUtc: now,
            LastAccessedUtc: now,
            AccessCount: 0,
            Time: key.Time);

        return PutAsync(metadata, cancellationToken);
    }

    public async Task RecordTileRemovalAsync(RasterTileCacheKey key, CancellationToken cancellationToken = default)
    {
        var metadataKey = GetMetadataKey(key);
        await DeleteAsync(metadataKey, cancellationToken);
    }

    public Task<TileCacheMetadata?> GetMetadataAsync(RasterTileCacheKey key, CancellationToken cancellationToken = default)
    {
        var metadataKey = GetMetadataKey(key);
        return GetAsync(metadataKey, cancellationToken);
    }

    public async Task<DatasetCacheMetadata> GetDatasetMetadataAsync(string datasetId, CancellationToken cancellationToken = default)
    {
        var tiles = await QueryAsync(m => m.DatasetId == datasetId, cancellationToken);

        if (tiles.Count == 0)
        {
            return new DatasetCacheMetadata(
                DatasetId: datasetId,
                TotalTiles: 0,
                TotalSizeBytes: 0,
                OldestTileUtc: null,
                NewestTileUtc: null,
                MinZoomLevel: 0,
                MaxZoomLevel: 0);
        }

        var metadata = new DatasetCacheMetadata(
            DatasetId: datasetId,
            TotalTiles: tiles.Count,
            TotalSizeBytes: tiles.Sum(t => t.SizeBytes),
            OldestTileUtc: tiles.Min(t => t.CreatedUtc),
            NewestTileUtc: tiles.Max(t => t.CreatedUtc),
            MinZoomLevel: tiles.Min(t => t.ZoomLevel),
            MaxZoomLevel: tiles.Max(t => t.ZoomLevel));

        return metadata;
    }

    public Task<IReadOnlyList<TileCacheMetadata>> GetAllTilesAsync(string datasetId, CancellationToken cancellationToken = default)
    {
        return QueryAsync(m => m.DatasetId == datasetId, cancellationToken);
    }

    private static string GetMetadataKey(RasterTileCacheKey key)
    {
        var baseKey = $"{key.DatasetId}/{key.TileMatrixSetId}/{key.Zoom}/{key.Column}/{key.Row}.{key.Format}";
        return string.IsNullOrWhiteSpace(key.Time) ? baseKey : $"{baseKey}?time={key.Time}";
    }
}
