# HonuaCluster - Advanced Clustering Component

## Overview

The HonuaCluster component provides advanced point clustering capabilities for Honua.MapSDK, powered by the industry-standard Supercluster library. It efficiently clusters thousands of point features on the map while providing rich interactivity and customization options.

## Features

- **Supercluster Integration**: Uses the high-performance Supercluster library for efficient clustering
- **Configurable Cluster Radius**: Adjust the clustering distance in pixels
- **Custom Cluster Styling**: Size and color based on point count with customizable scales
- **Cluster Click to Zoom**: Automatically zooms to show cluster children
- **Spider-fy Clusters**: Expands clusters to show individual points in a spiral pattern
- **Cluster Extent on Hover**: Shows the bounding box of all points in a cluster
- **Animated Transitions**: Smooth animations when zooming and transitioning between cluster states
- **Custom Cluster Properties**: Aggregate data properties (sum, max, min, mean) across clustered points
- **ComponentBus Integration**: Publishes cluster events and subscribes to filter updates
- **Real-time Statistics**: Shows cluster counts, sizes, and distribution

## Basic Usage

```razor
@page "/cluster-example"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Cluster
@using Honua.MapSDK.Models

<HonuaMap Id="main-map"
          Center="@(new[] { -122.4194, 37.7749 })"
          Zoom="12"
          Style="https://demotiles.maplibre.org/style.json" />

<HonuaCluster SyncWith="main-map"
              Data="@_clusterData"
              ShowControls="true"
              ShowStatistics="true"
              OnClusterClicked="HandleClusterClick" />

@code {
    private object _clusterData = new
    {
        type = "FeatureCollection",
        features = new[]
        {
            new
            {
                type = "Feature",
                geometry = new { type = "Point", coordinates = new[] { -122.4194, 37.7749 } },
                properties = new { name = "Point 1", value = 100 }
            },
            new
            {
                type = "Feature",
                geometry = new { type = "Point", coordinates = new[] { -122.4294, 37.7849 } },
                properties = new { name = "Point 2", value = 200 }
            }
            // Add more points...
        }
    };

    private async Task HandleClusterClick(ClusterClickedEventArgs args)
    {
        Console.WriteLine($"Cluster clicked: {args.Cluster.PointCount} points at {args.Cluster.Coordinates[0]}, {args.Cluster.Coordinates[1]}");
    }
}
```

## Advanced Configuration

### Custom Styling

```razor
<HonuaCluster SyncWith="main-map"
              Data="@_data"
              Configuration="@_clusterConfig" />

@code {
    private ClusterConfiguration _clusterConfig = new()
    {
        ClusterRadius = 60,
        ClusterMaxZoom = 14,
        EnableSpiderfy = true,
        ShowClusterExtent = true,
        AnimateTransitions = true,
        ZoomOnClick = true,
        Style = new ClusterStyle
        {
            ColorScale = new Dictionary<int, string>
            {
                { 10, "#51bbd6" },    // 10-49 points: blue
                { 50, "#f1f075" },    // 50-99 points: yellow
                { 100, "#f28cb1" },   // 100-499 points: pink
                { 500, "#ff6b6b" }    // 500+ points: red
            },
            SizeScale = new Dictionary<int, int>
            {
                { 10, 25 },
                { 50, 35 },
                { 100, 45 },
                { 500, 55 }
            },
            ShowCountLabel = true,
            LabelFontSize = 14,
            LabelColor = "#ffffff",
            UnclusteredColor = "#11b4da",
            UnclusteredRadius = 8
        }
    };
}
```

### Custom Cluster Properties (Aggregations)

Aggregate numeric properties across clustered points:

```razor
@code {
    private ClusterConfiguration _config = new()
    {
        ClusterProperties = new Dictionary<string, string>
        {
            { "totalSales", "sum" },      // Sum of all 'totalSales' values
            { "maxPrice", "max" },        // Maximum 'maxPrice' value
            { "minPrice", "min" },        // Minimum 'minPrice' value
            { "avgRating", "mean" }       // Average 'avgRating' value
        }
    };
}
```

## ComponentBus Messages

### Published Messages

**ClusterClickedMessage**: When a cluster is clicked
```csharp
{
    MapId: "main-map",
    ClusterId: 123,
    PointCount: 25,
    Coordinates: [-122.4194, 37.7749],
    ExpansionZoom: 15,
    ComponentId: "cluster-abc123",
    Properties: { ... }
}
```

**ClusterStatisticsUpdatedMessage**: When statistics change (zoom, data update)
```csharp
{
    ComponentId: "cluster-abc123",
    TotalPoints: 1000,
    ClusterCount: 45,
    UnclusteredCount: 12,
    ZoomLevel: 12.5,
    MaxClusterSize: 250,
    AverageClusterSize: 22.2
}
```

