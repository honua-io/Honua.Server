# Deck.gl Integration for Honua.MapSDK

Comprehensive guide for using Deck.gl's WebGL-powered big data visualization with Honua.MapSDK.

## Overview

Deck.gl is a WebGL-powered framework for visual exploratory data analysis of large datasets. This integration brings Deck.gl's powerful visualization capabilities to Honua.MapSDK, enabling you to:

- Visualize millions of data points with high performance
- Create 3D aggregations (hexagons, grids)
- Render origin-destination flows (arcs)
- Build interactive heatmaps
- Support real-time data updates

## Architecture

The Deck.gl integration consists of:

1. **JavaScript Layer** (`deck-gl-integration.js`)
   - `DeckLayerManager`: Manages Deck.gl overlay on MapLibre maps
   - Syncs Deck.gl view state with MapLibre camera
   - Handles layer lifecycle (add, update, remove)

2. **C# Models** (`DeckLayerDefinition.cs`)
   - Type-safe layer definitions for each Deck.gl layer type
   - Serializable to JSON for JS interop

3. **Blazor Component** (`HonuaDeckLayer.razor`)
   - Drop-in component for adding Deck.gl to your maps
   - Event callbacks for clicks and hovers
   - ComponentBus integration

4. **Service Layer** (`DeckLayerManager.cs`)
   - High-level C# API for layer management
   - ComponentBus message handling
   - Helper methods for creating layers

## Installation

The required npm packages are already included in `package.json`:

```json
{
  "dependencies": {
    "@deck.gl/core": "^8.9.33",
    "@deck.gl/layers": "^8.9.33",
    "@deck.gl/geo-layers": "^8.9.33",
    "@deck.gl/mesh-layers": "^8.9.33",
    "@deck.gl/aggregation-layers": "^8.9.33",
    "@deck.gl/mapbox": "^8.9.33"
  }
}
```

Run `npm install` to install dependencies.

## Supported Layer Types

### 1. ScatterplotLayer

Renders points with variable size and color.

**Use Cases:**
- Visualizing individual locations (stores, sensors, etc.)
- Creating bubble maps with size representing values
- Plotting GPS coordinates

**Example:**

```csharp
var layer = new ScatterplotLayerDefinition
{
    Name = "Store Locations",
    Data = storeData,
    RadiusScale = 1.0,
    RadiusMinPixels = 5,
    RadiusMaxPixels = 50,
    GetPosition = "coordinates",  // or "location" or nested "geometry.coordinates"
    GetRadius = "revenue",         // size based on revenue
    GetFillColor = "category"      // color based on category
};

await deckLayerManager.AddLayerAsync(layer);
```

### 2. HexagonLayer

3D hexagonal binning aggregation for spatial clustering.

**Use Cases:**
- Population density visualization
- Crime hotspot analysis
- Sales territory analysis
- WiFi signal strength mapping

**Example:**

```csharp
var layer = new HexagonLayerDefinition
{
    Name = "Population Density",
    Data = populationData,
    Radius = 1000,              // 1km hexagons
    Extruded = true,            // 3D extrusion
    ElevationScale = 50,        // Height multiplier
    ElevationRange = new[] { 0, 3000 },
    GetPosition = "coordinates"
};

await deckLayerManager.AddLayerAsync(layer);
```

### 3. ArcLayer

Renders arcs between two points (origin-destination flows).

**Use Cases:**
- Flight routes
- Migration patterns
- Supply chain flows
- Network connectivity

**Example:**

```csharp
var layer = new ArcLayerDefinition
{
    Name = "Flight Routes",
    Data = flightData,
    GetWidth = 3,
    GetSourcePosition = "origin",
    GetTargetPosition = "destination",
    GetSourceColor = "originColor",
    GetTargetColor = "destinationColor"
};

await deckLayerManager.AddLayerAsync(layer);
```

### 4. GridLayer

Rectangular grid aggregation.

**Use Cases:**
- Rectangular heatmaps
- Raster-style aggregations
- Urban planning grids

