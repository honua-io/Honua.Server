# HonuaDataGrid Component Examples

## Overview

The `HonuaDataGrid` component is a powerful, auto-syncing data grid for the Honua MapSDK. It seamlessly integrates with `HonuaMap` to display and interact with geospatial feature data.

## Features

- **Auto-sync with Maps**: Automatically filters data based on map viewport
- **Multiple Data Sources**: Supports GeoJSON, WFS, gRPC, and direct data binding
- **Interactive**: Click rows to highlight features on map, click features to highlight rows
- **Export**: Export to JSON, CSV, or GeoJSON formats
- **Filtering & Sorting**: Column-level filtering and multi-column sorting
- **Responsive**: Works on desktop and mobile devices
- **Customizable**: Full control over columns, styling, and behavior

## Basic Usage

### Example 1: Simple Auto-Synced Grid

```razor
@page "/basic-grid"
@using Dictionary = System.Collections.Generic.Dictionary<string, object>

<MudContainer>
    <MudGrid>
        <MudItem xs="12" md="8">
            <HonuaMap Id="map1"
                     MapStyle="https://demotiles.maplibre.org/style.json"
                     Center="@(new[] { -122.4194, 37.7749 })"
                     Zoom="12"
                     Style="height: 600px;" />
        </MudItem>
        <MudItem xs="12" md="4">
            <HonuaDataGrid TItem="Dictionary<string, object>"
                          Source="https://api.example.com/features.geojson"
                          SyncWith="map1"
                          Title="San Francisco Features"
                          PageSize="50"
                          Style="height: 600px;" />
        </MudItem>
    </MudGrid>
</MudContainer>
```

### Example 2: Custom Typed Data

```razor
@page "/typed-grid"

<HonuaMap Id="parcelMap" />

<HonuaDataGrid TItem="Parcel"
              Source="https://api.example.com/parcels.geojson"
              SyncWith="parcelMap"
              Title="Property Parcels"
              OnRowSelected="HandleRowSelected">
    <Columns>
        <PropertyColumn Property="@(p => p.ParcelId)" Title="Parcel ID" />
        <PropertyColumn Property="@(p => p.Address)" Title="Address" />
        <PropertyColumn Property="@(p => p.Owner)" Title="Owner" />
        <PropertyColumn Property="@(p => p.AssessedValue)" Title="Value" Format="C" />
        <PropertyColumn Property="@(p => p.Acreage)" Title="Acres" Format="N2" />
    </Columns>
</HonuaDataGrid>

@code {
    public class Parcel
    {
        public string ParcelId { get; set; } = "";
        public string Address { get; set; } = "";
        public string Owner { get; set; } = "";
        public decimal AssessedValue { get; set; }
        public double Acreage { get; set; }
        public string? Geometry { get; set; }
    }

    private void HandleRowSelected(Parcel parcel)
    {
        Console.WriteLine($"Selected: {parcel.Address}");
    }
}
```

### Example 3: Direct Data Binding

```razor
@page "/direct-data"

<HonuaDataGrid TItem="Feature"
              Items="@_features"
              Title="Local Data"
              PageSize="25"
              Sortable="true"
              Filterable="true"
              MultiSelection="true"
              OnMultipleRowsSelected="HandleMultiSelect" />

@code {
    private List<Feature> _features = new();

    protected override void OnInitialized()
    {
        _features = new List<Feature>
        {
            new Feature { Id = 1, Name = "Feature 1", Category = "Type A", Value = 100 },
            new Feature { Id = 2, Name = "Feature 2", Category = "Type B", Value = 200 },
            new Feature { Id = 3, Name = "Feature 3", Category = "Type A", Value = 150 },
        };
    }

    private void HandleMultiSelect(IEnumerable<Feature> selected)
    {
        Console.WriteLine($"Selected {selected.Count()} features");
    }

    public class Feature
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public int Value { get; set; }
    }
}
```

## Advanced Usage

### Example 4: Custom Toolbar Content

```razor
<HonuaDataGrid TItem="Dictionary<string, object>"
              Source="@_dataSource"
              Title="Advanced Grid">
    <ToolbarContent>
        <MudIconButton Icon="@Icons.Material.Filled.FilterList"
                      OnClick="ShowFilterDialog" />
        <MudIconButton Icon="@Icons.Material.Filled.Settings"
                      OnClick="ShowSettings" />
    </ToolbarContent>
</HonuaDataGrid>

@code {
    private string _dataSource = "https://api.example.com/data.geojson";

    private void ShowFilterDialog()
    {
        // Show custom filter dialog
    }

    private void ShowSettings()
    {
        // Show settings dialog
    }
}
```

