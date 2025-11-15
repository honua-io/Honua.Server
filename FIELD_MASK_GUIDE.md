# Field Mask Support for Partial Responses

## Overview

Field masking enables clients to request only specific fields in API responses, following the **Google API Design Guide AIP-161** standard for partial responses. This feature reduces bandwidth usage, improves performance, and provides better control over data exposure.

## Quick Start

### 1. Enable Field Mask Support

Add field mask support to your ASP.NET Core application in `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddFieldMaskSupport();  // Add this line

var app = builder.Build();
app.MapControllers();
app.Run();
```

### 2. Apply to Controller Actions

Decorate controller methods with `[FieldMask]` attribute:

```csharp
[ApiController]
[Route("api/v1.0/[controller]")]
public class SharesController : ControllerBase
{
    [HttpGet("{id}")]
    [FieldMask]
    public async Task<ActionResult<ShareDto>> GetShare(string id)
    {
        var share = await _service.GetShareAsync(id);
        return Ok(share);
    }

    [HttpGet]
    [FieldMask]
    public async Task<ActionResult<PagedResponse<ShareDto>>> GetShares(
        [FromQuery] int pageSize = 20,
        [FromQuery] string? pageToken = null)
    {
        var shares = await _service.GetSharesAsync(pageSize, pageToken);
        return Ok(shares);
    }
}
```

### 3. Use in API Requests

Clients can now request specific fields using the `fields` query parameter:

```bash
# Request only specific fields
GET /api/v1.0/shares/abc123?fields=id,token,permission

# Response (only requested fields):
{
  "id": "abc123",
  "token": "xyz789",
  "permission": "view"
}
```

## Field Mask Syntax

### Simple Field Selection

Request specific top-level fields:

```bash
GET /api/v1.0/users/123?fields=id,name,email
```

Response:
```json
{
  "id": "123",
  "name": "John Doe",
  "email": "john@example.com"
}
```

### Nested Field Selection

Request specific nested object properties:

```bash
GET /api/v1.0/shares/abc?fields=id,owner.name,owner.email,metadata.tags
```

Response:
```json
{
  "id": "abc",
  "owner": {
    "name": "John Doe",
    "email": "john@example.com"
  },
  "metadata": {
    "tags": ["geo", "public"]
  }
}
```

### Array Item Selection

Apply field masks to array items using parentheses notation:

```bash
GET /api/v1.0/shares?fields=items(id,name,createdAt),total,nextPageToken
```

Response:
```json
{
  "items": [
    { "id": "1", "name": "Share 1", "createdAt": "2025-01-15T10:00:00Z" },
    { "id": "2", "name": "Share 2", "createdAt": "2025-01-15T11:00:00Z" }
  ],
  "total": 100,
  "nextPageToken": "abc123"
}
```

### Wildcard Selection

Request all fields (same as omitting the parameter):

```bash
GET /api/v1.0/shares/abc?fields=*
```

## Advanced Usage

### Custom Query Parameter Name

You can customize the query parameter name:

```csharp
[HttpGet("{id}")]
[FieldMask(QueryParameterName = "select")]
public async Task<ActionResult<ShareDto>> GetShare(string id)
{
    var share = await _service.GetShareAsync(id);
    return Ok(share);
}

// Usage: GET /api/v1.0/shares/abc?select=id,name
```

### Disable Field Masking for Specific Endpoints

```csharp
[HttpGet("{id}")]
[FieldMask(Enabled = false)]
public async Task<ActionResult<ShareDto>> GetShare(string id)
{
    // Field masking is disabled for this endpoint
    var share = await _service.GetShareAsync(id);
    return Ok(share);
}
```

### Programmatic Field Mask Application

You can also use the `FieldMaskHelper` directly in your code:

```csharp
using Honua.Server.Host.Utilities;

var user = new User
{
    Id = "123",
    Name = "John",
    Email = "john@example.com",
    Password = "secret"
};

var masked = FieldMaskHelper.ApplyFieldMask(
    user,
    new[] { "id", "name", "email" }
);

// Result: { "id": "123", "name": "John", "email": "john@example.com" }
// Password field is excluded
```

### Working with JSON Strings

