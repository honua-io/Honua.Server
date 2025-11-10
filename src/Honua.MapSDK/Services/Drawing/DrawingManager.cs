// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json;
using Honua.MapSDK.Models;
using ModelDrawingStyle = Honua.MapSDK.Models.DrawingStyle;

namespace Honua.MapSDK.Services.Drawing;

/// <summary>
/// Core drawing service for managing map drawing operations
/// </summary>
public class DrawingManager : IDrawingManager
{
    private readonly List<DrawnGeometry> _geometries = new();
    private readonly Stack<DrawingOperation> _undoStack = new();
    private readonly Stack<DrawingOperation> _redoStack = new();
    private DrawnGeometry? _currentDrawing;
    private List<double[]> _currentCoordinates = new();

    public DrawingMode CurrentMode { get; private set; } = DrawingMode.None;
    public IReadOnlyList<DrawnGeometry> Geometries => _geometries.AsReadOnly();
    public DrawnGeometry? SelectedGeometry { get; private set; }
    public bool SnapToGrid { get; set; } = false;
    public bool SnapToVertices { get; set; } = true;
    public double GridSize { get; set; } = 10; // meters
    public double SnapDistance { get; set; } = 10; // pixels
    public DrawingStyle DefaultStyle { get; set; } = DrawingStyle.Defaults.Polygon;
    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    // Events
    public event EventHandler<DrawingStartedEventArgs>? DrawingStarted;
    public event EventHandler<DrawingCompletedEventArgs>? DrawingCompleted;
    public event EventHandler<DrawingCancelledEventArgs>? DrawingCancelled;
    public event EventHandler<GeometryEditedEventArgs>? GeometryEdited;
    public event EventHandler<GeometryDeletedEventArgs>? GeometryDeleted;
    public event EventHandler<GeometrySelectedEventArgs>? GeometrySelected;
    public event EventHandler<GeometryDeselectedEventArgs>? GeometryDeselected;

    public Task StartDrawingAsync(DrawingMode mode, DrawingStyle? style = null)
    {
        if (mode == DrawingMode.None)
            throw new ArgumentException("Invalid drawing mode", nameof(mode));

        CurrentMode = mode;
        _currentCoordinates.Clear();

        var drawingStyle = style ?? GetDefaultStyleForMode(mode);

        DrawingStarted?.Invoke(this, new DrawingStartedEventArgs
        {
            Mode = mode,
            Style = drawingStyle
        });

        return Task.CompletedTask;
    }

    public Task StopDrawingAsync()
    {
        CurrentMode = DrawingMode.None;
        _currentCoordinates.Clear();
        _currentDrawing = null;
        return Task.CompletedTask;
    }

    public Task AddCoordinateAsync(double longitude, double latitude)
    {
        if (CurrentMode == DrawingMode.None)
            throw new InvalidOperationException("Not in drawing mode");

        var coord = SnapToGrid ? SnapCoordinateToGrid(longitude, latitude) : new[] { longitude, latitude };

        if (SnapToVertices)
        {
            var snapped = TrySnapToVertex(coord[0], coord[1]);
            if (snapped != null)
                coord = snapped;
        }

        _currentCoordinates.Add(coord);
        return Task.CompletedTask;
    }

    public Task UndoLastCoordinateAsync()
    {
        if (_currentCoordinates.Count > 0)
        {
            _currentCoordinates.RemoveAt(_currentCoordinates.Count - 1);
        }
        return Task.CompletedTask;
    }

    public Task CompleteDrawingAsync()
    {
        if (CurrentMode == DrawingMode.None)
            throw new InvalidOperationException("Not in drawing mode");

        var geometry = CreateGeometryFromCoordinates(CurrentMode, _currentCoordinates);

        if (geometry != null)
        {
            _geometries.Add(geometry);
            RecordOperation(new DrawingOperation
            {
                Type = OperationType.Add,
                Geometry = geometry
            });

            DrawingCompleted?.Invoke(this, new DrawingCompletedEventArgs
            {
                Geometry = geometry
            });
        }

        CurrentMode = DrawingMode.None;
        _currentCoordinates.Clear();
        _currentDrawing = null;

        return Task.CompletedTask;
    }

    public Task CancelDrawingAsync()
    {
        var mode = CurrentMode;
        CurrentMode = DrawingMode.None;
        _currentCoordinates.Clear();
        _currentDrawing = null;

        DrawingCancelled?.Invoke(this, new DrawingCancelledEventArgs
        {
            Mode = mode
        });

        return Task.CompletedTask;
    }

    public Task EditGeometryAsync(string geometryId)
    {
        var geometry = GetGeometry(geometryId);
        if (geometry == null)
            throw new ArgumentException($"Geometry {geometryId} not found", nameof(geometryId));

        geometry.IsEditing = true;
        return SelectGeometryAsync(geometryId);
    }

