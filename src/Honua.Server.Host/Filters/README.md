# SecureExceptionFilter

## Overview

The `SecureExceptionFilter` provides global exception handling for all API endpoints following the unified error handling architecture specified in `UNIFIED_ERROR_HANDLING_ARCHITECTURE.md`.

## Features

- **Automatic Exception Handling**: Catches all unhandled exceptions before they reach clients
- **Structured Logging**: Full exception details logged with structured format for diagnostics
- **Security Audit Logging**: Automatic security audit logs for sensitive endpoints
- **Sanitized Responses**: Returns ProblemDetails with sanitized messages to prevent information disclosure
- **Environment-Aware**: Verbose in Development, minimal in Production
- **Request Correlation**: Includes requestId for troubleshooting

## Registration

Add the filter globally in your `Program.cs` or service configuration:

```csharp
// In Program.cs or ServiceCollectionExtensions.cs
services.AddControllers(options =>
{
    options.Filters.Add<SecureExceptionFilter>();
});
```

The filter requires these services to be registered:
- `ILogger<SecureExceptionFilter>`
- `IHostEnvironment`
- `ISecurityAuditLogger`

## Exception Mapping

| Exception Type | HTTP Status | Response |
|---------------|-------------|----------|
| `ValidationException` | 400 Bad Request | ValidationProblemDetails with field-level errors |
| `UnauthorizedAccessException` | 401 Unauthorized | Generic unauthorized message |
| `ArgumentException` | 400 Bad Request | Sanitized exception message |
| `InvalidOperationException` | 400 Bad Request | Sanitized exception message |
| All other exceptions | 500 Internal Server Error | Generic error message |

## Response Format

All responses follow the [RFC 7807 Problem Details](https://tools.ietf.org/html/rfc7807) format:

### Validation Error (400)
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred",
  "status": 400,
  "errors": {
    "fieldName": ["Error message 1", "Error message 2"]
  },
  "requestId": "0HN1234567890",
  "timestamp": "2025-10-18T10:30:00Z"
}
```

### Generic Error - Development (500)
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.1",
  "title": "An error occurred while processing your request",
  "status": 500,
  "detail": "An unexpected error occurred. Check server logs for details.",
  "instance": "0HN1234567890",
  "requestId": "0HN1234567890",
  "timestamp": "2025-10-18T10:30:00Z",
  "exceptionType": "NullReferenceException"
}
```

### Generic Error - Production (500)
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.1",
  "title": "An error occurred while processing your request",
  "status": 500,
  "instance": "0HN1234567890",
  "requestId": "0HN1234567890",
  "timestamp": "2025-10-18T10:30:00Z"
}
```

## Sensitive Endpoints

The following endpoints trigger security audit logging when exceptions occur:

- All admin endpoints (`/admin/*`)
- Authentication controllers (controllers with "Auth" in name)
- User management controllers (controllers with "User" in name)
- Mutation operations (Create, Update, Delete, Post, Put, Patch actions)

## Message Sanitization

The filter automatically sanitizes error messages by removing:

- File paths (`C:\path\to\file` or `/var/log/file`)
- Connection strings (anything with `Server=`, `Database=`, `Password=`)
- SQL statements (`SELECT`, `INSERT`, `UPDATE`, `DELETE`)
- Stack trace fragments (lines starting with `at `)

## Usage Examples

### Controller with Expected Exceptions

```csharp
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    // Let the filter handle all exceptions
    [HttpGet("{id}")]
    public async Task<Product> GetProduct(int id)
    {
        // Throw ValidationException for validation errors
        if (id <= 0)
            throw new ValidationException("id", "Product ID must be positive");

        // Throw ArgumentException for invalid arguments
        if (id > 1000000)
            throw new ArgumentException("Product ID is out of range");

        // Any other exceptions are caught as 500 errors
        return await _productService.GetByIdAsync(id);
    }
}
```

### Controller with Manual Error Handling

```csharp
[HttpPost]
public async Task<IActionResult> CreateProduct(CreateProductRequest request)
{
    try
    {
        var product = await _productService.CreateAsync(request);
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }
    catch (DuplicateProductException ex)
    {
        // Handle specific business logic exceptions manually
        return Conflict(new ProblemDetails
        {
            Title = "Product already exists",
            Detail = $"A product with name '{request.Name}' already exists",
            Status = StatusCodes.Status409Conflict
        });
    }
    // All other exceptions are caught by SecureExceptionFilter
}
```

## Testing

To test the filter, verify:

1. **Exception Logging**: Check that exceptions are logged with structured format
2. **Security Audit**: Verify sensitive endpoints log to security audit
3. **Response Format**: Ensure ProblemDetails format is correct
4. **Message Sanitization**: Verify sensitive data is not exposed
5. **Environment Behavior**: Test different responses in Dev vs Prod

Example unit test:

```csharp
[Fact]
public void OnException_WithValidationException_Returns400WithErrors()
{
    // Arrange
    var filter = new SecureExceptionFilter(logger, environment, auditLogger);
    var validationErrors = new Dictionary<string, string[]>
    {
        ["email"] = new[] { "Email is required" }
    };
    var exception = new ValidationException(validationErrors);
    var context = CreateExceptionContext(exception);

    // Act
    filter.OnException(context);

    // Assert
    var result = Assert.IsType<ObjectResult>(context.Result);
    Assert.Equal(400, result.StatusCode);
    var problemDetails = Assert.IsType<ValidationProblemDetails>(result.Value);
    Assert.Equal("One or more validation errors occurred", problemDetails.Title);
    Assert.Contains("email", problemDetails.Errors.Keys);
}
```

## Integration with Existing Code

This filter is designed to work alongside:

- **Input Validation Filters**: Catches validation exceptions and formats them
- **Authorization Filters**: Catches UnauthorizedAccessException
- **Custom Business Logic**: Catches all unhandled exceptions

The filter integrates with the existing `ISecurityAuditLogger` for audit logging.

## Performance Considerations

- The filter only executes when an exception occurs
- Message sanitization uses compiled regex patterns for efficiency
- Structured logging provides minimal overhead
- Security audit logging is async to avoid blocking

## Security Notes

- Never expose exception messages directly - always sanitize
- Production environments should have `IsDevelopment()` return false
- Security audit logs should be monitored for patterns
- Use structured logging correlation IDs for troubleshooting

## Related Documentation

- `/home/mike/projects/HonuaIO/UNIFIED_ERROR_HANDLING_ARCHITECTURE.md` - Full architecture specification
- `Honua.Server.Host.Validation.ValidationException` - Custom validation exception
- `Honua.Server.Core.Logging.ISecurityAuditLogger` - Security audit interface
