// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using BitMiracle.LibTiff.Classic;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Sources;
using NetTopologySuite.Geometries;
using SkiaSharp;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Rendering;

public sealed class SkiaSharpRasterRenderer : IRasterRenderer
{
    private const int GeoPixelScaleTagValue = 33550;
    private const int GeoTiePointsTagValue = 33922;
    private const int GeoKeyDirectoryTagValue = 34735;
    private const int GdalNoDataTagValue = 42113;

    private readonly IRasterSourceProviderRegistry _sourceProviderRegistry;
    private readonly RasterMetadataCache _metadataCache;

    public SkiaSharpRasterRenderer(IRasterSourceProviderRegistry sourceProviderRegistry, RasterMetadataCache metadataCache)
    {
        _sourceProviderRegistry = sourceProviderRegistry ?? throw new ArgumentNullException(nameof(sourceProviderRegistry));
        _metadataCache = metadataCache ?? throw new ArgumentNullException(nameof(metadataCache));
    }

    public async Task<RasterRenderResult> RenderAsync(RasterRenderRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedFormat = RasterFormatHelper.Normalize(request.Format);
        var contentType = RasterFormatHelper.GetContentType(normalizedFormat);

        using var datasetBitmap = await LoadDatasetBitmapAsync(request.Dataset, cancellationToken).ConfigureAwait(false);
        if (datasetBitmap is null)
        {
            return RenderFallback(request, normalizedFormat, contentType);
        }

        var datasetExtent = GetDatasetExtent(request.Dataset);
        var requestBbox = request.BoundingBox is { Length: 4 } bbox
            ? bbox
            : datasetExtent;

        var info = new SKImageInfo(request.Width, request.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;

        var background = normalizedFormat == "png" && request.Transparent
            ? SKColors.Transparent
            : SKColors.Black;
        canvas.Clear(background);

        var destRect = new SKRect(0, 0, request.Width, request.Height);

        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
        DrawRasterLayer(canvas, sampling, requestBbox, destRect, request.Dataset, datasetBitmap);

        if (request.AdditionalLayers is { Count: > 0 })
        {
            foreach (var layer in request.AdditionalLayers)
            {
                using var overlayBitmap = await LoadDatasetBitmapAsync(layer.Dataset, cancellationToken).ConfigureAwait(false);
                if (overlayBitmap is null)
                {
                    continue;
                }

                DrawRasterLayer(canvas, sampling, requestBbox, destRect, layer.Dataset, overlayBitmap);
            }
        }

        var style = ResolveStyle(request.Dataset, request.StyleId, request.Style);
        DrawVectorGeometries(canvas, request, style);

        if (request.Dataset.Title.HasValue())
        {
            var fontSize = Math.Max(12f, Math.Min(request.Width, request.Height) * 0.08f);
            using var typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
            using var font = new SKFont(typeface, fontSize);
            using var textPaint = new SKPaint
            {
                Color = style.LabelColor,
                IsAntialias = true,
                IsStroke = false
            };

            var label = request.Dataset.Title;
            var textWidth = font.MeasureText(label, textPaint);
            var metrics = font.Metrics;
            var textHeight = metrics.Descent - metrics.Ascent;
            var padding = fontSize * 0.5f;
            var x = padding;
            var y = request.Height - padding - metrics.Descent;
            using var backdropPaint = new SKPaint
            {
                Color = SKColors.Black.WithAlpha(128),
                IsAntialias = true
            };
            var rect = new SKRect(x - padding * 0.5f, y + metrics.Ascent - padding * 0.25f, x + textWidth + padding * 0.5f, y + metrics.Descent + padding * 0.25f);
            canvas.DrawRoundRect(rect, padding * 0.25f, padding * 0.25f, backdropPaint);
            canvas.DrawText(label, x, y, SKTextAlign.Left, font, textPaint);
        }

        using var image = surface.Snapshot();
        var encodedFormat = normalizedFormat == "jpeg"
            ? SKEncodedImageFormat.Jpeg
            : SKEncodedImageFormat.Png;

        using var data = image.Encode(encodedFormat, 90);
        var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Seek(0, SeekOrigin.Begin);

        return new RasterRenderResult(stream, contentType, request.Width, request.Height);
    }

    private void DrawRasterLayer(
        SKCanvas canvas,
        SKSamplingOptions sampling,
        double[]? requestBbox,
        SKRect destRect,
        RasterDatasetDefinition dataset,
        SKBitmap bitmap)
    {
        var datasetExtent = GetDatasetExtent(dataset);
        var srcRect = CalculateSourceRectangle(requestBbox, datasetExtent, bitmap.Width, bitmap.Height);
        using var paint = new SKPaint { IsAntialias = true };
        using var image = SKImage.FromBitmap(bitmap);
        canvas.DrawImage(image, srcRect, destRect, sampling, paint);
    }

    private static RasterStyle ResolveStyle(RasterDatasetDefinition dataset, string? styleId, StyleDefinition? style)
    {
        if (style is not null)
        {
            return BuildRasterStyle(style);
        }

        var requested = styleId;
        if (requested.IsNullOrWhiteSpace())
        {
            requested = dataset.Styles.DefaultStyleId;
        }

        if (requested.IsNullOrWhiteSpace())
        {
            requested = dataset.Id;
        }

        if (requested.HasValue() && StyleLibrary.TryGetValue(requested, out var palette))
        {
            return palette;
        }

        if (dataset.Styles.StyleIds.Count > 0)
        {
            foreach (var candidate in dataset.Styles.StyleIds)
            {
                if (candidate.HasValue() && StyleLibrary.TryGetValue(candidate, out var candidatePalette))
                {
                    return candidatePalette;
                }
            }
        }

        return CreateFallbackStyle(requested ?? dataset.Id);
    }

    private static RasterStyle BuildRasterStyle(StyleDefinition style)
    {
        var symbol = ResolvePrimarySymbol(style);
        if (symbol is null)
        {
            return CreateFallbackStyle(style.Id);
        }

        var defaultFill = new SKColor(90, 160, 110, 200);
        var defaultStroke = new SKColor(255, 255, 255, 230);

        var fillColor = ParseColor(symbol.FillColor, defaultFill);
        var strokeColor = ParseColor(symbol.StrokeColor, defaultStroke);

        if (symbol.Opacity is double opacity)
        {
            var alpha = (byte)Math.Clamp(Math.Round(Math.Clamp(opacity, 0d, 1d) * 255d), 0d, 255d);
            fillColor = fillColor.WithAlpha(alpha);
            strokeColor = strokeColor.WithAlpha(alpha);
        }

        var gradientStart = AdjustBrightness(fillColor, 1.1f).WithAlpha(fillColor.Alpha);
        var gradientEnd = AdjustBrightness(fillColor, 0.85f).WithAlpha(fillColor.Alpha);
        var baseBackground = symbol.SymbolType?.Equals("line", StringComparison.OrdinalIgnoreCase) == true
            ? strokeColor.WithAlpha(255)
            : fillColor.WithAlpha(255);
        var background = AdjustBrightness(baseBackground, 0.35f);
        var labelColor = strokeColor.WithAlpha(255);
        var strokeWidth = (float)(symbol.StrokeWidth ?? 2.0);

        return new RasterStyle(
            Background: background,
            GradientStart: gradientStart,
            GradientEnd: gradientEnd,
            LabelColor: labelColor,
            StrokeColor: strokeColor,
            FillColor: fillColor,
            StrokeWidth: strokeWidth);
    }

    private static SimpleStyleDefinition? ResolvePrimarySymbol(StyleDefinition style)
    {
        if (style.Simple is not null)
        {
            return style.Simple;
        }

        if (style.Rules is { Count: > 0 })
        {
            var rule = style.Rules.FirstOrDefault(r => r.IsDefault) ?? style.Rules[0];
            if (rule is not null)
            {
                return rule.Symbolizer;
            }
        }

        if (style.UniqueValue is { } unique)
        {
            return unique.DefaultSymbol ?? unique.Classes.FirstOrDefault()?.Symbol;
        }

        return null;
    }

    private static SKColor ParseColor(string? raw, SKColor fallback)
    {
        if (raw.IsNullOrWhiteSpace())
        {
            return fallback;
        }

        var value = raw.Trim();
        if (!value.StartsWith("#", StringComparison.OrdinalIgnoreCase))
        {
            value = "#" + value;
        }

        try
        {
            return SKColor.Parse(value);
        }
        catch
        {
            return fallback;
        }
    }

    private static SKColor AdjustBrightness(SKColor color, float factor)
    {
        var r = (byte)Math.Clamp(color.Red * factor, 0, 255);
        var g = (byte)Math.Clamp(color.Green * factor, 0, 255);
        var b = (byte)Math.Clamp(color.Blue * factor, 0, 255);
        return new SKColor(r, g, b, color.Alpha);
    }

    private static RasterStyle CreateFallbackStyle(string key)
    {
        var hashAccumulator = new HashCode();
        hashAccumulator.Add(key, StringComparer.OrdinalIgnoreCase);
        var hash = hashAccumulator.ToHashCode();
        var baseColor = new SKColor(
            (byte)(hash & 0xFF),
            (byte)((hash >> 8) & 0xFF),
            (byte)((hash >> 16) & 0xFF),
            200);

        var gradientStart = AdjustBrightness(baseColor.WithAlpha(230), 1.1f);
        var gradientEnd = AdjustBrightness(baseColor.WithAlpha(200), 0.85f);
        var strokeColor = AdjustBrightness(baseColor.WithAlpha(230), 0.6f);
        var fillColor = baseColor.WithAlpha(140);

        return new RasterStyle(
            Background: new SKColor(33, 47, 61),
            GradientStart: gradientStart,
            GradientEnd: gradientEnd,
            LabelColor: SKColors.White,
            StrokeColor: strokeColor,
            FillColor: fillColor,
            StrokeWidth: 2f);
    }

    private static readonly IReadOnlyDictionary<string, RasterStyle> StyleLibrary = new Dictionary<string, RasterStyle>(StringComparer.OrdinalIgnoreCase)
    {
        ["natural-color"] = new RasterStyle(
            Background: new SKColor(30, 45, 55),
            GradientStart: new SKColor(90, 160, 110, 230),
            GradientEnd: new SKColor(40, 110, 170, 230),
            LabelColor: SKColors.White,
            StrokeColor: new SKColor(255, 255, 255, 220),
            FillColor: new SKColor(90, 160, 110, 130),
            StrokeWidth: 2.5f),
        ["infrared"] = new RasterStyle(
            Background: new SKColor(45, 22, 35),
            GradientStart: new SKColor(220, 85, 120, 230),
            GradientEnd: new SKColor(120, 40, 90, 230),
            LabelColor: SKColors.White,
            StrokeColor: new SKColor(255, 120, 160, 220),
            FillColor: new SKColor(220, 85, 120, 130),
            StrokeWidth: 2.5f),
        ["elevation"] = new RasterStyle(
            Background: new SKColor(24, 34, 22),
            GradientStart: new SKColor(74, 160, 64, 230),
            GradientEnd: new SKColor(214, 183, 110, 230),
            LabelColor: new SKColor(28, 28, 28),
            StrokeColor: new SKColor(255, 255, 255, 200),
            FillColor: new SKColor(214, 183, 110, 120),
            StrokeWidth: 2.0f)
    };

    private static void DrawVectorGeometries(SKCanvas canvas, RasterRenderRequest request, RasterStyle style)
    {
        var geometries = request.VectorGeometries ?? Array.Empty<Geometry>();
        if (geometries.Count == 0)
        {
            return;
        }

        if (request.BoundingBox.Length < 4)
        {
            return;
        }

        var minX = request.BoundingBox[0];
        var minY = request.BoundingBox[1];
        var maxX = request.BoundingBox[2];
        var maxY = request.BoundingBox[3];

        var spanX = Math.Max(maxX - minX, double.Epsilon);
        var spanY = Math.Max(maxY - minY, double.Epsilon);

        using var strokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = style.StrokeColor,
            StrokeWidth = style.StrokeWidth,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };

        using var fillPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = style.FillColor,
            IsAntialias = true
        };

