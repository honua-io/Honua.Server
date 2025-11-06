# Honua.MapSDK

Next-generation map SDK for Blazor with zero-config component synchronization.

## Features

- 🗺️ **MapLibre GL** integration with GPU acceleration
- 🔌 **Component Bus** - Automatic synchronization between map, grid, charts
- 💾 **Configuration Export** - Save maps as JSON, YAML, or embeddable HTML
- ⚡ **High Performance** - Built for desktop-speed rendering in browser
- 🎨 **Blazor Native** - Pure C# components with MudBlazor integration

## Quick Start

### 1. Installation

Add to your `Program.cs`:

```csharp
builder.Services.AddHonuaMapSDK();
```

### 2. Basic Map

```razor
<HonuaMap Id="myMap"
          MapStyle="https://demotiles.maplibre.org/style.json"
          Center="@(new[] { -122.4194, 37.7749 })"
          Zoom="12" />
```

### 3. Auto-Synced Components

```razor
<HonuaMap Id="map1" Center="@(new[] { -122.4, 37.7 })" Zoom="12" />

<!-- Grid automatically filters when map moves -->
<HonuaDataGrid Source="grpc://api.honua.io/parcels" SyncWith="map1" />

<!-- Chart automatically updates with filtered data -->
<HonuaChart Type="Histogram" Source="grpc://api.honua.io/parcels" SyncWith="map1" />
```

That's it! No manual wiring - the ComponentBus handles everything.

## Architecture

```
┌─────────┐     ┌──────────────┐     ┌────────┐
│   Map   │────▶│ Message Bus  │────▶│  Grid  │
└─────────┘     │  (pub/sub)   │     └────────┘
                │              │     ┌────────┐
                └──────────────┘────▶│ Charts │
                                     └────────┘
```

## Configuration Export

Save and share map configurations:

```csharp
@inject IMapConfigurationService ConfigService

var config = new MapConfiguration
{
    Name = "My Map",
    Settings = new MapSettings
    {
        Style = "maplibre://honua/dark",
        Center = new[] { -122.4, 37.7 },
        Zoom = 12
    },
    Layers = new List<LayerConfiguration>
    {
        new()
        {
            Name = "Parcels",
            Type = LayerType.Vector,
            Source = "grpc://api.honua.io/parcels"
        }
    }
};

// Export as JSON
string json = ConfigService.ExportAsJson(config);

// Export as YAML
string yaml = ConfigService.ExportAsYaml(config);

// Export as embeddable HTML
string html = ConfigService.ExportAsHtmlEmbed(config, "https://cdn.honua.io/sdk");

// Export as Blazor component code
string razor = ConfigService.ExportAsBlazorComponent(config);
```

## Message Types

Components communicate via strongly-typed messages:

- `MapExtentChangedMessage` - Map viewport changed
- `FeatureClickedMessage` - Feature selected on map
- `FilterAppliedMessage` - Filter applied
- `DataRowSelectedMessage` - Grid row selected
- `LayerVisibilityChangedMessage` - Layer toggled
- And more...

## Custom Handlers

Subscribe to messages in your components:

```csharp
@inject ComponentBus Bus

protected override void OnInitialized()
{
    Bus.Subscribe<MapExtentChangedMessage>(async args =>
    {
        Console.WriteLine($"Map moved to zoom {args.Message.Zoom}");
        // Your custom logic here
    });
}
```

## Roadmap

- ✅ Core MapSDK with ComponentBus
- ✅ Map configuration save/load/export
- 🚧 Additional components (Grid, Chart, Legend, Filter)
- 🚧 gRPC streaming data sources
- 🚧 WebGPU compute acceleration
- 🚧 Visual map designer UI
- 🚧 PMTiles + FlatGeobuf support

## License

Part of Honua Server - see main repository for license.
