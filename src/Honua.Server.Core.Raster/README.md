# Honua.Server.Core.Raster

**Enterprise-grade raster data processing module** for the Honua geospatial server. Provides comprehensive support for Cloud Optimized GeoTIFF (COG), Zarr time-series arrays, multi-format raster processing, and advanced analytics.

---

## Purpose

The Raster module handles all aspects of raster data processing, including:

- **Cloud-Optimized GeoTIFF (COG)** reading and conversion
- **Zarr time-series** data storage and querying
- **Multi-format raster support** (GeoTIFF, NetCDF, HDF5, GRIB2)
- **Tile generation** for web mapping services (WMS, WMTS)
- **Raster analytics** (statistics, algebra, zonal analysis, terrain analysis)
- **Intelligent caching** (multi-tier storage with TTL support)
- **High-performance rendering** using SkiaSharp
- **Data export** (GeoParquet, GeoArrow)

---

## Architecture Overview

### Core Components

```
┌─────────────────────────────────────────────────────────────┐
│                    Raster Module                             │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │  COG Reader  │  │  Zarr Reader │  │ GDAL Wrapper │      │
│  │  LibTiff.NET │  │  HTTP-based  │  │  Config/Init │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
│                                                               │
│  ┌──────────────────────────────────────────────────────┐   │
│  │            Cache Layer (Multi-tier)                   │   │
│  │  • COG Cache (filesystem/S3/GCS/Azure)               │   │
│  │  • Tile Cache (filesystem/S3/GCS/Azure/Redis)        │   │
│  │  • Zarr Chunk Cache (in-memory with LRU)             │   │
│  │  • Kerchunk Reference Cache                          │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │  Rendering   │  │  Analytics   │  │   Mosaic     │      │
│  │  SkiaSharp   │  │  Statistics  │  │  Multi-raster│      │
│  │  Multi-band  │  │  Algebra     │  │  Blending    │      │
│  │  Styling     │  │  Terrain     │  │  Resampling  │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
│                                                               │
│  ┌──────────────────────────────────────────────────────┐   │
│  │         Data Sources (Provider Pattern)               │   │
│  │  • FileSystem  • HTTP  • S3  • GCS  • Azure Blob     │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

### Key Services

#### 1. **COG Processing**
- **`ICogReader`**: Pure .NET COG reader using LibTiff
  - HTTP range requests for remote COGs
  - Efficient tile-based access
  - GeoTIFF tag parsing
- **`GdalCogCacheService`**: GDAL-based COG converter
  - Converts NetCDF/HDF5/GRIB2 → COG
  - Intelligent caching with staleness detection
  - Thread-safe with collision detection

#### 2. **Zarr Time Series**
- **`IZarrReader`**: HTTP-based Zarr array reader
  - Supports sharded chunks (Zarr v3)
  - Multi-format decompression (Blosc, Zstd, LZ4, Gzip)
  - Efficient time-slice queries
- **`IZarrTimeSeriesService`**: Time-series data management
  - NetCDF/HDF5 → Zarr conversion
  - Time-range and single-slice queries
  - Chunked access for large datasets

#### 3. **Tile Generation**
- **`IRasterRenderer`**: Raster rendering engine
  - SkiaSharp-based rendering
  - Multi-band composition
  - Style-based visualization (color ramps, discrete values)
  - Vector overlay support
  - Format support: PNG, JPEG

#### 4. **Caching System**
- **`IRasterTileCacheProvider`**: Multi-provider tile caching
  - Filesystem (local disk)
  - Cloud storage (S3, GCS, Azure Blob)
  - Redis (metadata store)
  - Disk quota management
  - Cache statistics and metrics
- **`IRasterCacheService`**: COG/Zarr cache management
  - Lazy conversion (on-demand)
  - TTL-based invalidation
  - Source staleness detection

#### 5. **Analytics**
- **`IRasterAnalyticsService`**: Advanced raster analytics
  - Statistics (min/max/mean/stddev/median)
  - Raster algebra (band math, expressions)
  - Value extraction at points
  - Histogram generation
  - Zonal statistics (polygons)
  - Terrain analysis (hillshade, slope, aspect, curvature, roughness)

#### 6. **Mosaics**
- **`IRasterMosaicService`**: Multi-raster mosaicking
  - Blend modes: First, Last, Min, Max, Mean, Median, Blend
  - Resampling: NearestNeighbor, Bilinear, Cubic, Lanczos
  - Automatic reprojection and alignment

---

## Supported Formats

### Input Formats (via GDAL)
- **GeoTIFF** (.tif, .tiff) - including COG
- **NetCDF** (.nc, .nc4, .netcdf) - CF-compliant
- **HDF5** (.h5, .hdf, .hdf5)
- **GRIB/GRIB2** (.grib, .grib2, .grb, .grb2)
- **Zarr** (v2 and v3) - via HTTP, S3, GCS, filesystem
- **PNG/JPEG** (for styled output)

### Output Formats
- **COG** (Cloud Optimized GeoTIFF)
- **Zarr** (chunked time-series arrays)
- **PNG** (24-bit, 32-bit with alpha)
- **JPEG** (lossy compression)
- **GeoParquet** (raster data export)
- **GeoArrow** (in-memory columnar)

---

## Features

### 1. Multi-Band Raster Support
- Up to N bands per raster
- Band selection and composition
- Per-band statistics
- Band arithmetic operations

### 2. Time Series Data (Zarr)
- Multi-dimensional arrays: `[time, lat, lon]`
- Chunked storage for efficient access
- Multiple compression codecs:
  - **Blosc** (multi-threaded, high-performance)
  - **Zstd** (high compression ratio)
  - **LZ4** (fast compression/decompression)
  - **Gzip** (universal compatibility)
- Kerchunk references for virtual datasets
- Time-range queries with spatial filtering

### 3. Intelligent Tile Caching
- **Multi-tier architecture**:
  - L1: In-memory (hot tiles)
  - L2: Local disk (warm tiles)
  - L3: Cloud storage (cold tiles)
- **Cache key generation** with collision detection
- **Disk quota management** (LRU eviction)
- **Staleness detection** (file modification times, TTL)
- **Cache statistics** (hit rate, size, access patterns)

### 4. Reprojection
- On-the-fly coordinate system transformation
- Support for 4000+ EPSG codes
- Efficient resampling algorithms

### 5. High-Performance Rendering (SkiaSharp)
- Hardware-accelerated graphics
- Color ramp interpolation
- Discrete value mapping
- Alpha blending and transparency
- Anti-aliasing
- Vector overlay (lines, polygons, labels)

### 6. Advanced Analytics
- **Statistics**: Per-band min/max/mean/stddev/median
- **Raster Algebra**: Band math with expressions (e.g., NDVI, EVI)
- **Value Extraction**: Sample values at point locations
- **Histograms**: Distribution analysis with configurable bins
- **Zonal Statistics**: Aggregate values within polygons
- **Terrain Analysis**:
  - Hillshade (relief shading)
  - Slope (gradient)
  - Aspect (orientation)
  - Curvature
  - Roughness

### 7. Data Export
- **GeoParquet**: Columnar format with spatial indexing
- **GeoArrow**: In-memory columnar arrays
- Stream-based export for large datasets

---

## Usage Examples

### 1. Reading COG Files

```csharp
using Honua.Server.Core.Raster.Readers;

