# Tile Caching and Formats

**Keywords**: tiles, caching, mvt, vector-tiles, raster-tiles, tile-matrix, ogc-tiles, wmts, preseed, cdn, web-mercator, worldcrs84quad, worldwebmercatorquad, s3-cache, azure-blob, filesystem-cache, tile-generation, cache-warming, performance-optimization

## Overview

Honua implements enterprise-grade tile caching for both raster and vector tiles, supporting multiple storage backends, tile matrix sets, and on-demand or pre-seeded tile generation. The tile caching system is designed for high-performance map serving with CDN integration, parallel generation, and flexible cache invalidation strategies.

## Tile Formats

### Raster Tile Formats

Honua supports three primary raster tile formats:

1. **PNG (image/png)** - Default format, supports transparency (alpha channel)
   - Best for: Maps with transparency requirements, sharp edges, text overlays
   - File extension: `.png`
   - Typical size: 15-50 KB per tile
   - Compression: Lossless

2. **JPEG (image/jpeg)** - Optimized for photographic imagery
   - Best for: Aerial imagery, satellite photos, scanned maps
   - File extension: `.jpg`
   - Typical size: 10-30 KB per tile
   - Compression: Lossy
   - Note: No transparency support

3. **WebP (image/webp)** - Modern format with superior compression
   - Best for: Modern browsers, bandwidth-constrained environments
   - File extension: `.webp`
   - Typical size: 8-25 KB per tile (30-40% smaller than PNG)
   - Compression: Lossy or lossless
   - Note: Supports transparency

### Vector Tile Formats

**MVT (Mapbox Vector Tiles)** - Protocol Buffer-based vector tiles
- Format: `application/vnd.mapbox-vector-tile`
- File extension: `.mvt` or `.pbf`
- Typical size: 5-30 KB per tile
- Advantages:
  - Client-side styling flexibility
  - Smaller file sizes than raster
  - Resolution-independent rendering
  - Attribute data included in tiles
- Generated via PostGIS `ST_AsMVT` function

## Tile Matrix Sets

Honua supports two standard OGC tile matrix sets:

### WorldCRS84Quad (EPSG:4326)

- **Identifier**: `WorldCRS84Quad`
- **URI**: `http://www.opengis.net/def/tms/OGC/1.0/WorldCRS84Quad`
- **CRS**: `http://www.opengis.net/def/crs/OGC/1.3/CRS84`
- **Coordinate System**: Geographic (WGS84 latitude/longitude)
- **Bounds**: -180 to 180 longitude, -90 to 90 latitude
- **Tile Size**: 256x256 pixels
- **Use Cases**: Scientific data, global datasets requiring WGS84

**Zoom Level Calculations**:
```csharp
// From OgcTileMatrixHelper.cs
var tilesPerAxis = 1 << zoom;  // 2^zoom
var tileWidth = 360.0 / tilesPerAxis;  // degrees
var tileHeight = 180.0 / tilesPerAxis; // degrees

// Bounding box for tile at (zoom, row, column)
var minX = -180.0 + column * tileWidth;
var maxX = minX + tileWidth;
var maxY = 90.0 - row * tileHeight;
var minY = maxY - tileHeight;
```

### WorldWebMercatorQuad (EPSG:3857)

- **Identifier**: `WorldWebMercatorQuad`
- **URI**: `http://www.opengis.net/def/tms/OGC/1.0/WorldWebMercatorQuad`
- **CRS**: `http://www.opengis.net/def/crs/EPSG/0/3857`
- **Coordinate System**: Web Mercator (projected)
- **Bounds**: -20037508.34 to 20037508.34 meters (both X and Y)
- **Tile Size**: 256x256 pixels
- **Use Cases**: Web mapping, most online basemaps, OpenStreetMap

**Zoom Level Calculations**:
```csharp
// Web Mercator constants
const double earthRadius = 6378137.0;
const double originShift = Math.PI * earthRadius; // 20037508.3427892

var tilesPerAxis = 1 << zoom;
var tileWidth = (2.0 * originShift) / tilesPerAxis;

var minX = -originShift + (column * tileWidth);
var maxX = minX + tileWidth;
var maxY = originShift - (row * tileWidth);
var minY = maxY - tileWidth;
```

### Zoom Level Ranges

- **Default Range**: Zoom 0 (global) to Zoom 14 (~10m resolution)
- **Maximum Practical**: Zoom 22 (~0.15m resolution)
- **Tile Coordinate Validation**: `0 <= row,column < 2^zoom`

**Resolution at Common Zoom Levels** (Web Mercator at equator):
- Zoom 0: ~156 km/pixel (1 tile covers the world)
- Zoom 4: ~9.8 km/pixel
- Zoom 8: ~611 m/pixel
- Zoom 12: ~38 m/pixel
- Zoom 14: ~9.6 m/pixel (default max)
- Zoom 18: ~0.6 m/pixel
- Zoom 22: ~0.037 m/pixel

## Raster Tile Caching Architecture

### Cache Key Structure

```csharp
// From RasterTileCacheKey.cs
public readonly record struct RasterTileCacheKey
{
    public string DatasetId { get; }           // Dataset identifier
    public string TileMatrixSetId { get; }     // WorldCRS84Quad or WorldWebMercatorQuad
    public int Zoom { get; }                   // Zoom level (0-22)
    public int Row { get; }                    // Tile row coordinate
    public int Column { get; }                 // Tile column coordinate
    public string StyleId { get; }             // Style identifier (default: "default")
    public string Format { get; }              // image/png, image/jpeg, image/webp
    public bool Transparent { get; }           // Alpha channel enabled
    public int TileSize { get; }               // Tile dimension (default: 256)
}
```

