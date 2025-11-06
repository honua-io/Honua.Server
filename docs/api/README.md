# Honua API Reference

Complete API documentation for all Honua endpoints.

## API Endpoints Overview

### Geospatial APIs

| API | Base Path | Standard | Version |
|-----|-----------|----------|---------|
| OGC API Features | `/ogc` | OGC API - Features | 1.0 |
| OGC API Tiles | `/ogc/collections/{id}/tiles` | OGC API - Tiles | 1.0 |
| WFS | `/wfs` | OGC WFS | 2.0 |
| WMS | `/wms` | OGC WMS | 1.3 |
| WMTS | `/wmts` | OGC WMTS | 1.0 |
| WCS | `/wcs` | OGC WCS | 2.0 |
| CSW | `/csw` | OGC CSW | 2.0 |
| Geoservices REST a.k.a. Esri REST | `/rest/services` | Geoservices REST a.k.a. Esri REST API | 10.x |
| STAC | `/stac` | STAC | 1.0 |
| OData | `/odata` | OData | v4 |
| Carto SQL | `/carto/api/v1/sql` | Carto SQL API | - |

### Control Plane API (Admin)

| API | Base Path | Purpose | Required Role |
|-----|-----------|---------|---------------|
| **Configuration** | `/admin/config` | Runtime protocol toggles | Administrator |
| **Logging** | `/admin/logging` | Runtime log levels | Administrator |
| **Tracing** | `/admin/observability/tracing` | Runtime tracing config | Administrator |
| **Data Ingestion** | `/admin/ingestion` | Dataset uploads | DataPublisher |
| **Metadata** | `/admin/metadata` | Metadata management | Administrator |
| **Migration** | `/admin/migrations` | Esri service migrations | DataPublisher |
| **Raster Cache** | `/admin/raster-cache` | Tile cache management | Administrator |

📚 **[Complete Control Plane API Documentation →](../rag/06-01-control-plane-api.md)**

## Interactive Documentation

- **Swagger UI**: `http://localhost:8080/swagger` - Interactive API explorer with all endpoints
- **OpenAPI Spec**: `http://localhost:8080/ogc/api` - Machine-readable API definition

## OGC API Features

### Landing Page
```
GET /ogc
```

Returns service metadata and links to collections, conformance, and API definition.

### Conformance
```
GET /ogc/conformance
```

Lists all OGC conformance classes implemented.

### Collections
```
GET /ogc/collections
```

List all feature collections.

**Parameters:**
- `limit` - Max collections to return (default: 100)
- `offset` - Pagination offset

### Collection Metadata
```
GET /ogc/collections/{collectionId}
```

Get metadata for a specific collection.

### Features
```
GET /ogc/collections/{collectionId}/items
```

Query features from a collection.

**Parameters:**
- `limit` - Max features (default: 10, max: 10000)
- `offset` - Pagination offset
- `bbox` - Bounding box filter (minx,miny,maxx,maxy)
- `datetime` - Temporal filter (ISO 8601)
- `filter` - CQL filter expression
- `filter-lang` - Filter language (cql-text, cql2-json)
- `crs` - Coordinate reference system
- `f` - Output format (json, csv, kml, shp, gpkg, etc.)

**Example:**
```bash
curl "http://localhost:8080/ogc/collections/cities/items?limit=10&bbox=-180,-90,180,90&filter=population>1000000"
```

### Single Feature
```
GET /ogc/collections/{collectionId}/items/{featureId}
```

Get a specific feature by ID.

## WFS 2.0

### GetCapabilities
```
GET /wfs?service=WFS&version=2.0.0&request=GetCapabilities
```

### DescribeFeatureType
```
GET /wfs?service=WFS&version=2.0.0&request=DescribeFeatureType&typeNames={typeName}
```

### GetFeature
```
GET /wfs?service=WFS&version=2.0.0&request=GetFeature&typeNames={typeName}
```

**Parameters:**
- `count` - Max features
- `startIndex` - Pagination offset
- `bbox` - Bounding box
- `filter` - OGC Filter XML
- `outputFormat` - Output format

### Transaction (Insert/Update/Delete)
```
POST /wfs
Content-Type: application/xml

<wfs:Transaction service="WFS" version="2.0.0">
  <wfs:Insert>
    <!-- Feature to insert -->
  </wfs:Insert>
</wfs:Transaction>
```

## WMS 1.3

### GetCapabilities
```
GET /wms?service=WMS&version=1.3.0&request=GetCapabilities
```

### GetMap
```
GET /wms?service=WMS&version=1.3.0&request=GetMap&layers={layer}&bbox={bbox}&width={w}&height={h}&crs={crs}&format={format}
```

**Parameters:**
- `layers` - Comma-separated layer names
- `bbox` - Bounding box (depends on CRS axis order)
- `width`, `height` - Image dimensions
- `crs` - Coordinate reference system (EPSG:4326, EPSG:3857, etc.)
- `format` - Image format (image/png, image/jpeg, image/webp)
- `styles` - Comma-separated style names (optional)
- `transparent` - true/false for PNG transparency

