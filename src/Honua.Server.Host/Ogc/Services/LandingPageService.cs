// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Ogc.Services;

/// <summary>
/// Service for generating OGC API landing pages and API definitions.
/// Implements OGC API - Features Part 1 (Core) landing page requirements.
/// </summary>
internal interface IOgcLandingPageService
{
    /// <summary>
    /// Gets the OGC API landing page with catalog information and service links.
    /// Supports both JSON and HTML output formats.
    /// </summary>
    /// <param name="request">HTTP request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HTTP result containing the landing page response</returns>
    Task<IResult> GetLandingPageAsync(HttpRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the OpenAPI 3.0 definition for the OGC API.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HTTP result containing the API definition</returns>
    Task<IResult> GetApiDefinitionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of OGC API landing page service.
/// </summary>
internal sealed class LandingPageService : IOgcLandingPageService
{
    private readonly IMetadataRegistry metadataRegistry;
    private readonly OgcCacheHeaderService cacheHeaderService;
    private readonly IOgcFeaturesRenderingHandler renderingHandler;
    private readonly OgcApiDefinitionCache apiDefinitionCache;
    private readonly OgcLinkBuilder linkBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="LandingPageService"/> class.
    /// </summary>
    /// <param name="metadataRegistry">Metadata registry for accessing catalog and service information</param>
    /// <param name="cacheHeaderService">Cache header service for ETag generation</param>
    /// <param name="renderingHandler">HTML rendering handler for HTML output format</param>
    /// <param name="apiDefinitionCache">Cache for OpenAPI definition</param>
    /// <param name="linkBuilder">Link builder for generating HATEOAS links</param>
    public LandingPageService(
        IMetadataRegistry metadataRegistry,
        OgcCacheHeaderService cacheHeaderService,
        IOgcFeaturesRenderingHandler renderingHandler,
        OgcApiDefinitionCache apiDefinitionCache,
        OgcLinkBuilder linkBuilder)
    {
        this.metadataRegistry = Guard.NotNull(metadataRegistry);
        this.cacheHeaderService = Guard.NotNull(cacheHeaderService);
        this.renderingHandler = Guard.NotNull(renderingHandler);
        this.apiDefinitionCache = Guard.NotNull(apiDefinitionCache);
        this.linkBuilder = Guard.NotNull(linkBuilder);
    }

    /// <inheritdoc/>
    public async Task<IResult> GetLandingPageAsync(HttpRequest request, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);

        var snapshot = await this.metadataRegistry.GetInitializedSnapshotAsync(cancellationToken).ConfigureAwait(false);

        // Build links for the landing page
        var links = new List<OgcLink>(snapshot.Catalog.Links.Select(link => this.linkBuilder.ToLink(link)));
        links.AddRange(new[]
        {
            this.linkBuilder.BuildLink(request, "/ogc", "self", "application/json", "OGC API landing"),
            this.linkBuilder.BuildLink(request, "/ogc/conformance", "conformance", "application/json", "Conformance"),
            this.linkBuilder.BuildLink(request, "/ogc/collections", "data", "application/json", "Collections"),
            this.linkBuilder.BuildLink(request, "/ogc/api", "service-desc", "application/vnd.oai.openapi+json;version=3.0", "OGC API definition"),
            new OgcLink(
                "https://github.com/HonuaIO/HonuaIO/blob/master/docs/rag/03-01-ogc-api-features.md",
                "service-doc",
                "text/markdown",
                "OGC API documentation")
        });

        // Check if client wants HTML
        if (this.renderingHandler.WantsHtml(request))
        {
            var html = this.renderingHandler.RenderLandingHtml(request, snapshot, links);
            var htmlEtag = this.cacheHeaderService.GenerateETag(html);
            return Results.Content(html, OgcSharedHandlers.HtmlContentType)
                .WithMetadataCacheHeaders(this.cacheHeaderService, htmlEtag);
        }

        // Return JSON response
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

        var etag = this.cacheHeaderService.GenerateETagForObject(response);
        return Results.Ok(response)
            .WithMetadataCacheHeaders(this.cacheHeaderService, etag);
    }

    /// <inheritdoc/>
    public async Task<IResult> GetApiDefinitionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheEntry = await this.apiDefinitionCache.GetAsync(cancellationToken).ConfigureAwait(false);
            return Results
                .Content(cacheEntry.Payload, "application/vnd.oai.openapi+json;version=3.0")
                .WithCacheHeaders(
                    this.cacheHeaderService,
                    OgcResourceType.ApiDefinition,
                    cacheEntry.ETag,
                    cacheEntry.LastModified);
        }
        catch (FileNotFoundException ex)
        {
            return Results.Problem(
                ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Missing API definition");
        }
    }
}
