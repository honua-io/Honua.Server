# OData-Style Filtering Implementation Summary

## Overview

A lightweight OData-style filtering system has been implemented for REST API endpoints using the `?filter=` query parameter. This implementation provides secure, performant filtering without requiring the full OData library dependency.

## Implementation Date

November 15, 2025

## Files Created

### Core Implementation Files

| File | Location | LOC | Purpose |
|------|----------|-----|---------|
| **FilterExpression.cs** | `/src/Honua.Server.Host/Filtering/` | 270 | Expression models and enums |
| **FilterExpressionParser.cs** | `/src/Honua.Server.Host/Filtering/` | 480 | Parser for OData filter syntax |
| **FilterQueryableExtensions.cs** | `/src/Honua.Server.Host/Filtering/` | 374 | IQueryable extension methods |
| **FilterQueryAttribute.cs** | `/src/Honua.Server.Host/Filters/` | 246 | Controller action attribute |
| **FilterQueryActionFilter.cs** | `/src/Honua.Server.Host/Filters/` | 379 | Action filter for request processing |

### Documentation Files

| File | Location | Purpose |
|------|----------|---------|
| **README.md** | `/src/Honua.Server.Host/Filtering/` | Comprehensive usage guide |
| **FilteringExamples.cs** | `/src/Honua.Server.Host/Filtering/` | Code examples and unit tests |

**Total Lines of Code:** 2,324 lines (including comprehensive XML documentation)

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        Client Request                            │
│              GET /api/shares?filter=status eq 'active'           │
└─────────────────────────┬───────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│              FilterQueryActionFilter (IAsyncActionFilter)        │
│  • Reads filter query parameter                                 │
│  • Validates FilterQueryAttribute configuration                 │
│  • Parses filter string using FilterExpressionParser            │
│  • Validates property names against allowed list                │
│  • Stores FilterExpression in HttpContext.Items                 │
│  • Returns 400 Bad Request on errors                            │
└─────────────────────────┬───────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│                  HttpContext.Items["ParsedFilter"]               │
│                    (FilterExpression object)                     │
└─────────────────────────┬───────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│                      Controller Action                           │
│  var query = dbContext.Shares.AsQueryable();                     │
│  if (HttpContext.Items.TryGetValue("ParsedFilter", out var f))  │
│      query = query.ApplyFilter((FilterExpression)f);             │
└─────────────────────────┬───────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│           FilterQueryableExtensions.ApplyFilter<T>()             │
│  • Converts FilterExpression tree to LINQ Expression<Func<T>>   │
│  • Handles type conversions (string → DateTime, Guid, enum)     │
│  • Builds case-insensitive string comparisons                   │
│  • Supports nested properties with null checks                  │
└─────────────────────────┬───────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Entity Framework Core                         │
│  • Translates LINQ expression to SQL WHERE clause               │
│  • Parameterizes all values (SQL injection safe)                │
│  • Optimizes query execution plan                               │
└─────────────────────────┬───────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│                         Database                                 │
│  SELECT * FROM shares WHERE status = @p0                         │
└─────────────────────────────────────────────────────────────────┘
```

## Supported Features

### Comparison Operators
- `eq` - Equal to
- `ne` - Not equal to
- `gt` - Greater than
- `ge` - Greater than or equal to
- `lt` - Less than
- `le` - Less than or equal to

### Logical Operators
- `and` - Logical AND (both conditions must be true)
- `or` - Logical OR (at least one condition must be true)
- `not` - Logical NOT (inverts the condition)

### String Functions
- `contains` - Case-insensitive substring match
- `startswith` - Case-insensitive prefix match
- `endswith` - Case-insensitive suffix match

### Data Types
- String (quoted: `'value'` or `"value"`)
- Integer (`42`, `-10`)
- Decimal (`3.14`, `-2.5`)
- Boolean (`true`, `false`)
- Null (`null`)
- DateTime (ISO 8601: `2025-01-01T00:00:00Z`)
- Guid (`123e4567-e89b-12d3-a456-426614174000`)
- Enum (case-insensitive string matching)

## Usage Example

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
[FilterQuery(
    EntityType = typeof(Share),
    AllowedProperties = new[] { "createdAt", "permission", "isActive" })]
public async Task<ActionResult<PagedResponse<Share>>> GetShares(
    [FromQuery] string? filter)
{
    var query = dbContext.Shares.AsQueryable();

    if (HttpContext.Items.TryGetValue("ParsedFilter", out var parsedFilter))
    {
        query = query.ApplyFilter((FilterExpression)parsedFilter);
    }

    var shares = await query.ToListAsync();
    return Ok(new PagedResponse<Share> { Items = shares });
}
```

