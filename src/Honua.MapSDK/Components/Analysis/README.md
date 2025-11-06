# HonuaAnalysis Component

Comprehensive spatial analysis tools for Honua.MapSDK, providing client-side GIS operations powered by Turf.js.

## Overview

The HonuaAnalysis component provides a rich set of spatial analysis operations for performing GIS tasks directly in the browser. It integrates seamlessly with HonuaMap and other MapSDK components through the ComponentBus messaging system.

## Features

- **Buffer Analysis**: Create single or multi-ring buffer zones around features
- **Overlay Operations**: Intersect, union, difference between features
- **Proximity Analysis**: Find nearest neighbors, features within distance
- **Measurement**: Calculate area, length, perimeter, centroids, bounding boxes
- **Aggregation**: Dissolve features by attribute, merge features
- **Spatial Queries**: Point in polygon, spatial relationships (contains, within, touches)
- **Result Management**: Add results to map, export as GeoJSON
- **Performance**: Client-side processing using Turf.js for fast operations

## Dependencies

### Required
- **Turf.js**: Add to your application via CDN or bundle:

```html
<script src="https://cdn.jsdelivr.net/npm/@turf/turf@latest/turf.min.js"></script>
```

Or via npm:
```bash
npm install @turf/turf
```

## Basic Usage

### Simple Setup

```razor
@page "/analysis"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Analysis

<HonuaMap Id="map1" />

<HonuaAnalysis
    SyncWith="map1"
    ShowPreview="true"
    AllowExport="true" />
```

### With Event Callbacks

```razor
<HonuaAnalysis
    SyncWith="map1"
    OnAnalysisCompleted="@HandleAnalysisCompleted"
    OnAnalysisError="@HandleAnalysisError" />

@code {
    private void HandleAnalysisCompleted(AnalysisResult result)
    {
        Console.WriteLine($"Analysis completed: {result.OperationType}");
        Console.WriteLine($"Features: {result.FeatureCount}");
        Console.WriteLine($"Time: {result.ExecutionTime}ms");
    }

    private void HandleAnalysisError(string error)
    {
        Console.Error.WriteLine($"Analysis error: {error}");
    }
}
```

### Limited Operations

```razor
<HonuaAnalysis
    SyncWith="map1"
    AvailableOperations="@(new List<AnalysisOperationType> {
        AnalysisOperationType.Buffer,
        AnalysisOperationType.Within,
        AnalysisOperationType.Area
    })" />
```

## Parameters

### Component Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Id` | string | Auto-generated | Unique identifier for the component |
| `SyncWith` | string? | null | Map ID to synchronize with |
| `AvailableOperations` | List<AnalysisOperationType>? | null (all) | Limit available operations |
| `ShowPreview` | bool | true | Show preview option before running analysis |
| `AllowExport` | bool | true | Allow exporting results as GeoJSON |
| `ShowToolbar` | bool | true | Show component toolbar |
| `ResultLayerPrefix` | string | "Analysis_" | Prefix for result layer names |
| `CssClass` | string? | null | Additional CSS classes |
| `Style` | string? | null | Inline CSS styles |

### Event Callbacks

| Parameter | Type | Description |
|-----------|------|-------------|
| `OnAnalysisCompleted` | EventCallback<AnalysisResult> | Fired when analysis completes successfully |
| `OnAnalysisError` | EventCallback<string> | Fired when analysis encounters an error |

## Analysis Operations

### 1. Buffer Analysis

Create a buffer zone around features at a specified distance.

**Formula**: Uses Turf.js `buffer()` function
- Creates a polygon representing all points within the specified distance
- Distance is measured along the surface of the Earth (geodesic)

**Parameters**:
- Distance: Buffer distance (numeric)
- Unit: meters, kilometers, miles, feet
- Steps: Number of vertices (affects smoothness, 4-64)

**Use Cases**:
- Find all properties within 500m of a school
- Create service area boundaries
- Safety zones around hazardous areas
- Wildlife protection zones

**Example**:
```csharp
// Programmatically using the service
var service = new SpatialAnalysisService();
var result = await service.BufferAsync(feature, 500, DistanceUnit.Meters, steps: 8);
```

### 2. Multi-Ring Buffer

Create multiple concentric buffer rings at different distances.

**Formula**: Multiple buffer operations with different radii
- Each ring represents a distance threshold
- Useful for graduated impact zones

**Parameters**:
- Distances: Comma-separated values (e.g., "100,250,500,1000")
- Unit: meters, kilometers, miles

**Use Cases**:
- Emergency evacuation zones
- Noise impact assessment (e.g., 50dB, 60dB, 70dB zones)
- Market analysis (1km, 3km, 5km trade areas)

### 3. Intersection

Find the common area where two features overlap.

**Formula**: Geometric intersection using Turf.js `intersect()`
- Returns the overlapping polygon
- Returns null if features don't intersect

