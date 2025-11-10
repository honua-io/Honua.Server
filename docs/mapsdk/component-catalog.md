# Component Catalog

Complete reference for all Honua.MapSDK components. Each component is designed to work seamlessly with others through the ComponentBus messaging system.

## Table of Contents

1. [Core Components](#core-components)
2. [Search & Navigation](#search--navigation)
3. [Drawing & Editing](#drawing--editing)
4. [Layer Management](#layer-management)
5. [Data Management](#data-management)
6. [Visualization](#visualization)
7. [Export & Print](#export--print)
8. [Utility Components](#utility-components)
9. [Component Compatibility Matrix](#component-compatibility-matrix)

---

## Core Components

### HonuaMap

**The foundation component** - MapLibre GL-based interactive map with GPU rendering.

#### Description
Primary map component providing vector tile rendering, user interaction, and spatial data visualization. All other components reference or sync with a map instance.

#### Key Parameters

```csharp
<HonuaMap
    Id="myMap"                    // Unique identifier (required)
    MapStyle="..."                // MapLibre style URL (required)
    Center="@(new[] { -122, 37 })" // [longitude, latitude]
    Zoom="12"                     // Zoom level (0-22)
    Bearing="0"                   // Rotation (0-360)
    Pitch="0"                     // Tilt (0-60)
    MinZoom="5"                   // Minimum zoom constraint
    MaxZoom="18"                  // Maximum zoom constraint
    MaxBounds="@bounds"           // Geographic bounds constraint
    EnableGPU="true"              // WebGL acceleration
    Projection="mercator"         // "mercator" | "globe"
    OnMapReady="HandleReady"      // Callback when initialized
    OnExtentChanged="HandleMove"  // Callback when viewport changes
    OnFeatureClicked="HandleClick" // Callback when feature clicked
/>
```

#### Common Use Cases
- Base map for spatial applications
- Data visualization and overlay
- Interactive feature exploration
- Spatial analysis foundation

#### Messages Published
- `MapReadyMessage` - Map initialized
- `MapExtentChangedMessage` - Viewport changed
- `FeatureClickedMessage` - Feature clicked
- `FeatureHoveredMessage` - Feature hovered

#### Messages Subscribed
- `FlyToRequestMessage` - Fly to location
- `FitBoundsRequestMessage` - Fit to bounds
- `FilterAppliedMessage` - Apply data filter
- `LayerVisibilityChangedMessage` - Toggle layer
- `BasemapChangedMessage` - Change basemap

#### Documentation
[Full HonuaMap Documentation](../../src/Honua.MapSDK/Components/Map/README.md)

---

### HonuaDataGrid

**Feature-rich data grid** with automatic map synchronization and MudBlazor styling.

#### Description
Tabular data display with sorting, filtering, pagination, and bi-directional sync with map. Automatically shows features in current map extent.

#### Key Parameters

```csharp
<HonuaDataGrid
    Source="url-or-data"          // Data source (URL or array)
    SyncWith="mapId"              // Map to sync with
    EnableSelection="true"        // Row selection
    EnableFiltering="true"        // Column filtering
    EnableSorting="true"          // Column sorting
    EnablePagination="true"       // Pagination
    PageSize="50"                 // Rows per page
    VirtualizeThreshold="1000"    // Virtual scrolling threshold
    OnRowClicked="HandleRowClick" // Row click callback
    OnSelectionChanged="HandleSelect" // Selection callback
/>
```

#### Common Use Cases
- Displaying feature attributes
- Tabular data exploration
- Feature selection and highlighting
- Data export workflows

#### Messages Published
- `DataRowSelectedMessage` - Row selected
- `DataLoadedMessage` - Data loaded

#### Messages Subscribed
- `MapExtentChangedMessage` - Update visible rows
- `FilterAppliedMessage` - Apply filter to rows
- `FeatureClickedMessage` - Highlight row

#### Documentation
[Full HonuaDataGrid Documentation](../../src/Honua.MapSDK/Components/DataGrid/README.md)

---

### HonuaChart

**Interactive charting** with histogram, bar, pie, line, and scatter chart types.

#### Description
Dynamic charts that automatically update based on map extent or applied filters. Supports multiple chart types and interactive filtering.

#### Key Parameters

```csharp
<HonuaChart
    Type="ChartType.Histogram"    // Chart type
    Field="propertyName"          // Data field to chart
    SyncWith="mapId"              // Map to sync with
    Title="Chart Title"           // Chart title
    Bins="20"                     // Histogram bins
    ValueFormat="ValueFormat.Currency" // Value formatting
    EnableFilter="true"           // Click to filter
    ShowLegend="true"             // Show legend
    Height="300"                  // Chart height
    OnBinClicked="HandleBinClick" // Bin click callback
/>
```

#### Chart Types
- `Histogram` - Distribution of numeric values
- `Bar` - Categorical comparisons
- `Pie` - Part-to-whole relationships
- `Line` - Trends over time
- `Scatter` - Correlation between variables

#### Common Use Cases
- Data distribution analysis
- Statistical visualization
- Trend identification
- Interactive data filtering

#### Messages Published
- `FilterAppliedMessage` - User clicks bin/segment
- `DataLoadedMessage` - Chart data loaded

#### Messages Subscribed
- `MapExtentChangedMessage` - Update chart data
- `FilterAppliedMessage` - Filter chart data
- `TimeChangedMessage` - Filter by time

#### Documentation
[Full HonuaChart Documentation](../../src/Honua.MapSDK/Components/Chart/README.md)

---

### HonuaLegend

**Dynamic legend** showing active map layers and symbology.

#### Description
Automatically generated legend based on visible map layers. Shows layer names, symbols, and provides opacity/visibility controls.

#### Key Parameters

```csharp
<HonuaLegend
    SyncWith="mapId"              // Map to sync with
    ShowOpacityControls="true"    // Show opacity sliders
    ShowVisibilityToggles="true"  // Show visibility checkboxes
    Collapsible="true"            // Collapsible sections
    Position="LegendPosition.BottomRight" // Legend position
/>
```

#### Common Use Cases
- Explaining map symbology
- Layer visibility control
- Layer opacity adjustment
- Map legend for reports

#### Messages Published
- `LayerVisibilityChangedMessage` - User toggles layer
- `LayerOpacityChangedMessage` - User adjusts opacity

#### Messages Subscribed
- `LayerAddedMessage` - Add legend item
- `LayerRemovedMessage` - Remove legend item
- `LayerMetadataUpdatedMessage` - Update legend

#### Documentation
[Full HonuaLegend Documentation](../../src/Honua.MapSDK/Components/Legend/README.md)

---

### HonuaFilterPanel

**Advanced filtering UI** for spatial and attribute-based queries.

#### Description
Build complex filters with an intuitive UI. Supports spatial, attribute, and temporal filters with saved filter presets.

#### Key Parameters

```csharp
<HonuaFilterPanel
    SyncWith="mapId"              // Map to sync with
    EnableSpatialFilters="true"   // Enable spatial filters
    EnableAttributeFilters="true" // Enable attribute filters
    EnableTemporalFilters="true"  // Enable time filters
    ShowPresets="true"            // Show saved filters
    OnFilterApplied="HandleFilter" // Filter applied callback
/>
```

#### Filter Types
- **Spatial** - Extent, polygon, buffer, distance
- **Attribute** - Equals, contains, range, in list
- **Temporal** - Date range, before, after, between

#### Common Use Cases
- Data exploration
- Complex spatial queries
- Multi-criteria filtering
- Saved query presets

#### Messages Published
- `FilterAppliedMessage` - Filter applied
- `FilterClearedMessage` - Filter cleared
- `AllFiltersClearedMessage` - All cleared

#### Messages Subscribed
- `MapExtentChangedMessage` - Suggest extent filter
- `TimeChangedMessage` - Apply time filter

#### Documentation
[Full HonuaFilterPanel Documentation](../../src/Honua.MapSDK/Components/FilterPanel/README.md)

---

## Search & Navigation

### HonuaSearch

**Geocoding search** with multiple provider support (Nominatim, Mapbox, custom).

#### Description
Address and place search with autocomplete. Automatically flies map to selected result.

#### Key Parameters

```csharp
<HonuaSearch
    MapId="myMap"                 // Map to control
    Provider="GeocodeProvider.Nominatim" // Geocoding provider
    ApiKey="..."                  // Provider API key (if required)
    Placeholder="Search..."       // Input placeholder
    MinCharacters="3"             // Min chars for search
    ShowResultsOnMap="true"       // Show results as markers
    OnResultSelected="HandleResult" // Result selected callback
/>
```

#### Supported Providers
- `Nominatim` - Free OSM-based geocoding
- `Mapbox` - Mapbox Geocoding API
- Custom - Implement `IGeocoder`

#### Common Use Cases
- Address lookup
- Place search
- Location-based navigation
- Point of interest finding

#### Messages Published
- `SearchResultSelectedMessage` - Result selected
- `FlyToRequestMessage` - Navigate to result

#### Documentation
[Full HonuaSearch Documentation](../../src/Honua.MapSDK/Components/Search/README.md)

---

### HonuaBookmarks

**Save and restore** map views with organized folders.

#### Description
Bookmark current map extent, zoom, and bearing. Organize bookmarks in folders with persistent storage.

#### Key Parameters

```csharp
<HonuaBookmarks
    MapId="myMap"                 // Map to bookmark
    StorageType="BookmarkStorage.LocalStorage" // Storage type
    EnableFolders="true"          // Folder organization
    EnableSharing="true"          // Share bookmarks
    OnBookmarkSelected="HandleSelect" // Bookmark clicked
/>
```

#### Common Use Cases
- Quick navigation to favorite views
- Saved viewpoints for presentations
- Standard extents for workflows
- Shared team bookmarks

#### Messages Published
- `BookmarkSelectedMessage` - Bookmark clicked
- `BookmarkCreatedMessage` - New bookmark
- `BookmarkDeletedMessage` - Bookmark removed
- `FlyToRequestMessage` - Navigate to bookmark

#### Messages Subscribed
- `MapExtentChangedMessage` - Capture current view

#### Documentation
[Full HonuaBookmarks Documentation](../../src/Honua.MapSDK/Components/Bookmarks/README.md)

---

### HonuaCoordinateDisplay

**Real-time coordinate tracking** with multiple format support.

#### Description
Shows cursor coordinates in various formats (DD, DMS, UTM, MGRS). Pin coordinates for reference.

#### Key Parameters

```csharp
<HonuaCoordinateDisplay
    MapId="myMap"                 // Map to track
    Format="CoordinateFormat.DecimalDegrees" // Display format
    ShowElevation="true"          // Show elevation
    EnablePinning="true"          // Pin coordinates
    Precision="6"                 // Decimal places
/>
```

#### Coordinate Formats
- `DecimalDegrees` - -122.4194, 37.7749
- `DegreesMinutesSeconds` - 122°25'9.8"W, 37°46'29.6"N
- `UTM` - 10S 551620 4181690
- `MGRS` - 10SEG 51620 81690

#### Common Use Cases
- Coordinate reference
- Location verification
- Coordinate system conversion
- Coordinate sharing

#### Messages Published
- `CoordinateClickedMessage` - Map clicked
- `CoordinatePinnedMessage` - Coordinate pinned

#### Documentation
[Full HonuaCoordinateDisplay Documentation](../../src/Honua.MapSDK/Components/CoordinateDisplay/README.md)

---

### HonuaTimeline

**Temporal data visualization** with animation and playback controls.

#### Description
Visualize time-series data with play/pause, speed control, and time filtering. Automatically filters map and other components by time.

#### Key Parameters

```csharp
<HonuaTimeline
    SyncWith="mapId"              // Map to sync with
    TimeField="timestamp"         // Field containing timestamp
    StartTime="@startDate"        // Timeline start
    EndTime="@endDate"            // Timeline end
    StepInterval="TimeSpan.FromHours(1)" // Animation step
    PlaybackSpeed="1.0"           // Speed multiplier
    EnableLoop="true"             // Loop playback
    ShowStepControls="true"       // Step forward/back
    OnTimeChanged="HandleTimeChange" // Time position changed
/>
```

#### Common Use Cases
- Time-series animation
- Historical data playback
- Temporal pattern analysis
- Event sequence visualization

#### Messages Published
- `TimeChangedMessage` - Time position changed
- `TimelineStateChangedMessage` - Play/pause state
- `FilterAppliedMessage` - Time-based filter

#### Messages Subscribed
- `DataLoadedMessage` - Initialize timeline range

#### Documentation
[Full HonuaTimeline Documentation](../../src/Honua.MapSDK/Components/Timeline/README.md)

---

## Drawing & Editing

### HonuaDraw

**Sketch tools** for drawing and measuring features on the map.

#### Description
Interactive drawing tools for points, lines, polygons, circles, rectangles, and freehand. Includes measurement tools.

#### Key Parameters

```csharp
<HonuaDraw
    MapId="myMap"                 // Map to draw on
    EnabledModes="@modes"         // Available drawing modes
    DefaultMode="DrawMode.Polygon" // Initial mode
    Style="@drawStyle"            // Drawing style
    EnableMeasurements="true"     // Show measurements
    EnableSnapping="true"         // Snap to features
    OnFeatureDrawn="HandleDrawn"  // Drawing complete
    OnFeatureMeasured="HandleMeasured" // Measurement complete
/>
```

#### Drawing Modes
- `Point` - Single point
- `Line` - Polyline
- `Polygon` - Polygon
- `Circle` - Circle (radius)
- `Rectangle` - Rectangle
- `Freehand` - Freehand drawing
- `Text` - Text annotation

#### Common Use Cases
- Markup and annotations
- Area/distance measurements
- Region of interest definition
- Quick spatial queries

#### Messages Published
- `FeatureDrawnMessage` - Feature completed
- `FeatureMeasuredMessage` - Measurement complete
- `DrawModeChangedMessage` - Mode changed

#### Messages Subscribed
- `StartDrawingRequestMessage` - Start drawing
- `StopDrawingRequestMessage` - Stop drawing

#### Documentation
[Full HonuaDraw Documentation](../../src/Honua.MapSDK/Components/Draw/README.md)

---

### HonuaEditor

**Feature editing** with undo/redo, validation, and transaction support.

#### Description
Full-featured editing interface for creating, updating, and deleting spatial features. Includes edit sessions and validation.

#### Key Parameters

```csharp
<HonuaEditor
    MapId="myMap"                 // Map to edit
    EditableLayers="@layerIds"    // Layers that can be edited
    EnableUndo="true"             // Undo/redo support
    EnableValidation="true"       // Validate on save
    ValidationRules="@rules"      // Validation rules
    EnableTransactions="true"     // Transaction support
    OnFeatureCreated="HandleCreated" // Feature created
    OnFeatureUpdated="HandleUpdated" // Feature updated
    OnFeatureDeleted="HandleDeleted" // Feature deleted
/>
```

#### Edit Operations
- Create new features
- Update geometry
- Update attributes
- Delete features
- Undo/redo changes
- Batch operations

#### Common Use Cases
- Data creation and maintenance
- Feature geometry editing
- Attribute editing
- QA/QC workflows

#### Messages Published
- `FeatureCreatedMessage` - Feature created
- `FeatureUpdatedMessage` - Feature updated
- `FeatureDeletedMessage` - Feature deleted
- `EditSessionStartedMessage` - Session started
- `EditSessionEndedMessage` - Session ended
- `EditValidationErrorMessage` - Validation error

#### Messages Subscribed
- `FeatureSelectedMessage` - Select for editing

#### Documentation
[Full HonuaEditor Documentation](../../src/Honua.MapSDK/Components/Editor/README.md)

---

## Layer Management

### HonuaBasemapGallery

**Basemap selector** with thumbnails and preview.

#### Description
Visual gallery of available basemaps with thumbnail previews. Switch between different map styles.

#### Key Parameters

```csharp
<HonuaBasemapGallery
    MapId="myMap"                 // Map to control
    Basemaps="@basemapList"       // Available basemaps
    ShowThumbnails="true"         // Show preview thumbnails
    ThumbnailSize="100"           // Thumbnail size (px)
    Layout="GalleryLayout.Grid"   // Layout type
    OnBasemapChanged="HandleChange" // Basemap changed
/>
```

#### Common Use Cases
- Map style switching
- Context switching (streets, satellite, terrain)
- Basemap comparison
- Theme selection

#### Messages Published
- `BasemapChangedMessage` - Basemap changed
- `BasemapLoadingMessage` - Loading state

#### Documentation
[Full HonuaBasemapGallery Documentation](../../src/Honua.MapSDK/Components/BasemapGallery/README.md)

---

### HonuaLayerList

**Layer table of contents** with visibility, opacity, and reordering.

#### Description
Hierarchical layer list with visibility toggles, opacity controls, and drag-to-reorder.

#### Key Parameters

```csharp
<HonuaLayerList
    MapId="myMap"                 // Map to control
    ShowOpacityControls="true"    // Show opacity sliders
    EnableReordering="true"       // Drag to reorder
    EnableGrouping="true"         // Group layers
    ShowMetadata="true"           // Show layer info
    OnLayerSelected="HandleSelect" // Layer clicked
/>
```

#### Common Use Cases
- Layer visibility management
- Layer ordering
- Opacity adjustment
- Layer metadata access

#### Messages Published
- `LayerVisibilityChangedMessage` - Visibility toggled
- `LayerOpacityChangedMessage` - Opacity changed
- `LayerReorderedMessage` - Layer order changed
- `LayerSelectedMessage` - Layer clicked

#### Messages Subscribed
- `LayerAddedMessage` - Add to list
- `LayerRemovedMessage` - Remove from list

#### Documentation
[Full HonuaLayerList Documentation](../../src/Honua.MapSDK/Components/LayerList/README.md)

---

### HonuaPopup

**Feature popups** with templating and custom content.

#### Description
Display feature information in popups with customizable templates. Support for HTML content and custom Blazor components.

#### Key Parameters

```csharp
<HonuaPopup
    MapId="myMap"                 // Map to show popups on
    Template="@popupTemplate"     // Popup template
    ShowOnClick="true"            // Show on feature click
    ShowOnHover="false"           // Show on feature hover
    CloseButton="true"            // Show close button
    Anchor="PopupAnchor.Bottom"   // Popup anchor position
    OnOpened="HandleOpened"       // Popup opened
    OnClosed="HandleClosed"       // Popup closed
>
    <PopupContent>
        <MudCard>
            <MudCardHeader>@context.Properties["name"]</MudCardHeader>
            <MudCardContent>
                <!-- Custom content -->
            </MudCardContent>
        </MudCard>
    </PopupContent>
</HonuaPopup>
```

#### Common Use Cases
- Feature information display
- Feature details on click
- Tooltip on hover
- Interactive feature UI

#### Messages Published
- `PopupOpenedMessage` - Popup opened
- `PopupClosedMessage` - Popup closed

#### Messages Subscribed
- `FeatureClickedMessage` - Show popup
- `FeatureHoveredMessage` - Show hover popup
- `OpenPopupRequestMessage` - Open popup
- `ClosePopupRequestMessage` - Close popup

#### Documentation
[Full HonuaPopup Documentation](../../src/Honua.MapSDK/Components/Popup/README.md)

---

## Data Management

### HonuaImportWizard

**Multi-format data import** with preview and validation.

#### Description
Step-by-step wizard for importing GeoJSON, CSV, KML, and Shapefile data. Includes field mapping and validation.

#### Key Parameters

```csharp
<HonuaImportWizard
    MapId="myMap"                 // Map to import to
    SupportedFormats="@formats"   // Allowed file formats
    MaxFileSize="10485760"        // Max file size (bytes)
    EnableValidation="true"       // Validate before import
    EnablePreview="true"          // Show preview step
    OnImportComplete="HandleComplete" // Import completed
    OnImportError="HandleError"   // Import error
/>
```

#### Supported Formats
- **GeoJSON** - Standard GeoJSON features
- **CSV** - With lat/lon columns
- **KML** - Google Earth format
- **Shapefile** - ESRI Shapefile (zipped)

#### Import Steps
1. Upload file
2. Configure field mapping
3. Preview data
4. Import to map

#### Common Use Cases
- Bulk data loading
- User data upload
- Format conversion
- Data migration

#### Messages Published
- `DataImportedMessage` - Import complete
- `ImportProgressMessage` - Progress update
- `ImportErrorMessage` - Import error

#### Documentation
[Full HonuaImportWizard Documentation](../../src/Honua.MapSDK/Components/ImportWizard/README.md)

---

### HonuaAttributeTable

**Full-featured attribute table** with editing, sorting, and filtering.

#### Description
Spreadsheet-like interface for viewing and editing feature attributes. Supports bulk editing and validation.

#### Key Parameters

```csharp
<HonuaAttributeTable
    MapId="myMap"                 // Map to sync with
    LayerId="layerId"             // Layer to display
    EnableEditing="true"          // Enable cell editing
    EnableBulkEdit="true"         // Bulk editing
    EnableExport="true"           // Export to CSV
    ShowGeometry="true"           // Show geometry column
    OnCellEdited="HandleEdit"     // Cell edited
/>
```

#### Features
- In-place cell editing
- Bulk operations
- Column sorting
- Row filtering
- Export to CSV/Excel
- Field calculator

#### Common Use Cases
- Attribute data editing
- Bulk attribute updates
- Data QA/QC
- Data export

#### Messages Published
- `FeatureUpdatedMessage` - Attribute updated
- `DataRowSelectedMessage` - Row selected

#### Messages Subscribed
- `FeatureClickedMessage` - Select row
- `FilterAppliedMessage` - Filter rows

#### Documentation
[Full HonuaAttributeTable Documentation](../../src/Honua.MapSDK/Components/AttributeTable/README.md)

---

## Visualization

### HonuaOverviewMap

**Minimap** showing current extent on the main map.

#### Description
Small overview map showing the current viewport extent. Helps with context and navigation.

#### Key Parameters

```csharp
<HonuaOverviewMap
    MapId="myMap"                 // Main map to overview
    Width="200"                   // Overview map width
    Height="150"                  // Overview map height
    Style="@overviewStyle"        // Overview map style
    ZoomOffset="-5"               // Zoom level offset
    Position="OverviewPosition.BottomRight" // Position
/>
```

#### Common Use Cases
- Navigation context
- Extent indication
- Quick navigation
- Map orientation

#### Messages Published
- `OverviewMapClickedMessage` - Overview map clicked
- `FlyToRequestMessage` - Navigate main map

#### Messages Subscribed
- `MapExtentChangedMessage` - Update extent box

#### Documentation
[Full HonuaOverviewMap Documentation](../../src/Honua.MapSDK/Components/OverviewMap/README.md)

---

### HonuaHeatmap

**Density visualization** for point data.

#### Description
Generate heatmaps from point data to show density patterns and hotspots.

#### Key Parameters

```csharp
<HonuaHeatmap
    MapId="myMap"                 // Map to render on
    Source="@pointData"           // Point data source
    Radius="30"                   // Heat radius (pixels)
    Intensity="1.0"               // Heat intensity
    ColorRamp="@colors"           // Color gradient
    WeightField="value"           // Weight field (optional)
/>
```

#### Common Use Cases
- Point density visualization
- Hotspot analysis
- Clustering visualization
- Pattern identification

---

### HonuaElevationProfile

**Terrain profile** along a line or route.

#### Description
Generate elevation profiles for lines and routes. Shows elevation, grade, and cumulative distance.

#### Key Parameters

```csharp
<HonuaElevationProfile
    MapId="myMap"                 // Map to get route from
    Source="@lineGeometry"        // Line geometry
    ElevationService="@service"   // Elevation service
    ShowGrade="true"              // Show grade/slope
    Units="ProfileUnits.Metric"   // Distance/elevation units
/>
```

#### Common Use Cases
- Route planning
- Terrain analysis
- Hiking trail profiles
- Accessibility analysis

---

### HonuaCompare

**Side-by-side map comparison** with synchronized views.

#### Description
Display two maps side-by-side with synchronized extent and zoom. Perfect for before/after or style comparison.

#### Key Parameters

```csharp
<HonuaCompare
    LeftMapStyle="@beforeStyle"   // Left map style
    RightMapStyle="@afterStyle"   // Right map style
    SyncExtent="true"             // Synchronize views
    SplitRatio="0.5"              // Split position (0-1)
    EnableSwipe="true"            // Swipe between maps
/>
```

#### Common Use Cases
- Before/after comparison
- Basemap style comparison
- Temporal comparison
- Layer comparison

---

## Export & Print

### HonuaPrint

**MapFish Print integration** for PDF export.

#### Description
Generate high-quality PDF maps with legend, scale bar, and custom layout templates.

#### Key Parameters

```csharp
<HonuaPrint
    MapId="myMap"                 // Map to print
    PrintService="@printUrl"      // MapFish Print service URL
    Templates="@templates"        // Available templates
    IncludeLegend="true"          // Include legend
    IncludeScaleBar="true"        // Include scale bar
    Resolution="300"              // DPI
    Format="PrintFormat.PDF"      // Output format
    OnPrintComplete="HandleComplete" // Print complete
/>
```

#### Common Use Cases
- Report generation
- Map export
- High-quality printing
- Map sharing

#### Documentation
[Full HonuaPrint Documentation](../../src/Honua.MapSDK/Components/Print/README.md)

---

## Utility Components

### MapErrorBoundary

**Error boundary** for graceful error handling.

#### Description
Wrap components in error boundaries to prevent entire application crashes and provide retry functionality.

```csharp
<MapErrorBoundary
    ShowResetButton="true"
    ShowTechnicalDetails="@(Environment.IsDevelopment())"
    OnError="HandleError"
    OnRetry="HandleRetry">

    <HonuaMap ... />

</MapErrorBoundary>
```

---

## Component Compatibility Matrix

This matrix shows which components work well together:

|                | Map | Grid | Chart | Legend | Filter | Search | Timeline | Draw | Editor |
|----------------|-----|------|-------|--------|--------|--------|----------|------|--------|
| **Map**        | -   | ✓    | ✓     | ✓      | ✓      | ✓      | ✓        | ✓    | ✓      |
| **Grid**       | ✓   | -    | ✓     | ○      | ✓      | ○      | ✓        | ○    | ○      |
| **Chart**      | ✓   | ✓    | -     | ○      | ✓      | ○      | ✓        | ○    | ○      |
| **Legend**     | ✓   | ○    | ○     | -      | ○      | ○      | ○        | ○    | ○      |
| **Filter**     | ✓   | ✓    | ✓     | ○      | -      | ○      | ✓        | ○    | ○      |
| **Search**     | ✓   | ○    | ○     | ○      | ○      | -      | ○        | ○    | ○      |
| **Timeline**   | ✓   | ✓    | ✓     | ○      | ✓      | ○      | -        | ○    | ○      |
| **Draw**       | ✓   | ○    | ○     | ○      | ○      | ○      | ○        | -    | ○      |
| **Editor**     | ✓   | ○    | ○     | ○      | ○      | ○      | ○        | ○    | -      |

Legend:
- ✓ = Excellent integration
- ○ = Compatible but limited interaction
- - = Same component

## Quick Reference

### By Use Case

**Building a Dashboard**
- HonuaMap (core)
- HonuaDataGrid (data table)
- HonuaChart (analytics)
- HonuaFilterPanel (filtering)
- HonuaLegend (symbology)

**Data Editing Workflow**
- HonuaMap (base)
- HonuaEditor (editing)
- HonuaAttributeTable (attributes)
- HonuaLayerList (layer control)
- HonuaDraw (sketching)

**Location Intelligence**
- HonuaMap (base)
- HonuaSearch (geocoding)
- HonuaBookmarks (saved views)
- HonuaCoordinateDisplay (coordinates)
- HonuaHeatmap (density)

**Temporal Analysis**
- HonuaMap (base)
- HonuaTimeline (time control)
- HonuaChart (time series)
- HonuaDataGrid (filtered data)

**Data Import Pipeline**
- HonuaMap (base)
- HonuaImportWizard (import)
- HonuaAttributeTable (review)
- HonuaChart (validation)

## Further Reading

- [Getting Started](GettingStarted.md) - Learn the basics
- [Architecture](Architecture.md) - Understand how it works
- [Best Practices](BestPractices.md) - Optimal usage patterns
- [API Reference](api/) - Detailed API docs

---

**Need help?** Check the individual component documentation linked above for detailed examples and advanced usage.
