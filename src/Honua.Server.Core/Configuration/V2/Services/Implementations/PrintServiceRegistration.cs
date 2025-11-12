// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Core.Configuration.V2.Services.Implementations;

/// <summary>
/// Service registration for MapFish Print Service.
/// Enables declarative Print configuration from .honua files.
/// </summary>
[ServiceRegistration("print", Priority = 110)]
public sealed class PrintServiceRegistration : IServiceRegistration
{
    public string ServiceId => "print";
    public string DisplayName => "MapFish Print Service";
    public string Description => "Map printing service compatible with MapFish Print protocol";

    public void ConfigureServices(IServiceCollection services, ServiceBlock serviceConfig)
    {
        // Extract Print-specific settings
        var enablePdfOutput = GetSetting<bool>(serviceConfig, "enable_pdf_output", true);
        var enablePngOutput = GetSetting<bool>(serviceConfig, "enable_png_output", true);
        var maxDpi = GetSetting<int>(serviceConfig, "max_dpi", 300);
        var defaultDpi = GetSetting<int>(serviceConfig, "default_dpi", 150);
        var maxMapWidth = GetSetting<int>(serviceConfig, "max_map_width", 4096);
        var maxMapHeight = GetSetting<int>(serviceConfig, "max_map_height", 4096);
        var renderTimeoutSeconds = GetSetting<int>(serviceConfig, "render_timeout_seconds", 120);
        var enableCaching = GetSetting<bool>(serviceConfig, "enable_caching", false);

        // TODO: When Print implementation is fully integrated with Configuration V2, register actual Print services
        // For now, register metadata
        services.AddSingleton(new PrintServiceConfiguration
        {
            EnablePdfOutput = enablePdfOutput,
            EnablePngOutput = enablePngOutput,
            MaxDpi = maxDpi,
            DefaultDpi = defaultDpi,
            MaxMapWidth = maxMapWidth,
            MaxMapHeight = maxMapHeight,
            RenderTimeoutSeconds = renderTimeoutSeconds,
            EnableCaching = enableCaching
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, ServiceBlock serviceConfig)
    {
        // TODO: When Print implementation is fully integrated, map actual endpoints
        // Example (pseudo-code):
        // endpoints.MapMapFishPrint();

        // For now, just a placeholder
        endpoints.MapGet("/print", () => new
        {
            service = "MapFish Print",
            status = "configured",
            message = "Print service is configured but full integration pending"
        });
    }

    public ServiceValidationResult ValidateConfiguration(ServiceBlock serviceConfig)
    {
        var result = new ServiceValidationResult();

        // Validate max_dpi (72-600)
        if (serviceConfig.Settings.TryGetValue("max_dpi", out var maxDpiObj))
        {
            if (maxDpiObj is int maxDpi && (maxDpi < 72 || maxDpi > 600))
            {
                result.AddError("max_dpi must be between 72 and 600");
            }
        }

        // Validate default_dpi (72-600)
        if (serviceConfig.Settings.TryGetValue("default_dpi", out var defaultDpiObj))
        {
            if (defaultDpiObj is int defaultDpi && (defaultDpi < 72 || defaultDpi > 600))
            {
                result.AddError("default_dpi must be between 72 and 600");
            }
        }

        // Validate default_dpi <= max_dpi
        if (serviceConfig.Settings.TryGetValue("default_dpi", out var defDpiObj) &&
            serviceConfig.Settings.TryGetValue("max_dpi", out var maxDpiCheckObj))
        {
            if (defDpiObj is int defDpi && maxDpiCheckObj is int maxDpiCheck && defDpi > maxDpiCheck)
            {
                result.AddError("default_dpi cannot be greater than max_dpi");
            }
        }

        // Validate max_map_width (256-16384)
        if (serviceConfig.Settings.TryGetValue("max_map_width", out var maxWidthObj))
        {
            if (maxWidthObj is int maxWidth && (maxWidth < 256 || maxWidth > 16384))
            {
                result.AddError("max_map_width must be between 256 and 16384 pixels");
            }
        }

        // Validate max_map_height (256-16384)
        if (serviceConfig.Settings.TryGetValue("max_map_height", out var maxHeightObj))
        {
            if (maxHeightObj is int maxHeight && (maxHeight < 256 || maxHeight > 16384))
            {
                result.AddError("max_map_height must be between 256 and 16384 pixels");
            }
        }

        // Validate render_timeout_seconds (5-600)
        if (serviceConfig.Settings.TryGetValue("render_timeout_seconds", out var timeoutObj))
        {
            if (timeoutObj is int timeout && (timeout < 5 || timeout > 600))
            {
                result.AddError("render_timeout_seconds must be between 5 and 600 seconds");
            }
        }

        // Warn if both output formats are disabled
        var pdfEnabled = GetSetting<bool>(serviceConfig, "enable_pdf_output", true);
        var pngEnabled = GetSetting<bool>(serviceConfig, "enable_png_output", true);
        if (!pdfEnabled && !pngEnabled)
        {
            result.AddWarning("Both enable_pdf_output and enable_png_output are false. At least one output format should be enabled.");
        }

        return result;
    }

    private static T GetSetting<T>(ServiceBlock serviceConfig, string key, T defaultValue)
    {
        if (serviceConfig.Settings.TryGetValue(key, out var value))
        {
            if (value is T typedValue)
            {
                return typedValue;
            }

            // Try to convert
            try
            {
                return (T)Convert.ChangeType(value, typeof(T))!;
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }
}

/// <summary>
/// Print service configuration extracted from declarative config.
/// </summary>
public sealed class PrintServiceConfiguration
{
    public bool EnablePdfOutput { get; init; }
    public bool EnablePngOutput { get; init; }
    public int MaxDpi { get; init; }
    public int DefaultDpi { get; init; }
    public int MaxMapWidth { get; init; }
    public int MaxMapHeight { get; init; }
    public int RenderTimeoutSeconds { get; init; }
    public bool EnableCaching { get; init; }
}
