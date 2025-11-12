// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using NetTopologySuite.Geometries;

namespace Honua.Server.Core.Models.Ifc;

/// <summary>
/// Options for importing IFC files into Honua
/// </summary>
public class IfcImportOptions
{
    /// <summary>
    /// Whether to import 3D geometry from IFC elements
    /// </summary>
    public bool ImportGeometry { get; set; } = true;

    /// <summary>
    /// Whether to import properties and property sets
    /// </summary>
    public bool ImportProperties { get; set; } = true;

    /// <summary>
    /// Whether to import spatial relationships (building structure, containment)
    /// </summary>
    public bool ImportRelationships { get; set; } = true;

    /// <summary>
    /// Whether to create graph database relationships (requires Apache AGE)
    /// </summary>
    public bool CreateGraphRelationships { get; set; } = false;

    /// <summary>
    /// Target layer ID to store imported features
    /// </summary>
    public string? TargetLayerId { get; set; }

    /// <summary>
    /// Target service ID to store imported features
    /// </summary>
    public string? TargetServiceId { get; set; }

    /// <summary>
    /// Coordinate transformation options for georeferencing
    /// </summary>
    public CoordinateTransformOptions? GeoReference { get; set; }

    /// <summary>
    /// Filter to import only specific IFC entity types (e.g., IfcWall, IfcDoor)
    /// If null or empty, imports all supported types
    /// </summary>
    public List<string>? EntityTypeFilter { get; set; }

    /// <summary>
    /// Maximum number of entities to import (for testing/preview)
    /// </summary>
    public int? MaxEntities { get; set; }

    /// <summary>
    /// Whether to generate simplified geometry for performance
    /// </summary>
    public bool SimplifyGeometry { get; set; } = false;

    /// <summary>
    /// Tolerance for geometry simplification (in meters)
    /// </summary>
    public double SimplificationTolerance { get; set; } = 0.01;
}

/// <summary>
/// Coordinate transformation options for georeferencing IFC models
/// </summary>
public class CoordinateTransformOptions
{
    /// <summary>
    /// Target coordinate reference system (EPSG code)
    /// </summary>
    public int TargetSrid { get; set; } = 4326; // WGS84 default

    /// <summary>
    /// Translation offset in X direction (eastings/longitude)
    /// </summary>
    public double OffsetX { get; set; }

    /// <summary>
    /// Translation offset in Y direction (northings/latitude)
    /// </summary>
    public double OffsetY { get; set; }

    /// <summary>
    /// Translation offset in Z direction (elevation)
    /// </summary>
    public double OffsetZ { get; set; }

    /// <summary>
    /// Rotation angle in degrees (clockwise from north)
    /// </summary>
    public double RotationDegrees { get; set; }

    /// <summary>
    /// Scale factor for coordinate conversion
    /// </summary>
    public double Scale { get; set; } = 1.0;
}

/// <summary>
/// Result of an IFC import operation
/// </summary>
public class IfcImportResult
{
    /// <summary>
    /// Unique identifier for this import job
    /// </summary>
    public Guid ImportJobId { get; set; }

    /// <summary>
    /// Number of features created in Honua
    /// </summary>
    public int FeaturesCreated { get; set; }

    /// <summary>
    /// Number of graph relationships created (if graph import enabled)
    /// </summary>
    public int RelationshipsCreated { get; set; }

    /// <summary>
    /// Count of entities by IFC type
    /// </summary>
    public Dictionary<string, int> EntityTypeCounts { get; set; } = new();

    /// <summary>
    /// Warnings encountered during import
    /// </summary>
    public List<ImportWarning> Warnings { get; set; } = new();

    /// <summary>
    /// Errors encountered during import
    /// </summary>
    public List<ImportError> Errors { get; set; } = new();

    /// <summary>
    /// 3D bounding box of the imported project
    /// </summary>
    public BoundingBox3D? ProjectExtent { get; set; }

    /// <summary>
    /// IFC project metadata
    /// </summary>
    public IfcProjectMetadata? ProjectMetadata { get; set; }

    /// <summary>
    /// Import start time
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Import end time
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Total duration of import operation
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>
    /// Whether the import was successful
    /// </summary>
    public bool Success => Errors.Count == 0;
}

/// <summary>
/// Warning message from IFC import
/// </summary>
public class ImportWarning
{
    /// <summary>
    /// IFC entity ID that caused the warning
    /// </summary>
    public string? EntityId { get; set; }

    /// <summary>
    /// IFC entity type
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Warning message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Warning severity
    /// </summary>
    public WarningSeverity Severity { get; set; } = WarningSeverity.Low;
}

/// <summary>
/// Error message from IFC import
/// </summary>
public class ImportError
{
    /// <summary>
    /// IFC entity ID that caused the error
    /// </summary>
    public string? EntityId { get; set; }

    /// <summary>
    /// IFC entity type
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Error message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Stack trace for debugging
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Whether this error is fatal (stops import)
    /// </summary>
    public bool IsFatal { get; set; }
}

/// <summary>
/// Warning severity levels
/// </summary>
public enum WarningSeverity
{
    Low,
    Medium,
    High
}

/// <summary>
/// 3D bounding box
/// </summary>
public class BoundingBox3D
{
    /// <summary>
    /// Minimum X coordinate
    /// </summary>
    public double MinX { get; set; }

