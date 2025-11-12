# HonuaFullscreen Component

A Blazor component for toggling fullscreen mode in Honua.MapSDK applications. Provides a simple button control to enter and exit fullscreen mode with keyboard shortcuts support.

## Features

- **Toggle Fullscreen**: Single button to enter/exit fullscreen mode
- **Keyboard Shortcuts**: F11 to toggle fullscreen, Esc to exit
- **Flexible Positioning**: Float over map or embed in toolbar
- **Cross-browser Support**: Works with vendor-prefixed APIs
- **Map Auto-resize**: Automatically resizes map when entering/exiting fullscreen
- **ComponentBus Integration**: Publishes `FullscreenChangedMessage` for coordination
- **Customizable UI**: Configurable button style, size, color, and icons
- **Accessibility**: Proper ARIA labels and keyboard support

## Basic Usage

```razor
@using Honua.MapSDK.Components.Fullscreen

<!-- Simple fullscreen button in top-right corner -->
<HonuaFullscreen SyncWith="my-map" />
```

## Parameters

### Core Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Id` | `string` | auto-generated | Unique identifier for the component |
| `SyncWith` | `string?` | `null` | Map ID to synchronize with |
| `Position` | `string?` | `"top-right"` | Position: `top-right`, `top-left`, `bottom-right`, `bottom-left`, or `null` |
| `TargetElementId` | `string?` | `null` | Element ID to make fullscreen (defaults to map container) |

### UI Customization

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ButtonColor` | `Color` | `Color.Default` | MudBlazor button color |
| `ButtonVariant` | `Variant` | `Variant.Filled` | MudBlazor button variant |
| `ButtonSize` | `Size` | `Size.Medium` | Button size |
| `EnterIcon` | `string` | `Icons.Material.Filled.Fullscreen` | Icon when not in fullscreen |
| `ExitIcon` | `string` | `Icons.Material.Filled.FullscreenExit` | Icon when in fullscreen |
| `CssClass` | `string?` | `null` | Custom CSS class |
| `Style` | `string?` | `null` | Custom inline styles |

### Behavior

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `AlwaysShow` | `bool` | `false` | Show even if map is not ready |
| `Disabled` | `bool` | `false` | Disable the button |
| `EnableKeyboardShortcuts` | `bool` | `true` | Enable F11/Esc shortcuts |

### Events

| Event | Type | Description |
|-------|------|-------------|
| `OnFullscreenChanged` | `EventCallback<bool>` | Fired when fullscreen state changes |

## Examples

### Floating Button (Top-Right)

```razor
<HonuaFullscreen
    SyncWith="my-map"
    Position="top-right"
    ButtonColor="Color.Primary" />
```

### Embedded in Toolbar

```razor
<MudToolBar>
    <MudText>Map Controls</MudText>
    <MudSpacer />
    <HonuaFullscreen
        SyncWith="my-map"
        Position="@null"
        ButtonVariant="Variant.Text" />
</MudToolBar>
```

### Custom Styling

```razor
<HonuaFullscreen
    SyncWith="my-map"
    Position="bottom-right"
    ButtonColor="Color.Secondary"
    ButtonSize="Size.Large"
    ButtonVariant="Variant.Outlined"
    EnterIcon="@Icons.Material.Filled.AspectRatio"
    ExitIcon="@Icons.Material.Filled.CloseFullscreen" />
```

### With Event Handler

```razor
<HonuaFullscreen
    SyncWith="my-map"
    OnFullscreenChanged="HandleFullscreenChange" />

@code {
    private void HandleFullscreenChange(bool isFullscreen)
    {
        Console.WriteLine($"Fullscreen: {isFullscreen}");
        // Update UI, pause animations, etc.
    }
}
```

### Programmatic Control

```razor
<HonuaFullscreen @ref="_fullscreenControl" SyncWith="my-map" />

<MudButton OnClick="EnterFullscreenProgrammatically">
    Go Fullscreen
</MudButton>

@code {
    private HonuaFullscreen? _fullscreenControl;

    private async Task EnterFullscreenProgrammatically()
    {
        if (_fullscreenControl != null)
        {
            await _fullscreenControl.EnterFullscreen();
        }
    }
}
```

### Fullscreen Specific Element

```razor
<!-- Make only the map container fullscreen, not entire page -->
<HonuaFullscreen
    SyncWith="my-map"
    TargetElementId="my-map" />

