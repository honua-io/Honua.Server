# HonuaRouting Component

Turn-by-turn routing and directions component for Honua.MapSDK with support for multiple routing engines, alternative routes, and isochrone analysis.

## Features

- **Multiple Waypoints**: Support for A → B → C → D routing with unlimited waypoints
- **Turn-by-Turn Directions**: Detailed step-by-step instructions with maneuver icons
- **Alternative Routes**: Show up to 3 alternative route options
- **Multiple Travel Modes**: Driving, Walking, Cycling, Transit
- **Route Preferences**: Fastest, Shortest, Recommended, Most Efficient
- **Avoid Options**: Tolls, Highways, Ferries, etc.
- **Isochrone Analysis**: Service area/drive-time polygons (5, 10, 15, 30 min)
- **Multiple Routing Engines**: OSRM, Mapbox, GraphHopper, OpenRouteService, Custom
- **Route Export**: GPX, KML, GeoJSON formats
- **ComponentBus Integration**: Loosely coupled with other map components
- **Dark Mode**: Full dark mode support

## Quick Start

### Basic Usage

```razor
@page "/routing-demo"
@using Honua.MapSDK.Components
@using Honua.MapSDK.Components.Routing

<HonuaMap Id="main-map" />

<HonuaRouting SyncWith="main-map" />
```

### With Custom Configuration

```razor
<HonuaRouting
    SyncWith="main-map"
    RoutingEngine="RoutingEngine.OSRM"
    TravelMode="TravelMode.Driving"
    ShowInstructions="true"
    ShowAlternatives="true"
    MaxAlternatives="3"
    OnRouteCalculated="@HandleRouteCalculated"
    OnRoutingError="@HandleRoutingError" />

@code {
    private void HandleRouteCalculated(Route route)
    {
        Console.WriteLine($"Route: {route.Summary.FormattedDistance}, {route.Summary.FormattedDuration}");
    }

    private void HandleRoutingError(string error)
    {
        Console.WriteLine($"Error: {error}");
    }
}
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Id` | string | Auto-generated | Unique identifier for the component |
| `SyncWith` | string? | null | Map ID to synchronize with |
| `RoutingEngine` | RoutingEngine | OSRM | Routing engine to use |
| `ApiKey` | string? | null | API key for commercial services |
| `CustomRoutingService` | IRoutingService? | null | Custom routing service implementation |
| `TravelMode` | TravelMode | Driving | Default travel mode |
| `ShowInstructions` | bool | true | Show turn-by-turn instructions |
| `ShowAlternatives` | bool | true | Show alternative routes |
| `MaxAlternatives` | int | 3 | Maximum number of alternatives |
| `AllowMultipleWaypoints` | bool | true | Allow adding waypoints |
| `MaxWaypoints` | int | 10 | Maximum number of waypoints |
| `AllowWaypointReorder` | bool | true | Allow dragging waypoints to reorder |
| `ShowAvoidOptions` | bool | true | Show avoid options (tolls, etc.) |
| `ShowIsochroneOptions` | bool | true | Show isochrone/service area |
| `ShowExportOptions` | bool | true | Show export buttons |
| `ShowCloseButton` | bool | false | Show close button |
| `Elevation` | int | 2 | MudBlazor paper elevation |
| `Width` | string | 400px | Component width |
| `CssClass` | string? | null | Custom CSS class |
| `OnRouteCalculated` | EventCallback&lt;Route&gt; | - | Route calculation success callback |
| `OnRoutingError` | EventCallback&lt;string&gt; | - | Routing error callback |
| `OnWaypointAdded` | EventCallback&lt;Waypoint&gt; | - | Waypoint added callback |
| `OnClose` | EventCallback | - | Close button clicked callback |

## Routing Engines

### OSRM (Default - Free)

**Open Source Routing Machine** - Free, no API key required. Can use public demo server or self-host.

```razor
<HonuaRouting
    SyncWith="main-map"
    RoutingEngine="RoutingEngine.OSRM" />
```

**Pros:**
- Completely free
- No API key required
- Fast and accurate
- Can self-host for production
- Good global coverage

**Cons:**
- Demo server has rate limits
- No traffic data
- No isochrones (use GraphHopper instead)

**Demo Server:** `https://router.project-osrm.org`

### Mapbox Directions API (Requires API Key)

Premium routing with traffic data and high-quality routes.

```razor
<HonuaRouting
    SyncWith="main-map"
    RoutingEngine="RoutingEngine.Mapbox"
    ApiKey="pk.your_mapbox_token_here" />
```

**Pros:**
- Excellent route quality
- Real-time traffic
- Global coverage
- Isochrones supported
- Well-documented

**Cons:**
- Requires API key
- Paid service (free tier available)

