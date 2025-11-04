// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Honua.Server.Core.Caching;

/// <summary>
/// Service for invalidating distributed cache entries with pattern-based matching support.
/// Provides advanced invalidation capabilities beyond standard IDistributedCache.
/// </summary>
public interface IDistributedCacheInvalidationService
{
    /// <summary>
    /// Invalidates all cache entries matching the specified pattern.
    /// </summary>
    /// <param name="pattern">
    /// Redis glob pattern (e.g., "layer:123:*", "tile:*:256:*").
    /// Uses Redis SCAN for safe iteration in production.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of keys invalidated.</returns>
    Task<int> InvalidateByPatternAsync(string pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates all cache entries with the specified tag.
    /// </summary>
    /// <param name="tag">Tag name (e.g., "layer:123", "service:wfs").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of keys invalidated.</returns>
    Task<int> InvalidateByTagAsync(string tag, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates multiple specific cache keys in a single operation.
    /// </summary>
    /// <param name="keys">Cache keys to invalidate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of keys invalidated.</returns>
    Task<int> InvalidateKeysAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the cache is available and healthy.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if cache is available, false otherwise.</returns>
    Task<bool> IsCacheAvailableAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of distributed cache invalidation using StackExchange.Redis.
/// </summary>
public sealed class RedisCacheInvalidationService : IDistributedCacheInvalidationService
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<RedisCacheInvalidationService> _logger;
    private readonly string _instancePrefix;

    public RedisCacheInvalidationService(
        IConnectionMultiplexer? redis,
        IDistributedCache distributedCache,
        ILogger<RedisCacheInvalidationService> logger,
        string instancePrefix = "Honua:")
    {
        _redis = redis;
        _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _instancePrefix = instancePrefix;

        if (_redis == null)
        {
            _logger.LogWarning(
                "IConnectionMultiplexer is not available. Pattern-based invalidation will not work. " +
                "Register IConnectionMultiplexer in DI to enable advanced cache invalidation.");
        }
    }

    /// <inheritdoc/>
    public async Task<int> InvalidateByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        if (_redis == null)
        {
            _logger.LogWarning(
                "Redis connection multiplexer not available. Cannot invalidate by pattern: {Pattern}",
                pattern);
            return 0;
        }

        try
        {
            var db = _redis.GetDatabase();
            var server = GetServer();
            if (server == null)
            {
                _logger.LogWarning("No Redis server available for pattern invalidation");
                return 0;
            }

            var fullPattern = $"{_instancePrefix}{pattern}";
            _logger.LogInformation("Invalidating cache entries matching pattern: {Pattern}", fullPattern);

            var keys = new List<RedisKey>();
            await foreach (var key in server.KeysAsync(pattern: fullPattern, pageSize: 1000)
                .WithCancellation(cancellationToken))
            {
                keys.Add(key);

                // Batch delete every 1000 keys to avoid blocking Redis
                if (keys.Count >= 1000)
                {
                    await DeleteKeysAsync(db, keys, cancellationToken);
                    var deletedCount = keys.Count;
                    keys.Clear();
                    _logger.LogDebug("Deleted batch of {Count} keys matching {Pattern}", deletedCount, fullPattern);
                }
            }

            // Delete remaining keys
            if (keys.Count > 0)
            {
                await DeleteKeysAsync(db, keys, cancellationToken);
            }

            var totalCount = keys.Count;
            _logger.LogInformation("Invalidated {Count} cache entries matching pattern: {Pattern}", totalCount, fullPattern);
            return totalCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate cache by pattern: {Pattern}", pattern);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<int> InvalidateByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        // Tags are implemented as Redis Sets containing cache keys
        // When setting a cache entry with a tag, add the key to the tag's set
        // To invalidate by tag, get all keys from the set and delete them

        if (_redis == null)
        {
            _logger.LogWarning("Redis connection multiplexer not available. Cannot invalidate by tag: {Tag}", tag);
            return 0;
        }

        try
        {
            var db = _redis.GetDatabase();
            var tagSetKey = $"{_instancePrefix}tag:{tag}";

            // Get all keys associated with this tag
            var keys = await db.SetMembersAsync(tagSetKey);
            if (keys.Length == 0)
            {
                _logger.LogDebug("No keys found for tag: {Tag}", tag);
                return 0;
            }

            // Delete all keys
            var redisKeys = keys.Select(k => (RedisKey)k.ToString()).ToList();
            await DeleteKeysAsync(db, redisKeys, cancellationToken);

            // Delete the tag set itself
            await db.KeyDeleteAsync(tagSetKey);

            _logger.LogInformation("Invalidated {Count} cache entries with tag: {Tag}", keys.Length, tag);
            return keys.Length;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate cache by tag: {Tag}", tag);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<int> InvalidateKeysAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);

        var keyList = keys.ToList();
        if (keyList.Count == 0)
        {
            return 0;
        }

        try
        {
            // Use IDistributedCache for simple key removal
            var count = 0;
            foreach (var key in keyList)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                await _distributedCache.RemoveAsync($"{_instancePrefix}{key}", cancellationToken);
                count++;
            }

            _logger.LogInformation("Invalidated {Count} cache entries", count);
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate cache keys");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsCacheAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (_redis == null)
        {
            return false;
        }

        try
        {
            var db = _redis.GetDatabase();
            await db.PingAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache health check failed");
            return false;
        }
    }

    private IServer? GetServer()
    {
        if (_redis == null)
        {
            return null;
        }

        try
        {
            var endpoints = _redis.GetEndPoints();
            if (endpoints.Length == 0)
            {
                _logger.LogWarning("No Redis endpoints configured");
                return null;
            }

            // Use the first endpoint
            return _redis.GetServer(endpoints[0]);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Redis server");
            return null;
        }
    }

    private async Task DeleteKeysAsync(IDatabase db, IEnumerable<RedisKey> keys, CancellationToken cancellationToken)
    {
        var keyArray = keys.ToArray();
        if (keyArray.Length == 0)
        {
            return;
        }

        // Use batch for better performance
        var batch = db.CreateBatch();
        var tasks = keyArray.Select(k => batch.KeyDeleteAsync(k)).ToArray();
        batch.Execute();

        await Task.WhenAll(tasks);
    }
}

/// <summary>
/// Cache invalidation strategy for coordinating invalidation across different scenarios.
/// </summary>
public static class CacheInvalidationPatterns
{
    /// <summary>
    /// Invalidate all cache entries for a specific layer.
    /// </summary>
    /// <param name="serviceId">Service ID.</param>
    /// <param name="layerId">Layer ID.</param>
    /// <returns>Cache invalidation pattern.</returns>
    public static string ForLayer(string serviceId, string layerId)
    {
        return $"layer:{serviceId}:{layerId}:*";
    }

    /// <summary>
    /// Invalidate all tiles for a specific tile matrix set.
    /// </summary>
    /// <param name="tileMatrixSet">Tile matrix set identifier.</param>
    /// <returns>Cache invalidation pattern.</returns>
    public static string ForTileMatrixSet(string tileMatrixSet)
    {
        return $"tile:{tileMatrixSet}:*";
    }

    /// <summary>
    /// Invalidate all tiles at a specific zoom level.
    /// </summary>
    /// <param name="tileMatrixSet">Tile matrix set identifier.</param>
    /// <param name="z">Zoom level.</param>
    /// <returns>Cache invalidation pattern.</returns>
    public static string ForTileZoomLevel(string tileMatrixSet, int z)
    {
        return $"tile:{tileMatrixSet}:{z}:*";
    }

    /// <summary>
    /// Invalidate all query results for a specific layer.
    /// </summary>
    /// <param name="layerId">Layer ID.</param>
    /// <returns>Cache invalidation pattern.</returns>
    public static string ForQueryResults(string layerId)
    {
        return $"query:{layerId}:*";
    }

    /// <summary>
    /// Invalidate all metadata for a specific service.
    /// </summary>
    /// <param name="serviceId">Service ID.</param>
    /// <returns>Cache invalidation pattern.</returns>
    public static string ForServiceMetadata(string serviceId)
    {
        return $"metadata:{serviceId}:*";
    }

    /// <summary>
    /// Invalidate all STAC collection caches.
    /// </summary>
    /// <returns>Cache invalidation pattern.</returns>
    public static string ForStacCollections()
    {
        return "stac:collections:*";
    }

    /// <summary>
    /// Invalidate a specific STAC collection.
    /// </summary>
    /// <param name="collectionId">Collection ID.</param>
    /// <returns>Cache invalidation pattern.</returns>
    public static string ForStacCollection(string collectionId)
    {
        return $"stac:collection:{collectionId}:*";
    }
}
