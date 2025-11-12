// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Services.Csw;

/// <summary>
/// CSW Service Plugin implementing OGC Catalog Service for the Web protocol.
/// Dynamically loadable plugin that integrates with Configuration V2.
/// </summary>
public sealed class CswServicePlugin : IServicePlugin
{
    // IHonuaPlugin implementation
    public string Id => "honua.services.csw";
    public string Name => "CSW Service Plugin";
    public string Version => "1.0.0";
    public string Description => "OGC Catalog Service for the Web (CSW) protocol implementation";
    public string Author => "HonuaIO";
    public IReadOnlyList<PluginDependency> Dependencies => Array.Empty<PluginDependency>();
    public string MinimumHonuaVersion => "1.0.0";

    // IServicePlugin implementation
    public string ServiceId => "csw";
    public ServiceType ServiceType => ServiceType.OGC;

    /// <summary>
    /// Called when the plugin is loaded.
    /// </summary>
    public Task OnLoadAsync(PluginContext context)
    {
        context.Logger.LogInformation(
            "Loading CSW plugin v{Version} from {Path}",
            Version,
            context.PluginPath);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Configure dependency injection services for CSW.
    /// Reads Configuration V2 settings and registers CSW services.
    /// </summary>
    public void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration,
        PluginContext context)
    {
        context.Logger.LogInformation("Configuring CSW services from Configuration V2");

        // Read Configuration V2 settings for CSW
        var serviceConfig = configuration.GetSection($"honua:services:{ServiceId}");

        // Extract CSW-specific settings with defaults
        var capabilitiesCacheDuration = serviceConfig.GetValue("capabilities_cache_duration", 3600);
        var maxRecords = serviceConfig.GetValue("max_records", 1000);
        var version = serviceConfig.GetValue("version", "2.0.2");

        // Register CSW configuration
        services.AddSingleton(new CswPluginConfiguration
        {
            CapabilitiesCacheDuration = capabilitiesCacheDuration,
            MaxRecords = maxRecords,
            Version = version
        });

        // TODO: Register CSW-specific services:
        // - CSW capabilities builder
        // - Metadata catalog index/search
        // - CSW filter parser (OGC Filter Encoding)
        // - GetRecords query handler
        // - GetRecordById handler
        // - Harvest operation (optional)
        // - Transaction support (Insert, Update, Delete metadata)
        // - ISO 19115/19139 metadata formatter
        // - Dublin Core formatter

        context.Logger.LogInformation(
            "CSW services configured: version={Version}, maxRecords={MaxRecords}",
            version,
            maxRecords);
    }

    /// <summary>
    /// Map CSW HTTP endpoints.
    /// Only called if the service is enabled in Configuration V2.
    /// </summary>
    public void MapEndpoints(
        IEndpointRouteBuilder endpoints,
        PluginContext context)
    {
        context.Logger.LogInformation("Mapping CSW endpoints");

        // Get base path from configuration (default: /csw)
        var serviceConfig = context.Configuration.GetSection($"honua:services:{ServiceId}");
        var basePath = serviceConfig.GetValue("base_path", "/csw");

        // Map CSW endpoint group
        var cswGroup = endpoints.MapGroup(basePath)
            .WithTags("CSW", "OGC")
            .WithMetadata("Service", "CSW");

        // Map CSW endpoints
        cswGroup.MapGet(string.Empty, HandleCswRequestAsync)
            .WithName("CSW-Get")
            .WithDisplayName("CSW GET Request")
            .WithDescription("Handles CSW GET requests (GetCapabilities, GetRecords, GetRecordById)");

        cswGroup.MapPost(string.Empty, HandleCswRequestAsync)
            .WithName("CSW-Post")
            .WithDisplayName("CSW POST Request")
            .WithDescription("Handles CSW POST requests (GetRecords with Filter, Transaction, Harvest)");

        context.Logger.LogInformation("CSW endpoints mapped at {BasePath}", basePath);
    }

    /// <summary>
    /// Validate CSW configuration from Configuration V2.
    /// Called before services are registered.
    /// </summary>
    public PluginValidationResult ValidateConfiguration(IConfiguration configuration)
    {
        var result = new PluginValidationResult();

        var serviceConfig = configuration.GetSection($"honua:services:{ServiceId}");

        // Check if service is configured
        if (!serviceConfig.Exists())
        {
            result.AddError("CSW service not configured in Configuration V2");
            return result;
        }

        // Validate version
        var version = serviceConfig.GetValue<string>("version");
        if (!string.IsNullOrEmpty(version))
        {
            var validVersions = new[] { "2.0.2", "3.0.0" };
            if (!validVersions.Contains(version))
            {
                result.AddWarning(
                    $"version '{version}' is not a standard CSW version. Valid versions: {string.Join(", ", validVersions)}");
            }
        }

        // Validate max_records
        var maxRecords = serviceConfig.GetValue<int?>("max_records");
        if (maxRecords.HasValue && (maxRecords.Value < 1 || maxRecords.Value > 10_000))
        {
            result.AddError("max_records must be between 1 and 10,000");
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
    /// Handle CSW requests.
    /// TODO: Full implementation needed for:
    /// - GetCapabilities: Generate CSW capabilities XML
    /// - GetRecords: Search metadata catalog with filters
    /// - GetRecordById: Retrieve specific metadata record
    /// - DescribeRecord: Describe record schemas
    /// - GetDomain: Query value domains for properties
    /// - Transaction: Insert/Update/Delete metadata (optional)
    /// - Harvest: Harvest metadata from external sources (optional)
    /// </summary>
    private static Task<IResult> HandleCswRequestAsync(
        HttpContext context,
        CswPluginConfiguration config,
        ILogger<CswServicePlugin> logger)
    {
        var request = context.Request.Query["request"].ToString().ToUpperInvariant();
        var service = context.Request.Query["service"].ToString().ToUpperInvariant();

        // Validate service parameter
        if (service != "CSW")
        {
            return Task.FromResult(Results.BadRequest(new
            {
                error = "InvalidParameterValue",
                message = "service parameter must be 'CSW'"
            }));
        }

        logger.LogInformation("CSW service configured but implementation pending for request: {Request}", request);

        var message = $"CSW service is configured but full implementation is pending. Request: {request}";
        return Task.FromResult(Results.Ok(new { message, request, version = config.Version }));
    }
}

/// <summary>
/// CSW plugin configuration extracted from Configuration V2.
/// </summary>
public sealed class CswPluginConfiguration
{
    public int CapabilitiesCacheDuration { get; init; }
    public int MaxRecords { get; init; }
    public string Version { get; init; } = "2.0.2";
}
