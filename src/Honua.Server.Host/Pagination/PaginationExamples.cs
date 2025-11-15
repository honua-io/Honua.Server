// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
#if EXAMPLES_ENABLED
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Pagination;

/// <summary>
/// Example implementations demonstrating the standardized pagination infrastructure.
/// </summary>
/// <remarks>
/// This file is excluded from compilation (conditional on EXAMPLES_ENABLED) and serves
/// as documentation and reference for implementing pagination in controllers.
/// </remarks>
[ApiController]
[Route("api/v1/examples")]
public class PaginationExamplesController : ControllerBase
{
    // Example: Simple cursor-based pagination (recommended)
    [HttpGet("cursor-simple")]
    public async Task<ActionResult<PagedResponse<ExampleItem>>> GetItemsWithCursor(
        [FromQuery] int? limit,
        [FromQuery] string? pageToken,
        CancellationToken cancellationToken)
    {
        var pageSize = Math.Min(limit ?? 100, 1000);

        // Parse cursor from page token
        var (lastCollectionId, lastItemId) = PaginationHelper.ParseCursorToken(pageToken);

        // Fetch items (simulate repository call)
        var items = await GetItemsFromRepositoryAsync(lastItemId, pageSize, cancellationToken);

        // Determine if more pages exist
        var hasMore = items.Count >= pageSize;

        // Generate next cursor from last item
        string? nextCursor = null;
        if (hasMore && items.Count > 0)
        {
            var lastItem = items[^1];
            nextCursor = PaginationHelper.GenerateCursorToken("default", lastItem.Id);
        }

        // Build paginated response
        var response = PaginationBuilder
            .Create(items)
            .WithNextPageToken(nextCursor)
            .WithLinks(Request, "/api/v1/examples/cursor-simple")
            .Build();

        // Add RFC 5988 Link headers
        Response.AddPaginationHeaders(response);

        return Ok(response);
    }

    // Example: Cursor-based pagination with total count
    [HttpGet("cursor-with-count")]
    public async Task<ActionResult<PagedResponse<ExampleItem>>> GetItemsWithCursorAndCount(
        [FromQuery] int? limit,
        [FromQuery] string? pageToken,
        CancellationToken cancellationToken)
    {
        var pageSize = Math.Min(limit ?? 100, 1000);
        var (_, lastItemId) = PaginationHelper.ParseCursorToken(pageToken);

        var items = await GetItemsFromRepositoryAsync(lastItemId, pageSize, cancellationToken);
        var totalCount = await GetTotalCountAsync(cancellationToken);
        var hasMore = items.Count >= pageSize;

        string? nextCursor = null;
        if (hasMore && items.Count > 0)
        {
            nextCursor = PaginationHelper.GenerateCursorToken("default", items[^1].Id);
        }

        var response = PaginationBuilder
            .Create(items)
            .WithTotalCount(totalCount)
            .WithNextPageToken(nextCursor)
            .WithLinks(Request, "/api/v1/examples/cursor-with-count")
            .Build();

        Response.AddPaginationHeaders(response);

        return Ok(response);
    }

    // Example: Offset-based pagination (legacy)
    [HttpGet("offset")]
    public async Task<ActionResult<PagedResponse<ExampleItem>>> GetItemsWithOffset(
        [FromQuery] int? offset,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var currentOffset = offset ?? 0;
        var pageSize = Math.Min(limit ?? 100, 1000);

        var items = await GetItemsWithOffsetAsync(currentOffset, pageSize, cancellationToken);
        var totalCount = await GetTotalCountAsync(cancellationToken);

        var hasMore = (currentOffset + items.Count) < totalCount;

        var response = PaginationBuilder
            .Create(items)
            .WithTotalCount(totalCount)
            .WithOffsetPagination(currentOffset, pageSize, hasMore)
            .WithOffsetLinks(Request, "/api/v1/examples/offset", currentOffset, pageSize)
            .Build();

        Response.AddPaginationHeaders(response);

        return Ok(response);
    }

