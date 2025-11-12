# HonuaIsochrone Component

A comprehensive isochrone (travel time polygon) generation component for Honua.MapSDK.

## Overview

The HonuaIsochrone component allows users to generate and visualize isochrones - polygons representing areas reachable within specific time intervals from an origin point. This is useful for:

- Service area analysis
- Emergency response planning
- Real estate market analysis
- Public transit accessibility studies
- Delivery zone optimization
- Urban planning and walkability studies

## Architecture

The isochrone implementation consists of several layers:

### 1. Service Layer (`Services/`)
- **IsochroneService.cs**: Main service managing providers and caching
- **IIsochroneProvider.cs**: Interface for provider implementations
- **MapboxIsochroneProvider.cs**: Mapbox Isochrone API integration
- **OpenRouteServiceIsochroneProvider.cs**: OpenRouteService API integration
- **GraphHopperIsochroneProvider.cs**: GraphHopper API integration

### 2. Models (`Models/Routing/`)
- **IsochroneOptions.cs**: Configuration for isochrone generation
- **IsochroneResult.cs**: Result structure with polygons
- **IsochronePolygon.cs**: Individual polygon data
- **IsochroneProvider.cs**: Provider configuration models

### 3. UI Component (`Components/`)
- **HonuaIsochrone.razor**: Main Blazor component with UI

### 4. JavaScript (`wwwroot/js/`)
- **honua-isochrone.js**: MapLibre visualization and interaction

### 5. Styles (`wwwroot/css/`)
- **honua-isochrone.css**: Component styling

### 6. ComponentBus Messages (`Core/Messages/`)
- **IsochroneCalculatedMessage**: Isochrone generated
- **IsochroneOriginSelectedMessage**: Origin point selected
- **IsochroneVisibilityChangedMessage**: Visibility toggled
- **IsochroneClearedMessage**: Isochrone cleared
- **IsochronePolygonClickedMessage**: Polygon clicked
- **IsochroneExportedMessage**: Data exported

## Quick Start

### 1. Register Services

In `Program.cs`:

```csharp
using Honua.MapSDK.Services;
using Honua.MapSDK.Services.Routing;

// Register HTTP clients
builder.Services.AddHttpClient<MapboxIsochroneProvider>();
builder.Services.AddHttpClient<OpenRouteServiceIsochroneProvider>();
builder.Services.AddHttpClient<GraphHopperIsochroneProvider>();

// Register providers (choose at least one)
builder.Services.AddSingleton<IIsochroneProvider>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var logger = sp.GetService<ILogger<MapboxIsochroneProvider>>();
    return new MapboxIsochroneProvider(
        httpClient,
        apiKey: builder.Configuration["Mapbox:ApiKey"],
        logger
    );
});

// Register main service
builder.Services.AddSingleton<IsochroneService>();
```

### 2. Configure API Keys

In `appsettings.json`:

```json
{
  "Mapbox": {
    "ApiKey": "pk.your-mapbox-key"
  },
  "OpenRouteService": {
    "ApiKey": "your-ors-key"
  },
  "GraphHopper": {
    "ApiKey": "your-graphhopper-key"
  }
}
```

### 3. Add Component to Page

```razor
@page "/demo"
@using Honua.MapSDK.Components

<HonuaMap Id="my-map" Style="width: 100%; height: 600px;">
    <HonuaIsochrone MapId="my-map" />
</HonuaMap>
```

### 4. Include CSS (Optional)

In your layout or page:

```html
<link href="_content/Honua.MapSDK/css/honua-isochrone.css" rel="stylesheet" />
```

## Features

### Multiple Providers
- **Mapbox**: High-quality isochrones with traffic data
- **OpenRouteService**: Free tier available, good coverage
- **GraphHopper**: Fast computation, good for cycling routes
- **Custom**: Implement your own provider

### Travel Modes
- Driving (with optional traffic consideration)
- Walking
- Cycling
- Transit (provider-dependent)

### Time Intervals
- Configure multiple intervals (e.g., 5, 10, 15, 30 minutes)
- Visualized with graduated colors
- Area statistics for each interval

### Interactive Features
- Click map to set origin
- Drag origin marker to update
- Hover polygons to highlight
- Toggle visibility
- Export as GeoJSON

### Caching
- Automatic result caching
- Reduces redundant API calls
- Configurable cache size

### ComponentBus Integration
- Publish events for all actions
- Subscribe to coordinate with other components
- Loosely coupled architecture

## Provider Comparison

| Feature | Mapbox | OpenRouteService | GraphHopper |
|---------|--------|------------------|-------------|
| **API Key Required** | Yes | Yes | Yes |
| **Free Tier** | 100,000 req/month | 2,000 req/day | 500 req/day |
| **Max Intervals** | 4 | 10 | 5 |
| **Driving** | ✓ | ✓ | ✓ |
| **Walking** | ✓ | ✓ | ✓ |
| **Cycling** | ✓ | ✓ | ✓ |
| **Traffic Data** | ✓ | ✗ | ✗ |
| **Smoothing** | ✓ | ✓ | Limited |
| **Area Statistics** | ✗ | ✓ | ✗ |

## Advanced Usage

### Custom Provider Implementation

