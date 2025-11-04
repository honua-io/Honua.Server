# Test Data Improvements Summary

## Overview

This document summarizes comprehensive test data improvements made to use realistic, production-representative scenarios instead of simplistic test values. All improvements focus on edge cases, realistic data patterns, and security considerations observed in production GIS systems.

## Key Improvements

### 1. Realistic GIS Test Data Helper (NEW)
**File**: `tests/Honua.Server.Core.Tests/TestInfrastructure/RealisticGisTestData.cs`

#### Real Parcel ID Patterns
- **California APN**: `123-456-789-000` (Los Angeles County format)
- **New York BBL**: `1-00123-0001` (Borough-Block-Lot)
- **Texas Property ID**: `0123-0001-0001-0000` (Harris County)
- **Washington Parcel**: `322404-9075-06` (King County)
- **Florida Folio**: `01-3213-023-0010` (Miami-Dade)
- **European INSPIRE**: `DE-NW-12345678-001`
- **Australian Lot/Plan**: `LOT 123 DP 456789`

#### Real Addresses with Special Characters
- **Apostrophes**: `"123 O'Brien Street, Apt #4A"`
- **Spanish Unicode**: `"Calle José María López, 4º Izq."`
- **French Unicode**: `"12 Rue de l'Église, Côte d'Azur"`
- **German Umlauts**: `"Münchener Straße 42, München"`
- **Ampersands**: `"Smith & Johnson Building, Suite 200"`
- **Slashes**: `"456 Main St, Unit 3/4"`
- **Hyphens**: `"789 Twenty-First Avenue NE"`
- **Combining Diacritics**: `"123 Café Boulevard"` (e + combining acute)
- **Japanese**: `"東京都新宿区西新宿2-8-1"`
- **Arabic RTL**: `"شارع الملك فهد، الرياض"`

#### Real City Coordinates
- **New York** (Times Square): `(-73.9855, 40.7580)`
- **Tokyo** (Shibuya): `(139.7006, 35.6595)`
- **Sydney** (Opera House): `(151.2153, -33.8568)`
- **London** (Big Ben): `(-0.1246, 51.5007)`
- **São Paulo**: `(-46.6333, -23.5505)`
- **Mumbai**: `(72.8777, 19.0760)`
- **Cairo**: `(31.2357, 30.0444)`
- **Moscow**: `(37.6173, 55.7558)`
- **Reykjavik** (high latitude): `(-21.8952, 64.1466)`
- **Ushuaia** (extreme south): `(-68.3029, -54.8019)`
- **Fiji** (antimeridian): `(178.4419, -18.1416)`
- **Alert, Canada** (extreme north): `(-62.3481, 82.5018)`

#### Real SRID Values
- **WGS84**: `4326` (GPS standard)
- **Web Mercator**: `3857` (Google Maps, OSM)
- **NAD83 CA State Plane III**: `2227` (California feet)
- **NAD83 UTM Zone 10N**: `26910` (Pacific Northwest)
- **NAD83 UTM Zone 18N**: `26918` (East Coast)
- **OSGB British National Grid**: `27700` (UK)
- **ETRS89 UTM Zone 32N**: `25832` (Central Europe)
- **GDA94 MGA Zone 55**: `28355` (Eastern Australia)
- **Tokyo Datum Zone 9**: `2451` (Japan)
- **NZGD2000 NZTM**: `2193` (New Zealand)

#### Complex Geometries
- **Large Parcel**: 1024+ vertices (simulates complex boundary surveys)
- **Multiple Holes**: Polygon with 3 interior rings (lakes, exclusions)
- **Antimeridian Crossing**: Polygons and lines crossing 180° longitude
- **Polar Regions**: North Pole and South Pole polygons
- **Degenerate Geometries**: Zero-area polygons, zero-length lines
- **Special Locations**: "Null Island" (0,0), Prime Meridian, Equator
- **High Precision**: 15 decimal places, subnormal floats

