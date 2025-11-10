// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.MapSDK.Models;
using Honua.MapSDK.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Honua.MapSDK.Components.Controls;

public partial class LayerControl : ComponentBase, IDisposable
{
    [Inject]
    private ILayerManager LayerManager { get; set; } = default!;

    // ===== Parameters =====

    [Parameter]
    public string Title { get; set; } = "Layers";

    [Parameter]
    public string? CssClass { get; set; }

    [Parameter]
    public string Style { get; set; } = string.Empty;

    [Parameter]
    public bool ShowSearch { get; set; } = true;

    [Parameter]
    public bool ShowToolbar { get; set; } = true;

    [Parameter]
    public bool ShowOpacitySlider { get; set; } = true;

    [Parameter]
    public bool ShowLegend { get; set; } = true;

    [Parameter]
    public bool ShowActions { get; set; } = true;

    [Parameter]
    public bool AllowRemove { get; set; } = true;

    [Parameter]
    public bool EnableDragDrop { get; set; } = true;

    [Parameter]
    public EventCallback<string> OnLayerZoomRequested { get; set; }

    [Parameter]
    public EventCallback<string> OnLayerInfoRequested { get; set; }

    [Parameter]
    public EventCallback<string> OnLayerRemoved { get; set; }

    // ===== Private Fields =====

    private string _searchQuery = string.Empty;
    private string? _draggedLayerId;
    private List<LayerGroup> _filteredGroups = new();
    private List<LayerDefinition> _ungroupedLayers = new();

    // ===== Lifecycle Methods =====

    protected override void OnInitialized()
    {
        // Subscribe to layer manager events
        LayerManager.OnLayerAdded += OnLayerChanged;
        LayerManager.OnLayerRemoved += OnLayerChanged;
        LayerManager.OnLayerVisibilityChanged += OnLayerVisibilityChanged;
        LayerManager.OnLayerOpacityChanged += OnLayerOpacityChanged;
        LayerManager.OnLayersReordered += OnLayersReordered;
        LayerManager.OnGroupAdded += OnGroupChanged;
        LayerManager.OnGroupRemoved += OnGroupChanged;
        LayerManager.OnGroupVisibilityChanged += OnGroupVisibilityChanged;

        RefreshLayers();
    }

    protected override void OnParametersSet()
    {
        RefreshLayers();
    }

    public void Dispose()
    {
        // Unsubscribe from events
        LayerManager.OnLayerAdded -= OnLayerChanged;
        LayerManager.OnLayerRemoved -= OnLayerChanged;
        LayerManager.OnLayerVisibilityChanged -= OnLayerVisibilityChanged;
        LayerManager.OnLayerOpacityChanged -= OnLayerOpacityChanged;
        LayerManager.OnLayersReordered -= OnLayersReordered;
        LayerManager.OnGroupAdded -= OnGroupChanged;
        LayerManager.OnGroupRemoved -= OnGroupChanged;
        LayerManager.OnGroupVisibilityChanged -= OnGroupVisibilityChanged;
    }

    // ===== Event Handlers =====

    private void OnLayerChanged(object? sender, LayerEventArgs e)
    {
        RefreshLayers();
        InvokeAsync(StateHasChanged);
    }

    private void OnLayerVisibilityChanged(object? sender, LayerVisibilityEventArgs e)
    {
        InvokeAsync(StateHasChanged);
    }

    private void OnLayerOpacityChanged(object? sender, LayerOpacityEventArgs e)
    {
        InvokeAsync(StateHasChanged);
    }

    private void OnLayersReordered(object? sender, LayerReorderedEventArgs e)
    {
        RefreshLayers();
        InvokeAsync(StateHasChanged);
    }

    private void OnGroupChanged(object? sender, GroupEventArgs e)
    {
        RefreshLayers();
        InvokeAsync(StateHasChanged);
    }

    private void OnGroupVisibilityChanged(object? sender, GroupVisibilityEventArgs e)
    {
        InvokeAsync(StateHasChanged);
    }

    // ===== Layer Management =====

    private void RefreshLayers()
    {
        _filteredGroups = LayerManager.Groups.ToList();
        _ungroupedLayers = LayerManager.Layers
            .Where(l => string.IsNullOrWhiteSpace(l.GroupId))
            .ToList();
    }

    private IEnumerable<LayerDefinition> GetGroupLayers(string groupId)
    {
        return LayerManager.GetLayersByGroup(groupId);
    }