**Use Cases**:
- Find overlapping jurisdictions
- Identify shared habitat areas
- Calculate overlap between land use zones

### 4. Union

Combine multiple features into a single feature.

**Formula**: Geometric union using Turf.js `union()`
- Merges all input features
- Removes internal boundaries

**Use Cases**:
- Merge adjacent parcels
- Combine service areas
- Create regional boundaries from districts

### 5. Difference

Subtract one feature from another.

**Formula**: Geometric difference using Turf.js `difference()`
- Returns feature1 minus feature2
- Result = area in feature1 NOT in feature2

**Use Cases**:
- Find areas not covered by service
- Calculate remaining land after development
- Exclusion zones

### 6. Within Distance

Find all features within a specified distance of a target.

**Formula**: Calculate distance using Turf.js `distance()`
- Measures geodesic distance between geometries
- Filters candidates within threshold

**Parameters**:
- Distance: Search radius
- Unit: meters, kilometers, miles

**Use Cases**:
- Find all stores within 5km of a location
- Identify nearby hospitals
- Proximity-based searches

### 7. Nearest Neighbor

Find the closest N features to a target.

**Formula**: Distance calculation + sorting
- Calculates distance to all candidates
- Returns N closest features
- Includes distance in properties

**Parameters**:
- Count: Number of neighbors to find (1-100)

**Use Cases**:
- Find 5 nearest gas stations
- Identify closest emergency services
- Facility location analysis

### 8. Point in Polygon

Find points that fall within a polygon boundary.

**Formula**: Point-in-polygon test using Turf.js `pointsWithinPolygon()`
- Tests if point coordinates are inside polygon
- Handles polygon holes correctly

**Use Cases**:
- Assign customers to sales territories
- Count population in census tracts
- Spatial joins

### 9. Dissolve

Merge adjacent features based on a common attribute.

**Formula**: Group by attribute + union
- Groups features by field value
- Unions all features in each group
- Removes internal boundaries

**Parameters**:
- Field: Attribute field name to group by

**Use Cases**:
- Combine census tracts by county
- Merge parcels by owner
- Aggregate data by region

### 10. Area Calculation

Calculate the area of polygon features.

**Formula**: Planar area calculation using Turf.js `area()`
- Returns area in square meters
- Converts to hectares, acres, sq km, sq mi

**Output**:
- Area in square meters
- Area in hectares (m² / 10,000)
- Area in acres (m² / 4,046.86)
- Area in square miles (m² / 2,589,988.11)

**Use Cases**:
- Calculate parcel sizes
- Measure forest cover
- Land use statistics

### 11. Length/Perimeter Calculation

Calculate length of lines or perimeter of polygons.

**Formula**: Distance sum using Turf.js `length()`
- For lines: sum of segment lengths
- For polygons: perimeter using `polygonToLine()`

**Output**:
- Length in kilometers
- Length in miles

**Use Cases**:
- Measure road lengths
- Calculate fence requirements
- Trail distances

### 12. Centroid

Calculate the geometric center point of a feature.

**Formula**: Centroid calculation using Turf.js `centroid()`
- Average of all vertices (for polygons)
- May fall outside complex shapes

**Output**:
- Point geometry
- Longitude and latitude coordinates

**Use Cases**:
- Label placement
- Center of mass calculations
- Representative points

### 13. Bounding Box

Calculate the minimum bounding rectangle of a feature.

**Formula**: Min/max coordinates using Turf.js `bbox()`
- [minLon, minLat, maxLon, maxLat]
- Creates rectangle polygon from bounds

**Output**:
- Bounding box coordinates
- Width and height in degrees
- Rectangle polygon

**Use Cases**:
- Extent calculations
- Simplified collision detection
- Viewport fitting

## Result Object

All analysis operations return an `AnalysisResult` object:

```csharp
public class AnalysisResult
{
    public string Id { get; set; }
    public string OperationType { get; set; }
    public object Result { get; set; }              // GeoJSON feature(s)
    public Dictionary<string, double> Statistics { get; set; }
    public int FeatureCount { get; set; }
    public DateTime Timestamp { get; set; }
    public double ExecutionTime { get; set; }       // milliseconds
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Warnings { get; set; }
}
```

## ComponentBus Messages

### Published Messages

**AnalysisCompletedMessage**
```csharp
{
    ComponentId: "analysis-123",
    OperationType: "Buffer",
    Success: true,
    FeatureCount: 1
}
```

**AnalysisResultAddedMessage**
```csharp
{
    ComponentId: "analysis-123",
    LayerId: "analysis-result-1234567890",
    LayerName: "Analysis_Buffer_143052",
    OperationType: "Buffer"
}
```

### Subscribed Messages

**MapReadyMessage** - Initialize analysis when map is ready