**Pricing:** 100,000 free requests/month, then $0.40 per 1,000 requests

**Get API Key:** https://account.mapbox.com/

### GraphHopper (Free Tier Available)

Open-source routing engine with free tier.

```razor
<HonuaRouting
    SyncWith="main-map"
    RoutingEngine="RoutingEngine.GraphHopper"
    ApiKey="your_graphhopper_api_key" />
```

**Pros:**
- Isochrones supported
- Good route quality
- Free tier available
- Can self-host

**Cons:**
- Requires API key
- Free tier limits

**Pricing:** 500 free requests/day

**Get API Key:** https://www.graphhopper.com/

### OpenRouteService (Free API Key)

Free routing service from Heidelberg University.

```razor
<HonuaRouting
    SyncWith="main-map"
    RoutingEngine="RoutingEngine.OpenRouteService"
    ApiKey="your_ors_api_key" />
```

**Pros:**
- Free API key
- Isochrones supported
- Good European coverage
- Multiple travel modes

**Cons:**
- Lower rate limits
- Primarily European data

**Pricing:** Free with API key

**Get API Key:** https://openrouteservice.org/dev/#/signup

### Custom Routing Service

Implement your own routing service:

```csharp
public class MyRoutingService : IRoutingService
{
    public string ProviderName => "MyCustomRouter";
    public bool RequiresApiKey => false;
    public List<TravelMode> SupportedTravelModes => new() { TravelMode.Driving };

    public async Task<Route> CalculateRouteAsync(
        List<Waypoint> waypoints,
        RouteOptions options,
        CancellationToken cancellationToken = default)
    {
        // Your implementation
    }

    // Implement other interface methods...
}
```

```razor
<HonuaRouting
    SyncWith="main-map"
    RoutingEngine="RoutingEngine.Custom"
    CustomRoutingService="@_myRoutingService" />

@code {
    private readonly IRoutingService _myRoutingService = new MyRoutingService();
}
```

## Routing Engine Comparison

| Feature | OSRM | Mapbox | GraphHopper | OpenRouteService |
|---------|------|--------|-------------|------------------|
| **Price** | Free | $0.40/1k* | 500/day free | Free |
| **API Key** | No | Yes | Yes | Yes |
| **Isochrones** | No | Yes | Yes | Yes |
| **Traffic** | No | Yes | No | No |
| **Self-Hosting** | Yes | No | Yes | Yes |
| **Global Coverage** | Good | Excellent | Good | Good (EU) |
| **Rate Limit** | Medium | 100k/mo* | 500/day | Low |
| **Best For** | Development, Testing | Production, Enterprise | Isochrones, Development | European Routes, Research |

*Mapbox free tier: 100,000 requests/month

## Travel Modes

```razor
<HonuaRouting TravelMode="TravelMode.Driving" />
<HonuaRouting TravelMode="TravelMode.Walking" />
<HonuaRouting TravelMode="TravelMode.Cycling" />
<HonuaRouting TravelMode="TravelMode.Transit" />
```

**Support by Engine:**

| Mode | OSRM | Mapbox | GraphHopper | ORS |
|------|------|--------|-------------|-----|
| Driving | ✅ | ✅ | ✅ | ✅ |
| Walking | ✅ | ✅ | ✅ | ✅ |
| Cycling | ✅ | ✅ | ✅ | ✅ |
| Transit | ❌ | ✅ | ❌ | ✅ |

## Route Preferences

- **Fastest**: Minimize travel time (default for driving)
- **Shortest**: Minimize distance
- **Recommended**: Balance of time and experience
- **Most Efficient**: Best fuel economy (driving only)

## Avoid Options

```razor
<HonuaRouting ShowAvoidOptions="true" />
```

Available avoid options:
- Tolls
- Highways/Motorways
- Ferries
- Unpaved roads
- Tunnels
- Bridges

## ComponentBus Messages

### Published Messages

**RouteCalculatedMessage**
```csharp
Bus.Subscribe<RouteCalculatedMessage>(args => {
    var route = args.Message;
    Console.WriteLine($"Route: {route.FormattedDistance}");
});
```

**RoutingErrorMessage**
```csharp
Bus.Subscribe<RoutingErrorMessage>(args => {
    Console.WriteLine($"Error: {args.Message.ErrorMessage}");
});
```

**WaypointAddedMessage**
```csharp
Bus.Subscribe<WaypointAddedMessage>(args => {
    Console.WriteLine($"Added waypoint: {args.Message.Name}");
});
```

**RouteInstructionSelectedMessage**
```csharp
Bus.Subscribe<RouteInstructionSelectedMessage>(args => {
    Console.WriteLine($"Instruction: {args.Message.Text}");
});
```

