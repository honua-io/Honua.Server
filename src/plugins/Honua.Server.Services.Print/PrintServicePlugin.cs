// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Services.Print;

/// <summary>
/// Print Service Plugin implementing MapFish Print protocol.
/// Dynamically loadable plugin that integrates with Configuration V2.
/// </summary>
public sealed class PrintServicePlugin : IServicePlugin
{
    // IHonuaPlugin implementation
    public string Id => "honua.services.print";
    public string Name => "MapFish Print Plugin";
    public string Version => "1.0.0";
    public string Description => "MapFish Print service for generating PDF/PNG map exports";
    public string Author => "HonuaIO";
    public IReadOnlyList<PluginDependency> Dependencies => Array.Empty<PluginDependency>();
    public string MinimumHonuaVersion => "1.0.0";

    // IServicePlugin implementation
    public string ServiceId => "print";
    public ServiceType ServiceType => ServiceType.Specialized;

    /// <summary>
    /// Called when the plugin is loaded.
    /// </summary>
    public Task OnLoadAsync(PluginContext context)
    {
        context.Logger.LogInformation(
            "Loading Print plugin v{Version} from {Path}",
            Version,
            context.PluginPath);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Configure dependency injection services for Print.
    /// Reads Configuration V2 settings and registers Print services.
    /// </summary>
    public void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration,
        PluginContext context)
    {
        context.Logger.LogInformation("Configuring Print services from Configuration V2");

        // Read Configuration V2 settings for Print
        var serviceConfig = configuration.GetSection($"honua:services:{ServiceId}");

        // Extract Print-specific settings with defaults
        var maxPrintTimeout = serviceConfig.GetValue("max_print_timeout", 120); // 2 minutes
        var maxDpi = serviceConfig.GetValue("max_dpi", 300);
        var outputFormats = serviceConfig.GetValue("output_formats", "pdf,png");

        // Register Print configuration
        services.AddSingleton(new PrintPluginConfiguration
        {
            MaxPrintTimeout = maxPrintTimeout,
            MaxDpi = maxDpi,
            OutputFormats = outputFormats
        });

        // TODO: Register Print-specific services:
        // - Print template parser
        // - PDF renderer (using SkiaSharp or similar)
        // - PNG renderer
        // - Map layer composer
        // - Print job queue manager
        // - Template storage
        // - Legend generator
        // - Scale bar generator
        // - North arrow generator

        context.Logger.LogInformation(
            "Print services configured: maxPrintTimeout={MaxPrintTimeout}s, maxDpi={MaxDpi}, outputFormats={OutputFormats}",
            maxPrintTimeout,
            maxDpi,
            outputFormats);
    }

    /// <summary>
    /// Map Print HTTP endpoints.
    /// Only called if the service is enabled in Configuration V2.
    /// </summary>
    public void MapEndpoints(
        IEndpointRouteBuilder endpoints,
        PluginContext context)
    {
        context.Logger.LogInformation("Mapping Print endpoints");

        // Get base path from configuration (default: /print)
        var serviceConfig = context.Configuration.GetSection($"honua:services:{ServiceId}");
        var basePath = serviceConfig.GetValue("base_path", "/print");

        // Map Print endpoint group
        var printGroup = endpoints.MapGroup(basePath)
            .WithTags("Print", "MapFish")
            .WithMetadata("Service", "Print");

        // Get capabilities (list of available apps/templates)
        printGroup.MapGet("capabilities.json", HandleCapabilitiesAsync)
            .WithName("Print-Capabilities")
            .WithDisplayName("Print Capabilities")
            .WithDescription("Returns available print apps and their configurations");

        // Create print job
        printGroup.MapPost("{app}/report.{format}", HandleCreatePrintJobAsync)
            .WithName("Print-CreateJob")
            .WithDisplayName("Create Print Job")
            .WithDescription("Create a new print job and return job ID");

        // Get print job status
        printGroup.MapGet("{app}/status/{jobId}.json", HandleGetStatusAsync)
            .WithName("Print-GetStatus")
            .WithDisplayName("Get Print Job Status")
            .WithDescription("Get the status of a print job");

        // Download print output
        printGroup.MapGet("{app}/report/{jobId}", HandleDownloadAsync)
            .WithName("Print-Download")
            .WithDisplayName("Download Print Output")
            .WithDescription("Download the completed print output");

        // Cancel print job
        printGroup.MapDelete("{app}/cancel/{jobId}", HandleCancelAsync)
            .WithName("Print-Cancel")
            .WithDisplayName("Cancel Print Job")
            .WithDescription("Cancel a running print job");

        context.Logger.LogInformation("Print endpoints mapped at {BasePath}", basePath);
    }

