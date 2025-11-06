# HonuaCoordinateDisplay Examples

Comprehensive examples demonstrating various uses of the HonuaCoordinateDisplay component.

## Example 1: Basic Coordinate Display

The simplest setup with default configuration.

```razor
@page "/example1"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.CoordinateDisplay
@using Honua.MapSDK.Models

<PageTitle>Basic Coordinate Display</PageTitle>

<div style="position: relative; height: 100vh;">
    <HonuaMap
        Id="basicMap"
        Center="@(new[] { -122.4194, 37.7749 })"
        Zoom="12"
        Style="mapbox://styles/mapbox/streets-v12" />

    <HonuaCoordinateDisplay SyncWith="basicMap" />
</div>
```

**Features**:
- Default decimal degrees format
- Bottom-left position
- Shows scale and zoom
- 6 decimal precision

## Example 2: All Coordinate Formats Demo

Demonstrate all available coordinate formats side by side.

```razor
@page "/example2"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.CoordinateDisplay
@using Honua.MapSDK.Models

<PageTitle>All Coordinate Formats</PageTitle>

<MudText Typo="Typo.h4" Class="mb-4">Coordinate Format Comparison</MudText>
<MudText Typo="Typo.body2" Class="mb-6">
    Move your cursor over the map to see the same coordinates in different formats
</MudText>

<div style="position: relative; height: 600px; margin-bottom: 20px;">
    <HonuaMap
        Id="formatMap"
        Center="@(new[] { -122.4194, 37.7749 })"
        Zoom="12"
        Style="mapbox://styles/mapbox/streets-v12" />
</div>

<MudGrid>
    <MudItem xs="12" md="6" lg="4">
        <MudPaper Elevation="2" Class="pa-4">
            <MudText Typo="Typo.h6" Class="mb-2">Decimal Degrees (DD)</MudText>
            <HonuaCoordinateDisplay
                SyncWith="formatMap"
                Position="@null"
                CoordinateFormat="CoordinateFormat.DecimalDegrees"
                AllowFormatSwitch="false"
                AllowPinning="false"
                ShowScale="false"
                ShowZoom="false" />
        </MudPaper>
    </MudItem>

    <MudItem xs="12" md="6" lg="4">
        <MudPaper Elevation="2" Class="pa-4">
            <MudText Typo="Typo.h6" Class="mb-2">Degrees Decimal Minutes (DDM)</MudText>
            <HonuaCoordinateDisplay
                SyncWith="formatMap"
                Position="@null"
                CoordinateFormat="CoordinateFormat.DegreesDecimalMinutes"
                AllowFormatSwitch="false"
                AllowPinning="false"
                ShowScale="false"
                ShowZoom="false" />
        </MudPaper>
    </MudItem>

    <MudItem xs="12" md="6" lg="4">
        <MudPaper Elevation="2" Class="pa-4">
            <MudText Typo="Typo.h6" Class="mb-2">Degrees Minutes Seconds (DMS)</MudText>
            <HonuaCoordinateDisplay
                SyncWith="formatMap"
                Position="@null"
                CoordinateFormat="CoordinateFormat.DegreesMinutesSeconds"
                AllowFormatSwitch="false"
                AllowPinning="false"
                ShowScale="false"
                ShowZoom="false" />
        </MudPaper>
    </MudItem>

    <MudItem xs="12" md="6" lg="4">
        <MudPaper Elevation="2" Class="pa-4">
            <MudText Typo="Typo.h6" Class="mb-2">UTM</MudText>
            <HonuaCoordinateDisplay
                SyncWith="formatMap"
                Position="@null"
                CoordinateFormat="CoordinateFormat.UTM"
                AllowFormatSwitch="false"
                AllowPinning="false"
                ShowScale="false"
                ShowZoom="false" />
        </MudPaper>
    </MudItem>

    <MudItem xs="12" md="6" lg="4">
        <MudPaper Elevation="2" Class="pa-4">
            <MudText Typo="Typo.h6" Class="mb-2">MGRS</MudText>
            <HonuaCoordinateDisplay
                SyncWith="formatMap"
                Position="@null"
                CoordinateFormat="CoordinateFormat.MGRS"
                AllowFormatSwitch="false"
                AllowPinning="false"
                ShowScale="false"
                ShowZoom="false" />
        </MudPaper>
    </MudItem>

    <MudItem xs="12" md="6" lg="4">
        <MudPaper Elevation="2" Class="pa-4">
            <MudText Typo="Typo.h6" Class="mb-2">USNG</MudText>
            <HonuaCoordinateDisplay
                SyncWith="formatMap"
                Position="@null"
                CoordinateFormat="CoordinateFormat.USNG"
                AllowFormatSwitch="false"
                AllowPinning="false"
                ShowScale="false"
                ShowZoom="false" />
        </MudPaper>
    </MudItem>
</MudGrid>
```

