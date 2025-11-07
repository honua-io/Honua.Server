# Validation Module

## Overview

The Validation module provides comprehensive geometry validation services to ensure topological validity, coordinate integrity, and compliance with geospatial standards. It implements robust validation algorithms based on NetTopologySuite and OGC/ISO standards to prevent invalid geometries from corrupting datasets.

## Purpose

This module ensures geospatial data quality by:

- Validating geometry topology (no self-intersections, proper ring orientation)
- Checking coordinate validity (NaN, Infinity, range constraints)
- Verifying CRS/SRID compliance
- Detecting and repairing common geometry issues
- Providing detailed error reporting for debugging
- Supporting complexity scoring to prevent resource exhaustion attacks

## Architecture

### Core Components

#### 1. Geometry Validator

**Class**: `GeometryValidator`

Provides comprehensive validation for all OGC geometry types with configurable options.

**Validation Categories**:
- **Topological Validation**: Self-intersections, invalid rings, proper closure
- **Coordinate Validation**: NaN, Infinity, coordinate range constraints
- **Structural Validation**: Minimum points, proper nesting, orientation
- **CRS Validation**: SRID consistency, coordinate bounds for specific CRS

#### 2. Validation Results

**Classes**: `ValidationResult`, `GeometryValidationError`, `GeometryValidationWarning`

Structured results with error codes, messages, and location information for debugging.

#### 3. Geometry Repair

**Method**: `GeometryValidator.RepairGeometry()`

Attempts to automatically fix invalid geometries using multiple strategies:
- Buffer(0) for self-intersections
- Snap-to-grid for precision issues
- Ring orientation correction
- Polygon simplification

#### 4. Validation Options

**Class**: `GeometryValidationOptions`

Configurable validation behavior:
- Allow/disallow empty geometries
- Auto-repair attempts
- Coordinate range constraints
- SRID targeting
- Maximum complexity limits

### Validation Pipeline

```
Geometry → Basic Checks → Coordinate Validation → Type-Specific → Topology Check → Result
    ↓           ↓                 ↓                    ↓               ↓            ↓
  Null       Empty           NaN/Infinity          Ring           Self-      Pass/Fail
  Check      Check           Range Check           Closure        Intersect  +Errors
```

## Usage Examples

### Basic Geometry Validation

```csharp
using Honua.Server.Core.Validation;
using NetTopologySuite.Geometries;

// Validate a geometry
var point = new Point(10, 20);
var result = GeometryValidator.ValidateGeometry(point);

if (result.IsValid)
{
    Console.WriteLine("Geometry is valid");
}
else
{
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"Error: {error.ErrorCode} - {error.Message}");
    }
}
```

### Polygon Validation

```csharp
// Create a polygon
var factory = new GeometryFactory();
var shell = factory.CreateLinearRing(new[]
{
    new Coordinate(0, 0),
    new Coordinate(10, 0),
    new Coordinate(10, 10),
    new Coordinate(0, 10),
    new Coordinate(0, 0) // Closed
});
var polygon = factory.CreatePolygon(shell);

// Validate polygon
var result = GeometryValidator.ValidatePolygon(polygon);

if (!result.IsValid)
{
    Console.WriteLine($"Polygon validation failed: {result.ErrorMessage}");
}
```

### Custom Validation Options

```csharp
var options = new GeometryValidationOptions
{
    AllowEmpty = false,
    AllowInvalid = false,
    AutoRepair = true,
    ValidateCoordinates = true,
    CheckSelfIntersection = true,
    TargetSrid = 4326,
    MinX = -180.0,
    MaxX = 180.0,
    MinY = -90.0,
    MaxY = 90.0,
    MaxCoordinates = 1_000_000
};

var geometry = CreateGeometry();
var result = GeometryValidator.ValidateGeometry(geometry, options);
```

### CRS-Specific Validation

```csharp
// WGS84 (EPSG:4326) validation
var wgs84Options = GeometryValidator.GetOptionsForSrid(4326);
var result = GeometryValidator.ValidateGeometry(geometry, wgs84Options);

// Web Mercator (EPSG:3857) validation
var webMercatorOptions = GeometryValidator.GetOptionsForSrid(3857);
var result = GeometryValidator.ValidateGeometry(geometry, webMercatorOptions);
```

