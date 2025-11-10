// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.MapSDK.Models;

namespace Honua.MapSDK.Services;

/// <summary>
/// Service for managing map layers and layer groups
/// Provides centralized layer control with events and state management
/// </summary>
public interface ILayerManager
{
    /// <summary>
    /// All registered layers
    /// </summary>
    IReadOnlyList<LayerDefinition> Layers { get; }

    /// <summary>
    /// All registered layer groups
    /// </summary>
    IReadOnlyList<LayerGroup> Groups { get; }

    /// <summary>
    /// All registered layer sources
    /// </summary>
    IReadOnlyDictionary<string, LayerSource> Sources { get; }

    // ===== Source Management =====

    /// <summary>
    /// Register a layer source (must be done before adding layers)
    /// </summary>
    void AddSource(LayerSource source);

    /// <summary>
    /// Remove a layer source (removes all layers using this source)
    /// </summary>
    void RemoveSource(string sourceId);

    /// <summary>
    /// Get a layer source by ID
    /// </summary>
    LayerSource? GetSource(string sourceId);

    // ===== Layer Management =====

    /// <summary>
    /// Add a layer to the map
    /// </summary>
    void AddLayer(LayerDefinition layer, string? beforeId = null);

    /// <summary>
    /// Remove a layer from the map
    /// </summary>
    void RemoveLayer(string layerId);

    /// <summary>
    /// Get a layer by ID
    /// </summary>
    LayerDefinition? GetLayer(string layerId);

    /// <summary>
    /// Get all layers in a group
    /// </summary>
    IEnumerable<LayerDefinition> GetLayersByGroup(string groupId);

    /// <summary>
    /// Update layer properties
    /// </summary>
    void UpdateLayer(string layerId, Action<LayerDefinition> update);

    /// <summary>
    /// Check if layer exists
    /// </summary>
    bool HasLayer(string layerId);

    // ===== Layer Visibility =====

    /// <summary>
    /// Toggle layer visibility
    /// </summary>
    void ToggleLayerVisibility(string layerId);

    /// <summary>
    /// Set layer visibility
    /// </summary>
    void SetLayerVisibility(string layerId, bool visible);

    /// <summary>
    /// Show layer
    /// </summary>
    void ShowLayer(string layerId);

    /// <summary>
    /// Hide layer
    /// </summary>
    void HideLayer(string layerId);

    // ===== Layer Opacity =====

    /// <summary>
    /// Set layer opacity (0-1)
    /// </summary>
    void SetLayerOpacity(string layerId, double opacity);

    /// <summary>
    /// Get layer opacity
    /// </summary>
    double GetLayerOpacity(string layerId);

    // ===== Layer Ordering =====

    /// <summary>
    /// Reorder layers by ID list (bottom to top)
    /// </summary>
    void ReorderLayers(string[] layerIds);

    /// <summary>
    /// Move layer before another layer
    /// </summary>
    void MoveLayerBefore(string layerId, string beforeLayerId);

    /// <summary>
    /// Move layer to top
    /// </summary>
    void MoveLayerToTop(string layerId);

    /// <summary>
    /// Move layer to bottom
    /// </summary>
    void MoveLayerToBottom(string layerId);

    /// <summary>
    /// Move layer up one position
    /// </summary>
    void MoveLayerUp(string layerId);

    /// <summary>
    /// Move layer down one position
    /// </summary>
    void MoveLayerDown(string layerId);

    // ===== Layer Groups =====

    /// <summary>
    /// Add a layer group
    /// </summary>
    void AddGroup(LayerGroup group);

    /// <summary>
    /// Remove a layer group (does not remove layers)
    /// </summary>
    void RemoveGroup(string groupId);

    /// <summary>
    /// Get a layer group by ID
    /// </summary>
    LayerGroup? GetGroup(string groupId);

    /// <summary>
    /// Set group visibility (affects all layers in group)
    /// </summary>
    void SetGroupVisibility(string groupId, bool visible);

    /// <summary>
    /// Set group opacity (affects all layers in group)
    /// </summary>
    void SetGroupOpacity(string groupId, double opacity);

    /// <summary>
    /// Toggle group expanded state
    /// </summary>
    void ToggleGroupExpanded(string groupId);

    /// <summary>
    /// Add layer to group
    /// </summary>
    void AddLayerToGroup(string layerId, string groupId);

