// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net.Http.Json;
using Honua.Admin.Blazor.Shared.Models;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// API client for service management operations.
/// </summary>
public sealed class ServiceApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ServiceApiClient> _logger;

    public ServiceApiClient(IHttpClientFactory httpClientFactory, ILogger<ServiceApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("AdminApi");
        _logger = logger;
    }

    /// <summary>
    /// Gets dashboard statistics.
    /// </summary>
    public async Task<DashboardStats?> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/admin/metadata/stats", cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<DashboardStats>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching dashboard stats");
            return null;
        }
    }

    /// <summary>
    /// Gets all services.
    /// </summary>
    public async Task<List<ServiceListItem>> GetServicesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/admin/metadata/services", cancellationToken);
            response.EnsureSuccessStatusCode();
            var services = await response.Content.ReadFromJsonAsync<List<ServiceListItem>>(cancellationToken: cancellationToken);
            return services ?? new List<ServiceListItem>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching services");
            throw;
        }
    }

    /// <summary>
    /// Gets a service by ID.
    /// </summary>
    public async Task<ServiceResponse?> GetServiceByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/admin/metadata/services/{Uri.EscapeDataString(id)}", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ServiceResponse>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching service {ServiceId}", id);
            throw;
        }
    }

    /// <summary>
    /// Creates a new service.
    /// </summary>
    public async Task<ServiceResponse> CreateServiceAsync(CreateServiceRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/admin/metadata/services", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var created = await response.Content.ReadFromJsonAsync<ServiceResponse>(cancellationToken: cancellationToken);
            return created ?? throw new InvalidOperationException("Service creation returned null response");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating service {ServiceId}", request.Id);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing service.
    /// </summary>
    public async Task UpdateServiceAsync(string id, UpdateServiceRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/admin/metadata/services/{Uri.EscapeDataString(id)}", request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating service {ServiceId}", id);
            throw;
        }
    }

    /// <summary>
    /// Deletes a service.
    /// </summary>
    public async Task DeleteServiceAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/admin/metadata/services/{Uri.EscapeDataString(id)}", cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting service {ServiceId}", id);
            throw;
        }
    }
}
