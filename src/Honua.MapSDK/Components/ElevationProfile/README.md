# HonuaElevationProfile

A comprehensive elevation profile component for Honua.MapSDK that displays interactive elevation charts for routes, paths, and trails. Perfect for hiking, cycling, infrastructure planning, and any application requiring terrain analysis.

## Features

- **Interactive Elevation Charts** - Visualize elevation changes along any path
- **Multiple Data Sources** - Support for MapLibre Terrain, Mapbox, Open-Elevation, USGS, and Google APIs
- **Rich Analytics** - Calculate elevation gain/loss, grades, steep sections, and time estimates
- **Map Synchronization** - Hover on chart to highlight position on map
- **Grade Visualization** - Color-coded sections based on slope severity
- **Waypoint Management** - Automatic detection of summits, valleys, and custom waypoints
- **Export Options** - Export as PNG, CSV, GPX, or JSON
- **Responsive Design** - Works on desktop and mobile devices
- **Dark Mode** - Automatic dark mode support

## Installation

The HonuaElevationProfile component is included in Honua.MapSDK. No additional installation required.

## Basic Usage

```razor
@page "/elevation-demo"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.ElevationProfile
@using Honua.MapSDK.Models

<HonuaMap Id="map1"
          Center="new[] { -119.5383, 37.8651 }"
          Zoom="10"
          Style="outdoors" />

<HonuaElevationProfile
    SyncWith="map1"
    Position="bottom-right"
    ShowStatistics="true"
    AllowDraw="true" />
```

## Parameters

### Core Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Id` | string | auto-generated | Unique identifier for the component |
| `SyncWith` | string | null | Map ID to synchronize with |
| `ElevationSource` | ElevationSource | OpenElevation | Source for elevation data |
| `ApiKey` | string | null | API key for external services |
| `SamplePoints` | int | 100 | Number of points to sample along route |
| `Unit` | MeasurementUnit | Metric | Measurement unit system |

### Display Options

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ShowStatistics` | bool | true | Show statistics panel |
| `ShowGradeColors` | bool | true | Color-code chart by grade |
| `ShowToolbar` | bool | true | Show toolbar with controls |
| `ChartHeight` | int | 300 | Chart height in pixels |
| `AllowDraw` | bool | true | Allow path drawing on map |
| `Collapsible` | bool | true | Allow collapsible sections |

### Layout

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Position` | string | null | Position on map (top-right, bottom-left, etc.) |
| `Width` | string | 400px | Component width |
| `CssClass` | string | null | Custom CSS class |
| `Style` | string | null | Custom inline styles |

### Event Callbacks

| Parameter | Type | Description |
|-----------|------|-------------|
| `OnProfileGenerated` | EventCallback<ElevationProfile> | Fired when profile is generated |
| `OnError` | EventCallback<string> | Fired when error occurs |

## Elevation Data Sources

### 1. Open-Elevation (Recommended for Testing)

**Free, no API key required**

```razor
<HonuaElevationProfile
    SyncWith="map1"
    ElevationSource="ElevationSource.OpenElevation" />
```

**Pros:**
- Free and open source
- No API key required
- Good global coverage

**Cons:**
- Rate limited
- Slower response times
- Lower resolution

**API Endpoint:** `https://api.open-elevation.com/api/v1/lookup`

### 2. Mapbox Terrain API

**Requires API key, high quality**

```razor
<HonuaElevationProfile
    SyncWith="map1"
    ElevationSource="ElevationSource.MapboxAPI"
    ApiKey="pk.your_mapbox_token" />
```

**Pros:**
- High resolution (30m globally, 10m in select areas)
- Fast response times
- Global coverage

**Cons:**
- Requires API key
- Usage fees apply

**Pricing:** Free tier includes 100,000 requests/month
**API Docs:** https://docs.mapbox.com/api/maps/tilequery/

### 3. USGS Elevation Point Query Service

**Free for US locations**

```razor
<HonuaElevationProfile
    SyncWith="map1"
    ElevationSource="ElevationSource.USGSAPI" />
```

**Pros:**
- Free, no API key
- High accuracy for US locations
- Official government data

**Cons:**
- US only
- Individual point queries (slower for many points)

**API Endpoint:** `https://epqs.nationalmap.gov/v1/json`

### 4. Google Elevation API

**Requires API key, very reliable**

```razor
<HonuaElevationProfile
    SyncWith="map1"
    ElevationSource="ElevationSource.GoogleAPI"
    ApiKey="YOUR_GOOGLE_API_KEY" />
```

