# Honua Leaflet Map Component

A lightweight, Leaflet-based map control as an alternative to MapLibre for the Honua MapSDK. This component provides a simpler, raster-tile focused mapping solution with extensive plugin support.

## Overview

The HonuaLeaflet component offers:

- **Lightweight Alternative**: Smaller footprint than MapLibre for simple mapping needs
- **Raster Tile Support**: Optimized for PNG/JPEG tile layers
- **Consistent API**: Same component API as HonuaMapLibre for easy switching
- **Plugin Ecosystem**: Built-in support for popular Leaflet plugins
- **Touch-Friendly**: Responsive design with mobile-optimized controls
- **IBasemapTileProvider Integration**: Works with your existing tile providers

## Installation

### 1. Add Leaflet Support to Services

```csharp
// Program.cs or Startup.cs
builder.Services.AddHonuaMapSDK();
builder.Services.AddLeafletSupport(config =>
{
    config.AddCommonBasemaps(); // Optional: Add predefined basemaps
    config.DefaultZoom = 10;
    config.EnableMarkerCluster = true;
    config.EnableDraw = true;
    config.EnableMeasure = true;
    config.EnableFullscreen = true;
});
```

### 2. Include Leaflet CDN Resources

Add to your `_Host.cshtml`, `_Layout.cshtml`, or `App.razor`:

```html
<!-- Leaflet Core -->
<link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" />
<script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>

<!-- Leaflet Plugins (optional) -->
<!-- Marker Clustering -->
<link rel="stylesheet" href="https://unpkg.com/leaflet.markercluster@1.5.3/dist/MarkerCluster.Default.css" />
<script src="https://unpkg.com/leaflet.markercluster@1.5.3/dist/leaflet.markercluster.js"></script>

<!-- Drawing Tools -->
<link rel="stylesheet" href="https://unpkg.com/leaflet-draw@1.0.4/dist/leaflet.draw.css" />
<script src="https://unpkg.com/leaflet-draw@1.0.4/dist/leaflet.draw.js"></script>

<!-- Measure Tools -->
<link rel="stylesheet" href="https://unpkg.com/leaflet-measure@3.1.0/dist/leaflet-measure.css" />
<script src="https://unpkg.com/leaflet-measure@3.1.0/dist/leaflet-measure.js"></script>

<!-- Fullscreen Control -->
<link rel="stylesheet" href="https://unpkg.com/leaflet.fullscreen@2.4.0/Control.FullScreen.css" />
<script src="https://unpkg.com/leaflet.fullscreen@2.4.0/Control.FullScreen.js"></script>
```

Or use the helper method:

```razor
@inject LeafletConfiguration LeafletConfig

@((MarkupString)LeafletConfig.GetCdnIncludes())
```

## Basic Usage

### Simple Map

```razor
<HonuaLeaflet Id="my-map"
              Center="@(new[] { -157.8583, 21.3099 })"
              Zoom="10"
              OnMapReady="HandleMapReady" />

@code {
    private void HandleMapReady(MapReadyMessage message)
    {
        Console.WriteLine($"Map {message.MapId} is ready!");
    }
}
```

### Custom Tile Layer

```razor
<HonuaLeaflet TileUrl="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
              Attribution="&copy; OpenStreetMap contributors"
              Center="@(new[] { 0.0, 0.0 })"
              Zoom="2" />
```

### With All Features Enabled

```razor
<HonuaLeaflet Id="full-featured-map"
              Center="@_center"
              Zoom="10"
              EnableFullscreen="true"
              EnableMeasure="true"
              EnableDraw="true"
              EnableMarkerCluster="true"
              MaxClusterRadius="80"
              OnMapReady="OnMapReady"
              OnExtentChanged="OnExtentChanged"
              OnFeatureClicked="OnFeatureClicked"
              OnDrawCreated="OnDrawCreated"
              OnMeasureComplete="OnMeasureComplete" />
```

## Switching Between MapLibre and Leaflet

The component API is designed to be interchangeable:

```razor
@if (useMapLibre)
{
    <HonuaMapLibre Id="@mapId"
                   Center="@center"
                   Zoom="@zoom"
                   OnMapReady="OnMapReady" />
}
else
{
    <HonuaLeaflet Id="@mapId"
                  Center="@center"
                  Zoom="@zoom"
                  OnMapReady="OnMapReady" />
}
```

