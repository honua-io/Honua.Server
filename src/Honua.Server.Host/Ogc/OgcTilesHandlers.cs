// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Cdn;
using Honua.Server.Core.Data;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Caching;
using Honua.Server.Core.Raster.Rendering;
using Honua.Server.Core.Results;
using Honua.Server.Core.Styling;
using Honua.Server.Host.Raster;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using NetTopologySuite.Geometries;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// HTTP handlers for OGC API - Tiles endpoints.
/// Implements tile serving functionality compliant with OGC API - Tiles v1.0 specification.
/// </summary>
/// <remarks>
/// <para>
/// This class provides handlers for:
/// </para>
/// <list type="bullet">
/// <item>Tile matrix set definitions and discovery</item>
/// <item>Collection tileset metadata</item>
/// <item>Individual tile retrieval (raster and vector)</item>
/// <item>TileJSON metadata for client compatibility</item>
/// <item>Tile caching and CDN integration</item>
/// </list>
/// <para>
/// Supports both standard OGC API - Tiles URL patterns and legacy patterns for backward compatibility.
/// All handlers implement proper CRS handling, style resolution, and temporal filtering.
/// </para>
/// </remarks>
internal static class OgcTilesHandlers
{
    /// <summary>
    /// Gets metadata for all tilesets available for a collection.
    /// </summary>
    /// <param name="collectionId">The collection identifier (format: "serviceId::layerId").</param>
    /// <param name="request">The HTTP request.</param>
    /// <param name="resolver">Service for resolving collection context.</param>
    /// <param name="rasterRegistry">Registry for raster dataset definitions.</param>
    /// <param name="cacheHeaderService">Service for generating cache headers and ETags.</param>
    /// <param name="tilesHandler">Handler for OGC tiles operations.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// JSON response containing tileset metadata including tile matrix sets, zoom levels,
    /// bounding boxes, and links to tile endpoints.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This endpoint is part of OGC API - Tiles v1.0 core conformance class.
    /// Returns a list of all raster datasets (tilesets) associated with the collection,
    /// including metadata about supported tile matrix sets, zoom ranges, CRS, and styles.
    /// </para>
    /// <para>
    /// Each tileset includes:
    /// </para>
    /// <list type="bullet">
    /// <item>Tile matrix set links with zoom level limits</item>
    /// <item>Bounding box in collection CRS</item>
    /// <item>Available styles and default style</item>
    /// <item>Zoom range (minZoom, maxZoom)</item>
    /// </list>
    /// </remarks>
    public static async Task<IResult> GetCollectionTileSets(
        string collectionId,
        HttpRequest request,
        [FromServices] IFeatureContextResolver resolver,
        [FromServices] IRasterDatasetRegistry rasterRegistry,
        [FromServices] OgcCacheHeaderService cacheHeaderService,
        [FromServices] Services.IOgcTilesHandler tilesHandler,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);
        Guard.NotNull(resolver);
        Guard.NotNull(rasterRegistry);

        var resolution = await OgcSharedHandlers.ResolveCollectionAsync(collectionId, resolver, cancellationToken).ConfigureAwait(false);
        if (resolution.IsFailure)
        {
            return OgcSharedHandlers.MapCollectionResolutionError(resolution.Error!, collectionId);
        }

        var service = resolution.Value.Service;
        var layer = resolution.Value.Layer;

        var datasets = await rasterRegistry.GetByServiceAsync(service.Id, cancellationToken).ConfigureAwait(false);
        var matchingDatasets = datasets
            .Where(dataset => tilesHandler.DatasetMatchesCollection(dataset, service, layer))
            .ToList();

        var tilesets = new List<object>(matchingDatasets.Count);
        foreach (var dataset in matchingDatasets)
        {
            var (minZoom, maxZoom) = OgcTileMatrixHelper.ResolveZoomRange(dataset.Cache.ZoomLevels);
            var crs = dataset.Crs.Count > 0
                ? dataset.Crs.Select(CrsHelper.NormalizeIdentifier).ToArray()
                : new[] { CrsHelper.DefaultCrsIdentifier };

            var bounds = tilesHandler.ResolveBounds(layer, dataset);
            var boundingBox = new
            {
                lowerLeft = new[] { bounds[0], bounds[1] },
                upperRight = new[] { bounds[2], bounds[3] },
                crs = crs.Length > 0 ? crs[0] : CrsHelper.DefaultCrsIdentifier
            };

            var links = new List<OgcLink>
            {
                OgcSharedHandlers.BuildLink(request, $"/ogc/collections/{collectionId}/tiles/{dataset.Id}", "self", "application/json", dataset.Title ?? dataset.Id),
                OgcSharedHandlers.BuildLink(request, $"/ogc/collections/{collectionId}", "collection", "application/json", layer.Title ?? collectionId)
            };

            var tileMatrixSetLinks = BuildTileMatrixSetLinksWithLimits(request, collectionId, dataset.Id, minZoom, maxZoom, bounds);

            tilesets.Add(new
            {
                id = dataset.Id,
                title = dataset.Title,
                description = dataset.Description,
                dataType = "map",
                crs,
                boundingBox,
                defaultStyle = dataset.Styles.DefaultStyleId,
                styleIds = dataset.Styles.StyleIds,
                minZoom,
                maxZoom,
                links,
                tileMatrixSetLinks
            });
        }

