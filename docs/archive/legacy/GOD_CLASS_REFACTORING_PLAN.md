# God Class Refactoring Plan

**Status:** Phase 1 Complete - Foundation Architecture Created
**Date:** 2025-10-23
**Related Issues:** #11, #12, #13, #14

## Executive Summary

This document outlines the phased approach to refactoring two major "god classes" in the Honua codebase:
1. `GeoservicesRESTFeatureServerController` (3,283 lines)
2. `OgcSharedHandlers` (3,179 lines)

**Total Impact:** 6,462 lines being refactored into 18 new files with clear separation of concerns.

## Phase 1: Foundation (COMPLETED)

### Objectives
- Create architectural foundation WITHOUT moving implementation code
- Establish interfaces and stubs for future refactoring
- Verify compilation of new structure
- Document migration plan for Phase 2

### What Was Created

#### GeoservicesREST Refactoring (Issues #11, #12)

**New Controllers Created:**
1. `GeoservicesRestQueryController.cs` - Query operations
2. `GeoservicesRestEditController.cs` - Editing operations
3. `GeoservicesRestAttachmentController.cs` - Attachment management
4. `GeoservicesRestSyncController.cs` - Sync/replica operations (placeholder)
5. `GeoservicesRestMetadataController.cs` - Service/layer metadata

**Service Layer (Partially Existing):**
- `IGeoservicesQueryService` / `GeoservicesQueryService` - ALREADY IMPLEMENTED
- `IGeoservicesEditingService` / `GeoservicesEditingService` - ALREADY IMPLEMENTED
- `IGeoservicesAttachmentService` - Interface exists, implementation partial
- `IGeoservicesMetadataService` - ALREADY IMPLEMENTED

**Registration:**
- Updated `ServiceCollectionExtensions.AddGeoservicesRestServices()` to register services
- Services are scoped for per-request lifecycle

#### OGC Handlers Refactoring (Issues #13, #14)

**Handler Interfaces Created:**
1. `IWmsHandler.cs` - Web Map Service operations
2. `IWfsHandler.cs` - Web Feature Service operations
3. `IWcsHandler.cs` - Web Coverage Service operations
4. `IWmtsHandler.cs` - Web Map Tile Service operations

**Handler Implementations Created:**
1. `WmsHandlers.cs` - Stub with dependencies
2. `WfsHandlers.cs` - Stub with dependencies
3. `WcsHandlers.cs` - Stub with dependencies
4. `WmtsHandlers.cs` - Stub with dependencies

**Helper Class:**
- `OgcHelpers.cs` already exists with common utilities
- Additional shared logic will be consolidated here in Phase 2

**Registration:**
- Added `ServiceCollectionExtensions.AddOgcHandlers()` method (commented out until Phase 2)

## Current State Analysis

### GeoservicesRESTFeatureServerController (3,283 lines)

**Complexity Metrics:**
- Lines of Code: 3,283
- Primary Responsibilities: 7+
  1. Service metadata (GetService, GetLayer)
  2. Query operations (Query, QueryRelatedRecords)
  3. Editing operations (ApplyEdits, AddFeatures, UpdateFeatures, DeleteFeatures)
  4. Attachment management (partial - see .Attachments.cs)
  5. Export operations (Shapefile, CSV, KML, GeoJSON, TopoJSON)
  6. GlobalId normalization
  7. Extent calculations

**Dependencies:**
- ICatalogProjectionService
- IFeatureRepository
- IFeatureEditOrchestrator
- IFeatureAttachmentOrchestrator
- IAttachmentStoreSelector
- IShapefileExporter
- ICsvExporter
- IMetadataRegistry
- IGeoservicesAuditLogger

**Major Endpoints:**
- GET `/rest/services/{folderId}/{serviceId}/FeatureServer`
- GET `/rest/services/{folderId}/{serviceId}/FeatureServer/{layerIndex}`
- GET/POST `/rest/services/{folderId}/{serviceId}/FeatureServer/{layerIndex}/query`
- GET/POST `/rest/services/{folderId}/{serviceId}/FeatureServer/{layerIndex}/queryRelatedRecords`
- POST `/rest/services/{folderId}/{serviceId}/FeatureServer/{layerIndex}/applyEdits`
- POST `/rest/services/{folderId}/{serviceId}/FeatureServer/{layerIndex}/addFeatures`
- POST `/rest/services/{folderId}/{serviceId}/FeatureServer/{layerIndex}/updateFeatures`
- POST `/rest/services/{folderId}/{serviceId}/FeatureServer/{layerIndex}/deleteFeatures`

