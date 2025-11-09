# Terrain Visualization System

Complete guide to using Honua.Server's terrain visualization capabilities for 3D mapping and elevation analysis.

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Quick Start](#quick-start)
- [Elevation Services](#elevation-services)
- [Terrain Tiles](#terrain-tiles)
- [3D Terrain Rendering](#3d-terrain-rendering)
- [Elevation Profiles](#elevation-profiles)
- [Terrain Analysis](#terrain-analysis)
- [Performance Optimization](#performance-optimization)
- [API Reference](#api-reference)
- [Examples](#examples)

## Overview

The Terrain Visualization System provides comprehensive support for:

- **DEM/Elevation Data**: Parse and query Cloud Optimized GeoTIFF (COG) elevation data
- **Terrain Tiles**: Generate tiles in Mapbox Terrain-RGB format for efficient streaming
- **3D Rendering**: Deck.gl TerrainLayer integration with GPU-accelerated rendering
- **Elevation Profiles**: Generate and analyze elevation profiles along paths
- **Terrain Analysis**: Hillshade, slope, aspect, and contour generation

### Key Features

- ✅ Cloud Optimized GeoTIFF (COG) support with HTTP range requests
- ✅ Martini algorithm for efficient terrain mesh generation
- ✅ Mapbox Terrain-RGB tile encoding
- ✅ Dynamic Level of Detail (LOD) based on zoom
- ✅ Hillshade and slope visualization
- ✅ Elevation query API (point, batch, and area)
- ✅ Binary serialization for efficient data transfer
- ✅ Client-side caching and tile optimization

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Client (Blazor + Deck.gl)                │
├─────────────────────────────────────────────────────────────┤
│  TerrainLayerComponent.razor  │  ElevationProfileTool.razor │
│  terrain-layer.js             │  elevation-utils.js          │
└──────────────────────┬──────────────────────────────────────┘
                       │ REST API / Binary Streams
┌──────────────────────┴──────────────────────────────────────┐
│                    Server (C# / ASP.NET Core)                │
├─────────────────────────────────────────────────────────────┤
│  TerrainController            │  API Endpoints               │
│  - /api/terrain/elevation     │  - Point queries             │
│  - /api/terrain/profile       │  - Batch queries             │
│  - /api/terrain/tiles/*       │  - Tile generation           │
├─────────────────────────────────────────────────────────────┤
│  Services Layer                                              │
│  - ElevationService           │  Query elevation data        │
│  - TerrainTileService         │  Generate terrain tiles      │
├─────────────────────────────────────────────────────────────┤
│  Utilities Layer                                             │
│  - CogReader                  │  Read COG files              │
│  - TerrainMeshGenerator       │  Martini algorithm           │
│  - BinaryGeometrySerializer   │  Binary data transfer        │
└─────────────────────────────────────────────────────────────┘
                       │
┌──────────────────────┴──────────────────────────────────────┐
│                    Data Sources                              │
│  - Local DEM files (GeoTIFF, COG)                           │
│  - Remote COG (HTTP range requests)                         │
│  - Tile services (Mapbox, etc.)                             │
└─────────────────────────────────────────────────────────────┘
```

## Quick Start

### 1. Server Setup

Register terrain services in your `Program.cs`:

```csharp
using Honua.MapSDK.Services.Terrain;

builder.Services.AddSingleton<IElevationService, ElevationService>();
builder.Services.AddSingleton<ITerrainTileService, TerrainTileService>();

// Register elevation data sources
builder.Services.AddHostedService<ElevationDataSourceRegistrar>();
```

Configure elevation data sources:

```csharp
public class ElevationDataSourceRegistrar : IHostedService
{
    private readonly IElevationService _elevationService;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Register local DEM
        _elevationService.RegisterDataSource("srtm", new ElevationDataSource
        {
            Type = ElevationSourceType.LocalRaster,
            Path = "/data/srtm/srtm_merged.tif"
        });

        // Register remote COG
        _elevationService.RegisterDataSource("aws-terrain", new ElevationDataSource
        {
            Type = ElevationSourceType.RemoteCOG,
            Url = "https://s3.amazonaws.com/elevation-tiles-prod/terrain/{z}/{x}/{y}.png"
        });

        return Task.CompletedTask;
    }
}
```

### 2. Client Setup

Add terrain layer to your map:

```razor
@page "/terrain-demo"
@using Honua.MapSDK.Components
@using Honua.MapSDK.Components.Terrain

<HonuaMapLibre MapId="terrain-map" @ref="_map"
               Center="new[] { -122.4194, 37.7749 }"
               Zoom="10"
               Pitch="60"
               Style="mapbox://styles/mapbox/outdoors-v12">

    <Map3DComponent MapId="terrain-map" EnableLighting="true">
        <TerrainLayerComponent
            MapId="terrain-map"
            LayerId="terrain"
            TerrainSource="/api/terrain/tiles/terrain-rgb/{z}/{x}/{y}.png"
            Encoding="terrain-rgb"
            Exaggeration="2.0"
            EnableLOD="true" />
    </Map3DComponent>

</HonuaMapLibre>

@code {
    private HonuaMapLibre? _map;
}
```

## Elevation Services

### Point Queries

Query elevation at a single location:

```csharp
var elevation = await _elevationService.QueryElevationAsync(
    longitude: -122.4194,
    latitude: 37.7749);

Console.WriteLine($"Elevation: {elevation} meters");
```

### Batch Queries

Query multiple points efficiently:

```csharp
var points = new[]
{
    new[] { -122.5, 37.8 },
    new[] { -122.6, 37.9 },
    new[] { -122.7, 38.0 }
};

var elevations = await _elevationService.QueryElevationBatchAsync(points);
```

### Path Elevation Profiles

Generate elevation profiles along a route:

```csharp
var pathCoordinates = new[]
{
    new[] { -122.5, 37.8 },
    new[] { -122.6, 37.9 },
    new[] { -122.7, 38.0 }
};

var profile = await _elevationService.QueryPathElevationAsync(
    coordinates: pathCoordinates,
    samplePoints: 100);

Console.WriteLine($"Total distance: {profile.TotalDistance / 1000:F2} km");
Console.WriteLine($"Elevation gain: {profile.ElevationGain:F0} m");
Console.WriteLine($"Max elevation: {profile.MaxElevation:F0} m");
```

### Area Queries

Get elevation grid for an area:

```csharp
var grid = await _elevationService.QueryAreaElevationAsync(
    minLon: -122.6,
    minLat: 37.7,
    maxLon: -122.5,
    maxLat: 37.8,
    resolution: 100); // 100 cells per degree

// Access elevation at grid coordinates
var elevation = grid.Data[50, 50];
```

## Terrain Tiles

### Mapbox Terrain-RGB Format

Generate terrain tiles compatible with Mapbox GL and Deck.gl:

```csharp
// Generate tile at zoom 10, x=163, y=395
var tileData = await _terrainTileService.GenerateTerrainRGBTileAsync(
    z: 10,
    x: 163,
    y: 395,
    tileSize: 256);

// Returns PNG image with elevation encoded as RGB
// height = -10000 + ((R * 256 * 256 + G * 256 + B) * 0.1)
```

### Terrain Mesh Tiles

Generate optimized 3D meshes using Martini algorithm:

```csharp
var meshTile = await _terrainTileService.GenerateTerrainMeshTileAsync(
    z: 10,
    x: 163,
    y: 395,
    maxError: 1.0f); // Lower = more detail

Console.WriteLine($"Vertices: {meshTile.VertexCount}");
Console.WriteLine($"Triangles: {meshTile.TriangleCount}");

// Use vertices and indices for 3D rendering
var vertices = meshTile.Vertices; // [x1,y1,z1, x2,y2,z2, ...]
var indices = meshTile.Indices;   // [i1,i2,i3, i4,i5,i6, ...]
```

### Hillshade Tiles

Generate hillshade visualization:

```csharp
var hillshadeData = await _terrainTileService.GenerateHillshadeTileAsync(
    z: 10,
    x: 163,
    y: 395,
    azimuth: 315,  // Light direction (degrees)
    altitude: 45); // Light angle (degrees)
```

### Slope Analysis Tiles

Generate slope analysis visualization:

```csharp
var slopeData = await _terrainTileService.GenerateSlopeTileAsync(
    z: 10,
    x: 163,
    y: 395);

// Returns colored slope visualization (green to red)
```

## 3D Terrain Rendering

### Basic Terrain Layer

```razor
<TerrainLayerComponent
    MapId="my-map"
    LayerId="terrain"
    TerrainSource="/api/terrain/tiles/terrain-rgb/{z}/{x}/{y}.png"
    Encoding="terrain-rgb"
    Exaggeration="1.5" />
```

### Advanced Configuration

```razor
<TerrainLayerComponent
    MapId="my-map"
    LayerId="advanced-terrain"
    TerrainSource="/api/terrain/tiles/terrain-rgb/{z}/{x}/{y}.png"
    Encoding="terrain-rgb"
    Exaggeration="2.0"
    Wireframe="false"
    EnableLOD="true"
    MaxLOD="16"
    Material="@_terrainMaterial"
    OnTerrainLoaded="@OnTerrainLoaded" />

@code {
    private TerrainLayerComponent.TerrainMaterial _terrainMaterial = new()
    {
        Ambient = 0.3,
        Diffuse = 0.6,
        Shininess = 32.0,
        SpecularColor = 0.1
    };

    private async Task OnTerrainLoaded(TerrainLayerComponent.TerrainLoadedEvent e)
    {
        Console.WriteLine($"Terrain loaded: {e.LayerId}");
    }
}
```

### Custom Color Mapping

Apply elevation-based color ramps:

```razor
<TerrainLayerComponent
    MapId="my-map"
    LayerId="colored-terrain"
    TerrainSource="/api/terrain/tiles/terrain-rgb/{z}/{x}/{y}.png"
    ColorMap="@_elevationColors" />

@code {
    private double[][] _elevationColors = new[]
    {
        new[] { 0.0, 34, 139, 34 },      // 0m: green
        new[] { 500.0, 255, 255, 0 },    // 500m: yellow
        new[] { 1000.0, 255, 165, 0 },   // 1000m: orange
        new[] { 2000.0, 139, 69, 19 },   // 2000m: brown
        new[] { 3000.0, 255, 255, 255 }  // 3000m: white
    };
}
```

### Dynamic Exaggeration

Update terrain exaggeration in real-time:

```razor
<TerrainLayerComponent @ref="_terrainLayer" ... />

<MudSlider @bind-Value="_exaggeration"
           Min="0.5" Max="5.0" Step="0.1"
           ValueLabel="true"
           ValueChanged="@OnExaggerationChanged" />

@code {
    private TerrainLayerComponent? _terrainLayer;
    private double _exaggeration = 1.0;

    private async Task OnExaggerationChanged(double value)
    {
        if (_terrainLayer != null)
        {
            await _terrainLayer.UpdateExaggerationAsync(value);
        }
    }
}
```

## Elevation Profiles

### Interactive Profile Tool

```razor
<ElevationProfileTool
    MapId="my-map"
    SamplePoints="200"
    OnProfileGenerated="@OnProfileGenerated" />

@code {
    private async Task OnProfileGenerated(ElevationProfile profile)
    {
        Console.WriteLine($"Distance: {profile.TotalDistance / 1000:F2} km");
        Console.WriteLine($"Gain: {profile.ElevationGain:F0} m");
        Console.WriteLine($"Max: {profile.MaxElevation:F0} m");

        // Access individual points
        foreach (var point in profile.Points)
        {
            Console.WriteLine($"{point.Distance}m: {point.Elevation}m");
        }
    }
}
```

### Programmatic Profile Generation

```csharp
// Define path
var coordinates = new[]
{
    new[] { -122.5, 37.8 },
    new[] { -122.6, 37.9 }
};

// Generate profile
var profile = await _elevationService.QueryPathElevationAsync(
    coordinates,
    samplePoints: 100);

// Calculate statistics
var totalGain = 0f;
for (int i = 1; i < profile.Points.Length; i++)
{
    var diff = profile.Points[i].Elevation - profile.Points[i - 1].Elevation;
    if (diff > 0) totalGain += diff;
}
```

## Terrain Analysis

### Hillshade Calculation

```csharp
var hillshade = _terrainTileService.GenerateHillshade(
    elevationGrid,
    azimuth: 315,   // NW light direction
    altitude: 45);  // 45° angle
```

### Slope Calculation

```csharp
var slope = _terrainTileService.CalculateSlope(elevationGrid);

// Slope is in degrees (0-90)
var maxSlope = slope.Cast<float>().Max();
Console.WriteLine($"Maximum slope: {maxSlope:F1}°");
```

### Contour Generation

```javascript
// Client-side contour generation
import { generateContours } from './js/terrain/elevation-utils.js';

const contours = generateContours(elevationGrid, interval = 100);
// Returns array of contour line geometries
```

## Performance Optimization

### Tile Caching

The terrain tile service includes automatic caching:

```csharp
// Cache configuration (in TerrainTileService)
private readonly MemoryCache<string, byte[]> _tileCache =
    new MemoryCache<string, byte[]>(maxItems: 500);
```

### Binary Serialization

Use binary streams for efficient data transfer:

```csharp
[HttpGet("mesh/{z}/{x}/{y}/binary")]
public async Task<IActionResult> GetBinaryMesh(int z, int x, int y)
{
    var mesh = await _terrainTileService.GenerateTerrainMeshTileAsync(z, x, y);

    var stream = new MemoryStream();
    await BinaryGeometrySerializer.SerializeIndexedMeshAsync(
        stream,
        mesh.Vertices,
        ConvertVerticesToColors(mesh.Vertices),
        mesh.Indices);

    stream.Position = 0;
    return File(stream, "application/octet-stream");
}
```

### LOD Strategy

Configure dynamic LOD based on distance:

```javascript
const layer = new TerrainLayer({
    id: 'terrain',
    strategy: 'best-available', // or 'no-overlap'
    minZoom: 0,
    maxZoom: 16,
    loadOptions: {
        terrain: {
            meshMaxError: 4.0 // Higher = fewer triangles
        }
    }
});
```

## API Reference

### REST Endpoints

#### `GET /api/terrain/elevation`

Query elevation at a point.

**Parameters:**
- `lon` (double): Longitude
- `lat` (double): Latitude
- `source` (string, optional): Data source name

**Response:**
```json
{
    "longitude": -122.4194,
    "latitude": 37.7749,
    "elevation": 45.2,
    "source": "srtm",
    "timestamp": "2025-01-15T10:30:00Z"
}
```

#### `POST /api/terrain/elevation/batch`

Query elevation for multiple points.

**Request:**
```json
{
    "points": [
        [-122.5, 37.8],
        [-122.6, 37.9]
    ],
    "source": "srtm"
}
```

**Response:**
```json
{
    "elevations": [45.2, 123.5],
    "count": 2,
    "source": "srtm",
    "timestamp": "2025-01-15T10:30:00Z"
}
```

#### `POST /api/terrain/profile`

Generate elevation profile along a path.

**Request:**
```json
{
    "coordinates": [
        [-122.5, 37.8],
        [-122.6, 37.9],
        [-122.7, 38.0]
    ],
    "samplePoints": 100,
    "source": "srtm"
}
```

#### `GET /api/terrain/tiles/terrain-rgb/{z}/{x}/{y}.png`

Get Terrain-RGB tile (PNG format).

#### `GET /api/terrain/tiles/mesh/{z}/{x}/{y}`

Get terrain mesh tile (JSON format).

**Parameters:**
- `maxError` (float, optional): Maximum mesh error in meters

#### `GET /api/terrain/tiles/hillshade/{z}/{x}/{y}.png`

Get hillshade tile.

**Parameters:**
- `azimuth` (double, optional): Light azimuth (0-360°), default 315
- `altitude` (double, optional): Light altitude (0-90°), default 45

#### `GET /api/terrain/tiles/slope/{z}/{x}/{y}.png`

Get slope analysis tile.

## Examples

### Complete Terrain Viewer

See `TerrainExample.razor` for a complete implementation with:
- 3D terrain rendering
- Elevation profiles
- Hillshade overlay
- Slope analysis
- Interactive controls

### Outdoor Activity Planner

```razor
@* Route planning with elevation awareness *@
<ElevationProfileTool MapId="activity-map" SamplePoints="200"
                      OnProfileGenerated="@AnalyzeRoute" />

@code {
    private async Task AnalyzeRoute(ElevationProfile profile)
    {
        // Calculate difficulty
        var difficulty = CalculateDifficulty(
            profile.TotalDistance,
            profile.ElevationGain);

        // Estimate time
        var estimatedTime = EstimateHikingTime(
            profile.TotalDistance,
            profile.ElevationGain);

        // Find steep sections
        var steepSections = profile.Points
            .Where((p, i) => i > 0 &&
                Math.Abs(CalculateGrade(profile.Points[i - 1], p)) > 15)
            .ToList();
    }
}
```

## Troubleshooting

### Common Issues

**Terrain not rendering:**
- Verify Deck.gl is initialized before terrain layer
- Check terrain tile URLs are correct
- Ensure elevation data sources are registered

**Poor performance:**
- Increase `meshMaxError` to reduce triangle count
- Enable tile caching
- Reduce `maxLOD` for lower zoom levels

**Incorrect elevations:**
- Verify elevation encoding matches data source
- Check coordinate system (WGS84 expected)
- Validate COG file format

## Further Reading

- [Deck.gl TerrainLayer Documentation](https://deck.gl/docs/api-reference/geo-layers/terrain-layer)
- [Mapbox Terrain-RGB Specification](https://docs.mapbox.com/data/tilesets/reference/mapbox-terrain-rgb-v1/)
- [Martini Algorithm](https://github.com/mapbox/martini)
- [Cloud Optimized GeoTIFF](https://www.cogeo.org/)
- [BLAZOR_3D_INTEROP_PERFORMANCE.md](./BLAZOR_3D_INTEROP_PERFORMANCE.md)

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0
