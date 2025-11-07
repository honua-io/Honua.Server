# Quick Start Guide

Get up and running with Honua.MapSDK in under 10 minutes. This guide will help you create your first interactive mapping application.

---

## Prerequisites

Before starting, make sure you have:

- Completed the [Installation Guide](installation.md)
- A Blazor Server or WebAssembly project
- MapSDK package installed and configured

---

## Step 1: Create a New Page

Create a new Razor page in your `Pages` folder:

**Pages/MyFirstMap.razor**

```razor
@page "/my-first-map"

<PageTitle>My First Map</PageTitle>

<h1>My First Map</h1>
```

---

## Step 2: Add the Map Component

Add a `HonuaMap` component to your page:

```razor
@page "/my-first-map"
@using Honua.MapSDK.Components.Map

<PageTitle>My First Map</PageTitle>

<MudContainer MaxWidth="MaxWidth.False" Class="pa-4">
    <MudText Typo="Typo.h3" Class="mb-4">My First Map</MudText>

    <MudPaper Elevation="3" Style="height: 600px; padding: 0;">
        <HonuaMap Id="map1"
                  Center="@(new[] { -122.4194, 37.7749 })"
                  Zoom="12"
                  MapStyle="https://demotiles.maplibre.org/style.json" />
    </MudPaper>
</MudContainer>
```

**Run the application** and navigate to `/my-first-map`. You should see a map centered on San Francisco!

---

## Step 3: Add a Data Grid

Now let's add a synchronized data grid that displays data and interacts with the map:

```razor
@page "/my-first-map"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.DataGrid

<PageTitle>My First Map</PageTitle>

<MudContainer MaxWidth="MaxWidth.False" Class="pa-4">
    <MudText Typo="Typo.h3" Class="mb-4">My First Map</MudText>

    <MudGrid>
        <!-- Map -->
        <MudItem xs="12" md="8">
            <MudPaper Elevation="3" Style="height: 600px; padding: 0;">
                <HonuaMap Id="map1"
                          Center="@(new[] { -122.4194, 37.7749 })"
                          Zoom="12"
                          MapStyle="https://demotiles.maplibre.org/style.json" />
            </MudPaper>
        </MudItem>

        <!-- Data Grid -->
        <MudItem xs="12" md="4">
            <MudPaper Elevation="3" Style="height: 600px; overflow: auto;">
                <HonuaDataGrid TItem="FeatureData"
                               Items="@_sampleData"
                               SyncWith="map1"
                               Title="Locations"
                               ShowSearch="true"
                               Dense="true">
                    <Columns>
                        <PropertyColumn Property="x => x.Name" Title="Name" />
                        <PropertyColumn Property="x => x.Type" Title="Type" />
                        <PropertyColumn Property="x => x.Value" Title="Value" />
                    </Columns>
                </HonuaDataGrid>
            </MudPaper>
        </MudItem>
    </MudGrid>
</MudContainer>

@code {
    private List<FeatureData> _sampleData = new()
    {
        new FeatureData { Id = 1, Name = "Golden Gate Park", Type = "Park", Value = 1017 },
        new FeatureData { Id = 2, Name = "Fisherman's Wharf", Type = "Tourist", Value = 850 },
        new FeatureData { Id = 3, Name = "Alamo Square", Type = "Park", Value = 575 },
        new FeatureData { Id = 4, Name = "Coit Tower", Type = "Landmark", Value = 400 },
        new FeatureData { Id = 5, Name = "Palace of Fine Arts", Type = "Museum", Value = 650 }
    };

    public class FeatureData
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
```

The data grid is now **automatically synchronized** with the map via `SyncWith="map1"`.

---

## Step 4: Add a Chart

Let's add a chart to visualize the data:

