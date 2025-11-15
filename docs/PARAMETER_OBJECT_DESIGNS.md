# Parameter Object Design Document - High-Priority Methods

**Date:** 2025-11-14  
**Status:** Design Phase (No Implementation)  
**Analysis Based On:** docs/REFACTORING_PLANS.md - Method Parameter Reduction Analysis

---

## Executive Summary

This document provides detailed parameter object designs for 5 high-priority methods with 18-23 parameters. Each method is analyzed for:

- **Parameter grouping logic** - Semantic and functional relationships
- **Parameter object structures** - Class/record definitions with properties
- **Separation criteria** - Parameters that should remain separate
- **Breaking change assessment** - Impact on existing code
- **Implementation priority** - Recommended order of execution

**Total Impact:** Refactoring 5 methods will reduce API surface from 92 parameters to ~15-20 parameter objects (80%+ reduction in complexity).

---

## Method 1: ExecuteCollectionItemsAsync

**Location:** `src/Honua.Server.Host/Ogc/OgcFeaturesHandlers.Items.cs:84`  
**Current Parameters:** 18  
**Type:** Internal async method  
**Visibility:** Internal (static)

### 1.1 Current Signature

```csharp
internal static async Task<IResult> ExecuteCollectionItemsAsync(
    string collectionId,
    HttpRequest request,
    IFeatureContextResolver resolver,
    IFeatureRepository repository,
    IGeoPackageExporter geoPackageExporter,
    IShapefileExporter shapefileExporter,
    IFlatGeobufExporter flatGeobufExporter,
    IGeoArrowExporter geoArrowExporter,
    ICsvExporter csvExporter,
    IFeatureAttachmentOrchestrator attachmentOrchestrator,
    IMetadataRegistry metadataRegistry,
    IApiMetrics apiMetrics,
    OgcCacheHeaderService cacheHeaderService,
    Services.IOgcFeaturesAttachmentHandler attachmentHandler,
    Core.Elevation.IElevationService elevationService,
    ILogger logger,
    IQueryCollection? queryOverrides,
    CancellationToken cancellationToken)
```

### 1.2 Parameter Grouping Analysis

| Group | Category | Parameters | Rationale |
|-------|----------|------------|-----------|
| **Route & Request Context** | Route Parameters | collectionId | Identifies the collection being queried |
| | HTTP Context | request, queryOverrides | HTTP request metadata and query overrides |
| **Core Services** | Data Access | resolver, repository, metadataRegistry | Feature query and metadata resolution |
| **Export Handlers** | Format Exporters | geoPackageExporter, shapefileExporter, flatGeobufExporter, geoArrowExporter, csvExporter | 5 specialized format exporters (tightly coupled) |
| | Post-Processing | attachmentOrchestrator, attachmentHandler | Feature attachment handling |
| | Elevation Data | elevationService | Optional elevation data enrichment |
| **Cross-Cutting** | Observability | apiMetrics, cacheHeaderService, logger | Metrics, caching, and logging |
| **Flow Control** | Async Control | cancellationToken | Cancellation token (keep separate) |

### 1.3 Proposed Parameter Objects

#### A. RequestContext
```csharp
/// <summary>
/// Encapsulates HTTP request context for feature item queries.
/// </summary>
public sealed record OgcFeaturesRequestContext
{
    /// <summary>
    /// The HTTP request object with headers, cookies, and connection info.
    /// </summary>
    public required HttpRequest Request { get; init; }

    /// <summary>
    /// Optional query parameter overrides (used for internal routing).
    /// </summary>
    public IQueryCollection? QueryOverrides { get; init; }
}
```

#### B. ExportServices
```csharp
/// <summary>
/// Aggregates all format exporters for feature collections.
/// Supports multiple output formats in a single handler.
/// </summary>
public sealed record OgcFeatureExportServices
{
    /// <summary>
    /// Exports features to GeoPackage format (.gpkg).
    /// </summary>
    public required IGeoPackageExporter GeoPackage { get; init; }

    /// <summary>
    /// Exports features to Shapefile format (.shp).
    /// </summary>
    public required IShapefileExporter Shapefile { get; init; }

    /// <summary>
    /// Exports features to FlatGeobuf format (.fgb).
    /// </summary>
    public required IFlatGeobufExporter FlatGeobuf { get; init; }

    /// <summary>
    /// Exports features to GeoArrow format (.arrow).
    /// </summary>
    public required IGeoArrowExporter GeoArrow { get; init; }

    /// <summary>
    /// Exports features to CSV format with optional geometry.
    /// </summary>
    public required ICsvExporter Csv { get; init; }
}
```

#### C. AttachmentServices
```csharp
/// <summary>
/// Encapsulates attachment-related services for features.
/// </summary>
public sealed record OgcFeatureAttachmentServices
{
    /// <summary>
    /// Orchestrates bulk attachment operations for feature collections.
    /// </summary>
    public required IFeatureAttachmentOrchestrator Orchestrator { get; init; }

    /// <summary>
    /// Handles attachment-related HTTP requests and responses.
    /// </summary>
    public required Services.IOgcFeaturesAttachmentHandler Handler { get; init; }
}
```

#### D. ObservabilityServices
```csharp
/// <summary>
/// Aggregates cross-cutting concerns for request handling.
/// </summary>
public sealed record OgcFeatureObservabilityServices
{
    /// <summary>
    /// Collects and reports API usage metrics.
    /// </summary>
    public required IApiMetrics Metrics { get; init; }

    /// <summary>
    /// Generates cache control headers and ETags.
    /// </summary>
    public required OgcCacheHeaderService CacheHeaders { get; init; }

    /// <summary>
    /// Logs diagnostic information during request processing.
    /// </summary>
    public required ILogger Logger { get; init; }
}
```

