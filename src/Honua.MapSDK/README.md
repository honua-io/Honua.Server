# Honua.MapSDK

**Next-generation Blazor map SDK with zero-config component synchronization.**

[![.NET 9](https://img.shields.io/badge/.NET-9-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-ELv2-blue.svg)](LICENSE)

Honua.MapSDK is a comprehensive mapping solution for Blazor applications, built on MapLibre GL JS with GPU-accelerated rendering. It provides 20+ production-ready components that communicate seamlessly through a message bus architecture, eliminating the need for manual component wiring.

## Key Features

### Core Capabilities

- **MapLibre GL Integration** - GPU-accelerated vector tile rendering with WebGL
- **ComponentBus Architecture** - Zero-config synchronization between map, grids, charts, and other components
- **20+ Production Components** - From basic maps to advanced editing, all pre-integrated
- **Configuration as Code** - Save and restore entire map configurations as JSON/YAML
- **High Performance** - Built-in caching, streaming, compression, and parallel loading
- **MudBlazor Integration** - Beautiful Material Design UI components
- **Full .NET 9 Support** - Latest C# features and Blazor capabilities

### Performance Features

- Intelligent caching with LRU eviction
- Streaming data loading for large datasets
- Parallel HTTP request handling
- Compression (gzip, brotli) support
- Request deduplication
- Performance monitoring and metrics

### Developer Experience

- Zero-config component synchronization
- Strongly-typed message system
- Comprehensive error handling
- Built-in testing utilities
- Extensive documentation
- IntelliSense everywhere

## Quick Start

### Installation

Add MapSDK to your Blazor project:

```bash
dotnet add package Honua.MapSDK
```

### Setup

Register MapSDK services in `Program.cs`:

```csharp
using Honua.MapSDK;

var builder = WebApplication.CreateBuilder(args);

// Add MapSDK with default configuration
builder.Services.AddHonuaMapSDK();

// Or customize configuration
builder.Services.AddHonuaMapSDK(options =>
{
    options.EnablePerformanceMonitoring = true;
    options.Cache.MaxSizeMB = 100;
    options.Rendering.VirtualScrollThreshold = 1000;
});

var app = builder.Build();
app.Run();
```

### Your First Map

```razor
@page "/map"
@using Honua.MapSDK.Components

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudPaper Elevation="2" Style="height: 600px;">
        <HonuaMap
            Id="myMap"
            MapStyle="https://demotiles.maplibre.org/style.json"
            Center="@(new[] { -122.4194, 37.7749 })"
            Zoom="12" />
    </MudPaper>
</MudContainer>
```

### Auto-Synchronized Dashboard

The magic of ComponentBus - no manual wiring required:

```razor
@page "/dashboard"
@using Honua.MapSDK.Components

<MudGrid>
    <!-- Map updates when you filter -->
    <MudItem xs="12" md="8">
        <HonuaMap Id="map1"
                  MapStyle="https://demotiles.maplibre.org/style.json"
                  Center="@(new[] { -122.4, 37.7 })"
                  Zoom="12" />
    </MudItem>

    <!-- Filter panel automatically filters map -->
    <MudItem xs="12" md="4">
        <HonuaFilterPanel SyncWith="map1" />
    </MudItem>

    <!-- Grid automatically shows features in map extent -->
    <MudItem xs="12">
        <HonuaDataGrid
            Source="https://api.example.com/data.geojson"
            SyncWith="map1"
            EnableSelection="true" />
    </MudItem>

    <!-- Chart automatically updates with filtered data -->
    <MudItem xs="12" md="6">
        <HonuaChart
            Type="ChartType.Histogram"
            Field="propertyValue"
            SyncWith="map1"
            Title="Property Values"
            Bins="20" />
    </MudItem>

    <!-- Legend automatically shows active layers -->
    <MudItem xs="12" md="6">
        <HonuaLegend SyncWith="map1" />
    </MudItem>
</MudGrid>
```

That's it! Components automatically communicate through ComponentBus. No event handlers, no props drilling, no manual state management.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                      Blazor Application                      │
└─────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
┌───────▼───────┐    ┌────────▼────────┐   ┌───────▼──────┐
│   HonuaMap    │    │ HonuaDataGrid   │   │ HonuaChart   │
│               │    │                 │   │              │
│ MapLibre GL   │    │  MudBlazor      │   │  Chart.js    │
└───────┬───────┘    └────────┬────────┘   └───────┬──────┘
        │                     │                     │
        └─────────────────────┼─────────────────────┘
                              │
                    ┌─────────▼──────────┐
                    │   ComponentBus     │
                    │   (Pub/Sub Hub)    │
                    └─────────┬──────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
┌───────▼───────┐    ┌────────▼────────┐   ┌───────▼──────┐
│ HonuaLegend   │    │ HonuaFilterPanel│   │ HonuaSearch  │
└───────────────┘    └─────────────────┘   └──────────────┘
```

### Message Flow Example

1. User clicks a feature on the map
2. Map publishes `FeatureClickedMessage` to ComponentBus
3. ComponentBus broadcasts to all subscribers:
   - DataGrid highlights the row
   - Chart updates selection
   - Popup displays feature details
4. All happens automatically - zero configuration required

## Component Catalog

### Core Components

| Component | Description | Use Cases |
|-----------|-------------|-----------|
| **HonuaMap** | MapLibre GL map with GPU rendering | Base map, data visualization, spatial analysis |
| **HonuaDataGrid** | MudBlazor data grid with map sync | Feature tables, attribute data, editing |
| **HonuaChart** | Interactive charts (histogram, bar, pie, line) | Data analysis, statistics, trends |
| **HonuaLegend** | Dynamic legend for map layers | Layer explanation, symbology |
| **HonuaFilterPanel** | Advanced filtering UI | Data exploration, queries |

### Search & Navigation

| Component | Description | Use Cases |
|-----------|-------------|-----------|
| **HonuaSearch** | Geocoding search with provider support | Address lookup, place search |
| **HonuaBookmarks** | Save and restore map views | Quick navigation, favorites |
| **HonuaCoordinateDisplay** | Real-time coordinate tracking | Location display, coordinate conversion |
| **HonuaTimeline** | Temporal data visualization | Time-series animation, playback |

### Drawing & Editing

| Component | Description | Use Cases |
|-----------|-------------|-----------|
| **HonuaDraw** | Sketch tools (point, line, polygon, etc.) | Markup, annotations, measurements |
| **HonuaEditor** | Feature editing with undo/redo | Data creation, geometry editing |

### Layer Management

| Component | Description | Use Cases |
|-----------|-------------|-----------|
| **HonuaBasemapGallery** | Basemap selector with thumbnails | Style switching, context |
| **HonuaLayerList** | Layer visibility and opacity control | TOC, layer management |
| **HonuaPopup** | Feature popup with templates | Feature details, info windows |

### Data Management

| Component | Description | Use Cases |
|-----------|-------------|-----------|
| **HonuaImportWizard** | Multi-format data import (GeoJSON, CSV, KML) | Data ingestion, file uploads |
| **HonuaAttributeTable** | Full-featured attribute table | Data viewing, bulk editing |

### Visualization

| Component | Description | Use Cases |
|-----------|-------------|-----------|
| **HonuaOverviewMap** | Minimap for context | Navigation, orientation |
| **HonuaHeatmap** | Density visualization | Clustering, hotspot analysis |
| **HonuaElevationProfile** | Terrain profile charts | Route planning, terrain analysis |
| **HonuaCompare** | Side-by-side map comparison | Before/after, style comparison |

### Export & Print

| Component | Description | Use Cases |
|-----------|-------------|-----------|
| **HonuaPrint** | MapFish Print integration | PDF export, reports |

## ComponentBus Deep Dive

ComponentBus is the heart of MapSDK's architecture. It's a publish-subscribe message bus that enables loose coupling between components.

### Core Concepts

**Publishers** - Components that emit events (e.g., map publishes `MapExtentChangedMessage`)
**Subscribers** - Components that listen to events (e.g., grid subscribes to map extent changes)
**Messages** - Strongly-typed classes carrying event data

### Message Types

```csharp
// Spatial Events
MapExtentChangedMessage      // Map viewport changed
FeatureClickedMessage        // Feature selected
FeatureHoveredMessage        // Feature hovered

// Data Events
DataLoadedMessage            // Data loaded into component
DataRowSelectedMessage       // Grid row selected
FilterAppliedMessage         // Filter applied

// Layer Events
LayerVisibilityChangedMessage // Layer toggled
LayerOpacityChangedMessage    // Opacity changed
BasemapChangedMessage         // Basemap switched

// Drawing Events
FeatureDrawnMessage          // Feature drawn
FeatureEditedMessage         // Feature edited
FeatureDeletedMessage        // Feature deleted

// Temporal Events
TimeChangedMessage           // Timeline position changed
TimelineStateChangedMessage  // Playback state changed

// Navigation Events
SearchResultSelectedMessage  // Search result selected
BookmarkSelectedMessage      // Bookmark activated

// Import Events
DataImportedMessage         // Import completed
ImportProgressMessage       // Import progress update

// And 30+ more message types...
```

### Usage Patterns

#### Publishing Messages

```csharp
@inject ComponentBus Bus

private async Task OnMapClick(FeatureClickedMessage message)
{
    await Bus.PublishAsync(message, source: "MyMap");
}
```

#### Subscribing to Messages

```csharp
@inject ComponentBus Bus

protected override void OnInitialized()
{
    // Synchronous handler
    Bus.Subscribe<MapExtentChangedMessage>(args =>
    {
        Console.WriteLine($"Map moved to zoom {args.Message.Zoom}");
    });

    // Asynchronous handler
    Bus.Subscribe<FeatureClickedMessage>(async args =>
    {
        await LoadFeatureDetailsAsync(args.Message.FeatureId);
        StateHasChanged();
    });
}
```

#### Creating Custom Message Types

```csharp
public class CustomAnalysisCompleteMessage
{
    public required string AnalysisId { get; init; }
    public required Dictionary<string, object> Results { get; init; }
    public DateTime CompletedAt { get; init; } = DateTime.UtcNow;
}

// Publish
await Bus.PublishAsync(new CustomAnalysisCompleteMessage
{
    AnalysisId = "analysis-1",
    Results = analysisResults
});

// Subscribe
Bus.Subscribe<CustomAnalysisCompleteMessage>(args =>
{
    DisplayResults(args.Message.Results);
});
```

## Configuration Export

Save and restore complete map configurations:

```csharp
@inject IMapConfigurationService ConfigService

// Create configuration
var config = new MapConfiguration
{
    Name = "San Francisco Map",
    Settings = new MapSettings
    {
        Style = "https://api.maptiler.com/maps/streets/style.json",
        Center = new[] { -122.4194, 37.7749 },
        Zoom = 12,
        Bearing = 0,
        Pitch = 0
    },
    Layers = new List<LayerConfiguration>
    {
        new()
        {
            Name = "Parcels",
            Type = LayerType.Vector,
            Source = "https://api.example.com/parcels.geojson",
            Visible = true,
            Opacity = 0.8
        }
    }
};

// Export as JSON
string json = ConfigService.ExportAsJson(config);

// Export as YAML
string yaml = ConfigService.ExportAsYaml(config);

// Export as embeddable HTML
string html = ConfigService.ExportAsHtmlEmbed(
    config,
    cdnUrl: "https://cdn.honua.io/sdk"
);

// Export as Blazor component code
string razorCode = ConfigService.ExportAsBlazorComponent(config);

// Import from JSON
var imported = ConfigService.ImportFromJson(json);
```

## Performance Optimization

### Caching

```csharp
@inject DataLoader Loader
@inject DataCache Cache

// Automatic caching with DataLoader
var data = await Loader.LoadJsonAsync<MyData>(
    "https://api.example.com/data.json"
);

// Manual cache control
await Cache.SetAsync("key", data, ttl: TimeSpan.FromMinutes(10));
var cached = await Cache.GetAsync<MyData>("key");

// Cache statistics
var stats = Cache.GetStatistics();
Console.WriteLine($"Hit rate: {stats.HitRate:P}");
```

### Streaming Large Datasets

```csharp
@inject StreamingLoader Loader

await Loader.StreamGeoJsonFeaturesAsync(
    url: "https://api.example.com/large-dataset.geojson",
    chunkSize: 100,
    onChunk: features =>
    {
        // Process 100 features at a time
        AddFeaturesToMap(features);
        StateHasChanged();
    }
);
```

### Parallel Loading

```csharp
@inject DataLoader Loader

var urls = new[]
{
    "https://api.example.com/dataset1.json",
    "https://api.example.com/dataset2.json",
    "https://api.example.com/dataset3.json"
};

var results = await Loader.LoadManyAsync(urls, maxParallel: 4);
```

### Performance Monitoring

```csharp
@inject PerformanceMonitor Monitor

protected override async Task OnInitializedAsync()
{
    using (Monitor.Measure("DataLoad"))
    {
        await LoadDataAsync();
    }

    // Get statistics
    var stats = Monitor.GetStatistics("DataLoad");
    Console.WriteLine($"Average: {stats.Average}ms");
    Console.WriteLine($"P95: {stats.P95}ms");
}
```

## Error Handling

```razor
<MapErrorBoundary
    ShowResetButton="true"
    ShowTechnicalDetails="@(Environment.IsDevelopment())"
    OnError="HandleError">

    <HonuaMap Configuration="@config" />

</MapErrorBoundary>

@code {
    private void HandleError(Exception ex)
    {
        Logger.LogError(ex, "Map error occurred");
        Snackbar.Add("Map failed to load", Severity.Error);
    }
}
```

## Testing

### Unit Testing with bUnit

```csharp
using Honua.MapSDK.Testing;

public class MapComponentTests
{
    [Fact]
    public void Map_Publishes_ReadyMessage()
    {
        using var ctx = new MapSdkTestContext();
        var mockBus = ctx.UseMockComponentBus();

        var cut = ctx.RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.Id, "test-map")
            .Add(p => p.Zoom, 10)
        );

        mockBus.AssertMessagePublished<MapReadyMessage>(msg =>
            msg.MapId == "test-map" && msg.Zoom == 10
        );
    }
}
```

### Mock Data Generation

```csharp
using Honua.MapSDK.Testing;