    public Task UpdateVertexAsync(string geometryId, int vertexIndex, double longitude, double latitude)
    {
        var geometry = GetGeometry(geometryId);
        if (geometry == null)
            throw new ArgumentException($"Geometry {geometryId} not found", nameof(geometryId));

        // Update the coordinate in the geometry
        // This is simplified - real implementation would modify the coordinates array
        geometry.ModifiedAt = DateTime.UtcNow;

        RecordOperation(new DrawingOperation
        {
            Type = OperationType.Edit,
            Geometry = geometry,
            PreviousState = SerializeGeometry(geometry)
        });

        GeometryEdited?.Invoke(this, new GeometryEditedEventArgs
        {
            Geometry = geometry,
            ChangeType = "vertex_moved"
        });

        return Task.CompletedTask;
    }

    public Task AddVertexAsync(string geometryId, int insertAfterIndex, double longitude, double latitude)
    {
        var geometry = GetGeometry(geometryId);
        if (geometry == null)
            throw new ArgumentException($"Geometry {geometryId} not found", nameof(geometryId));

        geometry.ModifiedAt = DateTime.UtcNow;

        RecordOperation(new DrawingOperation
        {
            Type = OperationType.Edit,
            Geometry = geometry,
            PreviousState = SerializeGeometry(geometry)
        });

        GeometryEdited?.Invoke(this, new GeometryEditedEventArgs
        {
            Geometry = geometry,
            ChangeType = "vertex_added"
        });

        return Task.CompletedTask;
    }

    public Task RemoveVertexAsync(string geometryId, int vertexIndex)
    {
        var geometry = GetGeometry(geometryId);
        if (geometry == null)
            throw new ArgumentException($"Geometry {geometryId} not found", nameof(geometryId));

        geometry.ModifiedAt = DateTime.UtcNow;

        RecordOperation(new DrawingOperation
        {
            Type = OperationType.Edit,
            Geometry = geometry,
            PreviousState = SerializeGeometry(geometry)
        });

        GeometryEdited?.Invoke(this, new GeometryEditedEventArgs
        {
            Geometry = geometry,
            ChangeType = "vertex_removed"
        });

        return Task.CompletedTask;
    }

    public Task DeleteGeometryAsync(string geometryId)
    {
        var geometry = GetGeometry(geometryId);
        if (geometry == null)
            return Task.CompletedTask;

        _geometries.Remove(geometry);

        RecordOperation(new DrawingOperation
        {
            Type = OperationType.Delete,
            Geometry = geometry
        });

        GeometryDeleted?.Invoke(this, new GeometryDeletedEventArgs
        {
            GeometryId = geometryId
        });

        return Task.CompletedTask;
    }

    public Task DeleteGeometriesAsync(IEnumerable<string> geometryIds)
    {
        foreach (var id in geometryIds.ToList())
        {
            DeleteGeometryAsync(id).Wait();
        }
        return Task.CompletedTask;
    }

    public Task ClearAllAsync()
    {
        var allIds = _geometries.Select(g => g.Id).ToList();
        return DeleteGeometriesAsync(allIds);
    }

    public Task SelectGeometryAsync(string geometryId)
    {
        var geometry = GetGeometry(geometryId);
        if (geometry == null)
            throw new ArgumentException($"Geometry {geometryId} not found", nameof(geometryId));

        // Deselect previous
        if (SelectedGeometry != null)
        {
            SelectedGeometry.IsSelected = false;
        }

        SelectedGeometry = geometry;
        geometry.IsSelected = true;

        GeometrySelected?.Invoke(this, new GeometrySelectedEventArgs
        {
            Geometry = geometry
        });

        return Task.CompletedTask;
    }

    public Task DeselectGeometryAsync()
    {
        if (SelectedGeometry == null)
            return Task.CompletedTask;

        var id = SelectedGeometry.Id;
        SelectedGeometry.IsSelected = false;
        SelectedGeometry.IsEditing = false;
        SelectedGeometry = null;

        GeometryDeselected?.Invoke(this, new GeometryDeselectedEventArgs
        {
            GeometryId = id
        });

        return Task.CompletedTask;
    }

    public Task UpdateGeometryStyleAsync(string geometryId, DrawingStyle style)
    {
        var geometry = GetGeometry(geometryId);
        if (geometry == null)
            throw new ArgumentException($"Geometry {geometryId} not found", nameof(geometryId));

        geometry.Style = style;
        geometry.ModifiedAt = DateTime.UtcNow;

        return Task.CompletedTask;
    }

