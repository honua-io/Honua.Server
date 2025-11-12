// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Services.OgcApi;

/// <summary>
/// OGC API Features Service Plugin implementing OGC API - Features standard.
/// Dynamically loadable plugin that integrates with Configuration V2.
/// </summary>
public sealed class OgcApiServicePlugin : IServicePlugin
{
    // IHonuaPlugin implementation
    public string Id => "honua.services.ogc_api";
    public string Name => "OGC API Features Plugin";
    public string Version => "1.0.0";
    public string Description => "OGC API - Features (Part 1 Core, Part 2 CRS) implementation";
    public string Author => "HonuaIO";
    public IReadOnlyList<PluginDependency> Dependencies => Array.Empty<PluginDependency>();
    public string MinimumHonuaVersion => "1.0.0";

    // IServicePlugin implementation
    public string ServiceId => "ogc_api";
    public ServiceType ServiceType => ServiceType.API;

    /// <summary>
    /// Called when the plugin is loaded.
    /// </summary>
    public Task OnLoadAsync(PluginContext context)
    {
        context.Logger.LogInformation(
            "Loading OGC API Features plugin v{Version} from {Path}",
            Version,
            context.PluginPath);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Configure dependency injection services for OGC API Features.
    /// Reads Configuration V2 settings and registers OGC API services.
    /// </summary>
    public void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration,
        PluginContext context)
    {
        context.Logger.LogInformation("Configuring OGC API Features services from Configuration V2");

        // Read Configuration V2 settings for OGC API
        var serviceConfig = configuration.GetSection($"honua:services:{ServiceId}");

        // Extract OGC API-specific settings with defaults
        var limit = serviceConfig.GetValue("limit", 100);
        var maxLimit = serviceConfig.GetValue("max_limit", 10_000);
        var crs = serviceConfig.GetValue("crs", "http://www.opengis.net/def/crs/OGC/1.3/CRS84");

        // Register OGC API configuration
        services.AddSingleton(new OgcApiPluginConfiguration
        {
            Limit = limit,
            MaxLimit = maxLimit,
            Crs = crs
        });

        // TODO: Register OGC API-specific services:
        // - Landing page builder
        // - Conformance declaration
        // - Collections metadata builder
        // - Items query handler
        // - CRS transformation service
        // - JSON/GeoJSON/HTML formatters
        // - Filter (CQL2) parser and executor

        context.Logger.LogInformation(
            "OGC API Features services configured: limit={Limit}, maxLimit={MaxLimit}",
            limit,
            maxLimit);
    }

    /// <summary>
    /// Map OGC API Features HTTP endpoints.
    /// Only called if the service is enabled in Configuration V2.
    /// </summary>
    public void MapEndpoints(
        IEndpointRouteBuilder endpoints,
        PluginContext context)
    {
        context.Logger.LogInformation("Mapping OGC API Features endpoints");

        // Get base path from configuration (default: /ogcapi)
        var serviceConfig = context.Configuration.GetSection($"honua:services:{ServiceId}");
        var basePath = serviceConfig.GetValue("base_path", "/ogcapi");

        // Map OGC API endpoint group
        var ogcApiGroup = endpoints.MapGroup(basePath)
            .WithTags("OGC API Features")
            .WithMetadata("Service", "OGC API Features");

        // Landing page
        ogcApiGroup.MapGet(string.Empty, HandleLandingPageAsync)
            .WithName("OGC-API-LandingPage")
            .WithDisplayName("OGC API Landing Page")
            .WithDescription("Returns the landing page with links to API endpoints");

        // Conformance
        ogcApiGroup.MapGet("conformance", HandleConformanceAsync)
            .WithName("OGC-API-Conformance")
            .WithDisplayName("OGC API Conformance")
            .WithDescription("Returns conformance classes this API implements");

        // Collections
        ogcApiGroup.MapGet("collections", HandleCollectionsAsync)
            .WithName("OGC-API-Collections")
            .WithDisplayName("OGC API Collections")
            .WithDescription("Returns list of available feature collections");

        // Collection metadata
        ogcApiGroup.MapGet("collections/{collectionId}", HandleCollectionAsync)
            .WithName("OGC-API-Collection")
            .WithDisplayName("OGC API Collection Metadata")
            .WithDescription("Returns metadata for a specific collection");

        // Collection items
        ogcApiGroup.MapGet("collections/{collectionId}/items", HandleItemsAsync)
            .WithName("OGC-API-Items")
            .WithDisplayName("OGC API Items Query")
            .WithDescription("Query items from a collection with bbox, limit, datetime, properties filters");

        // Single item
        ogcApiGroup.MapGet("collections/{collectionId}/items/{itemId}", HandleItemAsync)
            .WithName("OGC-API-Item")
            .WithDisplayName("OGC API Single Item")
            .WithDescription("Returns a specific item from a collection");

        context.Logger.LogInformation("OGC API Features endpoints mapped at {BasePath}", basePath);
    }

