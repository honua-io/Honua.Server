# Integration Testing Strategy - Reference Client Library Coverage

**Date:** 2025-02-02
**Status:** ✅ ACTIVE
**Primary Client:** QGIS/PyQGIS 3.34+

## Overview

This document outlines Honua's comprehensive integration testing strategy using industry-standard GIS client libraries. The goal is to ensure 100% API standard coverage with at least ONE reference implementation client library (QGIS), plus additional specialized clients for specific use cases (rasterio for WCS, arcpy for GeoServices REST).

## Rationale

**Why Integration Tests with Reference Clients?**

1. **Real-World Validation**: Unit tests validate our code, but integration tests validate that real-world clients can actually consume our APIs
2. **Specification Compliance**: Reference implementations (QGIS, GDAL/OGR, OWSLib) are battle-tested against specifications
3. **Breaking Change Detection**: Client library tests catch subtle breaking changes that unit tests might miss
4. **Documentation by Example**: These tests serve as working examples for users integrating with Honua
5. **Multi-Language Coverage**: Tests in Python, C# validate cross-platform compatibility

## Honua API Standards Coverage

Honua implements the following API standards (from comprehensive review):

| API Standard | Current Coverage | Target Coverage | Primary Client | Secondary Client |
|--------------|------------------|-----------------|----------------|------------------|
| **OGC WFS 2.0/3.0** | Basic (4 tests) | Comprehensive | QGIS WFS Provider | OWSLib |
| **OGC WMS 1.3.0** | Smoke (1 test) | Comprehensive | QGIS WMS Provider | - |
| **OGC WMTS 1.0.0** | Basic (1 test) | Comprehensive | QGIS WMTS Provider | - |
| **OGC API - Features** | Basic (1 test) | Comprehensive | QGIS OGR OAPIF | OGC API Features Client |
| **OGC API - Tiles** | ❌ None | Comprehensive | QGIS OGR | - |
| **OGC WCS 2.0** | ❌ None | Comprehensive | QGIS WCS Provider | rasterio + GDAL |
| **STAC 1.0** | Basic (4 tests) | Comprehensive | pystac-client | - |
| **Esri GeoServices REST** | ❌ None | Comprehensive | ArcGIS Python API | QGIS ArcGIS FeatureServer |
| **OData 4.0** | ❌ None | Basic | Python requests | - |

## Test Framework Architecture

### Directory Structure

```
tests/
├── qgis/                          # QGIS/PyQGIS integration tests (PRIMARY)
│   ├── conftest.py                # Shared fixtures (QGIS app, auth, base URL)
│   ├── test_wfs_comprehensive.py  # WFS 2.0/3.0 - ALL operations
│   ├── test_wms_comprehensive.py  # WMS 1.3.0 - ALL operations
│   ├── test_wmts_comprehensive.py # WMTS 1.0.0 - ALL tile matrix sets
│   ├── test_ogc_features_comprehensive.py  # OGC API Features - ALL endpoints
│   ├── test_ogc_tiles_comprehensive.py     # OGC API Tiles - ALL endpoints
│   ├── test_wcs_comprehensive.py           # WCS 2.0 - ALL extensions
│   ├── test_stac_comprehensive.py          # STAC 1.0 - ALL endpoints
│   ├── test_geoservices_comprehensive.py   # GeoServices REST - ALL endpoints
│   └── test_rendering.py                   # Visual rendering validation
│
├── python/                        # Python client library tests
│   ├── conftest.py                # Shared fixtures (requests session, auth)
│   ├── test_wcs_rasterio.py       # WCS 2.0 with rasterio (COG, GeoTIFF)
│   ├── test_stac_pystac.py        # STAC 1.0 with pystac-client
│   ├── test_ogc_owslib.py         # OGC services with OWSLib
│   └── test_geoservices_arcpy.py  # GeoServices with ArcGIS Python API
│
├── csharp/                        # C# client library tests (future)
│   └── Honua.ClientLibrary.Tests/
│       ├── ArcGISProSdkTests.cs   # ArcGIS Pro SDK integration
│       └── EsriRestClientTests.cs # Esri.ArcGISRuntime tests
│
└── e2e/                           # End-to-end desktop automation
    ├── qgis_desktop_automation/   # QGIS desktop automation
    └── arcgis_pro_automation/     # ArcGIS Pro automation (future)
```

### Test Categories

All tests are marked with pytest markers for selective execution:

