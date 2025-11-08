# HonuaLegend Component

The `HonuaLegend` component provides an interactive layer list for Honua.MapSDK that displays map layers and allows users to control visibility and opacity.

## Features

- **Layer Management**: Automatically tracks layers added/removed from the map
- **Visibility Control**: Toggle layer visibility with checkboxes
- **Opacity Control**: Adjust layer opacity with sliders (0-100%)
- **Layer Grouping**: Organize layers into collapsible groups (basemaps, overlays, etc.)
- **Symbol Display**: Show legend symbols and layer type icons
- **Responsive Design**: Works in floating panels or embedded sidebars
- **ComponentBus Integration**: Fully integrated with the MapSDK event system
- **Dark Mode Support**: Automatic dark mode styling

## Basic Usage

### Auto-sync with Map

The simplest usage is to sync the legend with a map by providing the map's ID:

```razor
<HonuaMap Id="map1" />
<HonuaLegend SyncWith="map1" />
```

### Floating Legend

Position the legend as a floating panel on the map:

```razor
<div style="position: relative; width: 100%; height: 600px;">
    <HonuaMap Id="map1" />
    <HonuaLegend
        SyncWith="map1"
        Title="Map Layers"
        Position="top-right" />
</div>
```

### Embedded in Sidebar

Use the legend in a sidebar or container:

```razor
<div style="display: flex; height: 600px;">
    <div style="width: 300px; padding: 16px;">
        <HonuaLegend
            SyncWith="map1"
            Title="Layers"
            Position="@null" />
    </div>
    <div style="flex: 1;">
        <HonuaMap Id="map1" />
    </div>
</div>
```

## Configuration Options

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Id` | `string` | Auto-generated | Unique identifier for the legend |
| `SyncWith` | `string?` | `null` | Map ID to synchronize with (filters layer events) |
| `Title` | `string` | "Map Layers" | Title displayed in legend header |
| `Position` | `string?` | `null` | Floating position: "top-right", "top-left", "bottom-right", "bottom-left", or null for embedded |
| `Collapsible` | `bool` | `true` | Whether the legend can be collapsed |
| `InitiallyCollapsed` | `bool` | `false` | Initial collapsed state |
| `ShowOpacity` | `bool` | `true` | Show opacity sliders for each layer |
| `ShowReorder` | `bool` | `false` | Show drag handles for reordering (future feature) |
| `ShowGroups` | `bool` | `true` | Group layers by type (basemap, overlay, etc.) |
| `ShowTypeIcons` | `bool` | `true` | Show layer type icons |
| `ShowSymbols` | `bool` | `true` | Show legend symbols/swatches |
| `CssClass` | `string?` | `null` | Custom CSS class |
| `Style` | `string?` | `null` | Custom inline styles |
| `MaxHeight` | `string` | "400px" | Maximum height of legend content |

### Examples

#### Minimal Configuration

```razor
<HonuaLegend SyncWith="map1" />
```

#### Full Configuration

```razor
<HonuaLegend
    Id="legend1"
    SyncWith="map1"
    Title="Map Layers"
    Position="top-right"
    Collapsible="true"
    InitiallyCollapsed="false"
    ShowOpacity="true"
    ShowReorder="false"
    ShowGroups="true"
    ShowTypeIcons="true"
    ShowSymbols="true"
    MaxHeight="500px" />
```

#### Simple Legend Without Groups

```razor
<HonuaLegend
    SyncWith="map1"
    ShowGroups="false"
    ShowOpacity="false" />
```

#### Compact Legend

```razor
<HonuaLegend
    SyncWith="map1"
    Title="Layers"
    ShowSymbols="false"
    ShowOpacity="false"
    MaxHeight="200px" />
```

## ComponentBus Integration

The HonuaLegend component integrates with the ComponentBus for loose coupling with other components.

### Messages Subscribed To

- **`LayerAddedMessage`**: Adds a new layer to the legend when published
- **`LayerRemovedMessage`**: Removes a layer from the legend when published
- **`LayerVisibilityChangedMessage`**: Updates layer visibility in the UI when changed externally
- **`LayerOpacityChangedMessage`**: Updates layer opacity slider when changed externally

### Messages Published

- **`LayerVisibilityChangedMessage`**: Published when user toggles layer visibility
- **`LayerOpacityChangedMessage`**: Published when user adjusts layer opacity

### Example: Programmatically Add a Layer

```razor
@inject ComponentBus Bus

<button @onclick="AddLayer">Add Layer</button>
<HonuaLegend SyncWith="map1" />

