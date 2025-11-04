// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Caching;
using Honua.Server.Core.Exceptions;
using Honua.Server.Core.Resilience;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Raster.Resilience;

/// <summary>
/// Resilient wrapper for raster tile service with error boundaries and fallback support.
/// Implements the circuit breaker pattern and stale cache fallback.
/// </summary>
public sealed class ResilientRasterTileService
{
    private readonly ILogger<ResilientRasterTileService> _logger;
    private readonly ResilientServiceExecutor _executor;
    private readonly IDistributedCache? _cache;

    public ResilientRasterTileService(
        ILogger<ResilientRasterTileService> logger,
        IDistributedCache? cache = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _executor = new ResilientServiceExecutor(logger);
        _cache = cache;
    }

    /// <summary>
    /// Gets a raster tile with automatic fallback to cached data if generation fails.
    /// </summary>
    public async Task<FallbackResult<byte[]>> GetTileWithFallbackAsync(
        Func<CancellationToken, Task<byte[]>> generateTile,
        string tileKey,
        CancellationToken cancellationToken = default)
    {
        if (_cache == null)
        {
            // No cache available, just execute with default fallback
            return await _executor.ExecuteWithDefaultAsync(
                generateTile,
                Array.Empty<byte>(),
                $"GenerateTile({tileKey})",
                cancellationToken).ConfigureAwait(false);
        }

        // Execute with stale cache fallback
        return await _executor.ExecuteWithStaleCacheFallbackAsync(
            primary: async ct =>
            {
                try
                {
                    var tile = await generateTile(ct).ConfigureAwait(false);

                    // Cache the successfully generated tile
                    if (tile.Length > 0)
                    {
                        await CacheTileAsync(tileKey, tile, ct).ConfigureAwait(false);
                    }

                    return tile;
                }
                catch (Exception ex) when (IsTransientRasterError(ex))
                {
                    throw new RasterProcessingException("Transient error generating raster tile", ex, isTransient: true);
                }
                catch (Exception ex)
                {
                    throw new RasterProcessingException("Error generating raster tile", ex, isTransient: false);
                }
            },
            getStaleCache: async ct => await GetStaleCachedTileAsync(tileKey, ct).ConfigureAwait(false),
            defaultValue: Array.Empty<byte>(),
            operationName: $"GenerateTile({tileKey})",
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task CacheTileAsync(string key, byte[] tile, CancellationToken cancellationToken)
    {
        if (_cache == null) return;

        try
        {
            var options = CacheOptionsBuilder.ForRasterTiles()
                .WithSlidingExpiration(TimeSpan.FromHours(6))
                .BuildDistributed();

            await _cache.SetAsync(key, tile, options, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache tile {TileKey}. Continuing without cache.", key);
            // Don't throw - caching is optional
        }
    }

    private async Task<byte[]?> GetStaleCachedTileAsync(string key, CancellationToken cancellationToken)
    {
        if (_cache == null) return null;

        try
        {
            return await _cache.GetAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve cached tile {TileKey}.", key);
            return null;
        }
    }

    private static bool IsTransientRasterError(Exception ex)
    {
        // Network errors, timeouts, and I/O errors are transient
        return ex is TimeoutException ||
               ex is System.Net.Http.HttpRequestException ||
               ex is System.IO.IOException ||
               ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase);
    }
}
