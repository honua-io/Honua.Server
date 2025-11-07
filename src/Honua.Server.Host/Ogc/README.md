# OGC API Implementation

This module provides comprehensive support for Open Geospatial Consortium (OGC) standards, enabling standardized access to geospatial features, tiles, and coverages.

## Supported OGC Standards

### OGC API - Features (Primary Implementation)
**Specification Version:** 1.0 Core + 3.0 Extensions

**Implemented Conformance Classes:**
- `ogcapi-features-1/1.0/conf/core` - Core functionality
- `ogcapi-features-1/1.0/conf/geojson` - GeoJSON output format
- `ogcapi-features-1/1.0/conf/oas30` - OpenAPI 3.0 definition
- `ogcapi-features-3/1.0/conf/search` - Cross-collection search
- `ogcapi-features-3/1.0/conf/filter` - Advanced filtering
- `ogcapi-features-3/1.0/conf/filter-cql2-json` - CQL2-JSON filter language
- `ogcapi-features-3/1.0/conf/features-filter` - Feature filtering
- `ogcapi-features-3/1.0/conf/spatial-operators` - Spatial query operators
- `ogcapi-features-3/1.0/conf/temporal-operators` - Temporal query operators

**Features:**
- Collection discovery and metadata
- Feature retrieval with spatial/temporal filtering
- CQL2-JSON advanced filtering
- GeoJSON, FlatGeobuf, GML, CSV output formats
- HTML rendering for browser access
- CRUD operations (Create, Read, Update, Delete)
- Attachment management
- Multi-CRS support
- Queryables schema generation
- Pagination and result limiting

### OGC API - Tiles
**Specification Version:** 1.0

**Implemented Conformance Classes:**
- `ogcapi-tiles-1/1.0/conf/core` - Core tile functionality
- `ogcapi-tiles-1/1.0/conf/tileset` - Tileset metadata
- `ogcapi-tiles-1/1.0/conf/tilesets-list` - Multiple tilesets per collection

**Features:**
- Vector and raster tile serving
- MVT (Mapbox Vector Tiles) format
- Multiple tile matrix sets (WebMercatorQuad, WorldCRS84Quad, etc.)
- TileJSON metadata
- Tile caching with ETags
- Style-based tile rendering

### Legacy OGC Web Services (Stub Implementations)

> **Note:** These are stub implementations marked as obsolete. Full implementations remain in `OgcSharedHandlers.cs` pending Phase 2 refactoring.

#### WMS (Web Map Service)
- **Version:** 1.3.0
- **Operations:** GetCapabilities, GetMap, GetFeatureInfo
- **Status:** Stub implementation only
- **File:** `WmsHandlers.cs`

#### WCS (Web Coverage Service)
- **Version:** 2.0.1
- **Operations:** GetCapabilities, DescribeCoverage, GetCoverage
- **Status:** Stub implementation only
- **File:** `WcsHandlers.cs`

#### WMTS (Web Map Tile Service)
- **Version:** 1.0.0
- **Status:** Interface defined in `IWmtsHandler.cs`

## Architecture

### Refactoring Status

⚠️ **Important:** This module is undergoing active refactoring. See [REFACTORING_PLAN_OGC.md](/home/user/Honua.Server/REFACTORING_PLAN_OGC.md) for details.

**Current State:**
- `OgcSharedHandlers.cs` - 3,235 lines containing most functionality (being decomposed)
- Service-based architecture being implemented via dependency injection

**Completed Extractions:**
- `OgcCrsService` - CRS resolution and validation
- `OgcLinkBuilder` - Link generation with query parameters
- `OgcParameterParser` - Query parameter parsing
- `OgcCollectionResolver` - Collection resolution and validation

**In Progress:**
- `IOgcFeaturesQueryHandler` - Features query operations
- `IOgcFeaturesRenderingHandler` - HTML rendering
- `IOgcTilesHandler` - Tiles operations

### Key Components

#### Handlers
- **`OgcLandingHandlers`** - Landing page, collections list, conformance declaration
- **`OgcFeaturesHandlers`** - Feature CRUD operations
  - `OgcFeaturesHandlers.Items.cs` - Feature retrieval and listing
  - `OgcFeaturesHandlers.Mutations.cs` - Create, update, delete operations
  - `OgcFeaturesHandlers.Search.cs` - Cross-collection search
  - `OgcFeaturesHandlers.Styles.cs` - Style management
  - `OgcFeaturesHandlers.Attachments.cs` - Attachment handling