<div id="my-map">
    <HonuaMapLibre Id="my-map" />
</div>
```

### Disable Keyboard Shortcuts

```razor
<HonuaFullscreen
    SyncWith="my-map"
    EnableKeyboardShortcuts="false" />
```

## ComponentBus Messages

### Published Messages

#### FullscreenChangedMessage
Published when fullscreen state changes (user clicks button, presses F11/Esc, or programmatic toggle).

```csharp
public class FullscreenChangedMessage
{
    public required string ComponentId { get; init; }
    public required string MapId { get; init; }
    public required bool IsFullscreen { get; init; }
    public required string TargetElementId { get; init; }
}
```

### Subscribing to Messages

```razor
@inject ComponentBus Bus

@code {
    protected override void OnInitialized()
    {
        Bus.Subscribe<FullscreenChangedMessage>(args =>
        {
            var msg = args.Message;
            Console.WriteLine($"Map {msg.MapId} fullscreen: {msg.IsFullscreen}");

            // Update other components, hide/show UI, etc.
            InvokeAsync(StateHasChanged);
        });
    }
}
```

## Keyboard Shortcuts

- **F11**: Toggle fullscreen mode (enter if not fullscreen, exit if fullscreen)
- **Escape**: Exit fullscreen mode (browser default)

Keyboard shortcuts can be disabled by setting `EnableKeyboardShortcuts="false"`.

## Browser Support

The component automatically detects and uses the appropriate fullscreen API:
- **Standard**: `requestFullscreen()`, `exitFullscreen()`
- **WebKit**: `webkitRequestFullscreen()`, `webkitExitFullscreen()`
- **Mozilla**: `mozRequestFullScreen()`, `mozCancelFullScreen()`
- **IE/Edge**: `msRequestFullscreen()`, `msExitFullscreen()`

The button is automatically disabled if fullscreen is not supported.

## Styling

### CSS Classes

- `.honua-fullscreen` - Main container
- `.fullscreen-floating` - Floating position styles
- `.fullscreen-top-right`, `.fullscreen-top-left`, etc. - Position variants
- `.fullscreen-embedded` - Embedded (inline) styles
- `.fullscreen-button` - Button element

### Custom Styling Example

```css
/* Override floating position */
.honua-fullscreen.fullscreen-top-right {
    top: 20px;
    right: 20px;
}

/* Custom button hover effect */
.fullscreen-button:hover {
    transform: scale(1.1) rotate(15deg);
}
```

## Accessibility

- **ARIA Labels**: Proper `aria-label` attributes for screen readers
- **Keyboard Support**: Full keyboard navigation
- **Focus Management**: Button receives focus appropriately
- **State Indication**: Icon changes to indicate current state

## Notes

- The component automatically resizes the map when entering/exiting fullscreen
- Works with both `HonuaMapLibre` and `HonuaLeaflet` components
- Fullscreen state is maintained even if component is re-rendered
- The `TargetElementId` parameter allows making any element fullscreen, not just the map

## Common Issues

### Button Not Showing
- Ensure map is initialized (use `AlwaysShow="true"` to bypass check)
- Check browser console for JavaScript errors
- Verify `SyncWith` matches your map's `Id`

### Fullscreen Not Working
- Check browser console for API support
- Some browsers require user interaction before fullscreen
- Ensure target element exists in DOM

### Map Not Resizing
- The component automatically calls `map.resize()` (MapLibre) or `map.invalidateSize()` (Leaflet)
- If using custom map, ensure it has a resize method

## Related Components

- **HonuaMapLibre**: Main map component
- **HonuaLeaflet**: Alternative map component
- **HonuaCoordinateDisplay**: Coordinate tracking
- **HonuaBookmarks**: Save/restore map views

## API Reference

### Methods

```csharp
// Toggle fullscreen mode
public async Task ToggleFullscreen()

// Enter fullscreen mode
public async Task EnterFullscreen()

// Exit fullscreen mode
public async Task ExitFullscreen()
```

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0
