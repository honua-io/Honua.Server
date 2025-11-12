# Honua Style Editor

The Honua Style Editor is a comprehensive, visual tool for styling map layers. Built with insights from industry-leading platforms (Mapbox Studio, Esri VTSE, Felt, CARTO Builder), it provides an intuitive interface for both novice users and advanced cartographers.

## Features

### Core Capabilities

- **Visual Style Editing**: Real-time visual feedback as you modify layer styles
- **Multi-Layer Support**: Style vector, raster, heatmap, and extrusion layers
- **Property Panels**: Context-aware panels for fill, stroke, text, symbol, and visibility properties
- **Style Presets**: Quick-start templates for common use cases
- **Undo/Redo**: Full history support for safe experimentation
- **Live Preview**: Side-by-side map preview with optional auto-sync
- **JSON Export**: Export styles in MapLibre GL format

### Supported Layer Types

1. **Vector Layers**
   - Fill (Polygons)
   - Line (LineStrings)
   - Circle (Points)
   - Symbol (Icons + Text)

2. **Raster Layers**
   - Brightness/contrast/saturation controls
   - Hue rotation
   - Resampling methods

3. **Heatmap Layers**
   - Radius and intensity
   - Custom color gradients
   - Weight by data property

4. **3D Extrusion** (Future)
   - Building heights
   - Base elevation

## Getting Started

### Accessing the Style Editor

There are two ways to access the Style Editor:

1. **From Map Editor**:
   - Open an existing map in the Map Editor (`/maps/edit/{mapId}`)
   - Click the "Style Layers" button in the header
   - This opens the Style Editor with all layers from that map pre-loaded

2. **Direct Navigation**:
   - Navigate to `/style-editor/map/{mapId}` directly
   - Replace `{mapId}` with your map configuration ID

### Basic Workflow

1. **Load a Map**: Open a map from the Map Editor or navigate directly with a map ID
2. **Select a Layer**: Choose from the layer tree on the left
3. **Choose a Preset** (optional): Start with a predefined style
4. **Customize Properties**: Adjust colors, sizes, opacity, etc.
5. **Preview Changes**: View real-time updates in the map preview
6. **Save**: Styles are saved back to the map configuration on the server

## User Interface

### Layout

```
┌─────────────────────────────────────────────────┐
│ Header: Save, Undo, Redo, Export               │
├──────────────────┬──────────────────────────────┤
│ Left Panel       │ Right Panel: Map Preview     │
│ ┌──────────────┐ │                              │
│ │ Layer Tree   │ │                              │
│ └──────────────┘ │                              │
│ ┌──────────────┐ │                              │
│ │ Style Props  │ │                              │
│ │ - Fill       │ │                              │
│ │ - Stroke     │ │                              │
│ │ - Text       │ │                              │
│ │ - Visibility │ │                              │
│ └──────────────┘ │                              │
└──────────────────┴──────────────────────────────┘
```

### Property Panels

#### Fill Properties
- **Fill Color**: Color picker + hex input
- **Fill Opacity**: 0-100% slider
- **Outline Color**: Optional stroke around polygons
- **Fill Pattern**: Dots, stripes, diagonal, cross-hatch
- **Data-Driven**: Style by property values (advanced)

#### Stroke Properties
- **Stroke Color**: Color picker + hex input
- **Stroke Width**: 1-20px slider
- **Stroke Opacity**: 0-100% slider
- **Line Cap**: Butt, round, square
- **Line Join**: Miter, round, bevel
- **Dash Pattern**: Solid, dashed, dotted, custom

#### Text Properties
- **Text Field**: Property name to display
- **Text Color**: Color picker
- **Text Size**: 8-48px slider
- **Font**: Open Sans, Arial, Roboto, Noto Sans
- **Text Halo**: Outline for readability
- **Text Anchor**: Position relative to point
- **Advanced**: Opacity, rotation, letter spacing, max width