    /// <summary>
    /// Validate Print configuration from Configuration V2.
    /// Called before services are registered.
    /// </summary>
    public PluginValidationResult ValidateConfiguration(IConfiguration configuration)
    {
        var result = new PluginValidationResult();

        var serviceConfig = configuration.GetSection($"honua:services:{ServiceId}");

        // Check if service is configured
        if (!serviceConfig.Exists())
        {
            result.AddError("Print service not configured in Configuration V2");
            return result;
        }

        // Validate max_print_timeout
        var maxPrintTimeout = serviceConfig.GetValue<int?>("max_print_timeout");
        if (maxPrintTimeout.HasValue && (maxPrintTimeout.Value < 1 || maxPrintTimeout.Value > 600))
        {
            result.AddError("max_print_timeout must be between 1 and 600 seconds");
        }

        // Validate max_dpi
        var maxDpi = serviceConfig.GetValue<int?>("max_dpi");
        if (maxDpi.HasValue && (maxDpi.Value < 72 || maxDpi.Value > 600))
        {
            result.AddError("max_dpi must be between 72 and 600");
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
    /// Handle print capabilities requests.
    /// TODO: Return available print apps and templates
    /// </summary>
    private static Task<IResult> HandleCapabilitiesAsync(
        HttpContext context,
        PrintPluginConfiguration config,
        ILogger<PrintServicePlugin> logger)
    {
        logger.LogInformation("Print capabilities request - implementation pending");

        var message = "Print service is configured but capabilities listing is pending.";
        return Task.FromResult(Results.Ok(new { message }));
    }

    /// <summary>
    /// Handle create print job requests.
    /// TODO: Full implementation needed for:
    /// - Parse print spec JSON
    /// - Validate template and parameters
    /// - Queue print job
    /// - Return job ID
    /// </summary>
    private static Task<IResult> HandleCreatePrintJobAsync(
        HttpContext context,
        string app,
        string format,
        PrintPluginConfiguration config,
        ILogger<PrintServicePlugin> logger)
    {
        logger.LogInformation("Create print job request for app={App}, format={Format} - implementation pending", app, format);

        var message = $"Print service is configured but job creation is pending. App: {app}, Format: {format}";
        return Task.FromResult(Results.Ok(new { message, app, format }));
    }

    /// <summary>
    /// Handle get status requests.
    /// TODO: Return print job status (pending, running, finished, error)
    /// </summary>
    private static Task<IResult> HandleGetStatusAsync(
        HttpContext context,
        string app,
        string jobId,
        PrintPluginConfiguration config,
        ILogger<PrintServicePlugin> logger)
    {
        logger.LogInformation("Get print job status for {App}/{JobId} - implementation pending", app, jobId);

        var message = $"Print service is configured but status check is pending. App: {app}, JobId: {jobId}";
        return Task.FromResult(Results.Ok(new { message, app, jobId }));
    }

    /// <summary>
    /// Handle download requests.
    /// TODO: Return completed print output (PDF/PNG)
    /// </summary>
    private static Task<IResult> HandleDownloadAsync(
        HttpContext context,
        string app,
        string jobId,
        PrintPluginConfiguration config,
        ILogger<PrintServicePlugin> logger)
    {
        logger.LogInformation("Download print output for {App}/{JobId} - implementation pending", app, jobId);

        var message = $"Print service is configured but download is pending. App: {app}, JobId: {jobId}";
        return Task.FromResult(Results.Ok(new { message, app, jobId }));
    }

    /// <summary>
    /// Handle cancel requests.
    /// TODO: Cancel running print job
    /// </summary>
    private static Task<IResult> HandleCancelAsync(
        HttpContext context,
        string app,
        string jobId,
        PrintPluginConfiguration config,
        ILogger<PrintServicePlugin> logger)
    {
        logger.LogInformation("Cancel print job for {App}/{JobId} - implementation pending", app, jobId);

        var message = $"Print service is configured but cancellation is pending. App: {app}, JobId: {jobId}";
        return Task.FromResult(Results.Ok(new { message, app, jobId }));
    }
}

/// <summary>
/// Print plugin configuration extracted from Configuration V2.
/// </summary>
public sealed class PrintPluginConfiguration
{
    public int MaxPrintTimeout { get; init; }
    public int MaxDpi { get; init; }
    public string OutputFormats { get; init; } = "pdf,png";
}