```razor
@page "/my-first-map"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.DataGrid
@using Honua.MapSDK.Components.Chart

<PageTitle>My First Map</PageTitle>

<MudContainer MaxWidth="MaxWidth.False" Class="pa-4">
    <MudText Typo="Typo.h3" Class="mb-4">My First Map</MudText>

    <MudGrid>
        <!-- Map -->
        <MudItem xs="12" md="8">
            <MudPaper Elevation="3" Style="height: 600px; padding: 0;">
                <HonuaMap Id="map1"
                          Center="@(new[] { -122.4194, 37.7749 })"
                          Zoom="12"
                          MapStyle="https://demotiles.maplibre.org/style.json" />
            </MudPaper>
        </MudItem>

        <!-- Side Panel -->
        <MudItem xs="12" md="4">
            <!-- Data Grid -->
            <MudPaper Elevation="3" Style="height: 350px; overflow: auto; margin-bottom: 16px;">
                <HonuaDataGrid TItem="FeatureData"
                               Items="@_sampleData"
                               SyncWith="map1"
                               Title="Locations"
                               ShowSearch="true"
                               Dense="true"
                               PageSize="5">
                    <Columns>
                        <PropertyColumn Property="x => x.Name" Title="Name" />
                        <PropertyColumn Property="x => x.Type" Title="Type" />
                        <PropertyColumn Property="x => x.Value" Title="Value" />
                    </Columns>
                </HonuaDataGrid>
            </MudPaper>

            <!-- Chart -->
            <MudPaper Elevation="3" Style="height: 234px; padding: 0;">
                <HonuaChart Id="chart1"
                            Type="ChartType.Pie"
                            Field="Type"
                            SyncWith="map1"
                            Title="Locations by Type"
                            ColorScheme="cool" />
            </MudPaper>
        </MudItem>
    </MudGrid>
</MudContainer>

@code {
    private List<FeatureData> _sampleData = new()
    {
        new FeatureData { Id = 1, Name = "Golden Gate Park", Type = "Park", Value = 1017 },
        new FeatureData { Id = 2, Name = "Fisherman's Wharf", Type = "Tourist", Value = 850 },
        new FeatureData { Id = 3, Name = "Alamo Square", Type = "Park", Value = 575 },
        new FeatureData { Id = 4, Name = "Coit Tower", Type = "Landmark", Value = 400 },
        new FeatureData { Id = 5, Name = "Palace of Fine Arts", Type = "Museum", Value = 650 }
    };

    public class FeatureData
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
```

The chart automatically visualizes your data and updates based on map extent!

---

## Step 5: Add Filters

Finally, let's add a filter panel to enable data filtering:

```razor
@page "/my-first-map"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.DataGrid
@using Honua.MapSDK.Components.Chart
@using Honua.MapSDK.Components.FilterPanel
@using Honua.MapSDK.Models

<PageTitle>My First Map</PageTitle>

<MudContainer MaxWidth="MaxWidth.False" Class="pa-4">
    <MudText Typo="Typo.h3" Class="mb-4">My First Map</MudText>

    <MudGrid>
        <!-- Map -->
        <MudItem xs="12" md="8">
            <MudPaper Elevation="3" Style="height: 700px; padding: 0;">
                <HonuaMap Id="map1"
                          Center="@(new[] { -122.4194, 37.7749 })"
                          Zoom="12"
                          MapStyle="https://demotiles.maplibre.org/style.json" />
            </MudPaper>
        </MudItem>

        <!-- Side Panel -->
        <MudItem xs="12" md="4">
            <!-- Filter Panel -->
            <MudPaper Elevation="3" Class="pa-4 mb-4">
                <HonuaFilterPanel SyncWith="map1"
                                  Title="Filters"
                                  ShowSpatial="true"
                                  ShowAttribute="true"
                                  AttributeFields="@_filterFields" />
            </MudPaper>

            <!-- Data Grid -->
            <MudPaper Elevation="3" Style="height: 300px; overflow: auto; margin-bottom: 16px;">
                <HonuaDataGrid TItem="FeatureData"
                               Items="@_sampleData"
                               SyncWith="map1"
                               Title="Locations"
                               ShowSearch="true"
                               Dense="true"
                               PageSize="5">
                    <Columns>
                        <PropertyColumn Property="x => x.Name" Title="Name" />
                        <PropertyColumn Property="x => x.Type" Title="Type" />
                        <PropertyColumn Property="x => x.Value" Title="Value" />
                    </Columns>
                </HonuaDataGrid>
            </MudPaper>

            <!-- Chart -->
            <MudPaper Elevation="3" Style="height: 200px; padding: 0;">
                <HonuaChart Id="chart1"
                            Type="ChartType.Pie"
                            Field="Type"
                            SyncWith="map1"
                            Title="By Type"
                            ShowLegend="false" />
            </MudPaper>
        </MudItem>
    </MudGrid>
</MudContainer>

@code {
    private List<FeatureData> _sampleData = new()
    {
        new FeatureData { Id = 1, Name = "Golden Gate Park", Type = "Park", Value = 1017 },
        new FeatureData { Id = 2, Name = "Fisherman's Wharf", Type = "Tourist", Value = 850 },
        new FeatureData { Id = 3, Name = "Alamo Square", Type = "Park", Value = 575 },
        new FeatureData { Id = 4, Name = "Coit Tower", Type = "Landmark", Value = 400 },
        new FeatureData { Id = 5, Name = "Palace of Fine Arts", Type = "Museum", Value = 650 }
    };

    private List<FilterFieldConfig> _filterFields = new()
    {
        new FilterFieldConfig
        {
            Field = "Type",
            Label = "Location Type",
            Type = FieldType.String
        },
        new FilterFieldConfig
        {
            Field = "Value",
            Label = "Value",
            Type = FieldType.Number
        }
    };

    public class FeatureData
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
```

