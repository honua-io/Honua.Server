// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Utilities;
using SkiaSharp;

namespace Honua.Server.Host.GeoservicesREST;

internal static class GeoservicesRESTLegendBuilder
{
    private const int IconWidth = 32;
    private const int IconHeight = 18;

    public static GeoservicesRESTLegendResponse BuildLegend(
        CatalogServiceView serviceView,
        MetadataSnapshot snapshot,
        double currentVersion)
    {
        Guard.NotNull(serviceView);
        Guard.NotNull(snapshot);

        var styleLookup = snapshot.Styles.ToDictionary(style => style.Id, style => style, StringComparer.OrdinalIgnoreCase);

        var layers = new List<GeoservicesRESTLegendLayer>(serviceView.Layers.Count);
        for (var index = 0; index < serviceView.Layers.Count; index++)
        {
            var layerView = serviceView.Layers[index];
            var entries = BuildLegendEntries(layerView.Layer, styleLookup);
            layers.Add(new GeoservicesRESTLegendLayer
            {
                LayerId = index,
                LayerName = layerView.Layer.Title,
                Legend = entries
            });
        }

        return new GeoservicesRESTLegendResponse
        {
            CurrentVersion = currentVersion,
            Layers = new ReadOnlyCollection<GeoservicesRESTLegendLayer>(layers)
        };
    }

    public static GeoservicesRESTLegendResponse BuildRasterLegend(
        CatalogServiceView serviceView,
        IReadOnlyList<RasterDatasetDefinition> datasets,
        MetadataSnapshot snapshot,
        double currentVersion)
    {
        Guard.NotNull(serviceView);
        Guard.NotNull(datasets);
        Guard.NotNull(snapshot);

        var styleLookup = snapshot.Styles.ToDictionary(style => style.Id, style => style, StringComparer.OrdinalIgnoreCase);
        var layers = new List<GeoservicesRESTLegendLayer>(datasets.Count);
        for (var index = 0; index < datasets.Count; index++)
        {
            var dataset = datasets[index];
            if (dataset is null)
            {
                continue;
            }

            var entries = BuildRasterLegendEntries(dataset, styleLookup);
            layers.Add(new GeoservicesRESTLegendLayer
            {
                LayerId = index,
                LayerName = dataset.Title,
                Legend = entries
            });
        }

        return new GeoservicesRESTLegendResponse
        {
            CurrentVersion = currentVersion,
            Layers = new ReadOnlyCollection<GeoservicesRESTLegendLayer>(layers)
        };
    }

    private static IReadOnlyList<GeoservicesRESTLegendEntry> BuildLegendEntries(
        LayerDefinition layer,
        IReadOnlyDictionary<string, StyleDefinition> styles)
    {
        var rendererEntries = new List<GeoservicesRESTLegendEntry>();

        if (!string.IsNullOrWhiteSpace(layer.DefaultStyleId) &&
            styles.TryGetValue(layer.DefaultStyleId, out var defaultStyle))
        {
            rendererEntries.AddRange(CreateEntriesForStyle(defaultStyle, layer.DisplayField ?? layer.Id, layer.GeometryType));
        }
        else if (layer.StyleIds.Count > 0)
        {
            foreach (var styleId in layer.StyleIds)
            {
                if (styles.TryGetValue(styleId, out var style))
                {
                    rendererEntries.AddRange(CreateEntriesForStyle(style, style.Title ?? style.Id, layer.GeometryType));
                }
            }
        }

        if (rendererEntries.Count == 0)
        {
            rendererEntries.Add(CreateFallbackEntry(layer.GeometryType));
        }

        return new ReadOnlyCollection<GeoservicesRESTLegendEntry>(rendererEntries);
    }

    private static IReadOnlyList<GeoservicesRESTLegendEntry> BuildRasterLegendEntries(
        RasterDatasetDefinition dataset,
        IReadOnlyDictionary<string, StyleDefinition> styles)
    {
        var entries = new List<GeoservicesRESTLegendEntry>();

        if (!string.IsNullOrWhiteSpace(dataset.Styles.DefaultStyleId) &&
            styles.TryGetValue(dataset.Styles.DefaultStyleId, out var defaultStyle))
        {
            entries.AddRange(CreateEntriesForStyle(defaultStyle, dataset.Title, "raster"));
        }
        else if (dataset.Styles.StyleIds.Count > 0)
        {
            foreach (var styleId in dataset.Styles.StyleIds)
            {
                if (styles.TryGetValue(styleId, out var style))
                {
                    var label = styleId.EqualsIgnoreCase(dataset.Styles.DefaultStyleId)
                        ? dataset.Title
                        : style.Title ?? style.Id;
                    entries.AddRange(CreateEntriesForStyle(style, label, "raster"));
                }
            }
        }

        if (entries.Count == 0)
        {
            entries.Add(CreateFallbackEntry("raster", dataset.Title));
        }

        return new ReadOnlyCollection<GeoservicesRESTLegendEntry>(entries);
    }

