# Test Project Splitting Plan

## Executive Summary

This document outlines a plan to split the monolithic `Honua.Server.Core.Tests` project (320 files, 3,138 tests, 110K LOC) into 8 smaller, focused test projects that can execute in parallel across 22 CPU cores.

**Current State:**
- Single test project with 3,046 tests (after excluding GitOps)
- Test execution time: 30+ minutes even with parallelism
- 55 subdirectories organized by feature area
- Limited ability to leverage multi-core systems

**Target State:**
- 8 focused test projects with 200-500 tests each
- Parallel execution across all projects
- Shared test infrastructure library
- Estimated test time reduction: 60-70% (10-12 minutes)

---

## Test Distribution Analysis

### Current Test Count by Directory

| Directory | Tests | Files | Priority |
|-----------|-------|-------|----------|
| Raster | 563 | 45 | Split Required |
| Data | 383 | 32 | Split Required |
| Hosting | 240 | 24 | High |
| Query | 163 | 6 | High |
| Metadata | 156 | 15 | High |
| OpenRosa | 147 | 5 | Medium |
| Stac | 136 | 20 | Medium |
| Ogc | 116 | 17 | Medium |
| Authentication | 113 | 11 | Medium |
| Security | 110 | 7 | Medium |
| Configuration | 74 | 6 | Low |
| Styling | 70 | 5 | Low |
| Resilience | 50 | 4 | Low |
| Discovery | 48 | 5 | Low |
| Geometry | 48 | 3 | Low |
| Import | 47 | 6 | Low |
| Observability | 46 | 4 | Low |
| Geoservices | 43 | 2 | Low |
| Caching | 40 | 3 | Low |
| Others (24 dirs) | 360 | 66 | Low |

**Total: 3,046 tests across 309 files**

---

## Proposed Test Projects

### 1. Honua.Server.Core.Tests.Raster (563 tests)
**Purpose:** All raster processing, caching, and analytics tests

**Directories:**
- `Raster/` (all subdirectories)
  - `Cache/` - Raster tile caching (COG, Zarr)
  - `Caching/` - Legacy caching infrastructure
  - `Compression/` - Blosc, Gzip, Lz4, Zstd decompression
  - `Kerchunk/` - Kerchunk reference generation
  - `Mosaic/` - Raster mosaic operations
  - `Readers/` - COG and Zarr readers
  - `Sources/` - Provider registry (FileSystem, S3, GCS, Azure)
  - `Analytics/` - Raster analytics service

**Rationale:** Largest test suite; self-contained domain; heavy compute requirements make it ideal for isolation

**Dependencies:**
- Testcontainers.Minio (S3 compatible storage)
- Testcontainers.Azurite (Azure blob emulation)
- MaxRev.Gdal.Core (GDAL bindings)
- Test raster data files (cea.tif)

**Estimated Execution Time:** 5-7 minutes (parallel)

---

### 2. Honua.Server.Core.Tests.Data (383 tests)
**Purpose:** Data access, database providers, and query infrastructure

**Directories:**
- `Data/` (all subdirectories)
  - `Postgres/` - PostgreSQL-specific operations
  - `SqlServer/` - SQL Server provider
  - `MySql/` - MySQL provider
  - `Sqlite/` - SQLite provider
  - `Query/` - Query builder and optimization
  - `Rasters/` - Database raster metadata

**Rationale:** Second largest suite; database operations benefit from isolation to avoid connection pool contention

**Dependencies:**
- Testcontainers.PostgreSql
- Testcontainers.MySql
- Testcontainers.MsSql
- MySqlConnector
- Microsoft.Data.SqlClient
- SharedPostgresFixture (shared test infrastructure)

**Estimated Execution Time:** 6-8 minutes (parallel)

---

### 3. Honua.Server.Core.Tests.OgcProtocols (475 tests)
**Purpose:** OGC standard protocol implementations

