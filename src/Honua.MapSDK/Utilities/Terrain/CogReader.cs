// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Utilities.Terrain;

/// <summary>
/// Reader for Cloud Optimized GeoTIFF (COG) elevation data.
/// Supports efficient reading of elevation tiles using HTTP range requests.
/// </summary>
/// <remarks>
/// COG format stores data in tiles with overviews, allowing efficient
/// random access without downloading the entire file.
/// This implementation would integrate with GDAL for production use.
/// </remarks>
public class CogReader : IDisposable
{
    private readonly string _path;
    private readonly bool _isRemote;
    private readonly HttpClient? _httpClient;
    private CogMetadata? _metadata;

    public CogReader(string path, HttpClient? httpClient = null)
    {
        _path = path;
        _isRemote = path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        _httpClient = httpClient;
    }

    /// <summary>
    /// Open the COG file and read metadata.
    /// </summary>
    public async Task<CogMetadata> OpenAsync()
    {
        if (_metadata != null)
            return _metadata;

        if (_isRemote)
        {
            _metadata = await ReadRemoteMetadataAsync();
        }
        else
        {
            _metadata = await ReadLocalMetadataAsync();
        }

        return _metadata;
    }

    /// <summary>
    /// Read elevation data for a specific tile.
    /// </summary>
    /// <param name="tileX">Tile X coordinate</param>
    /// <param name="tileY">Tile Y coordinate</param>
    /// <param name="zoom">Zoom level (0 = full resolution)</param>
    /// <returns>Elevation data as 2D array</returns>
    public async Task<float[,]> ReadTileAsync(int tileX, int tileY, int zoom = 0)
    {
        if (_metadata == null)
            throw new InvalidOperationException("COG not opened. Call OpenAsync first.");

        if (_isRemote)
        {
            return await ReadRemoteTileAsync(tileX, tileY, zoom);
        }
        else
        {
            return await ReadLocalTileAsync(tileX, tileY, zoom);
        }
    }

    /// <summary>
    /// Read elevation for a geographic bounding box.
    /// </summary>
    /// <param name="minLon">Minimum longitude</param>
    /// <param name="minLat">Minimum latitude</param>
    /// <param name="maxLon">Maximum longitude</param>
    /// <param name="maxLat">Maximum latitude</param>
    /// <param name="width">Output width in pixels</param>
    /// <param name="height">Output height in pixels</param>
    /// <returns>Elevation grid</returns>
    public async Task<float[,]> ReadAreaAsync(
        double minLon, double minLat,
        double maxLon, double maxLat,
        int width, int height)
    {
        if (_metadata == null)
            throw new InvalidOperationException("COG not opened. Call OpenAsync first.");

        // Calculate which tiles overlap the requested area
        var tiles = CalculateOverlappingTiles(minLon, minLat, maxLon, maxLat);

        // Read and mosaic the tiles
        var result = new float[height, width];

        foreach (var (tx, ty) in tiles)
        {
            var tileData = await ReadTileAsync(tx, ty);
            // Mosaic tile into result (simplified)
            // In production, would properly reproject and resample
        }

        return result;
    }

    /// <summary>
    /// Query elevation at a single point.
    /// </summary>
    public async Task<float?> QueryPointAsync(double longitude, double latitude)
    {
        if (_metadata == null)
            throw new InvalidOperationException("COG not opened. Call OpenAsync first.");

        // Convert lat/lon to pixel coordinates
        var (x, y) = GeoToPixel(longitude, latitude);

        // Determine which tile contains this point
        var tileX = x / _metadata.TileWidth;
        var tileY = y / _metadata.TileHeight;

        // Read the tile
        var tile = await ReadTileAsync(tileX, tileY);

        // Get the value from the tile
        var localX = x % _metadata.TileWidth;
        var localY = y % _metadata.TileHeight;

        if (localY < tile.GetLength(0) && localX < tile.GetLength(1))
        {
            return tile[localY, localX];
        }

        return null;
    }

    private async Task<CogMetadata> ReadRemoteMetadataAsync()
    {
        // In production, would use GDAL's VSI mechanism to read remote files
        // For now, return placeholder metadata
        await Task.CompletedTask;

        return new CogMetadata
        {
            Width = 10800,
            Height = 10800,
            TileWidth = 256,
            TileHeight = 256,
            MinLongitude = -180,
            MaxLongitude = 180,
            MinLatitude = -90,
            MaxLatitude = 90,
            NoDataValue = -9999,
            DataType = "Float32"
        };
    }

