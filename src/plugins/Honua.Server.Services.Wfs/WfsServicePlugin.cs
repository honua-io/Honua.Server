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

namespace Honua.Server.Services.Wfs;

/// <summary>
/// WFS Service Plugin implementing OGC Web Feature Service protocol.
/// Dynamically loadable plugin that integrates with Configuration V2.
/// </summary>
public sealed class WfsServicePlugin : IServicePlugin
{
    // IHonuaPlugin implementation
    public string Id => "honua.services.wfs";
    public string Name => "WFS Service Plugin";
    public string Version => "1.0.0";
    public string Description => "OGC Web Feature Service (WFS) protocol implementation";
    public string Author => "HonuaIO";
    public IReadOnlyList<PluginDependency> Dependencies => Array.Empty<PluginDependency>();
    public string MinimumHonuaVersion => "1.0.0";

    // IServicePlugin implementation
    public string ServiceId => "wfs";
    public ServiceType ServiceType => ServiceType.OGC;

    /// <summary>
    /// Called when the plugin is loaded.
    /// </summary>
    public Task OnLoadAsync(PluginContext context)
    {
        context.Logger.LogInformation(
            "Loading WFS plugin v{Version} from {Path}",
            Version,
            context.PluginPath);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Configure dependency injection services for WFS.
    /// Reads Configuration V2 settings and registers WFS services.
    /// </summary>
    public void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration,
        PluginContext context)
    {
        context.Logger.LogInformation("Configuring WFS services from Configuration V2");

        // Read Configuration V2 settings for WFS
        var serviceConfig = configuration.GetSection($"honua:services:{ServiceId}");

        // Extract WFS-specific settings with defaults
        var capabilitiesCacheDuration = serviceConfig.GetValue("capabilities_cache_duration", 3600);
        var defaultCount = serviceConfig.GetValue("default_count", 100);
        var maxFeatures = serviceConfig.GetValue("max_features", 10_000);
        var version = serviceConfig.GetValue("version", "2.0.0");

        // Register WFS configuration
        services.AddSingleton(new WfsPluginConfiguration
        {
            CapabilitiesCacheDuration = capabilitiesCacheDuration,
            DefaultCount = defaultCount,
            MaxFeatures = maxFeatures,
            Version = version
        });

        // In a full implementation, we would register:
        // - WFS handlers
        // - WFS capabilities builder
        // - WFS schema cache
        // - WFS lock managers
        // - WFS transaction handlers
        // - etc.

        context.Logger.LogInformation(
            "WFS services configured: version={Version}, maxFeatures={MaxFeatures}, defaultCount={DefaultCount}",
            version,
            maxFeatures,
            defaultCount);
    }

    /// <summary>
    /// Map WFS HTTP endpoints.
    /// Only called if the service is enabled in Configuration V2.
    /// </summary>
    public void MapEndpoints(
        IEndpointRouteBuilder endpoints,
        PluginContext context)
    {
        // NOTE: WFS endpoints are mapped by the built-in WFS handler system
        // (Honua.Server.Host.Wfs.WfsEndpointExtensions.MapWfs)
        // This plugin only provides configuration and service registration.
        // The plugin endpoint mapping is intentionally skipped to avoid conflicts
        // with the existing comprehensive WFS implementation.

        context.Logger.LogInformation(
            "WFS plugin loaded. Endpoints are mapped by built-in WFS handler system.");
    }

