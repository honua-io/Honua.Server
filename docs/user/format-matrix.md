# Format Matrix

Complete reference for all supported data formats and content types across Honua APIs.

## Vector Feature Formats

### Standard Formats

| Format | Content-Type | Query Parameter | Accept Header | Notes |
|--------|--------------|-----------------|---------------|-------|
| **GeoJSON** | `application/geo+json` | `?f=geojson` | `Accept: application/geo+json` | Default format for OGC API Features. Standard GeoJSON FeatureCollection. |
| **GeoJSON-Seq** | `application/geo+json-seq` | `?f=geojsonl` or `?f=geojsonseq` | `Accept: application/geo+json-seq` | Streaming newline-delimited GeoJSON (RFC 8142). Each line is a complete GeoJSON Feature. |
| **TopoJSON** | `application/topo+json` | `?f=topojson` | `Accept: application/topo+json` | Shared-arc topology format. Reduces file size for adjacent polygons. Limited to ~5k features for synchronous responses. |
| **KML** | `application/vnd.google-earth.kml+xml` | `?f=kml` | `Accept: application/vnd.google-earth.kml+xml` | Google Earth KML format. Includes `<ExtendedData>` for attributes. |
| **KMZ** | `application/vnd.google-earth.kmz` | `?f=kmz` | `Accept: application/vnd.google-earth.kmz` | Compressed KML in ZIP archive. Includes icons and styles. |
| **CSV** | `text/csv` | `?f=csv` | `Accept: text/csv` | Comma-separated values with geometry as WKT or GeoJSON. |
| **Shapefile** | `application/zip` | `?f=shapefile` or `?f=shp` | `Accept: application/x-shapefile` | Zipped SHP/SHX/DBF/PRJ bundle. Enforces record limits (configurable, default 10k). |
| **GeoPackage** | `application/geopackage+sqlite3` | `?f=geopackage` or `?f=gpkg` | `Accept: application/geopackage+sqlite3` | OGC GeoPackage SQLite database. May return 202 Accepted with async job for large datasets. |
| **GeoServices JSON** | `application/json` | `?f=json` or `?f=pjson` | `Accept: application/json` | GeoServices FeatureSet format (FeatureServer endpoints only). |
| **GML 3.2** | `application/gml+xml; version=3.2` | `?outputFormat=application/gml+xml; version=3.2` | `Accept: application/gml+xml` | OGC Geography Markup Language 3.2. Used for WFS locking and transactions. |

### High-Performance & Cloud-Optimized Formats

| Format | Content-Type | Query Parameter | Accept Header | Notes |
|--------|--------------|-----------------|---------------|-------|
| **FlatGeobuf** | `application/flatgeobuf` | `?f=fgb` or `?f=flatgeobuf` | `Accept: application/flatgeobuf` | High-performance binary format with spatial indexing. HTTP range request support for cloud-optimized access. |
| **GeoParquet** | `application/parquet` | `?f=parquet` or `?f=geoparquet` | `Accept: application/parquet` | Columnar format optimized for analytics. GeoParquet v1.1.0 spec compliant. Ideal for big data workflows. |
| **GeoArrow** | `application/vnd.apache.arrow.file` | `?f=arrow` or `?f=geoarrow` | `Accept: application/vnd.apache.arrow.file` | Apache Arrow columnar format with WKB geometry encoding. Zero-copy reads, efficient for analytics. |
| **PMTiles** | `application/pmtiles` | `?f=pmtiles` | `Accept: application/pmtiles` | Cloudless tile archive format. Supports Gzip, Brotli, and Zstd compression. Optimized for serverless deployment. |

### Linked Data & Semantic Web Formats

| Format | Content-Type | Query Parameter | Accept Header | Notes |
|--------|--------------|-----------------|---------------|-------|
| **JSON-LD** | `application/ld+json` | `?f=jsonld` | `Accept: application/ld+json` | Linked Data JSON format with semantic annotations. |
| **GeoJSON-T** | `application/geo+json-t` | `?f=geojsont` | `Accept: application/geo+json-t` | Time-indexed GeoJSON variant for temporal data. |

### Geometry-Only Formats

| Format | Content-Type | Query Parameter | Accept Header | Notes |
|--------|--------------|-----------------|---------------|-------|
| **WKT** | `text/plain` | `?f=wkt` | `Accept: text/plain` | Well-Known Text geometry format. Returns geometries only, no attributes. |
| **WKB** | `application/octet-stream` | `?f=wkb` | `Accept: application/octet-stream` | Well-Known Binary geometry format. Compact binary representation. |

