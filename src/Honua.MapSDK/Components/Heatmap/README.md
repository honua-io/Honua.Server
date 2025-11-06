# HonuaHeatmap Component

A powerful and flexible heatmap visualization component for Honua.MapSDK that renders point density using MapLibre GL JS heatmap layers.

## Overview

HonuaHeatmap transforms point data into beautiful density visualizations, making it easy to identify patterns, hot spots, and concentrations in your geospatial data. Perfect for crime analysis, demographics, sensor networks, and any scenario where understanding point density is crucial.

## Features

- **Multiple Data Sources**: Support for layer data, GeoJSON, CSV with lat/lon
- **Interactive Controls**: Real-time adjustment of radius, intensity, and opacity
- **Predefined Gradients**: 6 built-in color schemes including colorblind-friendly options
- **Custom Gradients**: Define your own color ramps with multiple stops
- **Weighted Heatmaps**: Weight points by attribute values
- **Statistics Display**: Real-time point count and density metrics
- **ComponentBus Integration**: Automatic synchronization with other map components
- **Dark Mode Support**: Beautiful UI in both light and dark themes
- **Export Capability**: Save heatmap visualizations as PNG images
- **Zoom-Based Visibility**: Configure min/max zoom levels for optimal display
- **Responsive Design**: Works seamlessly on desktop and mobile devices

## Installation

The HonuaHeatmap component is included in Honua.MapSDK. No additional installation required.

## Basic Usage

```razor
@page "/heatmap-demo"
@using Honua.MapSDK.Components.Heatmap
@using Honua.MapSDK.Models

<HonuaMap Id="main-map" Style="streets" Center="new[] { -122.4, 37.8 }" Zoom="10" />

<HonuaHeatmap
    SyncWith="main-map"
    DataSource="crime-layer"
    Radius="30"
    Intensity="1.0"
    Opacity="0.6"
    ColorGradient="HeatmapGradient.Hot"
    ShowControls="true" />
```

## Parameters

### Required Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `SyncWith` | `string` | Map ID to synchronize with |

### Data Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `DataSource` | `string?` | `null` | Layer ID, GeoJSON URL, or "data" for inline data |
| `Data` | `object?` | `null` | GeoJSON FeatureCollection for inline data |

### Heatmap Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Radius` | `int` | `30` | Heatmap radius in pixels (blur distance) |
| `Intensity` | `double` | `1.0` | Heatmap intensity (0-2) |
| `Opacity` | `double` | `0.6` | Heatmap opacity (0-1) |
| `ColorGradient` | `HeatmapGradient` | `Hot` | Predefined color gradient |
| `CustomGradient` | `Dictionary<double, string>?` | `null` | Custom color stops |
| `WeightProperty` | `string?` | `null` | Property for weighting points |
| `MaxZoom` | `int?` | `null` | Maximum zoom to show heatmap |
| `MinZoom` | `int?` | `null` | Minimum zoom to show heatmap |

### UI Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ShowControls` | `bool` | `true` | Display interactive controls |
| `ShowStatistics` | `bool` | `true` | Show statistics panel |
| `ShowWeightProperty` | `bool` | `true` | Show weight property selector |
| `AllowExport` | `bool` | `true` | Allow image export |
| `AutoSync` | `bool` | `true` | Auto-sync with map extent |
| `DarkMode` | `bool` | `false` | Enable dark mode |
| `CssClass` | `string?` | `null` | Additional CSS class |

### Event Callbacks

| Parameter | Type | Description |
|-----------|------|-------------|
| `OnHeatmapUpdated` | `EventCallback<HeatmapUpdatedEventArgs>` | Fired when heatmap configuration changes |
| `OnImageExported` | `EventCallback<string>` | Fired when image is exported (base64 data) |

## Color Gradients

### Predefined Gradients

#### Hot (Default)
Classic heat map colors from yellow through orange to red.
- **Use cases**: Crime density, temperature, fire risk
- **Colors**: Yellow → Orange → Red

#### Cool
Cool tones from cyan through blue to navy.
- **Use cases**: Water quality, precipitation, cooling effects
- **Colors**: Cyan → Blue → Navy

#### Rainbow
Full color spectrum for maximum differentiation.
- **Use cases**: Multi-category analysis, diverse datasets
- **Colors**: Violet → Red (full spectrum)

#### Viridis
Perceptually uniform and colorblind-friendly gradient.
- **Use cases**: Scientific visualization, accessible dashboards
- **Colors**: Yellow → Green → Blue → Purple
- **Note**: Recommended for colorblind accessibility

