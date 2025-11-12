// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Core.Configuration.V2.Services.Implementations;

/// <summary>
/// Service registration for STAC (SpatioTemporal Asset Catalog).
/// Enables declarative STAC configuration from .honua files.
/// </summary>
[ServiceRegistration("stac", Priority = 60)]
public sealed class StacServiceRegistration : IServiceRegistration
{
    public string ServiceId => "stac";
    public string DisplayName => "STAC (SpatioTemporal Asset Catalog)";
    public string Description => "STAC API for geospatial asset discovery and metadata";

    public void ConfigureServices(IServiceCollection services, ServiceBlock serviceConfig)
    {
        // Extract STAC-specific search settings
        var countTimeoutSeconds = GetSetting<int>(serviceConfig, "count_timeout_seconds", 5);
        var useCountEstimation = GetSetting<bool>(serviceConfig, "use_count_estimation", true);
        var maxExactCountThreshold = GetSetting<int>(serviceConfig, "max_exact_count_threshold", 100_000);
        var skipCountForLargeResultSets = GetSetting<bool>(serviceConfig, "skip_count_for_large_result_sets", true);
        var skipCountLimitThreshold = GetSetting<int>(serviceConfig, "skip_count_limit_threshold", 1000);
        var streamingPageSize = GetSetting<int>(serviceConfig, "streaming_page_size", 100);
        var maxStreamingItems = GetSetting<int>(serviceConfig, "max_streaming_items", 100_000);
        var enableAutoStreaming = GetSetting<bool>(serviceConfig, "enable_auto_streaming", true);
        var streamingThreshold = GetSetting<int>(serviceConfig, "streaming_threshold", 1000);
        var version = GetSetting<string>(serviceConfig, "version", "1.0.0");

        // TODO: When STAC implementation is fully integrated with Configuration V2, register actual STAC services
        // Example (pseudo-code):
        // services.Configure<StacSearchOptions>(options => {
        //     options.CountTimeoutSeconds = countTimeoutSeconds;
        //     options.UseCountEstimation = useCountEstimation;
        //     options.MaxExactCountThreshold = maxExactCountThreshold;
        //     options.SkipCountForLargeResultSets = skipCountForLargeResultSets;
        //     options.SkipCountLimitThreshold = skipCountLimitThreshold;
        //     options.StreamingPageSize = streamingPageSize;
        //     options.MaxStreamingItems = maxStreamingItems;
        //     options.EnableAutoStreaming = enableAutoStreaming;
        //     options.StreamingThreshold = streamingThreshold;
        // });

        // For now, register metadata
        services.AddSingleton(new StacServiceConfiguration
        {
            CountTimeoutSeconds = countTimeoutSeconds,
            UseCountEstimation = useCountEstimation,
            MaxExactCountThreshold = maxExactCountThreshold,
            SkipCountForLargeResultSets = skipCountForLargeResultSets,
            SkipCountLimitThreshold = skipCountLimitThreshold,
            StreamingPageSize = streamingPageSize,
            MaxStreamingItems = maxStreamingItems,
            EnableAutoStreaming = enableAutoStreaming,
            StreamingThreshold = streamingThreshold,
            Version = version
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, ServiceBlock serviceConfig)
    {
        // NOTE: STAC uses ASP.NET Core Controllers with ApiController and Route attributes,
        // so endpoints are automatically mapped via MapControllers().
        // No explicit endpoint mapping needed here.

        // For now, just a placeholder route for diagnostics
        endpoints.MapGet("/stac", () => new
        {
            service = "STAC",
            status = "configured",
            message = "STAC service is configured but full integration pending"
        });
    }

    public ServiceValidationResult ValidateConfiguration(ServiceBlock serviceConfig)
    {
        var result = new ServiceValidationResult();

        // Validate count_timeout_seconds (1-300)
        if (serviceConfig.Settings.TryGetValue("count_timeout_seconds", out var timeoutObj))
        {
            if (timeoutObj is int timeout && (timeout < 1 || timeout > 300))
            {
                result.AddError("count_timeout_seconds must be between 1 and 300 seconds");
            }
        }

        // Validate max_exact_count_threshold
        if (serviceConfig.Settings.TryGetValue("max_exact_count_threshold", out var maxCountObj))
        {
            if (maxCountObj is int maxCount && maxCount < -1)
            {
                result.AddError("max_exact_count_threshold must be -1 (unlimited) or a positive number");
            }
        }

        // Validate skip_count_limit_threshold (1-1000000)
        if (serviceConfig.Settings.TryGetValue("skip_count_limit_threshold", out var skipThresholdObj))
        {
            if (skipThresholdObj is int skipThreshold && (skipThreshold < 1 || skipThreshold > 1_000_000))
            {
                result.AddError("skip_count_limit_threshold must be between 1 and 1,000,000");
            }
        }

        // Validate streaming_page_size (1-10000)
        if (serviceConfig.Settings.TryGetValue("streaming_page_size", out var pageSizeObj))
        {
            if (pageSizeObj is int pageSize && (pageSize < 1 || pageSize > 10_000))
            {
                result.AddError("streaming_page_size must be between 1 and 10,000");
            }
        }

        // Validate max_streaming_items
        if (serviceConfig.Settings.TryGetValue("max_streaming_items", out var maxStreamObj))
        {
            if (maxStreamObj is int maxStream && maxStream < -1)
            {
                result.AddError("max_streaming_items must be -1 (unlimited) or a positive number");
            }
        }

        // Validate streaming_threshold (1-1000000)
        if (serviceConfig.Settings.TryGetValue("streaming_threshold", out var streamThresholdObj))
        {
            if (streamThresholdObj is int streamThreshold && (streamThreshold < 1 || streamThreshold > 1_000_000))
            {
                result.AddError("streaming_threshold must be between 1 and 1,000,000");
            }
        }

        // Validate version
        if (serviceConfig.Settings.TryGetValue("version", out var versionObj))
        {
            if (versionObj is string version)
            {
                var validVersions = new[] { "1.0.0", "1.0.0-rc.1", "1.0.0-rc.2" };
                if (!validVersions.Contains(version))
                {
                    result.AddWarning($"version '{version}' is not a standard STAC API version. Valid versions: {string.Join(", ", validVersions)}");
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
/// STAC service configuration extracted from declarative config.
/// </summary>
public sealed class StacServiceConfiguration
{
    public int CountTimeoutSeconds { get; init; }
    public bool UseCountEstimation { get; init; }
    public int MaxExactCountThreshold { get; init; }
    public bool SkipCountForLargeResultSets { get; init; }
    public int SkipCountLimitThreshold { get; init; }
    public int StreamingPageSize { get; init; }
    public int MaxStreamingItems { get; init; }
    public bool EnableAutoStreaming { get; init; }
    public int StreamingThreshold { get; init; }
    public string Version { get; init; } = "1.0.0";
}
