// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Styling;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using SkiaSharp;

namespace Honua.Server.Host.Wms;

/// <summary>
/// Renders WMS legend graphics based on style definitions.
/// Implements WMS 1.3.0 GetLegendGraphic specification.
/// </summary>
internal static class WmsLegendRenderer
{
    private const int DefaultSymbolSize = 20;
    private const int DefaultMargin = 4;
    private const int DefaultTextHeight = 14;
    private const int DefaultTextPadding = 8;

    /// <summary>
    /// Generates a legend graphic for the specified dataset and style.
    /// </summary>
    /// <param name="dataset">The raster dataset.</param>
    /// <param name="style">The style definition (null for default style).</param>
    /// <param name="width">Requested width (optional, auto-sized if not specified).</param>
    /// <param name="height">Requested height (optional, auto-sized if not specified).</param>
    /// <returns>PNG image bytes representing the legend.</returns>
    public static byte[] GenerateLegend(
        RasterDatasetDefinition dataset,
        StyleDefinition? style,
        int? width = null,
        int? height = null)
    {
        // Clamp dimensions to reasonable values
        var requestedWidth = width.HasValue ? Math.Clamp(width.Value, 20, 500) : 0;
        var requestedHeight = height.HasValue ? Math.Clamp(height.Value, 20, 500) : 0;

        if (style is null || style.Rules.Count == 0)
        {
            // Generate a simple default legend
            return GenerateDefaultLegend(dataset, requestedWidth, requestedHeight);
        }

        // Check renderer type
        if (style.Renderer.EqualsIgnoreCase("unique") && style.UniqueValue is not null)
        {
            return GenerateUniqueValueLegend(dataset, style, style.UniqueValue, requestedWidth, requestedHeight);
        }

        if (style.Rules.Count > 0)
        {
            return GenerateRuleLegend(dataset, style, requestedWidth, requestedHeight);
        }

        return GenerateDefaultLegend(dataset, requestedWidth, requestedHeight);
    }

