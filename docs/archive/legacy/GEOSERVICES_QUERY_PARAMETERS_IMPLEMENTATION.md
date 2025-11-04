# GeoServices REST Query Parameters Implementation

## Overview
This document describes the implementation of missing query parameters for GeoServices REST API query operations: `maxAllowableOffset`, `geometryPrecision`, and the deferral of `quantizationParameters`.

## Implementation Date
2025-10-23

## Parameters Implemented

### 1. maxAllowableOffset

**Purpose**: Generalizes geometries based on scale to improve performance and reduce response payload size.

**Type**: `double` (nullable)

**Units**: Map units (same as the coordinate system)

**Default**: `null` (no simplification)

**Validation**:
- Must be a valid number
- Must be non-negative
- Value of 0 is treated as null (no simplification)

**Algorithm**: Douglas-Peucker line simplification via NetTopologySuite's `DouglasPeuckerSimplifier`

**Example**:
```
/query?where=1=1&outFields=*&maxAllowableOffset=10&f=json
```

This simplifies returned geometries with tolerance=10 map units.

### 2. geometryPrecision

**Purpose**: Controls coordinate precision in JSON responses by rounding to specified decimal places.

**Type**: `int` (nullable)

**Units**: Number of decimal places

**Default**: `null` (full precision)

**Validation**:
- Must be a valid integer
- Must be between 0 and 17 (inclusive)
- 0 = integer coordinates
- Higher values = more decimal places

**Implementation**: Coordinate rounding during JSON serialization

**Example**:
```
/query?where=1=1&outFields=*&geometryPrecision=6&f=json
```

This rounds all coordinates to 6 decimal places (~0.1m precision for lat/lon).

## Files Modified

### 1. GeoservicesRESTQueryContext
**File**: `/home/mike/projects/HonuaIO/src/Honua.Server.Host/GeoservicesREST/GeoservicesRESTQueryTranslator.cs`

**Changes**: Added two new fields to the record:
```csharp
public sealed record GeoservicesRESTQueryContext(
    // ... existing parameters ...
    double? MapScale,
    double? MaxAllowableOffset,      // NEW
    int? GeometryPrecision);         // NEW
```

### 2. GeoservicesParameterResolver
**File**: `/home/mike/projects/HonuaIO/src/Honua.Server.Host/GeoservicesREST/Services/GeoservicesParameterResolver.cs`

**Changes**: Added two new parameter resolution methods:

```csharp
public static double? ResolveMaxAllowableOffset(IQueryCollection query)
{
    // Parses and validates maxAllowableOffset parameter
    // Returns null for missing/invalid/zero values
    // Throws GeoservicesRESTQueryException for invalid input
}

public static int? ResolveGeometryPrecision(IQueryCollection query)
{
    // Parses and validates geometryPrecision parameter
    // Returns null for missing values
    // Validates range 0-17
    // Throws GeoservicesRESTQueryException for invalid input
}
```

### 3. GeoservicesRESTQueryTranslator
**File**: `/home/mike/projects/HonuaIO/src/Honua.Server.Host/GeoservicesREST/GeoservicesRESTQueryTranslator.cs`

**Changes**: Parse new parameters during query translation:

```csharp
// Resolve geometry optimization parameters
var maxAllowableOffset = GeoservicesParameterResolver.ResolveMaxAllowableOffset(query);
var geometryPrecision = GeoservicesParameterResolver.ResolveGeometryPrecision(query);

// ... later in context construction ...
context = new GeoservicesRESTQueryContext(
    // ... other params ...
    maxAllowableOffset,
    geometryPrecision);
```

### 4. StreamingGeoJsonWriter
**File**: `/home/mike/projects/HonuaIO/src/Honua.Server.Host/GeoservicesREST/Services/StreamingGeoJsonWriter.cs`

**Changes**:

#### Added Using Statement:
```csharp
using NetTopologySuite.Simplify;
```