```csharp
public class MyCustomProvider : IIsochroneProvider
{
    public string ProviderKey => "mycustom";
    public string DisplayName => "My Custom Provider";
    public bool RequiresApiKey => true;
    public int MaxIntervals => 5;

    public List<TravelMode> SupportedTravelModes => new()
    {
        TravelMode.Driving,
        TravelMode.Walking
    };

    public async Task<IsochroneResult> CalculateAsync(
        IsochroneOptions options,
        CancellationToken cancellationToken = default)
    {
        // Your implementation here
        // Call your API, transform results, return IsochroneResult
    }

    public bool IsConfigured() => !string.IsNullOrEmpty(_apiKey);
}
```

### Programmatic Generation

```csharp
@inject IsochroneService IsochroneService

private async Task GenerateIsochroneAtPoint(double lon, double lat)
{
    var options = new IsochroneOptions
    {
        Center = new[] { lon, lat },
        Intervals = new List<int> { 5, 10, 15 },
        TravelMode = TravelMode.Driving,
        Smoothing = 0.5
    };

    var result = await IsochroneService.CalculateAsync(
        options,
        providerKey: "mapbox"
    );

    // Process result
    Console.WriteLine($"Generated {result.Polygons.Count} polygons");
}
```

### Event Handling

```csharp
@inject ComponentBus Bus

protected override void OnInitialized()
{
    Bus.Subscribe<IsochroneCalculatedMessage>(args =>
    {
        var msg = args.Message;

        // React to isochrone generation
        _logger.LogInformation(
            "Isochrone calculated: {PolygonCount} polygons",
            msg.PolygonCount
        );

        // Coordinate with other components
        // e.g., update statistics, filter data, etc.
    });
}
```

## API Reference

### Component Parameters

```csharp
[Parameter] public string Id { get; set; }
[Parameter] public required string MapId { get; set; }
[Parameter] public string? DefaultProvider { get; set; }
[Parameter] public TravelMode TravelMode { get; set; }
[Parameter] public List<int> DefaultIntervals { get; set; }
[Parameter] public bool ShowCloseButton { get; set; }
[Parameter] public int Elevation { get; set; }
[Parameter] public string Width { get; set; }
[Parameter] public string? CssClass { get; set; }
[Parameter] public EventCallback<IsochroneResult> OnIsochroneGenerated { get; set; }
[Parameter] public EventCallback<string> OnError { get; set; }
[Parameter] public EventCallback OnClose { get; set; }
```

### Service Methods

```csharp
// Get available providers
IEnumerable<IIsochroneProvider> GetAvailableProviders();

// Get specific provider
IIsochroneProvider? GetProvider(string? providerKey = null);

// Calculate isochrone
Task<IsochroneResult> CalculateAsync(
    IsochroneOptions options,
    string? providerKey = null,
    CancellationToken cancellationToken = default);

// Export as GeoJSON
string ExportAsGeoJson(IsochroneResult result);

// Clear cache
Task ClearCacheAsync();
```

### JavaScript Functions

```javascript
// Initialize isochrone
initializeIsochrone(mapId, options);

// Set origin point
setOriginPoint(mapId, longitude, latitude);

// Display isochrone
displayIsochrone(mapId, isochroneResult);

// Toggle visibility
toggleIsochroneVisibility(mapId, visible);

// Clear isochrone
clearIsochrone(mapId);

// Export as GeoJSON
exportIsochroneAsGeoJson(mapId);

// Highlight interval
highlightIsochroneInterval(mapId, interval);

// Clear highlight
clearHighlight(mapId);
```

## Performance Considerations

1. **API Rate Limits**: Be aware of provider rate limits
2. **Caching**: Results are automatically cached to reduce API calls
3. **Interval Count**: More intervals = more API calls (some providers)
4. **Smoothing**: Higher smoothing may impact calculation time
5. **Polygon Complexity**: Large coverage areas generate complex polygons

## Testing

See `Examples/IsochroneDemo.razor` for a complete working example with:
- Multiple sample locations
- Preset scenarios (emergency, service area, etc.)
- Statistics display
- Activity logging
- Export functionality

## Troubleshooting

### Issue: "No isochrone providers configured"
**Solution**: Register at least one provider in DI container with valid API key

### Issue: Isochrone not displaying on map
**Solution**:
1. Verify MapId matches map component ID
2. Check map is fully initialized
3. Check browser console for errors

### Issue: API key errors
**Solution**: Verify API key in configuration and provider registration

### Issue: Performance issues
**Solution**:
1. Reduce number of intervals
2. Use caching effectively
3. Implement request throttling
4. Consider using a faster provider

## Browser Compatibility

- ✓ Chrome/Edge 90+
- ✓ Firefox 88+
- ✓ Safari 14+
- ✓ Mobile browsers (iOS Safari, Chrome Mobile)

## Future Enhancements

Potential future features:
- Distance-based isochrones (not just time)
- Multiple origin points (multi-source isochrones)
- Isochrone comparison mode
- Custom color schemes
- Animation/transitions
- Real-time traffic integration
- Batch generation
- Server-side rendering

## Resources

- [Mapbox Isochrone API](https://docs.mapbox.com/api/navigation/isochrone/)
- [OpenRouteService Isochrone](https://openrouteservice.org/dev/#/api-docs/v2/isochrones/{profile}/post)
- [GraphHopper Isochrone](https://docs.graphhopper.com/#tag/Isochrone-API)
- [Example Documentation](../Examples/IsochroneExample.md)
- [ComponentBus Documentation](../Core/README-ComponentBus.md)

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0