### OgcSharedHandlers (3,179 lines)

**Complexity Metrics:**
- Lines of Code: 3,179
- Primary Responsibilities: 4+ protocols
  1. WMS (Web Map Service)
  2. WFS (Web Feature Service)
  3. WCS (Web Coverage Service)
  4. WMTS (Web Map Tile Service)
  5. Common OGC utilities

**Static Class Issues:**
- Cannot use dependency injection
- Difficult to test in isolation
- Hard to mock for unit tests
- All methods must be static

**Major Operations:**
- GetCapabilities (all protocols)
- GetMap, GetFeatureInfo (WMS)
- GetFeature, DescribeFeatureType, Transaction (WFS)
- GetCoverage, DescribeCoverage (WCS)
- GetTile (WMTS)
- Common parameter parsing
- CRS transformations
- Error handling

## Target Architecture

### GeoservicesREST Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      ASP.NET Core Routing                   │
└─────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┼───────────────┐
              ▼               ▼               ▼
    ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
    │   Metadata  │  │    Query    │  │    Edit     │
    │ Controller  │  │ Controller  │  │ Controller  │
    └─────────────┘  └─────────────┘  └─────────────┘
              │               │               │
              └───────────────┼───────────────┘
                              ▼
              ┌─────────────────────────────┐
              │      Service Layer          │
              │  - QueryService             │
              │  - EditingService           │
              │  - AttachmentService        │
              │  - MetadataService          │
              └─────────────────────────────┘
                              │
              ┌───────────────┼───────────────┐
              ▼               ▼               ▼
    ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
    │  Repository │  │   Catalog   │  │   Export    │
    │             │  │             │  │             │
    └─────────────┘  └─────────────┘  └─────────────┘
```

### OGC Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    OGC Endpoint Routing                     │
└─────────────────────────────────────────────────────────────┘
                              │
              ┌───────┬───────┼───────┬───────┐
              ▼       ▼       ▼       ▼       ▼
         ┌─────┐ ┌─────┐ ┌─────┐ ┌─────┐ ┌─────────┐
         │ WMS │ │ WFS │ │ WCS │ │WMTS │ │ Common  │
         │     │ │     │ │     │ │     │ │ Helpers │
         └─────┘ └─────┘ └─────┘ └─────┘ └─────────┘
              │       │       │       │         │
              └───────┴───────┴───────┴─────────┘
                              ▼
              ┌─────────────────────────────┐
              │  Shared Infrastructure      │
              │  - Feature Repository       │
              │  - Raster Registry          │
              │  - Style Registry           │
              └─────────────────────────────┘
```

## Phase 2: Implementation Migration Plan

### Prerequisites
- All Phase 1 files compiled successfully
- All existing tests passing
- Team consensus on approach

### GeoservicesREST Migration Strategy

#### Step 1: Migrate Metadata Operations (Lowest Risk)
**Target:** `GeoservicesRestMetadataController`

**Endpoints to Move:**
1. `GetService()` - Service-level metadata
2. `GetLayer()` - Layer-level metadata

**Process:**
1. Copy method implementations to new controller
2. Add route attributes to new methods
3. Comment out original methods (DO NOT DELETE)
4. Run tests - verify metadata endpoints work
5. Update integration tests to use new controller
6. After 1 week of production validation, delete original methods

**Risk Level:** LOW - Pure read operations, no side effects

#### Step 2: Migrate Query Operations (Medium Risk)
**Target:** `GeoservicesRestQueryController`

**Endpoints to Move:**
1. `QueryAsync()` (GET)
2. `QueryPostAsync()` (POST)
3. `QueryRelatedRecordsGetAsync()`
4. `QueryRelatedRecordsPostAsync()`

**Helper Methods to Move:**
1. `FetchFeaturesAsync()`
2. `FetchDistinctAsync()`
3. `FetchStatisticsAsync()`
4. `FetchIdsAsync()`
5. `CalculateExtentAsync()`

**Export Methods to Move:**
1. `ExportShapefileAsync()`
2. `ExportKmlAsync()`
3. `ExportCsvAsync()`
4. `WriteGeoJsonAsync()`
5. `WriteTopoJsonAsync()`

**Process:**
1. Move methods to controller
2. Ensure IGeoservicesQueryService is fully utilized
3. Add comprehensive logging
4. Add feature flags for gradual rollout
5. A/B test old vs new endpoints
6. Monitor metrics for 2 weeks
7. Deprecate old endpoints

