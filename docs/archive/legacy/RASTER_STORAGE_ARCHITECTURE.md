# Raster Storage Architecture: Analysis & Recommendations

## The Core Question

**Should we:**
1. Convert everything to GeoTIFF/COG on-the-fly (current approach)?
2. Pre-convert and cache to GeoTIFF/COG?
3. Use a specialized raster database (PostGIS Raster, Rasdaman)?
4. Use a time-series database (TimescaleDB, InfluxDB)?
5. Use a cloud-native data store (Zarr, TileDB)?

## TL;DR Recommendations

**For your use case (GIS server with analytics), use a hybrid approach:**

1. **Keep source formats in S3/Azure Blob** (NetCDF, HDF5, GRIB2)
2. **Convert to COG on first access, cache result** (lazy conversion)
3. **Use object storage for COG tiles** (S3/Azure/GCS)
4. **Add Zarr for time-series analysis** (climate/weather use cases)

**Do NOT use:**
- ❌ Time-series databases (not designed for spatial data)
- ❌ In-memory only (loses on restart)
- ❌ Traditional relational DB (too slow for large rasters)

---

## Option Analysis

### Option 1: On-the-Fly Conversion (Current Design)

```
Request → GDAL → In-Memory GeoTIFF → Analytics → Response
```

**Pros:**
- ✅ No storage overhead
- ✅ Always uses latest source data
- ✅ Simple architecture
- ✅ Works with any format

**Cons:**
- ❌ Slow (conversion on every request)
- ❌ CPU intensive
- ❌ High latency (seconds to minutes)
- ❌ Doesn't scale

**Verdict**: ❌ **Only viable for prototyping**

---

### Option 2: Pre-Convert & Cache to COG

```
Ingestion:  NetCDF → GDAL → COG → S3/Azure Blob
Request:    COG (cached) → Analytics → Response
```

**Architecture:**
```yaml
raster_datasets:
  - id: "modis-ndvi"
    source:
      type: "hdf5"
      uri: "s3://nasa-modis/MOD13Q1.A2025001.h09v05.061.hdf"
      subdataset: "250m_16_days_NDVI"

    # Converted COG cache
    cache:
      enabled: true
      type: "cog"
      uri: "s3://honua-cache/rasters/modis-ndvi-20250114.tif"
      ttl: 86400  # 24 hours
```

**Conversion Process:**
```csharp
public async Task<string> ConvertToCogAsync(string sourceUri, string outputUri)
{
    // GDAL translate with COG driver
    var options = new[]
    {
        "-of", "COG",
        "-co", "COMPRESS=DEFLATE",
        "-co", "BLOCKSIZE=512",
        "-co", "OVERVIEW_RESAMPLING=BILINEAR",
        "-co", "NUM_THREADS=ALL_CPUS"
    };

    Gdal.Translate(outputUri, sourceUri, new GDALTranslateOptions(options));

    return outputUri;
}
```

**Pros:**
- ✅ Fast access (milliseconds vs seconds)
- ✅ Scales horizontally (CDN-friendly)
- ✅ Efficient storage (COG is compressed)
- ✅ Cloud-optimized (HTTP range requests)
- ✅ Standard format (interoperable)

**Cons:**
- ❌ Storage overhead (2x for source + COG)
- ❌ Stale data risk (cache invalidation)
- ❌ Conversion time on first access

**Verdict**: ✅ **RECOMMENDED for most use cases**

---

### Option 3: PostGIS Raster Database

```
Ingestion:  NetCDF → GDAL → PostGIS Raster → PostgreSQL
Request:    SQL Query → Raster → Analytics → Response
```

**Example:**
```sql
-- Import raster
raster2pgsql -I -C -s 4326 temperature.tif public.temperature | psql

-- Query statistics
SELECT
    (ST_SummaryStats(rast)).mean AS avg_temp,
    (ST_SummaryStats(rast)).stddev AS stddev_temp
FROM temperature
WHERE ST_Intersects(rast, ST_MakeEnvelope(-120, 35, -119, 36, 4326));

-- Raster algebra
SELECT
    ST_MapAlgebra(
        nir.rast, red.rast,
        '([rast1] - [rast2]) / ([rast1] + [rast2])::float'
    ) AS ndvi
FROM nir_band, red_band;
```

**Pros:**
- ✅ SQL analytics (familiar)
- ✅ Transactional integrity
- ✅ Spatial indexing
- ✅ Integrated with vector data

**Cons:**
- ❌ **Slow for large rasters** (designed for small tiles)
- ❌ Complex tiling/pyramids
- ❌ Limited to PostgreSQL ecosystem
- ❌ Not cloud-native (hard to scale)
- ❌ Storage overhead

**Verdict**: ⚠️ **Only for small rasters (<100MB) tightly integrated with vector data**

---

### Option 4: Time-Series Database

```
Ingestion:  NetCDF → Extract time series → TimescaleDB/InfluxDB
Request:    Time range query → Interpolate → Response
```