@code {
    private async Task AddLayer()
    {
        await Bus.PublishAsync(new LayerAddedMessage
        {
            LayerId = "parcels-layer",
            LayerName = "Parcels"
        }, "MyComponent");
    }
}
```

### Example: Listen for Visibility Changes

```razor
@inject ComponentBus Bus

<HonuaLegend SyncWith="map1" />

@code {
    protected override void OnInitialized()
    {
        Bus.Subscribe<LayerVisibilityChangedMessage>(args =>
        {
            Console.WriteLine($"Layer {args.Message.LayerId} visibility: {args.Message.Visible}");
        });
    }
}
```

## Layer Grouping

Layers are automatically grouped based on their name:

- **Basemap**: Names containing "basemap", "base", or "background"
- **Overlay**: Names containing "overlay", "vector", or "feature"
- **Default**: All other layers

You can customize grouping by publishing `LayerAddedMessage` with specific naming conventions.

## Styling

### Custom CSS Class

```razor
<HonuaLegend
    SyncWith="map1"
    CssClass="my-custom-legend" />

<style>
    .my-custom-legend {
        border: 2px solid #4A90E2;
    }
</style>
```

### Custom Inline Styles

```razor
<HonuaLegend
    SyncWith="map1"
    Style="min-width: 350px; max-width: 500px;" />
```

### Position Classes

The component automatically applies position classes based on the `Position` parameter:

- `legend-floating legend-top-right`
- `legend-floating legend-top-left`
- `legend-floating legend-bottom-right`
- `legend-floating legend-bottom-left`
- `legend-embedded` (when Position is null)

## Responsive Design

The legend automatically adjusts for mobile devices:

- Smaller min/max widths on screens < 768px
- Reduced max height on mobile
- Touch-friendly controls

## Dark Mode

The legend automatically adapts to system dark mode preferences using CSS media queries:

```css
@media (prefers-color-scheme: dark) {
    /* Dark mode styles applied automatically */
}
```

## Complete Example

Here's a complete example showing the legend with a map and layer management:

```razor
@page "/map-with-legend"
@inject ComponentBus Bus

<div style="position: relative; width: 100%; height: 600px;">
    <HonuaMap
        Id="mainMap"
        MapStyle="https://demotiles.maplibre.org/style.json"
        Center="@(new[] { -122.4, 37.8 })"
        Zoom="10"
        OnMapReady="OnMapReady" />

    <HonuaLegend
        SyncWith="mainMap"
        Title="San Francisco Layers"
        Position="top-right"
        Collapsible="true"
        ShowOpacity="true"
        ShowGroups="true" />
</div>

<div style="margin-top: 20px;">
    <MudButton Variant="Variant.Filled" Color="Color.Primary" OnClick="AddParcelLayer">
        Add Parcels
    </MudButton>
    <MudButton Variant="Variant.Filled" Color="Color.Secondary" OnClick="AddStreetsLayer">
        Add Streets
    </MudButton>
</div>

@code {
    private async Task OnMapReady(MapReadyMessage message)
    {
        Console.WriteLine($"Map {message.MapId} is ready!");
    }

    private async Task AddParcelLayer()
    {
        await Bus.PublishAsync(new LayerAddedMessage
        {
            LayerId = "parcels",
            LayerName = "Parcels - Vector Overlay"
        }, "DemoPage");
    }

    private async Task AddStreetsLayer()
    {
        await Bus.PublishAsync(new LayerAddedMessage
        {
            LayerId = "streets",
            LayerName = "Streets - Line Overlay"
        }, "DemoPage");
    }
}
```

## Browser Support

The HonuaLegend component supports all modern browsers:

- Chrome/Edge (latest)
- Firefox (latest)
- Safari (latest)
- Mobile browsers (iOS Safari, Chrome Mobile)

## Accessibility

The component includes accessibility features:

- Proper ARIA labels
- Keyboard navigation support
- Screen reader friendly
- High contrast mode support

## Future Enhancements

Planned features for future releases:

- **Drag-and-drop reordering**: Reorder layers by dragging
- **Layer search/filter**: Search layers by name
- **Export legend**: Export legend as image
- **Custom symbols**: Define custom legend symbols
- **Layer info**: Show metadata and attribution
- **Nested groups**: Support for nested layer groups
- **Mini map**: Optional mini overview map

## Related Components

- **HonuaMap**: Main map component
- **HonuaFilter**: Filter component for spatial/attribute filtering
- **HonuaTimeline**: Timeline component for temporal data

## API Reference

For detailed API documentation, see the XML documentation comments in the source code.
