# HonuaChart Component

Interactive charting component that auto-syncs with maps and other components through the ComponentBus.

## Features

- **Multiple Chart Types**: Histogram, Bar, Pie, Doughnut, and Line charts
- **Auto-Sync**: Automatically updates when map extent changes
- **Interactive Filtering**: Click segments to filter map and other components
- **Aggregations**: Count, Sum, Average, Min, Max
- **Themes**: Built-in light and dark theme support
- **Export**: Export charts as PNG or JPEG images
- **Responsive**: Adapts to container size automatically
- **Format Options**: Currency, percent, decimal, integer formatting

## Installation

The HonuaChart component is part of the Honua.MapSDK package. Ensure you've registered the SDK:

```csharp
// Program.cs
builder.Services.AddHonuaMapSDK();
```

## Quick Start

### Basic Bar Chart

```razor
<HonuaChart Type="ChartType.Bar"
            Field="category"
            Title="Sales by Category" />
```

### Histogram

```razor
<HonuaChart Type="ChartType.Histogram"
            Field="propertyValue"
            Title="Property Value Distribution"
            Bins="20"
            ValueFormat="ValueFormat.Currency" />
```

### Pie Chart

```razor
<HonuaChart Type="ChartType.Pie"
            Field="landUse"
            Title="Land Use Distribution"
            ShowLegend="true" />
```

### Line Chart (Time Series)

```razor
<HonuaChart Type="ChartType.Line"
            Field="temperature"
            TimeField="timestamp"
            Title="Temperature Over Time"
            Aggregation="AggregationType.Avg" />
```

## Auto-Sync with Map

The real power comes from syncing with a map. The chart automatically updates when the map extent changes:

```razor
<HonuaMap Id="map1"
          Center="@(new[] { -122.4, 37.7 })"
          Zoom="12" />

<!-- Chart auto-updates as map moves -->
<HonuaChart Type="ChartType.Histogram"
            SyncWith="map1"
            Field="propertyValue"
            Title="Property Values in View"
            Bins="15"
            AutoSync="true" />
```

## Complete Dashboard Example

```razor
<div class="dashboard">
    <div class="map-panel">
        <HonuaMap Id="parcelMap"
                  Center="@(new[] { -122.4194, 37.7749 })"
                  Zoom="12" />
    </div>

    <div class="charts-panel">
        <!-- Property Value Distribution -->
        <HonuaChart Type="ChartType.Histogram"
                    SyncWith="parcelMap"
                    Field="assessedValue"
                    Title="Property Value Distribution"
                    Bins="20"
                    ValueFormat="ValueFormat.Currency"
                    EnableFilter="true"
                    ColorScheme="cool" />

        <!-- Land Use Breakdown -->
        <HonuaChart Type="ChartType.Pie"
                    SyncWith="parcelMap"
                    Field="landUse"
                    Title="Land Use Types"
                    EnableFilter="true"
                    ColorScheme="default" />

        <!-- Year Built Timeline -->
        <HonuaChart Type="ChartType.Bar"
                    SyncWith="parcelMap"
                    Field="yearBuilt"
                    Title="Buildings by Year"
                    Aggregation="AggregationType.Count"
                    MaxCategories="15" />

        <!-- Sales Trend -->
        <HonuaChart Type="ChartType.Line"
                    SyncWith="parcelMap"
                    Field="salePrice"
                    TimeField="saleDate"
                    Title="Average Sale Price Trend"
                    Aggregation="AggregationType.Avg"
                    ValueFormat="ValueFormat.Currency" />
    </div>
</div>

<style>
    .dashboard {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 20px;
        height: 100vh;
    }

    .map-panel {
        height: 100%;
    }

    .charts-panel {
        display: grid;
        grid-template-rows: repeat(4, 1fr);
        gap: 20px;
        overflow-y: auto;
    }
</style>
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Id` | string | Auto-generated | Unique identifier for the chart |
| `SyncWith` | string? | null | Map ID to sync with |
| `Type` | ChartType | Bar | Chart type (Histogram, Bar, Pie, Doughnut, Line) |
| `Field` | string | Required | Field name to visualize |
| `TimeField` | string? | null | Time field for line charts |
| `Title` | string? | null | Chart title |
| `Bins` | int | 10 | Number of bins for histogram |
| `Aggregation` | AggregationType | Count | Aggregation method (Count, Sum, Avg, Min, Max) |
| `ValueFormat` | ValueFormat | None | Value formatting (None, Currency, Percent, Decimal, Integer) |
| `ColorScheme` | string | "default" | Color scheme (default, cool, warm, earth) |
| `Theme` | string | "light" | Theme (light, dark) |
| `ShowLegend` | bool | true | Show legend |
| `LegendPosition` | string | "top" | Legend position (top, bottom, left, right) |
| `EnableFilter` | bool | true | Enable click-to-filter |
| `AutoSync` | bool | true | Auto-sync with map extent changes |
| `MaxCategories` | int | 20 | Maximum categories (rest grouped as "Other") |
| `CssClass` | string? | null | Additional CSS class |
| `Style` | string | "width: 100%; height: 400px;" | Inline styles |