**Example (TimescaleDB):**
```sql
CREATE TABLE weather_observations (
    time TIMESTAMPTZ NOT NULL,
    location GEOMETRY(Point, 4326),
    temperature FLOAT,
    pressure FLOAT,
    humidity FLOAT
);

SELECT create_hypertable('weather_observations', 'time');

-- Query time series
SELECT time_bucket('1 hour', time) AS hour,
       avg(temperature) as avg_temp
FROM weather_observations
WHERE location && ST_MakeEnvelope(-120, 35, -119, 36, 4326)
  AND time > NOW() - INTERVAL '7 days'
GROUP BY hour;
```

**Pros:**
- ✅ Excellent for point data over time
- ✅ Fast time-range queries
- ✅ Downsampling/aggregation
- ✅ Retention policies

**Cons:**
- ❌ **Not designed for 2D/3D spatial data**
- ❌ Loses spatial continuity (point-based)
- ❌ Can't do raster algebra
- ❌ No spatial indexing for grids
- ❌ Massive storage (one row per pixel per time)

**Verdict**: ❌ **Wrong tool for raster data** (use for extracted point time-series only)

---

### Option 5: Cloud-Native Array Stores (Zarr, TileDB)

```
Ingestion:  NetCDF → Zarr (chunked, compressed)
Request:    Chunk query → Analytics → Response
```

**Zarr Example:**
```python
import zarr
import numpy as np

# Create Zarr array from NetCDF
store = zarr.DirectoryStore('s3://bucket/temperature.zarr')
root = zarr.group(store=store)

# 4D array: [time, level, lat, lon]
temp = root.create_dataset(
    'temperature',
    shape=(365, 10, 721, 1440),
    chunks=(1, 1, 128, 128),
    dtype='f4',
    compressor=zarr.Blosc(cname='zstd')
)

# Query specific time/location
data = temp[100:110, 5, 200:400, 600:800]  # 10 days, level 5, region
```

**C# Integration (via HTTP API):**
```csharp
// Access Zarr array via HTTP range requests
var zarrStore = new HttpZarrStore("s3://bucket/temperature.zarr");
var array = zarrStore.OpenArray("temperature");

// Read chunk
var data = await array.ReadAsync(
    new[] { 100, 5, 200, 600 },  // start
    new[] { 10, 1, 200, 200 }     // count
);
```

**TileDB Example:**
```python
import tiledb
import numpy as np

# Create TileDB array
ctx = tiledb.Ctx({"vfs.s3.region": "us-east-1"})
dom = tiledb.Domain(
    tiledb.Dim(name="time", domain=(0, 365), tile=1, dtype=np.int32),
    tiledb.Dim(name="lat", domain=(0, 720), tile=128, dtype=np.int32),
    tiledb.Dim(name="lon", domain=(0, 1440), tile=128, dtype=np.int32)
)

schema = tiledb.ArraySchema(
    domain=dom,
    sparse=False,
    attrs=[tiledb.Attr(name="temperature", dtype=np.float32)]
)

tiledb.DenseArray.create("s3://bucket/temperature", schema)

# Query
with tiledb.open("s3://bucket/temperature") as A:
    data = A[100:110, 200:400, 600:800]
```

**Pros:**
- ✅ **Cloud-native** (designed for S3/Azure/GCS)
- ✅ **Chunked** (efficient partial reads)
- ✅ **Compressed** (10-50x smaller than raw)
- ✅ **Multi-dimensional** (time, level, lat, lon)
- ✅ **Parallel I/O** (read chunks concurrently)
- ✅ **Metadata-rich** (self-describing)
- ✅ **Growing ecosystem** (Xarray, Dask integration)

**Cons:**
- ❌ Limited .NET support (mostly Python)
- ❌ Requires conversion from source formats
- ❌ Not GIS-standard (GDAL support limited)
- ❌ Learning curve

**Verdict**: ✅ **RECOMMENDED for time-series climate/weather data**

---

## Hybrid Architecture (RECOMMENDED)

```
┌─────────────────────────────────────────────────────────────┐
│                    Source Data (Read-Only)                   │
│  S3/Azure: NetCDF, HDF5, GRIB2 (original archives)          │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│              Ingestion Pipeline (One-time)                   │
│  1. GDAL read source format                                  │
│  2. Convert to COG (compressed, tiled, overviews)            │
│  3. Upload to cache (S3/Azure/GCS)                           │
│  4. Update metadata registry                                 │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│                 COG Cache (Primary)                          │
│  S3/Azure/GCS: *.tif (Cloud Optimized GeoTIFF)              │
│  - Fast HTTP range requests                                  │
│  - CDN-friendly                                              │
│  - Interoperable                                             │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│               Zarr Cache (Time-Series)                       │
│  S3: *.zarr (for multi-temporal analysis)                   │
│  - Climate model outputs                                     │
│  - Weather forecast time-series                              │
│  - Satellite image stacks                                    │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│                   Analytics Layer                            │
│  Your existing RasterAnalyticsService                        │
│  - NDVI, EVI, terrain analysis                               │
│  - Zonal statistics                                          │
│  - Works with COG via HTTP range requests                    │
└─────────────────────────────────────────────────────────────┘
```

