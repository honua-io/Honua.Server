// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// OGC API landing page handlers implemented with the async minimal API pattern to keep request threads unblocked.
/// </summary>
internal static class OgcLandingHandlers
{
    /// <summary>
    /// OGC API landing page handler.
    /// </summary>
    public static async Task<IResult> GetLanding(HttpRequest request, [FromServices] IMetadataRegistry registry, [FromServices] OgcCacheHeaderService cacheHeaderService, [FromServices] Services.IOgcFeaturesRenderingHandler renderingHandler, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        Guard.NotNull(registry);

        var snapshot = await registry.GetInitializedSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var links = new List<OgcLink>(snapshot.Catalog.Links.Select(OgcSharedHandlers.ToLink));
        links.AddRange(new[]
        {
            OgcSharedHandlers.BuildLink(request, "/ogc", "self", "application/json", "OGC API landing"),

            OgcSharedHandlers.BuildLink(request, "/ogc/conformance", "conformance", "application/json", "Conformance"),

            OgcSharedHandlers.BuildLink(request, "/ogc/collections", "data", "application/json", "Collections"),

            OgcSharedHandlers.BuildLink(request, "/ogc/api", "service-desc", "application/vnd.oai.openapi+json;version=3.0", "OGC API definition"),

            new OgcLink(
                "https://github.com/HonuaIO/HonuaIO/blob/master/docs/rag/03-01-ogc-api-features.md",
                "service-doc",
                "text/markdown",
                "OGC API documentation")
        });

        if (renderingHandler.WantsHtml(request))
        {
            var html = renderingHandler.RenderLandingHtml(request, snapshot, links);
            var htmlEtag = cacheHeaderService.GenerateETag(html);
            return Results.Content(html, OgcSharedHandlers.HtmlContentType)
                .WithMetadataCacheHeaders(cacheHeaderService, htmlEtag);
        }

        var response = new
        {
            catalog = new
            {
                id = snapshot.Catalog.Id,
                title = snapshot.Catalog.Title,
                description = snapshot.Catalog.Description,
                version = snapshot.Catalog.Version
            },
            services = snapshot.Services.Select(service => new
            {
                id = service.Id,
                title = service.Title,
                folderId = service.FolderId,
                serviceType = service.ServiceType
            }).ToArray(),
            title = snapshot.Catalog.Title ?? snapshot.Catalog.Id,
            description = snapshot.Catalog.Description,
            keywords = snapshot.Catalog.Keywords,
            links
        };
        var etag = cacheHeaderService.GenerateETagForObject(response);
        return Results.Ok(response).WithMetadataCacheHeaders(cacheHeaderService, etag);
    }