## Working with Layers

### Add GeoJSON Layer

```csharp
await leafletMap.AddGeoJsonLayerAsync("my-layer", new
{
    type = "FeatureCollection",
    features = new[]
    {
        new
        {
            type = "Feature",
            geometry = new { type = "Point", coordinates = new[] { -157.8583, 21.3099 } },
            properties = new Dictionary<string, object> { ["name"] = "Honolulu" }
        }
    }
}, new Dictionary<string, object>
{
    ["color"] = "#3388ff",
    ["weight"] = 2
});
```

### Add WMS Layer

```csharp
await leafletMap.AddWmsLayerAsync("wms-layer",
    "https://example.com/wms",
    new Dictionary<string, object>
    {
        ["layers"] = "layer_name",
        ["format"] = "image/png",
        ["transparent"] = true
    });
```

### Remove Layer

```csharp
await leafletMap.RemoveLayerAsync("my-layer");
```

## Working with Markers

### Add Single Marker

```csharp
await leafletMap.AddMarkerAsync(
    "marker-1",
    new[] { -157.8583, 21.3099 }, // lng, lat
    "<h3>Honolulu</h3><p>Population: 345,064</p>",
    new Dictionary<string, object>
    {
        ["draggable"] = true
    }
);
```

### Remove Marker

```csharp
await leafletMap.RemoveMarkerAsync("marker-1");
```

## Navigation and View Control

### Fly to Location

```csharp
await leafletMap.FlyToAsync(new[] { -157.8583, 21.3099 }, zoom: 12);
```

### Fit to Bounds

```csharp
await leafletMap.FitBoundsAsync(
    new[] { -158.0, 21.0, -157.5, 21.5 }, // [west, south, east, north]
    padding: 50
);
```

### Get Current View

```csharp
var center = await leafletMap.GetCenterAsync();
var zoom = await leafletMap.GetZoomAsync();
var bounds = await leafletMap.GetBoundsAsync();
```

## Event Handling

### Map Events

```razor
<HonuaLeaflet OnMapReady="OnMapReady"
              OnExtentChanged="OnExtentChanged"
              OnFeatureClicked="OnFeatureClicked" />

@code {
    private void OnMapReady(MapReadyMessage msg)
    {
        Console.WriteLine($"Map ready at {msg.Center[0]}, {msg.Center[1]}");
    }

    private void OnExtentChanged(MapExtentChangedMessage msg)
    {
        Console.WriteLine($"Zoom: {msg.Zoom}, Bounds: {string.Join(", ", msg.Bounds)}");
    }

    private void OnFeatureClicked(FeatureClickedMessage msg)
    {
        Console.WriteLine($"Clicked feature {msg.FeatureId} on layer {msg.LayerId}");
    }
}
```

### Drawing Events

```razor
<HonuaLeaflet EnableDraw="true"
              OnDrawCreated="OnDrawCreated" />

@code {
    private void OnDrawCreated(Dictionary<string, object> data)
    {
        var type = data["type"]?.ToString();
        var geometry = data["geometry"];
        Console.WriteLine($"Drew {type}: {JsonSerializer.Serialize(geometry)}");
    }
}
```

### Measurement Events

```razor
<HonuaLeaflet EnableMeasure="true"
              OnMeasureComplete="OnMeasureComplete" />

@code {
    private void OnMeasureComplete(Dictionary<string, object> data)
    {
        var length = data.ContainsKey("length") ? data["length"] : null;
        var area = data.ContainsKey("area") ? data["area"] : null;
        Console.WriteLine($"Measured - Length: {length}m, Area: {area}m²");
    }
}
```

## Configuration Options

### Component Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Id` | string | auto-generated | Unique map identifier |
| `TileUrl` | string | OSM tiles | Tile layer URL template |
| `Attribution` | string | OSM attribution | Attribution text |
| `Center` | double[] | [0, 0] | Initial center [lng, lat] |
| `Zoom` | double | 2 | Initial zoom level |
| `MaxBounds` | double[]? | null | Restrict map extent |
| `MinZoom` | double? | null | Minimum zoom level |
| `MaxZoom` | double? | null | Maximum zoom level |
| `EnableFullscreen` | bool | true | Enable fullscreen control |
| `EnableMeasure` | bool | false | Enable measure tools |
| `EnableDraw` | bool | false | Enable drawing tools |
| `EnableMarkerCluster` | bool | false | Enable marker clustering |
| `MaxClusterRadius` | int | 80 | Cluster radius in pixels |

