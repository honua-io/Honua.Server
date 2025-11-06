using System.Text.Json;
using Honua.MapSDK.Core;
using Honua.MapSDK.Core.Messages;
using Honua.MapSDK.Models;
using Microsoft.Extensions.Logging;

namespace Honua.MapSDK.Services;

/// <summary>
/// Default implementation of ILayerManager
/// Manages layers, sources, and groups with event notifications via ComponentBus
/// </summary>
public class LayerManager : ILayerManager
{
    private readonly List<LayerDefinition> _layers = new();
    private readonly Dictionary<string, LayerSource> _sources = new();
    private readonly List<LayerGroup> _groups = new();
    private readonly ComponentBus _bus;
    private readonly ILogger<LayerManager>? _logger;

    public IReadOnlyList<LayerDefinition> Layers => _layers.AsReadOnly();
    public IReadOnlyList<LayerGroup> Groups => _groups.AsReadOnly();
    public IReadOnlyDictionary<string, LayerSource> Sources => _sources;

    // Events
    public event EventHandler<LayerEventArgs>? OnLayerAdded;
    public event EventHandler<LayerEventArgs>? OnLayerRemoved;
    public event EventHandler<LayerVisibilityEventArgs>? OnLayerVisibilityChanged;
    public event EventHandler<LayerOpacityEventArgs>? OnLayerOpacityChanged;
    public event EventHandler<LayerReorderedEventArgs>? OnLayersReordered;
    public event EventHandler<LayerEventArgs>? OnLayerUpdated;
    public event EventHandler<SourceEventArgs>? OnSourceAdded;
    public event EventHandler<SourceEventArgs>? OnSourceRemoved;
    public event EventHandler<GroupEventArgs>? OnGroupAdded;
    public event EventHandler<GroupEventArgs>? OnGroupRemoved;
    public event EventHandler<GroupVisibilityEventArgs>? OnGroupVisibilityChanged;

