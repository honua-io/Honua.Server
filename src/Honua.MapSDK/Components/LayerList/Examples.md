# HonuaLayerList Examples

Practical examples demonstrating the HonuaLayerList component in various scenarios.

## Table of Contents

1. [Basic Layer List](#1-basic-layer-list)
2. [Layer List with Groups](#2-layer-list-with-groups)
3. [Custom Layer Templates](#3-custom-layer-templates)
4. [Drag & Drop Reordering](#4-drag--drop-reordering)
5. [Layer Search and Filtering](#5-layer-search-and-filtering)
6. [Integration with Legend Component](#6-integration-with-legend-component)
7. [Event Handling](#7-event-handling)
8. [Dynamic Layer Management](#8-dynamic-layer-management)
9. [Responsive Layout](#9-responsive-layout)
10. [Multi-Map Sync](#10-multi-map-sync)

---

## 1. Basic Layer List

Simple layer list with default settings.

### Code

```razor
@page "/examples/basic-layerlist"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.LayerList

<div class="map-container">
    <HonuaMap
        Id="basic-map"
        Center="new[] { -122.4, 37.8 }"
        Zoom="10"
        Style="mapbox://styles/mapbox/streets-v12"
        Width="100%"
        Height="600px" />

    <HonuaLayerList
        SyncWith="basic-map"
        Position="top-right"
        Title="Map Layers"
        Width="320px" />
</div>

@code {
    // No additional code needed - component auto-discovers layers
}
```

### Output

A floating layer list in the top-right corner displaying all map layers with:
- Visibility toggles
- Layer type icons
- Feature counts
- Opacity controls (when expanded)

---

## 2. Layer List with Groups

Organize layers into collapsible folders/groups.

### Code

```razor
@page "/examples/grouped-layers"
@using Honua.MapSDK.Models

<div class="map-container">
    <HonuaMap
        Id="grouped-map"
        Center="new[] { -100.0, 40.0 }"
        Zoom="4"
        Style="mapbox://styles/mapbox/light-v11"
        Width="100%"
        Height="600px"
        OnMapReady="OnMapReady" />

    <HonuaLayerList
        SyncWith="grouped-map"
        Position="top-left"
        AllowGrouping="true"
        ShowSearch="true"
        Width="350px" />
</div>

@code {
    private async Task OnMapReady(MapReadyEventArgs args)
    {
        // Add layers with group metadata
        await AddLayerWithGroup("population-density", "Demographics", "Population Density");
        await AddLayerWithGroup("income-levels", "Demographics", "Income Levels");
        await AddLayerWithGroup("roads-major", "Infrastructure", "Major Roads");
        await AddLayerWithGroup("roads-minor", "Infrastructure", "Minor Roads");
        await AddLayerWithGroup("parks", "Recreation", "Parks & Trails");
    }

    private async Task AddLayerWithGroup(string layerId, string groupName, string layerName)
    {
        // Implementation to add layer with metadata
        var layer = new LayerInfo
        {
            Id = layerId,
            Name = layerName,
            Type = "fill",
            GroupId = groupName.ToLowerInvariant().Replace(" ", "-"),
            Metadata = new Dictionary<string, object>
            {
                ["group"] = groupName,
                ["name"] = layerName
            }
        };

        // Add to map...
    }
}
```

### Features Demonstrated

- Layer grouping by category
- Collapsible group headers
- Group visibility toggle (affects all layers in group)
- Search across grouped layers

---

## 3. Custom Layer Templates

Customize layer appearance and information display.

### Code

```razor
@page "/examples/custom-templates"

<div class="map-container">
    <HonuaMap Id="custom-map" Center="new[] { 0.0, 0.0 }" Zoom="2" />

    <HonuaLayerList
        SyncWith="custom-map"
        Position="top-right"
        ViewMode="detailed"
        ShowLegend="true"
        Width="400px" />
</div>

<style>
    /* Custom layer item styling */
    .honua-layerlist .layer-item {
        border-left: 3px solid transparent;
        transition: all 0.3s;
    }

    .honua-layerlist .layer-item:hover {
        border-left-color: var(--mud-palette-primary);
        background: linear-gradient(90deg,
            rgba(33, 150, 243, 0.05) 0%,
            transparent 100%);
    }

    .honua-layerlist .layer-item.selected {
        border-left-color: var(--mud-palette-primary);
        background: rgba(33, 150, 243, 0.1);
    }

    /* Custom legend styling */
    .honua-layerlist .legend-swatch {
        box-shadow: 0 2px 4px rgba(0,0,0,0.2);
        border-radius: 4px;
    }
</style>

@code {
    private List<LayerInfo> _customLayers = new()
    {
        new LayerInfo
        {
            Id = "temperature",
            Name = "Temperature",
            Type = "fill",
            Description = "Average temperature in degrees Celsius",
            Icon = Icons.Material.Filled.Thermostat,
            LegendItems = new List<LegendItem>
            {
                new() { Label = "Hot (>30°C)", Color = "#d73027" },
                new() { Label = "Warm (20-30°C)", Color = "#fee08b" },
                new() { Label = "Cool (10-20°C)", Color = "#91bfdb" },
                new() { Label = "Cold (<10°C)", Color = "#4575b4" }
            },
            FeatureCount = 2547,
            Attribution = "Weather Data © OpenWeather"
        },
        new LayerInfo
        {
            Id = "precipitation",
            Name = "Precipitation",
            Type = "circle",
            Description = "Annual rainfall in millimeters",
            Icon = Icons.Material.Filled.WaterDrop,
            LegendItems = new List<LegendItem>
            {
                new() {
                    Label = "Heavy (>2000mm)",
                    Color = "#2166ac",
                    SymbolType = "circle",
                    Size = 12
                },
                new() {
                    Label = "Moderate (500-2000mm)",
                    Color = "#92c5de",
                    SymbolType = "circle",
                    Size = 8
                },
                new() {
                    Label = "Light (<500mm)",
                    Color = "#f4a582",
                    SymbolType = "circle",
                    Size = 4
                }
            },
            FeatureCount = 1823
        }
    };
}
```

### Features Demonstrated

- Custom icons for layers
- Rich legend items with colors and symbols
- Layer descriptions and metadata
- Attribution information
- Custom CSS styling

---

## 4. Drag & Drop Reordering

Enable users to reorder layers by dragging.

### Code

```razor
@page "/examples/drag-drop"

<div class="map-container">
    <HonuaMap Id="reorder-map" Center="new[] { -95.7, 37.1 }" Zoom="4" />

    <HonuaLayerList
        SyncWith="reorder-map"
        AllowReorder="true"
        Position="top-right"
        OnLayerReordered="HandleLayerReordered"
        Width="340px" />
</div>

<MudPaper Class="info-panel" Elevation="2">
    <MudText Typo="Typo.h6">Layer Order</MudText>
    <MudText Typo="Typo.body2">
        Drag layers to reorder. Top layers render above bottom layers.
    </MudText>
    @if (_reorderHistory.Any())
    {
        <MudText Typo="Typo.caption" Class="mt-2">Recent Changes:</MudText>
        <MudList Dense="true">
            @foreach (var change in _reorderHistory.TakeLast(5))
            {
                <MudListItem>
                    <MudText Typo="Typo.caption">@change</MudText>
                </MudListItem>
            }
        </MudList>
    }
</MudPaper>

@code {
    private List<string> _reorderHistory = new();

    private async Task HandleLayerReordered(List<LayerInfo> reorderedLayers)
    {
        var message = $"{DateTime.Now:HH:mm:ss} - Layers reordered: {string.Join(", ", reorderedLayers.Select(l => l.Name))}";
        _reorderHistory.Add(message);

        // Optionally save order to backend
        await SaveLayerOrder(reorderedLayers);

        StateHasChanged();
    }

    private async Task SaveLayerOrder(List<LayerInfo> layers)
    {
        // Save to database or local storage
        await Task.CompletedTask;
    }
}

<style>
    .info-panel {
        position: absolute;
        bottom: 20px;
        right: 20px;
        padding: 16px;
        max-width: 340px;
    }
</style>
```

### Features Demonstrated

- Drag handle on each layer
- Real-time reordering
- Event tracking
- Visual feedback during drag
- Persistence of layer order

---

## 5. Layer Search and Filtering

Search and filter layers by name, description, or metadata.

### Code

```razor
@page "/examples/layer-search"

<div class="map-container">
    <HonuaMap Id="search-map" Center="new[] { -74.0, 40.7 }" Zoom="10" />

    <HonuaLayerList
        SyncWith="search-map"
        ShowSearch="true"
        Position="top-left"
        Width="360px" />

    <div class="filter-panel">
        <MudPaper Elevation="2" Class="pa-4">
            <MudText Typo="Typo.h6" Class="mb-3">Advanced Filters</MudText>

            <MudSelect @bind-Value="_layerTypeFilter"
                       Label="Layer Type"
                       Variant="Variant.Outlined"
                       Margin="Margin.Dense">
                <MudSelectItem Value="@("all")">All Types</MudSelectItem>
                <MudSelectItem Value="@("fill")">Polygon</MudSelectItem>
                <MudSelectItem Value="@("line")">Line</MudSelectItem>
                <MudSelectItem Value="@("circle")">Point</MudSelectItem>
                <MudSelectItem Value="@("raster")">Raster</MudSelectItem>
            </MudSelect>

            <MudCheckBox @bind-Checked="_showVisibleOnly"
                         Label="Visible Layers Only"
                         Class="mt-2" />

            <MudCheckBox @bind-Checked="_showWithDataOnly"
                         Label="Layers with Data"
                         Class="mt-1" />

            <MudButton Variant="Variant.Filled"
                       Color="Color.Primary"
                       FullWidth="true"
                       Class="mt-3"
                       OnClick="ApplyFilters">
                Apply Filters
            </MudButton>
        </MudPaper>
    </div>
</div>

@code {
    private string _layerTypeFilter = "all";
    private bool _showVisibleOnly = false;
    private bool _showWithDataOnly = false;

    private void ApplyFilters()
    {
        // Filter logic would be implemented here
        // Could use ComponentBus messages to filter layers
    }
}

<style>
    .filter-panel {
        position: absolute;
        bottom: 20px;
        left: 20px;
        width: 280px;
    }
</style>
```

### Features Demonstrated

- Built-in search functionality
- Custom filter panel
- Filter by layer type
- Filter by visibility state
- Filter by data availability

---

## 6. Integration with Legend Component

Combine LayerList with Legend for complete layer control.

### Code

```razor
@page "/examples/layerlist-legend"

<div class="map-container">
    <HonuaMap Id="legend-map"
              Center="new[] { -105.0, 40.0 }"
              Zoom="5"
              OnMapReady="OnMapReady" />

    <!-- Layer List on left -->
    <HonuaLayerList
        @ref="_layerList"
        SyncWith="legend-map"
        Position="top-left"
        ShowLegend="false"
        OnLayerSelected="HandleLayerSelected"
        Width="300px" />

    <!-- Detailed Legend on right -->
    <HonuaLegend
        @ref="_legend"
        SyncWith="legend-map"
        Position="top-right"
        Title="@(_selectedLayerName ?? "Select a Layer")"
        ShowTitle="true"
        Width="280px" />
</div>

@code {
    private HonuaLayerList? _layerList;
    private HonuaLegend? _legend;
    private string? _selectedLayerName;

    private async Task OnMapReady(MapReadyEventArgs args)
    {
        // Add some layers with legends
        await AddDemoLayers();
    }

    private async Task HandleLayerSelected(LayerInfo layer)
    {
        _selectedLayerName = layer.Name;

        // Update legend to show selected layer
        // Legend component would filter to show only this layer's legend
        await InvokeAsync(StateHasChanged);
    }

    private async Task AddDemoLayers()
    {
        // Add layers with rich legend information
        await Task.CompletedTask;
    }
}
```

### Features Demonstrated

- Side-by-side LayerList and Legend
- Layer selection updates legend
- Coordinated visibility controls
- Separate concerns (list vs. detail)

---

## 7. Event Handling

Respond to user interactions with layers.

### Code

```razor
@page "/examples/layer-events"

<div class="map-container">
    <HonuaMap Id="events-map" Center="new[] { 0.0, 0.0 }" Zoom="2" />

    <HonuaLayerList
        SyncWith="events-map"
        Position="top-right"
        OnLayerVisibilityChanged="HandleVisibilityChanged"
        OnLayerOpacityChanged="HandleOpacityChanged"
        OnLayerSelected="HandleLayerSelected"
        OnLayerRemoved="HandleLayerRemoved"
        Width="320px" />

    <!-- Event Log -->
    <MudPaper Class="event-log" Elevation="3">
        <MudText Typo="Typo.h6" Class="mb-2">Event Log</MudText>
        <div class="log-content">
            @foreach (var logEntry in _eventLog.TakeLast(10).Reverse())
            {
                <MudText Typo="Typo.caption" Class="log-entry">
                    <MudIcon Icon="@GetEventIcon(logEntry.Type)" Size="Size.Small" />
                    [@logEntry.Time] @logEntry.Message
                </MudText>
            }
        </div>
        <MudButton OnClick="ClearLog"
                   Size="Size.Small"
                   Variant="Variant.Text"
                   Class="mt-2">
            Clear Log
        </MudButton>
    </MudPaper>
</div>

@code {
    private List<LogEntry> _eventLog = new();

    private void HandleVisibilityChanged(LayerInfo layer)
    {
        LogEvent("visibility", $"Layer '{layer.Name}' visibility: {(layer.Visible ? "ON" : "OFF")}");
    }

    private void HandleOpacityChanged(LayerInfo layer)
    {
        LogEvent("opacity", $"Layer '{layer.Name}' opacity: {layer.Opacity:P0}");
    }

    private void HandleLayerSelected(LayerInfo layer)
    {
        LogEvent("select", $"Selected layer: {layer.Name} ({layer.Type})");
    }

    private void HandleLayerRemoved(LayerInfo layer)
    {
        LogEvent("remove", $"Removed layer: {layer.Name}");
    }

    private void LogEvent(string type, string message)
    {
        _eventLog.Add(new LogEntry
        {
            Type = type,
            Message = message,
            Time = DateTime.Now.ToString("HH:mm:ss")
        });
        StateHasChanged();
    }

    private void ClearLog()
    {
        _eventLog.Clear();
    }

    private string GetEventIcon(string type) => type switch
    {
        "visibility" => Icons.Material.Filled.Visibility,
        "opacity" => Icons.Material.Filled.Opacity,
        "select" => Icons.Material.Filled.TouchApp,
        "remove" => Icons.Material.Filled.Delete,
        _ => Icons.Material.Filled.Info
    };

    record LogEntry
    {
        public required string Type { get; init; }
        public required string Message { get; init; }
        public required string Time { get; init; }
    }
}

<style>
    .event-log {
        position: absolute;
        bottom: 20px;
        right: 20px;
        width: 400px;
        padding: 16px;
        max-height: 300px;
    }

    .log-content {
        max-height: 200px;
        overflow-y: auto;
    }

    .log-entry {
        display: flex;
        align-items: center;
        gap: 8px;
        padding: 4px 0;
        border-bottom: 1px solid #f0f0f0;
    }
</style>
```

### Features Demonstrated

- Visibility change events
- Opacity change events
- Layer selection events
- Layer removal events
- Event logging and display
- Real-time feedback

---

## 8. Dynamic Layer Management

Programmatically add, remove, and update layers.

### Code

```razor
@page "/examples/dynamic-layers"
@inject ComponentBus Bus

<div class="map-container">
    <HonuaMap @ref="_map"
              Id="dynamic-map"
              Center="new[] { -98.0, 39.0 }"
              Zoom="4" />

    <HonuaLayerList
        SyncWith="dynamic-map"
        Position="top-right"
        Width="320px" />

    <!-- Control Panel -->
    <MudPaper Class="control-panel" Elevation="3">
        <MudText Typo="Typo.h6" Class="mb-3">Layer Management</MudText>

        <MudTextField @bind-Value="_newLayerName"
                      Label="Layer Name"
                      Variant="Variant.Outlined"
                      Margin="Margin.Dense" />

        <MudSelect @bind-Value="_newLayerType"
                   Label="Layer Type"
                   Variant="Variant.Outlined"
                   Margin="Margin.Dense"
                   Class="mt-2">
            <MudSelectItem Value="@("fill")">Polygon</MudSelectItem>
            <MudSelectItem Value="@("line")">Line</MudSelectItem>
            <MudSelectItem Value="@("circle")">Point</MudSelectItem>
        </MudSelect>

        <MudButton Variant="Variant.Filled"
                   Color="Color.Primary"
                   StartIcon="@Icons.Material.Filled.Add"
                   FullWidth="true"
                   Class="mt-3"
                   OnClick="AddLayer">
            Add Layer
        </MudButton>

        <MudDivider Class="my-3" />

        <MudButton Variant="Variant.Outlined"
                   Color="Color.Secondary"
                   StartIcon="@Icons.Material.Filled.Shuffle"
                   FullWidth="true"
                   OnClick="RandomizeOpacity">
            Randomize All Opacity
        </MudButton>

        <MudButton Variant="Variant.Outlined"
                   Color="Color.Error"
                   StartIcon="@Icons.Material.Filled.DeleteSweep"
                   FullWidth="true"
                   Class="mt-2"
                   OnClick="RemoveAllLayers">
            Remove All Layers
        </MudButton>
    </MudPaper>
</div>

@code {
    private HonuaMap? _map;
    private string _newLayerName = "New Layer";
    private string _newLayerType = "fill";
    private int _layerCounter = 1;

    private async Task AddLayer()
    {
        var layerId = $"layer-{_layerCounter++}";
        var layerName = $"{_newLayerName} {_layerCounter}";

        // Publish layer added message
        await Bus.PublishAsync(new LayerAddedMessage
        {
            LayerId = layerId,
            LayerName = layerName
        });

        _newLayerName = "New Layer";
        StateHasChanged();
    }

    private async Task RandomizeOpacity()
    {
        var random = new Random();
        // This would iterate through layers and set random opacity
        await Task.CompletedTask;
    }

    private async Task RemoveAllLayers()
    {
        // This would remove all non-locked layers
        await Task.CompletedTask;
    }
}

<style>
    .control-panel {
        position: absolute;
        top: 20px;
        left: 20px;
        width: 300px;
        padding: 20px;
    }
</style>
```

### Features Demonstrated

- Programmatic layer addition
- Dynamic layer removal
- Bulk layer operations
- ComponentBus message publishing
- User interface for layer management

---

## 9. Responsive Layout

Adapt layer list for mobile and desktop.

### Code

```razor
@page "/examples/responsive"
@inject IJSRuntime JS

<div class="map-container">
    <HonuaMap Id="responsive-map"
              Center="new[] { -80.0, 35.0 }"
              Zoom="6" />

    @if (_isMobile)
    {
        <!-- Mobile: Bottom sheet -->
        <MudDrawer @bind-Open="_drawerOpen"
                   Anchor="Anchor.Bottom"
                   Elevation="2"
                   Variant="DrawerVariant.Temporary">
            <div class="mobile-drawer-content">
                <div class="drawer-handle" @onclick="ToggleDrawer">
                    <div class="handle-bar"></div>
                </div>
                <HonuaLayerList
                    SyncWith="responsive-map"
                    Position="@null"
                    ViewMode="compact"
                    MaxHeight="60vh"
                    Width="100%" />
            </div>
        </MudDrawer>

        <!-- Floating button to open drawer -->
        <MudFab Color="Color.Primary"
                StartIcon="@Icons.Material.Filled.Layers"
                Class="layers-fab"
                OnClick="ToggleDrawer" />
    }
    else
    {
        <!-- Desktop: Floating panel -->
        <HonuaLayerList
            SyncWith="responsive-map"
            Position="top-right"
            ViewMode="detailed"
            Width="360px"
            MaxHeight="80vh" />
    }
</div>

@code {
    private bool _isMobile = false;
    private bool _drawerOpen = false;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _isMobile = await JS.InvokeAsync<bool>("eval",
                "window.innerWidth < 768");
            StateHasChanged();
        }
    }

    private void ToggleDrawer()
    {
        _drawerOpen = !_drawerOpen;
    }
}

<style>
    .layers-fab {
        position: fixed;
        bottom: 24px;
        right: 24px;
        z-index: 100;
    }

    .mobile-drawer-content {
        padding: 16px;
    }

    .drawer-handle {
        display: flex;
        justify-content: center;
        padding: 8px 0;
        cursor: pointer;
    }

    .handle-bar {
        width: 40px;
        height: 4px;
        background: #ccc;
        border-radius: 2px;
    }

    @media (max-width: 767px) {
        .honua-layerlist {
            font-size: 13px;
        }

        .layer-main {
            padding: 8px 10px !important;
        }
    }
</style>
```

### Features Demonstrated

- Responsive breakpoints
- Mobile drawer pattern
- Desktop floating panel
- Compact view for mobile
- FAB (Floating Action Button) trigger

---

## 10. Multi-Map Sync

Synchronize layer lists across multiple maps.

### Code

```razor
@page "/examples/multi-map"

<div class="multi-map-layout">
    <div class="map-panel">
        <MudText Typo="Typo.h6" Class="mb-2">Map 1</MudText>
        <HonuaMap Id="map-1"
                  Center="new[] { -100.0, 40.0 }"
                  Zoom="4"
                  Height="400px" />
    </div>

    <div class="map-panel">
        <MudText Typo="Typo.h6" Class="mb-2">Map 2</MudText>
        <HonuaMap Id="map-2"
                  Center="new[] { -100.0, 40.0 }"
                  Zoom="4"
                  Height="400px" />
    </div>

    <!-- Shared Layer List -->
    <div class="shared-layerlist">
        <MudPaper Elevation="3">
            <MudTabs Elevation="0" Rounded="true" Centered="true">
                <MudTabPanel Text="Map 1 Layers">
                    <HonuaLayerList
                        SyncWith="map-1"
                        ShowHeader="false"
                        Width="100%"
                        MaxHeight="500px" />
                </MudTabPanel>
                <MudTabPanel Text="Map 2 Layers">
                    <HonuaLayerList
                        SyncWith="map-2"
                        ShowHeader="false"
                        Width="100%"
                        MaxHeight="500px" />
                </MudTabPanel>
                <MudTabPanel Text="Sync Controls">
                    <div class="sync-controls">
                        <MudCheckBox @bind-Checked="_syncVisibility"
                                     Label="Sync Visibility" />
                        <MudCheckBox @bind-Checked="_syncOpacity"
                                     Label="Sync Opacity" />
                        <MudCheckBox @bind-Checked="_syncOrder"
                                     Label="Sync Layer Order" />

                        <MudButton Variant="Variant.Filled"
                                   Color="Color.Primary"
                                   FullWidth="true"
                                   Class="mt-3"
                                   OnClick="SyncMaps">
                            Sync Now
                        </MudButton>
                    </div>
                </MudTabPanel>
            </MudTabs>
        </MudPaper>
    </div>
</div>

@code {
    private bool _syncVisibility = true;
    private bool _syncOpacity = true;
    private bool _syncOrder = false;

    private async Task SyncMaps()
    {
        // Synchronize layer states between maps
        if (_syncVisibility)
        {
            await SyncLayerVisibility();
        }
        if (_syncOpacity)
        {
            await SyncLayerOpacity();
        }
        if (_syncOrder)
        {
            await SyncLayerOrder();
        }
    }

    private Task SyncLayerVisibility() => Task.CompletedTask;
    private Task SyncLayerOpacity() => Task.CompletedTask;
    private Task SyncLayerOrder() => Task.CompletedTask;
}

<style>
    .multi-map-layout {
        display: grid;
        grid-template-columns: 1fr 1fr;
        grid-template-rows: auto auto;
        gap: 20px;
        padding: 20px;
    }

    .map-panel {
        min-height: 400px;
    }

    .shared-layerlist {
        grid-column: 1 / -1;
    }

    .sync-controls {
        padding: 20px;
    }

    @media (max-width: 992px) {
        .multi-map-layout {
            grid-template-columns: 1fr;
        }
    }
</style>
```

### Features Demonstrated

- Multiple map instances
- Separate layer lists per map
- Synchronized controls
- Tab-based interface
- Selective synchronization options

---

## Additional Resources

### Component Documentation
- [HonuaLayerList README](./README.md)
- [HonuaMap Documentation](../Map/README.md)
- [HonuaLegend Documentation](../Legend/README.md)

### Related Examples
- [Map Configuration Examples](../Map/Examples.md)
- [ComponentBus Patterns](../../Core/README.md)

### API Reference
- [LayerInfo Model](../../Models/LayerInfo.cs)
- [LayerGroup Model](../../Models/LayerInfo.cs)
- [JavaScript API](../../wwwroot/js/honua-layerlist.js)

---

## Tips & Tricks

### Performance Optimization

For large layer lists (100+ layers):

```razor
<HonuaLayerList
    SyncWith="map"
    ViewMode="compact"
    ShowSearch="true"
    MaxHeight="500px"
    Collapsible="false" />
```

### Custom Sorting

Implement custom layer sorting:

```csharp
private List<LayerInfo> SortLayers(List<LayerInfo> layers)
{
    return layers
        .OrderBy(l => l.IsBasemap ? 0 : 1)  // Basemaps first
        .ThenBy(l => l.GroupId)              // Then by group
        .ThenByDescending(l => l.Order)      // Then by order
        .ToList();
}
```

### Keyboard Shortcuts

Add keyboard shortcuts for layer control:

```javascript
document.addEventListener('keydown', (e) => {
    if (e.key === 'l' && e.ctrlKey) {
        // Toggle layer list visibility
        e.preventDefault();
    }
});
```

---

## Contributing

Found a bug or have a feature request? Please open an issue on GitHub.

Want to contribute an example? Submit a pull request!