```csharp
var json = "{\"id\":\"123\",\"name\":\"John\",\"email\":\"john@example.com\",\"password\":\"secret\"}";
var masked = FieldMaskHelper.ApplyFieldMaskToJson(
    json,
    new[] { "id", "name" }
);

// Result: {"id":"123","name":"John"}
```

## Common Use Cases

### Mobile Applications

Request only essential fields for list views to reduce bandwidth:

```bash
GET /api/v1.0/shares?fields=items(id,name,thumbnail),nextPageToken
```

### Webhook Integrations

Request only relevant event data:

```bash
GET /api/v1.0/events/123?fields=id,type,status,updatedAt
```

### Embedding Related Data

Fetch main object with selected related data to avoid N+1 queries:

```bash
GET /api/v1.0/shares/abc?fields=id,name,owner.name,owner.avatar
```

### Security-Sensitive Endpoints

Prevent accidental exposure of sensitive fields:

```csharp
// Service layer returns full user object with sensitive fields
var user = await _userService.GetUserAsync(id);

// Client requests only safe fields
// GET /api/v1.0/users/123?fields=id,name,email
// Response excludes password, securityToken, etc.
```

## Performance Considerations

### Database-Level Projection (Recommended)

For optimal performance, combine field masks with database-level projections:

```csharp
[HttpGet("{id}")]
[FieldMask]
public async Task<ActionResult<ShareDto>> GetShare(
    string id,
    [FromQuery] string? fields = null)
{
    // Parse fields to determine which properties to fetch from database
    var includeOwner = fields?.Contains("owner") ?? true;
    var includeMetadata = fields?.Contains("metadata") ?? true;

    // Only fetch requested fields from database
    var share = await _repository.GetShareAsync(id, includeOwner, includeMetadata);

    return Ok(share);
}
```

### Performance Characteristics

| Operation | Typical Overhead | Notes |
|-----------|------------------|-------|
| No field mask | ~0.1ms | Filter passes through with minimal overhead |
| First request (cache miss) | ~2-3ms | Field mask parsing and caching |
| Subsequent requests (cache hit) | ~1-2ms | Uses cached field set |
| Large payloads (>100KB) | ~5-10ms | Streaming JSON processing |

### Caching

Field mask parsing is automatically cached to minimize overhead:

```csharp
// First request: parses and caches field set
GET /api/v1.0/shares/1?fields=id,name,email

// Subsequent requests with same fields: O(1) cache lookup
GET /api/v1.0/shares/2?fields=id,name,email
GET /api/v1.0/shares/3?fields=id,name,email
```

### Cache Management

```csharp
// Clear field mask cache (if needed)
FieldMaskHelper.ClearCache();

// Get current cache size
var cacheSize = FieldMaskHelper.GetCacheSize();
```

## Security Considerations

### Authorization Still Required

Field masks do NOT bypass authorization. Always protect sensitive fields at the service/repository layer:

```csharp
[HttpGet("{id}")]
[FieldMask]
[Authorize]
public async Task<ActionResult<UserDto>> GetUser(string id)
{
    // Authorization check
    if (!await _authService.CanAccessUser(User, id))
    {
        return Forbid();
    }

    // Service layer should filter sensitive fields based on user permissions
    var user = await _userService.GetUserAsync(id, User);

    // Field mask further reduces response based on client request
    return Ok(user);
}
```

### Prevent Information Disclosure

Use DTOs instead of domain entities to control what fields are exposed:

```csharp
// ❌ Bad - exposes internal domain model
[HttpGet("{id}")]
[FieldMask]
public async Task<ActionResult<User>> GetUser(string id)
{
    var user = await _repository.GetUserAsync(id);
    return Ok(user); // May expose sensitive internal fields
}

// ✅ Good - uses DTO to control exposure
[HttpGet("{id}")]
[FieldMask]
public async Task<ActionResult<UserDto>> GetUser(string id)
{
    var user = await _repository.GetUserAsync(id);
    var dto = _mapper.Map<UserDto>(user);
    return Ok(dto); // Only exposes safe DTO fields
}
```

### Invalid Field Names

Invalid or non-existent field names are silently ignored (fail-safe behavior):

```bash
# Request includes invalid field "invalidField"
GET /api/v1.0/shares/abc?fields=id,name,invalidField

# Response only includes valid fields
{
  "id": "abc",
  "name": "My Share"
}
```

## Error Handling