#### Feature Attributes with Edge Cases
- **SQL Injection Tests**: `"1' OR '1'='1"`, `"DROP TABLE parcels;--"`
- **Unicode Edge Cases**: Combining diacritics, RTL markers, zero-width chars
- **Numeric Extremes**: int.MaxValue, double.Epsilon, NaN, Infinity
- **DateTime Edge Cases**: DateTime.MinValue, Unix epoch, leap days, timezone offsets
- **Realistic Parcels**: California parcels with proper APNs, addresses, assessments

#### Realistic UUIDs
- **Deterministic UUIDs**: Seeded generation for reproducible tests
- **UUID v7**: Time-ordered UUIDs for realistic feature IDs

---

### 2. Enhanced Bulk Operations Tests
**File**: `tests/Honua.Server.Core.Tests/Data/BulkOperationsBenchmarkTests.cs`

#### New Test Cases Added

**100,000 Record Test** (added to existing theory):
```csharp
[InlineData(100000)]
```

**1,000,000 Record Test** (new):
```csharp
[Fact]
public async Task BulkInsert_OneMillionRecords_ShouldCompleteReasonably()
```
- Tests: 1 million record insertion
- Assertion: Completes in < 2 seconds
- Metrics: Total time, ms per record

**Memory Usage Tests** (new):
```csharp
[Theory]
[InlineData(10000)]
[InlineData(100000)]
public async Task BulkInsert_MemoryUsagePerRecord_ShouldBeLowForLargeDatasets()
```
- Measures: GC memory before/after
- Assertion: < 1KB per record
- Validates: Memory efficiency for bulk operations

---

### 3. Enhanced Query Builder Tests
**File**: `tests/Honua.Server.Enterprise.Tests/QueryBuilderTests.cs`

#### Improvements Made

**Realistic UUID Feature IDs**:
```csharp
[Fact]
public void BigQueryQueryBuilder_BuildById_WithRealisticUuid_ShouldGenerateValidSQL()
{
    var featureId = "a7b3c4d5-e6f7-8901-2345-6789abcdef01"; // Realistic UUID
    ...
}
```

**Large Pagination Offsets**:
```csharp
[Fact]
public void BigQueryQueryBuilder_BuildSelect_WithLargePagination_ShouldGenerateValidSQL()
{
    var query = new FeatureQuery(Limit: 1000, Offset: 50000); // Deep pagination
    ...
}
```

**Field Names with Special Characters**:
```csharp
[Fact]
public void SnowflakeQueryBuilder_BuildSelect_WithFieldNamesContainingSpecialCharacters_ShouldQuoteProperly()
{
    IdField = "feature-id", // Hyphen requires quoting
    ...
}
```

**Multi-Field Sorting with Nulls**:
```csharp
[Fact]
public void BigQueryQueryBuilder_BuildSelect_WithMultiFieldSortingAndNulls_ShouldGenerateValidSQL()
{
    var sortOrders = new List<FeatureSortOrder>
    {
        new("priority", FeatureSortDirection.Descending),
        new("name", FeatureSortDirection.Ascending),
        new("created_at", FeatureSortDirection.Descending)
    };
    ...
}
```

---

### 4. Geometry Edge Case Tests (NEW)
**File**: `tests/Honua.Server.Core.Tests/Integration/Matrix/GeometryEdgeCaseTests.cs`

#### Test Categories

**Degenerate Geometries**:
- Null Island (0,0) coordinate handling
- Zero-area polygons (collinear points)
- Zero-length linestrings (same point repeated)

**Antimeridian Crossing**:
- West-to-East line crossing
- East-to-West line crossing
- Polygon spanning Pacific Ocean
- Coordinate preservation across ±180°

**Polar Regions**:
- North Pole polygon (89°+ latitude)
- South Pole polygon (-89° latitude)
- Alert, Canada (82.5°N)
- Ushuaia, Argentina (54.8°S)

