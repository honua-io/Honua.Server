# Metadata Validation Test Suite - Implementation Summary

## Overview
Comprehensive metadata validation test suite implemented for the Honua geospatial platform, covering YAML/JSON parsing, schema validation, field validation, layer configuration, and metadata crosswalk to multiple standards (STAC, ISO 19115, OGC metadata).

## Test Files Created

### 1. MetadataValidationTests.cs (1,302 lines, 47 tests)
**Purpose**: Core validation tests for metadata parsing and schema validation

**Coverage Areas**:
- Valid YAML/JSON parsing (3 tests)
- Invalid JSON/YAML syntax detection (5 tests)
- Schema validation (required fields, data types) (9 tests)
- Layer validation (geometryType, idField, geometryField) (5 tests)
- Bbox validation (valid coordinates, correct format) (4 tests)
- CRS validation (valid EPSG codes, OGC URIs) (2 tests)
- Temporal extent validation (datetime format, open-ended ranges) (4 tests)
- Style validation (simple, uniqueValue renderers) (6 tests)
- Raster dataset validation (source types, URIs) (4 tests)
- Attachment validation (storage profiles, size limits) (3 tests)
- CORS validation (credentials, origins) (2 tests)
- JSON formatting (comments, trailing commas) (2 tests)

**Key Test Scenarios**:
- Valid complete metadata with all optional fields
- Minimal valid metadata
- Missing required fields (catalog ID, folder ID, service references)
- Duplicate IDs (folders, services, layers)
- Broken references (unknown folder/dataSource/service/style IDs)
- Invalid bbox (less than 4 coordinates)
- Valid bbox formats (4 values, 6 values, multiple bboxes)
- Invalid temporal formats
- Open-ended temporal extents (using "..")
- Style renderer validation (simple vs. uniqueValue)
- Raster source type validation
- Attachment configuration validation
- CORS allow-credentials with allow-any-origin conflict

### 2. MetadataFileValidationTests.cs (280 lines, 10 tests)
**Purpose**: File-based validation with error reporting

**Coverage Areas**:
- Loading complete JSON files with all features
- Loading minimal YAML files
- Error messages for common validation failures
- File not found scenarios

**Test Data Files** (8 files):
1. `valid-complete.json` - Comprehensive metadata with all fields
2. `valid-minimal.yaml` - Minimal working configuration
3. `invalid-missing-catalog-id.json` - Missing required catalog ID
4. `invalid-duplicate-ids.json` - Duplicate folder IDs
5. `invalid-broken-references.json` - Service referencing non-existent folder
6. `invalid-bbox.json` - Bbox with only 2 coordinates
7. `invalid-temporal-format.json` - Invalid datetime string
8. `invalid-cors-config.json` - CORS credentials with wildcard origins

**Key Features**:
- Tests real file loading scenarios
- Validates error messages are clear and actionable
- Tests both JSON and YAML providers
- Comprehensive valid-complete.json includes:
  - Full catalog with contact, license, extents
  - Server configuration with CORS
  - Multiple folders and data sources
  - Service with OGC configuration
  - Layer with fields, editing, attachments, styles
  - Multiple styles (simple and uniqueValue)
  - Raster dataset with cache configuration

### 3. MetadataInheritanceTests.cs (422 lines, 13 tests)
**Purpose**: Validate default values and metadata inheritance

**Coverage Areas**:
- Layer defaults (title, itemType, editing disabled)
- Service defaults (title, serviceType, enabled)
- Catalog entry inheritance
- Editing constraints default values
- Field defaults (nullable, editable)
- Raster cache defaults
- Style defaults (renderer, format, geometryType)
- Temporal reference system defaults (Gregorian)
- Empty collection handling (not null)
- Service-layer attachment

**Key Test Scenarios**:
- Fields default to nullable=true, editable=true
- Services default to enabled=true, type=feature
- Layers default to editing disabled, require authentication
- Styles default to simple renderer, legacy format, polygon geometry
- Temporal extents default to Gregorian TRS
- Raster cache defaults to enabled, not pre-seeded
- Empty collections are initialized (not null)
- Layers are correctly attached to their services