---

## Understanding Auto-Sync

Notice how we didn't write any code to connect the components? That's the power of MapSDK's **auto-sync**:

1. Each component has an `Id` property (e.g., `Id="map1"`)
2. Other components reference this ID with `SyncWith="map1"`
3. The **ComponentBus** handles all communication automatically
4. Changes in one component automatically update all synced components

### What Gets Synchronized?

- **Map extent changes** → Updates data grid and chart to show visible features only
- **Data grid row selection** → Highlights feature on map
- **Chart segment clicks** → Filters map and data grid
- **Filter changes** → Updates all synced components

---

## Complete Example

Here's the complete code for reference:

```razor
@page "/my-first-map"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.DataGrid
@using Honua.MapSDK.Components.Chart
@using Honua.MapSDK.Components.FilterPanel
@using Honua.MapSDK.Models

<PageTitle>My First Map</PageTitle>

<MudContainer MaxWidth="MaxWidth.False" Class="pa-4">
    <MudText Typo="Typo.h3" Class="mb-4">My First Interactive Dashboard</MudText>

    <MudGrid>
        <MudItem xs="12" md="8">
            <MudPaper Elevation="3" Style="height: 700px; padding: 0;">
                <HonuaMap Id="map1"
                          Center="@(new[] { -122.4194, 37.7749 })"
                          Zoom="12"
                          MapStyle="https://demotiles.maplibre.org/style.json"
                          OnMapReady="@HandleMapReady" />
            </MudPaper>
        </MudItem>

        <MudItem xs="12" md="4">
            <MudPaper Elevation="3" Class="pa-4 mb-4">
                <HonuaFilterPanel SyncWith="map1"
                                  Title="Filters"
                                  ShowSpatial="true"
                                  ShowAttribute="true"
                                  AttributeFields="@_filterFields" />
            </MudPaper>

            <MudPaper Elevation="3" Style="height: 300px; overflow: auto; margin-bottom: 16px;">
                <HonuaDataGrid TItem="FeatureData"
                               Items="@_sampleData"
                               SyncWith="map1"
                               Title="Locations"
                               ShowSearch="true"
                               ShowExport="true"
                               Dense="true"
                               PageSize="5">
                    <Columns>
                        <PropertyColumn Property="x => x.Name" Title="Name" />
                        <PropertyColumn Property="x => x.Type" Title="Type" />
                        <PropertyColumn Property="x => x.Value" Title="Value" Format="N0" />
                    </Columns>
                </HonuaDataGrid>
            </MudPaper>

            <MudPaper Elevation="3" Style="height: 200px; padding: 0;">
                <HonuaChart Id="chart1"
                            Type="ChartType.Pie"
                            Field="Type"
                            SyncWith="map1"
                            Title="Locations by Type"
                            ColorScheme="cool"
                            ShowLegend="false"
                            EnableFilter="true" />
            </MudPaper>
        </MudItem>
    </MudGrid>
</MudContainer>

@code {
    private List<FeatureData> _sampleData = new()
    {
        new FeatureData { Id = 1, Name = "Golden Gate Park", Type = "Park", Value = 1017 },
        new FeatureData { Id = 2, Name = "Fisherman's Wharf", Type = "Tourist", Value = 850 },
        new FeatureData { Id = 3, Name = "Alamo Square", Type = "Park", Value = 575 },
        new FeatureData { Id = 4, Name = "Coit Tower", Type = "Landmark", Value = 400 },
        new FeatureData { Id = 5, Name = "Palace of Fine Arts", Type = "Museum", Value = 650 },
        new FeatureData { Id = 6, Name = "Chinatown", Type = "Cultural", Value = 720 },
        new FeatureData { Id = 7, Name = "Mission District", Type = "Cultural", Value = 980 }
    };

    private List<FilterFieldConfig> _filterFields = new()
    {
        new FilterFieldConfig { Field = "Type", Label = "Location Type", Type = FieldType.String },
        new FilterFieldConfig { Field = "Value", Label = "Value", Type = FieldType.Number },
        new FilterFieldConfig { Field = "Name", Label = "Name", Type = FieldType.String }
    };

    private void HandleMapReady(MapReadyMessage message)
    {
        Console.WriteLine($"Map {message.MapId} is ready!");
    }

    public class FeatureData
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
```