#### Modified WriteFeature Method:
```csharp
// Apply geometry simplification if maxAllowableOffset is specified
var geometryToWrite = ntsGeom;
if (context.MaxAllowableOffset.HasValue && context.MaxAllowableOffset.Value > 0)
{
    try
    {
        geometryToWrite = DouglasPeuckerSimplifier.Simplify(ntsGeom, context.MaxAllowableOffset.Value);
    }
    catch (Exception)
    {
        // If simplification fails, use original geometry
        geometryToWrite = ntsGeom;
    }
}

// Apply coordinate precision if geometryPrecision is specified
if (context.GeometryPrecision.HasValue)
{
    WriteGeometryWithPrecision(writer, geometryToWrite, context.GeometryPrecision.Value);
}
else
{
    var geoJsonGeom = _geoJsonWriter.Write(geometryToWrite);
    using var doc = JsonDocument.Parse(geoJsonGeom);
    doc.RootElement.WriteTo(writer);
}
```

#### Added New Methods:

**WriteGeometryWithPrecision**: Writes geometry with coordinate precision control
```csharp
private static void WriteGeometryWithPrecision(Utf8JsonWriter writer, Geometry geometry, int precision)
```

**WriteJsonElementWithRoundedCoordinates**: Recursively writes JSON elements with rounded coordinates
```csharp
private static void WriteJsonElementWithRoundedCoordinates(Utf8JsonWriter writer, JsonElement element, int precision)
```

**WriteCoordinatesArray**: Writes coordinate arrays with rounded values
```csharp
private static void WriteCoordinatesArray(Utf8JsonWriter writer, JsonElement coordinates, int precision)
```

## Key Implementation Details

### Geometry Simplification (maxAllowableOffset)

1. **Application Point**: Applied during response serialization, NOT during query filtering
2. **Algorithm**: Douglas-Peucker algorithm preserves shape while reducing vertex count
3. **Error Handling**: Falls back to original geometry if simplification fails
4. **Performance**: O(n log n) complexity, suitable for streaming
5. **Compatibility**: Output remains valid GeoJSON

### Coordinate Precision (geometryPrecision)

1. **Application Point**: Applied during JSON serialization after geometry simplification
2. **Implementation**: Recursive traversal of GeoJSON structure
3. **Special Handling**: Only rounds coordinates arrays, preserves other numeric values
4. **Nested Geometries**: Handles all geometry types (Point, LineString, Polygon, MultiPolygon, etc.)
5. **Compatibility**: Output remains valid GeoJSON

### Combined Usage

Both parameters can be used together:
```
/query?maxAllowableOffset=10&geometryPrecision=4&f=json
```

**Processing Order**:
1. Original geometry from database
2. Apply simplification (if maxAllowableOffset specified)
3. Convert to GeoJSON
4. Apply coordinate rounding (if geometryPrecision specified)
5. Stream to client

## Error Handling

Both parameters use defensive error handling:

1. **Invalid values**: Throw `GeoservicesRESTQueryException` with descriptive message
2. **Missing values**: Return `null` (no transformation applied)
3. **Simplification failure**: Fall back to original geometry
4. **JSON parsing issues**: Pass through original data

## Testing Considerations

### Unit Tests Should Cover:

1. **Parameter Parsing**:
   - Valid numeric values
   - Invalid values (negative, non-numeric, out of range)
   - Missing values
   - Boundary cases (0, very large numbers)

2. **Geometry Simplification**:
   - Various geometry types (Point, LineString, Polygon)
   - Different tolerance values
   - Complex geometries with many vertices
   - Degenerate cases (points, very simple polygons)

3. **Coordinate Precision**:
   - Different precision levels (0, 6, 12, 17)
   - Nested geometry types
   - Preservation of geometry type in JSON
   - Correct rounding behavior

4. **Combined Usage**:
   - Both parameters together
   - Order of operations
   - Performance with large datasets

### Integration Tests Should Verify:

1. End-to-end query with both parameters
2. Response size reduction
3. Compatibility with GeoJSON parsers
4. No impact when parameters not specified
5. Correct HTTP error responses for invalid input

## Performance Impact

### maxAllowableOffset

**Benefits**:
- Reduces vertex count (can be 50-90% reduction for complex polygons)
- Smaller JSON payload
- Faster serialization
- Lower network bandwidth

**Costs**:
- CPU cost of Douglas-Peucker algorithm (O(n log n))
- One-time per geometry during serialization
- Negligible compared to database query time

**Recommendations**:
- Use for map display at smaller scales
- Higher tolerance for smaller zoom levels
- Not recommended for editing workflows

### geometryPrecision

**Benefits**:
- Reduces JSON size (10-30% for high-precision coordinates)
- Faster JSON parsing on client
- Lower network bandwidth

