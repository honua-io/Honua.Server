# Realistic Test Data - Quick Reference Guide

## New Files Created

| File | Size | Purpose |
|------|------|---------|
| `TestInfrastructure/RealisticGisTestData.cs` | 22KB | Central repository of realistic GIS test data |
| `Integration/Matrix/GeometryEdgeCaseTests.cs` | 15KB | Edge case tests for geometries |
| `Data/UnicodeStringEdgeCaseTests.cs` | 15KB | Unicode and string handling tests |
| `Data/ODataFilterEdgeCaseTests.cs` | 14KB | OData filter edge cases and injection tests |
| `TEST_DATA_IMPROVEMENTS_SUMMARY.md` | - | Comprehensive documentation |

## Files Updated

| File | Changes |
|------|---------|
| `Data/BulkOperationsBenchmarkTests.cs` | Added 100K, 1M record tests, memory usage validation |
| `../../Enterprise.Tests/QueryBuilderTests.cs` | Added UUID IDs, large pagination, special char fields |

## Quick Usage Examples

### 1. Use Realistic Coordinates
```csharp
// Instead of: (-122.4, 45.5)
var (lon, lat) = RealisticGisTestData.NewYork;  // (-73.9855, 40.7580)
```

### 2. Use Real Parcel IDs
```csharp
// Instead of: "123"
var apn = RealisticGisTestData.CaliforniaParcelId;  // "123-456-789-000"
```

### 3. Use Real Addresses
```csharp
// Instead of: "123 Main St"
var address = RealisticGisTestData.AddressWithApostrophe;  // "123 O'Brien Street, Apt #4A"
```

### 4. Use Real SRIDs
```csharp
// Instead of: 4326
var srid = RealisticGisTestData.NAD83_StatePlane_CA_III_Feet;  // 2227
```

### 5. Test Complex Geometries
```csharp
// Large parcel with 1024+ vertices
var parcel = RealisticGisTestData.CreateLargeParcel();

// Polygon crossing antimeridian
var pacific = RealisticGisTestData.CreateAntimeridianCrossingPolygon();

// Polygon with multiple holes
var donut = RealisticGisTestData.CreateParcelWithMultipleHoles();
```

### 6. Test Edge Cases
```csharp
// Null Island (0,0)
var nullIsland = RealisticGisTestData.CreateNullIsland();

// Degenerate polygon (zero area)
var degenerate = RealisticGisTestData.CreateDegeneratePolygon_ZeroArea();

// High precision point (15 decimals)
var precise = RealisticGisTestData.CreateMaxPrecisionPoint();
```

### 7. Test Unicode Edge Cases
```csharp
// Get all Unicode edge case attributes
var attrs = RealisticGisTestData.GetUnicodeEdgeCaseAttributes();
// Contains: combining diacritics, RTL text, zero-width chars, emoji

// Get SQL injection test attributes
var malicious = RealisticGisTestData.GetSqlInjectionTestAttributes();
// Contains: SQL injection attempts for security testing
```

### 8. Generate Realistic UUIDs
```csharp
// Deterministic UUID (for reproducible tests)
var uuid = RealisticGisTestData.GenerateFeatureUuid(seed: 42);

// Time-ordered UUID (UUID v7)
var timeUuid = RealisticGisTestData.GenerateTimeOrderedUuid(DateTime.UtcNow, sequence: 1);
```

### 9. Test Major World Cities
```csharp
// Access any major city coordinates
var cities = new[]
{
    RealisticGisTestData.NewYork,    // Times Square
    RealisticGisTestData.Tokyo,      // Shibuya Crossing
    RealisticGisTestData.Sydney,     // Opera House
    RealisticGisTestData.London,     // Big Ben
    RealisticGisTestData.SaoPaulo,
    RealisticGisTestData.Mumbai,
    RealisticGisTestData.Cairo,
    RealisticGisTestData.Moscow,
    RealisticGisTestData.Reykjavik,  // High latitude
    RealisticGisTestData.Ushuaia,    // Extreme south
    RealisticGisTestData.Fiji,       // Near antimeridian
    RealisticGisTestData.Alert       // Extreme north
};
```

### 10. Test Realistic California Parcel
```csharp
var parcel = RealisticGisTestData.GetRealisticCaliforniaParcel(index: 1);
// Returns:
// {
//   "apn": "123-456-001-000",
//   "owner": "O'Brien Family Trust",
//   "address": "123 José María Street, Unit #4A",
//   "city": "San José",
//   "zip": "95110",
//   "acreage": 0.35,
//   "assessed_value": 860000,
//   "year_built": 1986,
//   "use_code": "R1",
//   "last_sale_date": ...,
//   "tax_rate": 0.0125
// }
```

## Common Test Patterns

### Testing Antimeridian Handling
```csharp
[Fact]
public void AntimeridianCrossing_ShouldPreserveCoordinates()
{
    var line = RealisticGisTestData.CreateAntimeridianCrossingLine_WestToEast();

    line.Coordinates[0].X.Should().BeApproximately(179.9, 0.001);
    line.Coordinates[1].X.Should().BeApproximately(-179.9, 0.001);
}
```

