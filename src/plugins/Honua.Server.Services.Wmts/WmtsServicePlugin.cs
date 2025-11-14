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

namespace Honua.Server.Services.Wmts;

/// <summary>
/// WMTS Service Plugin implementing OGC Web Map Tile Service protocol.
/// Dynamically loadable plugin that integrates with Configuration V2.
/// </summary>
public sealed class WmtsServicePlugin : IServicePlugin
{
    // IHonuaPlugin implementation
    public string Id => "honua.services.wmts";
    public string Name => "WMTS Service Plugin";
    public string Version => "1.0.0";
    public string Description => "OGC Web Map Tile Service (WMTS) protocol implementation";
    public string Author => "HonuaIO";
    public IReadOnlyList<PluginDependency> Dependencies => Array.Empty<PluginDependency>();
    public string MinimumHonuaVersion => "1.0.0";

    // IServicePlugin implementation
    public string ServiceId => "wmts";
    public ServiceType ServiceType => ServiceType.OGC;

    /// <summary>
    /// Called when the plugin is loaded.
    /// </summary>
    public Task OnLoadAsync(PluginContext context)
    {
        context.Logger.LogInformation(
            "Loading WMTS plugin v{Version} from {Path}",
            Version,
            context.PluginPath);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Configure dependency injection services for WMTS.
    /// Reads Configuration V2 settings and registers WMTS services.
    /// </summary>
    public void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration,
        PluginContext context)
    {
        context.Logger.LogInformation("Configuring WMTS services from Configuration V2");

        // Read Configuration V2 settings for WMTS
        var serviceConfig = configuration.GetSection($"honua:services:{ServiceId}");

        // Extract WMTS-specific settings with defaults
        var capabilitiesCacheDuration = serviceConfig.GetValue("capabilities_cache_duration", 3600);
        var tileCacheDuration = serviceConfig.GetValue("tile_cache_duration", 86400);
        var tileMatrixSet = serviceConfig.GetValue("tile_matrix_set", "WebMercatorQuad");

        // Register WMTS configuration
        services.AddSingleton(new WmtsPluginConfiguration
        {
            CapabilitiesCacheDuration = capabilitiesCacheDuration,
            TileCacheDuration = tileCacheDuration,
            TileMatrixSet = tileMatrixSet
        });

        // TODO: Register WMTS-specific services:
        // - WMTS capabilities builder
        // - Tile matrix set registry
        // - Tile cache manager
        // - Tile renderer/fetcher
        // - Format encoders (PNG, JPEG, WebP)
        // - RESTful and KVP interface handlers

        context.Logger.LogInformation(
            "WMTS services configured: tileMatrixSet={TileMatrixSet}, tileCacheDuration={TileCacheDuration}s",
            tileMatrixSet,
            tileCacheDuration);
    }

    /// <summary>
    /// Map WMTS HTTP endpoints.
    /// Only called if the service is enabled in Configuration V2.
    /// </summary>
    public void MapEndpoints(
        IEndpointRouteBuilder endpoints,
        PluginContext context)
    {
        // NOTE: WMTS endpoints are mapped by the built-in WMTS handler system
        // (Honua.Server.Host.Wmts.WmtsEndpointExtensions.MapWmtsEndpoints)
        // This plugin only provides configuration and service registration.
        // The plugin endpoint mapping is intentionally skipped to avoid conflicts
        // with the existing comprehensive WMTS implementation.

        context.Logger.LogInformation(
            "WMTS plugin loaded. Endpoints are mapped by built-in WMTS handler system.");
    }