**LayerSelectedMessage** - React to layer selections

## Performance Considerations

### Client-Side Processing
- All operations run in the browser using Turf.js
- No server round-trips for analysis
- Fast for small to medium datasets (< 10,000 features)

### Large Dataset Recommendations
- For > 10,000 features, consider server-side processing
- Use spatial indexing for large candidate sets
- Implement progressive loading
- Cache frequently used results

### Memory Usage
- Complex geometries consume more memory
- Multi-ring buffers create multiple features
- Simplify geometries when appropriate
- Clear results when no longer needed

### Optimization Tips
```csharp
// Good: Batch operations
var results = await service.UnionAsync(features);

// Avoid: Multiple individual operations
foreach (var feature in features) {
    await service.BufferAsync(feature, 100, DistanceUnit.Meters);
}
```

## Styling Results

Customize result appearance:

```razor
<HonuaAnalysis
    SyncWith="map1"
    ResultStyle="@(new AnalysisStyle {
        FillColor = "#FF5722",
        FillOpacity = 0.3,
        StrokeColor = "#D32F2F",
        StrokeWidth = 2.5
    })" />
```

## Advanced Usage

### Programmatic Analysis

```csharp
@inject SpatialAnalysisService AnalysisService

private async Task PerformCustomAnalysis()
{
    // Buffer analysis
    var bufferResult = await AnalysisService.BufferAsync(
        feature: selectedFeature,
        distance: 1000,
        unit: DistanceUnit.Meters,
        steps: 16
    );

    // Intersection
    var intersectResult = await AnalysisService.IntersectAsync(
        feature1: layer1Features[0],
        feature2: layer2Features[0]
    );

    // Nearest neighbor
    var nearestResult = await AnalysisService.NearestNeighborAsync(
        target: clickedPoint,
        candidates: allStores,
        count: 5
    );
}
```

### Chaining Operations

```csharp
private async Task ChainedAnalysis()
{
    // 1. Buffer school locations
    var buffers = await AnalysisService.BufferAsync(school, 500, DistanceUnit.Meters);

    // 2. Find parcels within buffer
    var parcelsInBuffer = await AnalysisService.PointsWithinPolygonAsync(
        points: parcels,
        polygon: buffers.Result
    );

    // 3. Calculate total area
    var totalArea = await AnalysisService.CalculateAreaAsync(
        parcelsInBuffer.Result,
        DistanceUnit.Hectares
    );
}
```

## Error Handling

```csharp
private async Task SafeAnalysis()
{
    try
    {
        var result = await AnalysisService.BufferAsync(feature, 100, DistanceUnit.Meters);

        if (!result.Success)
        {
            Console.Error.WriteLine($"Analysis failed: {result.ErrorMessage}");
            return;
        }

        if (result.Warnings.Any())
        {
            foreach (var warning in result.Warnings)
            {
                Console.WriteLine($"Warning: {warning}");
            }
        }

        // Process successful result
        ProcessResult(result);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Exception during analysis: {ex.Message}");
    }
}
```

## Accessibility

The component follows WCAG 2.1 Level AA guidelines:

- Keyboard navigation for all controls
- ARIA labels on buttons and inputs
- Screen reader friendly
- Focus indicators
- Sufficient color contrast

## Browser Support

- Chrome 90+
- Firefox 88+
- Safari 14+
- Edge 90+

Requires ES6+ JavaScript support and WebGL for map rendering.

## Troubleshooting

### Turf.js Not Found
**Problem**: "turf is not defined" error in console

**Solution**: Include Turf.js in your app:
```html
<script src="https://cdn.jsdelivr.net/npm/@turf/turf@latest/turf.min.js"></script>
```

### Analysis Not Running
**Problem**: Clicking "Run Analysis" does nothing

**Solution**:
- Ensure map is ready (`MapReadyMessage` received)
- Check browser console for JavaScript errors
- Verify feature selection

### Incorrect Results
**Problem**: Analysis produces unexpected output

**Solution**:
- Verify input geometries are valid GeoJSON
- Check coordinate reference system (should be WGS84/EPSG:4326)
- Ensure geometries don't have self-intersections
- Validate distance units match expectations

### Performance Issues
**Problem**: Analysis is slow or freezes

**Solution**:
- Reduce feature complexity (simplify geometries)
- Limit dataset size
- Use appropriate buffer steps (lower = faster)
- Consider server-side processing for large datasets

## API Reference

See `AnalysisOperation.cs` for complete model definitions.

## Related Components

- **HonuaMap**: Base map component
- **HonuaDraw**: Drawing tools for creating analysis inputs
- **HonuaEditor**: Feature editing
- **HonuaLayerList**: Manage result layers
- **HonuaFilterPanel**: Filter features before analysis

## License

Part of Honua.MapSDK - See main SDK documentation for license information.
