// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.MapSDK.Models;

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

    /// <summary>
    /// MapLibre style URL (for MapLibre component)
    /// </summary>
    public required string Style { get; init; }

    /// <summary>
    /// Tile layer URL template (for Leaflet component)
    /// </summary>
    public string? TileUrl { get; init; }

    /// <summary>
    /// Attribution text (for Leaflet component)
    /// </summary>
    public string? Attribution { get; init; }

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
/// Published when drawing mode is started
/// </summary>
public class DrawingStartedMessage
{
    public required string MapId { get; init; }
    public required string DrawingMode { get; init; } // point, line, polygon, rectangle, circle
    public Dictionary<string, object> Style { get; init; } = new();
}

/// <summary>
/// Published when drawing is completed
/// </summary>
public class DrawingCompletedMessage
{
    public required string MapId { get; init; }
    public required string GeometryId { get; init; }
    public required string GeometryType { get; init; }
    public required object Geometry { get; init; } // GeoJSON geometry
    public Dictionary<string, object> Properties { get; init; } = new();
}

/// <summary>
/// Published when drawing is cancelled
/// </summary>
public class DrawingCancelledMessage
{
    public required string MapId { get; init; }
    public required string DrawingMode { get; init; }
}

/// <summary>
/// Published when a coordinate is added during drawing
/// </summary>
public class DrawingCoordinateAddedMessage
{
    public required string MapId { get; init; }
    public required double[] Coordinate { get; init; } // [longitude, latitude]
    public required int CoordinateIndex { get; init; }
}

/// <summary>
/// Published when a geometry is selected for editing
/// </summary>
public class GeometrySelectedMessage
{
    public required string MapId { get; init; }
    public required string GeometryId { get; init; }
    public required string GeometryType { get; init; }
    public required object Geometry { get; init; }
}

/// <summary>
/// Published when a geometry is deselected
/// </summary>
public class GeometryDeselectedMessage
{
    public required string MapId { get; init; }
    public required string GeometryId { get; init; }
}

/// <summary>
/// Published when a geometry is edited
/// </summary>
public class GeometryEditedMessage
{
    public required string MapId { get; init; }
    public required string GeometryId { get; init; }
    public required string EditType { get; init; } // vertex_moved, vertex_added, vertex_removed, etc.
    public required object UpdatedGeometry { get; init; }
}

/// <summary>
/// Published when a geometry is deleted
/// </summary>
public class GeometryDeletedMessage
{
    public required string MapId { get; init; }
    public required string GeometryId { get; init; }
}

/// <summary>
/// Published when a measurement is updated
/// </summary>
public class MeasurementUpdatedMessage
{
    public required string MapId { get; init; }
    public required string MeasurementType { get; init; } // distance, area, bearing
    public required double Value { get; init; }
    public required string Unit { get; init; }
    public required string FormattedValue { get; init; }
    public Dictionary<string, object> AdditionalData { get; init; } = new();
}

/// <summary>
/// Published when a vertex is updated during editing
/// </summary>
public class VertexUpdatedMessage
{
    public required string MapId { get; init; }
    public required string GeometryId { get; init; }
    public required int VertexIndex { get; init; }
    public required double[] NewCoordinate { get; init; }
}

/// <summary>
/// Request to start drawing on the map
/// </summary>
public class StartDrawingRequestMessage
{
    public required string MapId { get; init; }
    public required string Mode { get; init; }
    public string? DrawingMode { get; init; }
    public string? ComponentId { get; init; }
    public Dictionary<string, object>? Style { get; init; }
}

/// <summary>
/// Request to stop drawing on the map
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