    public static async Task<IResult> GetApiDefinition(
        [FromServices] OgcApiDefinitionCache definitionCache,
        [FromServices] OgcCacheHeaderService cacheHeaderService,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(definitionCache);
        Guard.NotNull(cacheHeaderService);

        try
        {
            var cacheEntry = await definitionCache.GetAsync(cancellationToken).ConfigureAwait(false);
            return Results
                .Content(cacheEntry.Payload, "application/vnd.oai.openapi+json;version=3.0")
                .WithCacheHeaders(cacheHeaderService, OgcResourceType.ApiDefinition, cacheEntry.ETag, cacheEntry.LastModified);
        }
        catch (FileNotFoundException ex)
        {
            return Results.Problem(
                ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Missing API definition");
        }
    }

    /// <summary>
    /// OGC API conformance handler.
    /// </summary>
    public static async Task<IResult> GetConformance(HttpRequest request, IMetadataRegistry registry, OgcCacheHeaderService cacheHeaderService, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(registry);

        var snapshot = await registry.GetInitializedSnapshotAsync(cancellationToken).ConfigureAwait(false);

        var classes = new HashSet<string>(OgcSharedHandlers.DefaultConformanceClasses);
        foreach (var service in snapshot.Services)
        {
            foreach (var conformance in service.Ogc.ConformanceClasses ?? Array.Empty<string>())
            {
                if (conformance.HasValue())
                {
                    classes.Add(conformance);
                }
            }
        }

        var response = new { conformsTo = classes };
        var etag = cacheHeaderService.GenerateETagForObject(response);
        return Results.Ok(response).WithMetadataCacheHeaders(cacheHeaderService, etag);
    }

    /// <summary>
    /// OGC API collections handler.
    /// </summary>
    public static async Task<IResult> GetCollections(HttpRequest request, IMetadataRegistry registry, OgcCacheHeaderService cacheHeaderService, Services.IOgcFeaturesRenderingHandler renderingHandler, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        Guard.NotNull(registry);

        var snapshot = await registry.GetInitializedSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var collections = new List<object>();
        var summaries = new List<OgcSharedHandlers.CollectionSummary>();

        foreach (var service in snapshot.Services)
        {
            if (!service.Enabled || !service.Ogc.CollectionsEnabled)
            {
                continue;
            }

            foreach (var layer in service.Layers)
            {
                var collectionId = OgcSharedHandlers.BuildCollectionId(service, layer);
                var styleIds = OgcSharedHandlers.BuildOrderedStyleIds(layer);
                var crs = layer.Crs.Count > 0
                    ? layer.Crs
                    : OgcSharedHandlers.BuildDefaultCrs(service);

                summaries.Add(new OgcSharedHandlers.CollectionSummary(
                    collectionId,
                    layer.Title,
                    layer.Description,
                    layer.ItemType,
                    crs,
                    OgcSharedHandlers.DetermineStorageCrs(layer)));

                collections.Add(new
                {
                    id = collectionId,
                    title = layer.Title,
                    description = layer.Description,
                    itemType = layer.ItemType,
                    extent = OgcSharedHandlers.ConvertExtent(layer.Extent),
                    crs,
                    keywords = layer.Keywords,
                    links = OgcSharedHandlers.BuildCollectionLinks(request, service, layer, collectionId),
                    storageCrs = OgcSharedHandlers.DetermineStorageCrs(layer),
                    defaultStyle = layer.DefaultStyleId.IsNullOrWhiteSpace() ? null : layer.DefaultStyleId,
                    styleIds,
                    minScale = layer.MinScale,
                    maxScale = layer.MaxScale
                });
            }

            // TODO: Add layer groups as collections (implementation incomplete)
            /*
            foreach (var group in snapshot.LayerGroups.Where(g => g.ServiceId == service.Id && g.Enabled))
            {
                var collectionId = OgcSharedHandlers.BuildLayerGroupCollectionId(service, group);
                var styleIds = OgcSharedHandlers.BuildOrderedStyleIds(group);
                var groupExtent = LayerGroupExpander.CalculateGroupExtent(group, snapshot);
                var groupCrs = LayerGroupExpander.GetSupportedCrs(group, snapshot);
                var crs = groupCrs.Count > 0
                    ? groupCrs
                    : OgcSharedHandlers.BuildDefaultCrs(service);

                summaries.Add(new OgcSharedHandlers.CollectionSummary(
                    collectionId,
                    group.Title,
                    group.Description,
                    "feature", // Layer groups contain features
                    crs,
                    null)); // Layer groups don't have a single storage CRS

                collections.Add(new
                {
                    id = collectionId,
                    title = group.Title,
                    description = group.Description,
                    itemType = "feature",
                    type = "layerGroup", // Mark as layer group
                    extent = OgcSharedHandlers.ConvertExtent(groupExtent),
                    crs,
                    keywords = group.Keywords,
                    links = OgcSharedHandlers.BuildLayerGroupCollectionLinks(request, service, group, collectionId),
                    defaultStyle = group.DefaultStyleId.IsNullOrWhiteSpace() ? null : group.DefaultStyleId,
                    styleIds,
                    minScale = group.MinScale,
                    maxScale = group.MaxScale,
                    members = group.Members.Select(m => new
                    {
                        type = m.Type.ToString(),
                        layerId = m.LayerId,
                        groupId = m.GroupId,
                        order = m.Order,
                        opacity = m.Opacity
                    })
                });
            }
            */
        }

        var responseLinks = new List<OgcLink>
        {
            OgcSharedHandlers.BuildLink(request, "/ogc/collections", "self", "application/json", "Collections"),
            OgcSharedHandlers.BuildLink(request, "/ogc", "alternate", "application/json", "Landing")
        };

        if (renderingHandler.WantsHtml(request))
        {
            var html = renderingHandler.RenderCollectionsHtml(request, snapshot, summaries);
            var htmlEtag = cacheHeaderService.GenerateETag(html);
            return Results.Content(html, OgcSharedHandlers.HtmlContentType)
                .WithMetadataCacheHeaders(cacheHeaderService, htmlEtag);
        }

        var response = new { collections, links = responseLinks };
        var etag = cacheHeaderService.GenerateETagForObject(response);
        return Results.Ok(response).WithMetadataCacheHeaders(cacheHeaderService, etag);
    }

    public static async Task<IResult> GetCollection(
        string collectionId,
        HttpRequest request,
        IFeatureContextResolver resolver,
        IMetadataRegistry metadataRegistry,
        OgcCacheHeaderService cacheHeaderService,
        Services.IOgcFeaturesRenderingHandler renderingHandler,
        CancellationToken cancellationToken)
    {
        // First, try to resolve as a regular layer collection
        var result = await OgcSharedHandlers.ResolveCollectionAsync(collectionId, resolver, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            var context = result.Value;
            var service = context.Service;
            var layer = context.Layer;
            var id = OgcSharedHandlers.BuildCollectionId(service, layer);
            var crs = layer.Crs.Count > 0 ? layer.Crs : OgcSharedHandlers.BuildDefaultCrs(service);
            var links = OgcSharedHandlers.BuildCollectionLinks(request, service, layer, id);
            var styleIds = OgcSharedHandlers.BuildOrderedStyleIds(layer);
            var response = new
            {
                id,
                title = layer.Title,
                description = layer.Description,
                itemType = layer.ItemType,
                extent = OgcSharedHandlers.ConvertExtent(layer.Extent),
                crs,
                keywords = layer.Keywords,
                links,
                storageCrs = OgcSharedHandlers.DetermineStorageCrs(layer),
                defaultStyle = layer.DefaultStyleId.IsNullOrWhiteSpace() ? null : layer.DefaultStyleId,
                styleIds,
                minScale = layer.MinScale,
                maxScale = layer.MaxScale
            };

            if (renderingHandler.WantsHtml(request))
            {
                var html = OgcSharedHandlers.RenderCollectionHtml(request, service, layer, id, crs, links);
                var htmlEtag = cacheHeaderService.GenerateETag(html);
                return Results.Content(html, OgcSharedHandlers.HtmlContentType)
                    .WithMetadataCacheHeaders(cacheHeaderService, htmlEtag);
            }

            var etag = cacheHeaderService.GenerateETagForObject(response);
            return Results.Ok(response).WithMetadataCacheHeaders(cacheHeaderService, etag);
        }

        // TODO: Re-enable layer group collection support when helper methods are implemented
        // The following code references BuildLayerGroupCollectionId, BuildLayerGroupCollectionLinks,
        // and RenderLayerGroupCollectionHtml which are not yet implemented
        /*
        // If not found as a layer, try to resolve as a layer group
        var snapshot = await metadataRegistry.GetInitializedSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var parts = collectionId.Split(new[] { "::" }, StringSplitOptions.None);
        if (parts.Length != 2)
        {
            return OgcSharedHandlers.MapCollectionResolutionError(result.Error!, collectionId);
        }

        var serviceId = parts[0];
        var layerGroupId = parts[1];

        if (!snapshot.TryGetService(serviceId, out var groupService))
        {
            return OgcSharedHandlers.MapCollectionResolutionError(result.Error!, collectionId);
        }

        if (!snapshot.TryGetLayerGroup(serviceId, layerGroupId, out var layerGroup))
        {
            return OgcSharedHandlers.MapCollectionResolutionError(result.Error!, collectionId);
        }

        // Build layer group collection response
        var groupId = OgcSharedHandlers.BuildLayerGroupCollectionId(groupService, layerGroup);
        var groupStyleIds = OgcSharedHandlers.BuildOrderedStyleIds(layerGroup);
        var groupExtent = LayerGroupExpander.CalculateGroupExtent(layerGroup, snapshot);
        var groupCrs = LayerGroupExpander.GetSupportedCrs(layerGroup, snapshot);
        var groupCrsResult = groupCrs.Count > 0 ? groupCrs : OgcSharedHandlers.BuildDefaultCrs(groupService);
        var groupLinks = OgcSharedHandlers.BuildLayerGroupCollectionLinks(request, groupService, layerGroup, groupId);

        var groupResponse = new
        {
            id = groupId,
            title = layerGroup.Title,
            description = layerGroup.Description,
            itemType = "feature",
            type = "layerGroup",
            extent = OgcSharedHandlers.ConvertExtent(groupExtent),
            crs = groupCrsResult,
            keywords = layerGroup.Keywords,
            links = groupLinks,
            defaultStyle = layerGroup.DefaultStyleId.IsNullOrWhiteSpace() ? null : layerGroup.DefaultStyleId,
            styleIds = groupStyleIds,
            minScale = layerGroup.MinScale,
            maxScale = layerGroup.MaxScale,
            members = layerGroup.Members.Select(m => new
            {
                type = m.Type.ToString(),
                layerId = m.LayerId,
                groupId = m.GroupId,
                order = m.Order,
                opacity = m.Opacity
            })
        };

        if (renderingHandler.WantsHtml(request))
        {
            var html = OgcSharedHandlers.RenderLayerGroupCollectionHtml(request, groupService, layerGroup, groupId, groupCrsResult, groupLinks);
            var htmlEtag = cacheHeaderService.GenerateETag(html);
            return Results.Content(html, OgcSharedHandlers.HtmlContentType)
                .WithMetadataCacheHeaders(cacheHeaderService, htmlEtag);
        }

        var groupEtag = cacheHeaderService.GenerateETagForObject(groupResponse);
        return Results.Ok(groupResponse).WithMetadataCacheHeaders(cacheHeaderService, groupEtag);
        */

        // For now, return the original error since layer groups are not yet fully supported
        return OgcSharedHandlers.MapCollectionResolutionError(result.Error!, collectionId);
    }
}
