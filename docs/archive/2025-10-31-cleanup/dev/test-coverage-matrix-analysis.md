# Test Coverage Matrix Analysis

**Date**: 2025-10-03
**Status**: 🔍 GAP ANALYSIS

## Executive Summary

Analysis of test coverage across the matrix of **Data Providers × API Endpoints × Output Formats** reveals significant gaps in integration testing. While unit tests exist for individual components, **cross-product integration testing is minimal**.

## Test Architecture Pattern

Current tests use **mock-based isolation**:
- OGC handler tests use `OgcTestUtilities.CreateRepository()` (in-memory fake)
- Data provider tests are isolated unit tests
- **No integration tests** combining real data providers with real API endpoints

## Coverage Matrix

### Data Providers (4)
1. **SQLite** - ✅ Unit tests exist
2. **PostgreSQL** - ✅ Unit tests exist
3. **MySQL** - ✅ Unit tests exist
4. **SQL Server** - ✅ Unit tests exist

### API Endpoints (11+)
1. **OGC API Features** - ✅ Tests with mock repository
2. **WFS** - ✅ Tests with mock data
3. **WMS** - ✅ Tests with mock data
4. **Geoservices REST** - ✅ Tests exist
5. **STAC** - ✅ Tests exist
6. **Catalog** - ✅ Tests exist
7. **Records** - ✅ Tests exist
8. **Carto** - ✅ Tests exist
9. **Geometry Server** - ✅ Tests exist
10. **Admin Metadata** - ✅ Tests exist
11. **OpenRosa** - ✅ Tests exist

### Output Formats (10+)
1. **GeoJSON** - ✅ OgcHandlersGeoJsonTests
2. **TopoJSON** - ✅ OgcHandlersTopoJsonTests
3. **KML** - ✅ OgcHandlersKmlTests
4. **KMZ** - ✅ OgcHandlersKmzTests
5. **GeoPackage** - ✅ OgcHandlersGeoPackageTests
6. **Shapefile** - ⚠️ Export tests only
7. **CSV** - ⚠️ Export tests only
8. **JSON** - ✅ Implicit in many tests
9. **HTML** - ❌ No tests found
10. **XML (GML)** - ⚠️ WFS tests only
11. **MVT (Mapbox Vector Tiles)** - ❌ No direct tests

## Gap Analysis

### 🔴 CRITICAL GAPS

#### Gap 1: No Real Data Provider × API Integration Tests
**Current**: OGC handler tests use `FakeFeatureRepository`
**Missing**: Tests that verify SQLite/PostgreSQL/MySQL/SQL Server actually work with OGC APIs

**Impact**: Could have bugs where:
- SQLite works in unit tests but breaks in OGC API
- PostgreSQL-specific SQL generation errors not caught
- MySQL geometry handling differs from test mocks

**Example Missing Test**:
```csharp
[Theory]
[InlineData("sqlite")]
[InlineData("postgres")]
[InlineData("mysql")]
[InlineData("sqlserver")]
public async Task OgcFeatures_WithRealDataProvider_ReturnsGeoJson(string provider)
{
    // Create real database
    // Seed with test data
    // Make actual OGC API request
    // Verify GeoJSON output
}
```

#### Gap 2: No Cross-Format Consistency Tests
**Current**: Each output format tested independently
**Missing**: Tests verifying same data produces consistent results across formats

**Impact**: Could have bugs where:
- Feature in GeoJSON has different coordinates than in KML
- Attribute present in GeoJSON missing in TopoJSON
- Geometry valid in one format, corrupted in another

**Example Missing Test**:
```csharp
[Fact]
public async Task SameFeatureData_ProducesConsistentOutput_AcrossAllFormats()
{
    var testData = CreateReferenceFeatureSet();

    var geoJson = await GetAsGeoJson(testData);
    var kml = await GetAsKml(testData);
    var topojson = await GetAsTopoJson(testData);
    var gpkg = await GetAsGeoPackage(testData);

    // Verify all formats have same feature count
    // Verify coordinates match within tolerance
    // Verify attributes present in all formats
}
```

