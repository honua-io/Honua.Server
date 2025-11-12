// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Core.Configuration.V2.Services.Implementations;

/// <summary>
/// Service registration for Carto API.
/// Enables declarative Carto configuration from .honua files.
/// </summary>
[ServiceRegistration("carto", Priority = 80)]
public sealed class CartoServiceRegistration : IServiceRegistration
{
    public string ServiceId => "carto";
    public string DisplayName => "Carto API";
    public string Description => "Carto-compatible API for SQL-based data access and visualization";

    public void ConfigureServices(IServiceCollection services, ServiceBlock serviceConfig)
    {
        // Extract Carto-specific settings
        var enableV2Api = GetSetting<bool>(serviceConfig, "enable_v2_api", true);
        var enableV3Api = GetSetting<bool>(serviceConfig, "enable_v3_api", true);
        var maxSqlQueryLength = GetSetting<int>(serviceConfig, "max_sql_query_length", 10000);
        var maxResultRows = GetSetting<int>(serviceConfig, "max_result_rows", 10000);
        var enableCaching = GetSetting<bool>(serviceConfig, "enable_caching", true);
        var cacheTtlSeconds = GetSetting<int>(serviceConfig, "cache_ttl_seconds", 300);

        // TODO: When Carto implementation is fully integrated with Configuration V2, register actual Carto services
        // For now, register metadata
        services.AddSingleton(new CartoServiceConfiguration
        {
            EnableV2Api = enableV2Api,
            EnableV3Api = enableV3Api,
            MaxSqlQueryLength = maxSqlQueryLength,
            MaxResultRows = maxResultRows,
            EnableCaching = enableCaching,
            CacheTtlSeconds = cacheTtlSeconds
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, ServiceBlock serviceConfig)
    {
        // TODO: When Carto implementation is fully integrated, map actual endpoints
        // Example (pseudo-code):
        // endpoints.MapCartoApi();

        // For now, just a placeholder
        endpoints.MapGet("/carto", () => new
        {
            service = "Carto",
            status = "configured",
            message = "Carto API is configured but full integration pending"
        });
    }

    public ServiceValidationResult ValidateConfiguration(ServiceBlock serviceConfig)
    {
        var result = new ServiceValidationResult();

        // Validate max_sql_query_length (1-100000)
        if (serviceConfig.Settings.TryGetValue("max_sql_query_length", out var maxLengthObj))
        {
            if (maxLengthObj is int maxLength && (maxLength < 1 || maxLength > 100000))
            {
                result.AddError("max_sql_query_length must be between 1 and 100000 characters");
            }
        }

        // Validate max_result_rows (1-100000)
        if (serviceConfig.Settings.TryGetValue("max_result_rows", out var maxRowsObj))
        {
            if (maxRowsObj is int maxRows && (maxRows < 1 || maxRows > 100000))
            {
                result.AddError("max_result_rows must be between 1 and 100000");
            }
        }

        // Validate cache_ttl_seconds (0-86400)
        if (serviceConfig.Settings.TryGetValue("cache_ttl_seconds", out var ttlObj))
        {
            if (ttlObj is int ttl && (ttl < 0 || ttl > 86400))
            {
                result.AddError("cache_ttl_seconds must be between 0 and 86400 seconds (24 hours)");
            }
        }

        // Warn if both APIs are disabled
        var v2Enabled = GetSetting<bool>(serviceConfig, "enable_v2_api", true);
        var v3Enabled = GetSetting<bool>(serviceConfig, "enable_v3_api", true);
        if (!v2Enabled && !v3Enabled)
        {
            result.AddWarning("Both enable_v2_api and enable_v3_api are false. At least one API version should be enabled.");
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
/// Carto service configuration extracted from declarative config.
/// </summary>
public sealed class CartoServiceConfiguration
{
    public bool EnableV2Api { get; init; }
    public bool EnableV3Api { get; init; }
    public int MaxSqlQueryLength { get; init; }
    public int MaxResultRows { get; init; }
    public bool EnableCaching { get; init; }
    public int CacheTtlSeconds { get; init; }
}
