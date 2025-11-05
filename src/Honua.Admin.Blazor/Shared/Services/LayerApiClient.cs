// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net.Http.Json;
using Honua.Admin.Blazor.Shared.Models;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// API client for layer management operations.
/// </summary>
public sealed class LayerApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LayerApiClient> _logger;
    private readonly ClientCacheService _cache;

    public LayerApiClient(IHttpClientFactory httpClientFactory, ILogger<LayerApiClient> logger, ClientCacheService cache)
    {
        _httpClient = httpClientFactory.CreateClient("AdminApi");
        _logger = logger;
        _cache = cache;
    }

    /// <summary>
    /// Gets all layers, optionally filtered by service.
    /// </summary>
    public async Task<List<LayerListItem>> GetLayersAsync(string? serviceId = null, CancellationToken cancellationToken = default)
    {
        var cacheKey = string.IsNullOrEmpty(serviceId) ? CacheKeys.Layers() : $"layers:service:{serviceId}";

        return await _cache.GetOrSetAsync(
            cacheKey,
            async () =>
            {
                try
                {
                    var url = "/admin/metadata/layers";
                    if (!string.IsNullOrEmpty(serviceId))
                    {
                        url += $"?serviceId={Uri.EscapeDataString(serviceId)}";
                    }

                    var response = await _httpClient.GetAsync(url, cancellationToken);
                    response.EnsureSuccessStatusCode();
                    var layers = await response.Content.ReadFromJsonAsync<List<LayerListItem>>(cancellationToken: cancellationToken);
                    return layers ?? new List<LayerListItem>();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching layers");
                    throw;
                }
            },
            ttl: TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Gets a layer by ID.
    /// </summary>
    public async Task<LayerResponse?> GetLayerByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrSetAsync(
            CacheKeys.Layer(id),
            async () =>
            {
                try
                {
                    var response = await _httpClient.GetAsync($"/admin/metadata/layers/{Uri.EscapeDataString(id)}", cancellationToken);

                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return null;
                    }

                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadFromJsonAsync<LayerResponse>(cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching layer {LayerId}", id);
                    throw;
                }
            },
            ttl: TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Creates a new layer.
    /// </summary>
    public async Task<LayerResponse> CreateLayerAsync(CreateLayerRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/admin/metadata/layers", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var created = await response.Content.ReadFromJsonAsync<LayerResponse>(cancellationToken: cancellationToken);

            // Invalidate layers cache
            _cache.InvalidatePrefix("layers:");

            return created ?? throw new InvalidOperationException("Layer creation returned null response");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating layer {LayerId}", request.Id);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing layer.
    /// </summary>
    public async Task UpdateLayerAsync(string id, UpdateLayerRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/admin/metadata/layers/{Uri.EscapeDataString(id)}", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            // Invalidate layers cache
            _cache.InvalidatePrefix("layers:");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating layer {LayerId}", id);
            throw;
        }
    }

    /// <summary>
    /// Deletes a layer.
    /// </summary>
    public async Task DeleteLayerAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/admin/metadata/layers/{Uri.EscapeDataString(id)}", cancellationToken);
            response.EnsureSuccessStatusCode();

            // Invalidate layers cache
            _cache.InvalidatePrefix("layers:");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting layer {LayerId}", id);
            throw;
        }
    }
}
