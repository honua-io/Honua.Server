// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Caching;
using Honua.Server.Core.LocationServices.Models;
using Honua.Server.Core.Performance;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.LocationServices.Caching;

/// <summary>
/// Decorator that adds caching to an <see cref="IBasemapTileProvider"/>.
/// Uses distributed cache by default for large binary tile data with aggressive TTL (7 days).
/// Thread-safe implementation with cache metrics collection.
/// </summary>
public sealed class CachedBasemapTileProvider : IBasemapTileProvider, IDisposable
{
    private readonly IBasemapTileProvider _innerProvider;
    private readonly IMemoryCache? _memoryCache;
    private readonly IDistributedCache? _distributedCache;
    private readonly LocationServiceCacheConfiguration _config;
    private readonly CacheMetricsCollector? _metricsCollector;
    private readonly ILogger<CachedBasemapTileProvider> _logger;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private const string CacheNamePrefix = "location:tiles";
    private const string TilesetsCacheKey = "location:tiles:tilesets";

    /// <summary>
    /// Initializes a new instance of the <see cref="CachedBasemapTileProvider"/> class.
    /// </summary>
    public CachedBasemapTileProvider(
        IBasemapTileProvider innerProvider,
        LocationServiceCacheConfiguration config,
        ILogger<CachedBasemapTileProvider> logger,
        IMemoryCache? memoryCache = null,
        IDistributedCache? distributedCache = null,
        CacheMetricsCollector? metricsCollector = null)
    {
        _innerProvider = innerProvider ?? throw new ArgumentNullException(nameof(innerProvider));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryCache = memoryCache;
        _distributedCache = distributedCache;
        _metricsCollector = metricsCollector;

        if (_memoryCache == null && _distributedCache == null)
        {
            _logger.LogWarning(
                "CachedBasemapTileProvider initialized without any cache backend. Caching will be disabled.");
        }

        if (_distributedCache == null && _config.BasemapTiles.UseDistributedCache)
        {
            _logger.LogWarning(
                "BasemapTiles configured to use distributed cache, but no IDistributedCache is registered. " +
                "Falling back to memory cache which may exhaust memory with large tiles.");
        }
    }

    /// <inheritdoc />
    public string ProviderKey => _innerProvider.ProviderKey;

    /// <inheritdoc />
    public string ProviderName => $"{_innerProvider.ProviderName} (Cached)";

