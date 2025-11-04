# STAC API Implementation - Comprehensive Code Review

**Project:** HonuaIO  
**Review Date:** October 2025  
**Scope:** STAC API implementation across `/src/Honua.Server.Core/Stac/` and `/src/Honua.Server.Host/Stac/`

---

## Executive Summary

The STAC API implementation demonstrates **strong overall architecture** with comprehensive feature coverage, robust validation, and excellent error handling. However, there are **critical issues** affecting conformance, performance optimization gaps, and several edge cases requiring attention.

**Key Findings:**
- STAC 1.0.0 compliant with proper conformance declarations
- Missing OGC API - Features implementation (correctly excluded)
- Strong pagination and field filtering implementation
- Good test coverage but gaps remain
- Security hardening in place (URI validation, SQL injection prevention)
- Performance optimizations for large result sets
- Several code duplication and missing implementation gaps

---

## 1. COMPLETENESS ANALYSIS

### 1.1 STAC API Conformance Classes

**File:** `/src/Honua.Server.Host/Stac/StacApiModels.cs` (lines 195-205)

**Status:** ✅ **PARTIALLY COMPLETE**

```csharp
public static readonly IReadOnlyList<string> DefaultConformance = new[]
{
    "https://api.stacspec.org/v1.0.0/core",
    "https://api.stacspec.org/v1.0.0/collections",
    "https://api.stacspec.org/v1.0.0/item-search",
    "https://api.stacspec.org/v1.0.0/item-search#fields",
    "https://api.stacspec.org/v1.0.0/item-search#sort",
    "https://api.stacspec.org/v1.0.0/item-search#filter",
    "http://www.opengis.net/spec/cql2/1.0/conf/cql2-json",
    "http://www.opengis.net/spec/cql2/1.0/conf/basic-cql2"
};
```

**Findings:**
- ✅ Core, Collections, Item-Search conformance correctly declared
- ✅ Fields, Sort, Filter extensions properly advertised
- ✅ CQL2-JSON filter support declared
- ⚠ **Missing:** "https://api.stacspec.org/v1.0.0/item-search#context" - context extension not explicitly declared but implemented
- ⚠ **Note:** OGC API - Features correctly excluded (not implemented)

### 1.2 Collection Management (CRUD)

**File:** `/src/Honua.Server.Host/Stac/StacCollectionsController.cs`

**Status:** ✅ **COMPLETE**

Implemented operations:
- ✅ `GetCollections()` (lines 69-116) - List with pagination
- ✅ `GetCollection()` (lines 133-165) - Get by ID with ETag caching
- ✅ `PostCollection()` (lines 232-262) - Create with validation
- ✅ `PutCollection()` (lines 286-314) - Update with optimistic concurrency
- ✅ `PatchCollection()` (lines 328-347) - Partial update
- ✅ `DeleteCollection()` (lines 354-380) - Delete with audit logging

**Features:**
- ✅ Request size limits (10 MB) to prevent abuse
- ✅ Authorization checks (`RequireDataPublisher` policy)
- ✅ ETag-based concurrency control with 412 responses
- ✅ Comprehensive error handling

### 1.3 Item Management (CRUD)

**File:** `/src/Honua.Server.Host/Stac/StacCollectionsController.cs`

**Status:** ✅ **COMPLETE**

Implemented operations:
- ✅ `GetCollectionItems()` (lines 174-194) - List items in collection with pagination
- ✅ `GetCollectionItem()` (lines 202-217) - Get item by ID
- ✅ `PostCollectionItem()` (lines 395-414) - Create item
- ✅ `PutCollectionItem()` (lines 439-471) - Update item with concurrency control
- ✅ `PatchCollectionItem()` (lines 485-504) - Partial update
- ✅ `DeleteCollectionItem()` (lines 511-527) - Delete item

### 1.4 Search Endpoint Parameters

**File:** `/src/Honua.Server.Host/Stac/StacSearchController.cs`

**Status:** ✅ **COMPLETE**

Implemented search parameters:

| Parameter | GET | POST | Status | Notes |
|-----------|-----|------|--------|-------|
| `collections` | ✅ | ✅ | Complete | Max 100 items (line 213) |
| `ids` | ✅ | ✅ | Complete | Max 100 items (line 219) |
| `bbox` | ✅ | ✅ | Complete | 4-6 element array support |
| `datetime` | ✅ | ✅ | Complete | ISO 8601 interval parsing |
| `intersects` | ✅ | ✅ | Complete | GeoJSON geometry validation |
| `limit` | ✅ | ✅ | Complete | Default 10, max 1000 |
| `token` | ✅ | ✅ | Complete | Continuation token for pagination |
| `sortby` | ✅ | ✅ | Complete | Field-based sorting (max 10 fields) |
| `fields` | ✅ | ✅ | Complete | Include/exclude field filtering |
| `filter` | ✅ | ✅ | Complete | CQL2-JSON filter support |
| `filter-lang` | ✅ | ✅ | Complete | Filter language declaration |

**Key implementations:**
- **GET search** (lines 78-201): Query string parameters with individual parsing
- **POST search** (lines 224-290): JSON body with model binding and validation
- **Mutual exclusivity validation** (lines 258-262): Enforces bbox XOR intersects

### 1.5 Links and Relations

**File:** `/src/Honua.Server.Host/Stac/StacApiMapper.cs`

**Status:** ✅ **COMPLETE**

Implemented link relationships:
- ✅ `self` - Current resource
- ✅ `root` - STAC root catalog
- ✅ `data` - Collections endpoint
- ✅ `conformance` - Conformance declaration
- ✅ `child` - Catalog hierarchy (through record.Links)
- ✅ `parent` - Parent catalog
- ✅ `collection` - Parent collection for items
- ✅ `items` - Items in collection
- ✅ `next` - Pagination link with token

**Issue Found:**
- ⚠ **Line 20:** BuildLink uses hardcoded media types - should validate against content negotiation

### 1.6 Pagination (limit, token)

**File:** `/src/Honua.Server.Core/Stac/Storage/RelationalStacCatalogStore.cs`

**Status:** ✅ **COMPLETE**

Pagination mechanisms:
- ✅ Keyset-based pagination using collection ID and item ID
- ✅ Token generation and validation
- ✅ Limit normalization (default 10, max 1000)
- ✅ "Has more" detection for next link generation

**Implementation details:**
- Uses ID-based cursors for stateless pagination
- Supports multiple collections with stable ordering
- Handles overflow detection (limit + 1 fetch pattern)

### 1.7 Extension Support

**Status:** ✅ **COMPLETE**

| Extension | Implementation | Status |
|-----------|----------------|--------|
| Fields | `/src/Honua.Server.Core/Stac/FieldsParser.cs` | ✅ Complete |
| Sort | `/src/Honua.Server.Core/Stac/StacSortParser.cs` | ✅ Complete |
| Query/Filter | `/src/Honua.Server.Core/Stac/Cql2/` | ✅ Complete |
| Context | Embedded in search responses | ✅ Complete |

**Fields Extension:**
- ✅ GET syntax: `?fields=id,properties.datetime,-geometry`
- ✅ POST syntax: `{"include": [...], "exclude": [...]}`
- ✅ Nested field support
- ✅ Mixed include/exclude with precedence rules
- ✅ Complex filtering via PathTreeNode algorithm

**Sort Extension:**
- ✅ GET syntax: `?sortby=-datetime,+id`
- ✅ POST syntax: `[{"field": "datetime", "direction": "desc"}]`
- ✅ Validation against allowed fields
- ✅ SQL injection prevention via whitelist
- ✅ Max 10 fields limit

**Filter/CQL2 Support:**
- ✅ CQL2-JSON parsing
- ✅ Expression tree building
- ✅ Multiple database provider support
- ✅ Parameterized SQL generation

---

## 2. CORRECTNESS ANALYSIS

### 2.1 STAC Schema Compliance

**File:** `/src/Honua.Server.Host/Stac/StacApiModels.cs`

**Status:** ⚠ **MOSTLY CORRECT WITH ISSUES**

**Response Models:**
- ✅ StacRootResponse: Correct structure with stac_version 1.0.0
- ✅ StacCollectionResponse: All required fields present
- ✅ StacItemResponse: Proper Feature structure
- ✅ StacItemCollectionResponse: Proper FeatureCollection
- ✅ StacConformanceResponse: Correct conformsTo array

