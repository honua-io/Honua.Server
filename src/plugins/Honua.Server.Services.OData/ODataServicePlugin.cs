// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Services.OData;

/// <summary>
/// OData Service Plugin implementing OData v4 protocol.
/// Dynamically loadable plugin that integrates with Configuration V2.
/// </summary>
public sealed class ODataServicePlugin : IServicePlugin
{
    // IHonuaPlugin implementation
    public string Id => "honua.services.odata";
    public string Name => "OData Service Plugin";
    public string Version => "1.0.0";
    public string Description => "OData v4 protocol implementation for querying geospatial data";
    public string Author => "HonuaIO";
    public IReadOnlyList<PluginDependency> Dependencies => Array.Empty<PluginDependency>();
    public string MinimumHonuaVersion => "1.0.0";

    // IServicePlugin implementation
    public string ServiceId => "odata";
    public ServiceType ServiceType => ServiceType.Custom;

    /// <summary>
    /// Called when the plugin is loaded.
    /// </summary>
    public Task OnLoadAsync(PluginContext context)
    {
        context.Logger.LogInformation(
            "Loading OData plugin v{Version} from {Path}",
            Version,
            context.PluginPath);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Configure dependency injection services for OData.
    /// Reads Configuration V2 settings and registers OData services.
    /// </summary>
    public void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration,
        PluginContext context)
    {
        context.Logger.LogInformation("Configuring OData services from Configuration V2");

        // Read Configuration V2 settings for OData
        var serviceConfig = configuration.GetSection($"honua:services:{ServiceId}");

        // Extract OData-specific settings with defaults
        var maxTop = serviceConfig.GetValue("max_top", 1000);
        var enableCount = serviceConfig.GetValue("enable_count", true);
        var enableExpand = serviceConfig.GetValue("enable_expand", true);

        // Register OData configuration
        services.AddSingleton(new ODataPluginConfiguration
        {
            MaxTop = maxTop,
            EnableCount = enableCount,
            EnableExpand = enableExpand
        });

        // TODO: Register OData-specific services:
        // - OData EDM model builder
        // - OData query parser
        // - OData filter expression translator
        // - OData entity set controllers
        // - GeoJSON/GeoOData formatter
        // - Geometry query operations ($geo.distance, $geo.intersects, etc.)

        context.Logger.LogInformation(
            "OData services configured: maxTop={MaxTop}, enableCount={EnableCount}, enableExpand={EnableExpand}",
            maxTop,
            enableCount,
            enableExpand);
    }

    /// <summary>
    /// Map OData HTTP endpoints.
    /// Only called if the service is enabled in Configuration V2.
    /// </summary>
    public void MapEndpoints(
        IEndpointRouteBuilder endpoints,
        PluginContext context)
    {
        context.Logger.LogInformation("Mapping OData endpoints");

        // Get base path from configuration (default: /odata)
        var serviceConfig = context.Configuration.GetSection($"honua:services:{ServiceId}");
        var basePath = serviceConfig.GetValue("base_path", "/odata");

        // Map OData endpoint group
        var odataGroup = endpoints.MapGroup(basePath)
            .WithTags("OData")
            .WithMetadata("Service", "OData");

        // Map metadata endpoint
        odataGroup.MapGet("$metadata", HandleMetadataRequestAsync)
            .WithName("OData-Metadata")
            .WithDisplayName("OData Metadata")
            .WithDescription("Returns OData EDM metadata document");

        // Map service document
        odataGroup.MapGet(string.Empty, HandleServiceDocumentAsync)
            .WithName("OData-ServiceDocument")
            .WithDisplayName("OData Service Document")
            .WithDescription("Returns OData service document listing available entity sets");

        // Map entity set query endpoint
        odataGroup.MapGet("{entitySet}", HandleEntitySetQueryAsync)
            .WithName("OData-EntitySetQuery")
            .WithDisplayName("OData Entity Set Query")
            .WithDescription("Queries an OData entity set with $filter, $select, $orderby, $top, $skip");

        context.Logger.LogInformation("OData endpoints mapped at {BasePath}", basePath);
    }

    /// <summary>
    /// Validate OData configuration from Configuration V2.
    /// Called before services are registered.
    /// </summary>
    public PluginValidationResult ValidateConfiguration(IConfiguration configuration)
    {
        var result = new PluginValidationResult();

        var serviceConfig = configuration.GetSection($"honua:services:{ServiceId}");

        // Check if service is configured
        if (!serviceConfig.Exists())
        {
            result.AddError("OData service not configured in Configuration V2");
            return result;
        }

        // Validate max_top
        var maxTop = serviceConfig.GetValue<int?>("max_top");
        if (maxTop.HasValue && (maxTop.Value < 1 || maxTop.Value > 10_000))
        {
            result.AddError("max_top must be between 1 and 10,000");
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
    /// Handle OData metadata requests.
    /// TODO: Generate EDM metadata document from layer schemas
    /// </summary>
    private static Task<IResult> HandleMetadataRequestAsync(
        HttpContext context,
        ODataPluginConfiguration config,
        ILogger<ODataServicePlugin> logger)
    {
        logger.LogInformation("OData metadata request - implementation pending");

        var message = "OData service is configured but metadata generation is pending.";
        return Task.FromResult(Results.Ok(new { message }));
    }

    /// <summary>
    /// Handle OData service document requests.
    /// TODO: Generate service document listing available entity sets
    /// </summary>
    private static Task<IResult> HandleServiceDocumentAsync(
        HttpContext context,
        ODataPluginConfiguration config,
        ILogger<ODataServicePlugin> logger)
    {
        logger.LogInformation("OData service document request - implementation pending");

        var message = "OData service is configured but service document generation is pending.";
        return Task.FromResult(Results.Ok(new { message }));
    }

    /// <summary>
    /// Handle OData entity set query requests.
    /// TODO: Full implementation needed for:
    /// - Parse OData query options ($filter, $select, $orderby, $top, $skip, $count, $expand)
    /// - Translate to SQL queries
    /// - Execute and format as OData JSON
    /// - Support spatial query functions
    /// </summary>
    private static Task<IResult> HandleEntitySetQueryAsync(
        HttpContext context,
        string entitySet,
        ODataPluginConfiguration config,
        ILogger<ODataServicePlugin> logger)
    {
        logger.LogInformation("OData entity set query for {EntitySet} - implementation pending", entitySet);

        var message = $"OData service is configured but entity set query implementation is pending. EntitySet: {entitySet}";
        return Task.FromResult(Results.Ok(new { message, entitySet }));
    }
}

/// <summary>
/// OData plugin configuration extracted from Configuration V2.
/// </summary>
public sealed class ODataPluginConfiguration
{
    public int MaxTop { get; init; }
    public bool EnableCount { get; init; }
    public bool EnableExpand { get; init; }
}