#### E. DataEnrichmentService (Optional)
```csharp
/// <summary>
/// Optional service for enriching features with additional data.
/// </summary>
public sealed record OgcFeatureEnrichmentServices
{
    /// <summary>
    /// Enriches features with elevation data if available.
    /// </summary>
    public Core.Elevation.IElevationService? Elevation { get; init; }
}
```

### 1.4 Proposed New Signature

```csharp
internal static async Task<IResult> ExecuteCollectionItemsAsync(
    string collectionId,
    OgcFeaturesRequestContext requestContext,
    IFeatureContextResolver contextResolver,
    IFeatureRepository repository,
    IMetadataRegistry metadataRegistry,
    OgcFeatureExportServices exportServices,
    OgcFeatureAttachmentServices attachmentServices,
    OgcFeatureEnrichmentServices enrichmentServices,
    OgcFeatureObservabilityServices observabilityServices,
    CancellationToken cancellationToken)
```

### 1.5 Before/After Comparison

| Aspect | Before | After | Reduction |
|--------|--------|-------|-----------|
| **Total Parameters** | 18 | 10 | 44% |
| **Service Parameters** | 11 | 5 objects | 55% |
| **Optional Parameters** | 1 | 1 | 0% |
| **Control Flow Params** | 1 | 1 | 0% |
| **Cognitive Load** | Very High | Medium | 50%+ |

### 1.6 Complexity Assessment

- **Overall Complexity:** Medium
- **Parameter Restructuring:** Straightforward (no logic changes)
- **Dependency Injection:** Update constructor/factory patterns
- **Call Site Updates:** ~5-10 locations that need updating
- **Testing Impact:** All existing unit tests need parameter updates

### 1.7 Breaking Changes Assessment

**Type:** Internal API (non-breaking)

- No public API changes
- Only internal method signature modification
- All callers are within the same assembly
- Estimated affected call sites: ~3-5
- Migration effort: Low (simple refactoring)

### 1.8 Implementation Priority

**Priority Level:** 2 (High)

**Rationale:**
- Already identified in refactoring plans
- Internal API (low risk)
- Part of larger OgcFeaturesHandlers refactoring
- Depends on: Parameter object classes (must be created first)

---

## Method 2: GetCollectionTile

**Location:** `src/Honua.Server.Host/Ogc/OgcTilesHandlers.cs:513`  
**Current Parameters:** 18  
**Type:** Public async handler method  
**Visibility:** Public (static)

### 2.1 Current Signature

```csharp
public static async Task<IResult> GetCollectionTile(
    string collectionId,
    string tilesetId,
    string tileMatrixSetId,
    string tileMatrix,
    int tileRow,
    int tileCol,
    HttpRequest request,
    [FromServices] IFeatureContextResolver resolver,
    [FromServices] IRasterDatasetRegistry rasterRegistry,
    [FromServices] IRasterRenderer rasterRenderer,
    [FromServices] IMetadataRegistry metadataRegistry,
    [FromServices] IFeatureRepository repository,
    [FromServices] IPmTilesExporter pmTilesExporter,
    [FromServices] IRasterTileCacheProvider tileCacheProvider,
    [FromServices] IRasterTileCacheMetrics tileCacheMetrics,
    [FromServices] OgcCacheHeaderService cacheHeaderService,
    [FromServices] Services.IOgcTilesHandler tilesHandler,
    CancellationToken cancellationToken)
```

### 2.2 Parameter Grouping Analysis

| Group | Category | Parameters | Rationale |
|-------|----------|------------|-----------|
| **URL Route** | Tile Coordinates | collectionId, tilesetId, tileMatrixSetId, tileMatrix, tileRow, tileCol | Uniquely identify a tile in the pyramid |
| | HTTP Context | request | Contains headers, query params, content negotiation |
| **Data Access** | Metadata & Query | resolver, rasterRegistry, metadataRegistry, repository | Resolve tile source and metadata |
| **Tile Processing** | Rendering | rasterRenderer | Render raster data to tile image |
| | Export/Caching | pmTilesExporter, tileCacheProvider, tileCacheMetrics | Export and cache tile results |
| **Cross-Cutting** | Observability | cacheHeaderService | Generate cache headers and ETags |
| **Handlers** | Business Logic | tilesHandler | Orchestrate tile retrieval logic |
| **Flow Control** | Async Control | cancellationToken | Cancellation token (keep separate) |

### 2.3 Proposed Parameter Objects

#### A. TileCoordinates
```csharp
/// <summary>
/// Represents a unique tile location in a tile matrix pyramid.
/// Follows OGC API - Tiles specification.
/// </summary>
public sealed record TileCoordinates
{
    /// <summary>
    /// Collection identifier (format: "serviceId::layerId").
    /// </summary>
    public required string CollectionId { get; init; }

    /// <summary>
    /// Tileset identifier (identifies the raster dataset).
    /// </summary>
    public required string TilesetId { get; init; }

    /// <summary>
    /// Tile matrix set identifier (defines the coordinate system).
    /// </summary>
    public required string TileMatrixSetId { get; init; }

    /// <summary>
    /// Tile matrix level (zoom level).
    /// </summary>
    public required string TileMatrix { get; init; }

    /// <summary>
    /// Tile row in the matrix at this zoom level (Y coordinate).
    /// </summary>
    public required int TileRow { get; init; }

    /// <summary>
    /// Tile column in the matrix at this zoom level (X coordinate).
    /// </summary>
    public required int TileCol { get; init; }
}
```

