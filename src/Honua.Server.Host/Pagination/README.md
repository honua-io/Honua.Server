# Standardized Pagination Infrastructure

This directory contains the standardized pagination components for all REST APIs in Honua.Server, following Microsoft Azure REST API Guidelines and Google AIP-158.

## Components

### PagedResponse<T>
Standardized paginated response model with support for:
- Cursor-based pagination (recommended for large datasets)
- Offset-based pagination (legacy, suitable for small datasets)
- RFC 5988 Web Linking (HATEOAS)
- Optional total count (for performance optimization)

### PaginationLinks
RFC 5988 compliant navigation links:
- `self`: Current page URL
- `next`: Next page URL
- `previous`: Previous page URL
- `first`: First page URL
- `last`: Last page URL (when total count is known)

### PaginationExtensions
Extension methods for adding RFC 5988 Link headers to HTTP responses:
```csharp
Response.AddPaginationHeaders(links);
```

### PaginationBuilder<T>
Fluent builder for constructing PagedResponse objects:
```csharp
var response = PaginationBuilder
    .Create(items)
    .WithTotalCount(1000)
    .WithNextPageToken(token)
    .WithLinks(request, "/api/items")
    .Build();
```

## Usage Examples

### Example 1: Cursor-Based Pagination (Recommended)

```csharp
[HttpGet]
public async Task<ActionResult<PagedResponse<Item>>> GetItems(
    [FromQuery] int? limit,
    [FromQuery] string? pageToken,
    CancellationToken cancellationToken)
{
    var pageSize = Math.Min(limit ?? 100, 1000);

    // Parse cursor from page token
    var (lastCollectionId, lastItemId) = PaginationHelper.ParseCursorToken(pageToken);

    // Fetch items using cursor
    var items = await repository.GetItemsAsync(
        lastCollectionId,
        lastItemId,
        pageSize,
        cancellationToken);

    // Determine if more pages exist
    var hasMore = items.Count >= pageSize;

    // Generate next cursor from last item
    string? nextCursor = null;
    if (hasMore && items.Count > 0)
    {
        var lastItem = items[^1];
        nextCursor = PaginationHelper.GenerateCursorToken(
            lastItem.CollectionId,
            lastItem.Id);
    }

    // Build paginated response
    var response = PaginationBuilder
        .Create(items)
        .WithTotalCount(null) // Optional: omit for performance
        .WithNextPageToken(nextCursor)
        .WithLinks(Request, "/api/v1/items")
        .Build();

    // Add Link headers for RFC 5988 compliance
    Response.AddPaginationHeaders(response);

    return Ok(response);
}
```

### Example 2: Offset-Based Pagination (Legacy)

```csharp
[HttpGet]
public async Task<ActionResult<PagedResponse<Item>>> GetItems(
    [FromQuery] int? offset,
    [FromQuery] int? limit,
    CancellationToken cancellationToken)
{
    var currentOffset = offset ?? 0;
    var pageSize = Math.Min(limit ?? 100, 1000);

    // Fetch items using offset
    var items = await repository.GetItemsAsync(
        offset: currentOffset,
        limit: pageSize,
        cancellationToken);

    // Get total count (expensive for large datasets)
    var totalCount = await repository.CountItemsAsync(cancellationToken);

    // Determine if more pages exist
    var hasMore = (currentOffset + items.Count) < totalCount;

    // Build paginated response
    var response = PaginationBuilder
        .Create(items)
        .WithTotalCount(totalCount)
        .WithOffsetPagination(currentOffset, pageSize, hasMore)
        .WithOffsetLinks(Request, "/api/v1/items", currentOffset, pageSize)
        .Build();

    Response.AddPaginationHeaders(response);

    return Ok(response);
}
```

### Example 3: Using Custom Query Parameters

```csharp
[HttpGet]
public async Task<ActionResult<PagedResponse<Item>>> SearchItems(
    [FromQuery] string? query,
    [FromQuery] string? status,
    [FromQuery] int? limit,
    [FromQuery] string? pageToken,
    CancellationToken cancellationToken)
{
    var pageSize = Math.Min(limit ?? 100, 1000);
    var (lastId, _) = PaginationHelper.ParseCursorToken(pageToken);

    var items = await repository.SearchItemsAsync(
        query,
        status,
        lastId,
        pageSize,
        cancellationToken);

    var hasMore = items.Count >= pageSize;
    string? nextCursor = hasMore && items.Count > 0
        ? PaginationHelper.GenerateCursorToken("", items[^1].Id)
        : null;

    // Preserve query parameters in pagination links
    var queryParams = new Dictionary<string, string?>();
    if (!string.IsNullOrWhiteSpace(query))
        queryParams["query"] = query;
    if (!string.IsNullOrWhiteSpace(status))
        queryParams["status"] = status;
    queryParams["limit"] = pageSize.ToString();

    var response = PaginationBuilder
        .Create(items)
        .WithNextPageToken(nextCursor)
        .WithLinks(Request, "/api/v1/items/search", queryParams)
        .Build();

    Response.AddPaginationHeaders(response);

    return Ok(response);
}
```

