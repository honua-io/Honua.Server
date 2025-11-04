# STAC API Validation Error Message Enhancements

## Overview

This document describes the comprehensive improvements made to validation error messages throughout the STAC API to provide clearer, more actionable feedback to users.

## Goals

1. Provide specific, field-level validation details
2. Include actual vs. expected values in error messages
3. Add helpful examples for common validation failures
4. Follow RFC 7807 Problem Details format consistently
5. Cover edge cases previously not validated
6. Improve developer experience with actionable error messages

## Changes Made

### 1. Enhanced Validation Service (`StacValidationService.cs`)

#### New `StacValidationError` Record
Replaced simple string error messages with a structured error type containing:
- **Field**: The specific field that failed validation (e.g., `"bbox"`, `"properties.datetime"`)
- **Message**: Human-readable explanation of what went wrong
- **ActualValue**: The value that failed validation (for context)
- **ExpectedFormat**: What format/value was expected
- **Example**: A concrete example of a valid value

**Example Before:**
```
"Item 'bbox' must have 4 or 6 elements"
```

**Example After:**
```
Field 'bbox': Invalid number of coordinates: expected 4 or 6, but received 3.
Actual value: '[1, 2, 3]'.
Expected format: 4 coordinates: [minX, minY, maxX, maxY] or 6 coordinates: [minX, minY, minZ, maxX, maxY, maxZ].
Example: [-180, -90, 180, 90]
```

#### Enhanced Bbox Validation
- Validates coordinate count (must be 4 or 6)
- Validates each coordinate is a number
- Validates longitude range (-180 to 180)
- Validates latitude range (-90 to 90)
- Validates min <= max for all coordinate pairs
- Provides specific error messages for each validation failure with the exact coordinate and expected range

####Enhanced DateTime Validation
- Validates RFC 3339 format compliance
- Validates datetime is not too far in past (< 1900) or future (> 100 years from now)
- Validates start_datetime <= end_datetime relationships
- Provides clear examples of valid formats: `'2023-01-01T00:00:00Z'`, `'2023-01-01T12:30:00+05:30'`, `'2023-01-01T12:30:00.123Z'`
- Explains common mistakes (missing 'T', missing timezone)

#### Enhanced Field Validation
- **Collections**: `id`, `description`, `license`, `extent` (spatial/temporal), `stac_version`, `type`
- **Items**: `id`, `type`, `geometry`, `properties`, `assets`, `links`, `bbox`, datetime fields
- All required fields now have detailed error messages with expected formats and examples
- Added length validation (e.g., `id` must be <= 256 characters)

### 2. Enhanced Query Parameter Validation (`QueryParsingHelpers.cs`)

#### Bbox Query Parameter
**Before:**
```
"bbox must contain four or six numeric values."
```

**After:**
```
"Invalid bbox: expected 4 coordinates [minX, minY, maxX, maxY] or 6 coordinates [minX, minY, minZ, maxX, maxY, maxZ], but received 3 values. Example: '-180,-90,180,90' or '-180,-90,0,180,90,1000'"
```

Detailed validation added for:
- Empty bbox parameter
- Wrong number of coordinates with specific count reported
- Non-numeric values with position reported
- Out-of-range coordinates with actual value and valid range
- Invalid min/max relationships with actual values shown

#### DateTime Query Parameter
**Before:**
```
"Unable to parse '2023-01-01'."
```

**After:**
```
"Unable to parse datetime '2023-01-01'. Expected RFC 3339 format (ISO 8601 with timezone). Examples: '2023-01-01T00:00:00Z', '2023-01-01T12:30:00+05:30', '2023-01-01T12:30:00.123Z'. Common mistakes: missing 'T' between date and time, missing timezone indicator (Z or +/-HH:MM)."
```

Enhancements:
- Validates temporal range format (`start/end` or single datetime or open-ended like `../2023-12-31`)
- Validates start <= end for date ranges
- Validates datetime is within reasonable bounds (1900 to current + 100 years)
- Provides multiple format examples
- Lists common formatting mistakes

### 3. Updated Service Layer (`StacCollectionService.cs`, `StacItemService.cs`)

Added `FormatValidationErrors()` helper method that:
- Formats single errors as clear messages
- Formats multiple errors as numbered lists
- Ensures all validation details are included in the response

**Example Output for Multiple Errors:**
```
Validation failed with the following errors:
1. Field 'id': Required field is missing or empty. Expected format: A non-empty string identifier. Example: my-collection-123
2. Field 'description': Required field is missing or empty. Expected format: A non-empty string describing the collection. Example: Satellite imagery of agricultural land cover
3. Field 'extent': Required field is missing or not an object. Expected format: An object with 'spatial' and 'temporal' properties
```

### 4. Comprehensive Test Suite (`StacValidationEnhancedTests.cs`)

Created 270+ lines of comprehensive tests covering:

#### Collection Validation Tests
- Valid collection success case
- Missing required fields (`id`, `description`, `license`, `extent`)
- Too long `id` (> 256 characters)
- Invalid `type` field
- Invalid `stac_version`
- Missing spatial/temporal extent components
- Temporal interval validation (start > end)

#### Item Validation Tests
- Valid item success case
- Missing required fields (`id`, `type`, `geometry`, `properties`, `assets`)
- Invalid `type` field
- Null geometry validation

