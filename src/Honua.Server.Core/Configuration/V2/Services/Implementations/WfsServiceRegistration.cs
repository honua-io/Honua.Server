// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Core.Configuration.V2.Services.Implementations;

/// <summary>
/// Service registration for Web Feature Service (WFS).
/// Enables declarative WFS configuration from .honua files.
/// </summary>
[ServiceRegistration("wfs", Priority = 20)]
public sealed class WfsServiceRegistration : IServiceRegistration
{
    public string ServiceId => "wfs";
    public string DisplayName => "Web Feature Service (WFS)";
    public string Description => "OGC Web Feature Service for vector data access";

    public void ConfigureServices(IServiceCollection services, ServiceBlock serviceConfig)
    {
        // Extract WFS-specific settings
        var capabilitiesCacheDuration = GetSetting<int>(serviceConfig, "capabilities_cache_duration", 3600);
        var defaultCount = GetSetting<int>(serviceConfig, "default_count", 100);
        var maxFeatures = GetSetting<int>(serviceConfig, "max_features", 10_000);
        var enableComplexityCheck = GetSetting<bool>(serviceConfig, "enable_complexity_check", true);
        var maxTransactionFeatures = GetSetting<int>(serviceConfig, "max_transaction_features", 5_000);
        var enableStreamingTransactionParser = GetSetting<bool>(serviceConfig, "enable_streaming_transaction_parser", true);
        var version = GetSetting<string>(serviceConfig, "version", "2.0.0");

        // TODO: When WFS implementation is fully integrated with Configuration V2, register actual WFS services
        // Example (pseudo-code):
        // services.Configure<WfsOptions>(options => {
        //     options.CapabilitiesCacheDuration = capabilitiesCacheDuration;
        //     options.DefaultCount = defaultCount;
        //     options.MaxFeatures = maxFeatures;
        //     options.EnableComplexityCheck = enableComplexityCheck;
        //     options.MaxTransactionFeatures = maxTransactionFeatures;
        //     options.EnableStreamingTransactionParser = enableStreamingTransactionParser;
        // });

        // For now, register metadata
        services.AddSingleton(new WfsServiceConfiguration
        {
            CapabilitiesCacheDuration = capabilitiesCacheDuration,
            DefaultCount = defaultCount,
            MaxFeatures = maxFeatures,
            EnableComplexityCheck = enableComplexityCheck,
            MaxTransactionFeatures = maxTransactionFeatures,
            EnableStreamingTransactionParser = enableStreamingTransactionParser,
            Version = version
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, ServiceBlock serviceConfig)
    {
        // TODO: When WFS implementation is fully integrated, map actual endpoints
        // Example (pseudo-code):
        // endpoints.MapWfs();

        // For now, just a placeholder
        endpoints.MapGet("/wfs", () => new
        {
            service = "WFS",
            status = "configured",
            message = "WFS service is configured but full integration pending"
        });
    }

    public ServiceValidationResult ValidateConfiguration(ServiceBlock serviceConfig)
    {
        var result = new ServiceValidationResult();

        // Validate capabilities_cache_duration (0-86400 seconds = 24 hours)
        if (serviceConfig.Settings.TryGetValue("capabilities_cache_duration", out var cacheDurationObj))
        {
            if (cacheDurationObj is int cacheDuration && (cacheDuration < 0 || cacheDuration > 86400))
            {
                result.AddError("capabilities_cache_duration must be between 0 and 86400 seconds");
            }
        }

        // Validate default_count (1-10000)
        if (serviceConfig.Settings.TryGetValue("default_count", out var defaultCountObj))
        {
            if (defaultCountObj is int defaultCount && (defaultCount < 1 || defaultCount > 10_000))
            {
                result.AddError("default_count must be between 1 and 10,000");
            }
        }

        // Validate max_features (1-100000)
        if (serviceConfig.Settings.TryGetValue("max_features", out var maxFeaturesObj))
        {
            if (maxFeaturesObj is int maxFeatures && (maxFeatures < 1 || maxFeatures > 100_000))
            {
                result.AddError("max_features must be between 1 and 100,000");
            }
        }

        // Validate max_transaction_features (1-100000)
        if (serviceConfig.Settings.TryGetValue("max_transaction_features", out var maxTxFeaturesObj))
        {
            if (maxTxFeaturesObj is int maxTxFeatures && (maxTxFeatures < 1 || maxTxFeatures > 100_000))
            {
                result.AddError("max_transaction_features must be between 1 and 100,000");
            }
        }

        // Validate default_count <= max_features
        if (serviceConfig.Settings.TryGetValue("default_count", out var defCountObj) &&
            serviceConfig.Settings.TryGetValue("max_features", out var maxFeatObj))
        {
            if (defCountObj is int defCount && maxFeatObj is int maxFeat && defCount > maxFeat)
            {
                result.AddError("default_count cannot be greater than max_features");
            }
        }

        // Validate version
        if (serviceConfig.Settings.TryGetValue("version", out var versionObj))
        {
            if (versionObj is string version)
            {
                var validVersions = new[] { "1.0.0", "1.1.0", "2.0.0" };
                if (!validVersions.Contains(version))
                {
                    result.AddWarning($"version '{version}' is not a standard WFS version. Valid versions: {string.Join(", ", validVersions)}");
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
/// WFS service configuration extracted from declarative config.
/// </summary>
public sealed class WfsServiceConfiguration
{
    public int CapabilitiesCacheDuration { get; init; }
    public int DefaultCount { get; init; }
    public int MaxFeatures { get; init; }
    public bool EnableComplexityCheck { get; init; }
    public int MaxTransactionFeatures { get; init; }
    public bool EnableStreamingTransactionParser { get; init; }
    public string Version { get; init; } = "2.0.0";
}
