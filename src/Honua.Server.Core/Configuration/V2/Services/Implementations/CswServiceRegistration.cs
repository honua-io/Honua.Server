// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Core.Configuration.V2.Services.Implementations;

/// <summary>
/// Service registration for Catalog Service for the Web (CSW).
/// Enables declarative CSW configuration from .honua files.
/// </summary>
[ServiceRegistration("csw", Priority = 50)]
public sealed class CswServiceRegistration : IServiceRegistration
{
    public string ServiceId => "csw";
    public string DisplayName => "Catalog Service for the Web (CSW)";
    public string Description => "OGC Catalog Service for metadata discovery and search";

    public void ConfigureServices(IServiceCollection services, ServiceBlock serviceConfig)
    {
        // Extract CSW-specific settings
        var version = GetSetting<string>(serviceConfig, "version", "2.0.2");
        var defaultMaxRecords = GetSetting<int>(serviceConfig, "default_max_records", 10);
        var maxRecordLimit = GetSetting<int>(serviceConfig, "max_record_limit", 100);
        var enableTransactions = GetSetting<bool>(serviceConfig, "enable_transactions", false);
        var supportedOutputSchemas = GetSetting<string[]>(serviceConfig, "supported_output_schemas",
            new[] { "http://www.opengis.net/cat/csw/2.0.2", "http://www.isotc211.org/2005/gmd" });

        // TODO: When CSW implementation is fully integrated with Configuration V2, register actual CSW services
        // For now, register metadata
        services.AddSingleton(new CswServiceConfiguration
        {
            Version = version,
            DefaultMaxRecords = defaultMaxRecords,
            MaxRecordLimit = maxRecordLimit,
            EnableTransactions = enableTransactions,
            SupportedOutputSchemas = supportedOutputSchemas
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, ServiceBlock serviceConfig)
    {
        // TODO: When CSW implementation is fully integrated, map actual endpoints
        // Example (pseudo-code):
        // endpoints.MapCswEndpoints();

        // For now, just a placeholder
        endpoints.MapGet("/csw", () => new
        {
            service = "CSW",
            status = "configured",
            message = "CSW service is configured but full integration pending"
        });
    }

    public ServiceValidationResult ValidateConfiguration(ServiceBlock serviceConfig)
    {
        var result = new ServiceValidationResult();

        // Validate default_max_records (1-1000)
        if (serviceConfig.Settings.TryGetValue("default_max_records", out var defaultMaxObj))
        {
            if (defaultMaxObj is int defaultMax && (defaultMax < 1 || defaultMax > 1000))
            {
                result.AddError("default_max_records must be between 1 and 1000");
            }
        }

        // Validate max_record_limit (1-10000)
        if (serviceConfig.Settings.TryGetValue("max_record_limit", out var maxLimitObj))
        {
            if (maxLimitObj is int maxLimit && (maxLimit < 1 || maxLimit > 10000))
            {
                result.AddError("max_record_limit must be between 1 and 10000");
            }
        }

        // Validate default_max_records <= max_record_limit
        if (serviceConfig.Settings.TryGetValue("default_max_records", out var defMaxObj) &&
            serviceConfig.Settings.TryGetValue("max_record_limit", out var maxLimObj))
        {
            if (defMaxObj is int defMax && maxLimObj is int maxLim && defMax > maxLim)
            {
                result.AddError("default_max_records cannot be greater than max_record_limit");
            }
        }

        // Validate version
        if (serviceConfig.Settings.TryGetValue("version", out var versionObj))
        {
            if (versionObj is string version && version != "2.0.2")
            {
                result.AddWarning($"version '{version}' is not a standard CSW version. Valid version: 2.0.2");
            }
        }

        // Warn about transactions in production
        if (serviceConfig.Settings.TryGetValue("enable_transactions", out var enableTxObj))
        {
            if (enableTxObj is bool enableTx && enableTx)
            {
                result.AddWarning("enable_transactions is true. Ensure proper authentication and authorization for write operations.");
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
/// CSW service configuration extracted from declarative config.
/// </summary>
public sealed class CswServiceConfiguration
{
    public string Version { get; init; } = "2.0.2";
    public int DefaultMaxRecords { get; init; }
    public int MaxRecordLimit { get; init; }
    public bool EnableTransactions { get; init; }
    public string[] SupportedOutputSchemas { get; init; } = Array.Empty<string>();
}
