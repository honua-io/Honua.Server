# Style Editor Component

A comprehensive visual style editor for Honua map layers, inspired by best practices from Mapbox Studio, Esri VTSE, Felt, and CARTO Builder.

## Overview

The Style Editor provides an intuitive interface for styling map layers with real-time preview, undo/redo support, and comprehensive property controls for all layer types.

## Components

### Main Component

- **StyleEditor.razor**: Main page component with layout, layer tree, and preview
- **StyleEditor.razor.css**: Scoped styles for layout and responsive design
- **StyleEditor.razor.cs**: Code-behind (future)

### Property Panels

- **FillStylePanel.razor**: Polygon fill properties (color, opacity, pattern, outline)
- **StrokeStylePanel.razor**: Line/stroke properties (color, width, dash patterns, caps/joins)
- **TextStylePanel.razor**: Label properties (font, size, color, halo, anchor)
- **SymbolStylePanel.razor**: Icon/symbol properties (image, size, rotation, color)
- **VisibilityPanel.razor**: Visibility controls (layer visibility, opacity, zoom range, z-index)
- **RasterStylePanel.razor**: Raster adjustments (brightness, contrast, saturation, hue)
- **HeatmapStylePanel.razor**: Heatmap controls (radius, intensity, gradient, weight)

### Shared Resources

- **StylePanels.css**: Common styles for all property panels
- **README.md**: This file

## Architecture

### Data Model

The editor operates on `LayerDefinition` instances from `Honua.MapSDK.Models`:

- `VectorLayer`: Paint and layout properties (MapLibre GL compatible)
- `RasterLayer`: Raster-specific adjustments
- Custom layer types can be added with new panels

### State Management

- **Undo/Redo**: JSON serialization-based history stack
- **Change Detection**: `UpdatedAt` timestamp tracks modifications
- **Auto-Sync**: Optional real-time preview updates

### Event Flow

```
User Input → Property Panel → UpdatePaint/UpdateLayout
                               ↓
                         OnStyleChanged Callback
                               ↓
                         SaveStateForUndo (Parent)
                               ↓
                         RefreshPreview (if auto-sync)
```

## Usage

### Standalone Page

Navigate to `/style-editor` or `/style-editor/{layerId}`:

```razor
@page "/style-editor"
@page "/style-editor/{LayerId}"
```

### Dialog/Modal (Future)

Can be adapted for use in dialogs:

```razor
<MudDialog>
    <StyleEditor LayerId="@selectedLayerId" />
</MudDialog>
```

### Embedding (Future)

Embed in other components:

```razor
<StyleEditor LayerId="@layerId"
             OnStyleChanged="HandleStyleChange" />
```

## Development

### Adding a New Property Panel

1. Create `[Type]StylePanel.razor` in this directory
2. Add parameters:
   ```csharp
   [Parameter]
   public LayerDefinition? Layer { get; set; }

   [Parameter]
   public EventCallback OnStyleChanged { get; set; }
   ```
3. Cast to appropriate layer type:
   ```csharp
   private VectorLayer? VectorLayer => Layer as VectorLayer;
   ```
4. Load current values in `OnParametersSet()`
5. Update properties via `UpdatePaint()` or `UpdateLayout()`
6. Invoke callback: `OnStyleChanged.InvokeAsync()`
7. Reference in `StyleEditor.razor` with conditional rendering

### Testing

Test scenarios:
- [ ] Load layer with existing styles
- [ ] Modify each property type
- [ ] Undo/redo operations
- [ ] Save and reload
- [ ] Switch between layers
- [ ] Apply presets
- [ ] Export JSON
- [ ] Responsive layout (mobile/tablet)

### Performance Considerations

- Debounce rapid slider changes
- Lazy load preview updates (auto-sync off)
- Minimize re-renders with `ShouldRender()` overrides
- Use `ChangeEventArgs` for native inputs (color picker)

## Dependencies

### NuGet Packages
- MudBlazor (UI components)
- Honua.MapSDK (map models and components)

### MapLibre GL JS
- Used for map preview rendering
- Style specification compatibility

## Styling

### CSS Variables

The component respects Honua's CSS variables:
- `--mud-palette-primary`
- `--mud-palette-surface`
- `--mud-palette-background`
- `--mud-palette-text-primary`
- `--mud-palette-divider`

### Dark Mode

Automatically adapts using `[data-theme="dark"]` attribute.

### Responsive Breakpoints

- Desktop: Side-by-side layout (layer panel + preview)
- Tablet/Mobile: Stacked layout (layer panel above preview)
- Breakpoint: 960px (`md` in MudBlazor)

## Known Limitations

1. **Expression Builder**: No visual UI yet (raw JSON only)
2. **Custom Sprites**: No upload interface (reference existing only)
3. **3D Extrusion**: Panel not implemented
4. **Hillshade**: Panel not implemented
5. **Background Layer**: Not editable
6. **Multi-Select**: Can only edit one layer at a time
7. **Copy/Paste**: Not implemented
8. **Collaboration**: No real-time multi-user editing

## Future Enhancements

### Short Term
- [ ] JSON editor dialog
- [ ] Import/export dialogs
- [ ] Preset management (save custom presets)
- [ ] Layer group styling
- [ ] Copy/paste styles between layers

### Medium Term
- [ ] Expression builder UI with autocomplete
- [ ] Custom sprite upload
- [ ] Style templates/library
- [ ] A/B comparison view
- [ ] Style validation and warnings

### Long Term
- [ ] Collaborative editing (real-time)
- [ ] Version control integration
- [ ] AI-assisted styling suggestions
- [ ] Animated property keyframes
- [ ] Advanced filter UI

## Contributing

When contributing to the Style Editor:

1. Follow existing component patterns
2. Add appropriate JSDoc comments
3. Update this README with new features
4. Add tests for new functionality
5. Ensure responsive design
6. Test dark mode appearance
7. Update user documentation

## References

### Internal
- [Style Editor Research](../../../../docs/research/style-editor-research.md)
- [MapSDK Documentation](../../../../docs/MAPSDK_FEATURE_ROADMAP.md)
- [Layer Models](../../../Honua.MapSDK/Models/LayerDefinition.cs)

### External
- [MapLibre GL Style Spec](https://maplibre.org/maplibre-style-spec/)
- [Mapbox Studio](https://www.mapbox.com/mapbox-studio)
- [MudBlazor Components](https://mudblazor.com/components)

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0

---

**Component Version**: 1.0.0
**Last Updated**: 2025-01-12
**Maintainer**: Honua Development Team
