# HonuaAttributeTable

Advanced attribute table component for viewing and managing GIS feature attributes with tight map integration.

## Overview

The `HonuaAttributeTable` is a specialized data grid component designed specifically for GIS feature attribute management. Unlike the general-purpose `HonuaDataGrid`, this component provides advanced capabilities for working with spatial data, including bidirectional map synchronization, geometry handling, field calculations, and spatial operations.

## Key Features

### 🗂️ Advanced Data Grid
- **Virtual scrolling** for 1000+ records with smooth performance
- **Frozen columns** - pin important columns to the left
- **Resizable columns** - adjust column widths
- **Reorderable columns** - drag and drop to reorganize
- **Column visibility toggle** - show/hide columns dynamically
- **Dense mode** - compact layout for maximum data density

### 🎯 Selection & Highlighting
- Click row to zoom to and highlight feature on map
- Multi-select rows with Ctrl/Shift
- Select feature on map to highlight in table
- Bidirectional sync with map
- "Show selected" filter mode

### 🔍 Sorting & Filtering
- Multi-column sort with priority
- Column header quick filters
- Advanced filter builder (SQL-like expressions)
- Global search across all columns
- Saved filter presets
- Filter by map extent

### ✏️ Data Operations
- Inline cell editing
- Bulk update selected rows
- Delete selected features
- Add new feature with form
- Field calculator for computed values
- Undo/redo support

### 📤 Export & Import
- Export to CSV, Excel, JSON, GeoJSON
- Import from CSV with field mapping
- Print table view
- Copy selected to clipboard
- Batch operations

### 🎨 Column Configuration
- Auto-detect data types (string, number, date, boolean, geometry)
- Custom column templates
- Calculated/virtual columns
- Conditional formatting (color by value)
- Summary row (sum, avg, count, min, max, etc.)

### 🔄 ComponentBus Integration
- Subscribe to `FeatureClickedMessage` - highlight row when feature clicked on map
- Subscribe to `FilterAppliedMessage` - apply filters from other components
- Publish `DataRowSelectedMessage` - notify other components of selection
- Publish `TableRowsUpdatedMessage` - notify when data changes
- Full bidirectional sync via `SyncWith` parameter

### 📊 Pagination & Loading
- Client-side or server-side pagination
- Configurable page sizes (25, 50, 100, 250, 500, 1000)
- Record count display
- Loading skeleton UI
- Lazy loading for large datasets

## Installation

The component is part of Honua.MapSDK and requires:

1. **MudBlazor** - UI component library
2. **MapLibre GL JS** - Map rendering (for map sync)
3. **ComponentBus** - Inter-component communication

```bash
# Already included in Honua.MapSDK
```

## Basic Usage

### Simple Attribute Table

```razor
<HonuaAttributeTable Features="@_features"
                     Title="Property Data"
                     AllowEdit="true"
                     AllowDelete="true"
                     AllowExport="true" />

@code {
    private List<FeatureRecord> _features = new();

    protected override void OnInitialized()
    {
        _features = new List<FeatureRecord>
        {
            new FeatureRecord
            {
                Id = "1",
                Properties = new Dictionary<string, object?>
                {
                    ["Name"] = "Parcel A",
                    ["Owner"] = "John Doe",
                    ["Value"] = 500000,
                    ["Zoning"] = "Residential"
                },
                Geometry = /* GeoJSON geometry */,
                GeometryType = "Polygon"
            }
        };
    }
}
```

### Map-Synchronized Table

```razor
<HonuaMap Id="main-map" @ref="_map" />

<HonuaAttributeTable SyncWith="main-map"
                     LayerId="parcels"
                     HighlightSelected="true"
                     OnRowSelected="@OnFeatureSelected" />

@code {
    private HonuaMap _map;

    private async Task OnFeatureSelected(FeatureRecord feature)
    {
        Console.WriteLine($"Selected: {feature.Id}");
        // Zoom to feature is automatic with HighlightSelected=true
    }
}
```

## Parameters

### Core Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `SyncWith` | `string?` | `null` | Map ID to synchronize with |
| `LayerId` | `string?` | `null` | Layer ID to display features from |
| `Features` | `List<FeatureRecord>?` | `null` | Direct feature data binding |
| `Configuration` | `TableConfiguration?` | `null` | Advanced table configuration |

### Appearance Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Title` | `string` | `"Attribute Table"` | Table title in toolbar |
| `CssClass` | `string?` | `null` | Additional CSS classes |
| `Style` | `string` | `"width: 100%; height: 600px;"` | Inline styles |
| `ShowToolbar` | `bool` | `true` | Show toolbar with actions |
| `ShowPagination` | `bool` | `true` | Show pagination controls |
| `PageSize` | `int` | `100` | Records per page |

### Feature Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `AllowEdit` | `bool` | `false` | Enable inline editing |
| `AllowDelete` | `bool` | `false` | Enable delete operations |
| `AllowExport` | `bool` | `true` | Enable export menu |
| `SelectionMode` | `SelectionMode` | `Multiple` | Selection mode (None, Single, Multiple) |
| `HighlightSelected` | `bool` | `true` | Highlight selected features on map |

