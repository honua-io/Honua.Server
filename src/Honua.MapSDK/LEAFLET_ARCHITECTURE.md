# Honua Leaflet Component Architecture

## Component Structure

```
src/Honua.MapSDK/
├── Components/Map/
│   ├── HonuaLeaflet.razor           # Main Leaflet component (457 lines)
│   ├── HonuaLeaflet.razor.css       # Component-scoped styles
│   └── LeafletExample.razor         # Usage examples
├── Models/
│   └── LeafletOptions.cs            # Configuration classes (510 lines)
├── wwwroot/js/
│   └── leaflet-interop.js           # JavaScript interop (529 lines)
└── LeafletExtensions.cs             # DI registration (289 lines)
```

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        Blazor Application                        │
└───────────────────────────┬─────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                    HonuaLeaflet.razor                            │
│  - Component parameters (Center, Zoom, TileUrl, etc.)           │
│  - Event callbacks (OnMapReady, OnExtentChanged, etc.)          │
│  - Public API methods (FlyToAsync, AddLayerAsync, etc.)         │
└───────────┬────────────────────────────┬────────────────────────┘
            │                            │
            │                            │
            ▼                            ▼
┌─────────────────────┐      ┌──────────────────────────┐
│   ComponentBus      │      │ DotNetObjectReference    │
│  (Message Broker)   │      │  (JS Interop Bridge)     │
└──────────┬──────────┘      └────────────┬─────────────┘
           │                              │
           │                              │
           ▼                              ▼
┌──────────────────────┐      ┌─────────────────────────┐
│  Map Messages        │      │ leaflet-interop.js      │
│  - FlyToRequest      │      │  - createLeafletMap()   │
│  - BasemapChanged    │      │  - setupEventHandlers() │
│  - LayerVisibility   │      │  - createLeafletAPI()   │
│  - FeatureClicked    │      └────────────┬────────────┘
└──────────────────────┘                   │
                                           │
                                           ▼
                            ┌──────────────────────────┐
                            │   Leaflet Library        │
                            │  - L.Map                 │
                            │  - L.TileLayer           │
                            │  - L.GeoJSON             │
                            │  - L.Marker              │
                            └────────────┬─────────────┘
                                         │
                                         ▼
                            ┌──────────────────────────┐
                            │  Leaflet Plugins         │
                            │  - MarkerCluster         │
                            │  - Draw                  │
                            │  - Measure               │
                            │  - Fullscreen            │
                            └──────────────────────────┘
```

## Data Flow

### 1. Map Initialization

```
User -> HonuaLeaflet.razor
  ↓
OnAfterRenderAsync()
  ↓
InitializeMap()
  ↓
JS.InvokeAsync("import", "leaflet-interop.js")
  ↓
createLeafletMap(container, options, dotNetRef)
  ↓
L.map(container, {...})
  ↓
setupEventHandlers(map, dotNetRef)
  ↓
createLeafletAPI(map)
  ↓
MapReadyMessage published via ComponentBus
```

### 2. User Interaction (e.g., Feature Click)

```
User clicks on map feature
  ↓
Leaflet fires 'click' event
  ↓
leaflet-interop.js event handler
  ↓
dotNetRef.invokeMethodAsync('OnFeatureClickedInternal', ...)
  ↓
HonuaLeaflet.OnFeatureClickedInternal()
  ↓
Publishes FeatureClickedMessage via ComponentBus
  ↓
Invokes OnFeatureClicked EventCallback
  ↓
User's event handler receives FeatureClickedMessage
```

### 3. Programmatic Navigation (e.g., FlyTo)

```
User calls leafletMap.FlyToAsync(center, zoom)
  ↓
Publishes FlyToRequestMessage via ComponentBus
  ↓
ComponentBus subscription receives message
  ↓
_mapInstance.InvokeVoidAsync("flyTo", options)
  ↓
leaflet-interop.js flyTo() method
  ↓
map.flyTo(center, zoom, {...})
  ↓
Leaflet animates to new location
  ↓
'moveend' event fires
  ↓
OnExtentChangedInternal() called
  ↓
MapExtentChangedMessage published
```

### 4. Layer Management

```
User calls AddGeoJsonLayerAsync(layerId, geoJson, style)
  ↓
_mapInstance.InvokeVoidAsync("addGeoJsonLayer", ...)
  ↓
