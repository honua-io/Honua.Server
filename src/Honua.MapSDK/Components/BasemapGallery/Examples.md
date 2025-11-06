# HonuaBasemapGallery Examples

Complete examples demonstrating various uses of the HonuaBasemapGallery component.

## Table of Contents

1. [Basic Examples](#basic-examples)
2. [Layout Examples](#layout-examples)
3. [Custom Basemaps](#custom-basemaps)
4. [Advanced Features](#advanced-features)
5. [Integration Examples](#integration-examples)
6. [Real-World Scenarios](#real-world-scenarios)

---

## Basic Examples

### Example 1: Simple Basemap Gallery

```razor
@page "/simple-basemap"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.BasemapGallery

<PageTitle>Simple Basemap Gallery</PageTitle>

<div style="width: 100%; height: 100vh; position: relative;">
    <HonuaMap Id="map1"
              Center="@(new[] { -122.4194, 37.7749 })"
              Zoom="12" />

    <HonuaBasemapGallery SyncWith="map1"
                         Position="top-right" />
</div>
```

### Example 2: Embedded Gallery in Sidebar

```razor
@page "/sidebar-basemap"

<MudLayout>
    <MudDrawer Open="true" Variant="DrawerVariant.Persistent" Width="300px">
        <MudDrawerHeader>
            <MudText Typo="Typo.h6">Map Controls</MudText>
        </MudDrawerHeader>

        <MudDivider />

        <!-- Basemap Gallery in sidebar -->
        <HonuaBasemapGallery SyncWith="mainMap"
                             Layout="list"
                             ShowCategories="false"
                             Title="Choose Basemap" />

        <MudDivider />

        <!-- Other controls... -->
    </MudDrawer>

    <MudMainContent>
        <div style="width: 100%; height: 100vh;">
            <HonuaMap Id="mainMap" />
        </div>
    </MudMainContent>
</MudLayout>
```

### Example 3: Compact Dropdown

```razor
@page "/compact-basemap"

<div style="width: 100%; height: 100vh; position: relative;">
    <HonuaMap Id="map1" />

    <!-- Compact dropdown in toolbar -->
    <MudPaper Class="map-toolbar" Style="position: absolute; top: 10px; left: 10px; padding: 8px;">
        <div style="display: flex; gap: 8px; align-items: center;">
            <HonuaBasemapGallery SyncWith="map1"
                                 Layout="dropdown"
                                 ShowCategories="false" />

            <MudButton Variant="Variant.Filled" Color="Color.Primary">
                Export
            </MudButton>
        </div>
    </MudPaper>
</div>
```

---

## Layout Examples

### Example 4: All Layout Modes

```razor
@page "/basemap-layouts"

<MudTabs Elevation="2" Rounded="true" ApplyEffectsToContainer="true">
    <MudTabPanel Text="Grid">
        <div style="width: 100%; height: 600px; position: relative;">
            <HonuaMap Id="gridMap" />
            <HonuaBasemapGallery SyncWith="gridMap"
                                 Layout="grid"
                                 Position="top-right" />
        </div>
    </MudTabPanel>

    <MudTabPanel Text="List">
        <div style="width: 100%; height: 600px; position: relative;">
            <HonuaMap Id="listMap" />
            <HonuaBasemapGallery SyncWith="listMap"
                                 Layout="list"
                                 Position="top-left" />
        </div>
    </MudTabPanel>

    <MudTabPanel Text="Dropdown">
        <div style="width: 100%; height: 600px; position: relative;">
            <HonuaMap Id="dropdownMap" />
            <HonuaBasemapGallery SyncWith="dropdownMap"
                                 Layout="dropdown"
                                 Position="top-right" />
        </div>
    </MudTabPanel>

    <MudTabPanel Text="Floating">
        <div style="width: 100%; height: 600px; position: relative;">
            <HonuaMap Id="floatingMap" />
            <HonuaBasemapGallery SyncWith="floatingMap"
                                 Layout="floating"
                                 Position="bottom-right" />
        </div>
    </MudTabPanel>

    <MudTabPanel Text="Modal">
        <div style="width: 100%; height: 600px; position: relative;">
            <HonuaMap Id="modalMap" />
            <HonuaBasemapGallery SyncWith="modalMap"
                                 Layout="modal"
                                 Position="top-right" />
        </div>
    </MudTabPanel>
</MudTabs>
```

### Example 5: Responsive Layout

```razor
@page "/responsive-basemap"
@inject IBreakpointService BreakpointService

<div style="width: 100%; height: 100vh; position: relative;">
    <HonuaMap Id="map1" />

    <HonuaBasemapGallery SyncWith="map1"
                         Layout="@_currentLayout"
                         Position="@_currentPosition" />
</div>

@code {
    private string _currentLayout = "grid";
    private string _currentPosition = "top-right";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            var breakpoint = await BreakpointService.GetBreakpoint();

            _currentLayout = breakpoint switch
            {
                Breakpoint.Xs => "dropdown",
                Breakpoint.Sm => "list",
                _ => "grid"
            };

            _currentPosition = breakpoint <= Breakpoint.Sm ? "top-left" : "top-right";
            StateHasChanged();
        }
    }
}
```

---

## Custom Basemaps

### Example 6: Add Custom Basemaps

```razor
@page "/custom-basemaps"
@using Honua.MapSDK.Models
@using Honua.MapSDK.Services

<div style="width: 100%; height: 100vh; position: relative;">
    <HonuaMap Id="map1" />

    <HonuaBasemapGallery SyncWith="map1"
                         Basemaps="@_customBasemaps"
                         Position="top-right" />
</div>

@code {
    private List<Basemap> _customBasemaps = new();

    protected override void OnInitialized()
    {
        _customBasemaps = new List<Basemap>
        {
            new Basemap
            {
                Id = "company-light",
                Name = "Company Light",
                Category = BasemapCategories.Streets,
                StyleUrl = "https://tiles.company.com/light/style.json",
                ThumbnailUrl = "/assets/basemaps/company-light.png",
                Provider = "Company Name",
                Description = "Light theme with company branding",
                Tags = new List<string> { "branded", "light", "custom" },
                SortOrder = 1
            },
            new Basemap
            {
                Id = "company-dark",
                Name = "Company Dark",
                Category = BasemapCategories.Streets,
                StyleUrl = "https://tiles.company.com/dark/style.json",
                ThumbnailUrl = "/assets/basemaps/company-dark.png",
                Provider = "Company Name",
                Description = "Dark theme with company branding",
                Tags = new List<string> { "branded", "dark", "custom" },
                SortOrder = 2
            },
            new Basemap
            {
                Id = "project-specific",
                Name = "Project Area",
                Category = BasemapCategories.Specialty,
                StyleUrl = "https://tiles.project.com/area/style.json",
                ThumbnailUrl = "/assets/basemaps/project-area.png",
                Provider = "Project Team",
                Description = "High-detail basemap for project area",
                Tags = new List<string> { "project", "detailed" },
                SortOrder = 10
            }
        };
    }
}
```

### Example 7: Mix Built-in and Custom

```razor
@page "/mixed-basemaps"

<div style="width: 100%; height: 100vh; position: relative;">
    <HonuaMap Id="map1" />

    <HonuaBasemapGallery SyncWith="map1"
                         Basemaps="@_allBasemaps"
                         Position="top-right" />
</div>

@code {
    private BasemapService _basemapService = new();
    private List<Basemap> _allBasemaps = new();

    protected override void OnInitialized()
    {
        // Start with built-in basemaps
        _allBasemaps = _basemapService.GetBasemaps();

        // Add custom basemaps
        _allBasemaps.AddRange(new[]
        {
            new Basemap
            {
                Id = "custom-1",
                Name = "Custom Basemap 1",
                Category = BasemapCategories.Custom,
                StyleUrl = "https://example.com/style1.json",
                ThumbnailUrl = "/assets/custom1.png",
                Provider = "Custom Provider",
                SortOrder = 100
            },
            new Basemap
            {
                Id = "custom-2",
                Name = "Custom Basemap 2",
                Category = BasemapCategories.Custom,
                StyleUrl = "https://example.com/style2.json",
                ThumbnailUrl = "/assets/custom2.png",
                Provider = "Custom Provider",
                SortOrder = 101
            }
        });
    }
}
```

---

## Advanced Features

### Example 8: With Search and Favorites

```razor
@page "/advanced-basemap"

<div style="width: 100%; height: 100vh; position: relative;">
    <HonuaMap Id="map1" />

    <HonuaBasemapGallery SyncWith="map1"
                         Position="top-right"
                         ShowSearch="true"
                         ShowFavorites="true"
                         Layout="floating" />
</div>
```

### Example 9: Category Filtering

```razor
@page "/filtered-basemaps"

<div style="width: 100%; height: 100vh; position: relative;">
    <HonuaMap Id="map1" />

    <!-- Only show Streets and Satellite basemaps -->
    <HonuaBasemapGallery SyncWith="map1"
                         Position="top-right"
                         Categories="@(new[] { "Streets", "Satellite" })" />
</div>
```

### Example 10: With Opacity Control

```razor
@page "/basemap-opacity"

<div style="width: 100%; height: 100vh; position: relative;">
    <HonuaMap Id="map1" />

    <!-- Add data overlays -->
    <!-- ... -->

    <!-- Basemap with opacity control to blend with overlays -->
    <HonuaBasemapGallery SyncWith="map1"
                         Position="top-right"
                         ShowOpacitySlider="true"
                         DefaultBasemap="carto-positron" />
</div>
```

### Example 11: Hover Preview

```razor
@page "/preview-basemap"

<div style="width: 100%; height: 100vh; position: relative;">
    <HonuaMap Id="map1" />

    <HonuaBasemapGallery SyncWith="map1"
                         Position="top-right"
                         EnablePreview="true"
                         Layout="floating" />
</div>

<MudAlert Severity="Severity.Info" Class="mt-4">
    Hover over basemap thumbnails to preview them on the map before selecting.
</MudAlert>
```

---

## Integration Examples

### Example 12: With Legend and Other Components

```razor
@page "/full-map-interface"
@using Honua.MapSDK.Components.Legend
@using Honua.MapSDK.Components.Search

<div style="width: 100%; height: 100vh; position: relative;">
    <HonuaMap Id="mainMap" />

    <!-- Basemap Gallery -->
    <HonuaBasemapGallery SyncWith="mainMap"
                         Position="top-right"
                         Layout="floating" />

    <!-- Legend -->
    <HonuaLegend SyncWith="mainMap"
                 Position="bottom-right"
                 Collapsible="true" />

    <!-- Search -->
    <HonuaSearch SyncWith="mainMap"
                 Position="top-left" />
</div>
```

### Example 13: Event Handling

```razor
@page "/basemap-events"

<div style="width: 100%; height: 100vh; position: relative;">
    <HonuaMap Id="map1" />

    <HonuaBasemapGallery SyncWith="map1"
                         Position="top-right"
                         OnBasemapChanged="@HandleBasemapChanged" />
</div>

<MudPaper Class="pa-4 mt-4">
    <MudText Typo="Typo.h6">Basemap Change Log</MudText>
    <MudList>
        @foreach (var log in _changeLog)
        {
            <MudListItem>
                <MudText Typo="Typo.body2">@log</MudText>
            </MudListItem>
        }
    </MudList>
</MudPaper>

@code {
    private List<string> _changeLog = new();

    private void HandleBasemapChanged(Basemap basemap)
    {
        var logEntry = $"{DateTime.Now:HH:mm:ss} - Changed to {basemap.Name} ({basemap.Provider})";
        _changeLog.Insert(0, logEntry);

        // Keep only last 10 entries
        if (_changeLog.Count > 10)
        {
            _changeLog.RemoveAt(_changeLog.Count - 1);
        }

        StateHasChanged();
    }
}
```

### Example 14: Programmatic Control

```razor
@page "/programmatic-basemap"

<div style="width: 100%; height: 100vh; position: relative;">
    <HonuaMap Id="map1" @ref="_map" />

    <HonuaBasemapGallery @ref="_gallery"
                         SyncWith="map1"
                         Position="top-right" />
</div>

<MudPaper Class="pa-4 mt-4">
    <MudText Typo="Typo.h6" Class="mb-4">Quick Basemap Switcher</MudText>

    <MudButtonGroup>
        <MudButton OnClick="@(() => SwitchToBasemap("osm-standard"))"
                   Variant="Variant.Filled"
                   Color="Color.Primary">
            OpenStreetMap
        </MudButton>
        <MudButton OnClick="@(() => SwitchToBasemap("carto-dark-matter"))"
                   Variant="Variant.Filled"
                   Color="Color.Secondary">
            Dark Mode
        </MudButton>
        <MudButton OnClick="@(() => SwitchToBasemap("esri-world-imagery"))"
                   Variant="Variant.Filled"
                   Color="Color.Tertiary">
            Satellite
        </MudButton>
    </MudButtonGroup>
</MudPaper>

@code {
    private HonuaMap? _map;
    private HonuaBasemapGallery? _gallery;

    private async Task SwitchToBasemap(string basemapId)
    {
        if (_gallery != null)
        {
            await _gallery.SelectBasemapAsync(basemapId);
        }
    }
}
```

---

## Real-World Scenarios

### Example 15: Dashboard with Multiple Maps

```razor
@page "/multi-map-dashboard"

<MudGrid>
    <MudItem xs="12" md="6">
        <MudPaper Class="pa-2" Style="height: 400px; position: relative;">
            <HonuaMap Id="map1" />
            <HonuaBasemapGallery SyncWith="map1"
                                 Layout="dropdown"
                                 Position="top-right" />
        </MudPaper>
    </MudItem>

    <MudItem xs="12" md="6">
        <MudPaper Class="pa-2" Style="height: 400px; position: relative;">
            <HonuaMap Id="map2" />
            <HonuaBasemapGallery SyncWith="map2"
                                 Layout="dropdown"
                                 Position="top-right" />
        </MudPaper>
    </MudItem>

    <MudItem xs="12">
        <MudPaper Class="pa-2" Style="height: 400px; position: relative;">
            <HonuaMap Id="map3" />
            <HonuaBasemapGallery SyncWith="map3"
                                 Layout="floating"
                                 Position="bottom-right" />
        </MudPaper>
    </MudItem>
</MudGrid>
```

### Example 16: Print-Ready Map

```razor
@page "/print-map"

<div style="width: 100%; height: 100vh; position: relative;">
    <HonuaMap Id="printMap"
              @ref="_map" />

    <HonuaBasemapGallery @ref="_gallery"
                         SyncWith="printMap"
                         Position="top-right"
                         Layout="floating" />

    <MudFab Color="Color.Primary"
            Icon="@Icons.Material.Filled.Print"
            Style="position: absolute; bottom: 20px; right: 20px;"
            OnClick="@PrepareForPrint" />
</div>

@code {
    private HonuaMap? _map;
    private HonuaBasemapGallery? _gallery;

    private async Task PrepareForPrint()
    {
        // Switch to print-friendly basemap
        if (_gallery != null)
        {
            await _gallery.SelectBasemapAsync("carto-positron");
        }

        // Wait for basemap to load
        await Task.Delay(1000);

        // Trigger print
        // await JS.InvokeVoidAsync("window.print");
    }
}
```

### Example 17: Mobile-First Design

```razor
@page "/mobile-map"
@inject IBreakpointService BreakpointService

<div style="width: 100%; height: 100vh; position: relative;">
    <HonuaMap Id="mobileMap" />

    @if (_isMobile)
    {
        <!-- Bottom sheet on mobile -->
        <MudDrawer @bind-Open="_drawerOpen"
                   Anchor="Anchor.Bottom"
                   Variant="DrawerVariant.Temporary"
                   Style="height: 50%;">
            <HonuaBasemapGallery SyncWith="mobileMap"
                                 Layout="grid"
                                 ShowCategories="false" />
        </MudDrawer>

        <MudFab Color="Color.Primary"
                Icon="@Icons.Material.Filled.Map"
                Style="position: absolute; bottom: 20px; right: 20px;"
                OnClick="@(() => _drawerOpen = true)" />
    }
    else
    {
        <!-- Floating panel on desktop -->
        <HonuaBasemapGallery SyncWith="mobileMap"
                             Layout="floating"
                             Position="top-right" />
    }
</div>

@code {
    private bool _isMobile;
    private bool _drawerOpen;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            var breakpoint = await BreakpointService.GetBreakpoint();
            _isMobile = breakpoint <= Breakpoint.Sm;
            StateHasChanged();
        }
    }
}
```

### Example 18: Admin Configuration Interface

```razor
@page "/admin/basemap-config"

<MudContainer MaxWidth="MaxWidth.Large">
    <MudText Typo="Typo.h4" Class="mb-4">Basemap Configuration</MudText>

    <MudGrid>
        <MudItem xs="12" md="6">
            <MudPaper Class="pa-4">
                <MudText Typo="Typo.h6" Class="mb-4">Available Basemaps</MudText>

                <HonuaBasemapGallery @ref="_gallery"
                                     SyncWith="previewMap"
                                     Layout="list"
                                     ShowFavorites="true"
                                     OnBasemapChanged="@HandleBasemapSelected" />

                <MudButton Class="mt-4"
                           Variant="Variant.Filled"
                           Color="Color.Primary"
                           OnClick="@AddCustomBasemap">
                    Add Custom Basemap
                </MudButton>
            </MudPaper>
        </MudItem>

        <MudItem xs="12" md="6">
            <MudPaper Class="pa-4" Style="height: 500px; position: relative;">
                <MudText Typo="Typo.h6" Class="mb-2">Preview</MudText>
                <div style="height: 450px; position: relative;">
                    <HonuaMap Id="previewMap" />
                </div>
            </MudPaper>
        </MudItem>

        @if (_selectedBasemap != null)
        {
            <MudItem xs="12">
                <MudPaper Class="pa-4">
                    <MudText Typo="Typo.h6" Class="mb-4">Basemap Details</MudText>

                    <MudGrid>
                        <MudItem xs="12" md="6">
                            <MudTextField @bind-Value="_selectedBasemap.Name"
                                          Label="Name"
                                          Variant="Variant.Outlined" />
                        </MudItem>
                        <MudItem xs="12" md="6">
                            <MudTextField @bind-Value="_selectedBasemap.Provider"
                                          Label="Provider"
                                          Variant="Variant.Outlined" />
                        </MudItem>
                        <MudItem xs="12">
                            <MudTextField @bind-Value="_selectedBasemap.StyleUrl"
                                          Label="Style URL"
                                          Variant="Variant.Outlined" />
                        </MudItem>
                        <MudItem xs="12">
                            <MudTextField @bind-Value="_selectedBasemap.Description"
                                          Label="Description"
                                          Variant="Variant.Outlined"
                                          Lines="3" />
                        </MudItem>
                    </MudGrid>

                    <div class="mt-4">
                        <MudButton Variant="Variant.Filled"
                                   Color="Color.Success"
                                   OnClick="@SaveBasemap">
                            Save Changes
                        </MudButton>
                    </div>
                </MudPaper>
            </MudItem>
        }
    </MudGrid>
</MudContainer>

@code {
    private HonuaBasemapGallery? _gallery;
    private Basemap? _selectedBasemap;

    private void HandleBasemapSelected(Basemap basemap)
    {
        _selectedBasemap = basemap;
    }

    private void AddCustomBasemap()
    {
        // Open dialog to add custom basemap
    }

    private void SaveBasemap()
    {
        // Save basemap configuration
    }
}
```

---

## Tips and Best Practices

### Tip 1: Choose the Right Layout

- **Grid**: Best for desktop, when space allows, and you want visual appeal
- **List**: Best for narrow spaces, mobile, when descriptions are important
- **Dropdown**: Best for compact interfaces, toolbars, when space is limited
- **Floating**: Best for overlay on map, when you want minimal initial footprint
- **Modal**: Best for occasional changes, large galleries, detailed selection

### Tip 2: Performance Optimization

```razor
<!-- Lazy load thumbnails for better performance -->
<HonuaBasemapGallery SyncWith="map1"
                     Position="top-right"
                     Layout="floating" />
<!-- Floating layout loads thumbnails only when opened -->
```

### Tip 3: User Experience

```razor
<!-- Enable preview for better UX -->
<HonuaBasemapGallery SyncWith="map1"
                     Position="top-right"
                     EnablePreview="true"
                     ShowFavorites="true" />
```

### Tip 4: Branding

```razor
<!-- Use custom basemaps for branding -->
<HonuaBasemapGallery SyncWith="map1"
                     Basemaps="@_brandedBasemaps"
                     DefaultBasemap="company-branded"
                     Categories="@(new[] { "Company", "Standard" })" />
```

---

## Additional Resources

- [Component API Documentation](./README.md)
- [Basemap Service Documentation](../../Services/BasemapService.cs)
- [MapLibre Style Specification](https://maplibre.org/maplibre-style-spec/)
- [Custom Tile Server Setup](https://github.com/maplibre/maplibre-tile-server)