```python
@pytest.mark.integration          # All integration tests
@pytest.mark.qgis                 # QGIS-specific tests
@pytest.mark.python               # Pure Python client tests
@pytest.mark.wfs                  # WFS-specific tests
@pytest.mark.wms                  # WMS-specific tests
@pytest.mark.wmts                 # WMTS-specific tests
@pytest.mark.ogc_features         # OGC API Features tests
@pytest.mark.ogc_tiles            # OGC API Tiles tests
@pytest.mark.wcs                  # WCS-specific tests
@pytest.mark.stac                 # STAC-specific tests
@pytest.mark.geoservices          # GeoServices REST tests
@pytest.mark.rendering            # Visual rendering tests
@pytest.mark.slow                 # Long-running tests (>5s)
@pytest.mark.requires_honua       # Requires running Honua instance
@pytest.mark.requires_testdata    # Requires specific test data loaded
```

## QGIS/PyQGIS - Primary Reference Client

### Why QGIS?

1. **Industry Standard**: Most widely-used open-source desktop GIS (millions of users)
2. **Multi-Protocol**: Supports WFS, WMS, WMTS, WCS, OGC API Features, ArcGIS FeatureServer
3. **Specification Compliance**: QGIS is a reference implementation for many OGC standards
4. **Headless Capable**: Can run without GUI in CI/CD via `QT_QPA_PLATFORM=offscreen`
5. **Docker Available**: Official `qgis/qgis` Docker images for consistent testing
6. **Python Bindings**: PyQGIS provides full API access for automation

### QGIS Test Coverage Matrix

Each API standard will have comprehensive tests covering:

#### WFS 2.0/3.0 Comprehensive Tests

**Capabilities & Metadata:**
- [ ] GetCapabilities returns valid WFS_Capabilities document
- [ ] DescribeFeatureType returns valid XSD schema
- [ ] All advertised feature types are accessible
- [ ] Spatial reference systems (EPSG:4326, EPSG:3857) are supported

**Feature Retrieval:**
- [ ] GetFeature with no filter returns features
- [ ] GetFeature with BBOX filter (2D and 3D)
- [ ] GetFeature with attribute filter (PropertyIsEqualTo, PropertyIsLike)
- [ ] GetFeature with spatial filter (Intersects, Within, DWithin)
- [ ] GetFeature with CQL filter (if supported)
- [ ] GetFeature with paging (startIndex, count)
- [ ] GetFeature with sorting (sortBy parameter)
- [ ] GetFeature output formats: GML, GeoJSON, CSV

**Feature Editing (WFS-T):**
- [ ] Transaction: Insert new feature
- [ ] Transaction: Update existing feature
- [ ] Transaction: Delete feature
- [ ] Transaction: Multiple operations in single request
- [ ] Transaction: Rollback on error
- [ ] LockFeature and locked feature updates

**Advanced Features:**
- [ ] Stored queries (if supported)
- [ ] Joins (if supported)
- [ ] Reprojection between CRS

#### WMS 1.3.0 Comprehensive Tests

**Capabilities:**
- [ ] GetCapabilities returns valid WMS_Capabilities
- [ ] All layers listed are accessible
- [ ] CRS support (EPSG:4326, EPSG:3857, CRS:84)
- [ ] Image formats (PNG, JPEG, WebP)
- [ ] GetFeatureInfo formats (GML, JSON, HTML)

**Map Rendering:**
- [ ] GetMap with single layer
- [ ] GetMap with multiple layers
- [ ] GetMap with CRS reprojection
- [ ] GetMap with transparency (PNG)
- [ ] GetMap with styles (SLD if supported)
- [ ] GetMap with time dimension (if supported)
- [ ] GetMap with elevation dimension (if supported)

**Feature Info:**
- [ ] GetFeatureInfo returns valid response
- [ ] GetFeatureInfo with multiple layers
- [ ] GetFeatureInfo with different formats (GeoJSON, HTML, GML)

**Legend:**
- [ ] GetLegendGraphic returns valid image
- [ ] GetLegendGraphic with custom styles

#### WMTS 1.0.0 Comprehensive Tests

**Capabilities:**
- [ ] GetCapabilities returns valid WMTS Capabilities
- [ ] Tile matrix sets include WorldWebMercatorQuad
- [ ] Tile matrix sets include WorldCRS84Quad
- [ ] Image formats (PNG, JPEG, WebP)

