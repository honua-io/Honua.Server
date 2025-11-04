# Honua - Known Gaps & Limitations

**Version:** 1.0 MVP
**Last Updated:** 2025-10-07
**Source:** Code analysis of commit `ab44774`

This document provides a comprehensive list of features that are **NOT implemented** or only **partially implemented** in Honua, based on actual codebase inspection.

---

## ❌ NOT IMPLEMENTED

### 1. 3D/Scene Services

**Status:** Not implemented
**Evidence:** Zero code matches for "3D", "I3S", "SceneLayer" in `src/Honua.Server.Host/`

**Missing:**
- I3S (Indexed 3D Scene Layer) protocol
- 3D tiles (Cesium/OGC 3D Tiles)
- Scene layers
- 3D symbology and rendering
- Z-axis support in geometries
- Terrain services
- Building extrusion
- 3D mesh layers

**Impact:**
- Cannot serve 3D visualizations
- No indoor mapping support
- No volumetric data

**Workaround:**
- Use external 3D tile services
- Pre-generate 3D tiles offline

---

### 2. Geoprocessing/WPS

**Status:** Not implemented
**Evidence:** Zero code matches for "WPS" or "Geoprocessing" in `src/Honua.Server.Host/`

**Missing:**
- OGC Web Processing Service (WPS)
- Esri Geoprocessing REST API
- Workflow engine
- Model builder
- Batch processing framework
- Analysis tools (buffer, clip, union beyond basic geometry service)
- Raster algebra
- Spatial statistics
- Network analysis

**Impact:**
- Cannot execute complex spatial analyses server-side
- No workflow automation
- No custom geoprocessing tools

**Workaround:**
- Use QGIS/ArcGIS Desktop for analysis
- Perform analysis in PostGIS
- Use external processing services

**Planned:** Enterprise Edition Q2 2026

---

### 3. Real-time/Streaming

**Status:** Not implemented
**Evidence:** Zero code matches for "WebSocket", "Stream", "SSE" in protocol handlers

**Missing:**
- WebSocket support
- Stream layers (real-time data feeds)
- Server-Sent Events (SSE)
- Change tracking feeds
- Real-time feature updates
- Live location tracking
- Event-driven notifications
- Change Data Capture (CDC) integration

**Impact:**
- Cannot serve real-time IoT data
- No live tracking applications
- No real-time collaboration

**Workaround:**
- Client-side polling
- External real-time services
- Use STAC for near-real-time data discovery

**Planned:** Enterprise Edition Q2-Q3 2026

---

### 4. Advanced Analytics

**Status:** Not implemented

