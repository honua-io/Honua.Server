// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Text;
using Honua.Server.Core.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Services.Wms;

/// <summary>
/// WMS Service Plugin implementing OGC Web Map Service protocol.
/// Dynamically loadable plugin that integrates with Configuration V2.
/// </summary>
public sealed class WmsServicePlugin : IServicePlugin
{
    // IHonuaPlugin implementation
    public string Id => "honua.services.wms";
    public string Name => "WMS Service Plugin";
    public string Version => "1.0.0";
    public string Description => "OGC Web Map Service (WMS) protocol implementation";
    public string Author => "HonuaIO";
    public IReadOnlyList<PluginDependency> Dependencies => Array.Empty<PluginDependency>();
    public string MinimumHonuaVersion => "1.0.0";

    // IServicePlugin implementation
    public string ServiceId => "wms";
    public ServiceType ServiceType => ServiceType.OGC;

    /// <summary>
    /// Called when the plugin is loaded.
    /// </summary>
    public Task OnLoadAsync(PluginContext context)
    {
        context.Logger.LogInformation(
            "Loading WMS plugin v{Version} from {Path}",
            Version,
            context.PluginPath);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Configure dependency injection services for WMS.
    /// Reads Configuration V2 settings and registers WMS services.
    /// </summary>
    public void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration,
        PluginContext context)
    {
        context.Logger.LogInformation("Configuring WMS services from Configuration V2");

        // Read Configuration V2 settings for WMS
        var serviceConfig = configuration.GetSection($"honua:services:{ServiceId}");

        // Extract WMS-specific settings with defaults
        var capabilitiesCacheDuration = serviceConfig.GetValue("capabilities_cache_duration", 3600);
        var version = serviceConfig.GetValue("version", "1.3.0");
        var maxWidth = serviceConfig.GetValue("max_width", 4096);
        var maxHeight = serviceConfig.GetValue("max_height", 4096);

        // Register WMS configuration
        services.AddSingleton(new WmsPluginConfiguration
        {
            CapabilitiesCacheDuration = capabilitiesCacheDuration,
            Version = version,
            MaxWidth = maxWidth,
            MaxHeight = maxHeight
        });

        // TODO: Register WMS-specific services:
        // - WMS capabilities builder
        // - WMS layer renderer
        // - WMS legend generator
        // - WMS GetFeatureInfo handler
        // - Image format encoders (PNG, JPEG, GeoTIFF)
        // - Style processor (SLD)
        // - CRS transformation service

        context.Logger.LogInformation(
            "WMS services configured: version={Version}, maxWidth={MaxWidth}, maxHeight={MaxHeight}",
            version,
            maxWidth,
            maxHeight);
    }

    /// <summary>
    /// Map WMS HTTP endpoints.
    /// Only called if the service is enabled in Configuration V2.
    /// </summary>
    public void MapEndpoints(
        IEndpointRouteBuilder endpoints,
        PluginContext context)
    {
        // WMS endpoints are mapped by the built-in versioned endpoint system
        // (Honua.Server.Host.Extensions.VersionedEndpointExtensions.MapConditionalServiceEndpoints)
        // which calls Honua.Server.Host.Wms.WmsEndpointExtensions.MapWms
        //
        // The plugin system loads this plugin to validate configuration and register services,
        // but endpoint mapping is handled by the existing endpoint infrastructure to maintain
        // backward compatibility and avoid conflicts.
        //
        // When Configuration V2 is active, the service is enabled via the HonuaConfig check
        // in MapConditionalServiceEndpoints, which reads from honua:services:wms:enabled

        context.Logger.LogInformation(
            "WMS plugin loaded. Endpoints are mapped by built-in versioned endpoint system at /v1/wms");
    }