### 4. MetadataCrosswalkTests.cs (495 lines, 8 tests)
**Purpose**: Validate metadata compatibility with multiple standards

**Coverage Areas**:
- STAC 1.0 crosswalk (2 tests)
- OGC API Features crosswalk (1 test)
- WFS 2.0 crosswalk (1 test)
- WMS 1.3 crosswalk (1 test)
- CSW 2.0.2 crosswalk (1 test)
- Geoservices REST a.k.a. Geoservices REST a.k.a. Esri REST API crosswalk (1 test)
- Multi-protocol validation (1 test)

**Standards Coverage**:

#### STAC (SpatioTemporal Asset Catalog)
- Required: id, title, bbox, source.uri
- Recommended: description, keywords, temporal extent, thumbnail
- Tests validate raster datasets can be transformed to STAC items

#### OGC API Features
- Required: id, title, geometryType, geometryField, bbox
- Recommended: description, keywords, fields, CRS
- Tests validate layer metadata supports OGC collections

#### WFS (Web Feature Service) 2.0
- Required: id, title, geometryType, geometryField, bbox, fields
- Recommended: CRS, description
- Tests validate XSD schema generation requirements

#### WMS (Web Map Service) 1.3
- Required: id, title, bbox, CRS
- Recommended: description, keywords, styles, scale denominators
- Tests validate layer can be served via WMS

#### CSW (Catalog Service for the Web) 2.0.2
- Required: id, title
- Recommended: description/summary, keywords, bbox, links
- Tests validate Dublin Core and ISO 19115 mapping

#### Geoservices REST a.k.a. Geoservices REST a.k.a. Esri REST API (FeatureServer)
- Required: id, title, geometryType, idField, geometryField, bbox, srid
- Recommended: displayField, fields
- Tests validate layer metadata for Esri services

**Multi-Protocol Test**:
- Creates comprehensive layer metadata
- Validates against 5 protocols simultaneously
- Ensures metadata is complete for all major standards

## Test Statistics

- **Total Test Files**: 4 new files (+ 8 existing metadata tests)
- **Total Test Methods**: 78 tests (47 + 10 + 13 + 8)
- **Total Lines of Code**: 2,499 lines (new tests only)
- **Sample Metadata Files**: 8 files (2 valid, 6 invalid scenarios)
- **Test Data Directory**: `/tests/Honua.Server.Core.Tests/Metadata/TestData/`

## Coverage Areas

### Parsing & Syntax
- ✅ Valid JSON parsing
- ✅ Valid YAML parsing
- ✅ Invalid JSON syntax detection
- ✅ Invalid YAML syntax detection
- ✅ Empty payload detection
- ✅ JSON comments support
- ✅ Trailing commas support

### Schema Validation
- ✅ Required field validation (catalog, folders, dataSources, services, layers)
- ✅ ID uniqueness validation
- ✅ Reference integrity (folder→service, service→dataSource, layer→service, layer→style)
- ✅ Data type validation (string, int, double, datetime)

### Bbox Validation
- ✅ Minimum 4 coordinates required
- ✅ 4-value bbox support (2D)
- ✅ 6-value bbox support (3D)
- ✅ Multiple bbox support
- ✅ Bbox in layer extents
- ✅ Bbox in catalog extents