    private static byte[] GenerateDefaultLegend(RasterDatasetDefinition dataset, int requestedWidth, int requestedHeight)
    {
        var symbolSize = DefaultSymbolSize;
        var margin = DefaultMargin;

        var width = requestedWidth > 0 ? requestedWidth : symbolSize + margin * 2;
        var height = requestedHeight > 0 ? requestedHeight : symbolSize + margin * 2;

        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        using var fillPaint = new SKPaint
        {
            Color = new SKColor(0x40, 0x80, 0xC0),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        using var strokePaint = new SKPaint
        {
            Color = new SKColor(0x20, 0x40, 0x60),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true
        };

        var rect = new SKRect(margin, margin, width - margin, height - margin);
        canvas.DrawRect(rect, fillPaint);
        canvas.DrawRect(rect, strokePaint);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    private static byte[] GenerateUniqueValueLegend(
        RasterDatasetDefinition dataset,
        StyleDefinition style,
        UniqueValueStyleDefinition uniqueValue,
        int requestedWidth,
        int requestedHeight)
    {
        var symbolSize = DefaultSymbolSize;
        var margin = DefaultMargin;
        var textPadding = DefaultTextPadding;

        var itemCount = uniqueValue.Classes.Count;
        if (itemCount == 0)
        {
            return GenerateDefaultLegend(dataset, requestedWidth, requestedHeight);
        }

        // Calculate dimensions
        var itemHeight = symbolSize + margin;
        var contentHeight = itemCount * itemHeight + margin;
        var height = requestedHeight > 0 ? requestedHeight : contentHeight;
        var width = requestedWidth > 0 ? requestedWidth : 150; // Default width for labels

        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        using var textPaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 11,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal)
        };

        var yOffset = margin;
        foreach (var classItem in uniqueValue.Classes)
        {
            var symbol = classItem.Symbol;
            DrawSymbol(canvas, margin, yOffset, symbolSize, symbol);

            // Draw label
            var label = classItem.Value ?? "Unknown";
            var textX = margin + symbolSize + textPadding;
            var textY = yOffset + symbolSize / 2 + 4; // Vertically center
            canvas.DrawText(label, textX, textY, textPaint);

            yOffset += itemHeight;
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    private static byte[] GenerateRuleLegend(
        RasterDatasetDefinition dataset,
        StyleDefinition style,
        int requestedWidth,
        int requestedHeight)
    {
        var symbolSize = DefaultSymbolSize;
        var margin = DefaultMargin;
        var textPadding = DefaultTextPadding;

        var visibleRules = style.Rules.Where(r => r.Label.HasValue()).ToList();
        var itemCount = Math.Max(1, visibleRules.Count);

        // Calculate dimensions
        var itemHeight = symbolSize + margin;
        var contentHeight = itemCount * itemHeight + margin;
        var height = requestedHeight > 0 ? requestedHeight : contentHeight;
        var width = requestedWidth > 0 ? requestedWidth : 150;

        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        using var textPaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 11,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal)
        };

        if (visibleRules.Count == 0)
        {
            // No labeled rules, draw default symbol
            var defaultSymbol = style.Simple ?? new SimpleStyleDefinition();
            DrawSymbol(canvas, margin, margin, symbolSize, defaultSymbol);
        }
        else
        {
            var yOffset = margin;
            foreach (var rule in visibleRules)
            {
                var symbol = rule.Symbolizer;
                DrawSymbol(canvas, margin, yOffset, symbolSize, symbol);

                // Draw label
                var label = rule.Label ?? "Unknown";
                var textX = margin + symbolSize + textPadding;
                var textY = yOffset + symbolSize / 2 + 4;
                canvas.DrawText(label, textX, textY, textPaint);

                yOffset += itemHeight;
            }
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    private static void DrawSymbol(SKCanvas canvas, float x, float y, int size, SimpleStyleDefinition symbol)
    {
        var fillColor = ParseColor(symbol.FillColor, new SKColor(0x40, 0x80, 0xC0));
        var strokeColor = ParseColor(symbol.StrokeColor, new SKColor(0x20, 0x40, 0x60));
        var strokeWidth = (float)(symbol.StrokeWidth ?? 2.0);

        using var fillPaint = new SKPaint
        {
            Color = fillColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        using var strokePaint = new SKPaint
        {
            Color = strokeColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = strokeWidth,
            IsAntialias = true
        };

        var rect = new SKRect(x, y, x + size, y + size);

        // Draw based on symbol type
        var symbolType = symbol.SymbolType ?? "shape";
        if (symbolType.EqualsIgnoreCase("circle") || symbolType.EqualsIgnoreCase("point"))
        {
            var centerX = x + size / 2f;
            var centerY = y + size / 2f;
            var radius = size / 2f - strokeWidth;
            canvas.DrawCircle(centerX, centerY, radius, fillPaint);
            canvas.DrawCircle(centerX, centerY, radius, strokePaint);
        }
        else if (symbolType.EqualsIgnoreCase("line"))
        {
            using var linePaint = new SKPaint
            {
                Color = strokeColor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = strokeWidth,
                IsAntialias = true
            };
            canvas.DrawLine(x, y + size / 2f, x + size, y + size / 2f, linePaint);
        }
        else
        {
            // Default: rectangle/polygon
            canvas.DrawRect(rect, fillPaint);
            canvas.DrawRect(rect, strokePaint);
        }
    }

    private static SKColor ParseColor(string? colorStr, SKColor defaultColor)
    {
        if (colorStr.IsNullOrWhiteSpace())
        {
            return defaultColor;
        }

        // Handle hex colors (#RGB, #RRGGBB, #RRGGBBAA)
        if (colorStr.StartsWith("#"))
        {
            if (SKColor.TryParse(colorStr, out var color))
            {
                return color;
            }
        }

        // Handle rgb(r,g,b) or rgba(r,g,b,a)
        if (colorStr.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var start = colorStr.IndexOf('(');
            var end = colorStr.IndexOf(')');
            if (start >= 0 && end > start)
            {
                var values = colorStr.Substring(start + 1, end - start - 1).Split(',');
                if (values.Length >= 3)
                {
                    if (byte.TryParse(values[0].Trim(), out var r) &&
                        byte.TryParse(values[1].Trim(), out var g) &&
                        byte.TryParse(values[2].Trim(), out var b))
                    {
                        byte a = 255;
                        if (values.Length >= 4 && byte.TryParse(values[3].Trim(), out var alpha))
                        {
                            a = alpha;
                        }
                        return new SKColor(r, g, b, a);
                    }
                }
            }
        }

        return defaultColor;
    }
}
