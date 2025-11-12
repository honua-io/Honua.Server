// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Core.Configuration.V2.Services.Implementations;

/// <summary>
/// Service registration for Web Coverage Service (WCS).
/// Enables declarative WCS configuration from .honua files.
/// </summary>
[ServiceRegistration("wcs", Priority = 70)]
public sealed class WcsServiceRegistration : IServiceRegistration
{
    public string ServiceId => "wcs";
    public string DisplayName => "Web Coverage Service (WCS)";
    public string Description => "OGC Web Coverage Service for raster/coverage data access";

    public void ConfigureServices(IServiceCollection services, ServiceBlock serviceConfig)
    {
        // Extract WCS-specific settings
        var version = GetSetting<string>(serviceConfig, "version", "2.0.1");
        var supportedFormats = GetSetting<string[]>(serviceConfig, "supported_formats",
            new[] { "image/tiff", "image/png", "image/jpeg" });
        var maxCoverageSize = GetSetting<int>(serviceConfig, "max_coverage_size", 10000);
        var enableSubsetting = GetSetting<bool>(serviceConfig, "enable_subsetting", true);
        var enableRangeSubsetting = GetSetting<bool>(serviceConfig, "enable_range_subsetting", true);
        var enableInterpolation = GetSetting<bool>(serviceConfig, "enable_interpolation", true);
        var defaultInterpolation = GetSetting<string>(serviceConfig, "default_interpolation", "nearest");

        // TODO: When WCS implementation is fully integrated with Configuration V2, register actual WCS services
        // For now, register metadata
        services.AddSingleton(new WcsServiceConfiguration
        {
            Version = version,
            SupportedFormats = supportedFormats,
            MaxCoverageSize = maxCoverageSize,
            EnableSubsetting = enableSubsetting,
            EnableRangeSubsetting = enableRangeSubsetting,
            EnableInterpolation = enableInterpolation,
            DefaultInterpolation = defaultInterpolation
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, ServiceBlock serviceConfig)
    {
        // TODO: When WCS implementation is fully integrated, map actual endpoints
        // Example (pseudo-code):
        // endpoints.MapWcsEndpoints();

        // For now, just a placeholder
        endpoints.MapGet("/wcs", () => new
        {
            service = "WCS",
            status = "configured",
            message = "WCS service is configured but full integration pending"
        });
    }

    public ServiceValidationResult ValidateConfiguration(ServiceBlock serviceConfig)
    {
        var result = new ServiceValidationResult();

        // Validate max_coverage_size (100-50000)
        if (serviceConfig.Settings.TryGetValue("max_coverage_size", out var maxSizeObj))
        {
            if (maxSizeObj is int maxSize && (maxSize < 100 || maxSize > 50000))
            {
                result.AddError("max_coverage_size must be between 100 and 50000 pixels");
            }
        }

        // Validate version
        if (serviceConfig.Settings.TryGetValue("version", out var versionObj))
        {
            if (versionObj is string version)
            {
                var validVersions = new[] { "1.0.0", "1.1.0", "2.0.0", "2.0.1" };
                if (!validVersions.Contains(version))
                {
                    result.AddWarning($"version '{version}' is not a standard WCS version. Valid versions: {string.Join(", ", validVersions)}");
                }
            }
        }

        // Validate supported_formats
        if (serviceConfig.Settings.TryGetValue("supported_formats", out var formatsObj))
        {
            if (formatsObj is string[] formats)
            {
                var validFormats = new[] { "image/tiff", "image/png", "image/jpeg", "application/netcdf" };
                foreach (var format in formats)
                {
                    if (!validFormats.Contains(format))
                    {
                        result.AddWarning($"supported_formats contains unsupported format '{format}'. Common formats: {string.Join(", ", validFormats)}");
                    }
                }
            }
        }

        // Validate default_interpolation
        if (serviceConfig.Settings.TryGetValue("default_interpolation", out var interpObj))
        {
            if (interpObj is string interp)
            {
                var validInterpolations = new[] { "nearest", "linear", "bilinear", "cubic", "average" };
                if (!validInterpolations.Contains(interp))
                {
                    result.AddWarning($"default_interpolation '{interp}' is not a standard method. Valid methods: {string.Join(", ", validInterpolations)}");
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
/// WCS service configuration extracted from declarative config.
/// </summary>
public sealed class WcsServiceConfiguration
{
    public string Version { get; init; } = "2.0.1";
    public string[] SupportedFormats { get; init; } = Array.Empty<string>();
    public int MaxCoverageSize { get; init; }
    public bool EnableSubsetting { get; init; }
    public bool EnableRangeSubsetting { get; init; }
    public bool EnableInterpolation { get; init; }
    public string DefaultInterpolation { get; init; } = "nearest";
}
