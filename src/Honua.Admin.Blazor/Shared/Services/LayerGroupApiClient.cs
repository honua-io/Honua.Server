// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net.Http.Json;
using Honua.Admin.Blazor.Shared.Models;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// API client for layer group management operations.
/// </summary>
public sealed class LayerGroupApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LayerGroupApiClient> _logger;

    public LayerGroupApiClient(IHttpClientFactory httpClientFactory, ILogger<LayerGroupApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("AdminApi");
        _logger = logger;
    }

    /// <summary>
    /// Gets all layer groups, optionally filtered by service.
    /// </summary>
    public async Task<List<LayerGroupListItem>> GetLayerGroupsAsync(string? serviceId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = "/admin/metadata/layergroups";
            if (!string.IsNullOrEmpty(serviceId))
            {
                url += $"?serviceId={Uri.EscapeDataString(serviceId)}";
            }

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            var layerGroups = await response.Content.ReadFromJsonAsync<List<LayerGroupListItem>>(cancellationToken: cancellationToken);
            return layerGroups ?? new List<LayerGroupListItem>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching layer groups");
            throw;
        }
    }

    /// <summary>
    /// Gets a layer group by ID.
    /// </summary>
    public async Task<LayerGroupResponse?> GetLayerGroupByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/admin/metadata/layergroups/{Uri.EscapeDataString(id)}", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<LayerGroupResponse>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching layer group {LayerGroupId}", id);
            throw;
        }
    }

    /// <summary>
    /// Creates a new layer group.
    /// </summary>
    public async Task<LayerGroupResponse> CreateLayerGroupAsync(CreateLayerGroupRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/admin/metadata/layergroups", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var created = await response.Content.ReadFromJsonAsync<LayerGroupResponse>(cancellationToken: cancellationToken);
            return created ?? throw new InvalidOperationException("Layer group creation returned null response");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating layer group {LayerGroupId}", request.Id);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing layer group.
    /// </summary>
    public async Task UpdateLayerGroupAsync(string id, UpdateLayerGroupRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/admin/metadata/layergroups/{Uri.EscapeDataString(id)}", request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating layer group {LayerGroupId}", id);
            throw;
        }
    }

    /// <summary>
    /// Deletes a layer group.
    /// </summary>
    public async Task DeleteLayerGroupAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/admin/metadata/layergroups/{Uri.EscapeDataString(id)}", cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting layer group {LayerGroupId}", id);
            throw;
        }
    }
}
