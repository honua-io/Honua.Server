// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.MapSDK.Models;

namespace Honua.MapSDK.Models.Import;

/// <summary>
/// Configuration for data import
/// </summary>
public class ImportConfiguration
{
    /// <summary>
    /// Name for the imported layer
    /// </summary>
    public string LayerName { get; set; } = "Imported Data";

    /// <summary>
    /// Type of geometry in the data
    /// </summary>
    public GeometryType GeometryType { get; set; } = GeometryType.Point;

    /// <summary>
    /// Source of geometry data
    /// </summary>
    public GeometrySource GeometrySource { get; set; } = GeometrySource.ExistingGeometry;

    /// <summary>
    /// Field containing latitude values (for CSV)
    /// </summary>
    public string? LatitudeField { get; set; }

    /// <summary>
    /// Field containing longitude values (for CSV)
    /// </summary>
    public string? LongitudeField { get; set; }

    /// <summary>
    /// Field containing address for geocoding
    /// </summary>
    public string? AddressField { get; set; }

    /// <summary>
    /// Field mappings from source to target
    /// </summary>
    public Dictionary<string, FieldMapping> FieldMappings { get; set; } = new();

    /// <summary>
    /// Maximum number of features to import (0 = unlimited)
    /// </summary>
    public int MaxFeatures { get; set; } = 0;

    /// <summary>
    /// Skip rows with errors
    /// </summary>
    public bool SkipErrors { get; set; } = true;

    /// <summary>
    /// Target coordinate reference system (default: EPSG:4326)
    /// </summary>
    public string TargetCRS { get; set; } = "EPSG:4326";

    /// <summary>
    /// Source coordinate reference system (if different from target)
    /// </summary>
    public string? SourceCRS { get; set; }

    /// <summary>
    /// Whether to automatically zoom to imported data
    /// </summary>
    public bool AutoZoom { get; set; } = true;

    /// <summary>
    /// Enable geocoding for address fields
    /// </summary>
    public bool EnableGeocoding { get; set; } = false;

    /// <summary>
    /// Geocoding provider to use
    /// </summary>
    public string? GeocodingProvider { get; set; }

    /// <summary>
    /// API key for geocoding service
    /// </summary>
    public string? GeocodingApiKey { get; set; }
}

/// <summary>
/// Field mapping configuration
/// </summary>
public class FieldMapping
{
    /// <summary>
    /// Source field name
    /// </summary>
    public required string SourceField { get; set; }

    /// <summary>
    /// Target field name
    /// </summary>
    public required string TargetField { get; set; }

    /// <summary>
    /// Field data type
    /// </summary>
    public FieldType FieldType { get; set; } = FieldType.String;

    /// <summary>
    /// Format string for parsing/formatting
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// Default value if source is null/empty
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Whether this field is required
    /// </summary>
    public bool Required { get; set; } = false;
}

/// <summary>
/// Source of geometry data
/// </summary>
public enum GeometrySource
{
    /// <summary>
    /// Geometry is already in the file (GeoJSON, Shapefile, etc.)
    /// </summary>
    ExistingGeometry,

    /// <summary>
    /// Create geometry from lat/lon columns
    /// </summary>
    LatLonColumns,

    /// <summary>
    /// Create geometry by geocoding address column
    /// </summary>
    AddressColumn,

    /// <summary>
    /// Parse geometry from WKT column
    /// </summary>
    WKTColumn
}

/// <summary>
/// Supported field data types
/// </summary>
public enum FieldType
{
    String,
    Number,
    Integer,
    Boolean,
    Date,
    DateTime,
    Time,
    Json
}