**Directories:**
- `Ogc/` (116 tests) - OGC API Features, Tiles, Processes
- `Wfs/` (21 tests) - WFS 1.0/2.0/3.0
- `Wcs/` (11 tests) - WCS 1.0/2.0
- `Wmts/` (13 tests) - WMTS 1.0
- `Hosting/` (240 tests) - Protocol endpoint integration tests
  - WmsEndpointTests.cs
  - WfsEndpointTests.cs
  - OgcLandingEndpointTests.cs
  - OgcTileJsonEndpointTests.cs
  - OgcEditingTests.cs
- `Csw/` (12 tests) - CSW 2.0/3.0 catalog
- `Carto/` (5 tests) - Carto toolkit
- `Print/` (2 tests) - MapFish print service

**Rationale:** Groups all OGC-related standards; significant overlap in test infrastructure

**Dependencies:**
- HonuaTestWebApplicationFactory
- Integration test fixtures
- Sample metadata files

**Estimated Execution Time:** 4-6 minutes (parallel)

---

### 4. Honua.Server.Core.Tests.Apis (390 tests)
**Purpose:** Modern API protocols and data access patterns

**Directories:**
- `Stac/` (136 tests) - STAC catalog, collections, items
- `Geoservices/` (43 tests) - Esri GeoServices REST API
- `OData/` (7 tests) - OData query protocol
- `OpenRosa/` (147 tests) - OpenRosa XForms protocol
- `Api/` (32 tests) - Generic API infrastructure
- `Catalog/` (3 tests) - Catalog endpoints
- `Metadata/` (156 tests) - Metadata management and caching
  - (Split from general hosting to API-focused project)

**Rationale:** Modern API standards that share similar testing patterns; frequently used together

**Dependencies:**
- HonuaTestWebApplicationFactory
- StacTestExtentStubs
- StacTestJsonHelpers
- Sample JSON metadata files

**Estimated Execution Time:** 4-6 minutes (parallel)

---

### 5. Honua.Server.Core.Tests.Security (435 tests)
**Purpose:** Authentication, authorization, and security features

**Directories:**
- `Authentication/` (113 tests) - JWT, SAML, OAuth, API keys
- `Security/` (110 tests) - Encryption, key management, path validation
- `Authorization/` (19 tests) - RBAC, resource authorization
- `Auth/` (20 tests) - Auth middleware and handlers
- `Query/` (163 tests) - SQL injection prevention, query security
- `Configuration/` (10 tests) - Security-related configuration

**Rationale:** Security tests should be grouped for comprehensive security auditing; query security overlaps heavily with general security

**Dependencies:**
- JWT libraries
- Mock authentication handlers
- ResourceAuthorizationCache test fixtures

**Estimated Execution Time:** 4-5 minutes (parallel)

---

### 6. Honua.Server.Core.Tests.DataOperations (330 tests)
**Purpose:** Data editing, import/export, and feature operations

**Directories:**
- `Import/` (47 tests) - Data ingestion service
- `Export/` (17 tests) - GeoParquet, FlatGeobuf, GeoArrow export
- `Editing/` (1 test) - Feature editing operations
- `Features/` (27 tests) - Feature repository and operations
- `Attachments/` (5 tests) - Feature attachments (files, images)
- `Geometry/` (48 tests) - Geometry operations and validation
- `Serialization/` (25 tests) - GeoJSON, GML serialization
- `SoftDelete/` (22 tests) - Soft delete functionality
- `Styling/` (70 tests) - SLD, MapBox styles
- `Concurrency/` (22 tests) - Optimistic concurrency
- `Resilience/` (50 tests) - Retry policies, circuit breakers

**Rationale:** All CRUD and data transformation operations; natural grouping for feature lifecycle

**Dependencies:**
- FlatGeobuf
- Apache.Arrow
- Test data files
- StubFeatureRepository

**Estimated Execution Time:** 3-5 minutes (parallel)

---

### 7. Honua.Server.Core.Tests.Infrastructure (305 tests)
**Purpose:** Cross-cutting infrastructure and operational concerns