#### Gap 3: No Data Provider × Geometry Type Matrix
**Current**: Tests use limited geometry types (mostly Point, LineString)
**Missing**: Systematic testing of all geometry types with all providers

**Geometry Types to Test**:
- Point
- LineString
- Polygon
- MultiPoint
- MultiLineString
- MultiPolygon
- GeometryCollection

**Data Providers**: SQLite, PostgreSQL, MySQL, SQL Server

**Matrix**: 7 geometry types × 4 providers = **28 test combinations** (MISSING)

#### Gap 4: No Large Dataset Performance Tests
**Current**: Tests use 2-3 features
**Missing**: Tests with realistic dataset sizes (1000+, 10000+, 100000+ features)

**Impact**: Performance regressions not caught:
- Pagination bugs with large datasets
- Memory leaks with streaming
- Query optimization issues

### 🟡 MEDIUM GAPS

#### Gap 5: Limited CRS Transformation Testing
**Current**: Most tests use EPSG:4326 only
**Missing**: CRS transformation across providers

**CRS to Test**:
- EPSG:4326 (WGS84)
- EPSG:3857 (Web Mercator)
- EPSG:2263 (NAD83 / New York)
- Custom CRS

**Matrix**: 4+ CRS × 4 providers × 5+ output formats = **80+ combinations** (mostly missing)

#### Gap 6: Temporal Query Testing Gaps
**Current**: Basic temporal tests exist
**Missing**: Temporal queries across all providers and APIs

**Temporal Scenarios**:
- Date range queries
- Instant (point in time) queries
- Before/after queries
- Timezone handling
- ISO 8601 parsing edge cases

#### Gap 7: Error Handling Consistency
**Current**: Each component has error tests
**Missing**: End-to-end error propagation tests

**Error Scenarios**:
- Database connection failures
- Invalid geometry in database
- CRS transformation failures
- Format serialization errors
- Query timeout handling

### 🟢 LOW PRIORITY GAPS

#### Gap 8: Concurrency Testing
**Missing**: Tests with concurrent requests to same data provider

#### Gap 9: Caching Behavior Verification
**Missing**: Tests verifying caching works across providers

#### Gap 10: Authentication/Authorization Integration
**Missing**: Tests combining auth with different providers

## Recommended Test Matrix (Prioritized)

### Phase 1: Critical Provider × API Integration (HIGH)

**Test Suite**: `OgcApiIntegrationTests`

```
Provider × API × Format Matrix (Sample):
┌──────────┬─────────┬─────────┬─────────┬─────────┐
│ Provider │ GeoJSON │ KML     │ TopoJSON│ GeoPackage│
├──────────┼─────────┼─────────┼─────────┼─────────┤
│ SQLite   │    ✅   │    ✅   │    ✅   │    ✅   │
│ Postgres │    ❌   │    ❌   │    ❌   │    ❌   │
│ MySQL    │    ❌   │    ❌   │    ❌   │    ❌   │
│ SQLServer│    ❌   │    ❌   │    ❌   │    ❌   │
└──────────┴─────────┴─────────┴─────────┴─────────┘

Current: 4/16 = 25% coverage (only SQLite via mocks)
Target:  16/16 = 100% coverage
```

**Estimated Effort**: 2-3 days
**Priority**: 🔴 CRITICAL

### Phase 2: Geometry Type Coverage (HIGH)

**Test Suite**: `GeometryTypeProviderTests`

```
Geometry × Provider Matrix:
┌───────────────┬────────┬─────────┬───────┬──────────┐
│ Geometry      │ SQLite │ Postgres│ MySQL │ SQLServer│
├───────────────┼────────┼─────────┼───────┼──────────┤
│ Point         │   ✅   │    ❌   │   ❌  │    ❌    │
│ LineString    │   ❌   │    ❌   │   ❌  │    ❌    │
│ Polygon       │   ❌   │    ❌   │   ❌  │    ❌    │
│ MultiPoint    │   ❌   │    ❌   │   ❌  │    ❌    │
│ MultiLineStr  │   ❌   │    ❌   │   ❌  │    ❌    │
│ MultiPolygon  │   ❌   │    ❌   │   ❌  │    ❌    │
│ GeomCollection│   ❌   │    ❌   │   ❌  │    ❌    │
└───────────────┴────────┴─────────┴───────┴──────────┘

Current: 1/28 = 3.6% coverage
Target:  28/28 = 100% coverage
```

