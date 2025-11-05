// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Shared.Models;
using System.Net.Http.Json;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// API client for dashboard metrics and monitoring.
/// </summary>
public class DashboardApiClient
{
    private readonly HttpClient _httpClient;

    public DashboardApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Gets comprehensive dashboard metrics.
    /// </summary>
    public async Task<DashboardMetrics> GetMetricsAsync(
        DashboardMetricsRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQueryString(request);
        var response = await _httpClient.GetAsync($"/admin/dashboard/metrics{query}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var metrics = await response.Content.ReadFromJsonAsync<DashboardMetrics>(cancellationToken);
        return metrics ?? new DashboardMetrics();
    }

    /// <summary>
    /// Gets overview metrics only (faster, for quick updates).
    /// </summary>
    public async Task<OverviewMetrics> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/admin/dashboard/overview", cancellationToken);
        response.EnsureSuccessStatusCode();

        var overview = await response.Content.ReadFromJsonAsync<OverviewMetrics>(cancellationToken);
        return overview ?? new OverviewMetrics();
    }

    /// <summary>
    /// Gets performance metrics for a specific time range.
    /// </summary>
    public async Task<PerformanceMetrics> GetPerformanceMetricsAsync(
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        CancellationToken cancellationToken = default)
    {
        var query = BuildTimeRangeQuery(startTime, endTime);
        var response = await _httpClient.GetAsync($"/admin/dashboard/performance{query}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var metrics = await response.Content.ReadFromJsonAsync<PerformanceMetrics>(cancellationToken);
        return metrics ?? new PerformanceMetrics();
    }

    /// <summary>
    /// Gets service distribution statistics.
    /// </summary>
    public async Task<ServiceDistribution> GetServiceDistributionAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/admin/dashboard/service-distribution", cancellationToken);
        response.EnsureSuccessStatusCode();

        var distribution = await response.Content.ReadFromJsonAsync<ServiceDistribution>(cancellationToken);
        return distribution ?? new ServiceDistribution();
    }

    /// <summary>
    /// Gets top services by usage.
    /// </summary>
    public async Task<List<ServiceUsageMetric>> GetTopServicesAsync(
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/admin/dashboard/top-services?limit={limit}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var services = await response.Content.ReadFromJsonAsync<List<ServiceUsageMetric>>(cancellationToken);
        return services ?? new List<ServiceUsageMetric>();
    }

    /// <summary>
    /// Gets recent activity feed.
    /// </summary>
    public async Task<List<ActivityEntry>> GetRecentActivityAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/admin/dashboard/recent-activity?limit={limit}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var activities = await response.Content.ReadFromJsonAsync<List<ActivityEntry>>(cancellationToken);
        return activities ?? new List<ActivityEntry>();
    }

    /// <summary>
    /// Gets system health status.
    /// </summary>
    public async Task<HealthStatus> GetHealthStatusAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/admin/dashboard/health", cancellationToken);
        response.EnsureSuccessStatusCode();

        var health = await response.Content.ReadFromJsonAsync<HealthStatus>(cancellationToken);
        return health ?? new HealthStatus();
    }

    /// <summary>
    /// Gets storage metrics.
    /// </summary>
    public async Task<StorageMetrics> GetStorageMetricsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/admin/dashboard/storage", cancellationToken);
        response.EnsureSuccessStatusCode();

        var storage = await response.Content.ReadFromJsonAsync<StorageMetrics>(cancellationToken);
        return storage ?? new StorageMetrics();
    }

    /// <summary>
    /// Gets request time series data for charts.
    /// </summary>
    public async Task<List<TimeSeriesDataPoint>> GetRequestTimeSeriesAsync(
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        string granularity = "1h",
        CancellationToken cancellationToken = default)
    {
        var query = BuildTimeRangeQuery(startTime, endTime);
        query += query.Contains('?') ? $"&granularity={granularity}" : $"?granularity={granularity}";

        var response = await _httpClient.GetAsync($"/admin/dashboard/timeseries/requests{query}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var timeSeries = await response.Content.ReadFromJsonAsync<List<TimeSeriesDataPoint>>(cancellationToken);
        return timeSeries ?? new List<TimeSeriesDataPoint>();
    }

    private string BuildQueryString(DashboardMetricsRequest? request)
    {
        if (request == null)
            return string.Empty;

        var queryParams = new List<string>();

        if (request.StartTime.HasValue)
            queryParams.Add($"startTime={request.StartTime.Value:O}");

        if (request.EndTime.HasValue)
            queryParams.Add($"endTime={request.EndTime.Value:O}");

        if (!request.IncludeTimeSeries)
            queryParams.Add("includeTimeSeries=false");

        if (!request.IncludeRecentActivity)
            queryParams.Add("includeRecentActivity=false");

        if (request.ActivityLimit != 20)
            queryParams.Add($"activityLimit={request.ActivityLimit}");

        return queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
    }

    private string BuildTimeRangeQuery(DateTimeOffset? startTime, DateTimeOffset? endTime)
    {
        var queryParams = new List<string>();

        if (startTime.HasValue)
            queryParams.Add($"startTime={startTime.Value:O}");

        if (endTime.HasValue)
            queryParams.Add($"endTime={endTime.Value:O}");

        return queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
    }
}