**Directories:**
- `Configuration/` (64 tests) - App configuration, options validation
- `Deployment/` (33 tests) - Blue/green deployment
- `BlueGreen/` (15 tests) - Deployment strategies
- `Docker/` (6 tests) - Container support
- `Caching/` (40 tests) - Output caching, cache policies
- `HealthChecks/` (18 tests) - Health check endpoints
- `Observability/` (46 tests) - Metrics, tracing, telemetry
- `Discovery/` (48 tests) - Service discovery
- `DependencyInjection/` (17 tests) - DI container configuration
- `Extensions/` (17 tests) - Extension methods
- `Utilities/` (33 tests) - Utility classes
- `Support/` (2 tests) - Support utilities
- `Pagination/` (16 tests) - Pagination helpers

**Rationale:** Operational and infrastructure concerns; generally faster tests

**Dependencies:**
- Docker.DotNet
- HealthCheck libraries
- Metrics/telemetry libraries
- RedisContainerFixture

**Estimated Execution Time:** 3-4 minutes (parallel)

---

### 8. Honua.Server.Core.Tests.Integration (165 tests)
**Purpose:** Full integration tests and property-based tests

**Directories:**
- `Integration/` (29 tests) - Full end-to-end integration tests
- `Performance/` (6 tests) - Performance benchmarks
- `PropertyTests/` (0 tests, but 6 files) - FsCheck property tests
  - GeoTiffPropertyTests
  - InputValidationPropertyTests
  - PathTraversalPropertyTests
  - SqlInjectionPropertyTests
  - TileCoordinatePropertyTests
  - ZarrChunkPropertyTests

**Additional Tests to Include:**
- Large-scale STAC bulk upsert tests (from Stac/)
- Multi-provider integration tests
- Cross-cutting scenario tests

**Rationale:** Slowest, most comprehensive tests; run separately to avoid blocking fast test feedback

**Dependencies:**
- All testcontainers
- Full application factory
- FsCheck.Xunit
- NetArchTest.Rules (architecture tests)

**Estimated Execution Time:** 5-8 minutes (parallel)

---

## Shared Test Infrastructure

### New Project: Honua.Server.Core.Tests.Shared

**Purpose:** Common test utilities, fixtures, and stubs shared across all test projects

**Contents:**
- `TestInfrastructure/` (all 25 files)
  - HonuaTestWebApplicationFactory.cs
  - SharedPostgresFixture.cs
  - RedisContainerFixture.cs
  - StorageContainerFixture.cs
  - GeometryTestData.cs
  - RealisticGisTestData.cs
  - MetadataSnapshotBuilder.cs
  - MockBuilders.cs
  - TestDatabaseSeeder.cs
  - DockerAvailability.cs
  - EmulatorEndpoints.cs
  - SkipException.cs
- `TestInfrastructure/Stubs/` (all stub implementations)
  - FakeFeatureRepository.cs
  - InMemoryRasterTileCacheProvider.cs
  - NoOpOutputCache.cs
  - NullExporters.cs
  - NullMetrics.cs
  - StaticMetadataRegistry.cs
  - StubAttachmentOrchestrator.cs
  - StubDataStoreProvider.cs
  - StubFeatureRepository.cs
- `Collections/` (xUnit collection definitions)
  - DatabaseTestsCollection.cs
  - EndpointTestsCollection.cs
  - IntegrationTestsCollection.cs
  - RedisTestsCollection.cs
  - UnitTestsCollection.cs
- `Data/` (shared test data files)
  - metadata-ogc-sample.json
  - Rasters/cea.tif

**Package References:**
All packages currently in Core.Tests.csproj that are used by shared infrastructure:
- xunit + xunit.runner.visualstudio
- FluentAssertions
- Moq / NSubstitute
- Microsoft.AspNetCore.Mvc.Testing
- Testcontainers.* (all variants)
- FsCheck.Xunit
- NetArchTest.Rules

**Project Type:** Library (not test project)
- Allows all test projects to reference it
- Contains no tests itself
- Provides reusable test infrastructure

---

## Migration Strategy

### Phase 1: Create Shared Infrastructure (Week 1)