**Estimated Effort**: 1-2 days
**Priority**: 🔴 CRITICAL

### Phase 3: CRS Transformation (MEDIUM)

**Test Suite**: `CrsTransformationIntegrationTests`

```
CRS × Provider × Format (Critical Combinations):
- EPSG:4326 → EPSG:3857 (SQLite, Postgres, MySQL, SQLServer) × GeoJSON
- EPSG:3857 → EPSG:4326 (SQLite, Postgres, MySQL, SQLServer) × GeoJSON
- Custom CRS support verification

Current: ~10% coverage (basic transform tests only)
Target:  80% coverage (common CRS pairs)
```

**Estimated Effort**: 1 day
**Priority**: 🟡 MEDIUM

### Phase 4: Performance & Scale (MEDIUM)

**Test Suite**: `PerformanceIntegrationTests`

```
Dataset Size × Provider:
- 1,000 features (SQLite, Postgres, MySQL, SQLServer)
- 10,000 features (SQLite, Postgres, MySQL, SQLServer)
- 100,000 features (Postgres, MySQL, SQLServer)

Operations:
- Query all
- Bbox query
- Attribute filter
- Pagination
- Streaming

Current: 0% (only small datasets tested)
Target:  50% (representative scenarios)
```

**Estimated Effort**: 2-3 days
**Priority**: 🟡 MEDIUM

## Test Infrastructure Needs

### 1. Multi-Provider Test Fixture

```csharp
public class MultiProviderTestFixture : IAsyncLifetime
{
    public Dictionary<string, TestDatabase> Providers { get; } = new();

    public async Task InitializeAsync()
    {
        // Spin up SQLite (local)
        Providers["sqlite"] = await CreateSqliteTestDb();

        // Spin up Postgres (Testcontainers)
        Providers["postgres"] = await CreatePostgresTestDb();

        // Spin up MySQL (Testcontainers)
        Providers["mysql"] = await CreateMySqlTestDb();

        // Spin up SQL Server (Testcontainers)
        Providers["sqlserver"] = await CreateSqlServerTestDb();

        // Seed all with identical test data
        await SeedAllProviders();
    }
}
```

### 2. Format Comparison Utilities

```csharp
public static class FormatComparer
{
    public static void AssertFeaturesEquivalent(
        GeoJsonFeature geoJson,
        KmlFeature kml,
        TopoJsonFeature topoJson)
    {
        // Compare coordinates
        // Compare attributes
        // Compare geometry types
    }
}
```

### 3. Test Data Generators

```csharp
public static class GeometryTestDataGenerator
{
    public static IEnumerable<Geometry> AllGeometryTypes()
    {
        yield return CreatePoint();
        yield return CreateLineString();
        yield return CreatePolygon();
        yield return CreateMultiPoint();
        yield return CreateMultiLineString();
        yield return CreateMultiPolygon();
        yield return CreateGeometryCollection();
    }
}
```

## Current Test Architecture Issues

### Issue 1: Over-Reliance on Mocks
**Problem**: Most API tests use `FakeFeatureRepository`
**Risk**: Real provider bugs not caught until production

### Issue 2: No Shared Test Data
**Problem**: Each test creates its own test data
**Risk**: Inconsistent test scenarios, hard to compare behavior

### Issue 3: Limited Geometry Coverage
**Problem**: Mostly Point and LineString geometries tested
**Risk**: Polygon, MultiGeometry bugs not caught

### Issue 4: No Cross-Cutting Validation
**Problem**: Tests validate one dimension at a time
**Risk**: Integration bugs between providers/APIs/formats

## Recommended Test Strategy