#### Plasma
High contrast gradient with warm-to-cool transition.
- **Use cases**: High-resolution data, detailed analysis
- **Colors**: Yellow → Pink → Purple → Blue

#### Inferno
Dramatic gradient from light to dark.
- **Use cases**: Night maps, dramatic visualizations
- **Colors**: Yellow → Orange → Red → Black

### Custom Gradients

Define your own color gradients using density stops:

```razor
@code {
    private Dictionary<double, string> customGradient = new()
    {
        { 0.0, "rgba(0, 0, 255, 0)" },      // Transparent blue at 0% density
        { 0.3, "rgba(0, 255, 255, 0.5)" },  // Semi-transparent cyan at 30%
        { 0.6, "rgba(255, 255, 0, 0.8)" },  // Mostly opaque yellow at 60%
        { 1.0, "rgba(255, 0, 0, 1)" }       // Fully opaque red at 100%
    };
}

<HonuaHeatmap
    SyncWith="main-map"
    DataSource="my-data"
    ColorGradient="HeatmapGradient.Custom"
    CustomGradient="@customGradient" />
```

## Advanced Usage

### Weighted Heatmap

Weight points by attribute values (e.g., population, severity, magnitude):

```razor
<HonuaHeatmap
    SyncWith="main-map"
    DataSource="earthquake-data"
    WeightProperty="magnitude"
    ColorGradient="HeatmapGradient.Inferno"
    Radius="40"
    Intensity="1.5" />
```

### Zoom-Based Visibility

Show heatmap at low zooms, switch to individual points at high zooms:

```razor
<HonuaHeatmap
    SyncWith="main-map"
    DataSource="poi-layer"
    MaxZoom="14"
    Radius="25"
    Intensity="1.2" />

<!-- Individual points visible beyond zoom 14 -->
```

### Inline GeoJSON Data

Provide data directly without a layer:

```razor
@code {
    private object heatmapData = new
    {
        type = "FeatureCollection",
        features = new[]
        {
            new
            {
                type = "Feature",
                geometry = new { type = "Point", coordinates = new[] { -122.4, 37.8 } },
                properties = new { value = 10 }
            },
            // ... more features
        }
    };
}

<HonuaHeatmap
    SyncWith="main-map"
    Data="@heatmapData"
    WeightProperty="value" />
```

### Event Handling

React to heatmap changes:

```razor
<HonuaHeatmap
    SyncWith="main-map"
    DataSource="my-data"
    OnHeatmapUpdated="HandleHeatmapUpdated"
    OnImageExported="HandleImageExported" />

@code {
    private void HandleHeatmapUpdated(HeatmapUpdatedEventArgs args)
    {
        Console.WriteLine($"Heatmap updated: {args.Statistics?.PointCount} points");
        Console.WriteLine($"Configuration: R={args.Configuration.Radius}, I={args.Configuration.Intensity}");
    }

    private async Task HandleImageExported(string imageData)
    {
        // imageData is base64-encoded PNG
        // Can be saved to database, sent to server, etc.
        await SaveToServer(imageData);
    }
}
```

### Programmatic Control

Control heatmap via component reference:

```razor
<HonuaHeatmap @ref="_heatmap" SyncWith="main-map" DataSource="my-data" />

<button @onclick="UpdateConfiguration">Change Settings</button>
<button @onclick="RefreshData">Refresh</button>

@code {
    private HonuaHeatmap _heatmap = null!;

    private async Task UpdateConfiguration()
    {
        var config = new HeatmapConfiguration
        {
            Radius = 50,
            Intensity = 1.5,
            Opacity = 0.8,
            Gradient = HeatmapGradient.Viridis
        };

        await _heatmap.SetConfigurationAsync(config);
    }

    private async Task RefreshData()
    {
        var newData = await FetchLatestData();
        await _heatmap.UpdateDataAsync(newData);
    }
}
```

## ComponentBus Integration

HonuaHeatmap integrates seamlessly with other MapSDK components via ComponentBus:

### Auto-Sync with Map Extent
When `AutoSync="true"`, statistics update automatically as the user pans/zooms the map.

### Data Request/Response
Automatically requests data from the specified layer or component.

### Filter Integration
Responds to filter changes from HonuaFilterPanel and other components.

### Layer Events
Publishes `LayerAddedMessage`, `LayerRemovedMessage`, and `LayerVisibilityChangedMessage`.

## Performance Tips

