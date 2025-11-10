// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Enterprise.BIConnectors.PowerBI.Configuration;

/// <summary>
/// Configuration options for Power BI integration.
/// </summary>
public class PowerBIOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "PowerBI";

    /// <summary>
    /// Azure AD Tenant ID
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Azure AD Client ID (Application ID) for Service Principal authentication
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Azure AD Client Secret for Service Principal authentication
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Power BI Workspace ID where datasets will be published
    /// </summary>
    public string WorkspaceId { get; set; } = string.Empty;

    /// <summary>
    /// Power BI API base URL (default: https://api.powerbi.com)
    /// </summary>
    public string ApiUrl { get; set; } = "https://api.powerbi.com";

    /// <summary>
    /// Enable OData feed endpoints for Power BI connectivity
    /// </summary>
    [Obsolete("OData feeds are now built into Honua.Server.Host at /odata/{collection}. This setting is ignored.")]
    public bool EnableODataFeeds { get; set; } = true;

    /// <summary>
    /// Enable Power BI Push Datasets for real-time streaming
    /// </summary>
    public bool EnablePushDatasets { get; set; } = true;

    /// <summary>
    /// Enable automatic dataset refresh configuration
    /// </summary>
    public bool EnableDatasetRefresh { get; set; } = true;

    /// <summary>
    /// Maximum rows per OData query (for performance)
    /// </summary>
    [Obsolete("OData configuration is now handled in Honua.Server.Host OData settings. This setting is ignored.")]
    public int MaxODataPageSize { get; set; } = 5000;

    /// <summary>
    /// Dataset configurations for each smart city dashboard
    /// </summary>
    public List<PowerBIDatasetConfig> Datasets { get; set; } = new();

    /// <summary>
    /// Streaming dataset configurations
    /// </summary>
    public List<PowerBIStreamingDatasetConfig> StreamingDatasets { get; set; } = new();

    /// <summary>
    /// Rate limit settings for Push Datasets API (rows per hour)
    /// </summary>
    public int PushDatasetRateLimitPerHour { get; set; } = 10000;

    /// <summary>
    /// Batch size for streaming data uploads
    /// </summary>
    public int StreamingBatchSize { get; set; } = 100;

    /// <summary>
    /// Base URL of Honua.Server (for generating OData feed URLs)
    /// </summary>
    public string HonuaServerBaseUrl { get; set; } = string.Empty;
}

/// <summary>
/// Configuration for a Power BI dataset
/// </summary>
public class PowerBIDatasetConfig
{
    /// <summary>
    /// Dataset name in Power BI
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Dataset ID (if already exists)
    /// </summary>
    public string? DatasetId { get; set; }

    /// <summary>
    /// Dataset type (Traffic, AirQuality, 311Requests, AssetManagement, BuildingOccupancy)
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// OData collection IDs to include in this dataset
    /// </summary>
    public List<string> CollectionIds { get; set; } = new();

    /// <summary>
    /// Enable incremental refresh
    /// </summary>
    public bool EnableIncrementalRefresh { get; set; } = true;

    /// <summary>
    /// Datetime column for incremental refresh
    /// </summary>
    public string? IncrementalRefreshColumn { get; set; }

    /// <summary>
    /// Refresh schedule (cron expression)
    /// </summary>
    public string? RefreshSchedule { get; set; }
}

/// <summary>
/// Configuration for a Power BI streaming dataset
/// </summary>
public class PowerBIStreamingDatasetConfig
{
    /// <summary>
    /// Streaming dataset name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Streaming dataset ID (if already exists)
    /// </summary>
    public string? DatasetId { get; set; }

    /// <summary>
    /// Push URL (if already exists)
    /// </summary>
    public string? PushUrl { get; set; }

    /// <summary>
    /// Data source type (Observations, Alerts, Events)
    /// </summary>
    public string SourceType { get; set; } = string.Empty;

    /// <summary>
    /// SensorThings Datastream IDs to stream (for Observations)
    /// </summary>
    public List<string> DatastreamIds { get; set; } = new();

    /// <summary>
    /// Enable automatic streaming
    /// </summary>
    public bool AutoStream { get; set; } = true;

    /// <summary>
    /// Historical rows to retain
    /// </summary>
    public int RetentionPolicy { get; set; } = 200000;
}
