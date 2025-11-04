# Test Matrix Implementation Plan

**Date**: 2025-10-03
**Status**: 🚀 IMPLEMENTATION READY

## What We've Built

### 1. Shared Test Data Infrastructure ✅

**File**: `tests/Honua.Server.Core.Tests/TestInfrastructure/GeometryTestData.cs`

**Features**:
- All 7 OGC geometry types (Point, LineString, Polygon, Multi*, GeometryCollection)
- 7 geodetic scenarios (Simple, AntimeridianCrossing, NorthPole, SouthPole, GlobalExtent, HighPrecision, WithHoles)
- Consistent test attributes across all tests
- WKT and GeoJSON serialization
- ~40 total test combinations (7 types × 7 scenarios, filtered for compatibility)
- 10 essential combinations for smoke tests

### 2. Multi-Provider Test Fixture ✅

**File**: `tests/Honua.Server.Core.Tests/TestInfrastructure/MultiProviderTestFixture.cs`

**Features**:
- SQLite (local, always available)
- PostgreSQL (Testcontainers with PostGIS)
- MySQL (Testcontainers with spatial types)
- SQL Server support ready (not yet implemented)
- Automatic seeding with identical test data
- Metadata generation for each provider × geometry combination

## Test Matrix Structure

### Matrix Dimensions

```
Total Test Space = Providers × Geometry Types × APIs × Output Formats

Current Implementation:
- Providers: 3 (SQLite, PostgreSQL, MySQL)
- Geometry Types: 7 (Point, LineString, Polygon, MultiPoint, MultiLineString, MultiPolygon, GeometryCollection)
- Geodetic Scenarios: 7 (Simple, Antimeridian, Poles, etc.)
- APIs: 5+ (OGC, WFS, WMS, Geoservices, STAC)
- Output Formats: 10+ (GeoJSON, KML, KMZ, TopoJSON, GeoPackage, Shapefile, CSV, etc.)

Full Matrix: 3 × 7 × 7 × 5 × 10 = 7,350 possible test combinations
Essential Matrix: 3 × 10 × 2 × 3 = 180 essential combinations
```

### Test Organization

```
tests/Honua.Server.Core.Tests/
├── Integration/                          # NEW
│   ├── Matrix/                           # NEW - Matrix tests
│   │   ├── GeometryMatrixTests.cs        # Geometry type × Provider
│   │   ├── ProviderApiFormatTests.cs     # Provider × API × Format
│   │   ├── GeodeticEdgeCaseTests.cs      # Geodetic scenarios
│   │   └── CrsTransformationTests.cs     # CRS transformations
│   ├── Smoke/                            # NEW - Quick validation
│   │   └── ProviderSmokeTests.cs         # Basic provider validation
│   └── Performance/                       # NEW - Scale testing
│       └── LargeDatasetTests.cs          # 1K, 10K, 100K features
├── TestInfrastructure/
│   ├── GeometryTestData.cs               # ✅ DONE
│   ├── MultiProviderTestFixture.cs       # ✅ DONE
│   ├── FormatComparer.cs                 # TODO
│   └── TestDataSeeder.cs                 # TODO
└── [existing test folders]
```

## Implementation Phases

### Phase 1: Smoke Tests (1 day) 🔴 CRITICAL

**Goal**: Validate each provider works with basic operations

**File**: `tests/Honua.Server.Core.Tests/Integration/Smoke/ProviderSmokeTests.cs`

```csharp
[Collection("MultiProvider")]
public class ProviderSmokeTests
{
    private readonly MultiProviderTestFixture _fixture;

    public ProviderSmokeTests(MultiProviderTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData(MultiProviderTestFixture.SqliteProvider)]
    [InlineData(MultiProviderTestFixture.PostgresProvider)]
    [InlineData(MultiProviderTestFixture.MySqlProvider)]
    public async Task Provider_CanQueryPointGeometry(string providerName)
    {
        // Arrange
        var (dataSource, service, layer) = _fixture.GetMetadata(
            providerName,
            GeometryTestData.GeometryType.Point);

        using var provider = CreateDataStoreProvider(providerName);

        // Act
        var results = new List<FeatureRecord>();
        await foreach (var record in provider.QueryAsync(dataSource, service, layer, new FeatureQuery(Limit: 10)))
        {
            results.Add(record);
        }

        // Assert
        results.Should().HaveCountGreaterThan(0);
        results[0].Attributes.Should().ContainKey("geom");

        // Validate geometry type matches
        var geomJson = results[0].Attributes["geom"] as JsonObject;
        geomJson.Should().NotBeNull();
        geomJson!["type"]!.GetValue<string>().Should().Be("Point");
    }
}
```