### Example 5: Complete Dashboard with Map Sync

```razor
@page "/dashboard"
@inject ComponentBus Bus

<MudContainer MaxWidth="MaxWidth.ExtraExtraLarge">
    <MudText Typo="Typo.h4" Class="mb-4">Geospatial Dashboard</MudText>

    <MudGrid>
        <!-- Map -->
        <MudItem xs="12" md="8">
            <MudPaper Elevation="2" Class="pa-4">
                <HonuaMap Id="dashMap"
                         MapStyle="https://demotiles.maplibre.org/style.json"
                         Center="@_mapCenter"
                         Zoom="10"
                         Style="height: 700px;"
                         OnFeatureClicked="HandleFeatureClicked" />
            </MudPaper>
        </MudItem>

        <!-- Data Grid -->
        <MudItem xs="12" md="4">
            <MudPaper Elevation="2" Class="pa-4">
                <HonuaDataGrid TItem="Dictionary<string, object>"
                              Id="dashGrid"
                              Source="@_dataUrl"
                              SyncWith="dashMap"
                              Title="Features in View"
                              PageSize="25"
                              ShowSearch="true"
                              ShowExport="true"
                              Filterable="true"
                              OnRowSelected="HandleRowSelected"
                              OnDataLoaded="HandleDataLoaded"
                              Style="height: 700px;" />
            </MudPaper>
        </MudItem>

        <!-- Stats Cards -->
        <MudItem xs="12">
            <MudGrid>
                <MudItem xs="12" sm="6" md="3">
                    <MudCard>
                        <MudCardContent>
                            <MudText Typo="Typo.h6">Total Features</MudText>
                            <MudText Typo="Typo.h3">@_featureCount</MudText>
                        </MudCardContent>
                    </MudCard>
                </MudItem>
                <MudItem xs="12" sm="6" md="3">
                    <MudCard>
                        <MudCardContent>
                            <MudText Typo="Typo.h6">Selected</MudText>
                            <MudText Typo="Typo.h3">@_selectedCount</MudText>
                        </MudCardContent>
                    </MudCard>
                </MudItem>
            </MudGrid>
        </MudItem>
    </MudGrid>
</MudContainer>

@code {
    private double[] _mapCenter = new[] { -122.4194, 37.7749 };
    private string _dataUrl = "https://api.example.com/features.geojson";
    private int _featureCount = 0;
    private int _selectedCount = 0;

    private void HandleFeatureClicked(FeatureClickedMessage msg)
    {
        Console.WriteLine($"Feature clicked: {msg.FeatureId}");
    }

    private void HandleRowSelected(Dictionary<string, object> row)
    {
        _selectedCount = 1;
    }

    private void HandleDataLoaded(int count)
    {
        _featureCount = count;
    }
}
```

### Example 6: Export and Download Data

```razor
@page "/export-example"

<HonuaDataGrid TItem="SensorReading"
              @ref="_grid"
              Items="@_readings"
              Title="Sensor Readings"
              ShowExport="true" />

<MudButton OnClick="ExportFiltered" Color="Color.Primary">
    Export Filtered Data
</MudButton>

@code {
    private HonuaDataGrid<SensorReading>? _grid;
    private List<SensorReading> _readings = new();

    protected override void OnInitialized()
    {
        // Load sensor data
        _readings = LoadSensorData();
    }

    private async Task ExportFiltered()
    {
        if (_grid != null)
        {
            var filtered = _grid.GetItems();
            // Process filtered data
            Console.WriteLine($"Exporting {filtered.Count()} items");
        }
    }

    private List<SensorReading> LoadSensorData()
    {
        return new List<SensorReading>
        {
            new SensorReading
            {
                Id = 1,
                SensorName = "Sensor A",
                Temperature = 72.5,
                Humidity = 45.2,
                Timestamp = DateTime.Now
            },
            // ... more readings
        };
    }

    public class SensorReading
    {
        public int Id { get; set; }
        public string SensorName { get; set; } = "";
        public double Temperature { get; set; }
        public double Humidity { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
```

### Example 7: Programmatic Control

