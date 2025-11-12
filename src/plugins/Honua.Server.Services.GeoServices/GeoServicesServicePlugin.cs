// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Services.GeoServices;

/// <summary>
/// GeoServices Service Plugin implementing Esri GeoServices REST API.
/// Dynamically loadable plugin that integrates with Configuration V2.
/// </summary>
public sealed class GeoServicesServicePlugin : IServicePlugin
{
    // IHonuaPlugin implementation
    public string Id => "honua.services.geoservices";
    public string Name => "GeoServices REST Plugin";
    public string Version => "1.0.0";
    public string Description => "Esri GeoServices REST API implementation for ArcGIS compatibility";
    public string Author => "HonuaIO";
    public IReadOnlyList<PluginDependency> Dependencies => Array.Empty<PluginDependency>();
    public string MinimumHonuaVersion => "1.0.0";

    // IServicePlugin implementation
    public string ServiceId => "geoservices";
    public ServiceType ServiceType => ServiceType.Proprietary;

    /// <summary>
    /// Called when the plugin is loaded.
    /// </summary>
    public Task OnLoadAsync(PluginContext context)
    {
        context.Logger.LogInformation(
            "Loading GeoServices REST plugin v{Version} from {Path}",
            Version,
            context.PluginPath);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Configure dependency injection services for GeoServices REST.
    /// Reads Configuration V2 settings and registers GeoServices services.
    /// </summary>
    public void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration,
        PluginContext context)
    {
        context.Logger.LogInformation("Configuring GeoServices REST services from Configuration V2");

        // Read Configuration V2 settings for GeoServices
        var serviceConfig = configuration.GetSection($"honua:services:{ServiceId}");

        // Extract GeoServices-specific settings with defaults
        var maxRecordCount = serviceConfig.GetValue("max_record_count", 2000);
        var supportsPagination = serviceConfig.GetValue("supports_pagination", true);

        // Register GeoServices configuration
        services.AddSingleton(new GeoServicesPluginConfiguration
        {
            MaxRecordCount = maxRecordCount,
            SupportsPagination = supportsPagination
        });

        // TODO: Register GeoServices-specific services:
        // - Service catalog builder
        // - Feature service handler
        // - Map service handler
        // - Query operation handler
        // - Identify operation handler
        // - Export map handler
        // - REST JSON formatter
        // - Feature editing support (Add/Update/Delete)
        // - Attachments support
        // - Token-based authentication

        context.Logger.LogInformation(
            "GeoServices REST services configured: maxRecordCount={MaxRecordCount}, supportsPagination={SupportsPagination}",
            maxRecordCount,
            supportsPagination);
    }

    /// <summary>
    /// Map GeoServices REST HTTP endpoints.
    /// Only called if the service is enabled in Configuration V2.
    /// </summary>
    public void MapEndpoints(
        IEndpointRouteBuilder endpoints,
        PluginContext context)
    {
        context.Logger.LogInformation("Mapping GeoServices REST endpoints");

        // Get base path from configuration (default: /rest/services)
        var serviceConfig = context.Configuration.GetSection($"honua:services:{ServiceId}");
        var basePath = serviceConfig.GetValue("base_path", "/rest/services");

        // Map GeoServices endpoint group
        var geoServicesGroup = endpoints.MapGroup(basePath)
            .WithTags("GeoServices", "Esri")
            .WithMetadata("Service", "GeoServices");

        // Service catalog root
        geoServicesGroup.MapGet(string.Empty, HandleCatalogAsync)
            .WithName("GeoServices-Catalog")
            .WithDisplayName("GeoServices Catalog")
            .WithDescription("Returns service catalog listing available services");

        // Feature service metadata
        geoServicesGroup.MapGet("{serviceName}/FeatureServer", HandleFeatureServiceAsync)
            .WithName("GeoServices-FeatureService")
            .WithDisplayName("GeoServices Feature Service Metadata")
            .WithDescription("Returns feature service metadata");

        // Layer metadata
        geoServicesGroup.MapGet("{serviceName}/FeatureServer/{layerId}", HandleLayerMetadataAsync)
            .WithName("GeoServices-Layer")
            .WithDisplayName("GeoServices Layer Metadata")
            .WithDescription("Returns layer metadata");

        // Query operation
        geoServicesGroup.MapGet("{serviceName}/FeatureServer/{layerId}/query", HandleQueryAsync)
            .WithName("GeoServices-Query-Get")
            .WithDisplayName("GeoServices Query (GET)")
            .WithDescription("Query features from a layer");

        geoServicesGroup.MapPost("{serviceName}/FeatureServer/{layerId}/query", HandleQueryAsync)
            .WithName("GeoServices-Query-Post")
            .WithDisplayName("GeoServices Query (POST)")
            .WithDescription("Query features from a layer via POST");

        // Map service
        geoServicesGroup.MapGet("{serviceName}/MapServer", HandleMapServiceAsync)
            .WithName("GeoServices-MapService")
            .WithDisplayName("GeoServices Map Service")
            .WithDescription("Returns map service metadata");

        // Export map
        geoServicesGroup.MapGet("{serviceName}/MapServer/export", HandleExportMapAsync)
            .WithName("GeoServices-ExportMap")
            .WithDisplayName("GeoServices Export Map")
            .WithDescription("Export map image");

        context.Logger.LogInformation("GeoServices REST endpoints mapped at {BasePath}", basePath);
    }

