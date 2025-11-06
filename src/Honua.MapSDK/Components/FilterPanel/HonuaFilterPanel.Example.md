# HonuaFilterPanel Usage Examples

## Overview
The `HonuaFilterPanel` component provides a comprehensive filtering UI for geospatial data with spatial, attribute, and temporal filters.

## Basic Usage

### Example 1: Full-Featured Filter Panel
```razor
@page "/map-with-filters"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.FilterPanel
@using Honua.MapSDK.Models

<div style="display: flex; height: 100vh;">
    <!-- Filter Panel (Left Side) -->
    <div style="width: 320px; overflow-y: auto;">
        <HonuaFilterPanel
            SyncWith="mainMap"
            Title="Filter Features"
            ShowSpatial="true"
            ShowAttribute="true"
            ShowTemporal="true" />
    </div>

    <!-- Map (Right Side) -->
    <div style="flex: 1;">
        <HonuaMap Id="mainMap"
                  Center="new[] { -122.4, 37.8 }"
                  Zoom="10" />
    </div>
</div>
```

### Example 2: Attribute Filters Only with Predefined Fields
```razor
@page "/property-search"

@code {
    private List<FilterFieldConfig> propertyFields = new()
    {
        new FilterFieldConfig
        {
            Field = "landUse",
            Label = "Land Use",
            Type = FieldType.String,
            PredefinedValues = new List<object> { "Residential", "Commercial", "Industrial" }
        },
        new FilterFieldConfig
        {
            Field = "value",
            Label = "Property Value",
            Type = FieldType.Number
        },
        new FilterFieldConfig
        {
            Field = "yearBuilt",
            Label = "Year Built",
            Type = FieldType.Number
        }
    };
}

<div style="display: flex; height: 100vh;">
    <div style="width: 320px; overflow-y: auto;">
        <HonuaFilterPanel
            SyncWith="propertyMap"
            Title="Property Search"
            ShowSpatial="false"
            ShowTemporal="false"
            AttributeFields="propertyFields" />
    </div>

    <div style="flex: 1;">
        <HonuaMap Id="propertyMap"
                  Center="new[] { -122.4, 37.8 }"
                  Zoom="12" />
    </div>
</div>
```

### Example 3: Temporal Filters for Time-Series Data
```razor
@page "/incidents-timeline"

<div style="display: flex; height: 100vh;">
    <div style="width: 320px; overflow-y: auto;">
        <HonuaFilterPanel
            SyncWith="incidentMap"
            Title="Incident Timeline"
            ShowSpatial="false"
            ShowAttribute="false"
            ShowTemporal="true"
            DefaultDateField="incident_date" />
    </div>

    <div style="flex: 1;">
        <HonuaMap Id="incidentMap"
                  Center="new[] { -118.2, 34.0 }"
                  Zoom="11" />
    </div>
</div>
```

### Example 4: Floating Filter Panel
```razor
@page "/floating-filters"

<div style="position: relative; height: 100vh;">
    <HonuaMap Id="floatingMap"
              Center="new[] { -122.4, 37.8 }"
              Zoom="10"
              Style="width: 100%; height: 100%;" />

    <HonuaFilterPanel
        SyncWith="floatingMap"
        Title="Filters"
        CssClass="floating"
        ShowSpatial="true"
        ShowAttribute="true"
        ShowTemporal="true" />
</div>
```

### Example 5: Programmatic Filter Control
```razor
@page "/custom-filters"
@using Honua.MapSDK.Models

@code {
    private HonuaFilterPanel? filterPanel;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Add filters programmatically
            await AddInitialFilters();
        }
    }

    private async Task AddInitialFilters()
    {
        if (filterPanel == null) return;

        // Add a spatial filter
        var spatialFilter = new SpatialFilter
        {
            Label = "San Francisco Bay Area",
            SpatialType = SpatialFilterType.BoundingBox,
            BoundingBox = new[] { -122.5, 37.7, -122.3, 37.9 },
            IsActive = true
        };
        await filterPanel.AddFilterAsync(spatialFilter);

        // Add an attribute filter
        var attributeFilter = new AttributeFilter
        {
            Field = "status",
            Label = "Status",
            Operator = AttributeOperator.Equals,
            Value = "Active",
            IsActive = true
        };
        await filterPanel.AddFilterAsync(attributeFilter);
    }

    private async Task ClearFilters()
    {
        if (filterPanel != null)
        {
            await filterPanel.ClearFiltersAsync();
        }
    }

    private void OnFiltersApplied(List<FilterDefinition> filters)
    {
        Console.WriteLine($"Applied {filters.Count} filters");
    }
}

<div style="display: flex; flex-direction: column; height: 100vh;">
    <div style="padding: 16px; border-bottom: 1px solid #e0e0e0;">
        <button @onclick="ClearFilters">Clear All Filters</button>
    </div>

    <div style="display: flex; flex: 1; overflow: hidden;">
        <div style="width: 320px; overflow-y: auto;">
            <HonuaFilterPanel @ref="filterPanel"
                SyncWith="customMap"
                Title="Custom Filters"
                OnFiltersApplied="OnFiltersApplied" />
        </div>

        <div style="flex: 1;">
            <HonuaMap Id="customMap"
                      Center="new[] { -122.4, 37.8 }"
                      Zoom="10" />
        </div>
    </div>
</div>
```

