# HonuaCompare

Side-by-side map comparison component for comparing different map styles, temporal changes, and data layers. Supports multiple comparison modes including swipe, overlay, flicker, spy glass, and side-by-side views.

## Features

- **5 Comparison Modes**:
  - **Side-by-Side**: Split view with adjustable divider (vertical or horizontal)
  - **Swipe**: Interactive swipe tool with draggable divider
  - **Overlay**: Transparency-based overlay with opacity control
  - **Flicker**: Alternating flicker between views
  - **Spy Glass**: Magnifying circle that reveals the comparison layer

- **Dual Map Management**:
  - Synchronized navigation (pan, zoom, rotate, pitch)
  - Independent navigation option
  - Lock/unlock sync control
  - Automatic extent matching

- **Flexible Configuration**:
  - Compare different basemaps
  - Compare same location at different times
  - Compare different data layers or styles
  - Vertical or horizontal split orientation

- **Rich UI Controls**:
  - Mode switcher (switch between all 5 modes)
  - Draggable divider (for swipe/side-by-side)
  - Opacity slider (for overlay mode)
  - Sync lock/unlock button
  - Fullscreen toggle
  - Screenshot capture

- **ComponentBus Integration**:
  - Publishes `CompareReadyMessage`
  - Publishes `CompareModeChangedMessage`
  - Publishes `CompareViewChangedMessage`
  - Publishes `CompareDividerChangedMessage`

## Installation

The component is part of the Honua.MapSDK package and requires MapLibre GL JS for map rendering.

```razor
@using Honua.MapSDK.Components.Compare
@using Honua.MapSDK.Models
```

## Basic Usage

### Simple Before/After Comparison

```razor
<HonuaCompare LeftMapStyle="@beforeStyle"
              RightMapStyle="@afterStyle"
              LeftLabel="Before Hurricane"
              RightLabel="After Hurricane"
              Center="@(new[] { -80.1918, 25.7617 })"
              Zoom="12" />

@code {
    private string beforeStyle = "https://api.maptiler.com/maps/satellite/style.json?key=YOUR_KEY";
    private string afterStyle = "https://api.maptiler.com/maps/satellite/style.json?key=YOUR_KEY";
}
```

### All Modes with Custom Configuration

```razor
<HonuaCompare LeftMapStyle="@lightStyle"
              RightMapStyle="@darkStyle"
              Mode="CompareMode.Swipe"
              Orientation="CompareOrientation.Vertical"
              SyncNavigation="true"
              InitialPosition="0.5"
              ShowLabels="true"
              AllowModeSwitch="true"
              AllowOrientationSwitch="true"
              LeftLabel="Light Theme"
              RightLabel="Dark Theme"
              Center="@(new[] { -122.4194, 37.7749 })"
              Zoom="13"
              OnModeChanged="HandleModeChanged"
              OnPositionChanged="HandlePositionChanged" />

@code {
    private string lightStyle = "https://demotiles.maplibre.org/style.json";
    private string darkStyle = "https://api.maptiler.com/maps/basic-v2-dark/style.json?key=YOUR_KEY";

    private void HandleModeChanged(CompareMode mode)
    {
        Console.WriteLine($"Comparison mode changed to: {mode}");
    }

    private void HandlePositionChanged(double position)
    {
        Console.WriteLine($"Divider position: {position:P0}");
    }
}
```

### Temporal Comparison with Timestamps