- **`OgcTilesHandlers`** - Tile serving and metadata
- **`OgcStylesHandlers`** - Style CRUD and versioning
- **`OgcSharedHandlers`** - Shared utilities (being refactored)

#### Services (Refactored)
Located in `/Ogc/Services/`:
- **`OgcCrsService`** - Coordinate reference system handling
- **`OgcLinkBuilder`** - Hypermedia link generation
- **`OgcParameterParser`** - Query string parsing and validation
- **`OgcCollectionResolver`** - Collection ID resolution with security validation

#### Supporting Components
- **`OgcQueryParser`** - OGC filter parsing (CQL, bbox, datetime)
- **`OgcFeatureCollectionWriter`** - Streaming GeoJSON writer
- **`FlatGeobufStreamingWriter`** - FlatGeobuf binary format writer
- **`GmlStreamingWriter`** - GML XML format writer
- **`OgcCapabilitiesBuilder`** - WMS/WCS capabilities document generation
- **`OgcCacheHeaderService`** - HTTP caching (ETags, Cache-Control)
- **`OgcTemporalParameterValidator`** - Temporal query validation
- **`OgcTileMatrixHelper`** - Tile matrix calculations

### Link Generation

OGC APIs are hypermedia-driven, using links for navigation:

```csharp
// Link generation with absolute URLs
var link = OgcSharedHandlers.BuildLink(
    request,
    "/ogc/collections/my-collection",
    "self",
    "application/json",
    "Collection metadata"
);
```

**Link Relationships (`rel`):**
- `self` - Current resource
- `alternate` - Alternate representation
- `items` - Feature items
- `data` - Data endpoint
- `conformance` - Conformance declaration
- `service-desc` - OpenAPI definition
- `service-doc` - Documentation

### CRS Support

**Default CRS:** `http://www.opengis.net/def/crs/OGC/1.3/CRS84` (WGS 84 lon/lat)

**Supported CRS:**
- CRS84 (OGC:CRS84)
- EPSG:4326 (WGS 84)
- EPSG:3857 (Web Mercator)
- Custom CRS per collection

**CRS Resolution:**
```csharp
var crsService = new OgcCrsService();
var resolvedCrs = crsService.ResolveCrs("EPSG:3857");
```

### Format Rendering

**Supported Output Formats:**

| Format | Media Type | File Extension | Use Case |
|--------|-----------|----------------|----------|
| GeoJSON | `application/geo+json` | `.geojson` | Default, web-friendly |
| FlatGeobuf | `application/flatgeobuf` | `.fgb` | Binary, efficient streaming |
| GML | `application/gml+xml` | `.gml` | OGC standard XML |
| CSV | `text/csv` | `.csv` | Spreadsheet import |
| HTML | `text/html` | `.html` | Browser viewing |
| GeoPackage | `application/geopackage+sqlite3` | `.gpkg` | Desktop GIS |
| Shapefile | `application/x-shapefile` | `.shp` | Legacy GIS format |
| GeoArrow | `application/vnd.apache.arrow.stream` | `.arrows` | High-performance analytics |

**Content Negotiation:**
```bash
# Request HTML
curl -H "Accept: text/html" https://api.example.com/ogc/collections

# Request GeoJSON (default)
curl https://api.example.com/ogc/collections/roads/items

# Request FlatGeobuf
curl https://api.example.com/ogc/collections/roads/items?f=fgb
```

## API Endpoints

### Landing and Discovery

| Endpoint | Method | Description | Auth |
|----------|--------|-------------|------|
| `/ogc` | GET | Landing page with catalog info | Anonymous |
| `/ogc/conformance` | GET | OGC conformance classes | Anonymous |
| `/ogc/api` | GET | OpenAPI 3.0 definition | Anonymous |
| `/ogc/collections` | GET | List all collections | Anonymous |
| `/ogc/collections/{collectionId}` | GET | Collection metadata | Anonymous |

### Features (OGC API - Features)

| Endpoint | Method | Description | Auth |
|----------|--------|-------------|------|
| `/ogc/collections/{collectionId}/items` | GET | List features | Anonymous |
| `/ogc/collections/{collectionId}/items` | POST | Create feature(s) | DataPublisher |
| `/ogc/collections/{collectionId}/items/{featureId}` | GET | Get single feature | Viewer |
| `/ogc/collections/{collectionId}/items/{featureId}` | PUT | Replace feature | DataPublisher |
| `/ogc/collections/{collectionId}/items/{featureId}` | PATCH | Update feature | DataPublisher |
| `/ogc/collections/{collectionId}/items/{featureId}` | DELETE | Delete feature | DataPublisher |
| `/ogc/collections/{collectionId}/queryables` | GET | Queryable properties schema | Viewer |