    // Example: Pagination with query parameters preserved
    [HttpGet("search")]
    public async Task<ActionResult<PagedResponse<ExampleItem>>> SearchItems(
        [FromQuery] string? query,
        [FromQuery] string? status,
        [FromQuery] string? category,
        [FromQuery] int? limit,
        [FromQuery] string? pageToken,
        CancellationToken cancellationToken)
    {
        var pageSize = Math.Min(limit ?? 100, 1000);
        var (_, lastItemId) = PaginationHelper.ParseCursorToken(pageToken);

        var items = await SearchItemsAsync(query, status, category, lastItemId, pageSize, cancellationToken);
        var hasMore = items.Count >= pageSize;

        string? nextCursor = null;
        if (hasMore && items.Count > 0)
        {
            nextCursor = PaginationHelper.GenerateCursorToken("default", items[^1].Id);
        }

        // Preserve query parameters in pagination links
        var queryParams = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(query))
            queryParams["query"] = query;
        if (!string.IsNullOrWhiteSpace(status))
            queryParams["status"] = status;
        if (!string.IsNullOrWhiteSpace(category))
            queryParams["category"] = category;
        queryParams["limit"] = pageSize.ToString();

        var response = PaginationBuilder
            .Create(items)
            .WithNextPageToken(nextCursor)
            .WithLinks(Request, "/api/v1/examples/search", queryParams)
            .Build();

        Response.AddPaginationHeaders(response);