**Meridian and Equator**:
- Prime Meridian vertical line (0° longitude)
- Equator horizontal line (0° latitude)

**Coordinate Precision**:
- 15 decimal places (max precision)
- Subnormal float values (1e-10)
- Precision preservation in round-trips

**Large and Complex**:
- 1000+ vertex parcels
- Polygons with 3+ interior holes
- Area calculations with holes

**Real-World Cities**:
- Theory test for 10 major cities
- Validates coordinates in valid ranges
- Tests global coverage

**GeoJSON Round-Trips**:
- Complex geometry preservation
- Antimeridian coordinate handling
- Hole preservation in polygons

---

### 5. Unicode and String Edge Case Tests (NEW)
**File**: `tests/Honua.Server.Core.Tests/Data/UnicodeStringEdgeCaseTests.cs`

#### Test Categories

**Combining Diacritics**:
- Combining vs precomposed characters (`"Café"` two ways)
- Length differences (5 vs 4 code units)
- Normalization form handling (NFC, NFD)
- Vietnamese multi-diacritic names

**Right-to-Left Text**:
- RTL marker (U+200F) preservation
- Mixed LTR/RTL text handling
- Arabic address preservation

**Zero-Width Characters**:
- Zero-width space (U+200B)
- Zero-width non-joiner (U+200C) in Persian
- Zero-width joiner (U+200D) in emoji sequences

**Emoji**:
- Simple emoji preservation (`🏠`)
- Emoji with skin tone modifiers (`👍🏽`)
- Flag emoji (regional indicators: `🇺🇸`)
- Emoji sequences with ZWJ (`👨‍👩‍👧`)

**SQL Injection with Unicode**:
- Basic injection attempts
- Unicode homoglyphs (Cyrillic vs Latin)
- Should be parameterized, not escaped

**Mixed Scripts**:
- Japanese (Hiragana/Kanji/Katakana mix)
- Cyrillic + Latin + CJK mix
- Script boundary handling

**Normalization Forms**:
- NFC vs NFD comparisons
- Theory tests for multiple diacritic combinations
- Equivalence after normalization

**Control Characters**:
- NUL, SOH, Unit Separator detection
- Byte Order Mark (BOM) handling

**Real-World Addresses**:
- Theory test covering all realistic address types
- Validates presence and length

**Surrogate Pairs**:
- Characters outside BMP (U+10000+)
- Invalid surrogate detection
- UTF-16 encoding edge cases

---

### 6. OData Filter Edge Case Tests (NEW)
**File**: `tests/Honua.Server.Core.Tests/Data/ODataFilterEdgeCaseTests.cs`

#### Test Categories

**Nested AND/OR Combinations**:
- 2-level nesting: `(a and b) or (c and d)`
- 3-level nesting with mixed operators
- 4-level deep nesting (realistic complex queries)

**Geo.Distance with Realistic Coordinates**:
- New York (Times Square) - 5km radius
- Tokyo - 10km radius
- Fiji (antimeridian) - 50km radius
- Reykjavik (high latitude) - 20km radius
- Geo.Intersects with LineString

**LIKE with Wildcards**:
- `startswith()`, `endswith()`, `contains()`
- `substringof()` (OData v3 compatibility)
- Unicode characters in patterns
- Special characters (apostrophes)

**SQL Injection Attempts**:
- Classic OR 1=1: `'1' OR '1'='1'`
- DROP TABLE: `'1'; DROP TABLE parcels;--'`
- UNION SELECT: `'1' UNION SELECT * FROM users--'`
- Error-based injection
- Comment injection: `'admin'--'`
- Time-based: `'1' OR SLEEP(5)--'`
- Injection in field names
- Unicode homoglyphs

**Complex Real-World Filters**:
- California parcel search (unicode city names, value ranges)
- Geospatial + metadata combined
- Date ranges with null handling

**Field Names with Special Characters**:
- Spaces: `"Property Owner"`
- Hyphens: `"tax-year"`
- Starting with numbers: `"2024_revenue"`
- Quoting requirements

