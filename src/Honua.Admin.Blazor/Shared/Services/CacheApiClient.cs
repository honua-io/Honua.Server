// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net.Http.Json;
using Honua.Admin.Blazor.Shared.Models;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// API client for raster tile cache operations.
/// </summary>
public sealed class CacheApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CacheApiClient> _logger;

    public CacheApiClient(IHttpClientFactory httpClientFactory, ILogger<CacheApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("AdminApi");
        _logger = logger;
    }

    /// <summary>
    /// Gets overall cache statistics.
    /// </summary>
    public async Task<CacheStatistics?> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/admin/raster-cache/statistics", cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<CacheStatistics>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache statistics");
            throw;
        }
    }

    /// <summary>
    /// Gets cache statistics for all datasets.
    /// </summary>
    public async Task<List<DatasetCacheStatistics>> GetAllDatasetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/admin/raster-cache/statistics/datasets", cancellationToken);
            response.EnsureSuccessStatusCode();

            var stats = await response.Content.ReadFromJsonAsync<List<DatasetCacheStatistics>>(cancellationToken: cancellationToken);
            return stats ?? new List<DatasetCacheStatistics>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dataset cache statistics");
            throw;
        }
    }

    /// <summary>
    /// Gets cache statistics for a specific dataset.
    /// </summary>
    public async Task<DatasetCacheStatistics?> GetDatasetStatisticsAsync(string datasetId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/admin/raster-cache/statistics/datasets/{Uri.EscapeDataString(datasetId)}", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<DatasetCacheStatistics>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dataset cache statistics for {DatasetId}", datasetId);
            throw;
        }
    }

    /// <summary>
    /// Resets cache statistics.
    /// </summary>
    public async Task ResetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync("/admin/raster-cache/statistics/reset", null, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting cache statistics");
            throw;
        }
    }

    /// <summary>
    /// Creates a preseed job to pre-generate tiles.
    /// </summary>
    public async Task<PreseedJobSnapshot?> CreatePreseedJobAsync(CreatePreseedJobRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/admin/raster-cache/jobs", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<PreseedJobResponse>(cancellationToken: cancellationToken);
            return result?.Job;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating preseed job");
            throw;
        }
    }

    /// <summary>
    /// Lists all preseed jobs.
    /// </summary>
    public async Task<List<PreseedJobSnapshot>> ListPreseedJobsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/admin/raster-cache/jobs", cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<PreseedJobListResponse>(cancellationToken: cancellationToken);
            return result?.Jobs ?? new List<PreseedJobSnapshot>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing preseed jobs");
            throw;
        }
    }

    /// <summary>
    /// Gets a specific preseed job.
    /// </summary>
    public async Task<PreseedJobSnapshot?> GetPreseedJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/admin/raster-cache/jobs/{jobId}", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<PreseedJobResponse>(cancellationToken: cancellationToken);
            return result?.Job;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting preseed job {JobId}", jobId);
            throw;
        }
    }

    /// <summary>
    /// Cancels a preseed job.
    /// </summary>
    public async Task<bool> CancelPreseedJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/admin/raster-cache/jobs/{jobId}", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }

            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling preseed job {JobId}", jobId);
            throw;
        }
    }

    /// <summary>
    /// Purges cached tiles for specific datasets.
    /// </summary>
    public async Task<PurgeCacheResult?> PurgeCacheAsync(PurgeCacheRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/admin/raster-cache/datasets/purge", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<PurgeCacheResult>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error purging cache");
            throw;
        }
    }

    private sealed class PreseedJobResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("job")]
        public PreseedJobSnapshot? Job { get; set; }
    }

    private sealed class PreseedJobListResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("jobs")]
        public List<PreseedJobSnapshot> Jobs { get; set; } = new();
    }
}
