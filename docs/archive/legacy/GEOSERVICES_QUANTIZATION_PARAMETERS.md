# GeoServices REST quantizationParameters Implementation Notes

## Overview
The `quantizationParameters` parameter is an advanced geometry optimization feature in Esri's GeoServices REST API. It provides an alternative to `maxAllowableOffset` for reducing coordinate precision and payload size.

## Status
**NOT IMPLEMENTED** - Deferred due to complexity. `maxAllowableOffset` and `geometryPrecision` provide sufficient optimization for most use cases.

## What is quantizationParameters?

Quantization is a process that:
1. Defines a grid with a specific extent and resolution
2. Snaps all geometry coordinates to the nearest grid point
3. Encodes coordinates as integers relative to the grid origin
4. Can reduce JSON payload size by 40-60% for large feature sets

## Expected Format

```json
{
  "extent": {
    "xmin": -180,
    "ymin": -90,
    "xmax": 180,
    "ymax": 90,
    "spatialReference": {"wkid": 4326}
  },
  "mode": "view",
  "originPosition": "upperLeft",
  "tolerance": 0.0001
}
```

## Implementation Requirements

### 1. Parameter Parsing
```csharp
public static QuantizationParameters? ResolveQuantizationParameters(IQueryCollection query)
{
    if (!query.TryGetValue("quantizationParameters", out var values) || values.Count == 0)
    {
        return null;
    }

    var json = values[^1];
    // Parse JSON to extract:
    // - extent (xmin, ymin, xmax, ymax, spatialReference)
    // - mode (typically "view" or "edit")
    // - originPosition (upperLeft, lowerLeft)
    // - tolerance (optional)

    return new QuantizationParameters(...);
}
```

### 2. Geometry Quantization Logic
```csharp
// Pseudo-code for quantization
var gridResolutionX = (extent.xmax - extent.xmin) / tolerance;
var gridResolutionY = (extent.ymax - extent.ymin) / tolerance;

foreach (var coordinate in geometry.Coordinates)
{
    // Snap to grid
    var quantizedX = Math.Round((coordinate.X - extent.xmin) / gridResolutionX);
    var quantizedY = Math.Round((coordinate.Y - extent.ymin) / gridResolutionY);

    // Store as integer offsets
    quantizedCoordinates.Add((int)quantizedX, (int)quantizedY);
}
```

### 3. Response Format Changes
When quantization is enabled, the response format changes significantly:

**Standard GeoJSON:**
```json
{
  "type": "Point",
  "coordinates": [-122.419, 37.775]
}
```

**Quantized format:**
```json
{
  "type": "Point",
  "x": 1000,
  "y": 2000
}
```

With quantization metadata in the response header:
```json
{
  "transform": {
    "originPosition": "upperLeft",
    "scale": [0.001, 0.001],
    "translate": [-180, 90]
  }
}
```

### 4. Integration Points

Would need updates in:
- `GeoservicesParameterResolver.cs` - parameter parsing
- `GeoservicesRESTQueryContext` - add QuantizationParameters field
- `StreamingGeoJsonWriter.cs` - alternative geometry encoding
- `GeoservicesRESTQueryTranslator.cs` - conflict detection with maxAllowableOffset

### 5. Challenges

1. **Format Compatibility**: Quantized output is NOT standard GeoJSON
   - Breaks compatibility with standard GeoJSON parsers
   - Requires Esri-specific clients to decode

2. **Coordinate System Complexity**:
   - Must handle different spatial references
   - Origin position (upperLeft vs lowerLeft) affects Y-axis calculation
   - Coordinate transformations needed if input/output SRs differ

3. **Mode Handling**:
   - "view" mode: Aggressive optimization for display
   - "edit" mode: Preserves precision for editing workflows
   - Different tolerance strategies

4. **Polygon Validity**:
   - Quantization can make valid polygons invalid
   - Self-intersections after snapping to grid
   - Requires topology validation/repair

5. **Performance Tradeoffs**:
   - CPU cost of quantization vs. bandwidth savings
   - Memory overhead for tracking grid state
   - Complexity in streaming scenarios

## Alternatives Implemented

Instead of `quantizationParameters`, we implemented:

1. **maxAllowableOffset**:
   - Simplifies geometry using Douglas-Peucker algorithm
   - Reduces vertex count while preserving shape
   - Standard GeoJSON output

2. **geometryPrecision**:
   - Rounds coordinates to N decimal places
   - Reduces JSON payload size
   - Maintains GeoJSON compatibility

These two parameters provide 80% of the benefit with 20% of the complexity.

## Example Use Cases

When quantizationParameters would be beneficial:
- Very large polygon datasets (millions of vertices)
- Low-zoom map displays where precision doesn't matter
- Bandwidth-constrained mobile clients
- Real-time streaming of high-frequency GPS tracks

When to use alternatives instead:
- Standard GeoJSON clients
- Need for coordinate precision
- Compatibility with other tools
- Simpler deployment/maintenance

## Future Implementation

If quantizationParameters is needed in the future:

1. Start with "view" mode only
2. Only support common spatial references (3857, 4326)
3. Document that output is NOT standard GeoJSON
4. Implement extensive validation/testing
5. Provide option to disable via configuration

## References

- [ArcGIS REST API Documentation - Query](https://developers.arcgis.com/rest/services-reference/query-feature-service-layer-.htm)
- [Esri Quantization Specification](https://github.com/Esri/quantized-mesh-tile)
- Douglas-Peucker Algorithm: https://en.wikipedia.org/wiki/Ramer%E2%80%93Douglas%E2%80%93Peucker_algorithm

## Decision

**DEFER IMPLEMENTATION** - The complexity and non-standard output format make quantizationParameters unsuitable for the current release. The implemented `maxAllowableOffset` and `geometryPrecision` parameters provide adequate geometry optimization with standard GeoJSON compatibility.
