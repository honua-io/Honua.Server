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
/// Published when layers are reordered
/// </summary>
public class LayerReorderedMessage
{
    public required string LayerId { get; init; }
    public required int NewOrder { get; init; }
    public string? ComponentId { get; init; }
}

/// <summary>
/// Published when a layer is selected in the layer list
/// </summary>
public class LayerSelectedMessage
{
    public required string LayerId { get; init; }
    public required string LayerName { get; init; }
    public string? ComponentId { get; init; }
}

/// <summary>
/// Published when layer metadata is updated
/// </summary>
public class LayerMetadataUpdatedMessage
{
    public required string LayerId { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
    public string? ComponentId { get; init; }
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
    public string? BasemapId { get; init; }
    public string? BasemapName { get; init; }
    public string? ComponentId { get; init; }
}

/// <summary>
/// Published when basemap is loading
/// </summary>
public class BasemapLoadingMessage
{
    public required string MapId { get; init; }
    public required bool IsLoading { get; init; }
    public string? BasemapId { get; init; }
    public string? ComponentId { get; init; }
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

/// <summary>
/// Published when data import completes successfully
/// </summary>
public class DataImportedMessage
{
    public required string LayerId { get; init; }
    public required string LayerName { get; init; }
    public required int FeatureCount { get; init; }
    public required string Format { get; init; }
    public required string ComponentId { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Published during import to report progress
/// </summary>
public class ImportProgressMessage
{
    public required int Current { get; init; }
    public required int Total { get; init; }
    public required string Status { get; init; }
    public required string ComponentId { get; init; }
    public double Percentage => Total > 0 ? (double)Current / Total * 100 : 0;
}

/// <summary>
/// Published when import fails with errors
/// </summary>
public class ImportErrorMessage
{
    public required string ComponentId { get; init; }
    public required string ErrorMessage { get; init; }
    public List<ImportValidationError> Errors { get; init; } = new();
}

/// <summary>
/// Import validation error details
/// </summary>
public class ImportValidationError
{
    public required int RowNumber { get; init; }
    public required string Field { get; init; }
    public required string Message { get; init; }
    public required string Severity { get; init; } // "error", "warning", "info"
}

/// <summary>
/// Published when user clicks on the overview map to navigate main map
/// </summary>
public class OverviewMapClickedMessage
{
    public required double[] Center { get; init; }
    public required double Zoom { get; init; }
    public required string ComponentId { get; init; }
}

/// <summary>
/// Published when a bookmark is selected and map should navigate to it
/// </summary>
public class BookmarkSelectedMessage
{
    public required string BookmarkId { get; init; }
    public required string BookmarkName { get; init; }
    public required double[] Center { get; init; }
    public required double Zoom { get; init; }
    public double Bearing { get; init; }
    public double Pitch { get; init; }
    public required string ComponentId { get; init; }
}

/// <summary>
/// Published when a new bookmark is created
/// </summary>
public class BookmarkCreatedMessage
{
    public required string BookmarkId { get; init; }
    public required string BookmarkName { get; init; }
    public required string ComponentId { get; init; }
    public required double[] Center { get; init; }
    public required double Zoom { get; init; }
}

/// <summary>
/// Published when a bookmark is deleted
/// </summary>
public class BookmarkDeletedMessage
{
    public required string BookmarkId { get; init; }
    public required string ComponentId { get; init; }
}

/// <summary>
/// Published when a bookmark is updated
/// </summary>
public class BookmarkUpdatedMessage
{
    public required string BookmarkId { get; init; }
    public required string BookmarkName { get; init; }
    public required string ComponentId { get; init; }
}

/// <summary>
/// Published when a feature is drawn on the map
/// </summary>
public class FeatureDrawnMessage
{
    public required string FeatureId { get; init; }
    public required string GeometryType { get; init; }
    public required object Geometry { get; init; }
    public Dictionary<string, object> Properties { get; init; } = new();
    public required string ComponentId { get; init; }
}

/// <summary>
/// Published when a feature is measured
/// </summary>
public class FeatureMeasuredMessage
{
    public required string FeatureId { get; init; }
    public double? Distance { get; init; }
    public double? Area { get; init; }
    public double? Perimeter { get; init; }
    public double? Radius { get; init; }
    public required string Unit { get; init; }
    public required string ComponentId { get; init; }
}

/// <summary>
/// Published when a drawn feature is edited
/// </summary>
public class FeatureEditedMessage
{
    public required string FeatureId { get; init; }
    public required object Geometry { get; init; }
    public required string EditType { get; init; } // "move", "reshape", "rotate", "scale"
    public required string ComponentId { get; init; }
}

/// <summary>
/// Published when a drawn feature is deleted
/// </summary>
public class FeatureDeletedMessage
{
    public required string FeatureId { get; init; }
    public required string ComponentId { get; init; }
}

/// <summary>
/// Published when drawing mode changes
/// </summary>
public class DrawModeChangedMessage
{
    public required string Mode { get; init; } // "point", "line", "polygon", "circle", "rectangle", "freehand", "text", "select", "edit", null for idle
    public required string ComponentId { get; init; }
}

/// <summary>
/// Request to start drawing mode
/// </summary>
public class StartDrawingRequestMessage
{
    public required string MapId { get; init; }
    public required string Mode { get; init; }
    public string? ComponentId { get; init; }
}

/// <summary>
/// Request to stop drawing mode
/// </summary>
public class StopDrawingRequestMessage
{
    public required string MapId { get; init; }
    public string? ComponentId { get; init; }
}

/// <summary>
/// Published when a feature is selected (clicked or hovered)
/// </summary>
public class FeatureSelectedMessage
{
    public required string MapId { get; init; }
    public required string FeatureId { get; init; }
    public required string LayerId { get; init; }
    public Dictionary<string, object> Properties { get; init; } = new();
    public object? Geometry { get; init; }
    public double[]? Coordinates { get; init; }
    public required string ComponentId { get; init; }
}

/// <summary>
/// Published when a popup is opened
/// </summary>
public class PopupOpenedMessage
{
    public required string PopupId { get; init; }
    public required string FeatureId { get; init; }
    public required string LayerId { get; init; }
    public required double[] Coordinates { get; init; }
    public required string ComponentId { get; init; }
}

/// <summary>
/// Published when a popup is closed
/// </summary>
public class PopupClosedMessage
{
    public required string PopupId { get; init; }
    public required string ComponentId { get; init; }
}

/// <summary>
/// Request to open a popup at a specific location
/// </summary>
public class OpenPopupRequestMessage
{
    public required string MapId { get; init; }
    public required double[] Coordinates { get; init; }
    public string? FeatureId { get; init; }
    public string? LayerId { get; init; }
    public Dictionary<string, object>? Properties { get; init; }
    public string? ComponentId { get; init; }
}

/// <summary>
/// Request to close popup
/// </summary>
public class ClosePopupRequestMessage
{
    public required string MapId { get; init; }
    public string? ComponentId { get; init; }
}

/// <summary>
/// Published when a feature is created in the editor
/// </summary>
public class FeatureCreatedMessage
{
    public required string FeatureId { get; init; }
    public required string LayerId { get; init; }
    public required string GeometryType { get; init; }
    public required object Geometry { get; init; }
    public Dictionary<string, object> Attributes { get; init; } = new();
    public required string ComponentId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Published when a feature is updated in the editor
/// </summary>
public class FeatureUpdatedMessage
{
    public required string FeatureId { get; init; }
    public required string LayerId { get; init; }
    public object? Geometry { get; init; }
    public Dictionary<string, object>? Attributes { get; init; }
    public required string UpdateType { get; init; } // "geometry", "attributes", "both"
    public required string ComponentId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Published when an edit session is started
/// </summary>
public class EditSessionStartedMessage
{
    public required string SessionId { get; init; }
    public required string ComponentId { get; init; }
    public List<string> EditableLayers { get; init; } = new();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Published when an edit session is ended
/// </summary>
public class EditSessionEndedMessage
{
    public required string SessionId { get; init; }
    public required string ComponentId { get; init; }
    public bool ChangesSaved { get; init; }
    public int OperationCount { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Published when edit session state changes (dirty/clean)
/// </summary>
public class EditSessionStateChangedMessage
{
    public required string SessionId { get; init; }
    public required string ComponentId { get; init; }
    public bool IsDirty { get; init; }
    public int UnsavedChanges { get; init; }
    public bool CanUndo { get; init; }
    public bool CanRedo { get; init; }
}

/// <summary>
/// Published when a feature is selected in the editor for editing
/// </summary>
public class EditorFeatureSelectedMessage
{
    public required string FeatureId { get; init; }
    public required string LayerId { get; init; }
    public required string ComponentId { get; init; }
    public Dictionary<string, object> Attributes { get; init; } = new();
}

/// <summary>
/// Published when validation errors occur during editing
/// </summary>
public class EditValidationErrorMessage
{
    public required string FeatureId { get; init; }
    public required string ComponentId { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}

/// <summary>
/// Published when coordinates are clicked on the map
/// </summary>
public class CoordinateClickedMessage
{
    public required string DisplayId { get; init; }
    public required double Longitude { get; init; }
    public required double Latitude { get; init; }
    public double? Elevation { get; init; }
    public required string Formatted { get; init; }
}

/// <summary>
/// Published when coordinates are pinned in the coordinate display
/// </summary>
public class CoordinatePinnedMessage
{
    public required string DisplayId { get; init; }
    public required double Longitude { get; init; }
    public required double Latitude { get; init; }
    public double? Elevation { get; init; }
    public required string Formatted { get; init; }
}
