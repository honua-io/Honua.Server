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

namespace Honua.Server.Core.LocationServices.Providers.AwsLocation;

/// <summary>
/// AWS Location Service implementation of basemap tile provider.
/// API Reference: https://docs.aws.amazon.com/location/latest/developerguide/map-concepts.html
/// </summary>
public class AwsLocationBasemapTileProvider : IBasemapTileProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _accessKeyId;
    private readonly string _secretAccessKey;
    private readonly string _region;
    private readonly string _mapName;
    private readonly ILogger<AwsLocationBasemapTileProvider> _logger;
    private readonly AwsSignatureHelper _signatureHelper;

    public string ProviderKey => "aws-location";
    public string ProviderName => "AWS Location Service Basemap";

    // Available AWS Location Service map styles
    private readonly Dictionary<string, BasemapTileset> _availableTilesets;

    public AwsLocationBasemapTileProvider(
        HttpClient httpClient,
        string accessKeyId,
        string secretAccessKey,
        string region,
        string mapName,
        ILogger<AwsLocationBasemapTileProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _accessKeyId = accessKeyId ?? throw new ArgumentNullException(nameof(accessKeyId));
        _secretAccessKey = secretAccessKey ?? throw new ArgumentNullException(nameof(secretAccessKey));
        _region = region ?? throw new ArgumentNullException(nameof(region));
        _mapName = mapName ?? throw new ArgumentNullException(nameof(mapName));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _signatureHelper = new AwsSignatureHelper(_accessKeyId, _secretAccessKey, _region, "geo-maps");

        _availableTilesets = InitializeTilesets();
    }

    private Dictionary<string, BasemapTileset> InitializeTilesets()
    {
        var baseUrl = $"https://maps.geo.{_region}.amazonaws.com/maps/v0/maps/{_mapName}/tiles";

        return new Dictionary<string, BasemapTileset>
        {
            ["standard"] = new BasemapTileset
            {
                Id = "standard",
                Name = "Standard Map",
                Description = "Standard road map with street labels",
                Format = TileFormat.Raster,
                TileSize = 256,
                MaxZoom = 20,
                Attribution = "© AWS Location Service, © OpenStreetMap contributors",
                TileUrlTemplate = $"{baseUrl}/{{z}}/{{x}}/{{y}}"
            },
            ["satellite"] = new BasemapTileset
            {
                Id = "satellite",
                Name = "Satellite Imagery",
                Description = "High-resolution satellite imagery",
                Format = TileFormat.Raster,
                TileSize = 256,
                MaxZoom = 19,
                Attribution = "© AWS Location Service, © Maxar",
                TileUrlTemplate = $"{baseUrl}/{{z}}/{{x}}/{{y}}"
            },
            ["hybrid"] = new BasemapTileset
            {
                Id = "hybrid",
                Name = "Hybrid Map",
                Description = "Satellite imagery with road labels overlay",
                Format = TileFormat.Raster,
                TileSize = 256,
                MaxZoom = 19,
                Attribution = "© AWS Location Service, © Maxar, © OpenStreetMap contributors",
                TileUrlTemplate = $"{baseUrl}/{{z}}/{{x}}/{{y}}"
            },
            ["vector"] = new BasemapTileset
            {
                Id = "vector",
                Name = "Vector Map",
                Description = "Vector tiles for customizable rendering",
                Format = TileFormat.Vector,
                TileSize = 512,
                MaxZoom = 20,
                Attribution = "© AWS Location Service, © OpenStreetMap contributors",
                TileUrlTemplate = $"{baseUrl}/{{z}}/{{x}}/{{y}}"
            }
        };
    }

    public Task<IReadOnlyList<BasemapTileset>> GetAvailableTilesetsAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<BasemapTileset> tilesets = _availableTilesets.Values.ToList();
        return Task.FromResult(tilesets);
    }

    public async Task<TileResponse> GetTileAsync(
        TileRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_availableTilesets.ContainsKey(request.TilesetId))
            {
                throw new ArgumentException($"Unknown tileset: {request.TilesetId}", nameof(request));
            }

            var url = BuildTileUrl(request);
            _logger.LogDebug("AWS Location Service tile request: {TilesetId} {Z}/{X}/{Y}",
                request.TilesetId, request.Z, request.X, request.Y);

            var httpRequest = await _signatureHelper.CreateSignedGetRequestAsync(url, cancellationToken);

            var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
            httpResponse.EnsureSuccessStatusCode();

            var data = await httpResponse.Content.ReadAsByteArrayAsync(cancellationToken);
            var contentType = httpResponse.Content.Headers.ContentType?.MediaType ?? "image/png";

            return new TileResponse
            {
                Data = data,
                ContentType = contentType,
                CacheControl = httpResponse.Headers.CacheControl?.ToString(),
                ETag = httpResponse.Headers.ETag?.Tag
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "AWS Location Service tile request failed for {TilesetId} {Z}/{X}/{Y}",
                request.TilesetId, request.Z, request.X, request.Y);
            throw new InvalidOperationException($"AWS Location Service tile request failed: {ex.Message}", ex);
        }
    }

    public async Task<string> GetTileUrlTemplateAsync(
        string tilesetId,
        CancellationToken cancellationToken = default)
    {
        if (!_availableTilesets.TryGetValue(tilesetId, out var tileset))
        {
            throw new ArgumentException($"Unknown tileset: {tilesetId}", nameof(tilesetId));
        }

        // For AWS Location Service, we need to generate signed URLs
        // However, for client-side rendering, we would need to implement a proxy endpoint
        // that signs the requests server-side, or use AWS Cognito for client-side auth
        // For now, return the base template (server will need to sign each request)
        await Task.CompletedTask;
        return tileset.TileUrlTemplate;
    }

    public async Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Test by fetching a single tile (zoom 0, x 0, y 0 is always valid)
            var testRequest = new TileRequest
            {
                TilesetId = "standard",
                Z = 0,
                X = 0,
                Y = 0
            };

            await GetTileAsync(testRequest, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AWS Location Service tile connectivity test failed");
            return false;
        }
    }

    private string BuildTileUrl(TileRequest request)
    {
        var url = $"https://maps.geo.{_region}.amazonaws.com/maps/v0/maps/{_mapName}/tiles/{request.Z}/{request.X}/{request.Y}";

        var queryParams = new List<string>();

        // Add optional parameters
        if (!string.IsNullOrEmpty(request.ImageFormat))
        {
            // AWS Location Service supports png format for raster tiles
            if (request.ImageFormat.ToLowerInvariant() != "png")
            {
                _logger.LogWarning("AWS Location Service only supports PNG format, requested: {Format}", request.ImageFormat);
            }
        }

        // Vector tiles are MVT format
        var tileset = _availableTilesets[request.TilesetId];
        if (tileset.Format == TileFormat.Vector)
        {
            queryParams.Add("format=mvt");
        }

        if (queryParams.Count > 0)
        {
            url += "?" + string.Join("&", queryParams);
        }

        return url;
    }
}