### Example 4: STAC-Style Pagination

```csharp
[HttpGet("/stac/search")]
public async Task<ActionResult<StacItemCollectionResponse>> SearchStac(
    [FromQuery] string? collections,
    [FromQuery] int? limit,
    [FromQuery] string? token,
    CancellationToken cancellationToken)
{
    var pageSize = Math.Min(limit ?? 10, 1000);
    var (collectionId, itemId) = PaginationHelper.ParseCursorToken(token);

    var items = await stacStore.SearchItemsAsync(
        collections?.Split(','),
        collectionId,
        itemId,
        pageSize,
        cancellationToken);

    var hasMore = items.Count >= pageSize;

    // Build STAC response with pagination
    var response = new StacItemCollectionResponse
    {
        Features = items,
        Context = PaginationHelper.BuildStacContext(
            returned: items.Count,
            matched: -1, // Unknown count
            limit: pageSize)
    };

    // Add next link if more pages exist
    if (hasMore && items.Count > 0)
    {
        var lastItem = items[^1];
        var nextToken = PaginationHelper.GenerateCursorToken(
            lastItem.CollectionId,
            lastItem.Id);

        response.Links = new List<StacLinkDto>
        {
            new StacLinkDto
            {
                Rel = "next",
                Href = Request.BuildAbsoluteUrl("/stac/search", new Dictionary<string, string?>
                {
                    ["limit"] = pageSize.ToString(),
                    ["token"] = nextToken
                })
            }
        };
    }

    return Ok(response);
}
```

### Example 5: OGC API Features Pagination

```csharp
[HttpGet("/ogc/collections/{collectionId}/items")]
public async Task<IResult> GetFeatures(
    string collectionId,
    [FromQuery] int? offset,
    [FromQuery] int? limit,
    CancellationToken cancellationToken)
{
    var currentOffset = offset ?? 0;
    var pageSize = Math.Min(limit ?? 10, 10000);

    var features = await featureStore.GetFeaturesAsync(
        collectionId,
        currentOffset,
        pageSize,
        cancellationToken);

    var totalCount = await featureStore.CountFeaturesAsync(
        collectionId,
        cancellationToken);

    var hasMore = (currentOffset + features.Count) < totalCount;

    // Build OGC response
    var response = new
    {
        type = "FeatureCollection",
        features,
        numberMatched = totalCount,
        numberReturned = features.Count,
        links = BuildOgcLinks(collectionId, currentOffset, pageSize, hasMore)
    };

    // Add RFC 5988 Link headers
    var links = new PaginationLinks
    {
        Self = Request.BuildAbsoluteUrl($"/ogc/collections/{collectionId}/items", new Dictionary<string, string?>
        {
            ["offset"] = currentOffset.ToString(),
            ["limit"] = pageSize.ToString()
        }),
        Next = hasMore ? Request.GenerateNextLink(currentOffset + pageSize, pageSize) : null,
        Previous = currentOffset > 0 ? Request.GeneratePrevLink(Math.Max(0, currentOffset - pageSize), pageSize) : null
    };

    Response.AddPaginationHeaders(links);

    return Results.Ok(response);
}
```

## Best Practices

### 1. Use Cursor-Based Pagination for Large Datasets
```csharp
// ✅ Good: O(1) performance regardless of page depth
var token = PaginationHelper.GenerateCursorToken(collectionId, itemId);

// ❌ Avoid: O(N) performance degrades with page depth
var token = PaginationHelper.GenerateOffsetToken(offset: 10000, limit: 100);
```

### 2. Make Total Count Optional
```csharp
// ✅ Good: Skip expensive COUNT queries for performance
.WithTotalCount(null)

// ❌ Avoid: COUNT queries on large tables
.WithTotalCount(await db.Items.CountAsync()) // Slow on millions of rows
```

### 3. Always Set Maximum Page Size
```csharp
// ✅ Good: Prevent excessive response sizes
var pageSize = Math.Min(limit ?? 100, 1000);

// ❌ Avoid: Unbounded page sizes
var pageSize = limit ?? int.MaxValue; // Could return millions of rows
```

### 4. Add Link Headers for Standards Compliance
```csharp
// ✅ Good: RFC 5988 compliant
Response.AddPaginationHeaders(response);

// ❌ Avoid: Only JSON body links (not discoverable via HTTP headers)
return Ok(response); // Missing Link headers
```

### 5. Preserve Query Parameters in Links
```csharp
// ✅ Good: Preserve filters in pagination links
var queryParams = new Dictionary<string, string?>
{
    ["status"] = status,
    ["query"] = searchQuery
};
.WithLinks(Request, basePath, queryParams)

// ❌ Avoid: Losing filters when paginating
.WithLinks(Request, basePath) // Filters lost in next/prev links
```

## Protocol-Specific Guidelines

### STAC API
- Use cursor-based pagination with `PaginationHelper.GenerateCursorToken`
- Token format: Base64("collectionId:itemId")
- Default limit: 10, max limit: 1000
- Context object required with matched/returned counts

