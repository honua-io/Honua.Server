// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Services.Zarr;

/// <summary>
/// Zarr Service Plugin implementing Zarr API for multidimensional arrays.
/// Dynamically loadable plugin that integrates with Configuration V2.
/// </summary>
public sealed class ZarrServicePlugin : IServicePlugin
{
    // IHonuaPlugin implementation
    public string Id => "honua.services.zarr";
    public string Name => "Zarr Time-Series Plugin";
    public string Version => "1.0.0";
    public string Description => "Zarr API for serving multidimensional time-series and gridded data";
    public string Author => "HonuaIO";
    public IReadOnlyList<PluginDependency> Dependencies => Array.Empty<PluginDependency>();
    public string MinimumHonuaVersion => "1.0.0";

    // IServicePlugin implementation
    public string ServiceId => "zarr";
    public ServiceType ServiceType => ServiceType.Specialized;

    /// <summary>
    /// Called when the plugin is loaded.
    /// </summary>
    public Task OnLoadAsync(PluginContext context)
    {
        context.Logger.LogInformation(
            "Loading Zarr plugin v{Version} from {Path}",
            Version,
            context.PluginPath);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Configure dependency injection services for Zarr.
    /// Reads Configuration V2 settings and registers Zarr services.
    /// </summary>
    public void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration,
        PluginContext context)
    {
        context.Logger.LogInformation("Configuring Zarr services from Configuration V2");

        // Read Configuration V2 settings for Zarr
        var serviceConfig = configuration.GetSection($"honua:services:{ServiceId}");

        // Extract Zarr-specific settings with defaults
        var chunkCacheDuration = serviceConfig.GetValue("chunk_cache_duration", 3600);
        var maxChunkSize = serviceConfig.GetValue("max_chunk_size", 10_485_760); // 10MB

        // Register Zarr configuration
        services.AddSingleton(new ZarrPluginConfiguration
        {
            ChunkCacheDuration = chunkCacheDuration,
            MaxChunkSize = maxChunkSize
        });

        // TODO: Register Zarr-specific services:
        // - Zarr metadata reader (.zarray, .zgroup, .zattrs)
        // - Chunk storage/retrieval
        // - Array subsetting handler
        // - Compression codec support (gzip, blosc, zstd)
        // - Dimension coordinate handling
        // - Time-series slicing
        // - NetCDF/HDF5 to Zarr converter

        context.Logger.LogInformation(
            "Zarr services configured: chunkCacheDuration={ChunkCacheDuration}s, maxChunkSize={MaxChunkSize}",
            chunkCacheDuration,
            maxChunkSize);
    }

    /// <summary>
    /// Map Zarr HTTP endpoints.
    /// Only called if the service is enabled in Configuration V2.
    /// </summary>
    public void MapEndpoints(
        IEndpointRouteBuilder endpoints,
        PluginContext context)
    {
        context.Logger.LogInformation("Mapping Zarr endpoints");

        // Get base path from configuration (default: /zarr)
        var serviceConfig = context.Configuration.GetSection($"honua:services:{ServiceId}");
        var basePath = serviceConfig.GetValue("base_path", "/zarr");

        // Map Zarr endpoint group
        var zarrGroup = endpoints.MapGroup(basePath)
            .WithTags("Zarr")
            .WithMetadata("Service", "Zarr");

        // Root metadata
        zarrGroup.MapGet("{dataset}/.zgroup", HandleZGroupAsync)
            .WithName("Zarr-ZGroup")
            .WithDisplayName("Zarr Group Metadata")
            .WithDescription("Returns Zarr group metadata");

        // Array metadata
        zarrGroup.MapGet("{dataset}/{variable}/.zarray", HandleZArrayAsync)
            .WithName("Zarr-ZArray")
            .WithDisplayName("Zarr Array Metadata")
            .WithDescription("Returns Zarr array metadata");

        // Array attributes
        zarrGroup.MapGet("{dataset}/{variable}/.zattrs", HandleZAttrsAsync)
            .WithName("Zarr-ZAttrs")
            .WithDisplayName("Zarr Attributes")
            .WithDescription("Returns Zarr array attributes");

        // Chunk data
        zarrGroup.MapGet("{dataset}/{variable}/{*chunkPath}", HandleChunkAsync)
            .WithName("Zarr-Chunk")
            .WithDisplayName("Zarr Chunk Data")
            .WithDescription("Returns Zarr chunk data");

        context.Logger.LogInformation("Zarr endpoints mapped at {BasePath}", basePath);
    }