## Raster Tile Formats

| Format | Content-Type | Query Parameter | Notes |
|--------|--------------|-----------------|-------|
| **PNG** | `image/png` | `?format=png` | Default raster tile format. Supports transparency. |
| **JPEG** | `image/jpeg` | `?format=jpeg` or `?format=jpg` | Lossy compression. No transparency. Smaller file size. |
| **WebP** | `image/webp` | `?format=webp` | Modern format with better compression. Not supported by all clients. |

## Vector Tile Formats

| Format | Content-Type | Query Parameter | Notes |
|--------|--------------|-----------------|-------|
| **MVT (Mapbox Vector Tile)** | `application/vnd.mapbox-vector-tile` | `?f=mvt` or `?f=pbf` | Protocol buffer format. Requires client-side rendering. Generated using PostGIS `ST_AsMVT` for PostGIS sources. |

## Metadata Formats

| Format | Content-Type | Query Parameter | Notes |
|--------|--------------|-----------------|-------|
| **JSON** | `application/json` | `?f=json` | Default for most API responses (landing pages, collections, etc.). |
| **HTML** | `text/html` | `?f=html` | Lightweight HTML view for OGC API endpoints. Human-readable browser view. |
| **TileJSON** | `application/json` | n/a | TileJSON 3.0 metadata for raster tile endpoints. |

## CSV Export Options

CSV export supports geometry encoding options:

### Geometry as WKT (default)
```bash
curl "https://localhost:5000/ogc/collections/roads/items?f=csv" > roads.csv
```

**Output:**
```csv
id,name,road_type,geometry
1,Main Street,primary,LINESTRING(-122.4 45.5, -122.3 45.6)
2,Oak Avenue,secondary,LINESTRING(-122.5 45.4, -122.4 45.5)
```

### Geometry as GeoJSON
```bash
curl "https://localhost:5000/ogc/collections/roads/items?f=csv&geometryFormat=geojson" > roads.csv
```

**Output:**
```csv
id,name,road_type,geometry
1,Main Street,primary,"{""type"":""LineString"",""coordinates"":[[-122.4,45.5],[-122.3,45.6]]}"
2,Oak Avenue,secondary,"{""type"":""LineString"",""coordinates"":[[-122.5,45.4],[-122.4,45.5]]}"
```

### Geometry as Separate X/Y Columns (Points only)
```bash
curl "https://localhost:5000/ogc/collections/poi/items?f=csv&geometryFormat=xy" > poi.csv
```

**Output:**
```csv
id,name,category,longitude,latitude
1,City Hall,government,-122.4,45.5
2,Library,education,-122.3,45.6
```

### Configuration
Configure CSV delimiter and options in metadata:
```json
{
  "layers": [{
    "id": "roads",
    "export": {
      "csv": {
        "delimiter": ",",
        "defaultGeometryFormat": "wkt",
        "includeHeader": true,
        "nullValue": ""
      }
    }
  }]
}
```

## GeoJSON-Seq (Streaming)

GeoJSON-Seq (RFC 8142) streams features as newline-delimited JSON, enabling:
- Large dataset export without memory limits
- Progressive rendering on client
- Line-by-line processing

**Request:**
```bash
curl "https://localhost:5000/ogc/collections/parcels/items?f=geojsonl&limit=1000000" > parcels.ndjson
```

**Output:**
```json
{"type":"Feature","id":1,"geometry":{...},"properties":{...}}
{"type":"Feature","id":2,"geometry":{...},"properties":{...}}
{"type":"Feature","id":3,"geometry":{...},"properties":{...}}
```

Each line is a complete, valid GeoJSON Feature.

**Processing with jq:**
```bash
# Count features
cat parcels.ndjson | wc -l

# Filter features
cat parcels.ndjson | jq 'select(.properties.zone == "Residential")'

# Convert to standard GeoJSON
jq -s '{type:"FeatureCollection",features:.}' parcels.ndjson > parcels.geojson
```

## Format Availability by Endpoint

### OGC API Features

| Endpoint | GeoJSON | GeoJSON-Seq | TopoJSON | KML | KMZ | CSV | Shapefile | GeoPackage |
|----------|---------|-------------|----------|-----|-----|-----|-----------|------------|
| `/ogc/collections/{id}/items` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `/ogc/collections/{id}/items/{featureId}` | ✓ | ✗ | ✗ | ✓ | ✗ | ✗ | ✗ | ✗ |

