# HonuaDraw Component

The HonuaDraw component provides comprehensive drawing and measurement tools for the Honua.MapSDK library. It allows users to draw shapes, measure distances and areas, edit geometries, and export drawn features.

## Features

- **Multiple Drawing Modes**: Point, Line, Polygon, Circle, Rectangle, Freehand, Text
- **Real-time Measurements**: Distance, area, perimeter, radius, bearing, coordinates
- **Feature Editing**: Move, reshape, add/delete vertices, rotate, scale
- **Feature Management**: List, select, toggle visibility, delete
- **Undo/Redo**: Complete action history with undo/redo support
- **Export**: GeoJSON, CSV, KML formats
- **ComponentBus Integration**: Publishes messages for inter-component communication
- **Measurement Units**: Metric, Imperial, Nautical
- **Responsive Design**: Works on desktop and mobile
- **Accessibility**: Full keyboard support, ARIA labels, screen reader friendly

## Installation

The HonuaDraw component is part of Honua.MapSDK. Ensure you have the following dependencies:

```json
{
  "@mapbox/mapbox-gl-draw": "^1.4.0",
  "@turf/turf": "^6.5.0"
}
```

## Basic Usage

### Simple Drawing Toolbar

```razor
<HonuaMap Id="map1" />
<HonuaDraw SyncWith="map1" />
```

### With Custom Configuration

```razor
<HonuaDraw
    SyncWith="map1"
    ShowMeasurements="true"
    AllowEdit="true"
    DefaultStrokeColor="#FF0000"
    DefaultFillColor="#FF0000"
    DefaultStrokeWidth="3"
    MeasurementUnit="MeasurementUnit.Imperial"
    Position="top-right" />
```

### With Event Callbacks

```razor
<HonuaDraw
    SyncWith="map1"
    OnFeatureDrawn="HandleFeatureDrawn"
    OnFeatureMeasured="HandleMeasurement"
    OnFeatureEdited="HandleEdit"
    OnFeatureDeleted="HandleDelete"
    ShowFeatureList="true"
    EnableExport="true" />

@code {
    private async Task HandleFeatureDrawn(DrawingFeature feature)
    {
        Console.WriteLine($"Feature drawn: {feature.Id}");
    }

    private async Task HandleMeasurement(FeatureMeasurements measurements)
    {
        if (measurements.Distance.HasValue)
        {
            Console.WriteLine($"Distance: {measurements.Distance} meters");
        }
    }

    private async Task HandleEdit(DrawingFeature feature)
    {
        Console.WriteLine($"Feature edited: {feature.Id}");
    }

    private async Task HandleDelete(string featureId)
    {
        Console.WriteLine($"Feature deleted: {featureId}");
    }
}
```

## Parameters

### Core Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Id` | string | Auto-generated | Unique identifier for the component |
| `SyncWith` | string? | null | Map ID to synchronize with |
| `ShowToolbar` | bool | true | Show toolbar with drawing tools |
| `ShowMeasurements` | bool | true | Show live measurements while drawing |
| `ShowFeatureList` | bool | true | Show list of drawn features |
| `AllowEdit` | bool | true | Allow editing of drawn features |
| `EnableUndo` | bool | true | Enable undo/redo functionality |
| `EnableExport` | bool | true | Enable export functionality |
| `Collapsible` | bool | true | Feature list can be collapsed |

### Style Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `DefaultStrokeColor` | string | "#3B82F6" | Default stroke color (hex) |
| `DefaultFillColor` | string | "#3B82F6" | Default fill color (hex) |
| `DefaultStrokeWidth` | double | 2.0 | Default stroke width in pixels |
| `DefaultFillOpacity` | double | 0.2 | Default fill opacity (0.0-1.0) |

### Display Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `MeasurementUnit` | MeasurementUnit | Metric | Measurement unit system |
| `Position` | string? | null | Position on map (top-right, top-left, etc.) |
| `Width` | string | "350px" | Width of the component |
| `CssClass` | string? | null | Custom CSS class |
| `Style` | string? | null | Custom inline styles |