### 3. Client Requests

```http
GET /api/shares?filter=createdAt gt 2025-01-01
GET /api/shares?filter=status eq 'active'
GET /api/shares?filter=createdAt gt 2025-01-01 and status eq 'active'
GET /api/shares?filter=name contains 'project'
GET /api/shares?filter=(status eq 'active' or status eq 'pending') and priority gt 5
```

## Security Features

### 1. Property Whitelisting
- Only properties in `AllowedProperties` can be filtered
- Prevents unauthorized access to sensitive fields
- Returns 400 Bad Request for unauthorized properties

### 2. SQL Injection Prevention
- All property names validated against entity type
- All values parameterized in generated SQL
- No raw SQL string concatenation

### 3. Complexity Limits
- **Max tokens:** 50 per filter expression
- **Max nesting depth:** 10 levels of parentheses
- **Max conditions:** Configurable per endpoint (default: 10)

### 4. Type Safety
- Runtime type checking for all property accesses
- Type conversion with error handling
- Null-safe comparisons

## Performance Considerations

### Database Optimization
1. **Index filtered properties** for optimal query performance
2. **Avoid string functions** on large tables (consider full-text search)
3. **Use composite indexes** for frequently combined filters
4. **Monitor query execution times** and optimize slow queries

### Application Optimization
1. **Project to DTOs** to reduce data transfer
2. **Combine with pagination** to limit result sets
3. **Cache filter parsing results** (future enhancement)
4. **Use compiled queries** for frequently used filters

## Error Handling

### Parse Errors (400 Bad Request)

```json
{
  "status": 400,
  "title": "Invalid filter expression",
  "detail": "Expected value after operator 'eq'",
  "instance": "0HMVD8K3N1234",
  "requestId": "0HMVD8K3N1234",
  "timestamp": "2025-11-15T00:00:00Z"
}
```

### Property Validation Errors (400 Bad Request)

```json
{
  "status": 400,
  "title": "Invalid filter property",
  "detail": "Property 'password' is not allowed in filter expressions. Allowed properties: createdAt, status, isActive",
  "instance": "0HMVD8K3N1234"
}
```

### Type Conversion Errors (400 Bad Request)

```json
{
  "status": 400,
  "title": "Invalid filter expression",
  "detail": "Cannot convert value 'invalid-date' to type 'DateTime'",
  "instance": "0HMVD8K3N1234"
}
```

## Filter Examples

### Simple Comparisons

```http
# Equality
GET /api/shares?filter=status eq 'active'

# Numeric comparison
GET /api/products?filter=price gt 100

# Date comparison
GET /api/orders?filter=createdAt ge 2025-01-01

# Boolean
GET /api/features?filter=isEnabled eq true
```

### Logical Combinations

```http
# AND
GET /api/shares?filter=createdAt gt 2025-01-01 and status eq 'active'

# OR
GET /api/users?filter=status eq 'active' or status eq 'pending'

# NOT
GET /api/items?filter=not (isDeleted eq true)
```

### String Functions

```http
# Contains
GET /api/projects?filter=name contains 'infrastructure'

# Starts with
GET /api/users?filter=email startswith 'admin'

# Ends with
GET /api/files?filter=filename endswith '.json'
```