### Geoservices REST a.k.a. Esri REST API

| Endpoint | GeoServices JSON format | GeoJSON | TopoJSON | KML | KMZ | CSV | Shapefile | MVT |
|----------|-----------|---------|----------|-----|-----|-----|-----------|-----|
| `/rest/services/.../FeatureServer/{layer}/query` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `/rest/services/.../FeatureServer/{layer}/{objectId}` | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |

### WFS

| Request | GeoJSON | GML 3.2 | Other |
|---------|---------|---------|-------|
| `GetFeature` | ✓ | ✓ | ✗ |
| `GetFeatureWithLock` | ✗ | ✓ | ✗ |
| `Transaction` | ✗ | ✓ | ✗ |

**Note:** WFS locking and transactions require GML 3.2.

### WMS

| Request | PNG | JPEG | Other |
|---------|-----|------|-------|
| `GetMap` | ✓ | ✓ | ✗ |
| `GetLegendGraphic` | ✓ | ✗ | ✗ |

**GetFeatureInfo Formats:**
- `application/json`
- `application/geo+json`
- `application/xml`
- `text/html`
- `text/plain`

## Export Limitations

### Record Limits

| Format | Default Limit | Configurable | Notes |
|--------|---------------|--------------|-------|
| GeoJSON | 10,000 | Yes | Set `maxRecordCount` in layer metadata |
| GeoJSON-Seq | Unlimited | No | Streaming format |
| CSV | 100,000 | Yes | Set `export.csv.maxRecords` |
| Shapefile | 10,000 | Yes | DBF format limit ~2GB |
| GeoPackage | Async for >10k | Yes | Returns 202 Accepted with job ID |
| TopoJSON | 5,000 | Yes | Topology calculation is memory-intensive |

### Size Limits

Configure in `appsettings.json`:
```json
{
  "honua": {
    "export": {
      "maxFileSizeMB": 500,
      "asyncThresholdRecords": 10000,
      "timeoutSeconds": 300
    }
  }
}
```

### Async Export

For large exports, Honua returns `202 Accepted` with a job ID:

**Request:**
```bash
curl "https://localhost:5000/ogc/collections/parcels/items?f=geopackage&limit=500000"
```

**Response (202 Accepted):**
```json
{
  "jobId": "export-a1b2c3d4",
  "status": "queued",
  "estimatedRecords": 500000,
  "statusUrl": "/jobs/export-a1b2c3d4"
}
```

**Check Status:**
```bash
curl "https://localhost:5000/jobs/export-a1b2c3d4"
```

**Response (200 OK - Running):**
```json
{
  "jobId": "export-a1b2c3d4",
  "status": "running",
  "progress": 0.45,
  "recordsProcessed": 225000,
  "estimatedRecords": 500000
}
```

**Response (200 OK - Completed):**
```json
{
  "jobId": "export-a1b2c3d4",
  "status": "completed",
  "downloadUrl": "/downloads/export-a1b2c3d4.gpkg",
  "expiresAt": "2025-10-02T12:00:00Z",
  "recordsExported": 500000,
  "fileSizeBytes": 125894672
}
```

**Download:**
```bash
curl -OJ "https://localhost:5000/downloads/export-a1b2c3d4.gpkg"
```

## Content Negotiation

Honua supports standard HTTP content negotiation via the `Accept` header:

**GeoJSON (default):**
```bash
curl "https://localhost:5000/ogc/collections/roads/items"
curl -H "Accept: application/geo+json" "https://localhost:5000/ogc/collections/roads/items"
```

**KML:**
```bash
curl -H "Accept: application/vnd.google-earth.kml+xml" "https://localhost:5000/ogc/collections/roads/items"
```

**CSV:**
```bash
curl -H "Accept: text/csv" "https://localhost:5000/ogc/collections/roads/items"
```

**HTML (browser view):**
```bash
curl -H "Accept: text/html" "https://localhost:5000/ogc/collections"
```

**Query Parameter Override:**
The `?f=` parameter always overrides the `Accept` header:
```bash
curl -H "Accept: application/geo+json" "https://localhost:5000/ogc/collections/roads/items?f=kml"
# Returns KML, not GeoJSON
```

## See Also

- [Endpoints Reference](endpoints.md) - API endpoint documentation
- [Export Configuration](metadata-authoring.md#export-configuration) - Layer export settings
- [Configuration Reference](configuration.md#export) - Global export limits