**Missing:**
- Hot spot analysis (Getis-Ord Gi*)
- Density mapping (heatmaps, kernel density)
- Spatial statistics (Moran's I, spatial autocorrelation)
- Suitability modeling
- Overlay analysis
- Terrain analysis (slope, aspect, hillshade)
- Raster classification
- Image segmentation
- Machine learning integration

**Impact:**
- No server-side analytical capabilities beyond basic queries
- Cannot perform spatial pattern detection
- No predictive modeling

**Workaround:**
- Use PostGIS for basic spatial statistics
- External analytics platforms (Carto, ArcGIS)
- Python/R spatial analysis libraries

**Planned:** Enterprise Edition Q3 2026

---

### 5. Printing/Layout Services

**Status:** MVP available (MapFish)

**Available:**
- `/print` MapFish-compatible endpoint with synchronous PDF export (A4 portrait/landscape)
- Title, subtitle, notes, scale annotation, and text-only legend output

**Missing:**
- Template designer and multi-frame layout engine
- Symbol-aware legends and north arrow/scale bar graphics
- Custom page sizes and async job/queue management

**Impact:**
- Suitable for lightweight stakeholder PDFs, but not full cartographic production
- Manual design work still required for complex layouts or branding

**Workaround:**
- External layout tools for advanced cartography (ArcGIS Pro, QGIS Layout Manager)
- Browser print/screenshot for quick reviews

**Planned:** Expand template support and richer layout controls in post-MVP phases

---

### 6. Geocoding Services

**Status:** Not implemented

**Missing:**
- Geocoding (address to coordinates)
- Reverse geocoding (coordinates to address)
- Address locators
- Batch geocoding
- Address standardization
- Match scores and candidates

**Impact:**
- Cannot convert addresses to locations
- No place search
- No address validation

**Workaround:**
- External geocoding services (Google, Mapbox, Nominatim, Pelias)
- Pre-geocoded data

**Planned:** Enterprise Edition Q2 2026

---

### 7. Routing/Network Analysis

**Status:** Not implemented

**Missing:**
- Routing service (A to B directions)
- Network analysis
- Service areas (drive-time polygons)
- Closest facility
- Origin-destination cost matrices
- Vehicle routing problem (VRP)
- Network datasets
- Turn restrictions
- Traffic integration

**Impact:**
- No navigation capabilities
- No logistics optimization
- No accessibility analysis

**Workaround:**
- External routing services (OSRM, Valhalla, GraphHopper, Mapbox)
- pgRouting extension in PostGIS

**Planned:** Enterprise Edition Q3 2026

---

### 8. Advanced Raster Processing

**Status:** Minimal implementation

**Missing:**
- Raster functions (e.g., NDVI, band math)
- On-the-fly raster processing
- Mosaic on-the-fly (image service)
- Dynamic raster generation
- Multi-temporal raster analysis
- Change detection
- Image classification
- Radiometric correction

**Available:**
- Basic raster rendering (WMS, WMTS, ImageServer)
- Tile caching
- COG support
- Static mosaics

**Impact:**
- Limited raster analytics
- Cannot perform on-demand raster calculations
- No dynamic image processing

**Workaround:**
- Pre-process rasters offline (GDAL)
- Use GeoServer for advanced raster processing
- PostGIS raster extension

**Planned:** Enterprise Edition Q3 2026

---

## 🚧 PARTIAL IMPLEMENTATIONS

### ~~1. Vector Tile Clustering~~ (NOT A GAP - Provider Capability)

**Status:** ✅ Fully implemented for PostGIS (ST_ClusterKMeans)
**Evidence:** `src/Honua.Server.Core/VectorTiles/VectorTileProcessor.cs` lines 64-71

**Clarification:**
This is NOT a gap in Honua - it's a **database provider capability difference**. Vector tile clustering uses PostGIS's native `ST_ClusterKMeans` function, which is specific to PostGIS. This is analogous to saying "SQL Server doesn't support PostgreSQL extensions" - it's expected behavior, not a missing feature.

**Provider Support:**
- ✅ **PostGIS:** Full clustering support via ST_ClusterKMeans
- ⚠️ **SQLite/SQL Server/MySQL:** No native MVT clustering (as expected)

**Recommendation:**
Use PostGIS for vector tile services (industry best practice). For other providers, pre-cluster data if needed.

**Removed from gaps list:** This is a provider capability, not a Honua gap

---

### 1. STAC Write Operations

**Status:** ✅ **IMPLEMENTED** (as of 2025-10-07)
**Evidence:** `src/Honua.Server.Host/Stac/StacCollectionsController.cs`

**Implemented:**
- ✅ Catalog root
- ✅ Conformance
- ✅ Collections (list, get)
- ✅ Items (list, get)
- ✅ Search (GET/POST)
- ✅ Synchronization service
- ✅ POST `/stac/collections` (create collection)
- ✅ PUT `/stac/collections/{id}` (replace collection)
- ✅ PATCH `/stac/collections/{id}` (update collection)
- ✅ DELETE `/stac/collections/{id}` (delete collection)
- ✅ POST `/stac/collections/{id}/items` (create item)
- ✅ PUT `/stac/collections/{id}/items/{id}` (replace item)
- ✅ PATCH `/stac/collections/{id}/items/{id}` (update item)
- ✅ DELETE `/stac/collections/{id}/items/{id}` (delete item)

**Still Missing:**
- ❌ Automatic metadata propagation to STAC
- ❌ Advanced item validation (basic validation implemented)

**Notes:**
- All write operations require DataPublisher authorization
- Full CRUD support for collections and items
- STAC catalog can now be managed programmatically via API

---

### 2. CSW Transaction

**Status:** ✅ **ENDPOINT ADDED** (returns OperationNotSupported) (as of 2025-10-07)
**Evidence:** `src/Honua.Server.Host/Csw/CswHandlers.cs` lines 452-471

**Implemented:**
- ✅ GetCapabilities
- ✅ GetRecords (search)
- ✅ GetRecordById
- ✅ ISO 19139 output
- ✅ Dublin Core output
- ✅ Transaction endpoint (returns helpful OperationNotSupported message)

**Not Implemented (Transaction operation details):**
- ❌ Insert metadata records
- ❌ Update metadata records
- ❌ Delete metadata records
- ❌ Harvest operation (metadata harvesting)
- ❌ Synchronization with external catalogs

**Reason:**
CSW Transaction would require a metadata persistence service to write YAML/JSON files, which is not part of the current architecture. Honua's metadata files are meant to be edited manually or via CLI tools.

**Workaround:**
- Edit metadata via JSON/YAML files directly
- Use STAC API for programmatic catalog management (fully supported with write operations)
- Trigger metadata reload via Admin API

**Priority:** Low (CSW Transaction rarely used; STAC API provides better alternative)

---

### ~~3. OData Spatial Functions~~ (FULLY IMPLEMENTED)

**Status:** ✅ **ALL OData v4 standard spatial functions implemented** (as of 2025-10-07)
**Evidence:** `src/Honua.Server.Core/Query/Filter/ODataFilterParser.cs` lines 101-103, database pushdown in PostgreSQL/SQL Server/MySQL query builders

**Implemented:**
- ✅ `geo.intersects()` - returns boolean, with database pushdown to all providers
- ✅ `geo.distance()` - returns distance in meters (geography) or CRS units (geometry), with database pushdown
- ✅ `geo.length()` - returns length in meters (geography) or CRS units (geometry), with database pushdown

**Database Support:**
- **PostGIS:** ST_Intersects, ST_Distance (geography cast for SRID 4326), ST_Length (geography cast for SRID 4326)
- **SQL Server:** STIntersects, STDistance, STLength (geography/geometry types)
- **MySQL:** ST_Intersects, ST_Distance_Sphere (SRID 4326), ST_Length

**Note:** OData v4 specification defines only 3 spatial functions (geo.intersects, geo.distance, geo.length). All 3 are now implemented with full database pushdown.

**Note:** OData is primarily for business intelligence tools and non-GIS clients. For advanced GIS workflows, use OGC API Features with CQL2.

---

## ⚠️ PROVIDER LIMITATIONS

### MVT (Vector Tile) Generation

| Provider | Native MVT | Performance | Notes |
|----------|-----------|-------------|-------|
| **PostGIS** | ✅ ST_AsMVT | Excellent | Recommended for vector tiles |
| **SQLite** | ❌ | Poor | No native MVT, client-side generation |
| **SQL Server** | ❌ | Poor | No native MVT |
| **MySQL** | ❌ | Poor | No native MVT |

**Impact:**
- Vector tiles only performant with PostGIS
- Other providers require client-side tile generation (slow)

**Recommendation:**
- Use PostGIS for vector tile services
- Use raster tiles for other providers

---

### CRS Transformations

| Provider | CRS Support | Notes |
|----------|------------|-------|
| **PostGIS** | ✅ Full | ST_Transform with PROJ |
| **SpatiaLite** | ✅ Full | PROJ integration |
| **SQL Server** | ✅ Good | Native spatial types |
| **MySQL** | ⚠️ Limited | Some projections may fail |

**Impact:**
- Coordinate system transformations may fail on MySQL

**Recommendation:**
- Use PostGIS or SQL Server for multi-CRS requirements
- Avoid complex projections on MySQL

---

## 📝 ARCHITECTURAL LIMITATIONS

### 1. No Native Clustering/High Availability

**Status:** Not implemented

**Missing:**
- Load balancing (built-in)
- Session replication
- Distributed caching
- Active-active clustering
- Failover mechanisms

**Current:**
- Stateless application design
- Shared database + metadata source
- Standard reverse proxy load balancing (nginx, HAProxy)

**Impact:**
- Manual load balancer setup required
- No session affinity (not needed due to stateless design)
- Database is single point of failure

**Workaround:**
- Deploy multiple Honua instances behind load balancer
- Use database clustering (PostgreSQL replication, SQL Server AlwaysOn)
- Shared metadata source (S3, Azure Blob, network file system)

**Planned:** Enterprise Edition Q2 2026

---

### 2. No Web Admin UI

**Status:** Not implemented

**Missing:**
- Visual metadata editor
- Service management dashboard
- User management interface
- Cache management UI
- Monitoring dashboard
- Log viewer

**Current:**
- CLI tools (`Honua.Cli`)
- Admin REST API
- JSON/YAML metadata editing

**Impact:**
- High barrier to entry for non-technical users
- No visual service management

**Workaround:**
- Use Honua.Cli for command-line operations
- Edit metadata files in text editor with schema validation
- Use Admin API programmatically

**Planned:** Phase 1 Q1 2026 (highest priority gap)

---

### 3. No File-Based Data Sources

**Status:** Not implemented

**Missing:**
- Direct Shapefile serving
- Direct GeoPackage serving
- Direct GeoTIFF serving
- Direct GeoJSON file serving
- Directory-based data sources

**Current:**
- Database-only data sources
- Must import files to database

**Impact:**
- Cannot serve files directly
- Data import step required

**Workaround:**
- Use data ingestion API
- Import shapefiles/GeoPackage to PostGIS/SQLite
- Use `ogr2ogr` for file-to-database conversion

**Planned:** Phase 1 Q1 2026 (GeoPackage direct read support)

---

### ~~4. CDN Integration~~ (REMOVED - Deployment Concern)

**Status:** Not a product gap - deployment/consulting service

**Note:**
CDN integration (CloudFront, Fastly, etc.) is a deployment architecture decision, not a missing feature. Honua's tile cache providers (S3, Azure Blob) work seamlessly as CDN origins. CDN setup is typically handled by deployment consultants based on customer infrastructure requirements.

**Deployment Options:**
- CloudFront with S3 origin
- Azure CDN with Azure Blob origin
- Fastly with any origin
- Standard reverse proxy caching (nginx, Varnish)

**Removed from gaps list:** This is infrastructure/deployment, not application functionality

---

## 🔍 EDGE CASES & CORNER CASES

### 1. Large Dataset Exports

**Limitation:**
- Synchronous exports limited to 10k-100k records (configurable)
- Async jobs required for larger exports

**Workaround:**
- Use GeoJSON-Seq for unlimited streaming
- Use async job API for GeoPackage/Shapefile >10k records

---

### 2. Concurrent Editing

**Limitation:**
- No distributed locking
- WFS locking uses in-memory lock manager (single-instance only)

**Workaround:**
- Use WFS locking for single-instance deployments
- Implement optimistic concurrency with ETags (OGC API Features)

---

### 3. Metadata Hot Reload

**Limitation:**
- Metadata reload requires manual trigger (Admin API POST)
- No file watcher for automatic reload

**Workaround:**
- Use Admin API `/admin/metadata/reload` endpoint
- Implement file watcher in deployment scripts

---

## 📊 Gap Summary Statistics

| Category | Count | Notes |
|----------|-------|-------|
| **Major Missing Features** | 8 | 3D, WPS, Real-time, Analytics, Printing, Geocoding, Routing, Advanced Raster |
| **Partial Implementations** | 0 | None |
| **Recently Implemented** | 3 | WMTS GetFeatureInfo, STAC write operations, OData spatial functions (geo.distance, geo.length) |
| **Provider Limitations** | 2 | MVT (PostGIS only), CRS (MySQL limited) |
| **Architectural Gaps** | 3 | Clustering/HA, Web Admin UI, File-based sources |
| **Not Product Gaps** | 2 | Vector clustering (provider capability), CDN (deployment) |
| **Edge Cases** | 3 | Large exports, Concurrent edits, Hot reload |

---

## 🎯 Priority Recommendations

### Critical (Q1 2026)
1. **Web Admin UI** - Biggest adoption barrier
2. **File-based data sources** (GeoPackage) - Common requirement

### High (Q2 2026)
3. **Clustering/HA** - Enterprise scalability
4. **Geoprocessing/WPS** - Analysis capabilities
5. **Geocoding integration** - Common feature request

### Medium (Q3 2026)
6. **3D Tiles support** - Modern mapping trend
7. **Real-time/streaming** - IoT use cases
8. **Advanced Raster Processing** - On-the-fly raster analytics

### Low Priority
9. Printing service

### ✅ Recently Implemented
- **WMTS GetFeatureInfo** - Implemented 2025-10-07
- **STAC write operations** - Implemented 2025-10-07
- **CSW Transaction endpoint** - Added 2025-10-07 (returns OperationNotSupported with helpful message)
- **OData spatial functions (geo.distance, geo.length)** - Implemented 2025-10-07 (all OData v4 spatial functions now complete)

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.3 | 2025-10-07 | Implemented OData spatial functions (geo.distance, geo.length) - all OData v4 spatial functions now complete |
| 1.2 | 2025-10-07 | Corrected gaps: removed "No Tile Cache Layer" (cache exists with statistics/quota APIs), removed "CDN Integration" (deployment concern), clarified vector clustering is provider capability not gap, detailed OData spatial function status |
| 1.1 | 2025-10-07 | Implemented WMTS GetFeatureInfo, STAC write operations, CSW Transaction endpoint |
| 1.0 | 2025-10-07 | Initial gap analysis from code inspection |

---

## See Also

- [CAPABILITIES.md](CAPABILITIES.md) - Full capability reference
- [Roadmap](user/roadmap.md) - Feature roadmap with timelines
- [GitHub Issues](https://github.com/honua/honua/issues) - Known issues and feature requests
