# HonuaCoordinateDisplay Component

A comprehensive coordinate display component for Honua.MapSDK that shows cursor coordinates, map scale, zoom level, elevation, and bearing information.

## Overview

The `HonuaCoordinateDisplay` component provides real-time coordinate tracking and display capabilities with multiple coordinate format options. It's designed to be lightweight, accurate, and highly customizable.

## Features

- **Real-time Coordinate Tracking**: Displays cursor coordinates as you move the mouse over the map
- **Multiple Coordinate Formats**:
  - Decimal Degrees (DD)
  - Degrees Decimal Minutes (DDM)
  - Degrees Minutes Seconds (DMS)
  - UTM (Universal Transverse Mercator)
  - MGRS (Military Grid Reference System)
  - USNG (United States National Grid)
- **Coordinate Pinning**: Click to pin coordinates and measure distances
- **Copy to Clipboard**: One-click copy of coordinates in current format
- **Scale Display**: Shows map scale ratio (e.g., 1:25,000)
- **Zoom Level**: Current zoom level with precision
- **Elevation**: Displays elevation at cursor (when terrain is enabled)
- **Bearing**: Shows map bearing/rotation when map is rotated
- **Distance Measurement**: Shows distance from pinned coordinate
- **Configurable Precision**: Adjustable decimal precision for DD format
- **Unit Systems**: Metric, Imperial, and Nautical units
- **ComponentBus Integration**: Publishes coordinate events for other components

## Installation

The component is part of the Honua.MapSDK package. No additional installation is required.

## Basic Usage

```razor
@page "/map"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.CoordinateDisplay

<HonuaMap Id="myMap" />
<HonuaCoordinateDisplay SyncWith="myMap" />
```

## Parameters

### Core Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Id` | `string` | Auto-generated | Unique identifier for the component |
| `SyncWith` | `string?` | `null` | Map ID to synchronize with |
| `CoordinateFormat` | `CoordinateFormat` | `DecimalDegrees` | Coordinate display format |
| `Precision` | `int` | `6` | Number of decimal places for DD format |

### Display Options

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ShowScale` | `bool` | `true` | Show map scale information |
| `ShowZoom` | `bool` | `true` | Show zoom level |
| `ShowElevation` | `bool` | `false` | Show elevation at cursor (requires terrain) |
| `ShowBearing` | `bool` | `false` | Show bearing/heading if map is rotated |
| `ShowDistance` | `bool` | `true` | Show distance from pinned coordinate |

### User Interaction

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `AllowFormatSwitch` | `bool` | `true` | Allow user to switch coordinate formats |
| `AllowPrecisionChange` | `bool` | `true` | Allow user to change precision |
| `AllowPinning` | `bool` | `true` | Allow pinning coordinates |
| `AllowCopy` | `bool` | `true` | Allow copying coordinates to clipboard |

### Styling

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Position` | `string?` | `"bottom-left"` | Position on map: top-right, top-left, bottom-right, bottom-left, or null for embedded |
| `Width` | `string?` | Auto | Width of coordinate display |
| `CssClass` | `string?` | `null` | Custom CSS class |

### Measurement

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Unit` | `MeasurementUnit` | `Metric` | Measurement unit system (Metric, Imperial, Nautical) |

### Events

| Event | Type | Description |
|-------|------|-------------|
| `OnCoordinateClick` | `EventCallback<double[]>` | Triggered when coordinates are clicked/pinned |
| `OnCoordinateCopy` | `EventCallback<string>` | Triggered when coordinates are copied |

## Coordinate Formats

### Decimal Degrees (DD)

The most common format using decimal notation.

**Format**: `37.774900°N, 122.419400°W`

**Formula**: Direct latitude/longitude values with direction indicator.

**Example**:
```razor
<HonuaCoordinateDisplay
    SyncWith="myMap"
    CoordinateFormat="CoordinateFormat.DecimalDegrees"
    Precision="6" />
```

### Degrees Decimal Minutes (DDM)

Degrees with decimal minutes.

**Format**: `37°46.494'N 122°25.164'W`

**Formula**:
- Degrees = floor(decimal degrees)
- Minutes = (decimal degrees - degrees) × 60

**Example**:
```razor
<HonuaCoordinateDisplay
    SyncWith="myMap"
    CoordinateFormat="CoordinateFormat.DegreesDecimalMinutes"
    Precision="3" />
