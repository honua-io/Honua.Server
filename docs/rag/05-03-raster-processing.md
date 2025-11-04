---
tags: [raster, cog, zarr, geotiff, tiles, rendering, analytics, processing]
category: development
difficulty: advanced
version: 1.0.0
last_updated: 2025-10-15
---

# Raster Processing Complete Guide

Comprehensive guide to Honua's raster processing architecture: COG and Zarr storage, tile rendering, and analytics.

## Table of Contents
- [Overview](#overview)
- [Architecture](#architecture)
- [Cloud Optimized GeoTIFF (COG)](#cloud-optimized-geotiff-cog)
- [Zarr Arrays](#zarr-arrays)
- [Hybrid Storage](#hybrid-storage)
- [Tile Rendering](#tile-rendering)
- [Tile Caching](#tile-caching)
- [Raster Analytics](#raster-analytics)
- [Performance Optimization](#performance-optimization)
- [Troubleshooting](#troubleshooting)
- [Related Documentation](#related-documentation)

## Overview

Honua implements a modern, cloud-native raster processing architecture optimized for performance and scalability.

### Key Features

- **Pure .NET COG Reader**: No GDAL dependency for reading COGs
- **Native Zarr Support**: Direct Zarr array access
- **Hybrid Storage**: COG for single-time rasters, Zarr for time-series
- **Efficient Tiling**: On-demand tile generation with caching
- **HTTP Range Requests**: Optimized cloud storage access
- **Analytics Engine**: Statistical and spatial analysis

### Supported Formats

| Format | Read | Write | Use Case |
|--------|------|-------|----------|
| COG | ✅ | ✅ | Single-time rasters, DEM, landcover |
| Zarr | ✅ | ✅ | Time-series, multidimensional data |
| GeoTIFF | ✅ | ✅ | Standard rasters |
| PNG | ❌ | ✅ | Tile rendering |
| JPEG | ❌ | ✅ | Photo-realistic tiles |
| WebP | ❌ | ✅ | Modern web tiles |

## Architecture

### Processing Pipeline

```
┌─────────────┐     ┌──────────────┐     ┌─────────────┐
│   Source    │────▶│   Reader     │────▶│  Renderer   │
│ (COG/Zarr)  │     │ (Pure .NET)  │     │  (SkiaSharp)│
└─────────────┘     └──────────────┘     └──────┬──────┘
                                                 │
                                          ┌──────▼──────┐
                                          │  Tile Cache │
                                          │(File/Redis) │
                                          └─────────────┘
```

### Storage Routing

Honua automatically routes to optimal storage:

```csharp
if (dataset.IsSingleTime && !dataset.HasBands)
    → COG (efficient range reads)
else if (dataset.HasTimeSteps || dataset.IsMultidimensional)
    → Zarr (chunk-based access)
else
    → GeoTIFF (standard)
```

## Cloud Optimized GeoTIFF (COG)

### What is COG?

COG is a GeoTIFF with:
- **Tiled structure**: 256x256 or 512x512 pixel tiles
- **Overviews**: Pre-computed lower resolutions
- **HTTP range-friendly**: Headers at start of file

### Creating COGs

**Using GDAL:**
```bash
gdal_translate -of COG \
  -co TILING_SCHEME=GoogleMapsCompatible \
  -co COMPRESS=DEFLATE \
  -co PREDICTOR=2 \
  -co OVERVIEW_RESAMPLING=BILINEAR \
  -co NUM_THREADS=ALL_CPUS \
  input.tif output_cog.tif
```

**Using Python (rio-cogeo):**
```python
from rio_cogeo.cogeo import cog_translate
from rio_cogeo.profiles import cog_profiles

cog_translate(
    "input.tif",
    "output_cog.tif",
    cog_profiles.get("lzw"),
    overview_level=5,
    overview_resampling="bilinear"
)
```

**Verification:**
```bash
# Validate COG
rio cogeo validate output_cog.tif

# View info
gdalinfo output_cog.tif
```

### COG Configuration

**metadata.yaml:**
```yaml
rasters:
  - id: elevation
    title: "Digital Elevation Model"
    source:
      type: cog
      uri: "s3://bucket/elevation.tif"
      # or local
      uri: "file:///data/rasters/elevation.tif"
    bands:
      - name: elevation
        units: meters
        nodata: -9999
    extent:
      spatial: [-180, -90, 180, 90]
      crs: "EPSG:4326"
```

### COG Best Practices

1. **Tile Size**: Use 512x512 for web, 256x256 for analytics
2. **Compression**: DEFLATE for general, LZW for integer, JPEG for photos
3. **Overviews**: Generate full pyramid (factor 2)
4. **Predictor**: Use PREDICTOR=2 for continuous data
5. **Cloud Storage**: Use S3/Azure Blob with HTTP range support

**Example:**
```bash
gdal_translate -of COG \
  -co BLOCKSIZE=512 \
  -co COMPRESS=DEFLATE \
  -co PREDICTOR=2 \
  -co OVERVIEW_RESAMPLING=AVERAGE \
  elevation.tif elevation_cog.tif
```

## Zarr Arrays

### What is Zarr?

Zarr is a chunked, compressed, N-dimensional array format optimized for:
- **Time-series data**: Climate, weather, satellite
- **Large datasets**: Cloud-native chunking
- **Parallel access**: Independent chunk reads
- **Compression**: Per-chunk compression

### Zarr Structure

```
dataset.zarr/
├── .zarray          # Array metadata
├── .zattrs          # User attributes
├── 0.0.0            # Chunk (t=0, y=0, x=0)
├── 0.0.1            # Chunk (t=0, y=0, x=1)
├── 1.0.0            # Chunk (t=1, y=0, x=0)
└── ...
```

### Creating Zarr Arrays

**Using Xarray:**
```python
import xarray as xr
import numpy as np

# Create sample data
times = pd.date_range('2024-01-01', periods=365, freq='D')
lats = np.arange(-90, 90, 0.5)
lons = np.arange(-180, 180, 0.5)

temperature = xr.DataArray(
    np.random.randn(len(times), len(lats), len(lons)),
    dims=['time', 'lat', 'lon'],
    coords={
        'time': times,
        'lat': lats,
        'lon': lons
    },
    attrs={
        'units': 'celsius',
        'long_name': 'Air Temperature'
    }
)

# Save as Zarr
temperature.to_zarr(
    'temperature.zarr',
    mode='w',
    encoding={
        'temperature': {
            'chunks': (1, 180, 360),  # 1 time step, 180x360 spatial
            'compressor': 'zstd'
        }
    }
)
```

**Using Zarr Python:**
```python
import zarr
import numpy as np

# Create array
store = zarr.DirectoryStore('data.zarr')
root = zarr.group(store=store)

dataset = root.create_dataset(
    'temperature',
    shape=(365, 360, 720),  # time, lat, lon
    chunks=(1, 180, 360),
    dtype='f4',
    compressor=zarr.Blosc(cname='zstd', clevel=3)
)

# Add metadata
dataset.attrs['units'] = 'celsius'
dataset.attrs['crs'] = 'EPSG:4326'
dataset.attrs['spatial_extent'] = [-180, -90, 180, 90]
```

### Zarr Configuration

**metadata.yaml:**
```yaml
rasters:
  - id: temperature
    title: "Global Temperature"
    source:
      type: zarr
      uri: "s3://bucket/temperature.zarr"
      array: "temperature"  # array name within store
    dimensions:
      time:
        type: temporal
        units: "days since 2024-01-01"
      lat:
        type: spatial
        units: degrees_north
      lon:
        type: spatial
        units: degrees_east
    extent:
      spatial: [-180, -90, 180, 90]
      temporal: ["2024-01-01", "2024-12-31"]
      crs: "EPSG:4326"
```

### Accessing Zarr from Honua

**WMS Request (specific time):**
```bash
curl "http://localhost:5000/wms?service=WMS&request=GetMap&layers=temperature&time=2024-06-15&crs=EPSG:4326&bbox=-180,-90,180,90&width=800&height=400&format=image/png" \
  -o temp_20240615.png
```

**WCS Request (time slice):**
```bash
curl "http://localhost:5000/wcs?service=WCS&version=2.0.1&request=GetCoverage&coverageId=temperature&subset=time(2024-06-01,2024-06-30)&format=image/tiff" \
  -o temp_june.tif
```

## Hybrid Storage

Honua intelligently routes between COG and Zarr.

### Decision Matrix

| Data Characteristics | Format | Reason |
|---------------------|--------|--------|
| Single time step | COG | Efficient range reads |
| Multiple time steps | Zarr | Chunk-based time access |
| Single band | COG | Simpler structure |
| Many bands | Zarr | Independent band access |
| Static data | COG | Simpler serving |
| Frequently updated | Zarr | Append-friendly |

### Configuration Example

**Mixed dataset collection:**
```yaml
rasters:
  # Static DEM - use COG
  - id: elevation
    source:
      type: cog
      uri: "s3://data/elevation.tif"

  # Time-series temperature - use Zarr
  - id: temperature
    source:
      type: zarr
      uri: "s3://data/temperature.zarr"

  # Landcover (updated yearly) - use COG
  - id: landcover
    source:
      type: cog
      uri: "s3://data/landcover_2024.tif"
```

## Tile Rendering

### Rendering Pipeline

1. **Request**: Client requests tile (z/x/y)
2. **Cache Check**: Check if tile is cached
3. **Source Read**: Read data window from COG/Zarr
4. **Styling**: Apply color ramps, classification
5. **Rendering**: Render with SkiaSharp
6. **Caching**: Store rendered tile
7. **Response**: Return PNG/JPEG/WebP

### Rendering Configuration

**metadata.yaml:**
```yaml
rasters:
  - id: elevation
    rendering:
      colorRamp:
        type: gradient
        stops:
          - value: 0
            color: "#0066ff"  # Blue (sea level)
          - value: 1000
            color: "#00ff00"  # Green
          - value: 3000
            color: "#ffff00"  # Yellow
          - value: 5000
            color: "#ff0000"  # Red (peaks)
      hillshade:
        enabled: true
        azimuth: 315
        altitude: 45
      resampling: bilinear
```

### Color Ramps

**Continuous Gradient:**
```yaml
colorRamp:
  type: gradient
  stops:
    - value: -100
      color: "#000080"
    - value: 0
      color: "#0000ff"
    - value: 100
      color: "#00ff00"
    - value: 500
      color: "#ffff00"
    - value: 1000
      color: "#ff0000"
```

**Classified:**
```yaml
colorRamp:
  type: classified
  classes:
    - min: 0
      max: 100
      color: "#e8f5e9"
      label: "Low"
    - min: 100
      max: 500
      color: "#66bb6a"
      label: "Medium"
    - min: 500
      max: 1000
      color: "#2e7d32"
      label: "High"
```

## Tile Caching

### Cache Providers

**File System:**
```json
{
  "honua": {
    "cache": {
      "enabled": true,
      "provider": "FileSystem",
      "basePath": "./data/cache",
      "maxSizeMb": 10240
    }
  }
}
```

**Redis:**
```json
{
  "honua": {
    "cache": {
      "enabled": true,
      "provider": "Redis",
      "redis": {
        "host": "redis-server",
        "port": 6379,
        "database": 0,
        "ttlSeconds": 86400
      }
    }
  }
}
```

**S3 (for CDN):**
```json
{
  "honua": {
    "cache": {
      "enabled": true,
      "provider": "S3",
      "s3": {
        "bucketName": "honua-tiles",
        "region": "us-west-2",
        "prefix": "tiles/"
      }
    }
  }
}
```

### Cache Preseeding

**Using CLI:**
```bash
# Preseed specific zoom levels
honua raster cache preseed \
  --dataset elevation \
  --zoom-levels "0-10" \
  --threads 8

# Preseed bounded area
honua raster cache preseed \
  --dataset elevation \
  --bbox "-120,35,-115,40" \
  --zoom-levels "10-14" \
  --threads 16

# Check status
honua raster cache status --dataset elevation
```

**Programmatic:**
```bash
curl -X POST http://localhost:5000/api/admin/raster/cache/preseed \
  -H "Authorization: Bearer token" \
  -H "Content-Type: application/json" \
  -d '{
    "datasetId": "elevation",
    "zoomLevels": [0, 1, 2, 3, 4, 5],
    "bbox": [-180, -90, 180, 90]
  }'
```

## Raster Analytics

### Statistical Analysis

**Calculate Statistics:**
```bash
# Using WCS
curl "http://localhost:5000/wcs?service=WCS&version=2.0.1&request=GetCoverage&coverageId=elevation&subset=Lat(35,40)&subset=Long(-120,-115)&format=stats" | jq .

# Response:
{
  "min": 0,
  "max": 4418,
  "mean": 1250.5,
  "stddev": 523.2,
  "count": 1048576
}
```

**Using Honua API:**
```bash
curl "http://localhost:5000/api/raster/elevation/stats?bbox=-120,35,-115,40" \
  -H "Authorization: Bearer token"
```

### Zonal Statistics

**By geometry:**
```bash
curl -X POST http://localhost:5000/api/raster/elevation/zonal-stats \
  -H "Content-Type: application/json" \
  -d '{
    "geometry": {
      "type": "Polygon",
      "coordinates": [[
        [-120, 35], [-120, 40], [-115, 40], [-115, 35], [-120, 35]
      ]]
    },
    "statistics": ["min", "max", "mean", "sum"]
  }'
```

### Sampling

**Sample at points:**
```bash
curl -X POST http://localhost:5000/api/raster/elevation/sample \
  -H "Content-Type: application/json" \
  -d '{
    "points": [
      {"lon": -118.2437, "lat": 34.0522},
      {"lon": -122.4194, "lat": 37.7749}
    ]
  }'

# Response:
[
  {"lon": -118.2437, "lat": 34.0522, "value": 89},
  {"lon": -122.4194, "lat": 37.7749, "value": 16}
]
```

## Performance Optimization

### COG Optimization

1. **Tile size**: 512x512 optimal for web
2. **Compression**: DEFLATE or LZW
3. **Overviews**: Full pyramid
4. **Layout**: Headers at start

**Optimal COG creation:**
```bash
gdal_translate -of COG \
  -co BLOCKSIZE=512 \
  -co COMPRESS=DEFLATE \
  -co PREDICTOR=2 \
  -co NUM_THREADS=ALL_CPUS \
  -co OVERVIEW_RESAMPLING=AVERAGE \
  -co OVERVIEW_COMPRESS=DEFLATE \
  input.tif optimal.tif
```

### Zarr Optimization

1. **Chunk size**: Balance between reads and overhead
2. **Compression**: zstd level 3 recommended
3. **Sharding**: For many small chunks

**Optimal chunking:**
```python
# For time-series (daily data)
chunks = (1, 180, 360)  # 1 day, 180x360 spatial

# For spatial analysis
chunks = (10, 512, 512)  # 10 days, 512x512 spatial
```

### Caching Strategy

**Aggressive (high traffic):**
```json
{
  "cache": {
    "ttlSeconds": 604800,  // 7 days
    "maxSizeMb": 102400,   // 100 GB
    "evictionPolicy": "LFU"  // Least Frequently Used
  }
}
```

**Conservative (low traffic):**
```json
{
  "cache": {
    "ttlSeconds": 86400,   // 1 day
    "maxSizeMb": 10240,    // 10 GB
    "evictionPolicy": "LRU"  // Least Recently Used
  }
}
```

## Troubleshooting

### Issue: Slow Tile Rendering

**Symptoms:** Tiles take >1s to render.

**Solutions:**
1. Check if source is COG (not regular GeoTIFF)
2. Verify overviews exist
3. Enable tile caching
4. Use appropriate zoom level

```bash
# Verify COG
rio cogeo validate data.tif

# Check overviews
gdalinfo data.tif | grep "Overviews"

# Enable caching
honua config set cache.enabled true
```

### Issue: Zarr Chunks Not Found

**Symptoms:** 404 errors reading Zarr chunks.

**Solutions:**
1. Verify Zarr structure
2. Check S3/storage permissions
3. Validate chunk coordinates

```python
import zarr
store = zarr.DirectoryStore('data.zarr')
z = zarr.open(store, mode='r')
print(z.info)
print(z.chunks)
```

### Issue: Out of Memory

**Symptoms:** Server crashes during rendering.

**Solutions:**
1. Reduce tile size
2. Increase memory limits
3. Enable streaming mode

```json
{
  "honua": {
    "raster": {
      "maxTileSize": 256,
      "streamingMode": true
    }
  }
}
```

## Related Documentation

- [OGC Standards](./01-02-ogc-standards-implementation.md) - WMS/WCS/WMTS
- [STAC Catalog](./03-03-stac-catalog.md) - Raster metadata
- [Export Formats](./03-04-export-formats.md) - Output formats
- [Performance Tuning](./04-01-docker-deployment.md) - Optimization

---

**Last Updated**: 2025-10-15
**Honua Version**: 1.0.0-rc1
**Raster Formats**: COG, Zarr, GeoTIFF
