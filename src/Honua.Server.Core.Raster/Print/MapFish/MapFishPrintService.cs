// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Print.MapFish;
using Honua.Server.Core.Raster.Rendering;
using Honua.Server.Core.Styling;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Raster.Print.MapFish;

public interface IMapFishPrintService
{
    Task<MapFishPrintResult> CreateReportAsync(string appId, MapFishPrintSpec spec, CancellationToken cancellationToken = default);
}

public sealed class MapFishPrintService : IMapFishPrintService
{
    private readonly IMapFishPrintApplicationStore _applicationStore;
    private readonly IMetadataRegistry _metadataRegistry;
    private readonly IRasterDatasetRegistry _datasetRegistry;
    private readonly IRasterRenderer _rasterRenderer;
    private readonly ILogger<MapFishPrintService> _logger;

    public MapFishPrintService(
        IMapFishPrintApplicationStore applicationStore,
        IMetadataRegistry metadataRegistry,
        IRasterDatasetRegistry datasetRegistry,
        IRasterRenderer rasterRenderer,
        ILogger<MapFishPrintService> logger)
    {
        _applicationStore = applicationStore ?? throw new ArgumentNullException(nameof(applicationStore));
        _metadataRegistry = metadataRegistry ?? throw new ArgumentNullException(nameof(metadataRegistry));
        _datasetRegistry = datasetRegistry ?? throw new ArgumentNullException(nameof(datasetRegistry));
        _rasterRenderer = rasterRenderer ?? throw new ArgumentNullException(nameof(rasterRenderer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MapFishPrintResult> CreateReportAsync(string appId, MapFishPrintSpec spec, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentNullException.ThrowIfNull(spec);

        await _metadataRegistry.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var application = await _applicationStore.FindAsync(appId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Print application '{appId}' was not found.");

        var layout = ResolveLayout(application, spec.Layout);
        var outputFormat = ResolveOutputFormat(application, spec.OutputFormat);
        if (!string.Equals(outputFormat, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Output format '{outputFormat}' is not supported. Only PDF is currently available.");
        }

        var attributes = spec.Attributes ?? new MapFishPrintSpecAttributes();
        var map = attributes.Map
                  ?? throw new InvalidOperationException("Print specification is missing required 'attributes.map' section.");

        var mapAttributeDefinition = application.Attributes.TryGetValue("map", out var mapAttribute)
            ? mapAttribute
            : null;

        var dpi = ResolveDpi(application, map.Dpi ?? mapAttributeDefinition?.ClientInfo?.DpiSuggestions?.FirstOrDefault());

        var projectionCandidate = map.Projection.HasValue()
            ? map.Projection
            : mapAttributeDefinition?.ClientInfo?.Projection;
        var targetCrs = NormalizeCrs(projectionCandidate);

        var bbox = ResolveBoundingBox(map, layout.Map, dpi)
                   ?? throw new InvalidOperationException("Unable to determine map bounding box. Provide either 'bbox' or 'center' with 'scale'.");

        var snapshot = await _metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var layerContexts = await ResolveLayerContextsAsync(map, snapshot, cancellationToken).ConfigureAwait(false);
        var render = await RenderMapAsync(layerContexts, bbox, layout.Map, targetCrs, dpi, cancellationToken).ConfigureAwait(false);

        var scale = ComputeScale(map, bbox, layout.Map, dpi);
        var fileName = BuildFileName(application.Id, layout.Name, outputFormat);
        var pdfStream = ComposePdf(application, layout, attributes, targetCrs, bbox, render.ImageBytes, render.PixelWidth, render.PixelHeight, dpi, scale);

        return new MapFishPrintResult(pdfStream, "application/pdf", fileName, scale, targetCrs);
    }

    private static string BuildFileName(string appId, string layoutName, string format)
    {
        var sanitizedApp = SanitizeFileComponent(appId);
        var sanitizedLayout = SanitizeFileComponent(layoutName);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        return $"{sanitizedApp}-{sanitizedLayout}-{timestamp}.{format.ToLowerInvariant()}";
    }

    private static string SanitizeFileComponent(string? value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return "print";
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch) || ch is '-' or '_' || ch == '.')
            {
                builder.Append(ch);
            }
            else if (char.IsWhiteSpace(ch))
            {
                builder.Append('-');
            }
        }

        if (builder.Length == 0)
        {
            return "print";
        }

        return builder.ToString();
    }

    private MapFishPrintLayoutDefinition ResolveLayout(MapFishPrintApplicationDefinition application, string? requestedLayout)
    {
        if (requestedLayout.HasValue())
        {
            var match = application.Layouts.FirstOrDefault(layout => string.Equals(layout.Name, requestedLayout, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }

            _logger.LogWarning("Requested print layout '{Layout}' was not found. Falling back to application default '{DefaultLayout}'.", requestedLayout, application.DefaultLayout);
        }

        var defaultLayout = application.Layouts.FirstOrDefault(layout => string.Equals(layout.Name, application.DefaultLayout, StringComparison.OrdinalIgnoreCase))
                            ?? application.Layouts.FirstOrDefault(layout => layout.Default)
                            ?? application.Layouts.First();

        return defaultLayout;
    }

    private static string ResolveOutputFormat(MapFishPrintApplicationDefinition application, string? requestedFormat)
    {
        if (requestedFormat.HasValue() &&
            application.OutputFormats.Any(format => string.Equals(format, requestedFormat, StringComparison.OrdinalIgnoreCase)))
        {
            return requestedFormat;
        }

        return application.DefaultOutputFormat;
    }

    private static int ResolveDpi(MapFishPrintApplicationDefinition application, int? requestedDpi)
    {
        if (requestedDpi is > 0 && application.Dpis.Contains(requestedDpi.Value))
        {
            return requestedDpi.Value;
        }

        if (requestedDpi is > 0 && !application.Dpis.Contains(requestedDpi.Value))
        {
            // Allow custom DPI values but clamp to reasonable range
            return Math.Clamp(requestedDpi.Value, 72, 600);
        }

        return application.DefaultDpi > 0 ? application.DefaultDpi : 150;
    }

    private static string NormalizeCrs(string? value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return "EPSG:3857";
        }

        return value.Trim().ToUpperInvariant();
    }

    private static double[]? ResolveBoundingBox(MapFishPrintMapSpec map, MapFishPrintLayoutMapDefinition layout, int dpi)
    {
        if (map.BoundingBox is { Length: >= 4 })
        {
            return new[] { map.BoundingBox[0], map.BoundingBox[1], map.BoundingBox[2], map.BoundingBox[3] };
        }

        if (map.Center is not { Length: >= 2 } || map.Scale is null)
        {
            return null;
        }

        var centerX = map.Center[0];
        var centerY = map.Center[1];
        var scale = map.Scale.Value;

        var widthMeters = layout.WidthPixels / (double)dpi * 0.0254 * scale;
        var heightMeters = layout.HeightPixels / (double)dpi * 0.0254 * scale;

        var halfWidth = widthMeters / 2.0;
        var halfHeight = heightMeters / 2.0;

        return new[]
        {
            centerX - halfWidth,
            centerY - halfHeight,
            centerX + halfWidth,
            centerY + halfHeight
        };
    }

    private async Task<IReadOnlyList<LayerRenderContext>> ResolveLayerContextsAsync(MapFishPrintMapSpec map, MetadataSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (map.Layers.Count == 0)
        {
            throw new InvalidOperationException("Print specification must include at least one layer.");
        }

        var contexts = new List<LayerRenderContext>();
        foreach (var layerSpec in map.Layers)
        {
            if (!string.Equals(layerSpec.Type, "wms", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException($"Layer type '{layerSpec.Type}' is not supported. Only WMS layers are currently supported.");
            }

            if (layerSpec.Layers.Count == 0)
            {
                continue;
            }

            var styles = layerSpec.Styles ?? new List<string>();
            for (var i = 0; i < layerSpec.Layers.Count; i++)
            {
                var layerName = layerSpec.Layers[i];
                var dataset = await ResolveDatasetAsync(layerName, cancellationToken).ConfigureAwait(false);
                if (dataset is null)
                {
                    throw new InvalidOperationException($"Layer '{layerName}' referenced by print specification was not found.");
                }

                string? requestedStyle = i < styles.Count ? styles[i] : null;
                var (success, styleId, error) = StyleResolutionHelper.TryResolveRasterStyleId(dataset, requestedStyle);
                if (!success)
                {
                    throw new InvalidOperationException(error ?? $"Style '{requestedStyle}' is not available for dataset '{dataset.Id}'.");
                }

                var styleDefinition = StyleResolutionHelper.ResolveStyleForRaster(snapshot, dataset, styleId);
                contexts.Add(new LayerRenderContext(dataset, styleId, styleDefinition));
            }
        }

        if (contexts.Count == 0)
        {
            throw new InvalidOperationException("Print specification does not include any renderable layers.");
        }

        return contexts;
    }

    private async Task<MapRenderResult> RenderMapAsync(
        IReadOnlyList<LayerRenderContext> contexts,
        double[] bbox,
        MapFishPrintLayoutMapDefinition layout,
        string targetCrs,
        int dpi,
        CancellationToken cancellationToken)
    {
        var primary = contexts[0];
        var overlays = contexts.Count > 1
            ? contexts.Skip(1)
                .Select(context => new RasterLayerRequest(context.Dataset, context.StyleId, context.StyleDefinition))
                .ToArray()
            : Array.Empty<RasterLayerRequest>();

        var sourceCrs = NormalizeCrs(primary.Dataset.Crs.FirstOrDefault());
        var request = new RasterRenderRequest(
            primary.Dataset,
            bbox,
            layout.WidthPixels,
            layout.HeightPixels,
            sourceCrs,
            targetCrs,
            Format: "png",
            Transparent: true,
            StyleId: primary.StyleId,
            Style: primary.StyleDefinition,
            AdditionalLayers: overlays.Length > 0 ? overlays : null);

        var renderResult = await _rasterRenderer.RenderAsync(request, cancellationToken).ConfigureAwait(false);
        await using var content = renderResult.Content;
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        var bytes = buffer.ToArray();

        return new MapRenderResult(bytes, layout.WidthPixels, layout.HeightPixels);
    }

    private async ValueTask<RasterDatasetDefinition?> ResolveDatasetAsync(string layerName, CancellationToken cancellationToken)
    {
        if (!layerName.Contains(':', StringComparison.Ordinal))
        {
            return await _datasetRegistry.FindAsync(layerName, cancellationToken);
        }

        var parts = layerName.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return await _datasetRegistry.FindAsync(layerName, cancellationToken);
        }

        var dataset = await _datasetRegistry.FindAsync(parts[1], cancellationToken).ConfigureAwait(false);
        if (dataset is null)
        {
            return null;
        }

        if (dataset.ServiceId.IsNullOrWhiteSpace() || string.Equals(dataset.ServiceId, parts[0], StringComparison.OrdinalIgnoreCase))
        {
            return dataset;
        }

        return null;
    }

    private static double? ComputeScale(MapFishPrintMapSpec map, double[] bbox, MapFishPrintLayoutMapDefinition layout, int dpi)
    {
        if (map.Scale is > 0)
        {
            return map.Scale.Value;
        }

        var widthUnits = Math.Abs(bbox[2] - bbox[0]);
        if (widthUnits <= 0d)
        {
            return null;
        }

        var physicalWidthMeters = layout.WidthPixels / (double)dpi * 0.0254;
        if (physicalWidthMeters <= 0d)
        {
            return null;
        }

        var scale = widthUnits / physicalWidthMeters;
        return double.IsFinite(scale) && scale > 0 ? scale : null;
    }

    private MemoryStream ComposePdf(
        MapFishPrintApplicationDefinition application,
        MapFishPrintLayoutDefinition layout,
        MapFishPrintSpecAttributes attributes,
        string targetCrs,
        IReadOnlyList<double> bbox,
        byte[] imageBytes,
        int imageWidth,
        int imageHeight,
        int dpi,
        double? scale)
    {
        var stream = new MemoryStream();
        using var document = SKDocument.CreatePdf(stream);

        var pageWidth = layout.Page.WidthPoints;
        var pageHeight = layout.Page.HeightPoints;
        using (var canvas = document.BeginPage(pageWidth, pageHeight))
        {
            canvas.Clear(SKColors.White);

            DrawTitle(canvas, layout.Title, attributes);
            DrawMap(canvas, layout.Map, imageBytes, imageWidth, imageHeight, dpi);
            DrawScale(canvas, layout.Scale, scale, targetCrs);
            DrawNotes(canvas, layout, attributes, dpi);
            DrawLegend(canvas, layout.Legend, attributes);
            DrawMetadata(canvas, layout, attributes, bbox, dpi, targetCrs);
            document.EndPage();
        }

        document.Close();
        stream.Position = 0;
        return stream;
    }

    private static void DrawTitle(SKCanvas canvas, MapFishPrintLayoutTitleDefinition titleLayout, MapFishPrintSpecAttributes attributes)
    {
        if (titleLayout is null)
        {
            return;
        }

#pragma warning disable CS0618
        using var titlePaint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold),
            TextSize = titleLayout.TitleFontSize
        };

        using var subtitlePaint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal),
            TextSize = titleLayout.SubtitleFontSize
        };

        var x = titleLayout.OffsetX;
        var y = titleLayout.OffsetY;

        if (attributes.Title.HasValue())
        {
            canvas.DrawText(attributes.Title, x, y, titlePaint);
            y += titleLayout.TitleFontSize + titleLayout.Spacing;
        }

        if (attributes.Subtitle.HasValue())
        {
            canvas.DrawText(attributes.Subtitle, x, y, subtitlePaint);
        }
