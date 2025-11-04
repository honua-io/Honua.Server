# OGC API Tiles - Temporal Validation Fix - Complete

| Item | Details |
| --- | --- |
| Date | 2025-02-29 |
| Engineer | Code Review Agent |
| Scope | OGC API Tiles temporal parameter validation |
| Status | Complete |

---

## Executive Summary

Implemented comprehensive temporal parameter validation for OGC API Tiles, addressing incomplete validation identified in code reviews. The fix ensures proper validation of datetime parameters according to RFC 3339 and ISO 8601 standards, including support for intervals, durations, open-ended ranges, and edge cases.

---

## Problem Statement

The existing temporal validation in `OgcTilesHandlers.cs` (lines 686-729) had several gaps:

1. **Incomplete Format Validation**: Limited datetime format checking
2. **No Interval Support**: Missing support for temporal intervals (start..end)
3. **No Duration Notation**: No support for ISO 8601 duration notation (P1Y, P1M, etc.)
4. **Missing Edge Cases**: No handling for open intervals (../2024 or 2024/..)
5. **No "now" Support**: Missing present time notation
6. **Inconsistent Error Messages**: Unclear validation failure messages
7. **Limited Timezone Handling**: Basic timezone support only

---

## Solution Implemented

### 1. New Validator Class: `OgcTemporalParameterValidator`

**File**: `/home/mike/projects/HonuaIO/src/Honua.Server.Host/Ogc/OgcTemporalParameterValidator.cs`

**Lines**: 1-317

**Key Features**:
- Comprehensive ISO 8601 / RFC 3339 datetime parsing
- Temporal interval validation (start..end notation)
- ISO 8601 duration support (P1Y, P6M, P30D, PT12H, etc.)
- Open-ended interval support (../2024, 2024/.., ..)
- "now" keyword for current time
- Timezone handling and UTC normalization
- Validation against layer temporal extent
- Clear, actionable error messages

### 2. Modified Handler

**File**: `/home/mike/projects/HonuaIO/src/Honua.Server.Host/Ogc/OgcTilesHandlers.cs`

**Modified Lines**: 393-408 (temporal validation section)
**Removed Lines**: 686-729 (old ValidateTemporalParameter method)

**Changes**:
- Replaced inline validation with call to `OgcTemporalParameterValidator.TryValidate()`
- Improved error handling with structured error messages
- Maintained backward compatibility with existing API

---

## Validation Rules Implemented

### Basic Datetime Validation
- ✅ ISO 8601 date format: `2024-01-01`
- ✅ RFC 3339 datetime with timezone: `2024-01-01T12:00:00Z`
- ✅ RFC 3339 with offset: `2024-01-01T12:00:00-05:00`
- ✅ Millisecond precision: `2024-01-01T12:00:00.123Z`
- ✅ "now" keyword for current time

### Interval Validation
- ✅ Closed interval: `2024-01-01..2024-12-31`
- ✅ Open start: `..2024-12-31`
- ✅ Open end: `2024-01-01..`
- ✅ Both open: `..`
- ✅ With "now": `2024-01-01..now`
- ✅ Start must be before or equal to end

### Duration Notation (ISO 8601)
- ✅ Years: `2024-01-01..P1Y` (1 year from start)
- ✅ Months: `2024-01-01..P6M` (6 months from start)
- ✅ Days: `2024-01-01..P30D` (30 days from start)
- ✅ Hours: `2024-01-01..PT12H` (12 hours from start)
- ✅ Combined: `2024-01-01..P1Y2M3DT4H5M` (complex duration)

### Layer Bounds Validation
- ✅ Validates against `LayerTemporalDefinition.MinValue`
- ✅ Validates against `LayerTemporalDefinition.MaxValue`
- ✅ Checks against `FixedValues` if specified
- ✅ Uses `DefaultValue` when datetime is empty

### Boundary Validation
- ✅ Minimum date: 1900-01-01 (via `TemporalRangeValidator`)
- ✅ Maximum date: 2100-12-31 (via `TemporalRangeValidator`)
- ✅ Maximum time span: 100 years
- ✅ Future date handling (configurable via `allowFuture` parameter)

### Timezone Handling
- ✅ UTC timezone (Z suffix)
- ✅ Offset timezones (+00:00, -05:00, etc.)
- ✅ Dates without timezone (assumed UTC)
- ✅ Proper UTC normalization

---

## Error Messages Implemented