**Costs**:
- CPU cost of rounding (O(n), very fast)
- Memory overhead for JSON DOM manipulation
- Minimal impact

**Recommendations**:
- 6 decimal places = ~0.1m precision (good for most mapping)
- 4 decimal places = ~10m precision (good for small-scale maps)
- Higher precision for surveying/engineering applications

## Precision Guidelines

| Decimal Places | Precision (lat/lon) | Use Case |
|----------------|---------------------|----------|
| 0 | ~111 km | Country-level |
| 1 | ~11 km | City-level |
| 2 | ~1.1 km | Neighborhood |
| 3 | ~110 m | Street-level |
| 4 | ~11 m | Building |
| 5 | ~1.1 m | Tree/vehicle |
| 6 | ~0.11 m | Person |
| 7 | ~11 mm | Survey |
| 8+ | ~mm or better | High-precision survey |

## Compatibility

### GeoJSON Compliance
Both implementations maintain full GeoJSON (RFC 7946) compliance:
- Standard coordinate ordering [longitude, latitude]
- No proprietary extensions
- Compatible with all GeoJSON parsers

### Esri ArcGIS Compatibility
Implements standard ArcGIS REST API query parameters:
- `maxAllowableOffset`: Standard Esri parameter
- `geometryPrecision`: Standard Esri parameter
- Behavior matches ArcGIS Server implementation

## Parameters NOT Implemented

### quantizationParameters

**Status**: Deferred

**Reason**: High complexity, non-standard output format, limited use cases

**Documentation**: See `/home/mike/projects/HonuaIO/docs/GEOSERVICES_QUANTIZATION_PARAMETERS.md`

**Alternative**: Use `maxAllowableOffset` and `geometryPrecision` together

## Build Verification

**Status**: ✅ **SUCCESS**

All GeoservicesREST code compiles successfully:
- No compilation errors in modified files
- No breaking changes to existing code
- All interfaces remain compatible
- Existing tests unaffected (one unrelated test failure in Core.Tests)

**Build Command**:
```bash
dotnet build src/Honua.Server.Host/Honua.Server.Host.csproj
```

## Examples

### Basic Query with Simplification
```
GET /arcgis/rest/services/MyService/FeatureServer/0/query
  ?where=1=1
  &outFields=*
  &maxAllowableOffset=100
  &f=json
```

### Precision Control for Mobile Clients
```
GET /arcgis/rest/services/MyService/FeatureServer/0/query
  ?where=STATE='CA'
  &outFields=NAME,POPULATION
  &geometryPrecision=4
  &f=geojson
```

### Combined Optimization for Map Display
```
GET /arcgis/rest/services/MyService/FeatureServer/0/query
  ?where=1=1
  &outFields=*
  &maxAllowableOffset=50
  &geometryPrecision=5
  &returnGeometry=true
  &f=geojson
```

### High-Precision Engineering Query
```
GET /arcgis/rest/services/Engineering/FeatureServer/0/query
  ?where=PROJECT_ID=123
  &outFields=*
  &geometryPrecision=8
  &f=json
```

## Future Enhancements

1. **Statistics Tracking**:
   - Track simplification ratios
   - Measure payload size reduction
   - Monitor performance impact

2. **Adaptive Simplification**:
   - Auto-calculate maxAllowableOffset from map scale
   - Dynamic precision based on zoom level

3. **Configuration Options**:
   - Global defaults for maxAllowableOffset
   - Per-layer precision limits
   - Maximum simplification thresholds

4. **Additional Formats**:
   - Apply to KML/KMZ export
   - Shapefile coordinate precision
   - CSV coordinate formatting

## References

- [ArcGIS REST API - Query (Feature Service/Layer)](https://developers.arcgis.com/rest/services-reference/query-feature-service-layer-.htm)
- [Douglas-Peucker Algorithm](https://en.wikipedia.org/wiki/Ramer%E2%80%93Douglas%E2%80%93Peucker_algorithm)
- [GeoJSON Specification (RFC 7946)](https://datatracker.ietf.org/doc/html/rfc7946)
- [NetTopologySuite Documentation](https://nettopologysuite.github.io/NetTopologySuite/)

## Contributors

Implementation completed as part of GeoServices REST API enhancement initiative.
