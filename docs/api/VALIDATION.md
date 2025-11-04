# API Input Validation

This document describes the comprehensive input validation implemented in the Honua API to ensure data integrity, security, and proper error handling.

## Overview

All API endpoints validate incoming requests using:
- **Data Annotation Attributes**: Standard and custom validation attributes on request DTOs
- **Validation Middleware**: Centralized exception handling for validation errors
- **RFC 7807 Problem Details**: Standardized error responses
- **Security Validation**: Protection against common attack vectors

## Validation Architecture

### Components

1. **ValidationMiddleware**: Global middleware that catches validation exceptions and converts them to RFC 7807 Problem Details responses
2. **ValidateModelStateAttribute**: Action filter that validates model state before controller execution
3. **Custom Validation Attributes**: Domain-specific validators for business rules
4. **IValidatableObject**: Interface for complex cross-property validation

### Configuration

Add validation to your application in `Program.cs`:

```csharp
// Add validation services
builder.Services.AddHonuaValidation();

var app = builder.Build();

// Add validation middleware (should be early in the pipeline)
app.UseHonuaValidation();
```

## Standard Error Response Format

All validation errors return RFC 7807 Problem Details:

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "instance": "/admin/vector-cache/jobs",
  "errors": {
    "serviceId": ["ServiceId is required."],
    "minZoom": ["MinZoom must be between 0 and 22."],
    "maxZoom": ["MaxZoom must be between 0 and 22."]
  }
}
```

## Custom Validation Attributes

### CollectionName

Validates collection/service/layer names contain only safe characters.

**Rules:**
- Only alphanumeric characters, underscores, and hyphens
- Maximum 255 characters
- Pattern: `^[a-zA-Z0-9_-]+$`

**Example:**
```csharp
[CollectionName]
public string ServiceId { get; set; }
```

**Valid:** `my-service`, `test_layer_123`, `service-name`
**Invalid:** `my service`, `service@name`, `../../../etc/passwd`

### Latitude

Validates latitude coordinates.

**Rules:**
- Must be between -90 and 90 degrees
- Type: `double`

**Example:**
```csharp
[Latitude]
public double Latitude { get; set; }
```

**Valid:** `0`, `45.5`, `90`, `-90`, `-45.5`
**Invalid:** `91`, `-91`, `180`

### Longitude

Validates longitude coordinates.

**Rules:**
- Must be between -180 and 180 degrees
- Type: `double`

**Example:**
```csharp
[Longitude]
public double Longitude { get; set; }
```

**Valid:** `0`, `180`, `-180`, `45.5`
**Invalid:** `181`, `-181`, `360`

### GeoJson

Validates GeoJSON geometry strings.

**Rules:**
- Must be valid JSON
- Must conform to GeoJSON specification
- Geometry must be valid (no self-intersections, etc.)

**Example:**
```csharp
[GeoJson]
public string? Geometry { get; set; }
```

**Valid:**
```json
{"type":"Point","coordinates":[0,0]}
{"type":"Polygon","coordinates":[[[0,0],[1,0],[1,1],[0,1],[0,0]]]}
```

**Invalid:**
```json
{}
{"type":"InvalidType","coordinates":[0,0]}
```

### ZoomLevel

Validates tile zoom levels.

**Rules:**
- Must be between 0 and 30
- Type: `int`

**Example:**
```csharp
[ZoomLevel]
public int MinZoom { get; set; }
```

**Valid:** `0`, `15`, `30`
**Invalid:** `-1`, `31`, `100`

### TileSize

Validates tile sizes.

**Rules:**
- Must be one of: 64, 128, 256, 512, 1024, 2048, 4096
- Type: `int`

**Example:**
```csharp
[TileSize]
public int TileSize { get; set; }
```

**Valid:** `256`, `512`, `1024`
**Invalid:** `100`, `0`, `8192`

### FileSize

Validates file sizes.

**Rules:**
- Must be non-negative
- Must not exceed maximum (configurable)
- Type: `long` (bytes)

**Example:**
```csharp
[FileSize(104857600)] // 100 MB max
public long ContentLength { get; set; }
```

### Iso8601DateTime

Validates ISO 8601 datetime strings or intervals.

**Rules:**
- Must be valid ISO 8601 format
- Supports single datetimes and intervals (start/end)

**Example:**
```csharp
[Iso8601DateTime]
public string? Datetime { get; set; }
```

**Valid:**
- `2024-01-01T00:00:00Z`
- `2024-01-01`
- `2024-01-01T00:00:00Z/2024-12-31T23:59:59Z` (interval)

**Invalid:**
- `not a date`
- `2024-13-01` (invalid month)
- `2024-01-01/` (incomplete interval)

### AllowedMimeTypes

Validates MIME types against an allowed list.

**Rules:**
- Must match one of the allowed types (case-insensitive)

**Example:**
```csharp
[AllowedMimeTypes("image/png", "image/jpeg", "image/webp")]
public string Format { get; set; }
```

**Valid:** `image/png`, `image/jpeg`, `IMAGE/PNG`
**Invalid:** `image/gif`, `application/json`

### NoPathTraversal

Validates strings do not contain path traversal sequences.

**Rules:**
- Rejects `../`, `..\\`, and URL-encoded variants
- Prevents directory traversal attacks

**Example:**
```csharp
[NoPathTraversal]
public string FilePath { get; set; }
```

**Valid:** `file.txt`, `safe/path/to/file.txt`
**Invalid:** `../../../etc/passwd`, `..\\..\\windows\\system32`

### SafeString

Validates strings are safe (no control characters, reasonable length).

**Rules:**
- Maximum length (configurable, default 1000)
- No control characters (except \n, \r, \t)
- Allows Unicode

**Example:**
```csharp
[SafeString(500)] // Max 500 characters
public string Description { get; set; }
```

**Valid:** Normal text, Unicode, newlines, tabs
**Invalid:** Null characters, other control characters, excessive length

## Request DTO Validation Rules

### VectorTilePreseedRequest

```csharp
public sealed class VectorTilePreseedRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1)]
    [CollectionName]
    public required string ServiceId { get; init; }

    [Required]
    [StringLength(255, MinimumLength = 1)]
    [CollectionName]
    public required string LayerId { get; init; }

    [Range(0, 22)]
    public int MinZoom { get; init; }

    [Range(0, 22)]
    public int MaxZoom { get; init; }

    [Iso8601DateTime]
    public string? Datetime { get; init; }

    public bool Overwrite { get; init; }
}
```

**Business Rules:**
- `MinZoom` cannot exceed `MaxZoom`

**Example Request:**
```json
{
  "serviceId": "my-service",
  "layerId": "my-layer",
  "minZoom": 0,
  "maxZoom": 10,
  "datetime": "2024-01-01T00:00:00Z",
  "overwrite": false
}
```

**Example Error Response:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "instance": "/admin/vector-cache/jobs",
  "errors": {
    "serviceId": ["ServiceId must contain only alphanumeric characters, underscores, and hyphens."],
    "minZoom": ["MinZoom must be between 0 and 22."],
    "maxZoom": ["MinZoom cannot exceed MaxZoom."]
  }
}
```