    /// <summary>
    /// Validate OGC API configuration from Configuration V2.
    /// Called before services are registered.
    /// </summary>
    public PluginValidationResult ValidateConfiguration(IConfiguration configuration)
    {
        var result = new PluginValidationResult();

        var serviceConfig = configuration.GetSection($"honua:services:{ServiceId}");

        // Check if service is configured
        if (!serviceConfig.Exists())
        {
            result.AddError("OGC API service not configured in Configuration V2");
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

        // Validate limit <= max_limit
        if (limit.HasValue && maxLimit.HasValue && limit.Value > maxLimit.Value)
        {
            result.AddError("limit cannot be greater than max_limit");
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
    /// Handle landing page requests.
    /// TODO: Generate landing page with API links
    /// </summary>
    private static Task<IResult> HandleLandingPageAsync(
        HttpContext context,
        OgcApiPluginConfiguration config,
        ILogger<OgcApiServicePlugin> logger)
    {
        logger.LogInformation("OGC API landing page request - implementation pending");

        var message = "OGC API Features service is configured but landing page implementation is pending.";
        return Task.FromResult(Results.Ok(new { message }));
    }

    /// <summary>
    /// Handle conformance requests.
    /// TODO: Return conformance classes
    /// </summary>
    private static Task<IResult> HandleConformanceAsync(
        HttpContext context,
        OgcApiPluginConfiguration config,
        ILogger<OgcApiServicePlugin> logger)
    {
        logger.LogInformation("OGC API conformance request - implementation pending");

        var message = "OGC API Features service is configured but conformance implementation is pending.";
        return Task.FromResult(Results.Ok(new { message }));
    }

    /// <summary>
    /// Handle collections list requests.
    /// TODO: Generate collections list from Configuration V2 layers
    /// </summary>
    private static Task<IResult> HandleCollectionsAsync(
        HttpContext context,
        OgcApiPluginConfiguration config,
        ILogger<OgcApiServicePlugin> logger)
    {
        logger.LogInformation("OGC API collections request - implementation pending");

        var message = "OGC API Features service is configured but collections listing is pending.";
        return Task.FromResult(Results.Ok(new { message }));
    }

    /// <summary>
    /// Handle collection metadata requests.
    /// TODO: Return collection metadata
    /// </summary>
    private static Task<IResult> HandleCollectionAsync(
        HttpContext context,
        string collectionId,
        OgcApiPluginConfiguration config,
        ILogger<OgcApiServicePlugin> logger)
    {
        logger.LogInformation("OGC API collection metadata request for {CollectionId} - implementation pending", collectionId);

        var message = $"OGC API Features service is configured but collection metadata is pending. Collection: {collectionId}";
        return Task.FromResult(Results.Ok(new { message, collectionId }));
    }

    /// <summary>
    /// Handle items query requests.
    /// TODO: Full implementation needed for items query with bbox, datetime, limit, properties, CQL2 filter
    /// </summary>
    private static Task<IResult> HandleItemsAsync(
        HttpContext context,
        string collectionId,
        OgcApiPluginConfiguration config,
        ILogger<OgcApiServicePlugin> logger)
    {
        logger.LogInformation("OGC API items query for {CollectionId} - implementation pending", collectionId);

        var message = $"OGC API Features service is configured but items query is pending. Collection: {collectionId}";
        return Task.FromResult(Results.Ok(new { message, collectionId }));
    }

    /// <summary>
    /// Handle single item requests.
    /// TODO: Return single item by ID
    /// </summary>
    private static Task<IResult> HandleItemAsync(
        HttpContext context,
        string collectionId,
        string itemId,
        OgcApiPluginConfiguration config,
        ILogger<OgcApiServicePlugin> logger)
    {
        logger.LogInformation("OGC API single item request for {CollectionId}/{ItemId} - implementation pending", collectionId, itemId);

        var message = $"OGC API Features service is configured but single item retrieval is pending. Collection: {collectionId}, Item: {itemId}";
        return Task.FromResult(Results.Ok(new { message, collectionId, itemId }));
    }
}

/// <summary>
/// OGC API plugin configuration extracted from Configuration V2.
/// </summary>
public sealed class OgcApiPluginConfiguration
{
    public int Limit { get; init; }
    public int MaxLimit { get; init; }
    public string Crs { get; init; } = "http://www.opengis.net/def/crs/OGC/1.3/CRS84";
}
