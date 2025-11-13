// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BitMiracle.LibTiff.Classic;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster.Rendering;
using Microsoft.Extensions.Logging;
using SkiaSharp;

using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Raster.Analytics;

/// <summary>
/// Service for performing analytical operations on raster data including statistics, algebra, and terrain analysis.
/// </summary>
public sealed class RasterAnalyticsService : IRasterAnalyticsService
{
    private readonly ILogger<RasterAnalyticsService> logger;
    private readonly RasterMemoryLimits memoryLimits;

    /// <summary>
    /// Initializes a new instance of the <see cref="RasterAnalyticsService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="memoryLimits">Optional memory limits for raster operations.</param>
    public RasterAnalyticsService(
        ILogger<RasterAnalyticsService> logger,
        RasterMemoryLimits? memoryLimits = null)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.memoryLimits = memoryLimits ?? new RasterMemoryLimits();
        this.memoryLimits.Validate();
    }

    /// <inheritdoc/>
    public Task<RasterStatistics> CalculateStatisticsAsync(RasterStatisticsRequest request, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);

        using var bitmap = LoadDatasetBitmap(request.Dataset);
        if (bitmap == null)
        {
            throw new InvalidOperationException($"Could not load dataset {request.Dataset.Id}");
        }

        var bandStats = new List<BandStatistics>();

        // For RGB/RGBA images, treat each channel as a band
        var bandCount = bitmap.ColorType == SKColorType.Rgba8888 ? 4 : 3;
        var targetBand = request.BandIndex ?? -1;

        for (int band = 0; band < bandCount; band++)
        {
            if (targetBand >= 0 && band != targetBand)
            {
                continue;
            }

            var values = this.ExtractBandValues(bitmap, band);
            var stats = this.CalculateBandStatistics(band, values);
            bandStats.Add(stats);
        }

        var result = new RasterStatistics(
            request.Dataset.Id,
            bandCount,
            bandStats,
            request.BoundingBox);

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<RasterAlgebraResult> CalculateAlgebraAsync(RasterAlgebraRequest request, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);

        // Validate dataset count
        if (request.Datasets.Count > this.memoryLimits.MaxSimultaneousDatasets)
        {
            throw new ArgumentException(
                $"Number of datasets ({request.Datasets.Count}) exceeds maximum allowed " +
                $"({this.memoryLimits.MaxSimultaneousDatasets}). " +
                $"Consider processing datasets in batches or increasing RasterMemoryLimits.MaxSimultaneousDatasets.",
                nameof(request));
        }

        // Validate output dimensions
        if (request.Width > this.memoryLimits.MaxRasterDimension || request.Height > this.memoryLimits.MaxRasterDimension)
        {
            throw new ArgumentException(
                $"Requested dimensions ({request.Width}x{request.Height}) exceed maximum allowed " +
                $"({this.memoryLimits.MaxRasterDimension}x{this.memoryLimits.MaxRasterDimension}).",
                nameof(request));
        }

        long totalPixels = (long)request.Width * request.Height;
        if (totalPixels > this.memoryLimits.MaxRasterPixels)
        {
            throw new ArgumentException(
                $"Total pixel count ({totalPixels:N0}) exceeds maximum allowed ({this.memoryLimits.MaxRasterPixels:N0}). " +
                $"Requested: {request.Width}x{request.Height}",
                nameof(request));
        }

        this.logger.LogDebug(
            "Processing raster algebra: {DatasetCount} datasets, output: {Width}x{Height} ({TotalPixels:N0} pixels)",
            request.Datasets.Count, request.Width, request.Height, totalPixels);

        // Load all datasets
        var bitmaps = new List<SKBitmap>(request.Datasets.Count);
        try
        {
            foreach (var dataset in request.Datasets)
            {
                var bitmap = LoadDatasetBitmap(dataset);
                if (bitmap != null)
                {
                    bitmaps.Add(bitmap);
                }
            }

            if (bitmaps.Count == 0)
            {
                throw new InvalidOperationException("No datasets could be loaded");
            }

            // Apply algebra expression
            var resultBitmap = this.ApplyAlgebraExpression(bitmaps, request.Expression, request.Width, request.Height);

            var normalizedFormat = RasterFormatHelper.Normalize(request.Format);
            var contentType = RasterFormatHelper.GetContentType(normalizedFormat);

            using var image = SKImage.FromBitmap(resultBitmap);
            using var data = image.Encode(this.GetEncodedImageFormat(normalizedFormat), 90);
            var bytes = data.ToArray();

            // Calculate statistics on result
            var values = this.ExtractAllValues(resultBitmap);
            var stats = new RasterStatistics(
                "algebra_result",
                1,
                new[] { this.CalculateBandStatistics(0, values) },
                request.BoundingBox);

            var result = new RasterAlgebraResult(bytes, contentType, request.Width, request.Height, stats);
            resultBitmap.Dispose();

            return Task.FromResult(result);
        }
        finally
        {
            foreach (var bitmap in bitmaps)
            {
                bitmap?.Dispose();
            }
        }
    }

    /// <inheritdoc/>
    public Task<RasterValueExtractionResult> ExtractValuesAsync(RasterValueExtractionRequest request, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);

        if (request.Points.Count > this.memoryLimits.MaxExtractionPoints)
        {
            throw new ArgumentException(
                $"Number of points ({request.Points.Count}) exceeds maximum allowed " +
                $"({this.memoryLimits.MaxExtractionPoints}). " +
                $"Consider processing points in batches or increasing RasterMemoryLimits.MaxExtractionPoints.",
                nameof(request));
        }

        using var bitmap = LoadDatasetBitmap(request.Dataset);
        if (bitmap == null)
        {
            throw new InvalidOperationException($"Could not load dataset {request.Dataset.Id}");
        }

        var extent = this.GetDatasetExtent(request.Dataset);
        var values = new List<PointValue>();

        var targetBand = request.BandIndex ?? 0;

        foreach (var point in request.Points)
        {
            var (pixelX, pixelY) = this.WorldToPixel(point.X, point.Y, extent, bitmap.Width, bitmap.Height);

            if (pixelX >= 0 && pixelX < bitmap.Width && pixelY >= 0 && pixelY < bitmap.Height)
            {
                var color = bitmap.GetPixel(pixelX, pixelY);
                var value = ExtractBandValue(color, targetBand);

                values.Add(new PointValue(point.X, point.Y, value, targetBand));
            }
            else
            {
                values.Add(new PointValue(point.X, point.Y, null, targetBand));
            }
        }

        var result = new RasterValueExtractionResult(request.Dataset.Id, values);
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<RasterHistogram> CalculateHistogramAsync(RasterHistogramRequest request, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);

        if (request.BinCount > this.memoryLimits.MaxHistogramBins)
        {
            throw new ArgumentException(
                $"Bin count ({request.BinCount}) exceeds maximum allowed ({this.memoryLimits.MaxHistogramBins}). " +
                $"Consider using fewer bins or increasing RasterMemoryLimits.MaxHistogramBins.",
                nameof(request));
        }

        if (request.BinCount <= 0)
        {
            throw new ArgumentException("Bin count must be greater than zero", nameof(request));
        }

        using var bitmap = LoadDatasetBitmap(request.Dataset);
        if (bitmap == null)
        {
            throw new InvalidOperationException($"Could not load dataset {request.Dataset.Id}");
        }

        var targetBand = request.BandIndex ?? 0;
        var values = ExtractBandValues(bitmap, targetBand);

        if (values.Count == 0)
        {
            return Task.FromResult(new RasterHistogram(request.Dataset.Id, targetBand, Array.Empty<HistogramBin>(), 0, 0));
        }

        var min = values.Min();
        var max = values.Max();
        var binWidth = (max - min) / request.BinCount;

        var bins = new List<HistogramBin>();
        for (int i = 0; i < request.BinCount; i++)
        {
            var rangeStart = min + i * binWidth;
            var rangeEnd = i == request.BinCount - 1 ? max : rangeStart + binWidth;
            var count = values.Count(v => v >= rangeStart && v < rangeEnd);

            bins.Add(new HistogramBin(rangeStart, rangeEnd, count));
        }

        var result = new RasterHistogram(request.Dataset.Id, targetBand, bins, min, max);
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<ZonalStatisticsResult> CalculateZonalStatisticsAsync(ZonalStatisticsRequest request, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);

        if (request.Zones.Count > this.memoryLimits.MaxZonalPolygons)
        {
            throw new ArgumentException(
                $"Number of zones ({request.Zones.Count}) exceeds maximum allowed ({this.memoryLimits.MaxZonalPolygons}). " +
                $"Consider processing zones in batches or increasing RasterMemoryLimits.MaxZonalPolygons.",
                nameof(request));
        }

        // Validate polygon complexity
        foreach (var zone in request.Zones)
        {
            if (zone.Coordinates.Count > this.memoryLimits.MaxZonalPolygonVertices)
            {
                throw new ArgumentException(
                    $"Polygon '{zone.ZoneId ?? "unknown"}' has {zone.Coordinates.Count} vertices, " +
                    $"which exceeds maximum allowed ({this.memoryLimits.MaxZonalPolygonVertices}). " +
                    $"Consider simplifying the polygon or increasing RasterMemoryLimits.MaxZonalPolygonVertices.",
                    nameof(request));
            }

            if (zone.Coordinates.Count < 3)
            {
                throw new ArgumentException(
                    $"Polygon '{zone.ZoneId ?? "unknown"}' must have at least 3 vertices",
                    nameof(request));
            }
        }

        this.logger.LogDebug(
            "Processing zonal statistics: {ZoneCount} zones, max vertices: {MaxVertices}",
            request.Zones.Count,
            request.Zones.Max(z => z.Coordinates.Count));

        using var bitmap = LoadDatasetBitmap(request.Dataset);
        if (bitmap == null)
        {
            throw new InvalidOperationException($"Could not load dataset {request.Dataset.Id}");
        }

        var extent = this.GetDatasetExtent(request.Dataset);
        var targetBand = request.BandIndex ?? 0;
        var zoneStats = new List<ZoneStatistics>(request.Zones.Count);

        foreach (var zone in request.Zones)
        {
            var pixelValues = new List<double>();

            // Get bounding box of polygon to limit pixel iteration
            var minX = zone.Coordinates.Min(p => p.X);
            var maxX = zone.Coordinates.Max(p => p.X);
            var minY = zone.Coordinates.Min(p => p.Y);
            var maxY = zone.Coordinates.Max(p => p.Y);

            // Convert world coordinates to pixel coordinates
            var (minPixelX, maxPixelY) = this.WorldToPixel(minX, minY, extent, bitmap.Width, bitmap.Height);
            var (maxPixelX, minPixelY) = this.WorldToPixel(maxX, maxY, extent, bitmap.Width, bitmap.Height);

            // Clamp to image bounds
            minPixelX = Math.Max(0, minPixelX);
            minPixelY = Math.Max(0, minPixelY);
            maxPixelX = Math.Min(bitmap.Width - 1, maxPixelX);
            maxPixelY = Math.Min(bitmap.Height - 1, maxPixelY);

            // Iterate through pixels in bounding box
            for (int y = minPixelY; y <= maxPixelY; y++)
            {
                for (int x = minPixelX; x <= maxPixelX; x++)
                {
                    // Convert pixel to world coordinates for point-in-polygon test
                    var worldX = extent[0] + (x / (double)bitmap.Width) * (extent[2] - extent[0]);
                    var worldY = extent[3] - (y / (double)bitmap.Height) * (extent[3] - extent[1]);

                    if (this.IsPointInPolygon(worldX, worldY, zone.Coordinates))
                    {
                        var color = bitmap.GetPixel(x, y);
                        if (color.Alpha > 0)
                        {
                            var value = this.ExtractBandValue(color, targetBand);
                            if (value.HasValue)
                            {
                                pixelValues.Add(value.Value);
                            }
                        }
                    }
                }
            }

            // Always add zone statistics, even if empty
            if (pixelValues.Count > 0)
            {
                var mean = pixelValues.Average();
                var min = pixelValues.Min();
                var max = pixelValues.Max();
                var sum = pixelValues.Sum();
                var variance = pixelValues.Sum(v => Math.Pow(v - mean, 2)) / pixelValues.Count;
                var stdDev = Math.Sqrt(variance);

                double? median = null;
                if (request.Statistics?.Contains("median") == true)
                {
                    // Sort in-place to avoid additional allocations
                    pixelValues.Sort();
                    median = pixelValues.Count % 2 == 0
                        ? (pixelValues[pixelValues.Count / 2 - 1] + pixelValues[pixelValues.Count / 2]) / 2
                        : pixelValues[pixelValues.Count / 2];
                }

                zoneStats.Add(new ZoneStatistics(
                    zone.ZoneId,
                    targetBand,
                    mean,
                    min,
                    max,
                    sum,
                    stdDev,
                    pixelValues.Count,
                    median,
                    zone.Properties));
            }
            else
            {
                // Add empty zone statistics
                zoneStats.Add(new ZoneStatistics(
                    zone.ZoneId,
                    targetBand,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    null,
                    zone.Properties));
            }
        }

        var result = new ZonalStatisticsResult(request.Dataset.Id, zoneStats);
        return Task.FromResult(result);
    }

    private bool IsPointInPolygon(double x, double y, IReadOnlyList<Point> polygon)
    {
        // Ray casting algorithm for point-in-polygon test
        bool inside = false;
        int j = polygon.Count - 1;

        for (int i = 0; i < polygon.Count; i++)
        {
            if (((polygon[i].Y > y) != (polygon[j].Y > y)) &&
                (x < (polygon[j].X - polygon[i].X) * (y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X))
            {
                inside = !inside;
            }
            j = i;
        }

        return inside;
    }

    /// <inheritdoc/>
    public Task<TerrainAnalysisResult> CalculateTerrainAsync(TerrainAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        var bitmap = LoadDatasetBitmap(request.ElevationDataset);
        if (bitmap == null)
        {
            throw new InvalidOperationException($"Could not load elevation dataset {request.ElevationDataset.Id}");
        }

        try
        {
            var resultBitmap = request.AnalysisType switch
            {
                TerrainAnalysisType.Hillshade => this.CalculateHillshade(bitmap, request.ZFactor, request.Azimuth, request.Altitude),
                TerrainAnalysisType.Slope => this.CalculateSlope(bitmap, request.ZFactor),
                TerrainAnalysisType.Aspect => this.CalculateAspect(bitmap),
                TerrainAnalysisType.Curvature => this.CalculateCurvature(bitmap, request.ZFactor),
                TerrainAnalysisType.Roughness => this.CalculateRoughness(bitmap),
                _ => throw new ArgumentException($"Unsupported terrain analysis type: {request.AnalysisType}")
            };

            using var image = SKImage.FromBitmap(resultBitmap);
            var format = this.GetEncodedImageFormat(request.Format);
            using var data = image.Encode(format, 90);

            var stats = this.CalculateTerrainStatistics(resultBitmap, request.AnalysisType);

            var result = new TerrainAnalysisResult(
                data.ToArray(),
                request.Format == "png" ? "image/png" : "image/jpeg",
                resultBitmap.Width,
                resultBitmap.Height,
                request.AnalysisType,
                stats);

            return Task.FromResult(result);
        }
        finally
        {
            bitmap.Dispose();
        }
    }

    private SKBitmap CalculateHillshade(SKBitmap elevationBitmap, double zFactor, double azimuth, double altitude)
    {
        var width = elevationBitmap.Width;
        var height = elevationBitmap.Height;
        var result = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);

        // Convert angles to radians
        var azimuthRad = azimuth * Math.PI / 180.0;
        var altitudeRad = altitude * Math.PI / 180.0;

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                // Get elevation values in 3x3 neighborhood
                var a = this.GetElevation(elevationBitmap, x - 1, y - 1);
                var b = this.GetElevation(elevationBitmap, x, y - 1);
                var c = this.GetElevation(elevationBitmap, x + 1, y - 1);
                var d = this.GetElevation(elevationBitmap, x - 1, y);
                var f = this.GetElevation(elevationBitmap, x + 1, y);
                var g = this.GetElevation(elevationBitmap, x - 1, y + 1);
                var h = this.GetElevation(elevationBitmap, x, y + 1);
                var i = this.GetElevation(elevationBitmap, x + 1, y + 1);

                // Calculate slope and aspect using Horn's method
                var dzdx = ((c + 2 * f + i) - (a + 2 * d + g)) / (8.0 * zFactor);
                var dzdy = ((g + 2 * h + i) - (a + 2 * b + c)) / (8.0 * zFactor);

                var slopeRad = Math.Atan(Math.Sqrt(dzdx * dzdx + dzdy * dzdy));
                var aspectRad = Math.Atan2(dzdy, -dzdx);

                // Calculate hillshade
                var hillshade = Math.Cos(altitudeRad) * Math.Cos(slopeRad) +
                               Math.Sin(altitudeRad) * Math.Sin(slopeRad) * Math.Cos(azimuthRad - aspectRad);

                var shade = (byte)Math.Clamp(hillshade * 255, 0, 255);
                result.SetPixel(x, y, new SKColor(shade, shade, shade, 255));
            }
        }

        return result;
    }

    private SKBitmap CalculateSlope(SKBitmap elevationBitmap, double zFactor)
    {
        var width = elevationBitmap.Width;
        var height = elevationBitmap.Height;
        var result = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                // Get elevation values in 3x3 neighborhood
                var a = this.GetElevation(elevationBitmap, x - 1, y - 1);
                var b = this.GetElevation(elevationBitmap, x, y - 1);
                var c = this.GetElevation(elevationBitmap, x + 1, y - 1);
                var d = this.GetElevation(elevationBitmap, x - 1, y);
                var f = this.GetElevation(elevationBitmap, x + 1, y);
                var g = this.GetElevation(elevationBitmap, x - 1, y + 1);
                var h = this.GetElevation(elevationBitmap, x, y + 1);
                var i = this.GetElevation(elevationBitmap, x + 1, y + 1);

                // Calculate slope using Horn's method
                var dzdx = ((c + 2 * f + i) - (a + 2 * d + g)) / (8.0 * zFactor);
                var dzdy = ((g + 2 * h + i) - (a + 2 * b + c)) / (8.0 * zFactor);

                var slopeDegrees = Math.Atan(Math.Sqrt(dzdx * dzdx + dzdy * dzdy)) * 180.0 / Math.PI;

                // Normalize to 0-255 (0-90 degrees)
                var value = (byte)Math.Clamp(slopeDegrees * 255.0 / 90.0, 0, 255);
                result.SetPixel(x, y, new SKColor(value, value, value, 255));
            }
        }

        return result;
    }

    private SKBitmap CalculateAspect(SKBitmap elevationBitmap)
    {
        var width = elevationBitmap.Width;
        var height = elevationBitmap.Height;
        var result = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                // Get elevation values in 3x3 neighborhood
                var a = this.GetElevation(elevationBitmap, x - 1, y - 1);
                var b = this.GetElevation(elevationBitmap, x, y - 1);
                var c = this.GetElevation(elevationBitmap, x + 1, y - 1);
                var d = this.GetElevation(elevationBitmap, x - 1, y);
                var f = this.GetElevation(elevationBitmap, x + 1, y);
                var g = this.GetElevation(elevationBitmap, x - 1, y + 1);
                var h = this.GetElevation(elevationBitmap, x, y + 1);
                var i = this.GetElevation(elevationBitmap, x + 1, y + 1);

                // Calculate aspect
                var dzdx = ((c + 2 * f + i) - (a + 2 * d + g)) / 8.0;
                var dzdy = ((g + 2 * h + i) - (a + 2 * b + c)) / 8.0;

                var aspectRad = Math.Atan2(dzdy, -dzdx);
                var aspectDegrees = aspectRad * 180.0 / Math.PI;
                if (aspectDegrees < 0)
                {
                    aspectDegrees += 360.0;
                }

                // Normalize to 0-255 (0-360 degrees)
                var value = (byte)Math.Clamp(aspectDegrees * 255.0 / 360.0, 0, 255);

                // Color code by direction (N=red, E=green, S=blue, W=yellow)
                var hue = (float)aspectDegrees;
                var color = SKColor.FromHsl(hue, 100, 50);
                result.SetPixel(x, y, color);
            }
        }

        return result;
    }

    private SKBitmap CalculateCurvature(SKBitmap elevationBitmap, double zFactor)
    {
        var width = elevationBitmap.Width;
        var height = elevationBitmap.Height;
        var result = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                var center = this.GetElevation(elevationBitmap, x, y);
                var left = this.GetElevation(elevationBitmap, x - 1, y);
                var right = this.GetElevation(elevationBitmap, x + 1, y);
                var top = this.GetElevation(elevationBitmap, x, y - 1);
                var bottom = this.GetElevation(elevationBitmap, x, y + 1);

                // Second derivative (curvature)
                var curvature = ((left + right + top + bottom) - 4 * center) / (zFactor * zFactor);

                // Normalize and clamp
                var value = (byte)Math.Clamp((curvature + 1) * 127.5, 0, 255);
                result.SetPixel(x, y, new SKColor(value, value, value, 255));
            }
        }

        return result;
    }

    private SKBitmap CalculateRoughness(SKBitmap elevationBitmap)
    {
        var width = elevationBitmap.Width;
        var height = elevationBitmap.Height;
        var result = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                var center = GetElevation(elevationBitmap, x, y);

                // Calculate standard deviation in 3x3 neighborhood
                var values = new List<double>();
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        values.Add(this.GetElevation(elevationBitmap, x + dx, y + dy));
                    }
                }

                var mean = values.Average();
                var variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
                var roughness = Math.Sqrt(variance);

                // Normalize
                var value = (byte)Math.Clamp(roughness * 10, 0, 255);
                result.SetPixel(x, y, new SKColor(value, value, value, 255));
            }
        }

        return result;
    }

    private double GetElevation(SKBitmap bitmap, int x, int y)
    {
        if (x < 0 || x >= bitmap.Width || y < 0 || y >= bitmap.Height)
        {
            return 0;
        }

        var pixel = bitmap.GetPixel(x, y);
        // Average RGB values as elevation
        return (pixel.Red + pixel.Green + pixel.Blue) / 3.0;
    }

    private TerrainAnalysisStatistics CalculateTerrainStatistics(SKBitmap bitmap, TerrainAnalysisType analysisType)
    {
        var values = new List<double>();
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                values.Add((pixel.Red + pixel.Green + pixel.Blue) / 3.0);
            }
        }

        var min = values.Min();
        var max = values.Max();
        var mean = values.Average();
        var stdDev = Math.Sqrt(values.Sum(v => Math.Pow(v - mean, 2)) / values.Count);

        var unit = analysisType switch
        {
            TerrainAnalysisType.Slope => "degrees",
            TerrainAnalysisType.Aspect => "degrees",
            TerrainAnalysisType.Hillshade => "0-255",
            TerrainAnalysisType.Curvature => "units",
            TerrainAnalysisType.Roughness => "units",
            _ => "units"
        };

        return new TerrainAnalysisStatistics(min, max, mean, stdDev, unit);
    }

    /// <inheritdoc/>
    public RasterAnalyticsCapabilities GetCapabilities()
    {
        return new RasterAnalyticsCapabilities(
            SupportedAlgebraOperators: new[] { "+", "-", "*", "/", "min", "max", "mean", "avg", "stddev", "sqrt", "square", "log" },
            SupportedAlgebraFunctions: new[] { "ndvi", "evi", "savi", "ndwi", "ndmi", "normalize" },
            SupportedTerrainAnalyses: new[] { "hillshade", "slope", "aspect", "curvature", "roughness" },
            MaxAlgebraDatasets: this.memoryLimits.MaxSimultaneousDatasets,
            MaxExtractionPoints: this.memoryLimits.MaxExtractionPoints,
            MaxHistogramBins: this.memoryLimits.MaxHistogramBins,
            MaxZonalPolygons: this.memoryLimits.MaxZonalPolygons);
    }

    private BandStatistics CalculateBandStatistics(int bandIndex, List<double> values)
    {
        if (values.Count == 0)
        {
            return new BandStatistics(bandIndex, 0, 0, 0, 0, 0, 0, 0, null);
        }

        var min = values.Min();
        var max = values.Max();
        var mean = values.Average();
        var stdDev = Math.Sqrt(values.Average(v => Math.Pow(v - mean, 2)));

        // Sort in-place to avoid additional allocations
        values.Sort();
        var median = values.Count % 2 == 0
            ? (values[values.Count / 2 - 1] + values[values.Count / 2]) / 2
            : values[values.Count / 2];

        return new BandStatistics(
            bandIndex,
            min,
            max,
            mean,
            stdDev,
            median,
            values.Count,
            0,
            null);
    }

    private List<double> ExtractBandValues(SKBitmap bitmap, int band)
    {
        var values = new List<double>();

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var color = bitmap.GetPixel(x, y);
                if (color.Alpha > 0)
                {
                    var value = this.ExtractBandValue(color, band);
                    if (value.HasValue)
                    {
                        values.Add(value.Value);
                    }
                }
            }
        }

        return values;
    }

    private List<double> ExtractAllValues(SKBitmap bitmap)
    {
        var values = new List<double>();

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var color = bitmap.GetPixel(x, y);
                if (color.Alpha > 0)
                {
                    var gray = (color.Red + color.Green + color.Blue) / 3.0;
                    values.Add(gray);
                }
            }
        }

        return values;
    }

    private double? ExtractBandValue(SKColor color, int band)
    {
        return band switch
        {
            0 => color.Red,
            1 => color.Green,
            2 => color.Blue,
            3 => color.Alpha,
            _ => null
        };
    }

    private SKBitmap ApplyAlgebraExpression(List<SKBitmap> bitmaps, string expression, int width, int height)
    {
        var resultBitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);

        // Simplified algebra - support basic operations and indices
        var expr = expression.ToLowerInvariant().Trim();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var value = this.EvaluateAlgebraAtPixel(bitmaps, expr, x, y);
                var normalized = Math.Clamp(value, 0, 255);
                var gray = (byte)normalized;
                resultBitmap.SetPixel(x, y, new SKColor(gray, gray, gray, 255));
            }
        }

        return resultBitmap;
    }

    private double EvaluateAlgebraAtPixel(List<SKBitmap> bitmaps, string expression, int x, int y)
    {
        var expr = expression.ToLowerInvariant().Trim();

        // Vegetation Indices
        if (expr.Contains("ndvi") && bitmaps.Count >= 2)
        {
            // NDVI = (NIR - RED) / (NIR + RED)
            var nir = this.GetPixelValue(bitmaps[0], x, y);
            var red = this.GetPixelValue(bitmaps[1], x, y);
            var ndvi = red + nir > 0 ? (nir - red) / (nir + red) : 0;
            return (ndvi + 1) * 127.5; // Scale from [-1, 1] to [0, 255]
        }

        if (expr.Contains("evi") && bitmaps.Count >= 3)
        {
            // EVI = 2.5 * ((NIR - RED) / (NIR + 6 * RED - 7.5 * BLUE + 1))
            var nir = this.GetPixelValue(bitmaps[0], x, y) / 255.0;
            var red = this.GetPixelValue(bitmaps[1], x, y) / 255.0;
            var blue = this.GetPixelValue(bitmaps[2], x, y) / 255.0;
            var denominator = nir + 6 * red - 7.5 * blue + 1;
            var evi = denominator != 0 ? 2.5 * ((nir - red) / denominator) : 0;
            return Math.Clamp((evi + 1) * 127.5, 0, 255);
        }

        if (expr.Contains("savi") && bitmaps.Count >= 2)
        {
            // SAVI = ((NIR - RED) / (NIR + RED + L)) * (1 + L)
            // L = 0.5 for intermediate vegetation
            var nir = GetPixelValue(bitmaps[0], x, y) / 255.0;
            var red = GetPixelValue(bitmaps[1], x, y) / 255.0;
            const double L = 0.5;
            var denominator = nir + red + L;
            var savi = denominator > 0 ? ((nir - red) / denominator) * (1 + L) : 0;
            return (savi + 1) * 127.5;
        }

        if (expr.Contains("ndwi") && bitmaps.Count >= 2)
        {
            // NDWI = (GREEN - NIR) / (GREEN + NIR)
            var green = this.GetPixelValue(bitmaps[0], x, y);
            var nir = this.GetPixelValue(bitmaps[1], x, y);
            var ndwi = green + nir > 0 ? (green - nir) / (green + nir) : 0;
            return (ndwi + 1) * 127.5;
        }

        if (expr.Contains("ndmi") && bitmaps.Count >= 2)
        {
            // NDMI = (NIR - SWIR) / (NIR + SWIR) - Moisture Index
            var nir = this.GetPixelValue(bitmaps[0], x, y);
            var swir = this.GetPixelValue(bitmaps[1], x, y);
            var ndmi = nir + swir > 0 ? (nir - swir) / (nir + swir) : 0;
            return (ndmi + 1) * 127.5;
        }

        // Basic arithmetic operations
        if (expr.Contains("-") && bitmaps.Count >= 2)
        {
            return this.GetPixelValue(bitmaps[0], x, y) - this.GetPixelValue(bitmaps[1], x, y);
        }

        if (expr.Contains("+") && bitmaps.Count >= 2)
        {
            return this.GetPixelValue(bitmaps[0], x, y) + this.GetPixelValue(bitmaps[1], x, y);
        }

        if (expr.Contains("*") && bitmaps.Count >= 2)
        {
            return this.GetPixelValue(bitmaps[0], x, y) * this.GetPixelValue(bitmaps[1], x, y) / 255.0;
        }

        if (expr.Contains("/") && bitmaps.Count >= 2)
        {
            var divisor = this.GetPixelValue(bitmaps[1], x, y);
            return divisor > 0 ? (this.GetPixelValue(bitmaps[0], x, y) / divisor) * 255.0 : 0;
        }

        // Statistical operations
        if (expr.Contains("mean") || expr.Contains("avg"))
        {
            return bitmaps.Average(b => this.GetPixelValue(b, x, y));
        }

        if (expr.Contains("min") && bitmaps.Count >= 2)
        {
            return bitmaps.Min(b => this.GetPixelValue(b, x, y));
        }

        if (expr.Contains("max") && bitmaps.Count >= 2)
        {
            return bitmaps.Max(b => this.GetPixelValue(b, x, y));
        }

        if (expr.Contains("stddev") && bitmaps.Count >= 2)
        {
            var values = bitmaps.Select(b => this.GetPixelValue(b, x, y)).ToList();
            var mean = values.Average();
            var variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
            return Math.Sqrt(variance);
        }

        // Single raster operations
        if (expr.Contains("normalize") && bitmaps.Count >= 1)
        {
            // Normalize to 0-255 range
            return this.GetPixelValue(bitmaps[0], x, y);
        }

        if (expr.Contains("sqrt") && bitmaps.Count >= 1)
        {
            return Math.Sqrt(GetPixelValue(bitmaps[0], x, y));
        }

        if (expr.Contains("square") && bitmaps.Count >= 1)
        {
            var val = this.GetPixelValue(bitmaps[0], x, y) / 255.0;
            return val * val * 255.0;
        }

        if (expr.Contains("log") && bitmaps.Count >= 1)
        {
            var val = this.GetPixelValue(bitmaps[0], x, y);
            return val > 0 ? Math.Log(val) * 50 : 0;
        }

        return bitmaps.Count > 0 ? this.GetPixelValue(bitmaps[0], x, y) : 0;
    }

    private double GetPixelValue(SKBitmap bitmap, int x, int y)
    {
        if (x < 0 || x >= bitmap.Width || y < 0 || y >= bitmap.Height)
        {
            return 0;
        }

        var color = bitmap.GetPixel(x, y);
        return (color.Red + color.Green + color.Blue) / 3.0;
    }

    private (int pixelX, int pixelY) WorldToPixel(double worldX, double worldY, double[] extent, int width, int height)
    {
        var normX = (worldX - extent[0]) / (extent[2] - extent[0]);
        var normY = (extent[3] - worldY) / (extent[3] - extent[1]);

        var pixelX = (int)(normX * width);
        var pixelY = (int)(normY * height);

        return (pixelX, pixelY);
    }

    private double[] GetDatasetExtent(RasterDatasetDefinition dataset)
    {
        var bbox = dataset.Extent?.Bbox?.FirstOrDefault();
        return bbox ?? throw new InvalidOperationException($"Dataset {dataset.Id} has no extent");
    }

    private SKBitmap? LoadDatasetBitmap(RasterDatasetDefinition dataset)
    {
        var filePath = dataset.Source.Uri;
        if (!File.Exists(filePath))
        {
            return null;
        }

        // Only accept GeoTIFF/TIFF - PNG/JPEG lack georeferencing and proper GIS metadata
        if (!filePath.EndsWith(".tif", StringComparison.OrdinalIgnoreCase) &&
            !filePath.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"Unsupported raster format: {Path.GetExtension(filePath)}. " +
                "Only GeoTIFF (.tif, .tiff) formats are supported for raster analytics. " +
                "PNG/JPEG lack georeferencing, coordinate systems, and proper data types required for GIS analysis.");
        }

        try
        {
            return this.LoadTiffBitmap(filePath);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to load TIFF bitmap from {FilePath}", filePath);
            return null;
        }
    }

    private SKBitmap? LoadTiffBitmap(string filePath)
    {
        using var tiff = Tiff.Open(filePath, "r");
        if (tiff == null)
        {
            return null;
        }

        var width = tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
        var height = tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
        var pixelCount = width * height;

        // Use ArrayPool to avoid LOH allocations for large images
        var raster = ArrayPool<int>.Shared.Rent(pixelCount);
        var pixels = ArrayPool<SKColor>.Shared.Rent(pixelCount);
        try
        {
            if (!tiff.ReadRGBAImageOriented(width, height, raster, Orientation.TOPLEFT))
            {
                return null;
            }

            var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            for (int i = 0; i < pixelCount; i++)
            {
                var argb = raster[i];
                var a = (byte)((argb >> 24) & 0xFF);
                var r = (byte)((argb >> 16) & 0xFF);
                var g = (byte)((argb >> 8) & 0xFF);
                var b = (byte)(argb & 0xFF);
                pixels[i] = new SKColor(r, g, b, a);
            }

            // Copy only the used portion to the bitmap
            var pixelSpan = new Span<SKColor>(pixels, 0, pixelCount);
            bitmap.Pixels = pixelSpan.ToArray();

            return bitmap;
        }
        finally
        {
            // Always return rented arrays to the pool
            ArrayPool<int>.Shared.Return(raster);
            ArrayPool<SKColor>.Shared.Return(pixels);
        }
    }

    private SKEncodedImageFormat GetEncodedImageFormat(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "png" => SKEncodedImageFormat.Png,
            "jpeg" or "jpg" => SKEncodedImageFormat.Jpeg,
            _ => SKEncodedImageFormat.Png
        };
    }
}
