# 3D GeoJSON Quick Start Guide

Get started with 3D geospatial visualization in Honua.Server MapSDK in 5 minutes.

## Prerequisites

- Honua.Server with MapSDK installed
- MapLibre GL JS integration
- Modern browser with WebGL support

## Installation

### 1. Install npm Dependencies

```bash
cd src/Honua.MapSDK
npm install
```

This installs:
- `@deck.gl/core` - Core 3D rendering engine
- `@deck.gl/layers` - Standard layers (GeoJSON, Scatterplot, Path)
- `@deck.gl/geo-layers` - Geospatial layers
- `earcut` - Polygon triangulation

### 2. Add Deck.gl Script Tag

Add to your `_Host.cshtml` or `index.html`:

```html
<head>
    <!-- Existing MapLibre GL JS -->
    <script src="https://unpkg.com/maplibre-gl@3.6.2/dist/maplibre-gl.js"></script>
    <link href="https://unpkg.com/maplibre-gl@3.6.2/dist/maplibre-gl.css" rel="stylesheet" />

    <!-- Add Deck.gl for 3D rendering -->
    <script src="https://unpkg.com/deck.gl@^8.9.0/dist.min.js"></script>
</head>
```

### 3. Add JavaScript Module References

In your `_Host.cshtml` or `App.razor`:

```html
<script src="_content/Honua.MapSDK/js/honua-geometry-3d.js"></script>
<script src="_content/Honua.MapSDK/js/honua-3d.js"></script>
```

## Basic Usage

### Create a 3D Map Page

```razor
@page "/map-3d"
@using Honua.MapSDK.Components
@using Honua.MapSDK.Models

<HonuaMapLibre MapId="my-map"
               Style="https://demotiles.maplibre.org/style.json"
               Center="new[] { -122.4194, 37.7749 }"
               Zoom="13"
               Pitch="45"
               Bearing="0">

    <Map3DComponent MapId="my-map"
                   EnableLighting="true"
                   EnablePicking="true"
                   @ref="_map3D" />
</HonuaMapLibre>

@code {
    private Map3DComponent? _map3D;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await LoadSampleData();
        }
    }

    private async Task LoadSampleData()
    {
        // Create 3D GeoJSON
        var geojson = new
        {
            type = "FeatureCollection",
            features = new[]
            {
                new
                {
                    type = "Feature",
                    geometry = new
                    {
                        type = "Point",
                        coordinates = new[] { -122.4194, 37.7749, 50.0 } // lon, lat, elevation
                    },
                    properties = new
                    {
                        name = "My 3D Point",
                        elevation_m = 50.0
                    }
                }
            }
        };

        // Load as 3D layer
        await _map3D!.LoadGeoJson3DLayerAsync("my-layer", geojson, new()
        {
            Extruded = true,
            FillColor = new[] { 160, 160, 180, 200 },
            Pickable = true
        });
    }
}
```

## Examples

### 3D Buildings

```csharp
var buildings = new
{
    type = "FeatureCollection",
    features = new[]
    {
        new
        {
            type = "Feature",
            geometry = new
            {
                type = "Polygon",
                coordinates = new[]
                {
                    new[]
                    {
                        new[] { -122.420, 37.775, 0.0 },
                        new[] { -122.419, 37.775, 0.0 },
                        new[] { -122.419, 37.776, 0.0 },
                        new[] { -122.420, 37.776, 0.0 },
                        new[] { -122.420, 37.775, 0.0 }
                    }
                }
            },
            properties = new
            {
                name = "Building A",
                height = 50.0,
                base_elevation = 0.0
            }
        }
    }
};

await _map3D.LoadGeoJson3DLayerAsync("buildings", buildings, new()
{
    Extruded = true,
    FillColor = new[] { 120, 140, 180, 200 },
    LineColor = new[] { 60, 70, 90, 255 },
    Pickable = true
});
```

### 3D Flight Path

```csharp
var flightPath = new
{
    type = "Feature",
    geometry = new
    {
        type = "LineString",
        coordinates = new[]
        {
            new[] { -122.425, 37.770, 100.0 },
            new[] { -122.422, 37.772, 150.0 },
            new[] { -122.419, 37.774, 200.0 },
            new[] { -122.416, 37.776, 150.0 },
            new[] { -122.413, 37.778, 100.0 }
        }
    },
    properties = new { name = "Flight Path 001" }
};

await _map3D.LoadPathLayerAsync("flight-path", new[] { flightPath.geometry }, new()
{
    Color = new[] { 255, 100, 0, 200 },
    Width = 3,
    Pickable = true
});
```