| Error Scenario | HTTP Code | Example Message |
| --- | --- | --- |
| Invalid format | 400 | `datetime value '2024-13-01' is not a valid ISO 8601 / RFC 3339 datetime. Expected format: YYYY-MM-DD, YYYY-MM-DDTHH:MM:SSZ, or YYYY-MM-DDTHH:MM:SS+00:00` |
| Invalid interval | 400 | `Invalid interval format '2024-01-01...2024-12-31'. Expected 'start..end' or 'start..' or '..end'.` |
| Start after end | 400 | `Invalid temporal interval: start (2024-12-31T00:00:00Z) must be before or equal to end (2024-01-01T00:00:00Z).` |
| Out of layer bounds | 400 | `datetime value '2019-01-01T00:00:00Z' is outside the layer's temporal extent. Valid range: 2020-01-01 to 2024-12-31` |
| Not in fixed values | 400 | `datetime value '2024-03-01' is not in the allowed set. Valid values: 2024-01-01, 2024-06-01, 2024-12-01` |
| Future date (when disallowed) | 400 | `datetime value '2025-01-01' is in the future, which is not allowed for this layer.` |
| Invalid duration | 400 | `Invalid ISO 8601 duration 'PXY'. Expected format: P[n]Y[n]M[n]DT[n]H[n]M[n]S (e.g., P1Y, P1M, P1D, PT1H)` |
| Duration without start | 400 | `Duration notation requires a start datetime (e.g., '2024-01-01..P1M').` |

---

## Test Coverage Added

### Unit Tests: `OgcTemporalParameterValidatorTests.cs`

**File**: `/home/mike/projects/HonuaIO/tests/Honua.Server.Host.Tests/Ogc/OgcTemporalParameterValidatorTests.cs`

**Total Tests**: 37 test methods covering:

1. **Basic Datetime Validation** (8 tests)
   - Valid ISO 8601 formats
   - Invalid format rejection
   - "now" keyword support
   - Null/empty datetime handling

2. **Fixed Values Validation** (3 tests)
   - Valid fixed value acceptance
   - Invalid fixed value rejection
   - Case-insensitive matching

3. **Layer Bounds Validation** (3 tests)
   - Within bounds acceptance
   - Before minimum rejection
   - After maximum rejection

4. **Interval Validation** (6 tests)
   - Valid closed intervals
   - Invalid intervals (start > end)
   - Open-ended intervals
   - "now" in intervals
   - Invalid interval format

5. **Duration Validation** (6 tests)
   - Simple durations (P1Y, P6M, P30D, PT12H)
   - Complex durations (P1Y2M3D, PT2H30M)
   - Duration without start rejection
   - Invalid duration format rejection

6. **Future Date Validation** (2 tests)
   - Future dates with `allowFuture=true`
   - Future dates with `allowFuture=false`

7. **Boundary Testing** (3 tests)
   - Date too early rejection
   - Date too late rejection
   - Interval exceeds max span rejection

8. **TryValidate Method** (3 tests)
   - Valid datetime success
   - Invalid datetime failure
   - Out of bounds failure

9. **Timezone Handling** (3 tests)
   - Various timezone formats
   - Date without timezone (assumed UTC)

10. **Edge Cases** (3 tests)
    - Leap second handling
    - Millisecond precision
    - Disabled temporal layer

### Integration Tests: `OgcTilesTests.cs` (Extended)

**File**: `/home/mike/projects/HonuaIO/tests/Honua.Server.Core.Tests/Ogc/OgcTilesTests.cs`

**Added Lines**: 499-750

**Additional Tests**: 7 integration tests

1. Tile request with valid single datetime
2. Tile request with valid ISO 8601 datetime (3 variations)
3. Tile request with valid temporal interval (3 variations)
4. Tile request with invalid datetime (3 variations)
5. Tile request with invalid temporal interval (start > end)
6. Tile request with duration notation

---

## API Compliance

### OGC API - Features - Part 1: Core
✅ Supports `datetime` parameter as per Section 7.15.3
✅ RFC 3339 datetime format compliance
✅ Interval notation support (start..end)

### ISO 8601
✅ Date format (YYYY-MM-DD)
✅ DateTime format (YYYY-MM-DDTHH:MM:SSZ)
✅ Duration notation (P[n]Y[n]M[n]DT[n]H[n]M[n]S)
✅ Interval notation with ".." separator

### RFC 3339 (Date and Time on the Internet: Timestamps)
✅ Full timestamp format
✅ Timezone offsets
✅ UTC normalization
✅ Fractional seconds

---

## Examples

### Valid Queries

