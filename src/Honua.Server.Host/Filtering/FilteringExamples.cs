// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

// This file contains examples of how to use the filtering system in your application.
// It is not compiled into the application - it serves as documentation only.

#if EXAMPLES_ONLY

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Honua.Server.Host.Filtering;
using Honua.Server.Host.Filters;

namespace Honua.Server.Host.Examples;

/// <summary>
/// Example controller demonstrating filter usage patterns.
/// </summary>
public class SharesController : ControllerBase
{
    private readonly ApplicationDbContext dbContext;

    public SharesController(ApplicationDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    /// <summary>
    /// Example 1: Basic filtering on a simple entity.
    /// </summary>
    [HttpGet("api/shares")]
    [FilterQuery(
        EntityType = typeof(Share),
        AllowedProperties = new[] { "createdAt", "permission", "isActive", "accessCount" })]
    public async Task<ActionResult<PagedResponse<Share>>> GetShares(
        [FromQuery] string? filter)
    {
        var query = dbContext.Shares.AsQueryable();

        // Apply filter if provided
        if (HttpContext.Items.TryGetValue(FilterQueryActionFilter.ParsedFilterKey, out var parsedFilter))
        {
            query = query.ApplyFilter((FilterExpression)parsedFilter);
        }

        var shares = await query.ToListAsync();
        return Ok(new PagedResponse<Share> { Items = shares });
    }

    /// <summary>
    /// Example 2: Combining filtering with pagination and sorting.
    /// </summary>
    [HttpGet("api/shares/paginated")]
    [FilterQuery(
        EntityType = typeof(Share),
        AllowedProperties = new[] { "createdAt", "status", "isActive" })]
    public async Task<ActionResult<PagedResponse<Share>>> GetSharesPaginated(
        [FromQuery] string? filter,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? orderBy = "createdAt",
        [FromQuery] bool descending = false)
    {
        var query = dbContext.Shares.AsQueryable();

        // 1. Apply filter
        if (HttpContext.Items.TryGetValue(FilterQueryActionFilter.ParsedFilterKey, out var parsedFilter))
        {
            query = query.ApplyFilter((FilterExpression)parsedFilter);
        }

        // 2. Apply sorting
        query = orderBy?.ToLower() switch
        {
            "createdat" => descending
                ? query.OrderByDescending(s => s.CreatedAt)
                : query.OrderBy(s => s.CreatedAt),
            "status" => descending
                ? query.OrderByDescending(s => s.Status)
                : query.OrderBy(s => s.Status),
            _ => query.OrderByDescending(s => s.CreatedAt)
        };

        // 3. Get total count (after filtering, before pagination)
        var totalCount = await query.CountAsync();

        // 4. Apply pagination
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResponse<Share>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        });
    }

    /// <summary>
    /// Example 3: Filtering with projection to DTO for reduced data transfer.
    /// </summary>
    [HttpGet("api/shares/dto")]
    [FilterQuery(
        EntityType = typeof(Share),
        AllowedProperties = new[] { "createdAt", "isActive", "permission" })]
    public async Task<ActionResult<PagedResponse<ShareDto>>> GetSharesAsDto(
        [FromQuery] string? filter)
    {
        var query = dbContext.Shares.AsQueryable();

        // Apply filter on entity
        if (HttpContext.Items.TryGetValue(FilterQueryActionFilter.ParsedFilterKey, out var parsedFilter))
        {
            query = query.ApplyFilter((FilterExpression)parsedFilter);
        }

        // Project to DTO to reduce data transfer
        var shares = await query
            .Select(s => new ShareDto
            {
                Id = s.Id,
                Name = s.Name,
                CreatedAt = s.CreatedAt,
                Permission = s.Permission,
                IsActive = s.IsActive
            })
            .ToListAsync();

        return Ok(new PagedResponse<ShareDto> { Items = shares });
    }

    /// <summary>
    /// Example 4: Advanced filtering with nested properties.
    /// </summary>
    [HttpGet("api/orders")]
    [FilterQuery(
        EntityType = typeof(Order),
        AllowedProperties = new[] { "totalAmount", "createdAt", "customer.email", "customer.name" },
        AllowNestedProperties = true)]
    public async Task<ActionResult<PagedResponse<Order>>> GetOrders(
        [FromQuery] string? filter)
    {
        var query = dbContext.Orders
            .Include(o => o.Customer) // Include related entity for nested filtering
            .AsQueryable();

        if (HttpContext.Items.TryGetValue(FilterQueryActionFilter.ParsedFilterKey, out var parsedFilter))
        {
            query = query.ApplyFilter((FilterExpression)parsedFilter);
        }

        var orders = await query.ToListAsync();
        return Ok(new PagedResponse<Order> { Items = orders });
    }

    /// <summary>
    /// Example 5: Filtering with custom complexity limits for admin endpoints.
    /// </summary>
    [HttpGet("api/admin/shares")]
    [FilterQuery(
        EntityType = typeof(Share),
        AllowedProperties = new[] { "id", "createdAt", "updatedAt", "status", "permission", "isActive", "accessCount" },
        MaxConditions = 20)] // Allow more complex queries for admin users
    public async Task<ActionResult<PagedResponse<Share>>> GetAdminShares(
        [FromQuery] string? filter)
    {
        var query = dbContext.Shares.AsQueryable();

        if (HttpContext.Items.TryGetValue(FilterQueryActionFilter.ParsedFilterKey, out var parsedFilter))
        {
            query = query.ApplyFilter((FilterExpression)parsedFilter);
        }

        var shares = await query.ToListAsync();
        return Ok(new PagedResponse<Share> { Items = shares });
    }

    /// <summary>
    /// Example 6: Filtering with performance monitoring.
    /// </summary>
    [HttpGet("api/shares/monitored")]
    [FilterQuery(
        EntityType = typeof(Share),
        AllowedProperties = new[] { "createdAt", "status", "isActive" })]
    public async Task<ActionResult<PagedResponse<Share>>> GetSharesMonitored(
        [FromQuery] string? filter)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var query = dbContext.Shares.AsQueryable();

        if (HttpContext.Items.TryGetValue(FilterQueryActionFilter.ParsedFilterKey, out var parsedFilter))
        {
            query = query.ApplyFilter((FilterExpression)parsedFilter);
        }

        var shares = await query.ToListAsync();

        stopwatch.Stop();

        // Log slow queries for investigation
        if (stopwatch.ElapsedMilliseconds > 1000)
        {
            // logger.LogWarning(
            //     "Slow filter query: {Filter} took {ElapsedMs}ms, returned {Count} results",
            //     filter,
            //     stopwatch.ElapsedMilliseconds,
            //     shares.Count);
        }

        return Ok(new PagedResponse<Share> { Items = shares });
    }
}