leaflet-interop.js addGeoJsonLayer()
  ↓
L.geoJSON(geoJson, {style, onEachFeature, ...})
  ↓
layer.addTo(map)
  ↓
map._layers.set(layerId, layer)
```

## Message Bus Integration

The component integrates with the Honua ComponentBus for cross-component communication:

### Published Messages
- `MapReadyMessage` - When map initialization completes
- `MapExtentChangedMessage` - When viewport changes
- `FeatureClickedMessage` - When feature is clicked
- `FeatureHoveredMessage` - When feature is hovered

### Subscribed Messages
- `FlyToRequestMessage` - Navigate to location
- `FitBoundsRequestMessage` - Fit to bounds
- `BasemapChangedMessage` - Change tile layer
- `LayerVisibilityChangedMessage` - Show/hide layer
- `LayerOpacityChangedMessage` - Change layer opacity
- `HighlightFeaturesRequestMessage` - Highlight features
- `ClearHighlightsRequestMessage` - Clear highlights
- `DataRowSelectedMessage` - Highlight from data grid

## JavaScript Interop

### .NET → JavaScript Calls

```csharp
// Component initialization
_mapInstance = await _leafletModule.InvokeAsync<IJSObjectReference>(
    "createLeafletMap", container, options, dotNetRef);

// Layer operations
await _mapInstance.InvokeVoidAsync("addGeoJsonLayer", layerId, geoJson, style);
await _mapInstance.InvokeVoidAsync("removeLayer", layerId);

// Navigation
await _mapInstance.InvokeVoidAsync("flyTo", options);
await _mapInstance.InvokeVoidAsync("fitBounds", bounds, padding);

// Queries
var center = await _mapInstance.InvokeAsync<double[]>("getCenter");
var zoom = await _mapInstance.InvokeAsync<double>("getZoom");
```

### JavaScript → .NET Calls

```javascript
// Event notifications
dotNetRef.invokeMethodAsync('OnExtentChangedInternal', bounds, zoom, center);
dotNetRef.invokeMethodAsync('OnFeatureClickedInternal', layerId, featureId, props, geom);
dotNetRef.invokeMethodAsync('OnFeatureHoveredInternal', featureId, layerId, props);
dotNetRef.invokeMethodAsync('OnDrawCreatedInternal', data);
dotNetRef.invokeMethodAsync('OnMeasureCompleteInternal', data);
```

## Configuration System

### Global Configuration (LeafletConfiguration)

```csharp
services.AddLeafletSupport(config =>
{
    // CDN URLs
    config.LeafletCdnUrl = "https://unpkg.com/leaflet@1.9.4/dist/leaflet.js";

    // Default settings
    config.DefaultZoom = 10;
    config.DefaultCenter = new[] { -157.8583, 21.3099 };

    // Plugin toggles
    config.EnableMarkerCluster = true;
    config.EnableDraw = true;
    config.EnableMeasure = true;
    config.EnableFullscreen = true;

    // Predefined basemaps
    config.AddCommonBasemaps();
});
```

### Component Configuration

```razor
<HonuaLeaflet TileUrl="@_tileUrl"
              Attribution="@_attribution"
              Center="@_center"
              Zoom="@_zoom"
              MaxBounds="@_maxBounds"
              MinZoom="@_minZoom"
              MaxZoom="@_maxZoom"
              EnableMarkerCluster="true"
              MaxClusterRadius="80" />
```

## Plugin Integration

### Marker Clustering

```javascript
if (options.enableMarkerCluster && typeof L.markerClusterGroup !== 'undefined') {
    map._markerClusterGroup = L.markerClusterGroup({
        maxClusterRadius: options.maxClusterRadius || 80
    });
    map.addLayer(map._markerClusterGroup);
}
```

### Drawing Tools

```javascript
if (options.enableDraw && typeof L.Control.Draw !== 'undefined') {
    map._drawLayer = new L.FeatureGroup();
    const drawControl = new L.Control.Draw({...});
    map.addControl(drawControl);

    map.on(L.Draw.Event.CREATED, (e) => {
        dotNetRef.invokeMethodAsync('OnDrawCreatedInternal', {...});
    });
}
```

### Measure Tools

```javascript
if (options.enableMeasure && typeof L.Control.Measure !== 'undefined') {
    map._measureControl = L.control.measure({...});
    map.addControl(map._measureControl);
}
```

## Coordinate System Handling

### Leaflet vs. Standard GeoJSON

Leaflet uses **[latitude, longitude]** while GeoJSON/MapLibre use **[longitude, latitude]**.

The component handles conversion automatically:

```csharp
// Input: GeoJSON format [lng, lat]
Center = new[] { -157.8583, 21.3099 }