**Issues Found:**

1. **Missing "assets" field in collection responses** (Line 75-77 shows it's optional but should be included if present)
   - Current: Only includes assets if present in properties
   - Should: Always include assets field in collection responses
   - **Severity:** Medium - STAC spec requires assets field

2. **DateTime field handling** (`/src/Honua.Server.Core/Stac/StacTypes.cs`, lines 46-93)
   - ✅ ISO 8601 parsing correct
   - ✅ Interval handling (start/end ranges)
   - ⚠ Single instant duplicates to both start and end (lines 77-79) - correct per spec

### 2.2 GeoJSON Formatting

**File:** `/src/Honua.Server.Core/Stac/GeometryParser.cs`

**Status:** ✅ **CORRECT**

**Geometry Validation:**
- ✅ Type validation (Point, LineString, Polygon, etc.)
- ✅ Coordinate validation (lat -90..90, lon -180..180)
- ✅ NaN and infinity checks (lines 390-398)
- ✅ Polygon ring closure validation (lines 273-286)
- ✅ GeometryCollection support

**Coordinate System:**
- ✅ WGS84 (EPSG:4326) coordinates enforced
- ✅ [lon, lat, alt] order enforced
- ✅ Proper conversion to WKT for database queries

**Issues Found:**

1. **Vertex count limit not configurable** (Line 41)
   ```csharp
   private const int MaxVertices = 10000;
   ```
   - Should be configurable for different deployment scenarios
   - **Severity:** Low

2. **Missing altitude validation in bounding box** (Line 91)
   - Calculates bbox from [lon, lat, alt] coordinates
   - But bbox is always [minX, minY, maxX, maxY] (2D)
   - 3D bbox [minX, minY, minZ, maxX, maxY, maxZ] not supported
   - **Severity:** Medium - Limits 3D spatial queries

### 2.3 DateTime Parsing and Filtering

**File:** `/src/Honua.Server.Host/Stac/StacSearchController.cs` (lines 432-446)

**Status:** ✅ **CORRECT**

```csharp
private ((DateTimeOffset? Start, DateTimeOffset? End) Range, ActionResult? Error) 
    ParseDatetimeRange(string? value)
{
    var (temporalRange, error) = QueryParsingHelpers.ParseTemporalRange(value, "datetime");
    // ...
}
```

**Supported formats:**
- ✅ Instant: `2020-12-31T23:59:59Z`
- ✅ Open-ended ranges: `2020-01-01T00:00:00Z/..`
- ✅ Closed ranges: `2020-01-01T00:00:00Z/2020-12-31T23:59:59Z`
- ✅ Negative infinity: `../2020-12-31T23:59:59Z`

**Issues Found:**
- ✅ Uses existing QueryParsingHelpers - good code reuse
- ✅ Error handling correct

### 2.4 CRS Handling

**Status:** ⚠ **INCOMPLETE**

**Current Implementation:**
- ✅ Spatial queries use WGS84 (EPSG:4326)
- ⚠ No support for `filter-crs` parameter (spec allows it)
- ⚠ No way to query in alternative CRS projections
- **Severity:** Medium - Limits interoperability

**Finding:** The STAC API spec allows `filter-crs` parameter to specify CRS for filter operations. This is not implemented.

**File references:**
- No CRS parameter parsing in search controllers
- No CRS transformation layer

### 2.5 Asset URLs and Metadata

**File:** `/src/Honua.Server.Host/Stac/StacApiMapper.cs` (lines 197+)

**Status:** ✅ **CORRECT**

Asset handling:
- ✅ Preserves href from item records
- ✅ Maintains type, roles, title fields
- ✅ Relative URL support via record data

**Issue Found:**
- ⚠ No validation that asset hrefs are valid URLs
- ⚠ No check for dangling references
- **Severity:** Low - Assumes data quality

### 2.6 Content Negotiation

**File:** `/src/Honua.Server.Host/Stac/`

**Status:** ⚠ **PARTIALLY IMPLEMENTED**

**Produces declarations:**
- ✅ StacSearchController: `application/geo+json` (line 73)
- ✅ StacCollectionsController: `application/json` and `application/geo+json`
- ✅ StacCatalogController: `application/json`

**Issues Found:**

1. **Missing `application/json` alternative for search** (Line 73)
   - Should also support `application/json` for search responses
   - **Severity:** Medium

2. **No Accept header negotiation**
   - Controllers don't validate Accept headers
   - Should return 406 Not Acceptable for unsupported types
   - **Severity:** Low

---

## 3. PERFORMANCE ANALYSIS

### 3.1 Query Optimization

**File:** `/src/Honua.Server.Core/Stac/Storage/RelationalStacCatalogStore.cs`

**Status:** ✅ **GOOD**

**Optimization strategies:**

1. **Count Query Optimization** (Lines 25-41 SearchOptions)
   ```csharp
   public int CountTimeoutSeconds { get; init; } = 5;
   public bool SkipCountForLargeResultSets { get; init; } = true;
   public int SkipCountLimitThreshold { get; init; } = 1000;
   ```
   - ✅ Timeout prevents hanging on large tables
   - ✅ Estimation fallback for large result sets
   - ✅ Configurable thresholds

2. **Provider-specific optimizations**
   ```
   EstimateCountPostgresAsync() - Uses pg_stat_user_tables
   EstimateCountMySqlAsync() - Uses information_schema
   EstimateCountSqlServerAsync() - Uses sys.dm_db_partition_stats
   ```
   - ✅ Database-aware estimation strategies

3. **Collection pre-filtering** (StacSearchController lines 331-372)
   ```csharp
   if (request.Collections is not null && request.Collections.Count > 0)
   {
       // Fetch only requested collections instead of all
       var collectionTasks = request.Collections
           .Select(id => _store.GetCollectionAsync(id, cancellationToken))
           .ToList();
   }
   ```
   - ✅ Reduces scope for search operations

**Issues Found:**

1. **N+1 queries for collection fetching** (StacSearchController line 335-338)
   - Each collection fetched individually in parallel
   - Should batch load collections by ID
   - **Severity:** Medium - Performance degrades with many requested collections
   - **Code:**
     ```csharp
     var collectionTasks = request.Collections
         .Select(id => _store.GetCollectionAsync(id, cancellationToken))
         .ToList();
     var fetchedCollections = await Task.WhenAll(collectionTasks)
     ```

2. **Missing indexes on search paths**
   - No documentation on required database indexes
   - Search can be slow without indexes on datetime, bbox, collection_id
   - **Severity:** Medium

3. **Geometry intersection not optimized**
   - Converts geometries to WKT but no spatial index usage documented
   - Should verify database has spatial indexes
   - **Severity:** Medium

### 3.2 Indexing Strategy

**Status:** ⚠ **NO DOCUMENTED STRATEGY**

**Finding:** No indexing strategy documented in comments or configuration

**Recommended indexes (missing documentation):**
- `stac_items(collection_id, datetime)` for temporal queries
- `stac_items(collection_id, id)` for pagination cursors
- Spatial index on geometry column for intersect queries
- `stac_collections(id)` primary key

### 3.3 Streaming Responses

**File:** `/src/Honua.Server.Host/Stac/StacSearchController.cs`

**Status:** ⚠ **NOT IMPLEMENTED**

**Finding:** All results loaded into memory before returning

**Code (Line 405):**
```csharp
var result = await _store.SearchAsync(parameters, cancellationToken)
    .ConfigureAwait(false);
```

Returns `StacSearchResult` with all items in memory. For large result sets:
- ⚠ Memory usage unbounded
- ⚠ No chunked streaming
- ⚠ Violates REST streaming principles

**Recommendation:** Implement `IAsyncEnumerable<StacItemRecord>` for streaming

### 3.4 Caching Headers

**File:** `/src/Honua.Server.Host/Stac/StacSearchController.cs`

**Status:** ✅ **GOOD**

**Caching policies:**

| Endpoint | Policy | Duration | Notes |
|----------|--------|----------|-------|
| GET /stac/search | OutputCache | Configured | Line 77 |
| POST /stac/search | NoCache | N/A | Line 223 - Correct, body not cacheable |
| GET /stac/collections | StacCollections | Configured | Controllers line 67 |
| GET /stac/collections/{id} | StacCollectionMetadata | Configured | Controllers line 131 |

**ETag Support:**
- ✅ Collections have ETags for efficient validation
- ✅ Items have ETags for concurrency control
- ✅ If-Match headers checked for PUT/PATCH

### 3.5 Large Result Set Handling

**Status:** ⚠ **PARTIALLY HANDLED**

**Current handling:**
- ✅ Limit capped at 1000 items
- ✅ Continuation tokens for pagination
- ⚠ All items loaded in memory
- ⚠ Count query can timeout on massive datasets

**Issue:** No streaming or server-side cursor support

---

## 4. RELIABILITY ANALYSIS

### 4.1 Error Handling

**File:** `/src/Honua.Server.Host/Stac/StacSearchController.cs`

**Status:** ✅ **COMPREHENSIVE**

**Error cases handled:**

1. **Validation errors** (Lines 93-151)
   - ✅ Collections/IDs count validation
   - ✅ Bbox validation with error response
   - ✅ Datetime parsing error handling
   - ✅ Sortby validation
   - ✅ Filter JSON parsing
   - ✅ Intersects geometry validation

2. **Feature availability** (Lines 300-305)
   - ✅ STAC disabled check
   - ✅ Returns 404 if not enabled

3. **Resource not found** (Lines 346-351)
   - ✅ No matching collections returns 404

4. **Concurrency issues** (StacCollectionsController lines 309-313)
   ```csharp
   catch (System.Data.DBConcurrencyException ex)
   {
       return _helper.HandleDBConcurrencyException(ex, "collection", Request.Path);
   }
   ```
   - ✅ 412 Precondition Failed for ETag mismatch
   - ✅ User-friendly error message

**Response format:**
- ✅ Problem Details (RFC 7807)
- ✅ Structured error messages
- ✅ HTTP status codes correct

### 4.2 Input Validation

**File:** `/src/Honua.Server.Host/Stac/StacValidationService.cs`

**Status:** ✅ **THOROUGH**

**Validation layers:**

1. **Collection validation** (Lines 126-150+)
   - ✅ Required ID field check
   - ✅ Extent structure validation
   - ✅ Geometry type validation
   - ✅ DateTime format validation
   - ✅ License field validation

2. **Item validation** (Similar pattern for items)
   - ✅ Required fields (id, type, geometry, properties, assets)
   - ✅ Datetime property requirement
   - ✅ Geometry structure validation

3. **Request parameter validation** (StacSearchController)
   - ✅ Collections count limit (line 96)
   - ✅ IDs count limit (line 96)
   - ✅ Bbox array length (4-6 elements)
   - ✅ Limit range (1-10000 in validation, normalized to 1000)
   - ✅ Sort field whitelist (StacSortParser.AllowedItemFields)

**Issues Found:**

1. **Insufficient error detail** (StacValidationService line 137)
   ```csharp
   _logger.LogDebug("Validating STAC collection: {CollectionId}", collectionId ?? "unknown");
   ```
   - Logs at DEBUG level, should be INFO for audit trail
   - **Severity:** Low

2. **Missing collection extent validation**
   - Doesn't validate extent bounds are valid
   - Should check minX < maxX, minY < maxY
   - **Severity:** Medium

### 4.3 Edge Cases

**Status:** ⚠ **PARTIALLY HANDLED**

**Handled edge cases:**
- ✅ Empty collections (returns empty FeatureCollection)
- ✅ Null geometries handled gracefully
- ✅ Special characters in IDs (URI escaped)
- ✅ Missing optional fields (properly omitted from JSON)

**Missing edge case handling:**

1. **Duplicate collection IDs in search** (StacSearchController line 93)
   - `collections=col1,col1,col2` not deduplicated
   - May cause duplicate results
   - **Severity:** Low
   - **Fix needed:** Lines 93-94 should deduplicate

2. **Empty search parameters** (StacSearchController line 186)
   - Allows `collections=[]` or `ids=[]`
   - May return unintended results
   - **Severity:** Low

3. **Very large geometries** 
   - Max vertices = 10000 (GeometryParser line 41)
   - No upper limit on WKT string length
   - Could cause SQL query generation failure
   - **Severity:** Medium

4. **Null datetime ranges** (StacTypes.cs lines 46-94)
   - Supports open-ended ranges (`.. /end`)
   - But search might not handle null Start/End correctly
   - **Severity:** Medium - Verify storage layer

### 4.4 Transaction Safety

**File:** `/src/Honua.Server.Core/Stac/Storage/RelationalStacCatalogStore.cs`

**Status:** ✅ **GOOD**

**Transaction handling:**

1. **Collection upsert** (Lines 76-131)
   ```csharp
   await using var transaction = await connection.BeginTransactionAsync()
   ```
   - ✅ Uses transactions for atomicity
   - ✅ Commits only on success
   - ✅ Auto-rollback on exception

2. **ETag-based optimistic locking** (Lines 89-93)
   ```csharp
   and (@expectedETag is null or etag = @expectedETag)
   ```
   - ✅ Prevents lost updates
   - ✅ Returns 412 on conflict

3. **Bulk operations** (BulkUpsertItemsAsync)
   - ✅ Transaction-wrapped
   - ✅ Atomic all-or-nothing semantics

**Issue Found:**
- ⚠ No explicit isolation level specification
- Should use `REPEATABLE READ` or `SERIALIZABLE` for consistency
- **Severity:** Low

---

## 5. CODE QUALITY ANALYSIS

### 5.1 Code Organization

**Structure:** ✅ **WELL-ORGANIZED**

```
src/Honua.Server.Core/Stac/
├── IStacCatalogStore.cs                    # Interface definition
├── Storage/
│   ├── RelationalStacCatalogStore.cs       # Base implementation
│   ├── PostgresStacCatalogStore.cs         # PostgreSQL-specific
│   ├── MySqlStacCatalogStore.cs            # MySQL-specific
│   ├── SqliteStacCatalogStore.cs           # SQLite-specific
│   ├── SqlServerStacCatalogStore.cs        # SQL Server-specific
│   └── InMemoryStacCatalogStore.cs         # Testing
├── Cql2/
│   ├── Cql2Parser.cs                       # CQL2 parsing
│   ├── Cql2SqlQueryBuilder.cs              # SQL generation
│   └── Cql2Expression.cs                   # Expression tree
├── FieldsParser.cs                         # Fields extension
├── StacSortParser.cs                       # Sort extension
├── GeometryParser.cs                       # Geometry handling
└── StacTypes.cs                            # Domain models

src/Honua.Server.Host/Stac/
├── Controllers/
│   ├── StacCatalogController.cs            # Root/conformance
│   ├── StacCollectionsController.cs        # Collection CRUD
│   └── StacSearchController.cs             # Search operations
├── Services/
│   ├── StacReadService.cs                  # Read operations
│   ├── StacCollectionService.cs            # Collection writes
│   ├── StacItemService.cs                  # Item writes
│   ├── StacValidationService.cs            # Validation
│   └── StacParsingService.cs               # JSON parsing
├── StacApiMapper.cs                        # DTO mapping
├── StacApiModels.cs                        # Response models
└── StacRequestHelpers.cs                   # Utilities
```

**Strengths:**
- ✅ Clear separation of concerns
- ✅ Factory pattern for database selection
- ✅ Service layer isolation
- ✅ Database providers abstracted

**Issues:**
- ⚠ StacValidationService mixed concerns (validation + error formatting)
- ⚠ StacApiMapper is large (200+ lines) - should split by entity

### 5.2 Testability

**Test files found:**

| Test File | Coverage | Status |
|-----------|----------|--------|
| StacSearchControllerTests.cs | Search logic | ✅ Good |
| StacCollectionsControllerTests.cs | Collections CRUD | ✅ Good |
| StacValidationServiceTests.cs | Validation | ✅ Comprehensive |
| StacComplianceTests.cs | STAC spec | ✅ Present |
| StacCatalogStoreTestsBase.cs | Database layer | ✅ Parameterized |

**Test coverage estimate:** 70-80% (good for core logic, lower for mappers)

**Gaps:**
- ⚠ Limited error scenario testing
- ⚠ Integration tests with real databases minimal
- ⚠ Performance/load testing not evident
- ⚠ Concurrency edge cases not fully tested

### 5.3 Duplication

**Code duplication identified:**

1. **Limit normalization** (Multiple locations)
   - StacSearchController (line 466-476)
   - StacReadService (line 188-198)
   - StacRequestHelpers (line 108-119)
   
   **Fix:** Create single `LimitHelper.Normalize()` method

2. **Error handling patterns**
   - StacCollectionService and StacItemService have identical error handling
   - Should extract to base class or helper
   - **Severity:** Low

3. **URI escaping**
   - `Uri.EscapeDataString()` used throughout mappers
   - No helper method for path escaping
   - **Severity:** Low

4. **Field validation in multiple places**
   - StacSortParser has AllowedItemFields (line 16)
   - StacParsingService may duplicate
   - **Severity:** Low

### 5.4 Complexity

**Cyclomatic complexity assessment:**

| File | Complexity | Assessment |
|------|-----------|------------|
| StacSearchController.cs | Medium-High | SearchInternalAsync() is complex (25+ lines) |
| FieldsFilter.cs | High | Recursive path tree logic (moderate complexity) |
| RelationalStacCatalogStore.cs | High | Multiple database providers, 500+ lines |
| GeometryParser.cs | High | Recursive geometry validation |
| Cql2SqlQueryBuilder.cs | High | Expression tree traversal |

**High complexity areas requiring testing:**
- ✅ Geometry validation (well-tested)
- ⚠ CQL2 expression building (minimal testing evident)
- ⚠ Fields filter logic (specific tests needed)
- ⚠ SearchInternalAsync() (needs refactoring)

---

## 6. CRITICAL ISSUES AND RECOMMENDATIONS

### Issue #1: N+1 Query Problem in Collection Fetching

**Location:** `/src/Honua.Server.Host/Stac/StacSearchController.cs`, lines 335-344

**Severity:** MEDIUM

**Problem:**
```csharp
var collectionTasks = request.Collections
    .Select(id => _store.GetCollectionAsync(id, cancellationToken))
    .ToList();
var fetchedCollections = await Task.WhenAll(collectionTasks)
```

Each collection is fetched individually, even when requested in bulk.

**Impact:** Performance degradation when searching multiple collections

**Recommendation:**
Add batch load method to `IStacCatalogStore`:
```csharp
Task<IReadOnlyList<StacCollectionRecord>> GetCollectionsAsync(
    IReadOnlyList<string> collectionIds, 
    CancellationToken cancellationToken);
```

---

### Issue #2: Missing CRS Filter Support

**Location:** STAC API specification compliance

**Severity:** MEDIUM

**Problem:**
The STAC API spec allows `filter-crs` parameter to specify CRS for filter operations. This is not implemented.

**Impact:** 
- Limited CRS flexibility
- May not be compatible with clients expecting CRS parameter
- Limits queries in non-WGS84 coordinate systems

**Recommendation:**
Implement CRS parameter parsing and coordinate transformation layer

---

### Issue #3: In-Memory Result Loading

**Location:** `/src/Honua.Server.Host/Stac/` controllers

**Severity:** MEDIUM

**Problem:**
All search results are loaded into memory before returning. No streaming support.

**Impact:**
- Memory usage scales with result set size
- Large queries risk OutOfMemoryException
- Violates streaming REST principles

**Recommendation:**
Implement IAsyncEnumerable streaming for large result sets

---

### Issue #4: Duplicate Search Parameters Not Deduplicated

**Location:** `/src/Honua.Server.Host/Stac/StacSearchController.cs`, lines 93-94

**Severity:** LOW

**Problem:**
`collections=col1,col1,col2` doesn't deduplicate, may cause duplicate results

**Recommendation:**
```csharp
var collectionsList = Split(collections)?
    .Distinct(StringComparer.Ordinal)
    .ToList();
```

---

### Issue #5: 3D Bounding Box Not Supported

**Location:** `/src/Honua.Server.Core/Stac/GeometryParser.cs`, line 91

**Severity:** MEDIUM

**Problem:**
Bounding boxes only support 2D [minX, minY, maxX, maxY]. 3D [minX, minY, minZ, maxX, maxY, maxZ] not handled.

**Impact:**
- 3D spatial queries not fully supported
- Altitude dimensions lost in bbox calculations

**Recommendation:**
Extend bounding box calculation to handle 3D coordinates

---

### Issue #6: Missing Content Negotiation for Search

**Location:** `/src/Honua.Server.Host/Stac/StacSearchController.cs`, line 73

**Severity:** MEDIUM

**Problem:**
Search endpoint only produces `application/geo+json`. Should also support `application/json`.

**Impact:**
- Client forcing JSON content type may fail
- STAC spec allows both formats

**Recommendation:**
```csharp
[Produces("application/geo+json", "application/json")]
```

---

### Issue #7: Sort Field Whitelist Too Restrictive

**Location:** `/src/Honua.Server.Core/Stac/StacSortParser.cs`, lines 16-43

**Severity:** LOW-MEDIUM

**Problem:**
Only allows hardcoded sortable fields. Users cannot sort by custom properties or extension fields.

**AllowedPropertyFields:**
```
cloud_cover, eo:cloud_cover, gsd, platform, instruments, 
constellation, mission, providers, license, created, updated, 
start_datetime, end_datetime
```

**Impact:**
- Limited sorting flexibility
- Extension fields not sortable
- Requires code change to add new sortable fields

**Recommendation:**
Make sortable fields configurable, or implement a more flexible validation strategy

---

## 7. RECOMMENDATIONS

### High Priority
1. ✅ Implement batch collection loading to fix N+1 queries
2. ✅ Add CRS filter parameter support
3. ✅ Implement streaming for large result sets

### Medium Priority
4. Extend 3D bounding box support
5. Add content negotiation for search responses
6. Refactor SearchInternalAsync() for better readability
7. Deduplicate limit normalization code

### Low Priority
8. Make sort fields whitelist configurable
9. Add missing context conformance class declaration
10. Document required database indexes
11. Add Accept header negotiation validation
12. Improve validation logging level from DEBUG to INFO

---

## 8. TEST COVERAGE RECOMMENDATIONS

**Priority test additions:**

1. **Error scenarios:**
   - Invalid geometries (missing coordinates, invalid types)
   - Concurrent updates (optimistic locking conflicts)
   - Timeout behavior (COUNT query timeouts)
   - Invalid sort field combinations

2. **Edge cases:**
   - Empty result sets
   - Very large result sets (>10000)
   - Unicode/special characters in IDs
   - Deeply nested geometries
   - Mixed include/exclude fields

3. **Performance:**
   - Query performance benchmarks
   - Memory usage profiling
   - Concurrent request handling

4. **Integration:**
   - Multi-database provider testing
   - CQL2 expression coverage
   - All conformance classes verified

---

## 9. STAC SPEC COMPLIANCE MATRIX

| Feature | Spec | Implementation | Status |
|---------|------|----------------|--------|
| Landing Page | Core | StacCatalogController.GetRoot() | ✅ Complete |
| Conformance | Core | StacCatalogController.GetConformance() | ✅ Complete |
| Collections | Collections | Fully implemented CRUD | ✅ Complete |
| Items | Collections | Fully implemented CRUD | ✅ Complete |
| Search | Item-Search | GET and POST both work | ✅ Complete |
| Fields | Fields Extension | Full include/exclude support | ✅ Complete |
| Sort | Sort Extension | Multiple field sorting with direction | ✅ Complete |
| Filter | Filter Extension | CQL2-JSON support | ✅ Complete |
| Context | Context Extension | Implemented in responses | ✅ Complete |
| CRS Filter | CQL2 1.0 | Not implemented | ❌ Missing |
| OGC Features | OGC Features | Correctly not implemented | ✅ N/A |

---

## CONCLUSION

The Honua STAC API implementation is **production-ready with caveats**. The architecture is sound, completeness is excellent, and most correctness issues are minor. The primary concerns are:

1. **Performance**: N+1 queries and in-memory loading need addressing
2. **Spec compliance**: CRS filter parameter missing
3. **Code quality**: Some refactoring for maintainability

With the recommended priority fixes, this would achieve excellent reliability and performance for most use cases.

**Estimated effort to address critical issues:** 20-30 development hours