    /// <summary>
    /// Validate WMTS configuration from Configuration V2.
    /// Called before services are registered.
    /// </summary>
    public PluginValidationResult ValidateConfiguration(IConfiguration configuration)
    {
        var result = new PluginValidationResult();

        var serviceConfig = configuration.GetSection($"honua:services:{ServiceId}");

        // Check if service is configured
        if (!serviceConfig.Exists())
        {
            result.AddError("WMTS service not configured in Configuration V2");
            return result;
        }

        // Validate tile_matrix_set
        var tileMatrixSet = serviceConfig.GetValue<string>("tile_matrix_set");
        if (!string.IsNullOrEmpty(tileMatrixSet))
        {
            var validSets = new[] { "WebMercatorQuad", "WorldCRS84Quad", "WorldMercatorWGS84Quad" };
            if (!validSets.Contains(tileMatrixSet))
            {
                result.AddWarning(
                    $"tile_matrix_set '{tileMatrixSet}' may not be standard. Common sets: {string.Join(", ", validSets)}");
            }
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
    /// Handle WMTS KVP requests.
    /// </summary>
    private static async Task<IResult> HandleWmtsKvpRequestAsync(
        HttpContext context,
        WmtsPluginConfiguration config,
        ILogger<WmtsServicePlugin> logger)
    {
        var request = context.Request.Query["request"].ToString().ToUpperInvariant();
        var service = context.Request.Query["service"].ToString().ToUpperInvariant();

        if (service != "WMTS")
        {
            return Results.BadRequest(new
            {
                error = "InvalidParameterValue",
                message = "service parameter must be 'WMTS'"
            });
        }

        logger.LogInformation("Handling WMTS {Request} request", request);

        return request switch
        {
            "GETCAPABILITIES" => await HandleGetCapabilitiesAsync(context, config, logger),
            _ => Results.Ok(new
            {
                message = $"WMTS {request} operation not yet implemented",
                request,
                tileMatrixSet = config.TileMatrixSet
            })
        };
    }

    private static async Task<IResult> HandleGetCapabilitiesAsync(
        HttpContext context,
        WmtsPluginConfiguration config,
        ILogger<WmtsServicePlugin> logger)
    {
        logger.LogInformation("Generating WMTS GetCapabilities");

        // Get metadata registry from DI
        var metadataRegistry = context.RequestServices.GetRequiredService<Core.Metadata.IMetadataRegistry>();
        var snapshot = await metadataRegistry.GetSnapshotAsync(context.RequestAborted);

        // Find WMTS service and its layers
        var wmtsService = snapshot.Services.FirstOrDefault(s =>
            string.Equals(s.Id, "wmts", StringComparison.OrdinalIgnoreCase));

        var layerElements = new System.Text.StringBuilder();
        var tileMatrixSetElements = new System.Text.StringBuilder();

        // Add TileMatrixSet definition
        tileMatrixSetElements.AppendLine($"""
            <TileMatrixSet>
              <ows:Identifier>{config.TileMatrixSet}</ows:Identifier>
              <ows:SupportedCRS>urn:ogc:def:crs:EPSG::3857</ows:SupportedCRS>
              <TileMatrix>
                <ows:Identifier>0</ows:Identifier>
                <ScaleDenominator>559082264.029</ScaleDenominator>
                <TopLeftCorner>-20037508.34 20037508.34</TopLeftCorner>
                <TileWidth>256</TileWidth>
                <TileHeight>256</TileHeight>
                <MatrixWidth>1</MatrixWidth>
                <MatrixHeight>1</MatrixHeight>
              </TileMatrix>
            </TileMatrixSet>
        """);

        if (wmtsService != null)
        {
            foreach (var layer in wmtsService.Layers)
            {
                var crs = layer.Crs.FirstOrDefault() ?? "EPSG:4326";
                layerElements.AppendLine($"""
                    <Layer>
                      <ows:Title>{layer.Title}</ows:Title>
                      <ows:Identifier>{layer.Id}</ows:Identifier>
                      <ows:WGS84BoundingBox>
                        <ows:LowerCorner>-180 -90</ows:LowerCorner>
                        <ows:UpperCorner>180 90</ows:UpperCorner>
                      </ows:WGS84BoundingBox>
                      <Style isDefault="true">
                        <ows:Identifier>default</ows:Identifier>
                      </Style>
                      <Format>image/png</Format>
                      <TileMatrixSetLink>
                        <TileMatrixSet>{config.TileMatrixSet}</TileMatrixSet>
                      </TileMatrixSetLink>
                    </Layer>
                """);
            }
        }

        var capabilities = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Capabilities xmlns="http://www.opengis.net/wmts/1.0"
                xmlns:ows="http://www.opengis.net/ows/1.1"
                xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                version="1.0.0">
              <ows:ServiceIdentification>
                <ows:Title>Honua WMTS Service (Plugin)</ows:Title>
                <ows:ServiceType>OGC WMTS</ows:ServiceType>
                <ows:ServiceTypeVersion>1.0.0</ows:ServiceTypeVersion>
              </ows:ServiceIdentification>
              <ows:OperationsMetadata>
                <ows:Operation name="GetCapabilities"/>
                <ows:Operation name="GetTile"/>
              </ows:OperationsMetadata>
              <Contents>
                {layerElements}
                {tileMatrixSetElements}
              </Contents>
            </Capabilities>
            """;

        context.Response.Headers.CacheControl = $"public, max-age={config.CapabilitiesCacheDuration}";

        return Results.Content(capabilities, "application/xml");
    }

    /// <summary>
    /// Handle WMTS RESTful tile requests.
    /// TODO: Full implementation needed for RESTful tile delivery
    /// </summary>
    private static Task<IResult> HandleWmtsRestRequestAsync(
        HttpContext context,
        string layer,
        string style,
        string tileMatrixSet,
        string tileMatrix,
        int tileRow,
        int tileCol,
        WmtsPluginConfiguration config,
        ILogger<WmtsServicePlugin> logger)
    {
        logger.LogInformation(
            "WMTS RESTful tile request: layer={Layer}, tileMatrix={TileMatrix}, row={Row}, col={Col}",
            layer, tileMatrix, tileRow, tileCol);

        var message = "WMTS service is configured but full tile rendering implementation is pending.";
        return Task.FromResult(Results.Ok(new
        {
            message,
            layer,
            style,
            tileMatrixSet,
            tileMatrix,
            tileRow,
            tileCol
        }));
    }
}

/// <summary>
/// WMTS plugin configuration extracted from Configuration V2.
/// </summary>
public sealed class WmtsPluginConfiguration
{
    public int CapabilitiesCacheDuration { get; init; }
    public int TileCacheDuration { get; init; }
    public string TileMatrixSet { get; init; } = "WebMercatorQuad";
}
