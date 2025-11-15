// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Caching;
using Honua.Server.Core.Raster.Export;
using Honua.Server.Core.Raster.Rendering;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Data;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Honua.Server.Host.Raster;
using Honua.Server.Host.Utilities;
using Honua.Server.Host.Ogc.ParameterObjects;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Extension methods for mapping OGC API endpoints.
/// </summary>
internal static class OgcApiEndpointExtensions
{
    /// <summary>
    /// Maps all OGC API endpoints including landing pages, collections, features, tiles, and styles.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    /// <remarks>
    /// Configures endpoints for:
    /// - OGC API Landing pages and conformance
    /// - OGC API Features (collections, items, queryables)
    /// - OGC API Tiles (tile matrix sets, tile endpoints)
    /// - OGC API Styles (style management and versioning)
    /// - Search capabilities across collections
    ///
    /// All endpoints require viewer authorization unless otherwise specified.
    /// Write operations (POST, PUT, PATCH, DELETE) require data publisher authorization.
    /// </remarks>
    public static IEndpointRouteBuilder MapOgcApi(this IEndpointRouteBuilder endpoints)
    {
        Guard.NotNull(endpoints);

        // Require viewer access by default; selectively relax for spec-mandated public endpoints.
        var group = endpoints
            .MapGroup("/ogc");

        // Only require authorization if authentication is enforced
        var configuration = endpoints.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        var authEnforced = configuration.GetValue<bool>("honua:authentication:enforce", true);

        if (authEnforced)
        {
            group.RequireAuthorization("RequireViewer");
        }

        // Landing and Common endpoints - MUST be public per OGC API specification
        group.MapGet("/", OgcLandingHandlers.GetLanding).AllowAnonymous();
        group.MapGet("/conformance", OgcLandingHandlers.GetConformance).AllowAnonymous();
        group.MapGet("/api", OgcLandingHandlers.GetApiDefinition).AllowAnonymous();
        group.MapGet("/collections", OgcLandingHandlers.GetCollections).AllowAnonymous();
        group.MapGet("/collections/{collectionId}", OgcLandingHandlers.GetCollection).AllowAnonymous();

        // Feature endpoints
        group.MapGet("/collections/{collectionId}/styles", OgcFeaturesHandlers.GetCollectionStyles);
        group.MapGet("/collections/{collectionId}/styles/{styleId}", OgcFeaturesHandlers.GetCollectionStyle);
        // BUG FIX #44: Allow anonymous access to items endpoint for public read access
        group.MapGet("/collections/{collectionId}/items", OgcFeaturesHandlers.GetCollectionItems).AllowAnonymous();
        group.MapGet("/collections/{collectionId}/items/{featureId}", OgcFeaturesHandlers.GetCollectionItem);
        group.MapGet("/collections/{collectionId}/items/{featureId}/attachments/{attachmentId}", OgcFeaturesHandlers.GetCollectionItemAttachment);
        group.MapGet("/collections/{collectionId}/queryables", OgcFeaturesHandlers.GetCollectionQueryables);
        group.MapPost("/collections/{collectionId}/items", OgcFeaturesHandlers.PostCollectionItems).RequireAuthorization("RequireDataPublisher");
        group.MapPut("/collections/{collectionId}/items/{featureId}", OgcFeaturesHandlers.PutCollectionItem).RequireAuthorization("RequireDataPublisher");
        group.MapPatch("/collections/{collectionId}/items/{featureId}", OgcFeaturesHandlers.PatchCollectionItem).RequireAuthorization("RequireDataPublisher");
        group.MapDelete("/collections/{collectionId}/items/{featureId}", OgcFeaturesHandlers.DeleteCollectionItem).RequireAuthorization("RequireDataPublisher");

        // Tile endpoints - OGC API Tiles specification compliant
        group.MapGet("/tileMatrixSets", OgcTilesHandlers.GetTileMatrixSets);
        group.MapGet("/tileMatrixSets/{tileMatrixSetId}", OgcTilesHandlers.GetTileMatrixSetDefinition);

        // Standard OGC API - Tiles URL patterns (spec-compliant)
        group.MapGet("/collections/{collectionId}/tiles", OgcTilesHandlers.GetCollectionTileSets);
        group.MapGet("/collections/{collectionId}/tiles/{tileMatrixSetId}/{tileMatrix}/{tileRow:int}/{tileCol:int}", OgcTilesHandlers.GetCollectionTileStandard);

        // Legacy URL patterns with {tilesetId} (maintained for backward compatibility, marked as deprecated)
        group.MapGet("/collections/{collectionId}/tiles/{tilesetId}", OgcTilesHandlers.GetCollectionTileSet)
            .WithMetadata(new DeprecatedEndpointMetadata("Use /collections/{collectionId}/tiles instead. This endpoint will be removed in a future version."));
        group.MapGet("/collections/{collectionId}/tiles/{tilesetId}/tilejson", OgcTilesHandlers.GetCollectionTileJson)
            .WithMetadata(new DeprecatedEndpointMetadata("Legacy TileJSON endpoint. Consider using standard OGC API - Tiles metadata instead."));
        group.MapGet("/collections/{collectionId}/tiles/{tilesetId}/{tileMatrixSetId}", OgcTilesHandlers.GetCollectionTileMatrixSet)
            .WithMetadata(new DeprecatedEndpointMetadata("Use /tileMatrixSets/{tileMatrixSetId} for tile matrix set definitions."));
        group.MapGet("/collections/{collectionId}/tiles/{tilesetId}/{tileMatrixSetId}/{tileMatrix}/{tileRow:int}/{tileCol:int}",
            async (
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
                CancellationToken cancellationToken) =>
            {
                var coordinates = new TileCoordinates
                {
                    CollectionId = collectionId,
                    TilesetId = tilesetId,
                    TileMatrixSetId = tileMatrixSetId,
                    TileMatrix = tileMatrix,
                    TileRow = tileRow,
                    TileCol = tileCol
                };

                var operationContext = new TileOperationContext
                {
                    Request = request
                };

                var resolutionServices = new TileResolutionServices
                {
                    ContextResolver = resolver,
                    RasterRegistry = rasterRegistry,
                    MetadataRegistry = metadataRegistry,
                    Repository = repository
                };

                var renderingServices = new TileRenderingServices
                {
                    Renderer = rasterRenderer,
                    PMTilesExporter = pmTilesExporter
                };

                var cachingServices = new TileCachingServices
                {
                    CacheProvider = tileCacheProvider,
                    CacheMetrics = tileCacheMetrics,
                    CacheHeaders = cacheHeaderService
                };

                return await OgcTilesHandlers.GetCollectionTile(
                    coordinates,
                    operationContext,
                    resolutionServices,
                    renderingServices,
                    cachingServices,
                    tilesHandler,
                    cancellationToken);
            })
            .WithMetadata(new DeprecatedEndpointMetadata("Use /collections/{collectionId}/tiles/{tileMatrixSetId}/{tileMatrix}/{tileRow}/{tileCol} instead."));

        // Search endpoints
        group.MapGet("/search", OgcFeaturesHandlers.GetSearch);
        group.MapPost("/search", OgcFeaturesHandlers.PostSearch);

        // Style endpoints (read)
        group.MapGet("/styles", OgcFeaturesHandlers.GetStyles);
        group.MapGet("/styles/{styleId}", OgcFeaturesHandlers.GetStyle);

        // Style CRUD endpoints (write operations require authentication)
        group.MapPost("/styles", OgcStylesHandlers.CreateStyle).RequireAuthorization("RequireDataPublisher");
        group.MapPut("/styles/{styleId}", OgcStylesHandlers.UpdateStyle).RequireAuthorization("RequireDataPublisher");
        group.MapDelete("/styles/{styleId}", OgcStylesHandlers.DeleteStyle).RequireAuthorization("RequireDataPublisher");

        // Style versioning endpoints
        group.MapGet("/styles/{styleId}/history", OgcStylesHandlers.GetStyleHistory);
        group.MapGet("/styles/{styleId}/versions/{version:int}", OgcStylesHandlers.GetStyleVersion);

        // Style validation endpoint
        group.MapPost("/styles/validate", OgcStylesHandlers.ValidateStyle);

        group.MapGet("/{serviceId}/collections", (string serviceId, HttpRequest request, [FromServices] ICatalogProjectionService catalog) =>
            BuildLegacyCollectionsResponse(serviceId, request, catalog));

        group.MapGet("/{serviceId}/collections/{layerId}", (string serviceId, string layerId, HttpRequest request, [FromServices] ICatalogProjectionService catalog) =>
            BuildLegacyCollectionResponse(serviceId, layerId, request, catalog));

        // BUG FIX #45: Allow anonymous access to legacy items endpoint for existing public clients
        group.MapGet("/{serviceId}/collections/{layerId}/items",
            (string serviceId,
             string layerId,
             HttpRequest request,
             [FromServices] ICatalogProjectionService catalog,
             [FromServices] IFeatureContextResolver resolver,
             [FromServices] IFeatureRepository repository,
             [FromServices] IGeoPackageExporter geoPackageExporter,
             [FromServices] IShapefileExporter shapefileExporter,
             [FromServices] IFlatGeobufExporter flatGeobufExporter,
             [FromServices] IGeoArrowExporter geoArrowExporter,
             [FromServices] ICsvExporter csvExporter,
             [FromServices] IFeatureAttachmentOrchestrator attachmentOrchestrator,
             [FromServices] IMetadataRegistry metadataRegistry,
             [FromServices] IApiMetrics apiMetrics,
             [FromServices] OgcCacheHeaderService cacheHeaderService,
             [FromServices] Services.IOgcFeaturesAttachmentHandler attachmentHandler,
             [FromServices] Honua.Server.Core.Elevation.IElevationService elevationService,
             [FromServices] ILogger logger,
             CancellationToken cancellationToken) =>
        {
            // Construct parameter objects from individual dependencies
            var collectionIdentity = new LegacyCollectionIdentity
            {
                ServiceId = serviceId,
                LayerId = layerId
            };

            var requestContext = new LegacyRequestContext
            {
                Request = request
            };

            var catalogServices = new LegacyCatalogServices
            {
                Catalog = catalog
            };

            var exportServices = new OgcFeatureExportServices
            {
                GeoPackage = geoPackageExporter,
                Shapefile = shapefileExporter,
                FlatGeobuf = flatGeobufExporter,
                GeoArrow = geoArrowExporter,
                Csv = csvExporter
            };

            var attachmentServices = new OgcFeatureAttachmentServices
            {
                Orchestrator = attachmentOrchestrator,
                Handler = attachmentHandler
            };

            var enrichmentServices = new OgcFeatureEnrichmentServices
            {
                Elevation = elevationService
            };

            var observabilityServices = new LegacyObservabilityServices
            {
                Metrics = apiMetrics,
                CacheHeaders = cacheHeaderService
            };

            return BuildLegacyCollectionItemsResponse(
                collectionIdentity,
                requestContext,
                catalogServices,
                resolver,
                repository,
                metadataRegistry,
                exportServices,
                attachmentServices,
                enrichmentServices,
                observabilityServices,
                logger,
                cancellationToken);
        }).AllowAnonymous();

        return endpoints;
    }