### CRS Validation
- ✅ EPSG code format (EPSG:4326)
- ✅ OGC URI format (http://www.opengis.net/def/crs/OGC/1.3/CRS84)
- ✅ Multiple CRS support
- ✅ CRS in layer definitions
- ✅ CRS in extent definitions

### Temporal Extent Validation
- ✅ ISO 8601 datetime format
- ✅ Open-ended intervals (using "..")
- ✅ Multiple temporal intervals
- ✅ Custom temporal reference systems
- ✅ Default TRS (Gregorian)
- ✅ Invalid datetime detection

### Layer Configuration
- ✅ Geometry type validation
- ✅ ID field requirement
- ✅ Geometry field requirement
- ✅ Field schema validation
- ✅ Editing capabilities validation
- ✅ Editing constraints validation
- ✅ Attachment configuration
- ✅ Query configuration
- ✅ Relationship definitions

### Style Validation
- ✅ Simple renderer validation
- ✅ UniqueValue renderer validation
- ✅ Required fields per renderer type
- ✅ Symbol definitions
- ✅ Style class validation
- ✅ Default style references

### Raster Dataset Validation
- ✅ Source type validation (cog, geotiff, vector)
- ✅ Source URI requirement
- ✅ Cache configuration
- ✅ Zoom level validation
- ✅ Style references

### Defaults & Inheritance
- ✅ Layer defaults (title, itemType, editing)
- ✅ Service defaults (title, type, enabled)
- ✅ Field defaults (nullable, editable)
- ✅ Style defaults (renderer, format, geometry)
- ✅ Cache defaults (enabled, preseed)
- ✅ Empty collection initialization
- ✅ Service-layer attachment

### Metadata Crosswalk
- ✅ STAC 1.0 compatibility
- ✅ OGC API Features compatibility
- ✅ WFS 2.0 compatibility
- ✅ WMS 1.3 compatibility
- ✅ CSW 2.0.2 compatibility
- ✅ Geoservices REST a.k.a. Geoservices REST a.k.a. Esri REST API compatibility
- ✅ Multi-protocol validation

### Error Reporting
- ✅ Clear error messages
- ✅ Context in error messages (field names, IDs)
- ✅ Invalid value reporting
- ✅ Multiple error accumulation

## Sample Metadata Files

### Valid Samples

#### valid-complete.json
Comprehensive 250-line example including:
- Catalog with full metadata (contact, license, extents, links)
- Server configuration (allowed hosts, CORS)
- Multiple folders (transportation, environment)
- Multiple data sources (PostGIS, SQLite)
- Service with OGC configuration
- Layer with 7 fields, editing, attachments, styles
- 2 styles (uniqueValue with 3 classes, simple line)
- Raster dataset with cache configuration

#### valid-minimal.yaml
Minimal 25-line example with:
- Basic catalog
- Single folder
- Single data source
- Single service
- Single layer with minimal fields

### Invalid Samples
Each demonstrates a specific validation error:
1. Missing catalog ID
2. Duplicate folder IDs
3. Service referencing non-existent folder
4. Invalid bbox (only 2 coordinates)
5. Invalid temporal format
6. Invalid CORS configuration

## Test Execution

Tests are designed to:
- Run in isolation (no dependencies between tests)
- Use in-memory data (no external dependencies)
- Provide clear failure messages
- Support parallel execution (xUnit collection attribute)
- Handle missing test data files gracefully

## Expected Coverage Impact

Based on the comprehensive test suite:

- **Metadata validation classes**: >80% coverage
  - `MetadataValidator`: ~90% coverage (all validation paths)
  - `JsonMetadataProvider`: ~85% coverage (parsing, defaults)
  - `YamlMetadataProvider`: ~85% coverage (YAML parsing)
  - `MetadataSchemaValidator`: ~75% coverage (schema validation)
  - `ProtocolMetadataValidator`: ~80% coverage (crosswalk validation)

- **Document classes**: ~70% coverage
  - All document types validated through parsing tests
  - Default value application tested
  - Field inheritance tested

- **Definition classes**: ~75% coverage
  - Creation through providers tested
  - Validation logic tested
  - Protocol compatibility tested

## Next Steps

To achieve >80% coverage on all metadata code:
1. Add tests for `MetadataRegistry` caching behavior
2. Add tests for `MetadataDiff` comparison logic
3. Add tests for metadata change notification
4. Add tests for metadata snapshot versioning
5. Add integration tests with actual file watchers
6. Add performance tests for large metadata files

## Benefits

This test suite provides:
1. **Comprehensive validation coverage** - All major validation paths tested
2. **Standards compliance** - Validates compatibility with 6+ geospatial standards
3. **Clear error messages** - Tests validate user-friendly error reporting
4. **Real-world scenarios** - Sample files represent actual use cases
5. **Regression protection** - Prevents breaking changes to metadata validation
6. **Documentation** - Tests serve as examples of valid metadata structure
