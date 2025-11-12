// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Core.Configuration.V2.Services.Implementations;

/// <summary>
/// Service registration for OData v4.
/// Enables declarative OData configuration from .honua files.
/// </summary>
[ServiceRegistration("odata", Priority = 10)]
public sealed class ODataServiceRegistration : IServiceRegistration
{
    public string ServiceId => "odata";
    public string DisplayName => "OData v4";
    public string Description => "Open Data Protocol (OData) v4 RESTful API";

    public void ConfigureServices(IServiceCollection services, ServiceBlock serviceConfig)
    {
        // Extract OData-specific settings
        var allowWrites = GetSetting<bool>(serviceConfig, "allow_writes", false);
        var maxPageSize = GetSetting<int>(serviceConfig, "max_page_size", 1000);
        var defaultPageSize = GetSetting<int>(serviceConfig, "default_page_size", 100);
        var emitWktShadowProperties = GetSetting<bool>(serviceConfig, "emit_wkt_shadow_properties", false);
        var exposeNavigationProperties = GetSetting<bool>(serviceConfig, "expose_navigation_properties", true);
        var enableCaseInsensitive = GetSetting<bool>(serviceConfig, "enable_case_insensitive", true);

        // TODO: When OData implementation is available, register actual OData services
        // Example (pseudo-code):
        // services.AddOData(options => {
        //     options.AllowWrites = allowWrites;
        //     options.MaxPageSize = maxPageSize;
        //     options.DefaultPageSize = defaultPageSize;
        //     options.EmitWktShadowProperties = emitWktShadowProperties;
        //     options.ExposeNavigationProperties = exposeNavigationProperties;
        //     options.EnableCaseInsensitive = enableCaseInsensitive;
        // });

        // For now, just register metadata
        services.AddSingleton(new ODataServiceConfiguration
        {
            AllowWrites = allowWrites,
            MaxPageSize = maxPageSize,
            DefaultPageSize = defaultPageSize,
            EmitWktShadowProperties = emitWktShadowProperties,
            ExposeNavigationProperties = exposeNavigationProperties,
            EnableCaseInsensitive = enableCaseInsensitive
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, ServiceBlock serviceConfig)
    {
        // TODO: When OData implementation is available, map actual endpoints
        // Example (pseudo-code):
        // endpoints.MapODataEndpoints();

        // For now, just a placeholder
        endpoints.MapGet("/odata", () => new
        {
            service = "OData v4",
            status = "configured",
            message = "OData service is configured but implementation pending"
        });
    }

    public ServiceValidationResult ValidateConfiguration(ServiceBlock serviceConfig)
    {
        var result = new ServiceValidationResult();

        // Validate max_page_size
        if (serviceConfig.Settings.TryGetValue("max_page_size", out var maxPageSizeObj))
        {
            if (maxPageSizeObj is int maxPageSize && maxPageSize < 1)
            {
                result.AddError("max_page_size must be greater than 0");
            }
        }

        // Validate default_page_size
        if (serviceConfig.Settings.TryGetValue("default_page_size", out var defaultPageSizeObj))
        {
            if (defaultPageSizeObj is int defaultPageSize && defaultPageSize < 1)
            {
                result.AddError("default_page_size must be greater than 0");
            }

            // Check if default <= max
            if (serviceConfig.Settings.TryGetValue("max_page_size", out var maxPageSizeCheck))
            {
                if (defaultPageSizeObj is int defSize && maxPageSizeCheck is int maxSize && defSize > maxSize)
                {
                    result.AddError("default_page_size cannot be greater than max_page_size");
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
/// OData service configuration extracted from declarative config.
/// </summary>
public sealed class ODataServiceConfiguration
{
    public bool AllowWrites { get; init; }
    public int MaxPageSize { get; init; }
    public int DefaultPageSize { get; init; }
    public bool EmitWktShadowProperties { get; init; }
    public bool ExposeNavigationProperties { get; init; }
    public bool EnableCaseInsensitive { get; init; }
}
