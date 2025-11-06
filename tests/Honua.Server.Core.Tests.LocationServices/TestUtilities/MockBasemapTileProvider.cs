// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.LocationServices;
using Honua.Server.Core.LocationServices.Models;

namespace Honua.Server.Core.Tests.LocationServices.TestUtilities;

/// <summary>
/// Mock basemap tile provider for testing.
/// </summary>
public class MockBasemapTileProvider : IBasemapTileProvider
{
    private IReadOnlyList<BasemapTileset>? _tilesets;
    private TileResponse? _nextTileResponse;
    private string? _tileUrlTemplate;
    private bool _isAvailable = true;
    private Exception? _nextException;

    public string ProviderKey { get; set; } = "mock";
    public string ProviderName { get; set; } = "Mock Basemap Provider";

    public void SetTilesets(IReadOnlyList<BasemapTileset> tilesets)
    {
        _tilesets = tilesets;
    }

    public void SetTileResponse(TileResponse response)
    {
        _nextTileResponse = response;
    }

    public void SetTileUrlTemplate(string template)
    {
        _tileUrlTemplate = template;
    }

    public void SetAvailability(bool isAvailable)
    {
        _isAvailable = isAvailable;
    }

    public void SetNextException(Exception exception)
    {
        _nextException = exception;
    }

    public void Reset()
    {
        _tilesets = null;
        _nextTileResponse = null;
        _tileUrlTemplate = null;
        _isAvailable = true;
        _nextException = null;
    }

    public Task<IReadOnlyList<BasemapTileset>> GetAvailableTilesetsAsync(
        CancellationToken cancellationToken = default)
    {
        if (_nextException != null)
        {
            var ex = _nextException;
            _nextException = null;
            throw ex;
        }

        if (_tilesets != null)
        {
            return Task.FromResult(_tilesets);
        }

        // Default tilesets
        return Task.FromResult<IReadOnlyList<BasemapTileset>>(new List<BasemapTileset>
        {
            new BasemapTileset
            {
                Id = "mock-standard",
                Name = "Mock Standard",
                Description = "Mock standard basemap",
                Format = TileFormat.Raster,
                TileSize = 256,
                MinZoom = 0,
                MaxZoom = 18,
                TileUrlTemplate = "https://example.com/tiles/{z}/{x}/{y}.png",
                Attribution = "Mock Provider"
            }
        });
    }

    public Task<TileResponse> GetTileAsync(
        TileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_nextException != null)
        {
            var ex = _nextException;
            _nextException = null;
            throw ex;
        }

        if (_nextTileResponse != null)
        {
            return Task.FromResult(_nextTileResponse);
        }

        // Default - return a simple 1x1 PNG
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        return Task.FromResult(new TileResponse
        {
            Data = pngBytes,
            ContentType = "image/png",
            CacheControl = "public, max-age=3600",
            ETag = $"\"{request.TilesetId}-{request.Z}-{request.X}-{request.Y}\""
        });
    }

    public Task<string> GetTileUrlTemplateAsync(
        string tilesetId,
        CancellationToken cancellationToken = default)
    {
        if (_nextException != null)
        {
            var ex = _nextException;
            _nextException = null;
            throw ex;
        }

        return Task.FromResult(_tileUrlTemplate ?? $"https://example.com/tiles/{tilesetId}/{{z}}/{{x}}/{{y}}.png");
    }

    public Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default)
    {
        if (_nextException != null)
        {
            var ex = _nextException;
            _nextException = null;
            throw ex;
        }

        return Task.FromResult(_isAvailable);
    }
}
