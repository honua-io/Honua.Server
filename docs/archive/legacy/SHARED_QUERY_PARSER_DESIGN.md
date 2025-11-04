# Shared Feature Query Parser - Design Document

**Date**: 2025-10-23
**Status**: Design Phase
**Target**: Eliminate query parsing duplication across OGC, WFS, and GeoServices APIs

---

## Problem Statement

Query parameter parsing is duplicated across three API implementations:

1. **OGC API Features** (`OgcSharedHandlers.ParseItemsQuery`)
2. **WFS** (distributed across WfsHandlers)
3. **GeoServices REST** (`GeoservicesRESTQueryTranslator`)

All three parse similar parameters (limit, offset, bbox, CRS, sorting, properties) with near-identical validation rules, but different parameter names and error formats.

**Current Duplication**: ~350+ lines of parsing logic across three locations

---

## Analysis Summary

### Common Parameters Across All APIs

| Concept | OGC Parameter | WFS Parameter | GeoServices Parameter | Common Type |
|---------|---------------|---------------|----------------------|-------------|
| **Limit** | `limit` | `count` / `maxFeatures` | `resultRecordCount` | Positive integer |
| **Offset** | `offset` | `startIndex` | `resultOffset` | Non-negative integer |
| **BBox** | `bbox` | `bbox` | `geometry` (with geometryType) | 4-6 doubles, CRS |
| **CRS** | `crs`, `Accept-Crs` header | `srsName` | `outSR` | EPSG code |
| **Result Type** | `resultType` (results/hits) | `resultType` (results/hits) | `returnCountOnly` (boolean) | Enum/Boolean |
| **Properties** | `properties` (comma-separated) | `propertyName` | `outFields` (comma-separated or *) | List of field names |
| **Sorting** | `sortby` (field:asc/desc) | `sortBy` (field ASC/DESC) | `orderByFields` (field ASC/DESC) | List of sort orders |
| **Temporal** | `datetime` (ISO 8601) | `time` (varies) | `time` (ISO 8601 or epoch) | Date range |

### Validation Rules (Shared Across All)

1. **Limit**: Positive integer (> 0), clamped to service/layer maximum
2. **Offset**: Non-negative integer (>= 0), defaults to 0
3. **BBox**: 4 or 6 numeric values, min < max validation
4. **CRS**: Normalized EPSG codes, validated against supported list
5. **Properties**: Field existence validation, deduplicated
6. **Sorting**: Field existence, direction validation (ASC/DESC)

### Error Format Differences

- **OGC**: RFC 7807 Problem Details (JSON)
- **WFS**: OGC ExceptionReport (XML)
- **GeoServices**: ArcGIS JSON error (`{"error": "message"}`)

---

## Design Approach

### Option 1: Single Unified Parser (Rejected)

**Pros**: Maximum code reuse
**Cons**:
- Complex parameter name mapping
- Different error format handling
- Hard to maintain backward compatibility
- Tight coupling between APIs

### Option 2: Shared Helper Library (SELECTED)

**Pros**:
- Each API keeps its orchestrator
- Minimal disruption to existing code
- Shared validation logic
- API-specific error formatting
- Easy to test and maintain

**Cons**:
- Slightly less code reduction than Option 1
- Each API still has its own parsing method

---

## Proposed Architecture

### New Utility Class: `QueryParameterHelper`

Location: `src/Honua.Server.Core/Query/QueryParameterHelper.cs`

```csharp
namespace Honua.Server.Core.Query;

/// <summary>
/// Shared utilities for parsing and validating query parameters across different API standards.
/// Provides parameter-specific parsing without assuming specific parameter names or error formats.
/// </summary>
public static class QueryParameterHelper
{
    // Pagination parsing
    public static (int Value, string? Error) ParseLimit(string? raw, int? serviceMax, int? layerMax, int fallback = 1000);
    public static (int? Value, string? Error) ParseOffset(string? raw);

    // Spatial parsing
    public static (SpatialFilter? Value, string? Error) ParseBoundingBox(string? raw, string? crs);
    public static (string? Value, string? Error) ParseCrs(string? raw, IReadOnlyList<string> supported, string? defaultCrs);

    // Temporal parsing
    public static (TemporalFilter? Value, string? Error) ParseTemporalRange(string? raw);

    // Property filtering
    public static (IReadOnlyList<string>? Value, string? Error) ParsePropertyNames(
        string? raw,
        IReadOnlyCollection<string> availableFields,
        string? idField,
        string? geometryField);

    // Sorting
    public static (IReadOnlyList<FeatureSortOrder>? Value, string? Error) ParseSortOrders(
        string? raw,
        IReadOnlyCollection<string> availableFields);

    // Result type
    public static (FeatureResultType Value, string? Error) ParseResultType(string? raw, FeatureResultType defaultValue = FeatureResultType.Results);

    // Boolean helpers
    public static (bool Value, string? Error) ParseBoolean(string? raw, bool defaultValue);
}
```

### Error Handling Strategy

**Parser returns tuples**: `(TValue, string? Error)`
- Success: `(value, null)`
- Failure: `(default, "Error message")`

