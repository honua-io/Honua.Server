# HonuaOverviewMap Examples

Comprehensive examples demonstrating all features and use cases of the HonuaOverviewMap component.

## Table of Contents
- [Basic Examples](#basic-examples)
- [Positioning Examples](#positioning-examples)
- [Styling Examples](#styling-examples)
- [Interaction Examples](#interaction-examples)
- [Advanced Examples](#advanced-examples)
- [Real-World Scenarios](#real-world-scenarios)

---

## Basic Examples

### 1. Minimal Setup

The simplest possible overview map:

```razor
@page "/basic-overview"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.OverviewMap

<div style="position: relative; width: 100%; height: 100vh;">
    <HonuaMap Id="map1"
              Center="new[] { -122.419, 37.775 }"
              Zoom="13" />

    <HonuaOverviewMap SyncWith="map1" />
</div>
```

### 2. Custom Size

Overview map with custom dimensions:

```razor
<HonuaMap Id="map2"
          Center="new[] { -0.1276, 51.5074 }"
          Zoom="12" />

<HonuaOverviewMap SyncWith="map2"
                  Width="300"
                  Height="250" />
```

### 3. Different Zoom Offset

Control how much the overview is zoomed out:

```razor
<HonuaMap Id="map3"
          Center="new[] { 2.3522, 48.8566 }"
          Zoom="14" />

<!-- Very zoomed out for large context -->
<HonuaOverviewMap SyncWith="map3"
                  ZoomOffset="-8" />

<!-- Slightly zoomed out for local context -->
<HonuaOverviewMap SyncWith="map3"
                  Position="bottom-left"
                  ZoomOffset="-3" />
```

---

## Positioning Examples

### 4. All Corner Positions

```razor
<HonuaMap Id="map4"
          Center="new[] { 139.6917, 35.6895 }"
          Zoom="11" />

<!-- Top-left corner -->
<HonuaOverviewMap SyncWith="map4"
                  Position="top-left"
                  Width="150"
                  Height="150" />

<!-- Top-right corner (uncomment one at a time) -->
<!-- <HonuaOverviewMap SyncWith="map4"
                  Position="top-right"
                  Width="150"
                  Height="150" /> -->

<!-- Bottom-left corner -->
<!-- <HonuaOverviewMap SyncWith="map4"
                  Position="bottom-left"
                  Width="150"
                  Height="150" /> -->

<!-- Bottom-right corner (default) -->
<!-- <HonuaOverviewMap SyncWith="map4"
                  Position="bottom-right"
                  Width="150"
                  Height="150" /> -->
```

### 5. Custom Positioning

Precise positioning with offsets:

```razor
<HonuaMap Id="map5"
          Center="new[] { 13.4050, 52.5200 }"
          Zoom="12" />

<HonuaOverviewMap SyncWith="map5"
                  Position="custom"
                  OffsetX="20"
                  OffsetY="100"
                  Width="200"
                  Height="200" />
```

### 6. Multiple Overview Maps

Different perspectives at once:

```razor
<HonuaMap Id="map6"
          Center="new[] { -74.0060, 40.7128 }"
          Zoom="15" />

<!-- City-level overview -->
<HonuaOverviewMap SyncWith="map6"
                  Position="bottom-right"
                  ZoomOffset="-5"
                  Title="City View"
                  Width="180"
                  Height="180" />

<!-- Regional overview -->
<HonuaOverviewMap SyncWith="map6"
                  Position="bottom-left"
                  ZoomOffset="-10"
                  Title="Regional View"
                  Width="180"
                  Height="180" />
```

---

## Styling Examples

### 7. Custom Extent Box Colors

Colorful extent indicators:

```razor
<HonuaMap Id="map7"
          Center="new[] { 18.0686, 59.3293 }"
          Zoom="13" />

<!-- Green extent box -->
<HonuaOverviewMap SyncWith="map7"
                  ExtentBoxColor="#00FF00"
                  ExtentBoxFillColor="#00FF00"
                  ExtentBoxWidth="3"
                  ExtentBoxOpacity="1.0"
                  ExtentBoxFillOpacity="0.15" />
```

### 8. Dark Theme Overview

Styled for dark mode:

```razor
<HonuaMap Id="map8"
          MapStyle="https://tiles.stadiamaps.com/styles/alidade_smooth_dark.json"
          Center="new[] { -118.2437, 34.0522 }"
          Zoom="12" />

<HonuaOverviewMap SyncWith="map8"
                  BackgroundColor="#1a1a1a"
                  BorderColor="#444"
                  BorderWidth="2"
                  ExtentBoxColor="#FFD700"
                  ExtentBoxFillColor="#FFD700"
                  ExtentBoxFillOpacity="0.2"
                  BoxShadow="0 4px 16px rgba(0,0,0,0.8)" />
```

### 9. Custom Borders and Shadows

```razor
<HonuaMap Id="map9"
          Center="new[] { 12.4964, 41.9028 }"
          Zoom="14" />

<HonuaOverviewMap SyncWith="map9"
                  BorderRadius="12"
                  BorderWidth="3"
                  BorderColor="#4A90E2"
                  BoxShadow="0 8px 24px rgba(74, 144, 226, 0.3)"
                  BackgroundColor="#f8f9fa" />
```

### 10. Minimal/Clean Style

Borderless, modern look:

```razor
<HonuaMap Id="map10"
          Center="new[] { 144.9631, -37.8136 }"
          Zoom="13" />

<HonuaOverviewMap SyncWith="map10"
                  BorderWidth="0"
                  BorderRadius="8"
                  BoxShadow="0 2px 12px rgba(0,0,0,0.15)"
                  ExtentBoxColor="#000"
                  ExtentBoxWidth="1"
                  ExtentBoxOpacity="0.6" />
```

---

## Interaction Examples

### 11. View-Only Mode

No interaction, just visualization:

```razor
<HonuaMap Id="map11"
          Center="new[] { -3.7038, 40.4168 }"
          Zoom="12" />

<HonuaOverviewMap SyncWith="map11"
                  ClickToPan="false"
                  DragToPan="false"
                  ScrollToZoom="false"
                  ShowToggleButton="false" />
```

### 12. Click-Only Navigation

Only allow clicking, no dragging:

```razor
<HonuaMap Id="map12"
          Center="new[] { 4.9041, 52.3676 }"
          Zoom="13" />

<HonuaOverviewMap SyncWith="map12"
                  ClickToPan="true"
                  DragToPan="false"
                  ScrollToZoom="false" />
```

### 13. Full Interaction

All interaction modes enabled:

```razor
<HonuaMap Id="map13"
          Center="new[] { 103.8198, 1.3521 }"
          Zoom="14" />

<HonuaOverviewMap SyncWith="map13"
                  ClickToPan="true"
                  DragToPan="true"
                  ScrollToZoom="true" />
```

### 14. With Click Event Handler

React to overview clicks:

```razor
<HonuaMap Id="map14"
          Center="new[] { -43.1729, -22.9068 }"
          Zoom="12" />

<HonuaOverviewMap SyncWith="map14"
                  OnOverviewClicked="HandleOverviewClick" />

@code {
    private void HandleOverviewClick(OverviewMapClickedMessage message)
    {
        Console.WriteLine($"Clicked at: {message.Center[0]}, {message.Center[1]}");
        // Perform custom actions
    }
}
```

### 15. Collapsible with Custom Behavior

```razor
<HonuaMap Id="map15"
          Center="new[] { 114.1095, 22.3964 }"
          Zoom="12" />

<HonuaOverviewMap @ref="_overview"
                  SyncWith="map15"
                  Collapsible="true"
                  InitiallyCollapsed="false" />

<div style="position: absolute; top: 10px; left: 10px; z-index: 1001;">
    <button @onclick="ToggleOverview">Toggle Overview</button>
</div>

@code {
    private HonuaOverviewMap _overview;

    private async Task ToggleOverview()
    {
        // Toggle using public API
        if (_isExpanded)
            await _overview.CollapseAsync();
        else
            await _overview.ExpandAsync();

        _isExpanded = !_isExpanded;
    }

    private bool _isExpanded = true;
}
```

---

## Advanced Examples

### 16. Different Basemap

Use a simplified basemap for clarity:

```razor
<HonuaMap Id="map16"
          MapStyle="https://tiles.stadiamaps.com/styles/outdoors.json"
          Center="new[] { -119.6982, 34.4208 }"
          Zoom="10" />

<!-- Simple grayscale basemap for overview -->
<HonuaOverviewMap SyncWith="map16"
                  OverviewBasemap="https://tiles.stadiamaps.com/styles/alidade_smooth.json" />
```

### 17. Rotate with Main Map

Overview rotates as main map rotates:

```razor
<HonuaMap Id="map17"
          Center="new[] { -122.3321, 47.6062 }"
          Zoom="15"
          Bearing="45" />

<HonuaOverviewMap SyncWith="map17"
                  RotateWithBearing="true"
                  ExtentBoxColor="#FF00FF" />
```

### 18. Dynamic Style Updates

Change extent box style programmatically:

```razor
<HonuaMap Id="map18"
          Center="new[] { 151.2093, -33.8688 }"
          Zoom="13" />

<HonuaOverviewMap @ref="_styledOverview"
                  SyncWith="map18" />

<div style="position: absolute; top: 10px; left: 10px; z-index: 1001;">
    <button @onclick="ChangeToRed">Red Box</button>
    <button @onclick="ChangeToBlue">Blue Box</button>
    <button @onclick="ChangeToGreen">Green Box</button>
</div>

@code {
    private HonuaOverviewMap _styledOverview;

    private async Task ChangeToRed()
    {
        await _styledOverview.UpdateExtentStyleAsync(
            color: "#FF0000",
            fillColor: "#FF0000",
            width: 2,
            opacity: 0.8,
            fillOpacity: 0.1
        );
    }

    private async Task ChangeToBlue()
    {
        await _styledOverview.UpdateExtentStyleAsync(
            color: "#0000FF",
            fillColor: "#0000FF",
            width: 3,
            opacity: 1.0,
            fillOpacity: 0.2
        );
    }

    private async Task ChangeToGreen()
    {
        await _styledOverview.UpdateExtentStyleAsync(
            color: "#00FF00",
            fillColor: "#00FF00",
            width: 2,
            opacity: 0.9,
            fillOpacity: 0.15
        );
    }
}
```

### 19. Mobile-Responsive

Hide on mobile, show on desktop:

```razor
<HonuaMap Id="map19"
          Center="new[] { -3.1883, 55.9533 }"
          Zoom="12" />

<HonuaOverviewMap SyncWith="map19"
                  HideOnMobile="true"
                  MobileBreakpoint="768" />
```

### 20. Performance Optimized

For smooth experience on slower devices:

```razor
<HonuaMap Id="map20"
          Center="new[] { -97.7431, 30.2672 }"
          Zoom="12" />

<HonuaOverviewMap SyncWith="map20"
                  UpdateThrottleMs="200"
                  OverviewBasemap="https://demotiles.maplibre.org/style.json"
                  Width="150"
                  Height="150"
                  InitiallyCollapsed="true" />
```

---

## Real-World Scenarios

### 21. Urban Planning Dashboard

```razor
@page "/urban-planning"

<div class="dashboard">
    <!-- Main map showing detailed view -->
    <HonuaMap Id="urbanMap"
              MapStyle="@_urbanStyle"
              Center="@_cityCenter"
              Zoom="14"
              Style="width: 100%; height: 100vh;" />

    <!-- Large overview for context -->
    <HonuaOverviewMap SyncWith="urbanMap"
                      Width="300"
                      Height="250"
                      Position="bottom-right"
                      ZoomOffset="-6"
                      Title="City Overview"
                      ExtentBoxColor="#4A90E2"
                      ExtentBoxFillOpacity="0.2"
                      BorderRadius="8"
                      BoxShadow="0 4px 16px rgba(0,0,0,0.2)" />
</div>

@code {
    private string _urbanStyle = "https://tiles.stadiamaps.com/styles/outdoors.json";
    private double[] _cityCenter = new[] { -122.4194, 37.7749 };
}
```

### 22. Real Estate Application

```razor
@page "/property-search"

<div class="property-viewer">
    <!-- Detailed neighborhood view -->
    <HonuaMap Id="propertyMap"
              Center="@_propertyLocation"
              Zoom="16"
              Style="width: 100%; height: 100vh;" />

    <!-- Small, collapsible overview -->
    <HonuaOverviewMap SyncWith="propertyMap"
                      Width="180"
                      Height="180"
                      Position="bottom-right"
                      ZoomOffset="-7"
                      InitiallyCollapsed="true"
                      Collapsible="true"
                      ShowToggleButton="true"
                      ExtentBoxColor="#E91E63"
                      Title="Area Overview" />
</div>

@code {
    private double[] _propertyLocation = new[] { -118.2437, 34.0522 };
}
```

### 23. Delivery Tracking

```razor
@page "/delivery-tracking"

<div class="tracking-view">
    <!-- Main map following delivery -->
    <HonuaMap Id="deliveryMap"
              Center="@_deliveryPosition"
              Zoom="15"
              Bearing="@_deliveryBearing"
              Style="width: 100%; height: 100vh;" />

    <!-- Overview with rotation -->
    <HonuaOverviewMap SyncWith="deliveryMap"
                      Width="200"
                      Height="200"
                      Position="top-right"
                      ZoomOffset="-5"
                      RotateWithBearing="true"
                      ExtentBoxColor="#00BCD4"
                      DragToPan="false"
                      Title="Route Overview" />
</div>

@code {
    private double[] _deliveryPosition = new[] { -73.9857, 40.7484 };
    private double _deliveryBearing = 45;
}
```

### 24. Environmental Monitoring

```razor
@page "/environmental-monitor"

<div class="monitor-dashboard">
    <!-- Detailed sensor view -->
    <HonuaMap Id="sensorMap"
              Center="@_sensorCenter"
              Zoom="13"
              Style="width: 100%; height: 100vh;" />

    <!-- Multiple overview scales -->
    <!-- Local area -->
    <HonuaOverviewMap SyncWith="sensorMap"
                      Position="bottom-right"
                      Width="160"
                      Height="160"
                      ZoomOffset="-4"
                      Title="Local"
                      ExtentBoxColor="#4CAF50" />

    <!-- Regional -->
    <HonuaOverviewMap SyncWith="sensorMap"
                      Position="bottom-left"
                      Width="160"
                      Height="160"
                      ZoomOffset="-8"
                      Title="Regional"
                      ExtentBoxColor="#FF9800" />
</div>

@code {
    private double[] _sensorCenter = new[] { -95.3698, 29.7604 };
}
```

### 25. Tourism/Sightseeing App

```razor
@page "/tour-guide"

<div class="tour-app">
    <!-- Close-up of attraction -->
    <HonuaMap Id="tourMap"
              Center="@_attractionLocation"
              Zoom="17"
              Pitch="60"
              Style="width: 100%; height: 100vh;" />

    <!-- City overview for orientation -->
    <HonuaOverviewMap SyncWith="tourMap"
                      Width="220"
                      Height="180"
                      Position="bottom-right"
                      ZoomOffset="-9"
                      Title="You are here"
                      ExtentBoxColor="#E91E63"
                      ExtentBoxWidth="3"
                      BorderRadius="12"
                      ClickToPan="true"
                      DragToPan="false" />
</div>

@code {
    private double[] _attractionLocation = new[] { 2.2945, 48.8584 }; // Eiffel Tower
}
```

### 26. Fleet Management

```razor
@page "/fleet-management"

<div class="fleet-view">
    <!-- Vehicle tracking -->
    <HonuaMap @ref="_fleetMap"
              Id="fleetMap"
              Center="@_fleetCenter"
              Zoom="12"
              Style="width: 100%; height: 100vh;"
              OnExtentChanged="HandleExtentChange" />

    <!-- Responsive overview -->
    <HonuaOverviewMap SyncWith="fleetMap"
                      Width="250"
                      Height="200"
                      Position="top-right"
                      ZoomOffset="-5"
                      HideOnMobile="true"
                      ExtentBoxColor="#673AB7"
                      Title="Fleet Area"
                      ShowControls="false" />
</div>

@code {
    private HonuaMap _fleetMap;
    private double[] _fleetCenter = new[] { -87.6298, 41.8781 };

    private void HandleExtentChange(MapExtentChangedMessage message)
    {
        // Track viewport changes
        Console.WriteLine($"Fleet view changed to zoom {message.Zoom}");
    }
}
```

### 27. Emergency Response

```razor
@page "/emergency-response"

<div class="emergency-dashboard">
    <!-- High-detail incident area -->
    <HonuaMap Id="incidentMap"
              Center="@_incidentLocation"
              Zoom="16"
              Style="width: 100%; height: 100vh;" />

    <!-- Large, prominent overview -->
    <HonuaOverviewMap SyncWith="incidentMap"
                      Width="320"
                      Height="240"
                      Position="top-left"
                      ZoomOffset="-6"
                      Title="Incident Area"
                      ExtentBoxColor="#F44336"
                      ExtentBoxWidth="4"
                      ExtentBoxOpacity="1.0"
                      ExtentBoxFillOpacity="0.25"
                      BorderWidth="2"
                      BorderColor="#D32F2F"
                      BoxShadow="0 4px 20px rgba(244, 67, 54, 0.4)" />
</div>

@code {
    private double[] _incidentLocation = new[] { -122.4194, 37.7749 };
}
```

### 28. Scientific Research

```razor
@page "/research-field-study"

<div class="research-view">
    <!-- Detailed study area -->
    <HonuaMap Id="studyMap"
              MapStyle="@_satelliteStyle"
              Center="@_studyArea"
              Zoom="15"
              Style="width: 100%; height: 100vh;" />

    <!-- Simple basemap overview -->
    <HonuaOverviewMap SyncWith="studyMap"
                      Width="200"
                      Height="200"
                      Position="bottom-right"
                      ZoomOffset="-7"
                      OverviewBasemap="https://tiles.stadiamaps.com/styles/alidade_smooth.json"
                      ExtentBoxColor="#009688"
                      Title="Study Region"
                      ClickToPan="false"
                      DragToPan="false" />
</div>

@code {
    private string _satelliteStyle = "https://tiles.stadiamaps.com/styles/satellite.json";
    private double[] _studyArea = new[] { -106.3468, 56.1304 };
}
```

---

## Tips and Best Practices

### Choosing the Right Size
- **Small (150x150)**: Mobile devices or minimal UI
- **Medium (200x200)**: Default, works well for most cases
- **Large (300x250)**: Dashboards or when overview is important

### Choosing Zoom Offset
- **-3 to -4**: Local context, nearby areas
- **-5 to -6**: City or regional context (default range)
- **-7 to -9**: Large regional or country-level context
- **-10+**: Continental or global context

### Interaction Modes
- **Read-only**: Presentations, public displays
- **Click only**: Touch devices, simple navigation
- **Full interaction**: Desktop apps, power users

### Performance
- Use `UpdateThrottleMs` >= 100ms for smooth updates
- Consider `InitiallyCollapsed="true"` for faster initial load
- Use simpler basemap for overview on mobile devices

### Accessibility
- Always include `Title` for screen readers
- Use high-contrast extent box colors
- Enable keyboard navigation with `Collapsible="true"`

---

## Complete Application Example

```razor
@page "/complete-example"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.OverviewMap
@using Honua.MapSDK.Components.Legend
@using Honua.MapSDK.Core.Messages

<div class="map-application">
    <!-- Main map -->
    <HonuaMap @ref="_mainMap"
              Id="mainMap"
              Center="@_center"
              Zoom="@_zoom"
              MapStyle="@_currentStyle"
              OnExtentChanged="HandleExtentChanged"
              Style="width: 100%; height: 100vh;" />

    <!-- Overview map -->
    <HonuaOverviewMap @ref="_overview"
                      SyncWith="mainMap"
                      Width="@_overviewWidth"
                      Height="@_overviewHeight"
                      Position="bottom-right"
                      ZoomOffset="@_zoomOffset"
                      ExtentBoxColor="@_extentColor"
                      ExtentBoxFillOpacity="0.15"
                      InitiallyCollapsed="@_startCollapsed"
                      OnOverviewClicked="HandleOverviewClick" />

    <!-- Control panel -->
    <div class="controls">
        <h3>Overview Controls</h3>

        <label>
            Size:
            <select @onchange="ChangeSize">
                <option value="150">Small</option>
                <option value="200" selected>Medium</option>
                <option value="300">Large</option>
            </select>
        </label>

        <label>
            Zoom Offset:
            <input type="range" min="-10" max="-2" @bind="_zoomOffset" />
            <span>@_zoomOffset</span>
        </label>

        <label>
            Extent Color:
            <input type="color" @bind="_extentColor" />
        </label>

        <button @onclick="ToggleOverview">
            @(_isExpanded ? "Collapse" : "Expand") Overview
        </button>

        <button @onclick="ChangeExtentColor">
            Random Color
        </button>
    </div>

    <!-- Status display -->
    <div class="status">
        <p>Center: @_center[0]:F4, @_center[1]:F4</p>
        <p>Zoom: @_zoom:F2</p>
        <p>Last click: @_lastClick</p>
    </div>
</div>

@code {
    private HonuaMap _mainMap;
    private HonuaOverviewMap _overview;

    private double[] _center = new[] { -122.4194, 37.7749 };
    private double _zoom = 12;
    private string _currentStyle = "https://demotiles.maplibre.org/style.json";

    private int _overviewWidth = 200;
    private int _overviewHeight = 200;
    private int _zoomOffset = -5;
    private string _extentColor = "#FF4444";
    private bool _startCollapsed = false;
    private bool _isExpanded = true;
    private string _lastClick = "None";

    private void HandleExtentChanged(MapExtentChangedMessage message)
    {
        _center = message.Center;
        _zoom = message.Zoom;
        StateHasChanged();
    }

    private void HandleOverviewClick(OverviewMapClickedMessage message)
    {
        _lastClick = $"{message.Center[0]:F4}, {message.Center[1]:F4}";
        StateHasChanged();
    }

    private async Task ToggleOverview()
    {
        if (_isExpanded)
            await _overview.CollapseAsync();
        else
            await _overview.ExpandAsync();

        _isExpanded = !_isExpanded;
    }

    private void ChangeSize(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out int size))
        {
            _overviewWidth = size;
            _overviewHeight = size;
        }
    }

    private async Task ChangeExtentColor()
    {
        var random = new Random();
        var color = $"#{random.Next(0x1000000):X6}";

        await _overview.UpdateExtentStyleAsync(
            color: color,
            fillColor: color
        );
    }
}

<style>
    .map-application {
        position: relative;
        width: 100%;
        height: 100vh;
    }

    .controls {
        position: absolute;
        top: 10px;
        left: 10px;
        background: white;
        padding: 15px;
        border-radius: 8px;
        box-shadow: 0 2px 8px rgba(0,0,0,0.2);
        z-index: 1000;
        max-width: 250px;
    }

    .controls h3 {
        margin-top: 0;
    }

    .controls label {
        display: block;
        margin: 10px 0;
    }

    .controls button {
        margin: 5px 0;
        padding: 8px 12px;
        width: 100%;
    }

    .status {
        position: absolute;
        top: 10px;
        right: 10px;
        background: rgba(0, 0, 0, 0.7);
        color: white;
        padding: 10px;
        border-radius: 4px;
        font-size: 12px;
        font-family: monospace;
        z-index: 1000;
    }

    .status p {
        margin: 5px 0;
    }
</style>
```

---

For more information, see the [README.md](./README.md) documentation.
