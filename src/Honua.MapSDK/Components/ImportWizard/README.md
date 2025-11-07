# HonuaImportWizard

A comprehensive data import wizard component for the Honua.MapSDK that enables users to upload, preview, configure, and import geographic data from various formats.

## Features

- **Multi-Format Support**: Import from GeoJSON, CSV, TSV, KML, and other formats
- **Smart Detection**: Automatically detects file format and data structure
- **Field Mapping**: Visual interface for mapping source fields to target schema
- **Coordinate Detection**: Automatically identifies latitude/longitude columns
- **Data Preview**: Review data before importing with validation feedback
- **Progress Tracking**: Real-time progress updates during import
- **Error Handling**: Comprehensive validation with detailed error reporting
- **Flexible Display**: Show as inline component or modal dialog
- **ComponentBus Integration**: Publishes messages for coordination with other components

## Basic Usage

```razor
@using Honua.MapSDK.Components.ImportWizard

<!-- Basic inline wizard -->
<HonuaMap Id="map1" />
<HonuaImportWizard SyncWith="map1" />
```

## Dialog Mode

```razor
<!-- Wizard triggered by button -->
<HonuaImportWizard SyncWith="map1"
                   ShowAsDialog="true"
                   TriggerText="Import Data" />
```

## Advanced Configuration

```razor
<HonuaImportWizard SyncWith="map1"
                   MaxFileSize="@(20 * 1024 * 1024)"
                   MaxFeatures="10000"
                   MaxPreviewRows="200"
                   AllowGeocoding="true"
                   AutoZoomToData="true"
                   ShowAsDialog="true"
                   TriggerText="Upload Data"
                   TriggerVariant="Variant.Outlined"
                   TriggerColor="Color.Primary"
                   Elevation="2"
                   OnImportComplete="@HandleImportComplete"
                   OnError="@HandleError" />

@code {
    private async Task HandleImportComplete(ImportResult result)
    {
        Console.WriteLine($"Imported {result.FeaturesImported} features to layer '{result.LayerName}'");

        if (result.Warnings.Any())
        {
            Console.WriteLine($"Import completed with {result.Warnings.Count} warnings");
        }
    }

    private async Task HandleError(string errorMessage)
    {
        Console.Error.WriteLine($"Import error: {errorMessage}");
    }
}
```

## Parameters

