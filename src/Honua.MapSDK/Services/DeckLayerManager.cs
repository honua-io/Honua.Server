// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.MapSDK.Core;
using Honua.MapSDK.Models;
using Microsoft.Extensions.Logging;

namespace Honua.MapSDK.Services;

/// <summary>
/// Service for managing Deck.gl layers in Honua MapSDK
/// Provides high-level API for adding, updating, and removing Deck.gl layers
/// Integrates with ComponentBus for loosely coupled component communication
/// </summary>
public class DeckLayerManager
{
    private readonly ComponentBus _componentBus;
    private readonly ILogger<DeckLayerManager> _logger;
    private readonly Dictionary<string, DeckLayerDefinition> _layers = new();

    public DeckLayerManager(
        ComponentBus componentBus,
        ILogger<DeckLayerManager> logger)
    {
        _componentBus = componentBus;
        _logger = logger;

        // Subscribe to layer messages
        _componentBus.Subscribe<AddDeckLayerMessage>(OnAddLayerMessage);
        _componentBus.Subscribe<RemoveDeckLayerMessage>(OnRemoveLayerMessage);
        _componentBus.Subscribe<UpdateDeckLayerDataMessage>(OnUpdateLayerDataMessage);
        _componentBus.Subscribe<SetDeckLayerVisibilityMessage>(OnSetVisibilityMessage);
        _componentBus.Subscribe<SetDeckLayerOpacityMessage>(OnSetOpacityMessage);
        _componentBus.Subscribe<ClearDeckLayersMessage>(OnClearLayersMessage);
    }

    /// <summary>
    /// Add or update a Deck.gl layer
    /// </summary>
    public async Task AddLayerAsync(DeckLayerDefinition layer, string? mapId = null)
    {
        _layers[layer.Id] = layer;
        layer.UpdatedAt = DateTime.UtcNow;

        await _componentBus.PublishAsync(new AddDeckLayerMessage
        {
            Layer = layer,
            MapId = mapId
        }, "DeckLayerManager");

        _logger.LogInformation("Added Deck.gl layer: {LayerId} ({LayerType})", layer.Id, layer.Type);
    }

    /// <summary>
    /// Remove a Deck.gl layer
    /// </summary>
    public async Task RemoveLayerAsync(string layerId, string? mapId = null)
    {
        _layers.Remove(layerId);

        await _componentBus.PublishAsync(new RemoveDeckLayerMessage
        {
            LayerId = layerId,
            MapId = mapId
        }, "DeckLayerManager");

        _logger.LogInformation("Removed Deck.gl layer: {LayerId}", layerId);
    }

    /// <summary>
    /// Update layer data
    /// </summary>
    public async Task UpdateLayerDataAsync(string layerId, List<object> data, string? mapId = null)
    {
        if (_layers.TryGetValue(layerId, out var layer))
        {
            layer.Data = data;
            layer.UpdatedAt = DateTime.UtcNow;
        }

        await _componentBus.PublishAsync(new UpdateDeckLayerDataMessage
        {
            LayerId = layerId,
            Data = data,
            MapId = mapId
        }, "DeckLayerManager");

        _logger.LogInformation("Updated data for Deck.gl layer: {LayerId} ({Count} items)", layerId, data.Count);
    }

    /// <summary>
    /// Set layer visibility
    /// </summary>
    public async Task SetLayerVisibilityAsync(string layerId, bool visible, string? mapId = null)
    {
        if (_layers.TryGetValue(layerId, out var layer))
        {
            layer.Visible = visible;
            layer.UpdatedAt = DateTime.UtcNow;
        }

        await _componentBus.PublishAsync(new SetDeckLayerVisibilityMessage
        {
            LayerId = layerId,
            Visible = visible,
            MapId = mapId
        }, "DeckLayerManager");

        _logger.LogInformation("Set Deck.gl layer {LayerId} visibility: {Visible}", layerId, visible);
    }

    /// <summary>
    /// Set layer opacity
    /// </summary>
    public async Task SetLayerOpacityAsync(string layerId, double opacity, string? mapId = null)
    {
        if (_layers.TryGetValue(layerId, out var layer))
        {
            layer.Opacity = opacity;
            layer.UpdatedAt = DateTime.UtcNow;
        }

        await _componentBus.PublishAsync(new SetDeckLayerOpacityMessage
        {
            LayerId = layerId,
            Opacity = opacity,
            MapId = mapId
        }, "DeckLayerManager");

        _logger.LogInformation("Set Deck.gl layer {LayerId} opacity: {Opacity}", layerId, opacity);
    }

    /// <summary>
    /// Clear all layers
    /// </summary>
    public async Task ClearAllLayersAsync(string? mapId = null)
    {
        _layers.Clear();

        await _componentBus.PublishAsync(new ClearDeckLayersMessage
        {
            MapId = mapId
        }, "DeckLayerManager");

        _logger.LogInformation("Cleared all Deck.gl layers");
    }