**IN Operator with Large Lists**:
- 100 values (realistic batch queries)
- UUID values in IN clause
- Filter length validation

**Numeric Edge Cases**:
- int.MaxValue, int.MinValue
- Geographic boundaries: ±90° lat, ±180° lon
- Theory tests for all edge values

---

## Implementation Patterns

### 1. Test Data Organization
```csharp
public static class RealisticGisTestData
{
    // Constants for common patterns
    public static string CaliforniaParcelId => "123-456-789-000";

    // Real coordinate tuples
    public static (double lon, double lat) NewYork => (-73.9855, 40.7580);

    // Factory methods for complex geometries
    public static Polygon CreateLargeParcel() { ... }

    // Edge case generators
    public static Dictionary<string, object?> GetSqlInjectionTestAttributes() { ... }
}
```

### 2. Realistic UUID Generation
```csharp
// Deterministic for testing
public static string GenerateFeatureUuid(int seed)
{
    var random = new Random(seed);
    // ... generate UUID
}

// Time-ordered (UUID v7)
public static string GenerateTimeOrderedUuid(DateTime timestamp, int sequence)
{
    var unixMs = ((DateTimeOffset)timestamp).ToUnixTimeMilliseconds();
    return $"{unixMs:x12}-{sequence:x4}-7{sequence:x3}-...";
}
```

### 3. Memory Usage Testing
```csharp
// Measure before
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
var memoryBefore = GC.GetTotalMemory(forceFullCollection: true);

// Perform operation
var records = CreateTestRecords(recordCount);
await provider.BulkInsertAsync(...);

// Measure after
var memoryAfter = GC.GetTotalMemory(forceFullCollection: false);
var bytesPerRecord = (memoryAfter - memoryBefore) / recordCount;

// Assert < 1KB per record
bytesPerRecord.Should().BeLessThan(1024.0);
```

### 4. Unicode Normalization Testing
```csharp
var combining = "Cafe\u0301";     // e + combining acute
var precomposed = "Café";         // precomposed é

// Different raw strings
combining.Should().NotBe(precomposed);

// Same after NFC normalization
combining.Normalize(NormalizationForm.FormC)
    .Should().Be(precomposed.Normalize(NormalizationForm.FormC));
```

---

## Test Coverage Summary

| Category | Test File | Test Count | Key Features |
|----------|-----------|------------|--------------|
| **Realistic Test Data** | RealisticGisTestData.cs | N/A (helper) | 7 parcel formats, 12 cities, 10 SRIDs, 15+ complex geometries |
| **Bulk Operations** | BulkOperationsBenchmarkTests.cs | +3 tests | 100K, 1M records, memory usage |
| **Query Builders** | QueryBuilderTests.cs | +4 tests | UUIDs, pagination, special chars, multi-sort |
| **Geometry Edge Cases** | GeometryEdgeCaseTests.cs | 20+ tests | Degenerate, antimeridian, polar, precision |
| **Unicode/Strings** | UnicodeStringEdgeCaseTests.cs | 25+ tests | Diacritics, RTL, emoji, normalization |
| **OData Filters** | ODataFilterEdgeCaseTests.cs | 20+ tests | Nested, geo, wildcards, injection, IN lists |

**Total New Tests**: ~70+ comprehensive test cases
**Total New Lines**: ~3,500+ lines of realistic test code

---

## Production Scenarios Covered

### Geographic Coverage
- ✅ North America (US, Canada, Mexico)
- ✅ Europe (UK, Germany, France)
- ✅ Asia (Japan, China, India, Middle East)
- ✅ South America (Brazil, Argentina)
- ✅ Africa (Egypt)
- ✅ Oceania (Australia, New Zealand, Pacific Islands)
- ✅ Arctic (Alert, Reykjavik)
- ✅ Antarctic (Ushuaia)
- ✅ Antimeridian (Fiji, Pacific)

