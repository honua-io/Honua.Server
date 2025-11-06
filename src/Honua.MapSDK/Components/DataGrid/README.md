# HonuaDataGrid Component

A production-ready Blazor data grid component that automatically synchronizes with HonuaMap through the ComponentBus messaging system.

## Quick Start

```razor
<HonuaMap Id="map1" />

<HonuaDataGrid TItem="Dictionary<string, object>"
              Source="https://api.example.com/features.geojson"
              SyncWith="map1"
              PageSize="50" />
```

## Key Features

✅ **Auto-Sync with Maps** - Automatically filters data based on map viewport
✅ **Multiple Data Sources** - GeoJSON, WFS, gRPC, and direct binding
✅ **Interactive** - Bi-directional selection between map and grid
✅ **Export** - JSON, CSV, and GeoJSON export capabilities
✅ **Filtering & Sorting** - Column-level filtering and multi-column sorting
✅ **Responsive** - Works seamlessly on desktop and mobile
✅ **Customizable** - Full control over appearance and behavior
✅ **Type-Safe** - Generic TItem support for strongly-typed data

## Architecture

The HonuaDataGrid follows the same architectural patterns as HonuaMap:

- **ComponentBus Integration**: Loosely coupled communication via pub/sub messages
- **No Direct Dependencies**: Components never reference each other directly
- **Event-Driven**: Reactive updates through message passing
- **Production Ready**: Comprehensive error handling and logging

## Files

- **HonuaDataGrid.razor** - Component markup (MudDataGrid UI)
- **HonuaDataGrid.razor.cs** - Component logic and data handling
- **HonuaDataGrid.razor.css** - Component styling (light/dark themes)
- **HonuaDataGrid.Examples.md** - Comprehensive usage examples
- **honua-datagrid.js** - JavaScript utilities for export and UI helpers

## ComponentBus Messages

### Subscribes To:
- `MapExtentChangedMessage` - Filters data by map viewport
- `FeatureClickedMessage` - Highlights row when feature clicked on map
- `FilterAppliedMessage` - Applies attribute filters
- `FilterClearedMessage` - Clears specific filter
- `AllFiltersClearedMessage` - Clears all filters

### Publishes:
- `DataRowSelectedMessage` - When user clicks a row
- `DataLoadedMessage` - When data finishes loading

## Usage Examples

See [HonuaDataGrid.Examples.md](./HonuaDataGrid.Examples.md) for comprehensive examples including:

1. Simple Auto-Synced Grid
2. Custom Typed Data
3. Direct Data Binding
4. Custom Toolbar Content
5. Complete Dashboard with Map Sync
6. Export and Download Data
7. Programmatic Control

## Integration with HonuaMap

The grid automatically integrates with HonuaMap when `SyncWith` parameter is set:

```razor
<HonuaMap Id="myMap" />

<!-- This grid will auto-filter based on map extent -->
<HonuaDataGrid TItem="Feature"
              Source="@_dataUrl"
              SyncWith="myMap" />
```

When the user pans or zooms the map, the grid automatically updates to show only features visible in the current viewport.

## Data Sources

### GeoJSON (HTTP/HTTPS)
```razor
<HonuaDataGrid TItem="Dictionary<string, object>"
              Source="https://api.example.com/features.geojson" />
```

### Direct Data Binding
```razor
<HonuaDataGrid TItem="MyModel"
              Items="@_myData" />
```

### WFS (Coming Soon)
```razor
<HonuaDataGrid TItem="Dictionary<string, object>"
              Source="wfs://geoserver.example.com/wfs?..." />
```

### gRPC (Coming Soon)
```razor
<HonuaDataGrid TItem="MyProtoModel"
              Source="grpc://api.example.com/features" />
```

## Customization

### Custom Columns
```razor
<HonuaDataGrid TItem="Parcel" Source="@_source">
    <Columns>
        <PropertyColumn Property="@(p => p.Id)" Title="Parcel ID" />
        <PropertyColumn Property="@(p => p.Address)" Title="Address" />
        <PropertyColumn Property="@(p => p.Value)" Title="Value" Format="C" />
    </Columns>
</HonuaDataGrid>
```

### Custom Toolbar
```razor
<HonuaDataGrid TItem="Feature" Source="@_source">
    <ToolbarContent>
        <MudIconButton Icon="@Icons.Material.Filled.Download" OnClick="Export" />
        <MudIconButton Icon="@Icons.Material.Filled.Settings" OnClick="Settings" />
    </ToolbarContent>
</HonuaDataGrid>
```

### Styling
```css
.honua-data-grid {
    /* Override component styles */
}

.honua-data-grid ::deep .mud-table-row.mud-selected {
    background: #your-color;
}
```

## Public API

```csharp
// Get component reference
<HonuaDataGrid @ref="_grid" ... />

// Refresh data
await _grid.RefreshAsync();

// Get selection
var item = _grid.GetSelectedItem();
var items = _grid.GetSelectedItems();

// Get data
var all = _grid.GetItems();
var count = _grid.GetTotalCount();

// Clear selection
_grid.ClearSelection();
```

## Dependencies

- **MudBlazor** 8.0.0+ - UI components
- **System.Text.Json** - JSON serialization
- **ComponentBus** - Message bus (included in MapSDK)

## Browser Support

- Chrome/Edge 90+
- Firefox 88+
- Safari 14+
- Mobile browsers (iOS Safari, Chrome Android)

## Performance

- Virtualization support via MudDataGrid
- Pagination to limit rendered rows
- Lazy loading for large datasets
- Efficient filtering and sorting

## Contributing

The component follows these patterns:
1. Use ComponentBus for all inter-component communication
2. Follow existing code style and conventions
3. Add comprehensive XML documentation
4. Include unit tests for new features
5. Update examples documentation

## License

Part of Honua.MapSDK - see main project license.

## Support

For issues, feature requests, or questions:
- GitHub Issues: [honua-io/Honua.Server](https://github.com/honua-io/Honua.Server)
- Documentation: See Examples.md in this directory
