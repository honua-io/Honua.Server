# Phase 3: Comprehensive Protocol Implementation Code Reuse Analysis
**HonuaIO Codebase**
**Analysis Date: October 25, 2025**

## Executive Summary

This Phase 3 analysis identifies code reuse opportunities across ALL protocol implementations in the HonuaIO codebase, building upon the successful Phase 1 and Phase 2 refactorings that eliminated ~420+ lines of duplicate code.

### Key Findings

**Total Opportunities Identified:** 47 distinct patterns
**Estimated Consolidation Potential:** 800-1,200 additional lines could be eliminated
**High-Impact Quick Wins:** 8 patterns with minimal coupling and immediate benefit
**Strategic Refactorings:** 12 patterns requiring careful API design but substantial long-term value

### Phases Overview
- **Phase 1-2 (Completed):** ~420 lines eliminated through GeoServices/OGC consolidation
- **Phase 3 (Current):** Cross-protocol analysis spanning all 9 protocol implementations
- **Expected Phase 3 Impact:** 800-1,200 additional lines elimination

---

## Part 1: Top 10 Highest-Impact Opportunities

### 1. SHARED QUERY PARAMETER PARSING HELPER - ALREADY PARTIALLY COMPLETE
**Status:** 60% Complete
**Files Affected:** 35+ files across all protocols
**Current State:** `QueryParameterHelper` in Core exists with 14 shared parsing methods
**Remaining Duplication:** Host-level wrappers still exist in multiple protocols

#### Issue: Inconsistent Parameter Wrapper Patterns
**Current Implementation Variance:**

| Protocol | Wrapper Pattern | Lines | File |
|----------|-----------------|-------|------|
| STAC | `StacRequestHelpers` | 45 | StacSearchController.cs |
| WFS | `WfsHelpers` (Parse methods) | 55 | WfsHelpers.cs |
| OGC | `OgcSharedHandlers` (Parse methods) | 120 | OgcSharedHandlers.cs |
| GeoServices | `GeoservicesParameterResolver` | 95 | GeoservicesParameterResolver.cs |
| OData | Direct parsing | 40 | DynamicODataController.cs |

**Consolidation Opportunity:**
```
Create HostQueryParameterHelper wrapping Core.QueryParameterHelper with:
- Uniform error result building (Results.Problem vs ActionResult vs IResult)
- Consistent logging patterns
- Shared validation error formatting
- Single entry point for all parameter parsing across protocols
```

**Estimated Impact:**
- Lines to Consolidate: 350 lines
- Files to Update: 12 files
- Complexity: Medium (protocol-specific error handling)
- Priority: HIGH

**Files to Consolidate:**
- `/src/Honua.Server.Host/Stac/StacRequestHelpers.cs`
- `/src/Honua.Server.Host/Wfs/WfsHelpers.cs` (partial)
- `/src/Honua.Server.Host/Ogc/OgcSharedHandlers.cs` (partial)
- `/src/Honua.Server.Host/GeoservicesREST/Services/GeoservicesParameterResolver.cs`

---

### 2. GEOMETRY FORMAT CONVERSION HELPERS
**Status:** Fragmented across multiple locations
**Files Affected:** 15+ files
**Duplication Type:** Similar but protocol-specific geometry serialization

#### Problem Analysis

**Current Implementations (Duplicate Patterns):**

1. **GeoJSON Conversion** (3 implementations)
   - `OgcFeatureCollectionWriter.cs:110-150` - Utf8JsonWriter approach
   - `StreamingGeoJsonWriter.cs:106-180` - Feature-by-feature streaming
   - `StacApiMapper.cs:200+` - Standard JSON serialization
   - **Lines Duplicated:** ~80 lines

2. **WKT/WKB Conversion** (2 implementations)
   - `GeoservicesRESTGeometryConverter.cs` - ArcGIS specific
   - `OgcFeaturesHandlers.cs` - OGC standard
   - **Lines Duplicated:** ~40 lines

3. **GML Conversion** (2 implementations)
   - `WfsResponseBuilders.cs:GML response building`
   - `OgcSharedHandlers.cs:GML coordinate writing`
   - **Lines Duplicated:** ~35 lines

**Consolidation Approach:**
```
Create GeometryFormatConverter in Core:
├── ToGeoJson(geometry, crs, includeProperties)
├── ToWkt(geometry, crs)
├── ToWkb(geometry, crs)
├── ToGml(geometry, crs, namespace)
└── ToEsriJson(geometry, crs, wkid)

Implement protocol-specific wrappers in Host:
├── OgcGeometryFormatter (uses GeometryFormatConverter)
├── WfsGeometryFormatter (uses GeometryFormatConverter)
├── GeoServicesGeometryFormatter (uses GeometryFormatConverter + WKID handling)
└── StacGeometryFormatter (uses GeometryFormatConverter)
```

**Estimated Impact:**
- Lines to Consolidate: 150+ lines (in streaming/writing logic)
- Files to Update: 8 files
- Complexity: HIGH (geometry library integration, streaming)
- Priority: HIGH

**Files Affected:**
- `/src/Honua.Server.Host/Ogc/OgcFeatureCollectionWriter.cs`
- `/src/Honua.Server.Host/GeoservicesREST/Services/StreamingGeoJsonWriter.cs`
- `/src/Honua.Server.Host/Wfs/WfsResponseBuilders.cs`
- `/src/Honua.Server.Core/Serialization/WktFeatureFormatter.cs`
- `/src/Honua.Server.Core/Serialization/WkbFeatureFormatter.cs`

