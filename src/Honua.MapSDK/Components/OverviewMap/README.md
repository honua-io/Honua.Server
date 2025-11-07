# HonuaOverviewMap Component

A mini overview map component that displays the context of the current main map view, showing where you are in relation to the larger geographic area.

## Features

### Core Functionality
- **Real-time Synchronization**: Automatically syncs with main map movements
- **Extent Indicator**: Visual box showing current viewport on main map
- **Interactive Navigation**: Click or drag to pan the main map
- **Collapsible**: Toggle visibility with smooth animations
- **Customizable Position**: Place in any corner or custom location
- **Bearing Rotation**: Optional rotation to match main map bearing

### Interaction Modes
1. **View Only**: Display extent box without interaction
2. **Click to Pan**: Click overview map to center main map
3. **Drag to Pan**: Drag extent box to pan main map
4. **Scroll to Zoom**: Scroll over overview to zoom main map

### Styling Options
- Custom extent box colors and opacity
- Configurable size and position
- Border, shadow, and corner radius
- Dark mode support
- Responsive design for mobile

## Basic Usage

```razor
@page "/map"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.OverviewMap

<HonuaMap Id="map1"
          Center="new[] { -122.4, 37.8 }"
          Zoom="12" />

<HonuaOverviewMap SyncWith="map1" />
```

## Parameters

### Required

| Parameter | Type | Description |
|-----------|------|-------------|
| `SyncWith` | `string` | ID of the main map to synchronize with |

### Size and Position

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Width` | `int` | `200` | Width in pixels |
| `Height` | `int` | `200` | Height in pixels |
| `Position` | `string` | `"bottom-right"` | Position: `top-left`, `top-right`, `bottom-left`, `bottom-right`, `custom` |
| `OffsetX` | `int` | `10` | Horizontal offset in pixels (for custom position) |
| `OffsetY` | `int` | `10` | Vertical offset in pixels (for custom position) |

### Zoom and View

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ZoomOffset` | `int` | `-5` | Zoom offset from main map (negative = zoomed out) |
| `MinZoom` | `double?` | `null` | Minimum zoom level |
| `MaxZoom` | `double?` | `null` | Maximum zoom level |
| `OverviewBasemap` | `string?` | `null` | Custom basemap (null = use same as main map) |

### Extent Box Styling

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ExtentBoxColor` | `string` | `"#FF4444"` | Outline color |
| `ExtentBoxWidth` | `int` | `2` | Outline width in pixels |
| `ExtentBoxOpacity` | `double` | `0.8` | Outline opacity (0-1) |
| `ExtentBoxFillColor` | `string` | `"#FF4444"` | Fill color |
| `ExtentBoxFillOpacity` | `double` | `0.1` | Fill opacity (0-1) |

### Interaction

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ClickToPan` | `bool` | `true` | Click overview to pan main map |
| `DragToPan` | `bool` | `true` | Drag extent box to pan main map |
| `ScrollToZoom` | `bool` | `false` | Scroll over overview to zoom main map |
| `RotateWithBearing` | `bool` | `false` | Rotate overview with main map bearing |

### UI Controls

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Collapsible` | `bool` | `true` | Can be collapsed/expanded |
| `InitiallyCollapsed` | `bool` | `false` | Start in collapsed state |
| `ShowToggleButton` | `bool` | `true` | Show expand/collapse button |
| `ShowControls` | `bool` | `false` | Show navigation controls on overview |
| `Title` | `string?` | `null` | Optional title text |

### Appearance

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `BorderRadius` | `int` | `4` | Corner radius in pixels |
| `BoxShadow` | `string` | `"0 2px 8px rgba(0,0,0,0.3)"` | CSS box shadow |
| `BorderColor` | `string` | `"#ccc"` | Border color |
| `BorderWidth` | `int` | `1` | Border width in pixels |
| `BackgroundColor` | `string` | `"#fff"` | Background color |
| `ZIndex` | `int` | `1000` | CSS z-index |

### Responsive

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `HideOnMobile` | `bool` | `false` | Hide on mobile devices |
| `MobileBreakpoint` | `int` | `768` | Mobile breakpoint in pixels |

### Performance

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `UpdateThrottleMs` | `int` | `100` | Throttle extent updates (milliseconds) |

### Custom Styling

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `CssClass` | `string?` | `null` | Additional CSS classes |
| `Style` | `string?` | `null` | Inline CSS styles |

## Events

### OnOverviewClicked

Fires when the overview map is clicked.

```razor
<HonuaOverviewMap SyncWith="map1"
                  OnOverviewClicked="HandleOverviewClick" />

@code {
    private void HandleOverviewClick(OverviewMapClickedMessage message)
    {
        Console.WriteLine($"Overview clicked at: {message.Center[0]}, {message.Center[1]}");
    }
}
```

## Public Methods

### ExpandAsync()

Programmatically expand the overview map.

```csharp
@ref HonuaOverviewMap _overview;

await _overview.ExpandAsync();
```

### CollapseAsync()

Programmatically collapse the overview map.

```csharp
await _overview.CollapseAsync();
```

### UpdateExtentStyleAsync()

Update extent box styling dynamically.

```csharp
await _overview.UpdateExtentStyleAsync(
    color: "#0000FF",
    width: 3,
    opacity: 1.0,
    fillColor: "#0000FF",
    fillOpacity: 0.2
);
```

## Component Bus Integration

The component subscribes to and publishes messages via the ComponentBus:

### Subscribes To:
- `MapReadyMessage` - Initializes when main map is ready
- `MapExtentChangedMessage` - Updates extent box when main map moves
- `BasemapChangedMessage` - Updates basemap when main map changes style

### Publishes:
- `FlyToRequestMessage` - Requests main map to pan/zoom
- `OverviewMapClickedMessage` - When user clicks overview map

## Examples

### Basic Overview Map

```razor
<HonuaMap Id="mainMap" />
<HonuaOverviewMap SyncWith="mainMap" />
```

### Customized Position and Size

```razor
<HonuaOverviewMap SyncWith="mainMap"
                  Width="250"
                  Height="200"
                  Position="bottom-left"
                  ZoomOffset="-6" />
