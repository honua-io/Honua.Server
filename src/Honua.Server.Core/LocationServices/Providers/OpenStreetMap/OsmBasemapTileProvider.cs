// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.LocationServices.Models;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.LocationServices.Providers.OpenStreetMap;

/// <summary>
/// OpenStreetMap tile provider implementation.
/// Uses public OSM tile servers (rate-limited, for development only).
/// For production, use your own tile server or commercial provider.
/// API Reference: https://wiki.openstreetmap.org/wiki/Raster_tile_providers
/// </summary>
public class OsmBasemapTileProvider : IBasemapTileProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OsmBasemapTileProvider> _logger;
    private readonly string _userAgent;

    public string ProviderKey => "openstreetmap";
    public string ProviderName => "OpenStreetMap Tiles";

    private static readonly Dictionary<string, BasemapTileset> AvailableTilesets = new()
    {
        ["osm-standard"] = new BasemapTileset
        {
            Id = "osm-standard",
            Name = "OpenStreetMap Standard",
            Description = "Standard OSM map style",
            Format = TileFormat.Raster,
            TileSize = 256,
            MaxZoom = 19,
            Attribution = "© OpenStreetMap contributors",
            TileUrlTemplate = "https://tile.openstreetmap.org/{z}/{x}/{y}.png"
        },
        ["osm-humanitarian"] = new BasemapTileset
        {
            Id = "osm-humanitarian",
            Name = "Humanitarian OSM",
            Description = "Humanitarian response focused map style",
            Format = TileFormat.Raster,
            TileSize = 256,
            MaxZoom = 20,
            Attribution = "© OpenStreetMap contributors, HOT",
            TileUrlTemplate = "https://tile-a.openstreetmap.fr/hot/{z}/{x}/{y}.png"
        },
        ["osm-topo"] = new BasemapTileset
        {
            Id = "osm-topo",
            Name = "OpenTopoMap",
            Description = "Topographic map with elevation contours",
            Format = TileFormat.Raster,
            TileSize = 256,
            MaxZoom = 17,
            Attribution = "© OpenStreetMap contributors, OpenTopoMap",
            TileUrlTemplate = "https://tile.opentopomap.org/{z}/{x}/{y}.png"
        },
        ["osm-cycle"] = new BasemapTileset
        {
            Id = "osm-cycle",
            Name = "CyclOSM",
            Description = "Bicycle-focused map style",
            Format = TileFormat.Raster,
            TileSize = 256,
            MaxZoom = 20,
            Attribution = "© OpenStreetMap contributors, CyclOSM",
            TileUrlTemplate = "https://tile.thunderforest.com/cycle/{z}/{x}/{y}.png" // Note: Requires API key for production
        }
    };

    public OsmBasemapTileProvider(
        HttpClient httpClient,
        ILogger<OsmBasemapTileProvider> logger,
        string? userAgent = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _userAgent = userAgent ?? "HonuaServer/1.0";

        // OSM tile servers require a User-Agent header
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", _userAgent);
        }
    }

    public Task<IReadOnlyList<BasemapTileset>> GetAvailableTilesetsAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<BasemapTileset> tilesets = AvailableTilesets.Values.ToList();
        return Task.FromResult(tilesets);
    }

    public async Task<TileResponse> GetTileAsync(
        TileRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!AvailableTilesets.TryGetValue(request.TilesetId, out var tileset))
            {
                throw new ArgumentException($"Unknown tileset: {request.TilesetId}", nameof(request));
            }

            var url = tileset.TileUrlTemplate
                .Replace("{z}", request.Z.ToString())
                .Replace("{x}", request.X.ToString())
                .Replace("{y}", request.Y.ToString());

            _logger.LogDebug("OSM tile request: {Url}", url);

            // OSM tile usage policy: respect rate limits
            await Task.Delay(100, cancellationToken);

            var tileUri = new Uri(url, UriKind.Absolute);
            var response = await _httpClient.GetAsync(tileUri, cancellationToken);
            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/png";

            return new TileResponse
            {
                Data = data,
                ContentType = contentType,
                CacheControl = response.Headers.CacheControl?.ToString(),
                ETag = response.Headers.ETag?.Tag
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "OSM tile request failed for {TilesetId} {Z}/{X}/{Y}",
                request.TilesetId, request.Z, request.X, request.Y);
            throw new InvalidOperationException($"OSM tile request failed: {ex.Message}", ex);
        }
    }

    public Task<string> GetTileUrlTemplateAsync(
        string tilesetId,
        CancellationToken cancellationToken = default)
    {
        if (!AvailableTilesets.TryGetValue(tilesetId, out var tileset))
        {
            throw new ArgumentException($"Unknown tileset: {tilesetId}", nameof(tilesetId));
        }

        return Task.FromResult(tileset.TileUrlTemplate);
    }

    public async Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Test by fetching a single tile (zoom 0, x 0, y 0)
            var testRequest = new TileRequest
            {
                TilesetId = "osm-standard",
                Z = 0,
                X = 0,
                Y = 0
            };

            await GetTileAsync(testRequest, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OSM tile connectivity test failed");
            return false;
        }
    }
}