---

## Try It Out

Run your application and try these interactions:

1. **Pan and zoom the map** - Notice the data grid and chart don't change (we haven't loaded map data yet)
2. **Click a row in the data grid** - The map highlights that feature
3. **Click a segment in the chart** - The map and data grid filter to show only that category
4. **Use the filter panel** - All components update to reflect the filters
5. **Search in the data grid** - Find specific locations quickly

---

## Next Steps

Congratulations! You've built your first MapSDK application. Now explore more advanced features:

### Learn More About Components

- [HonuaMap Deep Dive](first-map.md) - Learn all map features
- [Building Dashboards](your-first-dashboard.md) - Create production-ready dashboards
- [Component Documentation](../components/overview.md) - Explore all components

### Load Real Data

- [Working with Data](../guides/working-with-data.md) - Load GeoJSON, APIs, and more
- [Data Sources](../concepts/data-sources.md) - Understand data loading strategies

### Customize Your App

- [Styling Guide](../guides/custom-styling.md) - Theme and customize components
- [Advanced Filtering](../guides/advanced-filtering.md) - Build complex filters
- [Performance Tips](../recipes/performance-tips.md) - Optimize for large datasets

### Follow Tutorials

- [Property Dashboard](../tutorials/property-dashboard.md) - Build a real estate dashboard
- [Sensor Monitoring](../tutorials/sensor-monitoring.md) - IoT monitoring application
- [Fleet Tracking](../tutorials/fleet-tracking.md) - Vehicle tracking system

---

## Common Questions

### How do I load data from an API?

```razor
<HonuaMap Id="map1" Source="api/features.geojson" />
<HonuaDataGrid TItem="Feature" Source="api/features.geojson" SyncWith="map1" />
```

### How do I customize map styles?

```razor
<HonuaMap MapStyle="https://your-tile-server.com/style.json" />
```

Or use built-in styles:
```razor
<HonuaMap MapStyle="@MapStyles.OpenStreetMap" />
<HonuaMap MapStyle="@MapStyles.Satellite" />
```

### Can I have multiple maps on one page?

Yes! Just use different IDs:

```razor
<HonuaMap Id="map1" />
<HonuaMap Id="map2" />

<HonuaDataGrid SyncWith="map1" />
<HonuaDataGrid SyncWith="map2" />
```

### How do I handle events?

All components support event callbacks:

```razor
<HonuaMap OnMapReady="@HandleMapReady"
          OnExtentChanged="@HandleExtentChanged"
          OnFeatureClicked="@HandleFeatureClick" />

@code {
    private void HandleMapReady(MapReadyMessage msg)
    {
        Console.WriteLine("Map ready!");
    }

    private void HandleExtentChanged(MapExtentChangedMessage msg)
    {
        Console.WriteLine($"Zoom: {msg.Zoom}, Center: {msg.Center[0]}, {msg.Center[1]}");
    }

    private void HandleFeatureClick(FeatureClickedMessage msg)
    {
        Console.WriteLine($"Clicked feature: {msg.FeatureId}");
    }
}
```

---

## Getting Help

- [Troubleshooting Guide](../recipes/troubleshooting.md)
- [API Reference](../api/component-parameters.md)
- [GitHub Discussions](https://github.com/honua-io/Honua.Server/discussions)
- [Sample Applications](../tutorials/)

---

**You're all set!** You now have a working MapSDK application with synchronized components. Explore the documentation to learn more advanced features.