    /// <summary>
    /// Validate Zarr configuration from Configuration V2.
    /// Called before services are registered.
    /// </summary>
    public PluginValidationResult ValidateConfiguration(IConfiguration configuration)
    {
        var result = new PluginValidationResult();

        var serviceConfig = configuration.GetSection($"honua:services:{ServiceId}");

        // Check if service is configured
        if (!serviceConfig.Exists())
        {
            result.AddError("Zarr service not configured in Configuration V2");
            return result;
        }

        // Validate max_chunk_size
        var maxChunkSize = serviceConfig.GetValue<long?>("max_chunk_size");
        if (maxChunkSize.HasValue && (maxChunkSize.Value < 1 || maxChunkSize.Value > 100_000_000))
        {
            result.AddError("max_chunk_size must be between 1 and 100,000,000 bytes");
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
    /// Handle Zarr group metadata requests.
    /// TODO: Return .zgroup metadata
    /// </summary>
    private static Task<IResult> HandleZGroupAsync(
        HttpContext context,
        string dataset,
        ZarrPluginConfiguration config,
        ILogger<ZarrServicePlugin> logger)
    {
        logger.LogInformation("Zarr group metadata request for {Dataset} - implementation pending", dataset);

        var message = $"Zarr service is configured but group metadata is pending. Dataset: {dataset}";
        return Task.FromResult(Results.Ok(new { message, dataset }));
    }

    /// <summary>
    /// Handle Zarr array metadata requests.
    /// TODO: Return .zarray metadata (shape, chunks, dtype, compressor, fill_value, order)
    /// </summary>
    private static Task<IResult> HandleZArrayAsync(
        HttpContext context,
        string dataset,
        string variable,
        ZarrPluginConfiguration config,
        ILogger<ZarrServicePlugin> logger)
    {
        logger.LogInformation("Zarr array metadata request for {Dataset}/{Variable} - implementation pending", dataset, variable);

        var message = $"Zarr service is configured but array metadata is pending. Dataset: {dataset}, Variable: {variable}";
        return Task.FromResult(Results.Ok(new { message, dataset, variable }));
    }

    /// <summary>
    /// Handle Zarr attributes requests.
    /// TODO: Return .zattrs metadata
    /// </summary>
    private static Task<IResult> HandleZAttrsAsync(
        HttpContext context,
        string dataset,
        string variable,
        ZarrPluginConfiguration config,
        ILogger<ZarrServicePlugin> logger)
    {
        logger.LogInformation("Zarr attributes request for {Dataset}/{Variable} - implementation pending", dataset, variable);

        var message = $"Zarr service is configured but attributes are pending. Dataset: {dataset}, Variable: {variable}";
        return Task.FromResult(Results.Ok(new { message, dataset, variable }));
    }

    /// <summary>
    /// Handle Zarr chunk data requests.
    /// TODO: Full implementation needed for:
    /// - Read chunk from storage (filesystem, object storage, database)
    /// - Apply compression/decompression
    /// - Return binary chunk data
    /// - Handle chunk caching
    /// </summary>
    private static Task<IResult> HandleChunkAsync(
        HttpContext context,
        string dataset,
        string variable,
        string chunkPath,
        ZarrPluginConfiguration config,
        ILogger<ZarrServicePlugin> logger)
    {
        logger.LogInformation("Zarr chunk request for {Dataset}/{Variable}/{ChunkPath} - implementation pending", dataset, variable, chunkPath);

        var message = $"Zarr service is configured but chunk retrieval is pending. Dataset: {dataset}, Variable: {variable}, Chunk: {chunkPath}";
        return Task.FromResult(Results.Ok(new { message, dataset, variable, chunkPath }));
    }
}

/// <summary>
/// Zarr plugin configuration extracted from Configuration V2.
/// </summary>
public sealed class ZarrPluginConfiguration
{
    public int ChunkCacheDuration { get; init; }
    public long MaxChunkSize { get; init; }
}