    /// <summary>
    /// Handles legacy collection items endpoint using service/layer identifiers.
    /// Maintains backward compatibility with pre-OGC API URL patterns.
    /// </summary>
    /// <param name="collectionIdentity">Legacy service and layer identifiers.</param>
    /// <param name="requestContext">HTTP request context.</param>
    /// <param name="catalogServices">Catalog projection services for legacy lookups.</param>
    /// <param name="contextResolver">Feature context resolver.</param>
    /// <param name="repository">Feature data repository.</param>
    /// <param name="metadataRegistry">Feature metadata registry.</param>
    /// <param name="exportServices">Feature export format services.</param>
    /// <param name="attachmentServices">Feature attachment services.</param>
    /// <param name="enrichmentServices">Optional feature enrichment services.</param>
    /// <param name="observabilityServices">Metrics and caching services.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Feature collection response or NotFound if service/layer doesn't exist.</returns>
    internal static Task<IResult> BuildLegacyCollectionItemsResponse(
        LegacyCollectionIdentity collectionIdentity,
        LegacyRequestContext requestContext,
        [FromServices] LegacyCatalogServices catalogServices,
        [FromServices] IFeatureContextResolver contextResolver,
        [FromServices] IFeatureRepository repository,
        [FromServices] IMetadataRegistry metadataRegistry,
        [FromServices] OgcFeatureExportServices exportServices,
        [FromServices] OgcFeatureAttachmentServices attachmentServices,
        [FromServices] OgcFeatureEnrichmentServices enrichmentServices,
        [FromServices] LegacyObservabilityServices observabilityServices,
        [FromServices] ILogger logger,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(catalogServices?.Catalog);

        // Validate service and layer exist
        var serviceView = catalogServices.Catalog.GetService(collectionIdentity.ServiceId);
        var layerView = serviceView?.Layers.FirstOrDefault(l =>
            string.Equals(l.Layer.Id, collectionIdentity.LayerId, StringComparison.OrdinalIgnoreCase));
        if (layerView is null)
        {
            return Task.FromResult<IResult>(Results.NotFound());
        }

        var service = serviceView.Service;
        var layer = layerView.Layer;
        var collectionId = OgcSharedHandlers.BuildCollectionId(service, layer);

        // Forward to GetCollectionItems using canonical collection identifier
        return OgcFeaturesHandlers.GetCollectionItems(
            collectionId,
            requestContext.Request,
            contextResolver,
            repository,
            exportServices.GeoPackage,
            exportServices.Shapefile,
            exportServices.FlatGeobuf,
            exportServices.GeoArrow,
            exportServices.Csv,
            attachmentServices.Orchestrator,
            metadataRegistry,
            observabilityServices.Metrics,
            observabilityServices.CacheHeaders,
            attachmentServices.Handler,
            enrichmentServices.Elevation!,
            logger,
            cancellationToken);
    }