### Character Sets
- ✅ Latin (English, Spanish, French, German)
- ✅ Cyrillic (Russian)
- ✅ Arabic (RTL text)
- ✅ CJK (Chinese, Japanese, Korean)
- ✅ Devanagari (Hindi)
- ✅ Emoji (with modifiers and ZWJ sequences)
- ✅ Control characters and BOM
- ✅ Combining diacritics

### Security
- ✅ SQL injection (6+ attack patterns)
- ✅ Unicode homoglyphs
- ✅ Control character injection
- ✅ Field name injection
- ✅ Comment-based attacks
- ✅ Time-based attacks

### Performance
- ✅ 100 records (baseline)
- ✅ 1,000 records
- ✅ 10,000 records
- ✅ 100,000 records
- ✅ 1,000,000 records
- ✅ Memory efficiency (< 1KB/record)
- ✅ Linear scaling validation

### Data Quality
- ✅ Degenerate geometries
- ✅ Coordinate precision (15 decimals)
- ✅ Extreme values (int/double min/max)
- ✅ Null handling
- ✅ Timezone offsets
- ✅ Leap days
- ✅ Deep pagination (50K+ offset)

---

## Usage Examples

### Using Realistic Coordinates
```csharp
var (lon, lat) = RealisticGisTestData.NewYork;
var point = Factory.CreatePoint(new Coordinate(lon, lat));
```

### Using Realistic Parcel IDs
```csharp
var apn = RealisticGisTestData.CaliforniaParcelId; // "123-456-789-000"
```

### Testing Unicode Edge Cases
```csharp
var attributes = RealisticGisTestData.GetUnicodeEdgeCaseAttributes();
// Contains: combining diacritics, RTL text, zero-width chars, emoji, etc.
```

### Testing Large Geometries
```csharp
var largeParcel = RealisticGisTestData.CreateLargeParcel(); // 1024 vertices
largeParcel.NumPoints.Should().BeGreaterThan(1000);
```

### Testing Antimeridian
```csharp
var line = RealisticGisTestData.CreateAntimeridianCrossingLine_WestToEast();
// Coordinates: (179.9, 0.0) to (-179.9, 0.0)
```

---

## Benefits

1. **Production Realism**: Tests use actual coordinate systems, parcel formats, and addresses from real-world GIS systems
2. **Security**: Comprehensive SQL injection and Unicode attack surface testing
3. **Internationalization**: Full Unicode coverage including RTL, emoji, combining diacritics
4. **Performance**: Large-scale tests (1M records) with memory usage validation
5. **Edge Cases**: Degenerate geometries, antimeridian, polar regions, precision limits
6. **Maintainability**: Centralized test data in reusable helper classes
7. **Documentation**: Each test clearly documents what real-world scenario it represents

---

## Next Steps

1. **Run Tests**: Verify all new tests pass
   ```bash
   dotnet test --filter "Category=EdgeCases|Category=Unicode"
   ```

2. **Baseline Performance**: Establish baseline metrics for bulk operations
   ```bash
   dotnet test tests/Honua.Server.Core.Tests/Data/BulkOperationsBenchmarkTests.cs
   ```

3. **Integration**: Incorporate realistic test data into existing integration tests

4. **CI/CD**: Add edge case tests to continuous integration pipeline

5. **Documentation**: Update test documentation with realistic scenario coverage

---

## References

- **Parcel ID Formats**: County assessor documentation (LA, NYC, King County, etc.)
- **Coordinate Systems**: EPSG Registry (https://epsg.io)
- **Unicode**: Unicode Standard 15.0 (https://unicode.org)
- **OGC Standards**: OGC Simple Features Specification
- **GeoJSON**: RFC 7946
- **OData**: OData Version 4.01 Specification

---

**Generated**: 2025-10-20
**Test Framework**: xUnit
**Assertion Library**: FluentAssertions
**Geometry Library**: NetTopologySuite
