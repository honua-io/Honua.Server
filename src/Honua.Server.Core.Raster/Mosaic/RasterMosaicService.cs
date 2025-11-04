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
namespace Honua.Server.Core.Raster.Mosaic;

public sealed class RasterMosaicService : IRasterMosaicService
{
    private const int MaxDatasets = 100;
    private const int MaxWidth = 8192;
    private const int MaxHeight = 8192;

    private readonly ILogger<RasterMosaicService> _logger;

    public RasterMosaicService(ILogger<RasterMosaicService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<RasterMosaicResult> CreateMosaicAsync(RasterMosaicRequest request, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);

        if (request.Datasets.Count == 0)
        {
            throw new ArgumentException("At least one dataset is required", nameof(request));
        }

        if (request.Datasets.Count > MaxDatasets)
        {
            throw new ArgumentException($"Maximum {MaxDatasets} datasets allowed", nameof(request));
        }

        if (request.Width > MaxWidth || request.Height > MaxHeight)
        {
            throw new ArgumentException($"Maximum dimensions: {MaxWidth}x{MaxHeight}", nameof(request));
        }

        var normalizedFormat = RasterFormatHelper.Normalize(request.Format);
        var contentType = RasterFormatHelper.GetContentType(normalizedFormat);

        var info = new SKImageInfo(request.Width, request.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;

        var background = normalizedFormat == "png" && request.Transparent
            ? SKColors.Transparent
            : SKColors.Black;
        canvas.Clear(background);

        var destRect = new SKRect(0, 0, request.Width, request.Height);
        var sampling = GetSamplingOptions(request.Resampling);

        // Load and composite all datasets based on method
        var loadedBitmaps = new List<(RasterDatasetDefinition dataset, SKBitmap bitmap)>();
        try
        {
            foreach (var dataset in request.Datasets)
            {
                var bitmap = LoadDatasetBitmap(dataset);
                if (bitmap != null)
                {
                    loadedBitmaps.Add((dataset, bitmap));
                }
            }

            if (loadedBitmaps.Count == 0)
            {
                throw new InvalidOperationException("No datasets could be loaded");
            }

            CompositeDatasets(canvas, sampling, request.BoundingBox, destRect, loadedBitmaps, request.Method);

            var image = surface.Snapshot();
            using var data = image.Encode(GetEncodedImageFormat(normalizedFormat), 90);
            var bytes = data.ToArray();

            var metadata = new RasterMosaicMetadata(
                loadedBitmaps.Count,
                loadedBitmaps.Select(x => x.dataset.Id).ToList(),
                request.BoundingBox,
                request.Method.ToString(),
                request.Resampling.ToString());

            var result = new RasterMosaicResult(bytes, contentType, request.Width, request.Height, metadata);
            return Task.FromResult(result);
        }
        finally
        {
            foreach (var (_, bitmap) in loadedBitmaps)
            {
                bitmap?.Dispose();
            }
        }
    }

    public RasterMosaicCapabilities GetCapabilities()
    {
        return new RasterMosaicCapabilities(
            SupportedMethods: Enum.GetNames<RasterMosaicMethod>().ToList(),
            SupportedResamplingMethods: Enum.GetNames<RasterResamplingMethod>().ToList(),
            SupportedFormats: new[] { "png", "jpeg" },
            MaxDatasets: MaxDatasets,
            MaxWidth: MaxWidth,
            MaxHeight: MaxHeight);
    }

    private void CompositeDatasets(
        SKCanvas canvas,
        SKSamplingOptions sampling,
        double[] bbox,
        SKRect destRect,
        List<(RasterDatasetDefinition dataset, SKBitmap bitmap)> loadedBitmaps,
        RasterMosaicMethod method)
    {
        switch (method)
        {
            case RasterMosaicMethod.First:
            case RasterMosaicMethod.Last:
            case RasterMosaicMethod.Blend:
                CompositeLayered(canvas, sampling, bbox, destRect, loadedBitmaps, method);
                break;

            case RasterMosaicMethod.Min:
            case RasterMosaicMethod.Max:
            case RasterMosaicMethod.Mean:
            case RasterMosaicMethod.Median:
                CompositePixelwise(canvas, bbox, destRect, loadedBitmaps, method);
                break;

            default:
                throw new ArgumentException($"Unsupported mosaic method: {method}");
        }
    }

    private void CompositeLayered(
        SKCanvas canvas,
        SKSamplingOptions sampling,
        double[] bbox,
        SKRect destRect,
        List<(RasterDatasetDefinition dataset, SKBitmap bitmap)> loadedBitmaps,
        RasterMosaicMethod method)
    {
        var datasets = method == RasterMosaicMethod.Last
            ? loadedBitmaps.AsEnumerable().Reverse()
            : loadedBitmaps.AsEnumerable();

        var blendMode = method == RasterMosaicMethod.Blend ? SKBlendMode.SrcOver : SKBlendMode.Src;

        using var paint = new SKPaint
        {
            IsAntialias = true,
            BlendMode = blendMode
        };

        foreach (var (dataset, bitmap) in datasets)
        {
            var datasetExtent = GetDatasetExtent(dataset);
            var sourceRect = CalculateSourceRect(bitmap.Width, bitmap.Height, datasetExtent, bbox);
            canvas.DrawBitmap(bitmap, sourceRect, destRect, paint);
        }
    }

    /// <summary>
    /// Composites datasets pixel-by-pixel using statistical methods (min, max, mean, median).
    /// Uses ArrayPool to avoid Large Object Heap allocations for large images.
    /// </summary>
    private void CompositePixelwise(
        SKCanvas canvas,
        double[] bbox,
        SKRect destRect,
        List<(RasterDatasetDefinition dataset, SKBitmap bitmap)> loadedBitmaps,
        RasterMosaicMethod method)
    {
        var width = (int)destRect.Width;
        var height = (int)destRect.Height;
        var pixelCount = width * height;

        using var resultBitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);

        // Use ArrayPool to avoid LOH allocations for large images (>85KB = ~21K pixels = ~145x145)
        var pixels = ArrayPool<SKColor>.Shared.Rent(pixelCount);
        try
        {
            // For each pixel, collect values from all datasets and apply method
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var values = new List<SKColor>();

                    foreach (var (dataset, bitmap) in loadedBitmaps)
                    {
                        var datasetExtent = GetDatasetExtent(dataset);
                        var sourcePoint = MapDestToSource(x, y, width, height, bbox, datasetExtent, bitmap.Width, bitmap.Height);

                        if (sourcePoint.X >= 0 && sourcePoint.X < bitmap.Width &&
                            sourcePoint.Y >= 0 && sourcePoint.Y < bitmap.Height)
                        {
                            var color = bitmap.GetPixel((int)sourcePoint.X, (int)sourcePoint.Y);
                            if (color.Alpha > 0) // Exclude transparent/nodata pixels
                            {
                                values.Add(color);
                            }
                        }
                    }

                    pixels[y * width + x] = values.Count > 0
                        ? ApplyPixelMethod(values, method)
                        : SKColors.Transparent;
                }
            }