    /// <summary>
    /// Validate WFS configuration from Configuration V2.
    /// Called before services are registered.
    /// </summary>
    public PluginValidationResult ValidateConfiguration(IConfiguration configuration)
    {
        var result = new PluginValidationResult();

        var serviceConfig = configuration.GetSection($"honua:services:{ServiceId}");

        // Check if service is configured
        if (!serviceConfig.Exists())
        {
            result.AddError("WFS service not configured in Configuration V2");
            return result;
        }

        // Validate capabilities_cache_duration (0-86400 seconds = 24 hours)
        var capabilitiesCacheDuration = serviceConfig.GetValue<int?>("capabilities_cache_duration");
        if (capabilitiesCacheDuration.HasValue &&
            (capabilitiesCacheDuration.Value < 0 || capabilitiesCacheDuration.Value > 86400))
        {
            result.AddError("capabilities_cache_duration must be between 0 and 86400 seconds");
        }

        // Validate default_count (1-10000)
        var defaultCount = serviceConfig.GetValue<int?>("default_count");
        if (defaultCount.HasValue && (defaultCount.Value < 1 || defaultCount.Value > 10_000))
        {
            result.AddError("default_count must be between 1 and 10,000");
        }

        // Validate max_features (1-100000)
        var maxFeatures = serviceConfig.GetValue<int?>("max_features");
        if (maxFeatures.HasValue && (maxFeatures.Value < 1 || maxFeatures.Value > 100_000))
        {
            result.AddError("max_features must be between 1 and 100,000");
        }

        // Validate default_count <= max_features
        if (defaultCount.HasValue && maxFeatures.HasValue && defaultCount.Value > maxFeatures.Value)
        {
            result.AddError("default_count cannot be greater than max_features");
        }

        // Validate version
        var version = serviceConfig.GetValue<string>("version");
        if (!string.IsNullOrEmpty(version))
        {
            var validVersions = new[] { "1.0.0", "1.1.0", "2.0.0" };
            if (!validVersions.Contains(version))
            {
                result.AddWarning(
                    $"version '{version}' is not a standard WFS version. Valid versions: {string.Join(", ", validVersions)}");
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
    /// Handle WFS requests.
    /// This is a simplified implementation for demonstration purposes.
    /// In a full implementation, this would parse the request, validate parameters,
    /// execute the operation, and return the appropriate XML/JSON response.
    /// </summary>
    private static async Task<IResult> HandleWfsRequestAsync(
        HttpContext context,
        WfsPluginConfiguration config,
        ILogger<WfsServicePlugin> logger)
    {
        var request = context.Request.Query["request"].ToString().ToUpperInvariant();
        var service = context.Request.Query["service"].ToString().ToUpperInvariant();

        // Validate service parameter
        if (service != "WFS")
        {
            return Results.BadRequest(new
            {
                error = "InvalidParameterValue",
                message = "service parameter must be 'WFS'"
            });
        }

        logger.LogInformation("Handling WFS {Request} request", request);

        return request switch
        {
            "GETCAPABILITIES" => await HandleGetCapabilitiesAsync(context, config, logger),
            "DESCRIBEFEATURETYPE" => await HandleDescribeFeatureTypeAsync(context, config, logger),
            "GETFEATURE" => await HandleGetFeatureAsync(context, config, logger),
            "TRANSACTION" => await HandleTransactionAsync(context, config, logger),
            _ => Results.BadRequest(new
            {
                error = "OperationNotSupported",
                message = $"Request '{request}' is not supported"
            })
        };
    }

    private static async Task<IResult> HandleGetCapabilitiesAsync(
        HttpContext context,
        WfsPluginConfiguration config,
        ILogger<WfsServicePlugin> logger)
    {
        logger.LogInformation("Generating WFS {Version} GetCapabilities", config.Version);

        // Get metadata registry from DI
        var metadataRegistry = context.RequestServices.GetRequiredService<Core.Metadata.IMetadataRegistry>();
        var snapshot = await metadataRegistry.GetSnapshotAsync(context.RequestAborted);

        // Find WFS service and its layers
        var wfsService = snapshot.Services.FirstOrDefault(s =>
            string.Equals(s.Id, "wfs", StringComparison.OrdinalIgnoreCase));

        var featureTypeElements = new System.Text.StringBuilder();

        if (wfsService != null)
        {
            foreach (var layer in wfsService.Layers)
            {
                var crs = layer.Crs.FirstOrDefault() ?? "EPSG:4326";
                featureTypeElements.AppendLine($"""
                    <wfs:FeatureType>
                      <wfs:Name>{layer.Id}</wfs:Name>
                      <wfs:Title>{layer.Title}</wfs:Title>
                      <wfs:DefaultCRS>{crs}</wfs:DefaultCRS>
                    </wfs:FeatureType>
                """);
            }
        }

        var capabilities = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <wfs:WFS_Capabilities version="{config.Version}"
                xmlns:wfs="http://www.opengis.net/wfs/2.0"
                xmlns:ows="http://www.opengis.net/ows/1.1"
                xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <ows:ServiceIdentification>
                <ows:Title>Honua WFS Service (Plugin)</ows:Title>
                <ows:ServiceType>WFS</ows:ServiceType>
                <ows:ServiceTypeVersion>{config.Version}</ows:ServiceTypeVersion>
              </ows:ServiceIdentification>
              <ows:OperationsMetadata>
                <ows:Operation name="GetCapabilities"/>
                <ows:Operation name="DescribeFeatureType"/>
                <ows:Operation name="GetFeature"/>
              </ows:OperationsMetadata>
              <wfs:FeatureTypeList>
                {featureTypeElements}
              </wfs:FeatureTypeList>
            </wfs:WFS_Capabilities>
            """;

        context.Response.Headers.CacheControl = $"public, max-age={config.CapabilitiesCacheDuration}";

        return Results.Content(capabilities, "application/xml");
    }

    private static Task<IResult> HandleDescribeFeatureTypeAsync(
        HttpContext context,
        WfsPluginConfiguration config,
        ILogger<WfsServicePlugin> logger)
    {
        logger.LogInformation("Generating WFS DescribeFeatureType");

        // In a full implementation, this would:
        // 1. Get feature type name from request
        // 2. Query database for field metadata
        // 3. Build XML schema
        // 4. Apply caching

        var schema = """
            <?xml version="1.0" encoding="UTF-8"?>
            <xsd:schema xmlns:xsd="http://www.w3.org/2001/XMLSchema"
                xmlns:gml="http://www.opengis.net/gml/3.2">
              <xsd:element name="features" type="FeaturesType"/>
              <xsd:complexType name="FeaturesType">
                <xsd:sequence>
                  <xsd:element name="id" type="xsd:integer"/>
                  <xsd:element name="name" type="xsd:string"/>
                  <xsd:element name="geom" type="gml:GeometryPropertyType"/>
                </xsd:sequence>
              </xsd:complexType>
            </xsd:schema>
            """;

        return Task.FromResult(Results.Content(schema, "application/xml"));
    }

    private static Task<IResult> HandleGetFeatureAsync(
        HttpContext context,
        WfsPluginConfiguration config,
        ILogger<WfsServicePlugin> logger)
    {
        var count = context.Request.Query["count"].ToString();
        var featureCount = string.IsNullOrEmpty(count) ? config.DefaultCount : int.Parse(count);

        if (featureCount > config.MaxFeatures)
        {
            return Task.FromResult(Results.BadRequest(new
            {
                error = "InvalidParameterValue",
                message = $"count exceeds max_features ({config.MaxFeatures})"
            }));
        }

        logger.LogInformation("Fetching {Count} features (max: {MaxFeatures})", featureCount, config.MaxFeatures);

        // In a full implementation, this would:
        // 1. Parse filter/bbox/propertyname parameters
        // 2. Build SQL query
        // 3. Execute against database
        // 4. Stream GML/GeoJSON response

        var features = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <wfs:FeatureCollection
                xmlns:wfs="http://www.opengis.net/wfs/2.0"
                numberMatched="0" numberReturned="0">
              <!-- Features would be inserted here -->
              <!-- (Returning empty collection for demonstration) -->
            </wfs:FeatureCollection>
            """;

        return Task.FromResult(Results.Content(features, "application/xml"));
    }

    private static Task<IResult> HandleTransactionAsync(
        HttpContext context,
        WfsPluginConfiguration config,
        ILogger<WfsServicePlugin> logger)
    {
        logger.LogInformation("Processing WFS Transaction");

        // In a full implementation, this would:
        // 1. Parse transaction XML
        // 2. Validate features
        // 3. Execute Insert/Update/Delete operations
        // 4. Return transaction summary

        var response = """
            <?xml version="1.0" encoding="UTF-8"?>
            <wfs:TransactionResponse
                xmlns:wfs="http://www.opengis.net/wfs/2.0">
              <wfs:TransactionSummary>
                <wfs:totalInserted>0</wfs:totalInserted>
                <wfs:totalUpdated>0</wfs:totalUpdated>
                <wfs:totalDeleted>0</wfs:totalDeleted>
              </wfs:TransactionSummary>
            </wfs:TransactionResponse>
            """;

        return Task.FromResult(Results.Content(response, "application/xml"));
    }
}

/// <summary>
/// WFS plugin configuration extracted from Configuration V2.
/// </summary>
public sealed class WfsPluginConfiguration
{
    public int CapabilitiesCacheDuration { get; init; }
    public int DefaultCount { get; init; }
    public int MaxFeatures { get; init; }
    public string Version { get; init; } = "2.0.0";
}
