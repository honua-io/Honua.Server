# Architecture Clarification: Full vs Lite

## Key Distinction

**SkiaSharp vs GDAL - Different Purposes**

### SkiaSharp (Rendering Engine)
- **Purpose**: Render vectors to raster images
- **Examples**:
  - Drawing GeoJSON features on a map → PNG
  - Rendering vector tiles → JPEG
  - Creating map tiles from PostGIS data
- **Location**: `Honua.Server.Core` ✅
- **Available in**: Both Full and Lite ✅

### GDAL (Data Reader)
- **Purpose**: Read raster data files
- **Examples**:
  - Reading GeoTIFF satellite imagery
  - Loading COG (Cloud Optimized GeoTIFF) files
  - Processing elevation data (DEM)
- **Location**: `Honua.Server.Core.Raster` ✅
- **Available in**: Full only ❌

## Module Breakdown

### Honua.Server.Core (Base - Both Full & Lite)
```
Vector Data:
├─ NetTopologySuite (geometry operations)
├─ Database drivers (PostgreSQL, MySQL, SQLite, SQL Server)
├─ GeoJSON, Shapefiles, GeoPackage readers
└─ Vector tile generation

Rendering:
├─ SkiaSharp (vector-to-raster rendering)
└─ Map tile generation from vector data

Core Services:
├─ Authentication & Authorization
├─ Caching
├─ Configuration
└─ Observability
```

### Honua.Server.Core.Raster (Full only)
```
Raster Data Sources:
├─ GDAL (read GeoTIFF, COG, etc.)
├─ LibTIFF (TIFF file support)
├─ Parquet/Arrow (columnar data)
└─ LibGit2Sharp (Git operations)

NOT included:
├─ ✗ SkiaSharp (already in Core)
└─ ✗ Vector rendering (already in Core)
```

### Honua.Server.Core.OData (Both Full & Lite)
```
├─ Microsoft.AspNetCore.OData
└─ OData query translation
```

### Honua.Server.Core.Cloud (Full only)
```
├─ AWS SDK (S3, KMS)
├─ Azure SDK (Blob Storage, Resource Manager)
└─ Google Cloud SDK (GCS, KMS)
```

## What Each Deployment Can Do

### Full Deployment (Honua.Server.Host)

**Vector Data:**
- ✅ Read from PostGIS, MySQL, SQLite, SQL Server
- ✅ Read GeoJSON, Shapefiles, GeoPackage
- ✅ Generate vector tiles (MVT)
- ✅ Render vector data to PNG/JPEG using SkiaSharp

**Raster Data:**
- ✅ Read GeoTIFF files via GDAL
- ✅ Read COG (Cloud Optimized GeoTIFF)
- ✅ Process satellite imagery
- ✅ Serve raster tiles

**Cloud:**
- ✅ Direct S3/Blob/GCS integration
- ✅ Cloud-native storage

### Lite Deployment (Honua.Server.Host.Lite)

**Vector Data:**
- ✅ Read from PostGIS, MySQL, SQLite, SQL Server
- ✅ Read GeoJSON, Shapefiles, GeoPackage
- ✅ Generate vector tiles (MVT)
- ✅ Render vector data to PNG/JPEG using SkiaSharp ← **Important!**

**Raster Data:**
- ❌ Cannot read GeoTIFF files (no GDAL)
- ❌ Cannot read COG files
- ❌ No satellite imagery support
- ❌ No raster tile serving from files

**Cloud:**
- ❌ No direct cloud SDK integration
- ✅ Can still connect to databases in cloud (connection strings)

## Use Case Examples

### Example 1: Vector Tile Server
**Scenario:** Serve vector tiles from PostGIS, render to PNG for legacy clients

**Lite:** ✅ Perfect fit
- Read vectors from PostGIS
- Generate MVT vector tiles
- Render to PNG using SkiaSharp when needed
- Fast cold starts for serverless

**Full:** ✅ Also works, but overkill (larger image, slower startup)

### Example 2: Satellite Imagery Server
**Scenario:** Serve satellite imagery from GeoTIFF files in S3

**Lite:** ❌ Cannot do this (no GDAL to read GeoTIFF)

**Full:** ✅ Required
- Read GeoTIFF via GDAL
- Direct S3 access via AWS SDK
- Process raster data

### Example 3: Mixed Vector/Raster
**Scenario:** Vector data from PostGIS + Raster basemap from GeoTIFF

**Lite:** ❌ Cannot read GeoTIFF (no GDAL)

**Full:** ✅ Required

### Example 4: Vector-Only GIS
**Scenario:** Serve parcels, roads, buildings from PostGIS as WFS/OGC API Features

**Lite:** ✅ Perfect fit
- All vector operations supported
- Can render maps using SkiaSharp
- Smaller footprint
- Faster serverless scaling

**Full:** ✅ Also works, but unnecessary overhead

## Image Size Breakdown

### Full (~150MB)
```
Base .NET Runtime:     ~80MB
SkiaSharp:            ~15MB
GDAL:                 ~40MB
Cloud SDKs:           ~10MB
Other:                ~5MB
```

### Lite (~50-60MB)
```
Base .NET Runtime:     ~40MB (Alpine)
SkiaSharp:            ~12MB
Other:                ~8MB
```

**Savings:** ~90MB (60% smaller) by removing GDAL and Cloud SDKs

## Performance Impact

### Cold Start Comparison (Google Cloud Run)

**Full:**
- Image pull: 2-3s
- Container start: 1-2s
- .NET startup: 1-2s
- **Total: 4-7s**

**Lite:**
- Image pull: 0.5-1s (smaller image)
- Container start: 0.5-1s
- .NET startup: 0.5-1s
- **Total: 1.5-3s**

**50-70% faster cold starts** with Lite!

## Migration Path

### If you're using Lite and need raster:
1. Identify raster data sources
2. Migrate to Full deployment
3. Update Dockerfile reference
4. Redeploy

### If you're using Full but only have vectors:
1. Audit your data sources
2. If no GeoTIFF/COG files, switch to Lite
3. Enjoy smaller images and faster cold starts
4. Save on serverless costs

## Summary

| Feature | Core | Full | Lite |
|---------|------|------|------|
| **SkiaSharp (rendering)** | ✅ | ✅ | ✅ |
| **GDAL (raster files)** | ❌ | ✅ | ❌ |
| **Vector data** | ✅ | ✅ | ✅ |
| **Cloud SDKs** | ❌ | ✅ | ❌ |
| **OData** | via module | ✅ | ✅ |

The key insight: **SkiaSharp is for rendering, GDAL is for reading raster files** - completely different purposes!