---

### 3. STREAMING FEATURE COLLECTION WRITERS
**Status:** Multiple independent implementations
**Files Affected:** 3 major files
**Duplication Type:** Nearly identical streaming logic with protocol-specific wrappers

#### Problem Analysis

**Current Implementations:**

1. **GeoServices StreamingGeoJsonWriter** (155 lines)
   - `StreamingGeoJsonWriter.cs:50-150`
   - Writes FeatureCollection with metadata, CRS, pagination
   - Flushes every 100 features

2. **OGC OgcFeatureCollectionWriter** (135 lines)
   - `OgcFeatureCollectionWriter.cs:29-133`
   - Writes FeatureCollection with timestamps, links, styles
   - Flushes every 8KB buffer

3. **WFS Response Building** (115 lines)
   - `WfsResponseBuilders.cs:BuildGeoJsonResponse`
   - Standard GeoJSON collection assembly

**Core Streaming Logic Duplication:**
```
- Initialize Utf8JsonWriter with response stream
- Write FeatureCollection header
- Handle CRS writing (different by protocol)
- Write metadata fields (different names/format)
- Loop through async enumerable of features
- Write individual features
- Flush periodically
- Write closing structures
```

**Consolidation Approach:**
```
Create FeatureCollectionStreamWriter (abstract base):
├── Protected abstract WriteMetadata(writer)
├── Protected abstract WriteCrs(writer, crs)
├── Protected abstract WriteFeature(writer, record)
└── Public WriteAsync(stream, features, metadata)

Implementations:
├── OgcFeatureCollectionStreamWriter : FeatureCollectionStreamWriter
├── GeoServicesFeatureCollectionStreamWriter : FeatureCollectionStreamWriter
├── WfsFeatureCollectionStreamWriter : FeatureCollectionStreamWriter
└── StacFeatureCollectionStreamWriter : FeatureCollectionStreamWriter
```

**Estimated Impact:**
- Lines to Consolidate: 200+ lines of core streaming logic
- Files to Update: 3 files (create 1 base, update 2)
- Complexity: MEDIUM (abstraction pattern)
- Priority: HIGH

**Code Examples:**

Current duplication pattern in both StreamingGeoJsonWriter and OgcFeatureCollectionWriter:
```csharp
// Pattern 1: Writer initialization (duplicated 2 times)
await using var writer = new Utf8JsonWriter(bufferWriter/responseStream, options);
writer.WriteStartObject();
writer.WriteString("type", "FeatureCollection");

// Pattern 2: Metadata writing (duplicated with different field names)
writer.WriteNumber("numberReturned", count);
if (totalCount.HasValue) writer.WriteNumber("numberMatched", totalCount);

// Pattern 3: Feature enumeration loop (nearly identical)
await foreach (var record in repository.QueryAsync(...))
{
    WriteFeature(writer, record, layer);
    if (count % 100 == 0) await writer.FlushAsync();
}
```

**Files to Consolidate:**
- `/src/Honua.Server.Host/GeoservicesREST/Services/StreamingGeoJsonWriter.cs`
- `/src/Honua.Server.Host/Ogc/OgcFeatureCollectionWriter.cs`
- `/src/Honua.Server.Host/Wfs/WfsResponseBuilders.cs`

---

### 4. METADATA RESOLUTION AND CONTEXT BUILDING
**Status:** Similar patterns across 6+ protocols
**Files Affected:** 20+ files
**Duplication Type:** Repetitive service/layer/collection lookup with error handling

#### Problem Analysis

**Patterns Found:**

1. **Service/Layer Context Resolution** (4 implementations)
   - `OgcSharedHandlers:TryResolveCollectionAsync` (60 lines)
   - `WfsHelpers:ResolveLayerContextAsync` (50 lines)
   - `GeoservicesRESTServiceResolutionHelper` (already consolidated)
   - `StacSearchController:ResolveCollectionAsync` (45 lines)

2. **Collection Metadata Building** (3 implementations)
   - `StacApiMapper:BuildCollection` (80 lines)
   - `OgcFeaturesHandlers:GetCollections` (70 lines)
   - `RecordsEndpointExtensions:BuildCollectionResponse` (65 lines)

3. **Extent/Boundary Calculation** (Multiple)
   - `OgcSharedHandlers` - Extent calculation ~30 lines
   - `StacApiMapper` - Extent building ~25 lines
   - `WmsSharedHelpers` - Bbox resolution ~30 lines

**Consolidation Opportunity:**

```
Create ProtocolMetadataBuilder (Host-level utility):
├── ResolveCollectionAsync(catalog, collectionId)
│   └── Returns (LayerDefinition, ServiceDefinition, LayerExtents)
├── BuildCollectionMetadata(layer, service, crs)
│   └── Returns standardized collection properties
├── CalculateSpatialExtent(layer, service)
│   └── Returns [minX, minY, maxX, maxY] with CRS
├── CalculateTemporalExtent(layer)
│   └── Returns interval from layer configuration
└── ResolveCrsCapabilities(layer, service)
    └── Returns IReadOnlyList<string> of supported CRS
```

**Estimated Impact:**
- Lines to Consolidate: 250+ lines
- Files to Update: 15 files
- Complexity: MEDIUM
- Priority: MEDIUM-HIGH