**Features**:
- All 6 coordinate formats displayed simultaneously
- Embedded displays (not floating)
- Synchronized with single map
- Clean comparison layout

## Example 3: Coordinate Display with Elevation

Display elevation information with terrain data.

```razor
@page "/example3"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.CoordinateDisplay
@using Honua.MapSDK.Models

<PageTitle>Coordinates with Elevation</PageTitle>

<div style="position: relative; height: 100vh;">
    <HonuaMap
        Id="elevationMap"
        Center="@(new[] { -119.5383, 37.8651 })"
        Zoom="11"
        Style="mapbox://styles/mapbox/outdoors-v12"
        Terrain="true"
        TerrainExaggeration="1.5" />

    <HonuaCoordinateDisplay
        SyncWith="elevationMap"
        ShowElevation="true"
        ShowBearing="true"
        Unit="MeasurementUnit.Imperial"
        Position="bottom-left" />

    <MudPaper Elevation="4" Class="pa-4" Style="position: absolute; top: 10px; left: 10px; max-width: 300px;">
        <MudText Typo="Typo.h6" Class="mb-2">Elevation Display Demo</MudText>
        <MudText Typo="Typo.body2">
            This map shows Yosemite National Park with terrain enabled.
            Move your cursor over the mountains to see elevation changes.
        </MudText>
        <MudDivider Class="my-2" />
        <MudText Typo="Typo.caption">
            <strong>Features:</strong><br/>
            • Real-time elevation display<br/>
            • Imperial units (feet)<br/>
            • Terrain exaggeration: 1.5x<br/>
            • Bearing indicator
        </MudText>
    </MudPaper>
</div>
```

**Features**:
- 3D terrain visualization
- Real-time elevation display
- Imperial units
- Bearing display for map rotation
- Outdoor basemap optimized for terrain

## Example 4: Distance Measurement Between Points

Pin coordinates and measure distances.