#### Symbol Properties
- **Icon Name**: Reference to sprite sheet
- **Icon Size**: 0.1-3x scale
- **Icon Color**: For SDF (signed distance field) icons
- **Icon Opacity**: 0-100%
- **Icon Rotation**: 0-360 degrees
- **Icon Anchor**: Position relative to point
- **Advanced**: Offset, halo, overlap settings

#### Visibility Properties
- **Layer Visible**: On/off toggle
- **Layer Opacity**: 0-100%
- **Min Zoom**: Layer appears above this zoom level
- **Max Zoom**: Layer hidden above this zoom level
- **Z-Index**: Layer stacking order
- **Group ID**: Organize layers into groups

#### Raster Properties
- **Brightness**: Min/max brightness adjustment
- **Contrast**: -1 (low) to 1 (high)
- **Saturation**: -1 (grayscale) to 1 (oversaturated)
- **Hue Rotation**: 0-360 degrees color shift
- **Resampling**: Linear (smooth) or nearest (crisp)
- **Presets**: Normal, bright, dark, high contrast, grayscale, sepia

#### Heatmap Properties
- **Radius**: 5-100px influence radius
- **Intensity**: 0-2x multiplier
- **Weight**: Optional property-based weighting
- **Color Gradient**: Hot, cool, rainbow, viridis, plasma, inferno, custom
- **Opacity**: 0-100%

## Style Presets

Quick-start templates to accelerate styling:

| Preset | Description | Use Case |
|--------|-------------|----------|
| **Default** | Blue tones, standard opacity | General purpose |
| **Highlight** | Yellow/bright colors | Call attention to features |
| **Muted** | Gray tones, low opacity | Background/context layers |
| **Bold** | Red/strong colors, thick lines | Important features |

## Advanced Features

### Data-Driven Styling

Style properties based on feature attributes using MapLibre expressions:

```javascript
// Color by population density
["interpolate",
  ["linear"],
  ["get", "population"],
  0, "#ffffcc",
  1000, "#a1dab4",
  5000, "#41b6c4",
  10000, "#225ea8"
]
```

### Undo/Redo

- **Undo**: Revert to previous state (Ctrl+Z)
- **Redo**: Restore undone change (Ctrl+Y)
- **History**: Unlimited undo depth

### Auto-Sync Preview

- **Enabled**: Map updates instantly as you change properties
- **Disabled**: Manual refresh for better performance with large datasets

### JSON Editor (Future)

Direct access to the underlying MapLibre GL style JSON for power users.

## Tips & Best Practices

### Performance

1. **Use zoom-level visibility**: Hide detail layers at world view
2. **Limit heatmap radius**: Large radii slow rendering
3. **Minimize overlapping text**: Use `text-allow-overlap: false`
4. **Simplify geometries**: Use appropriate detail for zoom level

### Cartography

1. **Contrast is key**: Ensure features stand out from basemap
2. **Consistent color schemes**: Use related hues for similar features
3. **Text halos**: Always add halos for text readability
4. **Progressive disclosure**: Show more detail as users zoom in
5. **Accessibility**: Consider colorblind-friendly palettes

### Workflow

1. **Start with presets**: Modify instead of building from scratch
2. **Group related layers**: Use layer groups for organization
3. **Name layers clearly**: Descriptive names aid collaboration
4. **Test at multiple zooms**: Verify appearance across zoom ranges
5. **Export regularly**: Save incremental versions as JSON

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl/Cmd + S` | Save styles |
| `Ctrl/Cmd + Z` | Undo |
| `Ctrl/Cmd + Shift + Z` | Redo |
| `Ctrl/Cmd + E` | Export JSON |
| `Esc` | Close editor |

## Integration with Honua

### Data Persistence

The Style Editor integrates directly with Honua's persistence framework:

- **Loading**: Reads map configurations from `/admin/api/map-configurations/{id}`
- **Saving**: Updates map configurations via PUT to `/admin/api/map-configurations/{id}`
- **Format**: Stores styles in the `LayerConfiguration.Style` property as part of the `MapConfiguration` JSON

### Data Flow

```
Map Configuration (Server)
    ↓ Load