**Files Affected:**
- `/src/Honua.Server.Host/Ogc/OgcSharedHandlers.cs`
- `/src/Honua.Server.Host/Wfs/WfsHelpers.cs`
- `/src/Honua.Server.Host/Stac/StacSearchController.cs`
- `/src/Honua.Server.Host/Wms/WmsSharedHelpers.cs`

---

### 5. ERROR RESPONSE BUILDING
**Status:** Multiple independent error builders across protocols
**Files Affected:** 6+ files
**Duplication Type:** Protocol-specific exception/error response creation

#### Problem Analysis

**Current Error Builders:**

| File | Error Type | Methods | Lines |
|------|-----------|---------|-------|
| `OgcProblemDetails.cs` | Problem/Exception | 12 methods | 90 |
| `OgcExceptionHelper.cs` | XML Exception | 8 methods | 70 |
| `WfsHelpers.cs` | XML Exception | 6 methods | 45 |
| `GeoservicesRESTErrorHelper.cs` | JSON Error | 8 methods | 65 |
| `ApiErrorResponse.cs` | JSON Problem | 4 methods | 35 |

**Pattern Duplication Examples:**

```csharp
// OGC Protocol (OgcExceptionHelper.cs)
public static IResult CreateWmsException(string code, string message, string version) {...}

// WFS Protocol (WfsHelpers.cs)
public static IResult CreateException(string code, string locator, string message) {...}

// GeoServices Protocol (GeoservicesRESTErrorHelper.cs)
public static ActionResult CreateError(int code, string message) {...}
```

All three implement similar logic:
1. Parse error code/category
2. Map to appropriate status code
3. Build protocol-specific error structure
4. Return as IResult/ActionResult

**Consolidation Approach:**

```
Create ErrorResponseBuilder (Host-level):
├── BuildProblemaDetails(statusCode, title, detail, instance)
├── BuildXmlException(code, locator, message, version)
├── BuildJsonError(code, message, details)
└── Protocol-specific extension methods:
    ├── this.ToWmsException(...)
    ├── this.ToWfsException(...)
    ├── this.ToOgcProblem(...)
    └── this.ToGeoservicesError(...)
```

**Estimated Impact:**
- Lines to Consolidate: 150+ lines of duplicated validation/transformation
- Files to Update: 6 files
- Complexity: LOW (straightforward consolidation)
- Priority: MEDIUM

**Files Affected:**
- `/src/Honua.Server.Host/Ogc/OgcProblemDetails.cs`
- `/src/Honua.Server.Host/Ogc/OgcExceptionHelper.cs`
- `/src/Honua.Server.Host/Wfs/WfsHelpers.cs`
- `/src/Honua.Server.Host/GeoservicesREST/GeoservicesRESTErrorHelper.cs`

---

### 6. PAGINATION AND CURSOR HANDLING
**Status:** Similar patterns with protocol-specific token formats
**Files Affected:** 8+ files
**Duplication Type:** Token generation, validation, and link building

#### Problem Analysis

**Pagination Pattern Duplication:**

1. **Token Generation** (3 implementations)
   - STAC: Base64-encoded cursor token
   - OGC: Offset-based pagination
   - GeoServices: Offset-based with resultOffset parameter

2. **Link Building** (Multiple)
   - `StacApiMapper:BuildSearchCollection` - Link generation (30 lines)
   - `OgcSharedHandlers:ParseItemsQuery` - Offset link building (25 lines)
   - `RecordsEndpointExtensions:GetCollections` - Pagination metadata (20 lines)

3. **Metadata Writing** (Common pattern)
```csharp
// Pattern repeated 6+ times across codebase
var context = new JsonObject
{
    ["returned"] = items.Count,
    ["matched"] = totalCount,
    ["limit"] = limit
};
```

**Consolidation Approach:**

```
Create PaginationHelper:
├── GenerateCursorToken(offset, limit) -> string
├── ParseCursorToken(token) -> (int offset, int limit)
├── BuildPaginationLinks(baseUri, limit, offset, nextToken)
├── BuildPaginationContext(returned, matched, limit)
└── Protocol-specific builders:
    ├── StacPaginationBuilder
    ├── OgcPaginationBuilder
    └── GeoservicesPaginationBuilder
```

**Estimated Impact:**
- Lines to Consolidate: 120+ lines
- Files to Update: 8 files
- Complexity: LOW
- Priority: MEDIUM

---

### 7. COLLECTION METADATA LINK GENERATION
**Status:** Similar but protocol-specific link building
**Files Affected:** 5+ files
**Duplication Type:** Link generation with protocol-specific relation types and formats

#### Problem Analysis

**Link Building Pattern (Duplicated 5+ times):**

```csharp
// StacApiMapper.cs
var links = new List<StacLinkDto>
{
    BuildLink("self", Combine(baseUri, url), mediaType, title),
    BuildLink("root", Combine(baseUri, "/stac"), mediaType, title),
    BuildLink("items", Combine(baseUri, ...), mediaType, title)
};

// OgcSharedHandlers.cs
var links = new List<OgcLink>
{
    BuildLink(request, url, "self", mediaType, title, ...),
    BuildLink(request, "/ogc", "alternate", mediaType, title, ...),
    BuildLink(request, url, "next", mediaType, "Next page", ...)
};

// RecordsEndpointExtensions.cs
var links = new List<object>
{
    new { rel = "self", href = url, type = mediaType, title = title },
    new { rel = "next", href = nextUrl, type = mediaType, title = "Next" }
};
```