**API-specific error formatting** happens in the orchestrator:

```csharp
// OGC Example
var (limit, error) = QueryParameterHelper.ParseLimit(query["limit"], serviceMax, layerMax);
if (error is not null)
{
    return (null, OgcProblemDetails.CreateValidationProblem(error, "limit"));
}

// WFS Example
var (limit, error) = QueryParameterHelper.ParseLimit(query["count"], serviceMax, layerMax);
if (error is not null)
{
    return (null, WfsExceptionReport.CreateInvalidParameter("count", error));
}

// GeoServices Example
var (limit, error) = QueryParameterHelper.ParseLimit(query["resultRecordCount"], serviceMax, layerMax);
if (error is not null)
{
    throw new GeoservicesRESTQueryException(error);
}
```

---

## Implementation Plan

### Phase 1: Create Shared Helper Class ✓

**File**: `src/Honua.Server.Core/Query/QueryParameterHelper.cs`

Implement all static helper methods with:
- Parameter-agnostic parsing
- Consistent validation rules
- String error messages (not IResult)
- Comprehensive XML documentation

### Phase 2: Migrate OGC API

**File**: `src/Honua.Server.Host/Ogc/OgcSharedHandlers.cs`

Replace inline parsing in `ParseItemsQuery` with calls to `QueryParameterHelper`:
- `ParseLimit` → `QueryParameterHelper.ParseLimit`
- `ParseOffset` → `QueryParameterHelper.ParseOffset`
- `ParseBoundingBox` → `QueryParameterHelper.ParseBoundingBox`
- etc.

Wrap errors with `OgcProblemDetails.CreateValidationProblem`.

**Estimated Lines Removed**: ~120 lines

### Phase 3: Migrate WFS

**Files**: `src/Honua.Server.Host/Wfs/WfsHandlers.cs`, `src/Honua.Server.Host/Wfs/WfsHelpers.cs`

Replace inline parsing with calls to `QueryParameterHelper`:
- Handle WFS-specific parameter names (`count`, `startIndex`, `srsName`)
- Wrap errors with `WfsExceptionReport.CreateInvalidParameter`

**Estimated Lines Removed**: ~100 lines

### Phase 4: Migrate GeoServices REST

**Files**:
- `src/Honua.Server.Host/GeoservicesREST/Services/GeoservicesParameterResolver.cs`
- `src/Honua.Server.Host/GeoservicesREST/Services/GeoservicesFieldResolver.cs`
- `src/Honua.Server.Host/GeoservicesREST/Services/GeoservicesSpatialResolver.cs`

Replace resolver implementations with calls to `QueryParameterHelper`:
- Handle GeoServices-specific parameter names (`resultRecordCount`, `outSR`, `outFields`)
- Wrap errors with `throw new GeoservicesRESTQueryException(error)`

**Estimated Lines Removed**: ~130 lines

### Phase 5: Testing and Validation

- Build all projects
- Run existing integration tests (all should pass)
- Add new unit tests for `QueryParameterHelper`
- Verify error message consistency

---

## Benefits

### Code Reduction
- **Before**: ~350 lines of duplicated parsing logic
- **After**: ~100 lines of shared helpers + ~100 lines of orchestration
- **Net Reduction**: ~150 lines (43%)

### Quality Improvements
1. **Single source of truth** for validation rules
2. **Consistent behavior** across all APIs
3. **Easier testing** - test helpers once instead of three times
4. **Easier maintenance** - fix bugs in one place
5. **Future-proof** - new APIs can reuse helpers

### Backward Compatibility
- **Zero breaking changes** - all APIs keep existing signatures
- **Same error messages** - validation messages unchanged
- **Same behavior** - all edge cases preserved

---

## API-Specific Orchestrators (Keep Existing)

### OGC API Features
**File**: `src/Honua.Server.Host/Ogc/OgcSharedHandlers.cs`
**Method**: `ParseItemsQuery(HttpRequest, ServiceDefinition, LayerDefinition)`
**Responsibilities**:
- Call `QueryParameterHelper` for each parameter
- Convert errors to `OgcProblemDetails`
- Handle Accept-Crs header negotiation
- Format response handling
- Build final `FeatureQuery` object

### WFS
**File**: `src/Honua.Server.Host/Wfs/WfsHandlers.cs`
**Method**: `ParseGetFeatureQuery(HttpRequest, ServiceDefinition, LayerDefinition)`
**Responsibilities**:
- Call `QueryParameterHelper` for each parameter
- Convert errors to `WfsExceptionReport`
- Handle XML filter parsing (CQL-Text, CQL2)
- Output format negotiation
- Build final `FeatureQuery` object

### GeoServices REST
**File**: `src/Honua.Server.Host/GeoservicesREST/GeoservicesRESTQueryTranslator.cs`
**Method**: `TryParse(HttpRequest, CatalogServiceView, CatalogLayerView)`
**Responsibilities**:
- Call `QueryParameterHelper` via resolver services
- Convert errors to ArcGIS JSON format
- Handle WHERE clause parsing
- Statistics and grouping handling
- Build final `GeoservicesRESTQueryContext`