**Risk Level:** MEDIUM - Complex query logic, multiple formats

#### Step 3: Migrate Editing Operations (High Risk)
**Target:** `GeoservicesRestEditController`

**Endpoints to Move:**
1. `ApplyEditsAsync()`
2. `AddFeaturesAsync()`
3. `UpdateFeaturesAsync()`
4. `DeleteFeaturesAsync()`

**Helper Methods to Move:**
1. `ExecuteEditsAsync()`
2. `NormalizeGlobalIdCommandsAsync()`
3. `ResolveObjectIdByGlobalIdAsync()`

**Process:**
1. Create comprehensive test suite first
2. Add transaction logging
3. Move with feature flag (default: OFF)
4. Enable in dev environment for 1 week
5. Enable in staging for 1 week
6. Gradual production rollout (10%, 25%, 50%, 100%)
7. Keep rollback plan ready for 1 month

**Risk Level:** HIGH - Data modification, transaction integrity critical

#### Step 4: Migrate Attachment Operations (Medium Risk)
**Target:** `GeoservicesRestAttachmentController`

**File to Refactor:**
- `GeoservicesRESTFeatureServerController.Attachments.cs`

**Endpoints to Move:**
1. Attachment query endpoints
2. Add/update/delete attachment endpoints

**Process:**
1. Move partial class code to new controller
2. Test with various attachment types
3. Verify S3/Azure blob storage integration
4. Enable gradually with feature flag

**Risk Level:** MEDIUM - File handling, storage integration

### OGC Migration Strategy

#### Step 1: Extract Shared Helpers (Lowest Risk)
**Target:** Consolidate in existing `OgcHelpers.cs`

**Methods to Extract:**
1. `ParseItemsQuery()` - Common parameter parsing
2. `CreateValidationProblem()`
3. `CreateNotFoundProblem()`
4. `ResolveCollectionAsync()`
5. `WithResponseHeader()`
6. Common constants and record types

**Process:**
1. Copy methods from OgcSharedHandlers to OgcHelpers
2. Update method signatures to be instance methods (need DI)
3. Keep original static methods temporarily
4. Update callers gradually
5. Delete originals after validation

**Risk Level:** LOW - Pure utilities

#### Step 2: Migrate WMS Operations (Medium Risk)
**Target:** `WmsHandlers`

**Methods to Move:**
- GetCapabilities (WMS)
- GetMap
- GetFeatureInfo
- Related rendering logic

**Process:**
1. Convert from static to instance methods
2. Inject required dependencies (IFeatureRepository, IRasterRegistry, etc.)
3. Add comprehensive logging
4. Run WMS compliance tests
5. Deploy with feature flag

**Risk Level:** MEDIUM - Map rendering, performance critical

#### Step 3: Migrate WFS Operations (High Risk)
**Target:** `WfsHandlers`

**Methods to Move:**
- GetCapabilities (WFS)
- DescribeFeatureType
- GetFeature
- Transaction (WFS-T)

**Process:**
1. Focus on read operations first (GetCapabilities, DescribeFeatureType, GetFeature)
2. Extensive testing with GML output
3. Transaction operations last (most critical)
4. WFS compliance testing
5. Gradual rollout

**Risk Level:** HIGH - Transaction support, GML complexity

#### Step 4: Migrate WCS/WMTS Operations (Medium Risk)
**Target:** `WcsHandlers` and `WmtsHandlers`

**Process:**
1. WCS operations (coverage data)
2. WMTS operations (tile serving)
3. Performance testing critical
4. Cache integration verification

**Risk Level:** MEDIUM - Raster data handling, caching

## Testing Strategy

### Unit Testing
- Each new service/handler must have 80%+ code coverage
- Mock all dependencies
- Test error conditions thoroughly

### Integration Testing
- Test full request/response cycles
- Verify database transactions
- Test with real data stores

### Regression Testing
- Run full test suite after each migration step
- Compare responses between old and new implementations
- Performance benchmarking

### Production Validation
- Feature flags for gradual rollout
- A/B testing where possible
- Comprehensive monitoring and alerting
- Rollback procedures documented

## Risk Mitigation

### Technical Risks

1. **Breaking Changes**
   - Mitigation: Keep old code commented out during transition
   - Rollback: Feature flags allow instant rollback
   - Validation: Run side-by-side comparisons

2. **Performance Degradation**
   - Mitigation: Benchmark before/after
   - Monitoring: Track response times, throughput
   - Optimization: Profile and optimize hot paths