// Generate test GeoJSON
var geoJson = MockDataGenerator.GenerateGeoJsonPoints(
    count: 1000,
    bounds: (-122.5, 37.7, -122.3, 37.8),
    properties: new Dictionary<string, Func<int, object>>
    {
        ["name"] = i => $"Point {i}",
        ["temperature"] = _ => Random.Next(60, 90)
    }
);
```

## Accessibility

MapSDK is built with accessibility in mind:

- **Screen Readers** - Full ARIA support and announcements
- **Keyboard Navigation** - Complete keyboard control
- **High Contrast** - Automatic detection and adaptation
- **Reduced Motion** - Respects user preferences
- **Focus Management** - Clear focus indicators

Configure accessibility features:

```csharp
builder.Services.AddHonuaMapSDK(options =>
{
    options.Accessibility.EnableScreenReaderAnnouncements = true;
    options.Accessibility.EnableKeyboardShortcuts = true;
    options.Accessibility.RespectReducedMotion = true;
});
```

## Documentation

- **[Getting Started](../../docs/mapsdk/GettingStarted.md)** - Step-by-step tutorial
- **[Architecture](../../docs/mapsdk/Architecture.md)** - Deep dive into design
- **[Component Catalog](../../docs/mapsdk/ComponentCatalog.md)** - Complete component reference
- **[Best Practices](../../docs/mapsdk/BestPractices.md)** - Patterns and recommendations
- **[Troubleshooting](../../docs/mapsdk/Troubleshooting.md)** - Common issues and solutions
- **[Migration Guide](../../docs/mapsdk/Migration.md)** - Upgrading between versions
- **[Contributing](../../docs/mapsdk/Contributing.md)** - How to contribute

### Component Documentation

Each component has detailed documentation in its subdirectory:

- [Map](Components/Map/README.md)
- [DataGrid](Components/DataGrid/README.md)
- [Chart](Components/Chart/README.md)
- [Legend](Components/Legend/README.md)
- [FilterPanel](Components/FilterPanel/README.md)
- [Search](Components/Search/README.md)
- [Timeline](Components/Timeline/README.md)
- [Draw](Components/Draw/README.md)
- [Editor](Components/Editor/README.md)
- [BasemapGallery](Components/BasemapGallery/README.md)
- [Bookmarks](Components/Bookmarks/README.md)
- [LayerList](Components/LayerList/README.md)
- [Popup](Components/Popup/README.md)
- [ImportWizard](Components/ImportWizard/README.md)
- [AttributeTable](Components/AttributeTable/README.md)
- [CoordinateDisplay](Components/CoordinateDisplay/README.md)
- [OverviewMap](Components/OverviewMap/README.md)
- [Print](Components/Print/README.md)

## Examples

Check out the demo application:

```bash
cd examples/Honua.MapSDK.DemoApp
dotnet run
```

Visit https://localhost:5001 to see all components in action.

## Requirements

- .NET 9.0 or later
- Modern browser with WebGL support
- Optional: MapTiler API key for basemaps
- Optional: Mapbox API key for geocoding

## Installation from Source

```bash
# Clone repository
git clone https://github.com/honua/Honua.Server.git
cd Honua.Server

