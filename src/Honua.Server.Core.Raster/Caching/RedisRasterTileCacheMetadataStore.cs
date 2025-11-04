// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Caching;

/// <summary>
/// Redis-backed implementation of IRasterTileCacheMetadataStore.
/// Stores tile cache metadata in Redis for distributed deployments.
/// </summary>
public sealed class RedisRasterTileCacheMetadataStore : IRasterTileCacheMetadataStore, IDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly ILogger<RedisRasterTileCacheMetadataStore> _logger;
    private readonly string _keyPrefix;
    private readonly TimeSpan _defaultTtl;
    private readonly JsonSerializerOptions _jsonOptions;

    public RedisRasterTileCacheMetadataStore(
        IConnectionMultiplexer redis,
        ILogger<RedisRasterTileCacheMetadataStore> logger,
        string keyPrefix = "honua:raster:tile:",
        TimeSpan? defaultTtl = null)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _keyPrefix = keyPrefix;
        _defaultTtl = defaultTtl ?? TimeSpan.FromDays(30);
        _database = _redis.GetDatabase();
        _jsonOptions = JsonSerializerOptionsRegistry.Web;

        _logger.LogInformation(
            "RedisRasterTileCacheMetadataStore initialized with prefix: {KeyPrefix}, TTL: {TTL}",
            _keyPrefix,
            _defaultTtl);
    }

    /// <summary>
    /// Records a tile access using Redis Lua script for atomic read-modify-write.
    /// Thread-safe: Uses Lua script to prevent race conditions between GET and SET operations.
    /// </summary>
    public async Task RecordTileAccessAsync(RasterTileCacheKey key, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = GetRedisKey(key);

            // Use Lua script for atomic read-modify-write to prevent race conditions
            // This ensures thread-safety even with concurrent access from multiple servers
            var luaScript = @"
                local json = redis.call('GET', KEYS[1])
                if not json then
                    return nil
                end

                local metadata = cjson.decode(json)
                metadata.lastAccessedUtc = ARGV[1]
                metadata.accessCount = (metadata.accessCount or 0) + 1

                local updated = cjson.encode(metadata)
                redis.call('SET', KEYS[1], updated, 'EX', ARGV[2])
                return updated
            ";

            var now = DateTimeOffset.UtcNow.ToString("O"); // ISO 8601 format
            var ttlSeconds = (long)_defaultTtl.TotalSeconds;

            var result = await _database.ScriptEvaluateAsync(
                luaScript,
                new RedisKey[] { redisKey },
                new RedisValue[] { now, ttlSeconds }
            );

            if (result.IsNull)
            {
                _logger.LogWarning("Cannot record access for non-existent tile: {Key}", GetMetadataKey(key));
                return;
            }

            _logger.LogDebug("Recorded access for tile: {Key}", GetMetadataKey(key));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording tile access for: {Key}", GetMetadataKey(key));
            throw;
        }
    }

    public async Task RecordTileCreationAsync(RasterTileCacheKey key, long sizeBytes, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = GetRedisKey(key);
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

            var json = JsonSerializer.Serialize(metadata, _jsonOptions);
            await _database.StringSetAsync(redisKey, json, _defaultTtl);

            // Also add to dataset index
            var datasetSetKey = GetDatasetSetKey(key.DatasetId);
            await _database.SetAddAsync(datasetSetKey, redisKey);

            _logger.LogDebug("Recorded tile creation: {Key}, Size: {Size} bytes", GetMetadataKey(key), sizeBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording tile creation for: {Key}", GetMetadataKey(key));
            throw;
        }
    }

    public async Task RecordTileRemovalAsync(RasterTileCacheKey key, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = GetRedisKey(key);
            await _database.KeyDeleteAsync(redisKey);

            // Remove from dataset index
            var datasetSetKey = GetDatasetSetKey(key.DatasetId);
            await _database.SetRemoveAsync(datasetSetKey, redisKey);

            _logger.LogDebug("Recorded tile removal: {Key}", GetMetadataKey(key));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording tile removal for: {Key}", GetMetadataKey(key));
            throw;
        }
    }

    public async Task<TileCacheMetadata?> GetMetadataAsync(RasterTileCacheKey key, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = GetRedisKey(key);
            var json = await _database.StringGetAsync(redisKey);

            if (json.IsNullOrEmpty)
            {
                _logger.LogDebug("Tile metadata not found: {Key}", GetMetadataKey(key));
                return null;
            }

            var metadata = JsonSerializer.Deserialize<TileCacheMetadata>(json!, _jsonOptions);
            _logger.LogDebug("Retrieved tile metadata: {Key}", GetMetadataKey(key));
            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tile metadata for: {Key}", GetMetadataKey(key));
            throw;
        }
    }

    public async Task<DatasetCacheMetadata> GetDatasetMetadataAsync(string datasetId, CancellationToken cancellationToken = default)
    {
        try
        {
            var tiles = await GetAllTilesAsync(datasetId, cancellationToken);

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

            _logger.LogDebug(
                "Retrieved dataset metadata: {DatasetId}, Tiles: {TileCount}, Size: {Size} bytes",
                datasetId,
                tiles.Count,
                metadata.TotalSizeBytes);

            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dataset metadata for: {DatasetId}", datasetId);
            throw;
        }
    }

    public async Task<IReadOnlyList<TileCacheMetadata>> GetAllTilesAsync(string datasetId, CancellationToken cancellationToken = default)
    {
        try
        {
            var datasetSetKey = GetDatasetSetKey(datasetId);
            var tileKeys = await _database.SetMembersAsync(datasetSetKey);

            if (tileKeys.Length == 0)
            {
                return Array.Empty<TileCacheMetadata>();
            }

            var tiles = new List<TileCacheMetadata>();
            foreach (var tileKey in tileKeys)
            {
                var json = await _database.StringGetAsync(tileKey.ToString());
                if (!json.IsNullOrEmpty)
                {
                    var tileMetadata = JsonSerializer.Deserialize<TileCacheMetadata>(json!, _jsonOptions);
                    if (tileMetadata != null)
                    {
                        tiles.Add(tileMetadata);
                    }
                }
                else
                {
                    // Clean up stale entry
                    await _database.SetRemoveAsync(datasetSetKey, tileKey);
                }
            }

            _logger.LogDebug("Retrieved {Count} tiles for dataset: {DatasetId}", tiles.Count, datasetId);
            return tiles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tiles for dataset: {DatasetId}", datasetId);
            throw;
        }
    }

    private string GetRedisKey(RasterTileCacheKey key)
    {
        return $"{_keyPrefix}{GetMetadataKey(key)}";
    }

    private static string GetMetadataKey(RasterTileCacheKey key)
    {
        var baseKey = $"{key.DatasetId}/{key.TileMatrixSetId}/{key.Zoom}/{key.Column}/{key.Row}.{key.Format}";
        return string.IsNullOrWhiteSpace(key.Time) ? baseKey : $"{baseKey}?time={key.Time}";
    }

    private string GetDatasetSetKey(string datasetId)
    {
        return $"{_keyPrefix}dataset:{datasetId}";
    }

    public void Dispose()
    {
        // Redis connection is managed by DI, don't dispose it here
    }
}