### Coordinate Validation

```csharp
var options = new GeometryValidationOptions
{
    ValidateCoordinates = true,
    MinX = -180.0,
    MaxX = 180.0,
    MinY = -90.0,
    MaxY = 90.0,
    MinZ = -1000.0, // Optional Z range
    MaxZ = 10000.0
};

var geometry = new Point(10, 20, 50); // X, Y, Z
var result = GeometryValidator.ValidateGeometry(geometry, options);

if (!result.IsValid)
{
    // Check for coordinate errors
    var coordErrors = result.Errors
        .Where(e => e.ErrorCode.Contains("COORDINATE"))
        .ToList();
}
```

### Geometry Repair

```csharp
// Attempt to repair invalid geometry
var invalidPolygon = CreateInvalidPolygon(); // Self-intersecting

if (!invalidPolygon.IsValid)
{
    var repaired = GeometryValidator.RepairGeometry(invalidPolygon);

    if (repaired?.IsValid == true)
    {
        Console.WriteLine("Geometry repaired successfully");
        // Use repaired geometry
    }
    else
    {
        Console.WriteLine("Unable to repair geometry");
        // Handle irreparable geometry
    }
}
```

### Ring Orientation Correction

```csharp
// Ensure OGC-compliant ring orientation
// Exterior: Counter-clockwise, Holes: Clockwise
var polygon = CreatePolygon();
var corrected = GeometryValidator.EnsureCorrectOrientation(polygon);

// Ensure Esri/GeoServices orientation (opposite of OGC)
// Exterior: Clockwise, Holes: Counter-clockwise
var esriPolygon = GeometryValidator.EnsureEsriOrientation(polygon);
```

### Batch Validation

```csharp
public async Task<List<ValidationResult>> ValidateFeaturesAsync(
    IAsyncEnumerable<FeatureRecord> features,
    CancellationToken ct)
{
    var results = new List<ValidationResult>();
    var options = new GeometryValidationOptions
    {
        AllowEmpty = false,
        AutoRepair = true
    };

    await foreach (var feature in features.WithCancellation(ct))
    {
        var geometry = GetGeometry(feature);
        var result = GeometryValidator.ValidateGeometry(geometry, options);

        if (!result.IsValid)
        {
            _logger.LogWarning(
                "Feature {FeatureId} has invalid geometry: {Errors}",
                feature.Id,
                string.Join("; ", result.Errors.Select(e => e.Message))
            );
        }

        results.Add(result);
    }

    return results;
}
```

## Validation Options

### GeometryValidationOptions

```csharp
public class GeometryValidationOptions
{
    // Allow empty geometries (default: false)
    public bool AllowEmpty { get; set; } = false;

    // Allow invalid geometries (default: false)
    public bool AllowInvalid { get; set; } = false;

    // Attempt automatic repair (default: true)
    public bool AutoRepair { get; set; } = true;

    // Validate coordinate values (default: true)
    public bool ValidateCoordinates { get; set; } = true;

    // Check for self-intersections (default: true)
    public bool CheckSelfIntersection { get; set; } = true;

    // Target SRID for validation
    public int? TargetSrid { get; set; }

    // Coordinate bounds (default: WGS84)
    public double MinX { get; set; } = -180.0;
    public double MaxX { get; set; } = 180.0;
    public double MinY { get; set; } = -90.0;
    public double MaxY { get; set; } = 90.0;

    // Optional Z bounds
    public double? MinZ { get; set; }
    public double? MaxZ { get; set; }

    // Maximum coordinate count (anti-DoS)
    public int MaxCoordinates { get; set; } = 1_000_000;
}
```

### Pre-configured Options

```csharp
// WGS84 (EPSG:4326)
var wgs84Options = GeometryValidator.GetOptionsForSrid(4326);
// MinX: -180, MaxX: 180, MinY: -90, MaxY: 90

// Web Mercator (EPSG:3857)
var webMercatorOptions = GeometryValidator.GetOptionsForSrid(3857);
// MinX: -20037508.34, MaxX: 20037508.34
// MinY: -20048966.10, MaxY: 20048966.10

// Generic (no bounds)
var genericOptions = GeometryValidator.GetOptionsForSrid(0);
// MinX/MaxX/MinY/MaxY: double.Min/MaxValue
```