    /// <summary>
    /// Get all layers
    /// </summary>
    public IReadOnlyDictionary<string, DeckLayerDefinition> GetLayers()
    {
        return _layers;
    }

    /// <summary>
    /// Get layer by ID
    /// </summary>
    public DeckLayerDefinition? GetLayer(string layerId)
    {
        return _layers.GetValueOrDefault(layerId);
    }

    /// <summary>
    /// Get layers by type
    /// </summary>
    public IEnumerable<DeckLayerDefinition> GetLayersByType(string type)
    {
        return _layers.Values.Where(l => l.Type == type);
    }

    /// <summary>
    /// Create a scatterplot layer
    /// </summary>
    public ScatterplotLayerDefinition CreateScatterplotLayer(
        string name,
        List<object> data,
        string? getPosition = null,
        string? getRadius = null,
        string? getFillColor = null)
    {
        return new ScatterplotLayerDefinition
        {
            Name = name,
            Data = data,
            GetPosition = getPosition ?? "position",
            GetRadius = getRadius ?? "radius",
            GetFillColor = getFillColor ?? "color"
        };
    }

    /// <summary>
    /// Create a hexagon layer
    /// </summary>
    public HexagonLayerDefinition CreateHexagonLayer(
        string name,
        List<object> data,
        double radius = 1000,
        bool extruded = true,
        string? getPosition = null)
    {
        return new HexagonLayerDefinition
        {
            Name = name,
            Data = data,
            Radius = radius,
            Extruded = extruded,
            GetPosition = getPosition ?? "position"
        };
    }

    /// <summary>
    /// Create an arc layer (for origin-destination flows)
    /// </summary>
    public ArcLayerDefinition CreateArcLayer(
        string name,
        List<object> data,
        string? getSourcePosition = null,
        string? getTargetPosition = null)
    {
        return new ArcLayerDefinition
        {
            Name = name,
            Data = data,
            GetSourcePosition = getSourcePosition ?? "sourcePosition",
            GetTargetPosition = getTargetPosition ?? "targetPosition"
        };
    }

    /// <summary>
    /// Create a grid layer
    /// </summary>
    public GridLayerDefinition CreateGridLayer(
        string name,
        List<object> data,
        double cellSize = 1000,
        bool extruded = true,
        string? getPosition = null)
    {
        return new GridLayerDefinition
        {
            Name = name,
            Data = data,
            CellSize = cellSize,
            Extruded = extruded,
            GetPosition = getPosition ?? "position"
        };
    }

    /// <summary>
    /// Create a screen grid layer (heatmap)
    /// </summary>
    public ScreenGridLayerDefinition CreateScreenGridLayer(
        string name,
        List<object> data,
        int cellSizePixels = 50,
        string? getPosition = null)
    {
        return new ScreenGridLayerDefinition
        {
            Name = name,
            Data = data,
            CellSizePixels = cellSizePixels,
            GetPosition = getPosition ?? "position"
        };
    }

    // Event handlers for ComponentBus messages

    private void OnAddLayerMessage(MessageArgs<AddDeckLayerMessage> args)
    {
        var layer = args.Message.Layer;
        _layers[layer.Id] = layer;
        _logger.LogTrace("Received AddDeckLayer message: {LayerId}", layer.Id);
    }

    private void OnRemoveLayerMessage(MessageArgs<RemoveDeckLayerMessage> args)
    {
        var layerId = args.Message.LayerId;
        _layers.Remove(layerId);
        _logger.LogTrace("Received RemoveDeckLayer message: {LayerId}", layerId);
    }

    private void OnUpdateLayerDataMessage(MessageArgs<UpdateDeckLayerDataMessage> args)
    {
        var layerId = args.Message.LayerId;
        if (_layers.TryGetValue(layerId, out var layer))
        {
            layer.Data = args.Message.Data;
            layer.UpdatedAt = DateTime.UtcNow;
        }
        _logger.LogTrace("Received UpdateDeckLayerData message: {LayerId}", layerId);
    }

    private void OnSetVisibilityMessage(MessageArgs<SetDeckLayerVisibilityMessage> args)
    {
        var layerId = args.Message.LayerId;
        if (_layers.TryGetValue(layerId, out var layer))
        {
            layer.Visible = args.Message.Visible;
            layer.UpdatedAt = DateTime.UtcNow;
        }
        _logger.LogTrace("Received SetDeckLayerVisibility message: {LayerId}", layerId);
    }

    private void OnSetOpacityMessage(MessageArgs<SetDeckLayerOpacityMessage> args)
    {
        var layerId = args.Message.LayerId;
        if (_layers.TryGetValue(layerId, out var layer))
        {
            layer.Opacity = args.Message.Opacity;
            layer.UpdatedAt = DateTime.UtcNow;
        }
        _logger.LogTrace("Received SetDeckLayerOpacity message: {LayerId}", layerId);
    }

    private void OnClearLayersMessage(MessageArgs<ClearDeckLayersMessage> args)
    {
        _layers.Clear();
        _logger.LogTrace("Received ClearDeckLayers message");
    }
}
