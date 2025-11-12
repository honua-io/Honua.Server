// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Services.Stac;

/// <summary>
/// STAC Service Plugin implementing SpatioTemporal Asset Catalog API.
/// Dynamically loadable plugin that integrates with Configuration V2.
/// </summary>
public sealed class StacServicePlugin : IServicePlugin
{
    // IHonuaPlugin implementation
    public string Id => "honua.services.stac";
    public string Name => "STAC Service Plugin";
    public string Version => "1.0.0";
    public string Description => "SpatioTemporal Asset Catalog (STAC) API implementation";
    public string Author => "HonuaIO";
    public IReadOnlyList<PluginDependency> Dependencies => Array.Empty<PluginDependency>();
    public string MinimumHonuaVersion => "1.0.0";

    // IServicePlugin implementation
    public string ServiceId => "stac";
    public ServiceType ServiceType => ServiceType.API;

    /// <summary>
    /// Called when the plugin is loaded.
    /// </summary>
    public Task OnLoadAsync(PluginContext context)
    {
        context.Logger.LogInformation(
            "Loading STAC plugin v{Version} from {Path}",
            Version,
            context.PluginPath);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Configure dependency injection services for STAC.
    /// Reads Configuration V2 settings and registers STAC services.
    /// </summary>
    public void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration,
        PluginContext context)
    {
        context.Logger.LogInformation("Configuring STAC services from Configuration V2");

        // Read Configuration V2 settings for STAC
        var serviceConfig = configuration.GetSection($"honua:services:{ServiceId}");

        // Extract STAC-specific settings with defaults
        var limit = serviceConfig.GetValue("limit", 100);
        var maxLimit = serviceConfig.GetValue("max_limit", 10_000);

        // Register STAC configuration
        services.AddSingleton(new StacPluginConfiguration
        {
            Limit = limit,
            MaxLimit = maxLimit
        });

        // TODO: Register STAC-specific services:
        // - STAC catalog builder
        // - Collections metadata builder
        // - Items search handler
        // - STAC extension support (EO, SAR, Projection, etc.)
        // - Asset link generation
        // - CQL2 filter parser
        // - Temporal/spatial query optimization

        context.Logger.LogInformation(
            "STAC services configured: limit={Limit}, maxLimit={MaxLimit}",
            limit,
            maxLimit);
    }

    /// <summary>
    /// Map STAC HTTP endpoints.
    /// Only called if the service is enabled in Configuration V2.
    /// </summary>
    public void MapEndpoints(
        IEndpointRouteBuilder endpoints,
        PluginContext context)
    {
        context.Logger.LogInformation("Mapping STAC endpoints");

        // Get base path from configuration (default: /stac)
        var serviceConfig = context.Configuration.GetSection($"honua:services:{ServiceId}");
        var basePath = serviceConfig.GetValue("base_path", "/stac");

        // Map STAC endpoint group
        var stacGroup = endpoints.MapGroup(basePath)
            .WithTags("STAC")
            .WithMetadata("Service", "STAC");

        // Landing page
        stacGroup.MapGet(string.Empty, HandleLandingPageAsync)
            .WithName("STAC-LandingPage")
            .WithDisplayName("STAC Landing Page")
            .WithDescription("Returns the STAC catalog root with links to collections");

        // Conformance
        stacGroup.MapGet("conformance", HandleConformanceAsync)
            .WithName("STAC-Conformance")
            .WithDisplayName("STAC Conformance")
            .WithDescription("Returns STAC conformance classes");

        // Collections
        stacGroup.MapGet("collections", HandleCollectionsAsync)
            .WithName("STAC-Collections")
            .WithDisplayName("STAC Collections")
            .WithDescription("Returns list of STAC collections");

        // Collection metadata
        stacGroup.MapGet("collections/{collectionId}", HandleCollectionAsync)
            .WithName("STAC-Collection")
            .WithDisplayName("STAC Collection Metadata")
            .WithDescription("Returns metadata for a specific STAC collection");

        // Collection items
        stacGroup.MapGet("collections/{collectionId}/items", HandleItemsAsync)
            .WithName("STAC-Items")
            .WithDisplayName("STAC Items Query")
            .WithDescription("Query STAC items from a collection");

        // Single item
        stacGroup.MapGet("collections/{collectionId}/items/{itemId}", HandleItemAsync)
            .WithName("STAC-Item")
            .WithDisplayName("STAC Single Item")
            .WithDescription("Returns a specific STAC item");

        // Search endpoint
        stacGroup.MapGet("search", HandleSearchAsync)
            .WithName("STAC-Search-Get")
            .WithDisplayName("STAC Search (GET)")
            .WithDescription("Search across all collections");

        stacGroup.MapPost("search", HandleSearchAsync)
            .WithName("STAC-Search-Post")
            .WithDisplayName("STAC Search (POST)")
            .WithDescription("Search across all collections with JSON body");

        context.Logger.LogInformation("STAC endpoints mapped at {BasePath}", basePath);
    }

