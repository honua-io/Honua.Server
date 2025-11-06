# Getting Started with Honua.MapSDK

This comprehensive guide will help you get started with Honua.MapSDK, from installation to building your first interactive mapping application.

## Prerequisites

Before you begin, ensure you have:

- **.NET 9 SDK** or later ([Download](https://dotnet.microsoft.com/download))
- **Modern web browser** with WebGL support (Chrome 79+, Firefox 70+, Safari 13.1+, Edge 79+)
- **Code editor** (Visual Studio 2022, VS Code, or Rider)
- **Basic knowledge** of C# and Blazor

Optional but recommended:
- **MapTiler API key** for basemaps ([Get free key](https://www.maptiler.com/cloud/))
- **Mapbox API key** for geocoding ([Get free key](https://www.mapbox.com/))

## Installation

### Step 1: Create a Blazor Project

If you don't have an existing Blazor project:

```bash
# Create a new Blazor Web App
dotnet new blazor -o MyMappingApp
cd MyMappingApp
```

### Step 2: Add MapSDK Package

```bash
# Add the MapSDK NuGet package
dotnet add package Honua.MapSDK

# Add MudBlazor (required dependency)
dotnet add package MudBlazor
```

### Step 3: Register Services

Open `Program.cs` and register MapSDK services:

```csharp
using Honua.MapSDK;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Blazor services
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Add MudBlazor
builder.Services.AddMudServices();

// Add Honua MapSDK with default configuration
builder.Services.AddHonuaMapSDK();

// Or with custom configuration
builder.Services.AddHonuaMapSDK(options =>
{
    options.EnablePerformanceMonitoring = true;
    options.Cache.MaxSizeMB = 100;
    options.Cache.DefaultTtlSeconds = 600;

    // Configure rendering thresholds
    options.Rendering.VirtualScrollThreshold = 1000;
    options.Rendering.ChartDownsampleThreshold = 10000;

    // Configure data loading
    options.DataLoading.MaxParallelRequests = 4;
    options.DataLoading.EnableCompression = true;

    // Configure accessibility
    options.Accessibility.EnableKeyboardShortcuts = true;
    options.Accessibility.RespectReducedMotion = true;
});

var app = builder.Build();

// Configure middleware
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

### Step 4: Update _Imports.razor

Add MapSDK namespaces to `_Imports.razor`:

```razor
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Web
@using MudBlazor
@using Honua.MapSDK
@using Honua.MapSDK.Components
@using Honua.MapSDK.Core
@using Honua.MapSDK.Core.Messages
```

### Step 5: Update _Host.cshtml or App.razor

Add MudBlazor theme and MapSDK styles:

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>My Mapping App</title>
    <base href="~/" />

    <!-- MudBlazor CSS -->
    <link href="https://fonts.googleapis.com/css?family=Roboto:300,400,500,700&display=swap" rel="stylesheet" />
    <link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />

    <!-- MapLibre GL CSS -->
    <link href="https://unpkg.com/maplibre-gl@3.6.2/dist/maplibre-gl.css" rel="stylesheet" />

    <!-- MapSDK CSS (optional customizations) -->
    <link href="_content/Honua.MapSDK/styles/mapsdk.css" rel="stylesheet" />
</head>
<body>
    <component type="typeof(App)" render-mode="ServerPrerendered" />

    <!-- MudBlazor JS -->
    <script src="_content/MudBlazor/MudBlazor.min.js"></script>

    <!-- MapLibre GL JS -->
    <script src="https://unpkg.com/maplibre-gl@3.6.2/dist/maplibre-gl.js"></script>

    <!-- Blazor -->
    <script src="_framework/blazor.server.js"></script>
</body>
</html>
```

## Your First Map in 5 Minutes

### Create a Simple Map Page

Create a new file `Pages/Map.razor`:

```razor
@page "/map"
@using Honua.MapSDK.Components

<PageTitle>My First Map</PageTitle>

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h3" GutterBottom="true">My First Map</MudText>

    <MudPaper Elevation="2" Style="height: 600px; margin-top: 16px;">
        <HonuaMap
            Id="myFirstMap"
            MapStyle="https://demotiles.maplibre.org/style.json"
            Center="@(new[] { -122.4194, 37.7749 })"
            Zoom="12"
            Pitch="45"
            OnMapReady="HandleMapReady" />
    </MudPaper>

    @if (_mapReady)
    {
        <MudAlert Severity="Severity.Success" Class="mt-4">
            Map is ready! Center: @_center[0], @_center[1] | Zoom: @_zoom
        </MudAlert>
    }
</MudContainer>

@code {
    private bool _mapReady = false;
    private double[] _center = new[] { 0.0, 0.0 };
    private double _zoom = 0;

    private void HandleMapReady(MapReadyMessage message)
    {
        _mapReady = true;
        _center = message.Center;
        _zoom = message.Zoom;
        StateHasChanged();
    }
}
```

### Run Your Application

```bash
dotnet run
```

Navigate to `https://localhost:5001/map` and you should see your first interactive map!

## Understanding ComponentBus

ComponentBus is the core communication mechanism that makes MapSDK components work together seamlessly.

### How It Works

1. **Components publish messages** when something happens (e.g., map extent changes)
2. **Other components subscribe** to messages they care about
3. **ComponentBus delivers messages** to all subscribers automatically

### Example: Map and Grid Synchronization

Create `Pages/Synchronized.razor`:

```razor
@page "/synchronized"
@inject ComponentBus Bus

<MudGrid>
    <!-- Map -->
    <MudItem xs="12" md="8">
        <MudPaper Elevation="2" Style="height: 500px;">
            <HonuaMap
                Id="syncMap"
                MapStyle="https://demotiles.maplibre.org/style.json"
                Center="@(new[] { -122.4, 37.7 })"
                Zoom="10" />
        </MudPaper>
    </MudItem>

    <!-- Info Panel -->
    <MudItem xs="12" md="4">
        <MudPaper Elevation="2" Class="pa-4">
            <MudText Typo="Typo.h6">Map Information</MudText>
            <MudDivider Class="my-2" />

            <MudText Typo="Typo.body2">
                <strong>Zoom:</strong> @_currentZoom.ToString("F2")
            </MudText>
            <MudText Typo="Typo.body2">
                <strong>Center:</strong> @_currentCenter[0].ToString("F4"), @_currentCenter[1].ToString("F4")
            </MudText>
            <MudText Typo="Typo.body2" Class="mt-2">
                <strong>Updates:</strong> @_updateCount
            </MudText>
        </MudPaper>
    </MudItem>
</MudGrid>

@code {
    private double _currentZoom = 0;
    private double[] _currentCenter = new[] { 0.0, 0.0 };
    private int _updateCount = 0;

    protected override void OnInitialized()
    {
        // Subscribe to map extent changes
        Bus.Subscribe<MapExtentChangedMessage>(args =>
        {
            if (args.Message.MapId == "syncMap")
            {
                _currentZoom = args.Message.Zoom;
                _currentCenter = args.Message.Center;
                _updateCount++;
                StateHasChanged();
            }
        });
    }
}
```

## Adding Your First Components

Let's build a complete dashboard with multiple synchronized components.

### Complete Dashboard Example

Create `Pages/Dashboard.razor`:

```razor
@page "/dashboard"
@inject ComponentBus Bus

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h3" GutterBottom="true">Interactive Dashboard</MudText>

    <MudGrid>
        <!-- Main Map -->
        <MudItem xs="12" md="8">
            <MudPaper Elevation="2" Style="height: 600px;">
                <HonuaMap
                    Id="dashboardMap"
                    MapStyle="https://demotiles.maplibre.org/style.json"
                    Center="@(new[] { -122.4194, 37.7749 })"
                    Zoom="12" />
            </MudPaper>
        </MudItem>

        <!-- Sidebar with Tools -->
        <MudItem xs="12" md="4">
            <!-- Search -->
            <MudPaper Elevation="2" Class="pa-4 mb-4">
                <HonuaSearch
                    MapId="dashboardMap"
                    Provider="GeocodeProvider.Nominatim"
                    Placeholder="Search for a location..." />
            </MudPaper>

            <!-- Layer Control -->
            <MudPaper Elevation="2" Class="pa-4 mb-4">
                <HonuaLayerList MapId="dashboardMap" />
            </MudPaper>

            <!-- Bookmarks -->
            <MudPaper Elevation="2" Class="pa-4">
                <HonuaBookmarks MapId="dashboardMap" />
            </MudPaper>
        </MudItem>

        <!-- Coordinate Display -->
        <MudItem xs="12">
            <MudPaper Elevation="2" Class="pa-2">
                <HonuaCoordinateDisplay
                    MapId="dashboardMap"
                    Format="CoordinateFormat.DecimalDegrees"
                    ShowElevation="true" />
            </MudPaper>
        </MudItem>

        <!-- Data Grid -->
        <MudItem xs="12">
            <MudPaper Elevation="2" Style="height: 400px;">
                <HonuaDataGrid
                    Source="https://api.example.com/data.geojson"
                    SyncWith="dashboardMap"
                    EnableSelection="true"
                    EnableFiltering="true"
                    EnableSorting="true" />
            </MudPaper>
        </MudItem>

        <!-- Charts -->
        <MudItem xs="12" md="6">
            <MudPaper Elevation="2" Style="height: 300px;">
                <HonuaChart
                    Type="ChartType.Histogram"
                    Field="propertyValue"
                    SyncWith="dashboardMap"
                    Title="Property Value Distribution"
                    Bins="20"
                    ValueFormat="ValueFormat.Currency" />
            </MudPaper>
        </MudItem>

        <MudItem xs="12" md="6">
            <MudPaper Elevation="2" Style="height: 300px;">
                <HonuaChart
                    Type="ChartType.Pie"
                    Field="category"
                    SyncWith="dashboardMap"
                    Title="Categories"
                    ShowLegend="true" />
            </MudPaper>
        </MudItem>

        <!-- Legend -->
        <MudItem xs="12">
            <MudPaper Elevation="2" Class="pa-4">
                <HonuaLegend
                    SyncWith="dashboardMap"
                    ShowOpacityControls="true"
                    Collapsible="true" />
            </MudPaper>
        </MudItem>
    </MudGrid>
</MudContainer>
```

**Key Points:**
- All components reference `dashboardMap` through `SyncWith` or `MapId`
- Components automatically communicate through ComponentBus
- No manual event wiring or state management needed
- Components stay in sync automatically

## Common Patterns

### Pattern 1: Manual ComponentBus Usage

Sometimes you want to react to events in your own code:

```razor
@inject ComponentBus Bus

@code {
    protected override void OnInitialized()
    {
        // Subscribe to feature clicks
        Bus.Subscribe<FeatureClickedMessage>(async args =>
        {
            await HandleFeatureClick(args.Message);
        });

        // Subscribe to map extent changes
        Bus.Subscribe<MapExtentChangedMessage>(args =>
        {
            UpdateStatistics(args.Message);
        });
    }

    private async Task HandleFeatureClick(FeatureClickedMessage message)
    {
        // Load detailed feature data
        var details = await LoadFeatureDetails(message.FeatureId);

        // Show in a dialog
        await DialogService.ShowAsync<FeatureDetailsDialog>(
            "Feature Details",
            new DialogParameters { ["Data"] = details }
        );
    }

    private void UpdateStatistics(MapExtentChangedMessage message)
    {
        // Update analytics
        _currentZoom = message.Zoom;
        _visibleArea = CalculateArea(message.Bounds);
        StateHasChanged();
    }
}
```

### Pattern 2: Publishing Custom Messages

Create your own message types for custom workflows:

```csharp
// Define custom message
public class AnalysisCompleteMessage
{
    public required string AnalysisId { get; init; }
    public required Dictionary<string, object> Results { get; init; }
    public int FeaturesAnalyzed { get; init; }
}

// Publish from your component
@inject ComponentBus Bus

private async Task RunAnalysis()
{
    var results = await PerformAnalysis();

    await Bus.PublishAsync(new AnalysisCompleteMessage
    {
        AnalysisId = Guid.NewGuid().ToString(),
        Results = results,
        FeaturesAnalyzed = results.Count
    }, source: "AnalysisPanel");
}

// Subscribe in another component
protected override void OnInitialized()
{
    Bus.Subscribe<AnalysisCompleteMessage>(args =>
    {
        DisplayResults(args.Message.Results);
        Snackbar.Add(
            $"Analysis complete: {args.Message.FeaturesAnalyzed} features",
            Severity.Success
        );
    });
}
```

### Pattern 3: Loading External Data

```razor
@inject DataLoader Loader

@code {
    private async Task LoadGeoJsonData()
    {
        try
        {
            // Load with automatic caching
            var geoJson = await Loader.LoadJsonAsync<GeoJsonFeatureCollection>(
                "https://api.example.com/data.geojson"
            );

            // Process features
            foreach (var feature in geoJson.Features)
            {
                ProcessFeature(feature);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load GeoJSON");
            Snackbar.Add("Failed to load data", Severity.Error);
        }
    }
}
```

### Pattern 4: Streaming Large Datasets

```razor
@inject StreamingLoader Loader

@code {
    private async Task LoadLargeDataset()
    {
        var processed = 0;

        await Loader.StreamGeoJsonFeaturesAsync(
            url: "https://api.example.com/large-dataset.geojson",
            chunkSize: 100,
            onChunk: features =>
            {
                // Process 100 features at a time
                AddFeaturesToMap(features);

                processed += features.Count;
                UpdateProgress(processed);

                // Keep UI responsive
                StateHasChanged();
            }
        );

        Snackbar.Add($"Loaded {processed} features", Severity.Success);
    }
}
```

## Configuration and Customization

### Map Configuration

```razor
<HonuaMap
    Id="myMap"
    MapStyle="https://api.maptiler.com/maps/streets/style.json?key=YOUR_KEY"
    Center="@(new[] { -122.4194, 37.7749 })"
    Zoom="12"
    Bearing="45"
    Pitch="60"
    MinZoom="5"
    MaxZoom="18"
    MaxBounds="@(new[] { -123.0, 37.0, -121.0, 38.0 })"
    EnableGPU="true"
    Projection="globe"
    OnMapReady="HandleMapReady"
    OnExtentChanged="HandleExtentChanged"
    OnFeatureClicked="HandleFeatureClicked" />
```

### Global Configuration

```csharp
// In Program.cs
builder.Services.AddHonuaMapSDK(options =>
{
    // Default map style
    options.DefaultMapStyle = "https://api.maptiler.com/maps/streets/style.json";

    // Performance
    options.EnablePerformanceMonitoring = builder.Environment.IsDevelopment();
    options.Cache.MaxSizeMB = 100;
    options.Cache.DefaultTtlSeconds = 600;

    // Rendering optimization
    options.Rendering.VirtualScrollThreshold = 1000;
    options.Rendering.ChartDownsampleThreshold = 10000;
    options.Rendering.MapClusteringThreshold = 1000;
    options.Rendering.DebounceDelayMs = 300;

    // Data loading
    options.DataLoading.MaxParallelRequests = 4;
    options.DataLoading.TimeoutMs = 30000;
    options.DataLoading.EnableCompression = true;
    options.DataLoading.EnableRetry = true;
    options.DataLoading.MaxRetries = 3;

    // Geocoding
    options.Geocoding.Provider = "mapbox";
    options.Geocoding.ApiKey = builder.Configuration["MapboxApiKey"];
    options.Geocoding.EnableCaching = true;

    // Accessibility
    options.Accessibility.EnableKeyboardShortcuts = true;
    options.Accessibility.EnableScreenReaderAnnouncements = true;
    options.Accessibility.RespectReducedMotion = true;

    // Development
    options.EnableDevTools = builder.Environment.IsDevelopment();
    options.EnableMessageTracing = false; // Can be verbose
});
```

## Error Handling

Wrap your components in error boundaries for graceful error handling:

```razor
<MapErrorBoundary
    ShowResetButton="true"
    ShowTechnicalDetails="@(Environment.IsDevelopment())"
    OnError="HandleError"
    OnRetry="HandleRetry">

    <HonuaMap Id="myMap" ... />

</MapErrorBoundary>

@code {
    private void HandleError(Exception ex)
    {
        Logger.LogError(ex, "Map component error");
        Snackbar.Add("An error occurred loading the map", Severity.Error);
    }

    private async Task HandleRetry()
    {
        Logger.LogInformation("Retrying map load");
        await Task.Delay(1000); // Brief delay before retry
    }
}
```

## Next Steps

Now that you have a basic understanding of Honua.MapSDK:

1. **Explore Components** - Check out the [Component Catalog](ComponentCatalog.md) for detailed documentation on each component
2. **Learn Architecture** - Read the [Architecture Guide](Architecture.md) to understand how MapSDK works under the hood
3. **Best Practices** - Review [Best Practices](BestPractices.md) for optimal performance and maintainability
4. **Advanced Patterns** - Learn advanced techniques in the [Guides](guides/) section
5. **Try Examples** - Run the demo application to see components in action

### Useful Resources

- [Architecture Deep Dive](Architecture.md)
- [Component Catalog](ComponentCatalog.md)
- [Best Practices](BestPractices.md)
- [Troubleshooting](Troubleshooting.md)
- [API Reference](api/)
- [Performance Optimization](../Honua.MapSDK/PERFORMANCE_AND_OPTIMIZATIONS.md)

## Common Issues

### Map Not Rendering

**Problem:** Map container is empty or shows no content.

**Solutions:**
1. Ensure the map container has explicit height: `Style="height: 600px;"`
2. Check browser console for JavaScript errors
3. Verify MapLibre GL JS and CSS are loaded
4. Ensure `AddHonuaMapSDK()` is called in `Program.cs`

### Components Not Syncing

**Problem:** Components don't respond to each other's events.

**Solutions:**
1. Ensure all components reference the same `MapId` or use `SyncWith`
2. Verify ComponentBus is registered: `builder.Services.AddHonuaMapSDK()`
3. Check browser console for ComponentBus errors
4. Enable message tracing: `options.EnableMessageTracing = true`

### Performance Issues

**Problem:** Application is slow with large datasets.

**Solutions:**
1. Use streaming for large datasets: `StreamingLoader`
2. Enable caching: `options.Cache.MaxSizeMB = 100`
3. Increase rendering thresholds: `options.Rendering.VirtualScrollThreshold = 1000`
4. Enable data compression: `options.DataLoading.EnableCompression = true`

For more troubleshooting help, see the [Troubleshooting Guide](Troubleshooting.md).

## Getting Help

- **Documentation**: https://docs.honua.io
- **GitHub Issues**: https://github.com/honua/Honua.Server/issues
- **Discussions**: https://github.com/honua/Honua.Server/discussions
- **Email**: support@honua.io

---

**Congratulations!** You're now ready to build powerful mapping applications with Honua.MapSDK.