### Large Datasets

1. **Use appropriate zoom limits**: Set `MaxZoom` to switch to clustering or individual points
2. **Optimize radius**: Larger radius = better performance but less detail
3. **Consider data decimation**: Pre-filter points for overview maps
4. **Use GeoJSON tiles**: For massive datasets, use vector tiles instead of client-side GeoJSON

### Smooth Interactions

1. **Debounce updates**: Don't update data on every map move
2. **Lazy loading**: Load data only when heatmap layer is visible
3. **Progressive rendering**: Load coarse data first, refine on idle

### Mobile Devices

1. **Reduce radius on mobile**: Smaller screens need smaller radius
2. **Simplify gradients**: Fewer color stops = better performance
3. **Disable auto-sync**: Manual refresh on mobile to save battery

## Styling and Customization

### CSS Custom Properties

Override default styles using CSS:

```css
.my-custom-heatmap {
    --heatmap-control-bg: #1f2937;
    --heatmap-control-text: #f3f4f6;
    --heatmap-primary-color: #10b981;
}
```

### Custom Control Position

```razor
<HonuaHeatmap
    SyncWith="main-map"
    DataSource="my-data"
    CssClass="custom-position"
    ShowControls="true" />

<style>
    .custom-position .honua-heatmap-controls {
        top: 100px;
        left: 10px;
        right: auto;
    }
</style>
```

## Accessibility

- **Keyboard Navigation**: All controls are keyboard accessible
- **Screen Readers**: Proper ARIA labels on all interactive elements
- **Color Blind Friendly**: Viridis gradient recommended for accessibility
- **Focus Indicators**: Clear focus styles for all interactive elements

## Browser Support

- Chrome/Edge 90+
- Firefox 88+
- Safari 14+
- Mobile Safari 14+
- Chrome Android 90+

Requires WebGL support for MapLibre GL JS.

## Common Use Cases

### Crime Analysis
Identify crime hot spots by visualizing incident locations with severity weighting.

### Demographics
Display population density, income distribution, or demographic concentrations.

### Sensor Networks
Visualize IoT sensor readings like temperature, air quality, or noise levels.

### Retail Analytics
Show customer visit frequency, sales density, or foot traffic patterns.

### Wildlife Tracking
Display animal sightings or migration pattern densities.

### Network Coverage
Visualize WiFi access points, cellular towers, or signal strength.

### Transportation
Show traffic accident frequencies, parking availability, or transit usage.

## Troubleshooting

### Heatmap Not Displaying

1. **Check data format**: Ensure GeoJSON features are Point geometries
2. **Verify source ID**: DataSource must match an existing layer or source
3. **Check zoom level**: May be outside MinZoom/MaxZoom range
4. **Console errors**: Check browser console for JavaScript errors

### Performance Issues

1. **Reduce point count**: Filter or decimate data
2. **Lower radius**: Smaller radius = faster rendering
3. **Simplify gradient**: Use fewer color stops
4. **Disable auto-sync**: Update manually instead of on every map move

### Colors Not Matching

1. **Check gradient**: Ensure correct HeatmapGradient value
2. **Custom gradient format**: Verify density stops are 0-1 range
3. **Browser rendering**: Some gradients may appear slightly different across browsers

## API Reference

### Public Methods

#### `UpdateDataAsync(object geojsonData)`
Updates heatmap with new GeoJSON data.

```csharp
await heatmap.UpdateDataAsync(newData);
```

#### `SetConfigurationAsync(HeatmapConfiguration config)`
Updates heatmap configuration programmatically.

```csharp
var config = new HeatmapConfiguration { Radius = 50, Intensity = 1.5 };
await heatmap.SetConfigurationAsync(config);
```

#### `GetStatistics()`
Returns current heatmap statistics.

```csharp
var stats = heatmap.GetStatistics();
Console.WriteLine($"Points: {stats?.PointCount}");
```

## Related Components

- **HonuaMap**: Base map component
- **HonuaLayerList**: Manage layers including heatmaps
- **HonuaFilterPanel**: Filter data before heatmap visualization
- **HonuaLegend**: Display heatmap color scale
- **HonuaDataGrid**: View underlying point data

## Examples

See [Examples.md](Examples.md) for comprehensive examples including:
- Crime incident heatmap
- WiFi signal strength
- Population density
- Weather stations
- And more!

## License

Part of Honua.MapSDK. See main SDK license for details.

## Support

For issues, questions, or contributions, visit the Honua.MapSDK repository.