The field mask filter is designed to be fail-safe:

1. **Invalid field names**: Silently ignored
2. **Malformed field masks**: Returns complete response
3. **JSON serialization errors**: Logged as warning, returns original response
4. **Non-ObjectResult responses**: Passed through unchanged

## Testing

### Unit Test Example

```csharp
using Xunit;
using System.Text.Json;
using Honua.Server.Host.Utilities;

public class FieldMaskTests
{
    [Fact]
    public void ApplyFieldMask_WithSimpleFields_ReturnsOnlyRequestedFields()
    {
        // Arrange
        var user = new User
        {
            Id = "123",
            Name = "John",
            Email = "john@example.com",
            Password = "secret"
        };

        // Act
        var masked = FieldMaskHelper.ApplyFieldMask(
            user,
            new[] { "id", "name", "email" }
        );
        var json = JsonSerializer.Serialize(masked);
        var result = JsonSerializer.Deserialize<JsonElement>(json);

        // Assert
        Assert.True(result.TryGetProperty("id", out _));
        Assert.True(result.TryGetProperty("name", out _));
        Assert.True(result.TryGetProperty("email", out _));
        Assert.False(result.TryGetProperty("password", out _));
    }

    [Fact]
    public void ApplyFieldMask_WithNestedFields_ReturnsNestedStructure()
    {
        // Arrange
        var share = new Share
        {
            Id = "abc",
            Name = "My Share",
            Owner = new User { Name = "John", Email = "john@example.com" }
        };

        // Act
        var masked = FieldMaskHelper.ApplyFieldMask(
            share,
            new[] { "id", "owner.name" }
        );
        var json = JsonSerializer.Serialize(masked);
        var result = JsonSerializer.Deserialize<JsonElement>(json);

        // Assert
        Assert.True(result.TryGetProperty("id", out _));
        Assert.True(result.TryGetProperty("owner", out var owner));
        Assert.True(owner.TryGetProperty("name", out _));
        Assert.False(owner.TryGetProperty("email", out _));
    }
}
```

### Integration Test Example

```csharp
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http;
using System.Text.Json;

public class FieldMaskIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public FieldMaskIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetShare_WithFieldMask_ReturnsOnlyRequestedFields()
    {
        // Act
        var response = await _client.GetAsync("/api/v1.0/shares/abc?fields=id,token");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var share = JsonSerializer.Deserialize<JsonElement>(json);

        // Assert
        Assert.True(share.TryGetProperty("id", out _));
        Assert.True(share.TryGetProperty("token", out _));
        Assert.False(share.TryGetProperty("permission", out _));
        Assert.False(share.TryGetProperty("owner", out _));
    }
}
```

## API Guidelines Compliance

This implementation follows industry-standard API design guidelines:

### Google API Design Guide (AIP-161)

- **Specification**: https://google.aip.dev/161
- **Field mask syntax**: Comma-separated field paths with dot notation
- **Wildcard support**: `*` to request all fields
- **Array notation**: `items(field1,field2)` for array elements

### Microsoft Azure REST API Guidelines

- **Partial responses**: Compatible with Azure's partial response pattern
- **Query parameter**: Uses `fields` parameter (consistent with Azure APIs)
- **Error handling**: Fail-safe behavior for invalid field names

### JSON:API Sparse Fieldsets

- **Concept alignment**: Similar to JSON:API sparse fieldset concept
- **Field selection**: Client-driven field selection for bandwidth optimization
- **Nested resources**: Support for including related resource fields

## Implementation Files

The field mask implementation consists of three core files in the Honua.Server.Host project:

1. **FieldMaskAttribute.cs** (`src/Honua.Server.Host/Filters/FieldMaskAttribute.cs`)
   - Attribute to enable field masking on controller actions
   - Configurable query parameter name
   - Enable/disable flag for specific endpoints

2. **FieldMaskHelper.cs** (`src/Honua.Server.Host/Utilities/FieldMaskHelper.cs`)
   - Core field mask application logic
   - Supports simple fields, nested paths, and array notation
   - Includes caching for performance optimization
   - Thread-safe implementation

3. **FieldMaskActionFilter.cs** (`src/Honua.Server.Host/Filters/FieldMaskActionFilter.cs`)
   - ASP.NET Core action filter for automatic field mask application
   - Integrates with MVC pipeline
   - Fail-safe error handling
   - Comprehensive logging