**Example:**
```bash
curl "http://localhost:8080/wms?service=WMS&version=1.3.0&request=GetMap&layers=cities&bbox=-180,-90,180,90&width=800&height=600&crs=EPSG:4326&format=image/png" -o map.png
```

### GetFeatureInfo
```
GET /wms?service=WMS&version=1.3.0&request=GetFeatureInfo&layers={layer}&query_layers={layer}&bbox={bbox}&width={w}&height={h}&crs={crs}&i={x}&j={y}&info_format=application/json
```

Returns feature information for a specific pixel location.

## Geoservices REST a.k.a. Esri REST API

### Service Directory
```
GET /rest/services?f=json
```

### FeatureServer
```
GET /rest/services/{service}/FeatureServer?f=json
```

Returns service metadata including layers.

### Layer Metadata
```
GET /rest/services/{service}/FeatureServer/{layerId}?f=json
```

### Query Features
```
POST /rest/services/{service}/FeatureServer/{layerId}/query
Content-Type: application/x-www-form-urlencoded

where=1=1&outFields=*&f=json
```

**Parameters:**
- `where` - SQL WHERE clause
- `geometry` - Spatial filter geometry
- `geometryType` - esriGeometryPoint, esriGeometryEnvelope, etc.
- `spatialRel` - esriSpatialRelIntersects, esriSpatialRelContains, etc.
- `outFields` - Comma-separated field names or *
- `returnGeometry` - true/false
- `f` - json, geojson, pbf

### Apply Edits
```
POST /rest/services/{service}/FeatureServer/{layerId}/applyEdits
Content-Type: application/json

{
  "adds": [...],
  "updates": [...],
  "deletes": [...]
}
```

## STAC 1.0

### Catalog Root
```
GET /stac
```

### Collections
```
GET /stac/collections
```

### Collection Metadata
```
GET /stac/collections/{collectionId}
```

### Search Items
```
POST /stac/search
Content-Type: application/json

{
  "collections": ["collection-id"],
  "bbox": [-180, -90, 180, 90],
  "datetime": "2025-01-01T00:00:00Z/..",
  "limit": 10
}
```

## Admin API

All admin endpoints require authentication and `RequireAdministrator` role.

### Runtime Configuration
```
GET /admin/config/status
PATCH /admin/config/services/{protocol}
```

### Logging Configuration
```
GET /admin/logging/categories
PATCH /admin/logging/categories/{category}
DELETE /admin/logging/categories/{category}
```

### Tracing Configuration
```
GET /admin/observability/tracing
PATCH /admin/observability/tracing/exporter
PATCH /admin/observability/tracing/endpoint
PATCH /admin/observability/tracing/sampling
POST /admin/observability/tracing/test
```

### Health Checks
```
GET /health/live       # Liveness probe
GET /health/ready      # Readiness probe
GET /health/startup    # Startup probe
```

## Authentication

### Local Mode (JWT)
```
POST /auth/login
Content-Type: application/json

{
  "username": "admin",
  "password": "password"
}
```

Returns JWT token for subsequent requests:
```bash
curl -H "Authorization: Bearer {token}" http://localhost:8080/ogc/collections
```

### API Key Mode
```bash
curl -H "X-API-Key: {key}" http://localhost:8080/ogc/collections
# or
curl "http://localhost:8080/ogc/collections?api_key={key}"
```

## Export Formats

Supported output formats via `f` parameter:

| Format | Extension | MIME Type | Notes |
|--------|-----------|-----------|-------|
| GeoJSON | .geojson | application/geo+json | Default |
| GeoJSON-Seq | .geojsons | application/geo+json-seq | Streaming |
| CSV | .csv | text/csv | With WKT geometry |
| KML | .kml | application/vnd.google-earth.kml+xml | |
| KMZ | .kmz | application/vnd.google-earth.kmz | Compressed |
| Shapefile | .zip | application/zip | With .prj, .dbf, .shx |
| GeoPackage | .gpkg | application/geopackage+sqlite3 | Async for large datasets |
| FlatGeobuf | .fgb | application/octet-stream | Cloud-optimized |
| GeoParquet | .parquet | application/vnd.apache.parquet | Analytics-ready |
| PMTiles | .pmtiles | application/vnd.pmtiles | Serverless vector tiles |
| GML | .gml | application/gml+xml | GML 3.2 |

## Error Responses

All errors follow OGC API and RFC 7807 Problem Details:

```json
{
  "type": "https://honua.io/errors/invalid-parameter",
  "title": "Invalid Parameter",
  "status": 400,
  "detail": "The 'bbox' parameter is invalid",
  "instance": "/ogc/collections/cities/items"
}
```

**Common HTTP Status Codes:**
- `200 OK` - Success
- `400 Bad Request` - Invalid parameters
- `401 Unauthorized` - Authentication required
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Resource not found
- `500 Internal Server Error` - Server error
- `503 Service Unavailable` - Service temporarily unavailable

## Rate Limiting

Responses include rate limit headers:

```
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
X-RateLimit-Reset: 1234567890
```

When rate limited, returns `429 Too Many Requests`.

---

For detailed examples, see the [Quickstart Guide](../quickstart/).