### Testing Unicode Normalization
```csharp
[Fact]
public void CombiningDiacritics_ShouldNormalize()
{
    var attrs = RealisticGisTestData.GetUnicodeEdgeCaseAttributes();
    var combining = attrs["name_combining"] as string;
    var precomposed = attrs["name_precomposed"] as string;

    combining.Should().NotBe(precomposed);  // Different raw
    combining.Normalize(NormalizationForm.FormC)
        .Should().Be(precomposed.Normalize(NormalizationForm.FormC));  // Same normalized
}
```

### Testing SQL Injection Protection
```csharp
[Fact]
public void SqlInjection_ShouldBeParameterized()
{
    var malicious = RealisticGisTestData.GetSqlInjectionTestAttributes();
    var id = malicious["id"] as string;  // "1' OR '1'='1"

    // Should be passed as parameter, not concatenated
    var query = BuildParameterizedQuery(id);
    query.Parameters.Should().ContainKey("id");
    query.Parameters["id"].Should().Be(id);  // Stored as-is
    query.Sql.Should().NotContain("OR '1'='1'");  // Not in SQL string
}
```

### Testing Large Datasets
```csharp
[Fact]
public async Task BulkInsert_OneMillionRecords_ShouldComplete()
{
    const int recordCount = 1_000_000;
    var records = CreateTestRecords(recordCount);

    var stopwatch = Stopwatch.StartNew();
    var count = await provider.BulkInsertAsync(..., records);
    stopwatch.Stop();

    count.Should().Be(recordCount);
    stopwatch.Elapsed.TotalSeconds.Should().BeLessThan(2.0);
}
```

### Testing Memory Efficiency
```csharp
[Fact]
public async Task BulkInsert_MemoryPerRecord_ShouldBeLow()
{
    GC.Collect();
    var memoryBefore = GC.GetTotalMemory(forceFullCollection: true);

    var records = CreateTestRecords(100_000);
    await provider.BulkInsertAsync(..., records);

    var memoryAfter = GC.GetTotalMemory(forceFullCollection: false);
    var bytesPerRecord = (memoryAfter - memoryBefore) / 100_000.0;

    bytesPerRecord.Should().BeLessThan(1024.0);  // < 1KB per record
}
```

## Migration Guide

### Before (Simple Test Data)
```csharp
var point = Factory.CreatePoint(new Coordinate(-122.4, 45.5));
var id = "123";
var address = "123 Main St";
var srid = 4326;
```

### After (Realistic Test Data)
```csharp
var (lon, lat) = RealisticGisTestData.Tokyo;
var point = Factory.CreatePoint(new Coordinate(lon, lat));
var id = RealisticGisTestData.GenerateFeatureUuid(seed: 42);
var address = RealisticGisTestData.AddressWithSpanishUnicode;
var srid = RealisticGisTestData.NAD83_UTM_Zone_10N;
```

## Test Categories Added

Run specific test categories:

```bash
# All edge case tests
dotnet test --filter "Category=EdgeCases"

# Unicode tests only
dotnet test --filter "Category=Unicode"

# OData filter tests
dotnet test --filter "Category=OData"

# Geometry edge cases
dotnet test --filter "FullyQualifiedName~GeometryEdgeCaseTests"

# String edge cases
dotnet test --filter "FullyQualifiedName~UnicodeStringEdgeCaseTests"

# Performance tests
dotnet test --filter "Category=Performance"
```

## Data Coverage

### Geographic Coverage
- ✅ 6 continents
- ✅ 12 major cities worldwide
- ✅ Polar regions (Arctic, Antarctic)
- ✅ Antimeridian crossing
- ✅ All coordinate extremes

### Character Sets
- ✅ Latin (English, Spanish, French, German)
- ✅ Cyrillic
- ✅ Arabic (RTL)
- ✅ CJK (Chinese, Japanese, Korean)
- ✅ Emoji (with modifiers)
- ✅ Control characters

### Parcel ID Formats
- ✅ California APN
- ✅ New York BBL
- ✅ Texas Property ID
- ✅ Washington Parcel Number
- ✅ Florida Folio
- ✅ European INSPIRE
- ✅ Australian Lot/Plan

### Coordinate Systems
- ✅ 10 real-world SRIDs
- ✅ Geographic (WGS84)
- ✅ Projected (State Plane, UTM)
- ✅ Web Mercator
- ✅ International systems

## Best Practices

1. **Always use realistic data**: Replace magic numbers with named constants
2. **Test edge cases**: Use degenerate geometries, extreme coordinates
3. **Test security**: Include SQL injection and Unicode attack scenarios
4. **Test performance**: Use large datasets (100K+, 1M+)
5. **Test internationalization**: Use Unicode, RTL, mixed scripts
6. **Document why**: Each test should explain what real-world scenario it represents

## Performance Benchmarks

| Test | Records | Expected Time | Memory/Record |
|------|---------|---------------|---------------|
| Small | 100 | < 100ms | < 1KB |
| Medium | 1,000 | < 500ms | < 1KB |
| Large | 10,000 | < 1s | < 1KB |
| Very Large | 100,000 | < 2s | < 1KB |
| Extreme | 1,000,000 | < 2s | < 1KB |

## Further Reading

- Full documentation: `TEST_DATA_IMPROVEMENTS_SUMMARY.md`
- Source code: `TestInfrastructure/RealisticGisTestData.cs`
- Test examples: `Integration/Matrix/GeometryEdgeCaseTests.cs`
