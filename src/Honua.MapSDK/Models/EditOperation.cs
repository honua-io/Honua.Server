// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Models;

/// <summary>
/// Represents a single edit operation in an editing session
/// </summary>
public class EditOperation
{
    /// <summary>
    /// Unique identifier for this operation
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Type of edit operation
    /// </summary>
    public required EditOperationType Type { get; set; }

    /// <summary>
    /// The feature after the edit
    /// </summary>
    public required Feature Feature { get; set; }

    /// <summary>
    /// The feature state before the edit (for undo)
    /// </summary>
    public Feature? PreviousState { get; set; }

    /// <summary>
    /// When this operation was performed
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who performed the operation
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Layer ID where the edit occurred
    /// </summary>
    public string? LayerId { get; set; }

    /// <summary>
    /// Additional metadata about the operation
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Whether this operation has been synced to server
    /// </summary>
    public bool IsSynced { get; set; }

    /// <summary>
    /// Whether this operation can be undone
    /// </summary>
    public bool CanUndo => Type != EditOperationType.None;

    /// <summary>
    /// Description of the operation for display
    /// </summary>
    public string Description => Type switch
    {
        EditOperationType.Create => $"Created {Feature.GeometryType}",
        EditOperationType.Update => $"Updated {Feature.GeometryType}",
        EditOperationType.Delete => $"Deleted {Feature.GeometryType}",
        EditOperationType.Move => $"Moved {Feature.GeometryType}",
        EditOperationType.Reshape => $"Reshaped {Feature.GeometryType}",
        EditOperationType.Split => $"Split {Feature.GeometryType}",
        EditOperationType.Merge => $"Merged features",
        EditOperationType.AttributeUpdate => $"Updated attributes",
        _ => "Unknown operation"
    };
}

/// <summary>
/// Types of edit operations
/// </summary>
public enum EditOperationType
{
    /// <summary>
    /// No operation
    /// </summary>
    None,

    /// <summary>
    /// Create new feature
    /// </summary>
    Create,

    /// <summary>
    /// Update existing feature
    /// </summary>
    Update,

    /// <summary>
    /// Delete feature
    /// </summary>
    Delete,

    /// <summary>
    /// Move/translate feature
    /// </summary>
    Move,

    /// <summary>
    /// Reshape geometry (edit vertices)
    /// </summary>
    Reshape,

    /// <summary>
    /// Split feature into multiple
    /// </summary>
    Split,

    /// <summary>
    /// Merge multiple features
    /// </summary>
    Merge,

    /// <summary>
    /// Update only attributes (no geometry change)
    /// </summary>
    AttributeUpdate
}

/// <summary>
/// Represents a geographic feature with geometry and attributes
/// </summary>
public class Feature
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Layer this feature belongs to
    /// </summary>
    public string? LayerId { get; set; }

    /// <summary>
    /// Geometry type (Point, LineString, Polygon, etc.)
    /// </summary>
    public required string GeometryType { get; set; }

    /// <summary>
    /// GeoJSON geometry object
    /// </summary>
    public required object Geometry { get; set; }

    /// <summary>
    /// Feature attributes/properties
    /// </summary>
    public Dictionary<string, object> Attributes { get; set; } = new();

    /// <summary>
    /// Visual style for this feature
    /// </summary>
    public DrawingStyle? Style { get; set; }

    /// <summary>
    /// When feature was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When feature was last modified
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    /// Who created the feature
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Who last modified the feature
    /// </summary>
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Whether feature is locked from editing
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// Version number for conflict detection
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Clone this feature
    /// </summary>
    public Feature Clone()
    {
        return new Feature
        {
            Id = Id,
            LayerId = LayerId,
            GeometryType = GeometryType,
            Geometry = Geometry, // Note: shallow copy of geometry object
            Attributes = new Dictionary<string, object>(Attributes),
            Style = Style?.Clone(),
            CreatedAt = CreatedAt,
            ModifiedAt = ModifiedAt,
            CreatedBy = CreatedBy,
            ModifiedBy = ModifiedBy,
            IsLocked = IsLocked,
            Version = Version
        };
    }

    /// <summary>
    /// Convert to GeoJSON feature object
    /// </summary>
    public object ToGeoJson()
    {
        return new
        {
            type = "Feature",
            id = Id,
            geometry = Geometry,
            properties = Attributes
        };
    }
}
