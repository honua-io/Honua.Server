// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.Admin.Blazor.Shared.Models;

/// <summary>
/// Enhanced dashboard statistics with detailed metrics.
/// </summary>
public sealed class DashboardMetrics
{
    [JsonPropertyName("overview")]
    public OverviewMetrics Overview { get; set; } = new();

    [JsonPropertyName("performance")]
    public PerformanceMetrics Performance { get; set; } = new();

    [JsonPropertyName("serviceDistribution")]
    public ServiceDistribution ServiceDistribution { get; set; } = new();

    [JsonPropertyName("topServices")]
    public List<ServiceUsageMetric> TopServices { get; set; } = new();

    [JsonPropertyName("recentActivity")]
    public List<ActivityEntry> RecentActivity { get; set; } = new();

    [JsonPropertyName("healthStatus")]
    public HealthStatus HealthStatus { get; set; } = new();

    [JsonPropertyName("storageMetrics")]
    public StorageMetrics StorageMetrics { get; set; } = new();
}

/// <summary>
/// Overview metrics for the dashboard.
/// </summary>
public sealed class OverviewMetrics
{
    [JsonPropertyName("totalServices")]
    public int TotalServices { get; set; }

    [JsonPropertyName("totalLayers")]
    public int TotalLayers { get; set; }

    [JsonPropertyName("totalFolders")]
    public int TotalFolders { get; set; }

    [JsonPropertyName("enabledServices")]
    public int EnabledServices { get; set; }

    [JsonPropertyName("disabledServices")]
    public int DisabledServices { get; set; }

    [JsonPropertyName("activeUsers")]
    public int ActiveUsers { get; set; }

    [JsonPropertyName("totalRequests24h")]
    public long TotalRequests24h { get; set; }
}

/// <summary>
/// Performance metrics for API and cache.
/// </summary>
public sealed class PerformanceMetrics
{
    [JsonPropertyName("requestsPerSecond")]
    public double RequestsPerSecond { get; set; }

    [JsonPropertyName("averageResponseTime")]
    public double AverageResponseTime { get; set; }

    [JsonPropertyName("p95ResponseTime")]
    public double P95ResponseTime { get; set; }

    [JsonPropertyName("p99ResponseTime")]
    public double P99ResponseTime { get; set; }

    [JsonPropertyName("cacheHitRate")]
    public double CacheHitRate { get; set; }

    [JsonPropertyName("errorRate")]
    public double ErrorRate { get; set; }

    [JsonPropertyName("requestTimeSeries")]
    public List<TimeSeriesDataPoint> RequestTimeSeries { get; set; } = new();
}

/// <summary>
/// Service distribution by type.
/// </summary>
public sealed class ServiceDistribution
{
    [JsonPropertyName("wmsCount")]
    public int WmsCount { get; set; }

    [JsonPropertyName("wfsCount")]
    public int WfsCount { get; set; }

    [JsonPropertyName("wmtsCount")]
    public int WmtsCount { get; set; }

    [JsonPropertyName("ogcCount")]
    public int OgcCount { get; set; }

    [JsonPropertyName("otherCount")]
    public int OtherCount { get; set; }
}

/// <summary>
/// Service usage metric.
/// </summary>
public sealed class ServiceUsageMetric
{
    [JsonPropertyName("serviceId")]
    public string ServiceId { get; set; } = string.Empty;

    [JsonPropertyName("serviceName")]
    public string ServiceName { get; set; } = string.Empty;

    [JsonPropertyName("serviceType")]
    public string ServiceType { get; set; } = string.Empty;

    [JsonPropertyName("requestCount")]
    public long RequestCount { get; set; }

    [JsonPropertyName("errorCount")]
    public long ErrorCount { get; set; }

    [JsonPropertyName("averageResponseTime")]
    public double AverageResponseTime { get; set; }
}

/// <summary>
/// Activity entry for recent activity feed.
/// </summary>
public sealed class ActivityEntry
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("resourceType")]
    public string ResourceType { get; set; } = string.Empty;

    [JsonPropertyName("resourceId")]
    public string ResourceId { get; set; } = string.Empty;

    [JsonPropertyName("resourceName")]
    public string ResourceName { get; set; } = string.Empty;

    [JsonPropertyName("userName")]
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Health status for system components.
/// </summary>
public sealed class HealthStatus
{
    [JsonPropertyName("overall")]
    public string Overall { get; set; } = "Healthy";

    [JsonPropertyName("database")]
    public ComponentHealth Database { get; set; } = new();

    [JsonPropertyName("cache")]
    public ComponentHealth Cache { get; set; } = new();

    [JsonPropertyName("storage")]
    public ComponentHealth Storage { get; set; } = new();

    [JsonPropertyName("externalServices")]
    public ComponentHealth ExternalServices { get; set; } = new();

    [JsonPropertyName("lastChecked")]
    public DateTimeOffset LastChecked { get; set; }
}

/// <summary>
/// Component health status.
/// </summary>
public sealed class ComponentHealth
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Healthy"; // Healthy, Degraded, Unhealthy

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("responseTime")]
    public double? ResponseTime { get; set; }
}

/// <summary>
/// Storage metrics.
/// </summary>
public sealed class StorageMetrics
{
    [JsonPropertyName("totalSize")]
    public long TotalSize { get; set; }

    [JsonPropertyName("usedSize")]
    public long UsedSize { get; set; }

    [JsonPropertyName("availableSize")]
    public long AvailableSize { get; set; }

    [JsonPropertyName("usagePercentage")]
    public double UsagePercentage { get; set; }

    [JsonPropertyName("datasetCount")]
    public int DatasetCount { get; set; }

    [JsonPropertyName("largestDatasets")]
    public List<DatasetSize> LargestDatasets { get; set; } = new();
}

/// <summary>
/// Dataset size information.
/// </summary>
public sealed class DatasetSize
{
    [JsonPropertyName("datasetId")]
    public string DatasetId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("featureCount")]
    public long FeatureCount { get; set; }
}

/// <summary>
/// Time series data point for charts.
/// </summary>
public sealed class TimeSeriesDataPoint
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }
}

/// <summary>
/// Request for dashboard metrics with time range.
/// </summary>
public sealed class DashboardMetricsRequest
{
    [JsonPropertyName("startTime")]
    public DateTimeOffset? StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public DateTimeOffset? EndTime { get; set; }

    [JsonPropertyName("includeTimeSeries")]
    public bool IncludeTimeSeries { get; set; } = true;

    [JsonPropertyName("includeRecentActivity")]
    public bool IncludeRecentActivity { get; set; } = true;

    [JsonPropertyName("activityLimit")]
    public int ActivityLimit { get; set; } = 20;
}