    public LayerManager(ComponentBus bus, ILogger<LayerManager>? logger = null)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _logger = logger;
    }

    // ===== Source Management =====

    public void AddSource(LayerSource source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrWhiteSpace(source.Id)) throw new ArgumentException("Source ID is required", nameof(source));

        if (_sources.ContainsKey(source.Id))
        {
            _logger?.LogWarning("Source {SourceId} already exists, replacing", source.Id);
            _sources[source.Id] = source;
        }
        else
        {
            _sources[source.Id] = source;
            _logger?.LogDebug("Added source {SourceId} of type {Type}", source.Id, source.Type);
        }

        OnSourceAdded?.Invoke(this, new SourceEventArgs { SourceId = source.Id, Source = source });
    }

    public void RemoveSource(string sourceId)
    {
        if (!_sources.ContainsKey(sourceId))
        {
            _logger?.LogWarning("Source {SourceId} not found", sourceId);
            return;
        }

        var source = _sources[sourceId];

        // Remove all layers using this source
        var layersToRemove = _layers.Where(l => l.SourceId == sourceId).ToList();
        foreach (var layer in layersToRemove)
        {
            RemoveLayer(layer.Id);
        }

        _sources.Remove(sourceId);
        _logger?.LogDebug("Removed source {SourceId}", sourceId);

        OnSourceRemoved?.Invoke(this, new SourceEventArgs { SourceId = sourceId, Source = source });
    }

    public LayerSource? GetSource(string sourceId)
    {
        return _sources.TryGetValue(sourceId, out var source) ? source : null;
    }

    // ===== Layer Management =====

    public void AddLayer(LayerDefinition layer, string? beforeId = null)
    {
        if (layer == null) throw new ArgumentNullException(nameof(layer));
        if (string.IsNullOrWhiteSpace(layer.Id)) throw new ArgumentException("Layer ID is required", nameof(layer));
        if (string.IsNullOrWhiteSpace(layer.Name)) throw new ArgumentException("Layer name is required", nameof(layer));

        if (_layers.Any(l => l.Id == layer.Id))
        {
            _logger?.LogWarning("Layer {LayerId} already exists", layer.Id);
            return;
        }

        // Validate source exists
        if (!_sources.ContainsKey(layer.SourceId))
        {
            _logger?.LogWarning("Source {SourceId} not found for layer {LayerId}", layer.SourceId, layer.Id);
            throw new InvalidOperationException($"Source {layer.SourceId} must be added before layer {layer.Id}");
        }

        if (beforeId != null)
        {
            var beforeIndex = _layers.FindIndex(l => l.Id == beforeId);
            if (beforeIndex >= 0)
            {
                _layers.Insert(beforeIndex, layer);
            }
            else
            {
                _layers.Add(layer);
            }
        }
        else
        {
            _layers.Add(layer);
        }

        _logger?.LogDebug("Added layer {LayerId} ({LayerName})", layer.Id, layer.Name);

        // Fire events
        OnLayerAdded?.Invoke(this, new LayerEventArgs { LayerId = layer.Id, Layer = layer });

        // Publish to ComponentBus
        _bus.Publish(new LayerAddedMessage
        {
            LayerId = layer.Id,
            LayerName = layer.Name
        });
    }

    public void RemoveLayer(string layerId)
    {
        var layer = _layers.FirstOrDefault(l => l.Id == layerId);
        if (layer == null)
        {
            _logger?.LogWarning("Layer {LayerId} not found", layerId);
            return;
        }

        _layers.Remove(layer);

        // Remove from all groups
        foreach (var group in _groups)
        {
            group.LayerIds.Remove(layerId);
        }

        _logger?.LogDebug("Removed layer {LayerId}", layerId);

        // Fire events
        OnLayerRemoved?.Invoke(this, new LayerEventArgs { LayerId = layerId, Layer = layer });

        // Publish to ComponentBus
        _bus.Publish(new LayerRemovedMessage { LayerId = layerId });
    }

    public LayerDefinition? GetLayer(string layerId)
    {
        return _layers.FirstOrDefault(l => l.Id == layerId);
    }

    public IEnumerable<LayerDefinition> GetLayersByGroup(string groupId)
    {
        var group = _groups.FirstOrDefault(g => g.Id == groupId);
        if (group == null) return Enumerable.Empty<LayerDefinition>();

        return _layers.Where(l => group.LayerIds.Contains(l.Id));
    }

    public void UpdateLayer(string layerId, Action<LayerDefinition> update)
    {
        var layer = GetLayer(layerId);
        if (layer == null)
        {
            _logger?.LogWarning("Layer {LayerId} not found", layerId);
            return;
        }

        update(layer);
        layer.UpdatedAt = DateTime.UtcNow;

        _logger?.LogDebug("Updated layer {LayerId}", layerId);

        OnLayerUpdated?.Invoke(this, new LayerEventArgs { LayerId = layerId, Layer = layer });
    }

    public bool HasLayer(string layerId)
    {
        return _layers.Any(l => l.Id == layerId);
    }

    // ===== Layer Visibility =====

    public void ToggleLayerVisibility(string layerId)
    {
        var layer = GetLayer(layerId);
        if (layer == null) return;

        SetLayerVisibility(layerId, !layer.Visible);
    }

    public void SetLayerVisibility(string layerId, bool visible)
    {
        var layer = GetLayer(layerId);
        if (layer == null)
        {
            _logger?.LogWarning("Layer {LayerId} not found", layerId);
            return;
        }

        if (layer.Visible == visible) return;

        layer.Visible = visible;
        layer.UpdatedAt = DateTime.UtcNow;

        _logger?.LogDebug("Set layer {LayerId} visibility to {Visible}", layerId, visible);

        // Fire events
        OnLayerVisibilityChanged?.Invoke(this, new LayerVisibilityEventArgs
        {
            LayerId = layerId,
            Visible = visible
        });

        // Publish to ComponentBus
        _bus.Publish(new LayerVisibilityChangedMessage
        {
            LayerId = layerId,
            Visible = visible
        });
    }

    public void ShowLayer(string layerId) => SetLayerVisibility(layerId, true);

    public void HideLayer(string layerId) => SetLayerVisibility(layerId, false);

    // ===== Layer Opacity =====

    public void SetLayerOpacity(string layerId, double opacity)
    {
        if (opacity < 0 || opacity > 1)
            throw new ArgumentOutOfRangeException(nameof(opacity), "Opacity must be between 0 and 1");

        var layer = GetLayer(layerId);
        if (layer == null)
        {
            _logger?.LogWarning("Layer {LayerId} not found", layerId);
            return;
        }

        if (Math.Abs(layer.Opacity - opacity) < 0.001) return;

        layer.Opacity = opacity;
        layer.UpdatedAt = DateTime.UtcNow;

        _logger?.LogDebug("Set layer {LayerId} opacity to {Opacity}", layerId, opacity);

        // Fire events
        OnLayerOpacityChanged?.Invoke(this, new LayerOpacityEventArgs
        {
            LayerId = layerId,
            Opacity = opacity
        });

        // Publish to ComponentBus
        _bus.Publish(new LayerOpacityChangedMessage
        {
            LayerId = layerId,
            Opacity = opacity
        });
    }

    public double GetLayerOpacity(string layerId)
    {
        var layer = GetLayer(layerId);
        return layer?.Opacity ?? 1.0;
    }

    // ===== Layer Ordering =====

    public void ReorderLayers(string[] layerIds)
    {
        if (layerIds == null || layerIds.Length == 0) return;

        var newOrder = new List<LayerDefinition>();

        // Add layers in specified order
        foreach (var layerId in layerIds)
        {
            var layer = GetLayer(layerId);
            if (layer != null)
            {
                newOrder.Add(layer);
            }
        }

        // Add any layers not in the list at the end
        var remainingLayers = _layers.Where(l => !layerIds.Contains(l.Id));
        newOrder.AddRange(remainingLayers);

        _layers.Clear();
        _layers.AddRange(newOrder);

        _logger?.LogDebug("Reordered layers");

        OnLayersReordered?.Invoke(this, new LayerReorderedEventArgs { LayerIds = layerIds });
    }

    public void MoveLayerBefore(string layerId, string beforeLayerId)
    {
        var layer = GetLayer(layerId);
        var beforeLayer = GetLayer(beforeLayerId);

        if (layer == null || beforeLayer == null) return;

        _layers.Remove(layer);
        var beforeIndex = _layers.IndexOf(beforeLayer);
        _layers.Insert(beforeIndex, layer);

        _logger?.LogDebug("Moved layer {LayerId} before {BeforeLayerId}", layerId, beforeLayerId);

        OnLayersReordered?.Invoke(this, new LayerReorderedEventArgs
        {
            LayerIds = _layers.Select(l => l.Id).ToArray()
        });
    }

    public void MoveLayerToTop(string layerId)
    {
        var layer = GetLayer(layerId);
        if (layer == null) return;

        _layers.Remove(layer);
        _layers.Add(layer);

        _logger?.LogDebug("Moved layer {LayerId} to top", layerId);

        OnLayersReordered?.Invoke(this, new LayerReorderedEventArgs
        {
            LayerIds = _layers.Select(l => l.Id).ToArray()
        });
    }

    public void MoveLayerToBottom(string layerId)
    {
        var layer = GetLayer(layerId);
        if (layer == null) return;

        _layers.Remove(layer);
        _layers.Insert(0, layer);

        _logger?.LogDebug("Moved layer {LayerId} to bottom", layerId);

        OnLayersReordered?.Invoke(this, new LayerReorderedEventArgs
        {
            LayerIds = _layers.Select(l => l.Id).ToArray()
        });
    }

    public void MoveLayerUp(string layerId)
    {
        var index = _layers.FindIndex(l => l.Id == layerId);
        if (index < 0 || index >= _layers.Count - 1) return;

        var layer = _layers[index];
        _layers.RemoveAt(index);
        _layers.Insert(index + 1, layer);

        _logger?.LogDebug("Moved layer {LayerId} up", layerId);

        OnLayersReordered?.Invoke(this, new LayerReorderedEventArgs
        {
            LayerIds = _layers.Select(l => l.Id).ToArray()
        });
    }

    public void MoveLayerDown(string layerId)
    {
        var index = _layers.FindIndex(l => l.Id == layerId);
        if (index <= 0) return;

        var layer = _layers[index];
        _layers.RemoveAt(index);
        _layers.Insert(index - 1, layer);

        _logger?.LogDebug("Moved layer {LayerId} down", layerId);

        OnLayersReordered?.Invoke(this, new LayerReorderedEventArgs
        {
            LayerIds = _layers.Select(l => l.Id).ToArray()
        });
    }

    // ===== Layer Groups =====

    public void AddGroup(LayerGroup group)
    {
        if (group == null) throw new ArgumentNullException(nameof(group));
        if (string.IsNullOrWhiteSpace(group.Id)) throw new ArgumentException("Group ID is required", nameof(group));

        if (_groups.Any(g => g.Id == group.Id))
        {
            _logger?.LogWarning("Group {GroupId} already exists", group.Id);
            return;
        }

        _groups.Add(group);
        _logger?.LogDebug("Added group {GroupId} ({GroupName})", group.Id, group.Name);

        OnGroupAdded?.Invoke(this, new GroupEventArgs { GroupId = group.Id, Group = group });
    }

    public void RemoveGroup(string groupId)
    {
        var group = _groups.FirstOrDefault(g => g.Id == groupId);
        if (group == null)
        {
            _logger?.LogWarning("Group {GroupId} not found", groupId);
            return;
        }

        _groups.Remove(group);
        _logger?.LogDebug("Removed group {GroupId}", groupId);

        OnGroupRemoved?.Invoke(this, new GroupEventArgs { GroupId = groupId, Group = group });
    }

    public LayerGroup? GetGroup(string groupId)
    {
        return _groups.FirstOrDefault(g => g.Id == groupId);
    }

    public void SetGroupVisibility(string groupId, bool visible)
    {
        var group = GetGroup(groupId);
        if (group == null)
        {
            _logger?.LogWarning("Group {GroupId} not found", groupId);
            return;
        }

        group.Visible = visible;

        // Update all layers in group
        foreach (var layerId in group.LayerIds)
        {
            SetLayerVisibility(layerId, visible);
        }

        _logger?.LogDebug("Set group {GroupId} visibility to {Visible}", groupId, visible);

        OnGroupVisibilityChanged?.Invoke(this, new GroupVisibilityEventArgs
        {
            GroupId = groupId,
            Visible = visible
        });
    }

    public void SetGroupOpacity(string groupId, double opacity)
    {
        if (opacity < 0 || opacity > 1)
            throw new ArgumentOutOfRangeException(nameof(opacity), "Opacity must be between 0 and 1");

        var group = GetGroup(groupId);
        if (group == null)
        {
            _logger?.LogWarning("Group {GroupId} not found", groupId);
            return;
        }

        group.Opacity = opacity;

        // Update all layers in group
        foreach (var layerId in group.LayerIds)
        {
            SetLayerOpacity(layerId, opacity);
        }

        _logger?.LogDebug("Set group {GroupId} opacity to {Opacity}", groupId, opacity);
    }

    public void ToggleGroupExpanded(string groupId)
    {
        var group = GetGroup(groupId);
        if (group == null)
        {
            _logger?.LogWarning("Group {GroupId} not found", groupId);
            return;
        }

        group.Expanded = !group.Expanded;
        _logger?.LogDebug("Toggled group {GroupId} expanded to {Expanded}", groupId, group.Expanded);
    }

    public void AddLayerToGroup(string layerId, string groupId)
    {
        var layer = GetLayer(layerId);
        var group = GetGroup(groupId);

        if (layer == null || group == null) return;

        if (!group.LayerIds.Contains(layerId))
        {
            group.LayerIds.Add(layerId);
            layer.GroupId = groupId;
            _logger?.LogDebug("Added layer {LayerId} to group {GroupId}", layerId, groupId);
        }
    }

    public void RemoveLayerFromGroup(string layerId, string groupId)
    {
        var layer = GetLayer(layerId);
        var group = GetGroup(groupId);

        if (layer == null || group == null) return;

        group.LayerIds.Remove(layerId);
        layer.GroupId = null;
        _logger?.LogDebug("Removed layer {LayerId} from group {GroupId}", layerId, groupId);
    }

    // ===== Bulk Operations =====

    public void ShowAllLayers()
    {
        foreach (var layer in _layers)
        {
            SetLayerVisibility(layer.Id, true);
        }
        _logger?.LogDebug("Showed all layers");
    }

    public void HideAllLayers()
    {
        foreach (var layer in _layers)
        {
            SetLayerVisibility(layer.Id, false);
        }
        _logger?.LogDebug("Hid all layers");
    }

    public void ClearLayers()
    {
        var layerIds = _layers.Select(l => l.Id).ToList();
        foreach (var layerId in layerIds)
        {
            RemoveLayer(layerId);
        }
        _logger?.LogDebug("Cleared all layers");
    }

    public void Clear()
    {
        ClearLayers();
        _sources.Clear();
        _groups.Clear();
        _logger?.LogDebug("Cleared all layers, sources, and groups");
    }

    // ===== State Management =====

    public string ExportState()
    {
        var state = GetState();
        return JsonSerializer.Serialize(state, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });
    }

    public void ImportState(string json)
    {
        var state = JsonSerializer.Deserialize<LayerManagerState>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        if (state != null)
        {
            RestoreState(state);
        }
    }

    public LayerManagerState GetState()
    {
        return new LayerManagerState
        {
            Sources = _sources.Values.ToList(),
            Layers = _layers.ToList(),
            Groups = _groups.ToList(),
            LayerVisibility = _layers.ToDictionary(l => l.Id, l => l.Visible),
            LayerOpacity = _layers.ToDictionary(l => l.Id, l => l.Opacity),
            LayerOrder = _layers.Select(l => l.Id).ToArray(),
            CapturedAt = DateTime.UtcNow
        };
    }

    public void RestoreState(LayerManagerState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));

        // Clear existing state
        Clear();

        // Restore sources
        foreach (var source in state.Sources)
        {
            AddSource(source);
        }

        // Restore groups
        foreach (var group in state.Groups)
        {
            AddGroup(group);
        }

        // Restore layers
        foreach (var layer in state.Layers)
        {
            AddLayer(layer);
        }

        // Restore visibility and opacity
        foreach (var kvp in state.LayerVisibility)
        {
            SetLayerVisibility(kvp.Key, kvp.Value);
        }

        foreach (var kvp in state.LayerOpacity)
        {
            SetLayerOpacity(kvp.Key, kvp.Value);
        }

        // Restore order
        if (state.LayerOrder.Length > 0)
        {
            ReorderLayers(state.LayerOrder);
        }

        _logger?.LogInformation("Restored layer manager state from {CapturedAt}", state.CapturedAt);
    }
}