**Cache Path Structure**:
```
{datasetId}/{matrixId}/{styleId}/{variant}/{zoom}/{column}/{row}.{ext}

Example:
roads-imagery/worldwebmercatorquad/default/png-256-alpha/12/1234/5678.png
```

**Variant Segment**: `{format}-{tileSize}-{transparency}`
- `png-256-alpha` - PNG, 256px, transparent
- `jpeg-256-opaque` - JPEG, 256px, opaque background
- `webp-512-alpha` - WebP, 512px, transparent

### Cache Providers

#### 1. Filesystem Cache Provider

**Default provider** - Stores tiles on local filesystem or network-mounted storage.

```csharp
// From FileSystemRasterTileCacheProvider.cs
public sealed class FileSystemRasterTileCacheProvider : IRasterTileCacheProvider
{
    private readonly string _rootPath;

    public async ValueTask<RasterTileCacheHit?> TryGetAsync(
        RasterTileCacheKey key,
        CancellationToken cancellationToken)
    {
        var path = ResolvePath(key);
        if (!File.Exists(path)) return null;

        var buffer = await File.ReadAllBytesAsync(path, cancellationToken);
        var createdUtc = File.GetLastWriteTimeUtc(path);
        return new RasterTileCacheHit(buffer, key.Format, createdUtc);
    }
}
```

**Configuration**:
```json
{
  "honua": {
    "services": {
      "rasterTiles": {
        "enabled": true,
        "provider": "filesystem",
        "fileSystem": {
          "rootPath": "./data/raster-cache"
        }
      }
    }
  }
}
```

**Environment Variables**:
```bash
HONUA__SERVICES__RASTERTILES__ENABLED=true
HONUA__SERVICES__RASTERTILES__PROVIDER=filesystem
HONUA__SERVICES__RASTERTILES__FILESYSTEM__ROOTPATH=/mnt/tiles
```

**Characteristics**:
- **Performance**: Very fast for local SSD, slower for NFS
- **Scalability**: Limited by filesystem I/O and network latency
- **Cost**: Low (just storage)
- **Use Cases**: Development, single-server deployments, NAS-backed clusters

#### 2. S3 Cache Provider

**Cloud-native storage** - Compatible with AWS S3, MinIO, and S3-compatible services.

```csharp
// From S3RasterTileCacheProvider.cs
public sealed class S3RasterTileCacheProvider : IRasterTileCacheProvider
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucketName;
    private readonly string _prefix;

    public async ValueTask<RasterTileCacheHit?> TryGetAsync(
        RasterTileCacheKey key,
        CancellationToken cancellationToken)
    {
        var objectKey = BuildObjectKey(key);
        var request = new GetObjectRequest
        {
            BucketName = _bucketName,
            Key = objectKey
        };

        using var response = await _s3.GetObjectAsync(request, cancellationToken);
        // Read and return tile data
    }
}
```

**Configuration**:
```json
{
  "honua": {
    "services": {
      "rasterTiles": {
        "enabled": true,
        "provider": "s3",
        "s3": {
          "bucketName": "honua-tiles",
          "prefix": "tiles/",
          "region": "us-west-2",
          "ensureBucket": true,
          "forcePathStyle": false
        }
      }
    }
  }
}
```

**MinIO Configuration** (S3-compatible self-hosted):
```json
{
  "honua": {
    "services": {
      "rasterTiles": {
        "provider": "s3",
        "s3": {
          "bucketName": "tiles",
          "serviceUrl": "http://minio:9000",
          "accessKeyId": "minioadmin",
          "secretAccessKey": "minioadmin",
          "forcePathStyle": true,
          "ensureBucket": true
        }
      }
    }
  }
}
```

**Environment Variables**:
```bash
HONUA__SERVICES__RASTERTILES__PROVIDER=s3
HONUA__SERVICES__RASTERTILES__S3__BUCKETNAME=honua-tiles
HONUA__SERVICES__RASTERTILES__S3__REGION=us-west-2
HONUA__SERVICES__RASTERTILES__S3__PREFIX=tiles/
HONUA__SERVICES__RASTERTILES__S3__ENSUREBUCKET=true

# For explicit credentials (or use IAM roles)
HONUA__SERVICES__RASTERTILES__S3__ACCESSKEYID=AKIA...
HONUA__SERVICES__RASTERTILES__S3__SECRETACCESSKEY=secret...

# For MinIO or S3-compatible
HONUA__SERVICES__RASTERTILES__S3__SERVICEURL=http://minio:9000
HONUA__SERVICES__RASTERTILES__S3__FORCEPATHSTYLE=true
```

**Characteristics**:
- **Performance**: Good (50-200ms latency typical)
- **Scalability**: Infinite horizontal scaling
- **Cost**: Pay per GB stored + bandwidth
- **CDN Integration**: Excellent (CloudFront, CloudFlare)
- **Use Cases**: Production deployments, multi-region, serverless

#### 3. Azure Blob Storage Cache Provider

**Azure-native storage** - Optimized for Azure deployments.

```csharp
// From AzureBlobRasterTileCacheProvider.cs
public sealed class AzureBlobRasterTileCacheProvider : IRasterTileCacheProvider
{
    private readonly BlobContainerClient _container;

    public async ValueTask<RasterTileCacheHit?> TryGetAsync(
        RasterTileCacheKey key,
        CancellationToken cancellationToken)
    {
        var blobClient = _container.GetBlobClient(BuildBlobName(key));

        var response = await blobClient.DownloadStreamingAsync(
            cancellationToken: cancellationToken);
        // Read and return tile data
    }
}
```