**Tile Retrieval:**
- [ ] GetTile from WorldWebMercatorQuad at zoom 0
- [ ] GetTile from WorldWebMercatorQuad at zoom 10
- [ ] GetTile from WorldCRS84Quad
- [ ] GetTile with different image formats
- [ ] GetTile returns 404 for out-of-bounds tiles
- [ ] GetTile caching headers (ETag, Cache-Control)

**QGIS Integration:**
- [ ] QGIS can load WMTS layer
- [ ] QGIS renders tiles correctly
- [ ] QGIS caches tiles appropriately

#### OGC API - Features Comprehensive Tests

**Landing Page & Conformance:**
- [ ] Landing page (/) returns valid JSON
- [ ] Conformance (/conformance) lists all conformance classes
- [ ] OpenAPI (/api) returns valid OpenAPI 3.0 document

**Collections:**
- [ ] Collections list (/collections) returns all feature collections
- [ ] Collection detail (/collections/{id}) returns metadata
- [ ] Collection queryables (/collections/{id}/queryables)
- [ ] Collection schema (/collections/{id}/schema)

**Items:**
- [ ] Items endpoint (/collections/{id}/items) returns features
- [ ] Items with bbox filter
- [ ] Items with datetime filter
- [ ] Items with property filters
- [ ] Items with limit and paging
- [ ] Items with CRS negotiation (crs parameter)
- [ ] Items with specific properties only
- [ ] Single item (/collections/{id}/items/{featureId})

**CQL2 Filtering:**
- [ ] CQL2-JSON filter (POST request)
- [ ] CQL2-TEXT filter (GET request)
- [ ] Complex filters (AND, OR, NOT)
- [ ] Spatial predicates (S_INTERSECTS, S_WITHIN)
- [ ] Temporal predicates (T_DURING, T_AFTER)

**Output Formats:**
- [ ] GeoJSON (default)
- [ ] GeoJSON-LD
- [ ] FlatGeobuf
- [ ] GeoParquet (if supported)

#### OGC API - Tiles Comprehensive Tests

**Tile Matrix Sets:**
- [ ] Tile matrix sets endpoint lists available TMS
- [ ] WorldWebMercatorQuad available
- [ ] WorldCRS84Quad available
- [ ] Custom TMS (if supported)

**Tile Retrieval:**
- [ ] Get tile from vector tileset
- [ ] Get tile from raster tileset
- [ ] Tile formats: MVT, GeoJSON, PNG, JPEG
- [ ] Tiles with CRS transformation
- [ ] Tile caching headers

**Integration:**
- [ ] QGIS can consume OGC API Tiles as XYZ layer
- [ ] Tiles render correctly in QGIS

#### WCS 2.0 Comprehensive Tests

**Capabilities:**
- [ ] GetCapabilities returns valid WCS Capabilities
- [ ] All coverages are listed
- [ ] CRS support for coverages
- [ ] Supported formats (GeoTIFF, COG, NetCDF)

**Coverage Retrieval:**
- [ ] DescribeCoverage returns coverage metadata
- [ ] GetCoverage retrieves full coverage
- [ ] GetCoverage with subset (trim/slice)
- [ ] GetCoverage with CRS transformation
- [ ] GetCoverage with format parameter (GeoTIFF, COG)
- [ ] GetCoverage with scaling
- [ ] GetCoverage with interpolation

**Extensions:**
- [ ] Range subsetting extension
- [ ] Scaling extension
- [ ] Interpolation extension
- [ ] CRS extension

**QGIS/Rasterio Integration:**
- [ ] QGIS can load WCS coverage as raster layer
- [ ] Rasterio can open WCS coverage via GDAL WCS driver
- [ ] Downloaded rasters have correct georeferencing
- [ ] Downloaded rasters preserve CRS

#### STAC 1.0 Comprehensive Tests

**Catalog Structure:**
- [ ] Root catalog (/) returns valid STAC Catalog
- [ ] Conformance classes include STAC API - Features
- [ ] Conformance classes include STAC API - Item Search

**Collections:**
- [ ] Collections endpoint lists STAC collections
- [ ] Collection metadata includes STAC-specific fields (extent, license, providers)
- [ ] Collection includes links to items

