# OData-Style Filtering for REST APIs

Lightweight OData-style filtering implementation for REST API endpoints using the `?filter=` query parameter.

## Overview

This implementation provides a secure, performant way to filter collections in REST APIs without requiring the full OData library. It supports common filtering scenarios while maintaining security and performance.

## Quick Start

### 1. Register the Filter (Program.cs)

```csharp
builder.Services.AddControllers(options =>
{
    options.Filters.Add<FilterQueryActionFilter>();
});
```

### 2. Decorate Controller Actions

```csharp
[HttpGet]
[FilterQuery(EntityType = typeof(Share), AllowedProperties = new[] { "createdAt", "permission", "isActive" })]
public async Task<ActionResult<PagedResponse<Share>>> GetShares([FromQuery] string? filter)
{
    var query = dbContext.Shares.AsQueryable();

    // Filter applied by action filter, accessible via HttpContext.Items
    if (HttpContext.Items.TryGetValue("ParsedFilter", out var parsedFilter))
    {
        query = query.ApplyFilter((FilterExpression)parsedFilter);
    }

    var shares = await query.ToListAsync();
    return Ok(new PagedResponse<Share> { Items = shares });
}
```

### 3. Use in Client Requests

```http
GET /api/shares?filter=createdAt gt 2025-01-01 and status eq 'active'
GET /api/shares?filter=name contains 'project'
GET /api/shares?filter=isActive eq true
```

## Supported Operators

### Comparison Operators

| Operator | Description | Example |
|----------|-------------|---------|
| `eq` | Equal to | `status eq 'active'` |
| `ne` | Not equal to | `status ne 'deleted'` |
| `gt` | Greater than | `age gt 18` |
| `ge` | Greater than or equal | `age ge 18` |
| `lt` | Less than | `price lt 100` |
| `le` | Less than or equal | `price le 100` |

### Logical Operators

| Operator | Description | Example |
|----------|-------------|---------|
| `and` | Logical AND | `status eq 'active' and age gt 18` |
| `or` | Logical OR | `status eq 'active' or status eq 'pending'` |
| `not` | Logical NOT | `not (isDeleted eq true)` |

### String Functions

| Function | Description | Example |
|----------|-------------|---------|
| `contains` | Contains substring (case-insensitive) | `name contains 'test'` |
| `startswith` | Starts with prefix (case-insensitive) | `email startswith 'admin'` |
| `endswith` | Ends with suffix (case-insensitive) | `filename endswith '.pdf'` |

## Filter Examples

### Simple Comparisons

```http
# Equality
GET /api/users?filter=status eq 'active'

# Numeric comparison
GET /api/products?filter=price gt 100

# Date comparison
GET /api/orders?filter=createdAt ge 2025-01-01

# Boolean comparison
GET /api/features?filter=isEnabled eq true
```

### Logical Combinations

```http
# AND - both conditions must be true
GET /api/shares?filter=createdAt gt 2025-01-01 and status eq 'active'

# OR - at least one condition must be true
GET /api/users?filter=status eq 'active' or status eq 'pending'

# NOT - inverts the condition
GET /api/items?filter=not (isDeleted eq true)
```

### String Functions

```http
# Contains - case-insensitive substring match
GET /api/projects?filter=name contains 'infrastructure'

# Starts with - case-insensitive prefix match
GET /api/users?filter=email startswith 'admin'

# Ends with - case-insensitive suffix match
GET /api/files?filter=filename endswith '.json'
```

### Complex Expressions

```http
# Combining multiple operators with precedence
GET /api/tasks?filter=(status eq 'active' or status eq 'pending') and priority gt 5

# Multiple AND conditions
GET /api/events?filter=startDate ge 2025-01-01 and endDate le 2025-12-31 and isPublic eq true

# Nested NOT expressions
GET /api/users?filter=not (status eq 'deleted' or status eq 'suspended')
```

## Supported Data Types

### Primitives

- **String**: `'value'` or `"value"` (quotes required)
- **Integer**: `42`, `-10`
- **Decimal**: `3.14`, `-2.5`
- **Boolean**: `true`, `false`
- **Null**: `null`

### Dates and Times

```http
# ISO 8601 format
GET /api/orders?filter=createdAt gt 2025-01-01T00:00:00Z

# Date only
GET /api/orders?filter=createdAt ge 2025-01-01

# DateTime with timezone
GET /api/events?filter=startDate lt 2025-12-31T23:59:59+00:00
```

### Enums

```http
# Case-insensitive enum matching
GET /api/shares?filter=permission eq 'ReadWrite'
GET /api/shares?filter=permission eq 'readwrite'
```

### GUIDs