        var responseLinks = new List<OgcLink>
        {
            OgcSharedHandlers.BuildLink(request, $"/ogc/collections/{collectionId}/tiles", "self", "application/json", "Tilesets"),
            OgcSharedHandlers.BuildLink(request, $"/ogc/collections/{collectionId}", "collection", "application/json", layer.Title ?? collectionId),
            OgcSharedHandlers.BuildLink(request, "/ogc/tileMatrixSets", "http://www.opengis.net/def/rel/ogc/1.0/tiling-schemes", "application/json", "Tile matrix sets")
        };

        var response = new { tilesets, links = responseLinks };
        var etag = cacheHeaderService.GenerateETagForObject(response);
        return Results.Ok(response).WithMetadataCacheHeaders(cacheHeaderService, etag);
    }

    public static async Task<IResult> GetCollectionTileSet(
        string collectionId,
        string tilesetId,
        HttpRequest request,
        [FromServices] IFeatureContextResolver resolver,
        [FromServices] IRasterDatasetRegistry rasterRegistry,
        [FromServices] OgcCacheHeaderService cacheHeaderService,
        [FromServices] Services.IOgcTilesHandler tilesHandler,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);
        Guard.NotNull(resolver);
        Guard.NotNull(rasterRegistry);

        var resolution = await OgcSharedHandlers.ResolveCollectionAsync(collectionId, resolver, cancellationToken).ConfigureAwait(false);
        if (resolution.IsFailure)
        {
            return OgcSharedHandlers.MapCollectionResolutionError(resolution.Error!, collectionId);
        }

        var service = resolution.Value.Service;
        var layer = resolution.Value.Layer;

        var dataset = await rasterRegistry.FindAsync(tilesetId, cancellationToken).ConfigureAwait(false);
        if (dataset is null || !tilesHandler.DatasetMatchesCollection(dataset, service, layer))
        {
            return Results.NotFound();
        }

        var (minZoom, maxZoom) = OgcTileMatrixHelper.ResolveZoomRange(dataset.Cache.ZoomLevels);
        var crs = dataset.Crs.Count > 0
            ? dataset.Crs.Select(CrsHelper.NormalizeIdentifier).ToArray()
            : new[] { CrsHelper.DefaultCrsIdentifier };

        var bounds = OgcSharedHandlers.ResolveBounds(layer, dataset);
        var boundingBox = new
        {
            lowerLeft = new[] { bounds[0], bounds[1] },
            upperRight = new[] { bounds[2], bounds[3] },
            crs = crs.Length > 0 ? crs[0] : CrsHelper.DefaultCrsIdentifier
        };

        var links = new List<OgcLink>
        {
            OgcSharedHandlers.BuildLink(request, $"/ogc/collections/{collectionId}/tiles/{dataset.Id}", "self", "application/json", dataset.Title ?? dataset.Id),
            OgcSharedHandlers.BuildLink(request, $"/ogc/collections/{collectionId}/tiles", "collection", "application/json", layer.Title ?? collectionId),
            OgcSharedHandlers.BuildLink(request, "/ogc/tileMatrixSets", "http://www.opengis.net/def/rel/ogc/1.0/tiling-schemes", "application/json", "Tile matrix sets")
        };

        var tileMatrixSetLinks = BuildTileMatrixSetLinksWithLimits(request, collectionId, dataset.Id, minZoom, maxZoom, bounds);

        var response = new
        {
            id = dataset.Id,
            title = dataset.Title,
            description = dataset.Description,
            dataType = "map",
            crs,
            boundingBox,
            defaultStyle = dataset.Styles.DefaultStyleId,
            styleIds = dataset.Styles.StyleIds,
            minZoom,
            maxZoom,
            links,
            tileMatrixSetLinks
        };
        var etag = cacheHeaderService.GenerateETagForObject(response);
        return Results.Ok(response).WithMetadataCacheHeaders(cacheHeaderService, etag);
    }

    public static async Task<IResult> GetCollectionTileJson(
        string collectionId,
        string tilesetId,
        HttpRequest request,
        [FromServices] IFeatureContextResolver resolver,
        [FromServices] IRasterDatasetRegistry rasterRegistry,
        [FromServices] OgcCacheHeaderService cacheHeaderService,
        [FromServices] Services.IOgcTilesHandler tilesHandler,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);
        Guard.NotNull(resolver);
        Guard.NotNull(rasterRegistry);

        var resolution = await OgcSharedHandlers.ResolveCollectionAsync(collectionId, resolver, cancellationToken).ConfigureAwait(false);
        if (resolution.IsFailure)
        {
            return OgcSharedHandlers.MapCollectionResolutionError(resolution.Error!, collectionId);
        }

        var service = resolution.Value.Service;
        var layer = resolution.Value.Layer;

        var dataset = await rasterRegistry.FindAsync(tilesetId, cancellationToken).ConfigureAwait(false);
        if (dataset is null || !tilesHandler.DatasetMatchesCollection(dataset, service, layer))
        {
            return Results.NotFound();
        }

        var (minZoom, maxZoom) = OgcTileMatrixHelper.ResolveZoomRange(dataset.Cache.ZoomLevels);
        var sourceType = dataset.Source?.Type ?? string.Empty;
        var isVectorDataset = string.Equals(sourceType, "vector", StringComparison.OrdinalIgnoreCase);
        var tileFormat = isVectorDataset ? "geojson" : "png";
        var dataType = isVectorDataset ? "vector" : "map";
        var bounds = OgcSharedHandlers.ResolveBounds(layer, dataset);
        var center = new[]
        {
            Math.Round((bounds[0] + bounds[2]) / 2d, 6),
            Math.Round((bounds[1] + bounds[3]) / 2d, 6),
            Math.Clamp((double)minZoom, 0d, maxZoom)
        };

        var overrides = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["f"] = isVectorDataset ? tileFormat : null
        };

        var tiles = new[]
        {
            OgcSharedHandlers.BuildHref(request, $"/ogc/collections/{collectionId}/tiles/{dataset.Id}/{OgcTileMatrixHelper.WorldWebMercatorQuadId}/{{z}}/{{y}}/{{x}}", null, overrides)
        };

        var links = new List<OgcLink>
        {
            OgcSharedHandlers.BuildLink(request, $"/ogc/collections/{collectionId}/tiles/{dataset.Id}/tilejson", "self", "application/json", dataset.Title ?? dataset.Id),
            OgcSharedHandlers.BuildLink(request, $"/ogc/collections/{collectionId}/tiles/{dataset.Id}", "collection", "application/json", layer.Title ?? collectionId)
        };

        var vectorLayers = isVectorDataset
            ? new object[]
            {
                new
                {
                    id = dataset.LayerId ?? dataset.Id,
                    description = dataset.Title ?? dataset.Id
                }
            }
            : Array.Empty<object>();

        var response = new
        {
            tilejson = "3.0.0",
            name = dataset.Title ?? dataset.Id,
            description = dataset.Description ?? layer.Description,
            scheme = "xyz",
            format = tileFormat,
            minzoom = minZoom,
            maxzoom = maxZoom,
            bounds,
            center,
            tiles,
            dataType,
            links,
            vector_layers = vectorLayers
        };
        var etag = cacheHeaderService.GenerateETagForObject(response);
        return Results.Ok(response).WithMetadataCacheHeaders(cacheHeaderService, etag);
    }

    public static async Task<IResult> GetCollectionTileMatrixSet(
        string collectionId,
        string tilesetId,
        string tileMatrixSetId,
        HttpRequest request,
        [FromServices] IFeatureContextResolver resolver,
        [FromServices] IRasterDatasetRegistry rasterRegistry,
        [FromServices] OgcCacheHeaderService cacheHeaderService,
        [FromServices] Services.IOgcTilesHandler tilesHandler,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);
        Guard.NotNull(resolver);
        Guard.NotNull(rasterRegistry);

        var resolution = await OgcSharedHandlers.ResolveCollectionAsync(collectionId, resolver, cancellationToken).ConfigureAwait(false);
        if (resolution.IsFailure)
        {
            return OgcSharedHandlers.MapCollectionResolutionError(resolution.Error!, collectionId);
        }

        var service = resolution.Value.Service;
        var layer = resolution.Value.Layer;

        var dataset = await rasterRegistry.FindAsync(tilesetId, cancellationToken).ConfigureAwait(false);
        if (dataset is null || !tilesHandler.DatasetMatchesCollection(dataset, service, layer))
        {
            return Results.NotFound();
        }

        var normalized = tilesHandler.NormalizeTileMatrixSet(tileMatrixSetId);
        if (normalized is null)
        {
            return Results.NotFound();
        }

        var (matrixId, matrixUri, matrixCrs) = normalized.Value;
        var (minZoom, maxZoom) = OgcTileMatrixHelper.ResolveZoomRange(dataset.Cache.ZoomLevels);
        var tileMatrices = OgcTileMatrixHelper.BuildTileMatrices(matrixId, minZoom, maxZoom);

        var links = new List<OgcLink>
        {
            OgcSharedHandlers.BuildLink(request, $"/ogc/collections/{collectionId}/tiles/{dataset.Id}/{matrixId}", "self", "application/json", $"{matrixId} tile matrix set"),
            OgcSharedHandlers.BuildLink(request, $"/ogc/collections/{collectionId}/tiles/{dataset.Id}", "collection", "application/json", dataset.Title ?? dataset.Id),
            OgcSharedHandlers.BuildLink(request, $"/ogc/tileMatrixSets/{matrixId}", "describedby", "application/json", $"{matrixId} definition")
        };

        var response = new
        {
            id = matrixId,
            title = matrixId,
            tileMatrixSetUri = matrixUri,
            crs = matrixCrs,
            dataType = "map",
            minZoom,
            maxZoom,
            tileMatrices,
            links
        };
        var etag = cacheHeaderService.GenerateETagForObject(response);
        return Results.Ok(response).WithTileMatrixSetCacheHeaders(cacheHeaderService, etag);
    }

    /// <summary>
    /// OGC API - Tiles standard endpoint for retrieving tiles without explicit tileset ID.
    /// Conforms to the pattern: /collections/{collectionId}/tiles/{tileMatrixSetId}/{tileMatrix}/{tileRow}/{tileCol}
    /// </summary>
    public static async Task<IResult> GetCollectionTileStandard(
        string collectionId,
        string tileMatrixSetId,
        string tileMatrix,
        int tileRow,
        int tileCol,
        HttpRequest request,
        [FromServices] IFeatureContextResolver resolver,
        [FromServices] IRasterDatasetRegistry rasterRegistry,
        [FromServices] IRasterRenderer rasterRenderer,
        [FromServices] IMetadataRegistry metadataRegistry,
        [FromServices] IFeatureRepository repository,
        [FromServices] IPmTilesExporter pmTilesExporter,
        [FromServices] IRasterTileCacheProvider tileCacheProvider,
        [FromServices] IRasterTileCacheMetrics tileCacheMetrics,
        [FromServices] OgcCacheHeaderService cacheHeaderService,
        [FromServices] Services.IOgcTilesHandler tilesHandler,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);
        Guard.NotNull(resolver);
        Guard.NotNull(rasterRegistry);

        var resolution = await OgcSharedHandlers.ResolveCollectionAsync(collectionId, resolver, cancellationToken).ConfigureAwait(false);
        if (resolution.IsFailure)
        {
            return OgcSharedHandlers.MapCollectionResolutionError(resolution.Error!, collectionId);
        }

        var service = resolution.Value.Service;
        var layer = resolution.Value.Layer;

        // Find the first matching dataset for this collection
        var datasets = await rasterRegistry.GetByServiceAsync(service.Id, cancellationToken).ConfigureAwait(false);
        var dataset = datasets.FirstOrDefault(d => OgcSharedHandlers.DatasetMatchesCollection(d, service, layer));

        if (dataset is null)
        {
            return Results.NotFound();
        }

        // Forward to the legacy handler with the resolved tilesetId
        return await GetCollectionTile(
            collectionId,
            dataset.Id,
            tileMatrixSetId,
            tileMatrix,
            tileRow,
            tileCol,
            request,
            resolver,
            rasterRegistry,
            rasterRenderer,
            metadataRegistry,
            repository,
            pmTilesExporter,
            tileCacheProvider,
            tileCacheMetrics,
            cacheHeaderService,
            tilesHandler,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves a single tile for a collection at the specified coordinates.
    /// </summary>
    /// <param name="collectionId">The collection identifier (format: "serviceId::layerId").</param>
    /// <param name="tilesetId">The tileset (raster dataset) identifier.</param>
    /// <param name="tileMatrixSetId">The tile matrix set identifier (e.g., "WorldWebMercatorQuad").</param>
    /// <param name="tileMatrix">The zoom level (tile matrix).</param>
    /// <param name="tileRow">The tile row coordinate.</param>
    /// <param name="tileCol">The tile column coordinate.</param>
    /// <param name="request">The HTTP request.</param>
    /// <param name="resolver">Service for resolving collection context.</param>
    /// <param name="rasterRegistry">Registry for raster dataset definitions.</param>
    /// <param name="rasterRenderer">Service for rendering raster tiles.</param>
    /// <param name="metadataRegistry">Registry for metadata and style definitions.</param>
    /// <param name="repository">Repository for feature data (used for vector tiles and overlays).</param>
    /// <param name="pmTilesExporter">Exporter for PMTiles format.</param>
    /// <param name="tileCacheProvider">Provider for tile caching.</param>
    /// <param name="tileCacheMetrics">Metrics collector for tile cache operations.</param>
    /// <param name="cacheHeaderService">Service for generating cache headers and ETags.</param>
    /// <param name="tilesHandler">Handler for OGC tiles operations.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A file result containing the tile image (PNG, JPEG, WebP) or vector data (MVT, GeoJSON, PMTiles).
    /// Returns 304 Not Modified if the client's cached tile is still valid.
    /// Returns 404 if the tile is outside valid bounds or zoom range.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is the core tile retrieval endpoint supporting:
    /// </para>
    /// <list type="bullet">
    /// <item>Raster tiles with style application and transparency</item>
    /// <item>Vector tiles (MVT, GeoJSON, PMTiles formats)</item>
    /// <item>Temporal filtering via datetime parameter</item>
    /// <item>Server-side tile caching for performance</item>
    /// <item>CDN integration with appropriate cache headers</item>
    /// <item>ETag-based conditional requests (304 Not Modified)</item>
    /// </list>
    /// <para>
    /// Query parameters:
    /// </para>
    /// <list type="bullet">
    /// <item><term>f</term><description>Output format (e.g., "png", "mvt", "geojson", "pmtiles")</description></item>
    /// <item><term>styleId</term><description>The style identifier to apply (defaults to dataset default style)</description></item>
    /// <item><term>transparent</term><description>Whether to render with transparency (default: true)</description></item>
    /// <item><term>datetime</term><description>Temporal filter in ISO 8601 format</description></item>
    /// </list>
    /// <para>
    /// The method checks the tile cache before rendering and stores rendered tiles
    /// for subsequent requests. CDN cache headers are applied if CDN is enabled for the dataset.
    /// </para>
    /// </remarks>
    public static async Task<IResult> GetCollectionTile(
        string collectionId,
        string tilesetId,
        string tileMatrixSetId,
        string tileMatrix,
        int tileRow,
        int tileCol,
        HttpRequest request,
        [FromServices] IFeatureContextResolver resolver,
        [FromServices] IRasterDatasetRegistry rasterRegistry,
        [FromServices] IRasterRenderer rasterRenderer,
        [FromServices] IMetadataRegistry metadataRegistry,
        [FromServices] IFeatureRepository repository,
        [FromServices] IPmTilesExporter pmTilesExporter,
        [FromServices] IRasterTileCacheProvider tileCacheProvider,
        [FromServices] IRasterTileCacheMetrics tileCacheMetrics,
        [FromServices] OgcCacheHeaderService cacheHeaderService,
        [FromServices] Services.IOgcTilesHandler tilesHandler,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);
        Guard.NotNull(resolver);
        Guard.NotNull(rasterRegistry);
        Guard.NotNull(rasterRenderer);
        Guard.NotNull(metadataRegistry);
        Guard.NotNull(repository);
        Guard.NotNull(pmTilesExporter);
        Guard.NotNull(tileCacheProvider);
        Guard.NotNull(tileCacheMetrics);

        var resolution = await OgcSharedHandlers.ResolveCollectionAsync(collectionId, resolver, cancellationToken).ConfigureAwait(false);
        if (resolution.IsFailure)
        {
            return OgcSharedHandlers.MapCollectionResolutionError(resolution.Error!, collectionId);
        }

        var service = resolution.Value.Service;
        var layer = resolution.Value.Layer;

        var dataset = await rasterRegistry.FindAsync(tilesetId, cancellationToken).ConfigureAwait(false);
        if (dataset is null || !tilesHandler.DatasetMatchesCollection(dataset, service, layer))
        {
            return Results.NotFound();
        }

        var normalized = tilesHandler.NormalizeTileMatrixSet(tileMatrixSetId);
        if (normalized is null)
        {
            return Results.NotFound();
        }

        var (matrixId, _, matrixCrs) = normalized.Value;

        if (!OgcTileMatrixHelper.TryParseZoom(tileMatrix, out var zoom))
        {
            return Results.BadRequest(new { error = "tileMatrix must be a non-negative integer." });
        }

        var (minZoom, maxZoom) = OgcTileMatrixHelper.ResolveZoomRange(dataset.Cache.ZoomLevels);
        if (zoom < minZoom || zoom > maxZoom)
        {
            return Results.NotFound();
        }

        if (!OgcTileMatrixHelper.IsValidTileCoordinate(zoom, tileRow, tileCol))
        {
            return Results.NotFound();
        }

        var bbox = OgcTileMatrixHelper.GetBoundingBox(matrixId, zoom, tileRow, tileCol);
        var tileSize = tilesHandler.ResolveTileSize(request);
        var format = tilesHandler.ResolveTileFormat(request);
        var transparent = !string.Equals(request.Query["transparent"], "false", StringComparison.OrdinalIgnoreCase);

        var styleIdRaw = request.Query.TryGetValue("styleId", out var styleValues)
            ? styleValues.ToString()
            : null;

        if (!OgcSharedHandlers.TryResolveStyle(dataset, styleIdRaw, out var styleId, out var unresolvedStyle))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid styleId",
                detail: $"Dataset '{dataset.Id}' does not define style '{unresolvedStyle}'.");
        }

        // Extract and validate datetime parameter for temporal filtering
        var datetime = request.Query.TryGetValue("datetime", out var datetimeValues)
            ? datetimeValues.ToString()
            : null;

        if (layer.Temporal.Enabled && datetime.HasValue())
        {
            if (!OgcTemporalParameterValidator.TryValidate(datetime, layer.Temporal, allowFuture: true, out var validatedDatetime, out var errorMessage))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid datetime parameter",
                    detail: errorMessage);
            }
            datetime = validatedDatetime;
        }

        if (string.Equals(format, "pmtiles", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(dataset.Source?.Type, "vector", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Unsupported format",
                    detail: "PMTiles output is only available for vector datasets.");
            }

            if (dataset.ServiceId.IsNullOrWhiteSpace() || dataset.LayerId.IsNullOrWhiteSpace())
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Dataset misconfiguration",
                    detail: $"Dataset '{dataset.Id}' is missing service or layer identifiers required for PMTiles generation.");
            }

            // BUG FIX #6: Check if provider supports MVT generation before creating PMTiles archive
            var mvtBytes = await repository.GenerateMvtTileAsync(dataset.ServiceId, dataset.LayerId, zoom, tileCol, tileRow, datetime, cancellationToken).ConfigureAwait(false);
            if (mvtBytes is null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status501NotImplemented,
                    title: "Vector tiles not supported",
                    detail: $"The data provider for dataset '{dataset.Id}' does not support native MVT tile generation. PMTiles export requires MVT support.");
            }

            var pmTilesBytes = pmTilesExporter.CreateSingleTileArchive(zoom, tileCol, tileRow, mvtBytes, bbox, matrixId);
            var pmTilesStream = new MemoryStream(pmTilesBytes, writable: false);
            var pmTilesEtag = cacheHeaderService.GenerateETag(pmTilesBytes);
            return CreateTileResultWithCdn(pmTilesStream, RasterFormatHelper.GetContentType(format), dataset.Cdn, cacheHeaderService, pmTilesEtag);
        }

        if (RasterFormatHelper.IsVectorFormat(format))
        {
            return await OgcSharedHandlers.RenderVectorTileAsync(
                service,
                layer,
                dataset,
                bbox,
                zoom,
                tileRow,
                tileCol,
                datetime,
                repository,
                cancellationToken).ConfigureAwait(false);
        }

        // BUG FIX #2: Check cache before rendering raster tiles
        var cacheKey = new RasterTileCacheKey(
            tilesetId,
            matrixId,
            zoom,
            tileRow,
            tileCol,
            styleId,
            format,
            transparent,
            tileSize,
            datetime);

        var cacheHit = await tileCacheProvider.TryGetAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        if (cacheHit is not null)
        {
            tileCacheMetrics.RecordCacheHit(tilesetId);
            var cachedContent = cacheHit.Value.Content.ToArray();
            var cachedStream = new MemoryStream(cachedContent, writable: false);
            var cachedEtag = cacheHeaderService.GenerateETag(cachedContent);
            return CreateTileResultWithCdn(cachedStream, cacheHit.Value.ContentType, dataset.Cdn, cacheHeaderService, cachedEtag);
        }

        tileCacheMetrics.RecordCacheMiss(tilesetId);

        var selectedStyle = await OgcSharedHandlers.ResolveStyleDefinitionAsync(dataset, styleId, metadataRegistry, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<Geometry>? vectorGeometries = null;
        if (OgcSharedHandlers.RequiresVectorOverlay(selectedStyle))
        {
            vectorGeometries = await OgcSharedHandlers.CollectVectorGeometriesAsync(dataset, bbox, metadataRegistry, repository, cancellationToken).ConfigureAwait(false);
        }

        var sourceCrs = dataset.Crs.Count > 0 ? dataset.Crs[0] : CrsHelper.DefaultCrsIdentifier;
        var targetCrs = matrixCrs;

        var renderRequest = new RasterRenderRequest(
            dataset,
            bbox,
            tileSize,
            tileSize,
            sourceCrs,
            targetCrs,
            format,
            transparent,
            styleId,
            selectedStyle,
            vectorGeometries,
            Time: datetime);

        var renderResult = await rasterRenderer.RenderAsync(renderRequest, cancellationToken).ConfigureAwait(false);
        var (tileStream, tileEtag) = await PrepareTileStreamAsync(renderResult.Content, cacheHeaderService, cancellationToken).ConfigureAwait(false);

        if (tileStream.CanSeek)
        {
            tileStream.Seek(0, SeekOrigin.Begin);
        }

        return CreateTileResultWithCdn(tileStream, renderResult.ContentType, dataset.Cdn, cacheHeaderService, tileEtag);
    }

    private static IResult CreateTileResultWithCdn(Stream content, string contentType, RasterCdnDefinition cdnDefinition, OgcCacheHeaderService cacheHeaderService, string? etag)
    {
        if (!cdnDefinition.Enabled)
        {
            return new TileResultWithHeaders(content, contentType, cacheHeaderService, etag, cacheControlOverride: null);
        }

        var policy = CdnCachePolicy.FromRasterDefinition(cdnDefinition);
        var cacheControl = policy.ToCacheControlHeader();

        return new TileResultWithHeaders(content, contentType, cacheHeaderService, etag, cacheControl);
    }

    private static async Task<(Stream Stream, string? ETag)> PrepareTileStreamAsync(Stream content, OgcCacheHeaderService cacheHeaderService, CancellationToken cancellationToken)
    {
        Guard.NotNull(content);
        Guard.NotNull(cacheHeaderService);

        Stream workingStream = content;

        if (!workingStream.CanSeek)
        {
            var buffered = new MemoryStream();
            await workingStream.CopyToAsync(buffered, cancellationToken).ConfigureAwait(false);
            await DisposeStreamAsync(workingStream).ConfigureAwait(false);
            buffered.Seek(0, SeekOrigin.Begin);
            workingStream = buffered;
        }

        if (!workingStream.CanSeek)
        {
            return (workingStream, null);
        }

        var originalPosition = workingStream.Position;
        workingStream.Seek(0, SeekOrigin.Begin);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            int read;
            while ((read = await workingStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                hash.AppendData(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        var hashBytes = hash.GetHashAndReset();
        var etag = $"\"{Convert.ToHexString(hashBytes)}\"";

        workingStream.Seek(originalPosition, SeekOrigin.Begin);

        return (workingStream, etag);
    }

    private sealed class TileResultWithHeaders : IResult
    {
        private readonly Stream _content;
        private readonly string _contentType;
        private readonly OgcCacheHeaderService _cacheService;
        private readonly string? _etag;
        private readonly string? _cacheControlOverride;
        private bool _disposed;

        public TileResultWithHeaders(
            Stream content,
            string contentType,
            OgcCacheHeaderService cacheService,
            string? etag,
            string? cacheControlOverride)
        {
            _content = Guard.NotNull(content);
            _contentType = Guard.NotNull(contentType);
            _cacheService = Guard.NotNull(cacheService);
            _etag = etag;
            _cacheControlOverride = cacheControlOverride;
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            Guard.NotNull(httpContext);

            if (_cacheService.ShouldReturn304NotModified(httpContext, _etag, null))
            {
                _cacheService.ApplyCacheHeaders(httpContext, OgcResourceType.Tile, _etag, null);
                ApplyCdnHeaders(httpContext.Response);
                httpContext.Response.StatusCode = StatusCodes.Status304NotModified;
                await DisposeContentAsync().ConfigureAwait(false);
                return;
            }

            _cacheService.ApplyCacheHeaders(httpContext, OgcResourceType.Tile, _etag, null);
            ApplyCdnHeaders(httpContext.Response);

            var response = httpContext.Response;
            response.ContentType = _contentType;

            if (_content.CanSeek)
            {
                _content.Seek(0, SeekOrigin.Begin);
                response.ContentLength = _content.Length;
            }

            try
            {
                await _content.CopyToAsync(response.Body, 81920, httpContext.RequestAborted).ConfigureAwait(false);
            }
            finally
            {
                await DisposeContentAsync().ConfigureAwait(false);
            }
        }

        private void ApplyCdnHeaders(HttpResponse response)
        {
            if (_cacheControlOverride.IsNullOrWhiteSpace())
            {
                return;
            }

            response.Headers.CacheControl = _cacheControlOverride;
            response.Headers.Vary = "Accept-Encoding";
        }

        private async ValueTask DisposeContentAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            switch (_content)
            {
                case IAsyncDisposable asyncDisposable:
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    break;
                default:
                    _content.Dispose();
                    break;
            }
        }
    }

    private static async ValueTask DisposeStreamAsync(Stream stream)
    {
        switch (stream)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                break;
            default:
                stream.Dispose();
                break;
        }
    }


    /// <summary>
    /// Gets the list of supported tile matrix sets.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="cacheHeaderService">Service for generating cache headers and ETags.</param>
    /// <returns>
    /// JSON response containing a list of supported tile matrix sets with their identifiers and URIs.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This endpoint is part of OGC API - Tiles v1.0 core conformance class.
    /// Returns metadata about all tile matrix sets supported by the server:
    /// </para>
    /// <list type="bullet">
    /// <item>WorldCRS84Quad - WGS84 in CRS84 axis order (longitude, latitude)</item>
    /// <item>WorldWebMercatorQuad - Web Mercator (EPSG:3857) for web mapping</item>
    /// </list>
    /// <para>
    /// Each tile matrix set includes its OGC URI, CRS identifier, and a self link
    /// to the full tile matrix set definition.
    /// </para>
    /// </remarks>
    public static IResult GetTileMatrixSets(HttpRequest request, [FromServices] OgcCacheHeaderService cacheHeaderService)
    {
        Guard.NotNull(request);

        var sets = new List<object>
        {
            OgcSharedHandlers.BuildTileMatrixSetSummary(request, OgcTileMatrixHelper.WorldCrs84QuadId, OgcTileMatrixHelper.WorldCrs84QuadUri, OgcTileMatrixHelper.WorldCrs84QuadCrs),
            OgcSharedHandlers.BuildTileMatrixSetSummary(request, OgcTileMatrixHelper.WorldWebMercatorQuadId, OgcTileMatrixHelper.WorldWebMercatorQuadUri, OgcTileMatrixHelper.WorldWebMercatorQuadCrs)
        };

        var links = new List<OgcLink>
        {
            OgcSharedHandlers.BuildLink(request, "/ogc/tileMatrixSets", "self", "application/json", "Tile matrix sets"),
            OgcSharedHandlers.BuildLink(request, "/ogc", "alternate", "application/json", "Landing")
        };

        var response = new { tileMatrixSets = sets, links };
        var etag = cacheHeaderService.GenerateETagForObject(response);
        return Results.Ok(response).WithTileMatrixSetCacheHeaders(cacheHeaderService, etag);
    }

    public static IResult GetTileMatrixSetDefinition(
        string tileMatrixSetId,
        HttpRequest request,
        [FromServices] OgcCacheHeaderService cacheHeaderService)
    {
        Guard.NotNull(request);

        var normalized = OgcSharedHandlers.NormalizeTileMatrixSet(tileMatrixSetId);
        if (normalized is null)
        {
            return Results.NotFound();
        }

        var (matrixId, matrixUri, matrixCrs) = normalized.Value;
        const int defaultMinZoom = 0;
        const int defaultMaxZoom = 14;
        var tileMatrices = OgcTileMatrixHelper.BuildTileMatrices(matrixId, defaultMinZoom, defaultMaxZoom);

        var links = new List<OgcLink>
        {
            OgcSharedHandlers.BuildLink(request, $"/ogc/tileMatrixSets/{matrixId}", "self", "application/json", $"{matrixId} definition")
        };

        var response = new
        {
            id = matrixId,
            title = matrixId,
            tileMatrixSetUri = matrixUri,
            crs = matrixCrs,
            dataType = "map",
            minZoom = defaultMinZoom,
            maxZoom = defaultMaxZoom,
            tileMatrices,
            links
        };
        var etag = cacheHeaderService.GenerateETagForObject(response);
        return Results.Ok(response).WithTileMatrixSetCacheHeaders(cacheHeaderService, etag);
    }

    /// <summary>
    /// Builds tile matrix set links with tileMatrixSetLimits conforming to OGC API - Tiles specification.
    /// </summary>
    private static IReadOnlyList<object> BuildTileMatrixSetLinksWithLimits(
        HttpRequest request,
        string collectionId,
        string tilesetId,
        int minZoom,
        int maxZoom,
        double[] bounds)
    {
        return new object[]
        {
            BuildTileMatrixSetLinkWithLimits(
                request,
                collectionId,
                tilesetId,
                OgcTileMatrixHelper.WorldCrs84QuadId,
                OgcTileMatrixHelper.WorldCrs84QuadUri,
                OgcTileMatrixHelper.WorldCrs84QuadCrs,
                minZoom,
                maxZoom,
                bounds),
            BuildTileMatrixSetLinkWithLimits(
                request,
                collectionId,
                tilesetId,
                OgcTileMatrixHelper.WorldWebMercatorQuadId,
                OgcTileMatrixHelper.WorldWebMercatorQuadUri,
                OgcTileMatrixHelper.WorldWebMercatorQuadCrs,
                minZoom,
                maxZoom,
                bounds)
        };
    }

    private static object BuildTileMatrixSetLinkWithLimits(
        HttpRequest request,
        string collectionId,
        string tilesetId,
        string matrixId,
        string matrixUri,
        string crs,
        int minZoom,
        int maxZoom,
        double[] bounds)
    {
        // Build tile matrix limits for each zoom level
        var limits = new List<object>(maxZoom - minZoom + 1);
        for (var zoom = minZoom; zoom <= maxZoom; zoom++)
        {
            var (minRow, maxRow, minCol, maxCol) = OgcTileMatrixHelper.GetTileRange(
                matrixId,
                zoom,
                bounds[0],
                bounds[1],
                bounds[2],
                bounds[3]);

            limits.Add(new
            {
                tileMatrix = zoom.ToString(CultureInfo.InvariantCulture),
                minTileRow = minRow,
                maxTileRow = maxRow,
                minTileCol = minCol,
                maxTileCol = maxCol
            });
        }

        return new
        {
            tileMatrixSet = matrixId,
            tileMatrixSetURI = matrixUri,
            crs,
            tileMatrixSetLimits = limits,
            href = OgcSharedHandlers.BuildHref(request, $"/ogc/collections/{collectionId}/tiles/{tilesetId}/{matrixId}", null, null)
        };
    }
}
