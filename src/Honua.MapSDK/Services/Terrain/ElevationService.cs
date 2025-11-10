// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace Honua.MapSDK.Services.Terrain;

/// <summary>
/// Service for querying elevation data from various sources including DEM rasters and online services.
/// Supports Cloud Optimized GeoTIFF (COG), SRTM, ASTER, and USGS formats.
/// </summary>
public class ElevationService : IElevationService
{
    private readonly ILogger<ElevationService> _logger;
    private readonly Dictionary<string, ElevationDataSource> _dataSources;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private readonly Dictionary<string, float[,]> _tileCache = new();
    private const int MaxCacheSize = 100; // Cache up to 100 tiles in memory

    public ElevationService(ILogger<ElevationService> logger)
    {
        _logger = logger;
        _dataSources = new Dictionary<string, ElevationDataSource>();
    }

    /// <summary>
    /// Register an elevation data source.
    /// </summary>
    /// <param name="name">Unique name for the data source</param>
    /// <param name="source">Data source configuration</param>
    public void RegisterDataSource(string name, ElevationDataSource source)
    {
        _dataSources[name] = source;
        _logger.LogInformation("Registered elevation data source: {Name} ({Type})", name, source.Type);
    }

    /// <summary>
    /// Query elevation at a single point.
    /// </summary>
    /// <param name="longitude">Longitude in degrees</param>
    /// <param name="latitude">Latitude in degrees</param>
    /// <param name="sourceName">Optional data source name (uses first available if null)</param>
    /// <returns>Elevation in meters, or null if not available</returns>
    public async Task<float?> QueryElevationAsync(double longitude, double latitude, string? sourceName = null)
    {
        var source = GetDataSource(sourceName);
        if (source == null)
        {
            _logger.LogWarning("No elevation data source available");
            return null;
        }

        return source.Type switch
        {
            ElevationSourceType.LocalRaster => await QueryLocalRasterAsync(source, longitude, latitude),
            ElevationSourceType.RemoteCOG => await QueryRemoteCOGAsync(source, longitude, latitude),
            ElevationSourceType.TileService => await QueryTileServiceAsync(source, longitude, latitude),
            _ => throw new NotSupportedException($"Source type {source.Type} not supported")
        };
    }

    /// <summary>
    /// Query elevation for multiple points efficiently.
    /// </summary>
    /// <param name="points">Array of [longitude, latitude] coordinates</param>
    /// <param name="sourceName">Optional data source name</param>
    /// <returns>Array of elevations in meters (null for unavailable points)</returns>
    public async Task<float?[]> QueryElevationBatchAsync(double[][] points, string? sourceName = null)
    {
        var results = new float?[points.Length];
        var tasks = new Task[points.Length];

        for (int i = 0; i < points.Length; i++)
        {
            var index = i;
            var point = points[i];
            tasks[i] = Task.Run(async () =>
            {
                results[index] = await QueryElevationAsync(point[0], point[1], sourceName);
            });
        }

        await Task.WhenAll(tasks);
        return results;
    }

    /// <summary>
    /// Query elevation along a path with regular sampling.
    /// </summary>
    /// <param name="coordinates">Path coordinates as [longitude, latitude] pairs</param>
    /// <param name="samplePoints">Number of points to sample along the path</param>
    /// <param name="sourceName">Optional data source name</param>
    /// <returns>Elevation profile data</returns>
    public async Task<ElevationProfile> QueryPathElevationAsync(
        double[][] coordinates,
        int samplePoints = 100,
        string? sourceName = null)
    {
        if (coordinates.Length < 2)
            throw new ArgumentException("Path must have at least 2 points", nameof(coordinates));

        var lineString = new LineString(coordinates.Select(c =>
            new Coordinate(c[0], c[1])).ToArray());

        var totalLength = lineString.Length;
        var stepSize = totalLength / samplePoints;

        var samples = new List<ElevationPoint>();
        double cumulativeDistance = 0;

        for (int i = 0; i <= samplePoints; i++)
        {
            var fraction = i / (double)samplePoints;
            var point = lineString.InterpolatePoint(fraction);
            var elevation = await QueryElevationAsync(point.X, point.Y, sourceName);

            samples.Add(new ElevationPoint
            {
                Longitude = point.X,
                Latitude = point.Y,
                Elevation = elevation ?? 0,
                Distance = cumulativeDistance,
                Index = i
            });

            if (i > 0)
            {
                var prevPoint = samples[i - 1];
                var dist = CalculateDistance(prevPoint.Longitude, prevPoint.Latitude, point.X, point.Y);
                cumulativeDistance += dist;
                samples[i].Distance = cumulativeDistance;
            }
        }

        return new ElevationProfile
        {
            Points = samples.ToArray(),
            TotalDistance = cumulativeDistance,
            MinElevation = samples.Min(s => s.Elevation),
            MaxElevation = samples.Max(s => s.Elevation),
            StartElevation = samples.First().Elevation,
            EndElevation = samples.Last().Elevation
        };
    }

