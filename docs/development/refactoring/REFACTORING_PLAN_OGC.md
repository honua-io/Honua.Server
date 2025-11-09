# OGC Shared Handlers Refactoring Plan

## Executive Summary

This document outlines a comprehensive plan to refactor `OgcSharedHandlers.cs`, the largest architectural problem in the codebase. The file currently contains 3,235 lines with 47+ static methods handling OGC API Features, OGC API Tiles, and related operations. This makes the code untestable, difficult to maintain, and violates SOLID principles.

**Current State:**
- **File:** `src/Honua.Server.Host/Ogc/OgcSharedHandlers.cs`
- **Size:** 3,235 lines
- **Methods:** 47+ static methods
- **Responsibilities:** OGC API Features, OGC API Tiles, query parsing, HTML rendering, GeoJSON handling, editing, attachments, collection resolution
- **Problem:** Untestable due to static methods, god class anti-pattern

**Goal:**
Break down `OgcSharedHandlers` into focused, testable service classes that can be injected via dependency injection.

---

## Phase 1: Foundation (COMPLETED)

### ✅ Completed Work

1. **Analysis Complete**
   - Categorized all 47+ methods by responsibility
   - Identified logical service boundaries
   - Mapped dependencies between methods

2. **Service Interfaces Created**
   - `IOgcCollectionResolver` - Collection resolution and validation
   - `IOgcFeaturesQueryHandler` - Features query operations
   - `IOgcFeaturesRenderingHandler` - HTML rendering
   - `IOgcTilesHandler` - Tiles operations

3. **Example Implementation**
   - `OgcCollectionResolver` - Fully implemented and registered in DI
   - Demonstrates the refactoring pattern
   - Includes security validation and error handling

4. **DI Registration**
   - Added `IOgcCollectionResolver` to `ServiceCollectionExtensions.cs`
   - Pattern established for future service registrations

### Previously Extracted Services

The following services have already been extracted from `OgcSharedHandlers`:
- `OgcCrsService` - CRS resolution and validation
- `OgcLinkBuilder` - Link building with query parameters
- `OgcParameterParser` - Query parameter parsing

---

## Method Categorization

### 1. Collection Resolution (✅ Completed - OgcCollectionResolver)
- `ResolveCollectionAsync` - Resolves and validates collection IDs
- `TryResolveCollectionAsync` - Convenience wrapper for resolution
- `MapCollectionResolutionError` - Maps errors to HTTP results
- `BuildCollectionId` - Builds collection ID from service/layer
- `TryParseCollectionId` - Parses collection ID into parts (private)
- `ContainsDangerousCharacters` - Security validation (private)

### 2. OGC Features Query Operations (IOgcFeaturesQueryHandler)
- `ParseItemsQuery` - Parses query parameters for items endpoint
- `CombineFilters` - Combines multiple query filters (private helper)
- `ParseBoundingBox` - Parses bbox parameter (private)
- `ParseTemporal` - Parses datetime parameter (private)
- `ParseResultType` - Parses resultType parameter (private)
- `ExecuteSearchAsync` - Executes cross-collection search
- `EnumerateSearchAsync` - Streams search results (private)
- `WriteGeoJsonSearchResponseAsync` - Writes GeoJSON search response (private)
- `BuildQueryablesSchema` - Builds queryables JSON Schema
- `CreateQueryablesPropertySchema` - Creates property schema (private)
- `ConvertExtent` - Converts layer extent to OGC format
- `BuildOrderedStyleIds` - Builds ordered style ID list
- `AppendStyleMetadata` - Appends style metadata (private)

