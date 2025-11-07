# Tutorial 01: Build Your First Map in 10 Minutes

> **Learning Objectives**: By the end of this tutorial, you will have a working Blazor application with an interactive map, basemap gallery, and search functionality deployed locally.

---

## Prerequisites

Before starting, ensure you have:

- **.NET 8.0 SDK or later** installed
- **Visual Studio 2022** (or VS Code with C# extension)
- **Basic knowledge** of C# and Blazor
- **Internet connection** for NuGet packages

---

## Table of Contents

1. [Create New Blazor Project](#step-1-create-new-blazor-project)
2. [Install Honua.MapSDK](#step-2-install-honuamapsdk)
3. [Configure Services](#step-3-configure-services)
4. [Create Basic Map](#step-4-create-basic-map)
5. [Add Basemap Gallery](#step-5-add-basemap-gallery)
6. [Add Search](#step-6-add-search)
7. [Run and Test](#step-7-run-and-test)
8. [Deploy Locally](#step-8-deploy-locally)

---

## Step 1: Create New Blazor Project

Open your terminal and create a new Blazor Server project:

```bash
dotnet new blazorserver -n MyFirstMap
cd MyFirstMap
```

**Alternative for WebAssembly:**
```bash
dotnet new blazorwasm -n MyFirstMap
cd MyFirstMap
```

**What you just did:**
- Created a new Blazor application from template
- Named it "MyFirstMap"
- Changed directory into the project

---

## Step 2: Install Honua.MapSDK

Install the MapSDK NuGet package:

```bash
dotnet add package Honua.MapSDK
```

**What you just did:**
- Added Honua.MapSDK to your project
- Downloaded all required dependencies (MapLibre, Chart.js, MudBlazor)

**Expected output:**
```
info : Adding PackageReference for package 'Honua.MapSDK' into project
info : Restoring packages for MyFirstMap.csproj...
info : Package 'Honua.MapSDK' is compatible with all specified frameworks
```

---

## Step 3: Configure Services

Open `Program.cs` and add MapSDK services:

```csharp
using Honua.MapSDK.Extensions;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Add MudBlazor
builder.Services.AddMudServices();

// Add Honua.MapSDK
builder.Services.AddHonuaMapSDK();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
```

**What you just did:**
- Imported required namespaces
- Registered MudBlazor services (UI components)
- Registered MapSDK services (map components, ComponentBus, etc.)

---

## Step 4: Create Basic Map

Create a new Razor page at `Pages/MapPage.razor`:

```razor
@page "/map"
@using Honua.MapSDK.Components

<PageTitle>My First Map</PageTitle>

<MudContainer MaxWidth="MaxWidth.False" Class="pa-4">
    <MudText Typo="Typo.h3" Class="mb-4">My First Map</MudText>

    <MudPaper Elevation="3" Class="pa-3">
        <div style="height: 600px; width: 100%;">
            <HonuaMap @ref="_map"
                      Id="my-first-map"
                      Center="@(new[] { -122.4194, 37.7749 })"
                      Zoom="12"
                      MapStyle="https://demotiles.maplibre.org/style.json"
                      OnMapReady="@HandleMapReady" />
        </div>
    </MudPaper>

    @if (_mapReady)
    {
        <MudAlert Severity="Severity.Success" Class="mt-3">
            Map loaded successfully! Zoom: @_currentZoom
        </MudAlert>
    }
</MudContainer>

@code {
    private HonuaMap? _map;
    private bool _mapReady = false;
    private double _currentZoom = 12;

    private void HandleMapReady(MapReadyMessage message)
    {
        _mapReady = true;
        _currentZoom = message.Zoom;
        Console.WriteLine($"Map {message.MapId} is ready!");
    }
}
```

**What you just did:**
- Created a new page at `/map` route
- Added a HonuaMap component centered on San Francisco
- Set initial zoom level to 12
- Added a ready event handler to track when map loads

**Update Navigation Menu** - Add link to `Shared/NavMenu.razor`:

```razor
<MudNavLink Href="/map" Icon="@Icons.Material.Filled.Map">My Map</MudNavLink>
```

---

## Step 5: Add Basemap Gallery

Now let's add a basemap gallery so users can switch between different map styles:

```razor
@page "/map"
@using Honua.MapSDK.Components

<PageTitle>My First Map</PageTitle>

<MudContainer MaxWidth="MaxWidth.False" Class="pa-4">
    <MudText Typo="Typo.h3" Class="mb-4">My First Map</MudText>

    <MudPaper Elevation="3" Class="pa-3">
        <!-- Basemap Gallery Controls -->
        <MudStack Row="true" Class="mb-3" Spacing="2">
            <MudButton Variant="Variant.Filled"
                       Color="Color.Primary"
                       StartIcon="@Icons.Material.Filled.Layers"
                       OnClick="@(() => ShowBasemapGallery = !ShowBasemapGallery)">
                Change Basemap
            </MudButton>

            @if (ShowBasemapGallery)
            {
                <MudChip Color="Color.Info">Current: @_currentBasemapName</MudChip>
            }
        </MudStack>

        <!-- Basemap Selection -->
        @if (ShowBasemapGallery)
        {
            <MudPaper Elevation="1" Class="pa-3 mb-3">
                <MudText Typo="Typo.h6" Class="mb-2">Select Basemap</MudText>
                <MudGrid>
                    @foreach (var basemap in _basemaps)
                    {
                        <MudItem xs="12" sm="6" md="3">
                            <MudCard Class="cursor-pointer"
                                     @onclick="@(() => ChangeBasemap(basemap))">
                                <MudCardMedia Image="@basemap.ThumbnailUrl" Height="100" />
                                <MudCardContent>
                                    <MudText Typo="Typo.body2" Align="Align.Center">
                                        @basemap.Name
                                    </MudText>
                                </MudCardContent>
                            </MudCard>
                        </MudItem>
                    }
                </MudGrid>
            </MudPaper>
        }

        <!-- Map Container -->
        <div style="height: 600px; width: 100%; position: relative;">
            <HonuaMap @ref="_map"
                      Id="my-first-map"
                      Center="@_mapCenter"
                      Zoom="@_mapZoom"
                      MapStyle="@_currentBasemap"
                      OnMapReady="@HandleMapReady"
                      OnExtentChanged="@HandleExtentChanged" />

            <!-- Basemap Gallery Component (Alternative) -->
            <div style="position: absolute; bottom: 20px; right: 20px; z-index: 1000;">
                <HonuaBasemapGallery SyncWith="my-first-map"
                                     ShowThumbnails="true"
                                     Position="bottom-right" />
            </div>
        </div>
    </MudPaper>
</MudContainer>

@code {
    private HonuaMap? _map;
    private bool _mapReady = false;
    private double[] _mapCenter = new[] { -122.4194, 37.7749 };
    private double _mapZoom = 12;
    private string _currentBasemap = "https://demotiles.maplibre.org/style.json";
    private string _currentBasemapName = "OSM Liberty";
    private bool ShowBasemapGallery = false;

    private List<BasemapOption> _basemaps = new()
    {
        new BasemapOption
        {
            Name = "OSM Liberty",
            Url = "https://demotiles.maplibre.org/style.json",
            ThumbnailUrl = "https://demotiles.maplibre.org/tiles/preview.png"
        },
        new BasemapOption
        {
            Name = "Dark",
            Url = "https://basemaps.cartocdn.com/gl/dark-matter-gl-style/style.json",
            ThumbnailUrl = "https://basemaps.cartocdn.com/gl/dark-matter-gl-style/thumbnail.png"
        },
        new BasemapOption
        {
            Name = "Light",
            Url = "https://basemaps.cartocdn.com/gl/positron-gl-style/style.json",
            ThumbnailUrl = "https://basemaps.cartocdn.com/gl/positron-gl-style/thumbnail.png"
        },
        new BasemapOption
        {
            Name = "Voyager",
            Url = "https://basemaps.cartocdn.com/gl/voyager-gl-style/style.json",
            ThumbnailUrl = "https://basemaps.cartocdn.com/gl/voyager-gl-style/thumbnail.png"
        }
    };

    private void HandleMapReady(MapReadyMessage message)
    {
        _mapReady = true;
        Console.WriteLine($"Map {message.MapId} is ready!");
    }

    private void HandleExtentChanged(MapExtentChangedMessage message)
    {
        _mapCenter = message.Center;
        _mapZoom = message.Zoom;
    }

    private async Task ChangeBasemap(BasemapOption basemap)
    {
        _currentBasemap = basemap.Url;
        _currentBasemapName = basemap.Name;
        ShowBasemapGallery = false;
        await InvokeAsync(StateHasChanged);
    }

    private class BasemapOption
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
    }
}
```

**What you just did:**
- Added a basemap gallery with 4 different basemap options
- Created a UI for selecting basemaps
- Added the HonuaBasemapGallery component for quick switching
- Implemented basemap switching functionality

---

## Step 6: Add Search

Add search functionality to find locations on the map:

```razor
@page "/map"
@using Honua.MapSDK.Components
@inject ISnackbar Snackbar

<PageTitle>My First Map</PageTitle>

<MudContainer MaxWidth="MaxWidth.False" Class="pa-4">
    <MudText Typo="Typo.h3" Class="mb-4">My First Map</MudText>

    <MudPaper Elevation="3" Class="pa-3">
        <!-- Search Bar -->
        <MudStack Row="true" Class="mb-3" Spacing="2">
            <MudTextField @bind-Value="_searchQuery"
                          Label="Search for a location"
                          Variant="Variant.Outlined"
                          Adornment="Adornment.End"
                          AdornmentIcon="@Icons.Material.Filled.Search"
                          OnKeyUp="@HandleSearchKeyUp"
                          Style="flex: 1;" />
            <MudButton Variant="Variant.Filled"
                       Color="Color.Primary"
                       StartIcon="@Icons.Material.Filled.Search"
                       OnClick="@PerformSearch">
                Search
            </MudButton>
            <MudButton Variant="Variant.Filled"
                       Color="Color.Secondary"
                       StartIcon="@Icons.Material.Filled.Layers"
                       OnClick="@(() => ShowBasemapGallery = !ShowBasemapGallery)">
                Basemap
            </MudButton>
        </MudStack>

        <!-- Search Results -->
        @if (_searchResults.Any())
        {
            <MudPaper Elevation="1" Class="pa-3 mb-3">
                <MudText Typo="Typo.h6" Class="mb-2">Search Results</MudText>
                <MudList>
                    @foreach (var result in _searchResults)
                    {
                        <MudListItem Icon="@Icons.Material.Filled.Place"
                                     OnClick="@(() => GoToLocation(result))">
                            <MudText Typo="Typo.body1">@result.Name</MudText>
                            <MudText Typo="Typo.body2" Color="Color.Secondary">
                                @result.Type
                            </MudText>
                        </MudListItem>
                        <MudDivider />
                    }
                </MudList>
            </MudPaper>
        }

        <!-- Basemap Gallery -->
        @if (ShowBasemapGallery)
        {
            <MudPaper Elevation="1" Class="pa-3 mb-3">
                <MudText Typo="Typo.h6" Class="mb-2">Select Basemap</MudText>
                <MudGrid>
                    @foreach (var basemap in _basemaps)
                    {
                        <MudItem xs="12" sm="6" md="3">
                            <MudCard Class="cursor-pointer"
                                     @onclick="@(() => ChangeBasemap(basemap))">
                                <MudCardMedia Image="@basemap.ThumbnailUrl" Height="100" />
                                <MudCardContent>
                                    <MudText Typo="Typo.body2" Align="Align.Center">
                                        @basemap.Name
                                    </MudText>
                                </MudCardContent>
                            </MudCard>
                        </MudItem>
                    }
                </MudGrid>
            </MudPaper>
        }

        <!-- Map Container -->
        <div style="height: 600px; width: 100%; position: relative;">
            <HonuaMap @ref="_map"
                      Id="my-first-map"
                      Center="@_mapCenter"
                      Zoom="@_mapZoom"
                      MapStyle="@_currentBasemap"
                      OnMapReady="@HandleMapReady"
                      OnExtentChanged="@HandleExtentChanged" />

            <!-- Search Component (Alternative) -->
            <div style="position: absolute; top: 10px; left: 10px; z-index: 1000; width: 350px;">
                <HonuaSearch SyncWith="my-first-map"
                             Placeholder="Search for locations..."
                             ShowCategories="true"
                             MaxResults="5" />
            </div>
        </div>

        <!-- Status Bar -->
        @if (_mapReady)
        {
            <MudPaper Elevation="1" Class="pa-2 mt-2">
                <MudText Typo="Typo.caption">
                    📍 Lat: @_mapCenter[1].ToString("F4"), Lon: @_mapCenter[0].ToString("F4") |
                    🔍 Zoom: @_mapZoom.ToString("F1") |
                    🗺️ @_currentBasemapName
                </MudText>
            </MudPaper>
        }
    </MudPaper>
</MudContainer>

@code {
    private HonuaMap? _map;
    private bool _mapReady = false;
    private double[] _mapCenter = new[] { -122.4194, 37.7749 };
    private double _mapZoom = 12;
    private string _currentBasemap = "https://demotiles.maplibre.org/style.json";
    private string _currentBasemapName = "OSM Liberty";
    private bool ShowBasemapGallery = false;

    private string _searchQuery = string.Empty;
    private List<SearchResult> _searchResults = new();

    // Predefined locations for demo
    private readonly Dictionary<string, SearchResult> _locations = new()
    {
        { "san francisco", new SearchResult { Name = "San Francisco", Type = "City", Coordinates = new[] { -122.4194, 37.7749 }, Zoom = 12 } },
        { "new york", new SearchResult { Name = "New York", Type = "City", Coordinates = new[] { -74.0060, 40.7128 }, Zoom = 12 } },
        { "london", new SearchResult { Name = "London", Type = "City", Coordinates = new[] { -0.1278, 51.5074 }, Zoom = 12 } },
        { "tokyo", new SearchResult { Name = "Tokyo", Type = "City", Coordinates = new[] { 139.6917, 35.6895 }, Zoom = 12 } },
        { "paris", new SearchResult { Name = "Paris", Type = "City", Coordinates = new[] { 2.3522, 48.8566 }, Zoom = 12 } },
        { "sydney", new SearchResult { Name = "Sydney", Type = "City", Coordinates = new[] { 151.2093, -33.8688 }, Zoom = 12 } },
    };

    private List<BasemapOption> _basemaps = new()
    {
        new BasemapOption
        {
            Name = "OSM Liberty",
            Url = "https://demotiles.maplibre.org/style.json",
            ThumbnailUrl = "https://demotiles.maplibre.org/tiles/preview.png"
        },
        new BasemapOption
        {
            Name = "Dark",
            Url = "https://basemaps.cartocdn.com/gl/dark-matter-gl-style/style.json",
            ThumbnailUrl = "https://basemaps.cartocdn.com/gl/dark-matter-gl-style/thumbnail.png"
        },
        new BasemapOption
        {
            Name = "Light",
            Url = "https://basemaps.cartocdn.com/gl/positron-gl-style/style.json",
            ThumbnailUrl = "https://basemaps.cartocdn.com/gl/positron-gl-style/thumbnail.png"
        },
        new BasemapOption
        {
            Name = "Voyager",
            Url = "https://basemaps.cartocdn.com/gl/voyager-gl-style/style.json",
            ThumbnailUrl = "https://basemaps.cartocdn.com/gl/voyager-gl-style/thumbnail.png"
        }
    };

    private void HandleMapReady(MapReadyMessage message)
    {
        _mapReady = true;
        Console.WriteLine($"Map {message.MapId} is ready!");
    }

    private void HandleExtentChanged(MapExtentChangedMessage message)
    {
        _mapCenter = message.Center;
        _mapZoom = message.Zoom;
    }

    private async Task ChangeBasemap(BasemapOption basemap)
    {
        _currentBasemap = basemap.Url;
        _currentBasemapName = basemap.Name;
        ShowBasemapGallery = false;
        await InvokeAsync(StateHasChanged);
    }

    private void HandleSearchKeyUp(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            PerformSearch();
        }
    }

    private void PerformSearch()
    {
        if (string.IsNullOrWhiteSpace(_searchQuery))
        {
            _searchResults.Clear();
            return;
        }

        var query = _searchQuery.ToLower().Trim();

        // Search in predefined locations
        _searchResults = _locations
            .Where(kvp => kvp.Key.Contains(query) || kvp.Value.Name.ToLower().Contains(query))
            .Select(kvp => kvp.Value)
            .ToList();

        if (!_searchResults.Any())
        {
            Snackbar.Add($"No results found for '{_searchQuery}'", Severity.Info);
        }
    }

    private async Task GoToLocation(SearchResult location)
    {
        if (_map != null)
        {
            await _map.FlyToAsync(location.Coordinates, location.Zoom);
            _searchResults.Clear();
            _searchQuery = location.Name;
            Snackbar.Add($"Flying to {location.Name}", Severity.Success);
        }
    }

    private class BasemapOption
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
    }

    private class SearchResult
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public double[] Coordinates { get; set; } = Array.Empty<double>();
        public double Zoom { get; set; } = 12;
    }
}
```

**What you just did:**
- Added a search bar with autocomplete functionality
- Implemented search results display
- Added location navigation via search
- Integrated the HonuaSearch component for advanced search

---

## Step 7: Run and Test

Run your application:

```bash
dotnet run
```

**Expected output:**
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5001
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

**Open your browser** to `https://localhost:5001/map`

**Test the following:**

1. ✅ **Map loads** - You should see San Francisco
2. ✅ **Pan and zoom** - Click and drag, use mouse wheel
3. ✅ **Search** - Type "New York" and click result
4. ✅ **Basemap** - Click "Basemap" button and switch styles
5. ✅ **Status bar** - Watch coordinates update as you pan

**Troubleshooting:**

| Issue | Solution |
|-------|----------|
| Map is blank | Check browser console for errors, ensure internet connection |
| Search doesn't work | Verify Snackbar is injected, check browser console |
| Basemap won't change | Ensure MapStyle parameter is bound correctly |

---

## Step 8: Deploy Locally

Build the application for production:

```bash
dotnet publish -c Release -o ./publish
```

Run the published version:

```bash
cd publish
dotnet MyFirstMap.dll
```

**Your application is now running in production mode!**

Navigate to `https://localhost:5001/map` and verify everything works.

**What you just did:**
- Built an optimized production version
- Published all files to a single directory
- Ran the application in release mode

---

## What You Learned

In just 10 minutes, you've learned how to:

✅ **Create a Blazor project** and install Honua.MapSDK
✅ **Configure services** for MapSDK
✅ **Build an interactive map** with custom center and zoom
✅ **Add a basemap gallery** for style switching
✅ **Implement search functionality** to find locations
✅ **Handle map events** like ready and extent changed
✅ **Deploy locally** in production mode

---

## Next Steps

### Enhance Your Map

1. **Add Data Layers** - Display GeoJSON features
   ```razor
   <HonuaMap Source="api/features.geojson" />
   ```

2. **Add Popups** - Show feature details on click
   ```razor
   <HonuaPopup SyncWith="my-first-map" ShowOnClick="true" />
   ```

3. **Add Drawing Tools** - Let users draw shapes
   ```razor
   <HonuaDraw SyncWith="my-first-map" AllowPolygon="true" />
   ```

### Continue Learning

- 📖 [Tutorial 02: Property Management Dashboard](Tutorial_02_PropertyDashboard.md)
- 📖 [Component Documentation](../components/overview.md)
- 📖 [Working with Data Guide](../guides/working-with-data.md)

---

## Complete Working Code

Here's the final `Pages/MapPage.razor` with all features:

[See Step 6 code above for the complete implementation]

---

## Getting Help

- 📚 [Documentation](../README.md)
- 💬 [GitHub Discussions](https://github.com/honua-io/Honua.Server/discussions)
- 🐛 [Report Issues](https://github.com/honua-io/Honua.Server/issues)

---

**Congratulations!** You've successfully built your first map application with Honua.MapSDK!

**Tutorial Duration**: 10 minutes
**Lines of Code**: ~250
**Components Used**: HonuaMap, HonuaBasemapGallery, HonuaSearch
**Difficulty**: Beginner

---

*Last Updated: 2025-11-06*
*MapSDK Version: 1.0.0*