### RasterTilePreseedRequest

```csharp
public sealed class RasterTilePreseedRequest
{
    [Required]
    [MinLength(1)]
    public IReadOnlyList<string> DatasetIds { get; }

    [Required]
    [StringLength(100)]
    public string TileMatrixSetId { get; init; }

    [Range(0, 30)]
    public int? MinZoom { get; init; }

    [Range(0, 30)]
    public int? MaxZoom { get; init; }

    [StringLength(100)]
    public string? StyleId { get; init; }

    [Required]
    [AllowedMimeTypes("image/png", "image/jpeg", "image/webp", "image/avif")]
    public string Format { get; init; }

    [TileSize]
    public int TileSize { get; init; }
}
```

**Business Rules:**
- `MinZoom` cannot exceed `MaxZoom`
- `TileMatrixSetId` must be a supported matrix set

### DataIngestionRequest

```csharp
public sealed record DataIngestionRequest(
    [Required]
    [StringLength(255, MinimumLength = 1)]
    [RegularExpression(@"^[a-zA-Z0-9_-]+$")]
    string ServiceId,

    [Required]
    [StringLength(255, MinimumLength = 1)]
    [RegularExpression(@"^[a-zA-Z0-9_-]+$")]
    string LayerId,

    [Required]
    [StringLength(4096, MinimumLength = 1)]
    string SourcePath,

    [Required]
    [StringLength(4096, MinimumLength = 1)]
    string WorkingDirectory,

    [StringLength(500)]
    string? SourceFileName,

    [StringLength(100)]
    string? ContentType,

    bool Overwrite
);
```

## Security Validation

The validation system includes protection against common attack vectors:

### SQL Injection

**Protection:**
- `CollectionName` attribute rejects SQL keywords and special characters
- Only allows alphanumeric, underscore, and hyphen

**Rejected:**
- `'; DROP TABLE users; --`
- `1' OR '1'='1`
- `admin'--`