### 3. OGC Features HTML Rendering (IOgcFeaturesRenderingHandler)
- `WantsHtml` - Checks if request wants HTML
- `RenderLandingHtml` - Renders landing page
- `RenderCollectionsHtml` - Renders collections list
- `RenderCollectionHtml` - Renders single collection
- `RenderFeatureCollectionHtml` - Renders feature collection
- `RenderFeatureHtml` - Renders single feature
- `AppendLinksHtml` - Appends links section (private)
- `AppendFeaturePropertiesTable` - Appends properties table (private)
- `AppendGeometrySection` - Appends geometry section (private)
- `AppendMetadataRow` - Appends metadata row (private)
- `RenderHtmlDocument` - Renders HTML document shell (private)
- `HtmlEncode` - HTML encoding helper (private)
- `FormatPropertyValue` - Formats property for display
- `FormatGeometryValue` - Formats geometry for display

### 4. OGC Features GeoJSON/Feature Handling (IOgcFeaturesGeoJsonHandler - New Interface)
- `ToFeature` - Converts feature record to GeoJSON feature
- `BuildFeatureLinks` - Builds feature-level links
- `ParseJsonDocumentAsync` - Parses JSON request body with security limits
- `EnumerateGeoJsonFeatures` - Enumerates features from GeoJSON
- `ReadGeoJsonAttributes` - Reads attributes from GeoJSON feature
- `ConvertJsonElement` - Converts JsonElement to object (private)
- `ConvertJsonElementToString` - Converts JsonElement to string (private)

### 5. OGC Features Editing/Mutation (IOgcFeaturesEditingHandler - New Interface)
- `CreateEditFailureProblem` - Creates edit failure response
- `CreateFeatureEditBatch` - Creates batch edit command
- `FetchCreatedFeaturesWithETags` - Fetches created features with ETags
- `BuildMutationResponse` - Builds mutation response
- `ValidateIfMatch` - Validates If-Match header for optimistic concurrency
- `ComputeFeatureEtag` - Computes ETag for feature
- `NormalizeEtagValue` - Normalizes ETag value (private)
- `HasIfMatch` - Checks if If-Match header present (private)
- `PreferReturnMinimal` - Checks Prefer header (private)
- `ApplyPreferenceApplied` - Applies Preference-Applied header (private)

### 6. OGC Features Attachments (IOgcFeaturesAttachmentHandler - New Interface)
- `ShouldExposeAttachmentLinks` - Checks if attachments should be exposed
- `CreateAttachmentLinksAsync` (2 overloads) - Creates attachment links
- `CreateAttachmentLinksCoreAsync` - Core attachment link creation (private)
- `BuildAttachmentHref` - Builds attachment HREF (private)
- `ResolveLayerIndex` - Resolves layer index in service

### 7. OGC Tiles Operations (IOgcTilesHandler)
- `ResolveTileSize` - Resolves tile size from request
- `ResolveTileFormat` - Resolves tile format from request
- `BuildTileMatrixSetSummary` - Builds tile matrix set summary
- `DatasetMatchesCollection` - Checks if dataset matches collection
- `NormalizeTileMatrixSet` - Normalizes tile matrix set ID
- `TryResolveStyle` - Resolves style for raster dataset
- `ResolveStyleDefinitionAsync` (2 overloads) - Resolves style definition
- `RequiresVectorOverlay` - Checks if style needs vector overlay
- `CollectVectorGeometriesAsync` (2 overloads) - Collects vector geometries for overlay
- `RenderVectorTileAsync` - Renders vector tile (MVT)
- `ResolveBounds` - Resolves bounds for layer/dataset

### 8. Shared Utilities (Remain Static or Move to Utilities)
- `WithResponseHeader` - Adds response header to result
- `WithContentCrsHeader` - Adds Content-Crs header
- `CreateValidationProblem` - Creates validation problem response
- `CreateNotFoundProblem` - Creates not found problem response
- `BuildDownloadFileName` - Builds download filename (private)
- `BuildArchiveEntryName` - Builds archive entry name (private)

### 9. Constants and Enums
- `CollectionIdSeparator` - Constant
- `ApiDefinitionFileName` - Constant
- `DefaultTemporalReferenceSystem` - Constant
- `HtmlMediaType`, `HtmlContentType` - Constants
- `GeoJsonSerializerOptions` - Static field
- `DefaultConformanceClasses` - Static array
- `OgcResponseFormat` - Enum
- `CollectionSummary` - Record
- `HtmlFeatureEntry` - Record
- `HeaderResult` - Inner class