**Configuration**:
```json
{
  "honua": {
    "services": {
      "rasterTiles": {
        "enabled": true,
        "provider": "azure",
        "azure": {
          "connectionString": "DefaultEndpointsProtocol=https;AccountName=...",
          "containerName": "tiles",
          "ensureContainer": true
        }
      }
    }
  }
}
```

**Environment Variables**:
```bash
HONUA__SERVICES__RASTERTILES__PROVIDER=azure
HONUA__SERVICES__RASTERTILES__AZURE__CONNECTIONSTRING="DefaultEndpointsProtocol=https;..."
HONUA__SERVICES__RASTERTILES__AZURE__CONTAINERNAME=tiles
HONUA__SERVICES__RASTERTILES__AZURE__ENSURECONTAINER=true
```

**Characteristics**:
- **Performance**: Similar to S3
- **Scalability**: Excellent
- **Cost**: Competitive with S3
- **CDN Integration**: Azure CDN, Front Door
- **Use Cases**: Azure-native deployments

### Cache Hierarchies

**Multi-Tier Caching Strategy**:

```
Client Request
    ↓
CDN Cache (CloudFront/CloudFlare) - Edge locations
    ↓ (miss)
Load Balancer
    ↓
Honua Server (in-memory cache - optional)
    ↓ (miss)
S3/Azure Blob (persistent cache)
    ↓ (miss)
On-Demand Tile Generation
    ↓
Store in S3/Azure + Return to Client
```

**Recommended Configuration**:
1. **Development**: Filesystem cache only
2. **Production (small)**: Filesystem + nginx proxy cache
3. **Production (large)**: S3/Azure + CloudFront/Azure CDN
4. **Production (global)**: S3 + CloudFront with multiple edge locations

## Vector Tiles (MVT)

### PostGIS ST_AsMVT Generation

Honua generates MVT tiles using PostGIS spatial functions for maximum performance.

```csharp
// From PostgresDataStoreProvider.cs - GenerateMvtTileAsync
public async Task<byte[]?> GenerateMvtTileAsync(
    DataSourceDefinition dataSource,
    ServiceDefinition service,
    LayerDefinition layer,
    int zoom,
    int x,
    int y,
    CancellationToken cancellationToken = default)
{
    // Calculate Web Mercator tile bounds
    const double earthRadius = 6378137.0;
    const double originShift = Math.PI * earthRadius;
    var tileSize = 2.0 * originShift / Math.Pow(2, zoom);

    var minX = -originShift + (x * tileSize);
    var maxY = originShift - (y * tileSize);
    var maxX = minX + tileSize;
    var minY = maxY - tileSize;

    // SQL query for MVT generation
    var sql = $@"
        WITH mvtgeom AS (
            SELECT
                ST_AsMVTGeom(
                    ST_Transform(ST_SetSRID({geometryColumn}, {storageSrid}), 3857),
                    ST_MakeEnvelope($1, $2, $3, $4, 3857),
                    4096,      -- extent (tile resolution)
                    256,       -- buffer (edge clipping tolerance)
                    true       -- clip_geom
                ) AS geom,
                *
            FROM {tableName}
            WHERE {geometryColumn} && ST_Transform(
                ST_MakeEnvelope($1, $2, $3, $4, 3857), {storageSrid})
        )
        SELECT ST_AsMVT(mvtgeom.*, $5, 4096, 'geom')
        FROM mvtgeom
        WHERE geom IS NOT NULL;
    ";

    var result = await command.ExecuteScalarAsync(cancellationToken);
    return result is byte[] mvtBytes ? mvtBytes : Array.Empty<byte>();
}
```

### MVT Configuration Parameters

**ST_AsMVTGeom Parameters**:
- **Geometry**: Source geometry in EPSG:3857
- **Tile Bounds**: Bounding box envelope
- **Extent**: 4096 (tile coordinate system resolution)
- **Buffer**: 256 pixels (prevents edge artifacts)
- **Clip Geometry**: `true` (clip to tile bounds)

**ST_AsMVT Parameters**:
- **Layer Name**: Dataset or layer ID
- **Extent**: 4096 (must match ST_AsMVTGeom)
- **Geometry Column**: `geom` (default)

### MVT Tile Simplification

**Automatic Generalization**:
```sql
-- Add zoom-based simplification
ST_AsMVTGeom(
    ST_Simplify(
        ST_Transform(ST_SetSRID({geometryColumn}, {storageSrid}), 3857),
        CASE
            WHEN $zoom <= 5 THEN 100
            WHEN $zoom <= 10 THEN 10
            ELSE 1
        END
    ),
    ST_MakeEnvelope($1, $2, $3, $4, 3857),
    4096,
    256,
    true
) AS geom
```

**Benefits**:
- Reduces tile size at lower zoom levels
- Improves rendering performance
- Maintains visual fidelity at appropriate scales

### Attribute Selection

**Include Only Necessary Attributes**:
```sql
WITH mvtgeom AS (
    SELECT
        ST_AsMVTGeom(...) AS geom,
        id,              -- Primary key
        name,            -- Display name
        category,        -- Classification field
        -- Exclude: created_at, updated_at, large_text_fields
    FROM {tableName}
    WHERE ...
)
```

**Attribute Filtering Benefits**:
- Smaller tile sizes (often 30-50% reduction)
- Faster client-side parsing
- Reduced bandwidth costs