// Open a local COG file
var cogReader = serviceProvider.GetRequiredService<ICogReader>();
var dataset = await cogReader.OpenAsync("/path/to/file.tif");

Console.WriteLine($"Dimensions: {dataset.Metadata.Width}x{dataset.Metadata.Height}");
Console.WriteLine($"Bands: {dataset.Metadata.BandCount}");
Console.WriteLine($"Compression: {dataset.Metadata.Compression}");
Console.WriteLine($"Is COG: {dataset.Metadata.IsCog}");

// Read a specific tile
var tileData = await cogReader.ReadTileAsync(dataset, tileX: 0, tileY: 0, level: 0);

// Read a window/region
var windowData = await cogReader.ReadWindowAsync(dataset, x: 100, y: 100, width: 256, height: 256);

dataset.Dispose();
```

### 2. Reading Remote COG via HTTP Range Requests

```csharp
// Open a remote COG (HTTP range requests)
var httpClient = new HttpClient();
var cogReader = new LibTiffCogReader(logger, httpClient);
var dataset = await cogReader.OpenAsync("https://example.com/data/elevation.tif");

// Access is efficient - only downloads needed chunks
var metadata = await cogReader.GetMetadataAsync("https://example.com/data/elevation.tif");
Console.WriteLine($"Remote COG: {metadata.Width}x{metadata.Height}, {metadata.BandCount} bands");
```

### 3. Converting NetCDF to COG

```csharp
using Honua.Server.Core.Raster.Cache;