## Filter Types

### Spatial Filters

#### Bounding Box (Current Map Extent)
```razor
<!-- User clicks "Use Current Map Extent" button -->
<!-- Creates filter: BoundingBox = [west, south, east, north] -->
```

#### Circle (Center + Radius)
```razor
<!-- User enters:
     - Longitude: -122.4
     - Latitude: 37.8
     - Radius: 5000 (meters)
-->
```

#### Within Distance of Point
```razor
<!-- Similar to circle, but semantically different
     Used for "find all features within X meters of this point"
-->
```

#### Draw Polygon
```razor
<!-- User draws a polygon on the map (future feature)
     Filter applied to features within the polygon
-->
```

### Attribute Filters

#### Equals
```razor
<!-- Field: "status", Operator: Equals, Value: "Active" -->
<!-- Result: status = "Active" -->
```

#### Numeric Comparisons
```razor
<!-- Field: "population", Operator: GreaterThan, Value: 100000 -->
<!-- Result: population > 100000 -->
```

#### String Matching
```razor
<!-- Field: "name", Operator: Contains, Value: "Park" -->
<!-- Result: name contains "Park" -->
```

#### In List
```razor
<!-- Field: "category", Operator: In, Values: "A,B,C" -->
<!-- Result: category in ["A", "B", "C"] -->
```

#### Null Checks
```razor
<!-- Field: "description", Operator: IsNull -->
<!-- Result: description is null -->
```

### Temporal Filters

#### Before/After Date
```razor
<!-- DateField: "created_at", Type: After, Date: 2024-01-01 -->
<!-- Result: created_at > 2024-01-01 -->
```

#### Between Dates
```razor
<!-- DateField: "modified_at", Type: Between -->
<!-- StartDate: 2024-01-01, EndDate: 2024-12-31 -->
<!-- Result: modified_at between 2024-01-01 and 2024-12-31 -->
```

#### Last N Days/Weeks/Months
```razor
<!-- Quick filter buttons: Last 7 Days, Last 30 Days, Last 90 Days -->
<!-- Or custom: Value: 6, Unit: Months -->
<!-- Result: date >= (now - 6 months) -->
```

## ComponentBus Messages

### Published Messages

#### FilterAppliedMessage
Published when a filter is applied:
```csharp
new FilterAppliedMessage
{
    FilterId = "filter-123",
    Type = FilterType.Spatial,
    Expression = { /* MapLibre filter expression */ }
}
```

#### FilterClearedMessage
Published when a single filter is removed:
```csharp
new FilterClearedMessage
{
    FilterId = "filter-123"
}
```

#### AllFiltersClearedMessage
Published when all filters are cleared:
```csharp
new AllFiltersClearedMessage
{
    Source = "filter-panel-id"
}
```

### Subscribed Messages

#### MapExtentChangedMessage
Listens for map extent changes to enable "Use Current Extent" button:
```csharp
new MapExtentChangedMessage
{
    MapId = "mainMap",
    Bounds = new[] { -122.5, 37.7, -122.3, 37.9 },
    Zoom = 12,
    Center = new[] { -122.4, 37.8 }
}
```

## Advanced Customization

### Custom Filter UI with ChildContent
```razor
<HonuaFilterPanel SyncWith="map1" Title="Advanced Filters">
    <ChildContent>
        <div style="padding: 16px; border-top: 1px solid #e0e0e0;">
            <h6>Custom Filters</h6>
            <button @onclick="ApplyCustomLogic">Apply Custom Logic</button>
        </div>
    </ChildContent>
</HonuaFilterPanel>

@code {
    private async Task ApplyCustomLogic()
    {
        // Your custom filter logic
    }
}
```

### Styling with CSS Classes
```razor
<HonuaFilterPanel
    SyncWith="map1"
    CssClass="my-custom-panel compact"
    Title="Filters" />
```

```css
/* In your component's CSS */
.my-custom-panel {
    border: 2px solid #2196f3;
}

.my-custom-panel.compact {
    box-shadow: none;
}
```

## Best Practices

1. **Always sync with a map**: Use the `SyncWith` parameter to connect the filter panel to a specific map

2. **Provide field configurations**: For better UX, define `AttributeFields` with proper labels and types

3. **Set default date fields**: If your data has temporal attributes, set `DefaultDateField`

4. **Handle filter events**: Use `OnFiltersApplied` and `OnFiltersCleared` to respond to filter changes

5. **Validate filter inputs**: The component validates inputs, but you should also validate on the server

6. **Use programmatic API**: For complex scenarios, use the `AddFilterAsync()` and `ClearFiltersAsync()` methods

7. **Test with different data types**: Ensure your filters work with strings, numbers, dates, and booleans

8. **Consider performance**: For large datasets, implement server-side filtering

## Accessibility

- All form elements have proper labels
- Keyboard navigation is fully supported
- Focus indicators are visible
- ARIA attributes are included for screen readers

## Browser Support

- Modern browsers (Chrome, Firefox, Safari, Edge)
- Mobile browsers (iOS Safari, Chrome Mobile)
- Responsive design adapts to different screen sizes