```razor
@page "/example4"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.CoordinateDisplay
@using Honua.MapSDK.Models

<PageTitle>Distance Measurement</PageTitle>

<div style="position: relative; height: 100vh;">
    <HonuaMap
        Id="distanceMap"
        Center="@(new[] { -0.1276, 51.5074 })"
        Zoom="13"
        Style="mapbox://styles/mapbox/streets-v12" />

    <HonuaCoordinateDisplay
        SyncWith="distanceMap"
        AllowPinning="true"
        ShowDistance="true"
        Unit="MeasurementUnit.Metric"
        OnCoordinateClick="HandleCoordinateClick"
        Position="bottom-left" />

    @if (_pinnedCoord != null)
    {
        <MudPaper Elevation="4" Class="pa-4" Style="position: absolute; top: 10px; right: 10px; max-width: 300px;">
            <MudText Typo="Typo.h6" Class="mb-2">Pinned Location</MudText>
            <MudText Typo="Typo.body2">
                Lat: @_pinnedCoord[1].ToString("F6")<br/>
                Lng: @_pinnedCoord[0].ToString("F6")
            </MudText>
            <MudDivider Class="my-2" />
            <MudText Typo="Typo.caption">
                Move your cursor to see the distance from the pinned point.
                Click the pin icon again to unpin.
            </MudText>
        </MudPaper>
    }
    else
    {
        <MudPaper Elevation="4" Class="pa-4" Style="position: absolute; top: 10px; right: 10px; max-width: 300px;">
            <MudText Typo="Typo.h6" Class="mb-2">Instructions</MudText>
            <MudText Typo="Typo.body2">
                1. Click anywhere on the map to pin a location<br/>
                2. Move cursor to see distance from pinned point<br/>
                3. Click pin icon to unpin
            </MudText>
        </MudPaper>
    }
</div>

@code {
    private double[]? _pinnedCoord;

    private void HandleCoordinateClick(double[] coordinates)
    {
        _pinnedCoord = coordinates;
    }
}
```

**Features**:
- Click-to-pin functionality
- Real-time distance calculation
- Metric units
- Event callback for coordinate clicks
- Visual feedback for pinned location

## Example 5: Custom Position and Styling

Custom styling and positioning options.

```razor
@page "/example5"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.CoordinateDisplay
@using Honua.MapSDK.Models

<PageTitle>Custom Styled Coordinate Display</PageTitle>

<style>
    .custom-coords {
        background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        border: none;
        border-radius: 12px;
        box-shadow: 0 10px 30px rgba(0, 0, 0, 0.3);
    }

    .custom-coords .coordinate-text {
        color: white;
        font-weight: 600;
        text-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
    }

    .custom-coords .info-item {
        color: rgba(255, 255, 255, 0.9);
    }

    .custom-coords .info-icon {
        color: rgba(255, 255, 255, 0.9);
    }
</style>

<div style="position: relative; height: 100vh;">
    <HonuaMap
        Id="customMap"
        Center="@(new[] { 2.3522, 48.8566 })"
        Zoom="12"
        Style="mapbox://styles/mapbox/dark-v11" />

    <!-- Top-right position with custom styling -->
    <HonuaCoordinateDisplay
        SyncWith="customMap"
        Position="top-right"
        CssClass="custom-coords"
        Width="400px"
        CoordinateFormat="CoordinateFormat.DegreesMinutesSeconds"
        AllowFormatSwitch="true"
        AllowPrecisionChange="false" />

    <!-- Bottom-left minimal display -->
    <div style="position: absolute; bottom: 10px; left: 10px;">
        <HonuaCoordinateDisplay
            SyncWith="customMap"
            Position="@null"
            ShowScale="false"
            ShowZoom="false"
            AllowFormatSwitch="false"
            AllowPinning="false"
            AllowCopy="false"
            Width="250px"
            CoordinateFormat="CoordinateFormat.DecimalDegrees"
            Precision="4" />
    </div>
</div>
```

**Features**:
- Custom gradient background
- Multiple displays on same map
- Different positions
- Custom width
- Minimal vs. full-featured displays
- Dark theme compatible

## Example 6: Integration with Search Component

Coordinate display working with search results.