**Item Search:**
- [ ] Search endpoint (/search) GET request
- [ ] Search endpoint (/search) POST request with JSON
- [ ] Search with bbox parameter
- [ ] Search with datetime parameter (single, range)
- [ ] Search with collections parameter
- [ ] Search with limit and paging (next links)
- [ ] Search with ids parameter
- [ ] Search with intersects (GeoJSON geometry)
- [ ] Search with query (property filters)
- [ ] Search with sortby parameter

**Items & Assets:**
- [ ] Item detail includes all required STAC fields
- [ ] Item assets include COG asset
- [ ] Item assets include thumbnail (if supported)
- [ ] Assets are downloadable
- [ ] Assets have correct media types

**pystac-client Integration:**
- [ ] pystac-client can open Honua STAC catalog
- [ ] pystac-client can search items
- [ ] pystac-client can download assets

#### Esri GeoServices REST API Comprehensive Tests

**Service Metadata:**
- [ ] REST endpoint root returns service metadata
- [ ] FeatureServer metadata includes layers
- [ ] MapServer metadata includes layers
- [ ] Layer metadata includes fields and geometry type

**Feature Layer:**
- [ ] Query endpoint returns features
- [ ] Query with where clause
- [ ] Query with geometry filter
- [ ] Query with spatial relationship (esriSpatialRelIntersects)
- [ ] Query with outFields parameter
- [ ] Query with returnGeometry parameter
- [ ] Query with f=json, f=geojson
- [ ] Query with paging (resultOffset, resultRecordCount)

**Feature Editing:**
- [ ] AddFeatures operation
- [ ] UpdateFeatures operation
- [ ] DeleteFeatures operation
- [ ] ApplyEdits with multiple operations

**Advanced Features:**
- [ ] Identify operation
- [ ] Find operation
- [ ] Query related records
- [ ] Attachments (if supported)

**ArcGIS Python API Integration:**
- [ ] FeatureLayer can connect to Honua
- [ ] FeatureLayer.query() returns features
- [ ] Features can be converted to GeoDataFrame
- [ ] Editing operations work

## Python Client Libraries - Secondary Coverage

### rasterio (WCS 2.0 Coverage Client)

**Why rasterio?**
- Industry-standard Python library for raster data
- Built on GDAL, validates GDAL WCS driver compatibility
- Used extensively in scientific computing and remote sensing

**Coverage:**
- [ ] Open WCS coverage via GDAL WCS: connection string
- [ ] Read raster data with correct georeferencing
- [ ] Read coverage metadata (CRS, transform, bounds)
- [ ] Read subsets (windowed reads)
- [ ] Read with overviews (scaling)

### pystac-client (STAC Client)

**Why pystac-client?**
- Official Python client for STAC API
- Widely used in Earth observation workflows
- Validates STAC specification compliance

**Coverage:**
- [ ] Open STAC catalog
- [ ] Search with spatial filter
- [ ] Search with temporal filter
- [ ] Search with property filter
- [ ] Iterate through paged results
- [ ] Download assets

### OWSLib (OGC Web Services Client)

**Why OWSLib?**
- Reference Python implementation for OGC services
- Used by many Python GIS applications
- Validates OGC XML parsing

**Coverage:**
- [ ] WFS GetCapabilities parsing
- [ ] WFS GetFeature request building
- [ ] WMS GetCapabilities parsing
- [ ] WMS GetMap request building
- [ ] WCS GetCapabilities parsing
- [ ] WCS GetCoverage request building

### ArcGIS Python API (GeoServices REST Client)

**Why ArcGIS Python API?**
- Official Esri client library
- Most widely used library for ArcGIS integration
- Validates GeoServices REST API compatibility

**Coverage:**
- [ ] Connect to FeatureServer
- [ ] Query features with filters
- [ ] Convert to Pandas/GeoPandas
- [ ] Edit features (add/update/delete)

## Test Execution Strategy

### Local Development

```bash
# Run all integration tests
pytest tests/qgis tests/python -m integration

# Run only QGIS tests
pytest tests/qgis

# Run only WFS tests
pytest -m wfs

# Run fast tests only (exclude slow rendering tests)
pytest -m "not slow"
```

### CI/CD Pipeline

