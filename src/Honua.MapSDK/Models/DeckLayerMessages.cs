// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Models;

/// <summary>
/// Message to add or update a Deck.gl layer
/// </summary>
public class AddDeckLayerMessage
{
    /// <summary>
    /// Layer definition
    /// </summary>
    public required DeckLayerDefinition Layer { get; set; }

    /// <summary>
    /// Map ID (if multiple maps exist)
    /// </summary>
    public string? MapId { get; set; }
}

/// <summary>
/// Message to remove a Deck.gl layer
/// </summary>
public class RemoveDeckLayerMessage
{
    /// <summary>
    /// Layer ID to remove
    /// </summary>
    public required string LayerId { get; set; }

    /// <summary>
    /// Map ID (if multiple maps exist)
    /// </summary>
    public string? MapId { get; set; }
}

/// <summary>
/// Message to update layer data
/// </summary>
public class UpdateDeckLayerDataMessage
{
    /// <summary>
    /// Layer ID to update
    /// </summary>
    public required string LayerId { get; set; }

    /// <summary>
    /// New data for the layer
    /// </summary>
    public required List<object> Data { get; set; }

    /// <summary>
    /// Map ID (if multiple maps exist)
    /// </summary>
    public string? MapId { get; set; }
}

/// <summary>
/// Message to update layer visibility
/// </summary>
public class SetDeckLayerVisibilityMessage
{
    /// <summary>
    /// Layer ID
    /// </summary>
    public required string LayerId { get; set; }

    /// <summary>
    /// Visibility state
    /// </summary>
    public bool Visible { get; set; }

    /// <summary>
    /// Map ID (if multiple maps exist)
    /// </summary>
    public string? MapId { get; set; }
}

/// <summary>
/// Message to update layer opacity
/// </summary>
public class SetDeckLayerOpacityMessage
{
    /// <summary>
    /// Layer ID
    /// </summary>
    public required string LayerId { get; set; }

    /// <summary>
    /// Opacity (0-1)
    /// </summary>
    public double Opacity { get; set; }

    /// <summary>
    /// Map ID (if multiple maps exist)
    /// </summary>
    public string? MapId { get; set; }
}

/// <summary>
/// Message to clear all Deck.gl layers
/// </summary>
public class ClearDeckLayersMessage
{
    /// <summary>
    /// Map ID (if multiple maps exist)
    /// </summary>
    public string? MapId { get; set; }
}

/// <summary>
/// Message when a Deck.gl layer is clicked
/// </summary>
public class DeckLayerClickedMessage
{
    /// <summary>
    /// Layer ID that was clicked
    /// </summary>
    public required string LayerId { get; set; }

    /// <summary>
    /// Clicked object data
    /// </summary>
    public required object ClickedObject { get; set; }

    /// <summary>
    /// Screen X coordinate
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Screen Y coordinate
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// Geographic coordinate [lng, lat]
    /// </summary>
    public double[] Coordinate { get; set; } = Array.Empty<double>();

    /// <summary>
    /// Map ID (if multiple maps exist)
    /// </summary>
    public string? MapId { get; set; }
}

/// <summary>
/// Message when a Deck.gl layer is hovered
/// </summary>
public class DeckLayerHoveredMessage
{
    /// <summary>
    /// Layer ID being hovered
    /// </summary>
    public required string LayerId { get; set; }

    /// <summary>
    /// Hovered object data (null if no object)
    /// </summary>
    public object? HoveredObject { get; set; }

    /// <summary>
    /// Screen X coordinate
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Screen Y coordinate
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// Map ID (if multiple maps exist)
    /// </summary>
    public string? MapId { get; set; }
}

/// <summary>
/// Message when Deck.gl layer data is loaded from URL
/// </summary>
public class DeckLayerDataLoadedMessage
{
    /// <summary>
    /// Layer ID
    /// </summary>
    public required string LayerId { get; set; }

    /// <summary>
    /// URL that was loaded
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// Number of items loaded
    /// </summary>
    public int ItemCount { get; set; }

    /// <summary>
    /// Load duration in milliseconds
    /// </summary>
    public double DurationMs { get; set; }

    /// <summary>
    /// Map ID (if multiple maps exist)
    /// </summary>
    public string? MapId { get; set; }
}
