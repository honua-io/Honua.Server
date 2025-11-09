# Client-Side 3D Architecture Plan
**Honua.Server MapSDK 3D Enhancement**

## Executive Summary

This document outlines the architecture for adding comprehensive 3D geometry support to Honua.Server's client applications, building on open-source tooling with a focus on high performance. The plan bridges the current gap between the server's robust 3D support and the client's limited 2D capabilities.

**Current State:**
- ✅ Server: Full 3D support (PostGIS, GeoJSON Z, validation)
- ⚠️ Client: Visual 3D effects only (extrusion), no true 3D geometry handling

**Target State:**
- ✅ Parse and render 3D GeoJSON with Z coordinates
- ✅ Draw and edit 3D geometries
- ✅ 3D terrain visualization
- ✅ Performant rendering of large 3D datasets
- ✅ Round-trip 3D geometry data (client ↔ server)

---

## 1. Technology Stack (Open Source)

### Core 3D Rendering Engines

**Option A: MapLibre GL JS + Deck.gl (Recommended)**
- **MapLibre GL JS** (already integrated)
  - Vector tiles, 2D rendering, pitch/bearing
  - Terrain layer support (3D elevation)
  - WebGL-based, highly performant

- **Deck.gl** (Uber's WebGL framework)
  - Purpose-built for large-scale 3D geospatial data
  - 60fps rendering of millions of features
  - Built-in 3D layers: PointCloud, Path, Polygon, GeoJSON, Terrain
  - MapLibre integration available
  - License: MIT

**Option B: Cesium.js (Alternative for extreme 3D)**
- Full 3D globe engine with WGS84 ellipsoid
- Native 3D geometry support
- Time-dynamic visualization
- Heavier than MapLibre+Deck.gl (~3MB vs ~500KB)
- License: Apache 2.0

**Recommendation: MapLibre GL JS + Deck.gl**
- Builds on existing infrastructure
- Lightweight and performant
- Excellent for 2.5D and 3D data visualization
- Strong GeoJSON support

### Supporting Libraries

| Library | Purpose | License | Size |
|---------|---------|---------|------|
| **MapLibre GL JS** | Base map, 2D/2.5D | BSD | 500KB |
| **Deck.gl** | 3D visualization | MIT | 400KB |
| **Turf.js** | Geospatial calculations | MIT | 150KB |
| **@turf/invariant** | Z-aware calculations | MIT | 5KB |
| **earcut** | 3D polygon triangulation | ISC | 13KB |
| **web-worker-pool** | Parallel geometry processing | MIT | 8KB |
| **three.js** (optional) | Advanced 3D models | MIT | 600KB |

### Data Format Support

- **Input Formats:**
  - GeoJSON with Z coordinates `[lon, lat, elevation]`
  - WKT Z format: `POINT Z (lon lat elev)`
  - Mapbox Vector Tiles (2D only, but can reference Z from attributes)
  - Terrain RGB tiles (elevation encoding)
  - Terrarium tiles (Mapzen format)

- **Output Formats:**
  - GeoJSON 3D
  - WKT Z
  - Feature properties with Z metadata

---

## 2. Architecture Overview

### System Layers

```
┌──────────────────────────────────────────────────────┐
│          Blazor Components (C#)                      │
│  - Map3DComponent                                    │
│  - Geometry3DEditor                                  │
│  - TerrainLayer                                      │
│  - Elevation3DProfile                                │
└────────────────┬─────────────────────────────────────┘
                 │ JS Interop
┌────────────────▼─────────────────────────────────────┐
│          JavaScript 3D Layer                         │
│  - honua-3d.js (orchestrator)                        │
│  - honua-geometry-3d.js (parsing/serialization)      │
│  - honua-terrain.js (terrain tiles)                  │
│  - honua-draw-3d.js (3D editing)                     │
└────────────────┬─────────────────────────────────────┘
                 │
      ┌──────────┴───────────┐
      │                      │
┌─────▼─────┐         ┌─────▼─────┐
│ MapLibre  │         │  Deck.gl  │
│  GL JS    │◄────────┤  Layers   │
│           │         │           │
└─────┬─────┘         └─────┬─────┘
      │                     │
      └──────────┬──────────┘
                 │ WebGL
┌────────────────▼─────────────────────────────────────┐
│              GPU Rendering                           │
└──────────────────────────────────────────────────────┘
```

### Component Architecture

```
Honua.MapSDK/
├── Components/
│   ├── Map3DComponent.razor          (New)
│   ├── TerrainLayer.razor            (New)
│   ├── Geometry3DEditor.razor        (New)
│   └── PointCloud3DLayer.razor       (New)
├── Models/
│   ├── Coordinate3D.cs               (New)
│   ├── GeoJson3D.cs                  (New)
│   ├── TerrainOptions.cs             (New)
│   └── Layer3DDefinition.cs          (Enhanced)
├── Utils/
│   ├── Geometry3DUtils.cs            (New)
│   └── CoordinateTransform3D.cs      (New)
└── wwwroot/js/
    ├── honua-3d.js                   (New - main)
    ├── honua-geometry-3d.js          (New)
    ├── honua-terrain.js              (New)
    ├── honua-draw-3d.js              (New)
    ├── workers/
    │   └── geometry-processor.js     (New - Web Worker)
    └── libs/
        ├── deck.gl.min.js            (Vendor)
        └── earcut.min.js             (Vendor)
```

---

## 3. Performance Strategy

### Key Performance Optimizations

#### 3.1 WebGL Instancing
```javascript
// Render 100,000 points using GPU instancing
const pointLayer = new deck.ScatterplotLayer({
  id: 'points-3d',
  data: features,
  pickable: true,
  getPosition: d => d.geometry.coordinates, // [lon, lat, z]
  getRadius: 5,
  getFillColor: [255, 140, 0],
  instanceCount: features.length,
  // Single draw call for all instances
});
```

**Performance:** 1M+ points at 60fps

#### 3.2 Level of Detail (LOD)
```javascript
// Adaptive detail based on zoom/pitch
const lodConfig = {
  minZoom: 0,
  maxZoom: 22,
  detailLevels: [
    { zoom: 0,  maxPoints: 10000,   simplifyTolerance: 100 },
    { zoom: 10, maxPoints: 100000,  simplifyTolerance: 10 },
    { zoom: 15, maxPoints: 1000000, simplifyTolerance: 1 },
  ]
};
```

#### 3.3 Web Workers for Geometry Processing
```javascript
// Parallel geometry processing (doesn't block UI)
const worker = new Worker('/js/workers/geometry-processor.js');
worker.postMessage({
  type: 'parse3DGeoJSON',
  data: largeGeoJSON
});

worker.onmessage = (e) => {
  const processedGeometry = e.data;
  renderLayer(processedGeometry);
};
```

**Benefit:** Parse 10MB GeoJSON in ~100ms without UI freeze

#### 3.4 Tile-Based Streaming
```javascript
// Only load visible 3D tiles
const tileLayer = new deck.TileLayer({
  data: 'https://server.com/tiles/{z}/{x}/{y}.geojson',
  minZoom: 0,
  maxZoom: 14,
  tileSize: 512,
  renderSubLayers: props => {
    return new deck.GeoJsonLayer(props);
  }
});
```

**Memory:** Load only visible tiles (~500KB each vs full dataset)

#### 3.5 Binary Data Transfer
```javascript
// Use typed arrays for 3D coordinates (60% size reduction)
const coordinates = new Float32Array([
  lon1, lat1, z1,
  lon2, lat2, z2,
  // ... millions more
]);

// Transfer to Web Worker (zero-copy)
worker.postMessage({ coords: coordinates }, [coordinates.buffer]);
```

#### 3.6 GPU Culling
```javascript
// Let GPU cull non-visible geometries
const layer = new deck.GeoJsonLayer({
  data: features,
  extruded: true,
  wireframe: false,
  // GPU automatically culls back-facing triangles
  // and features outside viewport
});
```

### Performance Targets

| Metric | Target | Strategy |
|--------|--------|----------|
| **Frame Rate** | 60 FPS | WebGL batching, instancing |
| **Initial Load** | < 2s | Code splitting, lazy loading |
| **Large Dataset** | 1M+ features | Web Workers, LOD, tiling |
| **Memory** | < 500MB | Tile streaming, data cleanup |
| **3D Geometry Parse** | < 100ms/10MB | Web Workers, binary formats |

---

## 4. Data Flow Architecture

### 4.1 GeoJSON 3D Pipeline

```
Server (PostGIS)
  │
  │ SQL: ST_AsGeoJSON(geom, 15, 1) -- maxdecimaldigits=15, options=1 (bbox)
  ▼
OGC API Features / WFS
  │
  │ HTTP Response:
  │ {
  │   "type": "FeatureCollection",
  │   "features": [{
  │     "geometry": {
  │       "type": "Point",
  │       "coordinates": [-122.4194, 37.7749, 50.0]  // [lon, lat, elevation]
  │     }
  │   }]
  │ }
  ▼
Blazor C# (MapSDK)
  │
  │ JS Interop: DotNetObjectReference
  ▼
JavaScript Layer
  │
  │ honua-geometry-3d.js:
  │ - Parse GeoJSON
  │ - Extract Z coordinates
  │ - Validate ranges
  │ - Create Deck.gl data structures
  ▼
Deck.gl Layer
  │
  │ GeoJsonLayer with extruded=true
  │ getElevation: f => f.geometry.coordinates[2]
  ▼
WebGL Rendering
  │
  │ Triangulation, lighting, depth buffer
  ▼
GPU Framebuffer → Screen
```

### 4.2 Drawing 3D Geometry Flow

```
User Click (Map)
  ▼
MapLibre Event
  ▼
honua-draw-3d.js
  │
  │ 1. Get click position [lon, lat]
  │ 2. Query terrain for elevation OR
  │    prompt user for Z value
  │ 3. Create 3D coordinate
  ▼
Draw State Manager
  │
  │ Accumulate points: [[lon, lat, z], ...]
  ▼
Deck.gl Edit Layer
  │
  │ Visual feedback during drawing
  ▼
Complete Drawing
  │
  │ Create GeoJSON with Z
  ▼
C# Blazor Component
  │
  │ Feature3DCreated event callback
  ▼
Server API (POST)
  │
  │ INSERT INTO layers (geom) VALUES (ST_GeomFromGeoJSON(...))
```

---

## 5. Core Components Design

### 5.1 Coordinate3D Model (C#)

```csharp
// Honua.MapSDK/Models/Coordinate3D.cs
namespace Honua.MapSDK.Models;

/// <summary>
/// Represents a 3D geographic coordinate with optional measure value.
/// </summary>
public sealed record Coordinate3D
{
    /// <summary>Longitude (X) in degrees</summary>
    public required double Longitude { get; init; }

    /// <summary>Latitude (Y) in degrees</summary>
    public required double Latitude { get; init; }

    /// <summary>Elevation/Altitude (Z) in meters</summary>
    public double? Elevation { get; init; }

    /// <summary>Measure (M) value for linear referencing</summary>
    public double? Measure { get; init; }

    /// <summary>Coordinate dimension (2, 3, or 4)</summary>
    public int Dimension => Measure.HasValue
        ? (Elevation.HasValue ? 4 : 3)
        : (Elevation.HasValue ? 3 : 2);

    /// <summary>Convert to GeoJSON coordinate array</summary>
    public double[] ToArray() => Dimension switch
    {
        2 => [Longitude, Latitude],
        3 when Elevation.HasValue => [Longitude, Latitude, Elevation.Value],
        3 when Measure.HasValue => [Longitude, Latitude, Measure.Value],
        4 => [Longitude, Latitude, Elevation!.Value, Measure!.Value],
        _ => [Longitude, Latitude]
    };

    /// <summary>Parse from GeoJSON coordinate array</summary>
    public static Coordinate3D FromArray(double[] coords) => coords.Length switch
    {
        >= 4 => new() { Longitude = coords[0], Latitude = coords[1],
                        Elevation = coords[2], Measure = coords[3] },
        >= 3 => new() { Longitude = coords[0], Latitude = coords[1],
                        Elevation = coords[2] },
        >= 2 => new() { Longitude = coords[0], Latitude = coords[1] },
        _ => throw new ArgumentException("Invalid coordinate array")
    };

    /// <summary>WGS84 3D SRID</summary>
    public const int Srid3D = 4979;
}
```

### 5.2 GeoJson3D Model (C#)

```csharp
// Honua.MapSDK/Models/GeoJson3D.cs
namespace Honua.MapSDK.Models;

/// <summary>
/// Represents a 3D GeoJSON geometry with Z coordinates.
/// </summary>
public sealed record GeoJson3D
{
    public required string Type { get; init; } // "Point", "LineString", etc.
    public required List<double> Coordinates { get; init; } // Flat array
    public int Dimension { get; init; } = 3; // 2, 3, or 4

    /// <summary>Get OGC geometry type name (e.g., "PointZ", "LineStringZ")</summary>
    public string OgcTypeName => Dimension == 3 ? $"{Type}Z" : Type;

    /// <summary>Parse from GeoJSON geometry object</summary>
    public static GeoJson3D FromGeoJson(JsonElement geometryJson)
    {
        var type = geometryJson.GetProperty("type").GetString()!;
        var coords = geometryJson.GetProperty("coordinates");

        // Detect dimension from first coordinate
        var dimension = DetectDimension(coords);

        // Flatten coordinates to single array
        var flatCoords = FlattenCoordinates(coords);

        return new GeoJson3D
        {
            Type = type,
            Coordinates = flatCoords,
            Dimension = dimension
        };
    }

    private static int DetectDimension(JsonElement coords)
    {
        // Navigate to first coordinate (handle nested arrays)
        var current = coords;
        while (current.ValueKind == JsonValueKind.Array &&
               current.GetArrayLength() > 0 &&
               current[0].ValueKind == JsonValueKind.Array)
        {
            current = current[0];
        }

        return current.GetArrayLength(); // 2, 3, or 4
    }
}
```

### 5.3 TerrainLayer Component (Blazor)

```razor
<!-- Honua.MapSDK/Components/TerrainLayer.razor -->
@inject IJSRuntime JS

<div class="terrain-layer">
    @if (ShowControls)
    {
        <div class="terrain-controls">
            <label>
                <input type="checkbox" @bind="Enabled" @bind:after="OnEnabledChanged" />
                Enable Terrain
            </label>
            <label>
                Exaggeration: <input type="range" min="0" max="5" step="0.1"
                                      @bind="Exaggeration" @bind:after="OnExaggerationChanged" />
                @Exaggeration.ToString("F1")x
            </label>
        </div>
    }
</div>

@code {
    [Parameter] public string MapId { get; set; } = "map";
    [Parameter] public bool Enabled { get; set; } = false;
    [Parameter] public double Exaggeration { get; set; } = 1.0;
    [Parameter] public bool ShowControls { get; set; } = true;
    [Parameter] public string? TerrainSourceUrl { get; set; }

    private DotNetObjectReference<TerrainLayer>? _dotNetRef;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);

            var options = new
            {
                mapId = MapId,
                enabled = Enabled,
                exaggeration = Exaggeration,
                sourceUrl = TerrainSourceUrl ?? "https://demotiles.maplibre.org/terrain-tiles/{z}/{x}/{y}.png"
            };

            await JS.InvokeVoidAsync("HonuaTerrain.initialize", options);
        }
    }

    private async Task OnEnabledChanged()
    {
        await JS.InvokeVoidAsync("HonuaTerrain.setEnabled", MapId, Enabled);
    }

    private async Task OnExaggerationChanged()
    {
        await JS.InvokeVoidAsync("HonuaTerrain.setExaggeration", MapId, Exaggeration);
    }

    public void Dispose()
    {
        _dotNetRef?.Dispose();
    }
}
```

### 5.4 JavaScript 3D Geometry Parser

```javascript
// wwwroot/js/honua-geometry-3d.js

window.HonuaGeometry3D = {
    /**
     * Parse GeoJSON with 3D coordinates
     * @param {object} geojson - GeoJSON FeatureCollection
     * @returns {object} Parsed features with Z coordinates
     */
    parse3DGeoJSON(geojson) {
        const features = geojson.features || [geojson];
        const parsed = [];

        for (const feature of features) {
            const geometry = feature.geometry;
            const dimension = this._detectDimension(geometry.coordinates);

            if (dimension >= 3) {
                parsed.push({
                    ...feature,
                    properties: {
                        ...feature.properties,
                        _dimension: dimension,
                        _hasZ: true,
                        _zMin: this._getMinZ(geometry.coordinates),
                        _zMax: this._getMaxZ(geometry.coordinates)
                    }
                });
            } else {
                parsed.push(feature);
            }
        }

        return {
            type: 'FeatureCollection',
            features: parsed,
            metadata: {
                total: parsed.length,
                with3D: parsed.filter(f => f.properties._hasZ).length,
                withoutZ: parsed.filter(f => !f.properties._hasZ).length
            }
        };
    },

    /**
     * Extract Z coordinate from position array
     * @param {number[]} position - [lon, lat, z?, m?]
     * @returns {number|null}
     */
    getZ(position) {
        return position.length >= 3 ? position[2] : null;
    },

    /**
     * Set Z coordinate on position array
     * @param {number[]} position - [lon, lat, z?]
     * @param {number} z - New Z value
     * @returns {number[]} - [lon, lat, z]
     */
    setZ(position, z) {
        if (position.length === 2) {
            return [...position, z];
        } else {
            position[2] = z;
            return position;
        }
    },

    /**
     * Detect coordinate dimension from nested coordinate array
     */
    _detectDimension(coords) {
        // Navigate to first leaf coordinate
        let current = coords;
        while (Array.isArray(current[0])) {
            current = current[0];
        }
        return current.length; // 2, 3, or 4
    },

    /**
     * Get minimum Z value from coordinate array
     */
    _getMinZ(coords) {
        const flatCoords = this._flattenCoordinates(coords);
        const zValues = flatCoords.filter((_, i) => i % 3 === 2);
        return Math.min(...zValues);
    },

    /**
     * Get maximum Z value from coordinate array
     */
    _getMaxZ(coords) {
        const flatCoords = this._flattenCoordinates(coords);
        const zValues = flatCoords.filter((_, i) => i % 3 === 2);
        return Math.max(...zValues);
    },

    /**
     * Flatten nested coordinate arrays
     */
    _flattenCoordinates(coords, result = []) {
        for (const item of coords) {
            if (typeof item === 'number') {
                result.push(item);
            } else if (Array.isArray(item)) {
                this._flattenCoordinates(item, result);
            }
        }
        return result;
    }
};
```

### 5.5 Deck.gl Integration Layer

```javascript
// wwwroot/js/honua-3d.js

import { Deck } from '@deck.gl/core';
import { GeoJsonLayer, ScatterplotLayer, PathLayer, PolygonLayer } from '@deck.gl/layers';
import { TerrainLayer } from '@deck.gl/geo-layers';

window.Honua3D = {
    _deckInstances: new Map(),

    /**
     * Initialize 3D rendering for a map
     * @param {string} mapId - Map container ID
     * @param {object} map - MapLibre GL map instance
     */
    initialize(mapId, map) {
        const deck = new Deck({
            canvas: `${mapId}-deck-canvas`,
            width: '100%',
            height: '100%',
            initialViewState: {
                longitude: map.getCenter().lng,
                latitude: map.getCenter().lat,
                zoom: map.getZoom(),
                pitch: map.getPitch(),
                bearing: map.getBearing()
            },
            controller: false, // MapLibre handles controls
            layers: []
        });

        // Sync view state with MapLibre
        map.on('move', () => {
            deck.setProps({
                viewState: {
                    longitude: map.getCenter().lng,
                    latitude: map.getCenter().lat,
                    zoom: map.getZoom(),
                    pitch: map.getPitch(),
                    bearing: map.getBearing()
                }
            });
        });

        this._deckInstances.set(mapId, { deck, map, layers: [] });
        return deck;
    },

    /**
     * Add 3D GeoJSON layer
     * @param {string} mapId
     * @param {string} layerId
     * @param {object} geojson
     * @param {object} options
     */
    addGeoJsonLayer(mapId, layerId, geojson, options = {}) {
        const instance = this._deckInstances.get(mapId);
        if (!instance) return;

        const layer = new GeoJsonLayer({
            id: layerId,
            data: geojson,

            // 3D rendering options
            extruded: options.extruded ?? true,
            wireframe: options.wireframe ?? false,

            // Z coordinate handling
            getElevation: f => {
                const coords = f.geometry.coordinates;
                return this._getZ(coords) ?? 0;
            },

            // Height for extrusion
            getLineWidth: options.lineWidth ?? 2,
            getFillColor: options.fillColor ?? [160, 160, 180, 200],
            getLineColor: options.lineColor ?? [80, 80, 80],

            // Performance
            pickable: options.pickable ?? true,
            autoHighlight: true,
            highlightColor: [255, 255, 0, 100],

            // Callbacks
            onClick: options.onClick,
            onHover: options.onHover
        });

        instance.layers.push(layer);
        instance.deck.setProps({ layers: instance.layers });

        return layer;
    },

    /**
     * Add 3D point cloud layer (high performance for millions of points)
     * @param {string} mapId
     * @param {string} layerId
     * @param {Array} points - Array of [lon, lat, z]
     * @param {object} options
     */
    addPointCloudLayer(mapId, layerId, points, options = {}) {
        const instance = this._deckInstances.get(mapId);
        if (!instance) return;

        const layer = new ScatterplotLayer({
            id: layerId,
            data: points,

            // Use Z coordinate for elevation
            getPosition: d => d, // [lon, lat, z]
            getRadius: options.radius ?? 5,
            getFillColor: options.color ?? [255, 140, 0],

            // Performance optimizations
            radiusMinPixels: 1,
            radiusMaxPixels: 30,
            instanceCount: points.length,

            // Picking
            pickable: options.pickable ?? true,
            onClick: options.onClick
        });

        instance.layers.push(layer);
        instance.deck.setProps({ layers: instance.layers });

        return layer;
    },

    /**
     * Extract Z coordinate from various geometry types
     */
    _getZ(coords) {
        if (typeof coords[0] === 'number') {
            // Leaf coordinate: [lon, lat, z?]
            return coords[2];
        } else if (Array.isArray(coords[0])) {
            // Nested: recurse to first coordinate
            return this._getZ(coords[0]);
        }
        return null;
    },

    /**
     * Remove layer
     */
    removeLayer(mapId, layerId) {
        const instance = this._deckInstances.get(mapId);
        if (!instance) return;

        instance.layers = instance.layers.filter(l => l.id !== layerId);
        instance.deck.setProps({ layers: instance.layers });
    },

    /**
     * Update layer data
     */
    updateLayer(mapId, layerId, newData) {
        const instance = this._deckInstances.get(mapId);
        if (!instance) return;

        const layer = instance.layers.find(l => l.id === layerId);
        if (layer) {
            layer.setProps({ data: newData });
        }
    }
};
```

---

## 6. Terrain Visualization

### 6.1 Terrain Data Sources (Open Source)

| Source | Format | Resolution | Coverage | License |
|--------|--------|------------|----------|---------|
| **Mapzen Terrarium** | PNG RGB | 30m | Global | CC-BY 4.0 |
| **AWS Terrain Tiles** | Mapbox RGB | 30m | Global | Public |
| **USGS 3DEP** | GeoTIFF | 10m | USA | Public |
| **Mapbox Terrain-RGB** | PNG RGB | 30m | Global | Mapbox ToS |
| **OpenTopography** | Various | 1m-90m | Regional | Varies |

### 6.2 Terrain Encoding (Terrarium Format)

```javascript
// Decode elevation from RGB pixel
function decodeElevation(r, g, b) {
    return (r * 256 + g + b / 256) - 32768;
}

// Encode elevation to RGB
function encodeElevation(meters) {
    const elevation = meters + 32768;
    const r = Math.floor(elevation / 256);
    const g = Math.floor(elevation % 256);
    const b = Math.floor((elevation % 1) * 256);
    return [r, g, b];
}
```

### 6.3 Terrain Layer Implementation

```javascript
// wwwroot/js/honua-terrain.js

window.HonuaTerrain = {
    /**
     * Add terrain layer to map
     */
    initialize(options) {
        const map = window.HonuaMap._maps.get(options.mapId);
        if (!map) return;

        // Add terrain source
        map.addSource('terrain', {
            type: 'raster-dem',
            url: options.sourceUrl,
            tileSize: 512,
            maxzoom: 14,
            encoding: 'terrarium' // or 'mapbox'
        });

        // Enable 3D terrain
        if (options.enabled) {
            map.setTerrain({
                source: 'terrain',
                exaggeration: options.exaggeration ?? 1.0
            });
        }

        // Add hillshading for visual enhancement
        map.addLayer({
            id: 'hillshading',
            type: 'hillshade',
            source: 'terrain',
            layout: { visibility: 'visible' },
            paint: {
                'hillshade-exaggeration': 0.5
            }
        });
    },

    /**
     * Query terrain elevation at a point
     * @param {string} mapId
     * @param {number} lon
     * @param {number} lat
     * @returns {Promise<number|null>} Elevation in meters
     */
    async queryElevation(mapId, lon, lat) {
        const map = window.HonuaMap._maps.get(mapId);
        if (!map) return null;

        const terrain = map.getTerrain();
        if (!terrain) return null;

        // Use MapLibre's terrain query
        const point = map.project([lon, lat]);
        const elevation = map.queryTerrainElevation(point);

        return elevation;
    },

    setEnabled(mapId, enabled) {
        const map = window.HonuaMap._maps.get(mapId);
        if (!map) return;

        if (enabled) {
            map.setTerrain({ source: 'terrain', exaggeration: 1.0 });
        } else {
            map.setTerrain(null);
        }
    },

    setExaggeration(mapId, exaggeration) {
        const map = window.HonuaMap._maps.get(mapId);
        if (!map) return;

        const terrain = map.getTerrain();
        if (terrain) {
            map.setTerrain({ ...terrain, exaggeration });
        }
    }
};
```

---

## 7. Drawing 3D Geometries

### 7.1 3D Drawing Component (Blazor)

```razor
<!-- Honua.MapSDK/Components/Geometry3DEditor.razor -->
@inject IJSRuntime JS

<div class="geometry-3d-editor">
    <div class="editor-toolbar">
        <button @onclick="() => StartDrawing(GeometryType.Point)">
            Point 3D
        </button>
        <button @onclick="() => StartDrawing(GeometryType.LineString)">
            LineString 3D
        </button>
        <button @onclick="() => StartDrawing(GeometryType.Polygon)">
            Polygon 3D
        </button>
        <button @onclick="CancelDrawing">Cancel</button>
    </div>

    @if (IsDrawing)
    {
        <div class="z-input-panel">
            <label>
                Z Coordinate (elevation):
                <input type="number"
                       step="0.1"
                       @bind="CurrentZ"
                       placeholder="Auto (terrain)" />
            </label>
            <label>
                <input type="checkbox" @bind="UseTerrainZ" />
                Use terrain elevation
            </label>
        </div>
    }
</div>

@code {
    [Parameter] public string MapId { get; set; } = "map";
    [Parameter] public EventCallback<DrawnGeometry3D> OnGeometryCreated { get; set; }

    private bool IsDrawing { get; set; }
    private double? CurrentZ { get; set; }
    private bool UseTerrainZ { get; set; } = true;

    private async Task StartDrawing(GeometryType type)
    {
        IsDrawing = true;
        await JS.InvokeVoidAsync("HonuaDraw3D.startDrawing", MapId, type.ToString(),
            DotNetObjectReference.Create(this), UseTerrainZ, CurrentZ);
    }

    private async Task CancelDrawing()
    {
        IsDrawing = false;
        await JS.InvokeVoidAsync("HonuaDraw3D.cancel", MapId);
    }

    [JSInvokable]
    public async Task OnDrawComplete(DrawnGeometry3D geometry)
    {
        IsDrawing = false;
        await OnGeometryCreated.InvokeAsync(geometry);
    }
}

public class DrawnGeometry3D
{
    public string Type { get; set; } = "";
    public List<Coordinate3D> Coordinates { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
}
```

### 7.2 3D Drawing JavaScript

```javascript
// wwwroot/js/honua-draw-3d.js

window.HonuaDraw3D = {
    _drawState: null,

    /**
     * Start drawing 3D geometry
     */
    async startDrawing(mapId, geometryType, dotNetRef, useTerrainZ, fixedZ) {
        const map = window.HonuaMap._maps.get(mapId);
        if (!map) return;

        this._drawState = {
            mapId,
            geometryType,
            dotNetRef,
            useTerrainZ,
            fixedZ,
            coordinates: [],
            tempLayer: null
        };

        // Add click handler
        map.on('click', this._handleClick.bind(this));

        // Change cursor
        map.getCanvas().style.cursor = 'crosshair';
    },

    /**
     * Handle map click during drawing
     */
    async _handleClick(e) {
        if (!this._drawState) return;

        const { mapId, useTerrainZ, fixedZ, coordinates } = this._drawState;
        const { lng, lat } = e.lngLat;

        // Determine Z coordinate
        let z;
        if (fixedZ !== null && fixedZ !== undefined) {
            z = fixedZ;
        } else if (useTerrainZ) {
            z = await window.HonuaTerrain.queryElevation(mapId, lng, lat) ?? 0;
        } else {
            z = 0;
        }

        // Add coordinate
        coordinates.push([lng, lat, z]);

        // Update visual feedback
        this._updateTempLayer();

        // Check if geometry is complete
        if (this._isGeometryComplete()) {
            this._completeDrawing();
        }
    },

    /**
     * Update temporary visual layer during drawing
     */
    _updateTempLayer() {
        const { mapId, geometryType, coordinates } = this._drawState;

        if (this._drawState.tempLayer) {
            window.Honua3D.removeLayer(mapId, 'temp-draw-layer');
        }

        if (coordinates.length === 0) return;

        let geojson;
        if (geometryType === 'Point' && coordinates.length > 0) {
            geojson = {
                type: 'Feature',
                geometry: {
                    type: 'Point',
                    coordinates: coordinates[0]
                }
            };
        } else if (geometryType === 'LineString' && coordinates.length >= 2) {
            geojson = {
                type: 'Feature',
                geometry: {
                    type: 'LineString',
                    coordinates: coordinates
                }
            };
        } else if (geometryType === 'Polygon' && coordinates.length >= 3) {
            geojson = {
                type: 'Feature',
                geometry: {
                    type: 'Polygon',
                    coordinates: [[...coordinates, coordinates[0]]] // Close polygon
                }
            };
        }

        if (geojson) {
            window.Honua3D.addGeoJsonLayer(mapId, 'temp-draw-layer', geojson, {
                fillColor: [255, 165, 0, 100],
                lineColor: [255, 165, 0, 255]
            });
        }
    },

    /**
     * Check if geometry drawing is complete
     */
    _isGeometryComplete() {
        const { geometryType, coordinates } = this._drawState;

        if (geometryType === 'Point') {
            return coordinates.length >= 1;
        } else if (geometryType === 'LineString') {
            return coordinates.length >= 2; // Could add double-click to complete
        } else if (geometryType === 'Polygon') {
            return coordinates.length >= 3; // Could add double-click to complete
        }

        return false;
    },

    /**
     * Complete drawing and return to C#
     */
    async _completeDrawing() {
        const { mapId, geometryType, coordinates, dotNetRef } = this._drawState;
        const map = window.HonuaMap._maps.get(mapId);

        // Create GeoJSON
        const geojson = {
            type: geometryType,
            coordinates: geometryType === 'Polygon'
                ? [[...coordinates, coordinates[0]]]
                : coordinates
        };

        // Calculate Z statistics
        const zValues = coordinates.map(c => c[2]);
        const properties = {
            zMin: Math.min(...zValues),
            zMax: Math.max(...zValues),
            zMean: zValues.reduce((a, b) => a + b, 0) / zValues.length,
            dimension: 3
        };

        // Send to C#
        await dotNetRef.invokeMethodAsync('OnDrawComplete', {
            type: geometryType,
            coordinates: coordinates.map(([lng, lat, z]) => ({
                longitude: lng,
                latitude: lat,
                elevation: z
            })),
            properties
        });

        // Cleanup
        this.cancel(mapId);
    },

    /**
     * Cancel drawing
     */
    cancel(mapId) {
        const map = window.HonuaMap._maps.get(mapId);
        if (!map) return;

        // Remove handlers
        map.off('click', this._handleClick);

        // Remove temp layer
        if (this._drawState?.tempLayer) {
            window.Honua3D.removeLayer(mapId, 'temp-draw-layer');
        }

        // Reset cursor
        map.getCanvas().style.cursor = '';

        // Clear state
        this._drawState = null;
    }
};
```

---

## 8. Web Worker for Performance

### 8.1 Geometry Processing Worker

```javascript
// wwwroot/js/workers/geometry-processor.js

self.onmessage = async function(e) {
    const { type, data } = e.data;

    switch (type) {
        case 'parse3DGeoJSON':
            const parsed = parse3DGeoJSON(data);
            self.postMessage({ type: 'parsed', data: parsed });
            break;

        case 'simplify3D':
            const simplified = simplify3DGeometry(data.geometry, data.tolerance);
            self.postMessage({ type: 'simplified', data: simplified });
            break;

        case 'calculateZStats':
            const stats = calculateZStatistics(data);
            self.postMessage({ type: 'zStats', data: stats });
            break;
    }
};

/**
 * Parse large GeoJSON files without blocking UI
 */
function parse3DGeoJSON(geojson) {
    const features = geojson.features || [geojson];
    const result = {
        features: [],
        stats: {
            total: features.length,
            with3D: 0,
            without3D: 0,
            zMin: Infinity,
            zMax: -Infinity
        }
    };

    for (const feature of features) {
        const coords = feature.geometry.coordinates;
        const dimension = detectDimension(coords);

        if (dimension >= 3) {
            const { min, max } = getZRange(coords);
            result.stats.with3D++;
            result.stats.zMin = Math.min(result.stats.zMin, min);
            result.stats.zMax = Math.max(result.stats.zMax, max);

            result.features.push({
                ...feature,
                properties: {
                    ...feature.properties,
                    _hasZ: true,
                    _zMin: min,
                    _zMax: max
                }
            });
        } else {
            result.stats.without3D++;
            result.features.push(feature);
        }
    }

    return result;
}

/**
 * Simplify 3D LineString/Polygon using Douglas-Peucker in 3D
 */
function simplify3DGeometry(coords, tolerance) {
    // Implementation of 3D Douglas-Peucker algorithm
    // ...
    return simplified;
}

function detectDimension(coords) {
    let current = coords;
    while (Array.isArray(current[0])) {
        current = current[0];
    }
    return current.length;
}

function getZRange(coords, min = Infinity, max = -Infinity) {
    for (const coord of coords) {
        if (typeof coord === 'number') {
            // We're at a leaf coordinate
            return { min, max };
        } else if (Array.isArray(coord)) {
            if (typeof coord[0] === 'number') {
                // This is a coordinate array
                if (coord.length >= 3) {
                    min = Math.min(min, coord[2]);
                    max = Math.max(max, coord[2]);
                }
            } else {
                // Recurse
                const range = getZRange(coord, min, max);
                min = range.min;
                max = range.max;
            }
        }
    }
    return { min, max };
}
```

---

## 9. Implementation Phases

### Phase 1: Foundation (2-3 weeks)
**Goal:** Basic 3D infrastructure

- [ ] Install Deck.gl NPM package
- [ ] Create `Coordinate3D` and `GeoJson3D` models
- [ ] Implement `honua-geometry-3d.js` parser
- [ ] Add `honua-3d.js` Deck.gl integration
- [ ] Create basic `Map3DComponent.razor`
- [ ] Unit tests for 3D coordinate parsing

**Deliverable:** Can render 3D GeoJSON on map

### Phase 2: Terrain (1-2 weeks)
**Goal:** Terrain elevation visualization

- [ ] Implement `TerrainLayer.razor` component
- [ ] Create `honua-terrain.js` module
- [ ] Add terrain source configuration
- [ ] Implement elevation query API
- [ ] Add hillshading layer
- [ ] Performance testing with various data sources

**Deliverable:** 3D terrain visualization with exaggeration controls

### Phase 3: Drawing Tools (2-3 weeks)
**Goal:** Draw and edit 3D geometries

- [ ] Create `Geometry3DEditor.razor` component
- [ ] Implement `honua-draw-3d.js` drawing logic
- [ ] Add Z-coordinate input UI
- [ ] Terrain elevation snapping
- [ ] Visual feedback during drawing
- [ ] Save 3D geometries to server

**Deliverable:** Full 3D geometry editing capability

### Phase 4: Performance Optimization (1-2 weeks)
**Goal:** Handle large datasets efficiently

- [ ] Implement Web Worker for geometry processing
- [ ] Add LOD (Level of Detail) system
- [ ] Tile-based streaming for large datasets
- [ ] GPU instancing for point clouds
- [ ] Memory profiling and optimization
- [ ] Benchmark with 1M+ features

**Deliverable:** 60fps with 1M+ 3D features

### Phase 5: Advanced Features (2-3 weeks)
**Goal:** Production-ready 3D system

- [ ] 3D measurement tools (distance, area, volume)
- [ ] 3D spatial queries (within elevation range)
- [ ] Export 3D geometries (KML, Shapefile Z, GeoPackage)
- [ ] 3D feature selection/highlighting
- [ ] Lighting and shadow rendering
- [ ] Time-of-day sun simulation
- [ ] Documentation and examples

**Deliverable:** Complete 3D feature set

### Phase 6: Mobile Support (1-2 weeks)
**Goal:** 3D in HonuaField mobile app

- [ ] Evaluate MAUI 3D rendering options (SkiaSharp vs native)
- [ ] Implement basic 3D geometry display
- [ ] GPS altitude integration
- [ ] 3D track visualization
- [ ] Performance optimization for mobile devices

**Deliverable:** 3D support in mobile app

---

## 10. Testing Strategy

### 10.1 Unit Tests

```csharp
// Honua.MapSDK.Tests/Geometry3DTests.cs
public class Geometry3DTests
{
    [Fact]
    public void Coordinate3D_ToArray_Returns3DArray()
    {
        var coord = new Coordinate3D
        {
            Longitude = -122.4194,
            Latitude = 37.7749,
            Elevation = 50.0
        };

        var array = coord.ToArray();

        Assert.Equal(3, array.Length);
        Assert.Equal(-122.4194, array[0]);
        Assert.Equal(37.7749, array[1]);
        Assert.Equal(50.0, array[2]);
    }

    [Theory]
    [InlineData(new double[] { -122, 37 }, 2)]
    [InlineData(new double[] { -122, 37, 50 }, 3)]
    [InlineData(new double[] { -122, 37, 50, 100 }, 4)]
    public void Coordinate3D_FromArray_DetectsDimension(double[] input, int expectedDim)
    {
        var coord = Coordinate3D.FromArray(input);
        Assert.Equal(expectedDim, coord.Dimension);
    }
}
```

### 10.2 JavaScript Tests (Jest)

```javascript
// wwwroot/js/__tests__/honua-geometry-3d.test.js
describe('HonuaGeometry3D', () => {
    test('parse 3D GeoJSON point', () => {
        const geojson = {
            type: 'Feature',
            geometry: {
                type: 'Point',
                coordinates: [-122.4194, 37.7749, 50.0]
            }
        };

        const parsed = HonuaGeometry3D.parse3DGeoJSON(geojson);

        expect(parsed.features[0].properties._hasZ).toBe(true);
        expect(parsed.features[0].properties._dimension).toBe(3);
    });

    test('getZ extracts Z coordinate', () => {
        const position = [-122.4194, 37.7749, 50.0];
        const z = HonuaGeometry3D.getZ(position);
        expect(z).toBe(50.0);
    });

    test('setZ adds Z coordinate to 2D position', () => {
        const position = [-122.4194, 37.7749];
        const result = HonuaGeometry3D.setZ(position, 50.0);
        expect(result).toEqual([-122.4194, 37.7749, 50.0]);
    });
});
```

### 10.3 Integration Tests

```csharp
public class Map3DIntegrationTests : IAsyncLifetime
{
    private readonly PlaywrightFixture _playwright;

    [Fact]
    public async Task Can_Render_3D_GeoJSON()
    {
        await _playwright.Page.GotoAsync("/map-3d-test");

        // Add 3D layer
        await _playwright.Page.EvaluateAsync(@"
            const geojson = {
                type: 'FeatureCollection',
                features: [{
                    type: 'Feature',
                    geometry: {
                        type: 'Point',
                        coordinates: [-122.4194, 37.7749, 50.0]
                    }
                }]
            };
            Honua3D.addGeoJsonLayer('map', 'test-layer', geojson);
        ");

        // Verify layer exists
        var layerCount = await _playwright.Page.EvaluateAsync<int>(
            "Honua3D._deckInstances.get('map').layers.length"
        );

        Assert.Equal(1, layerCount);
    }
}
```

### 10.4 Performance Benchmarks

```javascript
// Performance benchmark for large datasets
async function benchmark3DRendering() {
    const sizes = [1000, 10000, 100000, 1000000];

    for (const size of sizes) {
        const points = generateRandomPoints3D(size);

        const start = performance.now();
        await Honua3D.addPointCloudLayer('map', `bench-${size}`, points);
        const end = performance.now();

        console.log(`${size} points: ${(end - start).toFixed(2)}ms`);

        // Measure FPS
        const fps = await measureFPS(2000); // 2 second average
        console.log(`FPS: ${fps.toFixed(1)}`);

        Honua3D.removeLayer('map', `bench-${size}`);
    }
}

function generateRandomPoints3D(count) {
    const points = [];
    for (let i = 0; i < count; i++) {
        points.push([
            -180 + Math.random() * 360,  // lon
            -90 + Math.random() * 180,   // lat
            Math.random() * 1000          // z
        ]);
    }
    return points;
}
```

---

## 11. Documentation & Examples

### 11.1 Quick Start Example

```razor
<!-- Pages/Map3DExample.razor -->
@page "/map-3d-example"
@using Honua.MapSDK.Components

<h1>3D Map Example</h1>

<MapComponent MapId="map-3d" @ref="_map">
    <TerrainLayer Enabled="true"
                  Exaggeration="1.5"
                  ShowControls="true" />

    <Geometry3DEditor OnGeometryCreated="HandleGeometryCreated" />
</MapComponent>

<div class="layer-list">
    @foreach (var layer in _layers)
    {
        <div>@layer.Name - Z: @layer.ZMin to @layer.ZMax m</div>
    }
</div>

@code {
    private MapComponent? _map;
    private List<Layer3D> _layers = new();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await LoadSampleData();
        }
    }

    private async Task LoadSampleData()
    {
        // Load 3D buildings
        var buildings = await Http.GetFromJsonAsync<FeatureCollection>(
            "/api/layers/buildings/features?srid=4979"
        );

        await _map!.AddGeoJson3DLayer("buildings", buildings, new()
        {
            Extruded = true,
            FillColor = new[] { 160, 160, 180, 200 },
            GetElevation = f => f.Properties["base_height"],
            GetHeight = f => f.Properties["height"]
        });

        _layers.Add(new Layer3D
        {
            Name = "Buildings",
            ZMin = 0,
            ZMax = 300
        });
    }

    private async Task HandleGeometryCreated(DrawnGeometry3D geometry)
    {
        // Save to server
        var response = await Http.PostAsJsonAsync(
            "/api/features",
            geometry
        );

        if (response.IsSuccessStatusCode)
        {
            // Refresh layer
            await LoadSampleData();
        }
    }
}
```

---

## 12. Deployment Considerations

### 12.1 Bundle Size Optimization

```javascript
// webpack.config.js - Code splitting
module.exports = {
    entry: {
        'honua-map': './wwwroot/js/honua-map.js',
        'honua-3d': './wwwroot/js/honua-3d.js',  // Lazy load
        'honua-terrain': './wwwroot/js/honua-terrain.js'  // Lazy load
    },
    optimization: {
        splitChunks: {
            chunks: 'all',
            cacheGroups: {
                deckgl: {
                    test: /[\\/]node_modules[\\/]@deck.gl/,
                    name: 'deck.gl',
                    priority: 10
                }
            }
        }
    }
};
```

**Bundle Sizes:**
- Core map: ~500KB (MapLibre)
- 3D extension: ~400KB (Deck.gl) - loaded on demand
- Total: ~900KB gzipped

### 12.2 CDN Strategy

```html
<!-- Load from CDN for better caching -->
<script src="https://unpkg.com/maplibre-gl@3.6.2/dist/maplibre-gl.js"></script>
<script src="https://unpkg.com/deck.gl@8.9.33/dist.min.js"></script>
```

### 12.3 Browser Compatibility

| Browser | Version | WebGL | 3D Terrain | Notes |
|---------|---------|-------|------------|-------|
| Chrome | 90+ | ✅ | ✅ | Full support |
| Firefox | 88+ | ✅ | ✅ | Full support |
| Safari | 14+ | ✅ | ✅ | Full support |
| Edge | 90+ | ✅ | ✅ | Full support |
| Mobile Safari | 14+ | ✅ | ⚠️ | Performance varies |
| Mobile Chrome | 90+ | ✅ | ⚠️ | Performance varies |

---

## 13. Cost Analysis

### Open Source Licenses (Zero Cost)

| Component | License | Commercial Use |
|-----------|---------|----------------|
| MapLibre GL JS | BSD-3-Clause | ✅ Free |
| Deck.gl | MIT | ✅ Free |
| Turf.js | MIT | ✅ Free |
| Terrarium Tiles | CC-BY 4.0 | ✅ Free (attribution) |
| earcut | ISC | ✅ Free |

**Total Software Cost: $0**

### Infrastructure Costs (Optional)

- **Terrain Tiles:** Self-host from AWS Open Data (free) or use Mapbox (pay per use)
- **CDN:** Cloudflare free tier or AWS CloudFront
- **Storage:** Minimal (tiles are streamed, not stored client-side)

---

## 14. Success Metrics

### Performance KPIs

| Metric | Target | Measurement |
|--------|--------|-------------|
| Time to First Render | < 2s | Lighthouse |
| 60 FPS | 100K features | FPS counter |
| Memory Usage | < 500MB | Chrome DevTools |
| Parse 10MB GeoJSON | < 100ms | Performance API |
| Tile Load Time | < 500ms | Network tab |

### Feature Completeness

- [ ] Parse 3D GeoJSON (Z coordinates)
- [ ] Render 3D terrain
- [ ] Draw 3D geometries (Point, Line, Polygon)
- [ ] Edit 3D geometries
- [ ] 3D measurements
- [ ] Export 3D data
- [ ] Mobile 3D support

---

## 15. Risk Mitigation

### Technical Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Performance on mobile | High | Medium | Implement aggressive LOD, test early |
| Browser compatibility | Low | High | Polyfills, graceful degradation |
| Large dataset rendering | Medium | High | Tile-based streaming, Web Workers |
| Deck.gl breaking changes | Low | Medium | Pin versions, monitor releases |

### Migration Strategy

**Incremental Rollout:**
1. Deploy 3D features as opt-in beta
2. Run A/B tests with subset of users
3. Monitor performance metrics
4. Gradual rollout to 100% of users

**Backward Compatibility:**
- All existing 2D functionality remains unchanged
- 3D features are additive only
- Feature detection for 3D support

---

## Conclusion

This architecture provides a **high-performance, open-source-based 3D geospatial visualization system** for Honua.Server clients. By leveraging MapLibre GL JS and Deck.gl, we achieve:

✅ **Zero licensing costs** - All components are open source
✅ **60+ FPS** with millions of features via GPU acceleration
✅ **Full 3D geometry support** - Parse, render, edit, export
✅ **Production-ready** - Battle-tested libraries used by Uber, Google, Meta
✅ **Extensible** - Clean architecture for future enhancements

**Next Steps:**
1. Review and approve architecture
2. Set up development environment (Phase 1)
3. Create feature branch: `feature/client-3d-support`
4. Begin Phase 1 implementation (2-3 weeks)

---

**Document Version:** 1.0
**Last Updated:** 2025-11-09
**Author:** Claude (Anthropic)
**Status:** Proposal - Awaiting Approval