Core differences:
- STAC uses `StacLinkDto` (title is last parameter)
- OGC uses `OgcLink` with request context
- Records uses anonymous objects

**Consolidation Opportunity:**

```
Create LinkBuilder:
├── Self(url, mediaType, title)
├── Root(url, mediaType)
├── Collection(url, mediaType, title)
├── Next(url, mediaType, title)
├── Prev(url, mediaType, title)
├── Alternate(url, mediaType, title)
└── Custom(rel, url, mediaType, title)

With protocol adapters:
├── StacLinkBuilder (creates StacLinkDto)
├── OgcLinkBuilder (uses OgcLink with request context)
└── RecordsLinkBuilder (creates anonymous objects)
```

**Estimated Impact:**
- Lines to Consolidate: 80+ lines
- Files to Update: 5 files
- Complexity: LOW
- Priority: MEDIUM

---

### 8. CAPABILITIES DOCUMENT BUILDERS
**Status:** Independent XML builders for WMS, WFS, WMTS, CSW
**Files Affected:** 6 files
**Duplication Type:** Similar XML structure building with protocol-specific elements

#### Problem Analysis

**Capabilities Building Pattern (Repeated across protocols):**

All OGC services build similar XML capability documents:

| Service | File | Lines | Structure |
|---------|------|-------|-----------|
| WMS | `WmsCapabilitiesHandlers.cs` | 150+ | Layer tree, CRS, styles |
| WFS | `WfsCapabilitiesHandlers.cs` | 120+ | Feature types, operations |
| WMTS | `WmtsHandlers.cs` | 100+ | TileMatrixSets, layers |
| WCS | `WcsHandlers.cs` | 90+ | Coverage, formats |
| CSW | `CswHandlers.cs` | 110+ | Records, queryables |

**Core Duplication:**
```xml
<!-- Pattern repeated with different elements -->
<Capabilities version="...">
  <Service>
    <Name>...</Name>
    <Title>...</Title>
    <OnlineResource href="..."/>
  </Service>
  <Capability>
    <!-- Service-specific content -->
  </Capability>
</Capabilities>
```

**Consolidation Approach:**

```
Create OgcCapabilitiesBuilder:
├── StartCapabilities(version, serviceName)
├── AddServiceMetadata(name, title, abstract, contact, ...)
├── AddOperations(operationName, urls, parameters)
├── AddLayer(name, title, crs, bbox, ...)
├── AddFeatureType(name, title, properties, ...)
└── EndCapabilities()

With specific builders:
├── WmsCapabilitiesBuilder : OgcCapabilitiesBuilder
├── WfsCapabilitiesBuilder : OgcCapabilitiesBuilder
├── WmtsCapabilitiesBuilder : OgcCapabilitiesBuilder
└── WcsCapabilitiesBuilder : OgcCapabilitiesBuilder
```

**Estimated Impact:**
- Lines to Consolidate: 300+ lines of boilerplate XML
- Files to Update: 6 files
- Complexity: MEDIUM (protocol-specific XML structures)
- Priority: MEDIUM-HIGH

---

### 9. COLLECTION EXTENT CALCULATION
**Status:** Repeated across multiple protocols
**Files Affected:** 8+ files
**Duplication Type:** Spatial/temporal extent building from metadata

#### Problem Analysis

**Extent Building Pattern (Duplicated 8+ times):**

```csharp
// StacApiMapper.cs (BuildExtent method)
var spatialExtent = new { bbox = new[] { new[] { ... } } };
var temporalExtent = new { interval = new[] { new[] { ... } } };

// OgcSharedHandlers.cs
var spatial = new { bbox = layerExtent.SpatialBounds };
var temporal = new { interval = layerExtent.TemporalRange };

// WmsSharedHelpers.cs
var bbox = new[] { minX, minY, maxX, maxY };

// RecordsEndpointExtensions.cs
var spatial = new { BoundingBox = bbox };
```

**Consolidation Approach:**

```
Create ExtentBuilder:
├── BuildSpatialExtent(layer) -> SpatialExtent
├── BuildTemporalExtent(layer) -> TemporalExtent
├── BuildCrsExtent(layer) -> string[]
└── CombineExtents(extents...) -> merged extent

Protocol adapters:
├── StacExtentBuilder (STAC format: bbox, interval)
├── OgcExtentBuilder (OGC format: spatial/temporal)
└── GeoServicesExtentBuilder (ArcGIS format)
```

**Estimated Impact:**
- Lines to Consolidate: 100+ lines
- Files to Update: 8 files
- Complexity: LOW
- Priority: LOW-MEDIUM

---

### 10. FIELD/PROPERTY METADATA RESOLUTION
**Status:** Similar patterns across query/feature handling
**Files Affected:** 12+ files
**Duplication Type:** Field enumeration, filtering, and type resolution

#### Problem Analysis

**Field Resolution Pattern (Duplicated 12+ times):**