        if (fillPaint.Color.Alpha == 0)
        {
            fillPaint.Color = style.StrokeColor.WithAlpha(60);
        }

        foreach (var geometry in geometries)
        {
            DrawGeometry(canvas, geometry, minX, minY, maxX, maxY, spanX, spanY, request.Width, request.Height, strokePaint, fillPaint);
        }
    }

    private static void DrawGeometry(
        SKCanvas canvas,
        Geometry geometry,
        double minX,
        double minY,
        double maxX,
        double maxY,
        double spanX,
        double spanY,
        int width,
        int height,
        SKPaint strokePaint,
        SKPaint fillPaint)
    {
        switch (geometry)
        {
            case Point point:
                DrawPoint(canvas, point.Coordinate, minX, minY, spanX, spanY, width, height, strokePaint, fillPaint);
                break;
            case MultiPoint multiPoint:
                foreach (Point innerPoint in multiPoint.Geometries)
                {
                    DrawPoint(canvas, innerPoint.Coordinate, minX, minY, spanX, spanY, width, height, strokePaint, fillPaint);
                }
                break;
            case LineString line:
                DrawLineString(canvas, line, minX, minY, spanX, spanY, width, height, strokePaint);
                break;
            case MultiLineString multiLine:
                foreach (LineString innerLine in multiLine.Geometries)
                {
                    DrawLineString(canvas, innerLine, minX, minY, spanX, spanY, width, height, strokePaint);
                }
                break;
            case Polygon polygon:
                DrawPolygon(canvas, polygon, minX, minY, spanX, spanY, width, height, strokePaint, fillPaint);
                break;
            case MultiPolygon multiPolygon:
                foreach (Polygon innerPolygon in multiPolygon.Geometries)
                {
                    DrawPolygon(canvas, innerPolygon, minX, minY, spanX, spanY, width, height, strokePaint, fillPaint);
                }
                break;
        }
    }

    private static void DrawPoint(
        SKCanvas canvas,
        Coordinate coordinate,
        double minX,
        double minY,
        double spanX,
        double spanY,
        int width,
        int height,
        SKPaint strokePaint,
        SKPaint fillPaint)
    {
        var pixel = ToPixel(coordinate, minX, minY, spanX, spanY, width, height);
        var radius = Math.Max(strokePaint.StrokeWidth * 1.5f, 3f);
        canvas.DrawCircle(pixel, radius, fillPaint);
        canvas.DrawCircle(pixel, radius, strokePaint);
    }

    private static void DrawLineString(
        SKCanvas canvas,
        LineString line,
        double minX,
        double minY,
        double spanX,
        double spanY,
        int width,
        int height,
        SKPaint strokePaint)
    {
        if (line.NumPoints < 2)
        {
            return;
        }

        using var path = new SKPath();
        for (var i = 0; i < line.NumPoints; i++)
        {
            var point = ToPixel(line.GetCoordinateN(i), minX, minY, spanX, spanY, width, height);
            if (i == 0)
            {
                path.MoveTo(point);
            }
            else
            {
                path.LineTo(point);
            }
        }

        canvas.DrawPath(path, strokePaint);
    }

    private static void DrawPolygon(
        SKCanvas canvas,
        Polygon polygon,
        double minX,
        double minY,
        double spanX,
        double spanY,
        int width,
        int height,
        SKPaint strokePaint,
        SKPaint fillPaint)
    {
        using var path = new SKPath();

        AddRing(path, polygon.ExteriorRing, minX, minY, spanX, spanY, width, height, close: true);

        for (var i = 0; i < polygon.NumInteriorRings; i++)
        {
            AddRing(path, polygon.GetInteriorRingN(i), minX, minY, spanX, spanY, width, height, close: true, inverse: true);
        }

        canvas.DrawPath(path, fillPaint);
        canvas.DrawPath(path, strokePaint);
    }

    private static void AddRing(
        SKPath path,
        LineString ring,
        double minX,
        double minY,
        double spanX,
        double spanY,
        int width,
        int height,
        bool close,
        bool inverse = false)
    {
        if (ring.NumPoints == 0)
        {
            return;
        }

        var points = new List<SKPoint>(ring.NumPoints);
        for (var i = 0; i < ring.NumPoints; i++)
        {
            points.Add(ToPixel(ring.GetCoordinateN(i), minX, minY, spanX, spanY, width, height));
        }

        if (inverse)
        {
            points.Reverse();
        }

        for (var i = 0; i < points.Count; i++)
        {
            if (i == 0)
            {
                path.MoveTo(points[i]);
            }
            else
            {
                path.LineTo(points[i]);
            }
        }

        if (close)
        {
            path.Close();
        }
    }

    private static SKPoint ToPixel(
        Coordinate coordinate,
        double minX,
        double minY,
        double spanX,
        double spanY,
        int width,
        int height)
    {
        var xRatio = (coordinate.X - minX) / spanX;
        var yRatio = (coordinate.Y - minY) / spanY;

        var x = (float)(xRatio * width);
        var y = (float)((1 - yRatio) * height);

        return new SKPoint(x, y);
    }

    private async Task<SKBitmap?> LoadDatasetBitmapAsync(RasterDatasetDefinition dataset, CancellationToken cancellationToken)
    {
        if (dataset?.Source?.Uri is null)
        {
            return null;
        }

        try
        {
            var uri = dataset.Source.Uri;

            // Try to extract GeoTIFF metadata for file:// URIs (cache it)
            if (Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri) && parsedUri.IsFile)
            {
                var path = parsedUri.LocalPath;
                if (File.Exists(path) && !_metadataCache.TryGetMetadata(uri, out _))
                {
                    var metadata = ExtractGeoTiffMetadata(path);
                    if (metadata is not null)
                    {
                        _metadataCache.SetMetadata(uri, metadata);
                    }
                }
            }

            // Use provider registry to open stream from any source
            await using var stream = await _sourceProviderRegistry.OpenReadAsync(uri, cancellationToken).ConfigureAwait(false);
            return SKBitmap.Decode(stream);
        }
        catch
        {
            // ignore and fall back
        }

        return null;
    }

    private static GeoRasterMetadata? ExtractGeoTiffMetadata(string path)
    {
        try
        {
            using var tiff = Tiff.Open(path, "r");
            if (tiff is null)
            {
                return null;
            }

            var widthField = tiff.GetField(TiffTag.IMAGEWIDTH);
            var heightField = tiff.GetField(TiffTag.IMAGELENGTH);
            if (widthField is null || heightField is null)
            {
                return null;
            }

            var width = widthField[0].ToInt();
            var height = heightField[0].ToInt();

            var samplesPerPixelField = tiff.GetField(TiffTag.SAMPLESPERPIXEL);
            var samplesPerPixel = samplesPerPixelField is null ? 1 : Math.Max(samplesPerPixelField[0].ToInt(), 1);
            var bandNames = new List<string>(samplesPerPixel);
            var bandDescriptions = new List<string?>(samplesPerPixel);
            for (var i = 0; i < samplesPerPixel; i++)
            {
                bandNames.Add($"Band {i + 1}");
                bandDescriptions.Add(null);
            }
            var bandNoData = new List<double?>(Enumerable.Repeat<double?>(null, samplesPerPixel));

            double? pixelSizeX = null;
            double? pixelSizeY = null;
            var scaleField = tiff.GetField((TiffTag)GeoPixelScaleTagValue);
            var scaleArray = scaleField is null ? null : scaleField[1].ToDoubleArray();
            if (scaleArray is { Length: > 0 })
            {
                pixelSizeX = scaleArray[0];
                if (scaleArray.Length > 1)
                {
                    pixelSizeY = scaleArray[1];
                }
            }

            double? originX = null;
            double? originY = null;
            var tiepointField = tiff.GetField((TiffTag)GeoTiePointsTagValue);
            var tiepoints = tiepointField is null ? null : tiepointField[1].ToDoubleArray();
            if (tiepoints is { Length: >= 6 })
            {
                originX = tiepoints[3];
                originY = tiepoints[4];
            }

            double? noData = null;
            var nodataField = tiff.GetField((TiffTag)GdalNoDataTagValue);
            if (nodataField is { Length: > 0 })
            {
                var nodataRaw = nodataField[0].ToString();
                if (double.TryParse(nodataRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var nodataValue))
                {
                    noData = nodataValue;
                }
            }

            if (noData.HasValue)
            {
                for (var i = 0; i < bandNoData.Count; i++)
                {
                    bandNoData[i] = noData;
                }
            }

            string? spatialReference = null;
            var geoKeyField = tiff.GetField((TiffTag)GeoKeyDirectoryTagValue);
            var directory = geoKeyField is null ? null : geoKeyField[1].ToShortArray();
            if (directory is { Length: >= 4 })
            {
                var keyCount = directory[3];
                for (var i = 0; i < keyCount; i++)
                {
                    var offset = 4 + i * 4;
                    if (offset + 3 >= directory.Length)
                    {
                        break;
                    }

                    var keyId = directory[offset];
                    var count = directory[offset + 2];
                    var valueOffset = directory[offset + 3];

                    if (count == 1 && valueOffset != 0 && (keyId == 3072 || keyId == 2048))
                    {
                        spatialReference = $"EPSG:{valueOffset}";
                        break;
                    }
                }
            }

            double[]? extent = null;
            if (pixelSizeX.HasValue && pixelSizeY.HasValue && originX.HasValue && originY.HasValue)
            {
                var minX = originX.Value;
                var maxY = originY.Value;
                var maxX = minX + pixelSizeX.Value * width;
                var minY = maxY - Math.Abs(pixelSizeY.Value) * height;
                extent = new[] { minX, minY, maxX, maxY };
            }

            var gdalMetadataField = tiff.GetField((TiffTag)42112);
            var gdalMetadataRaw = gdalMetadataField is { Length: > 0 } ? gdalMetadataField[0].ToString() : null;
            if (gdalMetadataRaw.HasValue())
            {
                try
                {
                    var doc = XDocument.Parse(gdalMetadataRaw);
                    foreach (var item in doc.Descendants("Item"))
                    {
                        if (item.Attribute("sample") is { } sampleAttr
                            && int.TryParse(sampleAttr.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sampleIndex)
                            && sampleIndex >= 0
                            && sampleIndex < bandNames.Count)
                        {
                            var nameAttr = item.Attribute("name")?.Value;
                            var value = item.Value;
                            if (nameAttr.HasValue() && nameAttr.StartsWith("Band_", StringComparison.OrdinalIgnoreCase) && value.HasValue())
                            {
                                bandNames[sampleIndex] = value;
                            }
                            else if (string.Equals(nameAttr, "DESCRIPTION", StringComparison.OrdinalIgnoreCase) && value.HasValue())
                            {
                                bandDescriptions[sampleIndex] = value;
                            }
                            else if (string.Equals(nameAttr, "NoDataValue", StringComparison.OrdinalIgnoreCase)
                                && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var bandNoDataValue))
                            {
                                bandNoData[sampleIndex] = bandNoDataValue;
                            }
                        }
                    }
                }
                catch
                {
                    // ignore metadata parsing failures
                }
            }

            return new GeoRasterMetadata
            {
                Width = width,
                Height = height,
                PixelSizeX = pixelSizeX,
                PixelSizeY = pixelSizeY,
                OriginX = originX,
                OriginY = originY,
                Extent = extent,
                NoDataValue = noData,
                SpatialReference = spatialReference,
                BandNames = bandNames.AsReadOnly(),
                BandDescriptions = bandDescriptions.AsReadOnly(),
                BandNoData = bandNoData.AsReadOnly()
            };
        }
        catch
        {
            return null;
        }
    }

    private static SKRectI CalculateSourceRectangle(double[]? bbox, double[]? datasetExtent, int bitmapWidth, int bitmapHeight)
    {
        if (bbox is null || datasetExtent is null || datasetExtent.Length < 4)
        {
            return new SKRectI(0, 0, bitmapWidth, bitmapHeight);
        }

        var dsMinX = datasetExtent[0];
        var dsMinY = datasetExtent[1];
        var dsMaxX = datasetExtent[2];
        var dsMaxY = datasetExtent[3];
        var rangeX = Math.Max(dsMaxX - dsMinX, double.Epsilon);
        var rangeY = Math.Max(dsMaxY - dsMinY, double.Epsilon);

        var bboxMinX = Math.Clamp(bbox[0], dsMinX, dsMaxX);
        var bboxMinY = Math.Clamp(bbox[1], dsMinY, dsMaxY);
        var bboxMaxX = Math.Clamp(bbox[2], dsMinX, dsMaxX);
        var bboxMaxY = Math.Clamp(bbox[3], dsMinY, dsMaxY);

        if (bboxMaxX <= bboxMinX || bboxMaxY <= bboxMinY)
        {
            return new SKRectI(0, 0, bitmapWidth, bitmapHeight);
        }

        var xFracMin = (bboxMinX - dsMinX) / rangeX;
        var xFracMax = (bboxMaxX - dsMinX) / rangeX;
        var yFracMin = (bboxMinY - dsMinY) / rangeY;
        var yFracMax = (bboxMaxY - dsMinY) / rangeY;

        var left = (int)Math.Floor(xFracMin * bitmapWidth);
        var right = (int)Math.Ceiling(xFracMax * bitmapWidth);
        var top = (int)Math.Floor((1 - yFracMax) * bitmapHeight);
        var bottom = (int)Math.Ceiling((1 - yFracMin) * bitmapHeight);

        left = Math.Clamp(left, 0, bitmapWidth - 1);
        right = Math.Clamp(right, left + 1, bitmapWidth);
        top = Math.Clamp(top, 0, bitmapHeight - 1);
        bottom = Math.Clamp(bottom, top + 1, bitmapHeight);

        return new SKRectI(left, top, right, bottom);
    }

    private double[]? GetDatasetExtent(RasterDatasetDefinition dataset)
    {
        var datasetExtent = dataset.Extent?.Bbox?.FirstOrDefault();
        if (datasetExtent is { Length: 4 })
        {
            return datasetExtent;
        }

        if (dataset.Source?.Uri.HasValue() == true
            && _metadataCache.TryGetMetadata(dataset.Source.Uri, out var metadata)
            && metadata?.Extent is { Length: 4 })
        {
            return metadata.Extent.ToArray();
        }

        return null;
    }

    private RasterRenderResult RenderFallback(RasterRenderRequest request, string normalizedFormat, string contentType)
    {
        var style = ResolveStyle(request.Dataset, request.StyleId, request.Style);

        var info = new SKImageInfo(request.Width, request.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;

        var background = normalizedFormat == "png" && request.Transparent
            ? SKColors.Transparent
            : style.Background;
        canvas.Clear(background);

        using (var paint = new SKPaint { IsAntialias = true })
        {
            paint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(request.Width, request.Height),
                new[] { style.GradientStart, style.GradientEnd },
                null,
                SKShaderTileMode.Clamp);
            canvas.DrawRect(new SKRect(0, 0, request.Width, request.Height), paint);
        }

        DrawVectorGeometries(canvas, request, style);

        using var image = surface.Snapshot();
        var encodedFormat = normalizedFormat == "jpeg"
            ? SKEncodedImageFormat.Jpeg
            : SKEncodedImageFormat.Png;

        using var data = image.Encode(encodedFormat, 90);
        var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Seek(0, SeekOrigin.Begin);

        return new RasterRenderResult(stream, contentType, request.Width, request.Height);
    }

    private readonly record struct RasterStyle(
        SKColor Background,
        SKColor GradientStart,
        SKColor GradientEnd,
        SKColor LabelColor,
        SKColor StrokeColor,
        SKColor FillColor,
        float StrokeWidth);
}