3. **Transaction Integrity**
   - Mitigation: Comprehensive audit logging
   - Validation: Transaction replay testing
   - Backup: Database snapshots before major deployments

4. **API Contract Changes**
   - Mitigation: Strict endpoint compatibility
   - Validation: Contract testing
   - Documentation: API changelog

### Process Risks

1. **Coordination**
   - Risk: Multiple developers working on same files
   - Mitigation: Clear ownership, branch strategy
   - Process: Daily stand-ups during migration

2. **Timeline Pressure**
   - Risk: Rushing migration leading to bugs
   - Mitigation: Strict phase gates
   - Process: Go/no-go decisions at each phase

3. **Knowledge Transfer**
   - Risk: Team unfamiliar with new architecture
   - Mitigation: Architecture review sessions
   - Documentation: This document + inline docs

## Success Criteria

### Phase 1 (Foundation) - COMPLETED ✓
- [x] All new files created
- [x] All interfaces defined
- [x] DI registration added
- [x] Code compiles successfully
- [x] Documentation complete

### Phase 2 (Implementation Migration) - NOT STARTED
- [ ] All endpoints migrated
- [ ] 100% test coverage maintained
- [ ] No performance regression (< 5% acceptable)
- [ ] Zero data loss incidents
- [ ] All integration tests passing
- [ ] Production deployment successful

### Phase 3 (Cleanup) - NOT STARTED
- [ ] Original god classes deleted
- [ ] Code coverage > 85%
- [ ] Technical debt reduced by 50%
- [ ] Architecture compliance verified

## Timeline Estimate

### Phase 2 Implementation (8-12 weeks)
- Week 1-2: Metadata operations migration
- Week 3-5: Query operations migration
- Week 6-8: OGC helpers and WMS migration
- Week 9-10: Editing operations migration (most critical)
- Week 11-12: Attachment operations, WFS/WCS/WMTS migration

### Phase 3 Cleanup (2 weeks)
- Week 13: Remove old code
- Week 14: Final optimization and documentation

**Total Estimated Time:** 14 weeks (3.5 months)

## Rollback Procedures

### Immediate Rollback (< 5 minutes)
1. Disable feature flag in configuration
2. Restart application if needed
3. Verify old endpoints working

### Code Rollback (< 30 minutes)
1. Revert Git commit
2. Rebuild and deploy
3. Verify functionality

### Database Rollback (if needed)
1. Restore from latest snapshot
2. Replay transaction log
3. Verify data integrity

## Monitoring and Metrics

### Key Metrics to Track
- Endpoint response times (p50, p95, p99)
- Error rates by endpoint
- Database query performance
- Memory usage
- CPU utilization
- Request throughput

### Alerting Thresholds
- Error rate > 1%: Warning
- Error rate > 5%: Critical
- Response time > 2x baseline: Warning
- Response time > 5x baseline: Critical

## Dependencies and Prerequisites

### Before Starting Phase 2
1. All Phase 1 code reviewed and approved
2. Test infrastructure in place
3. Feature flag system operational
4. Monitoring dashboards configured
5. Rollback procedures tested
6. Team training completed

### External Dependencies
- None - all dependencies internal to Honua

## Communication Plan

### Stakeholders
- Development team
- QA team
- DevOps team
- Product management

### Updates
- Weekly status updates
- Daily stand-ups during active migration
- Immediate notification of any issues
- Post-migration retrospective

## Conclusion

Phase 1 has successfully established the architectural foundation for refactoring two major god classes (6,462 total lines). The new structure provides:

1. **Clear Separation of Concerns**: Each controller/handler has a single, well-defined responsibility
2. **Dependency Injection**: All new classes support DI for better testing
3. **Service Layer**: Business logic separated from HTTP concerns
4. **Maintainability**: Smaller, focused classes easier to understand and modify
5. **Testability**: Each component can be tested in isolation

Phase 2 will be a careful, measured migration of implementation code with extensive testing and monitoring at each step. The risk-based approach (metadata → query → editing) ensures that lower-risk operations are migrated first, building confidence before tackling the most critical editing operations.

**Next Steps:**
1. Review this plan with team
2. Build consensus on Phase 2 timeline
3. Set up monitoring and feature flags
4. Begin Phase 2 Step 1 (Metadata migration)

---

**Document Version:** 1.0
**Last Updated:** 2025-10-23
**Owner:** Development Team
**Status:** Phase 1 Complete, Phase 2 Planning