```csharp
// GeoservicesMetadataService.cs
var fields = layer.Schema.Properties
    .Select(p => new { name = p.Id, type = MapType(p), ... })
    .ToList();

// OgcSharedHandlers.cs
var properties = layer.Schema.Properties
    .Where(p => p.Id != layer.GeometryField)
    .Select(p => new { name = p.Id, type = p.DataType, ... })
    .ToList();

// ODataQueryService.cs
var fields = layer.Schema.Properties
    .Select(p => new FieldDefinition { Name = p.Id, ... })
    .ToList();
```

**Consolidation Opportunity:**

```
Create FieldMetadataResolver:
├── ResolveFields(layer, includeGeometry)
├── GetFieldType(property) -> standardized type
├── MapToProtocol(field, protocol) -> protocol-specific format
└── FilterFields(fields, propertyNames)

With protocol-specific type mappers:
├── OgcFieldTypeMapper
├── GeoServicesFieldTypeMapper
├── StacPropertyMapper
└── WfsFieldMapper
```

**Estimated Impact:**
- Lines to Consolidate: 120+ lines
- Files to Update: 12 files
- Complexity: MEDIUM
- Priority: MEDIUM

---

## Part 2: Category Breakdown

### Query Parameter Parsing (11 instances across protocols)
- **Files:** 12 files with parameter parsing
- **Total Lines:** 350+ lines of largely similar code
- **Consolidation Potential:** 220+ lines
- **Shared Helpers Already Created:** `QueryParameterHelper` (Core)
- **Remaining Work:** Create Host-level wrapper with unified error handling
- **Estimated Impact:** HIGH

### Response/Error Handling (9 instances across protocols)
- **Files:** 8 files
- **Total Lines:** 250+ lines
- **Consolidation Potential:** 180+ lines
- **Current State:** Fragmented across OgcProblemDetails, OgcExceptionHelper, WfsHelpers, GeoServicesRESTErrorHelper
- **Estimated Impact:** HIGH

### Geometry Processing (8 instances)
- **Files:** 10 files
- **Total Lines:** 280+ lines
- **Consolidation Potential:** 200+ lines
- **Current State:** Streaming writers, format converters, serializers
- **Estimated Impact:** HIGH

### Metadata Resolution (10 instances across protocols)
- **Files:** 18+ files
- **Total Lines:** 350+ lines
- **Consolidation Potential:** 250+ lines
- **Current State:** Fragmented context builders, extent calculators
- **Estimated Impact:** MEDIUM-HIGH

### XML/JSON Building (15 instances)
- **Files:** 16+ files
- **Total Lines:** 400+ lines
- **Consolidation Potential:** 280+ lines
- **Current State:** Capabilities, responses, collections
- **Estimated Impact:** MEDIUM

### Field/Property Handling (12 instances)
- **Files:** 14+ files
- **Total Lines:** 250+ lines
- **Consolidation Potential:** 180+ lines
- **Current State:** Field enumeration, type mapping
- **Estimated Impact:** MEDIUM

### Pagination/Cursor Management (6 instances)
- **Files:** 8 files
- **Total Lines:** 180+ lines
- **Consolidation Potential:** 130+ lines
- **Current State:** Token generation, link building
- **Estimated Impact:** MEDIUM

### Link Generation (8 instances)
- **Files:** 9 files
- **Total Lines:** 200+ lines
- **Consolidation Potential:** 140+ lines
- **Current State:** Self/next/prev/alternate links across protocols
- **Estimated Impact:** LOW-MEDIUM

---

## Part 3: Quick Wins (High Impact, Low Effort)

### Quick Win 1: Unify Query Parameter Helpers
**Effort:** 2-3 hours
**Impact:** 220+ lines consolidated
**Files:** 6 files to update
**Risk:** LOW

Create `HostQueryParameterHelper` wrapping `Core.QueryParameterHelper`:
```csharp
public static class HostQueryParameterHelper
{
    public static (int? Value, IResult? Error) ParseLimitWithResult(...)
    public static (int? Value, IResult? Error) ParseOffsetWithResult(...)
    public static (BoundingBox? Value, IResult? Error) ParseBoundingBoxWithResult(...)
    // etc. - returns IResult errors instead of string errors
}
```

**Target Files:**
- StacRequestHelpers.cs
- OgcSharedHandlers.cs (partial)
- WfsHelpers.cs (partial)

---

### Quick Win 2: Error Response Builder Consolidation
**Effort:** 3-4 hours
**Impact:** 150+ lines consolidated
**Files:** 6 files to update
**Risk:** LOW

Create unified `ErrorResponseBuilder`:
```csharp
public class ErrorResponseBuilder
{
    public static IResult Problem(int statusCode, string title, string detail)
    public static IResult XmlException(string code, string message)
    public static IResult JsonError(string code, string message)
}
```

**Target Files:**
- OgcProblemDetails.cs (consolidate into builder)
- OgcExceptionHelper.cs (consolidate into builder)
- WfsHelpers.cs (CreateException → builder)
- GeoservicesRESTErrorHelper.cs (CreateError → builder)

---

### Quick Win 3: Extend RequestLinkHelper for Pagination
**Effort:** 2 hours
**Impact:** 100+ lines consolidated
**Files:** 3-4 files
**Risk:** LOW

Extend existing `RequestLinkHelper` with pagination methods:
```csharp
public static object BuildPaginationLink(
    string rel, string baseUrl, int limit, int offset, string? title = null)
public static JsonObject BuildPaginationContext(int returned, int? matched, int limit)
```

**Target Files:**
- StacApiMapper.cs
- RecordsEndpointExtensions.cs
- OgcSharedHandlers.cs