#### B. TileResolutionServices
```csharp
/// <summary>
/// Services for resolving tile metadata and data sources.
/// </summary>
public sealed record TileResolutionServices
{
    /// <summary>
    /// Resolves collection context from collection ID.
    /// </summary>
    public required IFeatureContextResolver ContextResolver { get; init; }

    /// <summary>
    /// Registry for raster dataset definitions and metadata.
    /// </summary>
    public required IRasterDatasetRegistry RasterRegistry { get; init; }

    /// <summary>
    /// Registry for feature metadata and layer definitions.
    /// </summary>
    public required IMetadataRegistry MetadataRegistry { get; init; }

    /// <summary>
    /// Repository for querying vector feature data.
    /// </summary>
    public required IFeatureRepository Repository { get; init; }
}
```

#### C. TileRenderingServices
```csharp
/// <summary>
/// Services for rendering raster tiles.
/// </summary>
public sealed record TileRenderingServices
{
    /// <summary>
    /// Renders raster data into tile image format.
    /// </summary>
    public required IRasterRenderer Renderer { get; init; }

    /// <summary>
    /// Exports tiles in PMTiles format for efficiency.
    /// </summary>
    public required IPmTilesExporter PMTilesExporter { get; init; }
}
```

#### D. TileCachingServices
```csharp
/// <summary>
/// Services for tile caching and performance optimization.
/// </summary>
public sealed record TileCachingServices
{
    /// <summary>
    /// Provides access to cached tiles and cache management.
    /// </summary>
    public required IRasterTileCacheProvider CacheProvider { get; init; }

    /// <summary>
    /// Records cache hit/miss metrics for performance monitoring.
    /// </summary>
    public required IRasterTileCacheMetrics CacheMetrics { get; init; }

    /// <summary>
    /// Generates cache control headers and ETags.
    /// </summary>
    public required OgcCacheHeaderService CacheHeaders { get; init; }
}
```

#### E. TileOperationContext
```csharp
/// <summary>
/// HTTP context for tile operations.
/// </summary>
public sealed record TileOperationContext
{
    /// <summary>
    /// The HTTP request with headers and content negotiation.
    /// </summary>
    public required HttpRequest Request { get; init; }
}
```

### 2.4 Proposed New Signature

```csharp
public static async Task<IResult> GetCollectionTile(
    TileCoordinates coordinates,
    TileOperationContext operationContext,
    [FromServices] TileResolutionServices resolutionServices,
    [FromServices] TileRenderingServices renderingServices,
    [FromServices] TileCachingServices cachingServices,
    [FromServices] Services.IOgcTilesHandler tilesHandler,
    CancellationToken cancellationToken)
```

### 2.5 Before/After Comparison

| Aspect | Before | After | Reduction |
|--------|--------|-------|-----------|
| **Total Parameters** | 18 | 7 | 61% |
| **Route Parameters** | 6 | 1 object | 83% |
| **Service Parameters** | 11 | 4 objects | 64% |
| **Optional Parameters** | 0 | 0 | 0% |
| **Control Flow Params** | 1 | 1 | 0% |

### 2.6 Complexity Assessment

- **Overall Complexity:** Medium-High
- **Parameter Restructuring:** Moderate (tile coordinates reorganization)
- **Dependency Injection:** Register 5 new objects in DI container
- **Call Site Updates:** ~3-8 locations
- **Testing Impact:** Medium (tile endpoint tests need updates)

### 2.7 Breaking Changes Assessment

**Type:** Public API (breaking change - requires versioning)

- **Scope:** Deprecated legacy endpoint (marked for removal)
- **API Stability:** This is a deprecated endpoint anyway
- **Migration Path:** Can be applied as part of deprecation cleanup
- **Estimated impact:** Low (legacy endpoint, <5 call sites)

**Recommendation:** This refactoring can be applied as part of the deprecation removal in a major version.

### 2.8 Implementation Priority

**Priority Level:** 3 (Medium)

**Rationale:**
- Public API (requires versioning)
- Deprecated endpoint (good candidate for refactoring during cleanup)
- Moderate parameter restructuring required
- Can be applied when deprecation is enforced
- Depends on: Parameter object classes

---

## Method 3: BuildLegacyCollectionItemsResponse

**Location:** `src/Honua.Server.Host/Ogc/OgcApiEndpointExtensions.cs:123`  
**Current Parameters:** 18  
**Type:** Public async handler method  
**Visibility:** Internal (static)

### 3.1 Current Signature

```csharp
internal static Task<IResult> BuildLegacyCollectionItemsResponse(
    string serviceId,
    string layerId,
    HttpRequest request,
    [FromServices] ICatalogProjectionService catalog,
    [FromServices] IFeatureContextResolver resolver,
    [FromServices] IFeatureRepository repository,
    [FromServices] IGeoPackageExporter geoPackageExporter,
    [FromServices] IShapefileExporter shapefileExporter,
    [FromServices] IFlatGeobufExporter flatGeobufExporter,
    [FromServices] IGeoArrowExporter geoArrowExporter,
    [FromServices] ICsvExporter csvExporter,
    [FromServices] IFeatureAttachmentOrchestrator attachmentOrchestrator,
    [FromServices] IMetadataRegistry metadataRegistry,
    [FromServices] IApiMetrics apiMetrics,
    [FromServices] OgcCacheHeaderService cacheHeaderService,
    [FromServices] Services.IOgcFeaturesAttachmentHandler attachmentHandler,
    [FromServices] Honua.Server.Core.Elevation.IElevationService elevationService,
    CancellationToken cancellationToken)
```

### 3.2 Parameter Grouping Analysis