var cogCacheService = serviceProvider.GetRequiredService<IRasterCacheService>();

// Convert NetCDF variable to COG
var cogUri = await cogCacheService.ConvertToCogAsync(
    sourceUri: "/data/temperature.nc",
    options: new CogConversionOptions
    {
        VariableName = "temperature",
        TimeIndex = 0,  // First time step
        Compression = "DEFLATE",
        BlockSize = 512,
        OverviewResampling = "BILINEAR",
        TargetCrs = "EPSG:3857"  // Web Mercator
    });

Console.WriteLine($"COG created at: {cogUri}");
```

### 4. Working with Zarr Time Series

```csharp
using Honua.Server.Core.Raster.Cache;

var zarrService = serviceProvider.GetRequiredService<IZarrTimeSeriesService>();

// Convert multi-temporal NetCDF to Zarr
await zarrService.ConvertToZarrAsync(
    sourceUri: "/data/climate_timeseries.nc",
    zarrUri: "/cache/zarr/climate_data",
    options: new ZarrConversionOptions
    {
        VariableName = "precipitation",
        TimeChunkSize = 1,
        LatitudeChunkSize = 128,
        LongitudeChunkSize = 128,
        Compression = "zstd",
        CompressionLevel = 5
    });

// Query a single time slice
var timeSlice = await zarrService.QueryTimeSliceAsync(
    zarrPath: "/cache/zarr/climate_data",
    variableName: "precipitation",
    timestamp: new DateTimeOffset(2023, 6, 15, 0, 0, 0, TimeSpan.Zero),
    spatialExtent: new BoundingBox(-180, -90, 180, 90));

Console.WriteLine($"Time slice: {timeSlice.Data.Length} bytes");

// Query a time range
var timeSeries = await zarrService.QueryTimeRangeAsync(
    zarrPath: "/cache/zarr/climate_data",
    variableName: "precipitation",
    startTime: new DateTimeOffset(2023, 6, 1, 0, 0, 0, TimeSpan.Zero),
    endTime: new DateTimeOffset(2023, 6, 30, 0, 0, 0, TimeSpan.Zero));

Console.WriteLine($"Time series: {timeSeries.Timestamps.Count} timesteps");
```

### 5. Generating Raster Tiles

```csharp
using Honua.Server.Core.Raster.Rendering;

var renderer = serviceProvider.GetRequiredService<IRasterRenderer>();
var datasetRegistry = serviceProvider.GetRequiredService<IRasterDatasetRegistry>();

var dataset = await datasetRegistry.FindAsync("elevation-dem");

var request = new RasterRenderRequest(
    Dataset: dataset,
    BoundingBox: new[] { -120.0, 35.0, -119.0, 36.0 },
    Width: 256,
    Height: 256,
    SourceCrs: "EPSG:4326",
    TargetCrs: "EPSG:3857",
    Format: "png",
    Transparent: true,
    StyleId: "elevation-ramp");