**Example:**

```csharp
var layer = new GridLayerDefinition
{
    Name = "Traffic Grid",
    Data = trafficData,
    CellSize = 500,             // 500m cells
    Extruded = true,
    ElevationScale = 40,
    GetPosition = "coordinates"
};

await deckLayerManager.AddLayerAsync(layer);
```

### 5. ScreenGridLayer

Screen-space grid aggregation (heatmap).

**Use Cases:**
- Fast heatmaps for very large datasets
- Real-time density visualization
- Click heatmaps

**Example:**

```csharp
var layer = new ScreenGridLayerDefinition
{
    Name = "Click Heatmap",
    Data = clickData,
    CellSizePixels = 40,
    GetPosition = "coordinates"
};

await deckLayerManager.AddLayerAsync(layer);
```

## Usage

### Basic Setup

1. **Add DeckLayerManager to Dependency Injection**

```csharp
// In Program.cs or Startup.cs
builder.Services.AddSingleton<DeckLayerManager>();
```

2. **Add HonuaDeckLayer to Your Map Component**

```razor
@page "/map"
@using Honua.MapSDK.Components
@using Honua.MapSDK.Components.Map
@inject DeckLayerManager DeckLayerManager

<HonuaMapLibre @ref="_mapComponent"
               MapId="my-map"
               Height="600px"
               Configuration="@_mapConfig"
               OnMapLoad="OnMapLoaded">

    @if (_mapLoaded)
    {
        <HonuaDeckLayer @ref="_deckLayer"
                       MapInstance="@_mapInstance"
                       MapElement="@_mapElement.Value"
                       MapId="my-map"
                       OnLayerClick="OnLayerClick"
                       OnLayerHover="OnLayerHover" />
    }
</HonuaMapLibre>

@code {
    private HonuaMapLibre? _mapComponent;
    private HonuaDeckLayer? _deckLayer;
    private bool _mapLoaded = false;
    private object? _mapInstance;
    private ElementReference? _mapElement;

    private async Task OnMapLoaded(MapLibreViewport viewport)
    {
        _mapLoaded = true;
        // Get map instance from your map component
        StateHasChanged();
    }
}
```

### Creating and Adding Layers

#### Method 1: Using DeckLayerManager Service

```csharp
@inject DeckLayerManager DeckLayerManager

private async Task AddScatterplotLayer()
{
    // Prepare your data
    var data = GetStoreLocations(); // List<object>

    // Create layer
    var layer = DeckLayerManager.CreateScatterplotLayer(
        name: "Stores",
        data: data,
        getPosition: "coordinates",
        getRadius: "size",
        getFillColor: "color"
    );

    // Add to map
    await DeckLayerManager.AddLayerAsync(layer, mapId: "my-map");
}
```

#### Method 2: Using Layer Definition Classes

```csharp
private async Task AddHexagonLayer()
{
    var layer = new HexagonLayerDefinition
    {
        Name = "Population",
        Data = populationData,
        Radius = 1000,
        Extruded = true,
        ElevationScale = 50,
        GetPosition = "coordinates"
    };

    await DeckLayerManager.AddLayerAsync(layer);
}
```

#### Method 3: Direct Component API

```csharp
private async Task AddLayerDirectly()
{
    var layer = new ScatterplotLayerDefinition
    {
        Name = "Direct Layer",
        Data = myData
    };

    await _deckLayer.AddLayerAsync(layer);
}
```

### Data Format

Deck.gl layers expect data in specific formats. Here are examples:

#### Scatterplot Data

```csharp
var data = new List<object>
{
    new {
        coordinates = new[] { -122.4, 37.8 },  // [lng, lat]
        radius = 50,
        color = new[] { 255, 0, 0 }            // [R, G, B]
    },
    new {
        coordinates = new[] { -122.5, 37.9 },
        radius = 30,
        color = new[] { 0, 255, 0 }
    }
};
```

#### Arc Data