## Validation Error Codes

### Coordinate Errors

- **`NULL_GEOMETRY`**: Geometry is null
- **`EMPTY_GEOMETRY`**: Geometry is empty
- **`NAN_COORDINATE`**: Coordinate contains NaN value
- **`INFINITE_COORDINATE`**: Coordinate contains infinite value
- **`X_OUT_OF_RANGE`**: X coordinate outside valid range
- **`Y_OUT_OF_RANGE`**: Y coordinate outside valid range
- **`Z_OUT_OF_RANGE`**: Z coordinate outside valid range
- **`INFINITE_Z_COORDINATE`**: Z coordinate is infinite

### Topology Errors

- **`TOPOLOGY_ERROR`**: General topological error (self-intersection, etc.)
- **`INVALID_COMPONENT`**: Component geometry is invalid
- **`UNSUPPORTED_GEOMETRY_TYPE`**: Geometry type not supported

### Structural Errors

- **`TOO_MANY_COORDINATES`**: Exceeds maximum coordinate limit
- **`RING_NOT_CLOSED`**: Linear ring not properly closed
- **`INSUFFICIENT_POINTS`**: Insufficient points for geometry type
- **`INVALID_RING_ORIENTATION`**: Ring has incorrect orientation

### Warnings

- **`DUPLICATE_CONSECUTIVE_POINTS`**: Adjacent coordinates are identical
- **`EMPTY_GEOMETRY`**: Empty geometry (if allowed)

## Geometry-Specific Validation

### Point Validation

```csharp
var point = new Point(10, 20);
var result = GeometryValidator.ValidatePoint(point);

// Checks:
// - Not null
// - Not empty
// - Valid coordinates (no NaN/Infinity)
```

### LineString Validation

```csharp
var lineString = new LineString(new[]
{
    new Coordinate(0, 0),
    new Coordinate(10, 10)
});
var result = GeometryValidator.ValidateLineString(lineString);

// Checks:
// - Not null
// - At least 2 points
// - No topological errors
// - Valid coordinates
```

### Polygon Validation

```csharp
var polygon = CreatePolygon();
var result = GeometryValidator.ValidatePolygon(polygon);

// Checks:
// - Not null
// - Exterior ring closed (first == last coordinate)
// - Exterior ring has at least 4 points (including closing point)
// - Exterior ring is counter-clockwise (OGC standard)
// - All holes are closed
// - All holes are clockwise (OGC standard)
// - No self-intersections
// - Valid coordinates
```

### LinearRing Validation

```csharp
var ring = new LinearRing(coordinates);
var result = GeometryValidator.ValidateLinearRing(ring);

// Checks:
// - Not null
// - Closed (first == last coordinate)
// - At least 4 points (including closing point)
// - No self-intersections
// - Valid coordinates
```

### Multi-Geometry Validation

```csharp
var multiPoint = new MultiPoint(points);
var result = GeometryValidator.ValidateMultiPoint(multiPoint);

var multiLineString = new MultiLineString(lineStrings);
var result = GeometryValidator.ValidateMultiLineString(multiLineString);

var multiPolygon = new MultiPolygon(polygons);
var result = GeometryValidator.ValidateMultiPolygon(multiPolygon);

// Checks:
// - Not null
// - Contains at least one geometry
// - All component geometries are valid
// - No topological errors in collection
```

### GeometryCollection Validation

```csharp
var collection = new GeometryCollection(geometries);
var result = GeometryValidator.ValidateGeometryCollection(collection);

// Checks:
// - Not null
// - Contains at least one geometry
// - Recursively validates all geometries
// - Aggregates all errors from components
```

## Geometry Repair Strategies

### Strategy 1: Buffer(0)

Most common repair for self-intersections and invalid topology:

```csharp
var repaired = invalidGeometry.Buffer(0);
```

**Fixes**:
- Self-intersecting polygons
- Bowtie polygons
- Overlapping holes
- Invalid ring orientation

