# Your First Map

This guide provides a comprehensive walkthrough of the `HonuaMap` component, teaching you everything you need to know to create and customize interactive maps.

---

## Table of Contents

1. [Basic Map Setup](#basic-map-setup)
2. [Map Configuration](#map-configuration)
3. [Adding Layers](#adding-layers)
4. [Handling Events](#handling-events)
5. [Map Controls](#map-controls)
6. [Styling](#styling)
7. [Advanced Features](#advanced-features)

---

## Basic Map Setup

### Minimal Map

The simplest map requires just an ID and will use defaults for everything else:

```razor
<div style="height: 500px;">
    <HonuaMap Id="simple-map" />
</div>
```

**Default Values:**
- Center: `[0, 0]` (Prime Meridian/Equator)
- Zoom: `2` (World view)
- Style: Demo tiles from MapLibre

### Recommended Starter Map

A better starting point with common configurations:

```razor
<div style="height: 600px;">
    <HonuaMap Id="my-map"
              Center="@(new[] { -122.4194, 37.7749 })"  @* San Francisco *@
              Zoom="12"
              MapStyle="https://demotiles.maplibre.org/style.json" />
</div>
```

---

## Map Configuration

### Center and Zoom

The map center is specified as `[longitude, latitude]`:

```razor
<HonuaMap Id="map1"
          Center="@(new[] { -73.9857, 40.7484 })"  @* Times Square, NYC *@
          Zoom="14" />
```

**Zoom Levels:**
- `0` - World view
- `5` - Continent view
- `10` - City view
- `15` - Neighborhood view
- `20` - Building view

### Bounds

Constrain the map to a specific area:

```razor
<HonuaMap Id="map1"
          Center="@(new[] { -122.4, 37.7 })"
          Zoom="10"
          MaxBounds="@(new[] { -123.0, 37.0, -121.5, 38.5 })"  @* [west, south, east, north] *@
          MinZoom="8"
          MaxZoom="16" />
```

### Projection

Choose from different map projections:

```razor
@* Mercator (default) *@
<HonuaMap Projection="mercator" />

@* Globe *@
<HonuaMap Projection="globe" />

@* Albers *@
<HonuaMap Projection="albers" />

@* Equal Earth *@
<HonuaMap Projection="equalEarth" />
```

### 3D View

Enable pitch and bearing for 3D perspectives:

```razor
<HonuaMap Id="map-3d"
          Center="@(new[] { -73.9857, 40.7484 })"
          Zoom="15"
          Pitch="60"        @* Tilt angle (0-85 degrees) *@
          Bearing="45"      @* Rotation (0-360 degrees) *@
          EnableGPU="true" />
```

---

## Map Styles

### Using Built-in Styles

```razor
@* Demo tiles *@
<HonuaMap MapStyle="https://demotiles.maplibre.org/style.json" />

@* OpenStreetMap *@
<HonuaMap MapStyle="https://tiles.openfreemap.org/styles/liberty.json" />
```

### Custom Tile Server

```razor
<HonuaMap MapStyle="https://your-tile-server.com/style.json" />
```

### Switching Styles Dynamically

```razor
<MudSelect @bind-Value="@_selectedStyle" Label="Map Style">
    <MudSelectItem Value="@_osmStyle">OpenStreetMap</MudSelectItem>
    <MudSelectItem Value="@_satelliteStyle">Satellite</MudSelectItem>
    <MudSelectItem Value="@_darkStyle">Dark Mode</MudSelectItem>
</MudSelect>

<HonuaMap Id="map1" MapStyle="@_selectedStyle" />

@code {
    private string _selectedStyle = "https://demotiles.maplibre.org/style.json";
    private string _osmStyle = "https://tiles.openfreemap.org/styles/liberty.json";
    private string _satelliteStyle = "https://your-server.com/satellite.json";
    private string _darkStyle = "https://your-server.com/dark.json";
}
```

---

## Adding Layers

### GeoJSON Source

```razor
<HonuaMap Id="map1"
          Source="api/features.geojson"
          Center="@_mapCenter"
          Zoom="10" />
```

### Multiple Layers

```razor
<HonuaMap Id="map1">
    <MapLayer Id="parcels"
              Source="api/parcels.geojson"
              Type="fill"
              FillColor="#4A90E2"
              FillOpacity="0.6" />

    <MapLayer Id="roads"
              Source="api/roads.geojson"
              Type="line"
              LineColor="#2E5C8A"
              LineWidth="2" />

    <MapLayer Id="poi"
              Source="api/points-of-interest.geojson"
              Type="circle"
              CircleColor="#E74C3C"
              CircleRadius="8" />
</HonuaMap>
```

### Vector Tiles

```razor
<MapLayer Id="buildings"
          Source="https://tiles.example.com/buildings/{z}/{x}/{y}.pbf"
          SourceLayer="building"
          Type="fill-extrusion"
          FillExtrusionColor="#aaa"
          FillExtrusionHeight="@(new[] { "get", "height" })"
          FillExtrusionBase="@(new[] { "get", "min_height" })" />
```

---

## Handling Events

### Map Ready

Fired when the map finishes initializing:

```razor
<HonuaMap OnMapReady="@HandleMapReady" />

@code {
    private async Task HandleMapReady(MapReadyMessage message)
    {
        Console.WriteLine($"Map {message.MapId} is ready!");
        Console.WriteLine($"Center: {message.Center[0]}, {message.Center[1]}");
        Console.WriteLine($"Zoom: {message.Zoom}");

        // Map is now ready for operations
        await LoadInitialData();
    }

    private async Task LoadInitialData()
    {
        // Load your data here
    }
}
```

### Extent Changed

Fired when the user pans or zooms:

```razor
<HonuaMap OnExtentChanged="@HandleExtentChanged" />

@code {
    private void HandleExtentChanged(MapExtentChangedMessage message)
    {
        Console.WriteLine($"New zoom: {message.Zoom}");
        Console.WriteLine($"New center: {message.Center[0]}, {message.Center[1]}");
        Console.WriteLine($"Bounds: {string.Join(", ", message.Bounds)}");
        Console.WriteLine($"Bearing: {message.Bearing}");
        Console.WriteLine($"Pitch: {message.Pitch}");

        // Update other components based on new extent
        UpdateDataForExtent(message.Bounds);
    }
}
```

### Feature Clicked

Fired when a user clicks on a map feature:

```razor
<HonuaMap OnFeatureClicked="@HandleFeatureClicked" />

@code {
    private void HandleFeatureClicked(FeatureClickedMessage message)
    {
        Console.WriteLine($"Feature clicked: {message.FeatureId}");
        Console.WriteLine($"Layer: {message.LayerId}");
        Console.WriteLine($"Properties: {string.Join(", ", message.Properties)}");

        // Show feature details
        ShowFeatureDetails(message.FeatureId, message.Properties);
    }

    private void ShowFeatureDetails(string featureId, Dictionary<string, object> props)
    {
        // Display popup or side panel with feature information
    }
}
```

### Feature Hovered

Fired when a user hovers over a feature:

```razor
<HonuaMap @ref="_map" />

@code {
    private HonuaMap? _map;

    protected override void OnInitialized()
    {
        // Subscribe to hover events via ComponentBus
        Bus.Subscribe<FeatureHoveredMessage>(args =>
        {
            if (args.Message.FeatureId != null)
            {
                Console.WriteLine($"Hovering over: {args.Message.FeatureId}");
                ShowTooltip(args.Message.Properties);
            }
            else
            {
                HideTooltip();
            }
        });
    }
}
```

---

## Map Controls

### Programmatic Control

Access the map API to control it programmatically:

```razor
<MudButton OnClick="@FlyToNewYork">Fly to New York</MudButton>
<MudButton OnClick="@FlyToLondon">Fly to London</MudButton>
<MudButton OnClick="@ResetView">Reset View</MudButton>

<HonuaMap @ref="_map" Id="map1" />

@code {
    private HonuaMap? _map;

    private async Task FlyToNewYork()
    {
        await _map!.FlyToAsync(new[] { -73.9857, 40.7484 }, zoom: 13);
    }

    private async Task FlyToLondon()
    {
        await _map!.FlyToAsync(new[] { -0.1278, 51.5074 }, zoom: 13);
    }

    private async Task ResetView()
    {
        await _map!.FlyToAsync(new[] { 0.0, 0.0 }, zoom: 2);
    }
}
```

### Fit to Bounds

Zoom to show a specific area:

```razor
<MudButton OnClick="@FitToSanFrancisco">Show San Francisco</MudButton>

<HonuaMap @ref="_map" Id="map1" />

@code {
    private HonuaMap? _map;

    private async Task FitToSanFrancisco()
    {
        // Bounds: [west, south, east, north]
        var bounds = new[] { -122.5, 37.7, -122.35, 37.85 };
        await _map!.FitBoundsAsync(bounds, padding: 50);
    }
}
```

### Get Current State

Retrieve the current map state:

```razor
<MudButton OnClick="@GetMapState">Get Current State</MudButton>

<HonuaMap @ref="_map" Id="map1" />

@code {
    private HonuaMap? _map;

    private async Task GetMapState()
    {
        var center = await _map!.GetCenterAsync();
        var zoom = await _map!.GetZoomAsync();
        var bounds = await _map!.GetBoundsAsync();

        Console.WriteLine($"Center: {center?[0]}, {center?[1]}");
        Console.WriteLine($"Zoom: {zoom}");
        Console.WriteLine($"Bounds: {string.Join(", ", bounds ?? Array.Empty<double>())}");
    }
}
```

---

## Styling

### Container Styling

The map fills its container, so control size via the parent:

```razor
@* Full screen *@
<div style="width: 100vw; height: 100vh;">
    <HonuaMap Id="map1" />
</div>

@* Fixed size *@
<div style="width: 800px; height: 600px;">
    <HonuaMap Id="map1" />
</div>

@* Responsive with aspect ratio *@
<div style="width: 100%; aspect-ratio: 16/9;">
    <HonuaMap Id="map1" />
</div>
```

### Custom CSS Classes

```razor
<HonuaMap Id="map1" CssClass="my-custom-map" />

<style>
    .my-custom-map {
        border-radius: 8px;
        box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
    }
</style>
```

### Inline Styles

```razor
<HonuaMap Id="map1"
          Style="width: 100%; height: 600px; border: 2px solid #ccc; border-radius: 12px;" />
```

---

## Advanced Features

### Loading Indicators

Show a loading state while the map initializes:

```razor
<div style="height: 600px; position: relative;">
    @if (!_mapReady)
    {
        <div class="map-loading">
            <MudProgressCircular Indeterminate="true" Size="Size.Large" />
            <MudText Typo="Typo.h6" Class="mt-3">Loading map...</MudText>
        </div>
    }

    <HonuaMap Id="map1"
              OnMapReady="@(() => _mapReady = true)"
              Style="@(_mapReady ? "opacity: 1" : "opacity: 0")" />
</div>

@code {
    private bool _mapReady = false;
}

<style>
    .map-loading {
        position: absolute;
        top: 50%;
        left: 50%;
        transform: translate(-50%, -50%);
        text-align: center;
        z-index: 1000;
    }
</style>
```

### Popup Integration

Create custom popups when features are clicked:

```razor
<div style="height: 600px; position: relative;">
    <HonuaMap Id="map1" OnFeatureClicked="@ShowPopup" />

    @if (_selectedFeature != null)
    {
        <div class="map-popup" style="top: @_popupTop; left: @_popupLeft;">
            <MudPaper Elevation="4" Class="pa-3">
                <div class="d-flex justify-space-between align-center mb-2">
                    <MudText Typo="Typo.h6">@_selectedFeature.Name</MudText>
                    <MudIconButton Icon="@Icons.Material.Filled.Close"
                                   Size="Size.Small"
                                   OnClick="@(() => _selectedFeature = null)" />
                </div>
                <MudText Typo="Typo.body2">Type: @_selectedFeature.Type</MudText>
                <MudText Typo="Typo.body2">Value: @_selectedFeature.Value</MudText>
            </MudPaper>
        </div>
    }
</div>

@code {
    private Feature? _selectedFeature;
    private string _popupTop = "0px";
    private string _popupLeft = "0px";

    private void ShowPopup(FeatureClickedMessage message)
    {
        // In a real app, you'd calculate popup position from mouse/click coordinates
        _popupTop = "100px";
        _popupLeft = "200px";

        _selectedFeature = new Feature
        {
            Name = message.Properties.GetValueOrDefault("name")?.ToString() ?? "Unknown",
            Type = message.Properties.GetValueOrDefault("type")?.ToString() ?? "Unknown",
            Value = message.Properties.GetValueOrDefault("value")?.ToString() ?? "0"
        };
    }

    private class Feature
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}

<style>
    .map-popup {
        position: absolute;
        z-index: 1000;
        min-width: 200px;
    }
</style>
```

### Map with Legend

Combine the map with a legend component:

```razor
<div style="height: 600px; position: relative;">
    <HonuaMap Id="map1" />

    <div style="position: absolute; top: 10px; right: 10px; z-index: 1000;">
        <HonuaLegend SyncWith="map1"
                     Collapsible="true"
                     ShowOpacity="true" />
    </div>
</div>
```

---

## Complete Example

Here's a fully-featured map with all common configurations:

```razor
@page "/advanced-map"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Legend
@inject ComponentBus Bus

<PageTitle>Advanced Map Example</PageTitle>

<MudContainer MaxWidth="MaxWidth.False" Class="pa-4">
    <MudText Typo="Typo.h3" Class="mb-4">Advanced Map Features</MudText>

    <MudPaper Elevation="3" Class="pa-3 mb-3">
        <MudGrid>
            <MudItem xs="12" sm="6" md="3">
                <MudButton Variant="Variant.Filled"
                           Color="Color.Primary"
                           OnClick="@FlyToNewYork"
                           FullWidth="true">
                    New York
                </MudButton>
            </MudItem>
            <MudItem xs="12" sm="6" md="3">
                <MudButton Variant="Variant.Filled"
                           Color="Color.Primary"
                           OnClick="@FlyToLondon"
                           FullWidth="true">
                    London
                </MudButton>
            </MudItem>
            <MudItem xs="12" sm="6" md="3">
                <MudButton Variant="Variant.Filled"
                           Color="Color.Primary"
                           OnClick="@FlyToTokyo"
                           FullWidth="true">
                    Tokyo
                </MudButton>
            </MudItem>
            <MudItem xs="12" sm="6" md="3">
                <MudButton Variant="Variant.Outlined"
                           Color="Color.Default"
                           OnClick="@ResetView"
                           FullWidth="true">
                    Reset
                </MudButton>
            </MudItem>
        </MudGrid>
    </MudPaper>

    <div style="height: 700px; position: relative;">
        @if (!_mapReady)
        {
            <div class="map-loading-overlay">
                <MudProgressCircular Indeterminate="true" Size="Size.Large" Color="Color.Primary" />
                <MudText Typo="Typo.h6" Class="mt-3">Loading map...</MudText>
            </div>
        }

        <HonuaMap @ref="_map"
                  Id="advanced-map"
                  Center="@_mapCenter"
                  Zoom="@_mapZoom"
                  Pitch="@_mapPitch"
                  Bearing="@_mapBearing"
                  MapStyle="@_mapStyle"
                  EnableGPU="true"
                  MinZoom="2"
                  MaxZoom="18"
                  OnMapReady="@HandleMapReady"
                  OnExtentChanged="@HandleExtentChanged"
                  OnFeatureClicked="@HandleFeatureClicked"
                  Style="@(_mapReady ? "opacity: 1; transition: opacity 0.3s" : "opacity: 0")" />

        <div class="map-overlay top-right">
            <HonuaLegend SyncWith="advanced-map"
                         Collapsible="true"
                         ShowOpacity="true"
                         ShowGroups="true" />
        </div>

        <div class="map-overlay bottom-left">
            <MudPaper Elevation="3" Class="pa-2">
                <MudText Typo="Typo.caption">
                    Zoom: @_currentZoom:F2 | Center: [@_currentCenter[0]:F4, @_currentCenter[1]:F4]
                </MudText>
            </MudPaper>
        </div>
    </div>
</MudContainer>

@code {
    private HonuaMap? _map;
    private bool _mapReady = false;

    private double[] _mapCenter = new[] { 0.0, 20.0 };
    private double _mapZoom = 2.5;
    private double _mapPitch = 0;
    private double _mapBearing = 0;
    private string _mapStyle = "https://demotiles.maplibre.org/style.json";

    private double _currentZoom = 2.5;
    private double[] _currentCenter = new[] { 0.0, 20.0 };

    private async Task HandleMapReady(MapReadyMessage message)
    {
        _mapReady = true;
        Console.WriteLine($"Map {message.MapId} initialized successfully");
        StateHasChanged();
    }

    private void HandleExtentChanged(MapExtentChangedMessage message)
    {
        _currentZoom = message.Zoom;
        _currentCenter = message.Center;
        StateHasChanged();
    }

    private void HandleFeatureClicked(FeatureClickedMessage message)
    {
        Console.WriteLine($"Clicked feature {message.FeatureId} in layer {message.LayerId}");
    }

    private async Task FlyToNewYork()
    {
        await _map!.FlyToAsync(new[] { -74.0060, 40.7128 }, zoom: 12);
    }

    private async Task FlyToLondon()
    {
        await _map!.FlyToAsync(new[] { -0.1276, 51.5074 }, zoom: 12);
    }

    private async Task FlyToTokyo()
    {
        await _map!.FlyToAsync(new[] { 139.6917, 35.6895 }, zoom: 12);
    }

    private async Task ResetView()
    {
        await _map!.FlyToAsync(new[] { 0.0, 20.0 }, zoom: 2.5);
    }
}

<style>
    .map-loading-overlay {
        position: absolute;
        top: 0;
        left: 0;
        right: 0;
        bottom: 0;
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        background: rgba(255, 255, 255, 0.9);
        z-index: 1000;
    }

    .map-overlay {
        position: absolute;
        z-index: 1000;
    }

    .map-overlay.top-right {
        top: 10px;
        right: 10px;
    }

    .map-overlay.bottom-left {
        bottom: 10px;
        left: 10px;
    }
</style>
```

---

## Next Steps

- [Build a Dashboard](your-first-dashboard.md) - Combine map with other components
- [Component Documentation](../components/honua-map.md) - Full API reference
- [Working with Data](../guides/working-with-data.md) - Load and display data
- [Performance Tips](../recipes/performance-tips.md) - Optimize large datasets

---

## Common Questions

**Q: Why isn't my map showing?**

A: Ensure the map container has an explicit height. Maps cannot auto-size.

**Q: Can I use multiple map styles?**

A: Yes! You can switch styles dynamically by changing the `MapStyle` parameter.

**Q: How do I add custom markers?**

A: Use a GeoJSON source with point features and style them with the `symbol` layer type.

**Q: Can I use offline tiles?**

A: Yes, configure your map style to point to locally hosted tiles.

**Q: How do I handle touch gestures on mobile?**

A: MapLibre GL JS handles this automatically. The map is fully touch-enabled.