```csharp
var data = new List<object>
{
    new {
        sourcePosition = new[] { -122.4, 37.8 },
        targetPosition = new[] { -74.0, 40.7 },
        sourceColor = new[] { 255, 0, 0 },
        targetColor = new[] { 0, 0, 255 }
    }
};
```

### Loading Data from API

```csharp
private async Task LoadDataFromApi()
{
    // Option 1: Load data in C# and pass to layer
    var data = await httpClient.GetFromJsonAsync<List<object>>("/api/data");
    var layer = new ScatterplotLayerDefinition
    {
        Name = "API Data",
        Data = data
    };
    await DeckLayerManager.AddLayerAsync(layer);

    // Option 2: Load data directly in JavaScript (faster for large datasets)
    var data = await _deckLayer.LoadDataFromUrlAsync("/api/data");
    if (data != null)
    {
        await DeckLayerManager.UpdateLayerDataAsync("layer-id", data);
    }
}
```

### Real-time Data Updates

```csharp
private async Task UpdateLayerData()
{
    // Get fresh data
    var newData = await GetLatestData();

    // Update existing layer
    await DeckLayerManager.UpdateLayerDataAsync(
        layerId: "my-layer",
        data: newData
    );
}

// Or using timer for periodic updates
private Timer? _updateTimer;

protected override void OnInitialized()
{
    _updateTimer = new Timer(async _ => await UpdateLayerData(), null, 0, 5000);
}
```

### Layer Control

```csharp
// Toggle visibility
await DeckLayerManager.SetLayerVisibilityAsync("layer-id", visible: false);

// Change opacity
await DeckLayerManager.SetLayerOpacityAsync("layer-id", opacity: 0.5);

// Remove layer
await DeckLayerManager.RemoveLayerAsync("layer-id");

// Clear all layers
await DeckLayerManager.ClearAllLayersAsync();
```

### Event Handling

```csharp
private Task OnLayerClick(DeckLayerClickEventArgs args)
{
    Console.WriteLine($"Clicked layer: {args.LayerId}");
    Console.WriteLine($"Object: {JsonSerializer.Serialize(args.ClickedObject)}");
    Console.WriteLine($"Coordinate: [{args.Coordinate[0]}, {args.Coordinate[1]}]");

    // Show popup, open details, etc.
    return Task.CompletedTask;
}

private Task OnLayerHover(DeckLayerHoverEventArgs args)
{
    if (args.HoveredObject != null)
    {
        // Show tooltip
        _tooltip = JsonSerializer.Serialize(args.HoveredObject);
    }
    else
    {
        _tooltip = null;
    }
    StateHasChanged();
    return Task.CompletedTask;
}
```

## ComponentBus Integration

All layer operations can be triggered via ComponentBus messages:

```csharp
@inject ComponentBus ComponentBus

// Add layer
await ComponentBus.PublishAsync(new AddDeckLayerMessage
{
    Layer = myLayer,
    MapId = "my-map"
});

// Update data
await ComponentBus.PublishAsync(new UpdateDeckLayerDataMessage
{
    LayerId = "layer-id",
    Data = newData
});

// Subscribe to events
ComponentBus.Subscribe<DeckLayerClickedMessage>(args =>
{
    Console.WriteLine($"Layer {args.Message.LayerId} clicked");
});
```

## Performance Optimization

### 1. Use Appropriate Layer Types

- **ScatterplotLayer**: < 100K points
- **HexagonLayer/GridLayer**: < 1M points
- **ScreenGridLayer**: < 10M points

### 2. Data Loading Strategies

```csharp
// For large datasets, load directly in JavaScript
var data = await _deckLayer.LoadDataFromUrlAsync("/api/large-dataset");

// For small datasets, pass from C#
var data = await httpClient.GetFromJsonAsync<List<object>>("/api/small-dataset");
layer.Data = data;
```

### 3. Update Frequency

```csharp
// Debounce rapid updates
private Timer? _debounceTimer;

private void OnDataChanged(List<object> newData)
{
    _debounceTimer?.Dispose();
    _debounceTimer = new Timer(async _ =>
    {
        await DeckLayerManager.UpdateLayerDataAsync("layer-id", newData);
    }, null, 500, Timeout.Infinite); // 500ms debounce
}
```

