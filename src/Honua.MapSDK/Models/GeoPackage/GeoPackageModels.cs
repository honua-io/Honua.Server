// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Models.GeoPackage;

/// <summary>
/// Represents metadata and structure information about a GeoPackage file
/// </summary>
public class GeoPackageInfo
{
    /// <summary>
    /// File name of the GeoPackage
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// GPKG application ID (should be 0x47504B47 for valid GeoPackage)
    /// </summary>
    public int ApplicationId { get; set; }

    /// <summary>
    /// GPKG user version (e.g., 10200 for version 1.2.0)
    /// </summary>
    public int UserVersion { get; set; }

    /// <summary>
    /// List of layers/tables in the GeoPackage
    /// </summary>
    public List<GpkgLayer> Layers { get; set; } = new();

    /// <summary>
    /// Spatial reference systems defined in the package
    /// </summary>
    public List<GpkgSpatialReference> SpatialReferences { get; set; } = new();

    /// <summary>
    /// Overall bounding box of all layers (if available)
    /// </summary>
    public double[]? BoundingBox { get; set; }

    /// <summary>
    /// When the file was parsed
    /// </summary>
    public DateTime ParsedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the version string (e.g., "1.2.0")
    /// </summary>
    public string Version
    {
        get
        {
            if (UserVersion == 0) return "Unknown";
            var major = UserVersion / 10000;
            var minor = (UserVersion / 100) % 100;
            var patch = UserVersion % 100;
            return $"{major}.{minor}.{patch}";
        }
    }

    /// <summary>
    /// Total number of features across all feature layers
    /// </summary>
    public int TotalFeatures => Layers.Where(l => l.DataType == "features").Sum(l => l.FeatureCount);

    /// <summary>
    /// Total number of tile layers
    /// </summary>
    public int TotalTileLayers => Layers.Count(l => l.DataType == "tiles");
}

/// <summary>
/// Represents a layer (features or tiles) within a GeoPackage
/// </summary>
public class GpkgLayer
{
    /// <summary>
    /// Table name in the GeoPackage database
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable identifier
    /// </summary>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>
    /// Layer description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Data type: "features", "tiles", "attributes"
    /// </summary>
    public string DataType { get; set; } = "features";

    /// <summary>
    /// Geometry column name (for feature layers)
    /// </summary>
    public string? GeometryColumn { get; set; }

    /// <summary>
    /// Geometry type name (POINT, LINESTRING, POLYGON, etc.)
    /// </summary>
    public string? GeometryType { get; set; }

    /// <summary>
    /// Spatial Reference System ID
    /// </summary>
    public int SrsId { get; set; }

    /// <summary>
    /// Has Z coordinates
    /// </summary>
    public bool HasZ { get; set; }

    /// <summary>
    /// Has M (measure) coordinates
    /// </summary>
    public bool HasM { get; set; }

    /// <summary>
    /// Bounding box [minX, minY, maxX, maxY]
    /// </summary>
    public double[]? BoundingBox { get; set; }

    /// <summary>
    /// Number of features in the layer
    /// </summary>
    public int FeatureCount { get; set; }

    /// <summary>
    /// Attribute field definitions
    /// </summary>
    public List<GpkgField> Fields { get; set; } = new();

    /// <summary>
    /// Last change timestamp
    /// </summary>
    public DateTime? LastChange { get; set; }

    /// <summary>
    /// Layer-specific statistics
    /// </summary>
    public LayerStatistics Statistics { get; set; } = new();

    /// <summary>
    /// Tile matrix set (for tile layers)
    /// </summary>
    public string? TileMatrixSet { get; set; }

    /// <summary>
    /// Available zoom levels (for tile layers)
    /// </summary>
    public int[]? ZoomLevels { get; set; }
}

/// <summary>
/// Represents a field/column in a GeoPackage feature layer
/// </summary>
public class GpkgField
{
    /// <summary>
    /// Field name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// SQLite data type (TEXT, INTEGER, REAL, BLOB)
    /// </summary>
    public string Type { get; set; } = "TEXT";

    /// <summary>
    /// Is this field nullable
    /// </summary>
    public bool Nullable { get; set; } = true;

    /// <summary>
    /// Is this field a primary key
    /// </summary>
    public bool IsPrimaryKey { get; set; }

    /// <summary>
    /// Default value
    /// </summary>
    public string? DefaultValue { get; set; }
}

/// <summary>
/// Spatial Reference System information from GeoPackage
/// </summary>
public class GpkgSpatialReference
{
    /// <summary>
    /// SRS ID (typically EPSG code)
    /// </summary>
    public int SrsId { get; set; }

    /// <summary>
    /// SRS name
    /// </summary>
    public string SrsName { get; set; } = string.Empty;

    /// <summary>
    /// Organization (e.g., "EPSG")
    /// </summary>
    public string Organization { get; set; } = string.Empty;

    /// <summary>
    /// Organization coordsys ID
    /// </summary>
    public int OrganizationCoordsysId { get; set; }

    /// <summary>
    /// WKT or other definition
    /// </summary>
    public string Definition { get; set; } = string.Empty;