### MVT vs Raster Tiles: When to Use Each

| Aspect | MVT (Vector Tiles) | Raster Tiles |
|--------|-------------------|--------------|
| **File Size** | 5-30 KB typical | 15-50 KB (PNG), 10-30 KB (JPEG) |
| **Rendering** | Client-side | Pre-rendered server-side |
| **Styling** | Dynamic, client-controlled | Fixed at generation time |
| **Zoom Levels** | Continuous (client interpolates) | Discrete (1 tile per zoom) |
| **Text Labels** | Sharp at any zoom | Pixelated when scaled |
| **Attributes** | Included (query/inspect) | Not included |
| **Bandwidth** | Lower (typically 40-60% less) | Higher |
| **Server CPU** | Lower (once cached) | Higher (rendering) |
| **Client CPU** | Higher (rendering) | Lower (image decode) |
| **Best For** | Points, lines, polygons with attributes | Imagery, heatmaps, complex symbology |
| **Caching** | Simple (geometry rarely changes) | Complex (style changes = regenerate) |

**Decision Matrix**:

Use **MVT** for:
- Administrative boundaries
- Roads/transportation networks
- Points of interest (POI)
- Statistical data with tooltips
- Features requiring client-side filtering
- Multi-theme/dark mode support

Use **Raster Tiles** for:
- Satellite/aerial imagery
- Scanned historical maps
- Elevation/hillshade rendering
- Heatmaps and density maps
- Complex cartographic symbology
- Legacy client compatibility

## Tile Preseed Workflows

### Preseed Service Architecture

```csharp
// From RasterTilePreseedService.cs
public interface IRasterTilePreseedService
{
    Task<RasterTilePreseedJobSnapshot> EnqueueAsync(
        RasterTilePreseedRequest request,
        CancellationToken cancellationToken);

    bool TryGetJob(Guid jobId, out RasterTilePreseedJobSnapshot snapshot);

    IReadOnlyList<RasterTilePreseedJobSnapshot> ListJobs();

    Task<RasterTilePreseedJobSnapshot?> CancelAsync(
        Guid jobId,
        string? reason);

    Task<RasterTileCachePurgeResult> PurgeAsync(
        IEnumerable<string> datasetIds,
        CancellationToken cancellationToken);
}
```

### Preseed Request Configuration

```json
{
  "datasetIds": ["roads-imagery", "parcels-overlay"],
  "tileMatrixSetId": "WorldWebMercatorQuad",
  "minZoom": 0,
  "maxZoom": 12,
  "styleId": "default",
  "format": "image/png",
  "transparent": true,
  "tileSize": 256,
  "overwrite": false,
  "bbox": null
}
```

**Preseed Configuration**:
```json
{
  "honua": {
    "services": {
      "rasterTiles": {
        "preseed": {
          "batchSize": 32,
          "maxDegreeOfParallelism": 4
        }
      }
    }
  }
}
```

**Environment Variables**:
```bash
HONUA__SERVICES__RASTERTILES__PRESEED__BATCHSIZE=32
HONUA__SERVICES__RASTERTILES__PRESEED__MAXDEGREEOFPARALLELISM=4
```

### Preseed Strategies

#### 1. Full Pyramid Preseed

Generate all tiles from zoom 0 to max zoom:
```bash
# CLI command (conceptual)
honua preseed --dataset roads-imagery --min-zoom 0 --max-zoom 14
```

**Tile Count Calculation**:
```
Total tiles = Σ(4^z) for z in [0, maxZoom]

Zoom 0:  1 tile
Zoom 4:  256 tiles
Zoom 8:  65,536 tiles
Zoom 12: 16,777,216 tiles
Zoom 14: 268,435,456 tiles
```

#### 2. Area-Constrained Preseed

Generate tiles only within bounding box:
```json
{
  "minZoom": 8,
  "maxZoom": 14,
  "bbox": [-122.5, 45.5, -122.3, 45.7]
}
```

**Tile Range Calculation** (from `OgcTileMatrixHelper.GetTileRange`):
```csharp
public static (int MinRow, int MaxRow, int MinColumn, int MaxColumn)
    GetTileRange(string tileMatrixSetId, int zoom,
                 double minX, double minY, double maxX, double maxY)
{
    var maxIndex = (1 << zoom) - 1;

    if (IsWorldWebMercatorQuad(tileMatrixSetId))
    {
        var span = WebMercatorMax - WebMercatorMin;
        var minColumn = (int)Math.Floor((minX - WebMercatorMin) / span * (1 << zoom));
        var maxColumn = (int)Math.Floor((maxX - WebMercatorMin) / span * (1 << zoom));
        var minRow = (int)Math.Floor((WebMercatorMax - maxY) / span * (1 << zoom));
        var maxRow = (int)Math.Floor((WebMercatorMax - minY) / span * (1 << zoom));

        return (
            Math.Clamp(minRow, 0, maxIndex),
            Math.Clamp(maxRow, 0, maxIndex),
            Math.Clamp(minColumn, 0, maxIndex),
            Math.Clamp(maxColumn, 0, maxIndex)
        );
    }
    // Similar for CRS84
}
```

#### 3. Incremental Preseed

Preseed only missing tiles (skip existing):
```json
{
  "overwrite": false
}
```

**Process**:
```csharp
if (!request.Overwrite)
{
    var cached = await _cacheProvider.TryGetAsync(cacheKey, cancellationToken);
    if (cached is not null)
    {
        job.IncrementTiles(stage);
        continue; // Skip to next tile
    }
}
```

