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
/// Published during timeline animation
/// </summary>
public class TimeChangedMessage
{
    public required DateTime Timestamp { get; init; }
    public required string TimeField { get; init; }
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
    public required string DrawingMode { get; init; }
    public Dictionary<string, object>? Style { get; init; }
}

/// <summary>
/// Request to stop drawing on the map
/// </summary>
public class StopDrawingRequestMessage
{
    public required string MapId { get; init; }
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