**ClusterSpiderfiedMessage**: When a cluster is expanded
```csharp
{
    MapId: "main-map",
    ClusterId: 123,
    PointCount: 8,
    Coordinates: [-122.4194, 37.7749],
    ComponentId: "cluster-abc123"
}
```

**ClusterExtentShownMessage**: When cluster extent is displayed on hover
```csharp
{
    MapId: "main-map",
    ClusterId: 123,
    Bounds: [-122.5, 37.7, -122.3, 37.8],
    ComponentId: "cluster-abc123"
}
```

### Subscribed Messages

**FilterAppliedMessage**: Automatically filters clustered data
**DataLoadedMessage**: Updates cluster data when source data changes
**MapExtentChangedMessage**: Updates statistics when map view changes

## API Methods

### UpdateDataAsync(geojsonData)
Updates the cluster data dynamically:
```csharp
await clusterComponent.UpdateDataAsync(newGeojsonData);
```

### SetConfigurationAsync(config)
Updates cluster configuration programmatically:
```csharp
await clusterComponent.SetConfigurationAsync(new ClusterConfiguration
{
    ClusterRadius = 80,
    EnableSpiderfy = false
});
```

### GetStatistics()
Gets current cluster statistics:
```csharp
var stats = clusterComponent.GetStatistics();
Console.WriteLine($"Total points: {stats.TotalPoints}");
```

## Performance Tips

1. **Cluster Radius**: Larger radius (50-80px) = fewer clusters, better performance
2. **ClusterMaxZoom**: Set to 14-16 to stop clustering at higher zoom levels
3. **Data Size**: Supercluster efficiently handles 10,000+ points
4. **Custom Properties**: Use sparingly - aggregations add computational overhead
5. **Spider-fy Limit**: Default 50 points - above this, zoom instead

## Example: Real Estate Properties

```razor
<HonuaCluster SyncWith="main-map"
              Data="@_properties"
              Configuration="@_realEstateConfig"
              OnClusterClicked="@HandlePropertyCluster" />

@code {
    private ClusterConfiguration _realEstateConfig = new()
    {
        ClusterRadius = 50,
        ClusterMaxZoom = 16,
        EnableSpiderfy = true,
        ClusterProperties = new Dictionary<string, string>
        {
            { "avgPrice", "mean" },
            { "totalListings", "sum" }
        },
        Style = new ClusterStyle
        {
            ColorScale = new Dictionary<int, string>
            {
                { 5, "#00b4d8" },
                { 20, "#48cae4" },
                { 50, "#90e0ef" },
                { 100, "#caf0f8" }
            }
        }
    };

    private async Task HandlePropertyCluster(ClusterClickedEventArgs args)
    {
        var avgPrice = args.Cluster.Properties.GetValueOrDefault("avgPrice");
        Console.WriteLine($"Cluster of {args.Cluster.PointCount} properties, avg price: ${avgPrice:N0}");
    }
}
```

## Styling with CSS

The cluster component can be styled with custom CSS:

```css
.honua-cluster-controls {
    background: rgba(255, 255, 255, 0.95);
    border-radius: 8px;
    box-shadow: 0 2px 8px rgba(0,0,0,0.15);
}

.honua-cluster.dark-mode .honua-cluster-controls {
    background: rgba(40, 40, 40, 0.95);
    color: #ffffff;
}

.honua-cluster-toggle {
    background: white;
    border: none;
    border-radius: 50%;
    width: 40px;
    height: 40px;
    cursor: pointer;
    box-shadow: 0 1px 4px rgba(0,0,0,0.3);
}
```

## Integration with Other Components

### With HonuaFilterPanel
```razor
<HonuaFilterPanel SyncWith="main-map" OnFilterApplied="@HandleFilter" />
<HonuaCluster SyncWith="main-map" Data="@_data" />

@* Cluster automatically responds to filter changes via ComponentBus *@
```

### With HonuaSearch
```razor
<HonuaSearch SyncWith="main-map" />
<HonuaCluster SyncWith="main-map" Data="@_data" AllowFitBounds="true" />

@* Search results can trigger cluster fit-to-bounds *@
```

## Browser Compatibility

- Chrome/Edge: Full support
- Firefox: Full support
- Safari: Full support (iOS 13+)
- MapLibre GL: v2.0+

## Dependencies

- **Supercluster**: v8.0.1+ (CDN via ESM)
- **MapLibre GL JS**: v2.0+
- **Honua.MapSDK**: v1.0+

## License

Part of Honua.MapSDK - Licensed under the Elastic License 2.0
