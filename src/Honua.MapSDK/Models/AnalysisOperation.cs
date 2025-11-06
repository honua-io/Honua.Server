namespace Honua.MapSDK.Models;

/// <summary>
/// Types of spatial analysis operations supported by HonuaAnalysis
/// </summary>
public enum AnalysisOperationType
{
    // Buffer Operations
    Buffer,
    MultiRingBuffer,

    // Overlay Operations
    Intersect,
    Union,
    Difference,
    SymmetricDifference,
    Clip,

    // Proximity Analysis
    Within,
    PointInPolygon,
    NearestNeighbor,
    DistanceMatrix,

    // Aggregation
    Dissolve,
    Merge,

    // Measurement
    Area,
    Length,
    Perimeter,
    Centroid,
    BoundingBox,
    Bearing,

    // Spatial Queries
    SelectByLocation,
    SpatialJoin,
    Contains,
    Touches,
    Crosses,
    Overlaps
}

/// <summary>
/// Represents a spatial analysis operation configuration
/// </summary>
public class AnalysisOperation
{
    /// <summary>
    /// Unique identifier for this operation
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Type of analysis operation
    /// </summary>
    public required AnalysisOperationType Type { get; set; }

    /// <summary>
    /// Primary input layer ID
    /// </summary>
    public required string InputLayer { get; set; }

    /// <summary>
    /// Secondary input layer ID (for overlay operations)
    /// </summary>
    public string? SecondaryLayer { get; set; }

    /// <summary>
    /// Operation-specific parameters
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Name/label for the operation
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Description of what this operation does
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Timestamp when operation was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Result of a spatial analysis operation
/// </summary>
public class AnalysisResult
{
    /// <summary>
    /// Unique identifier for this result
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Operation that produced this result
    /// </summary>
    public required string OperationType { get; set; }

    /// <summary>
    /// Result data (GeoJSON FeatureCollection or single value)
    /// </summary>
    public required object Result { get; set; }

    /// <summary>
    /// Statistical summary of the analysis
    /// </summary>
    public Dictionary<string, double> Statistics { get; set; } = new();

    /// <summary>
    /// Number of features in the result
    /// </summary>
    public int FeatureCount { get; set; }

    /// <summary>
    /// Timestamp when analysis completed
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Time taken to complete the analysis (milliseconds)
    /// </summary>
    public double ExecutionTime { get; set; }

    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// Error message if operation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Warnings generated during analysis
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Metadata about the operation
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Configuration for buffer analysis
/// </summary>
public class BufferConfig
{
    /// <summary>
    /// Buffer distance
    /// </summary>
    public required double Distance { get; set; }

    /// <summary>
    /// Unit of measurement
    /// </summary>
    public DistanceUnit Unit { get; set; } = DistanceUnit.Meters;

    /// <summary>
    /// Number of steps in the buffer (affects smoothness)
    /// </summary>
    public int Steps { get; set; } = 8;

    /// <summary>
    /// Multiple distances for multi-ring buffer
    /// </summary>
    public List<double>? Distances { get; set; }

    /// <summary>
    /// Whether to union overlapping buffers
    /// </summary>
    public bool Union { get; set; } = false;
}

/// <summary>
/// Configuration for proximity analysis
/// </summary>
public class ProximityConfig
{
    /// <summary>
    /// Maximum search distance
    /// </summary>
    public required double Distance { get; set; }

    /// <summary>
    /// Unit of measurement
    /// </summary>
    public DistanceUnit Unit { get; set; } = DistanceUnit.Meters;

    /// <summary>
    /// Number of nearest neighbors to find
    /// </summary>
    public int Count { get; set; } = 1;

    /// <summary>
    /// Include distance in results
    /// </summary>
    public bool IncludeDistance { get; set; } = true;
}

/// <summary>
/// Configuration for dissolve operation
/// </summary>
public class DissolveConfig
{
    /// <summary>
    /// Field name to dissolve by
    /// </summary>
    public required string Field { get; set; }

