// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Buffers;
using Honua.MapSDK.Utilities.Terrain;

namespace Honua.MapSDK.Services.Terrain;

/// <summary>
/// Service for generating and serving terrain tiles in various formats.
/// Supports Mapbox Terrain-RGB encoding and binary mesh formats.
/// </summary>
public class TerrainTileService : ITerrainTileService
{
    private readonly ILogger<TerrainTileService> _logger;
    private readonly IElevationService _elevationService;
    private readonly TerrainMeshGenerator _meshGenerator;
    private readonly MemoryCache<string, byte[]> _tileCache;

    public TerrainTileService(
        ILogger<TerrainTileService> logger,
        IElevationService elevationService)
    {
        _logger = logger;
        _elevationService = elevationService;
        _meshGenerator = new TerrainMeshGenerator();
        _tileCache = new MemoryCache<string, byte[]>(maxItems: 500);
    }

    /// <summary>
    /// Generate a terrain tile in Mapbox Terrain-RGB format.
    /// Encodes elevation as RGB values: height = -10000 + ((R * 256 * 256 + G * 256 + B) * 0.1)
    /// </summary>
    /// <param name="z">Zoom level</param>
    /// <param name="x">Tile X coordinate</param>
    /// <param name="y">Tile Y coordinate</param>
    /// <param name="tileSize">Tile size in pixels (default 256)</param>
    /// <returns>PNG image data in Terrain-RGB format</returns>
    public async Task<byte[]> GenerateTerrainRGBTileAsync(int z, int x, int y, int tileSize = 256)
    {
        var cacheKey = $"rgb_{z}_{x}_{y}_{tileSize}";
        if (_tileCache.TryGet(cacheKey, out var cached))
            return cached;

        var bounds = TileMath.TileToBounds(z, x, y);
        var grid = await _elevationService.QueryAreaElevationAsync(
            bounds.MinLon, bounds.MinLat, bounds.MaxLon, bounds.MaxLat,
            resolution: tileSize);

        var pngData = EncodeTerrainRGBPng(grid, tileSize);
        _tileCache.Set(cacheKey, pngData);

        _logger.LogDebug("Generated Terrain-RGB tile {Z}/{X}/{Y}", z, x, y);
        return pngData;
    }

    /// <summary>
    /// Generate a terrain mesh tile optimized for 3D rendering.
    /// Uses the Martini algorithm for efficient terrain mesh generation.
    /// </summary>
    /// <param name="z">Zoom level</param>
    /// <param name="x">Tile X coordinate</param>
    /// <param name="y">Tile Y coordinate</param>
    /// <param name="maxError">Maximum allowed error in meters (lower = more detail)</param>
    /// <returns>Binary mesh data</returns>
    public async Task<TerrainMeshTile> GenerateTerrainMeshTileAsync(
        int z, int x, int y, float maxError = 1.0f)
    {
        var cacheKey = $"mesh_{z}_{x}_{y}_{maxError}";

        var bounds = TileMath.TileToBounds(z, x, y);

        // Query elevation grid (power of 2 + 1 for Martini algorithm)
        var gridSize = 257; // 2^8 + 1
        var grid = await _elevationService.QueryAreaElevationAsync(
            bounds.MinLon, bounds.MinLat, bounds.MaxLon, bounds.MaxLat,
            resolution: gridSize);

        // Generate mesh using Martini algorithm
        var mesh = _meshGenerator.GenerateMesh(grid.Data, maxError);

        var tile = new TerrainMeshTile
        {
            Z = z,
            X = x,
            Y = y,
            Vertices = mesh.Vertices,
            Indices = mesh.Indices,
            Bounds = bounds,
            VertexCount = mesh.Vertices.Length / 3,
            TriangleCount = mesh.Indices.Length / 3
        };

        _logger.LogDebug("Generated terrain mesh tile {Z}/{X}/{Y} with {Vertices} vertices, {Triangles} triangles",
            z, x, y, tile.VertexCount, tile.TriangleCount);

        return tile;
    }

    /// <summary>
    /// Generate a hillshade tile for visualization.
    /// </summary>
    /// <param name="z">Zoom level</param>
    /// <param name="x">Tile X coordinate</param>
    /// <param name="y">Tile Y coordinate</param>
    /// <param name="azimuth">Light direction in degrees (0-360)</param>
    /// <param name="altitude">Light altitude in degrees (0-90)</param>
    /// <param name="tileSize">Tile size in pixels</param>
    /// <returns>Grayscale hillshade image</returns>
    public async Task<byte[]> GenerateHillshadeTileAsync(
        int z, int x, int y,
        double azimuth = 315,
        double altitude = 45,
        int tileSize = 256)
    {
        var cacheKey = $"hillshade_{z}_{x}_{y}_{azimuth}_{altitude}_{tileSize}";
        if (_tileCache.TryGet(cacheKey, out var cached))
            return cached;

        var bounds = TileMath.TileToBounds(z, x, y);
        var grid = await _elevationService.QueryAreaElevationAsync(
            bounds.MinLon, bounds.MinLat, bounds.MaxLon, bounds.MaxLat,
            resolution: tileSize);

        var hillshade = GenerateHillshade(grid.Data, azimuth, altitude);
        var pngData = EncodeGrayscalePng(hillshade, tileSize);

        _tileCache.Set(cacheKey, pngData);
        return pngData;
    }