### Search

| Endpoint | Method | Description | Auth |
|----------|--------|-------------|------|
| `/ogc/search` | GET | Cross-collection search | Viewer |
| `/ogc/search` | POST | Advanced search with CQL2-JSON | Viewer |

### Tiles (OGC API - Tiles)

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/ogc/tileMatrixSets` | GET | List available tile matrix sets |
| `/ogc/tileMatrixSets/{tileMatrixSetId}` | GET | Tile matrix set definition |
| `/ogc/collections/{collectionId}/tiles` | GET | Available tilesets for collection |
| `/ogc/collections/{collectionId}/tiles/{tileMatrixSetId}/{z}/{y}/{x}` | GET | Get single tile |

### Styles

| Endpoint | Method | Description | Auth |
|----------|--------|-------------|------|
| `/ogc/styles` | GET | List styles | Viewer |
| `/ogc/styles` | POST | Create style | DataPublisher |
| `/ogc/styles/{styleId}` | GET | Get style | Viewer |
| `/ogc/styles/{styleId}` | PUT | Update style | DataPublisher |
| `/ogc/styles/{styleId}` | DELETE | Delete style | DataPublisher |
| `/ogc/styles/{styleId}/history` | GET | Style version history | Viewer |
| `/ogc/styles/{styleId}/versions/{version}` | GET | Specific style version | Viewer |
| `/ogc/styles/validate` | POST | Validate style definition | Viewer |

### Attachments

| Endpoint | Method | Description | Auth |
|----------|--------|-------------|------|
| `/ogc/collections/{collectionId}/items/{featureId}/attachments/{attachmentId}` | GET | Download attachment | Viewer |

## Configuration

### CRS Configuration

```json
{
  "Ogc": {
    "DefaultCrs": "http://www.opengis.net/def/crs/OGC/1.3/CRS84",
    "SupportedCrs": [
      "http://www.opengis.net/def/crs/OGC/1.3/CRS84",
      "http://www.opengis.net/def/crs/EPSG/0/4326",
      "http://www.opengis.net/def/crs/EPSG/0/3857"
    ]
  }
}
```

### Format Options

```json
{
  "Export": {
    "MaxFeaturesPerRequest": 10000,
    "DefaultPageSize": 100,
    "SupportedFormats": ["geojson", "fgb", "gml", "csv", "html"]
  }
}
```

### Cache Configuration

```csharp
// In OgcCacheHeaderService
services.AddSingleton<OgcCacheHeaderService>(provider =>
    new OgcCacheHeaderService(new CacheHeaderOptions
    {
        MetadataCacheDuration = TimeSpan.FromMinutes(5),
        FeatureCacheDuration = TimeSpan.FromMinutes(1),
        TileCacheDuration = TimeSpan.FromHours(1)
    })
);
```

## Usage Examples

### Retrieve Collections

**Request:**
```bash
GET /ogc/collections
Accept: application/json
```

**Response:**
```json
{
  "collections": [
    {
      "id": "service1:roads",
      "title": "Roads",
      "description": "Road network",
      "itemType": "feature",
      "extent": {
        "spatial": {
          "bbox": [[-180, -90, 180, 90]]
        },
        "temporal": {
          "interval": [["2020-01-01T00:00:00Z", "2024-12-31T23:59:59Z"]]
        }
      },
      "crs": [
        "http://www.opengis.net/def/crs/OGC/1.3/CRS84"
      ],
      "links": [
        {
          "href": "https://api.example.com/ogc/collections/service1:roads",
          "rel": "self",
          "type": "application/json"
        },
        {
          "href": "https://api.example.com/ogc/collections/service1:roads/items",
          "rel": "items",
          "type": "application/geo+json"
        }
      ]
    }
  ]
}
```

### Query Features with Filters

**Request:**
```bash
GET /ogc/collections/service1:roads/items?
    bbox=-122.5,37.7,-122.3,37.9&
    datetime=2024-01-01T00:00:00Z/2024-12-31T23:59:59Z&
    limit=10&
    properties=name,type