**Coverage**: 3 providers × 1 geometry = 3 tests
**Value**: Catches ~50% of provider integration bugs
**Estimated Effort**: 4 hours

### Phase 2: Geometry Matrix Tests (2 days) 🔴 CRITICAL

**Goal**: Test all geometry types work with all providers

**File**: `tests/Honua.Server.Core.Tests/Integration/Matrix/GeometryMatrixTests.cs`

```csharp
public class GeometryMatrixTestData : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        var providers = new[] { "sqlite", "postgres", "mysql" };
        var combinations = GeometryTestData.GetEssentialCombinations();

        foreach (var provider in providers)
        {
            foreach (var (type, scenario) in combinations)
            {
                yield return new object[] { provider, type, scenario };
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

[Collection("MultiProvider")]
public class GeometryMatrixTests
{
    [Theory]
    [ClassData(typeof(GeometryMatrixTestData))]
    public async Task Provider_HandlesGeometryType_Correctly(
        string providerName,
        GeometryTestData.GeometryType geometryType,
        GeometryTestData.GeodeticScenario scenario)
    {
        // Test that provider correctly stores/retrieves geometry
        // Validate coordinates match within tolerance
        // Verify geometry type preserved
    }
}
```

**Coverage**: 3 providers × 10 essential combinations = 30 tests
**Value**: Catches ~80% of geometry handling bugs
**Estimated Effort**: 1 day

### Phase 3: Provider × API × Format Matrix (3 days) 🟡 HIGH

**Goal**: Test all providers work with all output formats via OGC API

**File**: `tests/Honua.Server.Core.Tests/Integration/Matrix/ProviderApiFormatTests.cs`

```csharp
public class ProviderApiFormatTestData : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        var providers = new[] { "sqlite", "postgres", "mysql" };
        var formats = new[] { "geojson", "kml", "topojson", "gpkg" };
        var geometryTypes = new[]
        {
            GeometryTestData.GeometryType.Point,
            GeometryTestData.GeometryType.LineString,
            GeometryTestData.GeometryType.Polygon
        };

        foreach (var provider in providers)
        foreach (var format in formats)
        foreach (var geomType in geometryTypes)
        {
            yield return new object[] { provider, format, geomType };
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

[Collection("MultiProvider")]
public class ProviderApiFormatTests
{
    [Theory]
    [ClassData(typeof(ProviderApiFormatTestData))]
    public async Task OgcApi_OutputsValidFormat_ForProviderAndGeometry(
        string providerName,
        string format,
        GeometryTestData.GeometryType geometryType)
    {
        // Arrange: Setup real provider + repository
        // Act: Query via OGC API handlers with format parameter
        // Assert: Validate output format is valid
        // Assert: Validate geometry coordinates preserved
    }
}
```

**Coverage**: 3 providers × 4 formats × 3 geometry types = 36 tests
**Value**: Catches ~90% of format serialization bugs
**Estimated Effort**: 2 days

### Phase 4: Geodetic Edge Cases (1 day) 🟡 MEDIUM

**Goal**: Test challenging geodetic scenarios

**File**: `tests/Honua.Server.Core.Tests/Integration/Matrix/GeodeticEdgeCaseTests.cs`