```http
GET /api/resources?filter=ownerId eq '123e4567-e89b-12d3-a456-426614174000'
```

## Operator Precedence

From highest to lowest:

1. **Parentheses** `( )`
2. **String functions** `contains`, `startswith`, `endswith`
3. **Comparison operators** `eq`, `ne`, `gt`, `ge`, `lt`, `le`
4. **Logical NOT** `not`
5. **Logical AND** `and`
6. **Logical OR** `or`

### Precedence Examples

```http
# Without parentheses (AND has higher precedence than OR)
# Equivalent to: (status eq 'active' and priority gt 5) or isUrgent eq true
GET /api/tasks?filter=status eq 'active' and priority gt 5 or isUrgent eq true

# With explicit parentheses for clarity
GET /api/tasks?filter=(status eq 'active' and priority gt 5) or isUrgent eq true

# Override precedence with parentheses
# Equivalent to: status eq 'active' and (priority gt 5 or isUrgent eq true)
GET /api/tasks?filter=status eq 'active' and (priority gt 5 or isUrgent eq true)
```

## Security Considerations

### Property Whitelisting

Only properties listed in `AllowedProperties` can be filtered:

```csharp
[FilterQuery(
    EntityType = typeof(User),
    AllowedProperties = new[] { "email", "createdAt", "isActive" }
)]
```

Attempting to filter on unauthorized properties returns 400 Bad Request:

```http
GET /api/users?filter=password eq 'secret'
→ 400 Bad Request: "Property 'password' is not allowed in filter expressions"
```

### Complexity Limits

Default security limits:

- **Max tokens**: 50 tokens per filter expression
- **Max nesting depth**: 10 levels of parentheses
- **Max conditions**: 10 conditions (configurable per endpoint)

```csharp
[FilterQuery(
    EntityType = typeof(Share),
    AllowedProperties = new[] { "createdAt", "status" },
    MaxConditions = 20  // Allow more complex queries for this endpoint
)]
```

### SQL Injection Prevention

- All property names are validated against the entity type
- All values are parameterized in generated SQL
- No raw SQL injection is possible

### Rate Limiting

Consider implementing rate limiting for complex filter queries:

```csharp
[RateLimit(PermitLimit = 10, Window = 60)]  // 10 requests per minute
[FilterQuery(EntityType = typeof(Share), AllowedProperties = new[] { "createdAt", "status" })]
public async Task<ActionResult<PagedResponse<Share>>> GetShares([FromQuery] string? filter)
{
    // ...
}
```

## Performance Optimization

### Index Your Filtered Properties

```sql
-- PostgreSQL example
CREATE INDEX idx_shares_created_at ON shares(created_at);
CREATE INDEX idx_shares_status ON shares(status);
CREATE INDEX idx_shares_is_active ON shares(is_active);

-- Composite index for common filter combinations
CREATE INDEX idx_shares_status_created_at ON shares(status, created_at);
```

### Avoid String Functions on Large Tables

String functions (`contains`, `startswith`, `endswith`) may not use indexes efficiently:

```http
# May be slow on large tables
GET /api/users?filter=email contains '@example.com'

# Consider full-text search instead
GET /api/users?search=example.com
```

### Use Projection to Reduce Data Transfer

```csharp
if (HttpContext.Items.TryGetValue("ParsedFilter", out var parsedFilter))
{
    query = query.ApplyFilter((FilterExpression)parsedFilter);
}

// Project to DTO to reduce data transfer
var shares = await query
    .Select(s => new ShareDto
    {
        Id = s.Id,
        Name = s.Name,
        CreatedAt = s.CreatedAt
    })
    .ToListAsync();
```

### Monitor Query Performance

```csharp
var stopwatch = Stopwatch.StartNew();
var results = await query.ToListAsync();
stopwatch.Stop();

if (stopwatch.ElapsedMilliseconds > 1000)
{
    logger.LogWarning(
        "Slow filter query: {Filter} took {ElapsedMs}ms",
        filterString,
        stopwatch.ElapsedMilliseconds);
}
```

## Error Handling

### Parse Errors

```http
GET /api/shares?filter=status eq
→ 400 Bad Request
{
  "status": 400,
  "title": "Invalid filter expression",
  "detail": "Expected value after operator 'eq'",
  "instance": "0HMVD8K3N1234"
}
```

### Property Validation Errors

```http
GET /api/shares?filter=invalidProperty eq 'value'
→ 400 Bad Request
{
  "status": 400,
  "title": "Invalid filter property",
  "detail": "Property 'invalidProperty' is not allowed in filter expressions. Allowed properties: createdAt, status, isActive"
}
```

### Type Conversion Errors