| Group | Category | Parameters | Rationale |
|-------|----------|------------|-----------|
| **Route Context** | Legacy Service/Layer IDs | serviceId, layerId | Legacy service/layer identification |
| | HTTP Request | request | HTTP request context |
| **Catalog Service** | Service Discovery | catalog | Legacy catalog/service projection |
| **Core Services** | Data Access | resolver, repository, metadataRegistry | Feature retrieval and metadata |
| **Export Handlers** | Format Exporters | geoPackageExporter, shapefileExporter, flatGeobufExporter, geoArrowExporter, csvExporter | 5 format exporters (identical to ExecuteCollectionItemsAsync) |
| | Attachments | attachmentOrchestrator, attachmentHandler | Feature attachment handling |
| | Elevation | elevationService | Optional elevation enrichment |
| **Cross-Cutting** | Observability | apiMetrics, cacheHeaderService | Metrics and caching |
| **Flow Control** | Async Control | cancellationToken | Cancellation token |

### 3.3 Proposed Parameter Objects

#### A. LegacyCollectionIdentity
```csharp
/// <summary>
/// Identifies a collection using legacy service/layer identifiers.
/// Used for backward compatibility with pre-OGC API endpoints.
/// </summary>
public sealed record LegacyCollectionIdentity
{
    /// <summary>
    /// Legacy service identifier.
    /// </summary>
    public required string ServiceId { get; init; }

    /// <summary>
    /// Legacy layer identifier within the service.
    /// </summary>
    public required string LayerId { get; init; }
}
```

#### B. LegacyCatalogServices
```csharp
/// <summary>
/// Services for legacy catalog/service discovery.
/// </summary>
public sealed record LegacyCatalogServices
{
    /// <summary>
    /// Service for resolving catalog projections.
    /// Used for legacy service/layer lookups.
    /// </summary>
    public required ICatalogProjectionService Catalog { get; init; }
}
```

#### C. LegacyRequestContext
```csharp
/// <summary>
/// HTTP request context for legacy endpoints.
/// </summary>
public sealed record LegacyRequestContext
{
    /// <summary>
    /// The HTTP request object.
    /// </summary>
    public required HttpRequest Request { get; init; }
}
```

#### D. LegacyFeatureExportServices (alias)
```csharp
// Reuse OgcFeatureExportServices from Method 1
// (GeoPackageExporter, ShapefileExporter, FlatGeobufExporter, GeoArrowExporter, CsvExporter)
```

#### E. LegacyObservabilityServices
```csharp
/// <summary>
/// Observability services for legacy endpoints.
/// </summary>
public sealed record LegacyObservabilityServices
{
    /// <summary>
    /// Collects API usage metrics.
    /// </summary>
    public required IApiMetrics Metrics { get; init; }

    /// <summary>
    /// Generates cache headers and ETags.
    /// </summary>
    public required OgcCacheHeaderService CacheHeaders { get; init; }
}
```

### 3.4 Proposed New Signature

```csharp
internal static Task<IResult> BuildLegacyCollectionItemsResponse(
    LegacyCollectionIdentity collectionIdentity,
    LegacyRequestContext requestContext,
    [FromServices] LegacyCatalogServices catalogServices,
    [FromServices] IFeatureContextResolver contextResolver,
    [FromServices] IFeatureRepository repository,
    [FromServices] IMetadataRegistry metadataRegistry,
    [FromServices] OgcFeatureExportServices exportServices,
    [FromServices] OgcFeatureAttachmentServices attachmentServices,
    [FromServices] OgcFeatureEnrichmentServices enrichmentServices,
    [FromServices] LegacyObservabilityServices observabilityServices,
    CancellationToken cancellationToken)
```

### 3.5 Before/After Comparison

| Aspect | Before | After | Reduction |
|--------|--------|-------|-----------|
| **Total Parameters** | 18 | 11 | 39% |
| **Service Parameters** | 15 | 7 objects | 53% |
| **Route Parameters** | 2 | 1 object | 50% |
| **Request Parameters** | 1 | 1 object | 0% |

### 3.6 Complexity Assessment

- **Overall Complexity:** Low-Medium
- **Parameter Restructuring:** Straightforward (mostly reuses objects from Method 1)
- **Dependency Injection:** Register 3-4 new objects
- **Call Site Updates:** ~1-2 locations
- **Testing Impact:** Low (internal endpoint)

### 3.7 Breaking Changes Assessment

**Type:** Internal API (non-breaking)

- Internal static method
- Only 1-2 call sites (within endpoint mapping)
- No external API surface affected
- Migration effort: Very Low (simple refactoring)

### 3.8 Implementation Priority

**Priority Level:** 2 (High)

**Rationale:**
- Internal API (low risk)
- Can reuse parameter objects from Method 1
- Good candidate for early refactoring
- Depends on: OgcFeatureExportServices, etc. from Method 1

---

## Method 4: GeoservicesRESTQueryContext

**Location:** `src/Honua.Server.Host/GeoservicesREST/GeoservicesRESTQueryTranslator.cs:267`  
**Current Parameters:** 19  
**Type:** Record type (data container)  
**Visibility:** Public (sealed record)

### 4.1 Current Signature

```csharp
public sealed record GeoservicesRESTQueryContext(
    FeatureQuery Query,
    bool PrettyPrint,
    bool ReturnGeometry,
    bool ReturnCountOnly,
    bool ReturnIdsOnly,
    bool ReturnExtentOnly,
    IReadOnlyDictionary<string, string> SelectedFields,
    int TargetWkid,
    GeoservicesResponseFormat Format,
    string OutFields,
    IReadOnlyCollection<string>? RequestedOutFields,
    bool ReturnDistinctValues,
    IReadOnlyList<string> GroupByFields,
    IReadOnlyList<GeoservicesRESTStatisticDefinition> Statistics,
    double? MapScale,
    double? MaxAllowableOffset,
    int? GeometryPrecision,
    DateTime? HistoricMoment,
    string? HavingClause);
```

### 4.2 Parameter Grouping Analysis