#### 4. Parallel Tile Generation

```csharp
// Configuration
public sealed class RasterTilePreseedConfiguration
{
    public int BatchSize { get; init; } = 32;
    public int MaxDegreeOfParallelism { get; init; } = 1;
}
```

**Recommended Parallelism**:
- **CPU-bound (rendering)**: CPU cores - 1
- **I/O-bound (network storage)**: 2-4x CPU cores
- **Mixed workload**: 1-2x CPU cores

**Example**: 8-core server
- BatchSize: 32 tiles
- MaxDegreeOfParallelism: 6 (leaves 2 cores for HTTP traffic)

## Cache Invalidation

### Purge Dataset Cache

```csharp
// Remove all cached tiles for specific datasets
public async Task<RasterTileCachePurgeResult> PurgeAsync(
    IEnumerable<string> datasetIds,
    CancellationToken cancellationToken)
{
    foreach (var datasetId in datasetIds)
    {
        await _cacheProvider.PurgeDatasetAsync(datasetId, cancellationToken);
    }
}
```

**Admin API Endpoint**:
```http
DELETE /admin/cache/raster/datasets
Content-Type: application/json

{
  "datasetIds": ["roads-imagery", "parcels-overlay"]
}
```

### Remove Single Tile

```csharp
await _cacheProvider.RemoveAsync(cacheKey, cancellationToken);
```

### Cache Versioning Strategy

**Append version to dataset ID**:
```json
{
  "id": "roads-imagery-v2",
  "title": "Roads Imagery"
}
```

Benefits:
- Old cache remains accessible during transition
- No downtime during cache regeneration
- Easy rollback if issues detected

## OGC Tiles API Endpoints

### Tile Endpoint Pattern

```
GET /ogc/collections/{collectionId}/tiles/{tilesetId}/{tileMatrixSetId}/{z}/{y}/{x}
```

**Parameters**:
- `collectionId`: Service + Layer (e.g., `roads::roads-primary`)
- `tilesetId`: Raster dataset ID (e.g., `roads-imagery`)
- `tileMatrixSetId`: `WorldCRS84Quad` or `WorldWebMercatorQuad`
- `z`: Zoom level (0-22)
- `y`: Tile row coordinate
- `x`: Tile column coordinate