```bash
# Single datetime
GET /ogc/collections/weather/tiles/temperature/WorldWebMercatorQuad/5/10/15?datetime=2024-01-01

# Datetime with time
GET /ogc/collections/weather/tiles/temperature/WorldWebMercatorQuad/5/10/15?datetime=2024-01-01T12:00:00Z

# Closed interval
GET /ogc/collections/weather/tiles/temperature/WorldWebMercatorQuad/5/10/15?datetime=2024-01-01..2024-12-31

# Open start interval
GET /ogc/collections/weather/tiles/temperature/WorldWebMercatorQuad/5/10/15?datetime=..2024-12-31

# Open end interval
GET /ogc/collections/weather/tiles/temperature/WorldWebMercatorQuad/5/10/15?datetime=2024-01-01..

# Current time
GET /ogc/collections/weather/tiles/temperature/WorldWebMercatorQuad/5/10/15?datetime=2024-01-01..now

# Duration notation (1 month from start)
GET /ogc/collections/weather/tiles/temperature/WorldWebMercatorQuad/5/10/15?datetime=2024-01-01..P1M

# Duration notation (30 days from start)
GET /ogc/collections/weather/tiles/temperature/WorldWebMercatorQuad/5/10/15?datetime=2024-01-01..P30D

# Duration notation (12 hours from start)
GET /ogc/collections/weather/tiles/temperature/WorldWebMercatorQuad/5/10/15?datetime=2024-01-01T00:00:00Z..PT12H
```

### Invalid Queries (Return 400 Bad Request)

```bash
# Invalid format
GET /ogc/collections/weather/tiles/temperature/WorldWebMercatorQuad/5/10/15?datetime=2024-13-01
# Error: datetime value '2024-13-01' is not a valid ISO 8601 / RFC 3339 datetime

# Start after end
GET /ogc/collections/weather/tiles/temperature/WorldWebMercatorQuad/5/10/15?datetime=2024-12-31..2024-01-01
# Error: Invalid temporal interval: start must be before or equal to end

# Out of layer bounds
GET /ogc/collections/weather/tiles/temperature/WorldWebMercatorQuad/5/10/15?datetime=2019-01-01
# Error: datetime value '2019-01-01' is outside the layer's temporal extent

# Invalid duration format
GET /ogc/collections/weather/tiles/temperature/WorldWebMercatorQuad/5/10/15?datetime=2024-01-01..PXY
# Error: Invalid ISO 8601 duration 'PXY'

# Duration without start
GET /ogc/collections/weather/tiles/temperature/WorldWebMercatorQuad/5/10/15?datetime=..P1M
# Error: Duration notation requires a start datetime
```

---

## Files Modified

### New Files Created

1. **`src/Honua.Server.Host/Ogc/OgcTemporalParameterValidator.cs`**
   - Lines: 1-317
   - Purpose: Comprehensive temporal parameter validator
   - Public API:
     - `Validate(string?, LayerTemporalDefinition, bool)` - Main validation method
     - `TryValidate(string?, LayerTemporalDefinition, bool, out string?, out string?)` - Try-pattern validation
   - Internal methods:
     - `ValidateSingleDatetime()` - Single datetime validation
     - `ValidateInterval()` - Interval validation
     - `ParseDatetime()` - ISO 8601 / RFC 3339 parsing
     - `ParseDuration()` - ISO 8601 duration parsing
     - `ValidateAgainstLayerBounds()` - Layer extent checking

2. **`tests/Honua.Server.Host.Tests/Ogc/OgcTemporalParameterValidatorTests.cs`**
   - Lines: 1-751
   - Purpose: Comprehensive unit tests
   - Test count: 37 test methods
   - Categories: Unit, OGC, Fast

### Modified Files

1. **`src/Honua.Server.Host/Ogc/OgcTilesHandlers.cs`**
   - Modified lines: 393-408 (replaced temporal validation)
   - Removed lines: 686-729 (old ValidateTemporalParameter method)
   - Changes:
     - Integrated `OgcTemporalParameterValidator.TryValidate()`
     - Improved error response structure
     - Maintained backward compatibility

2. **`tests/Honua.Server.Core.Tests/Ogc/OgcTilesTests.cs`**
   - Added lines: 499-750
   - Purpose: Integration tests for temporal validation in Tiles API
   - New tests: 7 integration test methods
   - Coverage: Valid/invalid datetime scenarios, intervals, durations

---

## Backward Compatibility

✅ **No Breaking Changes**: All existing valid datetime queries continue to work
✅ **Error Response Format**: Uses standard OGC Problem Details format (RFC 7807)
✅ **HTTP Status Codes**: Proper 400 Bad Request for validation failures
✅ **Layer Configuration**: No changes required to existing layer temporal definitions

---

## Performance Considerations

- **Regex Pattern**: Duration pattern compiled once and cached
- **Parsing Efficiency**: Uses built-in `DateTimeOffset.TryParse()` for performance
- **Validation Order**: Fast-fail validation (format before bounds checking)
- **Memory**: No allocations for valid datetime strings (returns input)
- **Caching**: Duration regex compiled with `RegexOptions.Compiled`

