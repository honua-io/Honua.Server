// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Ogc.Services;

/// <summary>
/// Service for managing OGC API collections (feature layers).
/// Implements OGC API - Features Part 1 (Core) collections requirements.
/// </summary>
internal interface IOgcCollectionService
{
    /// <summary>
    /// Gets all available OGC collections across all enabled services.
    /// Supports both JSON and HTML output formats with response caching.
    /// </summary>
    /// <param name="request">HTTP request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HTTP result containing the collections response</returns>
    Task<IResult> GetCollectionsAsync(HttpRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metadata for a specific collection by its ID.
    /// </summary>
    /// <param name="collectionId">Collection identifier (format: serviceId::layerId)</param>
    /// <param name="request">HTTP request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HTTP result containing the collection metadata</returns>
    Task<IResult> GetCollectionAsync(
        string collectionId,
        HttpRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of OGC API collection service with caching support.
/// </summary>
internal sealed class CollectionService : IOgcCollectionService
{
    private readonly IMetadataRegistry metadataRegistry;
    private readonly IFeatureContextResolver contextResolver;
    private readonly OgcCacheHeaderService cacheHeaderService;
    private readonly IOgcFeaturesRenderingHandler renderingHandler;
    private readonly IOgcCollectionsCache collectionsCache;
    private readonly OgcLinkBuilder linkBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="CollectionService"/> class.
    /// </summary>
    /// <param name="metadataRegistry">Metadata registry for accessing service and layer definitions</param>
    /// <param name="contextResolver">Feature context resolver for collection lookup</param>
    /// <param name="cacheHeaderService">Cache header service for ETag generation</param>
    /// <param name="renderingHandler">HTML rendering handler for HTML output format</param>
    /// <param name="collectionsCache">Collections cache for response caching</param>
    /// <param name="linkBuilder">Link builder for generating HATEOAS links</param>
    public CollectionService(
        IMetadataRegistry metadataRegistry,
        IFeatureContextResolver contextResolver,
        OgcCacheHeaderService cacheHeaderService,
        IOgcFeaturesRenderingHandler renderingHandler,
        IOgcCollectionsCache collectionsCache,
        OgcLinkBuilder linkBuilder)
    {
        this.metadataRegistry = Guard.NotNull(metadataRegistry);
        this.contextResolver = Guard.NotNull(contextResolver);
        this.cacheHeaderService = Guard.NotNull(cacheHeaderService);
        this.renderingHandler = Guard.NotNull(renderingHandler);
        this.collectionsCache = Guard.NotNull(collectionsCache);
        this.linkBuilder = Guard.NotNull(linkBuilder);
    }

    /// <inheritdoc/>
    public async Task<IResult> GetCollectionsAsync(HttpRequest request, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);

        // Extract Accept-Language for i18n support
        var acceptLanguage = request.Headers.AcceptLanguage.ToString();

        // Determine response format
        var wantsHtml = this.renderingHandler.WantsHtml(request);
        var format = wantsHtml ? "html" : "json";

        // Try to get from cache
        if (this.collectionsCache.TryGetCollections(null, format, acceptLanguage, out var cachedEntry) && cachedEntry != null)
        {
            return Results.Content(cachedEntry.Content, cachedEntry.ContentType)
                .WithMetadataCacheHeaders(this.cacheHeaderService, cachedEntry.ETag);
        }

        // Cache miss - generate response
        var snapshot = await this.metadataRegistry.GetInitializedSnapshotAsync(cancellationToken).ConfigureAwait(false);
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
                    links = this.linkBuilder.BuildCollectionLinks(request, service, layer, collectionId),
                    storageCrs = OgcSharedHandlers.DetermineStorageCrs(layer),
                    defaultStyle = layer.DefaultStyleId.IsNullOrWhiteSpace() ? null : layer.DefaultStyleId,
                    styleIds,
                    minScale = layer.MinScale,
                    maxScale = layer.MaxScale
                });
            }
        }

        var responseLinks = new List<OgcLink>
        {
            this.linkBuilder.BuildLink(request, "/ogc/collections", "self", "application/json", "Collections"),
            this.linkBuilder.BuildLink(request, "/ogc", "alternate", "application/json", "Landing")
        };

        // Generate and cache response based on format
        if (wantsHtml)
        {
            var html = this.renderingHandler.RenderCollectionsHtml(request, snapshot, summaries);
            var htmlEtag = this.cacheHeaderService.GenerateETag(html);

            // Cache the HTML response
            await this.collectionsCache.SetCollectionsAsync(
                null,
                "html",
                acceptLanguage,
                html,
                OgcSharedHandlers.HtmlContentType,
                htmlEtag,
                cancellationToken).ConfigureAwait(false);

            return Results.Content(html, OgcSharedHandlers.HtmlContentType)
                .WithMetadataCacheHeaders(this.cacheHeaderService, htmlEtag);
        }

        var response = new { collections, links = responseLinks };
        var etag = this.cacheHeaderService.GenerateETagForObject(response);

        // Serialize to JSON for caching
        var jsonContent = JsonSerializer.Serialize(response, JsonSerializerOptionsRegistry.Web);

        // Cache the JSON response
        await this.collectionsCache.SetCollectionsAsync(
            null,
            "json",
            acceptLanguage,
            jsonContent,
            "application/json",
            etag,
            cancellationToken).ConfigureAwait(false);

        return Results.Ok(response)
            .WithMetadataCacheHeaders(this.cacheHeaderService, etag);
    }

    /// <inheritdoc/>
    public async Task<IResult> GetCollectionAsync(
        string collectionId,
        HttpRequest request,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);

        // First, try to resolve as a regular layer collection
        var result = await OgcSharedHandlers.ResolveCollectionAsync(collectionId, this.contextResolver, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            var context = result.Value;
            var service = context.Service;
            var layer = context.Layer;
            var id = OgcSharedHandlers.BuildCollectionId(service, layer);
            var crs = layer.Crs.Count > 0 ? layer.Crs : OgcSharedHandlers.BuildDefaultCrs(service);
            var links = this.linkBuilder.BuildCollectionLinks(request, service, layer, id);
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

            if (this.renderingHandler.WantsHtml(request))
            {
                var html = OgcSharedHandlers.RenderCollectionHtml(request, service, layer, id, crs, links);
                var htmlEtag = this.cacheHeaderService.GenerateETag(html);
                return Results.Content(html, OgcSharedHandlers.HtmlContentType)
                    .WithMetadataCacheHeaders(this.cacheHeaderService, htmlEtag);
            }

            var etag = this.cacheHeaderService.GenerateETagForObject(response);
            return Results.Ok(response)
                .WithMetadataCacheHeaders(this.cacheHeaderService, etag);
        }

        // For now, return the original error since layer groups are not yet fully supported
        return OgcSharedHandlers.MapCollectionResolutionError(result.Error!, collectionId);
    }
}