var result = await renderer.RenderAsync(request);

// result.Data contains PNG bytes
await File.WriteAllBytesAsync("tile.png", result.Data);
```

### 6. Band Composition (RGB from Multispectral)

```csharp
// Render RGB composite from Landsat bands (B4=Red, B3=Green, B2=Blue)
var multispectralDataset = await datasetRegistry.FindAsync("landsat-8-scene");

var rgbRequest = new RasterRenderRequest(
    Dataset: multispectralDataset,
    BoundingBox: new[] { -122.0, 37.0, -121.0, 38.0 },
    Width: 512,
    Height: 512,
    SourceCrs: "EPSG:32610",  // UTM Zone 10N
    TargetCrs: "EPSG:3857",
    Format: "png",
    Transparent: false,
    StyleId: "rgb-composite",  // Style defines band mapping: R=4, G=3, B=2
    Style: new StyleDefinition
    {
        // Custom RGB mapping can be defined inline
        BandMapping = new Dictionary<string, int>
        {
            ["red"] = 4,
            ["green"] = 3,
            ["blue"] = 2
        }
    });

var rgbResult = await renderer.RenderAsync(rgbRequest);
```

### 7. Raster Analytics - Statistics

```csharp
using Honua.Server.Core.Raster.Analytics;

var analyticsService = serviceProvider.GetRequiredService<IRasterAnalyticsService>();

var statsRequest = new RasterStatisticsRequest(
    Dataset: dataset,
    BoundingBox: new[] { -120.0, 35.0, -119.0, 36.0 },
    BandIndex: null  // All bands
);

var stats = await analyticsService.CalculateStatisticsAsync(statsRequest);

foreach (var band in stats.Bands)
{
    Console.WriteLine($"Band {band.BandIndex}:");
    Console.WriteLine($"  Min: {band.Min}");
    Console.WriteLine($"  Max: {band.Max}");
    Console.WriteLine($"  Mean: {band.Mean}");
    Console.WriteLine($"  StdDev: {band.StdDev}");
    Console.WriteLine($"  Median: {band.Median}");
}
```

### 8. Raster Algebra (NDVI Calculation)

```csharp
// Calculate NDVI: (NIR - Red) / (NIR + Red)
var algebraRequest = new RasterAlgebraRequest(
    Datasets: new[] { nirDataset, redDataset },
    Expression: "(A - B) / (A + B)",  // A=NIR, B=Red
    BoundingBox: new[] { -120.0, 35.0, -119.0, 36.0 },
    Width: 512,
    Height: 512,
    Format: "png"
);

var ndviResult = await analyticsService.CalculateAlgebraAsync(algebraRequest);

Console.WriteLine($"NDVI statistics: {ndviResult.Statistics.Bands[0].Mean:F3}");
await File.WriteAllBytesAsync("ndvi.png", ndviResult.Data);
```

### 9. Terrain Analysis (Hillshade)

```csharp
var terrainRequest = new TerrainAnalysisRequest(
    ElevationDataset: demDataset,
    AnalysisType: TerrainAnalysisType.Hillshade,
    BoundingBox: new[] { -120.0, 35.0, -119.0, 36.0 },
    Width: 512,
    Height: 512,
    Format: "png",
    ZFactor: 1.0,
    Azimuth: 315.0,  // Light from northwest
    Altitude: 45.0   // 45 degrees above horizon
);

var hillshadeResult = await analyticsService.CalculateTerrainAsync(terrainRequest);
await File.WriteAllBytesAsync("hillshade.png", hillshadeResult.Data);
```

### 10. Zonal Statistics

```csharp
// Calculate mean elevation within polygon zones
var zonalRequest = new ZonalStatisticsRequest(
    Dataset: demDataset,
    Zones: new[]
    {
        new Polygon(
            Coordinates: new[]
            {
                new Point(-120.0, 35.0),
                new Point(-119.5, 35.0),
                new Point(-119.5, 35.5),
                new Point(-120.0, 35.5),
                new Point(-120.0, 35.0)
            },
            ZoneId: "zone-1"
        )
    },
    BandIndex: 0,
    Statistics: new[] { "mean", "min", "max", "stddev" }
);

