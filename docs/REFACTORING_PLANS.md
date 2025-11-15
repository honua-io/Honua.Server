# Phase 3 Refactoring Plans

**Date:** 2025-11-14
**Status:** Analysis Complete, Implementation Pending

## Overview

This document contains detailed refactoring plans for Phase 3 clean code improvements, focusing on breaking up God classes and reducing method parameter counts.

---

## 1. MetadataSnapshot.cs Refactoring Plan

**Current State:**
- **File:** `src/Honua.Server.Core/Metadata/MetadataSnapshot.cs`
- **Total Lines:** 1,819 lines
- **Main Class:** MetadataSnapshot (~990 lines)
- **Record Definitions:** 47 different record types (~810 lines)

### 1.1 Proposed Class Structure (7 Core Classes)

#### Primary Classes

1. **MetadataSnapshot** (Simplified) - ~100 lines
   - Immutable data container and facade
   - Delegates validation to MetadataValidator
   - Delegates index building to MetadataIndexBuilder
   - Query methods delegate to MetadataIndexes

2. **MetadataValidator** (Orchestrator) - ~50 lines
   - Coordinates all validation
   - Calls specialized validators in dependency order

3. **StyleValidator** - ~150 lines
   - Validates style definitions
   - Format, geometry type, rules, renderer validation

4. **LayerValidator** - ~200 lines
   - Validates layer metadata
   - SQL views, storage, scales, references

5. **SqlViewValidator** - ~180 lines
   - SQL query validation
   - Parameter validation
   - Security checks (SQL injection prevention)

6. **LayerGroupValidator** - ~200 lines
   - Layer group validation
   - Circular reference detection

7. **MetadataIndexBuilder** - ~100 lines
   - Builds lookup indexes
   - Attaches layers to services

#### Supporting Classes

- **MetadataIndexes** (~120 lines) - Encapsulates index data and query methods
- **CatalogValidator** (~50 lines) - Catalog, Server, CORS validation
- **FolderValidator** (~50 lines) - Folder validation
- **DataSourceValidator** (~50 lines) - Data source validation
- **ServiceValidator** (~150 lines) - Service and stored query validation
- **RasterDatasetValidator** (~100 lines) - Raster dataset validation
- **SqlSecurityValidator** (~80 lines) - SQL injection prevention
- **ParameterValidator** (~100 lines) - SQL parameter validation

### 1.2 Record Definition Organization

Move 47 record definitions to separate files grouped by domain:

```
src/Honua.Server.Core/Metadata/Definitions/
├── CatalogDefinitions.cs (~150 lines)
├── ServerDefinitions.cs (~200 lines)
├── FolderDefinitions.cs (~30 lines)
├── DataSourceDefinitions.cs (~30 lines)
├── ServiceDefinitions.cs (~150 lines)
├── LayerDefinitions.cs (~200 lines)
├── LayerEditingDefinitions.cs (~100 lines)
├── FieldDefinitions.cs (~60 lines)
├── StyleDefinitions.cs (~100 lines)
├── RasterDefinitions.cs (~150 lines)
├── MetadataStandardDefinitions.cs (~400 lines)
└── SharedDefinitions.cs (~40 lines)
```

### 1.3 Migration Path

**Phase 1: Extract Record Definitions** (1-2 days)
- Create Definitions/ directory
- Move records to new files (grouped by domain)
- Maintain namespace consistency

**Phase 2: Extract Index Infrastructure** (1 day)
- Create MetadataIndexes class
- Create MetadataIndexBuilder
- Update MetadataSnapshot to use builder

**Phase 3: Extract Validators - Part 1** (2 days)
- Simple validators: Catalog, Folder, DataSource, SqlSecurity, Parameter

**Phase 4: Extract Validators - Part 2** (3 days)
- Complex validators: Style, Service, RasterDataset, SqlView, Layer, LayerGroup

**Phase 5: Create Orchestrator** (1 day)
- Create MetadataValidator orchestrator
- Update MetadataSnapshot constructor

**Phase 6: Final Cleanup** (1 day)
- Access modifier review
- Documentation updates
- Integration tests

**Total Estimated Time:** 8-10 days

### 1.4 Risk Assessment

- **Complexity:** Medium
- **Risk:** Low
- **Breaking Changes:** None ✅
- **Backward Compatibility:** 100%

All public APIs remain unchanged. Only internal implementation is reorganized.

---

## 2. OgcFeaturesHandlers.Items.cs Refactoring Plan

**Current State:**
- **File:** `src/Honua.Server.Host/Ogc/OgcFeaturesHandlers.Items.cs`
- **Total Lines:** 1,467 lines
- **Responsibility:** Handles 12+ output formats in one monolithic method

### 2.1 Strategy Pattern Implementation

