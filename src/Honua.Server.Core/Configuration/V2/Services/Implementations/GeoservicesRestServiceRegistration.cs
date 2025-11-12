// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Core.Configuration.V2.Services.Implementations;

/// <summary>
/// Service registration for Esri GeoServices REST API.
/// Enables declarative GeoservicesREST configuration from .honua files.
/// </summary>
[ServiceRegistration("geoservices_rest", Priority = 90)]
public sealed class GeoservicesRestServiceRegistration : IServiceRegistration
{
    public string ServiceId => "geoservices_rest";
    public string DisplayName => "Esri GeoServices REST API";
    public string Description => "Esri-compatible REST API for feature and map services";

    public void ConfigureServices(IServiceCollection services, ServiceBlock serviceConfig)
    {
        // Extract GeoservicesREST-specific settings
        var version = GetSetting<double>(serviceConfig, "version", 10.81);
        var defaultMaxRecordCount = GetSetting<int>(serviceConfig, "default_max_record_count", 1000);
        var maxRecordCount = GetSetting<int>(serviceConfig, "max_record_count", 10_000);
        var enableAttachments = GetSetting<bool>(serviceConfig, "enable_attachments", true);
        var enableEditing = GetSetting<bool>(serviceConfig, "enable_editing", false);
        var enableShapefileExport = GetSetting<bool>(serviceConfig, "enable_shapefile_export", true);
        var enableKmlExport = GetSetting<bool>(serviceConfig, "enable_kml_export", true);
        var enableCsvExport = GetSetting<bool>(serviceConfig, "enable_csv_export", true);

        // TODO: When GeoservicesREST implementation is fully integrated with Configuration V2, register actual services
        // For now, register metadata
        services.AddSingleton(new GeoservicesRestServiceConfiguration
        {
            Version = version,
            DefaultMaxRecordCount = defaultMaxRecordCount,
            MaxRecordCount = maxRecordCount,
            EnableAttachments = enableAttachments,
            EnableEditing = enableEditing,
            EnableShapefileExport = enableShapefileExport,
            EnableKmlExport = enableKmlExport,
            EnableCsvExport = enableCsvExport
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, ServiceBlock serviceConfig)
    {
        // NOTE: GeoservicesREST uses ASP.NET Core Controllers with ApiController and Route attributes,
        // so endpoints are automatically mapped via MapControllers().
        // No explicit endpoint mapping needed here.

        // For now, just a placeholder route for diagnostics
        endpoints.MapGet("/rest/services", () => new
        {
            service = "GeoServices REST",
            status = "configured",
            message = "GeoServices REST API is configured but full integration pending"
        });
    }

    public ServiceValidationResult ValidateConfiguration(ServiceBlock serviceConfig)
    {
        var result = new ServiceValidationResult();

        // Validate default_max_record_count (1-10000)
        if (serviceConfig.Settings.TryGetValue("default_max_record_count", out var defaultMaxObj))
        {
            if (defaultMaxObj is int defaultMax && (defaultMax < 1 || defaultMax > 10_000))
            {
                result.AddError("default_max_record_count must be between 1 and 10,000");
            }
        }

        // Validate max_record_count (1-100000)
        if (serviceConfig.Settings.TryGetValue("max_record_count", out var maxObj))
        {
            if (maxObj is int max && (max < 1 || max > 100_000))
            {
                result.AddError("max_record_count must be between 1 and 100,000");
            }
        }

        // Validate default_max_record_count <= max_record_count
        if (serviceConfig.Settings.TryGetValue("default_max_record_count", out var defMaxObj) &&
            serviceConfig.Settings.TryGetValue("max_record_count", out var maxRecObj))
        {
            if (defMaxObj is int defMax && maxRecObj is int maxRec && defMax > maxRec)
            {
                result.AddError("default_max_record_count cannot be greater than max_record_count");
            }
        }

        // Validate version
        if (serviceConfig.Settings.TryGetValue("version", out var versionObj))
        {
            if (versionObj is double version && version < 10.0)
            {
                result.AddWarning("version is less than 10.0. This may cause compatibility issues with ArcGIS clients.");
            }
        }

        // Warn about editing in production
        if (serviceConfig.Settings.TryGetValue("enable_editing", out var enableEditObj))
        {
            if (enableEditObj is bool enableEdit && enableEdit)
            {
                result.AddWarning("enable_editing is true. Ensure proper authentication and authorization for write operations.");
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
/// GeoservicesREST service configuration extracted from declarative config.
/// </summary>
public sealed class GeoservicesRestServiceConfiguration
{
    public double Version { get; init; }
    public int DefaultMaxRecordCount { get; init; }
    public int MaxRecordCount { get; init; }
    public bool EnableAttachments { get; init; }
    public bool EnableEditing { get; init; }
    public bool EnableShapefileExport { get; init; }
    public bool EnableKmlExport { get; init; }
    public bool EnableCsvExport { get; init; }
}
