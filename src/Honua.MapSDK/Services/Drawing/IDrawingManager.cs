// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.MapSDK.Models;

namespace Honua.MapSDK.Services.Drawing;

/// <summary>
/// Interface for managing drawing operations on the map
/// </summary>
public interface IDrawingManager
{
    /// <summary>
    /// Currently active drawing mode
    /// </summary>
    DrawingMode CurrentMode { get; }

    /// <summary>
    /// All drawn geometries
    /// </summary>
    IReadOnlyList<DrawnGeometry> Geometries { get; }

    /// <summary>
    /// Currently selected geometry for editing
    /// </summary>
    DrawnGeometry? SelectedGeometry { get; }

    /// <summary>
    /// Whether snap to grid is enabled
    /// </summary>
    bool SnapToGrid { get; set; }

    /// <summary>
    /// Whether snap to vertices is enabled
    /// </summary>
    bool SnapToVertices { get; set; }

    /// <summary>
    /// Grid size in meters for snapping
    /// </summary>
    double GridSize { get; set; }

    /// <summary>
    /// Snap distance in pixels
    /// </summary>
    double SnapDistance { get; set; }

    /// <summary>
    /// Default style for new geometries
    /// </summary>
    DrawingStyle DefaultStyle { get; set; }

    /// <summary>
    /// Start drawing in the specified mode
    /// </summary>
    Task StartDrawingAsync(DrawingMode mode, DrawingStyle? style = null);

    /// <summary>
    /// Stop the current drawing operation
    /// </summary>
    Task StopDrawingAsync();

    /// <summary>
    /// Add a coordinate to the current drawing
    /// </summary>
    Task AddCoordinateAsync(double longitude, double latitude);

    /// <summary>
    /// Remove the last coordinate from the current drawing
    /// </summary>
    Task UndoLastCoordinateAsync();

    /// <summary>
    /// Complete the current drawing
    /// </summary>
    Task CompleteDrawingAsync();

    /// <summary>
    /// Cancel the current drawing
    /// </summary>
    Task CancelDrawingAsync();

    /// <summary>
    /// Edit an existing geometry
    /// </summary>
    Task EditGeometryAsync(string geometryId);

    /// <summary>
    /// Update a vertex position during editing
    /// </summary>
    Task UpdateVertexAsync(string geometryId, int vertexIndex, double longitude, double latitude);

    /// <summary>
    /// Add a vertex to an existing geometry
    /// </summary>
    Task AddVertexAsync(string geometryId, int insertAfterIndex, double longitude, double latitude);

    /// <summary>
    /// Remove a vertex from an existing geometry
    /// </summary>
    Task RemoveVertexAsync(string geometryId, int vertexIndex);

    /// <summary>
    /// Delete a geometry
    /// </summary>
    Task DeleteGeometryAsync(string geometryId);

    /// <summary>
    /// Delete multiple geometries
    /// </summary>
    Task DeleteGeometriesAsync(IEnumerable<string> geometryIds);

    /// <summary>
    /// Clear all geometries
    /// </summary>
    Task ClearAllAsync();

    /// <summary>
    /// Select a geometry for editing
    /// </summary>
    Task SelectGeometryAsync(string geometryId);

    /// <summary>
    /// Deselect the currently selected geometry
    /// </summary>
    Task DeselectGeometryAsync();

    /// <summary>
    /// Update the style of a geometry
    /// </summary>
    Task UpdateGeometryStyleAsync(string geometryId, DrawingStyle style);

    /// <summary>
    /// Update the properties of a geometry
    /// </summary>
    Task UpdateGeometryPropertiesAsync(string geometryId, Dictionary<string, object> properties);

    /// <summary>
    /// Undo the last operation
    /// </summary>
    Task UndoAsync();

    /// <summary>
    /// Redo the last undone operation
    /// </summary>
    Task RedoAsync();

    /// <summary>
    /// Check if undo is available
    /// </summary>
    bool CanUndo { get; }

    /// <summary>
    /// Check if redo is available
    /// </summary>
    bool CanRedo { get; }

    /// <summary>
    /// Export all geometries to GeoJSON
    /// </summary>
    string ExportToGeoJson(bool formatted = true);

    /// <summary>
    /// Export specific geometries to GeoJSON
    /// </summary>
    string ExportToGeoJson(IEnumerable<string> geometryIds, bool formatted = true);

    /// <summary>
    /// Export all geometries to WKT
    /// </summary>
    string ExportToWkt();

    /// <summary>
    /// Export specific geometries to WKT
    /// </summary>
    string ExportToWkt(IEnumerable<string> geometryIds);

    /// <summary>
    /// Import geometries from GeoJSON
    /// </summary>
    Task<List<DrawnGeometry>> ImportFromGeoJsonAsync(string geoJson);

    /// <summary>
    /// Import geometries from WKT
    /// </summary>
    Task<List<DrawnGeometry>> ImportFromWktAsync(string wkt, DrawingStyle? style = null);

    /// <summary>
    /// Get a geometry by ID
    /// </summary>
    DrawnGeometry? GetGeometry(string geometryId);

    /// <summary>
    /// Check if a geometry exists
    /// </summary>
    bool HasGeometry(string geometryId);

    // Events
    /// <summary>
    /// Fired when drawing is started
    /// </summary>
    event EventHandler<DrawingStartedEventArgs>? DrawingStarted;

    /// <summary>
    /// Fired when drawing is completed
    /// </summary>
    event EventHandler<DrawingCompletedEventArgs>? DrawingCompleted;

    /// <summary>
    /// Fired when drawing is cancelled
    /// </summary>
    event EventHandler<DrawingCancelledEventArgs>? DrawingCancelled;

    /// <summary>
    /// Fired when a geometry is edited
    /// </summary>
    event EventHandler<GeometryEditedEventArgs>? GeometryEdited;

    /// <summary>
    /// Fired when a geometry is deleted
    /// </summary>
    event EventHandler<GeometryDeletedEventArgs>? GeometryDeleted;

    /// <summary>
    /// Fired when a geometry is selected
    /// </summary>
    event EventHandler<GeometrySelectedEventArgs>? GeometrySelected;

    /// <summary>
    /// Fired when a geometry is deselected
    /// </summary>
    event EventHandler<GeometryDeselectedEventArgs>? GeometryDeselected;
}

/// <summary>
/// Drawing modes supported by the drawing manager
/// </summary>
public enum DrawingMode
{
    None,
    Point,
    Line,
    Polygon,
    Rectangle,
    Circle,
    Freehand
}

// Event argument classes
public class DrawingStartedEventArgs : EventArgs
{
    public required DrawingMode Mode { get; init; }
    public required DrawingStyle Style { get; init; }
}

public class DrawingCompletedEventArgs : EventArgs
{
    public required DrawnGeometry Geometry { get; init; }
}

public class DrawingCancelledEventArgs : EventArgs
{
    public required DrawingMode Mode { get; init; }
}

public class GeometryEditedEventArgs : EventArgs
{
    public required DrawnGeometry Geometry { get; init; }
    public required string ChangeType { get; init; } // "vertex_moved", "vertex_added", "vertex_removed", etc.
}

public class GeometryDeletedEventArgs : EventArgs
{
    public required string GeometryId { get; init; }
}

public class GeometrySelectedEventArgs : EventArgs
{
    public required DrawnGeometry Geometry { get; init; }
}

public class GeometryDeselectedEventArgs : EventArgs
{
    public required string GeometryId { get; init; }
}