| Group | Category | Parameters | Rationale |
|-------|----------|------------|-----------|
| **Core Query** | Feature Query | Query | The underlying feature query object |
| **Result Type** | Flags | ReturnGeometry, ReturnCountOnly, ReturnIdsOnly, ReturnExtentOnly, ReturnDistinctValues | Control which aspects of results are returned |
| **Field Selection** | Field Projection | SelectedFields, OutFields, RequestedOutFields | Which fields to include in response |
| **Response Format** | Serialization | PrettyPrint, Format | How to format the output |
| **Spatial** | CRS Handling | TargetWkid, GeometryPrecision, MaxAllowableOffset | Spatial coordinate system and precision |
| **Aggregation** | Grouping & Stats | GroupByFields, Statistics, HavingClause | GROUP BY and aggregation operations |
| **Temporal** | Time Filtering | HistoricMoment | Query specific point in time |
| **Scale** | Cartography | MapScale | Map scale for rendering (optional) |

### 4.3 Proposed Parameter Objects

#### A. QueryResultOptions
```csharp
/// <summary>
/// Controls what aspects of query results are returned.
/// </summary>
public sealed record QueryResultOptions
{
    /// <summary>
    /// Include geometry in results (default: true).
    /// If false, only attributes are returned.
    /// </summary>
    public bool ReturnGeometry { get; init; } = true;

    /// <summary>
    /// Return only feature count, no features (default: false).
    /// Incompatible with other result type flags.
    /// </summary>
    public bool ReturnCountOnly { get; init; } = false;

    /// <summary>
    /// Return only feature IDs, no attributes/geometry (default: false).
    /// Incompatible with other result type flags.
    /// </summary>
    public bool ReturnIdsOnly { get; init; } = false;

    /// <summary>
    /// Return only bounding box extent, no features (default: false).
    /// Incompatible with other result type flags.
    /// </summary>
    public bool ReturnExtentOnly { get; init; } = false;

    /// <summary>
    /// Return distinct values for GROUP BY operation (default: false).
    /// Requires GroupByFields to be specified.
    /// </summary>
    public bool ReturnDistinctValues { get; init; } = false;

    /// <summary>
    /// Validates that result type flags are mutually exclusive.
    /// </summary>
    /// <returns>Validation error message or null if valid.</returns>
    public string? Validate()
    {
        var flagCount = new[] { ReturnCountOnly, ReturnIdsOnly, ReturnExtentOnly, ReturnDistinctValues }
            .Count(f => f);
        return flagCount > 1 ? "Result type flags are mutually exclusive" : null;
    }
}
```

#### B. FieldProjectionOptions
```csharp
/// <summary>
/// Specifies which fields to include in query results.
/// </summary>
public sealed record FieldProjectionOptions
{
    /// <summary>
    /// Comma-separated list of field names (may include wildcards).
    /// Examples: "OBJECTID,NAME", "*" (all fields).
    /// </summary>
    public required string OutFields { get; init; }

    /// <summary>
    /// Parsed collection of requested field names.
    /// Null if OutFields uses wildcard.
    /// </summary>
    public IReadOnlyCollection<string>? RequestedOutFields { get; init; }

    /// <summary>
    /// Final set of fields to include after resolution.
    /// Maps field name to display name/alias.
    /// </summary>
    public required IReadOnlyDictionary<string, string> SelectedFields { get; init; }
}
```

#### C. ResponseFormatOptions
```csharp
/// <summary>
/// Controls response serialization format.
/// </summary>
public sealed record ResponseFormatOptions
{
    /// <summary>
    /// Response format (JSON, GeoJSON, TopoJSON, KML, etc.).
    /// </summary>
    public required GeoservicesResponseFormat Format { get; init; }

    /// <summary>
    /// Pretty print JSON with indentation (default: false).
    /// Increases response size but improves readability.
    /// </summary>
    public bool PrettyPrint { get; init; } = false;
}
```

#### D. SpatialOptions
```csharp
/// <summary>
/// Spatial coordinate system and precision options.
/// </summary>
public sealed record SpatialOptions
{
    /// <summary>
    /// Target WKID (spatial reference) for output geometries.
    /// </summary>
    public required int TargetWkid { get; init; }

    /// <summary>
    /// Number of decimal places for geometry coordinates.
    /// Higher precision = larger response size.
    /// </summary>
    public int? GeometryPrecision { get; init; }

    /// <summary>
    /// Maximum allowable offset for geometry simplification (optional).
    /// Smaller = more detailed geometries.
    /// </summary>
    public double? MaxAllowableOffset { get; init; }
}
```

#### E. AggregationOptions
```csharp
/// <summary>
/// Grouping and aggregation operations for query results.
/// </summary>
public sealed record AggregationOptions
{
    /// <summary>
    /// Fields to group results by.
    /// Required for GROUP BY operations.
    /// </summary>
    public required IReadOnlyList<string> GroupByFields { get; init; }

    /// <summary>
    /// Statistical functions to apply (COUNT, SUM, AVG, MIN, MAX, STDDEV).
    /// </summary>
    public required IReadOnlyList<GeoservicesRESTStatisticDefinition> Statistics { get; init; }

    /// <summary>
    /// HAVING clause for filtering aggregated results.
    /// Applied after aggregation.
    /// </summary>
    public string? HavingClause { get; init; }
}
```

#### F. TemporalOptions
```csharp
/// <summary>
/// Temporal query options for point-in-time queries.
/// </summary>
public sealed record TemporalOptions
{
    /// <summary>
    /// Query data as it existed at a specific point in time.
    /// Uses the layer's temporal column for filtering.
    /// </summary>
    public DateTime? HistoricMoment { get; init; }
}
```