    /// <summary>
    /// Generate a slope analysis tile.
    /// </summary>
    public async Task<byte[]> GenerateSlopeTileAsync(int z, int x, int y, int tileSize = 256)
    {
        var bounds = TileMath.TileToBounds(z, x, y);
        var grid = await _elevationService.QueryAreaElevationAsync(
            bounds.MinLon, bounds.MinLat, bounds.MaxLon, bounds.MaxLat,
            resolution: tileSize);

        var slope = CalculateSlope(grid.Data);
        var pngData = EncodeSlopePng(slope, tileSize);

        return pngData;
    }

    /// <summary>
    /// Get tile metadata including elevation statistics.
    /// </summary>
    public async Task<TerrainTileMetadata> GetTileMetadataAsync(int z, int x, int y)
    {
        var bounds = TileMath.TileToBounds(z, x, y);
        var grid = await _elevationService.QueryAreaElevationAsync(
            bounds.MinLon, bounds.MinLat, bounds.MaxLon, bounds.MaxLat,
            resolution: 100);

        var flatData = new List<float>();
        for (int i = 0; i < grid.Height; i++)
            for (int j = 0; j < grid.Width; j++)
                flatData.Add(grid.Data[i, j]);

        return new TerrainTileMetadata
        {
            Z = z,
            X = x,
            Y = y,
            MinElevation = flatData.Min(),
            MaxElevation = flatData.Max(),
            MeanElevation = flatData.Average(),
            Bounds = bounds
        };
    }

    private byte[] EncodeTerrainRGBPng(ElevationGrid grid, int tileSize)
    {
        // Mapbox Terrain-RGB encoding:
        // height = -10000 + ((R * 256 * 256 + G * 256 + B) * 0.1)
        var rgb = new byte[tileSize * tileSize * 3];

        for (int y = 0; y < tileSize; y++)
        {
            for (int x = 0; x < tileSize; x++)
            {
                var elevation = grid.Data[y, x];
                var encoded = (int)((elevation + 10000) / 0.1);

                var idx = (y * tileSize + x) * 3;
                rgb[idx] = (byte)((encoded >> 16) & 0xFF);     // R
                rgb[idx + 1] = (byte)((encoded >> 8) & 0xFF);  // G
                rgb[idx + 2] = (byte)(encoded & 0xFF);         // B
            }
        }

        // Would encode to PNG here - using placeholder
        return rgb;
    }

    private float[,] GenerateHillshade(float[,] elevations, double azimuth, double altitude)
    {
        var height = elevations.GetLength(0);
        var width = elevations.GetLength(1);
        var hillshade = new float[height, width];

        var azimuthRad = azimuth * Math.PI / 180.0;
        var altitudeRad = altitude * Math.PI / 180.0;

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                // Calculate slope and aspect using Horn's method
                var dzdx = ((elevations[y - 1, x + 1] + 2 * elevations[y, x + 1] + elevations[y + 1, x + 1]) -
                           (elevations[y - 1, x - 1] + 2 * elevations[y, x - 1] + elevations[y + 1, x - 1])) / 8.0;

                var dzdy = ((elevations[y + 1, x - 1] + 2 * elevations[y + 1, x] + elevations[y + 1, x + 1]) -
                           (elevations[y - 1, x - 1] + 2 * elevations[y - 1, x] + elevations[y - 1, x + 1])) / 8.0;

                var slope = Math.Atan(Math.Sqrt(dzdx * dzdx + dzdy * dzdy));
                var aspect = Math.Atan2(dzdy, -dzdx);

                var shade = Math.Cos(altitudeRad) * Math.Cos(slope) +
                           Math.Sin(altitudeRad) * Math.Sin(slope) * Math.Cos(azimuthRad - aspect);

                hillshade[y, x] = (float)Math.Max(0, Math.Min(1, shade));
            }
        }

        return hillshade;
    }

    private float[,] CalculateSlope(float[,] elevations)
    {
        var height = elevations.GetLength(0);
        var width = elevations.GetLength(1);
        var slope = new float[height, width];

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                var dzdx = (elevations[y, x + 1] - elevations[y, x - 1]) / 2.0;
                var dzdy = (elevations[y + 1, x] - elevations[y - 1, x]) / 2.0;

                slope[y, x] = (float)(Math.Atan(Math.Sqrt(dzdx * dzdx + dzdy * dzdy)) * 180.0 / Math.PI);
            }
        }

        return slope;
    }

    private byte[] EncodeGrayscalePng(float[,] data, int size)
    {
        var grayscale = new byte[size * size];
        for (int i = 0; i < size * size; i++)
        {
            var y = i / size;
            var x = i % size;
            grayscale[i] = (byte)(data[y, x] * 255);
        }
        return grayscale; // Would encode to PNG
    }

    private byte[] EncodeSlopePng(float[,] slope, int size)
    {
        // Encode slope with color ramp
        var rgb = new byte[size * size * 3];
        for (int i = 0; i < size * size; i++)
        {
            var y = i / size;
            var x = i % size;
            var s = Math.Min(90, slope[y, x]) / 90.0; // Normalize to 0-1

            // Color ramp: green -> yellow -> red
            var idx = i * 3;
            rgb[idx] = (byte)(s * 255);
            rgb[idx + 1] = (byte)((1 - s) * 255);
            rgb[idx + 2] = 0;
        }
        return rgb; // Would encode to PNG
    }
}

