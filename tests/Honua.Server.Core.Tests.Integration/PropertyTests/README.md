# Property-Based Testing with FsCheck

This directory contains property-based tests using FsCheck for comprehensive fuzzing and invariant testing.

## Overview

Property-based testing validates that code satisfies general properties across a wide range of inputs, rather than testing specific examples. This is particularly valuable for:

- **Security testing**: SQL injection, path traversal, command injection
- **Mathematical properties**: Coordinate transformations, tile calculations
- **Invariants**: Round-trip conversions, bounds checking
- **Edge cases**: Automatically discovering corner cases

## Test Files

### Security-Critical Tests

#### SqlInjectionPropertyTests.cs
- **Purpose**: Verify SQL query parameterization prevents injection attacks
- **Properties tested**:
  - Malicious SQL strings are always parameterized, never interpolated
  - Field names are properly quoted
  - Type mismatches reject SQL injection attempts
  - Multiple parameters are independently protected
- **Coverage**: 7 properties, 2,200+ test cases

#### PathTraversalPropertyTests.cs
- **Purpose**: Ensure file path sanitization prevents directory traversal
- **Properties tested**:
  - Path traversal sequences (.., /, \) are removed
  - Invalid filename characters are sanitized
  - Unicode normalization prevents bypass attempts
  - Null byte injection is blocked
- **Coverage**: 10 properties, 2,800+ test cases

### Input Validation Tests

#### InputValidationPropertyTests.cs
- **Purpose**: Validate geographic and API parameter inputs
- **Properties tested**:
  - Bounding boxes: min < max, within projection bounds
  - DateTime: ISO8601 parsing, malicious input rejection
  - SRID/CRS: Valid EPSG code ranges
  - Tile coordinates: Within zoom level bounds
  - Command injection: Shell metacharacter detection
- **Coverage**: 11 properties, 3,100+ test cases

### Data Transformation Tests

#### TileCoordinatePropertyTests.cs
- **Purpose**: Verify tile coordinate calculations and conversions
- **Properties tested**:
  - Tile to bounding box produces valid coordinates
  - Adjacent tiles share edges exactly
  - Tile size consistency within zoom levels
  - Tile size halves when zoom increases
  - Round-trip conversions preserve coordinates
  - Matrix set identification works for all variants
- **Coverage**: 9 properties, 2,300+ test cases

#### ZarrChunkPropertyTests.cs
- **Purpose**: Test multi-dimensional array chunking for Zarr format
- **Properties tested**:
  - Chunk indices are within bounds
  - Chunk offsets are within chunk dimensions
  - Flat index uniqueness for different coordinates
  - Round-trip flat index to multi-dimensional
  - Chunk count covers entire array
  - Partial chunk handling at boundaries
  - Time-series chunk calculations
  - Row-major ordering consistency
- **Coverage**: 10 properties, 2,600+ test cases

#### GeoTiffPropertyTests.cs
- **Purpose**: Verify GeoTIFF geospatial transformation calculations
- **Properties tested**:
  - Pixel-to-geo transformations are reversible
  - Pixel size determines resolution
  - Rotation preserves area (determinant check)
  - Bounding box covers entire image
  - Model pixel scale values are positive
  - Tiepoint pixel-to-geo mapping validity
  - 4x4 transformation matrix structure
  - EPSG code validity
  - Coordinate topology preservation
  - NoData value representation
- **Coverage**: 10 properties, 2,600+ test cases

## Running the Tests

### Run all property tests
```bash
dotnet test --filter "FullyQualifiedName~PropertyTests"
```

### Run specific test class
```bash
dotnet test --filter "FullyQualifiedName~SqlInjectionPropertyTests"
```

### Run with verbose output
```bash
dotnet test --filter "FullyQualifiedName~PropertyTests" --logger "console;verbosity=detailed"
```

## Writing New Property Tests

### Basic Pattern

```csharp
using FsCheck;
using FsCheck.Xunit;

public class MyPropertyTests
{
    [Property(MaxTest = 500)]
    public Property MyProperty_ShouldHoldForAllInputs()
    {
        return Prop.ForAll(
            GenerateTestData(),
            input =>
            {
                // Act
                var result = SystemUnderTest(input);

                // Assert properties
                Assert.True(PropertyHolds(result));

                return true;
            });
    }

    private static Arbitrary<MyData> GenerateTestData()
    {
        var gen = from field1 in Gen.Choose(0, 100)
                  from field2 in Gen.Elements("a", "b", "c")
                  select new MyData(field1, field2);

        return Arb.From(gen);
    }
}
```

### Custom Generators

FsCheck generators use LINQ comprehension syntax:

```csharp
// Generate valid tile coordinates
var gen = from zoom in Gen.Choose(0, 18)
          let dimension = 1 << zoom
          from row in Gen.Choose(0, dimension - 1)
          from col in Gen.Choose(0, dimension - 1)
          select (zoom, row, col);

return Arb.From(gen);
```

### Malicious Input Generators

For security testing, create generators that produce known attack patterns:

```csharp
private static Arbitrary<string> GenerateSqlInjectionAttempt()
{
    var attacks = new[]
    {
        "'; DROP TABLE users--",
        "1' OR '1'='1",
        "admin'--"
    };

    return Arb.From(Gen.Elements(attacks));
}
```

### Combining Generators

```csharp
var gen = Gen.OneOf(
    Gen.Elements(commonValues),
    Gen.Choose(0, 100),
    GenerateEdgeCases().Generator
);
```

## Best Practices

### 1. Test Count Configuration
- Security-critical: 500-1000 tests per property
- Data transformations: 200-500 tests
- Simple invariants: 100-200 tests

```csharp
[Property(MaxTest = 500)]  // Adjust based on complexity
```

### 2. Property Selection

Good properties to test:
- **Invariants**: Properties that always hold (e.g., min < max)
- **Round-trips**: f(g(x)) = x
- **Idempotence**: f(f(x)) = f(x)
- **Commutativity**: f(a, b) = f(b, a)
- **Boundary conditions**: Edge values behave correctly

### 3. Shrinking

FsCheck automatically shrinks failing inputs to find minimal examples:

```csharp
// If a test fails with input 1523, FsCheck will try:
// 1000, 500, 250, ... to find the smallest failing value
```

### 4. Combining with Example-Based Tests

Use both approaches:
- Property tests for discovering edge cases
- Example tests for documenting specific scenarios

### 5. Timeout Handling

For expensive properties:

```csharp
[Property(MaxTest = 100, Timeout = 5000)]  // 5 second timeout
```

## Common Patterns

### Pattern 1: Injection Prevention
```csharp
[Property]
public Property UserInput_ShouldNeverAppearInOutput_Unescaped()
{
    return Prop.ForAll(
        GenerateMaliciousInput(),
        input =>
        {
            var output = ProcessInput(input);
            Assert.DoesNotContain(input, output);  // Input is escaped
            return true;
        });
}
```

### Pattern 2: Boundary Checking
```csharp
[Property]
public Property Output_ShouldAlwaysBe_WithinBounds()
{
    return Prop.ForAll(
        Arb.Default.Int32(),
        input =>
        {
            var result = Calculate(input);
            Assert.InRange(result, min, max);
            return true;
        });
}
```

### Pattern 3: Reversibility
```csharp
[Property]
public Property RoundTrip_ShouldPreserve_OriginalValue()
{
    return Prop.ForAll(
        GenerateValue(),
        original =>
        {
            var encoded = Encode(original);
            var decoded = Decode(encoded);
            Assert.Equal(original, decoded);
            return true;
        });
}
```

### Pattern 4: Consistency
```csharp
[Property]
public Property MultipleInvocations_ShouldReturn_SameResult()
{
    return Prop.ForAll(
        GenerateInput(),
        input =>
        {
            var result1 = Function(input);
            var result2 = Function(input);
            Assert.Equal(result1, result2);
            return true;
        });
}
```

## Statistics

Total property-based tests: **57 properties**
Total test cases executed: **15,600+** (when all run with default settings)

### Coverage Breakdown
- Security tests: 17 properties (29.8%)
- Input validation: 11 properties (19.3%)
- Data transformations: 29 properties (50.9%)

### Execution Time
- Full suite: ~30-60 seconds (depending on hardware)
- Individual test class: ~5-10 seconds

## Bugs Discovered

Property-based testing has helped identify:
1. Edge case in tile boundary calculations at zoom level 0
2. Unicode normalization bypass in path sanitization
3. Floating point precision issues in coordinate transformations
4. Integer overflow in chunk index calculations for large arrays

## Future Enhancements

Potential areas for additional property tests:
- [ ] CRS transformation round-trips (EPSG:4326 ↔ EPSG:3857)
- [ ] Geometry validation (topology preservation)
- [ ] Metadata schema validation (ISO 19115, STAC)
- [ ] Cache key collision resistance
- [ ] Compression/decompression round-trips
- [ ] OAuth token validation
- [ ] Rate limiting behavior under load

## References

- [FsCheck Documentation](https://fscheck.github.io/FsCheck/)
- [Property-Based Testing Patterns](https://blog.johanneslink.net/2018/03/24/patterns-to-find-properties/)
- [QuickCheck Paper](https://www.cs.tufts.edu/~nr/cs257/archive/john-hughes/quick.pdf)
- [Choosing Properties for Property-Based Testing](https://fsharpforfunandprofit.com/posts/property-based-testing-2/)
