// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Caching;

/// <summary>
/// Provides comprehensive cache statistics similar to GeoWebCache
/// </summary>
public interface IRasterTileCacheStatisticsService
{
    /// <summary>
    /// Get overall cache statistics
    /// </summary>
    Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get statistics for a specific dataset
    /// </summary>
    Task<DatasetCacheStatistics?> GetDatasetStatisticsAsync(string datasetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get statistics for all datasets
    /// </summary>
    Task<IReadOnlyList<DatasetCacheStatistics>> GetAllDatasetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reset statistics
    /// </summary>
    Task ResetStatisticsAsync(CancellationToken cancellationToken = default);
}

public sealed record CacheStatistics(
    long TotalTiles,
    long TotalSizeBytes,
    double HitRate,
    long Hits,
    long Misses,
    int DatasetCount,
    DateTimeOffset LastUpdatedUtc,
    IReadOnlyDictionary<string, long> TilesByFormat,
    IReadOnlyDictionary<int, long> TilesByZoomLevel);

public sealed record DatasetCacheStatistics(
    string DatasetId,
    long TileCount,
    long SizeBytes,
    double HitRate,
    long Hits,
    long Misses,
    DateTimeOffset? OldestTileUtc,
    DateTimeOffset? NewestTileUtc,
    int MinZoomLevel,
    int MaxZoomLevel,
    IReadOnlyDictionary<string, long> TilesByFormat,
    IReadOnlyDictionary<int, long> TilesByZoomLevel);