#pragma warning restore CS0618
    }

    private static void DrawMap(SKCanvas canvas, MapFishPrintLayoutMapDefinition mapLayout, byte[] imageBytes, int imageWidth, int imageHeight, int dpi)
    {
        using var imageData = SKData.CreateCopy(imageBytes);
        using var image = SKImage.FromEncodedData(imageData);
        if (image is null)
        {
            return;
        }

        var widthPoints = mapLayout.WidthPixels / (float)dpi * 72f;
        var heightPoints = mapLayout.HeightPixels / (float)dpi * 72f;

        var destination = new SKRect(
            mapLayout.OffsetX,
            mapLayout.OffsetY,
            mapLayout.OffsetX + widthPoints,
            mapLayout.OffsetY + heightPoints);

        canvas.DrawImage(image, destination);
        using var borderPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColors.Black,
            StrokeWidth = 1f,
            IsAntialias = true
        };
        canvas.DrawRect(destination, borderPaint);
    }

    private static void DrawScale(SKCanvas canvas, MapFishPrintLayoutScaleDefinition scaleLayout, double? scale, string targetCrs)
    {
        if (scaleLayout is null)
        {
            return;
        }

        var scaleText = scale.HasValue
            ? $"Scale 1:{Math.Round(scale.Value):N0}"
            : "Scale unavailable";

#pragma warning disable CS0618
        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Italic),
            TextSize = scaleLayout.FontSize
        };

        canvas.DrawText(scaleText, scaleLayout.OffsetX, scaleLayout.OffsetY, paint);

        using var crsPaint = new SKPaint
        {
            Color = SKColors.Gray,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial"),
            TextSize = scaleLayout.FontSize - 1f
        };

        canvas.DrawText(targetCrs, scaleLayout.OffsetX, scaleLayout.OffsetY + scaleLayout.FontSize + 2f, crsPaint);