### Strategy 1: Smoke Tests (Quick Wins)
Create one integration test per provider that exercises full stack:
```csharp
[Theory]
[InlineData("sqlite")]
[InlineData("postgres")]
[InlineData("mysql")]
[InlineData("sqlserver")]
public async Task FullStack_SmokeTest(string provider)
{
    // Seed database with all geometry types
    // Query via OGC API
    // Export to all formats
    // Verify basic correctness
}
```

**Effort**: 1 day
**Coverage Gain**: Catches ~80% of integration bugs

### Strategy 2: Combinatorial Testing (Comprehensive)
Use `[Theory]` with `[ClassData]` to test all combinations:
```csharp
[Theory]
[ClassData(typeof(ProviderFormatCombinations))]
public async Task Provider_OutputsValidFormat(
    string provider,
    string format,
    GeometryType geometryType)
{
    // Test specific combination
}
```

**Effort**: 3-5 days
**Coverage Gain**: Catches ~95% of integration bugs

### Strategy 3: Property-Based Testing
Use FsCheck or similar for random test case generation:
```csharp
[Property]
public Property GeoJson_KML_Roundtrip_Preserves_Coordinates()
{
    return Prop.ForAll(
        GeometryGenerators.AnyValidGeometry,
        geom =>
        {
            var geoJson = SerializeAsGeoJson(geom);
            var kml = SerializeAsKml(geom);
            return CoordinatesMatch(geoJson, kml);
        });
}
```

**Effort**: 2-3 days
**Coverage Gain**: Catches edge cases and corner cases

## Immediate Recommendations

### 1. Create Smoke Test Suite (Week 1)
- ✅ Priority: CRITICAL
- ✅ Effort: 1-2 days
- ✅ Coverage: Basic integration validation

```csharp
// tests/Honua.Server.Core.Tests/Integration/SmokeTests.cs
public class IntegrationSmokeTests
{
    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    public async Task Provider_ReturnsValidGeoJson(string provider)
    {
        // Minimal end-to-end test
    }
}
```

### 2. Add Geometry Type Matrix Tests (Week 1-2)
- ✅ Priority: HIGH
- ✅ Effort: 1-2 days
- ✅ Coverage: All geometry types × SQLite (then expand)

### 3. Document Test Gaps (Week 1)
- ✅ Priority: HIGH
- ✅ Effort: 1 day (DONE - this document)
- ✅ Coverage: Awareness and planning

### 4. Create Multi-Provider Test Fixture (Week 2)
- ✅ Priority: MEDIUM
- ✅ Effort: 1 day
- ✅ Coverage: Infrastructure for future tests

## Success Metrics

### Before Implementation
- **Provider × API × Format Coverage**: ~25% (SQLite only, via mocks)
- **Geometry Type Coverage**: ~3.6% (1/28 combinations)
- **CRS Coverage**: ~10% (basic tests only)
- **Integration Test Count**: 0 (all tests use mocks)

### After Phase 1 (Smoke Tests)
- **Provider × API × Format Coverage**: ~50%
- **Geometry Type Coverage**: ~10%
- **Integration Test Count**: ~20 tests
- **Risk Reduction**: ~80%

### After Phase 2 (Full Matrix)
- **Provider × API × Format Coverage**: ~90%
- **Geometry Type Coverage**: ~100%
- **CRS Coverage**: ~80%
- **Integration Test Count**: ~100+ tests
- **Risk Reduction**: ~95%

## Conclusion

**Current State**: Strong unit test coverage, minimal integration testing
**Gap Severity**: 🔴 CRITICAL - Real provider × API interactions untested
**Recommended Action**: Implement Phase 1 smoke tests immediately

The test architecture follows good isolation principles but **lacks integration validation**. This is a common pattern but creates **significant risk** for a multi-database, multi-API, multi-format system like Honua.

**Key Insight**: The geometry type declaration issues we fixed are a **symptom** of insufficient cross-cutting integration tests. If we had tests that actually used real providers with real APIs, those mismatches would have been caught immediately.