**Steps:**
1. Create `tests/Honua.Server.Core.Tests.Shared` project
2. Move `TestInfrastructure/` directory to Shared project
3. Move `Collections/` to Shared project
4. Update namespaces: `Honua.Server.Core.Tests.Shared.Infrastructure`
5. Create shared `.csproj` with common package references
6. Build and verify no compilation errors

**Validation:**
- Shared project builds successfully
- All fixtures and stubs are accessible
- No circular dependencies

---

### Phase 2: Create Test Projects (Week 2)

**Steps:**
1. Create 8 new test projects with naming convention:
   - `Honua.Server.Core.Tests.Raster`
   - `Honua.Server.Core.Tests.Data`
   - `Honua.Server.Core.Tests.OgcProtocols`
   - `Honua.Server.Core.Tests.Apis`
   - `Honua.Server.Core.Tests.Security`
   - `Honua.Server.Core.Tests.DataOperations`
   - `Honua.Server.Core.Tests.Infrastructure`
   - `Honua.Server.Core.Tests.Integration`

2. Each project should:
   - Reference `Honua.Server.Core.Tests.Shared`
   - Reference `Honua.Server.Core`
   - Reference `Honua.Server.Core.Raster` (if needed)
   - Reference `Honua.Server.Host` (if needed)
   - Include only domain-specific package references
   - Use same TargetFramework (net9.0)
   - Enable ImplicitUsings and Nullable

3. Copy relevant directories to each new project
4. Update namespaces to match new project structure
5. Fix any broken references or imports

**Validation:**
- Each project builds independently
- No duplicate test files
- All original tests are accounted for

---

### Phase 3: Update CI/CD Pipeline (Week 2)

**Steps:**
1. Update `.github/workflows/integration-tests.yml`
2. Add separate test job for each project
3. Configure parallel execution:
   ```yaml
   strategy:
     matrix:
       test-project:
         - Raster
         - Data
         - OgcProtocols
         - Apis
         - Security
         - DataOperations
         - Infrastructure
         - Integration
   ```
4. Run all 8 projects in parallel
5. Aggregate test results and coverage

**Validation:**
- All tests pass in CI
- Total execution time reduced by 60-70%
- Code coverage maintained or improved

---

### Phase 4: Delete Original Project (Week 3)

**Steps:**
1. Verify all tests migrated successfully
2. Run full test suite multiple times to ensure stability
3. Delete `tests/Honua.Server.Core.Tests` directory
4. Update solution file to remove old project
5. Update documentation and README files

**Validation:**
- All 3,046 tests still passing
- No orphaned test files
- CI pipeline green
- Local development workflow updated

---

## Project Structure After Migration