```

### Degrees Minutes Seconds (DMS)

Traditional format with degrees, minutes, and seconds.

**Format**: `37°46'29.6" N 122°25'9.8" W`

**Formula**:
- Degrees = floor(decimal degrees)
- Minutes = floor((decimal degrees - degrees) × 60)
- Seconds = ((decimal degrees - degrees) × 60 - minutes) × 60

**Example**:
```razor
<HonuaCoordinateDisplay
    SyncWith="myMap"
    CoordinateFormat="CoordinateFormat.DegreesMinutesSeconds"
    Precision="1" />
```

### UTM (Universal Transverse Mercator)

Grid-based coordinate system dividing the Earth into zones.

**Format**: `10N 551316mE 4180969mN`

**Components**:
- Zone number (1-60)
- Hemisphere (N/S)
- Easting (meters)
- Northing (meters)

**Formula**: Complex projection using WGS84 ellipsoid parameters. See `CoordinateConverter.ToUTM()` for implementation.

**Example**:
```razor
<HonuaCoordinateDisplay
    SyncWith="myMap"
    CoordinateFormat="CoordinateFormat.UTM" />
```

### MGRS (Military Grid Reference System)

Military grid system based on UTM.

**Format**: `10SEG5131680969`

**Components**:
- Grid zone (10S)
- 100km grid square (EG)
- Easting within square (51316)
- Northing within square (80969)

**Example**:
```razor
<HonuaCoordinateDisplay
    SyncWith="myMap"
    CoordinateFormat="CoordinateFormat.MGRS" />
```

### USNG (United States National Grid)

US adaptation of MGRS (identical format).

**Format**: `10SEG5131680969`

**Example**:
```razor
<HonuaCoordinateDisplay
    SyncWith="myMap"
    CoordinateFormat="CoordinateFormat.USNG" />
```

## Measurement Units

### Metric (Default)

- Distance: meters (m) / kilometers (km)
- Elevation: meters (m)
- Conversion: < 1000m shows meters, >= 1000m shows km

### Imperial

- Distance: feet (ft) / miles (mi)
- Elevation: feet (ft)
- Conversion: < 5280ft shows feet, >= 5280ft shows miles
- Formula: meters × 3.28084 = feet

### Nautical

- Distance: meters (m) / nautical miles (nm)
- Elevation: meters (m)
- Conversion: < 185.2m shows meters, >= 185.2m shows nautical miles
- Formula: meters / 1852 = nautical miles

## ComponentBus Messages

### Published Messages

**CoordinateClickedMessage**
- Published when coordinates are clicked on the map
- Contains: `DisplayId`, `Longitude`, `Latitude`, `Elevation`, `Formatted`

**CoordinatePinnedMessage**
- Published when coordinates are pinned
- Contains: `DisplayId`, `Longitude`, `Latitude`, `Elevation`, `Formatted`

### Subscribed Messages

**MapReadyMessage**
- Listens for map ready state to initialize coordinate tracking

**MapExtentChangedMessage**
- Updates scale, zoom, and bearing information when map extent changes

## JavaScript Interop

The component uses `honua-coordinates.js` for map interaction:

```javascript
// Initialize coordinate tracking
initializeCoordinateTracking(mapId, dotNetRef)

// Stop tracking
stopTracking(mapId)

// Get map scale
getMapScale(mapId)

// Get zoom level
getZoomLevel(mapId)

// Get bearing
getBearing(mapId)

// Query elevation
queryElevation(mapId, longitude, latitude)

// Calculate distance
calculateDistance(lng1, lat1, lng2, lat2)

// Copy to clipboard
copyToClipboard(text)
```

## Positioning

The component can be positioned in several ways:

### Floating (Overlay on Map)

```razor
<!-- Top-right corner -->
<HonuaCoordinateDisplay SyncWith="myMap" Position="top-right" />

<!-- Top-left corner -->
<HonuaCoordinateDisplay SyncWith="myMap" Position="top-left" />

<!-- Bottom-right corner -->
<HonuaCoordinateDisplay SyncWith="myMap" Position="bottom-right" />

<!-- Bottom-left corner (default) -->
<HonuaCoordinateDisplay SyncWith="myMap" Position="bottom-left" />
```

