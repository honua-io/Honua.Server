# Tutorial 06: Advanced Map Styling and Theming

> **Learning Objectives**: Master advanced styling techniques including custom map styles, layer styling, conditional rendering, dark mode, component theming, and branding.

---

## Prerequisites

- Completed previous tutorials OR MapSDK basics
- .NET 8.0 SDK
- Understanding of CSS and MapLibre styles

**Estimated Time**: 45 minutes

---

## Table of Contents

1. [Custom Map Styles](#1-custom-map-styles)
2. [Layer Styling](#2-layer-styling)
3. [Conditional Rendering](#3-conditional-rendering)
4. [Dark Mode Theming](#4-dark-mode-theming)
5. [Custom Component Styling](#5-custom-component-styling)
6. [Branding Your App](#6-branding-your-app)

---

## 1. Custom Map Styles

### Create Custom MapLibre Style

Create `wwwroot/styles/custom-map-style.json`:

```json
{
  "version": 8,
  "name": "Custom Style",
  "sources": {
    "openmaptiles": {
      "type": "vector",
      "tiles": ["https://tiles.example.com/{z}/{x}/{y}.pbf"],
      "minzoom": 0,
      "maxzoom": 14
    }
  },
  "layers": [
    {
      "id": "background",
      "type": "background",
      "paint": {
        "background-color": "#f8f8f8"
      }
    },
    {
      "id": "water",
      "type": "fill",
      "source": "openmaptiles",
      "source-layer": "water",
      "paint": {
        "fill-color": "#4A90E2",
        "fill-opacity": 0.6
      }
    },
    {
      "id": "roads",
      "type": "line",
      "source": "openmaptiles",
      "source-layer": "transportation",
      "paint": {
        "line-color": "#FFFFFF",
        "line-width": {
          "stops": [[10, 1], [15, 3], [18, 8]]
        }
      }
    },
    {
      "id": "buildings",
      "type": "fill-extrusion",
      "source": "openmaptiles",
      "source-layer": "building",
      "paint": {
        "fill-extrusion-color": "#aaa",
        "fill-extrusion-height": ["get", "height"],
        "fill-extrusion-base": ["get", "min_height"],
        "fill-extrusion-opacity": 0.8
      }
    }
  ]
}
```

### Use Custom Style in Application

```razor
<HonuaMap Id="styled-map"
          MapStyle="/styles/custom-map-style.json"
          Center="@(new[] { -122.4194, 37.7749 })"
          Zoom="13" />
```

---

## 2. Layer Styling

### Data-Driven Layer Styling

```razor
@page "/styled-layers"
@using Honua.MapSDK.Components

<HonuaMap @ref="_map" Id="layers-map" OnMapReady="@HandleMapReady" />

@code {
    private HonuaMap? _map;

    private async Task HandleMapReady(MapReadyMessage message)
    {
        // Add source
        await _map!.AddSourceAsync("properties", new
        {
            type = "geojson",
            data = "/data/properties.geojson"
        });

        // Style by property value
        await _map.AddLayerAsync(new
        {
            id = "property-circles",
            type = "circle",
            source = "properties",
            paint = new
            {
                // Color based on property value
                circle_color = new object[]
                {
                    "interpolate",
                    new[] { "linear" },
                    new[] { "get", "price" },
                    0, "#2196F3",          // Blue for low
                    500000, "#FFC107",     // Yellow for medium
                    1000000, "#F44336"     // Red for high
                },
                // Size based on square feet
                circle_radius = new object[]
                {
                    "interpolate",
                    new[] { "linear" },
                    new[] { "get", "sqft" },
                    500, 5,
                    2000, 10,
                    5000, 20
                },
                // Opacity based on status
                circle_opacity = new object[]
                {
                    "match",
                    new[] { "get", "status" },
                    "Active", 1.0,
                    "Pending", 0.7,
                    "Sold", 0.3,
                    0.5  // default
                },
                circle_stroke_width = 2,
                circle_stroke_color = "#FFFFFF"
            }
        });
    }
}
```

### Pattern Fills and Textures

```csharp
// Add pattern image
await _map.LoadImageAsync("pattern-dots", "/images/pattern-dots.png");

// Use pattern in layer
await _map.AddLayerAsync(new
{
    id = "parks",
    type = "fill",
    source = "parks-source",
    paint = new
    {
        fill_pattern = "pattern-dots",
        fill_opacity = 0.6
    }
});
```

---

## 3. Conditional Rendering

### Feature Filtering

```csharp
// Filter by zoom level
await _map.AddLayerAsync(new
{
    id = "labels",
    type = "symbol",
    source = "places",
    minzoom = 12,
    maxzoom = 18,
    filter = new object[]
    {
        "all",
        new object[] { ">=", new[] { "get", "population" }, 10000 },
        new object[] { "==", new[] { "get", "type" }, "city" }
    },
    layout = new
    {
        text_field = new[] { "get", "name" },
        text_size = 14
    },
    paint = new
    {
        text_color = "#000000",
        text_halo_color = "#FFFFFF",
        text_halo_width = 2
    }
});
```

### Dynamic Visibility

```razor
<MudButtonGroup>
    <MudButton OnClick="@(() => ToggleLayer("buildings"))">Buildings</MudButton>
    <MudButton OnClick="@(() => ToggleLayer("roads"))">Roads</MudButton>
    <MudButton OnClick="@(() => ToggleLayer("labels"))">Labels</MudButton>
</MudButtonGroup>

<HonuaMap @ref="_map" Id="visibility-map" />

@code {
    private HonuaMap? _map;
    private Dictionary<string, bool> _layerVisibility = new()
    {
        { "buildings", true },
        { "roads", true },
        { "labels", true }
    };

    private async Task ToggleLayer(string layerId)
    {
        _layerVisibility[layerId] = !_layerVisibility[layerId];
        await _map!.SetLayerVisibilityAsync(layerId, _layerVisibility[layerId] ? "visible" : "none");
    }
}
```

---

## 4. Dark Mode Theming

### Complete Dark Mode Implementation

```razor
@page "/dark-mode"
@using Honua.MapSDK.Components

<MudThemeProvider @bind-IsDarkMode="@_isDarkMode" Theme="@_theme" />
<MudToggleIconButton @bind-Toggled="@_isDarkMode"
                     Icon="@Icons.Material.Filled.LightMode"
                     ToggledIcon="@Icons.Material.Filled.DarkMode"
                     ToggledChanged="@OnDarkModeChanged" />

<HonuaMap @ref="_map"
          Id="themed-map"
          MapStyle="@_currentMapStyle" />

@code {
    private HonuaMap? _map;
    private bool _isDarkMode = false;
    private string _currentMapStyle = "https://demotiles.maplibre.org/style.json";

    private MudTheme _theme = new()
    {
        Palette = new PaletteLight
        {
            Primary = "#4A90E2",
            Secondary = "#6C5CE7",
            Background = "#FFFFFF",
            Surface = "#F5F5F5",
            AppbarBackground = "#4A90E2"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#4A90E2",
            Secondary = "#6C5CE7",
            Background = "#1E1E1E",
            Surface = "#2D2D2D",
            AppbarBackground = "#1E1E1E"
        }
    };

    private async Task OnDarkModeChanged(bool isDark)
    {
        // Switch map style
        _currentMapStyle = isDark
            ? "https://basemaps.cartocdn.com/gl/dark-matter-gl-style/style.json"
            : "https://demotiles.maplibre.org/style.json";

        if (_map != null)
        {
            await _map.SetMapStyleAsync(_currentMapStyle);
        }
    }
}
```

### Custom Dark Map Style

Create `wwwroot/styles/dark-style.json`:

```json
{
  "version": 8,
  "name": "Dark Theme",
  "layers": [
    {
      "id": "background",
      "type": "background",
      "paint": {
        "background-color": "#1E1E1E"
      }
    },
    {
      "id": "water",
      "type": "fill",
      "paint": {
        "fill-color": "#2D4A66"
      }
    },
    {
      "id": "roads",
      "type": "line",
      "paint": {
        "line-color": "#3D3D3D",
        "line-width": 2
      }
    }
  ]
}
```

---

## 5. Custom Component Styling

### Style MudBlazor Components

```razor
<style>
    /* Custom map container */
    .custom-map-container {
        border-radius: 12px;
        box-shadow: 0 8px 16px rgba(0, 0, 0, 0.2);
        overflow: hidden;
    }

    /* Custom data grid */
    .custom-datagrid {
        border-radius: 8px;
    }

    .custom-datagrid .mud-table-cell {
        font-family: 'Roboto Mono', monospace;
    }

    /* Custom chart */
    .custom-chart {
        background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        border-radius: 12px;
        padding: 20px;
    }

    /* Custom legend */
    .custom-legend {
        backdrop-filter: blur(10px);
        background: rgba(255, 255, 255, 0.9);
        border-radius: 8px;
        padding: 12px;
        box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
    }
</style>

<div class="custom-map-container">
    <HonuaMap Id="styled-map" CssClass="custom-map" />
</div>

<HonuaDataGrid TItem="Feature"
               Items="@_features"
               CssClass="custom-datagrid" />

<HonuaChart Type="ChartType.Line"
            CssClass="custom-chart" />

<HonuaLegend SyncWith="styled-map"
             CssClass="custom-legend" />
```

### Custom Markers and Icons

```csharp
// Add custom marker with HTML
await _map.AddCustomMarkerAsync(new
{
    id = "custom-marker-1",
    coordinates = new[] { -122.4194, 37.7749 },
    html = @"
        <div class='custom-marker'>
            <div class='marker-icon'>🏠</div>
            <div class='marker-label'>$750,000</div>
        </div>
    "
});
```

```css
<style>
    .custom-marker {
        display: flex;
        flex-direction: column;
        align-items: center;
        cursor: pointer;
        transition: transform 0.2s;
    }

    .custom-marker:hover {
        transform: scale(1.2);
    }

    .marker-icon {
        font-size: 32px;
        filter: drop-shadow(0 2px 4px rgba(0, 0, 0, 0.3));
    }

    .marker-label {
        background: white;
        padding: 4px 8px;
        border-radius: 4px;
        font-weight: bold;
        font-size: 12px;
        box-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
        margin-top: 4px;
    }
</style>
```

---

## 6. Branding Your App

### Complete Branded Application

```razor
@page "/branded"

<MudThemeProvider Theme="@_brandTheme" />
<CascadingValue Value="@_brandTheme">
    <MudLayout>
        <MudAppBar Elevation="1" Dense="true">
            <img src="/images/logo.svg" height="32" class="mr-3" />
            <MudText Typo="Typo.h6">Acme Real Estate</MudText>
            <MudSpacer />
            <MudIconButton Icon="@Icons.Material.Filled.Menu" Color="Color.Inherit" />
        </MudAppBar>

        <MudMainContent Class="pa-0">
            <div style="height: 100vh; background: linear-gradient(180deg, #667eea 0%, #764ba2 100%);">
                <MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="pt-4">
                    <MudPaper Elevation="4" Class="branded-card pa-4">
                        <HonuaMap Id="branded-map"
                                  MapStyle="@_brandedMapStyle"
                                  Center="@(new[] { -122.4194, 37.7749 })"
                                  Zoom="12"
                                  Style="height: 600px; border-radius: 12px;" />
                    </MudPaper>
                </MudContainer>
            </div>
        </MudMainContent>
    </MudLayout>
</CascadingValue>

<style>
    :root {
        --brand-primary: #667eea;
        --brand-secondary: #764ba2;
        --brand-accent: #f093fb;
    }

    .branded-card {
        border-radius: 16px;
        background: rgba(255, 255, 255, 0.95);
        backdrop-filter: blur(10px);
    }

    .mud-appbar {
        background: linear-gradient(90deg, var(--brand-primary) 0%, var(--brand-secondary) 100%) !important;
    }

    .mud-button-filled-primary {
        background: linear-gradient(90deg, var(--brand-primary) 0%, var(--brand-secondary) 100%);
    }

    .mud-chip-primary {
        background: var(--brand-accent);
        color: white;
    }
</style>

@code {
    private MudTheme _brandTheme = new()
    {
        Palette = new PaletteLight
        {
            Primary = "#667eea",
            Secondary = "#764ba2",
            AppbarBackground = "#667eea",
            AppbarText = "#FFFFFF",
            DrawerBackground = "#FFFFFF"
        },
        Typography = new Typography
        {
            Default = new Default
            {
                FontFamily = new[] { "Inter", "Helvetica", "Arial", "sans-serif" },
                FontSize = "14px"
            },
            H1 = new H1 { FontSize = "3rem", FontWeight = 700 },
            H2 = new H2 { FontSize = "2.5rem", FontWeight = 600 },
            H6 = new H6 { FontSize = "1.25rem", FontWeight = 600 }
        }
    };

    private string _brandedMapStyle = "/styles/branded-map-style.json";
}
```

### Custom Brand Colors in Map Layers

```csharp
await _map.AddLayerAsync(new
{
    id = "branded-properties",
    type = "circle",
    source = "properties",
    paint = new
    {
        circle_color = "#667eea",  // Brand primary
        circle_radius = 10,
        circle_stroke_width = 3,
        circle_stroke_color = "#764ba2"  // Brand secondary
    }
});
```

---

## Complete Styling Example

### Full-Featured Styled Application

```razor
@page "/styling-demo"
@using Honua.MapSDK.Components

<MudThemeProvider @bind-IsDarkMode="@_isDarkMode" Theme="@_customTheme" />
<MudDialogProvider />
<MudSnackbarProvider />

<MudLayout>
    <MudAppBar Elevation="2">
        <MudIconButton Icon="@Icons.Material.Filled.Menu" Color="Color.Inherit" OnClick="@(() => _drawerOpen = !_drawerOpen)" />
        <MudText Typo="Typo.h6">Styled Map Application</MudText>
        <MudSpacer />
        <MudToggleIconButton @bind-Toggled="@_isDarkMode"
                             Icon="@Icons.Material.Filled.LightMode"
                             ToggledIcon="@Icons.Material.Filled.DarkMode"
                             ToggledChanged="@OnThemeChanged" />
    </MudAppBar>

    <MudDrawer @bind-Open="@_drawerOpen" Elevation="1">
        <MudDrawerHeader>
            <MudText Typo="Typo.h6">Map Layers</MudText>
        </MudDrawerHeader>
        <MudDivider />
        @foreach (var layer in _layers)
        {
            <MudNavLink @onclick="@(() => ToggleLayer(layer.Id))">
                <MudCheckBox Checked="@layer.Visible" Label="@layer.Name" Color="Color.Primary" />
            </MudNavLink>
        }
    </MudDrawer>

    <MudMainContent>
        <div style="height: 100vh;">
            <HonuaMap @ref="_map"
                      Id="demo-map"
                      MapStyle="@_currentStyle"
                      OnMapReady="@HandleMapReady"
                      Style="height: 100%;" />
        </div>
    </MudMainContent>
</MudLayout>

@code {
    private HonuaMap? _map;
    private bool _isDarkMode = false;
    private bool _drawerOpen = true;
    private string _currentStyle = "https://demotiles.maplibre.org/style.json";

    private MudTheme _customTheme = new()
    {
        Palette = new PaletteLight
        {
            Primary = "#4A90E2",
            Secondary = "#6C5CE7",
            Background = "#F8F9FA",
            Surface = "#FFFFFF"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#4A90E2",
            Secondary = "#6C5CE7",
            Background = "#121212",
            Surface = "#1E1E1E"
        }
    };

    private List<LayerInfo> _layers = new()
    {
        new LayerInfo { Id = "buildings", Name = "Buildings", Visible = true },
        new LayerInfo { Id = "roads", Name = "Roads", Visible = true },
        new LayerInfo { Id = "water", Name = "Water", Visible = true }
    };

    private async Task HandleMapReady(MapReadyMessage message)
    {
        await ApplyCustomStyling();
    }

    private async Task ApplyCustomStyling()
    {
        // Apply custom styling to layers
    }

    private async Task OnThemeChanged(bool isDark)
    {
        _currentStyle = isDark
            ? "https://basemaps.cartocdn.com/gl/dark-matter-gl-style/style.json"
            : "https://demotiles.maplibre.org/style.json";

        if (_map != null)
        {
            await _map.SetMapStyleAsync(_currentStyle);
        }
    }

    private async Task ToggleLayer(string layerId)
    {
        var layer = _layers.FirstOrDefault(l => l.Id == layerId);
        if (layer != null)
        {
            layer.Visible = !layer.Visible;
            if (_map != null)
            {
                await _map.SetLayerVisibilityAsync(layerId, layer.Visible ? "visible" : "none");
            }
        }
    }

    private class LayerInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool Visible { get; set; }
    }
}
```

---

## What You Learned

✅ **Custom map styles** with MapLibre
✅ **Data-driven styling** for layers
✅ **Conditional rendering** and filtering
✅ **Dark mode** implementation
✅ **Component theming** with MudBlazor
✅ **Branding** your application
✅ **Custom markers** and icons

---

## Best Practices

1. **Performance**: Use vector tiles for custom styles
2. **Consistency**: Maintain style guide across components
3. **Accessibility**: Ensure sufficient color contrast
4. **Responsiveness**: Test styles at different screen sizes
5. **Caching**: Cache map styles and images

---

## Next Steps

- 📖 [Performance Optimization Guide](../guides/PerformanceOptimization.md)
- 📖 [Accessibility Guide](../guides/Accessibility.md)
- 📖 [Component Documentation](../components/overview.md)

---

**Congratulations!** You've mastered advanced styling techniques!

*Last Updated: 2025-11-06*
*MapSDK Version: 1.0.0*