```razor
<HonuaCompare LeftMapStyle="@map2020"
              RightMapStyle="@map2024"
              Mode="CompareMode.Swipe"
              ShowTimestamps="true"
              LeftTimestamp="@beforeTimestamp"
              RightTimestamp="@afterTimestamp"
              Center="@(new[] { -118.2437, 34.0522 })"
              Zoom="10" />

@code {
    private CompareTimestamp beforeTimestamp = new CompareTimestamp
    {
        Time = new DateTime(2020, 1, 1),
        Label = "January 2020",
        Description = "Before development"
    };

    private CompareTimestamp afterTimestamp = new CompareTimestamp
    {
        Time = new DateTime(2024, 1, 1),
        Label = "January 2024",
        Description = "After development"
    };
}
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Id` | string | auto-generated | Unique identifier for the component |
| `LeftMapStyle` | string | **required** | Map style URL for left/before map |
| `RightMapStyle` | string | **required** | Map style URL for right/after map |
| `Mode` | CompareMode | `Swipe` | Comparison mode (SideBySide, Swipe, Overlay, Flicker, SpyGlass) |
| `SyncNavigation` | bool | `true` | Synchronize navigation between maps |
| `InitialPosition` | double | `0.5` | Initial divider position (0-1) |
| `ShowLabels` | bool | `true` | Show map labels (left/right) |
| `AllowModeSwitch` | bool | `true` | Allow users to switch modes |
| `AllowOrientationSwitch` | bool | `true` | Allow orientation switching (vertical/horizontal) |
| `LeftLabel` | string | `"Before"` | Label for left/before map |
| `RightLabel` | string | `"After"` | Label for right/after map |
| `Center` | double[] | `[0, 0]` | Initial center coordinates [lng, lat] |
| `Zoom` | double? | `2` | Initial zoom level |
| `Bearing` | double | `0` | Initial bearing (rotation) |
| `Pitch` | double | `0` | Initial pitch (tilt) |
| `Orientation` | CompareOrientation | `Vertical` | Split orientation (Vertical, Horizontal) |
| `OverlayOpacity` | double | `0.5` | Overlay opacity (0-1) for overlay mode |
| `SpyGlassRadius` | int | `150` | Spy glass radius in pixels |
| `FlickerInterval` | int | `1000` | Flicker interval in milliseconds |
| `AllowScreenshot` | bool | `true` | Allow screenshot capture |
| `AllowFullscreen` | bool | `true` | Allow fullscreen mode |
| `ShowTimestamps` | bool | `false` | Show timestamp information |
| `LeftTimestamp` | CompareTimestamp? | `null` | Left/before timestamp info |
| `RightTimestamp` | CompareTimestamp? | `null` | Right/after timestamp info |
| `Width` | string | `"100%"` | Component width |
| `Height` | string | `"600px"` | Component height |
| `CssClass` | string? | `null` | Additional CSS class |
| `Style` | string? | `null` | Inline styles |

## Events

| Event | Type | Description |
|-------|------|-------------|
| `OnModeChanged` | EventCallback&lt;CompareMode&gt; | Fired when comparison mode changes |
| `OnPositionChanged` | EventCallback&lt;double&gt; | Fired when divider position changes |
| `OnSyncChanged` | EventCallback&lt;bool&gt; | Fired when sync state changes |
| `OnOrientationChanged` | EventCallback&lt;CompareOrientation&gt; | Fired when orientation changes |

## Public Methods

### SetMode(CompareMode mode)

Change the comparison mode.

```csharp
await compareRef.SetMode(CompareMode.Overlay);
```

### SetDividerPosition(double position)

Set the divider position (0-1).

```csharp
await compareRef.SetDividerPosition(0.75); // 75% left, 25% right
```

### SetOrientation(CompareOrientation orientation)

Set the split orientation.

```csharp
await compareRef.SetOrientation(CompareOrientation.Horizontal);
```

### ToggleSync()

Toggle navigation synchronization between maps.

```csharp
await compareRef.ToggleSync();
```

### SetOpacity(double opacity)

Set overlay opacity (0-1) for overlay mode.

```csharp
await compareRef.SetOpacity(0.7);
```

### CaptureScreenshot()

Capture screenshot of both views.

```csharp
string? imageDataUrl = await compareRef.CaptureScreenshot();
if (imageDataUrl != null)
{
    // Use the data URL (can be used in <img> src or downloaded)
}
```

### ToggleFullscreen()

Toggle fullscreen mode.

```csharp
await compareRef.ToggleFullscreen();
```

### FlyTo(double[] center, double zoom, double? bearing = null, double? pitch = null)

Fly to a specific location on both maps.

```csharp
await compareRef.FlyTo(new[] { -122.4194, 37.7749 }, 13);
```

### UpdateLeftStyle(string styleUrl)

Update the left map style.

```csharp
await compareRef.UpdateLeftStyle("https://new-style-url.json");
```

### UpdateRightStyle(string styleUrl)

Update the right map style.

```csharp
await compareRef.UpdateRightStyle("https://new-style-url.json");
```

## Comparison Modes

### Side-by-Side

Displays maps side-by-side with a static divider. Good for detailed comparison when you need to see both maps clearly.

- Supports vertical (left/right) and horizontal (top/bottom) orientations
- Divider is not draggable
- Each map resizes independently

### Swipe

Interactive swipe tool with a draggable divider. Best for comparing aligned imagery or data.

