namespace Honua.MapSDK.Models.Import;

/// <summary>
/// Result of parsing a data file
/// </summary>
public class ParsedData
{
    /// <summary>
    /// Features parsed from the file
    /// </summary>
    public List<ParsedFeature> Features { get; set; } = new();

    /// <summary>
    /// Field definitions detected
    /// </summary>
    public List<FieldDefinition> Fields { get; set; } = new();

    /// <summary>
    /// Source format
    /// </summary>
    public ImportFormat Format { get; set; }

    /// <summary>
    /// Coordinate reference system (CRS)
    /// </summary>
    public string? CRS { get; set; }

    /// <summary>
    /// Character encoding
    /// </summary>
    public string Encoding { get; set; } = "UTF-8";

    /// <summary>
    /// Bounding box [west, south, east, north]
    /// </summary>
    public double[]? BoundingBox { get; set; }

    /// <summary>
    /// Total number of rows in source (before filtering)
    /// </summary>
    public int TotalRows { get; set; }

    /// <summary>
    /// Number of rows with valid data
    /// </summary>
    public int ValidRows { get; set; }

    /// <summary>
    /// Validation errors
    /// </summary>
    public List<ValidationError> Errors { get; set; } = new();

    /// <summary>
    /// Metadata from the source file
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// A single parsed feature
/// </summary>
public class ParsedFeature
{
    /// <summary>
    /// Feature ID
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Geometry (GeoJSON format)
    /// </summary>
    public object? Geometry { get; set; }

    /// <summary>
    /// Feature properties/attributes
    /// </summary>
    public Dictionary<string, object?> Properties { get; set; } = new();

    /// <summary>
    /// Row number in source file
    /// </summary>
    public int RowNumber { get; set; }

    /// <summary>
    /// Whether this feature is valid
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// Validation errors for this feature
    /// </summary>
    public List<ValidationError> Errors { get; set; } = new();
}

/// <summary>
/// Field definition detected from data
/// </summary>
public class FieldDefinition
{
    /// <summary>
    /// Field name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Detected field type
    /// </summary>
    public FieldType Type { get; set; } = FieldType.String;

    /// <summary>
    /// Display name (friendly)
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Whether this field might contain latitude values
    /// </summary>
    public bool IsLikelyLatitude { get; set; }

    /// <summary>
    /// Whether this field might contain longitude values
    /// </summary>
    public bool IsLikelyLongitude { get; set; }

    /// <summary>
    /// Whether this field might contain addresses
    /// </summary>
    public bool IsLikelyAddress { get; set; }

    /// <summary>
    /// Sample values from this field
    /// </summary>
    public List<object?> SampleValues { get; set; } = new();

    /// <summary>
    /// Number of null/empty values
    /// </summary>
    public int NullCount { get; set; }

    /// <summary>
    /// Number of unique values
    /// </summary>
    public int UniqueCount { get; set; }

    /// <summary>
    /// Minimum value (for numeric fields)
    /// </summary>
    public double? MinValue { get; set; }

    /// <summary>
    /// Maximum value (for numeric fields)
    /// </summary>
    public double? MaxValue { get; set; }
}