### Point Cloud (1 Million Points)

```csharp
// Generate random 3D points
var random = new Random();
var points = new List<double[]>();

for (int i = 0; i < 1_000_000; i++)
{
    var lon = -122.5 + (random.NextDouble() * 0.1);
    var lat = 37.7 + (random.NextDouble() * 0.1);
    var elevation = random.NextDouble() * 100;
    points.Add(new[] { lon, lat, elevation });
}

await _map3D.LoadPointCloudLayerAsync("point-cloud", points.ToArray(), new()
{
    Radius = 2,
    Color = new[] { 0, 255, 150, 180 },
    Pickable = false // Disable for performance
});
```

## Camera Controls

### Set 3D Camera Position

```csharp
await _map3D.SetCamera3DAsync(new Camera3DConfig
{
    Pitch = 60,      // Tilt angle (0-85°)
    Bearing = 45,    // Rotation (0-360°)
    Zoom = 15,       // Zoom level
    Center = new[] { -122.4194, 37.7749 }
}, new()
{
    Duration = 1000  // Animation duration (ms)
});
```

### Interactive Camera Controls

```razor
<label>
    Pitch: @_pitch°
    <input type="range" min="0" max="85" step="1"
           @bind="_pitch" @bind:event="oninput" @bind:after="UpdateCamera" />
</label>

@code {
    private double _pitch = 45;

    private async Task UpdateCamera()
    {
        await _map3D.SetCamera3DAsync(new Camera3DConfig
        {
            Pitch = _pitch,
            Bearing = 0,
            Zoom = 15
        });
    }
}
```

## Layer Management

### Remove Layer

```csharp
await _map3D.RemoveLayerAsync("my-layer");
```

### Update Layer Data

```csharp
await _map3D.UpdateLayerAsync("my-layer", newGeoJsonData);
```

## Feature Interaction

### Handle Feature Clicks

```razor
<Map3DComponent MapId="my-map"
               OnFeatureClick="HandleFeatureClick"
               @ref="_map3D" />

@code {
    private void HandleFeatureClick(Map3DComponent.Feature3DClickEvent e)
    {
        var featureJson = e.Feature.GetRawText();
        Console.WriteLine($"Clicked feature: {featureJson}");
    }
}
```

## Server Integration

### API Endpoint with 3D Data

```csharp
[HttpGet("features")]
public async Task<ActionResult<FeatureCollection>> GetFeatures([FromQuery] int srid = 4326)
{
    // For 3D data, use SRID 4979 (WGS84 with ellipsoidal height)
    var features = await _dbContext.Features
        .Where(f => f.LayerId == "buildings")
        .Select(f => new
        {
            type = "Feature",
            geometry = JsonDocument.Parse(f.GeometryJson), // GeoJSON with [lon, lat, z]
            properties = new
            {
                f.Name,
                f.Height,
                f.BaseElevation
            }
        })
        .ToListAsync();

    return Ok(new
    {
        type = "FeatureCollection",
        features
    });
}
```

### Load from API

```csharp
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        var geojson = await Http.GetFromJsonAsync<object>("/api/features?srid=4979");
        await _map3D!.LoadGeoJson3DLayerAsync("api-layer", geojson);
    }
}
```

## Working with Coordinate3D

### Parse Coordinates

```csharp
using Honua.MapSDK.Models;

// Create 3D coordinate
var coord = Coordinate3D.Create3D(-122.4194, 37.7749, 50.0);

// Convert to array for GeoJSON
var array = coord.ToArray(); // [-122.4194, 37.7749, 50.0]

// Parse from array
var parsed = Coordinate3D.FromArray(new[] { -122.4194, 37.7749, 50.0 });

// Check dimension
var dimension = coord.Dimension; // 3
var hasZ = coord.HasZ; // true

// Get OGC type suffix
var suffix = coord.GetOgcTypeSuffix(); // "Z"
```

### Validate Coordinates

```csharp
// Check if valid WGS84
var isValid = coord.IsValid(); // true if lon/lat in valid range

// Get string representation
var str = coord.ToString(); // "(-122.4194, 37.7749, 50)"
```