    public Task UpdateGeometryPropertiesAsync(string geometryId, Dictionary<string, object> properties)
    {
        var geometry = GetGeometry(geometryId);
        if (geometry == null)
            throw new ArgumentException($"Geometry {geometryId} not found", nameof(geometryId));

        geometry.Properties = new Dictionary<string, object>(properties);
        geometry.ModifiedAt = DateTime.UtcNow;

        return Task.CompletedTask;
    }

    public Task UndoAsync()
    {
        if (!CanUndo)
            return Task.CompletedTask;

        var operation = _undoStack.Pop();
        _redoStack.Push(operation);

        switch (operation.Type)
        {
            case OperationType.Add:
                _geometries.Remove(operation.Geometry);
                break;
            case OperationType.Delete:
                _geometries.Add(operation.Geometry);
                break;
            case OperationType.Edit:
                // Restore previous state
                if (operation.PreviousState != null)
                {
                    // Deserialize and restore
                }
                break;
        }

        return Task.CompletedTask;
    }

    public Task RedoAsync()
    {
        if (!CanRedo)
            return Task.CompletedTask;

        var operation = _redoStack.Pop();
        _undoStack.Push(operation);

        switch (operation.Type)
        {
            case OperationType.Add:
                _geometries.Add(operation.Geometry);
                break;
            case OperationType.Delete:
                _geometries.Remove(operation.Geometry);
                break;
            case OperationType.Edit:
                // Re-apply the edit
                break;
        }

        return Task.CompletedTask;
    }

    public string ExportToGeoJson(bool formatted = true)
    {
        return ExportToGeoJson(_geometries.Select(g => g.Id), formatted);
    }

    public string ExportToGeoJson(IEnumerable<string> geometryIds, bool formatted = true)
    {
        var features = geometryIds
            .Select(id => GetGeometry(id))
            .Where(g => g != null)
            .Select(g => g!.ToGeoJsonFeature())
            .ToList();

        var featureCollection = new GeoJsonFeatureCollection
        {
            Features = features
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = formatted
        };

        return JsonSerializer.Serialize(featureCollection, options);
    }

    public string ExportToWkt()
    {
        return ExportToWkt(_geometries.Select(g => g.Id));
    }

    public string ExportToWkt(IEnumerable<string> geometryIds)
    {
        var sb = new StringBuilder();

        foreach (var id in geometryIds)
        {
            var geometry = GetGeometry(id);
            if (geometry != null)
            {
                sb.AppendLine(ConvertToWkt(geometry));
            }
        }

        return sb.ToString();
    }

    public async Task<List<DrawnGeometry>> ImportFromGeoJsonAsync(string geoJson)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var featureCollection = JsonSerializer.Deserialize<GeoJsonFeatureCollection>(geoJson, options);
        if (featureCollection == null)
            throw new ArgumentException("Invalid GeoJSON", nameof(geoJson));

        var imported = new List<DrawnGeometry>();

        foreach (var feature in featureCollection.Features)
        {
            var geometry = CreateGeometryFromGeoJson(feature);
            if (geometry != null)
            {
                _geometries.Add(geometry);
                imported.Add(geometry);
            }
        }