var zonalResult = await analyticsService.CalculateZonalStatisticsAsync(zonalRequest);

foreach (var zone in zonalResult.Zones)
{
    Console.WriteLine($"Zone {zone.ZoneId}: Mean={zone.Mean}, Min={zone.Min}, Max={zone.Max}");
}
```

### 11. Creating Raster Mosaics

```csharp
using Honua.Server.Core.Raster.Mosaic;

var mosaicService = serviceProvider.GetRequiredService<IRasterMosaicService>();

var mosaicRequest = new RasterMosaicRequest(
    Datasets: new[] { tile1Dataset, tile2Dataset, tile3Dataset },
    BoundingBox: new[] { -121.0, 36.0, -119.0, 38.0 },
    Width: 1024,
    Height: 1024,
    SourceCrs: "EPSG:4326",
    TargetCrs: "EPSG:3857",
    Format: "png",
    Transparent: true,
    Method: RasterMosaicMethod.Blend,  // Smooth blending
    Resampling: RasterResamplingMethod.Bilinear
);

var mosaicResult = await mosaicService.CreateMosaicAsync(mosaicRequest);
Console.WriteLine($"Mosaic created from {mosaicResult.Metadata.DatasetCount} datasets");
```

---

## Configuration

### GDAL Configuration

```csharp
using Honua.Server.Core.Raster.Interop;

// Configure GDAL for cloud-optimized operations
GdalConfiguration.ConfigureForCloudOptimizedOperations();

// This sets:
// - HTTP/2 multiplexing for range requests
// - Optimal block sizes for COG
// - NetCDF/HDF5 optimizations
// - Multi-threading (based on CPU count)
```

### Cache Configuration

```csharp
// COG cache settings
services.AddSingleton<IRasterCacheService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<GdalCogCacheService>>();
    var storage = sp.GetRequiredService<ICogCacheStorage>();

    return new GdalCogCacheService(
        logger,
        stagingDirectory: "/tmp/cog-staging",
        storage: storage,
        cacheTtl: TimeSpan.FromHours(24)  // Cache for 24 hours
    );
});

// Tile cache settings
services.AddSingleton<IRasterTileCacheProvider>(sp =>
{
    // Filesystem cache
    return new FileSystemRasterTileCacheProvider(
        basePath: "/var/cache/tiles",
        maxSizeBytes: 10_000_000_000  // 10 GB
    );

    // Or S3 cache
    // return new S3RasterTileCacheProvider(s3Client, "my-tile-bucket");

    // Or GCS cache
    // return new GcsRasterTileCacheProvider(storageClient, "my-tile-bucket");

    // Or Azure Blob cache
    // return new AzureBlobRasterTileCacheProvider(blobServiceClient, "tiles");
});
```

### Memory Limits

```csharp
services.Configure<RasterMemoryLimits>(options =>
{
    options.MaxSliceSizeBytes = 100_000_000;      // 100 MB per Zarr slice
    options.MaxChunksPerRequest = 100;            // Max chunks to load
    options.MaxSimultaneousDatasets = 5;          // Max datasets in algebra
    options.MaxHistogramBins = 1000;              // Max histogram bins
    options.MaxZonalPolygonVertices = 10_000;     // Max polygon complexity
    options.MaxZonalPolygons = 1000;              // Max zones per request
    options.MaxExtractionPoints = 10_000;         // Max points for extraction
    options.MaxRasterDimension = 8192;            // Max width/height
    options.MaxRasterPixels = 16_777_216;         // Max total pixels (4096x4096)
});
```

### Compression Codecs

```csharp
using Honua.Server.Core.Raster.Compression;