---

## Testing Strategy

### Unit Tests for QueryParameterHelper

**New File**: `tests/Honua.Server.Core.Tests/Query/QueryParameterHelperTests.cs`

Test coverage:
- Valid inputs return correct values
- Invalid inputs return error messages
- Edge cases (empty, null, whitespace)
- Boundary conditions (min/max values)
- Type conversion errors
- Clamping behavior for limits
- CRS normalization
- Field validation

### Integration Tests (Existing)

All existing integration tests should continue to pass:
- `tests/Honua.Server.Core.Tests/Ogc/Ogc*.cs`
- `tests/Honua.Server.Core.Tests/Wfs/Wfs*.cs`
- `tests/Honua.Server.Core.Tests/Hosting/GeoservicesRest*.cs`

No test changes required - APIs maintain exact same behavior.

---

## Migration Checklist

- [ ] Create `QueryParameterHelper` class in Core
- [ ] Implement all helper methods with tests
- [ ] Migrate OGC `ParseItemsQuery` to use helpers
- [ ] Migrate WFS query parsing to use helpers
- [ ] Migrate GeoServices resolvers to use helpers
- [ ] Build and verify all projects compile
- [ ] Run full test suite
- [ ] Update this document with final line counts
- [ ] Create summary documentation

---

## Example: Limit Parsing Migration

### Before (OGC - Duplicated Code)

```csharp
// In OgcSharedHandlers.ParseItemsQuery
var limitRaw = query["limit"].ToString();
int limit;
if (string.IsNullOrWhiteSpace(limitRaw))
{
    limit = effectiveItemLimit;
}
else
{
    if (!int.TryParse(limitRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
    {
        return (null, OgcProblemDetails.CreateValidationProblem("limit must be a positive integer.", "limit"));
    }
    limit = Math.Min(parsed, effectiveItemLimit);
}
```

### Before (WFS - Duplicated Code)

```csharp
// In WfsHelpers
var countRaw = query["count"].ToString();
int limit;
if (string.IsNullOrWhiteSpace(countRaw))
{
    limit = effectiveLimit;
}
else
{
    if (!int.TryParse(countRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
    {
        return (null, CreateInvalidParameterValue("count", "count must be a positive integer."));
    }
    limit = Math.Min(parsed, effectiveLimit);
}
```

### Before (GeoServices - Duplicated Code)

```csharp
// In GeoservicesParameterResolver
var limitRaw = query["resultRecordCount"].ToString();
int limit;
if (string.IsNullOrWhiteSpace(limitRaw))
{
    limit = effectiveLimit;
}
else
{
    if (!int.TryParse(limitRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
    {
        throw new GeoservicesRESTQueryException("resultRecordCount must be a non-negative integer.");
    }
    limit = parsed == 0 ? effectiveLimit : Math.Min(parsed, effectiveLimit);
}
```

### After (Shared Helper)

```csharp
// In QueryParameterHelper (Core)
public static (int Value, string? Error) ParseLimit(
    string? raw,
    int? serviceMax,
    int? layerMax,
    int fallback = 1000)
{
    var effectiveMax = layerMax.HasValue && serviceMax.HasValue
        ? Math.Min(layerMax.Value, serviceMax.Value)
        : layerMax ?? serviceMax ?? fallback;

    if (string.IsNullOrWhiteSpace(raw))
    {
        return (effectiveMax, null);
    }

    if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
    {
        return (0, "limit must be a positive integer.");
    }

    return (Math.Min(parsed, effectiveMax), null);
}
```

### After (OGC Usage)

```csharp
var (limit, error) = QueryParameterHelper.ParseLimit(
    query["limit"].ToString(),
    service.Ogc.ItemLimit,
    layer.Query.MaxRecordCount);
if (error is not null)
{
    return (null, OgcProblemDetails.CreateValidationProblem(error, "limit"));
}
```

### After (WFS Usage)

```csharp
var (limit, error) = QueryParameterHelper.ParseLimit(
    query["count"].ToString(),
    service.Ogc.ItemLimit,
    layer.Query.MaxRecordCount);
if (error is not null)
{
    return (null, WfsExceptionReport.CreateInvalidParameterValue("count", error));
}
```

### After (GeoServices Usage)

```csharp
var (limit, error) = QueryParameterHelper.ParseLimit(
    query["resultRecordCount"].ToString(),
    service.Ogc.ItemLimit,
    layer.Query.MaxRecordCount);
if (error is not null)
{
    throw new GeoservicesRESTQueryException(error);
}
```

**Lines Saved**: 15 lines (per API) × 3 APIs = 45 lines just for limit parsing

---

## Success Criteria

1. ✅ All main projects build successfully
2. ✅ All existing tests pass
3. ✅ New unit tests for `QueryParameterHelper` with 90%+ coverage
4. ✅ Zero breaking changes to API behavior
5. ✅ ~150+ lines of duplicated code removed
6. ✅ Documentation updated

---

**End of Design Document**
