# HonuaFullscreen Quick Start Guide

## Installation

The component is ready to use! No additional installation needed.

## Basic Usage

### 1. Add the Component to Your Page

```razor
@page "/map-view"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Fullscreen

<div style="position: relative; width: 100%; height: 600px;">
    <HonuaMapLibre
        Id="my-map"
        Style="https://demotiles.maplibre.org/style.json"
        Center="@(new[] { -122.4194, 37.7749 })"
        Zoom="12" />

    <HonuaFullscreen SyncWith="my-map" />
</div>
```

That's it! You now have a fullscreen button in the top-right corner.

## Common Configurations

### Change Position

```razor
<!-- Top-right (default) -->
<HonuaFullscreen SyncWith="my-map" Position="top-right" />

<!-- Bottom-left -->
<HonuaFullscreen SyncWith="my-map" Position="bottom-left" />

<!-- Embedded (no floating) -->
<HonuaFullscreen SyncWith="my-map" Position="@null" />
```

### Customize Appearance

```razor
<HonuaFullscreen
    SyncWith="my-map"
    ButtonColor="Color.Primary"
    ButtonSize="Size.Large"
    ButtonVariant="Variant.Outlined" />
```

### Handle State Changes

```razor
<HonuaFullscreen
    SyncWith="my-map"
    OnFullscreenChanged="@((isFullscreen) => Console.WriteLine($"Fullscreen: {isFullscreen}"))" />
```

## Keyboard Shortcuts

- **F11** - Toggle fullscreen
- **Esc** - Exit fullscreen

To disable: `EnableKeyboardShortcuts="false"`

## ComponentBus Integration

```razor
@inject ComponentBus Bus

@code {
    protected override void OnInitialized()
    {
        Bus.Subscribe<FullscreenChangedMessage>(args =>
        {
            Console.WriteLine($"Map {args.Message.MapId} is now fullscreen: {args.Message.IsFullscreen}");
        });
    }
}
```

## Troubleshooting

**Button not showing?**
- Ensure `SyncWith` matches your map's `Id`
- Try `AlwaysShow="true"` to bypass map ready check

**Fullscreen not working?**
- Check browser console for errors
- Ensure your browser supports fullscreen API

**Map not resizing?**
- The component auto-resizes MapLibre/Leaflet maps
- For custom maps, implement a resize method

## Next Steps

- See [README.md](./README.md) for complete API documentation
- Check [Examples.md](./Examples.md) for more advanced examples
- Read about [ComponentBus architecture](../../Core/README.md)

## Support

For issues or questions:
1. Check the README and Examples
2. Review browser console for errors
3. Verify browser compatibility
4. Check that map container has proper dimensions