```razor
@page "/example6"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.CoordinateDisplay
@using Honua.MapSDK.Components.Search
@using Honua.MapSDK.Models
@using Honua.MapSDK.Services.Geocoding

<PageTitle>Search with Coordinates</PageTitle>

<div style="position: relative; height: 100vh;">
    <HonuaMap
        Id="searchMap"
        Center="@(new[] { -73.9857, 40.7484 })"
        Zoom="13"
        Style="mapbox://styles/mapbox/streets-v12" />

    <HonuaSearch
        SyncWith="searchMap"
        Position="top-right"
        Provider="GeocodeProvider.Nominatim"
        OnResultSelected="HandleSearchResult" />

    <HonuaCoordinateDisplay
        SyncWith="searchMap"
        Position="bottom-left"
        AllowCopy="true"
        OnCoordinateCopy="HandleCopy" />

    @if (_lastSearch != null)
    {
        <MudPaper Elevation="4" Class="pa-4" Style="position: absolute; top: 80px; right: 10px; max-width: 300px;">
            <MudText Typo="Typo.h6" Class="mb-2">Last Search</MudText>
            <MudText Typo="Typo.body2">
                <strong>@_lastSearch.DisplayName</strong><br/>
                Lat: @_lastSearch.Latitude.ToString("F6")<br/>
                Lng: @_lastSearch.Longitude.ToString("F6")
            </MudText>
        </MudPaper>
    }

    @if (_copiedCoords != null)
    {
        <MudSnackbar @bind-IsVisible="_showCopySnackbar" Variant="Variant.Filled" Color="Color.Success">
            Coordinates copied: @_copiedCoords
        </MudSnackbar>
    }
</div>

@code {
    private SearchResult? _lastSearch;
    private string? _copiedCoords;
    private bool _showCopySnackbar;

    private void HandleSearchResult(SearchResult result)
    {
        _lastSearch = result;
    }

    private void HandleCopy(string coordinates)
    {
        _copiedCoords = coordinates;
        _showCopySnackbar = true;
    }
}
```

**Features**:
- Search integration
- Copy coordinates
- Event callbacks
- Snackbar notifications
- Coordinate display updates on search

## Example 7: Military/Aviation Use Case

MGRS format with nautical units for aviation.

```razor
@page "/example7"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.CoordinateDisplay
@using Honua.MapSDK.Models

<PageTitle>Military/Aviation Coordinates</PageTitle>

<div style="position: relative; height: 100vh;">
    <HonuaMap
        Id="militaryMap"
        Center="@(new[] { -77.0369, 38.8951 })"
        Zoom="11"
        Style="mapbox://styles/mapbox/satellite-streets-v12"
        Bearing="45"
        Pitch="30" />

    <HonuaCoordinateDisplay
        SyncWith="militaryMap"
        CoordinateFormat="CoordinateFormat.MGRS"
        Unit="MeasurementUnit.Nautical"
        ShowBearing="true"
        ShowElevation="true"
        Position="bottom-left"
        AllowFormatSwitch="true"
        AllowCopy="true" />

    <MudPaper Elevation="4" Class="pa-4" Style="position: absolute; top: 10px; left: 10px; max-width: 350px; background: rgba(0, 0, 0, 0.7);">
        <MudText Typo="Typo.h6" Class="mb-2" Style="color: white;">Mission Coordinates</MudText>
        <MudText Typo="Typo.body2" Style="color: rgba(255, 255, 255, 0.9);">
            This example demonstrates military-grade coordinate display with MGRS format,
            commonly used in military and aviation applications.
        </MudText>
        <MudDivider Class="my-2" />
        <MudText Typo="Typo.caption" Style="color: rgba(255, 255, 255, 0.7);">
            <strong>Format:</strong> MGRS (Military Grid Reference System)<br/>
            <strong>Units:</strong> Nautical miles<br/>
            <strong>Map Bearing:</strong> 45°<br/>
            <strong>Map Pitch:</strong> 30°
        </MudText>
    </MudPaper>
</div>
```

**Features**:
- MGRS coordinate format
- Nautical units
- Bearing display
- 3D view (pitch/bearing)
- Satellite imagery
- Dark overlay for visibility

## Example 8: Precision Comparison

Different precision levels for decimal degrees.