### Display Options

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Id` | `string` | Generated GUID | Unique identifier for the component |
| `SyncWith` | `string?` | `null` | Map ID to synchronize with |
| `ShowAsDialog` | `bool` | `false` | Show wizard in a modal dialog |
| `TriggerText` | `string?` | `"Import Data"` | Text for trigger button (dialog mode) |
| `TriggerVariant` | `Variant` | `Variant.Filled` | Trigger button variant |
| `TriggerColor` | `Color` | `Color.Primary` | Trigger button color |
| `Elevation` | `int` | `1` | Paper elevation (inline mode) |
| `CssClass` | `string?` | `null` | Custom CSS class |
| `Style` | `string?` | `null` | Custom inline styles |

### Import Options

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `MaxFileSize` | `long` | `10485760` | Maximum file size in bytes (10 MB) |
| `MaxFeatures` | `int` | `0` | Maximum features to import (0 = unlimited) |
| `MaxPreviewRows` | `int` | `100` | Maximum rows to show in preview |
| `AllowGeocoding` | `bool` | `false` | Enable geocoding for address fields |
| `AutoZoomToData` | `bool` | `true` | Automatically zoom to imported data |

### Callbacks

| Parameter | Type | Description |
|-----------|------|-------------|
| `OnImportComplete` | `EventCallback<ImportResult>` | Invoked when import completes successfully |
| `OnError` | `EventCallback<string>` | Invoked when an error occurs |

## Supported Formats

### File-Based Formats

- **GeoJSON** (`.geojson`, `.json`)
  - Standard geographic JSON format
  - Supports all geometry types
  - Preserves properties and CRS information

- **CSV/TSV** (`.csv`, `.tsv`)
  - Comma or tab-separated values
  - Auto-detects latitude/longitude columns
  - Supports address geocoding
  - Field type detection

- **KML/KMZ** (`.kml`, `.kmz`)
  - Google Earth Keyhole Markup Language
  - Extracts Placemarks and ExtendedData
  - Supports Points, Lines, and Polygons

- **GPX** (`.gpx`)
  - GPS Exchange Format
  - Tracks and waypoints

### Text-Based Formats

- **Pasted Data**
  - Paste GeoJSON directly
  - Paste coordinate lists
  - Quick data entry

### URL-Based Formats

- **Remote URLs**
  - Load GeoJSON from URL
  - Load CSV from URL
  - Load KML from URL

## Wizard Steps

### 1. Upload File

The first step allows users to:
- Drag and drop files
- Browse and select files
- Paste data directly
- Load data from URL

**Features:**
- File format detection
- File size validation
- Visual drag-and-drop zone
- Alternative input methods

### 2. Preview Data

The preview step shows:
- Feature count and statistics
- Field definitions with detected types
- Data preview table (first 100 rows)
- Validation errors and warnings
- Format and encoding information

**Validations:**
- Valid geometry check
- Coordinate range validation
- Field type compatibility
- Encoding detection

### 3. Configure Import

Configuration options include:

**Layer Settings:**
- Layer name
- Geometry type (Point, Line, Polygon)

**Geometry Source:**
- Use existing geometry in file
- Create from Lat/Lon columns (auto-detected)
- Geocode from address column
- Parse from WKT column

**Field Mapping:**
- Map source fields to target names
- Set field data types
- View sample values
- Configure required fields

**Import Options:**
- Skip rows with errors
- Auto-zoom to data
- Maximum feature limit

### 4. Import Progress

Real-time progress display showing:
- Current feature being processed
- Progress percentage
- Completed steps checklist
- Cancel option

### 5. Import Complete

Success or error summary with:
- Features imported count
- Failed features count
- Warnings and errors
- Import duration
- Actions: View on Map, Import More, Close

## ComponentBus Messages

The component publishes and subscribes to the following messages:

### Published Messages

**`DataImportedMessage`**
```csharp
new DataImportedMessage
{
    ComponentId = "import-wizard-id",
    LayerId = "imported-layer-id",
    LayerName = "My Imported Data",
    FeatureCount = 100,
    Format = "GeoJSON",
    Metadata = new Dictionary<string, object>()
}
```

**`ImportProgressMessage`**
```csharp
new ImportProgressMessage
{
    ComponentId = "import-wizard-id",
    Current = 50,
    Total = 100,
    Status = "Processing features...",
    Percentage = 50.0
}
```

**`LayerAddedMessage`**
```csharp
new LayerAddedMessage
{
    LayerId = "imported-layer-id",
    LayerName = "My Imported Data"
}
```

**`FitBoundsRequestMessage`** (when AutoZoom is enabled)
```csharp
new FitBoundsRequestMessage
{
    MapId = "map1",
    Bounds = new[] { -122.5, 37.7, -122.3, 37.9 },
    Padding = 50
}
```

### Subscribed Messages

**`MapReadyMessage`**
- Listens for map initialization to enable auto-zoom functionality

## CSV Import Features

### Coordinate Detection

The CSV parser automatically detects coordinate columns by checking for common field names:

**Latitude:** `lat`, `latitude`, `y`, `coords_lat`, `Lat`, `LATITUDE`
**Longitude:** `lon`, `lng`, `longitude`, `x`, `coords_lon`, `Lon`, `LONGITUDE`

### Field Type Detection

The parser analyzes values to determine field types:

- **String**: Default for text data
- **Number**: Decimal values (e.g., `123.45`)
- **Integer**: Whole numbers (e.g., `42`)
- **Boolean**: `true`/`false` values
- **Date**: Date values (e.g., `2024-01-15`)
- **DateTime**: Date and time (e.g., `2024-01-15 14:30:00`)

### Address Geocoding

When enabled, the wizard can geocode address columns:

1. Detects likely address columns
2. Shows geocoding option in configuration step
3. Processes addresses in batches
4. Shows progress during geocoding
5. Handles failed geocodes gracefully

## Error Handling

The wizard provides comprehensive error handling with three severity levels:

### Error Severities

1. **Error** (❌)
   - Invalid geometry
   - Coordinate out of range
   - Missing required field
   - Parse failure

2. **Warning** (⚠️)
   - Missing optional field
   - Unexpected value type
   - Encoding issue
   - Non-critical validation failure

3. **Info** (ℹ️)
   - Format detection notice
   - Field type suggestion
   - CRS information
   - Performance tip

### Error Display

Errors are shown:
- In the preview step summary
- Expandable error list
- Per-row in validation
- In import results

### Skip Errors Option

When enabled, rows with errors are skipped during import, allowing partial imports to succeed.

## Styling

The component includes comprehensive CSS with:

- Modern wizard design
- Smooth step transitions
- Drag-and-drop visual feedback
- Responsive layout (mobile-friendly)
- Dark mode support
- Loading states and animations
- High contrast accessibility

### Custom Styling

```razor
<HonuaImportWizard SyncWith="map1"
                   CssClass="my-custom-wizard"
                   Style="max-width: 1200px; margin: 0 auto;" />
```

```css
.my-custom-wizard {
    border-radius: 12px;
    box-shadow: 0 4px 20px rgba(0,0,0,0.1);
}

.my-custom-wizard .drop-zone {
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    color: white;
}
```

## Accessibility

The component is fully accessible with:

- ARIA labels on all interactive elements
- Keyboard navigation through wizard steps
- Focus management between steps
- Screen reader announcements for progress
- High contrast mode support
- Semantic HTML structure

## Performance

### Large File Handling

For large datasets:
- Streaming parser for memory efficiency
- Chunk processing for responsiveness
- Progress updates during parsing
- Cancellation support

### Optimization Tips

1. Set `MaxPreviewRows` to a lower value for very large files
2. Use `MaxFeatures` to limit import size
3. Enable `SkipErrors` for faster processing of imperfect data
4. Consider splitting very large files into smaller chunks

## Examples

See [Examples.md](./Examples.md) for comprehensive usage examples.

## Troubleshooting

### File Won't Upload

- Check file size is under `MaxFileSize` limit
- Verify file extension is supported
- Ensure file is not corrupted
- Check browser console for errors

### Coordinates Not Detected

- Ensure column names match common patterns (lat, lon, latitude, longitude)
- Verify values are numeric
- Check coordinate ranges (-90 to 90 for lat, -180 to 180 for lon)
- Manually select columns in Configure step

### Import Fails

- Review validation errors in Preview step
- Check error details in Import results
- Enable `SkipErrors` to import partial data
- Verify file format is correct

### Performance Issues

- Reduce `MaxPreviewRows` for large files
- Set `MaxFeatures` limit
- Split large files into smaller batches
- Close other browser tabs to free memory

## API Reference

For detailed API documentation, see the component's XML documentation comments in the source code.