```razor
@page "/programmatic"

<MudStack>
    <MudButtonGroup>
        <MudButton OnClick="RefreshData">Refresh</MudButton>
        <MudButton OnClick="ClearSelection">Clear Selection</MudButton>
        <MudButton OnClick="GetSelection">Get Selection</MudButton>
    </MudButtonGroup>

    <HonuaDataGrid TItem="Dictionary<string, object>"
                  @ref="_grid"
                  Source="@_dataUrl"
                  Id="myGrid" />
</MudStack>

@code {
    private HonuaDataGrid<Dictionary<string, object>>? _grid;
    private string _dataUrl = "https://api.example.com/data.geojson";

    private async Task RefreshData()
    {
        if (_grid != null)
        {
            await _grid.RefreshAsync();
        }
    }

    private void ClearSelection()
    {
        _grid?.ClearSelection();
    }

    private void GetSelection()
    {
        if (_grid != null)
        {
            var selected = _grid.GetSelectedItem();
            if (selected != null)
            {
                Console.WriteLine($"Selected item: {selected}");
            }
        }
    }
}
```

## Parameter Reference

### Data Source Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Id` | `string` | auto-generated | Unique identifier for the grid |
| `Source` | `string?` | `null` | Data source URL (GeoJSON, WFS, gRPC) |
| `Items` | `IEnumerable<TItem>?` | `null` | Direct data binding |
| `SyncWith` | `string?` | `null` | Map ID to sync with |

### Appearance Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Title` | `string` | `"Data Grid"` | Grid title in toolbar |
| `CssClass` | `string?` | `null` | Additional CSS classes |
| `Style` | `string` | `"width: 100%; height: 500px;"` | Inline styles |
| `Dense` | `bool` | `true` | Use compact layout |

### Feature Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ShowToolbar` | `bool` | `true` | Show toolbar |
| `ShowSearch` | `bool` | `true` | Show search box |
| `ShowExport` | `bool` | `true` | Show export menu |
| `ShowRefresh` | `bool` | `true` | Show refresh button |
| `Filterable` | `bool` | `true` | Enable column filtering |
| `Sortable` | `bool` | `true` | Enable column sorting |
| `MultiSelection` | `bool` | `false` | Enable multi-row selection |
| `HideableColumns` | `bool` | `true` | Allow hiding columns |

### Pagination Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `PageSize` | `int` | `50` | Rows per page |
| `PageSizeOptions` | `int[]` | `[10, 25, 50, 100, 250]` | Available page sizes |

### Events

| Event | Type | Description |
|-------|------|-------------|
| `OnRowSelected` | `EventCallback<TItem>` | Row selected |
| `OnMultipleRowsSelected` | `EventCallback<IEnumerable<TItem>>` | Multiple rows selected |
| `OnDataLoaded` | `EventCallback<int>` | Data loaded |
| `OnError` | `EventCallback<string>` | Error occurred |

## ComponentBus Messages

### Published Messages

- **`DataRowSelectedMessage`**: When user clicks a row
- **`DataLoadedMessage`**: When data finishes loading

### Subscribed Messages

- **`MapExtentChangedMessage`**: Auto-filters by map viewport
- **`FeatureClickedMessage`**: Highlights corresponding row
- **`FilterAppliedMessage`**: Applies attribute filters
- **`FilterClearedMessage`**: Clears filters
- **`AllFiltersClearedMessage`**: Clears all filters

## Public Methods

```csharp
// Refresh data from source
await grid.RefreshAsync();

// Get selected item
var item = grid.GetSelectedItem();

// Get selected items (multi-selection)
var items = grid.GetSelectedItems();

// Get all items (filtered)
var all = grid.GetItems();

// Get total count (unfiltered)
var count = grid.GetTotalCount();

// Clear selection
grid.ClearSelection();
```

## Styling

The component includes comprehensive CSS with support for:
- Light and dark themes
- Responsive layouts
- Custom colors via CSS variables
- Dense and comfortable modes

Override styles by targeting `.honua-data-grid` and nested selectors.

## Best Practices

1. **Use typed models** when possible instead of dictionaries for better type safety
2. **Limit page size** for large datasets to improve performance
3. **Enable sync** only when you want spatial filtering
4. **Provide custom columns** for complex data types
5. **Handle events** to create interactive dashboards
6. **Cache data sources** to reduce network calls

## Troubleshooting

### Data not loading
- Check that `Source` URL is accessible
- Verify CORS is configured on the data server
- Check browser console for errors

### Map sync not working
- Ensure `SyncWith` matches the map's `Id`
- Verify ComponentBus is registered in DI container
- Check that data has geometry information

### Export failing
- Ensure JavaScript file is loaded: `honua-datagrid.js`
- Check browser console for JS errors
- Verify data is serializable

## Additional Resources

- [MudBlazor DataGrid Documentation](https://mudblazor.com/components/datagrid)
- [ComponentBus Architecture](../Core/ComponentBus.md)
- [HonuaMap Integration](../Map/HonuaMap.md)