    /// <summary>
    /// Minimum Y coordinate
    /// </summary>
    public double MinY { get; set; }

    /// <summary>
    /// Minimum Z coordinate
    /// </summary>
    public double MinZ { get; set; }

    /// <summary>
    /// Maximum X coordinate
    /// </summary>
    public double MaxX { get; set; }

    /// <summary>
    /// Maximum Y coordinate
    /// </summary>
    public double MaxY { get; set; }

    /// <summary>
    /// Maximum Z coordinate
    /// </summary>
    public double MaxZ { get; set; }

    /// <summary>
    /// Width (X dimension)
    /// </summary>
    public double Width => MaxX - MinX;

    /// <summary>
    /// Depth (Y dimension)
    /// </summary>
    public double Depth => MaxY - MinY;

    /// <summary>
    /// Height (Z dimension)
    /// </summary>
    public double Height => MaxZ - MinZ;

    /// <summary>
    /// Center point of the bounding box
    /// </summary>
    public (double X, double Y, double Z) Center =>
        ((MinX + MaxX) / 2, (MinY + MaxY) / 2, (MinZ + MaxZ) / 2);

    /// <summary>
    /// Convert to NetTopologySuite Envelope (2D projection)
    /// </summary>
    public Envelope ToEnvelope() => new Envelope(MinX, MaxX, MinY, MaxY);
}

/// <summary>
/// IFC project metadata extracted from the file
/// </summary>
public class IfcProjectMetadata
{
    /// <summary>
    /// IFC schema version (IFC2x3, IFC4, IFC4x3, etc.)
    /// </summary>
    public string SchemaVersion { get; set; } = string.Empty;

    /// <summary>
    /// Project name
    /// </summary>
    public string? ProjectName { get; set; }

    /// <summary>
    /// Project description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Project phase (e.g., "Design", "Construction", "As-Built")
    /// </summary>
    public string? Phase { get; set; }

    /// <summary>
    /// Building name(s)
    /// </summary>
    public List<string> BuildingNames { get; set; } = new();

    /// <summary>
    /// Site name
    /// </summary>
    public string? SiteName { get; set; }

    /// <summary>
    /// Site location (latitude/longitude if available)
    /// </summary>
    public SiteLocation? SiteLocation { get; set; }

    /// <summary>
    /// Authoring application that created the IFC file
    /// </summary>
    public string? AuthoringApplication { get; set; }

    /// <summary>
    /// Timestamp when the IFC file was created
    /// </summary>
    public DateTime? CreatedDate { get; set; }

    /// <summary>
    /// Organization that created the file
    /// </summary>
    public string? Organization { get; set; }

    /// <summary>
    /// Person who created the file
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Unit of measurement for length
    /// </summary>
    public string? LengthUnit { get; set; }

    /// <summary>
    /// Unit of measurement for area
    /// </summary>
    public string? AreaUnit { get; set; }

    /// <summary>
    /// Unit of measurement for volume
    /// </summary>
    public string? VolumeUnit { get; set; }

    /// <summary>
    /// Total number of spatial elements in the file
    /// </summary>
    public int TotalSpatialElements { get; set; }

    /// <summary>
    /// Total number of building elements in the file
    /// </summary>
    public int TotalBuildingElements { get; set; }
}

/// <summary>
/// Geographic location of a site
/// </summary>
public class SiteLocation
{
    /// <summary>
    /// Latitude in decimal degrees
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Longitude in decimal degrees
    /// </summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Elevation above sea level in meters
    /// </summary>
    public double? Elevation { get; set; }

    /// <summary>
    /// Address of the site
    /// </summary>
    public string? Address { get; set; }
}

/// <summary>
/// Validation result for an IFC file
/// </summary>
public class IfcValidationResult
{
    /// <summary>
    /// Whether the file is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// IFC schema version detected
    /// </summary>
    public string? SchemaVersion { get; set; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Number of entities in the file
    /// </summary>
    public int EntityCount { get; set; }

    /// <summary>
    /// Validation errors
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Validation warnings
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// File format (STEP, XML, ZIP)
    /// </summary>
    public string? FileFormat { get; set; }
}

/// <summary>
/// Represents an IFC entity mapped to Honua feature
/// </summary>
public class IfcEntityMapping
{
    /// <summary>
    /// IFC entity ID (e.g., #123)
    /// </summary>
    public string IfcEntityId { get; set; } = string.Empty;

    /// <summary>
    /// IFC entity type (e.g., IfcWall, IfcDoor)
    /// </summary>
    public string IfcEntityType { get; set; } = string.Empty;

    /// <summary>
    /// Honua feature ID (GUID)
    /// </summary>
    public Guid HonuaFeatureId { get; set; }

    /// <summary>
    /// Name/label of the entity
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Global ID (IfcGloballyUniqueId)
    /// </summary>
    public string? GlobalId { get; set; }

    /// <summary>
    /// Properties extracted from IFC
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = new();

    /// <summary>
    /// Geometry (if available)
    /// </summary>
    public Geometry? Geometry { get; set; }

    /// <summary>
    /// Parent entity ID (for hierarchical relationships)
    /// </summary>
    public string? ParentIfcEntityId { get; set; }

    /// <summary>
    /// Related entity IDs
    /// </summary>
    public List<string> RelatedEntityIds { get; set; } = new();
}
