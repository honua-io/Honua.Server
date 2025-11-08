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

namespace Honua.Server.Core.LocationServices.Providers.GoogleMaps;

/// <summary>
/// Google Maps implementation of basemap tile provider.
/// API Reference: https://developers.google.com/maps/documentation/tile
/// </summary>
public class GoogleMapsBasemapTileProvider : IBasemapTileProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string? _clientId;
    private readonly string? _clientSignature;
    private readonly ILogger<GoogleMapsBasemapTileProvider> _logger;
    private const string BaseUrl = "https://tile.googleapis.com";

    public string ProviderKey => "google-maps";
    public string ProviderName => "Google Maps Basemap";

    // Available Google Maps tilesets
    private static readonly Dictionary<string, BasemapTileset> AvailableTilesets = new()
    {
        ["roadmap"] = new BasemapTileset
        {
            Id = "roadmap",
            Name = "Road Map",
            Description = "Standard road map with street labels",
            Format = TileFormat.Raster,
            TileSize = 256,
            MaxZoom = 22,
            Attribution = "© Google Maps",
            TileUrlTemplate = $"{BaseUrl}/v1/2dtiles/{{z}}/{{x}}/{{y}}?session=&orientation=0"
        },
        ["satellite"] = new BasemapTileset
        {
            Id = "satellite",
            Name = "Satellite Imagery",
            Description = "High-resolution satellite imagery without labels",
            Format = TileFormat.Raster,
            TileSize = 256,
            MaxZoom = 22,
            Attribution = "© Google Maps",
            TileUrlTemplate = $"{BaseUrl}/v1/2dtiles/{{z}}/{{x}}/{{y}}?session=&orientation=0"
        },
        ["hybrid"] = new BasemapTileset
        {
            Id = "hybrid",
            Name = "Hybrid Map",
            Description = "Satellite imagery with road labels overlay",
            Format = TileFormat.Raster,
            TileSize = 256,
            MaxZoom = 22,
            Attribution = "© Google Maps",
            TileUrlTemplate = $"{BaseUrl}/v1/2dtiles/{{z}}/{{x}}/{{y}}?session=&orientation=0"
        },
        ["terrain"] = new BasemapTileset
        {
            Id = "terrain",
            Name = "Terrain Map",
            Description = "Physical terrain with elevation shading",
            Format = TileFormat.Raster,
            TileSize = 256,
            MaxZoom = 22,
            Attribution = "© Google Maps",
            TileUrlTemplate = $"{BaseUrl}/v1/2dtiles/{{z}}/{{x}}/{{y}}?session=&orientation=0"
        }
    };

    // Map type mapping for Google Maps
    private static readonly Dictionary<string, string> MapTypeMapping = new()
    {
        ["roadmap"] = "roadmap",
        ["satellite"] = "satellite",
        ["hybrid"] = "hybrid",
        ["terrain"] = "terrain"
    };

    public GoogleMapsBasemapTileProvider(
        HttpClient httpClient,
        string apiKey,
        ILogger<GoogleMapsBasemapTileProvider> logger,
        string? clientId = null,
        string? clientSignature = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clientId = clientId;
        _clientSignature = clientSignature;

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
            _logger.LogDebug("Google Maps tile request: {Url}", url);

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
            _logger.LogError(ex, "Google Maps tile request failed for {TilesetId} {Z}/{X}/{Y}",
                request.TilesetId, request.Z, request.X, request.Y);
            throw new InvalidOperationException($"Google Maps tile request failed: {ex.Message}", ex);
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

        // Build URL template with authentication
        var template = BuildUrlTemplate(tilesetId);
        return Task.FromResult(template);
    }

    public async Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Test by fetching a single tile (zoom 0, x 0, y 0 is always valid)
            var testRequest = new TileRequest
            {
                TilesetId = "roadmap",
                Z = 0,
                X = 0,
                Y = 0
            };

            await GetTileAsync(testRequest, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Maps tile connectivity test failed");
            return false;
        }
    }

    private string BuildTileUrl(TileRequest request)
    {
        if (!AvailableTilesets.TryGetValue(request.TilesetId, out _))
        {
            throw new ArgumentException($"Unknown tileset: {request.TilesetId}", nameof(request));
        }

        // Google Maps Tile API v1 format
        var queryParams = new List<string>();

        AddAuthenticationParams(queryParams);

        // Map type (layerType parameter for different map types)
        if (MapTypeMapping.TryGetValue(request.TilesetId, out var mapType))
        {
            if (mapType != "roadmap")
            {
                queryParams.Add($"layerTypes={mapType}");
            }
        }

        // Scale for high-DPI displays
        if (request.Scale > 1)
        {
            queryParams.Add($"scale={request.Scale}");
        }

        // Language
        if (!string.IsNullOrEmpty(request.Language))
        {
            queryParams.Add($"language={request.Language}");
        }

        // Image format
        if (!string.IsNullOrEmpty(request.ImageFormat))
        {
            queryParams.Add($"format={request.ImageFormat}");
        }

        // Session token (for API usage tracking)
        queryParams.Add("session=");
        queryParams.Add("orientation=0");

        return $"/v1/2dtiles/{request.Z}/{request.X}/{request.Y}?{string.Join("&", queryParams)}";
    }

    private string BuildUrlTemplate(string tilesetId)
    {
        var queryParams = new List<string>();

        AddAuthenticationParams(queryParams);

        // Map type
        if (MapTypeMapping.TryGetValue(tilesetId, out var mapType))
        {
            if (mapType != "roadmap")
            {
                queryParams.Add($"layerTypes={mapType}");
            }
        }

        queryParams.Add("session=");
        queryParams.Add("orientation=0");

        return $"{BaseUrl}/v1/2dtiles/{{z}}/{{x}}/{{y}}?{string.Join("&", queryParams)}";
    }

    private void AddAuthenticationParams(List<string> queryParams)
    {
        // Use API Key or Premium Plan client ID/signature
        if (!string.IsNullOrEmpty(_clientId) && !string.IsNullOrEmpty(_clientSignature))
        {
            queryParams.Add($"client={_clientId}");
            queryParams.Add($"signature={_clientSignature}");
        }
        else
        {
            queryParams.Add($"key={_apiKey}");
        }
    }
}
