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

namespace Honua.Server.Core.LocationServices.Providers.AzureMaps;

/// <summary>
/// Azure Maps implementation of basemap tile provider.
/// API Reference: https://learn.microsoft.com/en-us/rest/api/maps/render
/// </summary>
public class AzureMapsBasemapTileProvider : IBasemapTileProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _subscriptionKey;
    private readonly ILogger<AzureMapsBasemapTileProvider> _logger;
    private const string BaseUrl = "https://atlas.microsoft.com";
    private const string ApiVersion = "2024-07-01";

    public string ProviderKey => "azure-maps";
    public string ProviderName => "Azure Maps Basemap";

    // Available Azure Maps tilesets
    private static readonly Dictionary<string, BasemapTileset> AvailableTilesets = new()
    {
        ["road"] = new BasemapTileset
        {
            Id = "road",
            Name = "Road Map",
            Description = "Standard road map with street labels",
            Format = TileFormat.Raster,
            TileSize = 256,
            MaxZoom = 22,
            Attribution = "© Microsoft Azure Maps",
            TileUrlTemplate = $"{BaseUrl}/map/tile?api-version={ApiVersion}&tilesetId=microsoft.base.road&zoom={{z}}&x={{x}}&y={{y}}"
        },
        ["satellite"] = new BasemapTileset
        {
            Id = "satellite",
            Name = "Satellite Imagery",
            Description = "High-resolution satellite imagery",
            Format = TileFormat.Raster,
            TileSize = 256,
            MaxZoom = 22,
            Attribution = "© Microsoft Azure Maps",
            TileUrlTemplate = $"{BaseUrl}/map/tile?api-version={ApiVersion}&tilesetId=microsoft.imagery&zoom={{z}}&x={{x}}&y={{y}}"
        },
        ["hybrid"] = new BasemapTileset
        {
            Id = "hybrid",
            Name = "Hybrid Map",
            Description = "Satellite imagery with road labels overlay",
            Format = TileFormat.Raster,
            TileSize = 256,
            MaxZoom = 22,
            Attribution = "© Microsoft Azure Maps",
            TileUrlTemplate = $"{BaseUrl}/map/tile?api-version={ApiVersion}&tilesetId=microsoft.base.hybrid&zoom={{z}}&x={{x}}&y={{y}}"
        },
        ["dark"] = new BasemapTileset
        {
            Id = "dark",
            Name = "Dark Theme",
            Description = "Dark themed road map",
            Format = TileFormat.Raster,
            TileSize = 256,
            MaxZoom = 22,
            Attribution = "© Microsoft Azure Maps",
            TileUrlTemplate = $"{BaseUrl}/map/tile?api-version={ApiVersion}&tilesetId=microsoft.base.darkgrey&zoom={{z}}&x={{x}}&y={{y}}"
        },
        ["grayscale"] = new BasemapTileset
        {
            Id = "grayscale",
            Name = "Grayscale Map",
            Description = "Grayscale themed road map",
            Format = TileFormat.Raster,
            TileSize = 256,
            MaxZoom = 22,
            Attribution = "© Microsoft Azure Maps",
            TileUrlTemplate = $"{BaseUrl}/map/tile?api-version={ApiVersion}&tilesetId=microsoft.base.grayscale_dark&zoom={{z}}&x={{x}}&y={{y}}"
        },
        ["night"] = new BasemapTileset
        {
            Id = "night",
            Name = "Night Map",
            Description = "Night themed road map for low-light conditions",
            Format = TileFormat.Raster,
            TileSize = 256,
            MaxZoom = 22,
            Attribution = "© Microsoft Azure Maps",
            TileUrlTemplate = $"{BaseUrl}/map/tile?api-version={ApiVersion}&tilesetId=microsoft.base.night&zoom={{z}}&x={{x}}&y={{y}}"
        },
        ["terrain"] = new BasemapTileset
        {
            Id = "terrain",
            Name = "Terrain Map",
            Description = "Terrain with elevation shading",
            Format = TileFormat.Raster,
            TileSize = 256,
            MaxZoom = 22,
            Attribution = "© Microsoft Azure Maps",
            TileUrlTemplate = $"{BaseUrl}/map/tile?api-version={ApiVersion}&tilesetId=microsoft.weather.radar.main&zoom={{z}}&x={{x}}&y={{y}}"
        },
        ["vector-road"] = new BasemapTileset
        {
            Id = "vector-road",
            Name = "Vector Road Map",
            Description = "Vector tiles for road map",
            Format = TileFormat.Vector,
            TileSize = 512,
            MaxZoom = 22,
            Attribution = "© Microsoft Azure Maps",
            TileUrlTemplate = $"{BaseUrl}/map/tile?api-version={ApiVersion}&tilesetId=microsoft.base.road&zoom={{z}}&x={{x}}&y={{y}}&tileSize=512"
        }
    };

    public AzureMapsBasemapTileProvider(
        HttpClient httpClient,
        string subscriptionKey,
        ILogger<AzureMapsBasemapTileProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _subscriptionKey = subscriptionKey ?? throw new ArgumentNullException(nameof(subscriptionKey));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri(BaseUrl);
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
            var url = BuildTileUrl(request);
            _logger.LogDebug("Azure Maps tile request: {Url}", url);

            var response = await _httpClient.GetAsync(url, cancellationToken);
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
            _logger.LogError(ex, "Azure Maps tile request failed for {TilesetId} {Z}/{X}/{Y}",
                request.TilesetId, request.Z, request.X, request.Y);
            throw new InvalidOperationException($"Azure Maps tile request failed: {ex.Message}", ex);
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

        // Return URL template with subscription key
        var template = $"{tileset.TileUrlTemplate}&subscription-key={_subscriptionKey}";
        return Task.FromResult(template);
    }

    public async Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Test by fetching a single tile (zoom 0, x 0, y 0 is always valid)
            var testRequest = new TileRequest
            {
                TilesetId = "road",
                Z = 0,
                X = 0,
                Y = 0
            };

            await GetTileAsync(testRequest, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure Maps tile connectivity test failed");
            return false;
        }
    }

    private string BuildTileUrl(TileRequest request)
    {
        if (!AvailableTilesets.TryGetValue(request.TilesetId, out var tileset))
        {
            throw new ArgumentException($"Unknown tileset: {request.TilesetId}", nameof(request));
        }

        var queryParams = new List<string>
        {
            $"api-version={ApiVersion}",
            $"tilesetId={GetAzureTilesetId(request.TilesetId)}",
            $"zoom={request.Z}",
            $"x={request.X}",
            $"y={request.Y}",
            $"subscription-key={_subscriptionKey}"
        };

        if (request.Scale > 1)
        {
            queryParams.Add($"tileSize={tileset.TileSize * request.Scale}");
        }

        if (!string.IsNullOrEmpty(request.Language))
        {
            queryParams.Add($"language={request.Language}");
        }

        if (!string.IsNullOrEmpty(request.ImageFormat))
        {
            queryParams.Add($"format={request.ImageFormat}");
        }

        return $"/map/tile?{string.Join("&", queryParams)}";
    }

    private string GetAzureTilesetId(string tilesetId)
    {
        return tilesetId switch
        {
            "road" => "microsoft.base.road",
            "satellite" => "microsoft.imagery",
            "hybrid" => "microsoft.base.hybrid",
            "dark" => "microsoft.base.darkgrey",
            "grayscale" => "microsoft.base.grayscale_dark",
            "night" => "microsoft.base.night",
            "terrain" => "microsoft.dem.contours",
            "vector-road" => "microsoft.base.road",
            _ => "microsoft.base.road"
        };
    }
}