    /// <summary>
    /// Description of the SRS
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Content metadata from gpkg_contents table
/// </summary>
public class GpkgContents
{
    /// <summary>
    /// Table name
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// Data type
    /// </summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>
    /// Identifier
    /// </summary>
    public string? Identifier { get; set; }

    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Last change timestamp
    /// </summary>
    public string? LastChange { get; set; }

    /// <summary>
    /// Minimum X coordinate
    /// </summary>
    public double? MinX { get; set; }

    /// <summary>
    /// Minimum Y coordinate
    /// </summary>
    public double? MinY { get; set; }

    /// <summary>
    /// Maximum X coordinate
    /// </summary>
    public double? MaxX { get; set; }

    /// <summary>
    /// Maximum Y coordinate
    /// </summary>
    public double? MaxY { get; set; }

    /// <summary>
    /// Spatial Reference System ID
    /// </summary>
    public int? SrsId { get; set; }
}

/// <summary>
/// Statistics about a layer
/// </summary>
public class LayerStatistics
{
    /// <summary>
    /// Total number of features/rows
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Number of features with null/empty geometry
    /// </summary>
    public int NullGeometryCount { get; set; }

    /// <summary>
    /// Extent/bounding box [minX, minY, maxX, maxY]
    /// </summary>
    public double[]? Extent { get; set; }

    /// <summary>
    /// Geometry types found in the layer
    /// </summary>
    public Dictionary<string, int> GeometryTypeCounts { get; set; } = new();

    /// <summary>
    /// Attribute statistics by field name
    /// </summary>
    public Dictionary<string, FieldStatistics> FieldStats { get; set; } = new();
}

/// <summary>
/// Statistics for a single field
/// </summary>
public class FieldStatistics
{
    /// <summary>
    /// Number of null values
    /// </summary>
    public int NullCount { get; set; }

    /// <summary>
    /// Number of unique values (if calculated)
    /// </summary>
    public int? UniqueCount { get; set; }

    /// <summary>
    /// Minimum value (for numeric fields)
    /// </summary>
    public double? MinValue { get; set; }

    /// <summary>
    /// Maximum value (for numeric fields)
    /// </summary>
    public double? MaxValue { get; set; }

    /// <summary>
    /// Average value (for numeric fields)
    /// </summary>
    public double? AvgValue { get; set; }
}

/// <summary>
/// GeoPackage geometry header information
/// </summary>
public class GpkgGeometryHeader
{
    /// <summary>
    /// Magic bytes ('GP')
    /// </summary>
    public byte[] Magic { get; set; } = new byte[] { 0x47, 0x50 };

    /// <summary>
    /// Version
    /// </summary>
    public byte Version { get; set; }

    /// <summary>
    /// Flags byte
    /// </summary>
    public byte Flags { get; set; }

    /// <summary>
    /// SRID
    /// </summary>
    public int SrsId { get; set; }

    /// <summary>
    /// Envelope (bounding box) if present
    /// </summary>
    public double[]? Envelope { get; set; }

    /// <summary>
    /// Is little endian
    /// </summary>
    public bool IsLittleEndian => (Flags & 0x01) == 0x01;

    /// <summary>
    /// Has envelope
    /// </summary>
    public bool HasEnvelope => (Flags & 0x0E) != 0;
}

/// <summary>
/// Request to load features from a GeoPackage layer
/// </summary>
public class GpkgFeatureRequest
{
    /// <summary>
    /// Layer/table name to query
    /// </summary>
    public string LayerName { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of features to return
    /// </summary>
    public int? MaxFeatures { get; set; }

    /// <summary>
    /// Spatial filter (bounding box: [minX, minY, maxX, maxY])
    /// </summary>
    public double[]? BoundingBox { get; set; }

    /// <summary>
    /// Attribute filter (WHERE clause without the WHERE keyword)
    /// </summary>
    public string? AttributeFilter { get; set; }

    /// <summary>
    /// Fields to include (null = all fields)
    /// </summary>
    public string[]? Fields { get; set; }

    /// <summary>
    /// Offset for pagination
    /// </summary>
    public int Offset { get; set; } = 0;

    /// <summary>
    /// Sort field name
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Sort direction (ASC or DESC)
    /// </summary>
    public string SortDirection { get; set; } = "ASC";
}

/// <summary>
/// Response containing GeoJSON features from a GeoPackage
/// </summary>
public class GpkgFeatureResponse
{
    /// <summary>
    /// GeoJSON FeatureCollection as JSON string
    /// </summary>
    public string GeoJson { get; set; } = string.Empty;

    /// <summary>
    /// Number of features returned
    /// </summary>
    public int FeatureCount { get; set; }

    /// <summary>
    /// Total number of features available (before pagination)
    /// </summary>
    public int TotalFeatures { get; set; }

    /// <summary>
    /// Bounding box of returned features
    /// </summary>
    public double[]? BoundingBox { get; set; }

    /// <summary>
    /// Error message if request failed
    /// </summary>
    public string? Error { get; set; }
}