        return Ok(response);
    }

    // Example: Using PaginationBuilder fluent API
    [HttpGet("builder-example")]
    public async Task<ActionResult<PagedResponse<ExampleItem>>> BuilderExample(
        [FromQuery] int? limit,
        [FromQuery] string? pageToken,
        CancellationToken cancellationToken)
    {
        var pageSize = Math.Min(limit ?? 100, 1000);
        var (_, lastItemId) = PaginationHelper.ParseCursorToken(pageToken);

        var items = await GetItemsFromRepositoryAsync(lastItemId, pageSize, cancellationToken);
        var hasMore = items.Count >= pageSize;

        // Fluent builder pattern
        var response = PaginationBuilder
            .Create(items)
            .WithTotalCount(null) // Skip expensive COUNT query
            .WithCursorPagination("default", items.LastOrDefault()?.Id, hasMore)
            .WithLinks(Request, "/api/v1/examples/builder-example")
            .Build();

        Response.AddPaginationHeaders(response);

        return Ok(response);
    }

    // Example: Custom links without using builder
    [HttpGet("custom-links")]
    public async Task<ActionResult<PagedResponse<ExampleItem>>> CustomLinks(
        [FromQuery] int? limit,
        [FromQuery] string? pageToken,
        CancellationToken cancellationToken)
    {
        var pageSize = Math.Min(limit ?? 100, 1000);
        var (_, lastItemId) = PaginationHelper.ParseCursorToken(pageToken);

        var items = await GetItemsFromRepositoryAsync(lastItemId, pageSize, cancellationToken);
        var hasMore = items.Count >= pageSize;

        string? nextCursor = hasMore && items.Count > 0
            ? PaginationHelper.GenerateCursorToken("default", items[^1].Id)
            : null;

        // Manually construct links
        var links = new PaginationLinks
        {
            Self = Request.BuildAbsoluteUrl("/api/v1/examples/custom-links", new Dictionary<string, string?>
            {
                ["limit"] = pageSize.ToString(),
                ["pageToken"] = pageToken
            }),
            Next = nextCursor != null
                ? Request.BuildAbsoluteUrl("/api/v1/examples/custom-links", new Dictionary<string, string?>
                {
                    ["limit"] = pageSize.ToString(),
                    ["pageToken"] = nextCursor
                })
                : null,
            First = Request.BuildAbsoluteUrl("/api/v1/examples/custom-links", new Dictionary<string, string?>
            {
                ["limit"] = pageSize.ToString()
            })
        };

        var response = new PagedResponse<ExampleItem>
        {
            Items = items,
            NextPageToken = nextCursor,
            Links = links
        };

        Response.AddPaginationHeaders(response);

        return Ok(response);
    }

    // Example: Backward navigation support
    [HttpGet("bidirectional")]
    public async Task<ActionResult<PagedResponse<ExampleItem>>> BidirectionalPagination(
        [FromQuery] int? limit,
        [FromQuery] string? pageToken,
        [FromQuery] string? direction, // "forward" or "backward"
        CancellationToken cancellationToken)
    {
        var pageSize = Math.Min(limit ?? 100, 1000);
        var isBackward = string.Equals(direction, "backward", StringComparison.OrdinalIgnoreCase);

        var (_, cursorId) = PaginationHelper.ParseCursorToken(pageToken);

        var items = isBackward
            ? await GetItemsBeforeCursorAsync(cursorId, pageSize, cancellationToken)
            : await GetItemsAfterCursorAsync(cursorId, pageSize, cancellationToken);

        var hasMore = items.Count >= pageSize;

        string? nextCursor = null;
        string? prevCursor = null;

        if (hasMore && items.Count > 0)
        {
            var lastItem = items[^1];
            var firstItem = items[0];

            if (isBackward)
            {
                prevCursor = PaginationHelper.GenerateCursorToken("default", firstItem.Id);
            }
            else
            {
                nextCursor = PaginationHelper.GenerateCursorToken("default", lastItem.Id);
            }

            // Always provide opposite direction cursor
            if (!string.IsNullOrEmpty(cursorId))
            {
                if (isBackward)
                {
                    nextCursor = pageToken;
                }
                else
                {
                    prevCursor = PaginationHelper.GenerateCursorToken("default", firstItem.Id);
                }
            }
        }

        var response = PaginationBuilder
            .Create(items)
            .WithNextPageToken(nextCursor)
            .WithPreviousPageToken(prevCursor)
            .WithLinks(Request, "/api/v1/examples/bidirectional")
            .Build();

        Response.AddPaginationHeaders(response);

        return Ok(response);
    }

    // Example: Empty result set
    [HttpGet("empty")]
    public ActionResult<PagedResponse<ExampleItem>> EmptyResults()
    {
        var response = PaginationBuilder
            .Create<ExampleItem>(new List<ExampleItem>())
            .WithTotalCount(0)
            .WithLinks(Request, "/api/v1/examples/empty")
            .Build();

        Response.AddPaginationHeaders(response);

        return Ok(response);
    }

    // Example: Integration with existing PaginatedResponse<T>
    [HttpGet("legacy-compat")]
    public async Task<ActionResult> LegacyCompatibility(
        [FromQuery] int? limit,
        [FromQuery] string? pageToken,
        CancellationToken cancellationToken)
    {
        var pageSize = Math.Min(limit ?? 100, 1000);
        var (_, lastItemId) = PaginationHelper.ParseCursorToken(pageToken);

        var items = await GetItemsFromRepositoryAsync(lastItemId, pageSize, cancellationToken);
        var totalCount = await GetTotalCountAsync(cancellationToken);
        var hasMore = items.Count >= pageSize;

        string? nextCursor = hasMore && items.Count > 0
            ? PaginationHelper.GenerateCursorToken("default", items[^1].Id)
            : null;

        // Create modern PagedResponse
        var pagedResponse = PaginationBuilder
            .Create(items)
            .WithTotalCount(totalCount)
            .WithNextPageToken(nextCursor)
            .WithLinks(Request, "/api/v1/examples/legacy-compat")
            .Build();

        // Add Link headers for modern clients
        Response.AddPaginationHeaders(pagedResponse);

        // Return as legacy PaginatedResponse for backward compatibility
        var legacyResponse = new Admin.PaginatedResponse<ExampleItem>(
            pagedResponse.Items,
            pagedResponse.TotalCount ?? 0,
            pagedResponse.NextPageToken
        );

        return Ok(legacyResponse);
    }

    // Example: Multiple sorting with cursor
    [HttpGet("multi-sort")]
    public async Task<ActionResult<PagedResponse<ExampleItem>>> MultiSortPagination(
        [FromQuery] string? sortBy, // e.g., "created_at,id"
        [FromQuery] int? limit,
        [FromQuery] string? pageToken,
        CancellationToken cancellationToken)
    {
        var pageSize = Math.Min(limit ?? 100, 1000);

        // For multi-column sorting, cursor token would encode multiple values
        // Example: Base64("{\"created_at\":\"2025-01-15T10:30:00Z\",\"id\":\"123\"}")
        var cursor = ParseMultiColumnCursor(pageToken);

        var items = await GetItemsWithMultiSortAsync(sortBy, cursor, pageSize, cancellationToken);
        var hasMore = items.Count >= pageSize;

        string? nextCursor = null;
        if (hasMore && items.Count > 0)
        {
            nextCursor = GenerateMultiColumnCursor(items[^1], sortBy);
        }

        var queryParams = new Dictionary<string, string?>
        {
            ["sortBy"] = sortBy,
            ["limit"] = pageSize.ToString()
        };

        var response = PaginationBuilder
            .Create(items)
            .WithNextPageToken(nextCursor)
            .WithLinks(Request, "/api/v1/examples/multi-sort", queryParams)
            .Build();

        Response.AddPaginationHeaders(response);

        return Ok(response);
    }

    #region Helper Methods (Simulated Repository Operations)

    private Task<List<ExampleItem>> GetItemsFromRepositoryAsync(string? afterId, int limit, CancellationToken ct)
    {
        // Simulated database query with cursor
        var allItems = GenerateSampleItems(1000);
        var startIndex = string.IsNullOrEmpty(afterId)
            ? 0
            : allItems.FindIndex(i => i.Id == afterId) + 1;

        var items = allItems
            .Skip(startIndex)
            .Take(limit)
            .ToList();

        return Task.FromResult(items);
    }

    private Task<List<ExampleItem>> GetItemsWithOffsetAsync(int offset, int limit, CancellationToken ct)
    {
        var allItems = GenerateSampleItems(1000);
        var items = allItems
            .Skip(offset)
            .Take(limit)
            .ToList();

        return Task.FromResult(items);
    }

    private Task<List<ExampleItem>> SearchItemsAsync(
        string? query,
        string? status,
        string? category,
        string? afterId,
        int limit,
        CancellationToken ct)
    {
        var allItems = GenerateSampleItems(1000);

        var filtered = allItems.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query))
            filtered = filtered.Where(i => i.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(status))
            filtered = filtered.Where(i => i.Status == status);

        if (!string.IsNullOrWhiteSpace(category))
            filtered = filtered.Where(i => i.Category == category);

        if (!string.IsNullOrEmpty(afterId))
        {
            var startIndex = allItems.FindIndex(i => i.Id == afterId);
            if (startIndex >= 0)
                filtered = filtered.Skip(startIndex + 1);
        }

        return Task.FromResult(filtered.Take(limit).ToList());
    }

    private Task<List<ExampleItem>> GetItemsAfterCursorAsync(string? cursorId, int limit, CancellationToken ct)
    {
        return GetItemsFromRepositoryAsync(cursorId, limit, ct);
    }

    private Task<List<ExampleItem>> GetItemsBeforeCursorAsync(string? cursorId, int limit, CancellationToken ct)
    {
        var allItems = GenerateSampleItems(1000);
        var endIndex = string.IsNullOrEmpty(cursorId)
            ? allItems.Count
            : allItems.FindIndex(i => i.Id == cursorId);

        var items = allItems
            .Take(Math.Max(0, endIndex))
            .TakeLast(limit)
            .ToList();

        return Task.FromResult(items);
    }

    private Task<List<ExampleItem>> GetItemsWithMultiSortAsync(
        string? sortBy,
        Dictionary<string, string>? cursor,
        int limit,
        CancellationToken ct)
    {
        // Simulated multi-column sorting
        return GetItemsFromRepositoryAsync(cursor?.GetValueOrDefault("id"), limit, ct);
    }

    private Task<int> GetTotalCountAsync(CancellationToken ct)
    {
        return Task.FromResult(1000);
    }

    private List<ExampleItem> GenerateSampleItems(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => new ExampleItem
            {
                Id = i.ToString(),
                Name = $"Item {i}",
                Status = i % 3 == 0 ? "active" : "inactive",
                Category = $"Category {i % 5}",
                CreatedAt = DateTime.UtcNow.AddDays(-i)
            })
            .ToList();
    }

    private Dictionary<string, string>? ParseMultiColumnCursor(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        // Simplified: In production, decode Base64 JSON
        return new Dictionary<string, string>();
    }

    private string GenerateMultiColumnCursor(ExampleItem item, string? sortBy)
    {
        // Simplified: In production, encode multiple sort column values as Base64 JSON
        return PaginationHelper.GenerateCursorToken("default", item.Id);
    }

    #endregion
}

/// <summary>
/// Example item model for demonstration purposes.
/// </summary>
public class ExampleItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
#endif