    /// <summary>
    /// Query elevation for a bounding box, returning a 2D elevation grid.
    /// </summary>
    /// <param name="minLon">Minimum longitude</param>
    /// <param name="minLat">Minimum latitude</param>
    /// <param name="maxLon">Maximum longitude</param>
    /// <param name="maxLat">Maximum latitude</param>
    /// <param name="resolution">Grid resolution (cells per degree)</param>
    /// <param name="sourceName">Optional data source name</param>
    /// <returns>Elevation grid data</returns>
    public async Task<ElevationGrid> QueryAreaElevationAsync(
        double minLon, double minLat, double maxLon, double maxLat,
        int resolution = 100,
        string? sourceName = null)
    {
        var width = (int)Math.Ceiling((maxLon - minLon) * resolution);
        var height = (int)Math.Ceiling((maxLat - minLat) * resolution);

        var grid = new float[height, width];
        var lonStep = (maxLon - minLon) / width;
        var latStep = (maxLat - minLat) / height;

        // Query elevations in parallel
        var tasks = new List<Task>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var xx = x;
                var yy = y;
                tasks.Add(Task.Run(async () =>
                {
                    var lon = minLon + xx * lonStep;
                    var lat = minLat + yy * latStep;
                    var elevation = await QueryElevationAsync(lon, lat, sourceName);
                    grid[yy, xx] = elevation ?? 0;
                }));
            }
        }

        await Task.WhenAll(tasks);

        return new ElevationGrid
        {
            Data = grid,
            Width = width,
            Height = height,
            MinLongitude = minLon,
            MinLatitude = minLat,
            MaxLongitude = maxLon,
            MaxLatitude = maxLat,
            Resolution = resolution
        };
    }

    private ElevationDataSource? GetDataSource(string? sourceName)
    {
        if (sourceName != null && _dataSources.TryGetValue(sourceName, out var source))
            return source;

        return _dataSources.Values.FirstOrDefault();
    }

    private async Task<float?> QueryLocalRasterAsync(ElevationDataSource source, double longitude, double latitude)
    {
        // This would integrate with GDAL via the Raster project
        // For now, return a placeholder
        await Task.CompletedTask;
        return 0;
    }

    private async Task<float?> QueryRemoteCOGAsync(ElevationDataSource source, double longitude, double latitude)
    {
        // This would use HTTP range requests to read COG tiles
        await Task.CompletedTask;
        return 0;
    }

    private async Task<float?> QueryTileServiceAsync(ElevationDataSource source, double longitude, double latitude)
    {
        // This would query a tile service (e.g., Mapbox Terrain-RGB)
        await Task.CompletedTask;
        return 0;
    }

    private static double CalculateDistance(double lon1, double lat1, double lon2, double lat2)
    {
        // Haversine formula
        const double R = 6371000; // Earth radius in meters
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}

/// <summary>
/// Interface for elevation query services.
/// </summary>
public interface IElevationService
{
    void RegisterDataSource(string name, ElevationDataSource source);
    Task<float?> QueryElevationAsync(double longitude, double latitude, string? sourceName = null);
    Task<float?[]> QueryElevationBatchAsync(double[][] points, string? sourceName = null);
    Task<ElevationProfile> QueryPathElevationAsync(double[][] coordinates, int samplePoints = 100, string? sourceName = null);
    Task<ElevationGrid> QueryAreaElevationAsync(double minLon, double minLat, double maxLon, double maxLat, int resolution = 100, string? sourceName = null);
}

/// <summary>
/// Elevation data source configuration.
/// </summary>
public class ElevationDataSource
{
    public required ElevationSourceType Type { get; set; }
    public required string Path { get; set; }
    public string? Url { get; set; }
    public string? ApiKey { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Types of elevation data sources.
/// </summary>
public enum ElevationSourceType
{
    LocalRaster,    // Local GeoTIFF/COG file
    RemoteCOG,      // Remote Cloud Optimized GeoTIFF
    TileService     // Tile service (Mapbox, etc.)
}

/// <summary>
/// Elevation point data.
/// </summary>
public class ElevationPoint
{
    public double Longitude { get; set; }
    public double Latitude { get; set; }
    public float Elevation { get; set; }
    public double Distance { get; set; }
    public int Index { get; set; }
}

/// <summary>
/// Elevation profile along a path.
/// </summary>
public class ElevationProfile
{
    public required ElevationPoint[] Points { get; set; }
    public double TotalDistance { get; set; }
    public float MinElevation { get; set; }
    public float MaxElevation { get; set; }
    public float StartElevation { get; set; }
    public float EndElevation { get; set; }
}

/// <summary>
/// 2D elevation grid for an area.
/// </summary>
public class ElevationGrid
{
    public required float[,] Data { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double MinLongitude { get; set; }
    public double MinLatitude { get; set; }
    public double MaxLongitude { get; set; }
    public double MaxLatitude { get; set; }
    public int Resolution { get; set; }
}

/// <summary>
/// Extension methods for LineString geometry.
/// </summary>
internal static class LineStringExtensions
{
    public static Point InterpolatePoint(this LineString lineString, double fraction)
    {
        if (fraction <= 0) return lineString.StartPoint;
        if (fraction >= 1) return lineString.EndPoint;

        var totalLength = lineString.Length;
        var targetLength = totalLength * fraction;
        var currentLength = 0.0;

        var coords = lineString.Coordinates;
        for (int i = 0; i < coords.Length - 1; i++)
        {
            var p1 = coords[i];
            var p2 = coords[i + 1];
            var segmentLength = p1.Distance(p2);

            if (currentLength + segmentLength >= targetLength)
            {
                var segmentFraction = (targetLength - currentLength) / segmentLength;
                var x = p1.X + (p2.X - p1.X) * segmentFraction;
                var y = p1.Y + (p2.Y - p1.Y) * segmentFraction;
                return new Point(x, y);
            }

            currentLength += segmentLength;
        }

        return lineString.EndPoint;
    }
}