### Path Traversal

**Protection:**
- `NoPathTraversal` attribute detects directory traversal attempts
- Rejects `../`, `..\\`, and encoded variants

**Rejected:**
- `../../../etc/passwd`
- `..\\..\\..\\windows\\system32`
- `%2e%2e/%2e%2e/etc/passwd`

### Cross-Site Scripting (XSS)

**Protection:**
- `CollectionName` rejects HTML/JavaScript
- `SafeString` allows Unicode but rejects control characters

**Rejected:**
- `<script>alert('xss')</script>`
- `javascript:alert('xss')`

### Denial of Service (DoS)

**Protection:**
- String length limits prevent memory exhaustion
- File size limits prevent storage exhaustion
- Zoom level limits prevent excessive tile generation

**Limits:**
- Collection names: 255 characters
- File paths: 4096 characters
- File uploads: Configurable (default 1 GB)
- Safe strings: Configurable (default 1000 characters)

### Control Characters

**Protection:**
- `SafeString` rejects control characters (except \n, \r, \t)
- Prevents terminal injection and log injection

**Rejected:**
- Null character (`\u0000`)
- Bell (`\u0007`)
- Escape (`\u001B`)

## Integration with Endpoints

### Minimal API Endpoints

Validation is automatic for minimal API endpoints:

```csharp
group.MapPost("", async (VectorTilePreseedRequest request, IService service) =>
{
    // Request is already validated by the framework
    var result = await service.ProcessAsync(request);
    return Results.Ok(result);
});
```

### Controller Actions

For controller actions, validation is automatic:

```csharp
[HttpPost]
public async Task<IActionResult> CreateJob([FromBody] VectorTilePreseedRequest request)
{
    // ModelState is automatically validated
    // ValidationMiddleware catches exceptions
    var result = await _service.ProcessAsync(request);
    return Ok(result);
}
```

### Manual Validation

For custom validation logic:

```csharp
var validationResults = new List<ValidationResult>();
var context = new ValidationContext(request);

if (!Validator.TryValidateObject(request, context, validationResults, validateAllProperties: true))
{
    var errors = validationResults
        .GroupBy(r => r.MemberNames.FirstOrDefault() ?? "")
        .ToDictionary(g => g.Key, g => g.Select(r => r.ErrorMessage ?? "").ToArray());

    throw new ValidationException(errors);
}
```

## Testing Validation

Example test for validation:

```csharp
[Fact]
public void VectorTilePreseedRequest_InvalidServiceId_FailsValidation()
{
    var request = new VectorTilePreseedRequest
    {
        ServiceId = "invalid service@name",
        LayerId = "test-layer",
        MinZoom = 0,
        MaxZoom = 10
    };

    var results = new List<ValidationResult>();
    var context = new ValidationContext(request);
    var isValid = Validator.TryValidateObject(request, context, results, validateAllProperties: true);

    Assert.False(isValid);
    Assert.Contains(results, r => r.MemberNames.Contains("ServiceId"));
}
```

## Best Practices

1. **Always validate user input**: Never trust client data
2. **Use specific error messages**: Help users understand what's wrong
3. **Validate at multiple layers**: DTOs, business logic, and persistence
4. **Log validation failures**: Monitor for potential attacks
5. **Keep limits reasonable**: Balance security with usability
6. **Test edge cases**: Empty strings, null values, extreme lengths
7. **Document validation rules**: Include in API documentation

## Migration Guide

### Updating Existing Code

If you have existing requests without validation:

1. Add validation attributes to your DTO:
```csharp
public class MyRequest
{
    [Required]
    [StringLength(255)]
    [CollectionName]
    public string Name { get; set; }
}
```

2. Implement `IValidatableObject` for complex validation:
```csharp
public class MyRequest : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate > EndDate)
        {
            yield return new ValidationResult(
                "StartDate cannot be after EndDate.",
                new[] { nameof(StartDate), nameof(EndDate) });
        }
    }
}
```

3. Update tests to expect validation errors:
```csharp
[Fact]
public async Task InvalidRequest_ReturnsValidationError()
{
    var response = await _client.PostAsJsonAsync("/api/endpoint", invalidRequest);
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

    var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
    Assert.NotNull(problem);
    Assert.Contains("fieldName", problem.Errors.Keys);
}
```

## See Also

- [RFC 7807 - Problem Details for HTTP APIs](https://tools.ietf.org/html/rfc7807)
- [Data Annotations in ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/mvc/models/validation)
- [OWASP Input Validation Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Input_Validation_Cheat_Sheet.html)