### Event Callbacks

| Parameter | Type | Description |
|-----------|------|-------------|
| `OnFeatureDrawn` | EventCallback&lt;DrawingFeature&gt; | Fired when feature is drawn |
| `OnFeatureMeasured` | EventCallback&lt;FeatureMeasurements&gt; | Fired when feature is measured |
| `OnFeatureEdited` | EventCallback&lt;DrawingFeature&gt; | Fired when feature is edited |
| `OnFeatureDeleted` | EventCallback&lt;string&gt; | Fired when feature is deleted |

## Drawing Modes

### Point
Click to place a marker on the map.

```csharp
DrawMode.Point
```

### Line
Click points to draw a line. Double-click to finish.

```csharp
DrawMode.Line
```

### Polygon
Click points to draw a polygon. Double-click to auto-close.

```csharp
DrawMode.Polygon
```

### Circle
Click center point, then drag to set radius.

```csharp
DrawMode.Circle
```

### Rectangle
Click one corner, drag to opposite corner.

```csharp
DrawMode.Rectangle
```

### Freehand
Click and drag to draw freeform lines.

```csharp
DrawMode.Freehand
```

### Text
Click to place text annotation.

```csharp
DrawMode.Text
```

## Measurements

The component automatically calculates measurements for drawn features:

### Distance (Lines)
- **Metric**: meters (m), kilometers (km)
- **Imperial**: feet (ft), miles (mi)
- **Nautical**: nautical miles (nm)

### Area (Polygons)
- **Metric**: square meters (m²), hectares (ha)
- **Imperial**: square feet (ft²), acres

### Perimeter (Polygons)
Same units as distance.

### Radius (Circles)
Same units as distance.

### Bearing (Lines)
Degrees from north (0-360°).

### Coordinates (Points)
Latitude and longitude in decimal degrees.

## ComponentBus Messages

The HonuaDraw component publishes and subscribes to the following messages:

### Published Messages

#### FeatureDrawnMessage
```csharp
public record FeatureDrawnMessage(
    string FeatureId,
    string GeometryType,
    object Geometry,
    Dictionary<string, object> Properties,
    string ComponentId
);
```

Published when a feature is successfully drawn.

#### FeatureMeasuredMessage
```csharp
public record FeatureMeasuredMessage(
    string FeatureId,
    double? Distance,
    double? Area,
    double? Perimeter,
    double? Radius,
    string Unit,
    string ComponentId
);
```

Published when measurements are calculated for a feature.

#### FeatureEditedMessage
```csharp
public record FeatureEditedMessage(
    string FeatureId,
    object Geometry,
    string EditType,
    string ComponentId
);
```

Published when a feature is edited (move, reshape, etc.).

#### FeatureDeletedMessage
```csharp
public record FeatureDeletedMessage(
    string FeatureId,
    string ComponentId
);
```

Published when a feature is deleted.

#### DrawModeChangedMessage
```csharp
public record DrawModeChangedMessage(
    string Mode,
    string ComponentId
);
```

Published when the drawing mode changes.

### Subscribed Messages

#### MapReadyMessage
Listens for map initialization to enable drawing tools.

#### StartDrawingRequestMessage
External request to start drawing in a specific mode.

#### StopDrawingRequestMessage
External request to stop drawing.

## Advanced Features

### Programmatic Control

You can control drawing programmatically via ComponentBus:

```csharp
// Start drawing a polygon
await Bus.PublishAsync(new StartDrawingRequestMessage
{
    MapId = "map1",
    Mode = "polygon",
    ComponentId = "draw1"
});

// Stop drawing
await Bus.PublishAsync(new StopDrawingRequestMessage
{
    MapId = "map1",
    ComponentId = "draw1"
});
```

### Custom Styles

```razor
<HonuaDraw
    SyncWith="map1"
    DefaultStrokeColor="#EF4444"
    DefaultFillColor="#EF4444"
    DefaultStrokeWidth="3"
    DefaultFillOpacity="0.25" />
```