/// <summary>
/// Example unit tests for the filtering system.
/// </summary>
public class FilteringTests
{
    /// <summary>
    /// Test: Parser should parse simple equality expression.
    /// </summary>
    public void Parser_Should_Parse_Simple_Equality()
    {
        // Arrange
        var parser = new FilterExpressionParser();

        // Act
        var expression = parser.Parse("status eq 'active'");

        // Assert
        var comparison = expression as ComparisonExpression;
        if (comparison == null)
            throw new Exception("Expected ComparisonExpression");

        if (comparison.Property != "status")
            throw new Exception($"Expected property 'status', got '{comparison.Property}'");

        if (comparison.Operator != ComparisonOperator.Eq)
            throw new Exception($"Expected operator Eq, got '{comparison.Operator}'");

        if (comparison.Value.ToString() != "active")
            throw new Exception($"Expected value 'active', got '{comparison.Value}'");
    }

    /// <summary>
    /// Test: Parser should parse logical AND expression.
    /// </summary>
    public void Parser_Should_Parse_Logical_And()
    {
        // Arrange
        var parser = new FilterExpressionParser();

        // Act
        var expression = parser.Parse("status eq 'active' and age gt 18");

        // Assert
        var logical = expression as LogicalExpression;
        if (logical == null)
            throw new Exception("Expected LogicalExpression");

        if (logical.Operator != LogicalOperator.And)
            throw new Exception($"Expected operator And, got '{logical.Operator}'");

        var left = logical.Left as ComparisonExpression;
        var right = logical.Right as ComparisonExpression;

        if (left?.Property != "status")
            throw new Exception("Left property should be 'status'");

        if (right?.Property != "age")
            throw new Exception("Right property should be 'age'");
    }