    /// <summary>
    /// Validate STAC configuration from Configuration V2.
    /// Called before services are registered.
    /// </summary>
    public PluginValidationResult ValidateConfiguration(IConfiguration configuration)
    {
        var result = new PluginValidationResult();

        var serviceConfig = configuration.GetSection($"honua:services:{ServiceId}");

        // Check if service is configured
        if (!serviceConfig.Exists())
        {
            result.AddError("STAC service not configured in Configuration V2");
            return result;
        }

        // Validate limit
        var limit = serviceConfig.GetValue<int?>("limit");
        if (limit.HasValue && (limit.Value < 1 || limit.Value > 10_000))
        {
            result.AddError("limit must be between 1 and 10,000");
        }

        // Validate max_limit
        var maxLimit = serviceConfig.GetValue<int?>("max_limit");
        if (maxLimit.HasValue && (maxLimit.Value < 1 || maxLimit.Value > 100_000))
        {
            result.AddError("max_limit must be between 1 and 100,000");
        }

        return result;
    }

    /// <summary>
    /// Called when the plugin is unloaded (hot reload).
    /// </summary>
    public Task OnUnloadAsync()
    {
        // Cleanup resources if needed
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handle STAC landing page requests.
    /// TODO: Generate STAC catalog root
    /// </summary>
    private static Task<IResult> HandleLandingPageAsync(
        HttpContext context,
        StacPluginConfiguration config,
        ILogger<StacServicePlugin> logger)
    {
        logger.LogInformation("STAC landing page request - implementation pending");

        var message = "STAC service is configured but landing page implementation is pending.";
        return Task.FromResult(Results.Ok(new { message }));
    }

    /// <summary>
    /// Handle STAC conformance requests.
    /// TODO: Return STAC conformance classes
    /// </summary>
    private static Task<IResult> HandleConformanceAsync(
        HttpContext context,
        StacPluginConfiguration config,
        ILogger<StacServicePlugin> logger)
    {
        logger.LogInformation("STAC conformance request - implementation pending");

        var message = "STAC service is configured but conformance implementation is pending.";
        return Task.FromResult(Results.Ok(new { message }));
    }

    /// <summary>
    /// Handle STAC collections list requests.
    /// TODO: Generate collections list from STAC catalog
    /// </summary>
    private static Task<IResult> HandleCollectionsAsync(
        HttpContext context,
        StacPluginConfiguration config,
        ILogger<StacServicePlugin> logger)
    {
        logger.LogInformation("STAC collections request - implementation pending");

        var message = "STAC service is configured but collections listing is pending.";
        return Task.FromResult(Results.Ok(new { message }));
    }

    /// <summary>
    /// Handle STAC collection metadata requests.
    /// TODO: Return collection metadata
    /// </summary>
    private static Task<IResult> HandleCollectionAsync(
        HttpContext context,
        string collectionId,
        StacPluginConfiguration config,
        ILogger<StacServicePlugin> logger)
    {
        logger.LogInformation("STAC collection metadata request for {CollectionId} - implementation pending", collectionId);

        var message = $"STAC service is configured but collection metadata is pending. Collection: {collectionId}";
        return Task.FromResult(Results.Ok(new { message, collectionId }));
    }

    /// <summary>
    /// Handle STAC items query requests.
    /// TODO: Full implementation needed for items query with bbox, datetime, limit, properties
    /// </summary>
    private static Task<IResult> HandleItemsAsync(
        HttpContext context,
        string collectionId,
        StacPluginConfiguration config,
        ILogger<StacServicePlugin> logger)
    {
        logger.LogInformation("STAC items query for {CollectionId} - implementation pending", collectionId);

        var message = $"STAC service is configured but items query is pending. Collection: {collectionId}";
        return Task.FromResult(Results.Ok(new { message, collectionId }));
    }

    /// <summary>
    /// Handle STAC single item requests.
    /// TODO: Return single STAC item by ID
    /// </summary>
    private static Task<IResult> HandleItemAsync(
        HttpContext context,
        string collectionId,
        string itemId,
        StacPluginConfiguration config,
        ILogger<StacServicePlugin> logger)
    {
        logger.LogInformation("STAC single item request for {CollectionId}/{ItemId} - implementation pending", collectionId, itemId);

        var message = $"STAC service is configured but single item retrieval is pending. Collection: {collectionId}, Item: {itemId}";
        return Task.FromResult(Results.Ok(new { message, collectionId, itemId }));
    }

    /// <summary>
    /// Handle STAC search requests.
    /// TODO: Full implementation needed for cross-collection search with spatial/temporal/property filters
    /// </summary>
    private static Task<IResult> HandleSearchAsync(
        HttpContext context,
        StacPluginConfiguration config,
        ILogger<StacServicePlugin> logger)
    {
        logger.LogInformation("STAC search request - implementation pending");

        var message = "STAC service is configured but search implementation is pending.";
        return Task.FromResult(Results.Ok(new { message }));
    }
}

/// <summary>
/// STAC plugin configuration extracted from Configuration V2.
/// </summary>
public sealed class StacPluginConfiguration
{
    public int Limit { get; init; }
    public int MaxLimit { get; init; }
}