```
tests/
├── Honua.Server.Core.Tests.Shared/          # Shared test infrastructure
│   ├── Infrastructure/
│   │   ├── HonuaTestWebApplicationFactory.cs
│   │   ├── SharedPostgresFixture.cs
│   │   ├── RedisContainerFixture.cs
│   │   └── ...
│   ├── Stubs/
│   │   ├── FakeFeatureRepository.cs
│   │   └── ...
│   ├── Collections/
│   │   └── *.cs
│   └── Data/
│       ├── metadata-ogc-sample.json
│       └── Rasters/cea.tif
│
├── Honua.Server.Core.Tests.Raster/          # 563 tests
│   ├── Cache/
│   ├── Caching/
│   ├── Compression/
│   ├── Kerchunk/
│   ├── Mosaic/
│   ├── Readers/
│   ├── Sources/
│   └── Analytics/
│
├── Honua.Server.Core.Tests.Data/            # 383 tests
│   ├── Postgres/
│   ├── SqlServer/
│   ├── MySql/
│   ├── Sqlite/
│   ├── Query/
│   └── Rasters/
│
├── Honua.Server.Core.Tests.OgcProtocols/    # 475 tests
│   ├── Ogc/
│   ├── Wfs/
│   ├── Wcs/
│   ├── Wmts/
│   ├── Hosting/           # OGC-related endpoints only
│   ├── Csw/
│   ├── Carto/
│   └── Print/
│
├── Honua.Server.Core.Tests.Apis/            # 390 tests
│   ├── Stac/
│   ├── Geoservices/
│   ├── OData/
│   ├── OpenRosa/
│   ├── Api/
│   ├── Catalog/
│   └── Metadata/
│
├── Honua.Server.Core.Tests.Security/        # 435 tests
│   ├── Authentication/
│   ├── Security/
│   ├── Authorization/
│   ├── Auth/
│   ├── Query/             # Security-focused query tests
│   └── Configuration/     # Security config only
│
├── Honua.Server.Core.Tests.DataOperations/  # 330 tests
│   ├── Import/
│   ├── Export/
│   ├── Editing/
│   ├── Features/
│   ├── Attachments/
│   ├── Geometry/
│   ├── Serialization/
│   ├── SoftDelete/
│   ├── Styling/
│   ├── Concurrency/
│   └── Resilience/
│
├── Honua.Server.Core.Tests.Infrastructure/   # 305 tests
│   ├── Configuration/
│   ├── Deployment/
│   ├── BlueGreen/
│   ├── Docker/
│   ├── Caching/
│   ├── HealthChecks/
│   ├── Observability/
│   ├── Discovery/
│   ├── DependencyInjection/
│   ├── Extensions/
│   ├── Utilities/
│   ├── Support/
│   └── Pagination/
│
└── Honua.Server.Core.Tests.Integration/      # 165 tests
    ├── Integration/
    ├── Performance/
    └── PropertyTests/
```

---

## Package Reference Distribution

### Shared Project Packages
All test projects will transitively receive these through Shared project:
- xunit + xunit.runner.visualstudio
- Microsoft.NET.Test.Sdk
- FluentAssertions
- Moq
- NSubstitute
- Microsoft.AspNetCore.Mvc.Testing
- coverlet.collector

### Project-Specific Packages

**Raster:**
- MaxRev.Gdal.Core
- Testcontainers.Minio
- Testcontainers.Azurite
- AWSSDK.S3

**Data:**
- Testcontainers.PostgreSql
- Testcontainers.MySql
- Testcontainers.MsSql
- MySqlConnector
- Microsoft.Data.SqlClient

**Apis:**
- FlatGeobuf
- Apache.Arrow

**Infrastructure:**
- Testcontainers.Redis
- Docker.DotNet

**Integration:**
- FsCheck.Xunit
- NetArchTest.Rules
- All Testcontainers packages (comprehensive testing)

---

## Expected Performance Improvements

### Current Performance
- Single project: 30+ minutes
- Sequential test execution within xUnit
- Limited parallelism due to project size
- Resource contention (database connections, file I/O)

### Expected Performance (Conservative Estimates)

**Parallel Execution (8 projects × 22 cores):**
- Raster: 5-7 minutes
- Data: 6-8 minutes
- OgcProtocols: 4-6 minutes
- Apis: 4-6 minutes
- Security: 4-5 minutes
- DataOperations: 3-5 minutes
- Infrastructure: 3-4 minutes
- Integration: 5-8 minutes

**Wall-clock time:** 10-12 minutes (longest project determines total time)
**Improvement:** 60-70% reduction in test execution time

### Additional Benefits
1. **Faster feedback loops:** Developers can run domain-specific tests (2-5 min) instead of full suite
2. **Better resource utilization:** Each project can optimize test containers and fixtures
3. **Reduced flakiness:** Less resource contention between unrelated tests
4. **Easier troubleshooting:** Failed tests are isolated to specific domains
5. **Scalable CI:** Can distribute projects across multiple CI runners

---

## Risk Mitigation

### Risk 1: Shared Infrastructure Changes
**Risk:** Changes to shared test infrastructure break multiple projects

**Mitigation:**
- Comprehensive versioning of Shared project
- Integration tests that validate shared components
- Clear documentation of shared API contracts
- Gradual rollout with feature branches

### Risk 2: Circular Dependencies
**Risk:** Test projects accidentally reference each other

**Mitigation:**
- Strict project reference rules
- Automated dependency analysis in CI
- Code review checklist for new test files
- Namespace conventions prevent cross-project imports

