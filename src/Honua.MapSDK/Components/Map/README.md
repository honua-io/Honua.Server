# HonuaMapLibre Component

A comprehensive MapLibre GL JS based interactive map control for Honua.MapSDK that integrates seamlessly with the LocationServices infrastructure.

## Overview

The `HonuaMapLibre` component provides a full-featured, production-ready mapping solution built on MapLibre GL JS v4.x. It supports both raster and vector tiles, integrates with the `IBasemapTileProvider` interface, and offers extensive customization options.

## Features

- **MapLibre GL JS v4.x Integration** - Modern, performant WebGL-based rendering
- **LocationServices Integration** - Direct integration with `IBasemapTileProvider` for dynamic tile sources
- **Flexible Configuration** - Supports `MapConfiguration` model or direct parameters
- **Rich Event Model** - Comprehensive events for map interactions
- **Marker Management** - Add, remove, and update markers with popups
- **Layer Management** - Dynamic layer control (add, remove, toggle visibility, opacity)
- **Style Switching** - Change basemaps on the fly
- **Viewport Control** - Programmatic navigation (fly to, jump to, fit bounds)
- **Accessibility** - ARIA labels, keyboard navigation
- **Responsive Design** - Adapts to container size changes
- **TypeScript Interop** - Full JavaScript interop with type safety
- **Built-in Controls** - Navigation, scale, fullscreen, geolocate controls

## Installation

### 1. Add MapLibre Support to Services

In your `Program.cs` or startup configuration:

```csharp
builder.Services.AddMapLibreSupport(options =>
{
    options.DefaultStyle = "https://demotiles.maplibre.org/style.json";
    options.DefaultCenter = new[] { -122.4194, 37.7749 }; // San Francisco
    options.DefaultZoom = 12;
    options.EnableAccessibility = true;
    options.EnableHashNavigation = false;
});
```

### 2. Reference in Your Razor Component

```razor
@using Honua.MapSDK.Components.Map
```

## Basic Usage

### Simple Map with Default Configuration

```razor
<HonuaMapLibre Height="600px"
              Width="100%"
              OnMapLoad="HandleMapLoad" />

@code {
    private void HandleMapLoad(MapLibreViewport viewport)
    {
        Console.WriteLine($"Map loaded at {viewport.Center[0]}, {viewport.Center[1]}");
    }
}
```

### Map with Configuration Model

```razor
<HonuaMapLibre Configuration="@mapConfig"
              Height="600px"
              OnMapClick="HandleMapClick"
              OnMapMove="HandleMapMove" />

@code {
    private MapConfiguration mapConfig = new()
    {
        Name = "My Map",
        Settings = new MapSettings
        {
            Style = "https://demotiles.maplibre.org/style.json",
            Center = new[] { -122.4194, 37.7749 },
            Zoom = 12,
            Pitch = 0,
            Bearing = 0
        }
    };

    private void HandleMapClick(MapClickEventArgs args)
    {
        Console.WriteLine($"Clicked at: {args.LngLat[0]}, {args.LngLat[1]}");

        if (args.Features?.Count > 0)
        {
            Console.WriteLine($"Found {args.Features.Count} features");
        }
    }

    private void HandleMapMove(MapMoveEventArgs args)
    {
        Console.WriteLine($"Map moved to: {args.Center[0]}, {args.Center[1]} at zoom {args.Zoom}");
    }
}
```

### Map with Basemap Provider Integration

```razor
<HonuaMapLibre Configuration="@azureMapConfig"
              Height="100vh"
              ShowControls="true"
              OnMapLoad="OnMapLoaded" />

@code {
    [Inject]
    public IBasemapTileProvider? BasemapProvider { get; set; }

    private MapConfiguration azureMapConfig = new()
    {
        Name = "Azure Basemap",
        Settings = new MapSettings
        {
            // Use tileset:// protocol to reference basemap provider
            Style = "tileset://azure-maps/microsoft.base.road",
            Center = new[] { -95.7129, 37.0902 }, // Center of USA
            Zoom = 4
        }
    };

    private async Task OnMapLoaded(MapLibreViewport viewport)
    {
        if (BasemapProvider != null)
        {
            var tilesets = await BasemapProvider.GetAvailableTilesetsAsync();
            Console.WriteLine($"Available tilesets: {tilesets.Count}");
        }
    }
}
```

