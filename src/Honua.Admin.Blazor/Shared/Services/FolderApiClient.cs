// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net.Http.Json;
using Honua.Admin.Blazor.Shared.Models;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// API client for folder management operations.
/// </summary>
public sealed class FolderApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FolderApiClient> _logger;

    public FolderApiClient(IHttpClientFactory httpClientFactory, ILogger<FolderApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("AdminApi");
        _logger = logger;
    }

    /// <summary>
    /// Gets all folders.
    /// </summary>
    public async Task<List<FolderResponse>> GetFoldersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/admin/metadata/folders", cancellationToken);
            response.EnsureSuccessStatusCode();
            var folders = await response.Content.ReadFromJsonAsync<List<FolderResponse>>(cancellationToken: cancellationToken);
            return folders ?? new List<FolderResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching folders");
            throw;
        }
    }

    /// <summary>
    /// Creates a new folder.
    /// </summary>
    public async Task<FolderResponse> CreateFolderAsync(CreateFolderRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/admin/metadata/folders", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var created = await response.Content.ReadFromJsonAsync<FolderResponse>(cancellationToken: cancellationToken);
            return created ?? throw new InvalidOperationException("Folder creation returned null response");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating folder {FolderId}", request.Id);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing folder.
    /// </summary>
    public async Task UpdateFolderAsync(string id, UpdateFolderRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/admin/metadata/folders/{Uri.EscapeDataString(id)}", request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating folder {FolderId}", id);
            throw;
        }
    }

    /// <summary>
    /// Deletes a folder.
    /// </summary>
    public async Task DeleteFolderAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/admin/metadata/folders/{Uri.EscapeDataString(id)}", cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting folder {FolderId}", id);
            throw;
        }
    }
}