## Basemap Providers

### Predefined Basemaps

The `LeafletConfiguration.AddCommonBasemaps()` method adds these basemaps:

- **osm**: OpenStreetMap standard tiles
- **osm-hot**: Humanitarian OpenStreetMap
- **cartodb-dark**: CartoDB Dark Matter
- **cartodb-light**: CartoDB Positron
- **esri-worldimagery**: Esri World Imagery (satellite)
- **esri-worldstreetmap**: Esri World Street Map
- **stamen-terrain**: Stamen Terrain
- **stamen-watercolor**: Stamen Watercolor

### Using Predefined Basemaps

```csharp
@inject LeafletConfiguration LeafletConfig

@code {
    protected override void OnInitialized()
    {
        var basemap = LeafletConfig.CustomTileLayers["cartodb-dark"];
        // Use basemap.Url and basemap.Attribution
    }
}
```

## Integration with IBasemapTileProvider

```csharp
@inject IBasemapTileProvider TileProvider

@code {
    private string? _tileUrl;
    private string? _attribution;

    protected override async Task OnInitializedAsync()
    {
        _tileUrl = await TileProvider.GetTileUrlTemplateAsync("raster-road-base");
        _attribution = "Custom tiles";
    }
}

<HonuaLeaflet TileUrl="@_tileUrl" Attribution="@_attribution" />
```

## Advanced Styling

### Custom Layer Styles

```csharp
var style = new LeafletLayerStyle
{
    Color = "#ff0000",
    Weight = 3,
    Opacity = 0.8,
    FillColor = "#ff0000",
    FillOpacity = 0.4
};

await leafletMap.AddGeoJsonLayerAsync("styled-layer", geoJson, new Dictionary<string, object>
{
    ["color"] = style.Color,
    ["weight"] = style.Weight,
    ["fillOpacity"] = style.FillOpacity
});
```

## Performance Tips

1. **Use Marker Clustering**: For maps with many markers, enable clustering to improve performance
2. **Limit Max Zoom**: Set `MaxZoom` to prevent excessive tile loading
3. **Debounce Events**: The component already debounces extent changes by 100ms
4. **Remove Unused Layers**: Clean up layers when no longer needed
5. **Disable Unused Plugins**: Only enable plugins you actually use

## Browser Support

- Chrome/Edge (latest 2 versions)
- Firefox (latest 2 versions)
- Safari (latest 2 versions)
- Mobile browsers (iOS Safari, Chrome Mobile)

## Leaflet vs MapLibre: When to Use Which

### Use Leaflet When:
- You need simple raster tile display
- You want smaller bundle size
- You're using WMS services
- You need extensive Leaflet plugin ecosystem
- You prefer traditional marker/popup patterns

### Use MapLibre When:
- You need vector tiles
- You want 3D/terrain features
- You need complex styling capabilities
- You require better performance with large datasets
- You want modern WebGL rendering

## Troubleshooting

### Map Not Displaying

1. Ensure Leaflet CSS and JS are loaded:
   ```html
   <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" />
   <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
   ```

2. Check browser console for JavaScript errors

3. Verify container has explicit height:
   ```html
   <div style="height: 500px;">
       <HonuaLeaflet ... />
   </div>
   ```

### Plugins Not Working

Ensure plugin scripts are loaded in correct order:
1. Leaflet core
2. Plugin scripts
3. Your Blazor app

### Tiles Not Loading

1. Check tile URL template is correct (must include `{z}`, `{x}`, `{y}`)
2. Verify tile server is accessible
3. Check for CORS issues in browser console

## API Reference

See the following files for complete API documentation:

- `/src/Honua.MapSDK/Components/Map/HonuaLeaflet.razor` - Component implementation
- `/src/Honua.MapSDK/Models/LeafletOptions.cs` - Configuration classes
- `/src/Honua.MapSDK/wwwroot/js/leaflet-interop.js` - JavaScript interop

## Examples

See `/src/Honua.MapSDK/Components/Map/LeafletExample.razor` for a complete working example.

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0
