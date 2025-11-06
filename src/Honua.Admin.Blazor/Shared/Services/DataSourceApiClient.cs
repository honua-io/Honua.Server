// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net.Http.Json;
using Honua.Admin.Blazor.Shared.Models;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// API client for data source management operations.
/// </summary>
public sealed class DataSourceApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DataSourceApiClient> _logger;

    public DataSourceApiClient(IHttpClientFactory httpClientFactory, ILogger<DataSourceApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("AdminApi");
        _logger = logger;
    }

    /// <summary>
    /// Gets all data sources.
    /// </summary>
    public async Task<List<DataSourceListItem>> GetDataSourcesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/admin/metadata/datasources", cancellationToken);
            response.EnsureSuccessStatusCode();
            var dataSources = await response.Content.ReadFromJsonAsync<List<DataSourceListItem>>(cancellationToken: cancellationToken);
            return dataSources ?? new List<DataSourceListItem>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching data sources");
            throw;
        }
    }

    /// <summary>
    /// Gets a specific data source by ID.
    /// </summary>
    public async Task<DataSourceResponse?> GetDataSourceByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/admin/metadata/datasources/{Uri.EscapeDataString(id)}", cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<DataSourceResponse>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching data source {DataSourceId}", id);
            throw;
        }
    }

    /// <summary>
    /// Creates a new data source.
    /// </summary>
    public async Task<DataSourceResponse> CreateDataSourceAsync(CreateDataSourceRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/admin/metadata/datasources", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var created = await response.Content.ReadFromJsonAsync<DataSourceResponse>(cancellationToken: cancellationToken);
            return created ?? throw new InvalidOperationException("Data source creation returned null response");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating data source {DataSourceId}", request.Id);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing data source.
    /// </summary>
    public async Task UpdateDataSourceAsync(string id, UpdateDataSourceRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/admin/metadata/datasources/{Uri.EscapeDataString(id)}", request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating data source {DataSourceId}", id);
            throw;
        }
    }

    /// <summary>
    /// Deletes a data source.
    /// </summary>
    public async Task DeleteDataSourceAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/admin/metadata/datasources/{Uri.EscapeDataString(id)}", cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting data source {DataSourceId}", id);
            throw;
        }
    }

    /// <summary>
    /// Tests connection to a data source.
    /// </summary>
    public async Task<TestConnectionResponse> TestConnectionAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/admin/metadata/datasources/{Uri.EscapeDataString(id)}/test", null, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<TestConnectionResponse>(cancellationToken: cancellationToken);
            return result ?? new TestConnectionResponse { Success = false, Message = "No response from server" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing connection for data source {DataSourceId}", id);
            return new TestConnectionResponse { Success = false, Message = $"Connection test failed: {ex.Message}" };
        }
    }

    /// <summary>
    /// Gets list of tables from a data source.
    /// </summary>
    public async Task<List<TableInfo>> GetTablesAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/admin/metadata/datasources/{Uri.EscapeDataString(id)}/tables", cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<TableListResponse>(cancellationToken: cancellationToken);
            return result?.Tables ?? new List<TableInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tables for data source {DataSourceId}", id);
            throw;
        }
    }
}
