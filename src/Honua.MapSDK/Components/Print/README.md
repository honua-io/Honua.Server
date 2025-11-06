# HonuaPrint - Map Printing Component

Professional map printing component that integrates with **MapFish Print** backend service for high-quality PDF and image exports.

## Features

- **Print Configuration Dialog** - Comprehensive UI for print settings
- **Multiple Output Formats** - PDF, PNG, JPEG
- **Paper Sizes** - A3, A4, A5, Letter, Legal, Tabloid, Custom
- **Orientation** - Portrait or Landscape
- **DPI/Quality Settings** - 72, 150, 300, 600 DPI
- **Map Extent Options** - Current view, custom extent, fit all features
- **Scale Selection** - Common map scales from 1:1,000 to 1:500,000
- **Map Elements** - Legend, scale bar, north arrow, attribution
- **Live Preview** - See how your print will look before generating
- **Template Support** - Multiple layout templates from MapFish Print
- **Job Progress Tracking** - Real-time status updates during print generation
- **Auto-Download** - Automatic file download when complete
- **ComponentBus Integration** - Syncs with map state changes

## Quick Start

### 1. Configure MapFish Print Backend

First, ensure you have MapFish Print running. See [MapFish Print Documentation](https://mapfish.github.io/mapfish-print-doc/) for setup instructions.

```bash
# Docker example
docker run -p 8080:8080 camptocamp/mapfish_print:latest
```

### 2. Register Service

```csharp
// In Program.cs or Startup.cs
builder.Services.AddHttpClient<IMapFishPrintService, MapFishPrintService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:8080");
});
```

### 3. Add Component

```razor
@using Honua.MapSDK.Components.Print

<HonuaMap Id="myMap" MapStyle="..." />

<HonuaPrint SyncWith="myMap"
            PrintServiceUrl="http://localhost:8080/print"
            PrintApp="default" />
```

## Usage Examples

### Basic Print Button

```razor
<HonuaPrint SyncWith="map1"
            ButtonText="Print Map"
            ButtonClass="my-custom-class" />
```

### With Event Handlers

```razor
<HonuaPrint SyncWith="map1"
            OnPrintComplete="HandlePrintComplete"
            OnPrintError="HandlePrintError" />

@code {
    private async Task HandlePrintComplete(PrintJobStatus status)
    {
        Console.WriteLine($"Print completed! Job ID: {status.JobId}");
        // Show success message
    }

    private async Task HandlePrintError(string error)
    {
        Console.WriteLine($"Print failed: {error}");
        // Show error notification
    }
}
```

### Custom Configuration

```razor
<HonuaPrint SyncWith="map1"
            PrintServiceUrl="https://print.mycompany.com/print"
            PrintApp="custom-app"
            ButtonText="Export PDF"
            Disabled="@_isLoading" />
```

## Component Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `SyncWith` | `string?` | `null` | ID of map to sync with |
| `Id` | `string?` | `null` | Component identifier |
| `ButtonText` | `string` | `"Print"` | Text for the print button |
| `ButtonClass` | `string` | `""` | CSS class for the button |
| `Disabled` | `bool` | `false` | Disable the print button |
| `PrintServiceUrl` | `string` | `"/print"` | MapFish Print service URL |
| `PrintApp` | `string` | `"default"` | MapFish Print app name |
| `OnPrintComplete` | `EventCallback<PrintJobStatus>` | - | Called when print completes |
| `OnPrintError` | `EventCallback<string>` | - | Called when print fails |

## Print Configuration Options

### Basic Settings

- **Title** - Map title (appears at top)
- **Description** - Map description/subtitle
- **Author** - Creator name
- **Copyright** - Attribution text

### Page Settings

- **Paper Size** - A3, A4, A5, Letter, Legal, Tabloid, Custom
- **Orientation** - Portrait or Landscape
- **Output Format** - PDF, PNG, JPEG
- **DPI** - 72 (screen), 150 (draft), 300 (standard), 600 (high)
- **Layout Template** - Select from available MapFish Print templates

### Map Settings

- **Map Extent** - Current view, custom extent, or fit all features
- **Map Scale** - 1:1,000 to 1:500,000
- **Rotation** - Map bearing in degrees (0-360)

### Options

- **Include Legend** - Add map legend to output
- **Include Scale Bar** - Add scale bar
- **Include North Arrow** - Add north arrow indicator
- **Include Attribution** - Add copyright/attribution text

## MapFish Print Integration

### Configuration File

HonuaPrint expects a MapFish Print configuration with layouts like:

```yaml
# config.yaml
apps:
  - name: default
    layouts:
      - name: default
        label: "Default Layout"
        map:
          width: 800
          height: 600
          maxDPI: 600
        attributes:
          - name: title
            type: String
          - name: description
            type: String
          - name: author
            type: String
          - name: copyright
            type: String
```

### API Endpoints

HonuaPrint calls these MapFish Print endpoints:

- `GET /print/{app}/capabilities.json` - Get available layouts and formats
- `POST /print/{app}/report.{format}` - Submit print job
- `GET /print/{app}/status/{jobId}.json` - Check job status
- `GET /print/{app}/report/{jobId}` - Download completed print
- `DELETE /print/{app}/cancel/{jobId}` - Cancel print job

## Workflow

### 1. User Opens Dialog

Click the "Print" button to open the configuration dialog.

### 2. Configure Print Settings

- **Basic Tab** - Set title, description, author, copyright
- **Page Tab** - Choose paper size, orientation, format, DPI, template
- **Map Tab** - Select extent mode, scale, rotation
- **Options Tab** - Toggle legend, scale bar, north arrow, attribution

### 3. Generate Preview

Click "Generate Preview" or "Refresh Preview" to see a visual preview of the map as it will appear in the print output.

### 4. Submit Print Job

Click "Print" to submit the job to MapFish Print backend.

### 5. Monitor Progress

A progress dialog shows:
- Job status (Pending → Processing → Completed/Failed)
- Progress bar
- Status messages
- Any errors

### 6. Auto-Download

When the job completes, the file automatically downloads to the user's computer.

## Advanced Usage

### Custom Templates

Define custom templates in your MapFish Print config:

```yaml
layouts:
  - name: detailed
    label: "Detailed Report"
    map:
      width: 1000
      height: 800
    attributes:
      - name: project_name
        type: String
      - name: report_date
        type: Date
      - name: custom_field
        type: String
```

Use custom attributes:

```razor
<HonuaPrint SyncWith="map1"
            OnPrintComplete="ConfigureCustomAttributes" />

@code {
    private PrintConfiguration _config;

    protected override void OnInitialized()
    {
        _config = new PrintConfiguration
        {
            Layout = "detailed",
            Attributes = new Dictionary<string, object>
            {
                ["project_name"] = "Highway Expansion",
                ["report_date"] = DateTime.Today,
                ["custom_field"] = "Custom Value"
            }
        };
    }
}
```

### Programmatic Printing

Trigger print programmatically without user interaction:

```csharp
@inject IMapFishPrintService PrintService

private async Task ExportMapToPdf()
{
    var config = new PrintConfiguration
    {
        Title = "Automated Export",
        PaperSize = PaperSize.A4,
        Orientation = PageOrientation.Landscape,
        Format = PrintFormat.Pdf,
        Dpi = 300,
        Scale = 25000,
        Center = new[] { -122.4194, 37.7749 },
        Zoom = 12
    };

    // Submit job
    var jobId = await PrintService.SubmitPrintJobAsync(config);

    // Poll for completion
    PrintJobStatus? status;
    do
    {
        await Task.Delay(2000);
        status = await PrintService.GetJobStatusAsync(jobId);
    } while (status?.Status == PrintJobState.Processing);

    // Download result
    if (status?.Status == PrintJobState.Completed)
    {
        var data = await PrintService.DownloadPrintAsync(jobId);
        // Save to file or return to user
    }
}
```

### Batch Printing

Print multiple maps in sequence:

```csharp
private async Task BatchPrint(List<MapConfiguration> maps)
{
    foreach (var map in maps)
    {
        var config = new PrintConfiguration
        {
            Title = map.Name,
            Center = map.Center,
            Zoom = map.Zoom,
            // ... other settings
        };

        var jobId = await PrintService.SubmitPrintJobAsync(config);

        // Wait for completion
        await WaitForJobCompletion(jobId);
    }
}

private async Task WaitForJobCompletion(string jobId)
{
    PrintJobStatus? status;
    do
    {
        await Task.Delay(2000);
        status = await PrintService.GetJobStatusAsync(jobId);
    } while (status?.Status == PrintJobState.Processing);
}
```

## Styling

### Custom Button Styles

```razor
<HonuaPrint SyncWith="map1"
            ButtonClass="my-print-button" />

<style>
    .my-print-button {
        background: linear-gradient(45deg, #2196F3 30%, #21CBF3 90%);
        color: white;
        font-weight: bold;
        padding: 12px 24px;
        border-radius: 8px;
    }
</style>
```

### Custom Dialog Styles

```css
/* Override dialog width */
.honua-print .mud-dialog-maxwidth-lg {
    max-width: 1400px;
}

/* Custom preview panel */
.honua-print .print-preview-panel {
    background: #f5f5f5;
    border-radius: 8px;
    padding: 16px;
}

/* Custom button colors */
.honua-print .mud-button-root {
    text-transform: none;
    letter-spacing: normal;
}
```

## Performance Considerations

### Optimal Settings

- **Screen Preview**: 72-150 DPI
- **Draft Quality**: 150 DPI
- **Standard Print**: 300 DPI
- **High Quality**: 600 DPI (large files, slow generation)

### Print Times

Approximate generation times for A4 @ 300 DPI:

- Simple basemap: 2-5 seconds
- 1-2 vector layers: 5-15 seconds
- Complex multi-layer map: 15-60 seconds
- High-resolution imagery: 30-120 seconds

### Resource Usage

MapFish Print is CPU and memory intensive:

- Allocate at least 2GB RAM
- Consider horizontal scaling for high volumes
- Use job queues for batch processing
- Cache commonly used templates

## Troubleshooting

### Print Button Disabled

Check that:
- MapFish Print service is running
- `PrintServiceUrl` is correct
- Service is accessible from client (CORS)

### "Failed to submit print job"

- Verify MapFish Print service is online
- Check browser console for CORS errors
- Validate print configuration against capabilities

### Preview Not Generating

- Ensure map has loaded completely
- Check that `SyncWith` ID matches map component ID
- Verify JavaScript file is loaded: `honua-print.js`

### Job Never Completes

- Check MapFish Print logs for errors
- Verify map style URLs are accessible from server
- Check for invalid layer configurations
- Increase MapFish Print timeout settings

### Poor Print Quality

- Increase DPI setting (300+ recommended)
- Use vector data sources when possible
- Ensure base map supports high-resolution tiles
- Check MapFish Print maxDPI setting

## Browser Compatibility

- ✅ Chrome 90+
- ✅ Firefox 88+
- ✅ Safari 14+
- ✅ Edge 90+

Requires:
- Canvas API for screenshots
- Fetch API for downloads
- Promises for async operations

## Security Considerations

### API Access

- Secure MapFish Print endpoint with authentication
- Use HTTPS for production deployments
- Implement rate limiting to prevent abuse

### Input Validation

- Validate all user inputs server-side
- Sanitize title/description fields
- Limit file sizes and dimensions
- Restrict accessible templates

### CORS Configuration

```csharp
// In Program.cs
builder.Services.AddCors(options =>
{
    options.AddPolicy("PrintPolicy", policy =>
    {
        policy.WithOrigins("https://yourapp.com")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

app.UseCors("PrintPolicy");
```

## Related Components

- **HonuaMap** - Main map component for display
- **HonuaLegend** - Auto-generates legend for print
- **HonuaBookmarks** - Save map views for printing
- **HonuaExport** - Alternative export formats (GeoJSON, etc.)

## Resources

- [MapFish Print Documentation](https://mapfish.github.io/mapfish-print-doc/)
- [MapFish Print GitHub](https://github.com/mapfish/mapfish-print)
- [Example Configurations](https://github.com/mapfish/mapfish-print/tree/master/examples)
- [Docker Images](https://hub.docker.com/r/camptocamp/mapfish_print)

## License

Part of Honua.MapSDK - Licensed under MIT License