    /// <summary>
    /// Validate WMS configuration from Configuration V2.
    /// Called before services are registered.
    /// </summary>
    public PluginValidationResult ValidateConfiguration(IConfiguration configuration)
    {
        var result = new PluginValidationResult();

        var serviceConfig = configuration.GetSection($"honua:services:{ServiceId}");

        // Check if service is configured
        if (!serviceConfig.Exists())
        {
            result.AddError("WMS service not configured in Configuration V2");
            return result;
        }

        // Validate version
        var version = serviceConfig.GetValue<string>("version");
        if (!string.IsNullOrEmpty(version))
        {
            var validVersions = new[] { "1.1.1", "1.3.0" };
            if (!validVersions.Contains(version))
            {
                result.AddWarning(
                    $"version '{version}' is not a standard WMS version. Valid versions: {string.Join(", ", validVersions)}");
            }
        }

        // Validate max_width and max_height
        var maxWidth = serviceConfig.GetValue<int?>("max_width");
        if (maxWidth.HasValue && (maxWidth.Value < 1 || maxWidth.Value > 8192))
        {
            result.AddError("max_width must be between 1 and 8192");
        }

        var maxHeight = serviceConfig.GetValue<int?>("max_height");
        if (maxHeight.HasValue && (maxHeight.Value < 1 || maxHeight.Value > 8192))
        {
            result.AddError("max_height must be between 1 and 8192");
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
    /// Handle WMS requests.
    /// </summary>
    private static async Task<IResult> HandleWmsRequestAsync(
        HttpContext context,
        WmsPluginConfiguration config,
        ILogger<WmsServicePlugin> logger)
    {
        var request = context.Request.Query["request"].ToString().ToUpperInvariant();
        var service = context.Request.Query["service"].ToString().ToUpperInvariant();

        // Validate service parameter
        if (service != "WMS")
        {
            return Results.BadRequest(new
            {
                error = "InvalidParameterValue",
                message = "service parameter must be 'WMS'"
            });
        }

        logger.LogInformation("Handling WMS {Request} request", request);

        return request switch
        {
            "GETCAPABILITIES" => await HandleGetCapabilitiesAsync(context, config, logger),
            _ => Results.Ok(new
            {
                message = $"WMS {request} operation not yet implemented",
                request,
                version = config.Version
            })
        };
    }

    private static async Task<IResult> HandleGetCapabilitiesAsync(
        HttpContext context,
        WmsPluginConfiguration config,
        ILogger<WmsServicePlugin> logger)
    {
        logger.LogInformation("Generating WMS {Version} GetCapabilities", config.Version);

        // Get metadata registry from DI
        var metadataRegistry = context.RequestServices.GetRequiredService<Core.Metadata.IMetadataRegistry>();
        var snapshot = await metadataRegistry.GetSnapshotAsync(context.RequestAborted);

        // Find WMS service and its layers
        var wmsService = snapshot.Services.FirstOrDefault(s =>
            string.Equals(s.Id, "wms", StringComparison.OrdinalIgnoreCase));

        var layerElements = new System.Text.StringBuilder();

        if (wmsService != null)
        {
            foreach (var layer in wmsService.Layers)
            {
                var crs = layer.Crs.FirstOrDefault() ?? "EPSG:4326";
                layerElements.AppendLine($"""
                    <Layer>
                      <Name>{layer.Id}</Name>
                      <Title>{layer.Title}</Title>
                      <CRS>{crs}</CRS>
                    </Layer>
                """);
            }
        }

        var capabilities = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <WMS_Capabilities version="{config.Version}"
                xmlns="http://www.opengis.net/wms"
                xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <Service>
                <Name>WMS</Name>
                <Title>Honua WMS Service (Plugin)</Title>
              </Service>
              <Capability>
                <Request>
                  <GetCapabilities>
                    <Format>text/xml</Format>
                  </GetCapabilities>
                  <GetMap>
                    <Format>image/png</Format>
                  </GetMap>
                </Request>
                <Layer>
                  <Title>Honua Layers</Title>
                  {layerElements}
                </Layer>
              </Capability>
            </WMS_Capabilities>
            """;

        context.Response.Headers.CacheControl = $"public, max-age={config.CapabilitiesCacheDuration}";

        return Results.Content(capabilities, "application/xml");
    }
}

/// <summary>
/// WMS plugin configuration extracted from Configuration V2.
/// </summary>
public sealed class WmsPluginConfiguration
{
    public int CapabilitiesCacheDuration { get; init; }
    public string Version { get; init; } = "1.3.0";
    public int MaxWidth { get; init; }
    public int MaxHeight { get; init; }
}