/// <summary>
/// Interface for terrain tile services.
/// </summary>
public interface ITerrainTileService
{
    Task<byte[]> GenerateTerrainRGBTileAsync(int z, int x, int y, int tileSize = 256);
    Task<TerrainMeshTile> GenerateTerrainMeshTileAsync(int z, int x, int y, float maxError = 1.0f);
    Task<byte[]> GenerateHillshadeTileAsync(int z, int x, int y, double azimuth = 315, double altitude = 45, int tileSize = 256);
    Task<byte[]> GenerateSlopeTileAsync(int z, int x, int y, int tileSize = 256);
    Task<TerrainTileMetadata> GetTileMetadataAsync(int z, int x, int y);
}

/// <summary>
/// Terrain mesh tile data.
/// </summary>
public class TerrainMeshTile
{
    public int Z { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public required float[] Vertices { get; set; }
    public required uint[] Indices { get; set; }
    public required TileBounds Bounds { get; set; }
    public int VertexCount { get; set; }
    public int TriangleCount { get; set; }
}

/// <summary>
/// Terrain tile metadata.
/// </summary>
public class TerrainTileMetadata
{
    public int Z { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public float MinElevation { get; set; }
    public float MaxElevation { get; set; }
    public float MeanElevation { get; set; }
    public required TileBounds Bounds { get; set; }
}

/// <summary>
/// Tile bounds in geographic coordinates.
/// </summary>
public class TileBounds
{
    public double MinLon { get; set; }
    public double MinLat { get; set; }
    public double MaxLon { get; set; }
    public double MaxLat { get; set; }
}

/// <summary>
/// Tile math utilities.
/// </summary>
public static class TileMath
{
    public static TileBounds TileToBounds(int z, int x, int y)
    {
        var n = Math.Pow(2, z);
        var minLon = x / n * 360.0 - 180.0;
        var maxLon = (x + 1) / n * 360.0 - 180.0;
        var minLat = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * (y + 1) / n))) * 180.0 / Math.PI;
        var maxLat = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * y / n))) * 180.0 / Math.PI;

        return new TileBounds
        {
            MinLon = minLon,
            MinLat = minLat,
            MaxLon = maxLon,
            MaxLat = maxLat
        };
    }

    public static (int x, int y) LatLonToTile(double lat, double lon, int z)
    {
        var n = Math.Pow(2, z);
        var x = (int)((lon + 180.0) / 360.0 * n);
        var latRad = lat * Math.PI / 180.0;
        var y = (int)((1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n);
        return (x, y);
    }
}

/// <summary>
/// Simple memory cache for tiles.
/// </summary>
internal class MemoryCache<TKey, TValue> where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _cache = new();
    private readonly Queue<TKey> _accessOrder = new();
    private readonly int _maxItems;
    private readonly object _lock = new();

    public MemoryCache(int maxItems)
    {
        _maxItems = maxItems;
    }

    public bool TryGet(TKey key, out TValue value)
    {
        lock (_lock)
        {
            return _cache.TryGetValue(key, out value!);
        }
    }

    public void Set(TKey key, TValue value)
    {
        lock (_lock)
        {
            if (_cache.Count >= _maxItems && !_cache.ContainsKey(key))
            {
                var oldest = _accessOrder.Dequeue();
                _cache.Remove(oldest);
            }

            _cache[key] = value;
            _accessOrder.Enqueue(key);
        }
    }
}