```

**Response (GeoJSON):**
```json
{
  "type": "FeatureCollection",
  "features": [
    {
      "type": "Feature",
      "id": "road-1",
      "geometry": {
        "type": "LineString",
        "coordinates": [[-122.4, 37.8], [-122.35, 37.85]]
      },
      "properties": {
        "name": "Main Street",
        "type": "primary",
        "datetime": "2024-06-15T10:00:00Z"
      }
    }
  ],
  "links": [
    {
      "href": "https://api.example.com/ogc/collections/service1:roads/items?limit=10&offset=10",
      "rel": "next",
      "type": "application/geo+json"
    }
  ]
}
```

### Advanced Search with CQL2-JSON

**Request:**
```bash
POST /ogc/search
Content-Type: application/json

{
  "collections": ["service1:roads"],
  "filter": {
    "op": "and",
    "args": [
      {
        "op": "=",
        "args": [{"property": "type"}, "primary"]
      },
      {
        "op": "s_intersects",
        "args": [
          {"property": "geometry"},
          {
            "type": "Polygon",
            "coordinates": [[
              [-122.5, 37.7],
              [-122.3, 37.7],
              [-122.3, 37.9],
              [-122.5, 37.9],
              [-122.5, 37.7]
            ]]
          }
        ]
      }
    ]
  },
  "limit": 50
}
```

### Create Features

**Request:**
```bash
POST /ogc/collections/service1:roads/items
Content-Type: application/geo+json
Authorization: Bearer {token}

{
  "type": "FeatureCollection",
  "features": [
    {
      "type": "Feature",
      "geometry": {
        "type": "Point",
        "coordinates": [-122.4, 37.8]
      },
      "properties": {
        "name": "New Location",
        "category": "poi"
      }
    }
  ]
}
```

**Response:**
```json
{
  "created": 1,
  "failed": 0,
  "features": [
    {
      "id": "generated-id-123",
      "status": "created"
    }
  ]
}
```

### Request Tiles

**Request:**
```bash
GET /ogc/collections/service1:imagery/tiles/WebMercatorQuad/12/656/1582
Accept: application/vnd.mapbox-vector-tile
```

**Response:** Binary MVT tile data

## Standards Compliance

### OGC API - Features 1.0 Core
✅ Fully compliant with:
- Part 1: Core
- Part 2: Coordinate Reference Systems by Reference
- GeoJSON encoding
- HTML encoding
- OpenAPI 3.0 definition

### OGC API - Features 3.0 Extensions
✅ Implements:
- Search (cross-collection queries)
- Filter (CQL2-JSON filtering)
- Spatial operators (s_intersects, s_within, s_contains, etc.)
- Temporal operators (t_intersects, t_before, t_after, etc.)

⚠️ Partial implementation:
- CQL2-JSON: Subset of operators (no arithmetic, limited spatial functions)

### OGC API - Tiles 1.0
✅ Core compliance:
- Tile matrix sets
- Vector tiles (MVT)
- Raster tiles
- TileJSON metadata

### Legacy Standards (Stub Only)
⚠️ WMS 1.3.0 - Stub implementation
⚠️ WCS 2.0.1 - Stub implementation
⚠️ WFS - Interface only, not implemented

## Security

**Authorization Policies:**
- **Anonymous** - Landing page, conformance, collections list
- **RequireViewer** - Read operations (GET features, tiles)
- **RequireDataPublisher** - Write operations (POST, PUT, PATCH, DELETE)

**Input Validation:**
- Collection IDs validated for path traversal attacks
- CQL2-JSON depth limits to prevent DoS
- Geometry complexity limits
- Request size limits

## Performance Optimizations

- **Streaming:** Large feature collections streamed via `OgcFeatureCollectionWriter`
- **Caching:** ETags and Cache-Control headers for metadata and tiles
- **Indexing:** Spatial and temporal indexes in underlying data stores
- **Pagination:** Cursor-based pagination for large result sets
- **FlatGeobuf:** Binary format for efficient data transfer

## Related Documentation

- [REFACTORING_PLAN_OGC.md](/home/user/Honua.Server/REFACTORING_PLAN_OGC.md) - Ongoing refactoring details
- [OGC API - Features Specification](http://docs.ogc.org/is/17-069r3/17-069r3.html)
- [OGC API - Tiles Specification](http://docs.ogc.org/is/20-057/20-057.html)
- [CQL2 Specification](https://docs.ogc.org/DRAFTS/21-065.html)

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0
