# Honua Geospatial Server - Capabilities Reference

**Version:** 1.0 MVP
**Generated:** 2025-10-07
**Source:** Code analysis of commit `ab44774`

> **Note:** This document is generated from actual codebase inspection. All capabilities listed are verified to be implemented in code.

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [OGC Standards](#ogc-standards)
3. [Industry APIs](#industry-apis)
4. [Data Providers](#data-providers)
5. [Export Formats](#export-formats)
6. [Authentication & Security](#authentication--security)
7. [Advanced Features](#advanced-features)
8. [Known Gaps & Limitations](#known-gaps--limitations)

---

## Executive Summary

Honua is a production-ready geospatial server implementing:

- **7 OGC Standards** (OGC API Features, Tiles, WFS 2.0, WMS 1.3, WMTS 1.0, CSW 2.0.2, WCS 2.0.1)
- **STAC 1.0** (Full catalog, collections, items, search)
- **Geoservices REST a.k.a. Esri REST API** (FeatureServer, MapServer, ImageServer, GeometryServer)
- **OData v4** with spatial extensions
- **10 Data Providers** (4 open source + 6 enterprise)
- **9+ Export Formats**
- **Full CRUD** across OGC API Features, WFS-T, Geoservices REST a.k.a. Esri REST
- **RBAC** with 4 roles
- **Temporal support** across protocols
- **Vector tiles** with clustering
- **Cloud storage** integration

**Key Differentiator:** Full transactional WFS 2.0 + modern OGC APIs + Esri compatibility

---

## OGC Standards

All OGC standard implementations verified via handler code inspection.

### ✅ OGC API - Features 1.0 (FULLY IMPLEMENTED)

**Evidence:** `src/Honua.Server.Host/Ogc/OgcFeaturesHandlers.cs` (1202 lines)

**Conformance Classes:**
- Core
- GeoJSON
- OpenAPI 3.0
- Filter (CQL2-text, CQL2-json)
- Search
- Sorting
- Transactions

**Operations:**

| Operation | Method | Endpoint | Auth | Notes |
|-----------|--------|----------|------|-------|
| Landing page | GET | `/ogc` | - | Service metadata with links |
| Conformance | GET | `/ogc/conformance` | - | Conformance classes |
| API definition | GET | `/ogc/api` | - | OpenAPI 3.0 document |
| Collections list | GET | `/ogc/collections` | Viewer | Paginated collection list |
| Collection metadata | GET | `/ogc/collections/{id}` | Viewer | Single collection details |
| Queryables | GET | `/ogc/collections/{id}/queryables` | Viewer | Queryable properties schema |
| Query features | GET | `/ogc/collections/{id}/items` | Viewer | CQL2, bbox, datetime, sortby, limit/offset |
| Get feature | GET | `/ogc/collections/{id}/items/{featureId}` | Viewer | Single feature by ID |
| Create feature | POST | `/ogc/collections/{id}/items` | DataPublisher | Create new feature |
| Replace feature | PUT | `/ogc/collections/{id}/items/{featureId}` | DataPublisher | Full feature replacement |
| Update feature | PATCH | `/ogc/collections/{id}/items/{featureId}` | DataPublisher | Partial feature update |
| Delete feature | DELETE | `/ogc/collections/{id}/items/{featureId}` | DataPublisher | Delete feature |
| Cross-collection search | GET/POST | `/ogc/search` | Viewer | Search across multiple collections |
| Styles list | GET | `/ogc/styles` | Viewer | Available styles |
| Collection styles | GET | `/ogc/collections/{id}/styles` | Viewer | Styles for collection |

**Advanced Features:**
- **CQL2 Filtering:** Text and JSON syntax
- **CRS Negotiation:** Accept-Crs and Content-Crs headers
- **ETag Support:** If-Match for conditional updates
- **Temporal Queries:** datetime parameter (single instant or interval)
- **Multiple Formats:** GeoJSON, HTML, KML, KMZ, TopoJSON, GeoPackage, Shapefile

---

### ✅ OGC API - Tiles 1.0 (FULLY IMPLEMENTED)

**Evidence:** `src/Honua.Server.Host/Ogc/OgcTilesHandlers.cs`

**Operations:**

| Operation | Endpoint | Format | Notes |
|-----------|----------|--------|-------|
| Tile matrix sets | `/ogc/tileMatrixSets` | JSON | WebMercatorQuad, WorldCRS84Quad |
| Tile matrix set | `/ogc/tileMatrixSets/{id}` | JSON | TMS definition |
| Collection tilesets | `/ogc/collections/{id}/tiles` | JSON | Available tilesets |
| Tileset metadata | `/ogc/collections/{id}/tiles/{tilesetId}` | JSON | Tileset definition |
| TileJSON | `/ogc/collections/{id}/tiles/{tilesetId}/tilejson` | JSON | TileJSON 3.0.0 |
| Tile matrix | `/ogc/collections/{id}/tiles/{tilesetId}/{tileMatrixSetId}` | JSON | Matrix metadata |
| Get tile | `/ogc/collections/{id}/tiles/{tilesetId}/{tms}/{z}/{y}/{x}` | MVT/PNG | Individual tile |

**Supported:**
- **Vector tiles:** MVT/PBF format (PostGIS ST_AsMVT)
- **Raster tiles:** PNG format
- **Temporal filtering:** datetime parameter
- **Style selection:** styleId parameter
- **Transparency:** Transparent tiles support

---

### ✅ WFS 2.0 (FULLY TRANSACTIONAL)

**Evidence:** `src/Honua.Server.Host/Wfs/WfsHandlers.cs`, `src/Honua.Server.Host/Wfs/IWfsLockManager.cs`

**Operations:**

| Operation | Support | Notes |
|-----------|---------|-------|
| GetCapabilities | ✅ | Service metadata, feature types |
| DescribeFeatureType | ✅ | XML Schema for feature types |
| GetFeature | ✅ | Query with filters, bbox, sorting |
| GetFeatureWithLock | ✅ | Query and lock features |
| LockFeature | ✅ | Acquire locks for editing |
| Transaction | ✅ | Insert, Update, Delete operations |

**Advanced Features:**
- **Locking:** Feature-level locks with lock manager
- **Stored Queries:** Including mandatory GetFeatureById
- **Output Formats:** GML 3.2, CSV, Shapefile
- **Filter Encoding:** FES 2.0 XML filters
- **ResultType:** hits (count only) or results
- **Conformance Classes:**
  - Simple WFS
  - Transactional WFS (Insert, Update, Delete)
  - Locking WFS

**File References:**
- Lock manager: `src/Honua.Server.Host/Wfs/InMemoryWfsLockManager.cs`
- Filter parser: `src/Honua.Server.Host/Wfs/Filters/XmlFilterParser.cs`

---

### ✅ WMS 1.3.0 (FULLY IMPLEMENTED)

**Evidence:** `src/Honua.Server.Host/Wms/WmsHandlers.cs`

**Operations:**

| Operation | Support | Formats | Notes |
|-----------|---------|---------|-------|
| GetCapabilities | ✅ | XML | Service metadata, layers |
| GetMap | ✅ | PNG, JPEG | Render map image |
| GetFeatureInfo | ✅ | JSON, XML, HTML, Text | Query features at point |
| GetLegendGraphic | ✅ | PNG | Generate legend image |

**Features:**
- **CRS Support:** Transformation between coordinate systems
- **TIME Parameter:** Temporal filtering
- **Layer Combination:** Multi-layer requests
- **Styles:** SLD and named styles
- **Transparency:** Transparent backgrounds

---

### ✅ WMTS 1.0.0 (FULLY IMPLEMENTED)

**Evidence:** `src/Honua.Server.Host/Wmts/WmtsHandlers.cs`

**Operations:**

| Operation | Support | Binding | Notes |
|-----------|---------|---------|-------|
| GetCapabilities | ✅ | KVP, RESTful | Service capabilities |
| GetTile | ✅ | KVP, RESTful | Individual tiles |
| GetFeatureInfo | ❌ | - | Returns OperationNotSupported |

**Conformance Classes:**
- `http://www.opengis.net/spec/wmts/1.0/conf/core`
- `http://www.opengis.net/spec/wmts/1.0/conf/getcapabilities`
- `http://www.opengis.net/spec/wmts/1.0/conf/gettile`
- `http://www.opengis.net/spec/wmts/1.0/conf/kvp`

**Features:**
- **Tile Matrix Sets:** WebMercatorQuad, WorldCRS84Quad
- **Formats:** PNG
- **Caching:** Integration with raster cache

**Known Gap:** GetFeatureInfo not implemented (line 378-382)

---

### ✅ CSW 2.0.2 (Catalog Service - READ ONLY)

**Evidence:** `src/Honua.Server.Host/Csw/CswHandlers.cs`

**Operations:**

| Operation | Support | Output Formats | Notes |
|-----------|---------|----------------|-------|
| GetCapabilities | ✅ | XML | Service capabilities |
| GetRecords | ✅ | ISO 19139, Dublin Core | Search catalog |
| GetRecordById | ✅ | ISO 19139, Dublin Core | Retrieve specific record |

**Features:**
- **ISO 19115 metadata** mapped to ISO 19139 XML
- **Dublin Core** simple metadata
- **Filter support** for catalog searches

**File References:**
- ISO mapper: `src/Honua.Server.Host/Csw/Iso19139Mapper.cs`
- XML parser: `src/Honua.Server.Host/Csw/CswXmlParser.cs`

**Known Gap:** No Transaction support (Insert/Update/Delete metadata), no Harvest

---

### ✅ WCS 2.0.1 (Web Coverage Service)

**Evidence:** `src/Honua.Server.Host/Wcs/WcsHandlers.cs`

**Operations:**

| Operation | Support | Formats | Notes |
|-----------|---------|---------|-------|
| GetCapabilities | ✅ | XML | Service capabilities |
| DescribeCoverage | ✅ | XML | Coverage metadata |
| GetCoverage | ✅ | TIFF, PNG | Retrieve coverage data |

**Features:**
- **CRS support**
- **Subsetting support**
- **Raster rendering integration**

---

## Industry APIs

### ✅ STAC 1.0 (SpatioTemporal Asset Catalog)

**Evidence:** `src/Honua.Server.Host/Stac/` controllers

**Endpoints:**

| Endpoint | Support | Notes |
|----------|---------|-------|
| `/stac` | ✅ | Catalog root |
| `/stac/conformance` | ✅ | Conformance classes |
| `/stac/collections` | ✅ | List collections |
| `/stac/collections/{id}` | ✅ | Collection metadata |
| `/stac/collections/{id}/items` | ✅ | List items |
| `/stac/collections/{id}/items/{itemId}` | ✅ | Get item |
| `/stac/search` (GET) | ✅ | Search with query params |
| `/stac/search` (POST) | ✅ | Search with JSON body |

**Features:**
- **Spatial filtering:** bbox parameter
- **Temporal filtering:** datetime parameter
- **Collection filtering:** collections parameter
- **ID filtering:** ids parameter
- **Pagination:** Continuation tokens
- **Synchronization:** `StacCatalogSynchronizationHostedService`

**Known Gap:** Read-only (no POST/PUT/DELETE for items)

---

### ✅ Esri ArcGIS REST API (COMPREHENSIVE)

All Geoservices REST a.k.a. Esri REST implementations verified in `src/Honua.Server.Host/GeoservicesREST/` directory.

#### FeatureServer (FULL CRUD + ATTACHMENTS)

**Evidence:** `GeoservicesRESTFeatureServerController.cs` (3000+ lines)

**Core Operations:**

| Operation | Method | Endpoint | Auth | Notes |
|-----------|--------|----------|------|-------|
| Service metadata | GET | `/rest/services/{folder}/{service}/FeatureServer` | Viewer | Service definition |
| Layer metadata | GET | `/rest/services/.../FeatureServer/{layer}` | Viewer | Layer properties |
| Query features | GET/POST | `/rest/services/.../FeatureServer/{layer}/query` | Viewer | Where, geometry, spatial filters |
| Apply edits (batch) | POST | `/rest/services/.../FeatureServer/{layer}/applyEdits` | DataPublisher | Adds, updates, deletes |
| Add features | POST | `/rest/services/.../FeatureServer/{layer}/addFeatures` | DataPublisher | Create features |
| Update features | POST | `/rest/services/.../FeatureServer/{layer}/updateFeatures` | DataPublisher | Update features |
| Delete features | POST | `/rest/services/.../FeatureServer/{layer}/deleteFeatures` | DataPublisher | Delete features |
| Query related | GET | `/rest/services/.../FeatureServer/{layer}/queryRelatedRecords` | Viewer | Related records |

**Attachment Operations:**

| Operation | Method | Endpoint | Auth | Notes |
|-----------|--------|----------|------|-------|
| Query attachments | GET | `/rest/services/.../FeatureServer/{layer}/queryAttachments` | Viewer | List attachments |
| Add attachment | POST | `/rest/services/.../FeatureServer/{layer}/addAttachment` | DataPublisher | Upload file |
| Update attachment | POST | `/rest/services/.../FeatureServer/{layer}/updateAttachment` | DataPublisher | Replace file |
| Delete attachments | POST | `/rest/services/.../FeatureServer/{layer}/deleteAttachments` | DataPublisher | Remove files |
| Download attachment | GET | `/rest/services/.../FeatureServer/{layer}/{oid}/attachments/{id}` | Viewer | Download file |

**Features:**
- **Formats:** GeoServices JSON format, GeoJSON, Shapefile
- **Spatial queries:** geometry + spatialRel (intersects, contains, etc.)
- **Related records:** Query relationships
- **Cloud storage:** S3/Azure Blob integration for attachments

#### MapServer

**Evidence:** `GeoservicesRESTMapServerController.cs`

**Operations:**

| Operation | Endpoint | Notes |
|-----------|----------|-------|
| Service metadata | `/rest/services/{folder}/{service}/MapServer` | Service info |
| Layer metadata | `/rest/services/.../MapServer/{layer}` | Layer details |
| All layers | `/rest/services/.../MapServer/layers` | All layer info |
| Legend | `/rest/services/.../MapServer/legend` | Legend data |
| Export map | `/rest/services/.../MapServer/export` | Rendered image |
| Identify | `/rest/services/.../MapServer/identify` | Query at point |
| Find | `/rest/services/.../MapServer/find` | Text search |
| Generate KML | `/rest/services/.../MapServer/generateKml` | KML export |

#### ImageServer

**Evidence:** `GeoservicesRESTImageServerController.cs`

**Operations:**

| Operation | Endpoint | Notes |
|-----------|----------|-------|
| Service metadata | `/rest/services/{folder}/{service}/ImageServer` | Service info |
| Export image | `/rest/services/.../ImageServer/exportImage` | Rendered image |
| Identify | `/rest/services/.../ImageServer/identify` | Query pixel values |
| Get samples | `/rest/services/.../ImageServer/getSamples` | Sample pixel data |
| Compute histograms | `/rest/services/.../ImageServer/computeHistograms` | Image statistics |
| Raster attributes | `/rest/services/.../ImageServer/getRasterAttributes` | Attribute table |
| Raster info | `/rest/services/.../ImageServer/getRasterInfo` | Metadata |

#### GeometryServer

**Evidence:** `GeoservicesRESTGeometryServerController.cs`

Geometry operations service (project, buffer, simplify, union, intersect, difference, distance, etc.)

#### Services Directory

**Evidence:** `ServicesDirectoryController.cs`

HTML browsing interface for REST services

---

### ✅ OData v4 (WITH CRUD)

**Evidence:** `src/Honua.Server.Host/OData/DynamicODataController.cs`

**Implemented Methods:**

| Method | Operation | Line Reference | Auth | Notes |
|--------|-----------|----------------|------|-------|
| GET | Query entities | Line 72 | Viewer | $filter, $select, $orderby, $top, $skip, $count, $expand |
| POST | Create entity | Line 250 | DataPublisher | Create new record |
| PUT | Replace entity | Line 277 | DataPublisher | Full replacement |
| PATCH | Update entity | Line 318 | DataPublisher | Partial update |
| DELETE | Delete entity | Line 366 | DataPublisher | Delete record |

**Advanced Features:**
- **Dynamic EDM Model:** Per-layer model generation (`DynamicEdmModelBuilder.cs`)
- **Spatial Functions:** `geo.intersects()` with database pushdown
- **GeoJSON Geometry:** Spatial property serialization
- **Query Options:** Full OData query syntax support
- **Model Caching:** `ODataModelCache.cs` for performance

**Endpoints:**
- `/odata` - Service root
- `/odata/$metadata` - EDM metadata
- `/odata/{EntitySet}` - Entity operations

**File References:**
- Controller: `src/Honua.Server.Host/OData/DynamicODataController.cs`
- EDM builder: `src/Honua.Server.Host/OData/DynamicEdmModelBuilder.cs`
- Geo filter: `src/Honua.Server.Host/OData/ODataGeoFilterContext.cs`

---

### ✅ Carto SQL API (IMPLEMENTED)

**Evidence:** `src/Honua.Server.Host/Carto/`

**Endpoints:**

| Endpoint | Support | Notes |
|----------|---------|-------|
| `/carto` | ✅ | Landing page |
| `/carto/api/v3/datasets` | ✅ | List datasets |
| `/carto/api/v3/datasets/{id}` | ✅ | Dataset metadata |
| `/carto/api/v3/datasets/{id}/schema` | ✅ | Dataset schema |
| `/carto/api/v3/sql` (GET) | ✅ | Execute SQL query |
| `/carto/api/v3/sql` (POST) | ✅ | Execute SQL query (body) |
| `/carto/api/v2/sql` | ✅ | Legacy SQL endpoint |

**Features:**
- **SQL Query Execution:** SELECT with WHERE, ORDER BY, LIMIT, OFFSET
- **Aggregation:** GROUP BY, COUNT, SUM, AVG, MIN, MAX
- **Formats:** GeoJSON, JSON
- **Dataset Resolution:** Automatic layer mapping

**File References:**
- SQL parser: `src/Honua.Server.Host/Carto/CartoSqlQueryParser.cs`
- Executor: `src/Honua.Server.Host/Carto/CartoSqlQueryExecutor.cs`

---

### ✅ OpenRosa (ODK/KoboToolbox)

**Evidence:** `src/Honua.Server.Host/OpenRosa/OpenRosaEndpoints.cs`, `src/Honua.Server.Core/OpenRosa/`

**Endpoints:**

| Endpoint | Support | Notes |
|----------|---------|-------|
| `/openrosa/formList` | ✅ | List XForms |
| `/openrosa/forms/{formId}` | ✅ | XForm definition |
| `/openrosa/forms/{formId}/manifest` | ✅ | Form manifest |
| `/openrosa/submission` | ✅ | Submit form data |

**Features:**
- **XForm Generation:** Auto-generate from layer metadata (`IXFormGenerator`)
- **Form Submissions:** Store as features
- **ODK Collect Compatibility:** Full protocol compliance
- **KoboToolbox Integration:** Standard OpenRosa endpoints

---

## Data Providers

### Open Source Edition (4 Providers)

All providers implement `IDataStoreProvider` interface.

| Provider | Implementation | Full CRUD | MVT Native | Spatial Ops | CRS Transform |
|----------|----------------|-----------|------------|-------------|---------------|
| **PostgreSQL/PostGIS** | `Postgres/PostgresDataStoreProvider.cs` | ✅ | ✅ ST_AsMVT | ✅ PostGIS | ✅ ST_Transform |
| **SQLite/SpatiaLite** | `Sqlite/SqliteDataStoreProvider.cs` | ✅ | ❌ | ✅ SpatiaLite | ✅ SpatiaLite |
| **SQL Server** | `SqlServer/SqlServerDataStoreProvider.cs` | ✅ | ❌ | ✅ Spatial types | ✅ Native |
| **MySQL/MariaDB** | `MySql/MySqlDataStoreProvider.cs` | ✅ | ❌ | ✅ Geometry | ✅ Limited |

**Evidence:** `src/Honua.Server.Core/Data/`

**Common Features:**
- Connection pooling
- Prepared statements
- Transaction support
- Spatial indexing
- Query builders per provider (e.g., `PostgresFeatureQueryBuilder.cs`)
- Capabilities metadata (e.g., `PostgresDataStoreCapabilities.cs`)

### Enterprise Edition (6 Providers)

**Evidence:** `src/Honua.Server.Enterprise/Data/`

| Provider | Implementation | Status |
|----------|----------------|--------|
| **Oracle Spatial** | `Oracle/OracleDataStoreProvider.cs` | ✅ Implemented |
| **Snowflake** | `Snowflake/SnowflakeDataStoreProvider.cs` | ✅ Implemented |
| **Google BigQuery** | `BigQuery/BigQueryDataStoreProvider.cs` | ✅ Implemented |
| **MongoDB** | `MongoDB/MongoDbDataStoreProvider.cs` | ✅ Implemented |
| **Amazon Redshift** | `Redshift/RedshiftDataStoreProvider.cs` | ✅ Implemented |
| **Azure Cosmos DB** | `CosmosDb/CosmosDbDataStoreProvider.cs` | ✅ Implemented |

---

## Export Formats

All export formats verified via implementation inspection.

### Vector Formats

| Format | Exporter | Content-Type | Async Jobs | Max Records |
|--------|----------|--------------|------------|-------------|
| **GeoJSON** | Built-in | `application/geo+json` | ❌ | 10,000 (configurable) |
| **GeoJSON-Seq** | Built-in | `application/geo+json-seq` | ❌ | Unlimited (streaming) |
| **KML** | `KmlFeatureFormatter.cs` | `application/vnd.google-earth.kml+xml` | ❌ | 10,000 |
| **KMZ** | `KmzArchiveBuilder.cs` | `application/vnd.google-earth.kmz` | ❌ | 10,000 |
| **TopoJSON** | `TopoJsonFeatureFormatter.cs` | `application/topo+json` | ❌ | 5,000 (topology calc) |
| **GeoPackage** | `GeoPackageExporter.cs` | `application/geopackage+sqlite3` | ✅ >10k | Unlimited |
| **Shapefile** | `ShapefileExporter.cs` | `application/zip` | ✅ >10k | Unlimited |
| **CSV** | `CsvExporter.cs` | `text/csv` | ❌ | 100,000 |
| **GML 3.2** | Via WFS | `application/gml+xml; version=3.2` | ❌ | 10,000 |
| **GeoServices JSON format** | Via Geoservices REST a.k.a. Esri REST | `application/json` | ❌ | 10,000 |
| **HTML** | Built-in | `text/html` | ❌ | N/A (view only) |

**Evidence:** `src/Honua.Server.Core/Export/`, `src/Honua.Server.Core/Serialization/`

**Async Job Support:**
- GeoPackage and Shapefile automatically trigger async jobs for >10,000 records
- Job status polling via Admin API
- Download URLs with expiration

---

## Authentication & Security

### Authentication Modes

**Evidence:** `src/Honua.Server.Core/Authentication/`, configuration files

| Mode | Implementation | Use Case | Features |
|------|----------------|----------|----------|
| **QuickStart** | `appsettings.QuickStart.json` | Development | No authentication, auto-admin login |
| **Local** | `LocalAuthenticationService.cs` | Simple deployments | Username/password, JWT tokens, Argon2 hashing |
| **OIDC** | `JwtBearerOptionsConfigurator.cs` | Enterprise SSO | OAuth2/OpenID Connect integration |

**Local Mode Features:**
- Password hashing: `PasswordHasher.cs` (Argon2)
- Complexity validation: `PasswordComplexityValidator.cs`
- Token management: `LocalTokenService.cs`
- Signing keys: `LocalSigningKeyProvider.cs`
- Account lockout support
- Token expiration (configurable TTL)
- Database storage: `SqliteAuthRepository.cs`
- Bootstrap service: `AuthBootstrapService.cs`

### Authorization (RBAC)

**Evidence:** 41 occurrences across 23 files

**Roles:**

| Role | Policy | Permissions | Used By |
|------|--------|-------------|---------|
| **Viewer** | RequireViewer | Read-only access | OGC API GET, WFS GetFeature, WMS, WMTS, STAC, OData GET, Carto SQL |
| **DataPublisher** | RequireDataPublisher | Read + Write | OGC API POST/PUT/PATCH/DELETE, WFS-T, applyEdits, OData POST/PUT/PATCH/DELETE |
| **Admin** | RequireAdmin | Administrative | Metadata management, cache control, configuration |
| **Owner** | RequireOwner | Full control | System-level operations |

**Per-Layer Controls:**
- Editable/non-editable fields
- Required fields
- Immutable fields
- Edit capabilities: `LayerEditCapabilitiesDefinition` (MetadataSnapshot.cs:823-832)

### Security Features

**Evidence:** `src/Honua.Server.Host/` middleware

| Feature | Implementation | Status |
|---------|----------------|--------|
| **Rate Limiting** | "OgcApiPolicy" | ✅ Applied to WMTS, WCS |
| **CORS** | CORS middleware | ✅ Configurable origins, credentials |
| **Secure Exceptions** | `SecureExceptionHandlerMiddleware.cs` | ✅ Sanitized errors |
| **Input Validation** | Schema validators | ✅ Metadata, protocol-specific |
| **SQL Injection Protection** | Parameterized queries | ✅ All providers |
| **Field-level Security** | Edit constraints | ✅ Per-layer configuration |

---

## Advanced Features

### ✅ Temporal Support (COMPREHENSIVE)

**Evidence:** `MetadataSnapshot.cs` lines 718-730 (layer), 943-953 (raster)

**Layer Temporal:**
- Start/end field configuration
- Default values
- Fixed value lists (enumerated datetimes)
- Min/max ranges
- Period intervals (ISO 8601 duration, e.g., "P1D" for daily)

**Raster Temporal:**
- Datetime parameter in WMS TIME, WMTS datetime, OGC Tiles datetime
- Temporal validation in tile requests (`OgcTilesHandlers.cs` lines 372-452)

**Protocol Support:**
- WMS TIME parameter
- WMTS datetime parameter
- OGC API Features datetime parameter
- Vector tile datetime filtering
- Raster tile temporal selection

---

### ✅ Vector Tiles (OPTIMIZED)

**Evidence:** `src/Honua.Server.Core/VectorTiles/VectorTileProcessor.cs`

**Features:**
- **MVT/PBF Format:** Mapbox Vector Tile protocol buffers
- **PostGIS Native:** ST_AsMVT for optimal performance
- **Overzooming:** Serve higher zooms from lower zoom data
- **Geometry Simplification:** Automatic by zoom level
- **Feature Reduction:** Area-based thresholds
- **Clustering:** Feature clustering support (lines 64-71)
- **Custom Options:** Per-service tile configuration

**Configuration:** `VectorTileOptions.cs`

**Limitation:** Clustering only works with PostGIS provider

---

### ✅ Raster Support (COMPREHENSIVE)

**Evidence:** `src/Honua.Server.Core/Raster/` (20+ files)

**Capabilities:**
- **COG Support:** Cloud Optimized GeoTIFF
- **Rendering:** `Rendering/` subdirectory
- **Tile Caching:** `Caching/` subdirectory
- **Mosaics:** `Mosaic/` subdirectory
- **Analytics:** `Analytics/` subdirectory
- **Multiple Sources:** `Sources/` subdirectory
- **Format Detection:** `RasterFormatHelper.cs`
- **Registry:** `RasterDatasetRegistry.cs`

**Raster Sources:**
- Local files
- Cloud storage (S3, Azure Blob via URIs)
- HTTP/HTTPS with range requests
- Credentials management

**Cache Features:**
- Pre-seeding support
- Zoom level configuration
- Metrics tracking: `IRasterTileCacheMetrics`
- Provider abstraction: `IRasterTileCacheProvider`
- Quota management (Admin API)

---

### ✅ Styling

**Evidence:** `MetadataSnapshot.cs` lines 995-1045

**Style Types:**
- **Simple:** Single symbolizer (fill, stroke, marker)
- **Unique Value:** Categorized rendering
- **Rule-based:** Filters and scale ranges

**Style Formats:**
- Legacy JSON format
- SLD (Styled Layer Descriptor) export
- GeoServices JSON format renderer format
- Mapbox GL styles (for vector tiles)

**Features:**
- Per-layer default styles
- Multiple style alternatives
- Geometry-type specific
- Fill, stroke, icon, label support
- Opacity control

---

### ✅ Metadata Standards

**Evidence:** `MetadataSnapshot.cs` lines 1047+, CSW handlers

**Supported:**

1. **ISO 19115** - Geographic Information Metadata
   - Embedded in layer definitions
   - CSW ISO 19139 XML output
   - Contact, constraints, keywords, extent

2. **STAC Metadata** - SpatioTemporal Asset Catalog
   - Per-layer STAC properties
   - Per-raster-dataset STAC metadata
   - Automatic STAC item generation

3. **Dublin Core** - Simple metadata elements
   - CSW Dublin Core output

---

### ✅ Cloud Storage

**Evidence:** Raster source definitions, attachment storage configuration

**Supported:**
- Cloud URIs in raster sources (`RasterSourceDefinition`, MetadataSnapshot.cs:973-980)
- Attachment cloud storage profiles (S3, Azure Blob)
- Credentials management
- Pre-signed URLs for secure downloads
- HTTP range requests for efficient access

---

### ✅ Relationships

**Evidence:** `MetadataSnapshot.cs` lines 764-782

**Features:**
- One-to-many relationships
- Foreign key relationships
- Related record queries (Geoservices REST a.k.a. Esri REST API `queryRelatedRecords`)
- Composite relationships

**Configuration:**
- Relationship ID
- Related layer
- Origin/destination foreign keys
- Cardinality (one-to-many)

---

### ✅ Attachments

**Evidence:** `LayerAttachmentDefinition` (MetadataSnapshot.cs:845-857), FeatureServer controller

**Features:**
- File attachments per feature
- Cloud storage integration (S3, Azure Blob, local filesystem)
- Content type filtering (MIME types)
- Size limits (MaxSizeMiB per file)
- GlobalID requirement option
- Pre-signed URL generation
- OGC API link exposure (`ExposeAsOgcApiLinks`)
- Full CRUD via Geoservices REST a.k.a. Esri REST API

**Operations:**
- Query attachments
- Add attachment (multipart/form-data upload)
- Update attachment
- Delete attachments
- Download attachment

---

### ✅ Configuration & Deployment

**Metadata Providers:**
- **JSON:** `JsonMetadataProvider.cs` (file-based)
- **YAML:** `YamlMetadataProvider.cs` (file-based, multi-document)
- Schema validation: `MetadataSchemaValidator.cs`
- Live reload support
- Snapshot storage: `FileMetadataSnapshotStore.cs`

**Schema:** `src/Honua.Server.Core/schemas/metadata-schema.json` (16KB JSON Schema)

**Metadata Components:**
- Catalog definition
- Folders
- Data sources (connection strings, credentials)
- Services
- Layers (geometry, fields, storage, temporal, styles)
- Raster datasets
- Relationships
- Server settings (CORS, allowed hosts)

---

### ✅ Migration & Interoperability

**Evidence:** `src/Honua.Server.Core/Migration/`

**Supported:**
- ArcGIS Server migration (import services)
- Metadata import/export tools
- Data ingestion endpoints (Admin API)

---

### ✅ Observability

**Evidence:** `src/Honua.Server.Host/Observability/`, `src/Honua.Server.Core/Observability/`

**Features:**
- **API Metrics:** `IApiMetrics` interface
- **Feature Counting:** Request/response tracking
- **Cache Metrics:** Hit/miss rates
- **Logging:** Structured logging infrastructure
- **Health Checks:** `src/Honua.Server.Host/Health/`

---

## Known Gaps & Limitations

### ❌ NOT IMPLEMENTED

Based on comprehensive code scan (zero matches):

1. **3D/Scene Services**
   - No I3S (Indexed 3D Scene Layer)
   - No 3D tiles
   - No scene layers
   - No 3D symbology
   - **Evidence:** Zero matches for "3D", "I3S", "SceneLayer" in codebase

2. **Geoprocessing/WPS**
   - No OGC Web Processing Service (WPS)
   - No Esri Geoprocessing REST API
   - No workflow engine
   - No model builder
   - **Evidence:** Zero matches for "WPS" or "Geoprocessing" in Host

3. **Real-time/Streaming**
   - No WebSocket support
   - No stream layers
   - No real-time event feeds
   - No change tracking feeds
   - **Evidence:** Zero matches for "WebSocket", "Stream" in protocols

4. **Advanced Analytics**
   - No hot spot analysis
   - No density mapping
   - No spatial statistics services
   - No suitability modeling

5. **Geocoding**
   - No geocoding service
   - No reverse geocoding
   - No address locators

6. **Routing/Network Analysis**
   - No routing service
   - No directions
   - No service areas
   - No closest facility
   - No network datasets

### 🚧 PARTIAL IMPLEMENTATIONS

1. **WMTS GetFeatureInfo**
   - Operation defined but returns "OperationNotSupported" error
   - **Evidence:** `WmtsHandlers.cs` lines 378-382

2. **Vector Tile Clustering**
   - Clustering logic exists but **only works with PostGIS** provider
   - Other providers do not support MVT clustering
   - **Evidence:** `VectorTileProcessor.cs` lines 64-71

3. **Printing / MapFish**
   - MapFish-compatible `/print` endpoint produces PDF layouts (A4 portrait/landscape)
   - Legend output limited to text; no symbol synthesis yet
   - No template designer or multi-page layout engine

3. **STAC (Read-Only)**
   - Full catalog, collections, items, search implemented
   - **Missing:** STAC item creation/update/delete via API
   - **Missing:** Automatic STAC metadata propagation
   - Manual catalog synchronization required

4. **CSW (Read-Only)**
   - GetCapabilities, GetRecords, GetRecordById implemented
   - **Missing:** Transaction support (Insert/Update/Delete metadata)
   - **Missing:** Harvest support for metadata ingestion

5. **OData Spatial Functions**
   - `geo.intersects()` implemented with database pushdown
   - **Limited:** Other OData spatial functions (geo.distance, geo.length, etc.) may have limited support
   - Varies by data provider

### ⚠️ PROVIDER LIMITATIONS

1. **MVT Generation**
   - **PostGIS:** ✅ Native ST_AsMVT (optimal)
   - **SQLite:** ❌ No native MVT
   - **SQL Server:** ❌ No native MVT
   - **MySQL:** ❌ No native MVT
   - **Impact:** Vector tiles only performant with PostGIS

2. **CRS Transformations**
   - **PostGIS:** ✅ Full ST_Transform
   - **SpatiaLite:** ✅ Full transformation
   - **SQL Server:** ✅ Native support
   - **MySQL:** ⚠️ Limited CRS support
   - **Impact:** Some projections may fail on MySQL

3. **Spatial Indexes**
   - **PostGIS:** ✅ GIST indexes
   - **SpatiaLite:** ✅ R*Tree indexes
   - **SQL Server:** ✅ Spatial indexes
   - **MySQL:** ✅ Spatial indexes
   - **All:** ✅ Good support

### 📝 ARCHITECTURAL LIMITATIONS

1. **No Native Clustering**
   - No built-in load balancing
   - No session replication
   - No distributed caching
   - **Workaround:** Deploy stateless with shared database + metadata source

2. **No Dedicated Tile Cache Layer**
   - No GeoWebCache equivalent
   - S3/Azure/Local caching only
   - No tile seeding UI
   - **Available:** Admin API for pre-seeding jobs

3. **No Web Admin UI**
   - CLI tools only (`Honua.Cli`)
   - Admin API available
   - Metadata editing via JSON/YAML files
   - **Gap:** No visual management interface

4. **No File-Based Data Sources**
   - Cannot directly serve Shapefiles, GeoTIFF, GeoJSON files
   - Must import to database first
   - **Workaround:** Use data ingestion API

5. **Attachment Size Limits**
   - Per-file size limits (configurable MaxSizeMiB)
   - Cloud storage quotas apply
   - No built-in compression

---

## Summary Statistics

| Category | Count | Notes |
|----------|-------|-------|
| **OGC Standards** | 7 | Features, Tiles, WFS, WMS, WMTS, CSW, WCS |
| **STAC** | 1 | Full 1.0 implementation |
| **Industry APIs** | 5 | Geoservices REST a.k.a. Esri REST (3 servers), OData, Carto SQL, OpenRosa |
| **Data Providers (OSS)** | 4 | PostgreSQL, SQLite, SQL Server, MySQL |
| **Data Providers (Enterprise)** | 6 | Oracle, Snowflake, BigQuery, MongoDB, Redshift, Cosmos DB |
| **Export Formats** | 11 | GeoJSON, GeoJSON-Seq, KML, KMZ, TopoJSON, GeoPackage, Shapefile, CSV, GML, GeoServices JSON format, HTML |
| **Authentication Modes** | 3 | QuickStart, Local JWT, OIDC |
| **RBAC Roles** | 4 | Viewer, DataPublisher, Admin, Owner |
| **Major Gaps** | 7 | 3D, WPS, Real-time, Analytics, Printing, Geocoding, Routing |
| **Partial Features** | 5 | WMTS GetFeatureInfo, Vector clustering, STAC write, CSW write, Some OData spatial |

---

## Version History

| Version | Date | Source | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-10-07 | Commit `ab44774` | Initial code-derived documentation |

---

## Maintenance

This document is generated from codebase analysis. To update:

1. Re-scan codebase for new implementations
2. Verify handler/controller method signatures
3. Check for new data providers in `src/Honua.Server.Core/Data/` and `src/Honua.Server.Enterprise/Data/`
4. Review `MetadataSnapshot.cs` for schema changes
5. Update version history

**Last Verified:** 2025-10-07 (commit `ab44774`)