#### G. RenderingOptions (Optional)
```csharp
/// <summary>
/// Optional rendering hints for cartographic display.
/// </summary>
public sealed record RenderingOptions
{
    /// <summary>
    /// Map scale at which to render data (optional).
    /// Used for scale-dependent rendering decisions.
    /// </summary>
    public double? MapScale { get; init; }
}
```

### 4.4 Proposed New Record

```csharp
/// <summary>
/// Encapsulates all parameters for a Geoservices REST API query.
/// Organized into logical option groups for clarity and maintainability.
/// </summary>
public sealed record GeoservicesRESTQueryContext(
    FeatureQuery Query,
    QueryResultOptions ResultOptions,
    FieldProjectionOptions FieldProjection,
    ResponseFormatOptions Format,
    SpatialOptions Spatial,
    AggregationOptions Aggregation,
    TemporalOptions Temporal,
    RenderingOptions? Rendering = null);
```

### 4.5 Before/After Comparison

| Aspect | Before | After | Reduction |
|--------|--------|-------|-----------|
| **Total Properties** | 19 | 8 | 58% |
| **Direct Properties** | 19 | 1 + 7 objects | 95% |
| **Nested Properties** | 0 | ~15 | (reorganized) |
| **Cognitive Load** | Very High | Medium | 60%+ |
| **Grouping Clarity** | Poor | Excellent | - |

### 4.6 Complexity Assessment

- **Overall Complexity:** Medium
- **Parameter Restructuring:** Moderate (breaking change to record constructor)
- **Dependency Injection:** N/A (data container)
- **Call Site Updates:** ~10-15 locations that construct the record
- **Testing Impact:** Medium (record initialization tests)

### 4.7 Breaking Changes Assessment

**Type:** Public Data Type (breaking change)

- **Scope:** Public sealed record with parameterized constructor
- **API Stability:** Breaking change to the constructor signature
- **Migration Path:** Can provide factory method for backward compatibility
- **Estimated impact:** Medium (any code constructing this record)

**Recommendation:** Provide factory/builder methods for backward compatibility:
```csharp
/// <summary>
/// Factory method for creating context from individual parameters.
/// Provided for backward compatibility during migration.
/// </summary>
public static GeoservicesRESTQueryContext Create(
    FeatureQuery query,
    bool prettyPrint,
    bool returnGeometry,
    bool returnCountOnly,
    bool returnIdsOnly,
    bool returnExtentOnly,
    IReadOnlyDictionary<string, string> selectedFields,
    int targetWkid,
    GeoservicesResponseFormat format,
    string outFields,
    IReadOnlyCollection<string>? requestedOutFields,
    bool returnDistinctValues,
    IReadOnlyList<string> groupByFields,
    IReadOnlyList<GeoservicesRESTStatisticDefinition> statistics,
    double? mapScale = null,
    double? maxAllowableOffset = null,
    int? geometryPrecision = null,
    DateTime? historicMoment = null,
    string? havingClause = null)
{
    return new(
        query,
        new QueryResultOptions
        {
            ReturnGeometry = returnGeometry,
            ReturnCountOnly = returnCountOnly,
            ReturnIdsOnly = returnIdsOnly,
            ReturnExtentOnly = returnExtentOnly,
            ReturnDistinctValues = returnDistinctValues
        },
        new FieldProjectionOptions
        {
            OutFields = outFields,
            RequestedOutFields = requestedOutFields,
            SelectedFields = selectedFields
        },
        new ResponseFormatOptions
        {
            Format = format,
            PrettyPrint = prettyPrint
        },
        new SpatialOptions
        {
            TargetWkid = targetWkid,
            GeometryPrecision = geometryPrecision,
            MaxAllowableOffset = maxAllowableOffset
        },
        new AggregationOptions
        {
            GroupByFields = groupByFields,
            Statistics = statistics,
            HavingClause = havingClause
        },
        new TemporalOptions { HistoricMoment = historicMoment },
        mapScale != null ? new RenderingOptions { MapScale = mapScale } : null);
}
```

### 4.8 Implementation Priority

**Priority Level:** 3 (Medium)

**Rationale:**
- Public API (requires versioning strategy)
- Breaking change to constructor
- Can use factory method for backward compatibility
- High cognitive load improvement
- Affects ~10-15 call sites

**Suggested Approach:**
1. Create all option records first
2. Add factory `Create()` method to context
3. Update `TryParse()` to use new structure
4. Gradually migrate call sites to use new constructor
5. Keep factory method for backward compatibility (semantic versioning)

---

## Method 5: BuildJobDto

**Location:** `src/Honua.Server.Intake/BackgroundServices/BuildQueueManager.cs:415`  
**Current Parameters:** 23  
**Type:** Record type (data transfer object)  
**Visibility:** Private (sealed record)

### 5.1 Current Signature

```csharp
private sealed record BuildJobDto(
    Guid id,
    string customer_id,
    string customer_name,
    string customer_email,
    string manifest_path,
    string configuration_name,
    string tier,
    string architecture,
    string cloud_provider,
    string status,
    int priority,
    int progress_percent,
    string? current_step,
    string? output_path,
    string? image_url,
    string? download_url,
    string? error_message,
    int retry_count,
    DateTimeOffset enqueued_at,
    DateTimeOffset? started_at,
    DateTimeOffset? completed_at,
    DateTimeOffset updated_at,
    double? build_duration_seconds);
```

### 5.2 Parameter Grouping Analysis

| Group | Category | Parameters | Rationale |
|-------|----------|------------|-----------|
| **Identity** | Key | id | Unique identifier for build job |
| **Customer Info** | Organization | customer_id, customer_name, customer_email | Who requested the build |
| **Build Configuration** | Specification | manifest_path, configuration_name, tier, architecture, cloud_provider | What to build and where |
| **Job Status** | State | status, priority, retry_count | Current state and queue position |
| **Progress Tracking** | Execution | progress_percent, current_step | Build execution progress |
| **Outputs** | Results | output_path, image_url, download_url | Build artifacts and URLs |
| **Error Information** | Diagnostics | error_message | Failure diagnosis |
| **Timestamps** | Audit Trail | enqueued_at, started_at, completed_at, updated_at | Timeline of events |
| **Metrics** | Analytics | build_duration_seconds | Performance metrics |

