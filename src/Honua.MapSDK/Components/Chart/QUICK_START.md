# HonuaChart Quick Start Guide

## 5-Minute Setup

### Step 1: Add to your Razor page

```razor
@using Honua.MapSDK.Components.Chart
@using Honua.MapSDK.Components.Map

<HonuaChart Type="ChartType.Bar"
            Field="category"
            Title="My First Chart" />
```

### Step 2: Sync with a map

```razor
<HonuaMap Id="map1" Center="@(new[] { -122.4, 37.7 })" Zoom="12" />

<HonuaChart Type="ChartType.Histogram"
            SyncWith="map1"
            Field="propertyValue"
            Title="Property Values"
            Bins="20" />
```

### Step 3: Add interactivity

```razor
<HonuaChart Type="ChartType.Pie"
            SyncWith="map1"
            Field="landUse"
            Title="Land Use"
            EnableFilter="true"
            OnSegmentClicked="HandleClick" />

@code {
    private void HandleClick(ChartSegmentClickedEventArgs args)
    {
        Console.WriteLine($"Clicked: {args.Label}");
    }
}
```

## Common Patterns

### Property Value Dashboard

```razor
<div class="container">
    <HonuaMap Id="propertyMap" Zoom="12" />

    <HonuaChart Type="ChartType.Histogram"
                SyncWith="propertyMap"
                Field="assessedValue"
                Title="Property Values"
                Bins="15"
                ValueFormat="ValueFormat.Currency"
                EnableFilter="true" />
</div>
```

### Land Use Pie Chart

```razor
<HonuaChart Type="ChartType.Pie"
            SyncWith="propertyMap"
            Field="landUse"
            Title="Land Use Distribution"
            MaxCategories="8" />
```

### Time Series

```razor
<HonuaChart Type="ChartType.Line"
            SyncWith="propertyMap"
            Field="temperature"
            TimeField="timestamp"
            Title="Temperature Trend"
            Aggregation="AggregationType.Avg" />
```

### Category Comparison

```razor
<HonuaChart Type="ChartType.Bar"
            SyncWith="propertyMap"
            Field="zone"
            Title="Properties by Zone"
            Aggregation="AggregationType.Count" />
```

## Chart Types Quick Reference

| Type | Use For | Example |
|------|---------|---------|
| `Histogram` | Numeric distributions | Property values, ages, areas |
| `Bar` | Category comparison | Sales by region, count by type |
| `Pie` | Part-to-whole | Market share, land use % |
| `Doughnut` | Like pie with center | Same as pie, different style |
| `Line` | Trends over time | Temperature, sales over time |

## Parameters Cheat Sheet

### Essential
- `Type` - Chart type (required)
- `Field` - Data field to chart (required)
- `SyncWith` - Map ID to sync with
- `Title` - Chart title

### Formatting
- `ValueFormat` - None, Currency, Percent, Decimal, Integer
- `Bins` - Number of bins for histogram (default: 10)
- `Aggregation` - Count, Sum, Avg, Min, Max

### Appearance
- `ColorScheme` - default, cool, warm, earth
- `Theme` - light, dark
- `ShowLegend` - true/false
- `LegendPosition` - top, bottom, left, right

### Behavior
- `EnableFilter` - Click to filter (default: true)
- `AutoSync` - Auto-update on map move (default: true)
- `MaxCategories` - Limit categories shown (default: 20)

## Common Issues

### Chart not updating?
```razor
<!-- Make sure IDs match and AutoSync is true -->
<HonuaMap Id="map1" />
<HonuaChart SyncWith="map1" AutoSync="true" Field="value" />
```

### Field not found?
```razor
<!-- Field name must match your data property names -->
<HonuaChart Field="assessedValue" />  <!-- ✓ Matches data property -->
<HonuaChart Field="value" />          <!-- ✗ No such property -->
```

### Too many categories?
```razor
<!-- Limit categories to keep chart readable -->
<HonuaChart Field="zipCode" MaxCategories="10" />
```

## Pro Tips

1. **Use the right chart for the job**
   - Histogram for distributions
   - Bar for comparisons
   - Pie for proportions (limit to 5-8 slices)
   - Line for time series

2. **Format your values**
   ```razor
   ValueFormat="ValueFormat.Currency"  <!-- $1,234.56 -->
   ```

3. **Limit pie chart categories**
   ```razor
   MaxCategories="8"  <!-- Rest grouped as "Other" -->
   ```

4. **Use color schemes**
   ```razor
   ColorScheme="cool"  <!-- Blues and purples -->
   ColorScheme="warm"  <!-- Reds and oranges -->
   ```

5. **Export charts**
   ```csharp
   var image = await chartRef.ExportAsPngAsync();
   ```

## Next Steps

- [Full Documentation](README.md)
- [Live Examples](Examples.razor)
- [Component Bus Integration](../../Core/ComponentBus.cs)
- [Map Component](../Map/README.md)

## Need Help?

Check the full documentation for:
- Advanced filtering
- Custom event handlers
- Theme customization
- Performance optimization
- API reference