### 4. Memory Management

```csharp
// Dispose components when done
public async ValueTask DisposeAsync()
{
    await _deckLayer.DisposeAsync();
    _updateTimer?.Dispose();
}
```

## Troubleshooting

### Layer Not Appearing

1. Ensure map is fully loaded before adding Deck.gl component
2. Check browser console for JavaScript errors
3. Verify data format matches expected structure
4. Check layer visibility and opacity settings

### Performance Issues

1. Reduce number of data points
2. Use aggregation layers (Hexagon/Grid) instead of Scatterplot
3. Increase cell size for aggregation layers
4. Disable extrusion for 2D visualization

### TypeScript/JavaScript Errors

1. Ensure all required Deck.gl packages are installed
2. Check browser compatibility (WebGL 2.0 required)
3. Clear browser cache and reload

## Advanced Usage

### Custom Accessors

```csharp
// Use nested property paths
var layer = new ScatterplotLayerDefinition
{
    GetPosition = "geometry.coordinates",
    GetRadius = "properties.population",
    GetFillColor = "properties.color"
};
```

### Custom Color Scales

```csharp
var layer = new HexagonLayerDefinition
{
    ColorRange = new[]
    {
        new[] { 255, 255, 204 },  // Light yellow
        new[] { 255, 237, 160 },
        new[] { 254, 217, 118 },
        new[] { 254, 178, 76 },
        new[] { 253, 141, 60 },
        new[] { 240, 59, 32 }     // Dark red
    }
};
```

### Multiple Maps

```csharp
// Map 1
<HonuaDeckLayer MapId="map-1" ... />

// Map 2
<HonuaDeckLayer MapId="map-2" ... />

// Target specific map
await DeckLayerManager.AddLayerAsync(layer, mapId: "map-1");
```

## Examples

See `Components/DeckGLExample.razor` for complete working examples of all layer types.

## API Reference

### DeckLayerManager Methods

- `AddLayerAsync(layer, mapId?)`: Add or update a layer
- `RemoveLayerAsync(layerId, mapId?)`: Remove a layer
- `UpdateLayerDataAsync(layerId, data, mapId?)`: Update layer data
- `SetLayerVisibilityAsync(layerId, visible, mapId?)`: Toggle visibility
- `SetLayerOpacityAsync(layerId, opacity, mapId?)`: Set opacity
- `ClearAllLayersAsync(mapId?)`: Remove all layers
- `GetLayers()`: Get all layers
- `GetLayer(layerId)`: Get specific layer
- `CreateScatterplotLayer(...)`: Helper to create scatterplot layer
- `CreateHexagonLayer(...)`: Helper to create hexagon layer
- `CreateArcLayer(...)`: Helper to create arc layer
- `CreateGridLayer(...)`: Helper to create grid layer
- `CreateScreenGridLayer(...)`: Helper to create screen grid layer

### HonuaDeckLayer Component

**Parameters:**
- `MapInstance`: MapLibre map instance
- `MapElement`: Map element reference
- `MapId`: Map identifier
- `Layers`: Initial layers
- `OnLayerClick`: Click event callback
- `OnLayerHover`: Hover event callback
- `Debug`: Enable debug logging

**Methods:**
- `AddLayerAsync(layer)`
- `RemoveLayerAsync(layerId)`
- `UpdateLayerDataAsync(layerId, data)`
- `SetLayerVisibilityAsync(layerId, visible)`
- `SetLayerOpacityAsync(layerId, opacity)`
- `ClearAllLayersAsync()`
- `LoadDataFromUrlAsync(url)`

## Resources

- [Deck.gl Documentation](https://deck.gl/)
- [Deck.gl Layer Catalog](https://deck.gl/docs/api-reference/layers)
- [MapLibre GL JS](https://maplibre.org/)
- [Honua.MapSDK Documentation](./README.md)

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
