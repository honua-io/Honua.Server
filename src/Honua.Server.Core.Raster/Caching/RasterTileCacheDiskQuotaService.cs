// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Observability;
using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Caching;

public sealed class RasterTileCacheDiskQuotaService : IRasterTileCacheDiskQuotaService
{
    private const long DefaultMaxSizeBytes = 10L * 1024 * 1024 * 1024; // 10 GB

    private readonly ConcurrentDictionary<string, DiskQuotaConfiguration> _quotas = new();
    private readonly IRasterTileCacheMetadataStore _metadataStore;
    private readonly IRasterTileCacheProvider _cacheProvider;
    private readonly ILogger<RasterTileCacheDiskQuotaService> _logger;

    public RasterTileCacheDiskQuotaService(
        IRasterTileCacheMetadataStore metadataStore,
        IRasterTileCacheProvider cacheProvider,
        ILogger<RasterTileCacheDiskQuotaService> logger)
    {
        _metadataStore = metadataStore ?? throw new ArgumentNullException(nameof(metadataStore));
        _cacheProvider = cacheProvider ?? throw new ArgumentNullException(nameof(cacheProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> IsWithinQuotaAsync(string datasetId, CancellationToken cancellationToken = default)
    {
        var status = await GetQuotaStatusAsync(datasetId, cancellationToken);
        return !status.IsOverQuota;
    }

    public async Task<DatasetQuotaStatus> GetQuotaStatusAsync(string datasetId, CancellationToken cancellationToken = default)
    {
        var quota = _quotas.GetOrAdd(datasetId, _ => new DiskQuotaConfiguration(DefaultMaxSizeBytes));
        var metadata = await _metadataStore.GetDatasetMetadataAsync(datasetId, cancellationToken);

        var usagePercent = quota.MaxSizeBytes > 0
            ? (double)metadata.TotalSizeBytes / quota.MaxSizeBytes * 100
            : 0;

        return new DatasetQuotaStatus(
            DatasetId: datasetId,
            CurrentSizeBytes: metadata.TotalSizeBytes,
            MaxSizeBytes: quota.MaxSizeBytes,
            UsagePercent: usagePercent,
            IsOverQuota: metadata.TotalSizeBytes > quota.MaxSizeBytes,
            TileCount: metadata.TotalTiles);
    }

    public async Task<QuotaEnforcementResult> EnforceQuotaAsync(string datasetId, CancellationToken cancellationToken = default)
    {
        var (result, duration) = await PerformanceMeasurement.MeasureWithDurationAsync(async () =>
        {
            var status = await GetQuotaStatusAsync(datasetId, cancellationToken);

            if (!status.IsOverQuota)
            {
                return new QuotaEnforcementResult(datasetId, 0, 0, TimeSpan.Zero);
            }

            _logger.LogInformation(
                "Dataset {DatasetId} is over quota: {CurrentSize} / {MaxSize} bytes ({UsagePercent:F1}%)",
                datasetId, status.CurrentSizeBytes, status.MaxSizeBytes, status.UsagePercent);

            var quota = _quotas.GetOrAdd(datasetId, _ => new DiskQuotaConfiguration(DefaultMaxSizeBytes));
            var tilesToRemove = await GetTilesForEvictionAsync(datasetId, quota.ExpirationPolicy, cancellationToken);

            var tilesRemoved = 0;
            long bytesFreed = 0;
            var targetBytes = (long)(quota.MaxSizeBytes * 0.9); // Target 90% of max to provide buffer

            foreach (var tile in tilesToRemove)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                // Check if we've freed enough space
                var currentStatus = await GetQuotaStatusAsync(datasetId, cancellationToken);
                if (currentStatus.CurrentSizeBytes <= targetBytes)
                {
                    _logger.LogInformation(
                        "Target quota achieved for {DatasetId}: {CurrentSize} / {MaxSize} bytes",
                        datasetId, currentStatus.CurrentSizeBytes, quota.MaxSizeBytes);
                    break;
                }

                try
                {
                    var key = new RasterTileCacheKey(
                        datasetId: tile.DatasetId,
                        tileMatrixSetId: tile.TileMatrixSetId,
                        zoom: tile.ZoomLevel,
                        row: tile.TileRow,
                        column: tile.TileCol,
                        styleId: "default",
                        format: tile.Format,
                        transparent: true,
                        tileSize: 256);

                    await _cacheProvider.RemoveAsync(key, cancellationToken);
                    await _metadataStore.RecordTileRemovalAsync(key, cancellationToken);

                    tilesRemoved++;
                    bytesFreed += tile.SizeBytes;

                    _logger.LogDebug(
                        "Removed tile {DatasetId}/{Zoom}/{Col}/{Row}, freed {Size} bytes",
                        datasetId, tile.ZoomLevel, tile.TileCol, tile.TileRow, tile.SizeBytes);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to remove tile {DatasetId}/{Zoom}/{Col}/{Row}",
                        datasetId, tile.ZoomLevel, tile.TileCol, tile.TileRow);
                }
            }

            return new QuotaEnforcementResult(datasetId, tilesRemoved, bytesFreed, TimeSpan.Zero);
        });

        _logger.LogInformation(
            "Quota enforcement for {DatasetId} completed: removed {TilesRemoved} tiles, freed {BytesFreed} bytes in {Duration}ms",
            datasetId, result.TilesRemoved, result.BytesFreed, duration.TotalMilliseconds);

        // Update the result with actual duration
        return new QuotaEnforcementResult(datasetId, result.TilesRemoved, result.BytesFreed, duration);
    }

    private async Task<List<TileCacheMetadata>> GetTilesForEvictionAsync(
        string datasetId,
        QuotaExpirationPolicy policy,
        CancellationToken cancellationToken)
    {
        var allTiles = await _metadataStore.GetAllTilesAsync(datasetId, cancellationToken);

        // Sort tiles based on eviction policy
        var sortedTiles = policy switch
        {
            QuotaExpirationPolicy.LeastRecentlyUsed => allTiles.OrderBy(t => t.LastAccessedUtc).ToList(),
            QuotaExpirationPolicy.LeastFrequentlyUsed => allTiles.OrderBy(t => t.AccessCount).ThenBy(t => t.LastAccessedUtc).ToList(),
            QuotaExpirationPolicy.OldestFirst => allTiles.OrderBy(t => t.CreatedUtc).ToList(),
            _ => allTiles.OrderBy(t => t.LastAccessedUtc).ToList()
        };

        return sortedTiles;
    }

    public Task UpdateQuotaAsync(string datasetId, DiskQuotaConfiguration quota, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(quota);

        if (quota.MaxSizeBytes <= 0)
        {
            throw new ArgumentException("MaxSizeBytes must be greater than 0", nameof(quota));
        }

        _quotas[datasetId] = quota;
        _logger.LogInformation(
            "Updated quota for {DatasetId}: {MaxSize} bytes, policy: {Policy}",
            datasetId, quota.MaxSizeBytes, quota.ExpirationPolicy);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<string, DiskQuotaConfiguration>> GetAllQuotasAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyDictionary<string, DiskQuotaConfiguration>>(_quotas.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
    }
}