    /// <summary>
    /// Test: Parser should parse string function.
    /// </summary>
    public void Parser_Should_Parse_String_Function()
    {
        // Arrange
        var parser = new FilterExpressionParser();

        // Act
        var expression = parser.Parse("name contains 'test'");

        // Assert
        var stringFunc = expression as StringFunctionExpression;
        if (stringFunc == null)
            throw new Exception("Expected StringFunctionExpression");

        if (stringFunc.Property != "name")
            throw new Exception($"Expected property 'name', got '{stringFunc.Property}'");

        if (stringFunc.Function != StringFunction.Contains)
            throw new Exception($"Expected function Contains, got '{stringFunc.Function}'");

        if (stringFunc.Value != "test")
            throw new Exception($"Expected value 'test', got '{stringFunc.Value}'");
    }

    /// <summary>
    /// Test: Parser should parse NOT expression.
    /// </summary>
    public void Parser_Should_Parse_Not_Expression()
    {
        // Arrange
        var parser = new FilterExpressionParser();

        // Act
        var expression = parser.Parse("not (isDeleted eq true)");

        // Assert
        var notExpr = expression as NotExpression;
        if (notExpr == null)
            throw new Exception("Expected NotExpression");

        var comparison = notExpr.Expression as ComparisonExpression;
        if (comparison?.Property != "isDeleted")
            throw new Exception("Inner expression should be 'isDeleted eq true'");
    }

    /// <summary>
    /// Test: Parser should parse complex nested expression.
    /// </summary>
    public void Parser_Should_Parse_Complex_Expression()
    {
        // Arrange
        var parser = new FilterExpressionParser();

        // Act
        var expression = parser.Parse("(status eq 'active' or status eq 'pending') and priority gt 5");

        // Assert
        var logical = expression as LogicalExpression;
        if (logical == null)
            throw new Exception("Expected LogicalExpression");

        if (logical.Operator != LogicalOperator.And)
            throw new Exception("Top-level operator should be AND");

        var leftLogical = logical.Left as LogicalExpression;
        if (leftLogical?.Operator != LogicalOperator.Or)
            throw new Exception("Left side should be OR expression");

        var rightComparison = logical.Right as ComparisonExpression;
        if (rightComparison?.Property != "priority")
            throw new Exception("Right side should be priority comparison");
    }

    /// <summary>
    /// Test: Parser should throw on invalid syntax.
    /// </summary>
    public void Parser_Should_Throw_On_Invalid_Syntax()
    {
        // Arrange
        var parser = new FilterExpressionParser();

        // Act & Assert
        try
        {
            parser.Parse("status eq");
            throw new Exception("Should have thrown FilterParseException");
        }
        catch (FilterParseException ex)
        {
            if (!ex.Message.Contains("Expected value"))
                throw new Exception($"Expected error message about missing value, got: {ex.Message}");
        }
    }

    /// <summary>
    /// Test: Parser should throw on unterminated string.
    /// </summary>
    public void Parser_Should_Throw_On_Unterminated_String()
    {
        // Arrange
        var parser = new FilterExpressionParser();

        // Act & Assert
        try
        {
            parser.Parse("name eq 'unterminated");
            throw new Exception("Should have thrown FilterParseException");
        }
        catch (FilterParseException ex)
        {
            if (!ex.Message.Contains("Unterminated string"))
                throw new Exception($"Expected error about unterminated string, got: {ex.Message}");
        }
    }

