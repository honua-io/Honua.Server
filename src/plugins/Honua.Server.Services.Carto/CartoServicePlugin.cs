// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Services.Carto;

/// <summary>
/// Carto Service Plugin implementing Carto SQL API.
/// Dynamically loadable plugin that integrates with Configuration V2.
/// </summary>
public sealed class CartoServicePlugin : IServicePlugin
{
    // IHonuaPlugin implementation
    public string Id => "honua.services.carto";
    public string Name => "Carto API Plugin";
    public string Version => "1.0.0";
    public string Description => "Carto SQL API implementation for executing spatial SQL queries";
    public string Author => "HonuaIO";
    public IReadOnlyList<PluginDependency> Dependencies => Array.Empty<PluginDependency>();
    public string MinimumHonuaVersion => "1.0.0";

    // IServicePlugin implementation
    public string ServiceId => "carto";
    public ServiceType ServiceType => ServiceType.API;

    /// <summary>
    /// Called when the plugin is loaded.
    /// </summary>
    public Task OnLoadAsync(PluginContext context)
    {
        context.Logger.LogInformation(
            "Loading Carto API plugin v{Version} from {Path}",
            Version,
            context.PluginPath);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Configure dependency injection services for Carto API.
    /// Reads Configuration V2 settings and registers Carto services.
    /// </summary>
    public void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration,
        PluginContext context)
    {
        context.Logger.LogInformation("Configuring Carto API services from Configuration V2");

        // Read Configuration V2 settings for Carto
        var serviceConfig = configuration.GetSection($"honua:services:{ServiceId}");

        // Extract Carto-specific settings with defaults
        var maxQueryTime = serviceConfig.GetValue("max_query_time", 30); // 30 seconds
        var enableBatch = serviceConfig.GetValue("enable_batch", false);

        // Register Carto configuration
        services.AddSingleton(new CartoPluginConfiguration
        {
            MaxQueryTime = maxQueryTime,
            EnableBatch = enableBatch
        });

        // TODO: Register Carto-specific services:
        // - SQL query parser and validator
        // - Query execution engine
        // - GeoJSON formatter
        // - CSV formatter
        // - SQL injection protection
        // - Query timeout management
        // - Batch query handler

        context.Logger.LogInformation(
            "Carto API services configured: maxQueryTime={MaxQueryTime}s, enableBatch={EnableBatch}",
            maxQueryTime,
            enableBatch);
    }

    /// <summary>
    /// Map Carto API HTTP endpoints.
    /// Only called if the service is enabled in Configuration V2.
    /// </summary>
    public void MapEndpoints(
        IEndpointRouteBuilder endpoints,
        PluginContext context)
    {
        context.Logger.LogInformation("Mapping Carto API endpoints");

        // Get base path from configuration (default: /api/v2)
        var serviceConfig = context.Configuration.GetSection($"honua:services:{ServiceId}");
        var basePath = serviceConfig.GetValue("base_path", "/api/v2");

        // Map Carto endpoint group
        var cartoGroup = endpoints.MapGroup(basePath)
            .WithTags("Carto API")
            .WithMetadata("Service", "Carto");

        // SQL query endpoint (GET)
        cartoGroup.MapGet("sql", HandleSqlQueryAsync)
            .WithName("Carto-SQL-Get")
            .WithDisplayName("Carto SQL Query (GET)")
            .WithDescription("Execute SQL query and return results in GeoJSON, JSON, or CSV format");

        // SQL query endpoint (POST)
        cartoGroup.MapPost("sql", HandleSqlQueryAsync)
            .WithName("Carto-SQL-Post")
            .WithDisplayName("Carto SQL Query (POST)")
            .WithDescription("Execute SQL query via POST");

        // Batch SQL endpoint (conditionally mapped if enabled)
        var enableBatch = serviceConfig.GetValue("enable_batch", false);
        if (enableBatch)
        {
            cartoGroup.MapPost("sql/batch", HandleBatchSqlQueryAsync)
                .WithName("Carto-SQL-Batch")
                .WithDisplayName("Carto SQL Batch Query")
                .WithDescription("Execute multiple SQL queries in batch");
        }

        context.Logger.LogInformation("Carto API endpoints mapped at {BasePath}", basePath);
    }

    /// <summary>
    /// Validate Carto configuration from Configuration V2.
    /// Called before services are registered.
    /// </summary>
    public PluginValidationResult ValidateConfiguration(IConfiguration configuration)
    {
        var result = new PluginValidationResult();

        var serviceConfig = configuration.GetSection($"honua:services:{ServiceId}");

        // Check if service is configured
        if (!serviceConfig.Exists())
        {
            result.AddError("Carto service not configured in Configuration V2");
            return result;
        }

        // Validate max_query_time
        var maxQueryTime = serviceConfig.GetValue<int?>("max_query_time");
        if (maxQueryTime.HasValue && (maxQueryTime.Value < 1 || maxQueryTime.Value > 300))
        {
            result.AddError("max_query_time must be between 1 and 300 seconds");
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
    /// Handle Carto SQL query requests.
    /// TODO: Full implementation needed for:
    /// - Parse and validate SQL query
    /// - Execute query with timeout
    /// - Format results as GeoJSON/JSON/CSV
    /// - Handle spatial functions (ST_*, etc.)
    /// - Apply security restrictions (whitelist tables, prevent writes)
    /// </summary>
    private static Task<IResult> HandleSqlQueryAsync(
        HttpContext context,
        CartoPluginConfiguration config,
        ILogger<CartoServicePlugin> logger)
    {
        var query = context.Request.Query["q"].ToString();
        var format = context.Request.Query["format"].ToString();

        logger.LogInformation("Carto SQL query request - implementation pending. Format: {Format}", format);

        var message = $"Carto SQL API is configured but query execution is pending. Query length: {query?.Length ?? 0}";
        return Task.FromResult(Results.Ok(new { message, format }));
    }

    /// <summary>
    /// Handle Carto batch SQL query requests.
    /// TODO: Full implementation needed for batch query execution
    /// </summary>
    private static Task<IResult> HandleBatchSqlQueryAsync(
        HttpContext context,
        CartoPluginConfiguration config,
        ILogger<CartoServicePlugin> logger)
    {
        logger.LogInformation("Carto batch SQL query request - implementation pending");

        var message = "Carto SQL API batch queries are configured but implementation is pending.";
        return Task.FromResult(Results.Ok(new { message }));
    }
}

/// <summary>
/// Carto plugin configuration extracted from Configuration V2.
/// </summary>
public sealed class CartoPluginConfiguration
{
    public int MaxQueryTime { get; init; }
    public bool EnableBatch { get; init; }
}