```razor
@page "/example8"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.CoordinateDisplay
@using Honua.MapSDK.Models

<PageTitle>Coordinate Precision Comparison</PageTitle>

<MudText Typo="Typo.h4" Class="mb-2">Coordinate Precision Levels</MudText>
<MudText Typo="Typo.body2" Class="mb-4">
    See how different precision levels affect coordinate accuracy.
    Higher precision = more accuracy but longer display.
</MudText>

<div style="position: relative; height: 500px; margin-bottom: 20px;">
    <HonuaMap
        Id="precisionMap"
        Center="@(new[] { -122.4194, 37.7749 })"
        Zoom="15"
        Style="mapbox://styles/mapbox/streets-v12" />
</div>

<MudGrid>
    @foreach (var precision in new[] { 0, 2, 4, 6, 8 })
    {
        <MudItem xs="12" md="6" lg="4">
            <MudPaper Elevation="2" Class="pa-4">
                <MudText Typo="Typo.subtitle1" Class="mb-2">
                    Precision: @precision @(precision == 1 ? "decimal" : "decimals")
                </MudText>
                <MudText Typo="Typo.caption" Class="mb-2" Color="Color.Secondary">
                    Accuracy: ~@GetAccuracyDescription(precision)
                </MudText>
                <HonuaCoordinateDisplay
                    SyncWith="precisionMap"
                    Position="@null"
                    Precision="precision"
                    AllowFormatSwitch="false"
                    AllowPrecisionChange="false"
                    AllowPinning="false"
                    ShowScale="false"
                    ShowZoom="false" />
            </MudPaper>
        </MudItem>
    }
</MudGrid>

@code {
    private string GetAccuracyDescription(int precision)
    {
        return precision switch
        {
            0 => "111 km",
            2 => "1.1 km",
            4 => "11 m",
            6 => "11 cm",
            8 => "1.1 mm",
            _ => "Variable"
        };
    }
}
```

**Features**:
- Multiple precision levels
- Accuracy comparison
- Educational display
- Side-by-side comparison

## Example 9: Multi-Map Synchronization

Multiple maps with independent coordinate displays.

```razor
@page "/example9"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.CoordinateDisplay
@using Honua.MapSDK.Models

<PageTitle>Multi-Map Coordinates</PageTitle>

<MudText Typo="Typo.h4" Class="mb-4">Synchronized Maps</MudText>

<MudGrid>
    <MudItem xs="12" md="6">
        <MudPaper Elevation="2" Style="height: 400px; position: relative;">
            <MudText Typo="Typo.h6" Class="pa-2">Map 1 - Streets</MudText>
            <div style="height: calc(100% - 40px); position: relative;">
                <HonuaMap
                    Id="map1"
                    Center="@(new[] { -74.0060, 40.7128 })"
                    Zoom="12"
                    Style="mapbox://styles/mapbox/streets-v12" />
                <HonuaCoordinateDisplay
                    SyncWith="map1"
                    Position="bottom-right"
                    CoordinateFormat="CoordinateFormat.DecimalDegrees" />
            </div>
        </MudPaper>
    </MudItem>

    <MudItem xs="12" md="6">
        <MudPaper Elevation="2" Style="height: 400px; position: relative;">
            <MudText Typo="Typo.h6" Class="pa-2">Map 2 - Satellite</MudText>
            <div style="height: calc(100% - 40px); position: relative;">
                <HonuaMap
                    Id="map2"
                    Center="@(new[] { -74.0060, 40.7128 })"
                    Zoom="12"
                    Style="mapbox://styles/mapbox/satellite-v9" />
                <HonuaCoordinateDisplay
                    SyncWith="map2"
                    Position="bottom-right"
                    CoordinateFormat="CoordinateFormat.UTM" />
            </div>
        </MudPaper>
    </MudItem>

    <MudItem xs="12" md="6">
        <MudPaper Elevation="2" Style="height: 400px; position: relative;">
            <MudText Typo="Typo.h6" Class="pa-2">Map 3 - Terrain</MudText>
            <div style="height: calc(100% - 40px); position: relative;">
                <HonuaMap
                    Id="map3"
                    Center="@(new[] { -105.2705, 40.0150 })"
                    Zoom="10"
                    Style="mapbox://styles/mapbox/outdoors-v12"
                    Terrain="true" />
                <HonuaCoordinateDisplay
                    SyncWith="map3"
                    Position="bottom-right"
                    CoordinateFormat="CoordinateFormat.DegreesMinutesSeconds"
                    ShowElevation="true" />
            </div>
        </MudPaper>
    </MudItem>

    <MudItem xs="12" md="6">
        <MudPaper Elevation="2" Style="height: 400px; position: relative;">
            <MudText Typo="Typo.h6" Class="pa-2">Map 4 - Dark</MudText>
            <div style="height: calc(100% - 40px); position: relative;">
                <HonuaMap
                    Id="map4"
                    Center="@(new[] { -0.1276, 51.5074 })"
                    Zoom="11"
                    Style="mapbox://styles/mapbox/dark-v11" />
                <HonuaCoordinateDisplay
                    SyncWith="map4"
                    Position="bottom-right"
                    CoordinateFormat="CoordinateFormat.MGRS" />
            </div>
        </MudPaper>
    </MudItem>
</MudGrid>
```

