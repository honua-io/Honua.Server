// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Core.Configuration.V2.Services.Implementations;

/// <summary>
/// Service registration for OGC API Features.
/// Enables declarative OGC API configuration from .honua files.
/// </summary>
[ServiceRegistration("ogc_api", Priority = 10)]
public sealed class OgcApiServiceRegistration : IServiceRegistration
{
    public string ServiceId => "ogc_api";
    public string DisplayName => "OGC API Features";
    public string Description => "OGC API - Features (OAFeat) standard implementation";

    public void ConfigureServices(IServiceCollection services, ServiceBlock serviceConfig)
    {
        // Extract OGC API-specific settings
        var itemLimit = GetSetting<int>(serviceConfig, "item_limit", 1000);
        var defaultCrs = GetSetting<string>(serviceConfig, "default_crs", "EPSG:4326");
        var additionalCrs = GetSettingList<string>(serviceConfig, "additional_crs");
        var conformanceClasses = GetSettingList<string>(serviceConfig, "conformance");
        var maxFeatureUploadSizeBytes = GetSetting<long>(serviceConfig, "max_feature_upload_size_bytes", 100 * 1024 * 1024);

        // TODO: When OGC API implementation is available, register actual services
        // Example (pseudo-code):
        // services.AddOgcApi(options => {
        //     options.ItemLimit = itemLimit;
        //     options.DefaultCrs = defaultCrs;
        //     options.AdditionalCrs = additionalCrs;
        //     options.ConformanceClasses = conformanceClasses;
        //     options.MaxFeatureUploadSizeBytes = maxFeatureUploadSizeBytes;
        // });

        // For now, just register metadata
        services.AddSingleton(new OgcApiServiceConfiguration
        {
            ItemLimit = itemLimit,
            DefaultCrs = defaultCrs,
            AdditionalCrs = additionalCrs,
            ConformanceClasses = conformanceClasses,
            MaxFeatureUploadSizeBytes = maxFeatureUploadSizeBytes
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, ServiceBlock serviceConfig)
    {
        // TODO: When OGC API implementation is available, map actual endpoints
        // Example (pseudo-code):
        // endpoints.MapOgcApiEndpoints();

        // For now, placeholder endpoints
        endpoints.MapGet("/collections", () => new
        {
            service = "OGC API Features",
            status = "configured",
            message = "OGC API service is configured but implementation pending"
        });

        endpoints.MapGet("/conformance", () => new
        {
            conformsTo = GetSettingList<string>(serviceConfig, "conformance")
        });
    }

    public ServiceValidationResult ValidateConfiguration(ServiceBlock serviceConfig)
    {
        var result = new ServiceValidationResult();

        // Validate item_limit
        if (serviceConfig.Settings.TryGetValue("item_limit", out var itemLimitObj))
        {
            if (itemLimitObj is int itemLimit && itemLimit < 1)
            {
                result.AddError("item_limit must be greater than 0");
            }
        }

        // Validate default_crs format
        if (serviceConfig.Settings.TryGetValue("default_crs", out var defaultCrsObj))
        {
            if (defaultCrsObj is string defaultCrs && !IsValidCrsFormat(defaultCrs))
            {
                result.AddWarning($"default_crs '{defaultCrs}' should be in EPSG:#### format");
            }
        }

        // Validate conformance classes
        if (serviceConfig.Settings.TryGetValue("conformance", out var conformanceObj))
        {
            var conformance = GetSettingList<string>(serviceConfig, "conformance");
            var knownClasses = new[]
            {
                "core", "geojson", "html", "crs", "filter", "features-filter",
                "simple-cql", "cql-text", "cql-json", "sorting", "paging"
            };

            foreach (var cls in conformance)
            {
                if (!knownClasses.Contains(cls.ToLowerInvariant()) && !cls.StartsWith("http"))
                {
                    result.AddWarning($"Unknown conformance class '{cls}'");
                }
            }
        }

        // Validate max upload size
        if (serviceConfig.Settings.TryGetValue("max_feature_upload_size_bytes", out var maxSizeObj))
        {
            if (maxSizeObj is long maxSize && maxSize < 1024)
            {
                result.AddWarning("max_feature_upload_size_bytes is very small (< 1KB)");
            }
        }

        return result;
    }

    private static bool IsValidCrsFormat(string crs)
    {
        return crs.StartsWith("EPSG:", System.StringComparison.OrdinalIgnoreCase) ||
               crs.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase) ||
               crs.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase);
    }

    private static T GetSetting<T>(ServiceBlock serviceConfig, string key, T defaultValue)
    {
        if (serviceConfig.Settings.TryGetValue(key, out var value))
        {
            if (value is T typedValue)
            {
                return typedValue;
            }

            try
            {
                return (T)System.Convert.ChangeType(value, typeof(T))!;
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    private static List<T> GetSettingList<T>(ServiceBlock serviceConfig, string key)
    {
        if (serviceConfig.Settings.TryGetValue(key, out var value))
        {
            if (value is List<T> typedList)
            {
                return typedList;
            }

            if (value is System.Collections.IEnumerable enumerable)
            {
                var list = new List<T>();
                foreach (var item in enumerable)
                {
                    if (item is T typedItem)
                    {
                        list.Add(typedItem);
                    }
                }
                return list;
            }
        }

        return new List<T>();
    }
}

/// <summary>
/// OGC API Features service configuration extracted from declarative config.
/// </summary>
public sealed class OgcApiServiceConfiguration
{
    public int ItemLimit { get; init; }
    public string DefaultCrs { get; init; } = "EPSG:4326";
    public List<string> AdditionalCrs { get; init; } = new();
    public List<string> ConformanceClasses { get; init; } = new();
    public long MaxFeatureUploadSizeBytes { get; init; }
}
