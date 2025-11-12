# Honua.MapSDK Isochrone Component

The `HonuaIsochrone` component provides isochrone (travel time polygon) generation capabilities for your mapping applications. Users can visualize areas reachable within specific time intervals from a given origin point.

## Features

- **Multiple Provider Support**: Mapbox, OpenRouteService, GraphHopper, and custom endpoints
- **Travel Modes**: Driving, Walking, Cycling, and Transit (provider-dependent)
- **Flexible Time Intervals**: Configure multiple time intervals (e.g., 5, 10, 15, 30 minutes)
- **Interactive UI**: Click-to-select origin, drag-to-move, hover-to-highlight
- **Legend**: Color-coded visualization with area statistics
- **Export**: Export isochrones as GeoJSON
- **ComponentBus Integration**: Publish/subscribe to isochrone events
- **Caching**: Automatic result caching to reduce API calls

## Basic Usage

```razor
@page "/isochrone-demo"
@using Honua.MapSDK.Components
@using Honua.MapSDK.Models.Routing

<HonuaMap @ref="_map" Id="demo-map" Style="width: 100%; height: 600px;">
    <HonuaIsochrone
        MapId="demo-map"
        TravelMode="TravelMode.Driving"
        DefaultProvider="mapbox"
        DefaultIntervals="@(new List<int> { 5, 10, 15, 30 })"
        OnIsochroneGenerated="@OnIsochroneGenerated" />
</HonuaMap>

@code {
    private HonuaMap? _map;

    private void OnIsochroneGenerated(IsochroneResult result)
    {
        Console.WriteLine($"Generated isochrone with {result.Polygons.Count} polygons");
    }
}
```

## Configuration

### Provider Setup

To use the isochrone component, you need to configure at least one provider in your `Program.cs` or `Startup.cs`:

```csharp
using Honua.MapSDK.Services;
using Honua.MapSDK.Services.Routing;

// Register providers
builder.Services.AddHttpClient<MapboxIsochroneProvider>();
builder.Services.AddHttpClient<OpenRouteServiceIsochroneProvider>();
builder.Services.AddHttpClient<GraphHopperIsochroneProvider>();

// Register individual providers
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

builder.Services.AddSingleton<IIsochroneProvider>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var logger = sp.GetService<ILogger<OpenRouteServiceIsochroneProvider>>();
    return new OpenRouteServiceIsochroneProvider(
        httpClient,
        apiKey: builder.Configuration["OpenRouteService:ApiKey"],
        logger
    );
});

// Register the main service
builder.Services.AddSingleton<IsochroneService>();
```

### API Keys

Store your API keys securely in `appsettings.json` or user secrets:

```json
{
  "Mapbox": {
    "ApiKey": "your-mapbox-api-key"
  },
  "OpenRouteService": {
    "ApiKey": "your-openrouteservice-api-key"
  },
  "GraphHopper": {
    "ApiKey": "your-graphhopper-api-key"
  }
}
```

## Component Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Id` | `string` | Auto-generated | Unique identifier for the component |
| `MapId` | `string` | **Required** | ID of the map to sync with |
| `DefaultProvider` | `string?` | First available | Default routing provider key |
| `TravelMode` | `TravelMode` | `Driving` | Default travel mode |
| `DefaultIntervals` | `List<int>` | `[5, 10, 15, 30]` | Default time intervals in minutes |
| `ShowCloseButton` | `bool` | `false` | Show close button in header |
| `Elevation` | `int` | `2` | Paper elevation |
| `Width` | `string` | `"400px"` | Component width |
| `CssClass` | `string?` | `null` | Custom CSS class |

## Events

### OnIsochroneGenerated

Fired when an isochrone is successfully generated.

```csharp
private void OnIsochroneGenerated(IsochroneResult result)
{
    Console.WriteLine($"Center: {result.Center[0]}, {result.Center[1]}");
    Console.WriteLine($"Travel mode: {result.TravelMode}");
    Console.WriteLine($"Polygons: {result.Polygons.Count}");

    foreach (var polygon in result.Polygons)
    {
        Console.WriteLine($"  {polygon.Interval} min: {polygon.Area:N0} m²");
    }
}
```

### OnError

Fired when an error occurs during isochrone generation.

```csharp
private void OnError(string errorMessage)
{
    // Show error to user
    _snackbar.Add(errorMessage, Severity.Error);
}
```

### OnClose

Fired when the close button is clicked.

```csharp
private void OnClose()
{
    _showIsochrone = false;
}
```

## ComponentBus Messages

The isochrone component publishes and subscribes to various ComponentBus messages:

### Published Messages

- `IsochroneCalculatedMessage`: When isochrone is successfully calculated
- `IsochroneOriginSelectedMessage`: When origin point is selected
- `IsochroneVisibilityChangedMessage`: When visibility is toggled
- `IsochroneClearedMessage`: When isochrone is cleared
- `IsochronePolygonClickedMessage`: When a polygon is clicked
- `IsochroneExportedMessage`: When isochrone is exported

