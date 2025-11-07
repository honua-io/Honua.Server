// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Cdn;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Caching;
using Honua.Server.Core.Raster.Rendering;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Ogc;
using Honua.Server.Host.Raster;
using Honua.Server.Host.Utilities;
using Honua.Server.Host.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Honua.Server.Host.Wms;

/// <summary>
/// Handles WMS GetMap operations.
/// </summary>
internal static class WmsGetMapHandlers
{
    private sealed record WmsLayerContext(RasterDatasetDefinition Dataset, string RequestedLayerName, string CanonicalLayerName);

    /// <summary>
    /// Handles the WMS GetMap request.
    /// </summary>
    public static Task<IResult> HandleGetMapAsync(
        HttpRequest request,
        [FromServices] MetadataSnapshot snapshot,
        [FromServices] IRasterDatasetRegistry rasterRegistry,
        [FromServices] IRasterRenderer rasterRenderer,
        [FromServices] IRasterTileCacheProvider cacheProvider,
        [FromServices] IRasterTileCacheMetrics cacheMetrics,
        [FromServices] IOptions<WmsOptions> wmsOptions,
        CancellationToken cancellationToken) =>
        ActivityScope.ExecuteAsync(
            HonuaTelemetry.OgcProtocols,
            "WMS GetMap",
            [("wms.operation", "GetMap")],
            async activity =>
            {
                var options = wmsOptions.Value;
                var query = request.Query;
                var layerContexts = await ResolveDatasetContextsAsync(query, rasterRegistry, cancellationToken).ConfigureAwait(false);
                var primaryContext = layerContexts[0];
                var dataset = primaryContext.Dataset;

                activity.AddTag("wms.layer", dataset.Id);
                activity.AddTag("wms.layer_count", layerContexts.Count);

                // Parse CRS first, then parse bbox with correct axis order for the CRS
                var crsRaw = QueryParsingHelpers.GetQueryValue(query, "crs") ?? QueryParsingHelpers.GetQueryValue(query, "srs");
                var targetCrs = CrsNormalizationHelper.NormalizeForWms(crsRaw);

                var bbox = WmsSharedHelpers.ParseBoundingBox(QueryParsingHelpers.GetQueryValue(query, "bbox"), targetCrs);
                var width = WmsSharedHelpers.ParsePositiveInt(query, "width");
                var height = WmsSharedHelpers.ParsePositiveInt(query, "height");

                // Validate image size limits
                ValidateImageSize(width, height, options);

                var sourceCrs = CrsNormalizationHelper.NormalizeForWms(dataset.Crs.FirstOrDefault());

                var transparent = QueryParsingHelpers.ParseBoolean(query, "transparent", defaultValue: false);
                var formatRaw = QueryParsingHelpers.GetQueryValue(query, "format");
                var normalizedFormat = RasterFormatHelper.Normalize(formatRaw);

                activity.AddTag("wms.format", normalizedFormat);
                activity.AddTag("wms.width", width);
                activity.AddTag("wms.height", height);
                activity.AddTag("wms.crs", targetCrs);
                if (normalizedFormat.EqualsIgnoreCase("jpeg"))
                {
                    transparent = false;
                }

                // Parse TIME parameter once - validate per-layer to avoid mutation issues
                var rawTimeValue = QueryParsingHelpers.GetQueryValue(query, "time");

                // Validate TIME for primary layer
                var primaryTimeValue = dataset.Temporal.Enabled
                    ? WmsSharedHelpers.ValidateTimeParameter(rawTimeValue, dataset.Temporal)
                    : null;

                // WMS 1.3.0 Compliance: STYLES parameter is required (even if empty)
                var stylesParam = QueryParsingHelpers.GetQueryValue(query, "styles");
                if (stylesParam is null)
                {
                    throw new InvalidOperationException("Parameter 'styles' is required for WMS 1.3.0 GetMap requests (use empty string for default styles).");
                }

                // WMS 1.3.0 Compliance: Check for SLD/SLD_BODY parameters (basic support)
                var sldParam = QueryParsingHelpers.GetQueryValue(query, "sld");
                var sldBodyParam = QueryParsingHelpers.GetQueryValue(query, "sld_body");

                if (sldParam.HasValue() || sldBodyParam.HasValue())
                {
                    // SLD is requested but not yet fully implemented
                    // For now, we acknowledge the parameter and fall back to default styles
                    // Future: Parse SLD XML and apply custom styling
                    activity.AddTag("wms.sld_requested", true);
                    if (sldParam.HasValue())
                    {
                        activity.AddTag("wms.sld_url", sldParam);
                    }
                }

                var styleTokens = ParseStyleTokens(stylesParam, layerContexts.Count);
                var primaryStyleId = WmsSharedHelpers.ResolveRequestedStyleId(dataset, styleTokens[0]);
                var primaryStyleDefinition = WmsSharedHelpers.ResolveStyleDefinition(snapshot, primaryStyleId);

                var overlays = new List<RasterLayerRequest>();
                if (layerContexts.Count > 1)
                {
                    for (var index = 1; index < layerContexts.Count; index++)
                    {
                        var overlayContext = layerContexts[index];
                        var overlayStyleId = WmsSharedHelpers.ResolveRequestedStyleId(overlayContext.Dataset, styleTokens[index]);
                        var overlayStyleDefinition = WmsSharedHelpers.ResolveStyleDefinition(snapshot, overlayStyleId);
                        // Validate TIME independently for each overlay using the raw request value
                        var overlayTimeValue = overlayContext.Dataset.Temporal.Enabled
                            ? WmsSharedHelpers.ValidateTimeParameter(rawTimeValue, overlayContext.Dataset.Temporal)
                            : null;
                        overlays.Add(new RasterLayerRequest(overlayContext.Dataset, overlayStyleId, overlayStyleDefinition, overlayTimeValue));
                    }
                }

                var contentType = RasterFormatHelper.GetContentType(normalizedFormat);

                var useCache = layerContexts.Count == 1 && dataset.Cache.Enabled;
                var cacheKey = default(RasterTileCacheKey);
                string? cacheVariant = null;
                if (useCache)
                {
                    useCache = TryBuildTileCacheKeyForWms(
                        dataset,
                        bbox,
                        width,
                        height,
                        targetCrs,
                        primaryStyleId,
                        contentType,
                        transparent,
                        primaryTimeValue,
                        out cacheKey,
                        out cacheVariant);
                }

                if (useCache)
                {
                    var cached = await ActivityScope.ExecuteAsync(
                        HonuaTelemetry.RasterTiles,
                        "Cache Lookup",
                        [("cache.dataset", (object?)dataset.Id)],
                        async cacheActivity =>
                        {
                            try
                            {
                                var result = await cacheProvider.TryGetAsync(cacheKey, cancellationToken).ConfigureAwait(false);
                                if (result is not null)
                                {
                                    cacheMetrics.RecordCacheHit(dataset.Id, cacheVariant, primaryTimeValue);
                                    activity.AddTag("wms.cache_hit", true);
                                    if (!string.IsNullOrWhiteSpace(cacheVariant))
                                    {
                                        activity.AddTag("wms.cache_variant", cacheVariant);
                                    }
                                    if (!string.IsNullOrWhiteSpace(primaryTimeValue))
                                    {
                                        activity.AddTag("wms.cache_time", primaryTimeValue);
                                    }
                                    cacheActivity.AddTag("cache.hit", true);
                                }
                                return result;
                            }
                            catch
                            {
                                // Ignore cache lookup failures and fall back to rendering
                                return null;
                            }
                        }).ConfigureAwait(false);

                    if (cached is not null)
                    {
                        return CreateFileResultWithCdn(cached.Value.Content.ToArray(), cached.Value.ContentType, dataset.Cdn);
                    }

                    cacheMetrics.RecordCacheMiss(dataset.Id, cacheVariant, primaryTimeValue);
                    activity.AddTag("wms.cache_hit", false);
                    if (!string.IsNullOrWhiteSpace(cacheVariant))
                    {
                        activity.AddTag("wms.cache_variant", cacheVariant);
                    }
                    if (!string.IsNullOrWhiteSpace(primaryTimeValue))
                    {
                        activity.AddTag("wms.cache_time", primaryTimeValue);
                    }
                }

                var renderRequest = new RasterRenderRequest(
                    dataset,
                    bbox,
                    width,
                    height,
                    sourceCrs,
                    targetCrs,
                    normalizedFormat,
                    transparent,
                    primaryStyleId,
                    primaryStyleDefinition,
                    AdditionalLayers: overlays,
                    Time: primaryTimeValue);

                var stopwatch = Stopwatch.StartNew();

                // Create a timeout token for rendering
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(options.RenderTimeoutSeconds));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                var (renderResult, renderBytes) = await ActivityScope.ExecuteAsync(
                    HonuaTelemetry.RasterTiles,
                    "Raster Render",
                    [
                        ("raster.dataset", (object?)dataset.Id),
                        ("raster.format", normalizedFormat),
                        ("raster.width", width),
                        ("raster.height", height)
                    ],
                    async renderActivity =>
                    {
                        try
                        {
                            var result = await rasterRenderer.RenderAsync(renderRequest, linkedCts.Token).ConfigureAwait(false);

                            // Estimate size based on dimensions and format
                            var estimatedSize = EstimateImageSize(width, height, normalizedFormat);
                            var shouldBuffer = estimatedSize <= options.StreamingThresholdBytes && (useCache || dataset.Cdn.Enabled);

                            byte[]? bufferedBytes = null;
                            if (shouldBuffer && options.EnableStreaming)
                            {
                                // Buffer small images for caching/CDN headers
                                await using var renderStream = result.Content;
                                using var buffer = new MemoryStream((int)estimatedSize);
                                await renderStream.CopyToAsync(buffer, linkedCts.Token).ConfigureAwait(false);
                                bufferedBytes = buffer.ToArray();

                                renderActivity.AddTag("raster.bytes", bufferedBytes.Length);
                                renderActivity.AddTag("raster.buffered", true);
                            }
                            else
                            {
                                // For large images or when streaming is disabled, note streaming mode
                                // DO NOT dispose the stream here - it will be disposed by the StreamingFileResult
                                renderActivity.AddTag("raster.streaming", options.EnableStreaming);
                                renderActivity.AddTag("raster.estimated_bytes", estimatedSize);
                            }

                            stopwatch.Stop();
                            renderActivity.AddTag("raster.duration_ms", stopwatch.ElapsedMilliseconds);

                            return (result, bufferedBytes);
                        }
                        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                        {
                            throw new InvalidOperationException($"WMS GetMap rendering exceeded timeout of {options.RenderTimeoutSeconds} seconds");
                        }
                    }).ConfigureAwait(false);

                activity.AddTag("wms.render_duration_ms", stopwatch.ElapsedMilliseconds);
                cacheMetrics.RecordRenderLatency(dataset.Id, stopwatch.Elapsed, false);

                // Cache storage for buffered results
                if (useCache && renderBytes is not null && renderBytes.Length > 0)
                {
                    try
                    {
                        var entry = new RasterTileCacheEntry(renderBytes, renderResult.ContentType, DateTimeOffset.UtcNow);
                        await cacheProvider.StoreAsync(cacheKey, entry, cancellationToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Ignore cache store failures for on-demand rendering
                    }
                }

                // Return either buffered or streaming result
                if (renderBytes is not null)
                {
                    return CreateFileResultWithCdn(renderBytes, renderResult.ContentType, dataset.Cdn);
                }
                else
                {
                    return CreateStreamingResultWithCdn(renderResult.Content, renderResult.ContentType, dataset.Cdn);
                }
            });

