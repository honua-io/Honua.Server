// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.LocationServices.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.LocationServices.Observability;

/// <summary>
/// Decorator that adds comprehensive monitoring and metrics to basemap tile providers.
/// Tracks tile requests by zoom level, tile size distribution, and format types (raster vs vector).
/// </summary>
public class MonitoredBasemapTileProvider : IBasemapTileProvider
{
    private readonly IBasemapTileProvider _inner;
    private readonly LocationServiceMetrics _metrics;
    private readonly ILogger<MonitoredBasemapTileProvider> _logger;
    private readonly IMemoryCache? _cache;
    private readonly TimeSpan _cacheDuration;

    private const string ProviderType = "basemap";

    public string ProviderKey => _inner.ProviderKey;
    public string ProviderName => _inner.ProviderName;

    /// <summary>
    /// Initializes a new monitored basemap tile provider decorator.
    /// </summary>
    /// <param name="inner">Inner basemap tile provider to wrap.</param>
    /// <param name="metrics">Metrics collector for location services.</param>
    /// <param name="logger">Logger for structured logging.</param>
    /// <param name="cache">Optional memory cache for tile data.</param>
    /// <param name="cacheDuration">Cache duration (default: 24 hours).</param>
    public MonitoredBasemapTileProvider(
        IBasemapTileProvider inner,
        LocationServiceMetrics metrics,
        ILogger<MonitoredBasemapTileProvider> logger,
        IMemoryCache? cache = null,
        TimeSpan? cacheDuration = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache;
        _cacheDuration = cacheDuration ?? TimeSpan.FromHours(24);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BasemapTileset>> GetAvailableTilesetsAsync(
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var operation = "get_tilesets";
        var cacheKey = $"tilesets:{ProviderKey}";

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["ProviderType"] = ProviderType,
            ["ProviderKey"] = ProviderKey,
            ["Operation"] = operation
        });

        try
        {
            // Check cache first
            if (_cache != null && _cache.TryGetValue(cacheKey, out IReadOnlyList<BasemapTileset>? cachedTilesets))
            {
                stopwatch.Stop();

                _metrics.RecordCacheOperation(ProviderType, operation, "hit");
                _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: true);
                _metrics.RecordRequestDuration(ProviderType, ProviderKey, operation, stopwatch.Elapsed.TotalMilliseconds);

                _logger.LogDebug("Cache hit for tilesets from provider: {Provider}", ProviderKey);

                return cachedTilesets!;
            }

            if (_cache != null)
            {
                _metrics.RecordCacheOperation(ProviderType, operation, "miss");
            }

            // Execute the actual request
            _logger.LogInformation("Fetching available tilesets from provider: {Provider}", ProviderKey);

            var tilesets = await _inner.GetAvailableTilesetsAsync(cancellationToken);
            stopwatch.Stop();

