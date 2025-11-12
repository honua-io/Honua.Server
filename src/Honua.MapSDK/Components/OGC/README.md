# OGC WMS/WFS Components

Enhanced WMS (Web Map Service) and WFS (Web Feature Service) support for Honua.MapSDK with comprehensive OGC standards compliance.

## Components

### HonuaWmsLayer

Display WMS layers from OGC-compliant map services with advanced features including GetCapabilities parsing, layer selection, legend graphics, GetFeatureInfo, and time dimension support.

**Key Features:**
- Automatic GetCapabilities parsing
- Multiple layer support with dynamic selection
- Legend graphic retrieval and display
- GetFeatureInfo on click (interactive queries)
- Custom SRS/CRS support (EPSG:4326, EPSG:3857, etc.)
- Time dimension support for temporal data
- Opacity and visibility controls
- WMS 1.1.0, 1.1.1, and 1.3.0 support

### HonuaWfsLayer

Display vector features from WFS services with powerful querying and filtering capabilities.

**Key Features:**
- GetCapabilities parsing
- Feature type selection
- CQL and OGC filter support
- Pagination for large datasets
- GeoJSON output format
- Property name selection (attribute filtering)
- Bounding box queries
- Custom styling for points, lines, and polygons
- WFS 1.0.0, 1.1.0, and 2.0.0 support

### HonuaOgcServiceBrowser

Interactive UI component for browsing and connecting to OGC services.

**Key Features:**
- Automatic service type detection (WMS/WFS)
- Browse available layers/feature types
- Preview layer metadata
- Search and filter layers
- Add selected layers to map
- Service information display

## Installation

The OGC components are included in Honua.MapSDK. Ensure you have the SDK referenced in your project:

```xml
<PackageReference Include="Honua.MapSDK" Version="1.0.0" />
```

## Basic Usage

### WMS Layer Example

```razor
@page "/wms-demo"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.OGC

<HonuaMapLibre @ref="_map"
               SyncWith="demo-map"
               Style="height: 600px;">

    <HonuaWmsLayer SyncWith="demo-map"
                   ServiceUrl="https://ows.terrestris.de/osm/service"
                   Layers='new List<string> { "OSM-WMS" }'
                   Version="1.3.0"
                   Srs="EPSG:3857"
                   Transparent="true"
                   ShowControls="true"
                   EnableFeatureInfo="true" />
</HonuaMapLibre>

@code {
    private HonuaMapLibre? _map;
}
```

### WFS Layer Example

```razor
@page "/wfs-demo"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.OGC

<HonuaMapLibre @ref="_map"
               SyncWith="demo-map"
               Style="height: 600px;">

    <HonuaWfsLayer SyncWith="demo-map"
                   ServiceUrl="https://demo.geo-solutions.it/geoserver/wfs"
                   FeatureType="topp:states"
                   Version="2.0.0"
                   MaxFeatures="50"
                   ShowFilterControls="true"
                   EnablePagination="true"
                   OnFeaturesLoaded="@HandleFeaturesLoaded" />
</HonuaMapLibre>

@code {
    private HonuaMapLibre? _map;

    private void HandleFeaturesLoaded(WfsFeatureCollection features)
    {
        Console.WriteLine($"Loaded {features.NumberReturned} features");
    }
}
```

### OGC Service Browser Example

```razor
@page "/ogc-browser"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.OGC

<div class="d-flex" style="height: 100vh;">
    <div style="width: 400px; overflow-y: auto;">
        <HonuaOgcServiceBrowser TargetMapId="demo-map"
                               DefaultServiceUrl="https://ows.terrestris.de/osm/service"
                               OnServiceConnected="@HandleServiceConnected"
                               OnLayerAdded="@HandleLayerAdded" />
    </div>

    <div style="flex: 1;">
        <HonuaMapLibre @ref="_map"
                       SyncWith="demo-map"
                       Style="height: 100%;" />
    </div>
</div>

@code {
    private HonuaMapLibre? _map;

    private void HandleServiceConnected(OgcServiceInfo info)
    {
        Console.WriteLine($"Connected to {info.ServiceType}: {info.Title}");
    }

    private void HandleLayerAdded(string layerId)
    {
        Console.WriteLine($"Layer added: {layerId}");
    }
}
```

## Advanced Usage

### WMS with Time Dimension