#pragma warning restore CS0618
    }

    private static void DrawNotes(SKCanvas canvas, MapFishPrintLayoutDefinition layout, MapFishPrintSpecAttributes attributes, int dpi)
    {
        if (attributes.Notes.IsNullOrWhiteSpace())
        {
            return;
        }

        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true
        };

        var x = layout.Map.OffsetX;
        var mapHeightPoints = layout.Map.HeightPixels / (float)dpi * 72f;
        var y = layout.Map.OffsetY + mapHeightPoints + 30f;
        var maxWidth = layout.Page.WidthPoints - 2 * layout.Page.MarginPoints;

        DrawWrappedText(canvas, attributes.Notes, x, y, maxWidth, paint, 14f);
    }

    private static void DrawLegend(SKCanvas canvas, MapFishPrintLayoutLegendDefinition legendLayout, MapFishPrintSpecAttributes attributes)
    {
        if (legendLayout is null || !legendLayout.Enabled)
        {
            return;
        }

        var legend = attributes.Legend;
        if (legend is null || legend.Items.Count == 0)
        {
            return;
        }

#pragma warning disable CS0618
        using var titlePaint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold),
            TextSize = 12f
        };

        using var itemPaint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial"),
            TextSize = 10f
        };

        var x = legendLayout.OffsetX;
        var y = legendLayout.OffsetY;

        canvas.DrawText("Legend", x, y, titlePaint);
        y += legendLayout.ItemHeight;

        foreach (var item in legend.Items)
        {
            if (item.Name.HasValue())
            {
                canvas.DrawText(item.Name, x, y, itemPaint);
                y += legendLayout.ItemHeight;
            }

            foreach (var legendClass in item.Classes)
            {
                if (legendClass.Name.HasValue())
                {
                    canvas.DrawText($" • {legendClass.Name}", x + legendLayout.SymbolSize, y, itemPaint);
                    y += legendLayout.ItemHeight;
                }
            }
        }