**Pros:**
- Excellent global coverage
- High reliability
- Fast response times

**Cons:**
- Requires API key
- Usage fees

**Pricing:** $5 per 1,000 requests (free $200 monthly credit)
**API Docs:** https://developers.google.com/maps/documentation/elevation

### 5. MapLibre Terrain

**Uses terrain from current map style**

```razor
<HonuaElevationProfile
    SyncWith="map1"
    ElevationSource="ElevationSource.MapLibreTerrain" />
```

**Pros:**
- No external API calls
- Uses existing terrain data

**Cons:**
- Requires terrain in map style
- Limited to available terrain resolution

## Input Methods

### 1. Draw Path on Map

Enable drawing mode to trace a path directly on the map:

```razor
<HonuaElevationProfile
    SyncWith="map1"
    AllowDraw="true" />
```

Click the draw button, then click on the map to add points. Double-click to finish.

### 2. Import from GPX/KML

```csharp
// Handle imported route
private async Task OnDataImported(DataImportedMessage message)
{
    if (message.Format == "GPX" || message.Format == "KML")
    {
        // Elevation profile will automatically process imported routes
    }
}
```

### 3. Programmatic Path

```csharp
@code {
    private List<double[]> pathCoordinates = new()
    {
        new[] { -119.5383, 37.8651 },
        new[] { -119.5683, 37.8951 },
        new[] { -119.6083, 37.9251 }
    };

    private async Task LoadCustomPath()
    {
        // Component will pick up path from ComponentBus
        await Bus.PublishAsync(new FeatureDrawnMessage
        {
            FeatureId = Guid.NewGuid().ToString(),
            GeometryType = "LineString",
            Geometry = new {
                type = "LineString",
                coordinates = pathCoordinates
            },
            ComponentId = "elevation-1"
        });
    }
}
```

## Chart Customization

### Grade Color Scheme

By default, the chart uses color-coding based on grade severity:

- **Green** - Flat (< 5% grade)
- **Yellow** - Moderate (5-15% grade)
- **Red** - Steep (> 15% grade)

Disable with `ShowGradeColors="false"`.

### Chart Interactions

**Hover:** Hover over the chart to see elevation details and highlight position on map.

**Click:** Click a point on the chart to fly the map to that location.

**Zoom:** Use mouse wheel or pinch gestures to zoom into chart sections.

## Statistics

The statistics panel displays:

- **Distance** - Total route distance
- **Elevation Gain** - Total uphill climb
- **Elevation Loss** - Total downhill descent
- **Max Elevation** - Highest point
- **Min Elevation** - Lowest point
- **Average Grade** - Mean slope percentage
- **Max Grade** - Steepest section
- **Estimated Time** - Based on activity type

## Steep Sections

Steep sections are automatically identified and highlighted:

```razor
<HonuaElevationProfile
    SyncWith="map1"
    ShowGradeColors="true" />
```

Severity levels:
- **Flat** - < 5%
- **Low** - 5-10%
- **Moderate** - 10-15%
- **High** - 15-20%
- **Extreme** - > 20%

Click a steep section to zoom the map to that area.

## Waypoints

Waypoints are automatically added for:

- **Start** - Beginning of route
- **End** - End of route
- **Summit** - Highest elevation point
- **Valley** - Lowest elevation point (if significant)

Add custom waypoints programmatically:

```csharp
profile.Waypoints.Add(new Waypoint
{
    Name = "Water Source",
    Coordinates = new[] { -119.5683, 37.8951 },
    Type = WaypointType.Water,
    Distance = 5200.0,
    Elevation = 1250.0
});
```

## Time Estimation

Enable time estimation based on activity type:

```razor
<HonuaElevationProfile
    SyncWith="map1"
    ActivityType="ActivityType.Hiking" />
```

**Activity Types:**
- **Hiking** - 4 km/h base speed
- **Running** - 10 km/h base speed
- **Cycling** - 20 km/h base speed
- **Mountain Biking** - 15 km/h base speed
- **Walking** - 5 km/h base speed

Time estimation uses Naismith's rule: 1 hour per 5km + 1 hour per 600m elevation gain.

## Export Options

### Export as PNG

```csharp
// Exports chart as image
await ElevationProfileRef.ExportProfile(ElevationExportFormat.PNG);
```

### Export as CSV

