// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json;
using Microsoft.JSInterop;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// Service for managing draft data in browser localStorage with auto-save functionality
/// </summary>
public class DraftStorageService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<DraftStorageService> _logger;

    public DraftStorageService(IJSRuntime jsRuntime, ILogger<DraftStorageService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    /// <summary>
    /// Saves draft data to localStorage
    /// </summary>
    public async Task SaveDraftAsync<T>(string key, T data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data);
            var draft = new Draft
            {
                Key = key,
                Data = json,
                SavedAt = DateTime.UtcNow
            };

            var draftJson = JsonSerializer.Serialize(draft);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", $"draft_{key}", draftJson);

            _logger.LogDebug("Draft saved successfully for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving draft for {Key}", key);
        }
    }

    /// <summary>
    /// Loads draft data from localStorage
    /// </summary>
    public async Task<T?> LoadDraftAsync<T>(string key)
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", $"draft_{key}");
            if (string.IsNullOrEmpty(json))
            {
                _logger.LogDebug("No draft found for key: {Key}", key);
                return default;
            }

            var draft = JsonSerializer.Deserialize<Draft>(json);
            if (draft == null)
            {
                _logger.LogWarning("Failed to deserialize draft for key: {Key}", key);
                return default;
            }

            // Don't restore drafts older than 7 days
            if ((DateTime.UtcNow - draft.SavedAt).TotalDays > 7)
            {
                _logger.LogInformation("Draft expired for key: {Key}, saved at: {SavedAt}", key, draft.SavedAt);
                await DeleteDraftAsync(key);
                return default;
            }

            var data = JsonSerializer.Deserialize<T>(draft.Data);
            _logger.LogDebug("Draft loaded successfully for key: {Key}", key);
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading draft for {Key}", key);
            return default;
        }
    }

    /// <summary>
    /// Deletes a draft from localStorage
    /// </summary>
    public async Task DeleteDraftAsync(string key)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", $"draft_{key}");
            _logger.LogDebug("Draft deleted for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting draft for {Key}", key);
        }
    }

    /// <summary>
    /// Gets all drafts from localStorage
    /// </summary>
    public async Task<List<Draft>> GetAllDraftsAsync()
    {
        var drafts = new List<Draft>();

        try
        {
            // Get all localStorage keys
            var keysJson = await _jsRuntime.InvokeAsync<string>("eval", "JSON.stringify(Object.keys(localStorage))");
            var keys = JsonSerializer.Deserialize<string[]>(keysJson) ?? Array.Empty<string>();

            foreach (var key in keys.Where(k => k.StartsWith("draft_")))
            {
                var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);
                if (!string.IsNullOrEmpty(json))
                {
                    var draft = JsonSerializer.Deserialize<Draft>(json);
                    if (draft != null)
                    {
                        drafts.Add(draft);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all drafts");
        }

        return drafts;
    }

    /// <summary>
    /// Cleans up expired drafts (older than 7 days)
    /// </summary>
    public async Task CleanupExpiredDraftsAsync()
    {
        try
        {
            var allDrafts = await GetAllDraftsAsync();
            var expiredDrafts = allDrafts.Where(d => (DateTime.UtcNow - d.SavedAt).TotalDays > 7).ToList();

            foreach (var draft in expiredDrafts)
            {
                await DeleteDraftAsync(draft.Key);
            }

            if (expiredDrafts.Any())
            {
                _logger.LogInformation("Cleaned up {Count} expired drafts", expiredDrafts.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired drafts");
        }
    }
}

/// <summary>
/// Represents a draft stored in localStorage
/// </summary>
public class Draft
{
    public string Key { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public DateTime SavedAt { get; set; }
}