## Advanced Usage

### Programmatic Navigation

```csharp
@ref HonuaMapLibre map;

// Fly to location with animation
await map.FlyToAsync(new[] { -122.4194, 37.7749 }, zoom: 14, duration: 2000);

// Jump to location without animation
await map.JumpToAsync(new[] { -73.9352, 40.7306 }, zoom: 12);

// Fit to bounds
await map.FitBoundsAsync(new[] { -122.5, 37.7, -122.3, 37.8 }, padding: 50);
```

### Adding Markers

```csharp
var marker = new MapLibreMarker
{
    Position = new[] { -122.4194, 37.7749 },
    Color = "#FF0000",
    Draggable = true,
    Popup = new MapLibrePopup
    {
        Html = "<h3>San Francisco</h3><p>The Golden City</p>",
        CloseButton = true
    }
};

var markerId = await map.AddMarkerAsync(marker);

// Update marker position
await map.UpdateMarkerPositionAsync(markerId, new[] { -122.42, 37.78 });

// Remove marker
await map.RemoveMarkerAsync(markerId);
```

### Layer Management

```csharp
// Add a GeoJSON layer
var source = new MapLibreSource
{
    Type = "geojson",
    Data = geoJsonData
};

await map.AddSourceAsync("my-source", source);

var layer = new MapLibreLayer
{
    Id = "my-layer",
    Type = "fill",
    Source = "my-source",
    Paint = new Dictionary<string, object>
    {
        ["fill-color"] = "#088",
        ["fill-opacity"] = 0.6
    }
};

await map.AddLayerAsync(layer);

// Toggle visibility
await map.SetLayerVisibilityAsync("my-layer", false);

// Adjust opacity
await map.SetLayerOpacityAsync("my-layer", 0.3);

// Remove layer
await map.RemoveLayerAsync("my-layer");
await map.RemoveSourceAsync("my-source");
```

### Loading GeoJSON Data

```csharp
var geoJson = new
{
    type = "FeatureCollection",
    features = new[]
    {
        new
        {
            type = "Feature",
            geometry = new
            {
                type = "Point",
                coordinates = new[] { -122.4194, 37.7749 }
            },
            properties = new { name = "San Francisco" }
        }
    }
};

await map.LoadGeoJsonAsync("cities", geoJson);
```

### Style Switching

```csharp
// Switch to satellite view
await map.SetStyleAsync("tileset://azure-maps/microsoft.imagery.satellite");

// Switch to road view
await map.SetStyleAsync("https://demotiles.maplibre.org/style.json");
```

### Adding Built-in Controls

```csharp
await map.AddNavigationControlAsync("top-right");
await map.AddScaleControlAsync("bottom-left");
await map.AddFullscreenControlAsync("top-right");
await map.AddGeolocateControlAsync("top-right");
```

### Querying Features

```csharp
// Query features at a point
var features = await map.QueryRenderedFeaturesAsync(new[] { 100.0, 200.0 });

// Query features in a bounding box
var featuresInBounds = await map.QueryRenderedFeaturesInBoundsAsync(
    new[] { 0.0, 0.0, 100.0, 100.0 },
    layerIds: new[] { "my-layer" }
);
```

## Component Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `MapId` | `string` | Auto-generated | Unique identifier for the map instance |
| `Configuration` | `MapConfiguration?` | `null` | Complete map configuration |
| `Height` | `string` | `"600px"` | Map height (CSS value) |
| `Width` | `string` | `"100%"` | Map width (CSS value) |
| `ShowControls` | `bool` | `true` | Show built-in controls |
| `EnableInteractions` | `bool` | `true` | Enable pan, zoom, rotate interactions |
| `CssClass` | `string?` | `null` | Custom CSS class for container |
| `ContainerStyle` | `string?` | `null` | Custom inline styles |
| `ChildContent` | `RenderFragment?` | `null` | Custom controls/overlays |

## Events