### Implementation Plan

**Phase 1: COG Cache (Week 1-2)**
```csharp
public interface IRasterCacheService
{
    /// <summary>
    /// Get COG from cache, or convert and cache if missing
    /// </summary>
    Task<string> GetOrConvertAsync(RasterDatasetDefinition dataset);

    /// <summary>
    /// Pre-convert to COG (ingestion pipeline)
    /// </summary>
    Task<string> ConvertToCogAsync(string sourceUri, string outputUri);

    /// <summary>
    /// Check if COG cache is stale (source updated)
    /// </summary>
    Task<bool> IsStaleAsync(string cachedUri, string sourceUri);
}
```

**Phase 2: GDAL Integration (Week 2-3)**
```csharp
public sealed class GdalRasterSourceProvider : IRasterSourceProvider
{
    private readonly IRasterCacheService _cache;

    public async Task<Stream> OpenReadRangeAsync(string uri, long offset, long? length,
        CancellationToken ct)
    {
        // Check cache first
        var cogUri = await _cache.GetOrConvertAsync(dataset);

        // Open COG with GDAL (or direct HTTP range request)
        var dataset = Gdal.Open(cogUri, Access.GA_ReadOnly);

        // Read requested region
        return ReadRegion(dataset, offset, length);
    }
}
```

**Phase 3: Zarr for Time-Series (Week 4-5)**
```csharp
public sealed class ZarrTimeSeriesService
{
    /// <summary>
    /// Convert NetCDF/HDF5 to Zarr for time-series analysis
    /// </summary>
    Task ConvertToZarrAsync(string sourceUri, string zarrUri);

    /// <summary>
    /// Query time range from Zarr
    /// </summary>
    Task<float[,]> QueryTimeRangeAsync(string zarrUri,
        DateTime startTime, DateTime endTime,
        double[] bbox);
}
```

---

## Storage Comparison

| Format | Use Case | Size (100MB GeoTIFF) | Access Speed | Cloud-Optimized |
|--------|----------|----------------------|--------------|-----------------|
| **GeoTIFF** | Single raster | 100 MB | Fast | ❌ |
| **COG** | Single raster | 80 MB (compressed) | **Very Fast** | ✅ |
| **NetCDF** | Time-series | 50 MB (compressed) | Medium | ❌ |
| **Zarr** | Time-series | 40 MB (chunked) | **Very Fast** | ✅ |
| **PostGIS Raster** | Small tiles | 150 MB (overhead) | Slow | ❌ |
| **TileDB** | Arrays | 45 MB (chunked) | **Very Fast** | ✅ |

---

## Cost Analysis (1 TB of Raster Data)

### Option 1: Keep Source Only (No Cache)
- **Storage**: $23/month (S3 Standard)
- **Egress**: $90/TB transferred
- **Compute**: High (conversion on every request)
- **Latency**: 5-30 seconds per request
- **Total Monthly**: $23 + high compute costs

### Option 2: COG Cache (Recommended)
- **Storage**: $46/month (source + COG)
- **Egress**: $5/TB (range requests only)
- **Compute**: Low (one-time conversion)
- **Latency**: 100-500ms per request
- **Total Monthly**: ~$51 (plus CDN if needed)

### Option 3: PostGIS Raster
- **Storage**: $200/month (RDS storage)
- **Database**: $400/month (db.r5.xlarge)
- **Backup**: $50/month
- **Latency**: 1-10 seconds per request
- **Total Monthly**: ~$650

### Option 4: Zarr (Time-Series Workloads)
- **Storage**: $20/month (highly compressed)
- **Egress**: $10/TB (chunked reads)
- **Compute**: Medium (conversion)
- **Latency**: 200-800ms per request
- **Total Monthly**: ~$30

**Winner**: **COG Cache** for spatial, **Zarr** for time-series

---

## Final Recommendations

### For HonuaIO:

1. **Primary Strategy: COG Cache**
   - Convert NetCDF/HDF5/GRIB2 to COG on ingestion
   - Store in S3/Azure Blob
   - Use CloudFront/Azure CDN for distribution
   - Existing analytics work unchanged

2. **Add Zarr for Specific Use Cases:**
   - Climate model time-series
   - Weather forecast animations
   - Satellite image stacks
   - Multi-temporal analysis

3. **Keep Source Data:**
   - Archive originals in S3 Glacier Deep Archive ($1/TB/month)
   - Maintain data lineage
   - Allow re-processing if needed

4. **Do NOT Use:**
   - ❌ PostGIS Raster (too slow, wrong use case)
   - ❌ Time-series DB (not spatial)
   - ❌ On-the-fly only (doesn't scale)

### Implementation Priority

**Week 1-2**: COG caching with GDAL
**Week 3-4**: NetCDF/HDF5/GRIB2 ingestion
**Week 5-6**: Zarr integration (optional)

This gives you **95% of the value with 20% of the complexity**! 🎯