## Troubleshooting

### Field mask not applied

**Problem**: Response includes all fields even with `?fields=id,name` parameter.

**Solutions**:
1. Ensure `[FieldMask]` attribute is applied to the controller action
2. Verify field mask support is registered: `.AddFieldMaskSupport()`
3. Check that the result is `ObjectResult` (e.g., `return Ok(data)`)
4. Review logs for warnings about JSON serialization errors

### Case sensitivity issues

**Problem**: Fields with different casing not working (e.g., `createdAt` vs `CreatedAt`).

**Solution**: Field names are case-insensitive by default. Use the property names as they appear in the JSON response (typically camelCase).

### Nested fields not working

**Problem**: `?fields=owner.name` returns entire owner object.

**Solution**: Ensure you're using dot notation correctly: `owner.name` (not `owner->name` or `owner/name`).

### Performance issues

**Problem**: Field masking adds significant latency.

**Solutions**:
1. Combine with database-level projections (don't fetch unnecessary data)
2. Monitor cache size: `FieldMaskHelper.GetCacheSize()`
3. Clear cache periodically if needed: `FieldMaskHelper.ClearCache()`
4. Consider disabling for very large payloads (>1MB)

## Best Practices

### 1. Use DTOs for API Responses

```csharp
// ✅ Good
public class ShareDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public UserDto Owner { get; set; }
    // Only safe, client-facing properties
}

[HttpGet("{id}")]
[FieldMask]
public async Task<ActionResult<ShareDto>> GetShare(string id)
{
    var share = await _repository.GetShareAsync(id);
    var dto = _mapper.Map<ShareDto>(share);
    return Ok(dto);
}
```

### 2. Combine with Database Projections

```csharp
[HttpGet("{id}")]
[FieldMask]
public async Task<ActionResult<ShareDto>> GetShare(
    string id,
    [FromQuery] string? fields = null)
{
    // Parse fields to optimize database query
    var includeOwner = fields?.Contains("owner") ?? true;

    var share = await _repository.GetShareAsync(id, includeOwner);
    var dto = _mapper.Map<ShareDto>(share);
    return Ok(dto);
}
```

### 3. Document Available Fields

```csharp
/// <summary>
/// Gets a share by ID.
/// </summary>
/// <param name="id">The share ID.</param>
/// <param name="fields">Optional comma-separated field list.
/// Available fields: id, name, token, permission, owner.name, owner.email, metadata.tags, createdAt, updatedAt
/// </param>
[HttpGet("{id}")]
[FieldMask]
public async Task<ActionResult<ShareDto>> GetShare(
    string id,
    [FromQuery] string? fields = null)
{
    var share = await _service.GetShareAsync(id);
    return Ok(share);
}
```

### 4. Test Common Field Combinations

```csharp
[Theory]
[InlineData("id,name")]
[InlineData("id,name,owner.name")]
[InlineData("*")]
public async Task GetShare_WithVariousFieldMasks_ReturnsCorrectFields(string fields)
{
    var response = await _client.GetAsync($"/api/v1.0/shares/abc?fields={fields}");
    response.EnsureSuccessStatusCode();
    // Assert expected fields are present
}
```

## Examples Summary

### Simple Field Selection
```bash
GET /api/v1.0/users/123?fields=id,name,email
→ Returns: { "id": "123", "name": "John Doe", "email": "john@example.com" }
```

### Nested Fields
```bash
GET /api/v1.0/shares/abc?fields=id,owner.name,owner.email
→ Returns: { "id": "abc", "owner": { "name": "John", "email": "john@example.com" } }
```

### Array Items
```bash
GET /api/v1.0/shares?fields=items(id,name),total
→ Returns: { "items": [{ "id": "1", "name": "Share 1" }], "total": 100 }
```

### Wildcard
```bash
GET /api/v1.0/shares/abc?fields=*
→ Returns: Complete share object with all fields
```

## References

- **Google API Design Guide AIP-161**: https://google.aip.dev/161
- **Microsoft Azure REST API Guidelines**: https://github.com/microsoft/api-guidelines
- **System.Text.Json Documentation**: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/overview

---

**Copyright (c) 2025 HonuaIO**
Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