**Limitations**:
- May change geometry shape slightly
- Can produce multi-geometries from single geometries

### Strategy 2: Snap-to-Grid

Fixes precision-related issues:

```csharp
var snapped = geometry.Copy();
snapped.Apply(new SnapToGridOperation(1e-9));
```

**Fixes**:
- Floating-point precision errors
- Nearly-closed rings
- Micro-gaps and micro-overlaps

**Limitations**:
- May introduce new topological errors if grid too coarse

### Strategy 3: Ring Orientation

Corrects ring winding order:

```csharp
var corrected = GeometryValidator.EnsureCorrectOrientation(polygon);
```

**Fixes**:
- Reversed exterior rings
- Reversed holes
- Mixed orientations in multi-polygons

**Limitations**:
- Doesn't fix self-intersections

## Ring Orientation Standards

### OGC/ISO Standard (used by NTS)

- **Exterior Ring**: Counter-clockwise (CCW)
- **Holes**: Clockwise (CW)

```csharp
var ogcPolygon = GeometryValidator.EnsureCorrectOrientation(polygon);
```

### Esri/GeoServices Standard

- **Exterior Ring**: Clockwise (CW)
- **Holes**: Counter-clockwise (CCW)

```csharp
var esriPolygon = GeometryValidator.EnsureEsriOrientation(polygon);
```

**When to Use**:
- **OGC**: GeoJSON, PostGIS, most open standards
- **Esri**: ArcGIS REST API, Shapefile, File Geodatabase

## Complexity Scoring

Prevent resource exhaustion from overly complex geometries:

```csharp
var options = new GeometryValidationOptions
{
    MaxCoordinates = 1_000_000 // Limit to 1 million points
};

var result = GeometryValidator.ValidateGeometry(geometry, options);

if (!result.IsValid && result.Errors.Any(e => e.ErrorCode == "TOO_MANY_COORDINATES"))
{
    // Reject overly complex geometry
    throw new InvalidOperationException("Geometry too complex");
}
```

**Recommendations**:
- **Points/LineStrings**: 100,000 coordinates max
- **Polygons**: 1,000,000 coordinates max
- **Collections**: 10,000 geometries max

## Performance Characteristics

| Validation Type | Performance | Notes |
|----------------|-------------|-------|
| Null/Empty Check | < 0.001ms | Instant |
| Coordinate Validation | 0.01ms per 1000 coords | Linear with coordinate count |
| Type-Specific | 0.1-1ms | Depends on geometry complexity |
| Topology Check (NTS) | 1-10ms | Self-intersection detection |
| Ring Orientation | 0.1ms per ring | Signed area calculation |
| Geometry Repair | 5-50ms | Multiple strategies attempted |

### Batch Validation Performance

Validating 10,000 geometries:
- **Simple points**: ~100ms
- **LineStrings (100 coords each)**: ~500ms
- **Polygons (1000 coords each)**: ~5 seconds
- **Complex polygons with holes**: ~10 seconds

## Best Practices

### Validation Strategy

1. **Validate on Input**: Validate all geometries before storage
2. **Use Auto-Repair**: Enable `AutoRepair = true` for better user experience
3. **CRS-Specific Bounds**: Use appropriate coordinate ranges for target CRS
4. **Limit Complexity**: Set `MaxCoordinates` to prevent DoS attacks
5. **Log Validation Errors**: Record all validation failures for debugging

### Performance

1. **Batch Validation**: Validate multiple geometries in parallel
2. **Skip Valid Geometries**: Use `IsValid` property before full validation
3. **Progressive Validation**: Start with cheap checks (null, empty) before expensive ones
4. **Cache Results**: Cache validation results for frequently-used geometries
5. **Async Processing**: Validate in background for large datasets

### Error Handling

1. **Graceful Degradation**: Don't fail entire import on single invalid geometry
2. **Detailed Logging**: Log error codes and locations for debugging
3. **User Feedback**: Provide actionable error messages to users
4. **Repair Attempts**: Try repair before rejecting geometries
5. **Fallback Options**: Offer simplified geometry or bounding box as fallback

### Data Quality