// Register compression codecs for Zarr
var codecRegistry = new CompressionCodecRegistry();
codecRegistry.Register("blosc", new BloscDecompressor());  // Multi-threaded
codecRegistry.Register("zstd", new ZstdDecompressor());    // High ratio
codecRegistry.Register("lz4", new Lz4Decompressor());      // Fast
codecRegistry.Register("gzip", new GzipDecompressor());    // Universal
```

---

## Performance Optimization

### 1. COG Best Practices
- **Always use COG format** for cloud storage (S3, GCS, Azure)
- **Tile size**: 512x512 for optimal HTTP range requests
- **Compression**: DEFLATE for lossless, JPEG for lossy (aerial imagery)
- **Overviews**: Generate pyramids for fast zooming
- **Block alignment**: Ensure tiles align with data access patterns

### 2. Zarr Best Practices
- **Chunk size**: Balance between read performance and overhead
  - Time: 1 (single timestep per chunk)
  - Spatial: 128x128 or 256x256
- **Compression**: Zstd (level 5) for good ratio and speed
- **Sharding**: Use Zarr v3 sharding for datasets with many chunks
- **Metadata consolidation**: Always consolidate `.zmetadata` for fast discovery

### 3. Caching Strategy
- **L1 (Memory)**: Hot tiles, recently accessed (LRU)
- **L2 (Disk)**: Warm tiles, disk quota with eviction
- **L3 (Cloud)**: Cold tiles, infinite capacity
- **TTL**: Set appropriate cache lifetimes based on data update frequency
- **Prewarming**: Pre-generate tiles for common zoom levels

### 4. Rendering Optimization
- **Resampling**:
  - NearestNeighbor: Fastest, use for discrete data (land cover)
  - Bilinear: Balanced, use for continuous data (elevation)
  - Cubic/Lanczos: Highest quality, slower
- **Tile size**: 256x256 for web maps, 512x512 for high-DPI
- **Format**: PNG for transparency, JPEG for smaller size

### 5. Memory Management
- **ArrayPool**: Used for buffers >85KB to avoid LOH
- **Streaming**: Large datasets processed in chunks
- **Disposal**: Always dispose datasets, readers, and streams
- **Limits**: Configure `RasterMemoryLimits` based on available RAM

### 6. Parallel Processing
- **GDAL threads**: Configured automatically based on CPU count
- **Concurrent conversions**: Semaphore limits concurrent COG conversions
- **Tile generation**: Parallel tile generation for batch operations

---

## API Integration

### OGC Web Map Service (WMS)

The Raster module powers WMS `GetMap` requests:

1. **Request**: Client requests map tile for specific bbox/size/layers
2. **Dataset Lookup**: Registry finds raster dataset(s)
3. **COG Conversion**: Source formats converted to COG (cached)
4. **Tile Check**: Check if tile exists in cache
5. **Rendering**: Generate tile using renderer (if cache miss)
6. **Response**: Return PNG/JPEG to client

```csharp
// WMS GetMap flow
var dataset = await registry.FindAsync(layerId);
var cogUri = await cogCache.GetOrConvertToCogAsync(dataset);
var cachedTile = await tileCache.TryGetAsync(cacheKey);

if (cachedTile == null)
{
    var renderResult = await renderer.RenderAsync(request);
    await tileCache.StoreAsync(cacheKey, renderResult.Data);
    return renderResult.Data;
}