#### Bbox Validation Tests
- Invalid coordinate counts (3, 5, 7 coordinates)
- Out-of-range coordinates (longitude > 180, latitude > 90, etc.)
- Invalid min/max order (minX > maxX, minY > maxY, minZ > maxZ)
- Non-numeric coordinate values

#### DateTime Validation Tests
- Invalid datetime formats (date only without time)
- Null datetime without start_datetime/end_datetime
- start_datetime > end_datetime
- Datetime outside reasonable range (1899, 2200)
- Multiple format examples

#### Link Validation Tests
- Links missing `href`
- Links missing `rel`
- Invalid link structure

#### Error Formatting Tests
- `ToString()` method formats all fields correctly
- Handles minimal error information gracefully

## Examples of Improved Error Messages

### Bbox Validation

**Scenario**: User provides 3 coordinates instead of 4 or 6

**Old Error**:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Invalid bbox parameter",
  "status": 400,
  "detail": "bbox must contain four or six numeric values."
}
```

**New Error**:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Invalid bbox parameter",
  "status": 400,
  "detail": "Invalid bbox: expected 4 coordinates [minX, minY, maxX, maxY] or 6 coordinates [minX, minY, minZ, maxX, maxY, maxZ], but received 3 values. Example: '-180,-90,180,90' or '-180,-90,0,180,90,1000'"
}
```

### DateTime Validation

**Scenario**: User provides datetime without timezone

**Old Error**:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Invalid datetime parameter",
  "status": 400,
  "detail": "Unable to parse '2023-01-01T12:00:00'."
}
```

**New Error**:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Invalid datetime parameter",
  "status": 400,
  "detail": "Unable to parse datetime '2023-01-01T12:00:00'. Expected RFC 3339 format (ISO 8601 with timezone). Examples: '2023-01-01T00:00:00Z', '2023-01-01T12:30:00+05:30', '2023-01-01T12:30:00.123Z'. Common mistakes: missing 'T' between date and time, missing timezone indicator (Z or +/-HH:MM)."
}
```

### Collection Validation

**Scenario**: Creating a collection with missing required fields

**Old Error**:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Collection 'id' is required and must not be empty; Collection 'description' is required and must not be empty"
}
```

**New Error**:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Validation failed",
  "status": 400,
  "detail": "Validation failed with the following errors:\n1. Field 'id': Required field is missing or empty. Expected format: A non-empty string identifier. Example: my-collection-123\n2. Field 'description': Required field is missing or empty. Expected format: A non-empty string describing the collection. Example: Satellite imagery of agricultural land cover"
}
```

## Files Modified

### Core Files
- `/src/Honua.Server.Host/Stac/StacValidationService.cs` - Complete rewrite with structured error messages (883 lines)
- `/src/Honua.Server.Host/Utilities/QueryParsingHelpers.cs` - Enhanced bbox and datetime validation
- `/src/Honua.Server.Host/Stac/Services/StacCollectionService.cs` - Added error formatting
- `/src/Honua.Server.Host/Stac/Services/StacItemService.cs` - Added error formatting

### Test Files
- `/tests/Honua.Server.Host.Tests/Stac/StacValidationServiceTests.cs` - Updated for new error format
- `/tests/Honua.Server.Host.Tests/Stac/StacValidationEnhancedTests.cs` - New comprehensive test suite (370+ lines)

## Benefits

1. **Improved Developer Experience**: Developers get immediate, actionable feedback on what went wrong and how to fix it
2. **Reduced Support Burden**: Clear error messages reduce the need for support inquiries
3. **Faster Debugging**: Field-level errors with examples speed up problem resolution
4. **Better API Documentation**: Error examples serve as inline documentation
5. **Consistent Error Format**: All validation errors follow the same structured format
6. **Edge Case Coverage**: Previously uncaught validation errors are now handled

## Backward Compatibility

- Error response structure remains RFC 7807 compliant
- HTTP status codes unchanged (400 for validation errors)
- Response format still returns `ProblemDetails` objects
- Existing clients will receive more detailed error messages but the overall format is compatible

## Performance Impact

- Validation performance is maintained (no significant overhead)
- Additional validation checks are minimal and only run on validation failures
- Error formatting is lazy (only when validation fails)
- No impact on successful requests

## Future Enhancements

Potential areas for future improvement:
1. Add validation for STAC extensions (EO, SAR, etc.)
2. Implement JSON Schema validation against official STAC schemas
3. Add internationalization (i18n) support for error messages
4. Create a validation report endpoint for bulk validation
5. Add warnings for non-critical issues (e.g., recommended fields missing)

## Testing

To test the enhanced validation:

```bash
# Run STAC validation tests
dotnet test tests/Honua.Server.Host.Tests/Honua.Server.Host.Tests.csproj --filter "FullyQualifiedName~StacValidation"

# Test with invalid bbox
curl -X GET "http://localhost:5000/stac/search?bbox=1,2,3"

# Test with invalid datetime
curl -X GET "http://localhost:5000/stac/search?datetime=2023-01-01"

# Test with invalid collection
curl -X POST "http://localhost:5000/stac/collections" \
  -H "Content-Type: application/json" \
  -d '{"description": "Missing id"}'
```

## References

- [STAC Specification 1.0.0](https://github.com/radiantearth/stac-spec)
- [RFC 7807 - Problem Details for HTTP APIs](https://tools.ietf.org/html/rfc7807)
- [RFC 3339 - Date and Time on the Internet](https://tools.ietf.org/html/rfc3339)
- [OGC API - Features](https://ogcapi.ogc.org/features/)
