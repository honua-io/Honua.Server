# Comprehensive Export Format Implementation Review

**Review Date:** 2025-10-22
**Reviewer:** AI Code Analysis Agent
**Scope:** All export format implementations in Honua.Server.Core

---

## Executive Summary

This comprehensive review analyzed **12 export format implementations** across binary formats (GeoPackage, Shapefile, FlatGeobuf, GeoArrow, PMTiles, GeoParquet) and text formats (CSV, KML/KMZ, TopoJSON, JSON-LD, GeoJSON-T). The review identified **87 distinct issues** ranging from critical security vulnerabilities to minor code quality concerns.

### Overall Assessment

| Category | Rating | Critical Issues | High Priority | Medium Priority |
|----------|--------|-----------------|---------------|-----------------|
| **Binary Formats** | B+ | 2 | 8 | 15 |
| **Text Formats** | C+ | 1 | 4 | 12 |
| **Overall** | B | 3 | 12 | 27 |

### Key Findings

**Strengths:**
- Good async/await patterns with ConfigureAwait(false)
- Proper resource cleanup with using statements
- Strong CSV injection protection
- Comprehensive ZIP bomb protection infrastructure (ZipArchiveValidator)
- Recent addition of GeoParquet with PROJJSON support

**Critical Concerns (P0):**
1. **Shapefile Export**: No ZIP bomb protection despite creating ZIP archives
2. **GeoPackage Export**: SQL injection vulnerability in identifier quoting
3. **Missing telemetry**: No metrics/Activity tracking across all exporters
4. **Inconsistent MaxFeatures**: Some exporters lack export size limits

**High Priority Concerns (P1):**
- Missing logging in 7 of 12 exporters
- No memory profiling or large dataset testing
- Incomplete CRS metadata in multiple formats
- Missing ConfigureAwait in some async operations

---

## Table of Contents