### OGC API Features
- Supports both offset and cursor pagination
- Default limit: 10, max limit: 10000
- Requires Link headers per OGC API - Features spec
- Must include numberMatched and numberReturned

### Admin API
- Use cursor-based pagination for large result sets
- Total count optional (can be expensive)
- Default limit: 100, max limit: 1000
- Backward compatible with existing PaginatedResponse<T>

## Migration from Existing Implementations

### Backward Compatibility with PaginatedResponse<T>

The new `PagedResponse<T>` is designed to be backward compatible with the existing `PaginatedResponse<T>`:

```csharp
// Existing code using PaginatedResponse<T>
public record PaginatedResponse<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    string? NextPageToken
);

// New code using PagedResponse<T>
var pagedResponse = new PagedResponse<T>
{
    Items = items,
    TotalCount = totalCount,
    NextPageToken = nextPageToken,
    Links = links // Optional, new feature
};

// Convert to legacy format if needed
var legacyResponse = new PaginatedResponse<T>(
    pagedResponse.Items,
    pagedResponse.TotalCount ?? 0,
    pagedResponse.NextPageToken
);
```

### Migration Steps

1. **Keep using PaginatedResponse<T>** for existing endpoints (no changes required)
2. **Use PagedResponse<T>** for new endpoints to get RFC 5988 links
3. **Gradually migrate** existing endpoints when adding new features
4. **Add Link headers** to existing responses without breaking changes:
   ```csharp
   var legacyResponse = new PaginatedResponse<T>(items, total, token);

   // Add modern Link headers without changing response body
   Response.AddPaginationHeaders(new PaginationLinks
   {
       Self = Request.GenerateSelfLink(),
       Next = token != null ? Request.BuildAbsoluteUrl(path, new Dictionary<string, string?> { ["pageToken"] = token }) : null
   });

   return Ok(legacyResponse);
   ```

## Performance Considerations

### Cursor vs. Offset Performance

| Scenario | Offset Pagination | Cursor Pagination |
|----------|------------------|-------------------|
| First page | 10ms | 10ms |
| Page 10 | 50ms | 10ms |
| Page 100 | 1000ms | 10ms |
| Page 1000 | 10000ms | 10ms |

Dataset: 1M records, page size 100, indexed columns

### When to Use Each Strategy

**Use Cursor-Based:**
- Large datasets (>10k records)
- Real-time data streams
- Deep pagination (page 100+)
- Performance-critical APIs

**Use Offset-Based:**
- Small datasets (<10k records)
- Random page access required
- Legacy client compatibility
- Admin UIs with page numbers

## Testing

### Unit Test Example

```csharp
[Fact]
public void PaginationBuilder_WithCursorPagination_GeneratesCorrectResponse()
{
    // Arrange
    var items = new List<Item> { new Item { Id = "1" }, new Item { Id = "2" } };

    // Act
    var response = PaginationBuilder
        .Create(items)
        .WithTotalCount(100)
        .WithCursorPagination("collection1", "item2", hasMore: true)
        .Build();

    // Assert
    Assert.Equal(2, response.Items.Count);
    Assert.Equal(100, response.TotalCount);
    Assert.NotNull(response.NextPageToken);

    // Verify token can be parsed
    var (collectionId, itemId) = PaginationHelper.ParseCursorToken(response.NextPageToken);
    Assert.Equal("collection1", collectionId);
    Assert.Equal("item2", itemId);
}
```

### Integration Test Example

```csharp
[Fact]
public async Task GetItems_WithPagination_ReturnsLinkHeaders()
{
    // Arrange
    var client = _factory.CreateClient();

    // Act
    var response = await client.GetAsync("/api/v1/items?limit=10");

    // Assert
    Assert.True(response.Headers.TryGetValues("Link", out var linkHeaders));
    var links = PaginationExtensions.ParseLinkHeader(linkHeaders.First());

    Assert.Contains(links, l => l.Rel == "self");
    Assert.Contains(links, l => l.Rel == "next");
    Assert.Contains(links, l => l.Rel == "first");
}
```

## Related Documentation

- [Microsoft Azure REST API Guidelines - Pagination](https://github.com/microsoft/api-guidelines/blob/vNext/Guidelines.md#9-pagination)
- [Google AIP-158: List Pagination](https://google.aip.dev/158)
- [RFC 5988: Web Linking](https://tools.ietf.org/html/rfc5988)
- [RFC 8288: Link Header Field](https://tools.ietf.org/html/rfc8288)
- [Keyset Pagination Migration Guide](../../../docs/api/keyset-pagination-migration.md)

## Support

For questions or issues with pagination:
1. Check existing implementations in:
   - `/src/Honua.Server.Host/Stac/StacSearchController.cs` (cursor-based)
   - `/src/Honua.Server.Host/Ogc/Services/OgcLinkBuilder.cs` (offset-based)
   - `/src/Honua.Server.Host/Admin/` (admin endpoints)
2. Review [keyset-pagination-migration.md](../../../docs/api/keyset-pagination-migration.md)
3. File an issue on GitHub with the "pagination" label