    /// <summary>
    /// Validate GeoServices configuration from Configuration V2.
    /// Called before services are registered.
    /// </summary>
    public PluginValidationResult ValidateConfiguration(IConfiguration configuration)
    {
        var result = new PluginValidationResult();

        var serviceConfig = configuration.GetSection($"honua:services:{ServiceId}");

        // Check if service is configured
        if (!serviceConfig.Exists())
        {
            result.AddError("GeoServices service not configured in Configuration V2");
            return result;
        }

        // Validate max_record_count
        var maxRecordCount = serviceConfig.GetValue<int?>("max_record_count");
        if (maxRecordCount.HasValue && (maxRecordCount.Value < 1 || maxRecordCount.Value > 10_000))
        {
            result.AddError("max_record_count must be between 1 and 10,000");
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
    /// Handle service catalog requests.
    /// TODO: Generate service catalog from Configuration V2
    /// </summary>
    private static Task<IResult> HandleCatalogAsync(
        HttpContext context,
        GeoServicesPluginConfiguration config,
        ILogger<GeoServicesServicePlugin> logger)
    {
        logger.LogInformation("GeoServices catalog request - implementation pending");

        var message = "GeoServices REST API is configured but catalog listing is pending.";
        return Task.FromResult(Results.Ok(new { message }));
    }

    /// <summary>
    /// Handle feature service metadata requests.
    /// TODO: Generate feature service metadata
    /// </summary>
    private static Task<IResult> HandleFeatureServiceAsync(
        HttpContext context,
        string serviceName,
        GeoServicesPluginConfiguration config,
        ILogger<GeoServicesServicePlugin> logger)
    {
        logger.LogInformation("GeoServices feature service request for {ServiceName} - implementation pending", serviceName);

        var message = $"GeoServices REST API is configured but feature service metadata is pending. Service: {serviceName}";
        return Task.FromResult(Results.Ok(new { message, serviceName }));
    }

    /// <summary>
    /// Handle layer metadata requests.
    /// TODO: Generate layer metadata
    /// </summary>
    private static Task<IResult> HandleLayerMetadataAsync(
        HttpContext context,
        string serviceName,
        int layerId,
        GeoServicesPluginConfiguration config,
        ILogger<GeoServicesServicePlugin> logger)
    {
        logger.LogInformation("GeoServices layer metadata request for {ServiceName}/{LayerId} - implementation pending", serviceName, layerId);

        var message = $"GeoServices REST API is configured but layer metadata is pending. Service: {serviceName}, Layer: {layerId}";
        return Task.FromResult(Results.Ok(new { message, serviceName, layerId }));
    }

    /// <summary>
    /// Handle query requests.
    /// TODO: Full implementation needed for feature queries with where, geometry, spatialRel, outFields, etc.
    /// </summary>
    private static Task<IResult> HandleQueryAsync(
        HttpContext context,
        string serviceName,
        int layerId,
        GeoServicesPluginConfiguration config,
        ILogger<GeoServicesServicePlugin> logger)
    {
        logger.LogInformation("GeoServices query request for {ServiceName}/{LayerId} - implementation pending", serviceName, layerId);

        var message = $"GeoServices REST API is configured but query implementation is pending. Service: {serviceName}, Layer: {layerId}";
        return Task.FromResult(Results.Ok(new { message, serviceName, layerId }));
    }

    /// <summary>
    /// Handle map service metadata requests.
    /// TODO: Generate map service metadata
    /// </summary>
    private static Task<IResult> HandleMapServiceAsync(
        HttpContext context,
        string serviceName,
        GeoServicesPluginConfiguration config,
        ILogger<GeoServicesServicePlugin> logger)
    {
        logger.LogInformation("GeoServices map service request for {ServiceName} - implementation pending", serviceName);

        var message = $"GeoServices REST API is configured but map service metadata is pending. Service: {serviceName}";
        return Task.FromResult(Results.Ok(new { message, serviceName }));
    }

    /// <summary>
    /// Handle export map requests.
    /// TODO: Full implementation needed for map image export
    /// </summary>
    private static Task<IResult> HandleExportMapAsync(
        HttpContext context,
        string serviceName,
        GeoServicesPluginConfiguration config,
        ILogger<GeoServicesServicePlugin> logger)
    {
        logger.LogInformation("GeoServices export map request for {ServiceName} - implementation pending", serviceName);

        var message = $"GeoServices REST API is configured but export map is pending. Service: {serviceName}";
        return Task.FromResult(Results.Ok(new { message, serviceName }));
    }
}

/// <summary>
/// GeoServices plugin configuration extracted from Configuration V2.
/// </summary>
public sealed class GeoServicesPluginConfiguration
{
    public int MaxRecordCount { get; init; }
    public bool SupportsPagination { get; init; }
}