LayerConfiguration → LayerDefinition (Editing)
    ↓ Edit
LayerDefinition.Paint/Layout (MapLibre Properties)
    ↓ Save
LayerConfiguration.Style ← Convert Back
    ↓ Persist
Map Configuration (Server)
```

### Conversion Process

**Loading** (LayerConfiguration → LayerDefinition):
- `LayerConfiguration` has simplified `LayerStyle` properties
- Converted to `LayerDefinition` with full `Paint` and `Layout` dictionaries
- Enables editing with full MapLibre GL property support

**Saving** (LayerDefinition → LayerConfiguration):
- `LayerDefinition.Paint` properties extracted
- Converted back to `LayerStyle` simplified format
- Stored in map configuration JSON on server

### Supported Formats

#### Import
- MapLibre GL Style JSON
- Map Configuration JSON (Honua format)
- SLD (Styled Layer Descriptor) - via conversion
- YSLD (YAML SLD) - via conversion

#### Export
- MapLibre GL Style JSON (primary)
- Map Configuration JSON (Honua format)
- SLD (Styled Layer Descriptor)
- YSLD (YAML SLD)
- CSS (basic properties)
- KML (simple styles)

## Troubleshooting

### Styles Not Appearing

1. **Check layer visibility**: Ensure layer is visible and within zoom range
2. **Verify opacity**: Opacity set to 0 makes layers invisible
3. **Check source**: Ensure layer has valid data source
4. **Inspect browser console**: Look for MapLibre errors

### Performance Issues

1. **Disable auto-sync**: Manual refresh for large datasets
2. **Reduce heatmap radius**: Smaller radii = faster rendering
3. **Limit text labels**: Use `text-allow-overlap: false`
4. **Simplify filters**: Complex expressions slow performance

### Unexpected Appearance

1. **Check Z-index**: Higher layers may obscure lower layers
2. **Verify paint properties**: Ensure values are in valid ranges
3. **Review expressions**: Syntax errors fail silently
4. **Test on different zoom levels**: Some properties are zoom-dependent

## Developer Notes

### Component Architecture

```
StyleEditor.razor (main)
├── FillStylePanel.razor
├── StrokeStylePanel.razor
├── TextStylePanel.razor
├── SymbolStylePanel.razor
├── VisibilityPanel.razor
├── RasterStylePanel.razor
└── HeatmapStylePanel.razor
```

### Data Flow

1. User selects layer → `SelectLayer()`
2. User changes property → `UpdatePaint()` or `UpdateLayout()`
3. Component invokes `OnStyleChanged` callback
4. Parent saves state for undo → `SaveStateForUndo()`
5. Preview refreshes (if auto-sync enabled)

### Extending

To add a new property panel:

1. Create `[Type]StylePanel.razor` component
2. Accept `Layer` and `OnStyleChanged` parameters
3. Load current values in `OnParametersSet()`
4. Update layer properties and invoke callback on change
5. Reference in `StyleEditor.razor` with conditional rendering

## Future Enhancements

- [ ] Expression builder UI
- [ ] Style library/templates
- [ ] Copy/paste styles between layers
- [ ] A/B style comparison
- [ ] Collaborative editing
- [ ] Style history/versions
- [ ] Custom sprite sheet upload
- [ ] 3D terrain styling
- [ ] Animation properties
- [ ] Style validation

## Related Documentation

- [MapLibre GL Style Specification](https://maplibre.org/maplibre-style-spec/)
- [Honua MapSDK Documentation](../MAPSDK_FEATURE_ROADMAP.md)
- [Layer Configuration Guide](../user/layer-configuration.md)
- [Style Editor Research](../research/style-editor-research.md)

## Support

For issues or feature requests, please contact:
- GitHub Issues: https://github.com/honua-io/Honua.Server/issues
- Documentation: https://docs.honua.io

---

**Version**: 1.0.0
**Last Updated**: 2025-01-12
**Author**: Honua Development Team