## Enumerations

### ChartType

```csharp
public enum ChartType
{
    Histogram,  // Distribution of numeric values
    Bar,        // Categorical comparison
    Pie,        // Part-to-whole relationship
    Doughnut,   // Like pie with center cut out
    Line        // Trends over time
}
```

### AggregationType

```csharp
public enum AggregationType
{
    Count,  // Count records
    Sum,    // Sum values
    Avg,    // Average values
    Min,    // Minimum value
    Max     // Maximum value
}
```

### ValueFormat

```csharp
public enum ValueFormat
{
    None,      // No formatting
    Currency,  // $1,234.56
    Percent,   // 45.2%
    Decimal,   // 1234.56
    Integer    // 1,235
}
```

## Events

### OnSegmentClicked

Fires when a chart segment is clicked:

```razor
<HonuaChart Type="ChartType.Bar"
            Field="category"
            OnSegmentClicked="HandleSegmentClick" />

@code {
    private void HandleSegmentClick(ChartSegmentClickedEventArgs args)
    {
        Console.WriteLine($"Clicked: {args.Label} = {args.Value}");
        // args.Label - The segment label
        // args.Value - The segment value
        // args.Index - The segment index
        // args.Field - The field name
    }
}
```

## Public Methods

### RefreshAsync()

Manually refresh the chart data:

```csharp
@ref HonuaChart chartRef;

await chartRef.RefreshAsync();
```

### ExportAsPngAsync()

Export chart as PNG image (returns base64 data URL):

```csharp
string imageData = await chartRef.ExportAsPngAsync();
```

### ExportAsJpegAsync()

Export chart as JPEG image:

```csharp
string imageData = await chartRef.ExportAsJpegAsync();
```

### SetThemeAsync(string theme)

Change chart theme dynamically:

```csharp
await chartRef.SetThemeAsync("dark");
```

### UpdateData(List<Dictionary<string, object>> features)

Update chart with new data:

```csharp
var data = new List<Dictionary<string, object>>
{
    new() { ["category"] = "A", ["value"] = 100 },
    new() { ["category"] = "B", ["value"] = 200 }
};
await chartRef.UpdateData(data);
```

## ComponentBus Integration

The HonuaChart component automatically subscribes to these messages:

| Message | Description |
|---------|-------------|
| `MapExtentChangedMessage` | Recalculates stats for visible features |
| `FilterAppliedMessage` | Updates chart based on filter |
| `FilterClearedMessage` | Removes filter and refreshes |
| `AllFiltersClearedMessage` | Clears all filters |
| `DataLoadedMessage` | Requests data when new data is available |
| `DataResponseMessage` | Receives data from data source |

The component publishes these messages:

| Message | Description |
|---------|-------------|
| `FilterAppliedMessage` | When chart segment is clicked and `EnableFilter=true` |
| `DataRequestMessage` | When chart needs data from map |

## Advanced Usage

### Custom Click Handler with Filtering

```razor
<HonuaChart Type="ChartType.Bar"
            SyncWith="map1"
            Field="zoning"
            EnableFilter="false"
            OnSegmentClicked="HandleZoningClick" />

@code {
    [Inject] private ComponentBus Bus { get; set; }

    private async Task HandleZoningClick(ChartSegmentClickedEventArgs args)
    {
        // Custom filtering logic
        var customFilter = new object[] {
            "all",
            new object[] { "==", new object[] { "get", "zoning" }, args.Label },
            new object[] { ">=", new object[] { "get", "area" }, 5000 }
        };

        await Bus.PublishAsync(new FilterAppliedMessage
        {
            FilterId = "custom-zoning-filter",
            Type = FilterType.Attribute,
            Expression = customFilter
        }, "HonuaChart");
    }
}
```