---

## Phase 2: Query and GeoJSON Handlers (Next Steps)

### 2.1 Create IOgcFeaturesGeoJsonHandler Interface
```csharp
internal interface IOgcFeaturesGeoJsonHandler
{
    object ToFeature(
        HttpRequest request,
        string collectionId,
        LayerDefinition layer,
        FeatureRecord record,
        FeatureQuery query,
        FeatureComponents? componentsOverride = null,
        IReadOnlyList<OgcLink>? additionalLinks = null);

    IReadOnlyList<OgcLink> BuildFeatureLinks(
        HttpRequest request,
        string collectionId,
        LayerDefinition layer,
        FeatureComponents components,
        IReadOnlyList<OgcLink>? additionalLinks);

    Task<JsonDocument?> ParseJsonDocumentAsync(HttpRequest request, CancellationToken cancellationToken);

    IEnumerable<JsonElement> EnumerateGeoJsonFeatures(JsonElement root);

    Dictionary<string, object?> ReadGeoJsonAttributes(
        JsonElement featureElement,
        LayerDefinition layer,
        bool removeId,
        out string? featureId);
}
```

### 2.2 Implement OgcFeaturesGeoJsonHandler
- Extract all GeoJSON/feature handling methods from `OgcSharedHandlers`
- Add unit tests for each method
- Register in DI container

### 2.3 Implement OgcFeaturesQueryHandler
- Extract query handling methods
- Implement `IOgcFeaturesQueryHandler`
- Add comprehensive unit tests
- Register in DI container

---

## Phase 3: Editing and Attachments (Week 2)

### 3.1 Create and Implement IOgcFeaturesEditingHandler
- Extract all editing/mutation methods
- Implement optimistic concurrency (ETag) handling
- Add unit tests
- Register in DI

### 3.2 Create and Implement IOgcFeaturesAttachmentHandler
- Extract attachment-related methods
- Implement attachment link generation
- Add unit tests
- Register in DI

---

## Phase 4: Tiles and Rendering (Week 3)

### 4.1 Implement IOgcTilesHandler
- Extract all tile-related methods
- Implement raster and vector tile handling
- Add unit tests
- Register in DI

### 4.2 Implement IOgcFeaturesRenderingHandler
- Extract HTML rendering methods
- Implement template-based rendering
- Add unit tests
- Register in DI

---

## Phase 5: Migration and Cleanup (Week 4)

### 5.1 Update All Consumers
- Update `OgcFeaturesController` to use injected services
- Update `OgcTilesController` to use injected services
- Update any other controllers or handlers

### 5.2 Remove Static Methods from OgcSharedHandlers
- Keep only constants, enums, and records
- Mark old methods as `[Obsolete]` first
- Remove after all consumers updated

### 5.3 Testing
- Add integration tests for each service
- Test dependency injection registration
- Test service interactions
- Verify no regressions

---

## Implementation Guidelines

### Dependency Injection Pattern

**Before (Static):**
```csharp
var (context, error) = await OgcSharedHandlers.TryResolveCollectionAsync(
    collectionId, resolver, cancellationToken);
```

**After (Injected):**
```csharp
public class OgcFeaturesController : ControllerBase
{
    private readonly IOgcCollectionResolver _collectionResolver;

    public OgcFeaturesController(IOgcCollectionResolver collectionResolver)
    {
        _collectionResolver = collectionResolver;
    }

    public async Task<IResult> GetCollection(string collectionId)
    {
        var (context, error) = await _collectionResolver.TryResolveCollectionAsync(
            collectionId, _resolver, cancellationToken);
        // ...
    }
}
```

### Service Scope Guidelines

1. **Singleton Services**
   - Stateless services (most OGC services)
   - No per-request state
   - Thread-safe implementations

2. **Scoped Services**
   - Services that depend on HttpContext
   - Services with per-request state

3. **Transient Services**
   - Lightweight, short-lived operations
   - Not needed for these services

