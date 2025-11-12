// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Core.Configuration.V2.Services.Implementations;

/// <summary>
/// Service registration for Web Map Tile Service (WMTS).
/// Enables declarative WMTS configuration from .honua files.
/// </summary>
[ServiceRegistration("wmts", Priority = 40)]
public sealed class WmtsServiceRegistration : IServiceRegistration
{
    public string ServiceId => "wmts";
    public string DisplayName => "Web Map Tile Service (WMTS)";
    public string Description => "OGC Web Map Tile Service for pre-rendered or dynamic map tiles";

    public void ConfigureServices(IServiceCollection services, ServiceBlock serviceConfig)
    {
        // Extract WMTS-specific settings
        var version = GetSetting<string>(serviceConfig, "version", "1.0.0");
        var tileSize = GetSetting<int>(serviceConfig, "tile_size", 256);
        var supportedFormats = GetSetting<string[]>(serviceConfig, "supported_formats", new[] { "image/png", "image/jpeg" });
        var enableCaching = GetSetting<bool>(serviceConfig, "enable_caching", true);
        var maxFeatureCount = GetSetting<int>(serviceConfig, "max_feature_count", 50);

        // TODO: When WMTS implementation is fully integrated with Configuration V2, register actual WMTS services
        // For now, register metadata
        services.AddSingleton(new WmtsServiceConfiguration
        {
            Version = version,
            TileSize = tileSize,
            SupportedFormats = supportedFormats,
            EnableCaching = enableCaching,
            MaxFeatureCount = maxFeatureCount
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, ServiceBlock serviceConfig)
    {
        // TODO: When WMTS implementation is fully integrated, map actual endpoints
        // Example (pseudo-code):
        // endpoints.MapWmtsEndpoints();

        // For now, just a placeholder
        endpoints.MapGet("/wmts", () => new
        {
            service = "WMTS",
            status = "configured",
            message = "WMTS service is configured but full integration pending"
        });
    }

    public ServiceValidationResult ValidateConfiguration(ServiceBlock serviceConfig)
    {
        var result = new ServiceValidationResult();

        // Validate tile_size (must be power of 2, typically 256 or 512)
        if (serviceConfig.Settings.TryGetValue("tile_size", out var tileSizeObj))
        {
            if (tileSizeObj is int tileSize)
            {
                if (tileSize < 64 || tileSize > 2048)
                {
                    result.AddError("tile_size must be between 64 and 2048 pixels");
                }
                else if ((tileSize & (tileSize - 1)) != 0)
                {
                    result.AddWarning("tile_size should be a power of 2 (64, 128, 256, 512, 1024, etc.)");
                }
            }
        }

        // Validate max_feature_count
        if (serviceConfig.Settings.TryGetValue("max_feature_count", out var maxCountObj))
        {
            if (maxCountObj is int maxCount && (maxCount < 1 || maxCount > 100))
            {
                result.AddError("max_feature_count must be between 1 and 100");
            }
        }

        // Validate version
        if (serviceConfig.Settings.TryGetValue("version", out var versionObj))
        {
            if (versionObj is string version && version != "1.0.0")
            {
                result.AddWarning($"version '{version}' is not a standard WMTS version. Valid version: 1.0.0");
            }
        }

        // Validate supported_formats
        if (serviceConfig.Settings.TryGetValue("supported_formats", out var formatsObj))
        {
            if (formatsObj is string[] formats)
            {
                var validFormats = new[] { "image/png", "image/jpeg", "image/webp" };
                foreach (var format in formats)
                {
                    if (!validFormats.Contains(format))
                    {
                        result.AddWarning($"supported_formats contains unsupported format '{format}'. Valid formats: {string.Join(", ", validFormats)}");
                    }
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
/// WMTS service configuration extracted from declarative config.
/// </summary>
public sealed class WmtsServiceConfiguration
{
    public string Version { get; init; } = "1.0.0";
    public int TileSize { get; init; }
    public string[] SupportedFormats { get; init; } = Array.Empty<string>();
    public bool EnableCaching { get; init; }
    public int MaxFeatureCount { get; init; }
}