**Features**:
- Multiple independent maps
- Different coordinate formats per map
- Different basemaps
- Grid layout
- Each display synced to specific map

## Example 10: Responsive Dashboard

Responsive layout with coordinate displays.

```razor
@page "/example10"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.CoordinateDisplay
@using Honua.MapSDK.Models

<PageTitle>Coordinate Dashboard</PageTitle>

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="pa-4">
    <MudText Typo="Typo.h4" Class="mb-4">Coordinate Analysis Dashboard</MudText>

    <MudGrid>
        <!-- Main Map -->
        <MudItem xs="12" lg="8">
            <MudPaper Elevation="2" Style="height: 600px; position: relative;">
                <HonuaMap
                    Id="dashboardMap"
                    Center="@_mapCenter"
                    Zoom="_mapZoom"
                    Style="mapbox://styles/mapbox/streets-v12" />
                <HonuaCoordinateDisplay
                    SyncWith="dashboardMap"
                    Position="bottom-left"
                    OnCoordinateClick="HandleCoordinateClick"
                    OnCoordinateCopy="HandleCopy" />
            </MudPaper>
        </MudItem>

        <!-- Side Panel with Stats -->
        <MudItem xs="12" lg="4">
            <MudPaper Elevation="2" Class="pa-4" Style="height: 600px; overflow-y: auto;">
                <MudText Typo="Typo.h6" Class="mb-3">Coordinate Information</MudText>

                @if (_currentCoords != null)
                {
                    <MudText Typo="Typo.subtitle2" Class="mb-2">Current Position</MudText>
                    <MudSimpleTable Dense="true" Class="mb-4">
                        <tbody>
                            <tr>
                                <td><strong>Latitude:</strong></td>
                                <td>@_currentCoords[1].ToString("F6")°</td>
                            </tr>
                            <tr>
                                <td><strong>Longitude:</strong></td>
                                <td>@_currentCoords[0].ToString("F6")°</td>
                            </tr>
                        </tbody>
                    </MudSimpleTable>

                    <MudDivider Class="my-3" />

                    <MudText Typo="Typo.subtitle2" Class="mb-2">All Formats</MudText>
                    <MudList Dense="true">
                        <MudListItem>
                            <strong>DD:</strong> @CoordinateConverter.Format(_currentCoords[0], _currentCoords[1], CoordinateFormat.DecimalDegrees)
                        </MudListItem>
                        <MudListItem>
                            <strong>DDM:</strong> @CoordinateConverter.Format(_currentCoords[0], _currentCoords[1], CoordinateFormat.DegreesDecimalMinutes)
                        </MudListItem>
                        <MudListItem>
                            <strong>DMS:</strong> @CoordinateConverter.Format(_currentCoords[0], _currentCoords[1], CoordinateFormat.DegreesMinutesSeconds)
                        </MudListItem>
                        <MudListItem>
                            <strong>UTM:</strong> @CoordinateConverter.Format(_currentCoords[0], _currentCoords[1], CoordinateFormat.UTM)
                        </MudListItem>
                        <MudListItem>
                            <strong>MGRS:</strong> @CoordinateConverter.Format(_currentCoords[0], _currentCoords[1], CoordinateFormat.MGRS)
                        </MudListItem>
                    </MudList>
                }
                else
                {
                    <MudAlert Severity="Severity.Info">
                        Click on the map to capture coordinates
                    </MudAlert>
                }

                @if (_copyMessage != null)
                {
                    <MudAlert Severity="Severity.Success" Class="mt-3">
                        @_copyMessage
                    </MudAlert>
                }
            </MudPaper>
        </MudItem>
    </MudGrid>
</MudContainer>

@code {
    private double[] _mapCenter = new[] { -122.4194, 37.7749 };
    private double _mapZoom = 12;
    private double[]? _currentCoords;
    private string? _copyMessage;

    private void HandleCoordinateClick(double[] coordinates)
    {
        _currentCoords = coordinates;
    }

    private void HandleCopy(string coordinates)
    {
        _copyMessage = $"Copied: {coordinates}";
        StateHasChanged();

        // Clear message after 3 seconds
        _ = Task.Delay(3000).ContinueWith(_ =>
        {
            _copyMessage = null;
            InvokeAsync(StateHasChanged);
        });
    }
}
```