### Multiple Synchronized Charts

```razor
<HonuaMap Id="mainMap" />

<!-- All charts sync with same map -->
<div class="chart-grid">
    <HonuaChart SyncWith="mainMap" Field="price" Type="ChartType.Histogram" />
    <HonuaChart SyncWith="mainMap" Field="type" Type="ChartType.Pie" />
    <HonuaChart SyncWith="mainMap" Field="bedrooms" Type="ChartType.Bar" />
</div>
```

### Theme Toggle

```razor
<button @onclick="ToggleTheme">Toggle Theme</button>

<HonuaChart @ref="chartRef"
            Field="sales"
            Theme="@currentTheme" />

@code {
    private HonuaChart chartRef;
    private string currentTheme = "light";

    private async Task ToggleTheme()
    {
        currentTheme = currentTheme == "light" ? "dark" : "light";
        await chartRef.SetThemeAsync(currentTheme);
    }
}
```

### Export Chart Image

```razor
<HonuaChart @ref="chartRef" Field="revenue" />
<button @onclick="ExportChart">Export as PNG</button>

@code {
    private HonuaChart chartRef;

    private async Task ExportChart()
    {
        var imageData = await chartRef.ExportAsPngAsync();

        // Download the image
        await JS.InvokeVoidAsync("downloadImage", imageData, "chart.png");
    }
}

<script>
    function downloadImage(dataUrl, filename) {
        const link = document.createElement('a');
        link.href = dataUrl;
        link.download = filename;
        link.click();
    }
</script>
```

## Color Schemes

### Default
Blue, green, yellow, red, purple, pink, teal, orange, indigo, emerald

### Cool
Blues, purples, and teals

### Warm
Reds, oranges, yellows, and pinks

### Earth
Grays and browns

## Styling

### Custom Styles

```razor
<HonuaChart Field="data"
            CssClass="my-custom-chart"
            Style="width: 100%; height: 600px; border-radius: 12px;" />

<style>
    .my-custom-chart {
        box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
        background: white;
        padding: 20px;
    }
</style>
```

### Dark Theme

```razor
<div class="dark-container">
    <HonuaChart Field="values"
                Theme="dark"
                ColorScheme="cool" />
</div>

<style>
    .dark-container {
        background: #1a1a1a;
        padding: 20px;
    }
</style>
```

## Best Practices

1. **Use Appropriate Chart Types**
   - Histogram: Numeric distributions
   - Bar: Categorical comparisons
   - Pie: Part-to-whole (limit categories)
   - Line: Time series data

2. **Limit Categories**
   - Use `MaxCategories` to prevent overcrowding
   - Default is 20, consider reducing for pie charts

3. **Choose the Right Aggregation**
   - Count: How many?
   - Sum: Total amount?
   - Avg: What's typical?
   - Min/Max: Extremes?

4. **Format Values**
   - Use `ValueFormat` for readability
   - Currency for money
   - Percent for ratios

5. **Enable Filtering Thoughtfully**
   - Great for exploration
   - Can be confusing in presentations
   - Consider `EnableFilter="false"` for static dashboards

## Performance Tips

- Charts automatically debounce map extent changes
- Large datasets (>10,000 records) are handled efficiently
- Use `MaxCategories` to limit rendered segments
- Consider aggregating data server-side for very large datasets

## Browser Compatibility

- Chrome 90+
- Firefox 88+
- Safari 14+
- Edge 90+

Requires ES modules support for Chart.js CDN import.

## Troubleshooting

### Chart Not Updating

Ensure `AutoSync="true"` and `SyncWith` matches the map ID:

```razor
<HonuaMap Id="map1" />
<HonuaChart SyncWith="map1" AutoSync="true" />
```

### No Data Showing

Check that:
1. The `Field` name matches your data properties
2. Data has been loaded into the map
3. The field contains valid values

### Click Filtering Not Working

Verify:
1. `EnableFilter="true"` is set
2. `SyncWith` is specified
3. The map layer has the corresponding field

## License

Part of Honua.MapSDK - see main repository for license.