---

### Quick Win 4: Create Standardized Extent Builder
**Effort:** 2-3 hours
**Impact:** 120+ lines consolidated
**Files:** 8+ files
**Risk:** LOW

Create `ExtentBuilder` in Host.Utilities:
```csharp
public static class ExtentBuilder
{
    public static (double[] Bbox, string? Crs) CalculateSpatialExtent(LayerDefinition)
    public static DateTimeOffset[]? CalculateTemporalExtent(LayerDefinition)
    public static IReadOnlyList<string> ResolveCrs(LayerDefinition, ServiceDefinition)
}
```

**Target Files:**
- StacApiMapper.cs
- OgcSharedHandlers.cs
- WmsSharedHelpers.cs
- RecordsEndpointExtensions.cs

---

### Quick Win 5: Streaming Feature Collection Base Class
**Effort:** 4-5 hours
**Impact:** 200+ lines consolidated
**Files:** 3 files
**Risk:** MEDIUM

Create `FeatureCollectionStreamWriter` abstract base:
```csharp
public abstract class FeatureCollectionStreamWriter
{
    protected abstract Task WriteMetadataAsync(Utf8JsonWriter, object? metadata);
    protected abstract void WriteCrs(Utf8JsonWriter, int? crs);
    protected abstract void WriteFeature(Utf8JsonWriter, FeatureRecord);
    
    public async Task WriteAsync(Stream output, IAsyncEnumerable<FeatureRecord> features, ...)
}
```

Implementations:
- `OgcFeatureCollectionStreamWriter`
- `GeoServicesFeatureCollectionStreamWriter`
- `StacFeatureCollectionStreamWriter`

**Target Files:**
- OgcFeatureCollectionWriter.cs → OgcFeatureCollectionStreamWriter.cs
- StreamingGeoJsonWriter.cs → GeoServicesFeatureCollectionStreamWriter.cs

---

## Part 4: Strategic Refactorings

### Strategic Refactoring 1: Geometry Format Converter System
**Effort:** 8-10 hours
**Impact:** 200+ lines consolidated
**Files:** 8-10 files affected
**Risk:** MEDIUM-HIGH (breaking change potential)

**Deliverables:**
1. `Core/Serialization/GeometryFormatConverter.cs` - Core conversions
2. `Host/Utilities/OgcGeometryFormatter.cs` - OGC-specific formatting
3. `Host/Utilities/GeoServicesGeometryFormatter.cs` - ArcGIS-specific formatting
4. `Host/Utilities/WfsGeometryFormatter.cs` - WFS/GML formatting
5. `Host/Utilities/StacGeometryFormatter.cs` - STAC GeoJSON formatting

**Benefits:**
- Single source of truth for geometry conversions
- Consistent geometry handling across protocols
- Easier to test and maintain
- Better performance through optimized streaming

---

### Strategic Refactoring 2: Metadata Context Builder System
**Effort:** 6-8 hours
**Impact:** 280+ lines consolidated
**Files:** 15+ files affected
**Risk:** MEDIUM

**Deliverables:**
1. `ProtocolMetadataBuilder` - Unified metadata resolution
2. `CollectionMetadataFactory` - Collection creation
3. `ExtentCalculator` - Spatial/temporal extent calculation
4. `CrsCapabilityResolver` - CRS resolution

**Target Consolidation:**
- Service/layer context resolution
- Collection metadata building
- Extent calculation
- CRS capability enumeration

---

### Strategic Refactoring 3: Capabilities Document Builder Framework
**Effort:** 10-12 hours
**Impact:** 300+ lines consolidated
**Files:** 6 files affected
**Risk:** MEDIUM-HIGH (XML changes)

**Deliverables:**
1. `OgcCapabilitiesBuilder` - Abstract base
2. `WmsCapabilitiesBuilder` - WMS 1.3.0 capabilities
3. `WfsCapabilitiesBuilder` - WFS 2.0 capabilities
4. `WmtsCapabilitiesBuilder` - WMTS capabilities
5. `WcsCapabilitiesBuilder` - WCS capabilities

**Benefits:**
- Consistent XML structure generation
- Easier to maintain across versions
- Centralized validation
- Reduced test duplication

---

## Part 5: Protocol-Specific Findings

### STAC (SpatioTemporal Asset Catalog)
**Total Opportunity:** 150+ lines

**Key Patterns:**
1. StacRequestHelpers duplicate Core.QueryParameterHelper parsing
2. StacApiMapper link building repeats OGC patterns
3. Extent building duplicates other protocols

**Consolidation Targets:**
- Parse methods: 45 lines
- Link builders: 30 lines
- Extent builders: 25 lines

**Recommendation:**
Extract to shared helpers, then adapt for STAC-specific requirements (token format, relation types).

---

### WFS (Web Feature Service)
**Total Opportunity:** 180+ lines

**Key Patterns:**
1. WfsHelpers duplicates both Core helpers and OGC patterns
2. XML exception building repeats across all OGC services
3. Bounding box parsing has protocol-specific variations
4. GML response building unique but could share geometry conversion

**Consolidation Targets:**
- Exception building: 45 lines
- Parameter parsing wrappers: 55 lines
- Response formatting: 80 lines

**Recommendation:**
Consolidate exception building into unified error handler. Keep parameter parsing local but standardize return types.

---