### 5.3 Proposed Parameter Objects

#### A. CustomerInfo
```csharp
/// <summary>
/// Customer or organization information for a build job.
/// </summary>
public sealed record CustomerInfo
{
    /// <summary>
    /// Unique customer identifier.
    /// </summary>
    public required string CustomerId { get; init; }

    /// <summary>
    /// Customer display name.
    /// </summary>
    public required string CustomerName { get; init; }

    /// <summary>
    /// Customer email for notifications.
    /// </summary>
    public required string CustomerEmail { get; init; }
}
```

#### B. BuildConfiguration
```csharp
/// <summary>
/// Build job configuration and target specifications.
/// </summary>
public sealed record BuildConfiguration
{
    /// <summary>
    /// Path to the build manifest file.
    /// Contains the specification of what to build.
    /// </summary>
    public required string ManifestPath { get; init; }

    /// <summary>
    /// Named build configuration (e.g., "debug", "release", "production").
    /// </summary>
    public required string ConfigurationName { get; init; }

    /// <summary>
    /// Service tier for the build (e.g., "standard", "premium", "enterprise").
    /// Determines resource allocation.
    /// </summary>
    public required string Tier { get; init; }

    /// <summary>
    /// Target system architecture (e.g., "x86_64", "arm64").
    /// </summary>
    public required string Architecture { get; init; }

    /// <summary>
    /// Target cloud provider (e.g., "aws", "azure", "gcp").
    /// </summary>
    public required string CloudProvider { get; init; }
}
```

#### C. BuildJobStatus
```csharp
/// <summary>
/// Current status and position of a build job in the queue.
/// </summary>
public sealed record BuildJobStatus
{
    /// <summary>
    /// Current job status (e.g., "pending", "building", "completed", "failed").
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Queue priority (higher = earlier execution).
    /// Range: 0-100 (lower numbers = higher priority).
    /// </summary>
    public required int Priority { get; init; }

    /// <summary>
    /// Number of retry attempts for this job.
    /// Incremented when job fails and is retried.
    /// </summary>
    public required int RetryCount { get; init; }
}
```

#### D. BuildProgress
```csharp
/// <summary>
/// Build execution progress tracking.
/// </summary>
public sealed record BuildProgress
{
    /// <summary>
    /// Overall job completion percentage (0-100).
    /// </summary>
    public required int ProgressPercent { get; init; }

    /// <summary>
    /// Description of the current build step.
    /// Examples: "Compiling sources", "Running tests", "Packaging artifacts".
    /// </summary>
    public string? CurrentStep { get; init; }
}
```

#### E. BuildArtifacts
```csharp
/// <summary>
/// Build output artifacts and result links.
/// </summary>
public sealed record BuildArtifacts
{
    /// <summary>
    /// Path to the output artifact (e.g., S3 bucket location, file system path).
    /// </summary>
    public string? OutputPath { get; init; }

    /// <summary>
    /// URL to the build artifact image or container.
    /// Used for direct download or container registry reference.
    /// </summary>
    public string? ImageUrl { get; init; }

    /// <summary>
    /// URL for downloading the build artifact.
    /// </summary>
    public string? DownloadUrl { get; init; }
}
```

#### F. BuildDiagnostics
```csharp
/// <summary>
/// Diagnostic information for build failures.
/// </summary>
public sealed record BuildDiagnostics
{
    /// <summary>
    /// Error message if the build failed.
    /// Provides details about the failure cause.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
```

#### G. BuildTimeline
```csharp
/// <summary>
/// Timestamps tracking the lifecycle of a build job.
/// </summary>
public sealed record BuildTimeline
{
    /// <summary>
    /// When the job was added to the queue.
    /// </summary>
    public required DateTimeOffset EnqueuedAt { get; init; }

    /// <summary>
    /// When the job started executing (null if pending).
    /// </summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>
    /// When the job completed (null if still running).
    /// </summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>
    /// Last update timestamp (tracking modifications).
    /// </summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Calculates the duration of the build (from start to completion).
    /// Returns null if job hasn't completed.
    /// </summary>
    public TimeSpan? GetDuration()
    {
        if (StartedAt == null || CompletedAt == null)
            return null;
        return CompletedAt.Value - StartedAt.Value;
    }

    /// <summary>
    /// Calculates the wait time before build started.
    /// Returns null if job hasn't started.
    /// </summary>
    public TimeSpan? GetWaitTime()
    {
        if (StartedAt == null)
            return null;
        return StartedAt.Value - EnqueuedAt;
    }
}
```

#### H. BuildMetrics
```csharp
/// <summary>
/// Performance metrics and analytics for a build job.
/// </summary>
public sealed record BuildMetrics
{
    /// <summary>
    /// Total build execution time in seconds (from start to completion).
    /// Null if job hasn't completed.
    /// </summary>
    public double? BuildDurationSeconds { get; init; }

    /// <summary>
    /// Calculates throughput metric (builds per hour).
    /// </summary>
    public double? GetThroughput(int successfulBuilds = 1)
    {
        if (BuildDurationSeconds == null || BuildDurationSeconds == 0)
            return null;
        var hoursPerBuild = BuildDurationSeconds.Value / 3600.0;
        return 1.0 / hoursPerBuild * successfulBuilds;
    }
}
```

### 5.4 Proposed New Record

