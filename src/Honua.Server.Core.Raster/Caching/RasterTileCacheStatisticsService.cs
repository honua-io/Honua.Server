// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Caching;

public sealed class RasterTileCacheStatisticsService : IRasterTileCacheStatisticsService
{
    private readonly IRasterTileCacheMetadataStore _metadataStore;
    private readonly IRasterDatasetRegistry _datasetRegistry;

    private long _totalHits;
    private long _totalMisses;
    private DateTimeOffset _lastResetUtc = DateTimeOffset.UtcNow;

    public RasterTileCacheStatisticsService(
        IRasterTileCacheMetadataStore metadataStore,
        IRasterDatasetRegistry datasetRegistry)
    {
        _metadataStore = metadataStore ?? throw new ArgumentNullException(nameof(metadataStore));
        _datasetRegistry = datasetRegistry ?? throw new ArgumentNullException(nameof(datasetRegistry));
    }

    public void RecordHit() => Interlocked.Increment(ref _totalHits);
    public void RecordMiss() => Interlocked.Increment(ref _totalMisses);

    public async Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var datasets = await _datasetRegistry.GetAllAsync(cancellationToken);
        var datasetStats = new List<DatasetCacheMetadata>();

        long totalTiles = 0;
        long totalSize = 0;
        var tilesByFormat = new Dictionary<string, long>();
        var tilesByZoom = new Dictionary<int, long>();

        foreach (var dataset in datasets)
        {
            var metadata = await _metadataStore.GetDatasetMetadataAsync(dataset.Id, cancellationToken);
            datasetStats.Add(metadata);

            totalTiles += metadata.TotalTiles;
            totalSize += metadata.TotalSizeBytes;
        }

        var hits = Interlocked.Read(ref _totalHits);
        var misses = Interlocked.Read(ref _totalMisses);
        var total = hits + misses;
        var hitRate = total > 0 ? (double)hits / total : 0.0;

        return new CacheStatistics(
            TotalTiles: totalTiles,
            TotalSizeBytes: totalSize,
            HitRate: hitRate,
            Hits: hits,
            Misses: misses,
            DatasetCount: datasets.Count,
            LastUpdatedUtc: DateTimeOffset.UtcNow,
            TilesByFormat: tilesByFormat,
            TilesByZoomLevel: tilesByZoom);
    }

    public async Task<DatasetCacheStatistics?> GetDatasetStatisticsAsync(string datasetId, CancellationToken cancellationToken = default)
    {
        var metadata = await _metadataStore.GetDatasetMetadataAsync(datasetId, cancellationToken);
        if (metadata.TotalTiles == 0)
        {
            return null;
        }

        return new DatasetCacheStatistics(
            DatasetId: datasetId,
            TileCount: metadata.TotalTiles,
            SizeBytes: metadata.TotalSizeBytes,
            HitRate: 0, // Would need per-dataset hit tracking
            Hits: 0,
            Misses: 0,
            OldestTileUtc: metadata.OldestTileUtc,
            NewestTileUtc: metadata.NewestTileUtc,
            MinZoomLevel: metadata.MinZoomLevel,
            MaxZoomLevel: metadata.MaxZoomLevel,
            TilesByFormat: new Dictionary<string, long>(),
            TilesByZoomLevel: new Dictionary<int, long>());
    }

    public async Task<IReadOnlyList<DatasetCacheStatistics>> GetAllDatasetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var datasets = await _datasetRegistry.GetAllAsync(cancellationToken);
        var results = new List<DatasetCacheStatistics>();

        foreach (var dataset in datasets)
        {
            var stats = await GetDatasetStatisticsAsync(dataset.Id, cancellationToken);
            if (stats != null)
            {
                results.Add(stats);
            }
        }

        return results;
    }

    public Task ResetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Exchange(ref _totalHits, 0);
        Interlocked.Exchange(ref _totalMisses, 0);
        _lastResetUtc = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }
}