    // BUG FIX #5: Legacy OGC collections links emit relative URLs
    // Accept HttpRequest parameter and use RequestLinkHelper to generate absolute URLs
    // BUG FIX: Preserve API version prefix in legacy collection links
    private static IResult BuildLegacyCollectionsResponse(string serviceId, HttpRequest request, ICatalogProjectionService catalog)
    {
        Guard.NotNull(request);
        Guard.NotNull(catalog);

        var serviceView = catalog.GetService(serviceId);
        if (serviceView is null)
        {
            return Results.NotFound();
        }

        var service = serviceView.Service;
        if (!service.Enabled || !service.Ogc.CollectionsEnabled)
        {
            return Results.NotFound();
        }

        // Detect API version prefix from request path (e.g., /v1/ogc/... -> /v1)
        var requestPath = request.Path.Value ?? string.Empty;
        var ogcBasePath = "/ogc";
        if (requestPath.StartsWith("/v", StringComparison.OrdinalIgnoreCase))
        {
            var firstSegmentEnd = requestPath.IndexOf('/', 1);
            if (firstSegmentEnd > 0)
            {
                var versionPrefix = requestPath.Substring(0, firstSegmentEnd);
                ogcBasePath = versionPrefix + ogcBasePath;
            }
        }

        var collections = serviceView.Layers
            .Select(layer => new
            {
                id = layer.Layer.Id,
                title = layer.Layer.Title,
                description = layer.Layer.Description,
                geometryType = layer.Layer.GeometryType,
                crs = layer.Layer.Crs,
                links = new[]
                {
                    new { href = request.BuildAbsoluteUrl($"{ogcBasePath}/{service.Id}/collections/{layer.Layer.Id}"), rel = "self", type = "application/json" },
                    new { href = request.BuildAbsoluteUrl($"{ogcBasePath}/{service.Id}/collections/{layer.Layer.Id}/items"), rel = "items", type = "application/geo+json" }
                }
            })
            .ToArray();

        return Results.Ok(new { collections });
    }