            // Record metrics
            _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: true);
            _metrics.RecordRequestDuration(ProviderType, ProviderKey, operation, stopwatch.Elapsed.TotalMilliseconds);

            _logger.LogInformation(
                "Retrieved {TilesetCount} tilesets from {Provider} in {DurationMs}ms",
                tilesets.Count,
                ProviderKey,
                stopwatch.Elapsed.TotalMilliseconds);

            // Cache the successful response
            if (_cache != null)
            {
                _cache.Set(cacheKey, tilesets, _cacheDuration);
            }

            return tilesets;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _metrics.RecordError(ProviderType, ProviderKey, operation, "cancelled");
            _logger.LogWarning("Tileset fetch request was cancelled after {DurationMs}ms",
                stopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: false);
            _metrics.RecordError(ProviderType, ProviderKey, operation, "http_error");

            _logger.LogError(ex,
                "HTTP error fetching tilesets from {Provider}: {ErrorMessage}",
                ProviderKey,
                ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: false);
            _metrics.RecordError(ProviderType, ProviderKey, operation, ex.GetType().Name.ToLowerInvariant());

            _logger.LogError(ex,
                "Unexpected error fetching tilesets from {Provider}: {ErrorMessage}",
                ProviderKey,
                ex.Message);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<TileResponse> GetTileAsync(
        TileRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var operation = "get_tile";
        var cacheKey = GenerateTileCacheKey(request);

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["ProviderType"] = ProviderType,
            ["ProviderKey"] = ProviderKey,
            ["Operation"] = operation,
            ["TilesetId"] = request.TilesetId,
            ["ZoomLevel"] = request.Z,
            ["X"] = request.X,
            ["Y"] = request.Y
        });

        try
        {
            // Check cache first
            if (_cache != null && _cache.TryGetValue(cacheKey, out TileResponse? cachedTile))
            {
                stopwatch.Stop();

                _metrics.RecordCacheOperation(ProviderType, operation, "hit");
                _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: true);
                _metrics.RecordRequestDuration(ProviderType, ProviderKey, operation, stopwatch.Elapsed.TotalMilliseconds);

                _logger.LogDebug("Cache hit for tile {Tileset}/{Z}/{X}/{Y}",
                    request.TilesetId, request.Z, request.X, request.Y);

                return cachedTile!;
            }

            if (_cache != null)
            {
                _metrics.RecordCacheOperation(ProviderType, operation, "miss");
            }

            // Execute the actual tile request
            _logger.LogDebug("Fetching tile {Tileset}/{Z}/{X}/{Y} from {Provider}",
                request.TilesetId, request.Z, request.X, request.Y, ProviderKey);

            var tile = await _inner.GetTileAsync(request, cancellationToken);
            stopwatch.Stop();

            // Determine tile format
            var tileFormat = DetermineTileFormat(tile.ContentType);

            // Record metrics
            _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: true);
            _metrics.RecordRequestDuration(ProviderType, ProviderKey, operation, stopwatch.Elapsed.TotalMilliseconds);
            _metrics.RecordTileRequest(ProviderKey, request.TilesetId, request.Z, tileFormat);
            _metrics.RecordTileSize(ProviderKey, request.TilesetId, tile.Data.Length, tileFormat);
            _metrics.RecordTileFormat(ProviderKey, tileFormat, tile.ContentType);

            _logger.LogInformation(
                "Tile {Tileset}/{Z}/{X}/{Y} fetched successfully in {DurationMs}ms. " +
                "Size: {SizeKb:F2}KB, Format: {Format}",
                request.TilesetId,
                request.Z,
                request.X,
                request.Y,
                stopwatch.Elapsed.TotalMilliseconds,
                tile.Data.Length / 1024.0,
                tileFormat);

            // Cache the successful response
            if (_cache != null)
            {
                _cache.Set(cacheKey, tile, _cacheDuration);
            }

            return tile;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _metrics.RecordError(ProviderType, ProviderKey, operation, "cancelled");
            _logger.LogWarning("Tile request was cancelled after {DurationMs}ms",
                stopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: false);
            _metrics.RecordError(ProviderType, ProviderKey, operation, "http_error");

            _logger.LogError(ex,
                "HTTP error fetching tile {Tileset}/{Z}/{X}/{Y}: {ErrorMessage}",
                request.TilesetId,
                request.Z,
                request.X,
                request.Y,
                ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: false);
            _metrics.RecordError(ProviderType, ProviderKey, operation, ex.GetType().Name.ToLowerInvariant());

            _logger.LogError(ex,
                "Unexpected error fetching tile {Tileset}/{Z}/{X}/{Y}: {ErrorMessage}",
                request.TilesetId,
                request.Z,
                request.X,
                request.Y,
                ex.Message);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<string> GetTileUrlTemplateAsync(
        string tilesetId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var operation = "get_tile_url_template";
        var cacheKey = $"tile-url:{ProviderKey}:{tilesetId}";

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["ProviderType"] = ProviderType,
            ["ProviderKey"] = ProviderKey,
            ["Operation"] = operation,
            ["TilesetId"] = tilesetId
        });

        try
        {
            // Check cache first
            if (_cache != null && _cache.TryGetValue(cacheKey, out string? cachedUrl))
            {
                stopwatch.Stop();

                _metrics.RecordCacheOperation(ProviderType, operation, "hit");
                _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: true);
                _metrics.RecordRequestDuration(ProviderType, ProviderKey, operation, stopwatch.Elapsed.TotalMilliseconds);

                _logger.LogDebug("Cache hit for tile URL template: {Tileset}", tilesetId);

                return cachedUrl!;
            }

            if (_cache != null)
            {
                _metrics.RecordCacheOperation(ProviderType, operation, "miss");
            }

            // Execute the actual request
            _logger.LogDebug("Fetching tile URL template for {Tileset} from {Provider}",
                tilesetId, ProviderKey);

            var urlTemplate = await _inner.GetTileUrlTemplateAsync(tilesetId, cancellationToken);
            stopwatch.Stop();

            // Record metrics
            _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: true);
            _metrics.RecordRequestDuration(ProviderType, ProviderKey, operation, stopwatch.Elapsed.TotalMilliseconds);

            _logger.LogInformation(
                "Tile URL template for {Tileset} retrieved in {DurationMs}ms",
                tilesetId,
                stopwatch.Elapsed.TotalMilliseconds);

            // Cache the successful response
            if (_cache != null)
            {
                _cache.Set(cacheKey, urlTemplate, _cacheDuration);
            }

            return urlTemplate;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _metrics.RecordError(ProviderType, ProviderKey, operation, "cancelled");
            _logger.LogWarning("Tile URL template request was cancelled after {DurationMs}ms",
                stopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: false);
            _metrics.RecordError(ProviderType, ProviderKey, operation, ex.GetType().Name.ToLowerInvariant());

            _logger.LogError(ex,
                "Error fetching tile URL template for {Tileset}: {ErrorMessage}",
                tilesetId,
                ex.Message);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var operation = "test_connectivity";

        try
        {
            _logger.LogDebug("Testing connectivity to basemap provider: {Provider}", ProviderKey);

            var isHealthy = await _inner.TestConnectivityAsync(cancellationToken);
            stopwatch.Stop();

            _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: isHealthy);
            _metrics.RecordRequestDuration(ProviderType, ProviderKey, operation, stopwatch.Elapsed.TotalMilliseconds);
            _metrics.UpdateBasemapProviderHealth(isHealthy);

            if (isHealthy)
            {
                _logger.LogInformation(
                    "Basemap provider {Provider} is healthy (responded in {DurationMs}ms)",
                    ProviderKey,
                    stopwatch.Elapsed.TotalMilliseconds);
            }
            else
            {
                _logger.LogWarning(
                    "Basemap provider {Provider} connectivity test failed",
                    ProviderKey);
            }

            return isHealthy;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: false);
            _metrics.RecordError(ProviderType, ProviderKey, operation, ex.GetType().Name.ToLowerInvariant());
            _metrics.UpdateBasemapProviderHealth(false);

            _logger.LogError(ex,
                "Error testing connectivity to basemap provider {Provider}: {ErrorMessage}",
                ProviderKey,
                ex.Message);

            return false;
        }
    }

    private static string GenerateTileCacheKey(TileRequest request)
    {
        var key = $"tile:{request.TilesetId}:{request.Z}:{request.X}:{request.Y}";

        if (request.Scale > 1)
            key += $":@{request.Scale}x";

        if (!string.IsNullOrEmpty(request.ImageFormat))
            key += $":{request.ImageFormat}";

        if (!string.IsNullOrEmpty(request.Language))
            key += $":{request.Language}";

        return key;
    }

    private static string DetermineTileFormat(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            var ct when ct.Contains("mapbox-vector-tile") => "vector",
            var ct when ct.Contains("vector-tile") => "vector",
            var ct when ct.Contains("pbf") => "vector",
            var ct when ct.Contains("mvt") => "vector",
            _ => "raster"
        };
    }
}