    private bool MatchesSearch(LayerDefinition layer)
    {
        if (string.IsNullOrWhiteSpace(_searchQuery))
            return true;

        var query = _searchQuery.ToLower();
        return layer.Name.ToLower().Contains(query) ||
               (layer.Description?.ToLower().Contains(query) ?? false);
    }

    private bool HasVisibleLayers()
    {
        return _ungroupedLayers.Any(MatchesSearch) ||
               _filteredGroups.Any(g => GetGroupLayers(g.Id).Any(MatchesSearch));
    }

    // ===== Layer Actions =====

    private void ToggleLayerVisibility(string layerId, ChangeEventArgs e)
    {
        if (e.Value is bool visible)
        {
            LayerManager.SetLayerVisibility(layerId, visible);
        }
    }

    private void SetLayerOpacity(string layerId, ChangeEventArgs e)
    {
        if (e.Value is string value && int.TryParse(value, out var opacity))
        {
            LayerManager.SetLayerOpacity(layerId, opacity / 100.0);
        }
    }

    private async Task ZoomToLayer(string layerId)
    {
        await OnLayerZoomRequested.InvokeAsync(layerId);
    }

    private async Task ShowLayerInfo(string layerId)
    {
        await OnLayerInfoRequested.InvokeAsync(layerId);
    }

    private async Task RemoveLayer(string layerId)
    {
        LayerManager.RemoveLayer(layerId);
        await OnLayerRemoved.InvokeAsync(layerId);
    }

    // ===== Group Actions =====

    private void ToggleGroup(string groupId)
    {
        LayerManager.ToggleGroupExpanded(groupId);
    }

    private void ToggleGroupVisibility(string groupId, ChangeEventArgs e)
    {
        if (e.Value is bool visible)
        {
            LayerManager.SetGroupVisibility(groupId, visible);
        }
    }

    private void SetGroupOpacity(string groupId, ChangeEventArgs e)
    {
        if (e.Value is string value && int.TryParse(value, out var opacity))
        {
            LayerManager.SetGroupOpacity(groupId, opacity / 100.0);
        }
    }

    // ===== Toolbar Actions =====

    private void ShowAllLayers()
    {
        LayerManager.ShowAllLayers();
    }

    private void HideAllLayers()
    {
        LayerManager.HideAllLayers();
    }

    private void ExpandAllGroups()
    {
        foreach (var group in LayerManager.Groups)
        {
            var groupInstance = LayerManager.GetGroup(group.Id);
            if (groupInstance != null)
            {
                groupInstance.IsExpanded = true;
            }
        }
        StateHasChanged();
    }

    private void CollapseAllGroups()
    {
        foreach (var group in LayerManager.Groups)
        {
            var groupInstance = LayerManager.GetGroup(group.Id);
            if (groupInstance != null)
            {
                groupInstance.IsExpanded = false;
            }
        }
        StateHasChanged();
    }

    // ===== Drag and Drop =====

    private void OnDragStart(DragEventArgs e, string layerId)
    {
        if (!EnableDragDrop) return;
        _draggedLayerId = layerId;
    }

    private void OnDrop(DragEventArgs e, string targetLayerId)
    {
        if (!EnableDragDrop || _draggedLayerId == null) return;

        if (_draggedLayerId != targetLayerId)
        {
            LayerManager.MoveLayerBefore(_draggedLayerId, targetLayerId);
        }

        _draggedLayerId = null;
    }

    private void OnDragOver(DragEventArgs e)
    {
        if (!EnableDragDrop) return;
        // Note: PreventDefault is handled via @ondragover:preventDefault="true" in razor
    }

    // ===== Public API =====

    /// <summary>
    /// Refresh the layer list display
    /// </summary>
    public void Refresh()
    {
        RefreshLayers();
        StateHasChanged();
    }

    /// <summary>
    /// Set the search query programmatically
    /// </summary>
    public void SetSearchQuery(string query)
    {
        _searchQuery = query ?? string.Empty;
        StateHasChanged();
    }

    /// <summary>
    /// Clear the search query
    /// </summary>
    public void ClearSearch()
    {
        _searchQuery = string.Empty;
        StateHasChanged();
    }

    /// <summary>
    /// Expand a specific group
    /// </summary>
    public void ExpandGroup(string groupId)
    {
        var group = LayerManager.GetGroup(groupId);
        if (group != null)
        {
            group.IsExpanded = true;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Collapse a specific group
    /// </summary>
    public void CollapseGroup(string groupId)
    {
        var group = LayerManager.GetGroup(groupId);
        if (group != null)
        {
            group.IsExpanded = false;
            StateHasChanged();
        }
    }
}