```http
GET /api/shares?filter=createdAt eq 'invalid-date'
→ 400 Bad Request
{
  "status": 400,
  "title": "Invalid filter expression",
  "detail": "Cannot convert value 'invalid-date' to type 'DateTime'"
}
```

## Advanced Usage

### Nested Properties

Enable nested properties to filter on related entities:

```csharp
[FilterQuery(
    EntityType = typeof(Order),
    AllowedProperties = new[] { "customer.email", "customer.name", "totalAmount" },
    AllowNestedProperties = true
)]
```

```http
GET /api/orders?filter=customer.email startswith 'admin'
```

**Note**: Nested properties generate JOINs, which may impact performance.

### Combining with Pagination

```csharp
[HttpGet]
[FilterQuery(EntityType = typeof(Share), AllowedProperties = new[] { "createdAt", "status" })]
public async Task<ActionResult<PagedResponse<Share>>> GetShares(
    [FromQuery] string? filter,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20)
{
    var query = dbContext.Shares.AsQueryable();

    // Apply filter
    if (HttpContext.Items.TryGetValue("ParsedFilter", out var parsedFilter))
    {
        query = query.ApplyFilter((FilterExpression)parsedFilter);
    }

    // Apply pagination
    var totalCount = await query.CountAsync();
    var items = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    return Ok(new PagedResponse<Share>
    {
        Items = items,
        TotalCount = totalCount,
        Page = page,
        PageSize = pageSize
    });
}
```

### Combining with Sorting

```csharp
[HttpGet]
[FilterQuery(EntityType = typeof(Share), AllowedProperties = new[] { "createdAt", "status" })]
public async Task<ActionResult<PagedResponse<Share>>> GetShares(
    [FromQuery] string? filter,
    [FromQuery] string? orderBy = "createdAt",
    [FromQuery] bool descending = false)
{
    var query = dbContext.Shares.AsQueryable();

    // Apply filter
    if (HttpContext.Items.TryGetValue("ParsedFilter", out var parsedFilter))
    {
        query = query.ApplyFilter((FilterExpression)parsedFilter);
    }

    // Apply sorting
    query = orderBy?.ToLower() switch
    {
        "name" => descending ? query.OrderByDescending(s => s.Name) : query.OrderBy(s => s.Name),
        "createdat" => descending ? query.OrderByDescending(s => s.CreatedAt) : query.OrderBy(s => s.CreatedAt),
        _ => query.OrderByDescending(s => s.CreatedAt)
    };

    var shares = await query.ToListAsync();
    return Ok(new PagedResponse<Share> { Items = shares });
}
```

## Testing

### Unit Test Example

```csharp
[Fact]
public void Parser_Should_Parse_Simple_Equality()
{
    // Arrange
    var parser = new FilterExpressionParser();

    // Act
    var expression = parser.Parse("status eq 'active'");

    // Assert
    var comparison = Assert.IsType<ComparisonExpression>(expression);
    Assert.Equal("status", comparison.Property);
    Assert.Equal(ComparisonOperator.Eq, comparison.Operator);
    Assert.Equal("active", comparison.Value);
}

[Fact]
public void Parser_Should_Parse_Logical_And()
{
    // Arrange
    var parser = new FilterExpressionParser();

    // Act
    var expression = parser.Parse("status eq 'active' and age gt 18");

    // Assert
    var logical = Assert.IsType<LogicalExpression>(expression);
    Assert.Equal(LogicalOperator.And, logical.Operator);
}

[Fact]
public void Parser_Should_Throw_On_Invalid_Syntax()
{
    // Arrange
    var parser = new FilterExpressionParser();

    // Act & Assert
    var ex = Assert.Throws<FilterParseException>(() => parser.Parse("status eq"));
    Assert.Contains("Expected value", ex.Message);
}
```

### Integration Test Example

```csharp
[Fact]
public async Task GetShares_Should_Filter_By_Status()
{
    // Arrange
    var client = factory.CreateClient();

    // Act
    var response = await client.GetAsync("/api/shares?filter=status eq 'active'");

    // Assert
    response.EnsureSuccessStatusCode();
    var shares = await response.Content.ReadFromJsonAsync<PagedResponse<Share>>();
    Assert.All(shares!.Items, s => Assert.Equal("active", s.Status));
}
```

## OpenAPI/Swagger Documentation

Add filter documentation to Swagger:

```csharp
builder.Services.AddSwaggerGen(options =>
{
    options.OperationFilter<FilterQueryOperationFilter>();
});
```

Example implementation:

```csharp
public class FilterQueryOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var filterAttribute = context.MethodInfo
            .GetCustomAttributes(true)
            .OfType<FilterQueryAttribute>()
            .FirstOrDefault();

        if (filterAttribute == null)
            return;

        operation.Parameters ??= new List<OpenApiParameter>();
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "filter",
            In = ParameterLocation.Query,
            Description = $"OData-style filter expression. Allowed properties: {string.Join(", ", filterAttribute.AllowedProperties!)}",
            Required = false,
            Schema = new OpenApiSchema { Type = "string" },
            Example = new OpenApiString($"{filterAttribute.AllowedProperties![0]} eq 'value'")
        });
    }
}
```

## Best Practices

### 1. Only Allow Indexed Properties

```csharp
// Good - indexed properties
AllowedProperties = new[] { "id", "createdAt", "status" }

// Bad - computed or non-indexed properties
AllowedProperties = new[] { "fullName", "calculatedField" }
```

### 2. Use Meaningful Property Names

```csharp
// Good - client-friendly camelCase
AllowedProperties = new[] { "createdAt", "isActive", "status" }

// Bad - database column names
AllowedProperties = new[] { "created_at", "is_active", "status_id" }
```

### 3. Document Allowed Filters

```csharp
/// <summary>
/// Gets a paginated list of shares.
/// </summary>
/// <param name="filter">
/// Optional OData-style filter. Supported properties:
/// - createdAt: DateTime (ISO 8601)
/// - status: string (active, pending, deleted)
/// - isActive: boolean
/// </param>
[HttpGet]
[FilterQuery(EntityType = typeof(Share), AllowedProperties = new[] { "createdAt", "status", "isActive" })]
public async Task<ActionResult<PagedResponse<Share>>> GetShares([FromQuery] string? filter)
{
    // ...
}
```

### 4. Monitor and Log Slow Queries

```csharp
if (stopwatch.ElapsedMilliseconds > 1000)
{
    logger.LogWarning(
        "Slow filter query detected. Filter: {Filter}, Duration: {Duration}ms, ResultCount: {Count}",
        filterString,
        stopwatch.ElapsedMilliseconds,
        results.Count);
}
```

### 5. Set Appropriate Complexity Limits

```csharp
// Simple public API - strict limits
[FilterQuery(
    EntityType = typeof(PublicResource),
    AllowedProperties = new[] { "name", "createdAt" },
    MaxConditions = 5
)]

// Internal admin API - relaxed limits
[FilterQuery(
    EntityType = typeof(AdminResource),
    AllowedProperties = new[] { "id", "name", "status", "createdAt", "updatedAt" },
    MaxConditions = 20
)]
```

## Limitations

### Not Supported (by design)

- **Arithmetic operations**: `price mul 1.1 gt 100` - use database views instead
- **Date functions**: `year(createdAt) eq 2025` - filter on date ranges instead
- **Aggregations**: `count(items) gt 5` - use separate aggregation endpoints
- **Cross-entity queries**: `customer/orders/any(o: o.total gt 1000)` - use nested properties with caution

### Migration from Full OData

If you need more advanced OData features, consider using the official OData library:

```csharp
// Install: Microsoft.AspNetCore.OData
builder.Services.AddControllers().AddOData(opt =>
    opt.Select().Filter().OrderBy().Expand().SetMaxTop(100)
);
```

## Troubleshooting

### Filter Not Being Applied

Check that:
1. `FilterQueryActionFilter` is registered globally
2. Action has `[FilterQuery]` attribute
3. Controller reads from `HttpContext.Items["ParsedFilter"]`
4. `ApplyFilter()` extension method is called

### 400 Bad Request - Property Not Found

Ensure:
1. Property name matches entity property (case-insensitive)
2. Property is in `AllowedProperties` list
3. Property is public on the entity type

### Performance Issues

1. Add indexes on filtered properties
2. Avoid string functions on large tables
3. Use pagination to limit result sets
4. Monitor query execution times
5. Consider database query plan analysis

## Architecture

```
Client Request
    ↓
FilterQueryActionFilter (validates & parses filter)
    ↓
HttpContext.Items["ParsedFilter"] (stores FilterExpression)
    ↓
Controller Action (retrieves ParsedFilter)
    ↓
FilterQueryableExtensions.ApplyFilter() (converts to LINQ)
    ↓
Entity Framework Core (translates to SQL)
    ↓
Database (executes query)
```

## References

- [OData URL Conventions](http://docs.oasis-open.org/odata/odata/v4.01/odata-v4.01-part2-url-conventions.html)
- [RFC 7807 - Problem Details](https://tools.ietf.org/html/rfc7807)
- [Microsoft REST API Guidelines](https://github.com/microsoft/api-guidelines)
- [Entity Framework Core Query Filters](https://docs.microsoft.com/en-us/ef/core/querying/)