- Drag the divider left/right or up/down
- Smooth clipping of map views
- Touch-friendly on mobile devices

### Overlay

One map overlays the other with adjustable opacity. Excellent for comparing similar features or detecting changes.

- Adjust opacity with slider (0-100%)
- See both maps simultaneously
- Useful for subtle change detection

### Flicker

Rapidly alternates between the two views. Effective for detecting changes in similar scenes.

- Configurable flicker interval (default 1000ms)
- Automatic alternation between maps
- Useful for change detection

### Spy Glass

Magnifying circle reveals the comparison map. Ideal for exploring localized differences.

- Follows mouse/touch movement
- Configurable radius (default 150px)
- Interactive exploration

## Use Cases

### 1. Temporal Analysis

Compare the same location at different times:
- Urban development tracking
- Natural disaster impact assessment
- Seasonal changes
- Historical comparisons

### 2. Basemap Comparison

Compare different basemap styles:
- Light vs. dark themes
- Satellite vs. street maps
- Different providers
- Custom styling variations

### 3. Data Visualization

Compare different data layers:
- Before/after simulations
- Model predictions vs. actual data
- Different analysis results
- Multi-scenario planning

### 4. Quality Assessment

Verify data quality and accuracy:
- New data vs. reference data
- Different data sources
- Georeferencing validation
- Change detection

## ComponentBus Messages

### Published Messages

**CompareReadyMessage**
```csharp
public class CompareReadyMessage
{
    public string CompareId { get; init; }
    public CompareMode Mode { get; init; }
    public string LeftStyle { get; init; }
    public string RightStyle { get; init; }
}
```

**CompareModeChangedMessage**
```csharp
public class CompareModeChangedMessage
{
    public string CompareId { get; init; }
    public CompareMode Mode { get; init; }
}
```

**CompareViewChangedMessage**
```csharp
public class CompareViewChangedMessage
{
    public string CompareId { get; init; }
    public double[] Center { get; init; }
    public double Zoom { get; init; }
    public double Bearing { get; init; }
    public double Pitch { get; init; }
}
```

**CompareDividerChangedMessage**
```csharp
public class CompareDividerChangedMessage
{
    public string CompareId { get; init; }
    public double Position { get; init; } // 0-1
}
```

## Styling

The component supports custom styling through CSS classes and inline styles:

```razor
<HonuaCompare LeftMapStyle="@style1"
              RightMapStyle="@style2"
              CssClass="my-custom-compare"
              Style="border: 2px solid #ccc; border-radius: 8px;" />
```

### Dark Mode

The component automatically adapts to dark mode based on the `prefers-color-scheme` media query.

## Responsive Design

The component is fully responsive and adapts to different screen sizes:

- Mobile-optimized controls
- Touch-friendly interactions
- Adaptive layouts for small screens
- Automatic control repositioning

## Keyboard Shortcuts

No built-in keyboard shortcuts, but you can implement custom shortcuts using standard Blazor event handling.

## Best Practices

1. **Choose the Right Mode**:
   - Use **Swipe** for aligned imagery comparison
   - Use **Overlay** for subtle change detection
   - Use **Flicker** for rapid change identification
   - Use **Side-by-Side** for detailed analysis
   - Use **Spy Glass** for localized exploration

2. **Performance**:
   - Both maps are fully rendered, which can impact performance
   - Consider using lower-resolution tiles for better performance
   - Disable sync navigation when comparing different areas

3. **User Experience**:
   - Provide clear labels indicating what's being compared
   - Use timestamps for temporal comparisons
   - Allow users to switch modes for different perspectives
   - Consider fullscreen mode for detailed analysis

4. **Data Alignment**:
   - Ensure both map styles use the same projection
   - Verify coordinate systems match
   - Use sync navigation for aligned comparisons

## Examples

See [Examples.md](./Examples.md) for comprehensive examples including:
- Before/after natural disaster assessment
- Urban development tracking
- Basemap style comparison
- Data validation workflows
- Multi-scenario analysis

## Browser Support

- Chrome/Edge 90+
- Firefox 88+
- Safari 14+
- Mobile browsers (iOS Safari, Chrome Mobile)

## Dependencies

- MapLibre GL JS 4.7.1+
- MudBlazor (for UI components)
- Honua.MapSDK.Core (ComponentBus)

## License

Part of the Honua.MapSDK package. See main package license for details.