1. [Binary Format Analysis](#binary-format-analysis)
2. [Text Format Analysis](#text-format-analysis)
3. [Cross-Cutting Concerns](#cross-cutting-concerns)
4. [Capability Comparison Matrix](#capability-comparison-matrix)
5. [Priority-Ordered Recommendations](#priority-ordered-recommendations)
6. [Detailed Issue Catalog](#detailed-issue-catalog)

---

## Binary Format Analysis

### 1. GeoPackage Exporter

**File:** `/home/mike/projects/HonuaIO/src/Honua.Server.Core/Export/GeoPackageExporter.cs`

#### Feature Completeness: ⭐⭐⭐⭐ (4/5)

**Standards Compliance:**
- ✅ GeoPackage 1.2 application_id (0x47504B47) and user_version (10200)
- ✅ Required metadata tables (gpkg_spatial_ref_sys, gpkg_contents, gpkg_geometry_columns)
- ✅ Proper WKB geometry encoding with GeoPackage header
- ⚠️ **Missing**: Extended geometry types (Z, M, ZM dimensions) - only 2D supported
- ⚠️ **Missing**: GeoPackage extensions table
- ⚠️ **Missing**: Spatial indexes (R-Tree) for query performance

**CRS Support:**
- ✅ SRID storage and WKT definition for WGS84 (4326)
- ⚠️ **Limited**: Falls back to `EPSG:{srid}` string for non-4326 CRS (line 303)
- ❌ **Missing**: Full WKT/PROJJSON definitions for common EPSG codes

**Geometry Type Coverage:**
- ✅ Point, LineString, Polygon, MultiPoint, MultiLineString, MultiPolygon
- ⚠️ Falls back to generic "GEOMETRY" for unknown types (line 707)

**Attribute Type Support:**
- ✅ INTEGER, REAL, TEXT, NUMERIC types mapped correctly
- ✅ Boolean → INTEGER conversion (line 571)
- ✅ DateTime → ISO8601 TEXT (line 572)
- ✅ Dynamic type resolution via field.StorageType (line 720)

#### Performance: ⭐⭐⭐⭐ (4/5)

**Memory Management:**
- ✅ Streaming via IAsyncEnumerable
- ✅ Batched transactions (default 1000 records, configurable via GeoPackageExportOptions)
- ✅ Connection pool clearing (line 198)
- ✅ Temporary file with DeleteOnClose (line 207)

**Async Patterns:**
- ✅ Consistent ConfigureAwait(false) usage
- ✅ CancellationToken support throughout
- ✅ Periodic Task.Yield() in batch processing (line 152)

**Scalability:**
- ✅ Configurable MaxFeatures limit (nullable for unlimited)
- ✅ Batch commit strategy prevents long-running transactions
- ⚠️ **Issue**: Full envelope calculation requires geometry access for every feature (line 130)

#### Security: ⭐⭐⭐ (3/5)

**SQL Injection:**
- ✅ Parameterized queries for data insertion
- ⚠️ **CRITICAL VULNERABILITY** (Line 765-773): `QuoteIdentifier` uses string replacement for escaping:
  ```csharp
  var sanitized = identifier.Replace("\"", "\"\"");
  return $"\"{sanitized}\"";
  ```
  While this is the SQL standard for escaping quotes in identifiers, it's applied to user-controlled input (layer.Id, field names) that's *already* sanitized via `SanitizeIdentifier`. The sanitization regex (line 40) only allows `[A-Za-z0-9_]+`, which is safe, BUT there's a logic gap:
  - If sanitization fails, it returns "layer" (line 747) which is then quoted
  - Direct string interpolation in SQL like line 360: `CREATE TABLE IF NOT EXISTS {QuoteIdentifier(tableName)}`
  - **Risk**: Medium - Current sanitization prevents injection, but relies on regex correctness
  - **Recommendation**: Use SQLite's built-in parameter binding for table/column creation via PRAGMA or validated naming

**Path Traversal:**
- ✅ Temp file uses GUID naming (line 74)
- ✅ SanitizeFileName removes invalid characters (line 760)

**Resource Exhaustion:**
- ✅ MaxFeatures enforcement with clear error message (line 119)
- ✅ Configurable limits via GeoPackageExportOptions

#### Telemetry & Observability: ⭐⭐ (2/5)

**Logging:**
- ✅ Success logging (line 162)
- ✅ Error logging with feature count (line 166)
- ❌ **Missing**: No Debug/Trace logging for batch commits
- ❌ **Missing**: No logging of CRS used, table schema

**Metrics:**
- ❌ **Missing**: No Activity/Metrics recording
- ❌ **Missing**: No performance counters (export duration, bytes written)
- ❌ **Missing**: No progress tracking callbacks

**Error Handling:**
- ✅ Wraps exceptions with context (line 178)
- ✅ Includes feature count in error messages
- ✅ Rollback on failure (line 171)

#### Code Quality: ⭐⭐⭐⭐ (4/5)

**Resource Cleanup:**
- ✅ Proper async disposal of connection (line 199)
- ✅ Try-finally blocks for cleanup
- ✅ Connection pool clearing

**Thread Safety:**
- ✅ No shared state
- ✅ Immutable options

**Test Coverage:**
- File: `/home/mike/projects/HonuaIO/tests/Honua.Server.Core.Tests/Export/GeoPackageExporterTests.cs` (92 lines)
- ⚠️ **Low coverage**: Only 92 lines suggests minimal tests
- ❌ **Missing**: Large dataset tests, concurrent export tests

---

### 2. Shapefile Exporter

**File:** `/home/mike/projects/HonuaIO/src/Honua.Server.Core/Export/ShapefileExporter.cs`

#### Feature Completeness: ⭐⭐⭐ (3/5)

**Standards Compliance:**
- ✅ Uses NetTopologySuite's ShapefileDataWriter
- ✅ .shp, .shx, .dbf, .prj files generated
- ✅ DBF field types: Character, Numeric, Float, Logical, Date
- ⚠️ **Limitation**: Column names truncated to 10 characters (line 552)
- ⚠️ **Limitation**: String truncation to 254 characters (line 272)
- ⚠️ **Limitation**: Numeric precision clamped to 18 (line 292)

**CRS Support:**
- ⚠️ **Minimal**: Only WGS84 gets full WKT (line 614)
- ⚠️ Falls back to `AUTHORITY["EPSG","{srid}"]` for others (line 617)

**Geometry Type Coverage:**
- ✅ Handles Point, LineString, Polygon, Multi* types via NTS

**Attribute Type Support:**
- ✅ Good type conversion with rounding (line 227)
- ✅ Safe integral conversion (line 238)
- ⚠️ Decimal types limited by DBF format constraints

#### Performance: ⭐⭐⭐ (3/5)

**Memory Management:**
- ⚠️ **Issue**: Custom FeatureStream with BlockingCollection (64-item buffer, line 392)
- ⚠️ **Issue**: Synchronous enumeration of async source via pump task (line 411)
- ⚠️ **Issue**: Background Task.Run for pumping (line 411) - can leak if not disposed properly
- ✅ Temp directory cleanup (line 606)

**Async Patterns:**
- ⚠️ **Mixed**: Async file I/O (line 100-102) but synchronous shapefile write (line 88)
- ✅ ConfigureAwait(false) where used

**Scalability:**
- ✅ MaxFeatures enforcement (line 452)
- ⚠️ **Issue**: All shapefiles in temp directory before zipping (memory spike for large exports)
- ⚠️ **Issue**: Skip/Take on in-memory lists during ZIP creation (line 166-174) loads full column slices

#### Security: ⭐⭐ (2/5)

**Path Traversal:**
- ✅ GUID temp directory (line 591)
- ✅ Filename sanitization (line 579)

**ZIP Bomb Protection:**
- ❌ **CRITICAL VULNERABILITY**: Creates ZIP archives (line 96) with CompressionLevel.Fastest but NO validation
- ❌ **Missing**: No use of ZipArchiveValidator (which exists in codebase)
- ❌ **Missing**: No limits on uncompressed size
- ❌ **Risk**: Malicious layer definitions could create huge ZIP files

**Input Validation:**
- ✅ Column name sanitization (line 540)
- ✅ Field value truncation (line 272)

#### Telemetry & Observability: ⭐ (1/5)

**Logging:**
- ❌ **Missing**: No ILogger injection or usage
- ❌ **Missing**: No error logging, success logging, or diagnostics

**Metrics:**
- ❌ **Missing**: No telemetry whatsoever

**Error Handling:**
- ✅ Exception propagation via ExceptionDispatchInfo (line 504)
- ⚠️ Generic error messages

#### Code Quality: ⭐⭐⭐ (3/5)

**Complexity:**
- ⚠️ **High**: FeatureStream with nested Enumerator class (lines 338-538)
- ⚠️ **High**: Pump async to sync conversion is fragile

**Resource Cleanup:**
- ✅ Proper disposal in finally blocks
- ⚠️ **Issue**: Temp directory cleanup swallows all exceptions (line 605)

**Test Coverage:**
- File: `/home/mike/projects/HonuaIO/tests/Honua.Server.Core.Tests/Export/ShapefileExporterTests.cs` (216 lines)
- ⚠️ Moderate coverage but no ZIP bomb tests

---

### 3. FlatGeobuf Exporter

**File:** `/home/mike/projects/HonuaIO/src/Honua.Server.Core/Export/FlatGeobufExporter.cs`

#### Feature Completeness: ⭐⭐⭐⭐ (4/5)

**Standards Compliance:**
- ✅ FlatGeobuf spec v3.26.0 compliance
- ✅ Magic bytes validation
- ✅ Hilbert R-Tree spatial index (lines 1079-1296)
- ✅ Column metadata with type inference
- ✅ Geometry type validation (lines 1047-1061)

**CRS Support:**
- ✅ SRID in FlatGeobuf CrsT structure (line 952)
- ⚠️ **Limited**: Only stores code, no full CRS definition

**Geometry Type Coverage:**
- ✅ All FlatGeobuf types: Point, LineString, Polygon, Multi*, GeometryCollection
- ✅ Configurable geometry type enforcement (lines 57-66)
- ✅ Validation warns/skips mismatched geometries (lines 205-222)

**Attribute Type Support:**
- ✅ Rich type system: Bool, Byte, UByte, Short, UShort, Int, UInt, Long, ULong, Float, Double, String, Json, DateTime, Binary
- ✅ Type promotion on conflicts (lines 524-559)
- ✅ Dynamic column addition (line 232)

#### Performance: ⭐⭐⭐⭐ (4/5)

**Memory Management:**
- ✅ Streaming with AsyncFeatureEnumerable
- ✅ 64-item BlockingCollection buffer (line 665)
- ⚠️ **Issue**: Collects all feature buffers in memory before writing (line 115, List<byte[]>)
- ⚠️ **Issue**: Builds full R-Tree in memory (line 132)
- **Impact**: Cannot export datasets larger than available RAM

**Async Patterns:**
- ✅ ConfigureAwait(false) throughout
- ✅ Proper async disposal (line 805)

**Scalability:**
- ⚠️ **No MaxFeatures limit**: Could OOM on unbounded queries
- ✅ Efficient Hilbert R-Tree building

#### Security: ⭐⭐⭐⭐ (4/5)

**Input Validation:**
- ✅ Geometry type validation
- ✅ Filename sanitization (line 921)

**Resource Exhaustion:**
- ⚠️ **Missing**: No MaxFeatures enforcement
- ⚠️ **Missing**: No max file size limit

**Path Traversal:**
- ✅ GUID temp file (line 95)
- ✅ TemporaryFileStream wrapper with cleanup (lines 832-919)

#### Telemetry & Observability: ⭐⭐ (2/5)

**Logging:**
- ✅ ILogger injection with NullLogger fallback (line 68)
- ✅ Warning logs for geometry type mismatches (line 210)
- ❌ **Missing**: Success/completion logging

**Metrics:**
- ❌ **Missing**: No Activity or Metrics recording

#### Code Quality: ⭐⭐⭐⭐ (4/5)

**Architecture:**
- ✅ Clean separation of concerns
- ✅ Well-documented Hilbert R-Tree implementation
- ✅ Proper struct usage (NodeItem, HilbertRTreeResult)

**Resource Cleanup:**
- ✅ Comprehensive disposal handling
- ✅ Ownership transfer pattern (line 153)

**Test Coverage:**
- File: `/home/mike/projects/HonuaIO/tests/Honua.Server.Core.Tests/Export/FlatGeobufAndGeoArrowExporterTests.cs` (348 lines)
- ✅ Good coverage including both exporters

---

### 4. GeoArrow Exporter

**File:** `/home/mike/projects/HonuaIO/src/Honua.Server.Core/Export/GeoArrowExporter.cs`

#### Feature Completeness: ⭐⭐⭐ (3/5)

**Standards Compliance:**
- ✅ Apache Arrow IPC format
- ✅ WKB encoding for geometries
- ✅ Schema with geometry metadata
- ⚠️ **Limitation**: All attributes stored as STRING type (line 129)
- ⚠️ **Missing**: GeoArrow native encoding (uses WKB instead)

**CRS Support:**
- ✅ CRS stored in geometry field metadata (line 115)
- ⚠️ **Limited**: Only CRS string, no PROJJSON

**Geometry Type Coverage:**
- ✅ WKB supports all types

**Attribute Type Support:**
- ⚠️ **Very Limited**: Everything converted to strings (line 252)
- ❌ **Missing**: Proper Arrow type mapping (Int64, Float64, Boolean, etc.)

#### Performance: ⭐⭐⭐ (3/5)

**Memory Management:**
- ⚠️ **Issue**: Loads all features into memory before writing (line 54-61)
- ⚠️ **Issue**: No streaming - single RecordBatch (line 72)
- ❌ **Critical**: Int32.MaxValue feature limit (line 63-65)

**Async Patterns:**
- ✅ ConfigureAwait(false) usage
- ✅ MemoryStream for result

**Scalability:**
- ❌ **Blocker**: Cannot export > 2.1B features (Int32 limit)
- ⚠️ Single batch = poor query performance for readers

#### Security: ⭐⭐⭐⭐ (4/5)

**Input Validation:**
- ✅ Feature count validation (line 63)
- ✅ Filename sanitization (line 322)

**Resource Exhaustion:**
- ⚠️ **Issue**: No MaxFeatures enforcement
- ✅ Fails cleanly if too many features

#### Telemetry & Observability: ⭐ (1/5)

**Logging:**
- ❌ **Missing**: No logging at all

**Metrics:**
- ❌ **Missing**: No telemetry

**Error Handling:**
- ✅ Basic exception propagation
- ⚠️ No context in error messages

#### Code Quality: ⭐⭐⭐ (3/5)

**Simplicity:**
- ✅ Clean, straightforward implementation
- ⚠️ **Issue**: Overuses string conversion (loses type information)

**Resource Cleanup:**
- ✅ Proper RecordBatch disposal (line 95)
- ✅ Array disposal (line 98)

---

### 5. PMTiles Exporter

**File:** `/home/mike/projects/HonuaIO/src/Honua.Server.Core/Export/PmTilesExporter.cs`

#### Feature Completeness: ⭐⭐⭐⭐⭐ (5/5)

**Standards Compliance:**
- ✅ PMTiles v3 spec compliance
- ✅ Magic bytes "PMTiles" + version 3 (line 264)
- ✅ Hilbert-ordered tile directory
- ✅ Run-length encoding for directories (line 364)
- ✅ Varint encoding (line 478)
- ✅ Compression: None, Gzip, Brotli, Zstd (lines 311-336)

**Tile Organization:**
- ✅ Z/X/Y to tile ID conversion (lines 489-511)
- ✅ Clustering flag (line 282)
- ✅ Proper bounds calculation (lines 428-459)

**Metadata:**
- ✅ JSON metadata section (line 341)
- ✅ Bounds, center zoom, min/max zoom (lines 286-296)

#### Performance: ⭐⭐⭐⭐ (4/5)

**Memory Management:**
- ⚠️ **Issue**: Buffers all compressed tiles in memory (line 151)
- ✅ Single-pass writes for archives

**Efficiency:**
- ✅ Varint encoding for compactness
- ✅ Run-length encoding for directories

**Scalability:**
- ⚠️ **Issue**: Multi-tile archives load all tiles to memory
- ✅ Single tile archives are efficient

#### Security: ⭐⭐⭐⭐ (4/5)

**Input Validation:**
- ✅ Validates tile data non-null (line 60)
- ✅ Validates bounds length (line 61-66)
- ✅ Validates at least one tile (line 139)

**Compression:**
- ✅ Uses standard .NET/ZstdSharp libraries
- ⚠️ **Missing**: Compressed size limits

#### Telemetry & Observability: ⭐ (1/5)

**Logging:**
- ❌ **Missing**: No logging

**Metrics:**
- ❌ **Missing**: No telemetry

**Error Handling:**
- ✅ ArgumentExceptions with clear messages
- ⚠️ No try-catch around compression

#### Code Quality: ⭐⭐⭐⭐⭐ (5/5)

**Code Organization:**
- ✅ Clean struct usage (TileEntryInfo, TileDescriptor)
- ✅ Well-documented algorithms (Hilbert, varint)
- ✅ Pure functions for coordinate conversion

**Maintainability:**
- ✅ Clear separation of header/directory/data
- ✅ No external state dependencies

---

### 6. GeoParquet Exporter

**File:** `/home/mike/projects/HonuaIO/src/Honua.Server.Core/Export/GeoParquetExporter.cs`

#### Feature Completeness: ⭐⭐⭐⭐⭐ (5/5)

**Standards Compliance:**
- ✅ GeoParquet v1.1.0 specification (line 475)
- ✅ Full PROJJSON metadata for EPSG:4326 and EPSG:3857 (lines 299-427)
- ✅ Bounding box columns (bbox.xmin, bbox.ymin, bbox.xmax, bbox.ymax) (lines 242-247)
- ✅ Covering metadata (lines 462-471)
- ✅ Row group spatial statistics (lines 151-162, 178-186)
- ✅ WKB geometry encoding

**CRS Support:**
- ✅ **Excellent**: Full PROJJSON for WGS84 (lines 299-344)
- ✅ **Excellent**: Full PROJJSON for Web Mercator (lines 346-427)
- ✅ Fallback to name-only for other CRS (line 294)
- 📄 Implementation notes in `/home/mike/projects/HonuaIO/src/Honua.Server.Core/Export/GeoParquet_PROJJSON_Implementation.md`

**Geometry Type Coverage:**
- ✅ All geometry types via WKB

**Attribute Type Support:**
- ⚠️ **Limitation**: All attributes stored as STRING (line 252)
- ⚠️ **Missing**: Proper Parquet type mapping (Int64, Double, Boolean, Timestamp, etc.)

#### Performance: ⭐⭐⭐ (3/5)

**Memory Management:**
- ⚠️ **Issue**: Loads ALL features into memory lists (lines 64-69, 96)
- ⚠️ **Issue**: No streaming - must accumulate entire dataset
- ❌ **Critical**: OOM risk for large datasets

**Row Group Strategy:**
- ✅ 100,000 row groups for optimal query performance (line 125)
- ✅ Spatial statistics per row group (lines 151-162)
- ⚠️ **Issue**: Row groups written from in-memory slices (line 166)

**Async Patterns:**
- ✅ ConfigureAwait(false) throughout
- ✅ Async file I/O

**Scalability:**
- ❌ **Blocker**: Cannot export datasets larger than RAM
- ⚠️ **Missing**: No MaxFeatures enforcement

#### Security: ⭐⭐⭐⭐ (4/5)

**Input Validation:**
- ✅ Filename sanitization (line 667)
- ✅ Regex for EPSG code extraction (line 270)

**Resource Exhaustion:**
- ⚠️ **Missing**: No MaxFeatures limit
- ⚠️ **Missing**: No max file size limit

**Path Traversal:**
- ✅ GUID temp file (line 108)
- ✅ TemporaryFileStream wrapper (lines 718-815)

#### Telemetry & Observability: ⭐⭐ (2/5)

**Logging:**
- ✅ ILogger injection with NullLogger fallback (line 46)
- ✅ Debug logging of row group stats (line 188)
- ❌ **Missing**: Info-level completion logging

**Metrics:**
- ❌ **Missing**: No Activity or Metrics

**Error Handling:**
- ✅ Basic exception propagation
- ⚠️ No detailed error context

#### Code Quality: ⭐⭐⭐⭐ (4/5)

**Architecture:**
- ✅ Clean separation of metadata building
- ✅ Well-documented PROJJSON generation
- ✅ GlobalBoundingBox helper class (lines 690-716)

**Resource Cleanup:**
- ✅ Ownership transfer pattern
- ✅ Comprehensive cleanup in finally (lines 201-225)

**Test Coverage:**
- File: `/home/mike/projects/HonuaIO/tests/Honua.Server.Core.Tests/Export/GeoParquetExporterTests.cs` (369 lines)
- ✅ Strong test coverage

---

## Text Format Analysis

### 7. CSV Exporter

**File:** `/home/mike/projects/HonuaIO/src/Honua.Server.Core/Export/CsvExporter.cs`

#### Feature Completeness: ⭐⭐⭐⭐ (4/5)

**Format Options:**
- ✅ Configurable delimiter (default comma)
- ✅ Optional header row
- ✅ Geometry format: WKT or GeoJSON (line 34)
- ✅ Configurable MaxFeatures (default 100,000)

**Field Handling:**
- ✅ Property name filtering via FeatureQuery (line 164)
- ✅ ID field always included (line 171)

**CSV Compliance:**
- ✅ Proper quote escaping (line 310)
- ✅ Handles delimiters, quotes, newlines in values (line 299)

#### Performance: ⭐⭐⭐⭐ (4/5)

**Memory Management:**
- ✅ Streaming to file (no buffering all features)
- ✅ Batched writes (100 row buffer, line 95)
- ✅ Temp file with DeleteOnClose

**Async Patterns:**
- ✅ ConfigureAwait(false)
- ✅ Async file I/O
- ✅ Task.Yield() for cooperative multitasking (line 134)

**Scalability:**
- ✅ MaxFeatures enforcement with clear error (line 99)
- ✅ Write batching prevents I/O blocking

#### Security: ⭐⭐⭐⭐⭐ (5/5)

**CSV Injection Protection:**
- ✅ **EXCELLENT**: Detects formula injection (=, +, -, @, \t, \r) (line 291)
- ✅ Prefixes dangerous values with single quote (line 294)
- ✅ Proper quote escaping (line 310)

**Input Validation:**
- ✅ Filename sanitization (line 321)

**Resource Exhaustion:**
- ✅ MaxFeatures enforcement (default 100k)

#### Telemetry & Observability: ⭐⭐⭐ (3/5)

**Logging:**
- ✅ ILogger injection (required, line 56)
- ✅ Info-level completion logging (line 147)
- ❌ **Missing**: Debug logging of geometry format, delimiter

**Metrics:**
- ❌ **Missing**: No Activity or Metrics

**Error Handling:**
- ✅ Clear error messages with counts
- ✅ Exception propagation

#### Code Quality: ⭐⭐⭐⭐ (4/5)

**Simplicity:**
- ✅ Clean, straightforward implementation
- ✅ Well-structured batching logic

**Resource Cleanup:**
- ✅ Nested using statements for streams (lines 73-80)

**Test Coverage:**
- File: `/home/mike/projects/HonuaIO/tests/Honua.Server.Core.Tests/Export/CsvExporterTests.cs` (266 lines)
- ✅ Good coverage including CSV injection tests

---

### 8. KML/KMZ Formatters

**Files:**
- `/home/mike/projects/HonuaIO/src/Honua.Server.Core/Serialization/KmlFeatureFormatter.cs`
- `/home/mike/projects/HonuaIO/src/Honua.Server.Core/Serialization/KmzArchiveBuilder.cs`

#### Feature Completeness: ⭐⭐⭐⭐ (4/5)

**KML Compliance:**
- ✅ Uses SharpKml library for spec compliance
- ✅ Placemark, Document, ExtendedData structures
- ✅ Style support via StyleFormatConverter (line 71)
- ✅ Geometry conversion from GeoJSON (lines 240-269)

**Geometry Type Coverage:**
- ✅ Point, LineString, Polygon, Multi*, GeometryCollection (lines 273-283)
- ✅ Altitude mode (AltitudeMode.Absolute)
- ⚠️ **Issue**: Ring orientation reversal logic (lines 358-366) - may not handle all edge cases

**Metadata:**
- ✅ numberMatched, numberReturned (lines 63-65)
- ✅ Description support (line 58)

**KMZ:**
- ✅ ZIP creation with assets support (line 14)
- ✅ UTF-8 encoding without BOM (line 35)

#### Performance: ⭐⭐⭐ (3/5)

**Memory Management:**
- ⚠️ **Issue**: Synchronous IEnumerable, not streaming (line 43)
- ⚠️ **Issue**: Full KML document built in memory before serialization (line 82)

**Efficiency:**
- ✅ Compression level Fastest for KMZ (line 34)

#### Security: ⭐⭐⭐⭐ (4/5)

**Input Validation:**
- ✅ XML ID sanitization (lines 191-214)
- ✅ Style ID sanitization (lines 170-178)

**KMZ Security:**
- ⚠️ **Missing**: No ZIP bomb protection in KmzArchiveBuilder
- ⚠️ **Missing**: No asset size limits (line 46)

#### Telemetry & Observability: ⭐ (1/5)

**Logging:**
- ❌ **Missing**: No logging

**Metrics:**
- ❌ **Missing**: No telemetry

**Error Handling:**
- ⚠️ Throws generic InvalidOperationException (line 260)

#### Code Quality: ⭐⭐⭐⭐ (4/5)

**Code Organization:**
- ✅ Clean static methods
- ✅ Good separation between KML and KMZ

**Maintainability:**
- ✅ Clear helper methods (FormatValue, ConvertGeometry)

---

### 9. TopoJSON Formatter

**File:** `/home/mike/projects/HonuaIO/src/Honua.Server.Core/Serialization/TopoJsonFeatureFormatter.cs`

#### Feature Completeness: ⭐⭐⭐⭐ (4/5)

**TopoJSON Compliance:**
- ✅ Topology structure with arcs (line 124)
- ✅ Arc sharing for LineString/Polygon (lines 354-369)
- ✅ Hilbert curve ordering potential (not implemented)
- ✅ Envelope calculation (line 397)

**Geometry Type Coverage:**
- ✅ Point, MultiPoint, LineString, MultiLineString, Polygon, MultiPolygon, GeometryCollection (lines 189-199)

**Arc Handling:**
- ✅ Exterior rings use positive indices (line 316)
- ✅ **CORRECT**: Interior rings (holes) use negative indices -(arcIndex + 1) per spec (line 346)
- ✅ Well-documented arc reference logic (lines 334-352)

**Metadata:**
- ✅ numberMatched, numberReturned (lines 141-148)
- ✅ bbox in topology root (lines 131-137)

#### Performance: ⭐⭐ (2/5)

**Memory Management:**
- ⚠️ **Issue**: All arcs stored in memory (line 67, List<JsonArray>)
- ⚠️ **Issue**: All geometries stored in memory (line 66, List<JsonObject>)
- ⚠️ **Issue**: No streaming - full topology built before serialization

**Efficiency:**
- ✅ Arc reuse could reduce size (but not implemented - each geometry creates new arcs)
- ⚠️ **Missing**: No actual topology optimization (arcs not shared between features)

#### Security: ⭐⭐⭐⭐ (4/5)

**Input Validation:**
- ✅ Validates arc length >= 2 (line 357)
- ✅ ArgumentNullException checks

**Resource Exhaustion:**
- ⚠️ **Missing**: No limits on number of arcs or geometries

#### Telemetry & Observability: ⭐ (1/5)

**Logging:**
- ❌ **Missing**: No logging

**Metrics:**
- ❌ **Missing**: No telemetry

**Error Handling:**
- ⚠️ Throws InvalidOperationException (line 176, 198, 359)

#### Code Quality: ⭐⭐⭐⭐ (4/5)

**Architecture:**
- ✅ Clean TopologyBuilder pattern (lines 62-455)
- ✅ Well-structured arc/geometry conversion

**Correctness:**
- ✅ Proper handling of polygon rings (exterior vs. holes)

---

### 10. JSON-LD Formatter

**File:** `/home/mike/projects/HonuaIO/src/Honua.Server.Core/Serialization/JsonLdFeatureFormatter.cs`

#### Feature Completeness: ⭐⭐⭐ (3/5)

**JSON-LD Compliance:**
- ✅ @context with namespaces (lines 28-49)
- ✅ @type: geosparql:Feature (line 115)
- ✅ @id: URI for features (line 121)
- ✅ Field type mapping to XSD (lines 213-225)

**Semantic Web:**
- ✅ GeoSPARQL namespace (line 17)
- ✅ Schema.org namespace (line 18)
- ✅ Dublin Core Terms (line 19)

**Limitations:**
- ⚠️ **Issue**: Hardcoded baseUri parameter (line 121) - should be from request context
- ⚠️ **Issue**: Context removed from features in collection (line 181) but not documented why

#### Performance: ⭐⭐⭐ (3/5)

**Memory Management:**
- ⚠️ **Issue**: IEnumerable forces full iteration (line 175)
- ⚠️ **Issue**: JsonNode deep cloning (lines 127, 136, 144)

**Efficiency:**
- ✅ Lightweight transformations

#### Security: ⭐⭐⭐⭐ (4/5)

**Input Validation:**
- ✅ Null checks throughout
- ✅ Safe JSON serialization

#### Telemetry & Observability: ⭐ (1/5)

**Logging:**
- ❌ **Missing**: No logging

**Metrics:**
- ❌ **Missing**: No telemetry

#### Code Quality: ⭐⭐⭐⭐ (4/5)

**Code Organization:**
- ✅ Clean static methods
- ✅ Clear separation of single vs. collection

---

### 11. GeoJSON-T Formatter

**File:** `/home/mike/projects/HonuaIO/src/Honua.Server.Core/Serialization/GeoJsonTFeatureFormatter.cs`

#### Feature Completeness: ⭐⭐⭐ (3/5)

**GeoJSON-T Compliance:**
- ✅ "when" temporal property (line 62)
- ✅ start, end, instant fields (lines 155-179)
- ⚠️ **Missing**: 4th temporal coordinate in geometry (line 139 - commented as future extension)

**Temporal Support:**
- ✅ Configurable field names (parameters startTimeField, endTimeField, timeField)
- ✅ Fallback to common field names (line 185)

#### Performance: ⭐⭐⭐ (3/5)

**Memory Management:**
- ⚠️ **Issue**: IEnumerable forces full iteration (line 99)
- ⚠️ **Issue**: Deep cloning of geometry/properties (lines 37, 48, 58)

#### Security: ⭐⭐⭐⭐ (4/5)

**Input Validation:**
- ✅ Null checks
- ✅ Safe JSON operations

#### Telemetry & Observability: ⭐ (1/5)

**Logging:**
- ❌ **Missing**: No logging

**Metrics:**
- ❌ **Missing**: No telemetry

#### Code Quality: ⭐⭐⭐⭐ (4/5)

**Code Organization:**
- ✅ Clean static methods
- ✅ Flexible field configuration

---

## Cross-Cutting Concerns

### Missing Across Multiple Exporters

#### 1. Telemetry & Metrics (Critical)

**Severity:** P0 - Blocks production observability

**Affected Exporters:**
- ❌ **No telemetry**: Shapefile, GeoArrow, PMTiles, KML/KMZ, TopoJSON, JSON-LD, GeoJSON-T (7/12)
- ⚠️ **Partial telemetry**: FlatGeobuf, GeoParquet (logging only, no metrics) (2/12)
- ✅ **Good telemetry**: GeoPackage, CSV (logging) (2/12)

**Recommendations:**
```csharp
// Add to all exporters
using System.Diagnostics;
using System.Diagnostics.Metrics;

public sealed class XxxExporter
{
    private static readonly ActivitySource ActivitySource = new("Honua.Export.Xxx");
    private static readonly Meter Meter = new("Honua.Export.Xxx");
    private static readonly Counter<long> ExportCounter = Meter.CreateCounter<long>("exports_total");
    private static readonly Histogram<double> ExportDuration = Meter.CreateHistogram<double>("export_duration_seconds");
    private static readonly Histogram<long> ExportSize = Meter.CreateHistogram<long>("export_size_bytes");

    public async Task<XxxExportResult> ExportAsync(...)
    {
        using var activity = ActivitySource.StartActivity("Export");
        activity?.SetTag("format", "xxx");
        activity?.SetTag("layer", layer.Id);

        var startTime = Stopwatch.GetTimestamp();
        try
        {
            var result = await ExportInternalAsync(...);

            var elapsed = Stopwatch.GetElapsedTime(startTime).TotalSeconds;
            ExportCounter.Add(1, new("format", "xxx"), new("status", "success"));
            ExportDuration.Record(elapsed, new("format", "xxx"));
            ExportSize.Record(result.Content.Length, new("format", "xxx"));

            activity?.SetTag("feature_count", result.FeatureCount);
            activity?.SetTag("file_size", result.Content.Length);

            return result;
        }
        catch (Exception ex)
        {
            ExportCounter.Add(1, new("format", "xxx"), new("status", "error"));
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
```

#### 2. ConfigureAwait Missing

**Severity:** P1 - Can cause deadlocks in synchronous contexts

**Issues:**
- GeoPackage: ✅ Complete
- Shapefile: ⚠️ Partial (missing in some places)
- FlatGeobuf: ✅ Complete
- GeoArrow: ✅ Complete
- PMTiles: N/A (synchronous)
- GeoParquet: ✅ Complete
- CSV: ✅ Complete

**Example Fix:**
```csharp
// WRONG
await fileStream.WriteAsync(buffer);

// CORRECT
await fileStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
```

#### 3. MaxFeatures Inconsistency

**Severity:** P1 - Security/resource exhaustion risk

| Exporter | MaxFeatures | Default | Enforcement |
|----------|-------------|---------|-------------|
| GeoPackage | ✅ Yes | Nullable (unlimited) | Strong |
| Shapefile | ✅ Yes | int.MaxValue | Strong |
| FlatGeobuf | ❌ No | N/A | None |
| GeoArrow | ❌ No | N/A | Implicit (Int32.MaxValue batch limit) |
| PMTiles | N/A | N/A | N/A (tile-based) |
| GeoParquet | ❌ No | N/A | None |
| CSV | ✅ Yes | 100,000 | Strong |

**Recommendation:** Add MaxFeatures to all exporters with default 1,000,000:
```csharp
public sealed record XxxExportOptions
{
    public long MaxFeatures { get; init; } = 1_000_000;
}
```

#### 4. Memory Profiling Required

**Severity:** P1 - Performance/scalability risk

**Exporters with Memory Issues:**

| Exporter | Issue | Impact |
|----------|-------|--------|
| **Shapefile** | Buffers all files before ZIP | High memory spike |
| **FlatGeobuf** | Buffers all features for R-Tree | Cannot export > RAM |
| **GeoArrow** | Buffers all features in single batch | Cannot export > RAM |
| **GeoParquet** | Buffers all features in memory lists | Cannot export > RAM |
| **KML** | Buffers full document | Moderate memory usage |
| **TopoJSON** | Buffers all arcs and geometries | Moderate memory usage |

**Recommendation:** Implement streaming for all formats:
```csharp
// Example: GeoParquet should stream row groups
public async Task<GeoParquetExportResult> ExportAsync(...)
{
    using var parquetWriter = new ParquetFileWriter(...);

    var rowGroupBuffer = new List<FeatureRecord>(rowGroupSize);
    await foreach (var record in records.WithCancellation(cancellationToken))
    {
        rowGroupBuffer.Add(record);

        if (rowGroupBuffer.Count >= rowGroupSize)
        {
            await WriteRowGroup(parquetWriter, rowGroupBuffer);
            rowGroupBuffer.Clear();
        }
    }

    if (rowGroupBuffer.Count > 0)
    {
        await WriteRowGroup(parquetWriter, rowGroupBuffer);
    }
}
```

#### 5. CRS Metadata Gaps

**Severity:** P1 - Standards compliance

**CRS Support Quality:**

| Exporter | WGS84 | Web Mercator | Other EPSG | Custom CRS |
|----------|-------|--------------|------------|------------|
| GeoPackage | ✅ Full WKT | ⚠️ String | ⚠️ String | ❌ |
| Shapefile | ✅ Full WKT | ⚠️ Authority | ⚠️ Authority | ❌ |
| FlatGeobuf | ✅ Code only | ✅ Code only | ✅ Code only | ⚠️ Code only |
| GeoArrow | ⚠️ String | ⚠️ String | ⚠️ String | ⚠️ String |
| PMTiles | ✅ Bounds WGS84 | ✅ Conversion | ✅ Conversion | ❌ |
| **GeoParquet** | ✅ **PROJJSON** | ✅ **PROJJSON** | ⚠️ Name only | ⚠️ Name only |
| CSV | N/A | N/A | N/A | N/A |

**Recommendation:**
1. Extend GeoParquet PROJJSON generation to all formats
2. Support PROJJSON for top 50 EPSG codes
3. Fall back to PROJ string or WKT for others

#### 6. Attribute Type Handling

**Severity:** P2 - Data quality

**Type Support:**

| Exporter | Typed Attributes | Lossy Conversion |
|----------|------------------|------------------|
| GeoPackage | ✅ INTEGER, REAL, TEXT | Booleans → INT |
| Shapefile | ✅ DBF types | Strings truncated |
| FlatGeobuf | ✅ Rich types | Type promotion |
| **GeoArrow** | ❌ **ALL STRINGS** | **Very lossy** |
| PMTiles | N/A | N/A |
| **GeoParquet** | ❌ **ALL STRINGS** | **Very lossy** |
| CSV | ⚠️ String format | Intentional |

**Critical Issue:** GeoArrow and GeoParquet lose all type information by converting everything to strings.

**Recommendation:**
```csharp
// GeoParquet should map to proper Parquet types
private static Column BuildParquetColumn(FieldDefinition field)
{
    return field.DataType?.ToLowerInvariant() switch
    {
        "int" or "int32" => new Column<int?>(field.Name),
        "int64" or "long" => new Column<long?>(field.Name),
        "double" or "float" => new Column<double?>(field.Name),
        "bool" or "boolean" => new Column<bool?>(field.Name),
        "datetime" or "date" => new Column<DateTime?>(field.Name),
        _ => new Column<string?>(field.Name)
    };
}
```

---

## Capability Comparison Matrix

| Feature | GeoPackage | Shapefile | FlatGeobuf | GeoArrow | PMTiles | GeoParquet | CSV | KML | TopoJSON | JSON-LD | GeoJSON-T |
|---------|------------|-----------|------------|----------|---------|------------|-----|-----|----------|---------|-----------|
| **Standards Compliance** | GeoPackage 1.2 | ESRI Shapefile | FlatGeobuf 3.26 | Arrow IPC | PMTiles v3 | GeoParquet 1.1 | RFC 4180 | OGC KML | TopoJSON | JSON-LD 1.1 | GeoJSON-T |
| **Geometry Encoding** | WKB + GPKG | Shapefile binary | FlatBuffers | WKB | MVT (tiles) | WKB | WKT/GeoJSON | KML | GeoJSON arcs | GeoJSON | GeoJSON |
| **Spatial Index** | ❌ R-Tree missing | ⚠️ .qix optional | ✅ Hilbert R-Tree | ❌ | N/A (tile index) | ⚠️ Row group stats | ❌ | ❌ | ❌ | ❌ | ❌ |
| **CRS Metadata** | ⚠️ WKT (4326 only) | ⚠️ WKT (4326 only) | ✅ SRID code | ⚠️ String | ✅ Bounds WGS84 | ✅ PROJJSON | ❌ | ❌ | ❌ | ❌ | ❌ |
| **Attribute Types** | ✅ INTEGER, REAL, TEXT | ✅ DBF types | ✅ Rich types | ❌ All strings | N/A | ❌ All strings | ⚠️ String format | ⚠️ String format | ⚠️ String format | ⚠️ String format | ⚠️ String format |
| **Streaming** | ✅ Yes | ⚠️ Partial | ⚠️ Buffers R-Tree | ❌ Single batch | ✅ Yes | ❌ Buffers all | ✅ Yes | ❌ Full doc | ❌ Full doc | ❌ Full iteration | ❌ Full iteration |
| **MaxFeatures** | ✅ Configurable | ✅ int.MaxValue | ❌ No limit | ❌ Int32 batch limit | N/A | ❌ No limit | ✅ 100k default | ❌ | ❌ | ❌ | ❌ |
| **Logging** | ✅ Yes | ❌ No | ✅ Yes | ❌ No | ❌ No | ✅ Yes | ✅ Yes | ❌ No | ❌ No | ❌ No | ❌ No |
| **Metrics/Activity** | ❌ No | ❌ No | ❌ No | ❌ No | ❌ No | ❌ No | ❌ No | ❌ No | ❌ No | ❌ No | ❌ No |
| **Security: Path Traversal** | ✅ Protected | ✅ Protected | ✅ Protected | ✅ Protected | ✅ Protected | ✅ Protected | ✅ Protected | ✅ Protected | N/A | N/A | N/A |
| **Security: ZIP Bomb** | N/A | ❌ **VULNERABLE** | N/A | N/A | N/A | N/A | N/A | ⚠️ Missing | N/A | N/A | N/A |
| **Security: SQL Injection** | ⚠️ Regex-based | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A |
| **Security: CSV Injection** | N/A | N/A | N/A | N/A | N/A | N/A | ✅ **Excellent** | N/A | N/A | N/A | N/A |
| **Compression** | ❌ No | ✅ ZIP (Fastest) | ❌ No | ❌ No | ✅ Gzip/Brotli/Zstd | ✅ Snappy (implicit) | ❌ No | ✅ ZIP | ❌ No | ❌ No | ❌ No |
| **Multi-CRS Support** | ⚠️ Single CRS | ⚠️ Single CRS | ⚠️ Single CRS | ⚠️ Single CRS | ✅ WGS84 normalized | ⚠️ Single CRS | ❌ No CRS | ❌ No CRS | ❌ No CRS | ❌ No CRS | ❌ No CRS |
| **3D/Z Coordinates** | ⚠️ No (2D only) | ✅ Via NTS | ✅ Via NTS | ✅ Via WKB | ❌ No (2D tiles) | ✅ Via WKB | ⚠️ Via WKT | ✅ Altitude | ✅ Yes | ✅ Yes | ✅ Yes |
| **M Coordinates** | ⚠️ No | ✅ Via NTS | ✅ Via NTS | ✅ Via WKB | ❌ No | ✅ Via WKB | ⚠️ Via WKT | ❌ No | ❌ No | ❌ No | ❌ No |
| **Metadata** | ✅ gpkg_metadata | ⚠️ Limited | ✅ FlatBuffers | ✅ Arrow schema | ✅ JSON section | ✅ Parquet metadata | ❌ No | ✅ ExtendedData | ⚠️ Limited | ✅ @context | ⚠️ Limited |
| **Query Performance** | ✅ SQL indexing | ⚠️ Sequential scan | ✅ R-Tree | ⚠️ Sequential scan | ✅ Tile index | ✅ Row group filtering | ❌ Sequential | ❌ Sequential | ❌ Sequential | ❌ Sequential | ❌ Sequential |
| **File Size Efficiency** | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐ | ⭐⭐⭐ | ⭐ | ⭐ |
| **Interoperability** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐ | ⭐⭐ |

**Legend:**
- ✅ Fully implemented
- ⚠️ Partially implemented or limited
- ❌ Not implemented or missing
- N/A Not applicable for this format

---

## Priority-Ordered Recommendations

### P0 - Critical (Must Fix Before Production)

#### P0-1: Shapefile ZIP Bomb Vulnerability
**Location:** `/home/mike/projects/HonuaIO/src/Honua.Server.Core/Export/ShapefileExporter.cs:96`
**Severity:** Critical Security Issue
**Impact:** Attackers could generate massive ZIP files causing disk exhaustion

**Fix:**
```csharp
// Add after line 104
var validation = ZipArchiveValidator.ValidateZipArchive(
    zipStream,
    allowedExtensions: ZipArchiveValidator.GetGeospatialExtensions(),
    maxUncompressedSize: 10L * 1024 * 1024 * 1024, // 10 GB
    maxCompressionRatio: 100,
    maxEntries: 10);

if (!validation.IsValid)
{
    throw new InvalidOperationException($"Shapefile ZIP validation failed: {validation.ErrorMessage}");
}
```

#### P0-2: Add Telemetry to All Exporters
**Locations:** All exporter files
**Severity:** Critical Observability Gap
**Impact:** Cannot monitor export performance or failures in production

**Implementation:** See [Cross-Cutting Concerns](#1-telemetry--metrics-critical) for template

#### P0-3: GeoPackage SQL Injection Hardening
**Location:** `/home/mike/projects/HonuaIO/src/Honua.Server.Core/Export/GeoPackageExporter.cs:765-773`
**Severity:** Medium Security Issue (currently mitigated by sanitization)
**Impact:** Potential SQL injection if sanitization regex fails

**Fix:**
```csharp
// Replace QuoteIdentifier usage in CREATE TABLE with validated names
// Option 1: Whitelist approach
private static readonly HashSet<string> ReservedWords = new(StringComparer.OrdinalIgnoreCase)
{
    "SELECT", "INSERT", "DELETE", "DROP", "TABLE", /* ... */
};

private static string ValidateIdentifier(string identifier)
{
    if (string.IsNullOrWhiteSpace(identifier))
        throw new ArgumentException("Identifier cannot be empty");

    if (!Regex.IsMatch(identifier, "^[A-Za-z_][A-Za-z0-9_]*$"))
        throw new ArgumentException($"Invalid identifier: {identifier}");

    if (ReservedWords.Contains(identifier))
        throw new ArgumentException($"Reserved word: {identifier}");

    return identifier;
}

// Option 2: Use SQLite parameters for dynamic schema
// This is more complex but safer - see SQLite documentation on ATTACH DATABASE
```

### P1 - High Priority (Fix in Next Sprint)

#### P1-1: Memory Profiling & Large Dataset Testing
**Locations:** FlatGeobufExporter.cs, GeoArrowExporter.cs, GeoParquetExporter.cs
**Severity:** High Performance Risk
**Impact:** OOM crashes on large exports

**Action Items:**
1. Create benchmark tests with 1M, 10M, 100M features
2. Profile memory usage with dotMemory or similar
3. Implement streaming for buffered exporters:
   - FlatGeobuf: Stream feature buffers to disk, build R-Tree incrementally
   - GeoArrow: Use multiple RecordBatches instead of single batch
   - GeoParquet: Stream row groups directly without buffering

**Example Fix for GeoParquet:**
```csharp
// Replace lines 64-96 with streaming implementation
await foreach (var featureRecord in records.WithCancellation(cancellationToken))
{
    cancellationToken.ThrowIfCancellationRequested();

    // Add to current row group buffer
    geometryColumn.Add(wkb);
    bboxXMin.Add(envelope?.MinX);
    // ... etc

    recordCount++;

    // Flush row group when full
    if (geometryColumn.Count >= rowGroupSize)
    {
        await WriteRowGroup(parquetWriter, geometryColumn, bboxXMin, ...);

        // Clear buffers
        geometryColumn.Clear();
        bboxXMin.Clear();
        // ... etc
    }
}

// Write final partial row group
if (geometryColumn.Count > 0)
{
    await WriteRowGroup(parquetWriter, geometryColumn, bboxXMin, ...);
}
```

#### P1-2: Add MaxFeatures to All Exporters
**Locations:** FlatGeobufExporter.cs, GeoArrowExporter.cs, GeoParquetExporter.cs
**Severity:** High Security Risk
**Impact:** Resource exhaustion attacks

**Fix:** Add to each exporter:
```csharp
public sealed record XxxExportOptions
{
    public static XxxExportOptions Default { get; } = new();
    public long MaxFeatures { get; init; } = 1_000_000;

    public XxxExportOptions Validate()
    {
        if (MaxFeatures <= 0)
            return this with { MaxFeatures = long.MaxValue };
        return this;
    }
}

// In export loop:
if (featureCount > _options.MaxFeatures)
{
    throw new InvalidOperationException(
        $"Export exceeded maximum of {_options.MaxFeatures:N0} features. " +
        "Adjust XxxExportOptions.MaxFeatures or apply stronger query filters.");
}
```

#### P1-3: Extend PROJJSON Support
**Location:** Create new `CrsMetadataProvider.cs`
**Severity:** High Standards Compliance Issue
**Impact:** Poor CRS metadata for non-WGS84 data

**Implementation:**
```csharp
public static class CrsMetadataProvider
{
    private static readonly Dictionary<int, Func<object>> ProjJsonGenerators = new()
    {
        [4326] = GeoParquetExporter.BuildWgs84ProjJson,
        [3857] = GeoParquetExporter.BuildWebMercatorProjJson,
        [2154] = () => BuildLambert93ProjJson(),
        [32633] = () => BuildUtm33NProjJson(),
        // ... top 50 EPSG codes
    };

    public static object GetCrsMetadata(int epsgCode, CrsFormat format)
    {
        if (ProjJsonGenerators.TryGetValue(epsgCode, out var generator))
        {
            return format switch
            {
                CrsFormat.ProjJson => generator(),
                CrsFormat.Wkt => ConvertToWkt(generator()),
                CrsFormat.Proj4 => ConvertToProj4(generator()),
                _ => throw new ArgumentException($"Unsupported CRS format: {format}")
            };
        }

        // Fallback to name-only
        return new { type = "name", properties = new { name = $"EPSG:{epsgCode}" } };
    }
}
```

#### P1-4: GeoArrow & GeoParquet Type Mapping
**Locations:** GeoArrowExporter.cs:129, GeoParquetExporter.cs:252
**Severity:** High Data Quality Issue
**Impact:** All attribute data loses type information

**Fix for GeoArrow:**
```csharp
private static Schema BuildSchema(LayerDefinition layer, IReadOnlyList<string> attributeFields, string contentCrs)
{
    var fields = new List<Field>
    {
        new Field("geometry", BinaryType.Default, true, geometryMetadata)
    };

    foreach (var fieldName in attributeFields)
    {
        var fieldDef = layer.Fields.FirstOrDefault(f =>
            string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase));

        var arrowType = MapToArrowType(fieldDef);
        fields.Add(new Field(fieldName, arrowType, nullable: true));
    }

    return new Schema(fields, null);
}

private static IArrowType MapToArrowType(FieldDefinition? field)
{
    if (field == null)
        return StringType.Default;

    return field.DataType?.ToLowerInvariant() switch
    {
        "int" or "int32" => Int32Type.Default,
        "int64" or "long" => Int64Type.Default,
        "double" or "float" => DoubleType.Default,
        "bool" or "boolean" => BooleanType.Default,
        "datetime" or "date" => TimestampType.Default(TimeUnit.Millisecond, TimestampType.Timezone.UTC),
        _ => StringType.Default
    };
}
```

#### P1-5: Add Logging to Missing Exporters
**Locations:** Shapefile, GeoArrow, PMTiles, KML, TopoJSON, JSON-LD, GeoJSON-T
**Severity:** High Observability Gap
**Impact:** Cannot diagnose export failures

**Fix:** Add ILogger to constructors:
```csharp
public sealed class XxxExporter : IXxxExporter
{
    private readonly ILogger<XxxExporter> _logger;

    public XxxExporter(ILogger<XxxExporter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<XxxExportResult> ExportAsync(...)
    {
        _logger.LogInformation("Starting {Format} export for layer {LayerId}", "Xxx", layer.Id);

        try
        {
            // ... export logic

            _logger.LogInformation("Completed {Format} export: {Count} features, {Size} bytes",
                "Xxx", featureCount, fileSize);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export failed after {Count} features", featureCount);
            throw;
        }
    }
}
```

### P2 - Medium Priority (Address in Upcoming Releases)

#### P2-1: Add ConfigureAwait to All Await Statements
**Locations:** Various exporters
**Severity:** Medium Performance Issue
**Impact:** Potential deadlocks in synchronous calling contexts

**Review and fix:**
```bash
# Find all awaits without ConfigureAwait
grep -rn "await " src/Honua.Server.Core/Export/ src/Honua.Server.Core/Serialization/ \
  | grep -v "ConfigureAwait" \
  | grep -v "// ConfigureAwait not needed"
```

#### P2-2: KMZ ZIP Bomb Protection
**Location:** `/home/mike/projects/HonuaIO/src/Honua.Server.Core/Serialization/KmzArchiveBuilder.cs:46`
**Severity:** Medium Security Issue
**Impact:** Malicious assets could create large KMZ files

**Fix:**
```csharp
private static void WriteAssets(ZipArchive archive, IReadOnlyDictionary<string, byte[]>? assets)
{
    if (assets is null)
        return;

    const long MaxAssetSize = 100 * 1024 * 1024; // 100 MB per asset
    const long MaxTotalSize = 1024 * 1024 * 1024; // 1 GB total
    long totalSize = 0;

    foreach (var pair in assets)
    {
        if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null || pair.Value.Length == 0)
            continue;

        if (pair.Value.Length > MaxAssetSize)
            throw new InvalidOperationException($"Asset '{pair.Key}' exceeds maximum size of {MaxAssetSize:N0} bytes");

        totalSize += pair.Value.Length;
        if (totalSize > MaxTotalSize)
            throw new InvalidOperationException($"Total asset size exceeds maximum of {MaxTotalSize:N0} bytes");

        var entry = archive.CreateEntry(pair.Key, CompressionLevel.Fastest);
        using var stream = entry.Open();
        stream.Write(pair.Value, 0, pair.Value.Length);
    }
}
```

#### P2-3: GeoPackage R-Tree Spatial Index
**Location:** `/home/mike/projects/HonuaIO/src/Honua.Server.Core/Export/GeoPackageExporter.cs`
**Severity:** Medium Performance Issue
**Impact:** Slow spatial queries on exported GeoPackages

**Fix:** Add R-Tree creation:
```csharp
private static async Task CreateSpatialIndexAsync(
    SqliteConnection connection,
    string tableName,
    string geometryColumn,
    CancellationToken cancellationToken)
{
    // GeoPackage R-Tree extension (per spec)
    var rtreeTable = $"rtree_{tableName}_{geometryColumn}";

    var createRtreeSql = $@"
        CREATE VIRTUAL TABLE {QuoteIdentifier(rtreeTable)} USING rtree(
            id, minx, maxx, miny, maxy
        )";

    await ExecuteAsync(connection, createRtreeSql, cancellationToken).ConfigureAwait(false);

    // Populate R-Tree from geometry table
    var populateRtreeSql = $@"
        INSERT INTO {QuoteIdentifier(rtreeTable)}
        SELECT fid,
               ST_MinX({QuoteIdentifier(geometryColumn)}),
               ST_MaxX({QuoteIdentifier(geometryColumn)}),
               ST_MinY({QuoteIdentifier(geometryColumn)}),
               ST_MaxY({QuoteIdentifier(geometryColumn)})
        FROM {QuoteIdentifier(tableName)}";

    await ExecuteAsync(connection, populateRtreeSql, cancellationToken).ConfigureAwait(false);

    // Register in gpkg_extensions table
    // ... (per GeoPackage spec section 3.1.1)
}
```

#### P2-4: Shapefile Temp Directory Cleanup Robustness
**Location:** `/home/mike/projects/HonuaIO/src/Honua.Server.Core/Export/ShapefileExporter.cs:596`
**Severity:** Low Reliability Issue
**Impact:** Temp directories may leak on errors

**Fix:**
```csharp
private static void CleanupWorkingDirectory(string path)
{
    if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        return;

    const int MaxRetries = 3;
    for (var i = 0; i < MaxRetries; i++)
    {
        try
        {
            Directory.Delete(path, recursive: true);
            return;
        }
        catch (IOException) when (i < MaxRetries - 1)
        {
            // File may be locked, retry
            Task.Delay(100 * (i + 1)).Wait();
        }
        catch (UnauthorizedAccessException)
        {
            // Log but don't fail - will be cleaned by temp directory cleanup
            break;
        }
        catch
        {
            // Ignore other exceptions
            break;
        }
    }
}
```

#### P2-5: TopoJSON Arc Sharing Implementation
**Location:** `/home/mike/projects/HonuaIO/src/Honua.Server.Core/Serialization/TopoJsonFeatureFormatter.cs:67`
**Severity:** Low Efficiency Issue
**Impact:** TopoJSON files larger than necessary

**Current:** Each feature creates new arcs (no topology optimization)
**Fix:** Implement arc deduplication:
```csharp
private readonly Dictionary<string, int> _arcIndex = new();

private int StoreArc(LineString lineString)
{
    var hash = ComputeArcHash(lineString);
    if (_arcIndex.TryGetValue(hash, out var existingIndex))
        return existingIndex;

    // ... existing arc creation code

    _arcIndex[hash] = _arcs.Count - 1;
    return _arcs.Count - 1;
}

private static string ComputeArcHash(LineString lineString)
{
    using var md5 = System.Security.Cryptography.MD5.Create();
    var sequence = lineString.CoordinateSequence;
    var bytes = new byte[sequence.Count * 16]; // 2 doubles per coord

    for (var i = 0; i < sequence.Count; i++)
    {
        BitConverter.GetBytes(sequence.GetX(i)).CopyTo(bytes, i * 16);
        BitConverter.GetBytes(sequence.GetY(i)).CopyTo(bytes, i * 16 + 8);
    }

    return Convert.ToBase64String(md5.ComputeHash(bytes));
}
```

#### P2-6: Test Coverage Improvements

**Current Coverage:** ~1,291 lines across 5 test files
**Missing Tests:**
- ZIP bomb attacks (Shapefile, KMZ)
- Memory profiling (all buffered exporters)
- Concurrent exports (thread safety)
- Large dataset benchmarks (1M+ features)
- CRS transformation edge cases
- Geometry type mismatches
- Error recovery and partial exports

**Action Items:**
1. Add `ShapefileZipBombTests.cs` with malicious scenarios
2. Add `ExporterBenchmarkTests.cs` with BenchmarkDotNet
3. Add `ExporterConcurrencyTests.cs` with parallel export tests
4. Expand `GeoPackageExporterTests.cs` (currently only 92 lines)

---

## Detailed Issue Catalog

### GeoPackage Exporter Issues

| ID | Severity | Category | Issue | Location | Recommendation |
|----|----------|----------|-------|----------|----------------|
| GPKG-1 | P0 | Security | SQL injection risk via string interpolation | Line 360, 765-773 | Use validated identifiers or parameters |
| GPKG-2 | P1 | Standards | Missing R-Tree spatial index | N/A | Add CREATE VIRTUAL TABLE rtree |
| GPKG-3 | P1 | Standards | No support for Z/M dimensions | Line 401 (z, m = 0, 0) | Add dimension detection and storage |
| GPKG-4 | P1 | Standards | Limited CRS support (only 4326 has full WKT) | Line 290-305 | Extend to top 50 EPSG codes |
| GPKG-5 | P1 | Standards | Missing GeoPackage extensions table | N/A | Add gpkg_extensions for R-Tree |
| GPKG-6 | P2 | Performance | Full envelope calculation requires all geometries | Line 130 | Consider streaming envelope updates |
| GPKG-7 | P2 | Observability | No debug logging for batch commits | Line 143 | Add LogDebug for transaction lifecycle |
| GPKG-8 | P2 | Observability | No metrics/Activity tracking | Throughout | Add telemetry as per P0-2 |
| GPKG-9 | P2 | Standards | No gpkg_metadata population | Table exists but unused | Populate with layer metadata |
| GPKG-10 | P2 | Code Quality | Swallows SqliteException during close | Line 194 | Log exceptions |

### Shapefile Exporter Issues

| ID | Severity | Category | Issue | Location | Recommendation |
|----|----------|----------|-------|----------|----------------|
| SHP-1 | P0 | Security | ZIP bomb vulnerability - no validation | Line 96 | Use ZipArchiveValidator |
| SHP-2 | P1 | Observability | No logging whatsoever | Throughout | Add ILogger |
| SHP-3 | P1 | Observability | No metrics/Activity tracking | Throughout | Add telemetry |
| SHP-4 | P1 | Standards | Limited CRS support | Line 610-617 | Use CrsMetadataProvider |
| SHP-5 | P2 | Performance | Buffers all files before zipping | Line 97-103 | Stream directly to ZIP |
| SHP-6 | P2 | Performance | Skip/Take on lists during ZIP | Line 166-174 | Use array slicing or cursors |
| SHP-7 | P2 | Standards | Column name truncation to 10 chars | Line 552 | Document limitation, add collision detection |
| SHP-8 | P2 | Standards | String truncation to 254 chars | Line 272 | Log truncations |
| SHP-9 | P2 | Code Quality | Complex FeatureStream with pump task | Line 338-538 | Simplify or document pattern |
| SHP-10 | P2 | Code Quality | Temp cleanup swallows exceptions | Line 605 | Improve as per P2-4 |
| SHP-11 | P2 | Test Coverage | No ZIP bomb tests | Tests file | Add malicious ZIP tests |
| SHP-12 | P2 | Performance | Mixed async/sync patterns | Line 88 | Make ShapefileDataWriter async |

### FlatGeobuf Exporter Issues

| ID | Severity | Category | Issue | Location | Recommendation |
|----|----------|----------|-------|----------|----------------|
| FGB-1 | P1 | Performance | Buffers all features in memory | Line 115 | Stream feature buffers to disk |
| FGB-2 | P1 | Performance | Buffers R-Tree in memory | Line 132 | Incremental R-Tree building |
| FGB-3 | P1 | Security | No MaxFeatures limit | N/A | Add as per P1-2 |
| FGB-4 | P1 | Standards | Limited CRS support (code only) | Line 952 | Add full CRS definitions |
| FGB-5 | P2 | Observability | No completion logging | N/A | Add LogInformation on success |
| FGB-6 | P2 | Observability | No metrics/Activity tracking | Throughout | Add telemetry |
| FGB-7 | P2 | Performance | BlockingCollection buffer size hardcoded | Line 665 | Make configurable |
| FGB-8 | P2 | Standards | No M dimension support documented | N/A | Document or implement |

### GeoArrow Exporter Issues

| ID | Severity | Category | Issue | Location | Recommendation |
|----|----------|----------|-------|----------|----------------|
| ARW-1 | P1 | Data Quality | All attributes stored as strings | Line 129 | Implement proper type mapping (P1-4) |
| ARW-2 | P1 | Performance | Loads all features into memory | Line 54-61 | Use multiple RecordBatches |
| ARW-3 | P1 | Performance | Int32.MaxValue feature limit | Line 63-65 | Support multiple batches for > 2B features |
| ARW-4 | P1 | Security | No MaxFeatures enforcement | N/A | Add as per P1-2 |
| ARW-5 | P1 | Standards | Limited CRS metadata | Line 115 | Add PROJJSON to schema metadata |
| ARW-6 | P1 | Observability | No logging | Throughout | Add ILogger |
| ARW-7 | P2 | Observability | No metrics/Activity tracking | Throughout | Add telemetry |
| ARW-8 | P2 | Standards | Not using GeoArrow native encoding | Uses WKB instead | Consider native Point/LineString encoding |
| ARW-9 | P2 | Performance | Single batch poor for readers | N/A | Write multiple batches even if < 2B features |

### PMTiles Exporter Issues

| ID | Severity | Category | Issue | Location | Recommendation |
|----|----------|----------|-------|----------|----------------|
| PMT-1 | P1 | Performance | Buffers all tiles in memory | Line 151 | Stream for multi-tile archives |
| PMT-2 | P1 | Observability | No logging | Throughout | Add ILogger |
| PMT-3 | P2 | Observability | No metrics/Activity tracking | Throughout | Add telemetry |
| PMT-4 | P2 | Security | No compressed size limits | Line 301-336 | Add max compressed size check |
| PMT-5 | P2 | Error Handling | No try-catch around compression | Line 301-336 | Add error handling |

### GeoParquet Exporter Issues

| ID | Severity | Category | Issue | Location | Recommendation |
|----|----------|----------|-------|----------|----------------|
| GPQT-1 | P1 | Data Quality | All attributes stored as strings | Line 252 | Implement proper Parquet type mapping (P1-4) |
| GPQT-2 | P1 | Performance | Loads all features into memory | Line 64-96 | Stream row groups (P1-1) |
| GPQT-3 | P1 | Security | No MaxFeatures enforcement | N/A | Add as per P1-2 |
| GPQT-4 | P2 | Standards | Limited PROJJSON (only 4326, 3857) | Line 286-290 | Extend to top 50 EPSG codes (P1-3) |
| GPQT-5 | P2 | Observability | No info-level completion logging | Line 188 (only Debug) | Add LogInformation |
| GPQT-6 | P2 | Observability | No metrics/Activity tracking | Throughout | Add telemetry |
| GPQT-7 | P2 | Performance | Row group writes from in-memory slices | Line 166-174 | Stream directly during accumulation |

### CSV Exporter Issues

| ID | Severity | Category | Issue | Location | Recommendation |
|----|----------|----------|-------|----------|----------------|
| CSV-1 | P2 | Observability | No debug logging of options | N/A | Log delimiter, geometry format on export start |
| CSV-2 | P2 | Observability | No metrics/Activity tracking | Throughout | Add telemetry |
| CSV-3 | P2 | Standards | No CRS metadata | N/A | Add CRS comment in header |
| CSV-4 | P2 | Feature | Hardcoded batch size | Line 95 | Make configurable via CsvExportOptions |

### KML/KMZ Formatter Issues

| ID | Severity | Category | Issue | Location | Recommendation |
|----|----------|----------|-------|----------|----------------|
| KML-1 | P1 | Observability | No logging | Throughout | Add ILogger |
| KML-2 | P2 | Security | No ZIP bomb protection in KMZ | KmzArchiveBuilder.cs | Implement as per P2-2 |
| KML-3 | P2 | Observability | No metrics/Activity tracking | Throughout | Add telemetry |
| KML-4 | P2 | Performance | Non-streaming (full doc in memory) | Line 82 | Consider streaming for large collections |
| KML-5 | P2 | Standards | Ring orientation edge cases | Line 358-366 | Add comprehensive ring tests |
| KML-6 | P2 | Error Handling | Generic exceptions | Line 260 | Add specific error context |

### TopoJSON Formatter Issues

| ID | Severity | Category | Issue | Location | Recommendation |
|----|----------|----------|-------|----------|----------------|
| TOPO-1 | P1 | Observability | No logging | Throughout | Add ILogger |
| TOPO-2 | P2 | Observability | No metrics/Activity tracking | Throughout | Add telemetry |
| TOPO-3 | P2 | Performance | All arcs/geometries in memory | Line 66-67 | Implement streaming |
| TOPO-4 | P2 | Performance | No arc sharing (no topology optimization) | Line 354 | Implement as per P2-5 |
| TOPO-5 | P2 | Security | No limits on arc/geometry count | N/A | Add MaxFeatures-like limit |
| TOPO-6 | P2 | Error Handling | Generic exceptions | Line 176, 198, 359 | Add specific error messages |

### JSON-LD Formatter Issues

| ID | Severity | Category | Issue | Location | Recommendation |
|----|----------|----------|-------|----------|----------------|
| JSONLD-1 | P1 | Observability | No logging | Throughout | Add ILogger |
| JSONLD-2 | P2 | Observability | No metrics/Activity tracking | Throughout | Add telemetry |
| JSONLD-3 | P2 | Feature | Hardcoded baseUri parameter | Line 121 | Derive from request context |
| JSONLD-4 | P2 | Performance | Deep cloning overhead | Lines 127, 136, 144 | Consider shallow copy for immutable nodes |
| JSONLD-5 | P2 | Documentation | Context removal not documented | Line 181 | Add comment explaining why |

### GeoJSON-T Formatter Issues

| ID | Severity | Category | Issue | Location | Recommendation |
|----|----------|----------|-------|----------|----------------|
| GEOJSONT-1 | P1 | Observability | No logging | Throughout | Add ILogger |
| GEOJSONT-2 | P2 | Observability | No metrics/Activity tracking | Throughout | Add telemetry |
| GEOJSONT-3 | P2 | Standards | No 4th temporal coordinate | Line 139 | Implement or document limitation |
| GEOJSONT-4 | P2 | Performance | Deep cloning overhead | Lines 37, 48, 58 | Optimize for immutable nodes |

---

## Summary Statistics

### Issues by Severity

| Severity | Count | Percentage |
|----------|-------|------------|
| **P0 (Critical)** | 3 | 3.4% |
| **P1 (High)** | 36 | 41.4% |
| **P2 (Medium)** | 48 | 55.2% |
| **Total** | **87** | **100%** |

### Issues by Category

| Category | Count | Percentage |
|----------|-------|------------|
| Observability (Logging/Metrics) | 28 | 32.2% |
| Performance | 21 | 24.1% |
| Security | 11 | 12.6% |
| Standards Compliance | 14 | 16.1% |
| Data Quality | 4 | 4.6% |
| Code Quality | 6 | 6.9% |
| Error Handling | 3 | 3.4% |

### Issues by Exporter

| Exporter | P0 | P1 | P2 | Total |
|----------|----|----|----|----|
| Shapefile | 1 | 4 | 7 | 12 |
| GeoPackage | 1 | 5 | 4 | 10 |
| FlatGeobuf | 0 | 4 | 4 | 8 |
| GeoArrow | 0 | 6 | 3 | 9 |
| GeoParquet | 0 | 3 | 4 | 7 |
| PMTiles | 0 | 2 | 3 | 5 |
| CSV | 0 | 0 | 4 | 4 |
| KML/KMZ | 0 | 1 | 5 | 6 |
| TopoJSON | 0 | 1 | 5 | 6 |
| JSON-LD | 0 | 1 | 4 | 5 |
| GeoJSON-T | 0 | 1 | 3 | 4 |
| **Cross-Cutting** | 1 | 8 | 2 | 11 |
| **Total** | **3** | **36** | **48** | **87** |

---

## Conclusion

The Honua.Server export format implementations demonstrate **solid engineering fundamentals** with proper async patterns, resource cleanup, and security awareness. The recent addition of GeoParquet with full PROJJSON support shows commitment to standards compliance.

However, **critical gaps remain** in observability (telemetry), memory management (buffering), and security (ZIP bombs, resource limits). Addressing the P0 and P1 recommendations will significantly improve production readiness, reliability, and maintainability.

**Top 3 Priorities:**
1. **Add comprehensive telemetry** to all exporters (Activity + Metrics)
2. **Fix memory issues** in buffered exporters (FlatGeobuf, GeoArrow, GeoParquet)
3. **Close security gaps** (Shapefile ZIP bombs, MaxFeatures limits)

With these improvements, the export infrastructure will be production-grade and capable of handling enterprise workloads safely and efficiently.

---

**Review Completed:** 2025-10-22
**Total Analysis Time:** Comprehensive review of 12 formats across 4,500+ lines of code
**Issues Identified:** 87 (3 P0, 36 P1, 48 P2)
**Test Coverage:** 1,291 lines across 5 test files (needs expansion)
