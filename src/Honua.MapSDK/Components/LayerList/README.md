# HonuaLayerList Component

A comprehensive layer tree/table of contents component for managing map layers in Honua.MapSDK. Provides an intuitive interface for controlling layer visibility, opacity, ordering, and organization.

## Features

### Core Functionality
- **Layer Tree Display** - Hierarchical tree structure with folders/groups
- **Visibility Control** - Toggle layers on/off with checkboxes and eye icons
- **Opacity Control** - Adjust layer transparency with sliders (0-100%)
- **Layer Reordering** - Drag and drop to change render order
- **Search & Filter** - Quickly find layers by name or description
- **Legend Preview** - Visual legend swatches for each layer

### Advanced Features
- **Group Management** - Organize layers into collapsible folders
- **Zoom to Extent** - Jump to layer's geographic bounds
- **Layer Information** - View metadata, feature count, type, etc.
- **Lock/Unlock** - Prevent accidental modification of important layers
- **Compact/Detailed Views** - Toggle between viewing modes
- **Show/Hide All** - Bulk visibility controls
- **ComponentBus Integration** - Sync with map and other components

## Installation

The component is included in Honua.MapSDK. Ensure you have the required dependencies:

```xml
<PackageReference Include="MudBlazor" Version="6.x" />
```

## Basic Usage

### Simple Layer List

```razor
<HonuaLayerList SyncWith="main-map" />
```

### With Custom Configuration

```razor
<HonuaLayerList
    SyncWith="main-map"
    Title="Map Layers"
    Position="top-right"
    ShowOpacitySlider="true"
    AllowReorder="true"
    AllowGrouping="true"
    ViewMode="detailed"
    Width="350px" />
```

### Embedded in Sidebar

```razor
<div class="sidebar">
    <HonuaLayerList
        SyncWith="main-map"
        Position="@null"
        ShowHeader="true"
        MaxHeight="600px" />
</div>
```

## Parameters

### Core Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Id` | string | auto | Unique component identifier |
| `SyncWith` | string? | null | Map ID to synchronize with |
| `Title` | string | "Layers" | Header title text |
| `ShowHeader` | bool | true | Display header with controls |
| `ShowLayerCount` | bool | true | Show total layer count |

### Display Options

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ViewMode` | string | "detailed" | "compact" or "detailed" |
| `Position` | string? | null | Floating position: "top-right", "top-left", "bottom-right", "bottom-left" |
| `Width` | string | "320px" | Component width |
| `MaxHeight` | string? | null | Maximum height |
| `CssClass` | string? | null | Custom CSS class |
| `Style` | string? | null | Inline styles |

### Feature Toggles

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ShowOpacitySlider` | bool | true | Display opacity control |
| `ShowSearch` | bool | true | Enable search functionality |
| `ShowLegend` | bool | true | Show legend swatches |
| `ShowViewToggle` | bool | true | Toggle view mode button |
| `AllowReorder` | bool | true | Enable drag & drop |
| `AllowGrouping` | bool | true | Support layer groups |
| `Collapsible` | bool | true | Allow expand/collapse |

### Event Callbacks

| Parameter | Type | Description |
|-----------|------|-------------|
| `OnLayerVisibilityChanged` | EventCallback\<LayerInfo\> | Fired when visibility changes |
| `OnLayerOpacityChanged` | EventCallback\<LayerInfo\> | Fired when opacity changes |
| `OnLayerReordered` | EventCallback\<List\<LayerInfo\>\> | Fired when layers reorder |
| `OnLayerRemoved` | EventCallback\<LayerInfo\> | Fired when layer is removed |
| `OnLayerSelected` | EventCallback\<LayerInfo\> | Fired when layer is selected |

## LayerInfo Model

The component works with the `LayerInfo` model:

```csharp
public class LayerInfo
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; } // fill, line, circle, symbol, raster, etc.
    public string? SourceId { get; set; }
    public string? SourceLayer { get; set; }
    public bool Visible { get; set; } = true;
    public double Opacity { get; set; } = 1.0;
    public bool IsLocked { get; set; }
    public string? GroupId { get; set; }
    public int Order { get; set; }
    public int? FeatureCount { get; set; }
    public double? MinZoom { get; set; }
    public double? MaxZoom { get; set; }
    public double[]? Extent { get; set; }
    public List<LegendItem> LegendItems { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
    public string? Icon { get; set; }
    public string? Description { get; set; }
    public string? Attribution { get; set; }
    public bool IsBasemap { get; set; }
    public bool CanRemove { get; set; } = true;
    public bool CanRename { get; set; } = true;
}
```

## LayerGroup Model

Organize layers into groups/folders:

```csharp
public class LayerGroup
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? ParentId { get; set; }
    public bool IsExpanded { get; set; } = true;
    public bool Visible { get; set; } = true;
    public double Opacity { get; set; } = 1.0;
    public int Order { get; set; }
    public string? Icon { get; set; }
    public string? Description { get; set; }
    public bool IsLocked { get; set; }
}
```

## ComponentBus Messages

### Subscribed Messages

The component listens for:

- **MapReadyMessage** - Initialize when map loads
- **LayerAddedMessage** - Update list when layer added
- **LayerRemovedMessage** - Remove from list
- **LayerVisibilityChangedMessage** - Sync visibility state
- **LayerOpacityChangedMessage** - Sync opacity state

### Published Messages

The component publishes:

- **LayerVisibilityChangedMessage** - When user toggles visibility
- **LayerOpacityChangedMessage** - When user adjusts opacity
- **LayerRemovedMessage** - When user removes layer
- **LayerSelectedMessage** - When user selects layer
- **LayerReorderedMessage** - When layers are reordered

## JavaScript API

The component uses `honua-layerlist.js` for map interactions:

### Functions

```javascript
// Get all layers from map
getMapLayers(mapId) -> JSON string

// Set layer visibility
setLayerVisibility(mapId, layerId, visible)

// Set layer opacity
setLayerOpacity(mapId, layerId, opacity)

// Move layer in rendering order
moveLayer(mapId, layerId, beforeId)

// Get layer extent
getLayerExtent(mapId, layerId) -> [west, south, east, north]

// Zoom to layer extent
zoomToLayer(mapId, layerId)

// Remove layer from map
removeLayer(mapId, layerId)

// Get feature count
getLayerFeatureCount(mapId, layerId) -> number

// Set layer order
setLayerOrder(mapId, layerIds[])

// Toggle all layers
toggleAllLayers(mapId, visible)

// Get/update metadata
getLayerMetadata(mapId, layerId) -> object
updateLayerMetadata(mapId, layerId, metadata)
```

## Styling

### CSS Variables

Customize appearance with CSS variables:

```css
.honua-layerlist {
    --layer-item-height: 48px;
    --layer-item-compact-height: 40px;
    --group-header-bg: #fafafa;
    --layer-hover-bg: #f5f5f5;
    --layer-selected-bg: #e3f2fd;
    --layer-border-color: #e0e0e0;
}
```

### Custom Classes

The component supports custom CSS classes:

```razor
<HonuaLayerList CssClass="custom-layerlist" />
```

```css
.custom-layerlist .layer-item {
    border-left: 2px solid blue;
}
```

## Layer Types & Icons

The component automatically assigns icons based on layer type:

- **fill** / **fill-extrusion** - Rectangle icon
- **line** - Line icon
- **circle** / **symbol** - Point icon
- **raster** - Image icon
- **heatmap** - Fire icon
- **background** - Wallpaper icon

## View Modes

### Detailed View (Default)

Shows complete layer information:
- Layer name and icon
- Feature count
- Opacity slider when expanded
- Legend preview
- Description and metadata
- Full action menu

### Compact View

Streamlined display:
- Layer name and icon only
- Minimal spacing
- Hidden metadata
- Quick visibility toggle

Toggle between modes programmatically:

```csharp
<HonuaLayerList @ref="layerList" ViewMode="@_currentView" />

@code {
    private string _currentView = "detailed";

    void ToggleView() {
        _currentView = _currentView == "detailed" ? "compact" : "compact";
    }
}
```

## Layer Grouping

Organize layers into hierarchical groups:

```csharp
var groups = new List<LayerGroup>
{
    new() {
        Id = "basemaps",
        Name = "Base Maps",
        Icon = Icons.Material.Filled.Map,
        IsExpanded = true
    },
    new() {
        Id = "overlays",
        Name = "Data Overlays",
        Icon = Icons.Material.Filled.Layers,
        IsExpanded = true
    }
};

// Assign layers to groups
layer.GroupId = "overlays";
```

## Legend Configuration

Configure legend items for each layer:

```csharp
var layer = new LayerInfo
{
    Id = "population",
    Name = "Population Density",
    Type = "fill",
    LegendItems = new List<LegendItem>
    {
        new() {
            Label = "High (>1000)",
            Color = "#d73027",
            SymbolType = "polygon"
        },
        new() {
            Label = "Medium (100-1000)",
            Color = "#fee08b",
            SymbolType = "polygon"
        },
        new() {
            Label = "Low (<100)",
            Color = "#1a9850",
            SymbolType = "polygon"
        }
    }
};
```

## Accessibility

The component follows WCAG 2.1 guidelines:

- **Keyboard Navigation** - Tab through controls, Enter/Space to activate
- **ARIA Labels** - All buttons have descriptive labels
- **Screen Reader Support** - Semantic HTML structure
- **Focus Indicators** - Visible focus states
- **Color Contrast** - Meets AA standards

## Browser Support

- Chrome/Edge 90+
- Firefox 88+
- Safari 14+
- Mobile browsers (iOS Safari, Chrome Mobile)

## Performance

### Optimization Tips

1. **Virtual Scrolling** - For 100+ layers, consider pagination
2. **Lazy Loading** - Load layer metadata on demand
3. **Debounced Search** - 300ms debounce on search input
4. **Memoization** - Cache rendered layer items

### Large Datasets

For maps with many layers:

```razor
<HonuaLayerList
    SyncWith="main-map"
    MaxHeight="400px"
    ShowSearch="true"
    ViewMode="compact" />
```

## Troubleshooting

### Layers Not Appearing

1. Verify `SyncWith` matches map ID
2. Check map is ready (MapReadyMessage received)
3. Ensure layers aren't filtered out (not starting with 'gl-' or 'mapbox-')

### Visibility Toggle Not Working

1. Check layer isn't locked (`IsLocked = false`)
2. Verify JavaScript module loaded
3. Check browser console for errors

### Drag & Drop Not Working

1. Ensure `AllowReorder="true"`
2. Check layers aren't locked
3. Verify browser supports drag events

### Search Not Filtering

1. Check search is enabled (`ShowSearch="true"`)
2. Verify 300ms debounce is working
3. Ensure layer names/descriptions exist

## Best Practices

1. **Use Layer Groups** - Organize related layers together
2. **Set Metadata** - Provide descriptions and attribution
3. **Configure Legends** - Add visual legend items
4. **Lock Important Layers** - Prevent accidental removal
5. **Set Extent** - Enable zoom-to-layer functionality
6. **Handle Events** - Respond to user interactions
7. **Optimize Large Lists** - Use compact view and search
8. **Test Accessibility** - Keyboard and screen reader testing

## Related Components

- **HonuaMap** - Main map component
- **HonuaLegend** - Standalone legend component
- **HonuaBasemapGallery** - Basemap selector
- **HonuaDataGrid** - Feature data table
- **HonuaFilterPanel** - Layer filtering

## Examples

See [Examples.md](./Examples.md) for detailed usage examples.

## License

Part of Honua.MapSDK - see project license.

## Support

For issues and questions:
- GitHub Issues: [honua-mapsdk/issues](https://github.com/honua/mapsdk/issues)
- Documentation: [docs.honua.dev](https://docs.honua.dev)