#pragma warning restore CS0618
    }

    private static void DrawMetadata(SKCanvas canvas, MapFishPrintLayoutDefinition layout, MapFishPrintSpecAttributes attributes, IReadOnlyList<double> bbox, int dpi, string targetCrs)
    {
#pragma warning disable CS0618
        using var paint = new SKPaint
        {
            Color = SKColors.Gray,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial"),
            TextSize = 9f
        };
#pragma warning restore CS0618

        var lines = new List<string>
        {
            $"CRS: {targetCrs}",
            $"DPI: {dpi}"
        };

        if (bbox.Count >= 4)
        {
            lines.Add($"BBOX: {bbox[0]:0.###}, {bbox[1]:0.###}, {bbox[2]:0.###}, {bbox[3]:0.###}");
        }

        foreach (var (key, value) in attributes.Metadata)
        {
            if (key.IsNullOrWhiteSpace())
            {
                continue;
            }

            lines.Add(value.IsNullOrWhiteSpace() ? key : $"{key}: {value}");
        }

        var x = layout.Page.MarginPoints;
        var y = layout.Page.HeightPoints - layout.Page.MarginPoints - (lines.Count * 12f);

#pragma warning disable CS0618
        foreach (var line in lines)
        {
            canvas.DrawText(line, x, y, paint);
            y += 12f;
        }
#pragma warning restore CS0618
    }

    private static void DrawWrappedText(SKCanvas canvas, string text, float x, float y, float maxWidth, SKPaint paint, float lineHeight)
    {
#pragma warning disable CS0618
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return;
        }

        var currentLine = new StringBuilder();
        foreach (var word in words)
        {
            var candidate = currentLine.Length == 0 ? word : currentLine + " " + word;
            var candidateWidth = paint.MeasureText(candidate);
            if (candidateWidth > maxWidth && currentLine.Length > 0)
            {
                canvas.DrawText(currentLine.ToString(), x, y, paint);
                y += lineHeight;
                currentLine.Clear();
                currentLine.Append(word);
            }
            else
            {
                if (currentLine.Length > 0)
                {
                    currentLine.Append(' ');
                }
                currentLine.Append(word);
            }
        }

        if (currentLine.Length > 0)
        {
            canvas.DrawText(currentLine.ToString(), x, y, paint);
        }
#pragma warning restore CS0618
    }

    private readonly record struct LayerRenderContext(RasterDatasetDefinition Dataset, string? StyleId, StyleDefinition? StyleDefinition);

    private readonly record struct MapRenderResult(byte[] ImageBytes, int PixelWidth, int PixelHeight);
}

public sealed record MapFishPrintResult(Stream Content, string ContentType, string FileName, double? ScaleDenominator, string TargetCrs);