```csharp
// Exports elevation data as CSV
// Columns: Distance, Elevation, Grade, Cumulative Gain, Cumulative Loss
await ElevationProfileRef.ExportProfile(ElevationExportFormat.CSV);
```

### Export as GPX

```csharp
// Exports as GPS Exchange Format
// Compatible with GPS devices and mapping software
await ElevationProfileRef.ExportProfile(ElevationExportFormat.GPX);
```

### Export as JSON

```csharp
// Exports complete profile data
await ElevationProfileRef.ExportProfile(ElevationExportFormat.JSON);
```

## ComponentBus Integration

The component publishes and subscribes to several messages:

### Published Messages

```csharp
// Profile generated successfully
ElevationProfileGeneratedMessage
{
    ProfileId,
    TotalDistance,
    ElevationGain,
    ElevationLoss,
    ComponentId
}

// Point on chart hovered
ElevationPointHoveredMessage
{
    ProfileId,
    Distance,
    Elevation,
    Coordinates,
    Grade,
    ComponentId
}

// Point on chart clicked
ElevationPointClickedMessage
{
    ProfileId,
    Distance,
    Elevation,
    Coordinates,
    ComponentId
}
```

### Subscribed Messages

```csharp
// Map ready
MapReadyMessage

// Feature drawn (for path input)
FeatureDrawnMessage

// Data imported (for GPX/KML)
DataImportedMessage
```

## Advanced Configuration

### Custom Elevation Service

```razor
<HonuaElevationProfile
    SyncWith="map1"
    ElevationSource="ElevationSource.Custom"
    CustomServiceUrl="https://your-elevation-api.com" />
```

### Smoothing

Apply smoothing to reduce noise in elevation data:

```csharp
var options = new ElevationProfileOptions
{
    Smoothing = 0.3, // 0.0 = no smoothing, 1.0 = maximum
    SamplePoints = 100
};
```

### Custom Grade Thresholds

```csharp
var options = new ElevationProfileOptions
{
    SteepGradeThreshold = 15.0 // Consider >15% as steep
};
```

## Best Practices

### 1. Sample Point Selection

- **Short routes (< 5km):** 50-100 points
- **Medium routes (5-20km):** 100-200 points
- **Long routes (> 20km):** 200-500 points

More points = more accurate but slower.

### 2. API Rate Limiting

When using free APIs:
- Use Open-Elevation for development
- Cache results when possible
- Limit sample points to reduce requests

### 3. Mobile Optimization

```razor
<HonuaElevationProfile
    SyncWith="map1"
    Position="bottom-left"
    Width="calc(100vw - 40px)"
    ChartHeight="200" />
```

### 4. Performance

- Debounce hover events
- Use appropriate sample point count
- Enable chart zoom for detailed analysis

## Troubleshooting

### Profile not generating

**Issue:** No elevation data returned

**Solutions:**
1. Check internet connection
2. Verify API key (if required)
3. Ensure path has at least 2 points
4. Try different elevation source
5. Check browser console for errors

### Incorrect elevations

**Issue:** Elevation values seem wrong

**Solutions:**
1. Verify coordinate order (longitude, latitude)
2. Check elevation source accuracy
3. Try different data source
4. Verify coordinates are valid

### Chart not displaying

**Issue:** Chart container empty

**Solutions:**
1. Ensure Chart.js loaded successfully
2. Check chart container has height
3. Verify profile data structure
4. Check browser console for errors

### API rate limiting

**Issue:** "Rate limit exceeded" errors

**Solutions:**
1. Reduce sample points
2. Add delays between requests
3. Use different API provider
4. Implement caching

## Browser Support

- Chrome/Edge 90+
- Firefox 88+
- Safari 14+
- Mobile browsers (iOS Safari, Chrome Mobile)

## Dependencies

- **Chart.js 4.4+** - Charting library (loaded automatically)
- **Turf.js** - Geospatial calculations (loaded automatically)
- **MudBlazor** - UI components
- **MapLibre GL JS** - Map rendering

## API Reference

See [Examples.md](./Examples.md) for complete code examples.

## Related Components

- **HonuaDraw** - Draw paths and shapes on map
- **HonuaImportWizard** - Import GPX/KML files
- **HonuaTimeline** - Temporal data visualization
- **HonuaChart** - General-purpose charting

## License

Part of Honua.MapSDK - see main SDK documentation for license information.

## Support

For issues, questions, or feature requests, please refer to the main Honua.MapSDK documentation.