return cachedTile.Data;
```

### OGC Web Map Tile Service (WMTS)

The Raster module serves pre-generated tile pyramids:

1. **Request**: Client requests tile by `{z}/{x}/{y}`
2. **Cache Lookup**: Check tile cache (L1→L2→L3)
3. **On-demand Generation**: Generate tile if not cached
4. **Response**: Return cached or generated tile

### OGC API - Tiles

RESTful tile access:
- `/collections/{collectionId}/tiles/{tileMatrixSetId}/{z}/{x}/{y}` → Raster tile

### Zarr-Based Time Series API

Custom API for time-series queries:
- `GET /datasets/{id}/timeseries?var={variable}&start={time}&end={time}&bbox={bbox}`
- Returns Zarr slice or aggregated data

---

## Data Sources (Provider Pattern)

The module supports multiple data source backends:

| Provider | Schemes | Use Case |
|----------|---------|----------|
| **FileSystem** | `file://`, local paths | Local raster files |
| **HTTP** | `http://`, `https://` | Remote COGs, Zarr stores |
| **S3** | `s3://` | AWS S3 buckets |
| **GCS** | `gs://` | Google Cloud Storage |
| **Azure Blob** | `az://`, `wasb://` | Azure Blob Storage |
| **GDAL** | `/vsi*` paths | GDAL virtual file systems |

### Example: S3 Data Source

```csharp
// Access raster directly from S3
var dataset = await registry.FindAsync("s3-landsat-scene");
// Dataset.Source.Uri = "s3://my-bucket/landsat/LC08_L1TP_044034_20230615.tif"

var cogUri = await cogCache.GetOrConvertToCogAsync(dataset);
// COG is cached locally for fast access
```

---

## Dependencies

From `Honua.Server.Core.Raster.csproj`:

- **MaxRev.Gdal.Core** (3.11.3) - GDAL bindings for .NET
- **BitMiracle.LibTiff.NET** (2.4.660) - Pure .NET TIFF reader
- **ParquetSharp** (21.0.0) - Parquet file format
- **Apache.Arrow** (22.1.0) - Columnar data format
- **SkiaSharp** (via Core.Rendering) - Graphics rendering
- **LibGit2Sharp** (0.30.0) - Git operations for versioned data

---

## Thread Safety

All services are **thread-safe**:
- **CogCacheService**: Uses `SemaphoreSlim` for conversion concurrency
- **TileCacheProviders**: Concurrent access with atomic counters
- **ZarrReader**: HTTP client pooling, no shared state
- **Renderer**: Stateless, can be used concurrently

---

## Error Handling

The module uses structured exceptions:

- **`FileNotFoundException`**: Raster file not found
- **`InvalidOperationException`**: GDAL/format errors
- **`NotSupportedException`**: Unsupported format/operation
- **`ArgumentException`**: Invalid parameters
- **`OutOfMemoryException`**: Exceeded memory limits (RasterMemoryLimits)

---

## Testing

Run unit tests:

```bash
dotnet test tests/Honua.Server.Core.Raster.Tests
```

Integration tests (requires GDAL):

```bash
dotnet test tests/Honua.Server.Core.Raster.IntegrationTests
```

---

## Performance Metrics

Typical performance on modern hardware:

| Operation | Throughput | Latency (p95) |
|-----------|-----------|---------------|
| COG tile read (256x256) | 500-1000 tiles/sec | 2-5 ms |
| Zarr chunk read (128x128x1) | 200-500 chunks/sec | 5-10 ms |
| Tile rendering (256x256, PNG) | 100-300 tiles/sec | 10-30 ms |
| NetCDF→COG conversion | 10-50 MB/sec | Variable |
| Zonal statistics (1K polygons) | 50-200 polygons/sec | 20-50 ms |
| Terrain analysis (512x512) | 20-100 tiles/sec | 50-100 ms |

*Performance varies based on data complexity, compression, and hardware.*

---

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0

---

## Additional Resources

- **OGC Standards**: https://www.ogc.org/standards/wms, https://www.ogc.org/standards/wmts
- **Cloud Optimized GeoTIFF**: https://www.cogeo.org/
- **Zarr Format**: https://zarr.readthedocs.io/
- **GDAL**: https://gdal.org/
- **NetCDF**: https://www.unidata.ucar.edu/software/netcdf/
- **SkiaSharp**: https://github.com/mono/SkiaSharp

---

**For questions or issues, please contact the Honua development team.**