## Working with GeoJson3D

### Parse and Analyze 3D GeoJSON

```csharp
using System.Text.Json;
using Honua.MapSDK.Models;

var json = """
{
    "type": "LineString",
    "coordinates": [
        [-122.4194, 37.7749, 10.0],
        [-122.4184, 37.7759, 20.0],
        [-122.4174, 37.7769, 30.0]
    ]
}
""";

var geometryJson = JsonDocument.Parse(json).RootElement;
var geoJson3D = GeoJson3D.FromGeoJson(geometryJson);

// Check 3D properties
Console.WriteLine($"Type: {geoJson3D.OgcTypeName}"); // "LineStringZ"
Console.WriteLine($"Has Z: {geoJson3D.HasZ}"); // true
Console.WriteLine($"Dimension: {geoJson3D.Dimension}"); // 3

// Get Z statistics
var stats = geoJson3D.GetZStatistics();
Console.WriteLine($"Z Min: {stats.Min}"); // 10.0
Console.WriteLine($"Z Max: {stats.Max}"); // 30.0
Console.WriteLine($"Z Mean: {stats.Mean}"); // 20.0
Console.WriteLine($"Z Range: {stats.Range}"); // 20.0

// Validate Z range
var isValid = geoJson3D.ValidateZRange(minElevation: -500, maxElevation: 9000);
```

## Troubleshooting

### Deck.gl Not Loaded

**Error:** `Deck.gl not loaded. 3D features will be unavailable.`

**Solution:** Add Deck.gl script tag to your HTML:
```html
<script src="https://unpkg.com/deck.gl@^8.9.0/dist.min.js"></script>
```

### Map Not Found

**Error:** `MapLibre map 'my-map' not found`

**Solution:** Ensure `MapId` matches between `HonuaMapLibre` and `Map3DComponent`:
```razor
<HonuaMapLibre MapId="my-map" ...>
    <Map3DComponent MapId="my-map" />  <!-- Must match -->
</HonuaMapLibre>
```

### Layers Not Rendering

**Problem:** Layers added but not visible.

**Solutions:**
1. Check browser console for errors
2. Verify GeoJSON has `[lon, lat, z]` coordinates
3. Ensure camera has pitch > 0 for 3D view
4. Check layer colors are not transparent (alpha > 0)
5. Verify coordinates are in valid WGS84 range

### Poor Performance

**Problem:** Slow rendering or low FPS.

**Solutions:**
1. Reduce number of features (use tiling for large datasets)
2. Disable picking for non-interactive layers: `Pickable = false`
3. Use point cloud layer for millions of points
4. Simplify geometries with fewer coordinates
5. Check browser GPU acceleration is enabled

## Performance Tips

1. **Use Point Cloud Layer for Many Points:**
   ```csharp
   // For 1M+ points, use point cloud instead of GeoJSON
   await _map3D.LoadPointCloudLayerAsync("points", points, new() { Pickable = false });
   ```

2. **Disable Picking for Static Layers:**
   ```csharp
   await _map3D.LoadGeoJson3DLayerAsync("background", geojson, new() { Pickable = false });
   ```

3. **Simplify Geometries:**
   ```csharp
   // Use fewer coordinates for distant features
   // Consider Douglas-Peucker simplification
   ```

4. **Use Appropriate Zoom Levels:**
   ```csharp
   // Don't load city-level data at country-level zoom
   if (zoom > 12)
   {
       await LoadDetailedBuildings();
   }
   ```

## Next Steps

- See `/Examples/Map3DExample.razor` for a complete working example
- Read `/docs/CLIENT_3D_ARCHITECTURE.md` for architecture details
- Review `/src/Honua.MapSDK/PHASE_1_3D_IMPLEMENTATION.md` for implementation notes
- Run tests: `cd src/Honua.MapSDK && npm test`

## Resources

- [Deck.gl Documentation](https://deck.gl)
- [MapLibre GL JS Documentation](https://maplibre.org)
- [GeoJSON Specification](https://geojson.org)
- [WGS84 (EPSG:4326)](https://epsg.io/4326)
- [CRS84H (3D Geographic)](http://www.opengis.net/def/crs/OGC/0/CRS84h)

---

**Ready to build amazing 3D maps!** 🗺️✨