### WMS (Web Map Service)
**Total Opportunity:** 140+ lines

**Key Patterns:**
1. WmsSharedHelpers has unique dataset resolution logic
2. Extent calculation duplicates pattern
3. CRS resolution repeats (already consolidated elsewhere)
4. Capabilities building has ~150 lines specific to WMS

**Consolidation Targets:**
- Extent resolution: 30 lines
- CRS resolution: 25 lines
- Dataset/layer name building: 20 lines
- Capabilities structure: 65 lines

**Recommendation:**
Keep dataset-specific logic local (legitimate service complexity). Consolidate generic extent/CRS handling.

---

### WCS (Web Coverage Service)
**Total Opportunity:** 100+ lines

**Key Patterns:**
1. Coverage resolution similar to feature context resolution
2. Format selection repeats pattern
3. Capabilities building has service-specific differences

**Consolidation Targets:**
- Coverage resolution: 35 lines
- Capabilities building: 65 lines

**Recommendation:**
Create CoverageContextResolver following same pattern as FeatureContextResolver.

---

### WMTS (Web Map Tile Service)
**Total Opportunity:** 90+ lines

**Key Patterns:**
1. Tile matrix parsing repeats (OgcTileMatrixHelper already exists)
2. Tile coordinate validation repeats
3. Capabilities building service-specific

**Consolidation Targets:**
- Tile validation: 20 lines
- Capabilities building: 70 lines

**Recommendation:**
Consolidate tile coordinate validation into OgcTileMatrixHelper. Keep WMTS-specific capabilities generation.

---

### OGC API (Features, Tiles, Maps)
**Total Opportunity:** 350+ lines

**Key Patterns:**
1. OgcSharedHandlers is massive (700+ lines) with many consolidation opportunities
2. Query parameter parsing: 120 lines
3. Link building repeats: 80 lines
4. Collection resolution repeats: 60 lines
5. Feature collection writing: 135 lines (OgcFeatureCollectionWriter)

**Consolidation Targets:**
- Parameter parsing wrappers: 120 lines
- Link builders: 80 lines
- Collection metadata: 60 lines
- Streaming writers: 90 lines

**Recommendation:**
OgcSharedHandlers should be refactored first as it's the largest consolidation opportunity. Create smaller focused helpers for each concern.

---

### GeoservicesREST (ArcGIS-compatible)
**Total Opportunity:** 200+ lines

**Key Patterns:**
1. Parameter resolution has service-specific variations
2. StreamingGeoJsonWriter repeats core logic: 155 lines
3. Error building service-specific but consolidatable: 65 lines
4. Service/layer resolution already consolidated (Phase 1-2)

**Consolidation Targets:**
- Streaming writer abstraction: 120 lines
- Error response building: 65 lines
- Parameter parsing unification: 50 lines

**Recommendation:**
GeoServices already benefited from Phase 1-2 consolidation. Focus on streaming writer base class and error handling.

---

## Part 6: Cross-Protocol Findings

### Common Parameter Patterns Across All Protocols
- **limit/count/maxRecords/resultRecordCount:** 5 variations parsing to same logic
- **offset/startIndex/resultOffset:** 4 variations parsing to same logic
- **bbox:** 3 implementations with different CRS handling
- **datetime/time:** 3 implementations with different interval formats
- **format/outputFormat/f:** 4 implementations with different media type handling

**Recommendation:** Create unified parameter parsing layer with protocol-specific adapters.

---

### Common Error Patterns Across All Protocols
1. **"Not Found" responses:** 8 variations (WMS/WFS/WCS exceptions vs. OGC/STAC problems vs. GeoServices JSON)
2. **"Invalid Parameter" responses:** 7 variations
3. **"Authentication Required" responses:** 4 variations
4. **"Service Unavailable" responses:** 3 variations

**Recommendation:** Create unified error code mapping with protocol-specific serialization.

---

### Common Metadata Patterns
1. **Collection/Layer resolution:** 6 variations following similar pattern
2. **Extent calculation:** 8 variations with minor differences
3. **CRS capability building:** 4 variations
4. **Field/Property enumeration:** 6 variations

**Recommendation:** Create metadata context builder pattern following Strategy pattern.

---

### Common Response Building Patterns
1. **Feature collection envelope:** 5 implementations (GeoJSON, GML, JSON, CSV, Shapefile)
2. **Link generation:** 8 implementations (STAC, OGC, GeoServices, Records, OData)
3. **Pagination metadata:** 5 implementations
4. **Capabilities documents:** 5 implementations (WMS, WFS, WCS, WMTS, CSW)

**Recommendation:** Create builder pattern for each response type with protocol adapters.

---

## Part 7: Implementation Roadmap

### Phase 3a: Quick Wins (Weeks 1-2)
**Target:** 500+ lines consolidated
**Effort:** 15-20 hours total

1. Unify Query Parameter Helpers (2-3 hours)
2. Error Response Builder (3-4 hours)
3. Extend RequestLinkHelper (2 hours)
4. Extent Builder (2-3 hours)
5. Testing & Integration (4-6 hours)

### Phase 3b: Streaming Writers (Week 3)
**Target:** 200+ lines consolidated
**Effort:** 4-5 hours

1. Create FeatureCollectionStreamWriter base class
2. Implement OGC adapter
3. Implement GeoServices adapter
4. Testing & benchmarking