### Testing Strategy

#### Unit Tests
```csharp
public class OgcCollectionResolverTests
{
    [Fact]
    public async Task ResolveCollectionAsync_ValidId_ReturnsSuccess()
    {
        // Arrange
        var resolver = new OgcCollectionResolver();
        var mockContextResolver = new Mock<IFeatureContextResolver>();
        // Setup mock...

        // Act
        var result = await resolver.ResolveCollectionAsync(
            "service::layer", mockContextResolver.Object, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ResolveCollectionAsync_DangerousCharacters_ReturnsFailure()
    {
        // Test security validation
    }
}
```

#### Integration Tests
- Test full request flow with DI container
- Verify service registration
- Test service interactions

---

## Migration Checklist

### For Each Service:
- [ ] Create interface
- [ ] Implement service class
- [ ] Add unit tests (aim for >80% coverage)
- [ ] Register in DI container
- [ ] Update consumers
- [ ] Run integration tests
- [ ] Remove obsolete static methods

### Service Priority Order:
1. ✅ `IOgcCollectionResolver` (Completed)
2. `IOgcFeaturesGeoJsonHandler` (Next)
3. `IOgcFeaturesQueryHandler` (Next)
4. `IOgcFeaturesEditingHandler`
5. `IOgcFeaturesAttachmentHandler`
6. `IOgcTilesHandler`
7. `IOgcFeaturesRenderingHandler`

---

## Benefits

### Testability
- Methods can be unit tested in isolation
- Dependencies can be mocked
- Better code coverage possible

### Maintainability
- Single Responsibility Principle (SRP)
- Easier to understand each service
- Focused, cohesive classes

### Extensibility
- Easy to add new features to specific services
- Can swap implementations via DI
- Better separation of concerns

### Performance
- No change to runtime performance
- Singleton services cached by DI container
- Same method call overhead

---

## Risk Mitigation

### Breaking Changes
- Mark old static methods `[Obsolete]` first
- Keep backward compatibility during migration
- Use feature flags if needed

### Testing
- Add integration tests before refactoring
- Test each phase independently
- Maintain test coverage

### Deployment
- Can be deployed incrementally
- No database changes required
- Backward compatible

---

## Success Criteria

1. ✅ All 47+ methods categorized and assigned to services
2. ✅ Service interfaces defined
3. ✅ At least one service fully implemented (OgcCollectionResolver)
4. ✅ DI registration pattern established
5. [ ] All services implemented
6. [ ] >80% test coverage for all services
7. [ ] All consumers updated
8. [ ] Static methods removed from OgcSharedHandlers
9. [ ] Integration tests passing
10. [ ] Zero regressions

---

## Timeline Estimate

- **Phase 1 (Foundation):** ✅ COMPLETED
- **Phase 2 (Query/GeoJSON):** 1 week
- **Phase 3 (Editing/Attachments):** 1 week
- **Phase 4 (Tiles/Rendering):** 1 week
- **Phase 5 (Migration/Cleanup):** 1 week

**Total Estimated Time:** 4-5 weeks

---

## Notes

### Shared Constants
Constants, enums, and record types can remain in `OgcSharedHandlers.cs` or move to a separate `OgcConstants.cs` file:
- `CollectionIdSeparator`
- `OgcResponseFormat` enum
- `CollectionSummary` record
- `HtmlFeatureEntry` record
- Conformance classes array

### Backward Compatibility
During migration, keep static methods marked `[Obsolete]` to avoid breaking existing code. Remove after all consumers updated.

### Code Reviews
Each phase should be reviewed independently to ensure:
- Correct service boundaries
- Proper error handling
- Comprehensive tests
- Clean abstractions

---

## Conclusion

This refactoring plan addresses the largest architectural problem in the codebase by systematically breaking down `OgcSharedHandlers.cs` into focused, testable services. Phase 1 is complete with `OgcCollectionResolver` serving as a working example. The remaining phases will follow the same pattern, ensuring a smooth, incremental migration with minimal risk.