```razor
<HonuaWmsLayer SyncWith="demo-map"
               ServiceUrl="https://example.com/wms"
               Layers='new List<string> { "temperature" }'
               SupportsTime="true"
               TimeDimension="2024-01-01T00:00:00Z"
               ShowControls="true" />
```

### WFS with CQL Filtering

```razor
<HonuaWfsLayer SyncWith="demo-map"
               ServiceUrl="https://demo.geo-solutions.it/geoserver/wfs"
               FeatureType="topp:states"
               CqlFilter="PERSONS > 2000000"
               MaxFeatures="100"
               PropertyNames='new List<string> { "STATE_NAME", "PERSONS", "AREA" }' />
```

### Custom WFS Styling

```razor
@code {
    private object _customStyle = new
    {
        point = new
        {
            radius = 8,
            color = "#ff0000",
            strokeWidth = 2,
            strokeColor = "#ffffff",
            opacity = 0.9
        },
        line = new
        {
            color = "#00ff00",
            width = 3,
            opacity = 0.8
        },
        polygon = new
        {
            fillColor = "#0000ff",
            fillOpacity = 0.4,
            strokeColor = "#000080",
            strokeWidth = 2
        }
    };
}

<HonuaWfsLayer SyncWith="demo-map"
               ServiceUrl="https://example.com/wfs"
               FeatureType="my:layer"
               StyleConfig="@_customStyle" />
```

### Programmatic Control

```razor
<HonuaWmsLayer @ref="_wmsLayer"
               SyncWith="demo-map"
               ServiceUrl="https://example.com/wms"
               Layers='new List<string> { "layer1" }' />

<button @onclick="RefreshLayer">Refresh</button>
<button @onclick="ToggleVisibility">Toggle Visibility</button>

@code {
    private HonuaWmsLayer? _wmsLayer;
    private bool _isVisible = true;

    private async Task RefreshLayer()
    {
        if (_wmsLayer != null)
        {
            await _wmsLayer.RefreshAsync();
        }
    }

    private async Task ToggleVisibility()
    {
        if (_wmsLayer != null)
        {
            _isVisible = !_isVisible;
            await _wmsLayer.SetVisibilityAsync(_isVisible);
        }
    }
}
```

## Integration with Other Components

### WMS Legend in HonuaLegend

The WMS component automatically publishes layer events to the ComponentBus, allowing HonuaLegend to display legend graphics:

```razor
<HonuaMapLibre SyncWith="demo-map">
    <HonuaWmsLayer SyncWith="demo-map"
                   ServiceUrl="https://example.com/wms"
                   Layers='new List<string> { "layer1" }'
                   ShowLegend="true" />

    <HonuaLegend SyncWith="demo-map"
                 Position="top-right"
                 ShowSymbols="true" />
</HonuaMapLibre>
```

### GetFeatureInfo in HonuaPopup

WMS GetFeatureInfo results are automatically displayed in HonuaPopup:

```razor
<HonuaMapLibre SyncWith="demo-map">
    <HonuaWmsLayer SyncWith="demo-map"
                   ServiceUrl="https://example.com/wms"
                   Layers='new List<string> { "layer1" }'
                   EnableFeatureInfo="true"
                   FeatureInfoFormat="application/json" />

    <HonuaPopup SyncWith="demo-map"
                TriggerMode="PopupTrigger.Click"
                ShowCloseButton="true" />
</HonuaMapLibre>
```

## API Reference

### HonuaWmsLayer Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Id` | string | auto-generated | Unique identifier |
| `SyncWith` | string | required | Map ID to sync with |
| `ServiceUrl` | string | required | WMS service base URL |
| `Version` | string | "1.3.0" | WMS version (1.1.1, 1.3.0) |
| `Layers` | List<string> | required | Layer names to display |
| `Srs` | string | "EPSG:3857" | Coordinate reference system |
| `Format` | string | "image/png" | Image format |
| `Transparent` | bool | true | Enable transparency |
| `Opacity` | double | 1.0 | Layer opacity (0-1) |
| `ShowControls` | bool | true | Show interactive controls |
| `ShowLayerSelector` | bool | true | Show layer selector |
| `ShowOpacityControl` | bool | true | Show opacity control |
| `ShowLegend` | bool | true | Show legend graphic |
| `EnableFeatureInfo` | bool | true | Enable GetFeatureInfo on click |
| `FeatureInfoFormat` | string | "application/json" | GetFeatureInfo format |
| `SupportsTime` | bool | false | Support time dimension |
| `TimeDimension` | string? | null | Time dimension parameter |
| `MinZoom` | int? | null | Minimum zoom level |
| `MaxZoom` | int? | null | Maximum zoom level |

