// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Core.Configuration.V2.Services.Implementations;

/// <summary>
/// Service registration for Web Map Service (WMS).
/// Enables declarative WMS configuration from .honua files.
/// </summary>
[ServiceRegistration("wms", Priority = 30)]
public sealed class WmsServiceRegistration : IServiceRegistration
{
    public string ServiceId => "wms";
    public string DisplayName => "Web Map Service (WMS)";
    public string Description => "OGC Web Map Service for rendering map images";

    public void ConfigureServices(IServiceCollection services, ServiceBlock serviceConfig)
    {
        // Extract WMS-specific settings
        var maxWidth = GetSetting<int>(serviceConfig, "max_width", 4096);
        var maxHeight = GetSetting<int>(serviceConfig, "max_height", 4096);
        var renderTimeoutSeconds = GetSetting<int>(serviceConfig, "render_timeout_seconds", 60);
        var enableStreaming = GetSetting<bool>(serviceConfig, "enable_streaming", true);
        var version = GetSetting<string>(serviceConfig, "version", "1.3.0");

        // TODO: When WMS implementation is fully integrated with Configuration V2, register actual WMS services
        // Example (pseudo-code):
        // services.Configure<WmsOptions>(options => {
        //     options.MaxWidth = maxWidth;
        //     options.MaxHeight = maxHeight;
        //     options.RenderTimeoutSeconds = renderTimeoutSeconds;
        //     options.EnableStreaming = enableStreaming;
        // });

        // For now, register metadata
        services.AddSingleton(new WmsServiceConfiguration
        {
            MaxWidth = maxWidth,
            MaxHeight = maxHeight,
            RenderTimeoutSeconds = renderTimeoutSeconds,
            EnableStreaming = enableStreaming,
            Version = version
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, ServiceBlock serviceConfig)
    {
        // TODO: When WMS implementation is fully integrated, map actual endpoints
        // Example (pseudo-code):
        // endpoints.MapWms();

        // For now, just a placeholder
        endpoints.MapGet("/wms", () => new
        {
            service = "WMS",
            status = "configured",
            message = "WMS service is configured but full integration pending"
        });
    }

    public ServiceValidationResult ValidateConfiguration(ServiceBlock serviceConfig)
    {
        var result = new ServiceValidationResult();

        // Validate max_width (256-16384)
        if (serviceConfig.Settings.TryGetValue("max_width", out var maxWidthObj))
        {
            if (maxWidthObj is int maxWidth && (maxWidth < 256 || maxWidth > 16384))
            {
                result.AddError("max_width must be between 256 and 16384 pixels");
            }
        }

        // Validate max_height (256-16384)
        if (serviceConfig.Settings.TryGetValue("max_height", out var maxHeightObj))
        {
            if (maxHeightObj is int maxHeight && (maxHeight < 256 || maxHeight > 16384))
            {
                result.AddError("max_height must be between 256 and 16384 pixels");
            }
        }

        // Validate render_timeout_seconds (5-300)
        if (serviceConfig.Settings.TryGetValue("render_timeout_seconds", out var timeoutObj))
        {
            if (timeoutObj is int timeout && (timeout < 5 || timeout > 300))
            {
                result.AddError("render_timeout_seconds must be between 5 and 300 seconds");
            }
        }

        // Validate version
        if (serviceConfig.Settings.TryGetValue("version", out var versionObj))
        {
            if (versionObj is string version)
            {
                var validVersions = new[] { "1.1.1", "1.3.0" };
                if (!validVersions.Contains(version))
                {
                    result.AddWarning($"version '{version}' is not a standard WMS version. Valid versions: {string.Join(", ", validVersions)}");
                }
            }
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
/// WMS service configuration extracted from declarative config.
/// </summary>
public sealed class WmsServiceConfiguration
{
    public int MaxWidth { get; init; }
    public int MaxHeight { get; init; }
    public int RenderTimeoutSeconds { get; init; }
    public bool EnableStreaming { get; init; }
    public string Version { get; init; } = "1.3.0";
}
