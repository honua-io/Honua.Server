// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Shared.Models;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// API client for bulk operations.
/// </summary>
public class BulkOperationsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public BulkOperationsApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Bulk delete services.
    /// </summary>
    public async Task<BulkOperationResponse> BulkDeleteServicesAsync(
        List<string> serviceIds,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var request = new BulkDeleteRequest
        {
            Ids = serviceIds,
            Force = force
        };

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/admin/services/bulk-delete", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<BulkOperationResponse>(_jsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to deserialize bulk operation response");
    }

    /// <summary>
    /// Bulk delete layers.
    /// </summary>
    public async Task<BulkOperationResponse> BulkDeleteLayersAsync(
        List<string> layerIds,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var request = new BulkDeleteRequest
        {
            Ids = layerIds,
            Force = force
        };

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/admin/layers/bulk-delete", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<BulkOperationResponse>(_jsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to deserialize bulk operation response");
    }

    /// <summary>
    /// Bulk move services to folder.
    /// </summary>
    public async Task<BulkOperationResponse> BulkMoveServicesToFolderAsync(
        List<string> serviceIds,
        string? targetFolderId,
        CancellationToken cancellationToken = default)
    {
        var request = new BulkMoveToFolderRequest
        {
            Ids = serviceIds,
            TargetFolderId = targetFolderId
        };

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/admin/services/bulk-move", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<BulkOperationResponse>(_jsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to deserialize bulk operation response");
    }

    /// <summary>
    /// Bulk move layers to folder.
    /// </summary>
    public async Task<BulkOperationResponse> BulkMoveLayersToFolderAsync(
        List<string> layerIds,
        string? targetFolderId,
        CancellationToken cancellationToken = default)
    {
        var request = new BulkMoveToFolderRequest
        {
            Ids = layerIds,
            TargetFolderId = targetFolderId
        };

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/admin/layers/bulk-move", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<BulkOperationResponse>(_jsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to deserialize bulk operation response");
    }

    /// <summary>
    /// Bulk update metadata (tags, keywords) for services.
    /// </summary>
    public async Task<BulkOperationResponse> BulkUpdateServiceMetadataAsync(
        List<string> serviceIds,
        List<string>? tags,
        List<string>? keywords,
        string updateMode = "merge",
        CancellationToken cancellationToken = default)
    {
        var request = new BulkUpdateMetadataRequest
        {
            Ids = serviceIds,
            Tags = tags,
            Keywords = keywords,
            UpdateMode = updateMode
        };

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/admin/services/bulk-update-metadata", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<BulkOperationResponse>(_jsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to deserialize bulk operation response");
    }

    /// <summary>
    /// Bulk update metadata (tags, keywords) for layers.
    /// </summary>
    public async Task<BulkOperationResponse> BulkUpdateLayerMetadataAsync(
        List<string> layerIds,
        List<string>? tags,
        List<string>? keywords,
        string updateMode = "merge",
        CancellationToken cancellationToken = default)
    {
        var request = new BulkUpdateMetadataRequest
        {
            Ids = layerIds,
            Tags = tags,
            Keywords = keywords,
            UpdateMode = updateMode
        };

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/admin/layers/bulk-update-metadata", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<BulkOperationResponse>(_jsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to deserialize bulk operation response");
    }

    /// <summary>
    /// Bulk apply style to layers.
    /// </summary>
    public async Task<BulkOperationResponse> BulkApplyStyleAsync(
        List<string> layerIds,
        string styleId,
        bool setAsDefault = false,
        CancellationToken cancellationToken = default)
    {
        var request = new BulkApplyStyleRequest
        {
            LayerIds = layerIds,
            StyleId = styleId,
            SetAsDefault = setAsDefault
        };

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/admin/layers/bulk-apply-style", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<BulkOperationResponse>(_jsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to deserialize bulk operation response");
    }

    /// <summary>
    /// Bulk enable/disable services.
    /// </summary>
    public async Task<BulkOperationResponse> BulkSetServiceEnabledAsync(
        List<string> serviceIds,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var request = new BulkEnableServicesRequest
        {
            ServiceIds = serviceIds,
            Enabled = enabled
        };

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/admin/services/bulk-set-enabled", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<BulkOperationResponse>(_jsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to deserialize bulk operation response");
    }

    /// <summary>
    /// Gets the status of a bulk operation.
    /// </summary>
    public async Task<BulkOperationStatus> GetOperationStatusAsync(
        string operationId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"/admin/bulk-operations/{Uri.EscapeDataString(operationId)}/status",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<BulkOperationStatus>(_jsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to deserialize operation status");
    }

    /// <summary>
    /// Cancels a running bulk operation.
    /// </summary>
    public async Task CancelOperationAsync(
        string operationId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync(
            $"/admin/bulk-operations/{Uri.EscapeDataString(operationId)}/cancel",
            null,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Gets the results of a completed bulk operation.
    /// </summary>
    public async Task<BulkOperationResponse> GetOperationResultsAsync(
        string operationId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"/admin/bulk-operations/{Uri.EscapeDataString(operationId)}/results",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<BulkOperationResponse>(_jsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to deserialize operation results");
    }
}
