# H3 Hexagonal Binning - Backend Implementation

Advanced spatial aggregation using Uber's H3 hierarchical hexagonal grid system.

## Overview

The H3 Hexagonal Binning implementation provides enterprise-grade spatial aggregation capabilities for Honua Server. This document covers the backend architecture, geoprocessing operations, and API design.

## Architecture

### Components

1. **H3Service** (`H3Service.cs`)
   - Core H3 operations wrapper
   - Point-to-hex conversions
   - Hex-to-boundary conversions
   - Neighbor and ring queries
   - Resolution metadata

2. **H3BinningOperation** (`H3BinningOperation.cs`)
   - Implements `IGeoprocessingOperation`
   - Point binning and aggregation
   - Statistical calculations
   - GeoJSON output generation

3. **H3AnalysisEndpoints** (`H3AnalysisEndpoints.cs`)
   - REST API endpoints
   - Request validation
   - Response formatting
   - Async job support

### Data Flow

```
Point Data → H3Service → Binning → Aggregation → GeoJSON → Response
              ↓
         H3 Index
              ↓
         Boundary Polygon
```

## H3Service API

### Core Methods

#### PointToH3
Converts geographic coordinates to H3 index.

```csharp
public string PointToH3(double lat, double lon, int resolution)
```

**Example:**
```csharp
var h3Service = new H3Service();
var h3Index = h3Service.PointToH3(37.7749, -122.4194, 7);
// Returns: "872830828ffffff"
```

#### GetH3Boundary
Returns hexagon boundary as NTS Polygon.

```csharp
public Polygon GetH3Boundary(string h3Index)
```

**Example:**
```csharp
var boundary = h3Service.GetH3Boundary("872830828ffffff");
// Returns: Polygon with 7 coordinates (6 vertices + close)
```

#### GetH3Neighbors
Returns neighboring hexagons.

```csharp
public List<string> GetH3Neighbors(string h3Index)
```

**Example:**
```csharp
var neighbors = h3Service.GetH3Neighbors("872830828ffffff");
// Returns: List of 6 H3 indices
```

#### GetH3Ring
Returns hexagons within k distance.

```csharp
public List<string> GetH3Ring(string h3Index, int k)
```

**Example:**
```csharp
var ring = h3Service.GetH3Ring("872830828ffffff", 2);
// Returns: List of 19 H3 indices (1 + 6 + 12)
```

### Utility Methods

```csharp
// Get hexagon center
Coordinate center = h3Service.GetH3Center(h3Index);

// Get hexagon area
double area = h3Service.GetH3Area(h3Index); // in m²

// Get resolution
int resolution = h3Service.GetH3Resolution(h3Index);

// Validate H3 index
bool isValid = h3Service.IsValidH3Index(h3Index);

// Get average metrics for resolution
double avgEdge = h3Service.GetAverageEdgeLength(7); // meters
double avgArea = h3Service.GetAverageArea(7); // m²
```

## H3BinningOperation

### Execution Flow

1. **Parameter Parsing**
   - Extract resolution, aggregation type, value field
   - Validate parameters

2. **Geometry Loading**
   - Load point geometries from input
   - Validate all geometries are points

3. **Binning**
   - Convert each point to H3 index
   - Group points by H3 index
   - Extract values from properties

4. **Aggregation**
   - Apply aggregation function per hexagon
   - Calculate statistics if requested

5. **Output Generation**
   - Create hexagon boundaries
   - Generate GeoJSON FeatureCollection
   - Return results with metadata

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `resolution` | int | 7 | H3 resolution (0-15) |
| `aggregation` | string | "count" | Aggregation type |
| `valueField` | string | null | Property field to aggregate |
| `includeBoundaries` | bool | true | Include hex boundaries |
| `includeStatistics` | bool | false | Include detailed stats |

### Aggregation Types

```csharp
public enum H3AggregationType
{
    Count,      // Number of points
    Sum,        // Sum of values
    Average,    // Mean of values
    Min,        // Minimum value
    Max,        // Maximum value
    StdDev,     // Standard deviation
    Median      // Median value
}
```

### Usage Example

```csharp
var operation = new H3BinningOperation();

var parameters = new Dictionary<string, object>
{
    ["resolution"] = 7,
    ["aggregation"] = "average",
    ["valueField"] = "temperature",
    ["includeBoundaries"] = true,
    ["includeStatistics"] = true
};

var inputs = new List<GeoprocessingInput>
{
    new()
    {
        Name = "points",
        Type = "geojson",
        Source = pointsGeoJson
    }
};

var result = await operation.ExecuteAsync(
    parameters,
    inputs,
    progress: new Progress<GeoprocessingProgress>(p =>
    {
        Console.WriteLine($"Progress: {p.ProgressPercent}% - {p.Message}");
    })
);

if (result.Success)
{
    var geoJson = result.Data["geojson"];
    var hexCount = result.Data["hexagonCount"];
    var pointCount = result.Data["pointCount"];
}
```