| Event | Type | Description |
|-------|------|-------------|
| `OnMapLoad` | `EventCallback<MapLibreViewport>` | Fired when map is fully loaded |
| `OnMapClick` | `EventCallback<MapClickEventArgs>` | Fired when map is clicked |
| `OnMapMove` | `EventCallback<MapMoveEventArgs>` | Fired when map moves |
| `OnViewportChange` | `EventCallback<ViewportChangeEventArgs>` | Fired when viewport changes |
| `OnStyleLoad` | `EventCallback` | Fired when style is loaded |
| `OnError` | `EventCallback<string>` | Fired when an error occurs |

## Public API Methods

### Navigation
- `FlyToAsync(center, zoom?, bearing?, pitch?, duration?)` - Animate to location
- `JumpToAsync(center, zoom?, bearing?, pitch?)` - Jump to location
- `FitBoundsAsync(bounds, padding?)` - Fit map to bounds

### Style & Layers
- `SetStyleAsync(styleUrl)` - Change map style
- `AddSourceAsync(sourceId, source)` - Add data source
- `RemoveSourceAsync(sourceId)` - Remove data source
- `AddLayerAsync(layer, beforeId?)` - Add map layer
- `RemoveLayerAsync(layerId)` - Remove map layer
- `SetLayerVisibilityAsync(layerId, visible)` - Toggle layer visibility
- `SetLayerOpacityAsync(layerId, opacity)` - Set layer opacity

### Markers
- `AddMarkerAsync(marker)` - Add marker to map
- `RemoveMarkerAsync(markerId)` - Remove marker
- `UpdateMarkerPositionAsync(markerId, position)` - Update marker position

### Viewport
- `GetViewportAsync()` - Get current viewport state
- `GetBoundsAsync()` - Get current bounds
- `GetCenterAsync()` - Get current center
- `GetZoomAsync()` - Get current zoom
- `ResizeAsync()` - Resize map (after container change)

### Data
- `LoadGeoJsonAsync(sourceId, geoJson, layer?)` - Load GeoJSON data
- `QueryRenderedFeaturesAsync(point, layerIds?)` - Query features at point
- `QueryRenderedFeaturesInBoundsAsync(bbox, layerIds?)` - Query features in bounds

### Controls
- `AddNavigationControlAsync(position?)` - Add zoom/compass control
- `AddScaleControlAsync(position?)` - Add scale control
- `AddFullscreenControlAsync(position?)` - Add fullscreen control
- `AddGeolocateControlAsync(position?)` - Add geolocation control

### Configuration
- `LoadConfigurationAsync(configuration)` - Load and apply MapConfiguration

## Integration with LocationServices

The component seamlessly integrates with `IBasemapTileProvider`:

```csharp
// In your startup
builder.Services.AddSingleton<IBasemapTileProvider, AzureMapsBasemapProvider>();
builder.Services.AddMapLibreSupport();

// In your component
<HonuaMapLibre Configuration="@config" />

@code {
    private MapConfiguration config = new()
    {
        Settings = new MapSettings
        {
            // Reference tileset by provider key and tileset ID
            Style = "tileset://azure-maps/microsoft.base.road"
        }
    };
}
```

The component will automatically:
1. Detect the `tileset://` protocol
2. Query the basemap provider for available tilesets
3. Retrieve the tile URL template
4. Build a MapLibre style object
5. Apply the style to the map

## Styling & Theming

The component includes built-in CSS with support for:
- Responsive design (mobile, tablet, desktop)
- Dark mode (`prefers-color-scheme: dark`)
- High contrast mode (`prefers-contrast: high`)
- Print styles
- Accessibility focus indicators

Custom styling can be applied via `CssClass` or `ContainerStyle` parameters.

## Accessibility

The component follows WCAG 2.1 guidelines:
- Semantic HTML with ARIA labels
- Keyboard navigation support (arrow keys, +/-, home/end)
- Focus indicators
- Screen reader compatible
- High contrast mode support

## Browser Support

- Chrome/Edge 90+
- Firefox 88+
- Safari 14+
- Mobile browsers (iOS Safari 14+, Chrome Mobile)

## Performance

- WebGL-based rendering for smooth interactions
- Tile caching for reduced bandwidth
- Vector tile support for smaller file sizes
- Lazy loading of MapLibre GL JS library
- Efficient event debouncing

## Examples

See the `/examples` directory for complete examples including:
- Basic map setup
- Azure Maps integration
- Custom markers and popups
- Layer management
- GeoJSON visualization
- 3D terrain
- Data-driven styling

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0