### Example: Listening to Isochrone Events

```csharp
@inject ComponentBus Bus

protected override void OnInitialized()
{
    Bus.Subscribe<IsochroneCalculatedMessage>(args =>
    {
        var msg = args.Message;
        _logger.LogInformation(
            "Isochrone calculated at {Center} with {Count} polygons",
            msg.Center,
            msg.PolygonCount
        );
    });
}
```

## Advanced Usage

### Custom Provider

You can implement a custom isochrone provider by implementing the `IIsochroneProvider` interface:

```csharp
public class CustomIsochroneProvider : IIsochroneProvider
{
    public string ProviderKey => "custom";
    public string DisplayName => "Custom Provider";
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
        // Implement your custom logic here
        // Call your API, process results, return IsochroneResult
    }

    public bool IsConfigured()
    {
        return !string.IsNullOrEmpty(_apiKey);
    }
}
```

### Programmatic Control

You can control the isochrone component programmatically:

```razor
<HonuaIsochrone @ref="_isochroneRef" MapId="my-map" />

@code {
    private HonuaIsochrone? _isochroneRef;

    private async Task GenerateIsochroneAtLocation(double lon, double lat)
    {
        // Set origin via ComponentBus
        await Bus.PublishAsync(new IsochroneOriginSelectedMessage
        {
            ComponentId = _isochroneRef?.Id ?? "",
            Longitude = lon,
            Latitude = lat
        });
    }
}
```

### Styling with CSS

Customize the appearance by targeting CSS classes:

```css
/* Custom legend colors */
.honua-isochrone .legend-item {
    border-left: 4px solid transparent;
}

.honua-isochrone .legend-item:hover {
    border-left-color: var(--mud-palette-primary);
}

/* Custom marker style */
.isochrone-origin-marker {
    width: 40px !important;
    height: 40px !important;
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}
```

## Travel Modes

Different providers support different travel modes:

| Provider | Driving | Walking | Cycling | Transit |
|----------|---------|---------|---------|---------|
| Mapbox | ✓ | ✓ | ✓ | ✗ |
| OpenRouteService | ✓ | ✓ | ✓ | ✗ |
| GraphHopper | ✓ | ✓ | ✓ | ✗ |

## Performance Tips

1. **Caching**: Results are automatically cached based on center, intervals, and travel mode
2. **Interval Limits**: Each provider has maximum interval limits (check `MaxIntervals`)
3. **Rate Limiting**: Be aware of API rate limits for commercial providers
4. **Batch Requests**: If generating multiple isochrones, space them out to avoid hitting rate limits

## Examples

### Service Area Analysis

```razor
<HonuaIsochrone
    MapId="service-map"
    TravelMode="TravelMode.Driving"
    DefaultIntervals="@(new List<int> { 15, 30, 45 })"
    OnIsochroneGenerated="@AnalyzeServiceArea" />

@code {
    private void AnalyzeServiceArea(IsochroneResult result)
    {
        var totalArea = result.Polygons.Sum(p => p.Area);
        var coverage30Min = result.Polygons
            .FirstOrDefault(p => p.Interval == 30)?.Area ?? 0;

        Console.WriteLine($"Total coverage: {totalArea / 1_000_000:F2} km²");
        Console.WriteLine($"30-min coverage: {coverage30Min / 1_000_000:F2} km²");
    }
}
```

### Emergency Response Planning

```razor
<HonuaIsochrone
    MapId="emergency-map"
    TravelMode="TravelMode.DrivingTraffic"
    DefaultIntervals="@(new List<int> { 5, 10, 15 })"
    DefaultProvider="mapbox" />
```

### Walking Accessibility

```razor
<HonuaIsochrone
    MapId="walkability-map"
    TravelMode="TravelMode.Walking"
    DefaultIntervals="@(new List<int> { 5, 10, 20 })" />
```

## Troubleshooting

### No providers available

**Error**: "No isochrone providers configured"

**Solution**: Ensure you've registered at least one provider in your DI container and provided valid API keys.

### API key errors

**Error**: "API key is not configured"

**Solution**: Check that your API key is correctly configured in `appsettings.json` and being passed to the provider.

### Isochrone not displaying

**Issue**: Isochrone generated but not visible on map

**Solution**:
1. Check that `MapId` matches the map component's ID
2. Verify the map is fully initialized before generating isochrones
3. Check browser console for JavaScript errors

## Browser Support

- Chrome/Edge: ✓ Full support
- Firefox: ✓ Full support
- Safari: ✓ Full support
- Mobile browsers: ✓ Full support with touch gestures

## License

This component is part of Honua.MapSDK, licensed under the Elastic License 2.0.

## See Also

- [HonuaRouting Component](./RoutingExample.md)
- [HonuaMap Component](./MapExample.md)
- [ComponentBus Documentation](./ComponentBus.md)