#### Format Handler Categories

**Export Formats (5 handlers):**
- GeoPackageFormatHandler
- ShapefileFormatHandler
- FlatGeobufFormatHandler
- GeoArrowFormatHandler
- CsvFormatHandler

**Streaming Formats (2 handlers):**
- GeoJsonStreamingFormatHandler
- HtmlStreamingFormatHandler

**Buffered Formats (7 handlers):**
- GeoJsonBufferedFormatHandler
- HtmlBufferedFormatHandler
- KmlFormatHandler / KmzFormatHandler
- TopoJsonFormatHandler
- WktFormatHandler
- WkbFormatHandler
- JsonLdFormatHandler
- GeoJsonTFormatHandler

### 2.2 Core Interface Design

```csharp
public interface IOgcItemsFormatHandler
{
    OgcResponseFormat Format { get; }

    ValidationResult Validate(
        FeatureQuery query,
        string? requestedCrs,
        FormatContext context);

    bool RequiresBuffering(FormatContext context);

    Task<IResult> HandleAsync(
        FormatRequest request,
        CancellationToken cancellationToken);
}
```

### 2.3 File Organization

```
src/Honua.Server.Host/Ogc/Features/
├── Handlers/
│   ├── IOgcItemsFormatHandler.cs
│   ├── FormatHandlerBase.cs
│   ├── OgcFormatHandlerRegistry.cs
│   ├── Export/
│   │   ├── GeoPackageFormatHandler.cs
│   │   ├── ShapefileFormatHandler.cs
│   │   ├── FlatGeobufFormatHandler.cs
│   │   ├── GeoArrowFormatHandler.cs
│   │   └── CsvFormatHandler.cs
│   ├── Streaming/
│   │   ├── GeoJsonStreamingFormatHandler.cs
│   │   ├── HtmlStreamingFormatHandler.cs
│   │   └── StreamingResultClasses.cs
│   └── Buffered/
│       ├── GeoJsonBufferedFormatHandler.cs
│       ├── HtmlBufferedFormatHandler.cs
│       ├── KmlFormatHandler.cs
│       ├── TopoJsonFormatHandler.cs
│       ├── WktFormatHandler.cs
│       ├── WkbFormatHandler.cs
│       ├── JsonLdFormatHandler.cs
│       └── GeoJsonTFormatHandler.cs
└── Utilities/
    ├── FeatureCollectionBuilder.cs
    └── PaginationHelper.cs
```

### 2.4 Migration Strategy

**Phase 1: Preparation** (3-5 days)
- Create interfaces and base classes
- Create registry infrastructure
- Extract shared utilities

**Phase 2: Incremental Handler Implementation** (15-20 days)
- Week 1: Export formats (simplest)
- Week 2: Simple formatters (WKT, WKB, TopoJSON)
- Week 3: Complex formatters (KML, JsonLD, GeoJSON-T)
- Week 4: Streaming handlers
- Week 5: Default handlers

**Phase 3: Orchestration Refactor** (5-7 days)
- Update ExecuteCollectionItemsAsync
- Shadow testing (run both paths)
- Gradual switchover with feature flags

**Phase 4: Cleanup** (2-3 days)
- Remove old code
- Optimize performance
- Documentation

**Total Estimated Time:** 25-35 days (5-7 weeks)

### 2.5 Benefits

- **Main handler reduced:** 1,467 → ~400 lines
- **Each format handler:** <200 lines
- **Testability:** Each handler unit-testable in isolation
- **Extensibility:** Plugin architecture for custom formats
- **Maintainability:** Change one format without affecting others

### 2.6 Risk Assessment

- **Complexity:** Medium
- **Risk:** Medium (due to size)
- **Breaking Changes:** None ✅
- **Backward Compatibility:** 100%
- **Performance Impact:** <5% variance expected

---

## 3. Method Parameter Reduction Analysis

**Finding:** 102 methods with 10+ parameters across the codebase

### 3.1 Priority Categories

**CRITICAL (20+ parameters): 3 methods**
1. MapCommentDto - 34 params (SqliteCommentRepository.cs:767)
2. BuildJobDto - 23 params (BuildQueueManager.cs:415)
3. BuildHeader - 21 params (PmTilesExporter.cs:241)

**HIGH (15-19 parameters): 25 methods**
1. CreateCommentAsync - 19 params (CommentService.cs:29)
2. GeoservicesRESTQueryContext - 19 params (GeoservicesRESTQueryTranslator.cs:267)
3. BuildLegacyCollectionItemsResponse - 18 params (OgcApiEndpointExtensions.cs:123)
4. ExecuteCollectionItemsAsync - 18 params (OgcFeaturesHandlers.Items.cs:84)
5. GetCollectionTile - 18 params (OgcTilesHandlers.cs:513)