### Feature Properties

Access and modify feature properties:

```csharp
private async Task HandleFeatureDrawn(DrawingFeature feature)
{
    // Add custom properties
    feature.Properties["creator"] = "John Doe";
    feature.Properties["category"] = "Important";
    feature.Name = "My Custom Feature";

    // Access measurements
    if (feature.Measurements?.Area.HasValue == true)
    {
        Console.WriteLine($"Area: {feature.Measurements.Area} sq meters");
    }
}
```

### Export Options

Export drawn features in multiple formats:

- **GeoJSON**: Standard geographic data format
- **CSV**: Tabular format with WKT geometry
- **KML**: Google Earth compatible format

```csharp
// Programmatic export (via JavaScript)
await _jsModule.InvokeVoidAsync("exportFeatures", "map1", "geojson");
```

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| `Esc` | Cancel current drawing / Deselect |
| `Delete` | Delete selected feature |
| `Enter` | Complete drawing (polygon/line) |
| `Ctrl+Z` | Undo |
| `Ctrl+Y` | Redo |
| `Backspace` | Remove last point while drawing |

## Styling

### CSS Variables

You can customize the appearance using CSS variables:

```css
.honua-draw {
    --draw-primary-color: #3B82F6;
    --draw-toolbar-bg: white;
    --draw-panel-bg: white;
    --draw-border-color: #E0E0E0;
}
```

### Custom CSS Classes

```razor
<HonuaDraw
    CssClass="my-custom-draw"
    SyncWith="map1" />
```

```css
.my-custom-draw .draw-toolbar {
    background: linear-gradient(to right, #667eea 0%, #764ba2 100%);
}

.my-custom-draw .tool-button {
    color: white;
}
```

## Best Practices

### 1. Always Sync with a Map
```razor
<!-- Good -->
<HonuaMap Id="map1" />
<HonuaDraw SyncWith="map1" />

<!-- Bad -->
<HonuaDraw /> <!-- No map to draw on -->
```

### 2. Handle Feature Events
```csharp
<HonuaDraw
    OnFeatureDrawn="SaveFeature"
    OnFeatureDeleted="RemoveFeature" />
```

### 3. Provide Clear Instructions
```razor
<HonuaDraw ShowToolbar="true" /> <!-- Instructions appear automatically -->
```

### 4. Set Appropriate Defaults
```razor
<HonuaDraw
    DefaultStrokeWidth="3"
    MeasurementUnit="MeasurementUnit.Imperial" />
```

### 5. Use Feature List for Management
```razor
<HonuaDraw
    ShowFeatureList="true"
    AllowEdit="true" />
```

## Accessibility

The component is fully accessible:

- All buttons have ARIA labels
- Keyboard navigation supported
- Screen reader announcements for measurements
- High contrast mode support
- Focus management

## Browser Support

- Chrome/Edge: ✓ Full support
- Firefox: ✓ Full support
- Safari: ✓ Full support
- Mobile browsers: ✓ Touch-optimized

## Dependencies

- **MapboxGL Draw**: Drawing functionality
- **Turf.js**: Accurate geographic measurements
- **MudBlazor**: UI components

## Troubleshooting

### Drawing not working
- Ensure map is initialized (`MapReadyMessage` received)
- Check `SyncWith` parameter matches map ID
- Verify MapboxGL Draw is loaded

### Measurements incorrect
- Ensure Turf.js is loaded
- Check measurement unit is set correctly
- Verify geometry is valid

### Features not appearing
- Check browser console for errors
- Ensure features have valid geometries
- Verify draw control is added to map

## Performance Considerations

- **Large datasets**: Use feature visibility to hide/show features
- **Complex geometries**: Simplify geometries before drawing
- **Many features**: Consider pagination or clustering
- **Undo history**: Limited to 50 actions by default

## Examples

See [Examples.md](./Examples.md) for comprehensive usage examples.

## License

Part of Honua.MapSDK - See main project license.

## Support

For issues, questions, or contributions, please visit the project repository.