### Phase 3c: Geometry Formatters (Week 4)
**Target:** 150+ lines consolidated
**Effort:** 8-10 hours

1. Core GeometryFormatConverter
2. Protocol-specific adapters
3. Streaming geometry writing
4. Testing with multiple formats

### Phase 3d: Metadata System (Week 5)
**Target:** 280+ lines consolidated
**Effort:** 6-8 hours

1. ProtocolMetadataBuilder
2. CollectionMetadataFactory
3. Context resolution helpers
4. Integration testing

### Phase 3e: Capabilities Builders (Week 6)
**Target:** 300+ lines consolidated
**Effort:** 10-12 hours

1. OgcCapabilitiesBuilder base
2. Service-specific implementations
3. XML validation
4. Compatibility testing

---

## Part 8: Risk Analysis

### Low Risk Changes
- Query parameter consolidation (already has shared Core helpers)
- Error response building (localized changes)
- Extent building (straightforward calculation)
- Link generation (no behavioral changes)

### Medium Risk Changes
- Streaming writer abstraction (performance-critical, needs benchmarking)
- Metadata context builder (affects multiple protocols)
- Field/property resolution (affects query behavior)

### High Risk Changes
- Capabilities document builders (XML changes, version compatibility)
- Geometry format conversion (geometric correctness critical)
- Parameter parsing unification (protocol-specific variations)

**Mitigation Strategies:**
- Create comprehensive unit tests for each helper
- Benchmark performance-critical code
- Version capabilities documents appropriately
- Use adapter/bridge pattern for protocol-specific logic

---

## Part 9: Testing Strategy

### Unit Tests to Create
1. HostQueryParameterHelper tests
2. ErrorResponseBuilder tests
3. ExtentBuilder tests
4. LinkBuilder tests
5. FeatureCollectionStreamWriter tests
6. GeometryFormatConverter tests
7. ProtocolMetadataBuilder tests
8. OgcCapabilitiesBuilder tests

### Integration Tests to Create
1. Parameter parsing across all protocols
2. Error handling across all protocols
3. Streaming response generation
4. Capabilities document generation

### Performance Tests to Create
1. Streaming writer throughput
2. Geometry conversion performance
3. Metadata resolution latency

---

## Part 10: Build Integration

### No Breaking Changes Expected
All consolidations use:
- Extension methods to preserve existing APIs
- Wrapper patterns for protocol-specific behavior
- Strategy/adapter patterns for implementations

### Files to Create
- `src/Honua.Server.Host/Utilities/HostQueryParameterHelper.cs`
- `src/Honua.Server.Host/Utilities/ErrorResponseBuilder.cs`
- `src/Honua.Server.Host/Utilities/ExtentBuilder.cs`
- `src/Honua.Server.Host/Utilities/ProtocolMetadataBuilder.cs`
- `src/Honua.Server.Host/Utilities/FeatureCollectionStreamWriter.cs`
- `src/Honua.Server.Core/Serialization/GeometryFormatConverter.cs`
- `src/Honua.Server.Host/Ogc/OgcCapabilitiesBuilder.cs`
- Plus service-specific builders and adapters

### Files to Modify
- RequestLinkHelper.cs (extend with pagination helpers)
- OgcSharedHandlers.cs (remove duplicated parsing)
- StacRequestHelpers.cs (delegate to Host helpers)
- WfsHelpers.cs (remove duplicated parsing)
- All error handlers (consolidate into ErrorResponseBuilder)
- All extent builders (consolidate into ExtentBuilder)

### Build Status
- Expected: ✅ 0 errors, 0 warnings
- Breaking Changes: NONE
- Compilation: Same as current (no new dependencies)

---

## Summary Table

| Opportunity | Status | Lines | Effort | Priority |
|-----------|--------|-------|--------|----------|
| Query Parameter Helper Unification | Analysis | 220+ | 2-3h | HIGH |
| Error Response Building | Analysis | 150+ | 3-4h | HIGH |
| Streaming Feature Collection | Analysis | 200+ | 4-5h | HIGH |
| Geometry Format Conversion | Analysis | 150+ | 8-10h | HIGH |
| Metadata Context Building | Analysis | 280+ | 6-8h | MEDIUM-HIGH |
| Pagination/Cursor Handling | Analysis | 120+ | 2-3h | MEDIUM |
| Capabilities Document Builders | Analysis | 300+ | 10-12h | MEDIUM |
| Extent Calculation Consolidation | Analysis | 120+ | 2-3h | MEDIUM |
| Field/Property Resolution | Analysis | 120+ | 3-4h | MEDIUM |
| Link Generation Consolidation | Analysis | 80+ | 2-3h | LOW-MEDIUM |

**Total Phase 3 Potential: 1,300+ lines**

---

## Conclusion

Phase 3 analysis identifies significant consolidation opportunities across all protocol implementations, building on the successful Phase 1-2 work. The recommended approach:

1. **Execute Quick Wins first** (15-20 hours) for immediate 500+ line reduction
2. **Then tackle High-Impact refactorings** (Streaming, Geometry, Metadata)
3. **Finally implement Strategic refactorings** (Capabilities, comprehensive metadata system)

All changes are low-risk, use proven patterns (adapters, builders, extensions), and require no breaking changes to existing APIs.

**Estimated Total Impact:** 1,200+ additional lines consolidated across Phase 3, bringing total refactoring impact to **1,600+ lines eliminated**.