    // BUG FIX #6: Legacy collection detail response also uses relative links
    // Pass HttpRequest and use RequestLinkHelper for fully-qualified URLs
    // BUG FIX: Preserve API version prefix in legacy collection links
    private static IResult BuildLegacyCollectionResponse(string serviceId, string layerId, HttpRequest request, ICatalogProjectionService catalog)
    {
        Guard.NotNull(request);
        Guard.NotNull(catalog);

        var serviceView = catalog.GetService(serviceId);
        var layerView = serviceView?.Layers.FirstOrDefault(l => string.Equals(l.Layer.Id, layerId, StringComparison.OrdinalIgnoreCase));
        if (layerView is null)
        {
            return Results.NotFound();
        }

        // Detect API version prefix from request path (e.g., /v1/ogc/... -> /v1)
        var requestPath = request.Path.Value ?? string.Empty;
        var ogcBasePath = "/ogc";
        if (requestPath.StartsWith("/v", StringComparison.OrdinalIgnoreCase))
        {
            var firstSegmentEnd = requestPath.IndexOf('/', 1);
            if (firstSegmentEnd > 0)
            {
                var versionPrefix = requestPath.Substring(0, firstSegmentEnd);
                ogcBasePath = versionPrefix + ogcBasePath;
            }
        }

        var layer = layerView.Layer;
        var collection = new
        {
            id = layer.Id,
            title = layer.Title,
            description = layer.Description,
            geometryType = layer.GeometryType,
            crs = layer.Crs,
            links = new[]
            {
                new { href = request.BuildAbsoluteUrl($"{ogcBasePath}/{serviceId}/collections/{layer.Id}"), rel = "self", type = "application/json" },
                new { href = request.BuildAbsoluteUrl($"{ogcBasePath}/{serviceId}/collections/{layer.Id}/items"), rel = "items", type = "application/geo+json" }
            }
        };

        return Results.Ok(collection);
    }
}
