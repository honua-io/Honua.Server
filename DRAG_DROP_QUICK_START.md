# Drag-and-Drop Upload - Quick Start Guide

Get started with zero-configuration geospatial data upload in under 5 minutes.

## Installation

The components are already included in Honua.MapSDK. No additional installation required.

## Basic Usage

### 1. Simple File Upload

Add to any Blazor page:

```razor
@page "/upload"
@using Honua.MapSDK.Components.Upload
@using Honua.MapSDK.Models.Import

<DragDropUpload OnDataParsed="@((data) => Console.WriteLine($"Uploaded {data.ValidRows} features"))" />
```

### 2. Upload with Map Visualization

```razor
@page "/visualize"
@using Honua.MapSDK.Components.Upload

<UploadAndVisualize />
```

That's it! Your users can now drag-and-drop GeoJSON, Shapefiles, CSV, KML, and more.

## Features You Get for Free

- ✅ Format auto-detection (10+ formats)
- ✅ Instant visualization on map
- ✅ Auto-generated styling
- ✅ Progress indicators
- ✅ Error handling
- ✅ CSV geometry detection
- ✅ CRS detection and conversion
- ✅ Mobile-friendly interface

## Example with All Options

```razor
<UploadAndVisualize
    MaxFileSizeMB="500"
    ShowMap="true"
    ShowDataSummary="true"
    ShowLayerControls="true"
    MapPosition="right"
    OnDataLoaded="HandleData" />

@code {
    private void HandleData(ParsedData data)
    {
        // Your code here
        Console.WriteLine($"Loaded {data.ValidRows} features");
    }
}
```

## Supported Formats

- **GeoJSON** (`.geojson`, `.json`)
- **Shapefile** (`.zip` with .shp/.shx/.dbf)
- **GeoPackage** (`.gpkg`)
- **KML/KMZ** (`.kml`, `.kmz`)
- **CSV** (`.csv` with lat/lon columns)
- **GPX** (`.gpx`)

## CSV Requirements

For CSV files, name your columns:
- **Latitude**: `lat`, `latitude`, `y`, `LAT`
- **Longitude**: `lon`, `lng`, `longitude`, `x`, `LON`

The system will automatically create point geometries.

## Common Customizations

### Change Map Position

```razor
<UploadAndVisualize MapPosition="left" />  <!-- or "top", "bottom", "right" -->
```

### Custom Max File Size

```razor
<UploadAndVisualize MaxFileSizeMB="1000" />
```

### Hide Map, Show Upload Only

```razor
<DragDropUpload OnDataParsed="HandleData" />
```

### Handle Errors

```razor
<DragDropUpload OnError="@((error) => Console.WriteLine($"Error: {error}"))" />
```

## Integration with Existing Import

Use the API client extensions for server-side import:

```csharp
@inject ImportApiClient ImportApi

private async Task UploadToServer(IBrowserFile file)
{
    var result = await ImportApi.CreateInstantImportJobAsync(
        serviceId: "my-service",
        layerId: "my-layer",
        file: file,
        overwrite: false,
        progress: new Progress<double>(p => Console.WriteLine($"{p}%"))
    );

    if (result.Success)
    {
        // Preview data available immediately
        var preview = result.ParsedData;
        var style = result.Style;

        // Job running on server
        var job = result.Job;
    }
}
```

## Troubleshooting

**Map not showing?**
- Ensure MapLibre GL JS is included in your page
- Check browser console for errors

**File not uploading?**
- Check file size is under limit (default 500 MB)
- Verify file format is supported

**CSV not showing points?**
- Check column names contain `lat`/`lon`
- Verify coordinates are in valid range (-90 to 90, -180 to 180)

## Next Steps

- See [DRAG_DROP_UPLOAD_README.md](./DRAG_DROP_UPLOAD_README.md) for complete documentation
- Check [DragDropUploadExample.razor](./src/Honua.MapSDK/Components/Upload/DragDropUploadExample.razor) for live examples
- Explore auto-styling options in [AutoStylingService.cs](./src/Honua.MapSDK/Services/Import/AutoStylingService.cs)

## Need Help?

- Check the example page at `/upload-example` in your app
- Review error messages in browser console
- Ensure all required dependencies are installed