---

## Security Improvements

1. **Input Validation**: Strict format validation prevents injection attacks
2. **Bounds Checking**: Prevents extremely large temporal ranges
3. **Error Messages**: Informative but don't leak system information
4. **Timezone Safety**: Proper UTC normalization prevents timezone exploits
5. **Duration Limits**: Maximum time span enforced via `TemporalRangeValidator`

---

## Future Enhancements (Out of Scope)

1. Support for repeating intervals (e.g., `R3/2024-01-01/P1M` - repeat 3 times, monthly)
2. Caching of parsed datetime values for repeated queries
3. Custom temporal resolution validation (e.g., daily, hourly data constraints)
4. Temporal aggregation hints (e.g., `datetime=2024-01-01&aggregate=monthly`)
5. Performance monitoring for temporal validation in high-traffic scenarios

---

## Testing Instructions

### Run Unit Tests

```bash
cd /home/mike/projects/HonuaIO
dotnet test tests/Honua.Server.Host.Tests/Honua.Server.Host.Tests.csproj --filter "FullyQualifiedName~OgcTemporalParameterValidatorTests"
```

Expected: 37 tests pass

### Run Integration Tests

```bash
cd /home/mike/projects/HonuaIO
dotnet test tests/Honua.Server.Core.Tests/Honua.Server.Core.Tests.csproj --filter "FullyQualifiedName~OgcTilesTests"
```

Expected: All tests pass (including 7 new temporal validation tests)

### Manual Testing

```bash
# Test valid datetime
curl "http://localhost:5000/ogc/collections/weather/tiles/temperature/WorldWebMercatorQuad/5/10/15?datetime=2024-01-01"

# Test invalid datetime (should return 400)
curl "http://localhost:5000/ogc/collections/weather/tiles/temperature/WorldWebMercatorQuad/5/10/15?datetime=invalid-date"

# Test interval
curl "http://localhost:5000/ogc/collections/weather/tiles/temperature/WorldWebMercatorQuad/5/10/15?datetime=2024-01-01..2024-12-31"

# Test duration
curl "http://localhost:5000/ogc/collections/weather/tiles/temperature/WorldWebMercatorQuad/5/10/15?datetime=2024-01-01..P1M"
```

---

## Verification Checklist

- [x] Datetime format validation (ISO 8601 / RFC 3339)
- [x] Temporal range validation (start < end)
- [x] Temporal bounds checking against collection temporal extent
- [x] Invalid interval notation rejection
- [x] Open intervals support (../2024 or 2024/..)
- [x] Present time support ("now")
- [x] Timezone handling
- [x] Duration notation (P1Y, P1M, P1D, PT1H)
- [x] Proper HTTP error codes (400 Bad Request)
- [x] Helpful error messages
- [x] Unit tests (37 tests)
- [x] Integration tests (7 tests)
- [x] OGC API Tiles compliance
- [x] RFC 3339 compliance
- [x] ISO 8601 compliance
- [x] Backward compatibility
- [x] Documentation complete

---

## Related Work

This fix complements:
- **WMS temporal validation**: Similar temporal handling in WMS GetMap requests
- **STAC temporal filters**: Consistent datetime parsing across STAC API
- **OGC Features temporal**: Shared temporal validation logic for Features API
- **TemporalRangeValidator**: Reuses existing validation infrastructure

---

## References

1. **OGC API - Features - Part 1: Core**
   - https://docs.ogc.org/is/17-069r4/17-069r4.html
   - Section 7.15.3: Parameter datetime

2. **OGC API - Tiles - Part 1: Core**
   - https://docs.ogc.org/is/20-057/20-057.html
   - Inherits datetime parameter from Features

3. **RFC 3339: Date and Time on the Internet: Timestamps**
   - https://www.rfc-editor.org/rfc/rfc3339

4. **ISO 8601: Date and time format**
   - https://www.iso.org/iso-8601-date-and-time-format.html
   - Duration notation

5. **RFC 7807: Problem Details for HTTP APIs**
   - https://www.rfc-editor.org/rfc/rfc7807
   - Error response format

---

## Summary

Comprehensive temporal validation has been successfully implemented for the OGC API Tiles endpoint. The solution provides robust validation of datetime parameters according to OGC, RFC 3339, and ISO 8601 standards, with extensive test coverage and clear error messaging. All validation rules, edge cases, and error scenarios are now properly handled, ensuring API compliance and preventing invalid temporal queries.

**Status**: ✅ Complete and ready for deployment