```yaml
# GitHub Actions example
jobs:
  integration-tests:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgis/postgis:16-3.4

    steps:
      - name: Start Honua Server
        run: |
          docker run -d --name honua \
            -p 5005:8080 \
            -e ConnectionStrings__DefaultConnection="..." \
            ghcr.io/honuaio/honua-server:latest

      - name: Run QGIS Integration Tests
        run: |
          docker run --rm --network host \
            -e HONUA_QGIS_BASE_URL=http://localhost:5005 \
            -e QT_QPA_PLATFORM=offscreen \
            -v $PWD:/workspace -w /workspace \
            qgis/qgis:3.34 \
            bash -c "pip install pytest && pytest tests/qgis"

      - name: Run Python Integration Tests
        run: |
          pip install -r tests/python/requirements.txt
          HONUA_BASE_URL=http://localhost:5005 pytest tests/python
```

### Test Data Requirements

All integration tests require a running Honua instance with test data loaded:

```bash
# Option 1: QuickStart mode (in-memory SQLite)
HONUA_ALLOW_QUICKSTART=true \
DOTNET_ENVIRONMENT=QuickStart \
dotnet run --project src/Honua.Server.Host --urls http://127.0.0.1:5005

# Option 2: Docker with PostgreSQL
docker-compose -f docker-compose.test.yml up -d

# Option 3: Load specific test datasets
dotnet run --project src/Honua.Cli -- \
  ingest --service test-data --layer roads-primary \
  --file tests/TestData/roads.geojson
```

## Test Metrics & Coverage Goals

### Coverage Targets

| API Standard | Operations Tested | Target Coverage |
|--------------|-------------------|-----------------|
| WFS 2.0/3.0 | 15+ operations | 100% |
| WMS 1.3.0 | 8+ operations | 100% |
| WMTS 1.0.0 | 5+ operations | 100% |
| OGC API Features | 12+ endpoints | 100% |
| OGC API Tiles | 6+ endpoints | 100% |
| WCS 2.0 | 10+ operations | 100% |
| STAC 1.0 | 10+ endpoints | 100% |
| GeoServices REST | 12+ operations | 90% |

### Success Criteria

- ✅ All API standards have at least 10 integration tests
- ✅ QGIS can connect to and consume all OGC services
- ✅ rasterio can read WCS coverages
- ✅ pystac-client can search STAC catalog
- ✅ ArcGIS Python API can query GeoServices REST endpoints
- ✅ All tests pass in CI/CD pipeline
- ✅ Test execution time < 5 minutes for full suite

## Maintenance & Extension

### Adding New Tests

1. Identify the API operation to test
2. Create test in appropriate file (e.g., `test_wfs_comprehensive.py`)
3. Use appropriate pytest markers
4. Add to coverage tracking matrix
5. Document in this strategy document

### Updating for New API Versions

When Honua adds support for new API versions:

1. Create new test file or section (e.g., `test_wfs_3_0.py`)
2. Validate backwards compatibility with existing tests
3. Add version-specific tests for new features
4. Update coverage matrix

## Future Enhancements

### Desktop Application Automation

**QGIS Desktop Automation:**
- PyQGIS scripts for automated layer loading
- Project file (.qgs) generation and validation
- Print layout rendering tests
- Plugin compatibility tests

**ArcGIS Pro Automation (Future):**
- ArcPy scripts for project (.aprx) automation
- Geoprocessing workflow tests
- Layout rendering tests

### Performance Testing

- Load testing with k6 or Locust
- Tile serving performance benchmarks
- Large dataset query performance
- Concurrent client connection tests

### Visual Regression Testing

- Render maps via QGIS and compare with baseline images
- WMS GetMap image comparison
- WMTS tile visual validation
- Legend graphics validation

## Summary

This integration testing strategy ensures Honua's API implementations are validated against real-world client libraries and desktop GIS applications. By focusing on QGIS/PyQGIS as the primary comprehensive reference client, supplemented with specialized Python libraries (rasterio, pystac-client, OWSLib, arcpy), we achieve:

1. **Specification Compliance**: Real client libraries enforce OGC/STAC/GeoServices REST specifications
2. **Real-World Validation**: Tests match actual user workflows
3. **Breaking Change Detection**: Client tests catch subtle compatibility issues
4. **Cross-Platform Coverage**: Python, .NET, desktop application tests
5. **Comprehensive API Coverage**: Every Honua API standard thoroughly tested

**Next Steps:**
1. ✅ Create comprehensive QGIS tests for each API standard
2. ✅ Add rasterio WCS tests
3. ✅ Add pystac-client STAC tests
4. ✅ Add ArcGIS Python API GeoServices tests
5. ✅ Integrate into CI/CD pipeline
6. ⏳ Add desktop automation (QGIS, ArcGIS Pro)
