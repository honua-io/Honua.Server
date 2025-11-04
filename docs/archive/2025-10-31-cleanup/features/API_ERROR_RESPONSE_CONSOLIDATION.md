# API Error Response Consolidation

## Overview

This document describes the consolidated API error response architecture implemented to standardize error handling across all API protocols in the Honua platform.

## Problem Statement

Prior to consolidation, error responses were scattered across multiple implementations:

- **GeoservicesRESTErrorHelper**: 54 lines for JSON error responses
- **OgcExceptionHelper**: 59 lines for WMS/WFS XML exceptions
- **OgcProblemDetails**: 316 lines for RFC 7807 Problem Details
- **Inline patterns**: 200+ inline `Results.BadRequest(new { error })` calls

This resulted in:
- Code duplication across 50+ files
- Inconsistent error formats
- Difficulty maintaining error response standards
- No single source of truth for error response construction

## Solution Architecture

### Unified Error Response Builder

**File**: `/src/Honua.Server.Host/Utilities/ApiErrorResponse.cs`

A single, comprehensive error response builder organized by protocol format:

```
ApiErrorResponse
├── Json                    - Simple JSON error responses
│   ├── BadRequest()
│   ├── BadRequestResult()
│   ├── NotFound()
│   ├── NotFound(resource, identifier)
│   ├── InternalServerError()
│   ├── Conflict()
│   └── UnprocessableEntity()
│
├── OgcXml                  - OGC standards XML exceptions
│   ├── WmsException()
│   └── WfsException()
│
└── ProblemDetails          - RFC 7807 Problem Details
    ├── Types               - OGC exception type URIs
    ├── InvalidParameter()
    ├── NotFound()
    ├── Conflict()
    ├── ServerError()
    ├── InvalidCrs()
    ├── InvalidBbox()
    ├── InvalidDatetime()
    └── ... (14 total methods)
```

## Protocol-Specific Formats

### 1. JSON Error Responses (REST APIs)

**Used by**: GeoservicesREST, Admin endpoints, Carto, OpenRosa, Print APIs

**Format**:
```json
{
  "error": "Error message here"
}
```

**Usage**:
```csharp
// Before
return new BadRequestObjectResult(new { error = "Invalid input" });
return Results.NotFound(new { error = "Dataset 'xyz' not found." });

// After
return ApiErrorResponse.Json.BadRequest("Invalid input");
return ApiErrorResponse.Json.NotFound("Dataset", "xyz");
```

### 2. OGC XML Exceptions (WMS/WFS)

**Used by**: WMS, WFS, WMTS services

**WMS Format** (ServiceExceptionReport):
```xml
<?xml version="1.0" encoding="utf-8"?>
<ServiceExceptionReport version="1.3.0" xmlns="http://www.opengis.net/wms">
  <ServiceException code="InvalidParameterValue">Error message</ServiceException>
</ServiceExceptionReport>
```

**WFS Format** (ExceptionReport):
```xml
<?xml version="1.0" encoding="utf-8"?>
<ExceptionReport version="2.0.0" xmlns:ows="http://www.opengis.net/ows/1.1">
  <Exception exceptionCode="InvalidParameterValue" locator="bbox">
    <ExceptionText>Error message</ExceptionText>
  </Exception>
</ExceptionReport>
```

**Usage**:
```csharp
// Before
return OgcExceptionHelper.CreateWmsException("InvalidParameterValue", "BBOX is required");
return OgcExceptionHelper.CreateWfsException("OperationParsingFailed", "filter", "Invalid filter");

// After
return ApiErrorResponse.OgcXml.WmsException("InvalidParameterValue", "BBOX is required");
return ApiErrorResponse.OgcXml.WfsException("OperationParsingFailed", "filter", "Invalid filter");
```

### 3. RFC 7807 Problem Details (OGC API)

**Used by**: OGC API Features, OGC API Tiles, OGC API Styles, Records API

**Format**:
```json
{
  "type": "http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/invalid-parameter",
  "title": "Invalid Parameter",
  "status": 400,
  "detail": "The 'limit' parameter must be between 1 and 10000",
  "parameter": "limit"
}
```

**Usage**:
```csharp
// Before
var problem = new ProblemDetails
{
    Type = OgcProblemDetails.ExceptionTypes.InvalidParameter,
    Title = "Invalid Parameter",
    Status = 400,
    Detail = "Invalid limit value"
};
problem.Extensions["parameter"] = "limit";
return Results.Problem(problem);

// After
return ApiErrorResponse.ProblemDetails.InvalidParameter(
    "Invalid limit value",
    parameterName: "limit");
```

## Backward Compatibility

The old helper classes are maintained for backward compatibility but marked as `[Obsolete]`:

- **GeoservicesRESTErrorHelper** → delegates to `ApiErrorResponse.Json`
- **OgcExceptionHelper** → delegates to `ApiErrorResponse.OgcXml`
- **OgcProblemDetails** → delegates to `ApiErrorResponse.ProblemDetails`

All existing code continues to work without modification. New code should use `ApiErrorResponse` directly.

## Migration Guide

### For New Code

Use `ApiErrorResponse` directly:

```csharp
using Honua.Server.Host.Utilities;

// JSON errors
return ApiErrorResponse.Json.BadRequest("Invalid request");
return ApiErrorResponse.Json.NotFound("Layer", layerId);

// OGC XML errors
return ApiErrorResponse.OgcXml.WmsException("InvalidFormat", "Format PNG is not supported");
return ApiErrorResponse.OgcXml.WfsException("InvalidValue", "count", "Count must be positive");

// RFC 7807 Problem Details
return ApiErrorResponse.ProblemDetails.InvalidParameter("Invalid CRS", "crs");
return ApiErrorResponse.ProblemDetails.NotFound("Collection 'roads' does not exist");
```

### For Existing Code (Optional Migration)

Replace old helper calls:

```csharp
// Old
using Honua.Server.Host.GeoservicesREST;
return GeoservicesRESTErrorHelper.BadRequest("message");

// New
using Honua.Server.Host.Utilities;
return ApiErrorResponse.Json.BadRequest("message");
```

Replace inline patterns:

```csharp
// Old
return Results.BadRequest(new { error = "Invalid input" });
return Results.NotFound(new { error = $"Dataset '{id}' not found." });

// New
return ApiErrorResponse.Json.BadRequestResult("Invalid input");
return ApiErrorResponse.Json.NotFound("Dataset", id);
```

## Benefits

### 1. Centralized Error Handling
- Single source of truth for all error response formats
- Easier to maintain and update error standards
- Consistent error formats across all APIs

### 2. Protocol-Aware Design
- Respects protocol-specific error formats (JSON vs XML)
- Maintains compliance with OGC standards
- Supports RFC 7807 Problem Details

### 3. Developer Experience
- IntelliSense-friendly nested structure
- Clear method names indicating protocol and error type
- Reduced boilerplate code

### 4. Maintainability
- One file to update for error format changes
- Easier to add new error types
- Backward compatible with existing code

## Statistics

### Consolidation Impact

- **Files created**: 1 (`ApiErrorResponse.cs`)
- **Files refactored**: 3 (backward compatibility wrappers)
- **Lines of consolidated logic**: ~650 lines
- **Error patterns unified**: 20+ distinct patterns
- **Inline error occurrences**: 200+ instances that can be migrated
- **Files using old helpers**: 50+ files

### Error Response Methods

#### JSON Errors (7 methods)
- BadRequest
- BadRequestResult
- NotFound (2 overloads)
- InternalServerError
- Conflict
- UnprocessableEntity

#### OGC XML Errors (2 methods)
- WmsException
- WfsException

#### Problem Details (14 methods)
- InvalidParameter
- NotFound
- Conflict
- ServerError
- NotAcceptable
- UnsupportedMediaType
- OperationNotSupported
- InvalidValue
- Forbidden
- Unauthorized
- InvalidCrs
- InvalidBbox
- InvalidDatetime
- LimitOutOfRange

**Total**: 23 error response methods + 15 OGC exception type constants

## Future Enhancements

### Phase 2: Migrate Inline Patterns
Replace the remaining ~200 inline error patterns with `ApiErrorResponse` calls.

### Phase 3: Enhanced Error Context
Add support for:
- Structured error details (validation errors, field-level errors)
- Error correlation IDs
- Help URLs for error documentation
- Internationalization support

### Phase 4: Error Analytics
- Track error frequency by type and endpoint
- Error response time metrics
- Error format compliance validation

## Testing Considerations

When testing error responses:

1. **JSON APIs**: Verify `{ error: "message" }` format
2. **OGC XML**: Validate against OGC schema (WMS 1.3.0, WFS 2.0.0)
3. **Problem Details**: Verify RFC 7807 compliance
4. **Status Codes**: Ensure correct HTTP status codes
5. **Content-Type**: Verify correct media types

## Related Files

### Core Implementation
- `/src/Honua.Server.Host/Utilities/ApiErrorResponse.cs` - Main implementation

### Backward Compatibility Wrappers
- `/src/Honua.Server.Host/GeoservicesREST/GeoservicesRESTErrorHelper.cs`
- `/src/Honua.Server.Host/Ogc/OgcExceptionHelper.cs`
- `/src/Honua.Server.Host/Ogc/OgcProblemDetails.cs`

### Documentation
- `/docs/features/API_ERROR_RESPONSE_CONSOLIDATION.md` - This document

## Standards Compliance

### OGC Standards
- **WMS 1.3.0**: ServiceExceptionReport format
- **WFS 2.0.0**: OWS Common ExceptionReport format
- **OGC API - Features**: RFC 7807 Problem Details with OGC exception types

### Web Standards
- **RFC 7807**: Problem Details for HTTP APIs
- **HTTP Status Codes**: Correct usage per RFC 7231

## Conclusion

The API Error Response consolidation provides a unified, protocol-aware approach to error handling across all Honua APIs. The nested structure (`ApiErrorResponse.Json`, `ApiErrorResponse.OgcXml`, `ApiErrorResponse.ProblemDetails`) makes it clear which error format is being used while maintaining backward compatibility with existing code.

This consolidation improves code maintainability, ensures consistent error formats, and provides a foundation for future enhancements to error handling and reporting.