        return imported;
    }

    public Task<List<DrawnGeometry>> ImportFromWktAsync(string wkt, DrawingStyle? style = null)
    {
        // Parse WKT and create geometries
        // This would use NetTopologySuite in a real implementation
        var imported = new List<DrawnGeometry>();
        return Task.FromResult(imported);
    }

    public DrawnGeometry? GetGeometry(string geometryId)
    {
        return _geometries.FirstOrDefault(g => g.Id == geometryId);
    }

    public bool HasGeometry(string geometryId)
    {
        return _geometries.Any(g => g.Id == geometryId);
    }

    // Private helper methods

    private DrawingStyle GetDefaultStyleForMode(DrawingMode mode)
    {
        return mode switch
        {
            DrawingMode.Point => DrawingStyle.Defaults.Point,
            DrawingMode.Line => DrawingStyle.Defaults.Line,
            DrawingMode.Polygon => DrawingStyle.Defaults.Polygon,
            DrawingMode.Rectangle => DrawingStyle.Defaults.Rectangle,
            DrawingMode.Circle => DrawingStyle.Defaults.Circle,
            _ => DefaultStyle
        };
    }

    private DrawnGeometry? CreateGeometryFromCoordinates(DrawingMode mode, List<double[]> coordinates)
    {
        if (coordinates.Count == 0)
            return null;

        GeometryType geoType;
        object coords;
        double? radius = null;

        switch (mode)
        {
            case DrawingMode.Point:
                if (coordinates.Count == 0) return null;
                geoType = GeometryType.Point;
                coords = coordinates[0];
                break;

            case DrawingMode.Line:
                if (coordinates.Count < 2) return null;
                geoType = GeometryType.LineString;
                coords = coordinates;
                break;

            case DrawingMode.Polygon:
                if (coordinates.Count < 3) return null;
                geoType = GeometryType.Polygon;
                // Close the polygon
                var polyCoords = new List<double[]>(coordinates);
                if (!AreCoordinatesEqual(polyCoords[0], polyCoords[^1]))
                {
                    polyCoords.Add(polyCoords[0]);
                }
                coords = new[] { polyCoords };
                break;

            case DrawingMode.Rectangle:
                if (coordinates.Count < 2) return null;
                geoType = GeometryType.Polygon;
                coords = CreateRectangleCoordinates(coordinates[0], coordinates[1]);
                break;

            case DrawingMode.Circle:
                if (coordinates.Count < 2) return null;
                geoType = GeometryType.Circle;
                coords = coordinates[0];
                radius = CalculateDistance(coordinates[0], coordinates[1]);
                break;

            default:
                return null;
        }

        return new DrawnGeometry
        {
            Type = geoType,
            Geometry = new GeoJsonGeometry
            {
                Type = geoType == GeometryType.Circle ? "Point" : geoType.ToString(),
                Coordinates = coords,
                Radius = radius
            },
            Style = GetDefaultStyleForMode(mode)
        };
    }

    private DrawnGeometry? CreateGeometryFromGeoJson(GeoJsonFeature feature)
    {
        var typeStr = feature.Geometry.Type;
        var geoType = Enum.TryParse<GeometryType>(typeStr, out var parsed) ? parsed : GeometryType.Point;

        return new DrawnGeometry
        {
            Id = feature.Id ?? Guid.NewGuid().ToString(),
            Type = geoType,
            Geometry = feature.Geometry,
            Name = feature.Properties.TryGetValue("name", out var name) ? name?.ToString() : null,
            Description = feature.Properties.TryGetValue("description", out var desc) ? desc?.ToString() : null,
            Properties = feature.Properties,
            Style = DefaultStyle.Clone()
        };
    }

    private double[] SnapCoordinateToGrid(double longitude, double latitude)
    {
        // Simplified grid snapping - real implementation would use proper projection
        var gridSizeDegrees = GridSize / 111320.0; // Approximate meters to degrees at equator
        var snappedLon = Math.Round(longitude / gridSizeDegrees) * gridSizeDegrees;
        var snappedLat = Math.Round(latitude / gridSizeDegrees) * gridSizeDegrees;
        return new[] { snappedLon, snappedLat };
    }

    private double[]? TrySnapToVertex(double longitude, double latitude)
    {
        // Try to snap to nearby vertices in existing geometries
        // This would require calculating distance in pixels based on current zoom
        return null;
    }

    private object CreateRectangleCoordinates(double[] start, double[] end)
    {
        var coords = new List<double[]>
        {
            new[] { start[0], start[1] },
            new[] { end[0], start[1] },
            new[] { end[0], end[1] },
            new[] { start[0], end[1] },
            new[] { start[0], start[1] }
        };
        return new[] { coords };
    }

    private double CalculateDistance(double[] coord1, double[] coord2)
    {
        // Haversine formula for distance between two coordinates
        var R = 6371000; // Earth radius in meters
        var lat1 = coord1[1] * Math.PI / 180;
        var lat2 = coord2[1] * Math.PI / 180;
        var deltaLat = (coord2[1] - coord1[1]) * Math.PI / 180;
        var deltaLon = (coord2[0] - coord1[0]) * Math.PI / 180;

        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) *
                Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return R * c;
    }

    private bool AreCoordinatesEqual(double[] coord1, double[] coord2)
    {
        return Math.Abs(coord1[0] - coord2[0]) < 0.0000001 &&
               Math.Abs(coord1[1] - coord2[1]) < 0.0000001;
    }

    private string SerializeGeometry(DrawnGeometry geometry)
    {
        return JsonSerializer.Serialize(geometry);
    }

    private string ConvertToWkt(DrawnGeometry geometry)
    {
        // Convert geometry to WKT format
        // This would use NetTopologySuite in a real implementation
        return $"{geometry.Type.ToString().ToUpper()}(...)";
    }

    private void RecordOperation(DrawingOperation operation)
    {
        _undoStack.Push(operation);
        _redoStack.Clear(); // Clear redo stack when new operation is recorded
    }
}

// Internal classes for undo/redo

internal class DrawingOperation
{
    public required OperationType Type { get; init; }
    public required DrawnGeometry Geometry { get; init; }
    public string? PreviousState { get; init; }
}

internal enum OperationType
{
    Add,
    Edit,
    Delete
}