### HonuaWfsLayer Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Id` | string | auto-generated | Unique identifier |
| `SyncWith` | string | required | Map ID to sync with |
| `ServiceUrl` | string | required | WFS service base URL |
| `Version` | string | "2.0.0" | WFS version (1.0.0, 1.1.0, 2.0.0) |
| `FeatureType` | string | required | Feature type name |
| `Srs` | string? | null | Coordinate reference system |
| `OutputFormat` | string | "application/json" | Output format |
| `CqlFilter` | string? | null | CQL filter expression |
| `MaxFeatures` | int? | 1000 | Maximum features to retrieve |
| `PropertyNames` | List<string>? | null | Property names to retrieve |
| `ShowControls` | bool | true | Show interactive controls |
| `ShowFeatureTypeSelector` | bool | true | Show feature type selector |
| `ShowFilterControls` | bool | true | Show filter controls |
| `ShowStatistics` | bool | true | Show statistics |
| `EnablePagination` | bool | true | Enable pagination |
| `AutoLoad` | bool | true | Auto-load data on init |
| `StyleConfig` | object? | null | Custom style configuration |

### Public Methods

**HonuaWmsLayer:**
- `RefreshAsync()` - Refresh the layer
- `SetVisibilityAsync(bool visible)` - Set visibility
- `GetCapabilities()` - Get WMS capabilities

**HonuaWfsLayer:**
- `RefreshAsync()` - Refresh features
- `SetVisibilityAsync(bool visible)` - Set visibility
- `GetCapabilities()` - Get WFS capabilities
- `GetFeatures()` - Get current features

**HonuaOgcServiceBrowser:**
- `GetServiceInfo()` - Get current service info
- `GetWmsCapabilities()` - Get WMS capabilities
- `GetWfsCapabilities()` - Get WFS capabilities

## CRS/SRS Support

The components support various coordinate reference systems:

- **EPSG:4326** - WGS 84 (lat/lon)
- **EPSG:3857** - Web Mercator (default for web maps)
- **EPSG:32633** - UTM Zone 33N
- **Custom CRS** - Any EPSG code supported by the service

Example:
```razor
<HonuaWmsLayer ServiceUrl="..."
               Layers='...'
               Srs="EPSG:4326" />
```

## Common WMS/WFS Services

### Public WMS Services
- **Terrestris OSM WMS**: https://ows.terrestris.de/osm/service
- **NASA GIBS**: https://gibs.earthdata.nasa.gov/wms/epsg4326/best/wms.cgi
- **NOAA Weather**: https://nowcoast.noaa.gov/arcgis/services/nowcoast/radar_meteo_imagery_nexrad_time/MapServer/WMSServer

### Public WFS Services
- **GeoServer Demo**: https://demo.geo-solutions.it/geoserver/wfs
- **OpenStreetMap WFS**: Various regional providers

## Troubleshooting

### CORS Issues
If you encounter CORS errors when accessing OGC services, you may need to:
1. Use a CORS proxy
2. Configure your server to add appropriate CORS headers
3. Use a server-side proxy endpoint

### GetCapabilities Fails
- Verify the service URL is correct
- Check that the service supports the requested version
- Ensure the service is accessible from your network

### Features Not Displaying
- Check CRS/SRS compatibility
- Verify bounding box overlaps with map viewport
- Check browser console for JavaScript errors
- Ensure GeoJSON output format is supported by the WFS

## Performance Tips

1. **Limit Feature Count**: Use `MaxFeatures` parameter to limit the number of features loaded
2. **Use Pagination**: Enable pagination for large datasets
3. **Filter Strategically**: Use CQL filters to retrieve only needed data
4. **Property Selection**: Use `PropertyNames` to retrieve only required attributes
5. **Bounding Box Queries**: Load features only within the visible extent
6. **Cache Tiles**: WMS tiles are automatically cached by MapLibre

## Security Considerations

1. **Authentication**: Services may require authentication (basic auth, API keys)
2. **HTTPS**: Always use HTTPS endpoints in production
3. **Rate Limiting**: Be aware of service rate limits
4. **Data Validation**: Validate all user inputs before querying services

## Browser Support

The OGC components work in all modern browsers:
- Chrome/Edge (latest)
- Firefox (latest)
- Safari (latest)

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0