    private static IEnumerable<GeoservicesRESTLegendEntry> CreateEntriesForStyle(StyleDefinition style, string? label, string geometryType)
    {
        if (style.Renderer.EqualsIgnoreCase("uniqueValue") && style.UniqueValue is not null)
        {
            var entries = new List<GeoservicesRESTLegendEntry>();
            foreach (var uniqueClass in style.UniqueValue.Classes)
            {
                entries.Add(RenderSimpleSymbol(uniqueClass.Symbol, uniqueClass.Value, geometryType));
            }

            if (style.UniqueValue.DefaultSymbol is not null)
            {
                entries.Insert(0, RenderSimpleSymbol(style.UniqueValue.DefaultSymbol, label ?? style.Title ?? style.Id, geometryType));
            }

            return entries;
        }

        if (style.Simple is not null)
        {
            return new[] { RenderSimpleSymbol(style.Simple, label ?? style.Title ?? style.Id, geometryType) };
        }

        return new[] { CreateFallbackEntry(geometryType, label ?? style.Title ?? style.Id) };
    }

    private static GeoservicesRESTLegendEntry RenderSimpleSymbol(SimpleStyleDefinition symbol, string? label, string geometryType)
    {
        var imageData = RenderSymbolImage(symbol, geometryType);
        return new GeoservicesRESTLegendEntry
        {
            Label = label ?? symbol.Label ?? string.Empty,
            Description = symbol.Description ?? string.Empty,
            ContentType = "image/png",
            ImageData = imageData,
            Height = IconHeight,
            Width = IconWidth
        };
    }

    private static GeoservicesRESTLegendEntry CreateFallbackEntry(string geometryType, string? label = null)
    {
        var simple = new SimpleStyleDefinition
        {
            SymbolType = geometryType.EqualsIgnoreCase("polyline") ? "line" : geometryType.EqualsIgnoreCase("point") ? "point" : "polygon",
            FillColor = "#4A90E2FF",
            StrokeColor = "#1F364DFF",
            StrokeWidth = 2
        };

        return RenderSimpleSymbol(simple, label ?? "Symbol", geometryType);
    }

    private static string RenderSymbolImage(SimpleStyleDefinition symbol, string geometryType)
    {
        using var surface = SKSurface.Create(new SKImageInfo(IconWidth, IconHeight, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var fill = ParseColor(symbol.FillColor) ?? new SKColor(0x4A, 0x90, 0xE2, 0xFF);
        var stroke = ParseColor(symbol.StrokeColor) ?? new SKColor(0x1F, 0x36, 0x4D, 0xFF);
        var strokeWidth = (float)(symbol.StrokeWidth ?? 2.0);

        switch ((symbol.SymbolType ?? geometryType).ToLowerInvariant())
        {
            case "point":
            case "shape":
                DrawPoint(canvas, fill, stroke, strokeWidth);
                break;
            case "line":
            case "polyline":
                DrawLine(canvas, stroke, strokeWidth);
                break;
            default:
                DrawPolygon(canvas, fill, stroke, strokeWidth);
                break;
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return Convert.ToBase64String(data.ToArray());
    }

    private static void DrawPoint(SKCanvas canvas, SKColor fill, SKColor stroke, float strokeWidth)
    {
        using var fillPaint = new SKPaint { Color = fill, IsAntialias = true };
        using var strokePaint = new SKPaint { Color = stroke, Style = SKPaintStyle.Stroke, StrokeWidth = strokeWidth, IsAntialias = true };
        var radius = Math.Min(IconWidth, IconHeight) / 3f;
        var center = new SKPoint(IconWidth / 2f, IconHeight / 2f);
        canvas.DrawCircle(center, radius, fillPaint);
        canvas.DrawCircle(center, radius, strokePaint);
    }

    private static void DrawLine(SKCanvas canvas, SKColor stroke, float strokeWidth)
    {
        using var paint = new SKPaint { Color = stroke, Style = SKPaintStyle.Stroke, StrokeWidth = strokeWidth, IsAntialias = true };
        var start = new SKPoint(4, IconHeight - 4);
        var end = new SKPoint(IconWidth - 4, 4);
        canvas.DrawLine(start, end, paint);
    }

    private static void DrawPolygon(SKCanvas canvas, SKColor fill, SKColor stroke, float strokeWidth)
    {
        using var fillPaint = new SKPaint { Color = fill, Style = SKPaintStyle.Fill, IsAntialias = true };
        using var strokePaint = new SKPaint { Color = stroke, Style = SKPaintStyle.Stroke, StrokeWidth = strokeWidth, IsAntialias = true };
        var rect = new SKRect(4, 4, IconWidth - 4, IconHeight - 4);
        canvas.DrawRect(rect, fillPaint);
        canvas.DrawRect(rect, strokePaint);
    }

    private static SKColor? ParseColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var hex = value.Trim();
        if (hex.StartsWith("#", StringComparison.Ordinal))
        {
            hex = hex[1..];
        }

        try
        {
            return hex.Length switch
            {
                8 => new SKColor(
                    byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)),
                6 => new SKColor(
                    byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    255),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }
}
