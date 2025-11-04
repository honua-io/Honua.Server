# OGC API Features Conformance Tests

## Overview

This test suite validates Honua's implementation of the OGC API - Features specification and prevents regression of OGC compliance.

## Test Coverage

### Required Conformance Classes (MUST implement)
- **Core** (`ogcapi-features-1/1.0/conf/core`) - Basic landing page, conformance, collections
- **GeoJSON** (`ogcapi-features-1/1.0/conf/geojson`) - GeoJSON output format

### Optional Conformance Classes (MAY implement)
- **OpenAPI 3.0** (`ogcapi-features-1/1.0/conf/oas30`) - API definition
- **HTML** (`ogcapi-features-1/1.0/conf/html`) - HTML representation
- **Search** (`ogcapi-features-3/1.0/conf/search`) - Advanced search
- **Filter** (`ogcapi-features-3/1.0/conf/filter`) - CQL2 filtering
- **CRS** (`ogcapi-features-3/1.0/conf/crs`) - Coordinate reference system support
- **Tiles** (`ogcapi-tiles-1/1.0/conf/core`) - Vector tile support

## Test Categories

### 1. Landing Page Tests (Core)
- ✅ Returns 200 OK status
- ✅ Returns JSON content type
- ✅ Contains required links (self, conformance, data, service-desc)
- ✅ Links have required properties (href, rel)

### 2. Conformance Tests
- ✅ Returns conformsTo array
- ✅ Declares required conformance classes
- ✅ Tracks optional conformance classes

### 3. Collections Tests
- ✅ Returns collections array
- ✅ Collections have required properties (id, links)
- ✅ Individual collection retrieval works
- ✅ 404 for nonexistent collections

### 4. Features (Items) Tests
- ✅ Returns GeoJSON FeatureCollection
- ✅ Supports limit parameter
- ✅ Supports bbox parameter
- ✅ Validates query parameters (returns 400 for invalid input)
- ✅ Individual feature retrieval works

### 5. Pagination Tests
- ✅ Supports offset parameter
- ✅ Includes HATEOAS links (self, next, prev)

### 6. CRS Support Tests
- ✅ Supports CRS parameter (optional)
- ✅ Validates CRS codes (returns 400 for invalid)

### 7. Content Negotiation Tests
- ✅ Defaults to GeoJSON for features
- ✅ Supports HTML via Accept header (optional)

### 8. Error Handling Tests
- ✅ Returns 404 for missing resources
- ✅ Returns 400 for invalid parameters
- ✅ Uses Problem Details format (optional)

### 9. Regression Prevention Tests
- ✅ All required endpoints accessible
- ✅ Conformance classes count not reduced

## Running Tests

### Run All OGC Conformance Tests
```bash
dotnet test tests/Honua.Server.Host.Tests/Honua.Server.Host.Tests.csproj \
  --filter "Category=OGC&Category=Conformance"
```

### Run Only Required Tests
```bash
dotnet test tests/Honua.Server.Host.Tests/Honua.Server.Host.Tests.csproj \
  --filter "FullyQualifiedName~OgcConformanceRegressionTests"
```

### Run with Detailed Output
```bash
dotnet test tests/Honua.Server.Host.Tests/Honua.Server.Host.Tests.csproj \
  --filter "Category=Conformance" \
  --logger "console;verbosity=detailed"
```

## CI Integration

These tests run on:
1. **Every commit** - As part of the main CI pipeline
2. **Every pull request** - To prevent conformance regression
3. **Nightly** - Full conformance suite with external validation

## Baseline and Regression Detection

The `OgcConformanceBaseline.json` file establishes the minimum acceptable conformance level:
- Defines required conformance classes
- Tracks optional features
- Sets minimum passing test threshold
- Prevents regression by failing if any test that previously passed now fails

## Test Results Interpretation

### All Tests Passing ✅
OGC compliance is maintained. Safe to merge.

### Required Test Failing ❌
**CRITICAL** - OGC compliance broken. Must be fixed before merge.

### Optional Feature Test Failing ⚠️
Optional feature not supported or regressed. Review if this was intentional.

### New Test Added 🆕
Increase in coverage. Update baseline if appropriate.

## Conformance Levels

### Level 0: Minimal (Required)
- Core conformance class
- GeoJSON conformance class
- All required endpoints functional

### Level 1: Standard (Current Target)
- Level 0 +
- OpenAPI 3.0 definition
- CRS support
- Query parameter validation

### Level 2: Advanced (Future)
- Level 1 +
- CQL2 filtering
- HTML representations
- Temporal operators
- Advanced spatial operators

## Known Issues and Limitations

1. **404 vs 500 for Missing Collections**
   - Spec requires: 404 Not Found
   - Current behavior: Sometimes returns 500
   - Tracked in: Issue #XXX

2. **HTML Content Negotiation**
   - Currently limited support
   - May return 406 or fall back to JSON

## Updating Baseline

When intentionally adding new conformance classes or features:

1. Run tests and verify all pass
2. Update `OgcConformanceBaseline.json` with new requirements
3. Document changes in commit message
4. Update this README with new coverage

## References

- [OGC API - Features Specification](https://docs.ogc.org/is/17-069r4/17-069r4.html)
- [OGC Conformance Classes](http://www.opengis.net/def/rel/ogc/1.0/conformance)
- [GeoJSON Specification (RFC 7946)](https://tools.ietf.org/html/rfc7946)
- [OGC API - Features Conformance Test Suite](https://github.com/opengeospatial/ets-ogcapi-features10)

## Contributing

When adding new OGC features:
1. Add tests to `OgcConformanceRegressionTests.cs`
2. Update baseline if adding required features
3. Mark as optional if feature is not required by spec
4. Document in this README

## Support

For questions about OGC compliance or test failures:
- Check existing test documentation
- Review OGC specification
- Open an issue with test output and context