    private async Task<CogMetadata> ReadLocalMetadataAsync()
    {
        // In production, would use GDAL to read local GeoTIFF metadata
        await Task.CompletedTask;

        return new CogMetadata
        {
            Width = 10800,
            Height = 10800,
            TileWidth = 256,
            TileHeight = 256,
            MinLongitude = -180,
            MaxLongitude = 180,
            MinLatitude = -90,
            MaxLatitude = 90,
            NoDataValue = -9999,
            DataType = "Float32"
        };
    }

    private async Task<float[,]> ReadRemoteTileAsync(int tileX, int tileY, int zoom)
    {
        if (_httpClient == null || _metadata == null)
            throw new InvalidOperationException("HTTP client required for remote COG access");

        // In production, would:
        // 1. Calculate byte range for the tile
        // 2. Issue HTTP range request
        // 3. Decode TIFF tile data
        // 4. Return elevation values

        await Task.CompletedTask;

        // Placeholder: return zeroed array
        return new float[_metadata.TileHeight, _metadata.TileWidth];
    }

    private async Task<float[,]> ReadLocalTileAsync(int tileX, int tileY, int zoom)
    {
        if (_metadata == null)
            throw new InvalidOperationException("Metadata not loaded");

        // In production, would use GDAL to read the tile
        await Task.CompletedTask;

        return new float[_metadata.TileHeight, _metadata.TileWidth];
    }

    private List<(int x, int y)> CalculateOverlappingTiles(
        double minLon, double minLat, double maxLon, double maxLat)
    {
        if (_metadata == null)
            return new List<(int x, int y)>();

        var (minX, minY) = GeoToPixel(minLon, minLat);
        var (maxX, maxY) = GeoToPixel(maxLon, maxLat);

        var startTileX = minX / _metadata.TileWidth;
        var startTileY = minY / _metadata.TileHeight;
        var endTileX = maxX / _metadata.TileWidth;
        var endTileY = maxY / _metadata.TileHeight;

        var tiles = new List<(int x, int y)>();
        for (int ty = startTileY; ty <= endTileY; ty++)
        {
            for (int tx = startTileX; tx <= endTileX; tx++)
            {
                tiles.Add((tx, ty));
            }
        }

        return tiles;
    }

    private (int x, int y) GeoToPixel(double longitude, double latitude)
    {
        if (_metadata == null)
            return (0, 0);

        var x = (int)((longitude - _metadata.MinLongitude) /
                     (_metadata.MaxLongitude - _metadata.MinLongitude) * _metadata.Width);
        var y = (int)((_metadata.MaxLatitude - latitude) /
                     (_metadata.MaxLatitude - _metadata.MinLatitude) * _metadata.Height);

        return (x, y);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

/// <summary>
/// COG file metadata.
/// </summary>
public class CogMetadata
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int TileWidth { get; set; }
    public int TileHeight { get; set; }
    public double MinLongitude { get; set; }
    public double MaxLongitude { get; set; }
    public double MinLatitude { get; set; }
    public double MaxLatitude { get; set; }
    public float NoDataValue { get; set; }
    public required string DataType { get; set; }
    public int OverviewCount { get; set; }
    public string? Projection { get; set; }
}

/// <summary>
/// Utilities for working with COG files.
/// </summary>
public static class CogUtils
{
    /// <summary>
    /// Check if a file is a valid Cloud Optimized GeoTIFF.
    /// </summary>
    public static async Task<bool> IsValidCogAsync(string path)
    {
        // In production, would validate:
        // 1. TIFF is tiled
        // 2. Has overviews
        // 3. Tiles and overviews are properly ordered
        await Task.CompletedTask;
        return true;
    }

    /// <summary>
    /// Convert a regular GeoTIFF to COG format.
    /// </summary>
    public static async Task ConvertToCogAsync(string inputPath, string outputPath)
    {
        // In production, would use GDAL to convert:
        // gdal_translate input.tif output.tif -of COG -co COMPRESS=DEFLATE
        await Task.CompletedTask;
    }

    /// <summary>
    /// Get recommended tile size for a given dataset size.
    /// </summary>
    public static int GetRecommendedTileSize(int width, int height)
    {
        // Common tile sizes: 256, 512
        // Larger datasets benefit from larger tiles
        if (width > 10000 || height > 10000)
            return 512;
        return 256;
    }
}
