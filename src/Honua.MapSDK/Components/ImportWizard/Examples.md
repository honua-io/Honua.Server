# HonuaImportWizard - Usage Examples

Comprehensive examples for using the HonuaImportWizard component.

## Table of Contents

1. [Basic Usage](#basic-usage)
2. [Dialog Mode](#dialog-mode)
3. [CSV Import](#csv-import)
4. [GeoJSON Import](#geojson-import)
5. [Custom Configuration](#custom-configuration)
6. [Event Handling](#event-handling)
7. [Integration with Other Components](#integration-with-other-components)
8. [Advanced Scenarios](#advanced-scenarios)

## Basic Usage

### Inline Wizard

```razor
@page "/import"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.ImportWizard

<PageTitle>Import Data</PageTitle>

<div class="import-page">
    <MudContainer MaxWidth="MaxWidth.ExtraLarge">
        <MudGrid>
            <MudItem xs="12" md="8">
                <HonuaMap Id="mainMap"
                          Style="height: 600px;"
                          Zoom="10"
                          Center="@(new[] { -122.4194, 37.7749 })" />
            </MudItem>

            <MudItem xs="12" md="4">
                <HonuaImportWizard SyncWith="mainMap" />
            </MudItem>
        </MudGrid>
    </MudContainer>
</div>
```

### Full-Width Layout

```razor
@page "/import-fullwidth"

<div class="import-layout">
    <div class="map-container">
        <HonuaMap Id="map1" Style="height: 100vh;" />
    </div>

    <div class="wizard-container">
        <HonuaImportWizard SyncWith="map1"
                           Elevation="3"
                           Style="max-width: 800px; margin: 20px auto;" />
    </div>
</div>

<style>
    .import-layout {
        display: grid;
        grid-template-columns: 1fr 500px;
        height: 100vh;
    }

    .wizard-container {
        overflow-y: auto;
        padding: 20px;
        background-color: var(--mud-palette-background);
    }
</style>
```

## Dialog Mode

### Basic Dialog

```razor
<HonuaMap Id="map1" Style="height: 600px;" />

<div class="toolbar">
    <HonuaImportWizard SyncWith="map1"
                       ShowAsDialog="true"
                       TriggerText="Import Data" />
</div>
```

### Custom Trigger Button

```razor
<HonuaImportWizard SyncWith="map1"
                   ShowAsDialog="true"
                   TriggerText="📁 Upload GeoData"
                   TriggerVariant="Variant.Outlined"
                   TriggerColor="Color.Secondary" />
```

### Toolbar Integration

```razor
<MudAppBar Elevation="1">
    <MudIconButton Icon="@Icons.Material.Filled.Menu" Color="Color.Inherit" Edge="Edge.Start" />
    <MudText Typo="Typo.h6">Map Application</MudText>
    <MudSpacer />

    <HonuaImportWizard SyncWith="mainMap"
                       ShowAsDialog="true"
                       TriggerText="Import"
                       TriggerVariant="Variant.Text"
                       TriggerColor="Color.Inherit" />

    <MudIconButton Icon="@Icons.Material.Filled.Settings" Color="Color.Inherit" />
</MudAppBar>

<HonuaMap Id="mainMap" Style="height: calc(100vh - 64px);" />
```

## CSV Import

### Basic CSV with Coordinates

```razor
@page "/import-csv"

<HonuaMap Id="csvMap" Style="height: 600px;" />

<HonuaImportWizard SyncWith="csvMap"
                   ShowAsDialog="true"
                   TriggerText="Import CSV"
                   OnImportComplete="@OnCsvImported" />

@code {
    private void OnCsvImported(ImportResult result)
    {
        Console.WriteLine($"Imported {result.FeaturesImported} locations from CSV");
    }
}
```

**Sample CSV file:**
```csv
name,latitude,longitude,category,value
Location A,37.7749,-122.4194,Restaurant,85
Location B,37.7849,-122.4094,Shop,92
Location C,37.7949,-122.3994,Park,78
```

### CSV with Address Geocoding

```razor
<HonuaImportWizard SyncWith="map1"
                   AllowGeocoding="true"
                   ShowAsDialog="true"
                   TriggerText="Import Addresses"
                   OnImportComplete="@HandleAddressImport" />

@code {
    private async Task HandleAddressImport(ImportResult result)
    {
        if (result.Success)
        {
            await ShowSnackbar($"Geocoded {result.FeaturesImported} addresses");
        }
    }
}
```

**Sample CSV with addresses:**
```csv
id,name,address,city,state,zip
1,Store A,123 Main St,San Francisco,CA,94102
2,Store B,456 Market St,San Francisco,CA,94103
3,Store C,789 Mission St,San Francisco,CA,94104
```

## GeoJSON Import

### Remote GeoJSON URL

```razor
<HonuaImportWizard SyncWith="map1"
                   ShowAsDialog="true"
                   TriggerText="Load GeoJSON"
                   MaxFeatures="5000"
                   OnImportComplete="@OnGeoJsonLoaded" />

@code {
    private async Task OnGeoJsonLoaded(ImportResult result)
    {
        Console.WriteLine($"Loaded {result.FeaturesImported} features from GeoJSON");
        Console.WriteLine($"Bounding box: {string.Join(", ", result.BoundingBox ?? Array.Empty<double>())}");
    }
}
```

### Large GeoJSON Files

```razor
<!-- For large GeoJSON files, increase limits and show progress -->
<HonuaImportWizard SyncWith="map1"
                   MaxFileSize="@(50 * 1024 * 1024)"
                   MaxFeatures="50000"
                   MaxPreviewRows="50"
                   ShowAsDialog="true"
                   TriggerText="Import Large Dataset" />
```

## Custom Configuration

### Preset Import Configuration

```razor
<HonuaImportWizard SyncWith="map1"
                   ShowAsDialog="true"
                   MaxFileSize="@(20 * 1024 * 1024)"
                   MaxFeatures="10000"
                   MaxPreviewRows="200"
                   AllowGeocoding="true"
                   AutoZoomToData="true"
                   OnImportComplete="@HandleImport"
                   OnError="@HandleError" />

@code {
    private async Task HandleImport(ImportResult result)
    {
        if (result.Success)
        {
            // Custom post-import processing
            await ProcessImportedData(result);
        }
    }

    private async Task HandleError(string errorMessage)
    {
        await ShowErrorDialog(errorMessage);
    }

    private async Task ProcessImportedData(ImportResult result)
    {
        // Add custom styling to imported layer
        // Analyze imported data
        // Create summary statistics
        await Task.CompletedTask;
    }
}
```

## Event Handling

### Complete Import Workflow

```razor
@using Honua.MapSDK.Models.Import

<HonuaImportWizard SyncWith="map1"
                   ShowAsDialog="true"
                   TriggerText="Import Data"
                   OnImportComplete="@OnImportComplete"
                   OnError="@OnImportError" />

<MudSnackbarProvider />

@code {
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private async Task OnImportComplete(ImportResult result)
    {
        if (result.Success)
        {
            Snackbar.Add(
                $"Successfully imported {result.FeaturesImported} features to '{result.LayerName}'",
                Severity.Success,
                config => config.VisibleStateDuration = 5000
            );

            // Log import details
            await LogImport(result);

            // Update statistics
            await UpdateStatistics(result);

            // Notify other systems
            await NotifyImportComplete(result);
        }
        else
        {
            Snackbar.Add(
                $"Import failed: {result.Errors.FirstOrDefault()?.Message ?? "Unknown error"}",
                Severity.Error
            );

            // Log errors for debugging
            await LogErrors(result);
        }
    }

    private async Task OnImportError(string errorMessage)
    {
        Snackbar.Add($"Import error: {errorMessage}", Severity.Error);
        await LogError(errorMessage);
    }

    private async Task LogImport(ImportResult result)
    {
        Console.WriteLine($"Import Statistics:");
        Console.WriteLine($"  Format: {result.SourceFormat}");
        Console.WriteLine($"  File: {result.SourceFileName}");
        Console.WriteLine($"  Features: {result.FeaturesImported}/{result.TotalFeatures}");
        Console.WriteLine($"  Duration: {result.Duration.TotalSeconds:F2}s");
        Console.WriteLine($"  Warnings: {result.Warnings.Count}");

        if (result.BoundingBox != null)
        {
            Console.WriteLine($"  Bounds: [{string.Join(", ", result.BoundingBox)}]");
        }

        await Task.CompletedTask;
    }

    private async Task UpdateStatistics(ImportResult result)
    {
        // Update application statistics
        await Task.CompletedTask;
    }

    private async Task NotifyImportComplete(ImportResult result)
    {
        // Send notification to other parts of the application
        await Task.CompletedTask;
    }

    private async Task LogErrors(ImportResult result)
    {
        foreach (var error in result.Errors)
        {
            Console.Error.WriteLine($"Row {error.RowNumber}: {error.Message}");
        }
        await Task.CompletedTask;
    }

    private async Task LogError(string message)
    {
        Console.Error.WriteLine($"Import error: {message}");
        await Task.CompletedTask;
    }
}
```

## Integration with Other Components

### With Legend Component

```razor
<MudGrid>
    <MudItem xs="12" md="9">
        <HonuaMap Id="map1" Style="height: 600px;" />
    </MudItem>

    <MudItem xs="12" md="3">
        <MudStack Spacing="3">
            <HonuaLegend SyncWith="map1" Title="Map Layers" />

            <MudDivider />

            <HonuaImportWizard SyncWith="map1"
                               ShowAsDialog="true"
                               TriggerText="Add Layer"
                               TriggerVariant="Variant.Outlined"
                               OnImportComplete="@OnLayerAdded" />
        </MudStack>
    </MudItem>
</MudGrid>

@code {
    private async Task OnLayerAdded(ImportResult result)
    {
        // Layer will automatically appear in legend via ComponentBus
        Console.WriteLine($"New layer '{result.LayerName}' added to map");
        await Task.CompletedTask;
    }
}
```

### With Data Grid

```razor
<MudGrid>
    <MudItem xs="12">
        <MudPaper Class="pa-2 mb-2">
            <HonuaImportWizard SyncWith="map1"
                               ShowAsDialog="true"
                               TriggerText="Import Data" />
        </MudPaper>
    </MudItem>

    <MudItem xs="12" md="8">
        <HonuaMap Id="map1" Style="height: 600px;" />
    </MudItem>

    <MudItem xs="12" md="4">
        <HonuaDataGrid SyncWith="map1"
                       Title="Imported Features"
                       ShowToolbar="true"
                       ShowExport="true" />
    </MudItem>
</MudGrid>
```

### With Filter Panel

```razor
<MudDrawer @bind-Open="@_drawerOpen" Anchor="Anchor.Right" Width="400px" Variant="@DrawerVariant.Temporary">
    <MudDrawerHeader>
        <MudText Typo="Typo.h6">Tools</MudText>
    </MudDrawerHeader>

    <MudStack Class="pa-4" Spacing="3">
        <HonuaImportWizard SyncWith="map1"
                           ShowAsDialog="true"
                           TriggerText="Import Data"
                           TriggerVariant="Variant.Outlined" />

        <MudDivider />

        <HonuaFilterPanel SyncWith="map1" />
    </MudStack>
</MudDrawer>

<HonuaMap Id="map1" Style="height: 100vh;" />

<MudFab Color="Color.Primary"
        StartIcon="@Icons.Material.Filled.Menu"
        Style="position: fixed; right: 20px; top: 20px;"
        OnClick="@(() => _drawerOpen = !_drawerOpen)" />

@code {
    private bool _drawerOpen = false;
}
```

## Advanced Scenarios

### Multi-Map Import

```razor
<MudGrid>
    <MudItem xs="12" md="6">
        <MudPaper Class="pa-2">
            <MudText Typo="Typo.h6">Map 1</MudText>
            <HonuaMap Id="map1" Style="height: 400px;" />
            <HonuaImportWizard SyncWith="map1"
                               ShowAsDialog="true"
                               TriggerText="Import to Map 1" />
        </MudPaper>
    </MudItem>

    <MudItem xs="12" md="6">
        <MudPaper Class="pa-2">
            <MudText Typo="Typo.h6">Map 2</MudText>
            <HonuaMap Id="map2" Style="height: 400px;" />
            <HonuaImportWizard SyncWith="map2"
                               ShowAsDialog="true"
                               TriggerText="Import to Map 2" />
        </MudPaper>
    </MudItem>
</MudGrid>
```

### Batch Import

```razor
@page "/batch-import"

<HonuaMap Id="map1" Style="height: 600px;" />

<MudPaper Class="pa-4 mt-4">
    <MudText Typo="Typo.h6" Class="mb-3">Batch Import</MudText>

    <MudFileUpload T="IReadOnlyList<IBrowserFile>"
                   OnFilesChanged="@OnFilesSelected"
                   Accept=".geojson,.csv,.kml"
                   MaximumFileCount="10">
        <ButtonTemplate>
            <MudButton HtmlTag="label"
                       Variant="Variant.Filled"
                       Color="Color.Primary"
                       StartIcon="@Icons.Material.Filled.UploadFile"
                       for="@context">
                Select Multiple Files
            </MudButton>
        </ButtonTemplate>
    </MudFileUpload>

    @if (_importQueue.Any())
    {
        <MudList Class="mt-4">
            @foreach (var item in _importQueue)
            {
                <MudListItem>
                    <div class="d-flex align-center justify-space-between">
                        <MudText>@item.FileName</MudText>
                        @if (item.Status == "Pending")
                        {
                            <MudChip Size="Size.Small">Pending</MudChip>
                        }
                        else if (item.Status == "Processing")
                        {
                            <MudProgressCircular Size="Size.Small" Indeterminate="true" />
                        }
                        else if (item.Status == "Complete")
                        {
                            <MudChip Size="Size.Small" Color="Color.Success">Complete</MudChip>
                        }
                        else
                        {
                            <MudChip Size="Size.Small" Color="Color.Error">Failed</MudChip>
                        }
                    </div>
                </MudListItem>
            }
        </MudList>

        <MudButton Variant="Variant.Filled"
                   Color="Color.Primary"
                   Class="mt-2"
                   OnClick="@ProcessBatch"
                   Disabled="@_isProcessing">
            Import All (@_importQueue.Count files)
        </MudButton>
    }
</MudPaper>

@code {
    private List<ImportQueueItem> _importQueue = new();
    private bool _isProcessing = false;

    private class ImportQueueItem
    {
        public string FileName { get; set; } = string.Empty;
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string Status { get; set; } = "Pending";
    }

    private async Task OnFilesSelected(InputFileChangeEventArgs e)
    {
        _importQueue.Clear();

        foreach (var file in e.GetMultipleFiles(10))
        {
            using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);

            _importQueue.Add(new ImportQueueItem
            {
                FileName = file.Name,
                Content = ms.ToArray(),
                Status = "Pending"
            });
        }
    }

    private async Task ProcessBatch()
    {
        _isProcessing = true;

        // Note: In a real implementation, you would use the FileParserFactory
        // and directly call the import logic for each file
        foreach (var item in _importQueue)
        {
            item.Status = "Processing";
            StateHasChanged();

            await Task.Delay(1000); // Simulate import

            item.Status = "Complete";
            StateHasChanged();
        }

        _isProcessing = false;
    }
}
```

### Custom Field Transformations

```razor
@using Honua.MapSDK.Services.Import

<HonuaImportWizard SyncWith="map1"
                   ShowAsDialog="true"
                   TriggerText="Import with Transformations"
                   OnImportComplete="@OnImportWithTransform" />

@code {
    private async Task OnImportWithTransform(ImportResult result)
    {
        // Apply custom transformations after import
        // - Convert units (e.g., feet to meters)
        // - Normalize values
        // - Calculate derived fields
        // - Filter outliers

        Console.WriteLine("Applying custom transformations...");

        await Task.CompletedTask;
    }
}
```

### Import with Validation Rules

```razor
<HonuaImportWizard SyncWith="map1"
                   ShowAsDialog="true"
                   MaxFeatures="1000"
                   OnImportComplete="@ValidateAndImport" />

@code {
    private async Task ValidateAndImport(ImportResult result)
    {
        // Custom validation after import
        var validationErrors = new List<string>();

        // Check for required fields
        // Validate coordinate ranges
        // Check for duplicates
        // Verify data quality

        if (validationErrors.Any())
        {
            await ShowValidationErrors(validationErrors);
        }
        else
        {
            await FinalizeImport(result);
        }
    }

    private async Task ShowValidationErrors(List<string> errors)
    {
        // Show validation errors to user
        await Task.CompletedTask;
    }

    private async Task FinalizeImport(ImportResult result)
    {
        // Finalize the import
        Console.WriteLine("Import validated and finalized");
        await Task.CompletedTask;
    }
}
```

## Sample Data

### Sample GeoJSON

```json
{
  "type": "FeatureCollection",
  "features": [
    {
      "type": "Feature",
      "geometry": {
        "type": "Point",
        "coordinates": [-122.4194, 37.7749]
      },
      "properties": {
        "name": "San Francisco",
        "population": 883305,
        "category": "City"
      }
    }
  ]
}
```

### Sample CSV

```csv
id,name,latitude,longitude,type,value,date
1,Point A,37.7749,-122.4194,Store,1250,2024-01-15
2,Point B,37.7849,-122.4094,Restaurant,890,2024-01-16
3,Point C,37.7649,-122.4294,Park,0,2024-01-17
```

### Sample KML

```xml
<?xml version="1.0" encoding="UTF-8"?>
<kml xmlns="http://www.opengis.net/kml/2.2">
  <Document>
    <Placemark>
      <name>Location A</name>
      <description>Sample location</description>
      <Point>
        <coordinates>-122.4194,37.7749,0</coordinates>
      </Point>
    </Placemark>
  </Document>
</kml>
```

## Best Practices

1. **Set Appropriate Limits**
   - Use `MaxFileSize` to prevent memory issues
   - Set `MaxFeatures` for large datasets
   - Adjust `MaxPreviewRows` based on performance

2. **Handle Errors Gracefully**
   - Always implement `OnError` callback
   - Provide user-friendly error messages
   - Log errors for debugging

3. **Optimize User Experience**
   - Use dialog mode for cleaner UI
   - Show import progress for large files
   - Auto-zoom to imported data
   - Provide immediate visual feedback

4. **Validate Data**
   - Review data in preview step
   - Check coordinate ranges
   - Verify field types
   - Enable skip errors for partial imports

5. **Integration**
   - Coordinate with other components via ComponentBus
   - Update legends and data grids automatically
   - Maintain consistent layer naming
   - Document custom workflows

## Next Steps

- Explore [README.md](./README.md) for detailed API documentation
- Check component source code for advanced customization
- Review MapSDK documentation for integration patterns
- Test with your own data files
