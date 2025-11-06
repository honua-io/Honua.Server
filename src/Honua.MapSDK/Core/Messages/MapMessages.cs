namespace Honua.MapSDK.Core.Messages;

/// <summary>
/// Published when map extent/viewport changes
/// </summary>
public class MapExtentChangedMessage
{
    public required string MapId { get; init; }
    public required double[] Bounds { get; init; } // [west, south, east, north]
    public required double Zoom { get; init; }
    public required double[] Center { get; init; }
    public double Bearing { get; init; }
    public double Pitch { get; init; }
}

/// <summary>
/// Published when user clicks a feature on the map
/// </summary>
public class FeatureClickedMessage
{
    public required string MapId { get; init; }
    public required string LayerId { get; init; }
    public required string FeatureId { get; init; }
    public Dictionary<string, object> Properties { get; init; } = new();
    public object? Geometry { get; init; }
}

/// <summary>
/// Published when user hovers over a feature
/// </summary>
public class FeatureHoveredMessage
{
    public required string MapId { get; init; }
    public string? FeatureId { get; init; } // null when hover ends
    public string? LayerId { get; init; }
    public Dictionary<string, object>? Properties { get; init; }
}

/// <summary>
/// Published when map is ready and initialized
/// </summary>
public class MapReadyMessage
{
    public required string MapId { get; init; }
    public required double[] Center { get; init; }
    public required double Zoom { get; init; }
}

/// <summary>
/// Published when a filter is applied
/// </summary>
public class FilterAppliedMessage
{
    public required string FilterId { get; init; }
    public required FilterType Type { get; init; }
    public required object Expression { get; init; }
    public string[]? AffectedLayers { get; init; }
}

/// <summary>
/// Published when a filter is cleared
/// </summary>
public class FilterClearedMessage
{
    public required string FilterId { get; init; }
}

/// <summary>
/// Published when all filters are cleared
/// </summary>
public class AllFiltersClearedMessage
{
    public required string Source { get; init; }
}

public enum FilterType
{
    Spatial,
    Attribute,
    Temporal
}

/// <summary>
/// Published when grid/table row is selected
/// </summary>
public class DataRowSelectedMessage
{
    public required string GridId { get; init; }
    public required string RowId { get; init; }
    public Dictionary<string, object> Data { get; init; } = new();
    public object? Geometry { get; init; }
}

/// <summary>
/// Published when data is loaded into a component
/// </summary>
public class DataLoadedMessage
{
    public required string ComponentId { get; init; }
    public required int FeatureCount { get; init; }
    public required string Source { get; init; }
}

/// <summary>
/// Published when layer visibility changes
/// </summary>
public class LayerVisibilityChangedMessage
{
    public required string LayerId { get; init; }
    public required bool Visible { get; init; }
}

/// <summary>
/// Published when layer opacity changes
/// </summary>
public class LayerOpacityChangedMessage
{
    public required string LayerId { get; init; }
    public required double Opacity { get; init; }
}

/// <summary>
/// Published when layer is added to map
/// </summary>
public class LayerAddedMessage
{
    public required string LayerId { get; init; }
    public required string LayerName { get; init; }
}

/// <summary>
/// Published when layer is removed from map
/// </summary>
public class LayerRemovedMessage
{
    public required string LayerId { get; init; }
}

/// <summary>
/// Published during timeline animation when time position changes
/// </summary>
public class TimeChangedMessage
{
    /// <summary>
    /// Current time position
    /// </summary>
    public required DateTime CurrentTime { get; init; }

    /// <summary>
    /// Timeline start time
    /// </summary>
    public required DateTime StartTime { get; init; }

    /// <summary>
    /// Timeline end time
    /// </summary>
    public required DateTime EndTime { get; init; }

    /// <summary>
    /// Timeline component ID
    /// </summary>
    public required string ComponentId { get; init; }

    /// <summary>
    /// Field name containing timestamp data
    /// </summary>
    public string? TimeField { get; init; }

    /// <summary>
    /// Current step index (0-based)
    /// </summary>
    public int CurrentStep { get; init; }

    /// <summary>
    /// Total number of steps
    /// </summary>
    public int TotalSteps { get; init; }

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public double Progress { get; init; }

    /// <summary>
    /// Whether timeline is playing
    /// </summary>
    public bool IsPlaying { get; init; }

    /// <summary>
    /// Playback direction (1 = forward, -1 = reverse)
    /// </summary>
    public int Direction { get; init; } = 1;
}

/// <summary>
/// Published when timeline playback state changes
/// </summary>
public class TimelineStateChangedMessage
{
    /// <summary>
    /// Timeline component ID
    /// </summary>
    public required string ComponentId { get; init; }

    /// <summary>
    /// New playback state
    /// </summary>
    public required string State { get; init; } // "playing", "paused", "stopped"

    /// <summary>
    /// Current speed multiplier
    /// </summary>
    public double Speed { get; init; } = 1.0;

    /// <summary>
    /// Loop enabled
    /// </summary>
    public bool Loop { get; init; }
}

/// <summary>
/// Request to fly map to specific location
/// </summary>
public class FlyToRequestMessage
{
    public required string MapId { get; init; }
    public required double[] Center { get; init; }
    public double? Zoom { get; init; }
    public double? Bearing { get; init; }
    public double? Pitch { get; init; }
    public int Duration { get; init; } = 1000; // milliseconds
}

/// <summary>
/// Request to fit map to bounds
/// </summary>
public class FitBoundsRequestMessage
{
    public required string MapId { get; init; }
    public required double[] Bounds { get; init; } // [west, south, east, north]
    public int Padding { get; init; } = 50; // pixels
}

/// <summary>
/// Published when basemap style changes
/// </summary>
public class BasemapChangedMessage
{
    public required string MapId { get; init; }
    public required string Style { get; init; }
}

/// <summary>
/// Request to highlight features on map
/// </summary>
public class HighlightFeaturesRequestMessage
{
    public required string MapId { get; init; }
    public required string[] FeatureIds { get; init; }
    public string? LayerId { get; init; }
}

/// <summary>
/// Request to clear feature highlights
/// </summary>
public class ClearHighlightsRequestMessage
{
    public required string MapId { get; init; }
}

/// <summary>
/// Request for data from a component (e.g., chart requesting data from map)
/// </summary>
public class DataRequestMessage
{
    public required string ComponentId { get; init; }
    public required string MapId { get; init; }
}

/// <summary>
/// Response with data to requesting component
/// </summary>
public class DataResponseMessage
{
    public required string RequesterId { get; init; }
    public required List<Dictionary<string, object>> Features { get; init; }
}

/// <summary>
/// Published when a search result is selected in HonuaSearch
/// </summary>
public class SearchResultSelectedMessage
{
    public required string SearchId { get; init; }
    public required string DisplayName { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public double[]? BoundingBox { get; init; }
    public string? Type { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();
}