    /// <summary>
    /// Test: Parser should parse various data types.
    /// </summary>
    public void Parser_Should_Parse_Various_Data_Types()
    {
        var parser = new FilterExpressionParser();

        // Integer
        var intExpr = parser.Parse("age eq 42") as ComparisonExpression;
        if (intExpr?.Value is not int intVal || intVal != 42)
            throw new Exception("Should parse integer value");

        // Boolean
        var boolExpr = parser.Parse("isActive eq true") as ComparisonExpression;
        if (boolExpr?.Value is not bool boolVal || !boolVal)
            throw new Exception("Should parse boolean value");

        // Null
        var nullExpr = parser.Parse("deletedAt eq null") as ComparisonExpression;
        if (nullExpr?.Value != null)
            throw new Exception("Should parse null value");

        // Double
        var doubleExpr = parser.Parse("price gt 3.14") as ComparisonExpression;
        if (doubleExpr?.Value is not double)
            throw new Exception("Should parse double value");
    }
}

/// <summary>
/// Example client usage patterns.
/// </summary>
public class ClientExamples
{
    /// <summary>
    /// Example HTTP requests demonstrating filter usage.
    /// </summary>
    public static class HttpRequests
    {
        // Simple equality
        public const string SimpleEquality = "GET /api/shares?filter=status eq 'active'";

        // Numeric comparison
        public const string NumericComparison = "GET /api/shares?filter=accessCount gt 100";

        // Date range
        public const string DateRange = "GET /api/shares?filter=createdAt ge 2025-01-01 and createdAt lt 2025-02-01";

        // Boolean
        public const string Boolean = "GET /api/shares?filter=isActive eq true";

        // String function
        public const string StringContains = "GET /api/shares?filter=name contains 'project'";

        // Logical OR
        public const string LogicalOr = "GET /api/shares?filter=status eq 'active' or status eq 'pending'";

        // Complex nested
        public const string ComplexNested = "GET /api/shares?filter=(status eq 'active' and priority gt 5) or isUrgent eq true";

        // NOT expression
        public const string NotExpression = "GET /api/shares?filter=not (isDeleted eq true)";

        // Combined with pagination
        public const string WithPagination = "GET /api/shares?filter=status eq 'active'&page=2&pageSize=20";

        // Combined with sorting
        public const string WithSorting = "GET /api/shares?filter=status eq 'active'&orderBy=createdAt&descending=true";
    }

    /// <summary>
    /// Example JavaScript/TypeScript client code.
    /// </summary>
    public static class JavaScriptClient
    {
        public const string Example = @"
// TypeScript example using fetch API

interface FilterParams {
    filter?: string;
    page?: number;
    pageSize?: number;
}

async function getShares(params: FilterParams) {
    const url = new URL('/api/shares', window.location.origin);

    if (params.filter) {
        url.searchParams.set('filter', params.filter);
    }
    if (params.page) {
        url.searchParams.set('page', params.page.toString());
    }
    if (params.pageSize) {
        url.searchParams.set('pageSize', params.pageSize.toString());
    }

    const response = await fetch(url.toString());

    if (!response.ok) {
        const error = await response.json();
        throw new Error(error.detail || 'Failed to fetch shares');
    }

    return await response.json();
}

// Usage examples
const activeShares = await getShares({
    filter: ""status eq 'active'""
});

const recentShares = await getShares({
    filter: ""createdAt gt 2025-01-01 and isActive eq true"",
    page: 1,
    pageSize: 20
});

const searchResults = await getShares({
    filter: ""name contains 'infrastructure'""
});
";
    }
}

// Example entity models
public class Share
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int AccessCount { get; set; }
}

public class ShareDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Permission { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class Order
{
    public Guid Id { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public Customer Customer { get; set; } = null!;
}

public class Customer
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class PagedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class ApplicationDbContext : DbContext
{
    public DbSet<Share> Shares { get; set; } = null!;
    public DbSet<Order> Orders { get; set; } = null!;
}

#endif
