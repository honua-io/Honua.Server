// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Caching;
using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Cache;

/// <summary>
/// In-memory cache for Zarr chunks to reduce HTTP requests.
/// Implements LRU eviction policy based on memory pressure.
/// </summary>
public sealed class ZarrChunkCache : IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<ZarrChunkCache> _logger;
    private readonly ZarrChunkCacheOptions _options;

    public ZarrChunkCache(
        ILogger<ZarrChunkCache> logger,
        ZarrChunkCacheOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new ZarrChunkCacheOptions();

        _cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = _options.MaxCacheSizeBytes,
            ExpirationScanFrequency = TimeSpan.FromMinutes(5)
        });

        _logger.LogInformation(
            "Initialized Zarr chunk cache: MaxSize={MaxSize}MB, TTL={TTL}min",
            _options.MaxCacheSizeBytes / (1024 * 1024),
            _options.ChunkTtlMinutes);
    }

    /// <summary>
    /// Get a chunk from cache, or fetch using the provided factory function.
    /// </summary>
    public async Task<byte[]> GetOrFetchAsync(
        string zarrUri,
        string variableName,
        int[] chunkCoords,
        Func<Task<byte[]>> fetchFunc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fetchFunc);

        var cacheKey = BuildCacheKey(zarrUri, variableName, chunkCoords);

        // Try to get from cache
        if (_cache.TryGetValue(cacheKey, out byte[]? cachedData) && cachedData != null)
        {
            _logger.LogDebug("Zarr chunk cache HIT: {Key}", cacheKey);
            return cachedData;
        }

        // Cache miss - fetch data
        _logger.LogDebug("Zarr chunk cache MISS: {Key}", cacheKey);

        var data = await fetchFunc();

        // Store in cache
        var entryOptions = CacheOptionsBuilder.ForZarrChunks()
            .WithSize(data.Length)
            .BuildMemory();

        // Add eviction callback for metrics
        entryOptions.RegisterPostEvictionCallback((key, value, reason, state) =>
        {
            _logger.LogTrace("Zarr chunk evicted: {Key}, reason: {Reason}", key, reason);
        });

        _cache.Set(cacheKey, data, entryOptions);

        return data;
    }

    /// <summary>
    /// Invalidate a specific chunk.
    /// </summary>
    public void Invalidate(string zarrUri, string variableName, int[] chunkCoords)
    {
        var cacheKey = BuildCacheKey(zarrUri, variableName, chunkCoords);
        _cache.Remove(cacheKey);
        _logger.LogDebug("Invalidated Zarr chunk: {Key}", cacheKey);
    }

    /// <summary>
    /// Invalidate all chunks for a specific array.
    /// </summary>
    public void InvalidateArray(string zarrUri, string variableName)
    {
        // Note: MemoryCache doesn't support pattern-based eviction
        // For production use, consider using a cache implementation that supports this
        _logger.LogWarning(
            "InvalidateArray called for {ZarrUri}/{VariableName}, but MemoryCache doesn't support pattern eviction. " +
            "Consider implementing a custom cache or using Redis.",
            zarrUri, variableName);
    }

    /// <summary>
    /// Clear all cached chunks.
    /// </summary>
    public void Clear()
    {
        if (_cache is MemoryCache memCache)
        {
            memCache.Compact(1.0); // Compact 100% = clear all
            _logger.LogInformation("Cleared Zarr chunk cache");
        }
    }

    /// <summary>
    /// Get cache statistics.
    /// </summary>
    public ZarrChunkCacheStats GetStats()
    {
        // Note: MemoryCache doesn't expose internal metrics
        // For production, consider using a cache with built-in metrics
        return new ZarrChunkCacheStats
        {
            MaxSizeBytes = _options.MaxCacheSizeBytes,
            ChunkTtlMinutes = _options.ChunkTtlMinutes
        };
    }

    private string BuildCacheKey(string zarrUri, string variableName, int[] chunkCoords)
    {
        return CacheKeyGenerator.GenerateZarrChunkKey(zarrUri, variableName, chunkCoords);
    }

    public void Dispose()
    {
        _cache.Dispose();
    }
}

/// <summary>
/// Configuration options for Zarr chunk cache.
/// </summary>
public sealed class ZarrChunkCacheOptions
{
    /// <summary>
    /// Maximum cache size in bytes. Default: 256 MB.
    /// </summary>
    public long MaxCacheSizeBytes { get; set; } = 256 * 1024 * 1024;

    /// <summary>
    /// Time-to-live for cached chunks in minutes. Default: 60 minutes.
    /// </summary>
    public int ChunkTtlMinutes { get; set; } = 60;
}

/// <summary>
/// Cache statistics.
/// </summary>
public sealed class ZarrChunkCacheStats
{
    public long MaxSizeBytes { get; init; }
    public int ChunkTtlMinutes { get; init; }
}
