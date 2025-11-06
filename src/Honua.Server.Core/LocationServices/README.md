# Location Services - Pluggable Providers

Honua Server includes a pluggable provider architecture for location services:
- **Geocoding**: Convert addresses ↔ coordinates
- **Routing**: Calculate routes between waypoints
- **Basemap Tiles**: Provide map tiles for visualization

This allows you to switch between different providers (Azure Maps, AWS Location, Google Maps, OpenStreetMap, etc.) without changing your application code.

## Table of Contents

1. [Quick Start](#quick-start)
2. [Available Providers](#available-providers)
3. [Configuration](#configuration)
4. [Usage Examples](#usage-examples)
5. [Creating Custom Providers](#creating-custom-providers)
6. [Production Recommendations](#production-recommendations)

---

## Quick Start

### 1. Add to your `appsettings.json`

```json
{
  "LocationServices": {
    "GeocodingProvider": "azure-maps",
    "RoutingProvider": "azure-maps",
    "BasemapTileProvider": "azure-maps",
    "AzureMaps": {
      "SubscriptionKey": "your-azure-maps-key"
    }
  }
}
```

### 2. Register services in `Program.cs` or `Startup.cs`

```csharp
using Honua.Server.Core.LocationServices;

var builder = WebApplication.CreateBuilder(args);

// Add location services
builder.Services.AddLocationServices(builder.Configuration);
```

### 3. Inject and use in your code

```csharp
public class MyController : ControllerBase
{
    private readonly IGeocodingProvider _geocoding;
    private readonly IRoutingProvider _routing;
    private readonly IBasemapTileProvider _tiles;

    public MyController(
        IGeocodingProvider geocoding,
        IRoutingProvider routing,
        IBasemapTileProvider tiles)
    {
        _geocoding = geocoding;
        _routing = routing;
        _tiles = tiles;
    }

    [HttpGet("geocode")]
    public async Task<IActionResult> Geocode(string address)
    {
        var request = new GeocodingRequest { Query = address };
        var response = await _geocoding.GeocodeAsync(request);
        return Ok(response);
    }
}
```

---

## Available Providers

### Geocoding Providers

| Provider | Key | Cost | Rate Limits | Notes |
|----------|-----|------|-------------|-------|
| **Azure Maps** | `azure-maps` | Pay-as-you-go | None | Requires subscription key |
| **Nominatim (OSM)** | `nominatim` | Free | 1 req/sec | Public instance rate-limited |
| **AWS Location** | `aws-location` | Pay-as-you-go | High | (Coming soon) |
| **Google Maps** | `google-maps` | Pay-as-you-go | High | (Coming soon) |

### Routing Providers

| Provider | Key | Cost | Rate Limits | Notes |
|----------|-----|------|-------------|-------|
| **Azure Maps** | `azure-maps` | Pay-as-you-go | None | Requires subscription key |
| **OSRM** | `osrm` | Free | Moderate | Public instance rate-limited |
| **AWS Location** | `aws-location` | Pay-as-you-go | High | (Coming soon) |
| **Google Maps** | `google-maps` | Pay-as-you-go | High | (Coming soon) |

### Basemap Tile Providers

| Provider | Key | Cost | Rate Limits | Notes |
|----------|-----|------|-------------|-------|
| **Azure Maps** | `azure-maps` | Pay-as-you-go | None | Multiple styles available |
| **OpenStreetMap** | `openstreetmap` | Free | Moderate | For development only |
| **Mapbox** | `mapbox` | Pay-as-you-go | High | (Coming soon) |
| **AWS Location** | `aws-location` | Pay-as-you-go | High | (Coming soon) |

---

## Configuration

### Azure Maps (Recommended for Production)

```json
{
  "LocationServices": {
    "GeocodingProvider": "azure-maps",
    "RoutingProvider": "azure-maps",
    "BasemapTileProvider": "azure-maps",
    "AzureMaps": {
      "SubscriptionKey": "your-subscription-key",
      "BaseUrl": "https://atlas.microsoft.com"
    }
  }
}
```

**Get a subscription key:**
1. Sign up for Azure: https://azure.microsoft.com/free/
2. Create an Azure Maps account
3. Copy the subscription key from the Azure Portal

**Pricing:** https://azure.microsoft.com/pricing/details/azure-maps/

### OpenStreetMap (Free for Development)

```json
{
  "LocationServices": {
    "GeocodingProvider": "nominatim",
    "RoutingProvider": "osrm",
    "BasemapTileProvider": "openstreetmap",
    "Nominatim": {
      "BaseUrl": "https://nominatim.openstreetmap.org",
      "UserAgent": "YourApp/1.0 (contact@yourapp.com)"
    },
    "Osrm": {
      "BaseUrl": "https://router.project-osrm.org"
    },
    "OsmTiles": {
      "UserAgent": "YourApp/1.0 (contact@yourapp.com)"
    }
  }
}
```

**Important:**
- Public OSM services are **rate-limited** and intended for **development/testing only**
- For production, host your own instances or use a commercial provider
- **Always** provide a valid User-Agent with contact information

### Mix and Match Providers

You can use different providers for different services:

```json
{
  "LocationServices": {
    "GeocodingProvider": "azure-maps",
    "RoutingProvider": "osrm",
    "BasemapTileProvider": "openstreetmap"
  }
}
```

### Runtime Provider Selection

You can also select providers at runtime:

```csharp
// Get specific provider by key
var azureGeocoding = serviceProvider.GetGeocodingProvider("azure-maps");
var nominatimGeocoding = serviceProvider.GetGeocodingProvider("nominatim");

// Use the appropriate provider based on user preference
var provider = userPreference == "azure" ? azureGeocoding : nominatimGeocoding;
var result = await provider.GeocodeAsync(request);
```

---

## Usage Examples

### Geocoding

#### Forward Geocoding (Address → Coordinates)

```csharp
var request = new GeocodingRequest
{
    Query = "1600 Amphitheatre Parkway, Mountain View, CA",
    MaxResults = 5,
    CountryCodes = new[] { "US" },
    Language = "en"
};

var response = await geocodingProvider.GeocodeAsync(request);

foreach (var result in response.Results)
{
    Console.WriteLine($"Address: {result.FormattedAddress}");
    Console.WriteLine($"Location: {result.Latitude}, {result.Longitude}");
    Console.WriteLine($"Type: {result.Type}");
    Console.WriteLine($"Confidence: {result.Confidence}");
}
```

#### Reverse Geocoding (Coordinates → Address)

```csharp
var request = new ReverseGeocodingRequest
{
    Longitude = -122.084,
    Latitude = 37.422,
    Language = "en"
};

var response = await geocodingProvider.ReverseGeocodeAsync(request);
var address = response.Results.FirstOrDefault()?.FormattedAddress;
Console.WriteLine($"Address: {address}");
```

### Routing

#### Calculate Route

```csharp
var request = new RoutingRequest
{
    Waypoints = new[]
    {
        new[] { -122.335167, 47.608013 },  // Seattle
        new[] { -122.419418, 37.774929 }   // San Francisco
    },
    TravelMode = "car",
    AvoidTolls = true,
    UseTraffic = true,
    Language = "en",
    UnitSystem = "metric"
};

var response = await routingProvider.CalculateRouteAsync(request);
var route = response.Routes.FirstOrDefault();

if (route != null)
{
    Console.WriteLine($"Distance: {route.DistanceMeters / 1000:F1} km");
    Console.WriteLine($"Duration: {TimeSpan.FromSeconds(route.DurationSeconds)}");
    Console.WriteLine($"Geometry: {route.Geometry}");

    foreach (var instruction in route.Instructions ?? Enumerable.Empty<RouteInstruction>())
    {
        Console.WriteLine($"- {instruction.Text} ({instruction.DistanceMeters}m)");
    }
}
```

#### Truck Routing with Vehicle Restrictions

```csharp
var request = new RoutingRequest
{
    Waypoints = new[] { /* ... */ },
    TravelMode = "truck",
    Vehicle = new VehicleSpecifications
    {
        WeightKg = 15000,
        HeightMeters = 4.2,
        WidthMeters = 2.5,
        LengthMeters = 12.0,
        AxleCount = 3
    },
    AvoidTolls = false,
    AvoidFerries = true
};

var response = await routingProvider.CalculateRouteAsync(request);
```

### Basemap Tiles

#### Get Available Tilesets

```csharp
var tilesets = await basemapProvider.GetAvailableTilesetsAsync();

foreach (var tileset in tilesets)
{
    Console.WriteLine($"ID: {tileset.Id}");
    Console.WriteLine($"Name: {tileset.Name}");
    Console.WriteLine($"Format: {tileset.Format}");
    Console.WriteLine($"Max Zoom: {tileset.MaxZoom}");
}
```

#### Get Tile URL Template (for client-side rendering)

```csharp
var urlTemplate = await basemapProvider.GetTileUrlTemplateAsync("road");
// Returns: "https://atlas.microsoft.com/map/tile?...&zoom={z}&x={x}&y={y}&..."

// Use in MapLibre, Leaflet, OpenLayers, etc.
```

#### Proxy Tiles Through Your Server

```csharp
[HttpGet("tiles/{tilesetId}/{z}/{x}/{y}")]
public async Task<IActionResult> GetTile(
    string tilesetId, int z, int x, int y)
{
    var request = new TileRequest
    {
        TilesetId = tilesetId,
        Z = z,
        X = x,
        Y = y,
        Scale = 1
    };

    var response = await basemapProvider.GetTileAsync(request);
    return File(response.Data, response.ContentType);
}
```

---

## Creating Custom Providers

You can create custom providers by implementing the interfaces:

### Custom Geocoding Provider

```csharp
public class MyGeocodingProvider : IGeocodingProvider
{
    public string ProviderKey => "my-provider";
    public string ProviderName => "My Custom Geocoding Service";

    public async Task<GeocodingResponse> GeocodeAsync(
        GeocodingRequest request,
        CancellationToken cancellationToken = default)
    {
        // Call your geocoding API
        var results = await CallMyGeocodingApi(request.Query);

        return new GeocodingResponse
        {
            Results = results.Select(r => new GeocodingResult
            {
                FormattedAddress = r.Address,
                Longitude = r.Lon,
                Latitude = r.Lat
            }).ToList(),
            Attribution = "© My Company"
        };
    }

    public async Task<GeocodingResponse> ReverseGeocodeAsync(
        ReverseGeocodingRequest request,
        CancellationToken cancellationToken = default)
    {
        // Implement reverse geocoding
    }

    public async Task<bool> TestConnectivityAsync(
        CancellationToken cancellationToken = default)
    {
        // Test your API connectivity
    }
}
```

### Register Custom Provider

```csharp
services.AddKeyedSingleton<IGeocodingProvider>("my-provider",
    sp => new MyGeocodingProvider());
```

---

## Production Recommendations

### 1. **Choose Appropriate Providers**

| Scenario | Recommendation |
|----------|---------------|
| Enterprise production | Azure Maps or AWS Location |
| Cost-sensitive | Self-hosted OSM stack |
| Development/testing | Public OSM services |
| Global coverage | Azure Maps, Google Maps, or AWS Location |
| Specific regions | Check regional provider availability |

### 2. **Self-Host OpenStreetMap Services**

For production use with free OSM data:

**Nominatim (Geocoding):**
```bash
docker run -d \
  -p 8080:8080 \
  -e PBF_URL=https://download.geofabrik.de/north-america-latest.osm.pbf \
  mediagis/nominatim:latest
```

**OSRM (Routing):**
```bash
# Download OSM data
wget https://download.geofabrik.de/north-america-latest.osm.pbf

# Prepare routing data
docker run -t -v $(pwd):/data ghcr.io/project-osrm/osrm-backend \
  osrm-extract -p /opt/car.lua /data/north-america-latest.osm.pbf

docker run -t -v $(pwd):/data ghcr.io/project-osrm/osrm-backend \
  osrm-contract /data/north-america-latest.osrm

# Run routing server
docker run -d -p 5000:5000 -v $(pwd):/data \
  ghcr.io/project-osrm/osrm-backend \
  osrm-routed /data/north-america-latest.osrm
```

**Tile Server:**
```bash
docker run -d \
  -p 8081:80 \
  -v osm-data:/data/database/ \
  overv/openstreetmap-tile-server
```

Then configure Honua to use your self-hosted services:

```json
{
  "LocationServices": {
    "Nominatim": {
      "BaseUrl": "http://your-server:8080"
    },
    "Osrm": {
      "BaseUrl": "http://your-server:5000"
    }
  }
}
```

### 3. **Implement Caching**

Cache geocoding results to reduce API costs:

```csharp
public class CachedGeocodingProvider : IGeocodingProvider
{
    private readonly IGeocodingProvider _innerProvider;
    private readonly IMemoryCache _cache;

    public async Task<GeocodingResponse> GeocodeAsync(
        GeocodingRequest request,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"geocode:{request.Query}";

        if (_cache.TryGetValue<GeocodingResponse>(cacheKey, out var cached))
        {
            return cached;
        }

        var result = await _innerProvider.GeocodeAsync(request, cancellationToken);

        _cache.Set(cacheKey, result, TimeSpan.FromHours(24));

        return result;
    }
}
```

### 4. **Monitor Usage and Costs**

Track API usage to avoid unexpected costs:

```csharp
public class MonitoredGeocodingProvider : IGeocodingProvider
{
    private readonly IGeocodingProvider _innerProvider;
    private readonly ILogger _logger;
    private readonly IMetrics _metrics;

    public async Task<GeocodingResponse> GeocodeAsync(
        GeocodingRequest request,
        CancellationToken cancellationToken = default)
    {
        _metrics.Increment("geocoding.requests", tags: new[]
        {
            $"provider:{_innerProvider.ProviderKey}"
        });

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await _innerProvider.GeocodeAsync(request, cancellationToken);

            _metrics.Histogram("geocoding.duration", stopwatch.ElapsedMilliseconds);
            _metrics.Increment("geocoding.success");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Geocoding failed for query: {Query}", request.Query);
            _metrics.Increment("geocoding.errors");
            throw;
        }
    }
}
```

### 5. **Implement Fallback Providers**

Use multiple providers for reliability:

```csharp
public class FallbackGeocodingProvider : IGeocodingProvider
{
    private readonly IGeocodingProvider _primary;
    private readonly IGeocodingProvider _fallback;

    public async Task<GeocodingResponse> GeocodeAsync(
        GeocodingRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _primary.GeocodeAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Primary geocoding provider failed, trying fallback");
            return await _fallback.GeocodeAsync(request, cancellationToken);
        }
    }
}
```

---

## API Comparison

### Geocoding Feature Matrix

| Feature | Azure Maps | Nominatim | AWS Location | Google Maps |
|---------|------------|-----------|--------------|-------------|
| Forward geocoding | ✅ | ✅ | ✅ | ✅ |
| Reverse geocoding | ✅ | ✅ | ✅ | ✅ |
| Structured address | ✅ | ✅ | ✅ | ✅ |
| Autocomplete | ✅ | ⚠️ Limited | ✅ | ✅ |
| Batch geocoding | ✅ | ❌ | ✅ | ✅ |
| Global coverage | ✅ | ✅ | ✅ | ✅ |
| POI search | ✅ | ⚠️ Limited | ✅ | ✅ |

### Routing Feature Matrix

| Feature | Azure Maps | OSRM | AWS Location | Google Maps |
|---------|------------|------|--------------|-------------|
| Car routing | ✅ | ✅ | ✅ | ✅ |
| Truck routing | ✅ | ❌ | ✅ | ✅ |
| Bicycle routing | ✅ | ✅ | ❌ | ✅ |
| Pedestrian routing | ✅ | ✅ | ❌ | ✅ |
| Traffic data | ✅ | ❌ | ✅ | ✅ |
| Toll avoidance | ✅ | ❌ | ✅ | ✅ |
| Alternative routes | ✅ | ✅ | ✅ | ✅ |
| Isochrones | ✅ | ❌ | ✅ | ❌ |

---

## Troubleshooting

### Nominatim Rate Limiting

**Error:** `HTTP 429 Too Many Requests`

**Solution:** Respect the 1 request per second limit or self-host Nominatim.

```csharp
// Built-in rate limiting in NominatimGeocodingProvider
await Task.Delay(1000, cancellationToken); // 1 request/second
```

### Azure Maps Authentication Errors

**Error:** `HTTP 401 Unauthorized`

**Solution:** Verify your subscription key is correct and active.

```bash
# Test your Azure Maps key
curl "https://atlas.microsoft.com/search/address/json?api-version=2025-01-01&query=Seattle&subscription-key=YOUR_KEY"
```

### OSM Tile Server Requires User-Agent

**Error:** `HTTP 403 Forbidden`

**Solution:** Always provide a User-Agent header with contact information.

```json
{
  "OsmTiles": {
    "UserAgent": "YourApp/1.0 (contact@yourcompany.com)"
  }
}
```

---

## License

This location services abstraction is part of Honua Server and is licensed under the Elastic License 2.0.

The underlying services (Azure Maps, OpenStreetMap, etc.) have their own licensing terms.

---

## Contributing

To add a new provider (e.g., AWS Location Service, Google Maps, Mapbox):

1. Implement the provider interfaces (`IGeocodingProvider`, `IRoutingProvider`, `IBasemapTileProvider`)
2. Add configuration to `LocationServiceConfiguration`
3. Register in `LocationServiceExtensions`
4. Add tests
5. Update documentation

See existing providers for examples.