### Risk 3: Test Data Conflicts
**Risk:** Multiple projects try to use same test containers/databases

**Mitigation:**
- Each project uses isolated test containers
- Unique database names per project
- Port allocation strategies for local development
- Testcontainers' built-in resource management

### Risk 4: Migration Errors
**Risk:** Tests lost or duplicated during migration

**Mitigation:**
- Automated test count validation (3,046 tests before/after)
- File-by-file migration checklist
- Git history preservation
- Parallel development: keep old project until validation complete

### Risk 5: Increased Complexity
**Risk:** Developers don't know which project to use for new tests

**Mitigation:**
- Clear documentation of project boundaries
- IDE templates for new test files
- Naming conventions enforced by analyzers
- README.md in each project explaining scope

---

## Success Criteria

### Phase 1 (Shared Infrastructure)
- [ ] Shared project builds without errors
- [ ] All fixtures and stubs accessible from test projects
- [ ] No circular dependencies detected
- [ ] Documentation updated

### Phase 2 (Test Project Creation)
- [ ] All 8 test projects build independently
- [ ] Total test count = 3,046 (no tests lost)
- [ ] No duplicate test files detected
- [ ] All projects have correct package references
- [ ] Namespaces updated correctly

### Phase 3 (CI/CD Integration)
- [ ] All tests pass in CI pipeline
- [ ] Parallel execution configured (8 projects)
- [ ] Test execution time < 15 minutes
- [ ] Code coverage maintained (>80%)
- [ ] Test result aggregation working

### Phase 4 (Cleanup)
- [ ] Original project deleted
- [ ] Solution file updated
- [ ] Documentation updated
- [ ] Team training completed
- [ ] Developer workflow validated

---

## Open Questions

1. **Should we split Hosting tests differently?**
   - Currently split between OgcProtocols (240 tests) and other domains
   - Alternative: Create separate Hosting project (240 tests)
   - Recommendation: Split by protocol to maintain domain cohesion

2. **Should PropertyTests be separate or in Integration?**
   - Property tests are few but conceptually different
   - Recommendation: Include in Integration for now, split later if they grow

3. **Should we create even smaller projects (12-15 instead of 8)?**
   - Smaller = more parallelism, but more overhead
   - Recommendation: Start with 8, split further if needed

4. **How should we handle test data files?**
   - Some tests need specific data files (CEA.tif, metadata.json)
   - Recommendation: Copy to Shared project, reference with relative paths

5. **Should Shared project be a NuGet package?**
   - Would allow versioning and independent releases
   - Recommendation: Start as project reference, convert to NuGet if needed

---

## Timeline

### Week 1: Shared Infrastructure
- Days 1-2: Create Shared project, move TestInfrastructure
- Days 3-4: Verify builds, update namespaces
- Day 5: Documentation and review

### Week 2: Test Project Creation
- Days 1-3: Create 8 test projects, copy directories
- Days 4-5: Fix references, update namespaces, verify builds

### Week 3: CI/CD and Validation
- Days 1-2: Update CI pipeline for parallel execution
- Days 3-4: Validate test counts, coverage, performance
- Day 5: Team training and documentation

### Week 4: Cleanup and Stabilization
- Days 1-2: Delete original project, final validation
- Days 3-5: Monitor for issues, address any edge cases

**Total Duration:** 4 weeks (with buffer for unexpected issues)

---

## Conclusion

Splitting the monolithic Core.Tests project into 8 focused test projects will:

1. **Reduce test execution time by 60-70%** (30+ min → 10-12 min)
2. **Enable true parallel execution** across 22 CPU cores
3. **Improve developer experience** with faster domain-specific test runs
4. **Reduce flakiness** through better resource isolation
5. **Scale better** as the codebase grows

The migration is low-risk due to:
- Gradual rollout with validation at each phase
- Preservation of all existing tests
- Shared infrastructure library for common components
- Automated validation of test counts and coverage

**Recommendation:** Proceed with migration following the 4-week timeline outlined above.