### Embedded

```razor
<!-- No position = embedded in page layout -->
<div class="coordinate-panel">
    <HonuaCoordinateDisplay SyncWith="myMap" Position="@null" />
</div>
```

## Advanced Features

### Coordinate Pinning and Distance Measurement

Pin a coordinate and measure distance from that point:

```razor
<HonuaCoordinateDisplay
    SyncWith="myMap"
    AllowPinning="true"
    ShowDistance="true"
    Unit="MeasurementUnit.Metric" />
```

**Usage**:
1. Move cursor to location
2. Click the pin button or click on the map
3. Distance from pinned point is shown as you move cursor
4. Click pin button again to unpin

### Elevation Display

Show elevation at cursor position (requires terrain):

```razor
<HonuaMap Id="myMap" Terrain="true" />
<HonuaCoordinateDisplay
    SyncWith="myMap"
    ShowElevation="true"
    Unit="MeasurementUnit.Imperial" />
```

### Copy Coordinates

One-click copy to clipboard:

```razor
<HonuaCoordinateDisplay
    SyncWith="myMap"
    AllowCopy="true"
    OnCoordinateCopy="HandleCopy" />

@code {
    private void HandleCopy(string coordinates)
    {
        // Coordinates copied to clipboard
        Console.WriteLine($"Copied: {coordinates}");
    }
}
```

### Custom Format and Precision

```razor
<HonuaCoordinateDisplay
    SyncWith="myMap"
    CoordinateFormat="CoordinateFormat.DecimalDegrees"
    Precision="8"
    AllowFormatSwitch="true"
    AllowPrecisionChange="true" />
```

## Styling and Customization

### Custom Width

```razor
<HonuaCoordinateDisplay
    SyncWith="myMap"
    Width="500px" />
```

### Custom CSS Class

```razor
<HonuaCoordinateDisplay
    SyncWith="myMap"
    CssClass="my-coordinate-display" />
```

Add custom styles:

```css
.my-coordinate-display {
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    border: 2px solid white;
    box-shadow: 0 10px 30px rgba(0,0,0,0.3);
}

.my-coordinate-display .coordinate-text {
    color: white;
    font-weight: bold;
}
```

### Dark Mode

The component automatically adapts to dark mode:

```css
@media (prefers-color-scheme: dark) {
    /* Automatic dark mode styles applied */
}
```

## Performance Considerations

- **Throttling**: Mouse move events are throttled to 50ms to prevent excessive updates
- **Efficient Rendering**: Uses Blazor's optimized rendering with `StateHasChanged` only when needed
- **Lazy Calculation**: Scale and elevation are calculated only when displayed
- **Memory Management**: Proper cleanup on dispose to prevent memory leaks

## Accessibility

- ARIA labels on all interactive buttons
- Keyboard navigation support
- High contrast mode support
- Screen reader friendly
- Reduced motion support

## Browser Compatibility

- Chrome 90+
- Firefox 88+
- Safari 14+
- Edge 90+

Requires:
- JavaScript ES6 modules
- Clipboard API (for copy functionality)
- Geolocation API (if using location features)

## Troubleshooting

### Coordinates Not Updating

1. Ensure `SyncWith` parameter matches the map ID
2. Verify map is initialized before coordinate display
3. Check browser console for JavaScript errors

### Elevation Not Showing

1. Ensure map has terrain enabled: `<HonuaMap Terrain="true" />`
2. Set `ShowElevation="true"` on coordinate display
3. Note: Some areas may not have elevation data

### Copy Not Working

1. Ensure browser supports Clipboard API
2. Page must be served over HTTPS (localhost is okay)
3. User may need to grant clipboard permissions

### Format Conversion Issues

1. UTM/MGRS only valid between 80°S and 84°N
2. Some formats may have precision limitations
3. Check console for conversion warnings

## Examples

See [Examples.md](./Examples.md) for comprehensive usage examples.

## API Reference

For complete API documentation, see the component source code and XML documentation comments.

## Related Components

- **HonuaMap**: The base map component
- **HonuaSearch**: Geographic search component
- **HonuaDraw**: Drawing and measurement tools
- **HonuaOverviewMap**: Mini-map overview

## License

Part of Honua.MapSDK - see main SDK license.
