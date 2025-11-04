# Test Suite Refactoring Plan

**Goal**: Make the Honua test suite shine with DRY principles, clear organization, and optimal infrastructure

## Executive Summary

### Current State
- **639 test files**: 613 C#, 26 Python, 0 Node.js
- **5,970 test methods**: 5,509 Fact + 461 Theory tests
- **227,560 lines** of test code
- **21 C# test projects** organized by domain
- **Well-organized** but has room for consolidation and DRY improvements

### Key Issues Identified
1. **Large test files** (14 files >900 lines) need splitting
2. **Repetitive patterns** across protocol tests (WFS, WMS, WMTS, STAC)
3. **C# vs Python overlap** - some protocols tested in both languages
4. **No shared Docker test infrastructure** - each test project has its own Docker setup
5. **Repetitive fixture code** across similar test scenarios

### Strategy
Rather than removing tests, we'll:
1. **Consolidate & DRY** - Extract common patterns into base classes
2. **Clarify roles** - C# for unit/integration, Python for protocol compliance
3. **Shared infrastructure** - Single Docker Compose setup with cached SQLite backend
4. **Split large files** - Better organization and maintainability

---

## Phase 1: Shared Test Infrastructure (HIGH PRIORITY)

### Create Shared Docker Test Environment

**Goal**: Single docker-compose setup that all tests can use

**File**: `tests/docker-compose.shared-test-env.yml`

```yaml
services:
  # Honua Server with SQLite backend (fast, cached)
  honua-test:
    image: honua-server:test
    build:
      context: ..
      dockerfile: src/Honua.Server.Host/Dockerfile
    environment:
      - ConnectionStrings__HonuaDb=Data Source=/app/data/honua-test.db
      - Metadata__Provider=File
      - Metadata__FilePath=/app/data/test-metadata.json
      - Authentication__Provider=SQLite
      - Authentication__SQLite__DatabasePath=/app/data/auth-test.db
      - ASPNETCORE_ENVIRONMENT=Development
    volumes:
      - ./TestData:/app/data:ro
      - ./shared-cache:/app/cache
    ports:
      - "5000:8080"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 5s
      timeout: 3s
      retries: 10

  # PostgreSQL for integration tests requiring real DB
  postgres-test:
    image: postgis/postgis:16-3.4
    environment:
      - POSTGRES_DB=honua_test
      - POSTGRES_USER=postgres
      - POSTGRES_PASSWORD=test
    volumes:
      - ./TestData/seed-data:/docker-entrypoint-initdb.d/data:ro
    ports:
      - "5433:5432"
```

**Benefits**:
- **Cached instance**: Start once, reuse for all test runs
- **Consistent data**: All tests use same pre-populated dataset
- **Fast startup**: SQLite backend = <1s startup vs 10-30s for PostgreSQL
- **Language agnostic**: C#, Python, Node can all use the same instance

---

## Phase 2: C# Test Consolidation (DRY Improvements)

### 2.1 Create Base Test Classes

**New file**: `tests/Honua.Server.Core.Tests.Shared/TestBases/ProtocolTestBase.cs`

Extract common patterns from protocol tests:
- OGC service client setup
- Standard compliance assertions
- Feature/layer verification
- Error handling patterns

```csharp
public abstract class OgcProtocolTestBase<TClient> : IClassFixture<HonuaTestWebApplicationFactory>
{
    protected readonly HonuaTestWebApplicationFactory Factory;
    protected readonly HttpClient HttpClient;

    protected abstract string ServiceEndpoint { get; }
    protected abstract TClient CreateClient(string baseUrl);

    // Common test patterns
    protected async Task<TClient> GetClientAsync() { /* ... */ }
    protected async Task AssertServiceIdentification(/* ... */) { /* ... */ }
    protected async Task AssertOperationsSupported(List<string> operations) { /* ... */ }
    // ... more common patterns
}
```

**Usage**: WMS, WFS, WMTS, WCS tests extend this base class (reduces ~400 lines of duplication)

### 2.2 Split Large Test Files

Break down files >900 lines using nested classes or separate files:

**Current**: `ProcessFrameworkEmulatorE2ETests.cs` (2,037 lines)
**Refactor to**:
- `ProcessFrameworkEmulatorE2ETests.Deployment.cs` (AWS, Azure, GCP scenarios)
- `ProcessFrameworkEmulatorE2ETests.StateMachine.cs` (state transitions)
- `ProcessFrameworkEmulatorE2ETests.ErrorHandling.cs` (failure scenarios)
- `ProcessFrameworkEmulatorE2ETests.Storage.cs` (S3, Blob, GCS tests)

**Current**: `StacEdgeCaseTests.cs` (998 lines)
**Refactor to**:
- `StacEdgeCaseTests.EmptyCatalog.cs`
- `StacEdgeCaseTests.BoundaryConditions.cs`
- `StacEdgeCaseTests.SpecialCharacters.cs`
- `StacEdgeCaseTests.DatelineHandling.cs`

### 2.3 Create Test Data Builders

**New file**: `tests/Honua.Server.Core.Tests.Shared/Builders/`

Extract repetitive test object creation:
```csharp
public class StacItemBuilder
{
    public StacItemBuilder WithId(string id) { /* ... */ }
    public StacItemBuilder WithGeometry(/* ... */) { /* ... */ }
    public StacItemBuilder CrossingDateline() { /* ... */ }
    public StacItemRecord Build() { /* ... */ }
}
```

**Benefit**: Replaces ~200 lines of repetitive test setup across STAC tests

### 2.4 Consolidate Database Tests

**Current**: Separate test classes for PostgreSQL, MySQL, SQLite
**Pattern**: Each database store has near-identical tests

**Solution**: Create parametrized test base
```csharp
public abstract class DatabaseStoreTestBase<TStore>
{
    [Theory]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    [InlineData("sqlite")]
    public async Task TestInsertFeature(string provider) { /* ... */ }
}
```

**Benefit**: Reduces ~300 lines of database test duplication

---

## Phase 3: Python Test Consolidation

### 3.1 Enhanced conftest.py

Add shared fixtures for all protocol tests:
```python
@pytest.fixture(scope="session")
def honua_server():
    """Start or connect to shared Honua test server"""
    # Check if server is already running (cached instance)
    # If not, start docker-compose.shared-test-env.yml
    yield base_url
    # Keep running for next test session

@pytest.fixture
def protocol_client(honua_server, request):
    """Factory for creating protocol clients (WFS, WMS, WMTS, etc.)"""
    protocol = request.param
    return create_client(protocol, honua_server)

# Shared assertions
def assert_ogc_capabilities(client, expected_operations):
    """Common capability assertion for all OGC services"""
    pass

def assert_feature_collection_valid(features, min_count=0):
    """Common GeoJSON validation"""
    pass
```

### 3.2 DRY Protocol Test Patterns

**Current**: Each protocol test file (WFS, WMS, WMTS) has duplicate patterns:
- GetCapabilities validation
- Service metadata checks
- Operation support verification
- Error handling

**Solution**: Extract to shared utilities in `tests/python/shared_assertions.py`

**Benefit**: Reduces Python test code by ~200 lines

### 3.3 Clarify Test Roles

**C# Tests** (keep):
- Unit tests for internal logic
- Integration tests for database operations
- Performance tests
- Security tests
- E2E tests for deployment workflows

**Python Tests** (keep & enhance):
- Protocol compliance using reference clients (OWSLib, pystac)
- Interoperability testing
- Client library compatibility

**Remove** (duplicates):
- C# tests that duplicate Python protocol compliance tests
- Files to review:
  - `Honua.Server.Host.Tests/Wms/WmsGetCapabilitiesTests.cs` (if duplicates Python)
  - `Honua.Server.Host.Tests/Wfs/WfsComplianceTests.cs` (if duplicates Python)

---

## Phase 4: Create Node.js Test Suite (Optional Future)

For future implementation when needed:
- `tests/nodejs/package.json` with STAC, OGC clients
- Use shared Docker test infrastructure
- Focus on: leaflet, OpenLayers, maplibre client integration

---

## Phase 5: Test Data & Fixtures

### 5.1 Pre-populated SQLite Databases