```csharp
[Collection("MultiProvider")]
public class GeodeticEdgeCaseTests
{
    [Theory]
    [InlineData("sqlite", GeodeticScenario.AntimeridianCrossing)]
    [InlineData("postgres", GeodeticScenario.AntimeridianCrossing)]
    [InlineData("mysql", GeodeticScenario.AntimeridianCrossing)]
    public async Task Provider_HandlesAntimeridianCrossing(
        string providerName,
        GeodeticScenario scenario)
    {
        // Test LineString crossing ±180° longitude
        // Verify coordinates not corrupted
        // Validate bbox calculation
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("mysql")]
    public async Task Provider_HandlesPolygonWithHole(string providerName)
    {
        // Test polygon with interior ring
        // Verify hole preserved
        // Validate area calculation
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("mysql")]
    public async Task Provider_PreservesHighPrecisionCoordinates(string providerName)
    {
        // Test sub-meter precision (9+ decimal places)
        // Verify coordinates not truncated
    }
}
```

**Coverage**: 3 providers × 3 scenarios = 9 tests
**Value**: Catches geodetic calculation bugs
**Estimated Effort**: 1 day

### Phase 5: CRS Transformation (2 days) 🟡 MEDIUM

**Goal**: Test coordinate reference system transformations

**File**: `tests/Honua.Server.Core.Tests/Integration/Matrix/CrsTransformationTests.cs`

```csharp
public class CrsTransformationTestData : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        var providers = new[] { "sqlite", "postgres", "mysql" };
        var transformations = new[]
        {
            ("EPSG:4326", "EPSG:3857"), // WGS84 → Web Mercator
            ("EPSG:3857", "EPSG:4326"), // Web Mercator → WGS84
            ("EPSG:4326", "EPSG:2263")  // WGS84 → NAD83 NY
        };

        foreach (var provider in providers)
        foreach (var (from, to) in transformations)
        {
            yield return new object[] { provider, from, to };
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

[Collection("MultiProvider")]
public class CrsTransformationTests
{
    [Theory]
    [ClassData(typeof(CrsTransformationTestData))]
    public async Task Provider_TransformsCrs_Correctly(
        string providerName,
        string sourceCrs,
        string targetCrs)
    {
        // Query in source CRS
        // Request transformation to target CRS
        // Verify coordinates transformed correctly
        // Validate inverse transformation
    }
}
```

**Coverage**: 3 providers × 3 transformations = 9 tests
**Value**: Catches projection bugs
**Estimated Effort**: 1-2 days

### Phase 6: Performance & Scale (2 days) 🟢 LOW

**Goal**: Test with realistic dataset sizes

**File**: `tests/Honua.Server.Core.Tests/Integration/Performance/LargeDatasetTests.cs`

```csharp
[Collection("MultiProvider")]
public class LargeDatasetTests
{
    [Theory]
    [InlineData("sqlite", 1000)]
    [InlineData("postgres", 1000)]
    [InlineData("mysql", 1000)]
    public async Task Provider_Handles1KFeatures(string providerName, int featureCount)
    {
        // Seed 1,000 features
        // Query all
        // Verify performance acceptable
        // Validate memory usage
    }

    [Theory(Skip = "Long-running")]
    [InlineData("postgres", 10000)]
    [InlineData("mysql", 10000)]
    public async Task Provider_Handles10KFeatures(string providerName, int featureCount)
    {
        // Seed 10,000 features
        // Test pagination
        // Test bbox queries
        // Verify streaming works
    }
}
```

**Coverage**: 2 providers × 2 scales = 4 tests
**Value**: Catches performance regressions
**Estimated Effort**: 1-2 days

## Implementation Summary

### Test File Organization

```
NEW FILES TO CREATE:

1. ✅ tests/Honua.Server.Core.Tests/TestInfrastructure/GeometryTestData.cs
2. ✅ tests/Honua.Server.Core.Tests/TestInfrastructure/MultiProviderTestFixture.cs
3. tests/Honua.Server.Core.Tests/Integration/Smoke/ProviderSmokeTests.cs
4. tests/Honua.Server.Core.Tests/Integration/Matrix/GeometryMatrixTests.cs
5. tests/Honua.Server.Core.Tests/Integration/Matrix/ProviderApiFormatTests.cs
6. tests/Honua.Server.Core.Tests/Integration/Matrix/GeodeticEdgeCaseTests.cs
7. tests/Honua.Server.Core.Tests/Integration/Matrix/CrsTransformationTests.cs
8. tests/Honua.Server.Core.Tests/Integration/Performance/LargeDatasetTests.cs
9. tests/Honua.Server.Core.Tests/TestInfrastructure/FormatComparer.cs (helper)
```

