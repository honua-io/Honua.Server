# Geoprocessing Component

Client-side spatial analysis using Turf.js for high-performance geometric operations directly in the browser.

## Overview

The Honua.MapSDK Geoprocessing module provides comprehensive spatial analysis capabilities without requiring server round-trips. All operations are executed client-side using Turf.js, enabling fast, interactive GIS workflows.

## Features

### Geometric Operations
- **Buffer** - Create buffer zones at fixed distances around features
- **Intersection** - Find overlapping areas between layers
- **Union** - Combine multiple features into one
- **Difference** - Remove areas of one layer from another
- **Clip** - Cut features to a clipping boundary
- **Simplify** - Reduce geometry complexity

### Measurements
- **Area** - Calculate polygon area (supports multiple units)
- **Length** - Calculate line length
- **Distance** - Calculate distance between points
- **Perimeter** - Calculate polygon perimeter

### Spatial Relationships
- **Contains** - Test if one feature contains another
- **Intersects** - Test if features intersect
- **Within** - Test if feature is within another
- **Overlaps** - Test if features overlap

### Geometric Calculations
- **Centroid** - Find center point of features
- **Convex Hull** - Create smallest convex polygon containing all points
- **Bounding Box** - Calculate feature extent
- **Envelope** - Create bounding box polygon
- **Voronoi** - Generate Voronoi diagrams from points

### Advanced Operations
- **Dissolve** - Merge adjacent features
- **Transform** - Apply coordinate transformations

## Usage

### Basic Component Usage

```razor
<HonuaGeoprocessing
    MapId="map1"
    OnOperationComplete="@HandleResult"
    OnError="@HandleError" />
```

### Programmatic Usage

```csharp
@inject IGeoprocessingService GeoprocessingService

// Buffer operation
var buffered = await GeoprocessingService.BufferAsync(polygon, 1000, "meters");

// Area calculation
var area = await GeoprocessingService.AreaAsync(polygon, "squaremeters");

// Distance measurement
var distance = await GeoprocessingService.DistanceAsync(
    new Coordinate { Longitude = -122.4, Latitude = 37.8 },
    new Coordinate { Longitude = -122.5, Latitude = 37.9 },
    "kilometers"
);

// Intersection
var intersection = await GeoprocessingService.IntersectAsync(layer1, layer2);

// Using ExecuteOperationAsync for detailed results
var result = await GeoprocessingService.ExecuteOperationAsync(
    GeoprocessingOperationType.Buffer,
    new GeoprocessingParameters
    {
        Input = polygon,
        Distance = 1000,
        Units = "meters"
    }
);

Console.WriteLine($"Operation completed in {result.ExecutionTimeMs}ms");
```

## Performance

The geoprocessing service is designed to handle large datasets efficiently:

- **Target**: Process 1000 polygons in less than 1 second
- **Client-side execution**: No server round-trips
- **Browser-based**: Leverages WebAssembly and optimized JavaScript
- **Memory efficient**: Streams large datasets when possible

## Supported Units

### Distance/Length
- meters (default)
- kilometers
- miles
- feet
- yards
- nauticalMiles

### Area
- meters/squaremeters (default)
- kilometers/squarekilometers
- miles/squaremiles
- hectares
- acres

## Error Handling

All operations include comprehensive error handling:

```csharp
try
{
    var result = await GeoprocessingService.BufferAsync(geometry, 1000, "meters");
}
catch (GeoprocessingException ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"Error Code: {ex.ErrorCode}");
}
```

## Examples

See `GeoprocessingExample.razor` for complete examples of all operations.

## Dependencies

- **@turf/turf**: ^7.1.0 - Spatial analysis library
- **Microsoft.JSInterop**: Blazor JavaScript interop
- **MudBlazor**: UI components (optional)

## API Reference

### IGeoprocessingService

Complete interface for all geoprocessing operations.

### GeoprocessingResult

Contains operation results, execution time, and error information.

### GeoprocessingParameters

Configuration for batch operations.

## Notes

- All operations return GeoJSON-compatible objects
- Input geometries must be valid GeoJSON
- Results can be exported to GeoJSON files
- Operations are non-destructive (original data unchanged)
- Browser memory limits apply to large datasets
- Complex polygon operations may fail for invalid geometries

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0
