// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Core.Configuration.V2.Services.Implementations;

/// <summary>
/// Service registration for Zarr Time-Series API.
/// Enables declarative Zarr configuration from .honua files.
/// </summary>
[ServiceRegistration("zarr", Priority = 100)]
public sealed class ZarrServiceRegistration : IServiceRegistration
{
    public string ServiceId => "zarr";
    public string DisplayName => "Zarr Time-Series API";
    public string Description => "REST API for temporal raster data with Zarr chunk-based storage";

    public void ConfigureServices(IServiceCollection services, ServiceBlock serviceConfig)
    {
        // Extract Zarr-specific settings
        var enableCaching = GetSetting<bool>(serviceConfig, "enable_caching", true);
        var cacheTtlSeconds = GetSetting<int>(serviceConfig, "cache_ttl_seconds", 3600);
        var maxSlicesPerQuery = GetSetting<int>(serviceConfig, "max_slices_per_query", 1000);
        var maxBoundingBoxSize = GetSetting<int>(serviceConfig, "max_bounding_box_size", 10000);
        var enableBinaryOutput = GetSetting<bool>(serviceConfig, "enable_binary_output", true);
        var enableAggregation = GetSetting<bool>(serviceConfig, "enable_aggregation", true);
        var defaultVariable = GetSetting<string>(serviceConfig, "default_variable", "data");

        // TODO: When Zarr implementation is fully integrated with Configuration V2, register actual Zarr services
        // For now, register metadata
        services.AddSingleton(new ZarrServiceConfiguration
        {
            EnableCaching = enableCaching,
            CacheTtlSeconds = cacheTtlSeconds,
            MaxSlicesPerQuery = maxSlicesPerQuery,
            MaxBoundingBoxSize = maxBoundingBoxSize,
            EnableBinaryOutput = enableBinaryOutput,
            EnableAggregation = enableAggregation,
            DefaultVariable = defaultVariable
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, ServiceBlock serviceConfig)
    {
        // TODO: When Zarr implementation is fully integrated, map actual endpoints
        // Example (pseudo-code):
        // endpoints.MapZarrTimeSeriesEndpoints();

        // For now, just a placeholder
        endpoints.MapGet("/api/raster/zarr", () => new
        {
            service = "Zarr Time-Series",
            status = "configured",
            message = "Zarr API is configured but full integration pending"
        });
    }

    public ServiceValidationResult ValidateConfiguration(ServiceBlock serviceConfig)
    {
        var result = new ServiceValidationResult();

        // Validate cache_ttl_seconds (0-86400)
        if (serviceConfig.Settings.TryGetValue("cache_ttl_seconds", out var ttlObj))
        {
            if (ttlObj is int ttl && (ttl < 0 || ttl > 86400))
            {
                result.AddError("cache_ttl_seconds must be between 0 and 86400 seconds (24 hours)");
            }
        }

        // Validate max_slices_per_query (1-10000)
        if (serviceConfig.Settings.TryGetValue("max_slices_per_query", out var maxSlicesObj))
        {
            if (maxSlicesObj is int maxSlices && (maxSlices < 1 || maxSlices > 10_000))
            {
                result.AddError("max_slices_per_query must be between 1 and 10,000");
            }
        }

        // Validate max_bounding_box_size (100-100000)
        if (serviceConfig.Settings.TryGetValue("max_bounding_box_size", out var maxBboxObj))
        {
            if (maxBboxObj is int maxBbox && (maxBbox < 100 || maxBbox > 100_000))
            {
                result.AddError("max_bounding_box_size must be between 100 and 100,000 pixels");
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
/// Zarr service configuration extracted from declarative config.
/// </summary>
public sealed class ZarrServiceConfiguration
{
    public bool EnableCaching { get; init; }
    public int CacheTtlSeconds { get; init; }
    public int MaxSlicesPerQuery { get; init; }
    public int MaxBoundingBoxSize { get; init; }
    public bool EnableBinaryOutput { get; init; }
    public bool EnableAggregation { get; init; }
    public string DefaultVariable { get; init; } = "data";
}