    /// <summary>
    /// Fields to aggregate with statistics
    /// </summary>
    public Dictionary<string, AggregationType> AggregateFields { get; set; } = new();
}

/// <summary>
/// Configuration for spatial join
/// </summary>
public class SpatialJoinConfig
{
    /// <summary>
    /// Spatial relationship to use for join
    /// </summary>
    public SpatialRelationship Relationship { get; set; } = SpatialRelationship.Intersects;

    /// <summary>
    /// Fields to include from target layer
    /// </summary>
    public List<string>? TargetFields { get; set; }

    /// <summary>
    /// Join type (one-to-one, one-to-many)
    /// </summary>
    public JoinType JoinType { get; set; } = JoinType.OneToOne;
}

/// <summary>
/// Distance units for analysis operations
/// </summary>
public enum DistanceUnit
{
    Meters,
    Kilometers,
    Miles,
    Feet,
    NauticalMiles,
    Yards
}

/// <summary>
/// Aggregation types for dissolve operations
/// </summary>
public enum AggregationType
{
    Sum,
    Average,
    Min,
    Max,
    Count,
    First,
    Last
}

/// <summary>
/// Spatial relationship types for queries
/// </summary>
public enum SpatialRelationship
{
    Intersects,
    Contains,
    Within,
    Touches,
    Crosses,
    Overlaps,
    Disjoint,
    Equals
}

/// <summary>
/// Join types for spatial join
/// </summary>
public enum JoinType
{
    OneToOne,
    OneToMany
}

/// <summary>
/// Layer reference for analysis
/// </summary>
public class AnalysisLayerReference
{
    /// <summary>
    /// Layer ID
    /// </summary>
    public required string LayerId { get; set; }

    /// <summary>
    /// Layer name for display
    /// </summary>
    public required string LayerName { get; set; }

    /// <summary>
    /// Geometry type
    /// </summary>
    public string? GeometryType { get; set; }

    /// <summary>
    /// Number of features
    /// </summary>
    public int FeatureCount { get; set; }

    /// <summary>
    /// Available fields
    /// </summary>
    public List<string> Fields { get; set; } = new();
}

/// <summary>
/// Feature reference for analysis
/// </summary>
public class Feature
{
    /// <summary>
    /// Feature ID
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Layer ID this feature belongs to
    /// </summary>
    public string? LayerId { get; set; }

    /// <summary>
    /// Geometry object
    /// </summary>
    public required object Geometry { get; set; }

    /// <summary>
    /// Feature attributes
    /// </summary>
    public Dictionary<string, object> Attributes { get; set; } = new();

    /// <summary>
    /// Feature version for conflict detection
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// When feature was last modified
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    /// Convert feature to GeoJSON
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

    /// <summary>
    /// Clone this feature
    /// </summary>
    public Feature Clone()
    {
        return new Feature
        {
            Id = Id,
            LayerId = LayerId,
            Geometry = Geometry,
            Attributes = new Dictionary<string, object>(Attributes),
            Version = Version,
            ModifiedAt = ModifiedAt
        };
    }
}

/// <summary>
/// Analysis style configuration
/// </summary>
public class AnalysisStyle
{
    /// <summary>
    /// Fill color
    /// </summary>
    public string FillColor { get; set; } = "#3B82F6";

    /// <summary>
    /// Fill opacity
    /// </summary>
    public double FillOpacity { get; set; } = 0.2;

    /// <summary>
    /// Stroke color
    /// </summary>
    public string StrokeColor { get; set; } = "#3B82F6";

    /// <summary>
    /// Stroke width
    /// </summary>
    public double StrokeWidth { get; set; } = 2.0;

    /// <summary>
    /// Stroke opacity
    /// </summary>
    public double StrokeOpacity { get; set; } = 1.0;
}