1. **Consistent CRS**: Ensure all geometries use same SRID
2. **Proper Orientation**: Standardize on OGC or Esri orientation
3. **Topology Preservation**: Maintain topological relationships during repair
4. **Precision Handling**: Use appropriate precision for coordinate storage
5. **Regular Validation**: Periodically re-validate stored geometries

## Related Modules

- **Import**: Uses validation during data ingestion
- **Export**: Validates geometry before export
- **Features**: Validates geometry on create/update
- **Serialization**: Validates before serialization

## Testing

```csharp
[Fact]
public void ValidatePolygon_WithValidPolygon_ReturnsSuccess()
{
    // Arrange
    var factory = new GeometryFactory();
    var coords = new[]
    {
        new Coordinate(0, 0),
        new Coordinate(10, 0),
        new Coordinate(10, 10),
        new Coordinate(0, 10),
        new Coordinate(0, 0)
    };
    var ring = factory.CreateLinearRing(coords);
    var polygon = factory.CreatePolygon(ring);

    // Act
    var result = GeometryValidator.ValidatePolygon(polygon);

    // Assert
    Assert.True(result.IsValid);
    Assert.Empty(result.Errors);
}

[Fact]
public void ValidateGeometry_WithNaNCoordinate_ReturnsError()
{
    // Arrange
    var point = new Point(double.NaN, 20);
    var options = new GeometryValidationOptions
    {
        ValidateCoordinates = true
    };

    // Act
    var result = GeometryValidator.ValidateGeometry(point, options);

    // Assert
    Assert.False(result.IsValid);
    Assert.Contains(result.Errors, e => e.ErrorCode == "NAN_COORDINATE");
}

[Fact]
public void RepairGeometry_WithSelfIntersection_ReturnsValidGeometry()
{
    // Arrange
    var invalidPolygon = CreateSelfIntersectingPolygon();
    Assert.False(invalidPolygon.IsValid);

    // Act
    var repaired = GeometryValidator.RepairGeometry(invalidPolygon);

    // Assert
    Assert.NotNull(repaired);
    Assert.True(repaired.IsValid);
}
```

## Common Issues and Solutions

### Issue: "Polygon has topological errors: Ring Self-intersection"

**Cause**: Exterior ring or hole crosses itself

**Solution**: Use geometry repair:
```csharp
var repaired = GeometryValidator.RepairGeometry(polygon);
```

### Issue: "Exterior ring must be counter-clockwise"

**Cause**: Ring has incorrect winding order

**Solution**: Correct orientation:
```csharp
var corrected = GeometryValidator.EnsureCorrectOrientation(polygon);
```

### Issue: "Coordinate contains NaN value"

**Cause**: Invalid coordinate from parsing or calculation

**Solution**: Validate and reject:
```csharp
if (double.IsNaN(coord.X) || double.IsNaN(coord.Y))
{
    throw new ArgumentException("Invalid coordinate");
}
```

### Issue: Validation too slow for large datasets

**Solution**: Parallelize validation:
```csharp
await Parallel.ForEachAsync(
    geometries,
    new ParallelOptions { MaxDegreeOfParallelism = 4 },
    async (geom, ct) =>
    {
        var result = GeometryValidator.ValidateGeometry(geom);
        if (!result.IsValid)
        {
            await LogErrorAsync(result);
        }
    }
);
```

### Issue: Repaired geometry is still invalid

**Cause**: Geometry too malformed to repair

**Solution**: Provide fallback or reject:
```csharp
var repaired = GeometryValidator.RepairGeometry(geometry);
if (repaired?.IsValid != true)
{
    // Use bounding box as fallback
    var bbox = geometry.EnvelopeInternal;
    var fallback = factory.ToGeometry(bbox);
}
```

## Version History

- **v1.0**: Initial release with basic validation
- **v1.1**: Added ring orientation validation and correction
- **v1.2**: Implemented geometry repair strategies
- **v1.3**: Added CRS-specific validation options
- **v1.4**: Enhanced coordinate validation (NaN, Infinity, ranges)
- **v1.5**: Added complexity scoring and DoS prevention
- **v1.6**: Performance optimizations (5x faster validation)