```

### Custom Extent Box Styling

```razor
<HonuaOverviewMap SyncWith="mainMap"
                  ExtentBoxColor="#00FF00"
                  ExtentBoxWidth="3"
                  ExtentBoxOpacity="1.0"
                  ExtentBoxFillColor="#00FF00"
                  ExtentBoxFillOpacity="0.2" />
```

### Different Basemap

```razor
<HonuaOverviewMap SyncWith="mainMap"
                  OverviewBasemap="https://tiles.stadiamaps.com/styles/alidade_smooth_dark.json" />
```

### View-Only Mode

```razor
<HonuaOverviewMap SyncWith="mainMap"
                  ClickToPan="false"
                  DragToPan="false"
                  ScrollToZoom="false" />
```

### With Rotation

```razor
<HonuaOverviewMap SyncWith="mainMap"
                  RotateWithBearing="true" />
```

### Custom Positioning

```razor
<HonuaOverviewMap SyncWith="mainMap"
                  Position="custom"
                  OffsetX="20"
                  OffsetY="80" />
```

### Initially Collapsed

```razor
<HonuaOverviewMap SyncWith="mainMap"
                  InitiallyCollapsed="true"
                  Collapsible="true" />
```

### Mobile-Friendly

```razor
<HonuaOverviewMap SyncWith="mainMap"
                  HideOnMobile="true"
                  MobileBreakpoint="768" />
```

### With Title

```razor
<HonuaOverviewMap SyncWith="mainMap"
                  Title="Overview"
                  Width="200"
                  Height="150" />
```

### Dark Theme

```razor
<HonuaOverviewMap SyncWith="mainMap"
                  BackgroundColor="#1a1a1a"
                  BorderColor="#444"
                  ExtentBoxColor="#FFD700"
                  ExtentBoxFillColor="#FFD700" />
```

## Performance Tips

1. **Use Throttling**: The `UpdateThrottleMs` parameter controls how often the extent box updates. Default (100ms) is good for most cases.

2. **Simplified Basemap**: Use a simpler basemap for the overview to improve rendering performance:
   ```razor
   <HonuaOverviewMap SyncWith="mainMap"
                     OverviewBasemap="https://demotiles.maplibre.org/style.json" />
   ```

3. **Hide on Mobile**: For mobile performance, hide the overview:
   ```razor
   <HonuaOverviewMap SyncWith="mainMap"
                     HideOnMobile="true" />
   ```

4. **Start Collapsed**: Initialize collapsed and let users expand on demand:
   ```razor
   <HonuaOverviewMap SyncWith="mainMap"
                     InitiallyCollapsed="true" />
   ```

## Accessibility

The component includes:
- ARIA labels on toggle button
- Keyboard navigation (Tab to focus, Enter to toggle)
- Focus indicators
- High contrast mode support
- Screen reader friendly

### Keyboard Shortcuts
- `Tab` - Focus toggle button
- `Enter/Space` - Toggle expand/collapse
- `Escape` - Collapse (when expanded)

## Browser Support

- Chrome/Edge (latest)
- Firefox (latest)
- Safari (latest)
- Mobile browsers (iOS Safari, Chrome Mobile)

Requires WebGL support for MapLibre GL JS.

## Troubleshooting

### Overview map not showing
- Ensure `SyncWith` matches the main map's `Id`
- Check that main map is initialized before overview
- Verify MapLibre GL JS is loaded

### Extent box not updating
- Check ComponentBus is properly injected
- Verify main map is publishing `MapExtentChangedMessage`
- Check browser console for JavaScript errors

### Performance issues
- Increase `UpdateThrottleMs` (e.g., to 200ms)
- Use a simpler basemap for overview
- Reduce overview map size

### Click/drag not working
- Ensure `ClickToPan` or `DragToPan` is `true`
- Check JavaScript console for errors
- Verify MapLibre GL JS events are firing

## Advanced Usage

### Multiple Overview Maps

You can have multiple overview maps for the same main map:

```razor
<HonuaMap Id="mainMap" />

<!-- Wide area overview -->
<HonuaOverviewMap SyncWith="mainMap"
                  Position="bottom-right"
                  ZoomOffset="-8" />

<!-- Nearby overview -->
<HonuaOverviewMap SyncWith="mainMap"
                  Position="bottom-left"
                  ZoomOffset="-3" />
```

### Programmatic Control

```razor
<HonuaOverviewMap @ref="_overview" SyncWith="mainMap" />

<button @onclick="ToggleOverview">Toggle</button>
<button @onclick="ChangeStyle">Change Style</button>

@code {
    private HonuaOverviewMap _overview;

    private async Task ToggleOverview()
    {
        if (_isExpanded)
            await _overview.CollapseAsync();
        else
            await _overview.ExpandAsync();

        _isExpanded = !_isExpanded;
    }

    private async Task ChangeStyle()
    {
        await _overview.UpdateExtentStyleAsync(
            color: "#0000FF",
            width: 4,
            opacity: 1.0
        );
    }

    private bool _isExpanded = true;
}
```

## See Also

- [HonuaMap](../Map/README.md) - Main map component
- [HonuaLegend](../Legend/README.md) - Map legend component
- [ComponentBus](../../Core/README.md) - Component messaging system
- [MapLibre GL JS](https://maplibre.org/) - Underlying map library