## API Endpoints

### POST /api/analysis/h3/bin

Bins point data into H3 hexagons with aggregation.

**Request:**
```json
{
  "resolution": 7,
  "aggregation": "average",
  "valueField": "temperature",
  "includeBoundaries": true,
  "includeStatistics": true,
  "async": false,
  "inputType": "geojson",
  "inputSource": "{...GeoJSON...}"
}
```

**Sync Response (async=false):**
```json
{
  "jobId": "job-123",
  "status": "completed",
  "result": {
    "geojson": "{...}",
    "hexagonCount": 1234,
    "pointCount": 50000,
    "resolution": 7,
    "aggregationType": "average",
    "avgHexagonArea": 5161000,
    "avgEdgeLength": 1220,
    "results": [
      {
        "h3Index": "872830828ffffff",
        "count": 42,
        "value": 23.5,
        "statistics": {
          "min": 18.2,
          "max": 28.9,
          "avg": 23.5,
          "stdDev": 2.3
        }
      }
    ]
  }
}
```

**Async Response (async=true):**
```json
{
  "jobId": "job-456",
  "status": "accepted",
  "message": "Job queued for processing"
}
```

### POST /api/analysis/h3/info

Get H3 resolution information.

**Request:**
```json
{
  "resolution": 7
}
```

**Response:**
```json
{
  "resolution": 7,
  "averageAreaKm2": 5.161,
  "averageAreaM2": 5161000,
  "averageEdgeLengthKm": 1.22,
  "averageEdgeLengthM": 1220,
  "totalCells": 4442882
}
```

### POST /api/analysis/h3/boundary

Get boundary polygon for H3 index.

**Request:**
```json
{
  "h3Index": "872830828ffffff"
}
```

**Response:**
```json
{
  "h3Index": "872830828ffffff",
  "resolution": 7,
  "boundary": {
    "type": "Polygon",
    "coordinates": [[
      [-122.419, 37.775],
      [-122.420, 37.776],
      [-122.421, 37.775],
      [-122.420, 37.774],
      [-122.419, 37.774],
      [-122.418, 37.775],
      [-122.419, 37.775]
    ]]
  },
  "center": [-122.4194, 37.7749],
  "areaM2": 5161234.5,
  "areaKm2": 5.161
}
```

### POST /api/analysis/h3/neighbors

Get neighboring hexagons.

**Request:**
```json
{
  "h3Index": "872830828ffffff",
  "ringDistance": 1
}
```

**Response:**
```json
{
  "h3Index": "872830828ffffff",
  "ringDistance": 1,
  "neighbors": [
    "872830829ffffff",
    "87283082affffff",
    "87283082bffffff",
    "872830828ffffff",
    "872830825ffffff",
    "872830827ffffff"
  ],
  "count": 6
}
```

## OGC Processes Integration

H3 binning is registered as an OGC Process:

```http
POST /processes/h3_binning/execution
Content-Type: application/json

{
  "inputs": {
    "resolution": 7,
    "aggregation": "count",
    "includeBoundaries": true
  },
  "response": "document"
}
```

## Performance Considerations

### Resolution Impact

| Resolution | Hexagons (Global) | Typical Dataset | Processing Time |
|-----------|-------------------|-----------------|-----------------|
| 0 | 122 | Continental | < 1s |
| 3 | 4,117,882 | Country | 1-5s |
| 5 | 2,016,842,882 | State | 5-30s |
| 7 | ~4.4 billion | City | 30s-5min |
| 9 | ~688 billion | District | 5-30min |
| 11+ | Trillions+ | Building | > 30min |

### Optimization Strategies

1. **Use appropriate resolution**
   - Start with resolution 5-7 for initial exploration
   - Increase resolution only for focused areas

2. **Filter data first**
   - Apply spatial filters before binning
   - Use CQL filters to reduce input size

3. **Async processing for large datasets**
   - Set `async: true` for > 100K points
   - Poll job status endpoint

4. **Batch processing**
   - Process multiple smaller regions vs one large region
   - Parallelize across tiles

5. **Caching**
   - Cache H3 binning results for static datasets
   - Invalidate cache on data updates

### Memory Requirements

Approximate memory usage:

```
Memory (MB) ≈ (PointCount × 100 bytes) + (HexagonCount × 500 bytes)
```

For 1M points at resolution 7 (~100K hexagons):
```
Memory ≈ (1,000,000 × 100) + (100,000 × 500) = 150 MB
```

## Error Handling

### Validation Errors

```csharp
// Resolution out of range
{
  "error": "H3 resolution must be between 0 and 15",
  "parameter": "resolution",
  "value": 20
}

// Invalid aggregation type
{
  "error": "Aggregation type must be one of: count, sum, average, min, max, stddev, median",
  "parameter": "aggregation",
  "value": "invalid"
}

// Non-point geometries
{
  "error": "All input geometries must be points. Found 5 non-point geometries.",
  "geometryTypes": ["Polygon", "LineString"]
}
```

### Processing Errors

```csharp
// Timeout
{
  "error": "Processing timeout exceeded",
  "timeout": 300,
  "processed": 500000,
  "total": 1000000
}

// Memory limit
{
  "error": "Memory limit exceeded",
  "limit": 2048,
  "used": 2500
}
```

## Testing

### Unit Tests

```csharp
[Test]
public void PointToH3_ValidCoordinates_ReturnsH3Index()
{
    var h3Service = new H3Service();
    var h3Index = h3Service.PointToH3(37.7749, -122.4194, 7);

    Assert.IsNotNull(h3Index);
    Assert.IsTrue(h3Service.IsValidH3Index(h3Index));
    Assert.AreEqual(7, h3Service.GetH3Resolution(h3Index));
}

[Test]
public async Task H3BinningOperation_ValidInput_ReturnsAggregatedResults()
{
    var operation = new H3BinningOperation();
    var parameters = new Dictionary<string, object>
    {
        ["resolution"] = 7,
        ["aggregation"] = "count"
    };

    var result = await operation.ExecuteAsync(parameters, testInputs);

    Assert.IsTrue(result.Success);
    Assert.Greater((int)result.Data["hexagonCount"], 0);
}
```

### Integration Tests

```csharp
[Test]
public async Task H3BinningEndpoint_LargeDataset_ProcessesSuccessfully()
{
    var client = CreateTestClient();
    var request = new H3BinRequest
    {
        Resolution = 7,
        Aggregation = "count",
        Async = true
    };

    var response = await client.PostAsJsonAsync("/api/analysis/h3/bin", request);
    Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode);

    var result = await response.Content.ReadFromJsonAsync<H3BinResponse>();
    Assert.IsNotNull(result.JobId);
}
```

## Deployment

### Dependencies

Add to `Honua.Server.Enterprise.csproj`:

```xml
<PackageReference Include="H3" Version="4.1.0" />
```

### Endpoint Registration

In `Program.cs` or startup:

```csharp
app.MapH3AnalysisEndpoints();
```

### Environment Variables

```bash
# H3 processing limits
H3_MAX_RESOLUTION=12
H3_MAX_POINTS=10000000
H3_TIMEOUT_SECONDS=600
H3_MAX_MEMORY_MB=4096
```

## Monitoring

### Metrics to Track

- **Processing time** by resolution
- **Memory usage** by point count
- **Hexagon counts** per request
- **Error rates** by error type
- **API response times**

### Logging

```csharp
logger.LogInformation("H3 binning started: Resolution={Resolution}, Points={Points}",
    resolution, pointCount);

logger.LogInformation("H3 binning completed: Hexagons={Hexagons}, Duration={Duration}ms",
    hexagonCount, duration);
```

## Security

### Rate Limiting

- Limit requests per user/tenant
- Throttle based on resolution and point count

### Resource Limits

- Max resolution per tier (Free: 10, Pro: 12, Enterprise: 15)
- Max points per request
- Max processing time
- Max memory usage

### Input Validation

- Sanitize H3 indices
- Validate coordinate ranges
- Check for malformed GeoJSON

## Best Practices

1. **Choose resolution wisely**: Match to zoom level and data density
2. **Use async for large datasets**: > 10K points
3. **Enable statistics selectively**: Only when needed
4. **Cache results**: Store pre-computed binnings
5. **Monitor performance**: Track processing times and resource usage
6. **Handle errors gracefully**: Provide meaningful error messages
7. **Document limitations**: Clearly state resolution and size limits

## References

- [H3 Official Documentation](https://h3geo.org/)
- [H3.NET Library](https://github.com/paillave/H3.NET)
- [Uber H3 Engineering Blog](https://eng.uber.com/h3/)
- [NetTopologySuite](https://github.com/NetTopologySuite/NetTopologySuite)

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0