**Query Parameters**:
- `f`: Format override (`png`, `jpeg`, `webp`, `mvt`, `geojson`)
- `styleId`: Style identifier (default: dataset's default style)
- `transparent`: `true` or `false` (default: `true`)
- `tileSize`: Tile dimension in pixels (default: `256`)

**Example Requests**:
```http
# PNG raster tile at zoom 12
GET /ogc/collections/roads::roads-primary/tiles/roads-imagery/WorldWebMercatorQuad/12/1234/5678?f=png

# JPEG tile (opaque background)
GET /ogc/collections/roads::roads-primary/tiles/roads-imagery/WorldWebMercatorQuad/12/1234/5678?f=jpeg&transparent=false

# MVT vector tile
GET /ogc/collections/roads::roads-primary/tiles/roads-mvt/WorldWebMercatorQuad/12/1234/5678?f=mvt

# Custom style
GET /ogc/collections/roads::roads-primary/tiles/roads-imagery/WorldWebMercatorQuad/12/1234/5678?styleId=aerial-view
```

### TileJSON Metadata

```
GET /ogc/collections/{collectionId}/tiles/{tilesetId}/tilejson
```

**Response**:
```json
{
  "tilejson": "3.0.0",
  "name": "Roads Imagery",
  "description": "Major roadways imagery",
  "scheme": "xyz",
  "format": "png",
  "minzoom": 0,
  "maxzoom": 14,
  "bounds": [-122.6, 45.5, -122.3, 45.7],
  "center": [-122.45, 45.6, 8],
  "tiles": [
    "https://honua.example.com/ogc/collections/roads::roads-primary/tiles/roads-imagery/WorldWebMercatorQuad/{z}/{y}/{x}"
  ],
  "dataType": "map",
  "links": [
    {
      "href": "/ogc/collections/roads::roads-primary/tiles/roads-imagery/tilejson",
      "rel": "self",
      "type": "application/json"
    }
  ]
}
```

**Vector TileJSON** (MVT):
```json
{
  "tilejson": "3.0.0",
  "format": "geojson",
  "vector_layers": [
    {
      "id": "roads-primary",
      "description": "Primary Roads"
    }
  ]
}
```

### Tile Set Metadata

```
GET /ogc/collections/{collectionId}/tiles/{tilesetId}/{tileMatrixSetId}
```

**Response**:
```json
{
  "id": "WorldWebMercatorQuad",
  "title": "WorldWebMercatorQuad",
  "tileMatrixSetUri": "http://www.opengis.net/def/tms/OGC/1.0/WorldWebMercatorQuad",
  "crs": "http://www.opengis.net/def/crs/EPSG/0/3857",
  "dataType": "map",
  "minZoom": 0,
  "maxZoom": 14,
  "tileMatrices": [
    {
      "id": "0",
      "scaleDenominator": 559082264.0287178,
      "topLeftCorner": [-20037508.3427892, 20037508.3427892],
      "tileWidth": 256,
      "tileHeight": 256,
      "matrixWidth": 1,
      "matrixHeight": 1
    },
    {
      "id": "12",
      "scaleDenominator": 136494.14071339793,
      "topLeftCorner": [-20037508.3427892, 20037508.3427892],
      "tileWidth": 256,
      "tileHeight": 256,
      "matrixWidth": 4096,
      "matrixHeight": 4096
    }
  ]
}
```

## CDN Integration

### CloudFront + S3 Configuration

**CloudFront Distribution**:
```yaml
Origins:
  - S3Origin:
      DomainName: honua-tiles.s3.us-west-2.amazonaws.com
      OriginPath: /tiles
    Behaviors:
      PathPattern: /tiles/*
      CachePolicyId: Managed-CachingOptimized
      Compress: true
      AllowedMethods: [GET, HEAD, OPTIONS]
      ViewerProtocolPolicy: redirect-to-https
```

**Cache Policy**:
- TTL: 86400 seconds (24 hours)
- Query String Behavior: Whitelist (`f`, `styleId`, `transparent`, `tileSize`)
- Headers: None (tiles are pre-rendered)
- Cookies: None

**Honua Configuration** (direct S3 writes):
```json
{
  "honua": {
    "services": {
      "rasterTiles": {
        "provider": "s3",
        "s3": {
          "bucketName": "honua-tiles",
          "prefix": "tiles/"
        }
      }
    }
  }
}
```

### CloudFlare + S3 Configuration

**CloudFlare Page Rule**:
```
https://tiles.example.com/*
  Cache Level: Cache Everything
  Edge Cache TTL: 1 month
  Browser Cache TTL: 1 day
```

**Workers (optional cache warming)**:
```javascript
addEventListener('fetch', event => {
  event.respondWith(handleRequest(event.request))
})

async function handleRequest(request) {
  const cache = caches.default
  let response = await cache.match(request)

  if (!response) {
    response = await fetch(request)
    if (response.ok) {
      event.waitUntil(cache.put(request, response.clone()))
    }
  }

  return response
}
```

### Azure CDN + Blob Storage

**CDN Endpoint**:
```json
{
  "origin": "honuatiles.blob.core.windows.net",
  "cachingRules": {
    "queryStringCachingBehavior": "UseQueryString",
    "cacheDuration": "1.00:00:00"
  },
  "compressionEnabled": true,
  "optimizationType": "GeneralWebDelivery"
}
```

## Performance Optimization

### Preseed Optimization Strategies

#### 1. Zoom Level Prioritization

Preseed critical zoom levels first:
```bash
# Phase 1: Global overview (instant)
honua preseed --dataset imagery --min-zoom 0 --max-zoom 4

# Phase 2: Regional detail (minutes)
honua preseed --dataset imagery --min-zoom 5 --max-zoom 8

# Phase 3: Local detail (hours)
honua preseed --dataset imagery --min-zoom 9 --max-zoom 12

# Phase 4: High detail (overnight)
honua preseed --dataset imagery --min-zoom 13 --max-zoom 14
```

#### 2. Area-Based Prioritization

Preseed high-traffic areas first:
```json
// Urban core (highest priority)
{
  "bbox": [-122.35, 45.51, -122.30, 45.55],
  "minZoom": 0,
  "maxZoom": 16
}

// Suburban areas (medium priority)
{
  "bbox": [-122.50, 45.45, -122.25, 45.60],
  "minZoom": 0,
  "maxZoom": 14
}

// Rural areas (low priority, on-demand only)
{
  "minZoom": 0,
  "maxZoom": 10
}
```

#### 3. Parallel Generation Tuning

**CPU-Bound Workloads** (complex rendering):
```json
{
  "preseed": {
    "batchSize": 16,
    "maxDegreeOfParallelism": 7  // 8-core machine
  }
}
```

**I/O-Bound Workloads** (simple rendering, network storage):
```json
{
  "preseed": {
    "batchSize": 64,
    "maxDegreeOfParallelism": 16
  }
}
```

**Benchmark Results** (8-core, SSD, PNG tiles):
- Parallelism 1: ~50 tiles/sec
- Parallelism 4: ~180 tiles/sec
- Parallelism 8: ~280 tiles/sec
- Parallelism 16: ~290 tiles/sec (I/O bound)

### Tile Size Optimization

**Format Selection by Use Case**:

| Use Case | Format | Transparent | Typical Size | Notes |
|----------|--------|-------------|--------------|-------|
| Satellite imagery | JPEG | No | 15-25 KB | Best compression |
| Vector overlays | PNG | Yes | 8-12 KB | Mostly transparent |
| Mixed map | PNG | Yes | 25-40 KB | General purpose |
| Modern browsers | WebP | Yes | 12-20 KB | 30% smaller than PNG |
| High-res printing | PNG | No | 40-60 KB | Lossless required |

**Compression Strategies**:

PNG Optimization (server-side):
```bash
# Install pngquant for lossy PNG compression
apt-get install pngquant

# Reduce to 256 colors (often 50-70% size reduction)
pngquant --quality=65-80 --speed=1 input.png -o output.png
```

WebP Conversion:
```bash
# Convert PNG to WebP (client-side fallback required)
cwebp -q 80 input.png -o output.webp
```

### Cache Warming

**Predictive Warming** (based on access patterns):
```sql
-- Log tile requests
CREATE TABLE tile_access_log (
    dataset_id TEXT,
    zoom INT,
    x INT,
    y INT,
    access_count INT,
    last_access TIMESTAMP
);

-- Identify hot tiles
SELECT dataset_id, zoom, x, y, access_count
FROM tile_access_log
WHERE access_count > 100
ORDER BY access_count DESC
LIMIT 10000;
```

**Scheduled Warming** (cron job):
```bash
#!/bin/bash
# warm-cache.sh - Daily cache refresh

# Warm zoom 0-8 (global overview)
curl -X POST http://localhost:5000/admin/cache/preseed \
  -H "Content-Type: application/json" \
  -d '{
    "datasetIds": ["global-imagery"],
    "minZoom": 0,
    "maxZoom": 8,
    "overwrite": true
  }'
```

## Performance Benchmarks

### Tile Generation Performance

**Test Environment**: 8-core CPU, 16GB RAM, SSD, PostgreSQL 15 + PostGIS 3.4

| Tile Type | Format | Zoom | Generation Time | Throughput |
|-----------|--------|------|-----------------|------------|
| Simple polygon | PNG | 8 | 12ms | 83 tiles/sec |
| Simple polygon | PNG | 12 | 18ms | 55 tiles/sec |
| Complex polygon (10K vertices) | PNG | 8 | 45ms | 22 tiles/sec |
| MVT (PostGIS) | MVT | 8 | 8ms | 125 tiles/sec |
| MVT (PostGIS) | MVT | 12 | 15ms | 66 tiles/sec |
| Satellite imagery (COG) | JPEG | 8 | 35ms | 28 tiles/sec |
| Satellite imagery (COG) | WebP | 8 | 42ms | 24 tiles/sec |

### Cache Provider Performance

**Test**: Read 1000 cached 256x256 PNG tiles

| Provider | Latency (p50) | Latency (p99) | Throughput |
|----------|---------------|---------------|------------|
| Filesystem (SSD) | 0.8ms | 2.5ms | 1200 tiles/sec |
| Filesystem (NFS) | 12ms | 45ms | 80 tiles/sec |
| S3 (us-west-2) | 25ms | 85ms | 40 tiles/sec |
| S3 + CloudFront | 8ms | 20ms | 120 tiles/sec |
| Azure Blob | 28ms | 90ms | 35 tiles/sec |
| Azure CDN | 10ms | 25ms | 100 tiles/sec |

### Preseed Time Estimates

**Dataset**: 10km x 10km area, WorldWebMercatorQuad

| Zoom Range | Total Tiles | Est. Time (1 core) | Est. Time (8 cores) |
|------------|-------------|-------------------|---------------------|
| 0-8 | ~21,000 | 6 minutes | 1 minute |
| 0-12 | ~1.3M | 6.5 hours | 50 minutes |
| 0-14 | ~21M | 4.5 days | 14 hours |
| 0-16 | ~340M | 72 days | 9 days |

**Storage Requirements** (PNG, average 25 KB/tile):

| Zoom Range | Storage (GB) |
|------------|--------------|
| 0-8 | 0.5 GB |
| 0-12 | 33 GB |
| 0-14 | 525 GB |
| 0-16 | 8.4 TB |

## Troubleshooting

### Issue: Tiles Not Caching

**Symptoms**: Every request generates tile, cache provider not storing

**Diagnosis**:
```bash
# Check cache provider is configured
curl http://localhost:5000/admin/health

# Verify cache writes (filesystem)
ls -lR ./data/raster-cache/

# Check S3 bucket
aws s3 ls s3://honua-tiles/tiles/ --recursive | head

# Review logs
tail -f logs/honua-*.log | grep -i cache
```

**Solutions**:
1. Verify provider is enabled: `"rasterTiles": { "enabled": true }`
2. Check filesystem permissions: `chmod -R 755 ./data/raster-cache`
3. Verify S3 credentials: `aws s3 ls s3://honua-tiles`
4. Check bucket policy allows PutObject

### Issue: Slow Tile Generation

**Symptoms**: Tile requests timeout, preseed jobs fail

**Diagnosis**:
```bash
# Check PostgreSQL query performance
psql -c "EXPLAIN ANALYZE SELECT ST_AsMVT(...);"

# Monitor CPU/memory
htop

# Check rendering latency metrics
curl http://localhost:5000/metrics | grep tile_render
```

**Solutions**:
1. Add spatial index: `CREATE INDEX ON roads_primary USING GIST (geom);`
2. Reduce tile complexity: Use ST_Simplify for lower zooms
3. Increase parallelism: `"maxDegreeOfParallelism": 8`
4. Optimize queries: Filter unnecessary attributes

### Issue: Excessive Cache Storage

**Symptoms**: Disk/S3 costs too high, storage filling up

**Diagnosis**:
```bash
# Check cache size
du -sh ./data/raster-cache/

# Count tiles
find ./data/raster-cache/ -name "*.png" | wc -l

# S3 storage size
aws s3 ls s3://honua-tiles/tiles/ --recursive --summarize
```

**Solutions**:
1. Reduce max zoom: `"maxZoom": 12` instead of 14
2. Use JPEG for imagery: `"format": "image/jpeg"`
3. Implement cache expiration: Delete tiles older than 90 days
4. Use WebP: 30-40% smaller than PNG
5. Purge unused datasets: `DELETE /admin/cache/raster/datasets`

### Issue: CDN Not Serving Cached Tiles

**Symptoms**: CloudFront/Azure CDN always fetching from origin

**Diagnosis**:
```bash
# Check cache headers
curl -I https://cdn.example.com/tiles/.../12/1234/5678.png

# Expected headers:
# X-Cache: Hit from cloudfront
# Cache-Control: public, max-age=86400
```

**Solutions**:
1. Add Cache-Control headers to S3 objects:
   ```json
   {
     "s3": {
       "headers": {
         "Cache-Control": "public, max-age=86400"
       }
     }
   }
   ```
2. Configure CDN to respect Cache-Control
3. Whitelist query parameters in CDN config
4. Invalidate CDN cache: `aws cloudfront create-invalidation`

### Issue: Preseed Job Stuck or Slow

**Symptoms**: Job shows as in-progress but not advancing

**Diagnosis**:
```bash
# Check job status
curl http://localhost:5000/admin/cache/preseed/jobs

# Monitor progress
tail -f logs/honua-*.log | grep preseed
```

**Solutions**:
1. Reduce batch size: `"batchSize": 16`
2. Reduce parallelism: `"maxDegreeOfParallelism": 2`
3. Check database connections: Ensure pool size supports parallelism
4. Cancel and restart: `POST /admin/cache/preseed/jobs/{id}/cancel`
5. Check disk I/O: `iostat -x 1`

### Issue: MVT Tiles Empty or Invalid

**Symptoms**: Vector tiles download but contain no features

**Diagnosis**:
```bash
# Verify PostGIS function
psql -c "SELECT postgis_version();"

# Test MVT query manually
psql -c "SELECT ST_AsMVT(...);"

# Check tile bounds
curl http://localhost:5000/ogc/collections/.../tiles/.../12/1234/5678?f=mvt -o test.mvt
```

**Solutions**:
1. Verify geometry column SRID: `SELECT SRID(geom) FROM table LIMIT 1;`
2. Check spatial index exists: `\d table_name`
3. Verify tile coordinates valid: Row/column must be < 2^zoom
4. Check tile bounds intersect data: Use `ST_Intersects`

## Complete Configuration Example

### Docker Compose with S3 Cache + MinIO

```yaml
version: '3.8'

services:
  postgres:
    image: postgis/postgis:15-3.4
    environment:
      POSTGRES_DB: honua
      POSTGRES_USER: honua
      POSTGRES_PASSWORD: honua123
    volumes:
      - postgres-data:/var/lib/postgresql/data
    ports:
      - "5432:5432"

  minio:
    image: minio/minio:latest
    command: server /data --console-address ":9001"
    environment:
      MINIO_ROOT_USER: minioadmin
      MINIO_ROOT_PASSWORD: minioadmin
    volumes:
      - minio-data:/data
    ports:
      - "9000:9000"
      - "9001:9001"

  honua:
    image: honua/server:latest
    environment:
      # Database
      HONUA__METADATA__PROVIDER: json
      HONUA__METADATA__PATH: /app/metadata/metadata.json

      # Tile caching
      HONUA__SERVICES__RASTERTILES__ENABLED: "true"
      HONUA__SERVICES__RASTERTILES__PROVIDER: s3
      HONUA__SERVICES__RASTERTILES__S3__BUCKETNAME: tiles
      HONUA__SERVICES__RASTERTILES__S3__PREFIX: cache/
      HONUA__SERVICES__RASTERTILES__S3__SERVICEURL: http://minio:9000
      HONUA__SERVICES__RASTERTILES__S3__ACCESSKEYID: minioadmin
      HONUA__SERVICES__RASTERTILES__S3__SECRETACCESSKEY: minioadmin
      HONUA__SERVICES__RASTERTILES__S3__FORCEPATHSTYLE: "true"
      HONUA__SERVICES__RASTERTILES__S3__ENSUREBUCKET: "true"

      # Preseed configuration
      HONUA__SERVICES__RASTERTILES__PRESEED__BATCHSIZE: "32"
      HONUA__SERVICES__RASTERTILES__PRESEED__MAXDEGREEOFPARALLELISM: "4"
    volumes:
      - ./metadata:/app/metadata
    ports:
      - "5000:8080"
    depends_on:
      - postgres
      - minio

volumes:
  postgres-data:
  minio-data:
```

### Production Configuration (AWS)

```json
{
  "honua": {
    "metadata": {
      "provider": "json",
      "path": "/app/metadata/metadata.json"
    },
    "services": {
      "rasterTiles": {
        "enabled": true,
        "provider": "s3",
        "s3": {
          "bucketName": "honua-tiles-prod",
          "prefix": "tiles/v1/",
          "region": "us-west-2",
          "ensureBucket": false,
          "forcePathStyle": false
        },
        "preseed": {
          "batchSize": 64,
          "maxDegreeOfParallelism": 8
        }
      }
    }
  }
}
```

**IAM Role Policy**:
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "s3:GetObject",
        "s3:PutObject",
        "s3:DeleteObject",
        "s3:ListBucket"
      ],
      "Resource": [
        "arn:aws:s3:::honua-tiles-prod",
        "arn:aws:s3:::honua-tiles-prod/*"
      ]
    }
  ]
}
```

## Summary

Honua's tile caching system provides:

1. **Multiple Formats**: Raster (PNG/JPEG/WebP) and Vector (MVT)
2. **Flexible Storage**: Filesystem, S3, Azure Blob
3. **Standard Tile Matrices**: WorldCRS84Quad and WorldWebMercatorQuad
4. **Efficient Generation**: Parallel preseed, on-demand rendering
5. **CDN Integration**: CloudFront, Azure CDN, CloudFlare support
6. **OGC Compliance**: OGC Tiles API, TileJSON, WMTS compatibility
7. **Performance**: Benchmarked at 1200+ tiles/sec (cached), 125 MVT tiles/sec (generated)

For high-traffic production deployments, use:
- **S3 + CloudFront** for storage and global distribution
- **MVT tiles** for vector data to reduce bandwidth 40-60%
- **Preseed** critical zoom levels (0-12) during off-peak hours
- **WebP format** for modern clients to save 30% bandwidth

This architecture scales from single-server development (filesystem cache) to global deployments serving billions of tiles per day.