**MEDIUM (10-14 parameters): 74 methods**

### 3.2 Recommended Refactoring Patterns

1. **Parameter Object Pattern**
   - Group related parameters into single objects
   - Example: 19 params → 2-3 parameter objects

2. **Builder Pattern**
   - For complex record/DTO initialization
   - Fluent API for optional parameters

3. **Options Pattern**
   - For configuration and boolean flags
   - Matches ASP.NET Core best practices

4. **Context Objects**
   - Group service dependencies
   - Reduce constructor parameter counts

### 3.3 Example Refactoring

**Before:**
```csharp
public async Task<Comment> CreateCommentAsync(
    string entityType, string entityId, string text, string? replyToId,
    string userId, string userName, string? userEmail, string? userRole,
    Dictionary<string, string>? metadata, bool isInternal, bool isPinned,
    DateTime? expiresAt, string? ipAddress, string? userAgent,
    List<string>? mentions, List<string>? tags, string? attachmentId,
    string? sentiment, double? sentimentScore, CancellationToken ct)
```

**After:**
```csharp
public record CreateCommentRequest
{
    public required CommentTarget Target { get; init; }
    public required CommentContent Content { get; init; }
    public required UserContext User { get; init; }
    public CommentOptions? Options { get; init; }
    public RequestMetadata? Metadata { get; init; }
}

public async Task<Comment> CreateCommentAsync(
    CreateCommentRequest request,
    CancellationToken ct)
```

**Benefits:**
- 19 params → 1 param
- Named properties improve readability
- Easy to add new fields without breaking changes
- Better IntelliSense support

### 3.4 Implementation Priority

**Phase 1 (High Impact):** Focus on public APIs
- CreateCommentAsync (19 params) - HIGH visibility
- ExecuteCollectionItemsAsync (18 params) - Already being refactored
- GetCollectionTile (18 params) - PUBLIC API

**Phase 2 (Medium Impact):** Internal services
- BuildJobDto, MapCommentDto (records)
- Service layer methods

**Phase 3 (Low Impact):** Private/internal methods
- Helper methods
- Database schema definitions

**Estimated Effort:** 10-15 days for high-priority methods

---

## 4. Implementation Roadmap

### Week 1-2: MetadataSnapshot Refactoring
- Extract record definitions
- Extract validators
- Create orchestrator
- **Deliverable:** 28 focused files vs 1 God class

### Week 3-4: Parameter Object Refactoring
- Refactor top 10 methods with excessive parameters
- Create parameter object classes
- Update call sites
- **Deliverable:** 10 methods refactored, improved APIs

### Week 5-9: OgcFeaturesHandlers Refactoring
- Implement format handlers incrementally
- Shadow testing
- Gradual migration
- **Deliverable:** 14 format handlers, extensible architecture

### Week 10: Documentation & Testing
- Update architecture documentation
- Create ADRs (Architecture Decision Records)
- Performance benchmarking
- Integration testing
- **Deliverable:** Complete documentation, validated performance

---

## 5. Success Metrics

### Code Quality
- ✅ Average file size: <300 lines (from 1,000+ line files)
- ✅ Average method size: <50 lines
- ✅ Average parameters: <5 per method (from 10-20+)
- ✅ Cyclomatic complexity: <10 per method

### Maintainability
- ✅ Single Responsibility: Each class has one clear purpose
- ✅ Testability: 90%+ unit test coverage on new classes
- ✅ Documentation: Complete XML docs on public APIs

### Performance
- ✅ Zero performance regressions
- ✅ Response time variance: <5%
- ✅ Memory usage: Stable or improved

### Compatibility
- ✅ Zero breaking changes to public APIs
- ✅ 100% backward compatibility
- ✅ All existing tests pass

---

## 6. Risk Management

### Low Risk Items
- MetadataSnapshot refactoring (internal only)
- Parameter object pattern (additive changes)
- Documentation updates

### Medium Risk Items
- OgcFeaturesHandlers refactoring (large scope)
- Public API changes (requires careful versioning)

### Mitigation Strategies
1. **Incremental migration** - One component at a time
2. **Shadow testing** - Run old and new code paths in parallel
3. **Feature flags** - Enable new code gradually
4. **Rollback capability** - Keep old code until fully validated
5. **Extensive testing** - Unit, integration, performance tests
6. **Code review** - Peer review for all changes

---

## 7. Next Steps

1. **Review and approve** this refactoring plan
2. **Prioritize** which refactorings to tackle first
3. **Create tickets** for each phase
4. **Assign resources** (developers, reviewers)
5. **Set milestones** and track progress
6. **Execute** incrementally with continuous validation

---

*This document will be updated as refactorings progress. Last updated: 2025-11-14*
