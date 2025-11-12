# H3 Hexagonal Binning Component

Advanced spatial aggregation using Uber's H3 hierarchical hexagonal grid system for density mapping and spatial analytics.

## Overview

The H3 Hexagonal Binning component provides a powerful way to visualize point data by aggregating it into hexagonal grid cells. Unlike traditional square grids, hexagons provide:

- **Equal distance to neighbors**: Each hexagon has 6 neighbors at equal distance
- **No orientation bias**: Hexagons distribute data more uniformly
- **Hierarchical structure**: H3 resolutions nest perfectly for multi-scale analysis
- **Efficient spatial indexing**: Fast point-to-cell lookups and neighbor queries

## Features

- ✅ **15 Resolution Levels**: From global (resolution 0) to sub-centimeter (resolution 15)
- ✅ **Multiple Aggregations**: Count, Sum, Average, Min, Max, StdDev, Median
- ✅ **Interactive Visualization**: Click hexagons for details, hover highlighting
- ✅ **Color Schemes**: 7 built-in color ramps (YlOrRd, Blues, Viridis, etc.)
- ✅ **Real-time Updates**: Dynamically adjust resolution and aggregation
- ✅ **Statistics**: Automatic calculation of min, max, count, and area metrics
- ✅ **Multi-scale**: Zoom-aware visualization with resolution recommendations

## Installation

### 1. Add H3.js Library

Include the H3.js library in your HTML:

```html
<script src="https://unpkg.com/h3-js@4.1.0/dist/h3-js.umd.js"></script>
```

### 2. Add Component to Page

```razor
@using Honua.MapSDK.Components.H3

<HonuaH3Hexagons
    Resolution="7"
    Aggregation="count"
    ColorScheme="YlOrRd"
    ShowControls="true"
    AutoRefresh="true" />
```

## Usage Examples

### Basic Point Count Visualization

```razor
<HonuaH3Hexagons
    Resolution="7"
    Aggregation="count"
    ColorScheme="YlOrRd"
    SourceLayer="my-points-layer" />
```

### Value-Based Aggregation

```razor
<HonuaH3Hexagons
    Resolution="8"
    Aggregation="average"
    ValueField="temperature"
    ColorScheme="Plasma"
    Opacity="0.8" />
```

### Programmatic Control

```razor
<HonuaH3Hexagons @ref="_h3Component"
    Resolution="@_currentResolution"
    OnStatsUpdated="HandleStatsUpdated"
    OnHexagonClick="HandleHexClick" />

<button @onclick="RefreshData">Refresh</button>
<button @onclick="IncreaseResolution">Zoom In</button>

@code {
    private HonuaH3Hexagons _h3Component;
    private int _currentResolution = 7;

    private async Task RefreshData()
    {
        await _h3Component.RefreshHexagons();
    }

    private void IncreaseResolution()
    {
        if (_currentResolution < 15)
        {
            _currentResolution++;
        }
    }

    private void HandleStatsUpdated(HonuaH3Hexagons.H3Stats stats)
    {
        Console.WriteLine($"Hexagons: {stats.HexagonCount}, Points: {stats.PointCount}");
    }

    private void HandleHexClick(HonuaH3Hexagons.H3HexagonClickEventArgs args)
    {
        Console.WriteLine($"Clicked H3: {args.H3Index}, Value: {args.Value}");
    }
}
```

## H3 Resolutions

Each resolution provides different hexagon sizes suitable for various use cases:

| Resolution | Avg Hexagon Edge | Avg Hexagon Area | Use Case |
|-----------|------------------|------------------|----------|
| 0 | ~1,107 km | ~4,250,000 km² | Global/Continental |
| 1 | ~418 km | ~607,000 km² | Large Countries |
| 2 | ~158 km | ~86,700 km² | Countries/States |
| 3 | ~59 km | ~12,400 km² | Regions |
| 4 | ~22 km | ~1,770 km² | Metropolitan Areas |
| 5 | ~8.5 km | ~252 km² | Cities |
| 6 | ~3.2 km | ~36 km² | City Districts |
| 7 | ~1.2 km | ~5.2 km² | **Neighborhoods** ⭐ |
| 8 | ~461 m | ~737,000 m² | City Blocks |
| 9 | ~174 m | ~105,000 m² | Buildings |
| 10 | ~66 m | ~15,000 m² | Building Floors |
| 11 | ~25 m | ~2,140 m² | Rooms |
| 12 | ~9.4 m | ~305 m² | Small Spaces |
| 13 | ~3.5 m | ~43.6 m² | Very Fine Detail |
| 14 | ~1.3 m | ~6.2 m² | Ultra Fine |
| 15 | ~0.5 m | ~0.9 m² | Sub-meter |

**Recommended starting resolution: 7** (neighborhood scale)

## Aggregation Types

### Count
Counts the number of points in each hexagon.

```razor
<HonuaH3Hexagons Aggregation="count" />
```

### Sum
Sums the values of a specified field.

```razor
<HonuaH3Hexagons
    Aggregation="sum"
    ValueField="sales_amount" />
```

### Average
Calculates the mean value of a field.

```razor
<HonuaH3Hexagons
    Aggregation="average"
    ValueField="temperature" />
```

### Min/Max
Finds minimum or maximum values.

