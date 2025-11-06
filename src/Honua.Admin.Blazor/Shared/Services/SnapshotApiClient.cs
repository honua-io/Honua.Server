// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net.Http.Json;
using System.Text.Json;
using Honua.Admin.Blazor.Shared.Models;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// API client for metadata snapshot/versioning operations.
/// </summary>
public sealed class SnapshotApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SnapshotApiClient> _logger;

    public SnapshotApiClient(IHttpClientFactory httpClientFactory, ILogger<SnapshotApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("AdminApi");
        _logger = logger;
    }

    /// <summary>
    /// Lists all metadata snapshots.
    /// </summary>
    public async Task<List<SnapshotDescriptor>> ListSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/admin/metadata/snapshots", cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<SnapshotListResponse>(cancellationToken: cancellationToken);
            return result?.Snapshots ?? new List<SnapshotDescriptor>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing snapshots");
            throw;
        }
    }

    /// <summary>
    /// Gets snapshot details including metadata content.
    /// </summary>
    public async Task<SnapshotDetails?> GetSnapshotAsync(string label, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/admin/metadata/snapshots/{Uri.EscapeDataString(label)}", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SnapshotDetails>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting snapshot {Label}", label);
            throw;
        }
    }

    /// <summary>
    /// Creates a new metadata snapshot.
    /// </summary>
    public async Task<SnapshotDescriptor?> CreateSnapshotAsync(CreateSnapshotRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/admin/metadata/snapshots", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<CreateSnapshotResponse>(cancellationToken: cancellationToken);
            return result?.Snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating snapshot");
            throw;
        }
    }

    /// <summary>
    /// Restores a metadata snapshot.
    /// </summary>
    public async Task<bool> RestoreSnapshotAsync(string label, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/admin/metadata/snapshots/{Uri.EscapeDataString(label)}/restore", null, cancellationToken);
            response.EnsureSuccessStatusCode();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring snapshot {Label}", label);
            throw;
        }
    }

    /// <summary>
    /// Computes a diff between two snapshots.
    /// Client-side computation by comparing metadata content.
    /// </summary>
    public MetadataDiffResult ComputeDiff(string oldMetadata, string newMetadata)
    {
        var diff = new MetadataDiffResult();

        try
        {
            var oldDoc = JsonDocument.Parse(oldMetadata);
            var newDoc = JsonDocument.Parse(newMetadata);

            // Compare services
            var oldServices = GetEntityIds(oldDoc, "services");
            var newServices = GetEntityIds(newDoc, "services");
            diff.AddedServices = newServices.Except(oldServices).ToList();
            diff.RemovedServices = oldServices.Except(newServices).ToList();
            diff.ModifiedServices = oldServices.Intersect(newServices)
                .Where(id => IsModified(oldDoc, newDoc, "services", id))
                .ToList();

            // Compare layers
            var oldLayers = GetEntityIds(oldDoc, "layers");
            var newLayers = GetEntityIds(newDoc, "layers");
            diff.AddedLayers = newLayers.Except(oldLayers).ToList();
            diff.RemovedLayers = oldLayers.Except(newLayers).ToList();
            diff.ModifiedLayers = oldLayers.Intersect(newLayers)
                .Where(id => IsModified(oldDoc, newDoc, "layers", id))
                .ToList();

            // Compare folders
            var oldFolders = GetEntityIds(oldDoc, "folders");
            var newFolders = GetEntityIds(newDoc, "folders");
            diff.AddedFolders = newFolders.Except(oldFolders).ToList();
            diff.RemovedFolders = oldFolders.Except(newFolders).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error computing diff");
        }

        return diff;
    }

    private List<string> GetEntityIds(JsonDocument doc, string entityType)
    {
        var ids = new List<string>();

        if (doc.RootElement.TryGetProperty(entityType, out var entitiesElement) &&
            entitiesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var entity in entitiesElement.EnumerateArray())
            {
                if (entity.TryGetProperty("id", out var idElement))
                {
                    var id = idElement.GetString();
                    if (!string.IsNullOrEmpty(id))
                    {
                        ids.Add(id);
                    }
                }
            }
        }

        return ids;
    }

    private bool IsModified(JsonDocument oldDoc, JsonDocument newDoc, string entityType, string id)
    {
        var oldEntity = FindEntity(oldDoc, entityType, id);
        var newEntity = FindEntity(newDoc, entityType, id);

        if (oldEntity == null || newEntity == null)
            return false;

        // Simple string comparison - could be enhanced with semantic diff
        return oldEntity.Value.GetRawText() != newEntity.Value.GetRawText();
    }

    private JsonElement? FindEntity(JsonDocument doc, string entityType, string id)
    {
        if (doc.RootElement.TryGetProperty(entityType, out var entitiesElement) &&
            entitiesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var entity in entitiesElement.EnumerateArray())
            {
                if (entity.TryGetProperty("id", out var idElement) &&
                    idElement.GetString() == id)
                {
                    return entity;
                }
            }
        }

        return null;
    }
}