/// <summary>
/// Published when a spatial analysis operation is started
/// </summary>
public class AnalysisStartedMessage
{
    public required string ComponentId { get; init; }
    public required string OperationType { get; init; }
    public Dictionary<string, object> Parameters { get; init; } = new();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Published when a spatial analysis operation completes successfully
/// </summary>
public class AnalysisCompletedMessage
{
    public required string ComponentId { get; init; }
    public required string OperationType { get; init; }
    public bool Success { get; init; }
    public int FeatureCount { get; init; }
    public double ExecutionTime { get; init; }
    public Dictionary<string, double> Statistics { get; init; } = new();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Published when a spatial analysis operation fails
/// </summary>
public class AnalysisErrorMessage
{
    public required string ComponentId { get; init; }
    public required string OperationType { get; init; }
    public required string ErrorMessage { get; init; }
    public List<string> Warnings { get; init; } = new();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Published when an analysis result is added to the map as a layer
/// </summary>
public class AnalysisResultAddedMessage
{
    public required string ComponentId { get; init; }
    public required string LayerId { get; init; }
    public required string LayerName { get; init; }
    public required string OperationType { get; init; }
    public int FeatureCount { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Published when an analysis result layer is removed from the map
/// </summary>
public class AnalysisResultRemovedMessage
{
    public required string ComponentId { get; init; }
    public required string LayerId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Published when analysis progress updates (for long-running operations)
/// </summary>
public class AnalysisProgressMessage
{
    public required string ComponentId { get; init; }
    public required string OperationType { get; init; }
    public required int Current { get; init; }
    public required int Total { get; init; }
    public required string Status { get; init; }
    public double Percentage => Total > 0 ? (double)Current / Total * 100 : 0;
}

/// <summary>
/// Request to perform a spatial analysis operation
/// </summary>
public class AnalysisRequestMessage
{
    public required string RequesterId { get; init; }
    public required string OperationType { get; init; }
    public required string InputLayerId { get; init; }
    public string? SecondaryLayerId { get; init; }
    public Dictionary<string, object> Parameters { get; init; } = new();
}

/// <summary>
/// Request to clear all analysis results from the map
/// </summary>
public class ClearAnalysisResultsRequestMessage
{
    public required string MapId { get; init; }
    public string? ComponentId { get; init; }
}

/// <summary>
/// Published when compare component is initialized and ready
/// </summary>
public class CompareReadyMessage
{
    public required string CompareId { get; init; }
    public required CompareMode Mode { get; init; }
    public required string LeftStyle { get; init; }
    public required string RightStyle { get; init; }
}

/// <summary>
/// Published when comparison mode changes
/// </summary>
public class CompareModeChangedMessage
{
    public required string CompareId { get; init; }
    public required CompareMode Mode { get; init; }
}

/// <summary>
/// Published when compare view (camera position) changes
/// </summary>
public class CompareViewChangedMessage
{
    public required string CompareId { get; init; }
    public required double[] Center { get; init; }
    public required double Zoom { get; init; }
    public double Bearing { get; init; }
    public double Pitch { get; init; }
}

/// <summary>
/// Published when divider position changes in swipe/side-by-side mode
/// </summary>
public class CompareDividerChangedMessage
{
    public required string CompareId { get; init; }
    public required double Position { get; init; } // 0-1
}

/// <summary>
/// Published when an elevation profile is successfully generated
/// </summary>
public class ElevationProfileGeneratedMessage
{
    public required string ProfileId { get; init; }
    public required double TotalDistance { get; init; }
    public required double ElevationGain { get; init; }
    public required double ElevationLoss { get; init; }
    public double MaxElevation { get; init; }
    public double MinElevation { get; init; }
    public double AverageGrade { get; init; }
    public required string ComponentId { get; init; }
    public int SampleCount { get; init; }
    public string? Source { get; init; }
}

/// <summary>
/// Published when a point on the elevation chart is hovered
/// </summary>
public class ElevationPointHoveredMessage
{
    public required string ProfileId { get; init; }
    public required double Distance { get; init; }
    public required double Elevation { get; init; }
    public required double[] Coordinates { get; init; }
    public double Grade { get; init; }
    public required string ComponentId { get; init; }
}

/// <summary>
/// Published when a point on the elevation chart is clicked
/// </summary>
public class ElevationPointClickedMessage
{
    public required string ProfileId { get; init; }
    public required double Distance { get; init; }
    public required double Elevation { get; init; }
    public required double[] Coordinates { get; init; }
    public required string ComponentId { get; init; }
}

/// <summary>
/// Published when a steep section is identified in the profile
/// </summary>
public class SteepSectionIdentifiedMessage
{
    public required string ProfileId { get; init; }
    public required double StartDistance { get; init; }
    public required double EndDistance { get; init; }
    public required double AverageGrade { get; init; }
    public required double MaxGrade { get; init; }
    public required string Severity { get; init; }
    public required string ComponentId { get; init; }
}

/// <summary>
/// Request to generate elevation profile for a path
/// </summary>
public class GenerateElevationProfileRequestMessage
{
    public required string MapId { get; init; }
    public required double[][] Coordinates { get; init; }
    public string? ElevationSource { get; init; }
    public int SamplePoints { get; init; } = 100;
    public string? ComponentId { get; init; }
}

/// <summary>
/// Published when elevation profile generation fails
/// </summary>
public class ElevationProfileErrorMessage
{
    public required string ComponentId { get; init; }
    public required string ErrorMessage { get; init; }
    public string? ErrorType { get; init; } // "api", "network", "data", "validation"
}

/// <summary>
/// Published when a route is successfully calculated
/// </summary>
public class RouteCalculatedMessage
{
    public required string RoutingId { get; init; }
    public required string RouteId { get; init; }
    public required double Distance { get; init; } // meters
    public required int Duration { get; init; } // seconds
    public required string FormattedDistance { get; init; }
    public required string FormattedDuration { get; init; }
    public required string TravelMode { get; init; }
    public required string ComponentId { get; init; }
    public int WaypointCount { get; init; }
    public int InstructionCount { get; init; }
    public bool IsAlternative { get; init; }
    public int AlternativeIndex { get; init; }
}

/// <summary>
/// Published when route calculation fails
/// </summary>
public class RoutingErrorMessage
{
    public required string RoutingId { get; init; }
    public required string ErrorMessage { get; init; }
    public required string ComponentId { get; init; }
    public string? ErrorCode { get; init; }
    public string? RoutingEngine { get; init; }
}

/// <summary>
/// Published when a waypoint is added to the route
/// </summary>
public class WaypointAddedMessage
{
    public required string RoutingId { get; init; }
    public required string WaypointId { get; init; }
    public required double Longitude { get; init; }
    public required double Latitude { get; init; }
    public string? Name { get; init; }
    public string? Address { get; init; }
    public required string WaypointType { get; init; } // "start", "via", "end"
    public required int Index { get; init; }
    public required string ComponentId { get; init; }
}

/// <summary>
/// Published when a waypoint is removed from the route
/// </summary>
public class WaypointRemovedMessage
{
    public required string RoutingId { get; init; }
    public required string WaypointId { get; init; }
    public required int Index { get; init; }
    public required string ComponentId { get; init; }
}

/// <summary>
/// Published when waypoints are reordered
/// </summary>
public class WaypointsReorderedMessage
{
    public required string RoutingId { get; init; }
    public required string ComponentId { get; init; }
    public List<string> WaypointOrder { get; init; } = new();
}

/// <summary>
/// Published when a route instruction/step is selected
/// </summary>
public class RouteInstructionSelectedMessage
{
    public required string RoutingId { get; init; }
    public required int InstructionIndex { get; init; }
    public required string Text { get; init; }
    public required double Distance { get; init; }
    public required double[] Coordinate { get; init; }
    public required string ComponentId { get; init; }
}

/// <summary>
/// Published when routing options change (travel mode, preferences, etc.)
/// </summary>
public class RoutingOptionsChangedMessage
{
    public required string RoutingId { get; init; }
    public required string TravelMode { get; init; }
    public required string Preference { get; init; }
    public List<string> AvoidOptions { get; init; } = new();
    public required string ComponentId { get; init; }
}

/// <summary>
/// Request to calculate a route from a location selected on the map
/// </summary>
public class LocationSelectedForRoutingMessage
{
    public required string MapId { get; init; }
    public required double Longitude { get; init; }
    public required double Latitude { get; init; }
    public string? Name { get; init; }
    public string? Address { get; init; }
    public required string WaypointType { get; init; } // "start", "via", "end"
}

/// <summary>
/// Published when an isochrone is calculated
/// </summary>
public class IsochroneCalculatedMessage
{
    public required string RoutingId { get; init; }
    public required double[] Center { get; init; }
    public required string TravelMode { get; init; }
    public List<int> Intervals { get; init; } = new();
    public required string ComponentId { get; init; }
    public int PolygonCount { get; init; }
}

/// <summary>
/// Published when route is cleared/reset
/// </summary>
public class RouteClearedMessage
{
    public required string RoutingId { get; init; }
    public required string ComponentId { get; init; }
}

/// <summary>
/// Published when a route is exported (GPX, KML, etc.)
/// </summary>
public class RouteExportedMessage
{
    public required string RoutingId { get; init; }
    public required string RouteId { get; init; }
    public required string Format { get; init; } // "gpx", "kml", "geojson", "pdf"
    public required string ComponentId { get; init; }
    public string? FileName { get; init; }
}

/// <summary>
/// Published when a route segment is highlighted
/// </summary>
public class RouteSegmentHighlightedMessage
{
    public required string RoutingId { get; init; }
    public required int SegmentIndex { get; init; }
    public required string ComponentId { get; init; }
}

/// <summary>
/// Request to edit a geometry
/// </summary>
public class EditGeometryRequestMessage
{
    public required string MapId { get; init; }
    public required string GeometryId { get; init; }
}

/// <summary>
/// Request to delete a geometry
/// </summary>
public class DeleteGeometryRequestMessage
{
    public required string MapId { get; init; }
    public required string GeometryId { get; init; }
}

/// <summary>
/// Request to export geometries
/// </summary>
public class ExportGeometriesRequestMessage
{
    public required string MapId { get; init; }
    public required string Format { get; init; } // geojson, wkt, kml, etc.
    public string[]? GeometryIds { get; init; } // null = export all
}

/// <summary>
/// Published when geometries are exported
/// </summary>
public class GeometriesExportedMessage
{
    public required string MapId { get; init; }
    public required string Format { get; init; }
    public required string Data { get; init; }
    public required int GeometryCount { get; init; }
}