    private static IResult CreateFileResultWithCdn(byte[] content, string contentType, RasterCdnDefinition cdnDefinition)
    {
        if (!cdnDefinition.Enabled)
        {
            return Results.File(content, contentType);
        }

        var policy = CdnCachePolicy.FromRasterDefinition(cdnDefinition);
        var cacheControl = policy.ToCacheControlHeader();

        return new FileResultWithHeaders(content, contentType, cacheControl);
    }

    private sealed class FileResultWithHeaders : IResult
    {
        private readonly byte[] _content;
        private readonly string _contentType;
        private readonly string _cacheControl;

        public FileResultWithHeaders(byte[] content, string contentType, string cacheControl)
        {
            _content = content;
            _contentType = contentType;
            _cacheControl = cacheControl;
        }

        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.ContentType = _contentType;
            httpContext.Response.Headers.CacheControl = _cacheControl;
            httpContext.Response.Headers.Vary = "Accept-Encoding";
            return httpContext.Response.Body.WriteAsync(_content).AsTask();
        }
    }

    private static async Task<IReadOnlyList<WmsLayerContext>> ResolveDatasetContextsAsync(
        IQueryCollection query,
        IRasterDatasetRegistry rasterRegistry,
        CancellationToken cancellationToken)
    {
        var layersRaw = QueryParsingHelpers.GetQueryValue(query, "layers");
        if (layersRaw.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("Parameter 'layers' is required.");
        }

        var layerNames = QueryParsingHelpers.ParseCsv(layersRaw);
        if (layerNames.Count == 0)
        {
            throw new InvalidOperationException("Parameter 'layers' must include at least one entry.");
        }

        var contexts = new List<WmsLayerContext>(layerNames.Count);
        foreach (var requestedLayerName in layerNames)
        {
            var dataset = await WmsSharedHelpers.ResolveDatasetAsync(requestedLayerName, rasterRegistry, cancellationToken).ConfigureAwait(false);
            if (dataset is null)
            {
                throw new InvalidOperationException($"Layer '{requestedLayerName}' was not found.");
            }

            var canonicalLayerName = WmsSharedHelpers.BuildLayerName(dataset);
            contexts.Add(new WmsLayerContext(dataset, requestedLayerName, canonicalLayerName));
        }

        return contexts;
    }

    private static string?[] ParseStyleTokens(string? stylesParameter, int layerCount)
    {
        if (layerCount <= 0)
        {
            return Array.Empty<string?>();
        }

        var tokens = new string?[layerCount];
        if (stylesParameter.IsNullOrWhiteSpace())
        {
            return tokens;
        }

        var rawTokens = stylesParameter.Split(',');
        for (var index = 0; index < layerCount; index++)
        {
            if (index < rawTokens.Length)
            {
                var value = rawTokens[index].Trim();
                tokens[index] = value.IsNullOrWhiteSpace() ? null : value;
            }
            else
            {
                tokens[index] = null;
            }
        }

        return tokens;
    }

    private static bool TryBuildTileCacheKeyForWms(
        RasterDatasetDefinition dataset,
        double[] bbox,
        int width,
        int height,
        string targetCrs,
        string? styleId,
        string format,
        bool transparent,
        string? time,
        out RasterTileCacheKey cacheKey,
        out string? cacheVariant)
    {
        cacheKey = default;
        cacheVariant = null;

        if (bbox is null || bbox.Length < 4)
        {
            return false;
        }

        if (width <= 0 || height <= 0)
        {
            return false;
        }

        var matrixId = ResolveTileMatrixIdForCrs(targetCrs);
        if (matrixId is null)
        {
            return false;
        }

        var (minZoom, maxZoom) = OgcTileMatrixHelper.ResolveZoomRange(dataset.Cache.ZoomLevels);

        for (var zoom = minZoom; zoom <= maxZoom; zoom++)
        {
            var range = OgcTileMatrixHelper.GetTileRange(matrixId, zoom, bbox[0], bbox[1], bbox[2], bbox[3]);
            if (range.MinRow != range.MaxRow || range.MinColumn != range.MaxColumn)
            {
                continue;
            }

            var expectedBbox = OgcTileMatrixHelper.GetBoundingBox(matrixId, zoom, range.MinRow, range.MinColumn);
            if (!BoundingBoxesEqual(expectedBbox, bbox))
            {
                continue;
            }

            var cacheStyleId = NormalizeStyleKey(styleId, width, height);
            cacheKey = new RasterTileCacheKey(dataset.Id, matrixId, zoom, range.MinRow, range.MinColumn, cacheStyleId, format, transparent, width, time);
            cacheVariant = cacheStyleId;
            return true;
        }

        return false;
    }

    private static string? NormalizeStyleKey(string? styleId, int width, int height)
    {
        var baseStyle = styleId.HasValue() ? styleId : "default";
        if (width == height)
        {
            return baseStyle;
        }

        return $"{baseStyle}__{width}x{height}";
    }

    private static string? ResolveTileMatrixIdForCrs(string crs)
    {
        if (crs.EqualsIgnoreCase(OgcTileMatrixHelper.WorldWebMercatorQuadCrs)
            || crs.EqualsIgnoreCase("EPSG:3857")
            || crs.EqualsIgnoreCase("EPSG:900913"))
        {
            return OgcTileMatrixHelper.WorldWebMercatorQuadId;
        }

        if (crs.EqualsIgnoreCase("CRS:84")
            || crs.EqualsIgnoreCase("EPSG:4326"))
        {
            return OgcTileMatrixHelper.WorldCrs84QuadId;
        }

        return null;
    }

    private static bool BoundingBoxesEqual(double[] expected, double[] actual, double tolerance = 0d)
    {
        if (expected.Length < 4 || actual.Length < 4)
        {
            return false;
        }

        if (tolerance <= 0d)
        {
            var expectedWidth = Math.Abs(expected[2] - expected[0]);
            var expectedHeight = Math.Abs(expected[3] - expected[1]);
            var scale = Math.Max(expectedWidth, expectedHeight);
            tolerance = Math.Max(scale * 1e-4, 1e-6);
        }

        for (var i = 0; i < 4; i++)
        {
            if (Math.Abs(expected[i] - actual[i]) > tolerance)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates that the requested image dimensions are within configured limits.
    /// </summary>
    private static void ValidateImageSize(int width, int height, WmsOptions options)
    {
        if (width > options.MaxWidth)
        {
            throw new InvalidOperationException($"Requested width {width} exceeds maximum allowed width of {options.MaxWidth} pixels");
        }

        if (height > options.MaxHeight)
        {
            throw new InvalidOperationException($"Requested height {height} exceeds maximum allowed height of {options.MaxHeight} pixels");
        }

        var totalPixels = (long)width * height;
        if (totalPixels > options.MaxTotalPixels)
        {
            throw new InvalidOperationException($"Requested image size {width}x{height} ({totalPixels:N0} pixels) exceeds maximum allowed total pixels of {options.MaxTotalPixels:N0}");
        }
    }

    /// <summary>
    /// Estimates the size of a rendered image based on dimensions and format.
    /// Used to determine whether to buffer or stream the response.
    /// </summary>
    private static long EstimateImageSize(int width, int height, string format)
    {
        var pixels = (long)width * height;

        // Rough estimates based on typical compression ratios
        return format.ToLowerInvariant() switch
        {
            "png" => pixels * 3 / 2, // ~1.5 bytes per pixel for compressed PNG
            "jpeg" or "jpg" => pixels / 4, // ~0.25 bytes per pixel for JPEG
            "webp" => pixels / 3, // ~0.33 bytes per pixel for WebP
            "tiff" or "tif" => pixels * 4, // ~4 bytes per pixel for uncompressed TIFF
            _ => pixels * 2 // Default estimate
        };
    }

    private static IResult CreateStreamingResultWithCdn(Stream content, string contentType, RasterCdnDefinition cdnDefinition)
    {
        if (!cdnDefinition.Enabled)
        {
            return new StreamingFileResult(content, contentType, cacheControl: null);
        }

        var policy = CdnCachePolicy.FromRasterDefinition(cdnDefinition);
        var cacheControl = policy.ToCacheControlHeader();

        return new StreamingFileResult(content, contentType, cacheControl);
    }

    private sealed class StreamingFileResult : IResult
    {
        private readonly Stream _content;
        private readonly string _contentType;
        private readonly string? _cacheControl;

        public StreamingFileResult(Stream content, string contentType, string? cacheControl)
        {
            _content = content;
            _contentType = contentType;
            _cacheControl = cacheControl;
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.ContentType = _contentType;

            if (!string.IsNullOrWhiteSpace(_cacheControl))
            {
                httpContext.Response.Headers.CacheControl = _cacheControl;
                httpContext.Response.Headers.Vary = "Accept-Encoding";
            }

            await using (_content)
            {
                await _content.CopyToAsync(httpContext.Response.Body, httpContext.RequestAborted).ConfigureAwait(false);
            }
        }
    }
}