```csharp
/// <summary>
/// Data transfer object for build job information.
/// Organizes 23 parameters into 8 logical groups.
/// Suitable for API responses, database records, and queue management.
/// </summary>
private sealed record BuildJobDto(
    Guid Id,
    CustomerInfo Customer,
    BuildConfiguration Configuration,
    BuildJobStatus JobStatus,
    BuildProgress Progress,
    BuildArtifacts Artifacts,
    BuildDiagnostics Diagnostics,
    BuildTimeline Timeline,
    BuildMetrics Metrics);
```

### 5.5 Before/After Comparison

| Aspect | Before | After | Reduction |
|--------|--------|-------|-----------|
| **Total Properties** | 23 | 9 | 61% |
| **Direct Properties** | 23 | 1 + 8 objects | 96% |
| **Nested Properties** | 0 | ~23 | (reorganized) |
| **Cognitive Load** | Very High | Low-Medium | 70%+ |
| **Semantic Clarity** | Poor | Excellent | - |

### 5.6 Complexity Assessment

- **Overall Complexity:** Medium
- **Parameter Restructuring:** Moderate (significant but logical reorganization)
- **Dependency Injection:** N/A (private DTO)
- **Call Site Updates:** 15-25 locations (internal to BuildQueueManager)
- **Testing Impact:** Medium (DTO initialization tests)
- **Database Mapping:** Will need Dapper mapping updates

### 5.7 Breaking Changes Assessment

**Type:** Private Data Type (non-breaking)

- **Scope:** Private sealed record (internal to BuildQueueManager)
- **API Stability:** No public API impact
- **Migration Path:** Straightforward refactoring
- **Estimated impact:** Very Low (only internal to this class)

**Additional Considerations:**
- Database query mapping will need updates
- Dapper mapping to DTO properties will need adjustment
- Tests that construct BuildJobDto will need updates
- Internal factory/constructor methods may need updates

### 5.8 Implementation Priority

**Priority Level:** 1 (Highest)

**Rationale:**
- Private DTO (non-breaking change)
- No external API impact
- Highest parameter count (23 params)
- Biggest reduction in complexity (61%)
- Easiest to implement (internal scope)
- Good candidate for early refactoring
- Improves code maintainability significantly
- Can serve as example for other refactorings

---

## Implementation Roadmap

### Phase 1: Preparation (1-2 days)
1. Create all parameter object classes
2. Set up unit test fixtures
3. Document design rationale

### Phase 2: Priority 1 Implementation (1-2 days)
1. **BuildJobDto** refactoring
   - Update record definition
   - Update Dapper mappings
   - Update internal constructors
   - Add helper methods to records

### Phase 3: Priority 2 Implementation (2-3 days)
1. **ExecuteCollectionItemsAsync** refactoring
   - Create parameter objects in DI container
   - Update method signature
   - Update call sites (~5 locations)

2. **BuildLegacyCollectionItemsResponse** refactoring
   - Reuse parameter objects from ExecuteCollectionItemsAsync
   - Update method signature
   - Update endpoint mapping

### Phase 4: Priority 3 Implementation (2-3 days)
1. **GetCollectionTile** refactoring
   - Create tile-specific parameter objects
   - Update method signature
   - Update handler implementations
   - Plan deprecation strategy

2. **GeoservicesRESTQueryContext** refactoring
   - Create option records
   - Add factory method for backward compatibility
   - Update TryParse() implementation
   - Migrate call sites gradually

### Phase 5: Testing & Validation (2-3 days)
1. Unit tests for all parameter objects
2. Integration tests for refactored methods
3. Performance regression testing
4. Documentation updates

### Phase 6: Documentation (1 day)
1. Update architecture documentation
2. Create ADR (Architecture Decision Records)
3. Update developer guide
4. Add code examples

**Total Estimated Effort:** 9-15 days

---

## Risk Assessment

### Low Risk
- BuildJobDto (private DTO)
- ExecuteCollectionItemsAsync (internal method)
- BuildLegacyCollectionItemsResponse (internal method)
- Parameter object design itself

### Medium Risk
- GetCollectionTile (public deprecated endpoint, API change)
- GeoservicesRESTQueryContext (public record, constructor breaking change)
- Dapper mapping updates for BuildJobDto

### High Risk
- None identified

### Mitigation Strategies

1. **Backward Compatibility**
   - Provide factory methods for public records
   - Keep old signatures as overloads during transition

2. **Testing**
   - Comprehensive unit tests for all objects
   - Integration tests for each refactored method
   - Shadow testing (run both old and new paths)

3. **Gradual Rollout**
   - Implement in priority order (lowest risk first)
   - One method per PR for easier review
   - Feature flags for public API changes

4. **Documentation**
   - ADR for each significant change
   - Migration guide for developers
   - Code examples in comments

---

## Success Metrics

### Code Quality
- Average method parameters: 18 → 5 (72% reduction)
- Parameter object cohesion: 100% (all related params grouped)
- Cognitive load: Reduced by 50-70%

### Testing
- 95%+ unit test coverage for new classes
- All existing tests pass
- New integration tests added

### Performance
- Zero performance regression (<5% variance)
- Memory usage: Stable or improved
- Response times: Unchanged

### Maintainability
- Clear semantic grouping of related parameters
- Self-documenting parameter objects
- Easier to add new parameters in future

---

## References

- **Refactoring Plans:** `/docs/REFACTORING_PLANS.md`
- **Parameter Object Pattern:** https://refactoring.com/catalog/introduceParameterObject.html
- **OGC API Specification:** https://www.ogc.org/standards/features
- **Geoservices REST API:** https://resources.arcgis.com/en/help/arcgis-rest-api/

---

**Document Version:** 1.0  
**Last Updated:** 2025-11-14  
**Status:** Design Phase - Ready for Review and Implementation Planning