**Features**:
- Dashboard layout
- Coordinate capture
- All formats display
- Responsive design
- Side panel with details
- Copy notifications

## Common Patterns

### Pattern 1: Minimal Display

```razor
<HonuaCoordinateDisplay
    SyncWith="myMap"
    ShowScale="false"
    ShowZoom="false"
    AllowFormatSwitch="false"
    AllowPinning="false"
    AllowCopy="false" />
```

### Pattern 2: Full-Featured Display

```razor
<HonuaCoordinateDisplay
    SyncWith="myMap"
    ShowScale="true"
    ShowZoom="true"
    ShowElevation="true"
    ShowBearing="true"
    ShowDistance="true"
    AllowFormatSwitch="true"
    AllowPrecisionChange="true"
    AllowPinning="true"
    AllowCopy="true" />
```

### Pattern 3: Read-Only Display

```razor
<HonuaCoordinateDisplay
    SyncWith="myMap"
    Position="@null"
    AllowFormatSwitch="false"
    AllowPrecisionChange="false"
    AllowPinning="false"
    AllowCopy="false" />
```

## Tips and Best Practices

1. **Choose the Right Format**: Use DD for general purposes, UTM/MGRS for military/technical applications
2. **Adjust Precision**: Higher precision increases accuracy but makes coordinates harder to read
3. **Use Appropriate Units**: Match units to your audience (metric for international, imperial for US)
4. **Enable Elevation**: Only when terrain data is available and relevant
5. **Position Wisely**: Avoid overlapping with other map controls
6. **Responsive Design**: Use embedded displays for mobile-friendly layouts
7. **Event Callbacks**: Leverage events for integration with other components
8. **Performance**: Coordinate tracking is throttled automatically for optimal performance

## Troubleshooting

See [README.md](./README.md#troubleshooting) for common issues and solutions.

## Further Reading

- [Coordinate Systems Explained](https://en.wikipedia.org/wiki/Geographic_coordinate_system)
- [UTM Grid System](https://en.wikipedia.org/wiki/Universal_Transverse_Mercator_coordinate_system)
- [MGRS Reference](https://en.wikipedia.org/wiki/Military_Grid_Reference_System)
- [MapLibre GL JS Documentation](https://maplibre.org/maplibre-gl-js-docs/api/)