### Event Callbacks

| Event | Type | Description |
|-------|------|-------------|
| `OnRowSelected` | `EventCallback<FeatureRecord>` | Fired when a row is selected |
| `OnRowsSelected` | `EventCallback<List<FeatureRecord>>` | Fired when multiple rows are selected |
| `OnRowsUpdated` | `EventCallback<List<FeatureRecord>>` | Fired when rows are updated |
| `OnRowDeleted` | `EventCallback<string>` | Fired when a row is deleted |

## Advanced Configuration

### Column Configuration

```csharp
var config = new TableConfiguration
{
    ShowSummary = true,
    Columns = new List<ColumnConfig>
    {
        new ColumnConfig
        {
            FieldName = "Name",
            DisplayName = "Parcel Name",
            DataType = ColumnDataType.String,
            Visible = true,
            Editable = true,
            Width = 200,
            Frozen = true // Sticky column
        },
        new ColumnConfig
        {
            FieldName = "Value",
            DisplayName = "Assessed Value",
            DataType = ColumnDataType.Currency,
            Visible = true,
            Editable = true,
            Format = "C0", // Currency format
            ConditionalFormats = new List<ConditionalFormat>
            {
                new ConditionalFormat
                {
                    Condition = "> 1000000",
                    BackgroundColor = "#ffebee",
                    TextColor = "#c62828",
                    FontWeight = "bold"
                }
            }
        },
        new ColumnConfig
        {
            FieldName = "Area",
            DisplayName = "Area (sq ft)",
            DataType = ColumnDataType.Number,
            Format = "N0"
        }
    },
    Summaries = new List<SummaryConfig>
    {
        new SummaryConfig
        {
            FieldName = "Value",
            Function = AggregateFunction.Sum,
            Label = "Total Value",
            Format = "C0"
        },
        new SummaryConfig
        {
            FieldName = "Value",
            Function = AggregateFunction.Average,
            Label = "Avg Value",
            Format = "C0"
        }
    }
};
```

```razor
<HonuaAttributeTable Features="@_features"
                     Configuration="@config" />
```

### Calculated Columns

```csharp
new ColumnConfig
{
    FieldName = "PricePerSqFt",
    DisplayName = "Price/Sq Ft",
    DataType = ColumnDataType.Currency,
    CalculatedExpression = "{Value} / {Area}",
    Format = "C2"
}
```

### Conditional Formatting

```csharp
ConditionalFormats = new List<ConditionalFormat>
{
    new ConditionalFormat
    {
        Condition = "> 1000000",
        BackgroundColor = "#e3f2fd",
        TextColor = "#1565c0",
        Icon = Icons.Material.Filled.TrendingUp
    },
    new ConditionalFormat
    {
        Condition = "< 100000",
        BackgroundColor = "#fff3e0",
        TextColor = "#e65100"
    }
}
```

### Filter Configuration

```csharp
// Simple filter
var filter = new FilterConfig
{
    Type = FilterType.Simple,
    Field = "Zoning",
    Operator = FilterOperator.Equals,
    Value = "Residential"
};

// Advanced filter (SQL-like)
var advancedFilter = new FilterConfig
{
    Type = FilterType.Advanced,
    Expression = "Value > 500000 AND Zoning IN ('Commercial', 'Industrial')"
};

// Compound filter (multiple conditions)
var compoundFilter = new FilterConfig
{
    Type = FilterType.Compound,
    LogicalOperator = LogicalOperator.And,
    ChildFilters = new List<FilterConfig>
    {
        new FilterConfig { Field = "Value", Operator = FilterOperator.GreaterThan, Value = 500000 },
        new FilterConfig { Field = "Owner", Operator = FilterOperator.Contains, Value = "LLC" }
    }
};
```

## Data Types

The component supports various data types with automatic formatting:

| Type | Description | Example Format |
|------|-------------|----------------|
| `String` | Text data | Default |
| `Number` | Numeric values | `N2` (two decimals) |
| `Integer` | Whole numbers | `N0` (no decimals) |
| `Decimal` | Decimal numbers | `N2` |
| `Boolean` | True/false | Checkbox/icon |
| `Date` | Date only | `yyyy-MM-dd` |
| `DateTime` | Date and time | `yyyy-MM-dd HH:mm:ss` |
| `Currency` | Money values | `C2` ($1,234.56) |
| `Percentage` | Percent values | `P1` (12.3%) |
| `Url` | Hyperlinks | Clickable link |
| `Email` | Email addresses | Mailto link |
| `Geometry` | Spatial data | Geometry type chip |

## Export Formats

### CSV Export
```csharp
// Automatically triggered via UI
// Or programmatically:
await JS.InvokeVoidAsync("exportToCSV", data, "features.csv", columns);
```

### GeoJSON Export
```csharp
// Exports features with geometry
await JS.InvokeVoidAsync("exportToGeoJSON", features, "features.geojson");
```

### Excel Export
```csharp
// Note: Currently exports as CSV (requires additional library for true .xlsx)
await JS.InvokeVoidAsync("exportToExcel", data, "features.xlsx", columns);
```