# Build MapSDK
dotnet build src/Honua.MapSDK/

# Run tests
dotnet test tests/Honua.MapSDK.Tests/

# Run demo
dotnet run --project examples/Honua.MapSDK.DemoApp/
```

## Browser Support

| Browser | Minimum Version |
|---------|----------------|
| Chrome | 79+ |
| Firefox | 70+ |
| Safari | 13.1+ |
| Edge | 79+ |

## Performance Targets

- Initial load: < 2s
- Map interaction: 60 FPS
- Data grid rendering: < 100ms for 10,000 rows
- Chart rendering: < 500ms for 100,000 points
- Memory usage: < 100MB for typical dashboards

## Roadmap

- [x] Core MapSDK with ComponentBus
- [x] 20+ production components
- [x] Configuration export/import
- [x] Performance optimizations
- [x] Testing utilities
- [ ] WebGPU compute acceleration
- [ ] 3D terrain visualization
- [ ] Real-time collaboration
- [ ] Plugin system
- [ ] Cloud sync for configurations

## Contributing

We welcome contributions! See [CONTRIBUTING.md](../../docs/mapsdk/Contributing.md) for guidelines.

## License

Honua.MapSDK is licensed under the Elastic License 2.0 (ELv2). See [LICENSE](../../LICENSE) for details.

## Support

- **Documentation**: https://docs.honua.io
- **Issues**: https://github.com/honua/Honua.Server/issues
- **Discussions**: https://github.com/honua/Honua.Server/discussions
- **Email**: support@honua.io

## Acknowledgments

Built with:
- [MapLibre GL JS](https://maplibre.org/) - Open-source map rendering
- [MudBlazor](https://mudblazor.com/) - Material Design components
- [Blazor](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor) - .NET web framework

---

**Made with ❤️ by the Honua team**