    /// <summary>
    /// Remove layer from group
    /// </summary>
    void RemoveLayerFromGroup(string layerId, string groupId);

    // ===== Bulk Operations =====

    /// <summary>
    /// Show all layers
    /// </summary>
    void ShowAllLayers();

    /// <summary>
    /// Hide all layers
    /// </summary>
    void HideAllLayers();

    /// <summary>
    /// Remove all layers
    /// </summary>
    void ClearLayers();

    /// <summary>
    /// Remove all layers and sources
    /// </summary>
    void Clear();

    // ===== State Management =====

    /// <summary>
    /// Export layer state as JSON
    /// </summary>
    string ExportState();

    /// <summary>
    /// Import layer state from JSON
    /// </summary>
    void ImportState(string json);

    /// <summary>
    /// Get layer state snapshot
    /// </summary>
    LayerManagerState GetState();

    /// <summary>
    /// Restore layer state from snapshot
    /// </summary>
    void RestoreState(LayerManagerState state);

    // ===== Events =====

    /// <summary>
    /// Fired when a layer is added
    /// </summary>
    event EventHandler<LayerEventArgs>? OnLayerAdded;

    /// <summary>
    /// Fired when a layer is removed
    /// </summary>
    event EventHandler<LayerEventArgs>? OnLayerRemoved;

    /// <summary>
    /// Fired when layer visibility changes
    /// </summary>
    event EventHandler<LayerVisibilityEventArgs>? OnLayerVisibilityChanged;

    /// <summary>
    /// Fired when layer opacity changes
    /// </summary>
    event EventHandler<LayerOpacityEventArgs>? OnLayerOpacityChanged;

    /// <summary>
    /// Fired when layers are reordered
    /// </summary>
    event EventHandler<LayerReorderedEventArgs>? OnLayersReordered;

    /// <summary>
    /// Fired when a layer is updated
    /// </summary>
    event EventHandler<LayerEventArgs>? OnLayerUpdated;

    /// <summary>
    /// Fired when a source is added
    /// </summary>
    event EventHandler<SourceEventArgs>? OnSourceAdded;

    /// <summary>
    /// Fired when a source is removed
    /// </summary>
    event EventHandler<SourceEventArgs>? OnSourceRemoved;

    /// <summary>
    /// Fired when a group is added
    /// </summary>
    event EventHandler<GroupEventArgs>? OnGroupAdded;

    /// <summary>
    /// Fired when a group is removed
    /// </summary>
    event EventHandler<GroupEventArgs>? OnGroupRemoved;

    /// <summary>
    /// Fired when group visibility changes
    /// </summary>
    event EventHandler<GroupVisibilityEventArgs>? OnGroupVisibilityChanged;
}

// ===== Event Args Classes =====

public class LayerEventArgs : EventArgs
{
    public required string LayerId { get; init; }
    public required LayerDefinition Layer { get; init; }
}

public class LayerVisibilityEventArgs : EventArgs
{
    public required string LayerId { get; init; }
    public required bool Visible { get; init; }
}

public class LayerOpacityEventArgs : EventArgs
{
    public required string LayerId { get; init; }
    public required double Opacity { get; init; }
}

public class LayerReorderedEventArgs : EventArgs
{
    public required string[] LayerIds { get; init; }
}

public class SourceEventArgs : EventArgs
{
    public required string SourceId { get; init; }
    public required LayerSource Source { get; init; }
}

public class GroupEventArgs : EventArgs
{
    public required string GroupId { get; init; }
    public required LayerGroup Group { get; init; }
}

public class GroupVisibilityEventArgs : EventArgs
{
    public required string GroupId { get; init; }
    public required bool Visible { get; init; }
}

// ===== State Management Classes =====

/// <summary>
/// Snapshot of layer manager state for save/restore
/// </summary>
public class LayerManagerState
{
    public List<LayerSource> Sources { get; set; } = new();
    public List<LayerDefinition> Layers { get; set; } = new();
    public List<LayerGroup> Groups { get; set; } = new();
    public Dictionary<string, bool> LayerVisibility { get; set; } = new();
    public Dictionary<string, double> LayerOpacity { get; set; } = new();
    public string[] LayerOrder { get; set; } = Array.Empty<string>();
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}