    /// <inheritdoc />
    public async Task<IReadOnlyList<BasemapTileset>> GetAvailableTilesetsAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_config.EnableCaching || !_config.BasemapTiles.EnableCaching)
        {
            return await _innerProvider.GetAvailableTilesetsAsync(cancellationToken).ConfigureAwait(false);
        }

        var cacheKey = BuildTilesetsCacheKey();

        // Try to get from cache
        var cachedTilesets = await GetFromCacheAsync<List<BasemapTileset>>(cacheKey, cancellationToken)
            .ConfigureAwait(false);

        if (cachedTilesets != null)
        {
            _metricsCollector?.RecordHit(CacheNamePrefix);
            _logger.LogDebug("Cache hit for available tilesets from provider: {Provider}", _innerProvider.ProviderKey);
            return cachedTilesets;
        }

        _metricsCollector?.RecordMiss(CacheNamePrefix);
        _logger.LogDebug("Cache miss for available tilesets from provider: {Provider}", _innerProvider.ProviderKey);

        // Get from provider
        var tilesets = await _innerProvider.GetAvailableTilesetsAsync(cancellationToken).ConfigureAwait(false);

        // Cache the tilesets (they rarely change)
        if (tilesets.Count > 0)
        {
            var ttl = CacheTtlPolicy.Long.ToTimeSpan(); // 24 hours for tileset metadata
            await SetCacheAsync(cacheKey, tilesets, ttl, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Cached {Count} tilesets from provider: {Provider}", tilesets.Count, _innerProvider.ProviderKey);
        }

        return tilesets;
    }

    /// <inheritdoc />
    public async Task<TileResponse> GetTileAsync(
        TileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_config.EnableCaching || !_config.BasemapTiles.EnableCaching)
        {
            return await _innerProvider.GetTileAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var cacheKey = BuildTileCacheKey(request);

        // Try to get from cache
        var cachedTile = await GetTileFromCacheAsync(cacheKey, cancellationToken).ConfigureAwait(false);

        if (cachedTile != null)
        {
            _metricsCollector?.RecordHit(CacheNamePrefix);
            _logger.LogDebug(
                "Cache hit for tile: {Tileset}/{Z}/{X}/{Y}",
                request.TilesetId,
                request.Z,
                request.X,
                request.Y);
            return cachedTile;
        }

        _metricsCollector?.RecordMiss(CacheNamePrefix);
        _logger.LogDebug(
            "Cache miss for tile: {Tileset}/{Z}/{X}/{Y}",
            request.TilesetId,
            request.Z,
            request.X,
            request.Y);

        // Get from provider
        var tile = await _innerProvider.GetTileAsync(request, cancellationToken).ConfigureAwait(false);

        // Check if tile should be cached based on size
        if (_config.ShouldCacheTile(tile.Data.Length))
        {
            var ttl = _config.GetTileTtl();
            await SetTileCacheAsync(cacheKey, tile, ttl, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug(
                "Cached tile: {Tileset}/{Z}/{X}/{Y} ({Size} bytes, TTL: {Ttl})",
                request.TilesetId,
                request.Z,
                request.X,
                request.Y,
                tile.Data.Length,
                ttl);
        }
        else
        {
            _logger.LogDebug(
                "Tile too large to cache: {Tileset}/{Z}/{X}/{Y} ({Size} bytes > {MaxSize} bytes)",
                request.TilesetId,
                request.Z,
                request.X,
                request.Y,
                tile.Data.Length,
                _config.BasemapTiles.MaxTileSizeBytes);
        }

        return tile;
    }

    /// <inheritdoc />
    public async Task<string> GetTileUrlTemplateAsync(
        string tilesetId,
        CancellationToken cancellationToken = default)
    {
        // URL templates rarely change, cache them
        if (!_config.EnableCaching || !_config.BasemapTiles.EnableCaching)
        {
            return await _innerProvider.GetTileUrlTemplateAsync(tilesetId, cancellationToken).ConfigureAwait(false);
        }

        var cacheKey = BuildTileUrlTemplateCacheKey(tilesetId);

        // Try to get from cache
        var cachedTemplate = await GetFromCacheAsync<string>(cacheKey, cancellationToken).ConfigureAwait(false);

        if (cachedTemplate != null)
        {
            _metricsCollector?.RecordHit(CacheNamePrefix);
            return cachedTemplate;
        }

        _metricsCollector?.RecordMiss(CacheNamePrefix);

        // Get from provider
        var template = await _innerProvider.GetTileUrlTemplateAsync(tilesetId, cancellationToken)
            .ConfigureAwait(false);

        // Cache the template (they rarely change)
        var ttl = CacheTtlPolicy.Long.ToTimeSpan(); // 24 hours
        await SetCacheAsync(cacheKey, template, ttl, cancellationToken).ConfigureAwait(false);

        return template;
    }

    /// <inheritdoc />
    public Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default)
    {
        // Pass through - don't cache connectivity tests
        return _innerProvider.TestConnectivityAsync(cancellationToken);
    }

    /// <summary>
    /// Builds a cache key for tiles.
    /// Format: "tile:{provider}:{tilesetId}:{z}:{x}:{y}:{scale}:{format}:{language}"
    /// </summary>
    private string BuildTileCacheKey(TileRequest request)
    {
        var keyBuilder = Honua.Server.Core.Caching.CacheKeyBuilder.ForTile(
            request.TilesetId ?? "default",
            request.Z,
            request.X,
            request.Y,
            request.ImageFormat ?? "default")
            .WithComponent(_innerProvider.ProviderKey);

        // Include scale if configured
        if (_config.BasemapTiles.CachePerScale && request.Scale != 1)
        {
            keyBuilder.WithComponent($"scale{request.Scale}");
        }

        // Include language if configured
        if (_config.BasemapTiles.CachePerLanguage && !string.IsNullOrWhiteSpace(request.Language))
        {
            keyBuilder.WithComponent(request.Language);
        }

        return keyBuilder.Build();
    }

    /// <summary>
    /// Builds a cache key for tilesets list.
    /// Format: "tilesets:{provider}"
    /// </summary>
    private string BuildTilesetsCacheKey()
    {
        return CacheKeyNormalizer.Combine("tilesets", _innerProvider.ProviderKey);
    }

    /// <summary>
    /// Builds a cache key for tile URL template.
    /// Format: "tile-template:{provider}:{tilesetId}"
    /// </summary>
    private string BuildTileUrlTemplateCacheKey(string tilesetId)
    {
        return CacheKeyNormalizer.Combine("tile-template", _innerProvider.ProviderKey, tilesetId);
    }

    /// <summary>
    /// Gets a tile from cache (preferring distributed cache for large binary data).
    /// </summary>
    private async Task<TileResponse?> GetTileFromCacheAsync(string key, CancellationToken cancellationToken)
    {
        // For tiles, prefer distributed cache due to size
        if (_distributedCache != null && _config.BasemapTiles.UseDistributedCache)
        {
            try
            {
                var bytes = await _distributedCache.GetAsync(key, cancellationToken).ConfigureAwait(false);
                if (bytes != null && bytes.Length > 0)
                {
                    return JsonSerializer.Deserialize<TileResponse>(bytes, JsonSerializerOptionsRegistry.Web);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve tile from distributed cache for key: {Key}", key);
            }
        }

        // Fall back to memory cache
        if (_memoryCache != null)
        {
            if (_memoryCache.TryGetValue(key, out TileResponse? tile) && tile != null)
            {
                return tile;
            }
        }

        return null;
    }

    /// <summary>
    /// Sets a tile in cache (preferring distributed cache for large binary data).
    /// </summary>
    private async Task SetTileCacheAsync(
        string key,
        TileResponse tile,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        if (ttl <= TimeSpan.Zero)
        {
            return;
        }

        // For tiles, prefer distributed cache
        if (_distributedCache != null && _config.BasemapTiles.UseDistributedCache)
        {
            try
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(tile, JsonSerializerOptionsRegistry.Web);
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl
                };
                await _distributedCache.SetAsync(key, bytes, options, cancellationToken).ConfigureAwait(false);

                // Update metrics
                _metricsCollector?.UpdateSizeBytes(CacheNamePrefix, bytes.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache tile in distributed cache for key: {Key}", key);
            }
        }
        else if (_memoryCache != null)
        {
            // Fall back to memory cache (not ideal for large tiles)
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl,
                Priority = Microsoft.Extensions.Caching.Memory.CacheItemPriority.Low, // Lower priority due to size
                Size = tile.Data.Length // Track size for eviction
            };
            _memoryCache.Set(key, tile, options);

            // Update metrics
            _metricsCollector?.UpdateSizeBytes(CacheNamePrefix, tile.Data.Length);
        }
    }

    /// <summary>
    /// Gets a value from cache (memory or distributed).
    /// </summary>
    private async Task<T?> GetFromCacheAsync<T>(string key, CancellationToken cancellationToken)
        where T : class
    {
        // Try memory cache first for metadata
        if (_memoryCache != null)
        {
            if (_memoryCache.TryGetValue(key, out T? value) && value != null)
            {
                return value;
            }
        }

        // Try distributed cache
        if (_distributedCache != null)
        {
            try
            {
                var bytes = await _distributedCache.GetAsync(key, cancellationToken).ConfigureAwait(false);
                if (bytes != null && bytes.Length > 0)
                {
                    var value = JsonSerializer.Deserialize<T>(bytes, JsonSerializerOptionsRegistry.Web);

                    // Populate memory cache if available
                    if (value != null && _memoryCache != null)
                    {
                        var options = CacheTtlPolicy.Medium.ToMemoryCacheOptions();
                        _memoryCache.Set(key, value, options);
                    }

                    return value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve value from distributed cache for key: {Key}", key);
            }
        }

        return null;
    }

    /// <summary>
    /// Sets a value in cache (memory and/or distributed).
    /// </summary>
    private async Task SetCacheAsync<T>(
        string key,
        T value,
        TimeSpan ttl,
        CancellationToken cancellationToken)
        where T : class
    {
        if (ttl <= TimeSpan.Zero)
        {
            return;
        }

        // Set in memory cache
        if (_memoryCache != null)
        {
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl,
                Priority = Microsoft.Extensions.Caching.Memory.CacheItemPriority.Normal
            };
            _memoryCache.Set(key, value, options);
        }

        // Set in distributed cache
        if (_distributedCache != null)
        {
            try
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonSerializerOptionsRegistry.Web);
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl
                };
                await _distributedCache.SetAsync(key, bytes, options, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache value in distributed cache for key: {Key}", key);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cacheLock.Dispose();
    }
}