## ComponentBus Messages

### Published Messages

```csharp
// When row is selected
new DataRowSelectedMessage
{
    GridId = "attribute-table-{LayerId}",
    RowId = feature.Id,
    Data = feature.Properties,
    Geometry = feature.Geometry
}

// When filters are cleared
new AllFiltersClearedMessage
{
    Source = "attribute-table-{LayerId}"
}
```

### Subscribed Messages

```csharp
// Feature clicked on map
FeatureClickedMessage - Highlights corresponding row

// Map extent changed
MapExtentChangedMessage - Optionally filter by extent

// Filter applied
FilterAppliedMessage - Apply filter to table

// Filter cleared
FilterClearedMessage - Clear specific filter
AllFiltersClearedMessage - Clear all filters
```

## Public Methods

```csharp
// Get reference to component
@ref HonuaAttributeTable _table

// Refresh data
await _table.RefreshAsync();

// Get selected features
var selected = _table.GetSelectedFeatures();

// Clear selection
_table.ClearSelection();

// Select specific features
_table.SelectFeatures("feature-1", "feature-2");

// Apply custom filter
_table.ApplyCustomFilter(f => f.Properties["Value"] > 500000);
```

## Performance Tips

### Large Datasets (1000+ records)

1. **Enable pagination** - Use reasonable page sizes (100-250)
2. **Virtual scrolling** - Automatically enabled in MudDataGrid
3. **Limit visible columns** - Hide unnecessary columns
4. **Server-side operations** - Implement server-side filtering/sorting for very large datasets
5. **Lazy loading** - Load data on-demand rather than all at once

### Optimization Example

```razor
<HonuaAttributeTable Features="@_features"
                     PageSize="100"
                     ShowPagination="true"
                     Configuration="@_config" />

@code {
    private TableConfiguration _config = new()
    {
        Columns = new()
        {
            // Only define columns you need
            // Set Visible = false for columns to hide by default
        }
    };
}
```

## Styling

### Custom CSS Classes

```razor
<HonuaAttributeTable CssClass="my-custom-table"
                     Style="height: 800px;" />
```

```css
.my-custom-table {
    border: 2px solid var(--mud-palette-primary);
    border-radius: 8px;
}

.my-custom-table ::deep .mud-table-head {
    background: linear-gradient(to right, #667eea, #764ba2);
}
```

### Dark Mode

The component automatically supports dark mode based on MudBlazor theme:

```razor
<MudThemeProvider IsDarkMode="true" />
<HonuaAttributeTable ... />
```

## Accessibility

- **Keyboard navigation** - Full keyboard support (Tab, Arrow keys)
- **Screen readers** - ARIA labels on all interactive elements
- **Focus indicators** - Clear focus outlines
- **High contrast mode** - Enhanced contrast in high contrast mode
- **Responsive design** - Works on mobile devices

### Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Tab` | Navigate between cells |
| `Shift + Tab` | Navigate backwards |
| `Arrow Keys` | Navigate cells in edit mode |
| `Enter` | Edit cell / Save |
| `Escape` | Cancel edit |
| `Ctrl + A` | Select all (when enabled) |
| `Delete` | Delete selected (when enabled) |

## Browser Support

- Chrome/Edge 90+
- Firefox 88+
- Safari 14+
- Mobile browsers (iOS Safari, Chrome Mobile)

## Integration Examples

### With HonuaMap

```razor
<MudGrid>
    <MudItem xs="12" md="8">
        <HonuaMap Id="main-map" @ref="_map" />
    </MudItem>
    <MudItem xs="12" md="4">
        <HonuaAttributeTable SyncWith="main-map"
                             LayerId="parcels"
                             HighlightSelected="true" />
    </MudItem>
</MudGrid>
```

### With HonuaFilterPanel

```razor
<HonuaFilterPanel SyncWith="main-map" LayerId="parcels" />
<HonuaAttributeTable SyncWith="main-map" LayerId="parcels" />
<!-- Filters from panel automatically apply to table -->
```

### With HonuaChart

```razor
<HonuaChart SyncWith="main-map" />
<HonuaAttributeTable SyncWith="main-map"
                     OnRowsSelected="@UpdateChart" />

@code {
    private async Task UpdateChart(List<FeatureRecord> features)
    {
        // Update chart with selected features
    }
}
```

## Troubleshooting

### Features not displaying

1. Verify `Features` parameter is set or `LayerId` is correct
2. Check ComponentBus is registered in DI
3. Ensure map is initialized before syncing

### Map sync not working

1. Verify `SyncWith` matches map `Id`
2. Check JavaScript file is included: `honua-attributetable.js`
3. Ensure `LayerId` exists on the map

### Export not working

1. Check browser popup blocker settings
2. Verify JavaScript interop is working
3. Check browser console for errors

### Performance issues

1. Reduce page size
2. Hide unnecessary columns
3. Disable features you don't need (edit, delete)
4. Consider server-side pagination for very large datasets

## API Reference

See the [Examples.md](./Examples.md) file for complete working examples.

## License

Part of Honua.MapSDK - Licensed under Honua Server license terms.