### Complex Expressions

```http
# Parentheses for precedence
GET /api/tasks?filter=(status eq 'active' or status eq 'pending') and priority gt 5

# Multiple conditions
GET /api/events?filter=startDate ge 2025-01-01 and endDate le 2025-12-31 and isPublic eq true

# Nested NOT
GET /api/users?filter=not (status eq 'deleted' or status eq 'suspended')
```

## Testing

### Unit Tests

```csharp
[Fact]
public void Parser_Should_Parse_Simple_Equality()
{
    var parser = new FilterExpressionParser();
    var expression = parser.Parse("status eq 'active'");

    var comparison = Assert.IsType<ComparisonExpression>(expression);
    Assert.Equal("status", comparison.Property);
    Assert.Equal(ComparisonOperator.Eq, comparison.Operator);
    Assert.Equal("active", comparison.Value);
}
```

### Integration Tests

```csharp
[Fact]
public async Task GetShares_Should_Filter_By_Status()
{
    var client = factory.CreateClient();
    var response = await client.GetAsync("/api/shares?filter=status eq 'active'");

    response.EnsureSuccessStatusCode();
    var shares = await response.Content.ReadFromJsonAsync<PagedResponse<Share>>();
    Assert.All(shares!.Items, s => Assert.Equal("active", s.Status));
}
```

## Operator Precedence

From highest to lowest:
1. Parentheses `( )`
2. String functions (`contains`, `startswith`, `endswith`)
3. Comparison operators (`eq`, `ne`, `gt`, `ge`, `lt`, `le`)
4. Logical NOT (`not`)
5. Logical AND (`and`)
6. Logical OR (`or`)

## Limitations (By Design)

The following features are intentionally **not** supported to maintain simplicity and security:

- **Arithmetic operations:** `price mul 1.1 gt 100` - use database views
- **Date functions:** `year(createdAt) eq 2025` - filter on date ranges
- **Aggregations:** `count(items) gt 5` - use separate aggregation endpoints
- **Lambda expressions:** `items/any(i: i.price gt 100)` - use joins or nested queries

## Migration from Full OData

If you need more advanced OData features in the future, you can migrate to the official OData library:

```csharp
// Install: Microsoft.AspNetCore.OData
builder.Services.AddControllers().AddOData(opt =>
    opt.Select().Filter().OrderBy().Expand().SetMaxTop(100)
);
```

## Next Steps

### Recommended Enhancements

1. **Filter Parsing Cache** - Cache parsed filter expressions for better performance
2. **OpenAPI/Swagger Integration** - Add operation filter for automatic documentation
3. **Metrics and Monitoring** - Track filter usage and performance
4. **Client Libraries** - Generate TypeScript/JavaScript client helpers
5. **Rate Limiting** - Implement rate limits for complex filter queries

### Integration Points

- **Pagination System:** `/src/Honua.Server.Host/Pagination/`
- **API Versioning:** Can be combined with versioned endpoints
- **Authentication:** Works with existing authentication filters
- **Caching:** Consider caching filtered query results

## References

- **OData Specification:** [OData v4.01 URL Conventions](http://docs.oasis-open.org/odata/odata/v4.01/odata-v4.01-part2-url-conventions.html)
- **RFC 7807:** [Problem Details for HTTP APIs](https://tools.ietf.org/html/rfc7807)
- **Microsoft REST Guidelines:** [API Design Guidelines](https://github.com/microsoft/api-guidelines)
- **Entity Framework Core:** [Query Filters](https://docs.microsoft.com/en-us/ef/core/querying/)

## Support and Documentation

- **Implementation Guide:** `/src/Honua.Server.Host/Filtering/README.md`
- **Code Examples:** `/src/Honua.Server.Host/Filtering/FilteringExamples.cs`
- **Source Code:** `/src/Honua.Server.Host/Filtering/` and `/src/Honua.Server.Host/Filters/`

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0