```razor
<HonuaH3Hexagons
    Aggregation="max"
    ValueField="elevation" />
```

### Standard Deviation
Measures variability in the data.

```razor
<HonuaH3Hexagons
    Aggregation="stddev"
    ValueField="price" />
```

### Median
Finds the median value (robust to outliers).

```razor
<HonuaH3Hexagons
    Aggregation="median"
    ValueField="response_time" />
```

## API Endpoints

### Bin Data into H3 Hexagons

```http
POST /api/analysis/h3/bin
Content-Type: application/json

{
  "resolution": 7,
  "aggregation": "count",
  "valueField": "temperature",
  "includeBoundaries": true,
  "includeStatistics": true,
  "inputType": "geojson",
  "inputSource": "{GeoJSON data or collection ID}",
  "async": false
}
```

**Response:**
```json
{
  "jobId": "123e4567-e89b-12d3-a456-426614174000",
  "status": "completed",
  "result": {
    "geojson": "{...}",
    "hexagonCount": 1523,
    "pointCount": 50000,
    "resolution": 7,
    "aggregationType": "count",
    "avgHexagonArea": 5161000,
    "avgEdgeLength": 1220
  }
}
```

### Get H3 Resolution Info

```http
POST /api/analysis/h3/info
Content-Type: application/json

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

### Get H3 Hexagon Boundary

```http
POST /api/analysis/h3/boundary
Content-Type: application/json

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
    "coordinates": [[...]]
  },
  "center": [-122.4194, 37.7749],
  "areaM2": 5161234.5,
  "areaKm2": 5.161
}
```

### Get Neighboring Hexagons

```http
POST /api/analysis/h3/neighbors
Content-Type: application/json

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
    // ... 5 more neighbors
  ],
  "count": 6
}
```

## Component Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Id` | string | auto | Unique component identifier |
| `CssClass` | string | "" | Additional CSS classes |
| `ShowControls` | bool | true | Show control panel |
| `Resolution` | int | 7 | H3 resolution (0-15) |
| `Aggregation` | string | "count" | Aggregation type |
| `ValueField` | string? | null | Field to aggregate |
| `Opacity` | double | 0.7 | Hexagon opacity (0-1) |
| `ColorScheme` | string | "YlOrRd" | Color ramp name |
| `AutoRefresh` | bool | true | Auto-refresh on changes |
| `SourceLayer` | string? | null | Source layer ID |
| `OnHexagonClick` | EventCallback | - | Hexagon click handler |
| `OnStatsUpdated` | EventCallback | - | Stats update handler |

## Color Schemes

Available color schemes:

- **YlOrRd**: Yellow → Orange → Red (good for heat/intensity)
- **Blues**: Light Blue → Dark Blue (good for water/depth)
- **Greens**: Light Green → Dark Green (good for vegetation)
- **Viridis**: Perceptually uniform, colorblind-friendly
- **Plasma**: Purple → Yellow, high contrast
- **Inferno**: Black → Red → Yellow, dramatic
- **Turbo**: Rainbow-like, maximum contrast

## Performance Tips

1. **Choose appropriate resolution**:
   - Lower resolutions (0-6) for large datasets
   - Higher resolutions (7-10) for detailed city-level analysis
   - Very high resolutions (11+) only for small areas

2. **Use async mode for large datasets**:
   ```json
   { "async": true }
   ```

3. **Limit data extents**:
   - Filter data before binning
   - Use viewport bounds for dynamic loading

4. **Optimize aggregations**:
   - `count` is fastest
   - `median` and `stddev` are slowest

## Use Cases

### Urban Planning
- Population density mapping
- Traffic pattern analysis
- Service coverage visualization

### Real Estate
- Property value heatmaps
- Market analysis by neighborhood
- Amenity accessibility

### Environmental Science
- Temperature distribution
- Pollution monitoring
- Wildlife habitat analysis

### Retail & Business
- Customer distribution
- Sales territory mapping
- Competitive analysis

### Public Health
- Disease outbreak tracking
- Healthcare facility access
- Demographic analysis

## Integration with OGC Processes

H3 binning is available as an OGC Process:

```http
POST /processes/h3_binning/execution
Content-Type: application/json

{
  "inputs": {
    "resolution": 7,
    "aggregation": "count",
    "valueField": "temperature"
  },
  "response": "document"
}
```

## Best Practices

1. **Start with resolution 7**: Good balance of detail and performance
2. **Use zoom-based resolution**: Adjust resolution based on map zoom level
3. **Color scheme selection**: Match scheme to data type (sequential vs diverging)
4. **Include statistics**: Enable for better data insights
5. **Handle sparse data**: Lower resolutions for sparse datasets

## Troubleshooting

### Hexagons not appearing
- Ensure h3-js library is loaded
- Check that source data contains Point geometries
- Verify resolution is appropriate for data extent

### Performance issues
- Reduce resolution for large datasets
- Use async mode for long-running operations
- Filter data before processing

### Unexpected aggregation results
- Verify valueField exists in source data
- Check that field contains numeric values
- Consider data outliers affecting averages

## References

- [H3 Documentation](https://h3geo.org/)
- [H3.js Library](https://github.com/uber/h3-js)
- [H3 Resolution Table](https://h3geo.org/docs/core-library/restable/)
- [Uber Engineering Blog](https://eng.uber.com/h3/)

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0
