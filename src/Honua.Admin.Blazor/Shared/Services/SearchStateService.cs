// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json;
using Honua.Admin.Blazor.Shared.Models;
using Microsoft.JSInterop;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// Service for managing search state, filter presets, and search history.
/// </summary>
public sealed class SearchStateService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<SearchStateService> _logger;
    private const string PresetStorageKey = "honua.search.presets";
    private const string HistoryStorageKey = "honua.search.history";
    private const string AdvancedPresetStorageKey = "honua.search.advanced.presets";
    private const int MaxHistoryEntries = 50;

    private List<FilterPreset> _presets = new();
    private List<SearchHistoryEntry> _history = new();
    private Dictionary<string, List<AdvancedFilterPreset>> _advancedPresets = new();
    private bool _isInitialized = false;

    public event EventHandler? OnChange;

    public SearchStateService(IJSRuntime jsRuntime, ILogger<SearchStateService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    /// <summary>
    /// Current search filter.
    /// </summary>
    public SearchFilter CurrentFilter { get; private set; } = new();

    /// <summary>
    /// Gets all saved filter presets.
    /// </summary>
    public IReadOnlyList<FilterPreset> Presets => _presets.AsReadOnly();

    /// <summary>
    /// Gets recent search history.
    /// </summary>
    public IReadOnlyList<SearchHistoryEntry> History => _history.AsReadOnly();

    /// <summary>
    /// Initializes the service by loading presets and history from localStorage.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        try
        {
            await LoadPresetsAsync();
            await LoadHistoryAsync();
            await LoadAdvancedPresetsAsync();
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing search state");
        }
    }

    /// <summary>
    /// Gets advanced filter presets for a specific table type.
    /// </summary>
    public IReadOnlyList<AdvancedFilterPreset> GetAdvancedPresets(string tableType)
    {
        if (_advancedPresets.TryGetValue(tableType, out var presets))
        {
            return presets.AsReadOnly();
        }
        return new List<AdvancedFilterPreset>().AsReadOnly();
    }

    /// <summary>
    /// Saves an advanced filter preset.
    /// </summary>
    public async Task<AdvancedFilterPreset> SaveAdvancedPresetAsync(string tableType, string name, string? description, List<ColumnFilter> filters)
    {
        var preset = new AdvancedFilterPreset
        {
            Name = name,
            Description = description,
            TableType = tableType,
            Filters = filters.Select(f => new ColumnFilter
            {
                Column = f.Column,
                Operator = f.Operator,
                Value = f.Value,
                SecondValue = f.SecondValue,
                Values = new List<string>(f.Values)
            }).ToList()
        };

        if (!_advancedPresets.ContainsKey(tableType))
        {
            _advancedPresets[tableType] = new List<AdvancedFilterPreset>();
        }

        _advancedPresets[tableType].Add(preset);
        await SaveAdvancedPresetsAsync();
        NotifyStateChanged();

        return preset;
    }

    /// <summary>
    /// Deletes an advanced filter preset.
    /// </summary>
    public async Task DeleteAdvancedPresetAsync(string tableType, string presetId)
    {
        if (_advancedPresets.TryGetValue(tableType, out var presets))
        {
            presets.RemoveAll(p => p.Id == presetId);
            await SaveAdvancedPresetsAsync();
            NotifyStateChanged();
        }
    }

    /// <summary>
    /// Updates the current filter.
    /// </summary>
    public void UpdateFilter(SearchFilter filter)
    {
        CurrentFilter = filter;
        NotifyStateChanged();
    }

    /// <summary>
    /// Clears the current filter.
    /// </summary>
    public void ClearFilter()
    {
        CurrentFilter.Clear();
        NotifyStateChanged();
    }

    /// <summary>
    /// Saves the current filter as a preset.
    /// </summary>
    public async Task<FilterPreset> SavePresetAsync(string name, string? description = null)
    {
        var preset = new FilterPreset
        {
            Name = name,
            Description = description,
            Filter = CurrentFilter.Clone()
        };

        _presets.Add(preset);
        await SavePresetsAsync();
        NotifyStateChanged();

        return preset;
    }

    /// <summary>
    /// Loads a filter preset.
    /// </summary>
    public void LoadPreset(FilterPreset preset)
    {
        CurrentFilter = preset.Filter.Clone();
        preset.LastUsedAt = DateTime.UtcNow;
        _ = SavePresetsAsync(); // Fire and forget
        NotifyStateChanged();
    }

    /// <summary>
    /// Deletes a filter preset.
    /// </summary>
    public async Task DeletePresetAsync(string presetId)
    {
        _presets.RemoveAll(p => p.Id == presetId);
        await SavePresetsAsync();
        NotifyStateChanged();
    }

    /// <summary>
    /// Adds a search to the history.
    /// </summary>
    public async Task AddToHistoryAsync(string searchText, int resultCount)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return;

        // Remove duplicate if exists
        _history.RemoveAll(h => h.SearchText.Equals(searchText, StringComparison.OrdinalIgnoreCase));

        // Add to beginning
        _history.Insert(0, new SearchHistoryEntry
        {
            SearchText = searchText,
            ResultCount = resultCount
        });

        // Keep only recent entries
        if (_history.Count > MaxHistoryEntries)
        {
            _history = _history.Take(MaxHistoryEntries).ToList();
        }

        await SaveHistoryAsync();
        NotifyStateChanged();
    }

    /// <summary>
    /// Clears search history.
    /// </summary>
    public async Task ClearHistoryAsync()
    {
        _history.Clear();
        await SaveHistoryAsync();
        NotifyStateChanged();
    }

    private async Task LoadPresetsAsync()
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", PresetStorageKey);
            if (!string.IsNullOrEmpty(json))
            {
                _presets = JsonSerializer.Deserialize<List<FilterPreset>>(json) ?? new();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading filter presets from localStorage");
        }
    }

    private async Task SavePresetsAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_presets);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", PresetStorageKey, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error saving filter presets to localStorage");
        }
    }

    private async Task LoadHistoryAsync()
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", HistoryStorageKey);
            if (!string.IsNullOrEmpty(json))
            {
                _history = JsonSerializer.Deserialize<List<SearchHistoryEntry>>(json) ?? new();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading search history from localStorage");
        }
    }

    private async Task SaveHistoryAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_history);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", HistoryStorageKey, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error saving search history to localStorage");
        }
    }

    private async Task LoadAdvancedPresetsAsync()
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", AdvancedPresetStorageKey);
            if (!string.IsNullOrEmpty(json))
            {
                _advancedPresets = JsonSerializer.Deserialize<Dictionary<string, List<AdvancedFilterPreset>>>(json)
                    ?? new Dictionary<string, List<AdvancedFilterPreset>>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading advanced filter presets from localStorage");
        }
    }

    private async Task SaveAdvancedPresetsAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_advancedPresets);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", AdvancedPresetStorageKey, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error saving advanced filter presets to localStorage");
        }
    }

    private void NotifyStateChanged() => OnChange?.Invoke(this, EventArgs.Empty);
}