Create cached, read-only test databases:
- `tests/TestData/databases/honua-test-full.db` (10K+ features, all protocols)
- `tests/TestData/databases/honua-test-mini.db` (100 features, smoke tests)
- `tests/TestData/databases/honua-test-empty.db` (schema only, negative tests)

**Benefits**:
- Instant test startup (no seeding needed)
- Consistent test data across all languages
- Can be committed to git (SQLite is compact)

### 5.2 Fixture Consolidation

**Current**: 67 test fixtures across projects
**Target**: ~30 fixtures with clearer responsibilities

Consolidate similar fixtures:
- Multiple PostgreSQL fixtures → single `PostgresTestFixture`
- Multiple in-memory fixtures → single `InMemoryTestFixture`
- Multiple auth fixtures → single `TestAuthenticationFixture`

---

## Phase 6: Documentation & Guidelines

### 6.1 Test Organization Guide

**Update**: `tests/TEST_ORGANIZATION.md`
- When to write C# vs Python vs Node tests
- How to use shared Docker infrastructure
- Guidelines for test size (unit vs integration vs e2e)

### 6.2 Running Tests Guide

**Update**: `tests/QUICK_START_TESTING_GUIDE.md`
- Add section for shared Docker environment
- Commands for C# + Python + Node together
- CI/CD pipeline examples

---

## Metrics & Success Criteria

### Before Refactoring
- 227,560 lines of test code
- 67 test fixtures
- 98 docker-compose files
- ~15-30s average test startup time
- Some duplicate protocol tests

### After Refactoring (Target)
- **~180,000 lines** (20% reduction through DRY)
- **~30 test fixtures** (consolidation)
- **1 shared docker-compose** + specialized ones only when needed
- **<5s average test startup** (cached Docker instance)
- **Zero duplicate protocol tests** (clear C#/Python separation)
- **100% test pass rate** (no broken tests)

---

## Implementation Order

1. ✅ **Analysis complete** (this document)
2. 🚀 **Phase 1**: Shared Docker infrastructure (HIGHEST IMPACT)
3. 🔧 **Phase 2.1**: Create C# base classes
4. 🔧 **Phase 2.2**: Split large test files
5. 🔧 **Phase 2.3**: Test data builders
6. 🐍 **Phase 3**: Python consolidation
7. 🗑️ **Remove duplicates** (after consolidation complete)
8. 📚 **Update documentation**
9. ✅ **Validate** all tests pass
10. 🚀 **Commit & Push**

---

## Risk Assessment

### Risks
- Breaking existing tests during refactoring
- CI/CD pipeline disruption
- Team members using old patterns

### Mitigation
- Refactor incrementally, run tests after each change
- Keep old patterns working during transition
- Update CI/CD pipeline in parallel
- Add test organization guide for team

---

## Files to Create

- `tests/docker-compose.shared-test-env.yml`
- `tests/TestData/databases/honua-test-full.db`
- `tests/TestData/databases/honua-test-mini.db`
- `tests/Honua.Server.Core.Tests.Shared/TestBases/ProtocolTestBase.cs`
- `tests/Honua.Server.Core.Tests.Shared/TestBases/DatabaseStoreTestBase.cs`
- `tests/Honua.Server.Core.Tests.Shared/Builders/StacItemBuilder.cs`
- `tests/Honua.Server.Core.Tests.Shared/Builders/FeatureBuilder.cs`
- `tests/python/shared_assertions.py`
- `tests/python/shared_fixtures.py`
- `tests/TEST_ORGANIZATION.md`

## Files to Refactor

- Split: `tests/Honua.Cli.AI.E2ETests/ProcessFrameworkEmulatorE2ETests.cs`
- Split: `tests/Honua.Server.Core.Tests.Apis/Stac/StacEdgeCaseTests.cs`
- Enhance: `tests/python/conftest.py`
- Update: `tests/QUICK_START_TESTING_GUIDE.md`

## Files to Review for Deletion

- Duplicate protocol tests in C# (after Python tests verified)
- Unused docker-compose files in `tests/e2e-assistant/results/` (appear to be test artifacts)

---

**Last Updated**: 2025-11-04
**Status**: Plan Created - Ready for Implementation