            // Copy only the used portion to the bitmap
            var pixelSpan = new Span<SKColor>(pixels, 0, pixelCount);
            resultBitmap.Pixels = pixelSpan.ToArray();
            canvas.DrawBitmap(resultBitmap, destRect);
        }
        finally
        {
            // Always return the rented array to the pool
            ArrayPool<SKColor>.Shared.Return(pixels);
        }
    }

    private SKColor ApplyPixelMethod(List<SKColor> values, RasterMosaicMethod method)
    {
        if (values.Count == 0)
        {
            return SKColors.Transparent;
        }

        return method switch
        {
            RasterMosaicMethod.Min => values.OrderBy(c => c.Red + c.Green + c.Blue).First(),
            RasterMosaicMethod.Max => values.OrderByDescending(c => c.Red + c.Green + c.Blue).First(),
            RasterMosaicMethod.Mean => AverageColor(values),
            RasterMosaicMethod.Median => MedianColor(values),
            _ => values.First()
        };
    }

    private SKColor AverageColor(List<SKColor> colors)
    {
        var avgR = (byte)colors.Average(c => c.Red);
        var avgG = (byte)colors.Average(c => c.Green);
        var avgB = (byte)colors.Average(c => c.Blue);
        var avgA = (byte)colors.Average(c => c.Alpha);
        return new SKColor(avgR, avgG, avgB, avgA);
    }

    private SKColor MedianColor(List<SKColor> colors)
    {
        // Sort in-place by intensity to avoid additional allocations
        colors.Sort((a, b) => (a.Red + a.Green + a.Blue).CompareTo(b.Red + b.Green + b.Blue));
        return colors[colors.Count / 2];
    }

    private SKPoint MapDestToSource(int destX, int destY, int destWidth, int destHeight,
        double[] destBbox, double[] sourceBbox, int sourceWidth, int sourceHeight)
    {
        var normX = (double)destX / destWidth;
        var normY = (double)destY / destHeight;

        var worldX = destBbox[0] + normX * (destBbox[2] - destBbox[0]);
        var worldY = destBbox[3] - normY * (destBbox[3] - destBbox[1]);

        var sourceNormX = (worldX - sourceBbox[0]) / (sourceBbox[2] - sourceBbox[0]);
        var sourceNormY = (sourceBbox[3] - worldY) / (sourceBbox[3] - sourceBbox[1]);

        var sourceX = (float)(sourceNormX * sourceWidth);
        var sourceY = (float)(sourceNormY * sourceHeight);

        return new SKPoint(sourceX, sourceY);
    }

    private double[] GetDatasetExtent(RasterDatasetDefinition dataset)
    {
        var bbox = dataset.Extent?.Bbox?.FirstOrDefault();
        return bbox ?? throw new InvalidOperationException($"Dataset {dataset.Id} has no extent");
    }

    private SKRect CalculateSourceRect(int sourceWidth, int sourceHeight, double[] sourceExtent, double[] requestExtent)
    {
        var left = (float)((requestExtent[0] - sourceExtent[0]) / (sourceExtent[2] - sourceExtent[0]) * sourceWidth);
        var top = (float)((sourceExtent[3] - requestExtent[3]) / (sourceExtent[3] - sourceExtent[1]) * sourceHeight);
        var right = (float)((requestExtent[2] - sourceExtent[0]) / (sourceExtent[2] - sourceExtent[0]) * sourceWidth);
        var bottom = (float)((sourceExtent[3] - requestExtent[1]) / (sourceExtent[3] - sourceExtent[1]) * sourceHeight);

        return SKRect.Create(left, top, right - left, bottom - top);
    }

    private SKBitmap? LoadDatasetBitmap(RasterDatasetDefinition dataset)
    {
        var filePath = dataset.Source.Uri;
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            if (filePath.EndsWith(".tif", StringComparison.OrdinalIgnoreCase) ||
                filePath.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase))
            {
                return LoadTiffBitmap(filePath);
            }

            return SKBitmap.Decode(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load dataset bitmap from {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Loads a TIFF bitmap using LibTiff.
    /// Uses ArrayPool to avoid Large Object Heap allocations for large images.
    /// </summary>
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

        // Use ArrayPool to avoid LOH allocations
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

    private SKSamplingOptions GetSamplingOptions(RasterResamplingMethod method)
    {
        return method switch
        {
            RasterResamplingMethod.NearestNeighbor => new SKSamplingOptions(SKFilterMode.Nearest),
            RasterResamplingMethod.Bilinear => new SKSamplingOptions(SKFilterMode.Linear),
            RasterResamplingMethod.Cubic => new SKSamplingOptions(SKCubicResampler.CatmullRom),
            RasterResamplingMethod.Lanczos => new SKSamplingOptions(SKCubicResampler.Mitchell),
            _ => new SKSamplingOptions(SKFilterMode.Linear)
        };
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