**IsochroneCalculatedMessage**
```csharp
Bus.Subscribe<IsochroneCalculatedMessage>(args => {
    Console.WriteLine($"Isochrone with {args.Message.PolygonCount} polygons");
});
```

## Isochrone Analysis

Show areas reachable within time limits:

```razor
<HonuaRouting
    ShowIsochroneOptions="true"
    SyncWith="main-map" />
```

**Use Cases:**
- Service area analysis (e.g., 15-minute delivery zones)
- Emergency response coverage
- Real estate accessibility
- Public transit access
- Site selection

**Supported Engines:** Mapbox, GraphHopper, OpenRouteService

## Export Formats

### GPX (GPS Exchange Format)
```razor
@code {
    private async Task ExportGpx()
    {
        // Automatically exported via UI button
        // Creates downloadable GPX file with route and waypoints
    }
}
```

Use GPX for:
- GPS devices (Garmin, etc.)
- Hiking/cycling apps
- Route sharing

### KML (Keyhole Markup Language)
For Google Earth and other GIS applications.

### GeoJSON
For web mapping and GIS analysis.

## Programmatic Usage

```csharp
@inject ComponentBus Bus

@code {
    private async Task CreateRoute()
    {
        var waypoints = new List<Waypoint>
        {
            new() { Longitude = -122.4194, Latitude = 37.7749, Name = "San Francisco" },
            new() { Longitude = -122.2712, Latitude = 37.8044, Name = "Oakland" }
        };

        var options = new RouteOptions
        {
            TravelMode = TravelMode.Driving,
            Preference = RoutePreference.Fastest,
            IncludeInstructions = true,
            MaxAlternatives = 2
        };

        var service = new OsrmRoutingService(new HttpClient());
        var route = await service.CalculateRouteAsync(waypoints, options);

        Console.WriteLine($"Distance: {route.Distance}m");
        Console.WriteLine($"Duration: {route.Duration}s");
        Console.WriteLine($"Steps: {route.Instructions.Count}");
    }
}
```

## Styling

### Custom CSS

```razor
<HonuaRouting CssClass="my-routing-panel" />

<style>
.my-routing-panel {
    border: 2px solid #1976D2;
    border-radius: 12px;
}

.my-routing-panel ::deep .route-summary {
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    color: white;
}
</style>
```

### Custom Width

```razor
<HonuaRouting Width="500px" />
<HonuaRouting Width="100%" />
```

## Performance Tips

1. **Use OSRM for Development**: Free and fast for testing
2. **Cache Routes**: Store frequently-used routes
3. **Limit Alternatives**: Set `MaxAlternatives="1"` if not needed
4. **Self-Host for Production**: Host OSRM/GraphHopper for high-volume apps
5. **Debounce Waypoint Changes**: Don't recalculate on every keystroke

## Browser Support

- Chrome 90+
- Firefox 88+
- Safari 14+
- Edge 90+

## Dependencies

- MudBlazor 6.x
- MapLibre GL JS 3.x
- .NET 8.0+

## Accessibility

- ARIA labels on all interactive elements
- Keyboard navigation support
- Screen reader compatible
- High contrast mode support

## Security Considerations

**API Key Protection:**
```csharp
// Don't expose API keys in client-side code
// Use server-side proxy for production

public class RoutingController : ControllerBase
{
    [HttpPost("api/routing/calculate")]
    public async Task<Route> Calculate([FromBody] RouteRequest request)
    {
        var apiKey = _configuration["Routing:MapboxApiKey"];
        var service = new MapboxRoutingService(new HttpClient(), apiKey);
        return await service.CalculateRouteAsync(request.Waypoints, request.Options);
    }
}
```

## Troubleshooting

### No Route Found
- Check waypoints are valid coordinates
- Ensure waypoints are reachable by the selected travel mode
- Try increasing search radius

### CORS Errors
- Use OSRM demo server or self-host
- Configure CORS on your routing server

### Slow Performance
- Reduce number of waypoints
- Disable alternatives if not needed
- Use faster routing engine (OSRM is fastest)

### Rate Limit Exceeded
- Implement caching
- Use self-hosted OSRM
- Upgrade to paid tier

## Resources

- [OSRM Documentation](http://project-osrm.org/)
- [Mapbox Directions API](https://docs.mapbox.com/api/navigation/directions/)
- [GraphHopper API](https://docs.graphhopper.com/)
- [OpenRouteService API](https://openrouteservice.org/dev/#/api-docs)
- [GPX Format Specification](https://www.topografix.com/gpx.asp)

## See Also

- [HonuaSearch](../Search/README.md) - Geocoding and location search
- [HonuaElevationProfile](../ElevationProfile/README.md) - Elevation charts for routes
- [Examples](./Examples.md) - Practical routing examples