// Conversion to Leaflet format [lat, lng]
center = new[] { Center[1], Center[0] }

// JavaScript receives: [21.3099, -157.8583]
```

Similar conversion happens for:
- Center coordinates
- Bounds (west, south, east, north → south, west, north, east)
- Marker positions
- Feature coordinates

## State Management

### Component State

```csharp
private ElementReference _mapContainer;        // DOM element
private IJSObjectReference? _leafletModule;    // JS module
private IJSObjectReference? _mapInstance;      // Map API wrapper
private DotNetObjectReference<HonuaLeaflet>? _dotNetRef;  // Callback bridge
private bool _isInitialized;                   // Init flag
```

### JavaScript State

```javascript
map._dotNetRef = dotNetRef;           // Callback reference
map._honuaId = options.id;            // Map identifier
map._layers = new Map();              // Layer registry
map._markers = new Map();             // Marker registry
map._markerClusterGroup = null;       // Cluster group
map._highlightLayer = null;           // Highlight layer
map._drawLayer = null;                // Drawing layer
map._measureControl = null;           // Measure control
```

## Lifecycle

### Initialization
1. Component renders → `OnAfterRenderAsync(firstRender: true)`
2. Load leaflet-interop.js module
3. Create map instance via JS interop
4. Setup event handlers
5. Publish MapReadyMessage
6. Setup ComponentBus subscriptions

### Updates
1. Parameter changes trigger re-render
2. Message bus messages trigger actions
3. User interactions trigger events
4. State changes propagate via ComponentBus

### Cleanup
1. `DisposeAsync()` called
2. `_mapInstance.InvokeVoidAsync("dispose")`
3. JavaScript: `map.remove()`
4. Dispose JS object references
5. Dispose .NET object reference

## Performance Optimizations

1. **Debounced Events**: Extent changes debounced by 100ms
2. **Layer Caching**: Layers stored in Map for quick access
3. **Marker Clustering**: Reduces DOM elements for many markers
4. **Lazy Plugin Loading**: Plugins only loaded if enabled
5. **Event Propagation**: `stopPropagation()` on layer clicks
6. **Style Caching**: Original styles cached for reset

## Error Handling

```csharp
try
{
    await InitializeMap();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error initializing Leaflet map: {ex.Message}");
}
```

```javascript
if (typeof L === 'undefined') {
    console.error('Leaflet library not loaded');
    return null;
}
```

## Compatibility with MapLibre

Both components share:
- Same message types (MapReadyMessage, etc.)
- Same ComponentBus integration
- Same public API methods
- Same event callback signatures

This allows switching between components with minimal code changes:

```razor
@if (useLeaflet)
{
    <HonuaLeaflet @ref="_map" ... />
}
else
{
    <HonuaMapLibre @ref="_map" ... />
}
```

## Extension Points

### Custom Tile Layers

```csharp
config.CustomTileLayers["my-tiles"] = new LeafletTileLayer
{
    Id = "my-tiles",
    Url = "https://example.com/{z}/{x}/{y}.png",
    Attribution = "Custom tiles"
};
```

### Custom Popups

The JavaScript automatically generates popups from feature properties, but can be customized in `createPopupContent()`.

### Custom Markers

Override `pointToLayer` in `addGeoJsonLayer` options.

### Custom Event Handlers

Subscribe to ComponentBus messages for cross-component integration.

## Testing Considerations

1. **Unit Tests**: Test component parameters and event callbacks
2. **Integration Tests**: Test ComponentBus message flow
3. **E2E Tests**: Test JavaScript interop and map interactions
4. **Manual Tests**: Use LeafletExample.razor for visual testing

## Future Enhancements

Potential additions:
- Geocoding integration
- Routing/directions
- Heat map support
- Custom controls
- Vector tile support
- Offline tile caching
- Print/export capabilities
- Animation/playback controls