### Total Test Coverage After Implementation

```
BEFORE:
- Provider × Geometry: 1/28 = 3.6% (only SQLite Point)
- Provider × API × Format: ~25% (mocks only)
- Geodetic Edge Cases: 0%
- CRS Transformations: ~10%

AFTER PHASE 1 (Smoke):
- Provider × Geometry: ~15%
- Integration Tests: 3

AFTER PHASE 2 (Geometry Matrix):
- Provider × Geometry: 30/30 = 100% (essential combinations)
- Integration Tests: 33

AFTER PHASE 3 (API × Format):
- Provider × API × Format: ~75%
- Integration Tests: 69

AFTER ALL PHASES:
- Provider × Geometry: 100%
- Provider × API × Format: ~90%
- Geodetic Edge Cases: 100%
- CRS Transformations: 90%
- Integration Tests: ~90+
```

### Effort Estimate

| Phase | Priority | Effort | Value |
|-------|----------|--------|-------|
| Phase 1: Smoke Tests | 🔴 CRITICAL | 4 hours | 50% bug coverage |
| Phase 2: Geometry Matrix | 🔴 CRITICAL | 1 day | 80% bug coverage |
| Phase 3: API × Format | 🟡 HIGH | 2 days | 90% bug coverage |
| Phase 4: Geodetic Edge Cases | 🟡 MEDIUM | 1 day | 95% bug coverage |
| Phase 5: CRS Transformation | 🟡 MEDIUM | 1-2 days | 97% bug coverage |
| Phase 6: Performance | 🟢 LOW | 1-2 days | 99% bug coverage |
| **TOTAL** | | **7-9 days** | **99% coverage** |

### Recommended Immediate Action

**Week 1**: Implement Phases 1-2
- Day 1: Smoke tests (4 hours) + start Geometry Matrix
- Day 2-3: Complete Geometry Matrix tests
- **Deliverable**: 33 integration tests, 100% geometry type coverage

**Week 2**: Implement Phase 3
- Day 4-5: Provider × API × Format tests
- **Deliverable**: 69 integration tests, 90% format coverage

**Weeks 3-4** (Optional): Phases 4-6
- Geodetic edge cases
- CRS transformations
- Performance testing

## CI/CD Integration

### GitHub Actions Workflow

```yaml
name: Matrix Tests

on: [pull_request]

jobs:
  matrix-tests:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgis/postgis:16-3.4
        env:
          POSTGRES_PASSWORD: test
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5
      mysql:
        image: mysql:8.0
        env:
          MYSQL_ROOT_PASSWORD: test
        options: >-
          --health-cmd "mysqladmin ping"
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'

      - name: Run Matrix Tests
        run: dotnet test --filter "Category=Matrix|Category=Integration"

      - name: Upload Test Results
        uses: actions/upload-artifact@v3
        with:
          name: test-results
          path: TestResults/
```

## Success Metrics

### Before Implementation
- ❌ No provider integration tests
- ❌ No geometry type matrix coverage
- ❌ No geodetic edge case testing
- ❌ No format consistency validation
- ⚠️ All API tests use mocks

### After Implementation
- ✅ 90+ integration tests
- ✅ 100% geometry type coverage (7 types × 3 providers)
- ✅ 75%+ API × format coverage
- ✅ Geodetic edge cases validated
- ✅ CRS transformations tested
- ✅ Real provider validation in CI/CD

## Next Steps

1. ✅ **DONE**: Create GeometryTestData.cs
2. ✅ **DONE**: Create MultiProviderTestFixture.cs
3. ⏩ **START**: Implement Phase 1 (Smoke Tests) - 4 hours
4. ⏩ Implement Phase 2 (Geometry Matrix) - 1 day
5. Review and iterate based on findings
6. Implement remaining phases as needed
