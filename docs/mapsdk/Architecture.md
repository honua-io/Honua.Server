# Honua.MapSDK Architecture

This document provides a deep dive into the architecture and design patterns of Honua.MapSDK, explaining how components work together to create a seamless mapping experience.

## Table of Contents

1. [Overview](#overview)
2. [Component Architecture](#component-architecture)
3. [ComponentBus Messaging System](#componentbus-messaging-system)
4. [JavaScript Interop Patterns](#javascript-interop-patterns)
5. [State Management](#state-management)
6. [Performance Architecture](#performance-architecture)
7. [Extensibility Points](#extensibility-points)
8. [Design Patterns](#design-patterns)

## Overview

Honua.MapSDK is built on three core architectural principles:

1. **Loose Coupling** - Components don't reference each other directly
2. **Message-Driven** - All inter-component communication happens through messages
3. **Declarative** - Configuration over imperative code

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      Blazor Application Layer                    │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐       │
│  │Component │  │Component │  │Component │  │Component │       │
│  │    A     │  │    B     │  │    C     │  │    D     │       │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘  └────┬─────┘       │
│       │             │             │             │               │
│       └─────────────┴─────────────┴─────────────┘               │
│                          │                                       │
└──────────────────────────┼───────────────────────────────────────┘
                           │
┌──────────────────────────▼───────────────────────────────────────┐
│                    ComponentBus (Message Hub)                     │
│  ┌───────────────┐  ┌───────────────┐  ┌───────────────┐        │
│  │  Subscriptions │  │  Publishers   │  │  Message      │        │
│  │  Registry      │  │  Queue        │  │  History      │        │
│  └───────────────┘  └───────────────┘  └───────────────┘        │
└──────────────────────────┬───────────────────────────────────────┘
                           │
┌──────────────────────────▼───────────────────────────────────────┐
│                     Service Layer                                 │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐        │
│  │DataLoader│  │DataCache │  │GeocodingSvc│ │ConfigSvc│        │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘  └────┬─────┘        │
│       │             │             │             │                │
└───────┼─────────────┼─────────────┼─────────────┼────────────────┘
        │             │             │             │
┌───────▼─────────────▼─────────────▼─────────────▼────────────────┐
│                    JavaScript Interop Layer                       │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐        │
│  │MapLibre  │  │Chart.js  │  │MapBox GL │  │Custom    │        │
│  │   GL     │  │          │  │Draw      │  │Modules   │        │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘        │
└───────────────────────────────────────────────────────────────────┘
```

## Component Architecture

### Component Hierarchy

Components in MapSDK follow a clear hierarchy:

```
DisposableComponentBase (Base class)
    │
    ├─── Map Components
    │    ├─── HonuaMap (core)
    │    ├─── HonuaOverviewMap
    │    ├─── HonuaCompare
    │    └─── HonuaHeatmap
    │
    ├─── Data Components
    │    ├─── HonuaDataGrid
    │    ├─── HonuaAttributeTable
    │    └─── HonuaChart
    │
    ├─── Control Components
    │    ├─── HonuaSearch
    │    ├─── HonuaFilterPanel
    │    ├─── HonuaLegend
    │    ├─── HonuaLayerList
    │    └─── HonuaBookmarks
    │
    ├─── Drawing Components
    │    ├─── HonuaDraw
    │    └─── HonuaEditor
    │
    └─── Utility Components
         ├─── HonuaPopup
         ├─── HonuaCoordinateDisplay
         ├─── HonuaTimeline
         └─── HonuaImportWizard
```

### Component Lifecycle

All MapSDK components follow this lifecycle:

```csharp
public class ExampleComponent : DisposableComponentBase
{
    // 1. Dependency Injection
    [Inject] private ComponentBus Bus { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // 2. Parameters
    [Parameter] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [Parameter] public string? SyncWith { get; set; }

    // 3. State
    private IJSObjectReference? _jsModule;
    private bool _initialized = false;

    // 4. Initialization
    protected override void OnInitialized()
    {
        // Subscribe to messages
        SubscribeToMessage<MapExtentChangedMessage>(HandleExtentChange);
        SubscribeToMessage<FilterAppliedMessage>(HandleFilterApplied);
    }

    // 5. First Render
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Initialize JavaScript
            await InitializeJavaScript();

            // Setup event handlers
            SetupEventHandlers();

            _initialized = true;
        }
    }

    // 6. Message Handlers
    private void HandleExtentChange(MessageArgs<MapExtentChangedMessage> args)
    {
        if (args.Message.MapId == SyncWith)
        {
            // React to map extent change
            UpdateData(args.Message.Bounds);
        }
    }

    // 7. Disposal (automatic via DisposableComponentBase)
    protected override void OnDispose()
    {
        // Custom cleanup if needed
        // Subscriptions are auto-cleaned
    }
}
```

### Component Communication Matrix

| Source → Target | Via Message | Use Case |
|----------------|-------------|----------|
| Map → Grid | `MapExtentChangedMessage` | Update visible rows |
| Map → Chart | `MapExtentChangedMessage` | Update chart with filtered data |
| Map → Legend | `LayerVisibilityChangedMessage` | Update legend items |
| Grid → Map | `DataRowSelectedMessage` | Highlight feature on map |
| FilterPanel → Map | `FilterAppliedMessage` | Apply spatial filter |
| Search → Map | `SearchResultSelectedMessage` | Zoom to location |
| Timeline → Map | `TimeChangedMessage` | Filter temporal data |
| Draw → Map | `FeatureDrawnMessage` | Add drawn feature |
| ImportWizard → Map | `DataImportedMessage` | Load imported data |

## ComponentBus Messaging System

### Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                        ComponentBus                          │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐ │
│  │           Subscription Registry                        │ │
│  │  Dictionary<Type, List<Delegate>>                      │ │
│  │                                                        │ │
│  │  MapExtentChangedMessage → [Handler1, Handler2, ...]  │ │
│  │  FeatureClickedMessage   → [Handler1, Handler2, ...]  │ │
│  │  ...                                                   │ │
│  └────────────────────────────────────────────────────────┘ │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐ │
│  │           Message Processing Pipeline                  │ │
│  │                                                        │ │
│  │  1. Receive Message                                    │ │
│  │  2. Create MessageArgs (metadata wrapper)              │ │
│  │  3. Find Subscribers                                   │ │
│  │  4. Invoke Handlers (parallel)                         │ │
│  │  5. Error Handling                                     │ │
│  │  6. Logging                                            │ │
│  └────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────┘
```

### Message Flow Diagram

```
Publisher Component                ComponentBus               Subscriber Components
      │                                 │                           │
      │ 1. Create Message              │                           │
      │─────────────────────────────▶  │                           │
      │                                 │                           │
      │ 2. PublishAsync()               │                           │
      │─────────────────────────────▶  │                           │
      │                                 │                           │
      │                                 │ 3. Wrap in MessageArgs   │
      │                                 │    (add metadata)         │
      │                                 │                           │
      │                                 │ 4. Find Subscribers      │
      │                                 │    for message type       │
      │                                 │                           │
      │                                 │ 5. Invoke Handler        │
      │                                 │───────────────────────▶  │
      │                                 │                           │
      │                                 │ 5. Invoke Handler        │
      │                                 │───────────────────────▶  │
      │                                 │                           │
      │                                 │ 5. Invoke Handler        │
      │                                 │───────────────────────▶  │
      │                                 │                           │
      │                                 │ 6. Log Completion        │
      │                                 │                           │
      │ 7. Async Complete              │                           │
      │◀─────────────────────────────  │                           │
```

### Message Types Taxonomy

```
MapSDK Messages
│
├── Spatial Events
│   ├── MapExtentChangedMessage
│   ├── FeatureClickedMessage
│   ├── FeatureHoveredMessage
│   ├── FeatureSelectedMessage
│   └── MapReadyMessage
│
├── Data Events
│   ├── DataLoadedMessage
│   ├── DataRowSelectedMessage
│   ├── DataRequestMessage
│   └── DataResponseMessage
│
├── Filter Events
│   ├── FilterAppliedMessage
│   ├── FilterClearedMessage
│   └── AllFiltersClearedMessage
│
├── Layer Events
│   ├── LayerVisibilityChangedMessage
│   ├── LayerOpacityChangedMessage
│   ├── LayerAddedMessage
│   ├── LayerRemovedMessage
│   ├── LayerReorderedMessage
│   ├── LayerSelectedMessage
│   └── LayerMetadataUpdatedMessage
│
├── Navigation Events
│   ├── FlyToRequestMessage
│   ├── FitBoundsRequestMessage
│   ├── SearchResultSelectedMessage
│   ├── BookmarkSelectedMessage
│   └── BasemapChangedMessage
│
├── Drawing Events
│   ├── FeatureDrawnMessage
│   ├── FeatureEditedMessage
│   ├── FeatureDeletedMessage
│   ├── FeatureMeasuredMessage
│   └── DrawModeChangedMessage
│
├── Editing Events
│   ├── FeatureCreatedMessage
│   ├── FeatureUpdatedMessage
│   ├── EditSessionStartedMessage
│   ├── EditSessionEndedMessage
│   ├── EditSessionStateChangedMessage
│   └── EditValidationErrorMessage
│
├── Temporal Events
│   ├── TimeChangedMessage
│   └── TimelineStateChangedMessage
│
├── Import Events
│   ├── DataImportedMessage
│   ├── ImportProgressMessage
│   └── ImportErrorMessage
│
└── UI Events
    ├── PopupOpenedMessage
    ├── PopupClosedMessage
    ├── CoordinateClickedMessage
    ├── CoordinatePinnedMessage
    └── OverviewMapClickedMessage
```

### Message Metadata

Every message is wrapped in `MessageArgs<T>`:

```csharp
public class MessageArgs<TMessage> where TMessage : class
{
    // The actual message payload
    public required TMessage Message { get; init; }

    // Component that published the message
    public string? Source { get; init; }

    // When the message was published
    public DateTime Timestamp { get; init; }

    // Unique ID for correlation/debugging
    public string CorrelationId { get; init; }
}
```

This enables:
- **Debugging** - Track message origin and timing
- **Filtering** - Ignore messages from certain sources
- **Correlation** - Link related messages
- **Auditing** - Log message flow

## JavaScript Interop Patterns

### Interop Architecture

```
┌───────────────────────────────────────────────────────────┐
│                   Blazor Component (C#)                    │
│                                                            │
│  ┌──────────────────────────────────────────────────────┐ │
│  │  Component Code                                      │ │
│  │  - State Management                                  │ │
│  │  - Event Handlers                                    │ │
│  │  - ComponentBus Integration                          │ │
│  └──────────────────┬───────────────────────────────────┘ │
│                     │                                      │
│  ┌──────────────────▼───────────────────────────────────┐ │
│  │  IJSRuntime / JSObjectReference                      │ │
│  │  - Invoke JS functions                               │ │
│  │  - Pass parameters                                   │ │
│  │  - Receive return values                             │ │
│  └──────────────────┬───────────────────────────────────┘ │
└────────────────────┬┼────────────────────────────────────┘
                     ││
                     ││ JS Interop Boundary
                     ││
┌────────────────────▼▼────────────────────────────────────┐
│             JavaScript Module (honua-map.js)             │
│                                                           │
│  ┌──────────────────────────────────────────────────┐   │
│  │  Module Functions                                │   │
│  │  - createMap(container, options, dotNetRef)      │   │
│  │  - flyTo(center, zoom)                           │   │
│  │  - addLayer(layerConfig)                         │   │
│  │  - dispose()                                     │   │
│  └──────────────────┬───────────────────────────────┘   │
│                     │                                    │
│  ┌──────────────────▼───────────────────────────────┐   │
│  │  Map Instance                                    │   │
│  │  - MapLibre GL Map object                        │   │
│  │  - Event listeners                               │   │
│  │  - Layer management                              │   │
│  └──────────────────┬───────────────────────────────┘   │
│                     │                                    │
│  ┌──────────────────▼───────────────────────────────┐   │
│  │  Callbacks to .NET                               │   │
│  │  - dotNetRef.invokeMethodAsync()                 │   │
│  │  - Pass events back to C#                        │   │
│  └──────────────────────────────────────────────────┘   │
└───────────────────────────────────────────────────────────┘
```

### Interop Patterns

#### Pattern 1: Module Import and Initialization

```csharp
// Component initialization
private IJSObjectReference? _mapModule;
private IJSObjectReference? _mapInstance;

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        // 1. Import JavaScript module
        _mapModule = await JS.InvokeAsync<IJSObjectReference>(
            "import",
            "./_content/Honua.MapSDK/js/honua-map.js"
        );

        // 2. Create map instance
        _mapInstance = await _mapModule.InvokeAsync<IJSObjectReference>(
            "createMap",
            _mapContainer,
            new
            {
                id = Id,
                style = MapStyle,
                center = Center,
                zoom = Zoom
            },
            DotNetObjectReference.Create(this)
        );
    }
}
```

#### Pattern 2: Bidirectional Communication

```javascript
// JavaScript side (honua-map.js)
export function createMap(container, options, dotNetRef) {
    const map = new maplibregl.Map({
        container: container,
        style: options.style,
        center: options.center,
        zoom: options.zoom
    });

    // Set up event listeners that call back to .NET
    map.on('moveend', () => {
        dotNetRef.invokeMethodAsync('OnExtentChangedInternal',
            map.getBounds().toArray(),
            map.getZoom(),
            map.getCenter().toArray(),
            map.getBearing(),
            map.getPitch()
        );
    });

    map.on('click', (e) => {
        const features = map.queryRenderedFeatures(e.point);
        if (features.length > 0) {
            const feature = features[0];
            dotNetRef.invokeMethodAsync('OnFeatureClickedInternal',
                feature.layer.id,
                feature.id,
                feature.properties,
                feature.geometry
            );
        }
    });

    return {
        // Return object with methods C# can call
        flyTo: (options) => {
            map.flyTo(options);
        },
        getBounds: () => {
            return map.getBounds().toArray();
        },
        dispose: () => {
            map.remove();
        }
    };
}
```

```csharp
// C# side - JSInvokable methods
[JSInvokable]
public async Task OnExtentChangedInternal(
    double[] bounds,
    double zoom,
    double[] center,
    double bearing,
    double pitch)
{
    var message = new MapExtentChangedMessage
    {
        MapId = Id,
        Bounds = bounds,
        Zoom = zoom,
        Center = center,
        Bearing = bearing,
        Pitch = pitch
    };

    await Bus.PublishAsync(message, Id);
    await OnExtentChanged.InvokeAsync(message);
}
```

#### Pattern 3: Error Handling Across Interop

```csharp
private async Task<T?> SafeJsInvokeAsync<T>(
    string method,
    params object[] args)
{
    try
    {
        if (_mapInstance == null)
        {
            Logger.LogWarning("Map instance not initialized");
            return default;
        }

        return await _mapInstance.InvokeAsync<T>(method, args);
    }
    catch (JSException jsEx)
    {
        Logger.LogError(jsEx, "JavaScript error in {Method}", method);
        await Bus.PublishAsync(new ErrorMessage
        {
            Source = Id,
            Message = jsEx.Message,
            Severity = ErrorSeverity.Error
        });
        return default;
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Interop error in {Method}", method);
        return default;
    }
}
```

## State Management

### Component-Level State

Each component manages its own state:

```csharp
public class HonuaMap : DisposableComponentBase
{
    // Public parameters (inputs)
    [Parameter] public string MapStyle { get; set; } = "...";
    [Parameter] public double[] Center { get; set; } = new[] { 0.0, 0.0 };
    [Parameter] public double Zoom { get; set; } = 2;

    // Private state
    private bool _isInitialized = false;
    private double _currentZoom = 0;
    private double[] _currentCenter = new[] { 0.0, 0.0 };
    private List<Layer> _layers = new();

    // State updates trigger re-renders
    private async Task UpdateZoom(double newZoom)
    {
        _currentZoom = newZoom;
        StateHasChanged(); // Trigger Blazor re-render
        await PublishZoomChanged();
    }
}
```

### Shared State via ComponentBus

For shared state between components:

```csharp
// Component A publishes state change
await Bus.PublishAsync(new FilterAppliedMessage
{
    FilterId = "filter1",
    Expression = filterExpression,
    AffectedLayers = new[] { "layer1", "layer2" }
});

// Component B reacts to state change
Bus.Subscribe<FilterAppliedMessage>(args =>
{
    ApplyFilter(args.Message.Expression);
    StateHasChanged();
});
```

### Configuration State

Persistent configuration through `IMapConfigurationService`:

```csharp
// Save configuration
var config = new MapConfiguration
{
    Name = "My Map",
    Settings = new MapSettings { ... },
    Layers = _layers.Select(l => l.ToConfiguration()).ToList()
};

await ConfigService.SaveAsync(config);

// Load configuration
var loaded = await ConfigService.LoadAsync(configId);
ApplyConfiguration(loaded);
```

## Performance Architecture

### Caching Layer

```
┌─────────────────────────────────────────────────────────────┐
│                      DataCache (LRU)                         │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Memory Cache                                         │  │
│  │  - Max Size: 100MB (configurable)                     │  │
│  │  - TTL: 600s (configurable)                           │  │
│  │  - Eviction: LRU policy                               │  │
│  └───────────────────────────────────────────────────────┘  │
│                                                              │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Compression                                          │  │
│  │  - GZIP for large payloads                            │  │
│  │  - Brotli for maximum compression                     │  │
│  │  - Automatic compression detection                    │  │
│  └───────────────────────────────────────────────────────┘  │
│                                                              │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Cache Statistics                                     │  │
│  │  - Hit rate tracking                                  │  │
│  │  - Size monitoring                                    │  │
│  │  - Eviction metrics                                   │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

### Data Loading Pipeline

```
Request → Deduplication → Cache Check → HTTP Request → Compression → Parse → Cache Store → Return
    │                         │                                                    │
    │                         └─── Cache Hit ────────────────────────────────────┘
    │
    └─── Already in Flight ───→ Wait for existing request ───────────────────────┘
```

### Rendering Optimization

```csharp
public class RenderingOptions
{
    // Virtual scrolling threshold
    public int VirtualScrollThreshold { get; set; } = 1000;

    // Chart downsampling threshold
    public int ChartDownsampleThreshold { get; set; } = 10000;

    // Map clustering threshold
    public int MapClusteringThreshold { get; set; } = 1000;

    // Debounce delay for frequent updates
    public int DebounceDelayMs { get; set; } = 300;
}
```

## Extensibility Points

### Custom Components

Create custom components that integrate with ComponentBus:

```csharp
public class CustomAnalysisComponent : DisposableComponentBase
{
    [Inject] private ComponentBus Bus { get; set; } = default!;
    [Parameter] public string SyncWith { get; set; } = "";

    protected override void OnInitialized()
    {
        // Subscribe to map events
        SubscribeToMessage<MapExtentChangedMessage>(HandleExtentChange);

        // Subscribe to custom events
        SubscribeToMessage<CustomAnalysisRequestMessage>(HandleAnalysisRequest);
    }

    private async Task HandleExtentChange(
        MessageArgs<MapExtentChangedMessage> args)
    {
        if (args.Message.MapId == SyncWith)
        {
            // Perform analysis on visible area
            var results = await AnalyzeArea(args.Message.Bounds);

            // Publish results
            await Bus.PublishAsync(new CustomAnalysisCompleteMessage
            {
                AnalysisId = Guid.NewGuid().ToString(),
                Results = results
            }, source: "CustomAnalysis");
        }
    }
}
```

### Custom Message Types

```csharp
// Define custom message
public class CustomAnalysisRequestMessage
{
    public required string AnalysisType { get; init; }
    public required double[] Bounds { get; init; }
    public Dictionary<string, object> Parameters { get; init; } = new();
}

// Publish from any component
await Bus.PublishAsync(new CustomAnalysisRequestMessage
{
    AnalysisType = "hotspot",
    Bounds = mapBounds,
    Parameters = new() { ["threshold"] = 0.95 }
});
```

### Custom Services

```csharp
// Register custom service
builder.Services.AddScoped<ICustomAnalysisService, CustomAnalysisService>();

// Inject in components
[Inject] private ICustomAnalysisService AnalysisService { get; set; }
```

## Design Patterns

### 1. Publish-Subscribe Pattern

**Intent:** Loose coupling between components

**Implementation:** ComponentBus

```csharp
// Publisher
await Bus.PublishAsync(message);

// Subscriber
Bus.Subscribe<TMessage>(HandleMessage);
```

### 2. Dependency Injection Pattern

**Intent:** Decouple service creation from usage

**Implementation:** .NET DI container

```csharp
// Registration
builder.Services.AddScoped<ComponentBus>();
builder.Services.AddScoped<DataLoader>();

// Injection
[Inject] private ComponentBus Bus { get; set; }
```

### 3. Facade Pattern

**Intent:** Simplified interface to complex subsystems

**Implementation:** DataLoader wraps HTTP, caching, compression

```csharp
// Complex operations hidden behind simple API
var data = await DataLoader.LoadJsonAsync<T>(url);
```

### 4. Strategy Pattern

**Intent:** Pluggable algorithms

**Implementation:** Geocoding providers

```csharp
public interface IGeocoder
{
    Task<SearchResult[]> SearchAsync(string query);
}

public class NominatimGeocoder : IGeocoder { ... }
public class MapboxGeocoder : IGeocoder { ... }
```

### 5. Observer Pattern

**Intent:** Notify dependents of state changes

**Implementation:** ComponentBus subscriptions

```csharp
Bus.Subscribe<MapExtentChangedMessage>(args => {
    // React to changes
});
```

### 6. Factory Pattern

**Intent:** Create objects without specifying exact class

**Implementation:** File parser factory

```csharp
public class FileParserFactory
{
    public IFileParser CreateParser(ImportFormat format)
    {
        return format switch
        {
            ImportFormat.GeoJson => new GeoJsonParser(),
            ImportFormat.Csv => new CsvParser(),
            ImportFormat.Kml => new KmlParser(),
            _ => throw new NotSupportedException()
        };
    }
}
```

### 7. Disposable Pattern

**Intent:** Deterministic resource cleanup

**Implementation:** DisposableComponentBase

```csharp
public class DisposableComponentBase : ComponentBase, IDisposable
{
    private readonly List<IDisposable> _disposables = new();

    protected void RegisterDisposable(IDisposable disposable)
    {
        _disposables.Add(disposable);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable?.Dispose();
        }
        OnDispose();
    }

    protected virtual void OnDispose() { }
}
```

## Summary

Honua.MapSDK's architecture provides:

- **Loose Coupling** - Components communicate through messages, not direct references
- **Scalability** - Add components without modifying existing code
- **Testability** - Mock ComponentBus for isolated testing
- **Performance** - Built-in caching, streaming, and optimization
- **Extensibility** - Easy to add custom components and messages
- **Type Safety** - Strongly-typed messages with compile-time checking
- **Error Handling** - Graceful degradation and error boundaries
- **Developer Experience** - Simple, declarative API

This architecture enables building complex mapping applications while keeping the codebase maintainable and performant.

## Further Reading

- [Component Catalog](ComponentCatalog.md) - Detailed component documentation
- [Best Practices](BestPractices.md) - Recommended patterns and approaches
- [Performance Guide](../Honua.MapSDK/PERFORMANCE_AND_OPTIMIZATIONS.md) - Optimization techniques
- [API Reference](api/) - Complete API documentation
