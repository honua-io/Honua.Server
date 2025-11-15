// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Pagination;

/// <summary>
/// Builder for constructing standardized PagedResponse objects with HATEOAS links.
/// </summary>
/// <remarks>
/// <para>
/// Provides fluent API for building paginated responses that comply with Microsoft Azure REST API
/// Guidelines, Google AIP-158, and RFC 5988 (Web Linking).
/// </para>
/// <para>
/// Supports both cursor-based and offset-based pagination strategies, automatically generating
/// appropriate pagination tokens and navigation links.
/// </para>
/// </remarks>
/// <example>
/// <para>Example usage with cursor-based pagination:</para>
/// <code>
/// var response = PaginationBuilder
///     .Create(items)
///     .WithTotalCount(1000)
///     .WithCursorToken(nextCursor)
///     .WithLinks(request, "/api/items")
///     .Build();
/// </code>
/// <para>Example usage with offset-based pagination:</para>
/// <code>
/// var response = PaginationBuilder
///     .Create(items)
///     .WithTotalCount(1000)
///     .WithOffsetPagination(offset: 100, limit: 50, hasMore: true)
///     .WithLinks(request, "/api/items")
///     .Build();
/// </code>
/// </example>
public sealed class PaginationBuilder<T>
{
    private readonly IReadOnlyList<T> items;
    private int? totalCount;
    private string? nextPageToken;
    private string? previousPageToken;
    private PaginationLinks? links;

    private PaginationBuilder(IReadOnlyList<T> items)
    {
        this.items = Guard.NotNull(items);
    }

    /// <summary>
    /// Creates a new pagination builder with the given items.
    /// </summary>
    /// <param name="items">The items for the current page.</param>
    /// <returns>A new pagination builder instance.</returns>
    public static PaginationBuilder<T> Create(IReadOnlyList<T> items)
    {
        return new PaginationBuilder<T>(items);
    }

    /// <summary>
    /// Sets the total count of items across all pages.
    /// </summary>
    /// <param name="count">The total count, or null if unknown.</param>
    /// <returns>The builder instance for chaining.</returns>
    /// <remarks>
    /// Total count may be null for performance reasons when dealing with large datasets.
    /// Some protocols (OGC API Features) require total count, while others (STAC) make it optional.
    /// </remarks>
    public PaginationBuilder<T> WithTotalCount(int? count)
    {
        this.totalCount = count;
        return this;
    }

    /// <summary>
    /// Sets the next page token for cursor-based pagination.
    /// </summary>
    /// <param name="token">The opaque page token, or null if this is the last page.</param>
    /// <returns>The builder instance for chaining.</returns>
    /// <remarks>
    /// The token should be a Base64-encoded string that can be used to retrieve the next page.
    /// Use <see cref="PaginationHelper.GenerateCursorToken"/> for STAC-style tokens or
    /// <see cref="PaginationHelper.GenerateOffsetToken"/> for offset-based tokens.
    /// </remarks>
    public PaginationBuilder<T> WithNextPageToken(string? token)
    {
        this.nextPageToken = token;
        return this;
    }

    /// <summary>
    /// Sets the previous page token for backward navigation.
    /// </summary>
    /// <param name="token">The opaque page token, or null if this is the first page.</param>
    /// <returns>The builder instance for chaining.</returns>
    /// <remarks>
    /// Previous page tokens are optional and may not be supported by all implementations.
    /// Cursor-based pagination typically doesn't support backward navigation unless
    /// explicitly designed for it.
    /// </remarks>
    public PaginationBuilder<T> WithPreviousPageToken(string? token)
    {
        this.previousPageToken = token;
        return this;
    }

    /// <summary>
    /// Configures offset-based pagination and automatically generates the next page token.
    /// </summary>
    /// <param name="offset">The current page offset (0-based).</param>
    /// <param name="limit">The page size limit.</param>
    /// <param name="hasMore">Whether more pages exist after the current page.</param>
    /// <returns>The builder instance for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This is a convenience method that automatically generates offset-based page tokens
    /// using <see cref="PaginationHelper.GenerateOffsetToken"/>.
    /// </para>
    /// <para>
    /// Note: Offset-based pagination has O(N) performance characteristics where N is the
    /// page depth. Consider using cursor-based pagination for better performance with large datasets.
    /// </para>
    /// </remarks>
    public PaginationBuilder<T> WithOffsetPagination(int offset, int limit, bool hasMore)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset must be non-negative.");
        }

        if (limit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be at least 1.");
        }

        // Generate next page token if more pages exist
        if (hasMore)
        {
            var nextOffset = offset + limit;
            this.nextPageToken = PaginationHelper.GenerateOffsetToken(nextOffset, limit);
        }

        // Generate previous page token if not on first page
        if (offset > 0)
        {
            var prevOffset = Math.Max(0, offset - limit);
            this.previousPageToken = PaginationHelper.GenerateOffsetToken(prevOffset, limit);
        }

        return this;
    }

    /// <summary>
    /// Configures cursor-based pagination with automatic token generation.
    /// </summary>
    /// <param name="collectionId">The collection ID of the last item (for STAC-style pagination).</param>
    /// <param name="itemId">The item ID of the last item (for STAC-style pagination).</param>
    /// <param name="hasMore">Whether more pages exist after the current page.</param>
    /// <returns>The builder instance for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This is a convenience method for STAC-style cursor pagination that uses
    /// "collectionId:itemId" tokens. For other cursor formats, use <see cref="WithNextPageToken"/>
    /// and generate the token manually.
    /// </para>
    /// <para>
    /// Cursor-based pagination provides O(1) performance regardless of page depth,
    /// making it ideal for large datasets and real-time data.
    /// </para>
    /// </remarks>
    public PaginationBuilder<T> WithCursorPagination(string? collectionId, string? itemId, bool hasMore)
    {
        if (hasMore && !string.IsNullOrWhiteSpace(collectionId) && !string.IsNullOrWhiteSpace(itemId))
        {
            this.nextPageToken = PaginationHelper.GenerateCursorToken(collectionId, itemId);
        }

        return this;
    }

    /// <summary>
    /// Sets custom pagination links.
    /// </summary>
    /// <param name="paginationLinks">The pagination links.</param>
    /// <returns>The builder instance for chaining.</returns>
    public PaginationBuilder<T> WithLinks(PaginationLinks? paginationLinks)
    {
        this.links = paginationLinks;
        return this;
    }

    /// <summary>
    /// Automatically generates pagination links based on the current request and pagination state.
    /// </summary>
    /// <param name="request">The current HTTP request.</param>
    /// <param name="basePath">The base path for the resource (e.g., "/api/items").</param>
    /// <param name="queryParameters">Optional query parameters to include in links.</param>
    /// <returns>The builder instance for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Automatically generates RFC 5988 compliant links for:
    /// </para>
    /// <list type="bullet">
    ///   <item>self: Current page</item>
    ///   <item>next: Next page (if nextPageToken is set)</item>
    ///   <item>prev: Previous page (if previousPageToken is set)</item>
    ///   <item>first: First page</item>
    ///   <item>last: Last page (if total count is known and using offset pagination)</item>
    /// </list>
    /// </remarks>
    public PaginationBuilder<T> WithLinks(
        HttpRequest request,
        string? basePath = null,
        IDictionary<string, string?>? queryParameters = null)
    {
        Guard.NotNull(request);

        var path = basePath ?? request.Path.Value ?? "/";
        var baseParams = queryParameters ?? new Dictionary<string, string?>();

        var linkBuilder = new PaginationLinks
        {
            Self = BuildLinkUrl(request, path, baseParams, null),
            Next = this.nextPageToken != null
                ? BuildLinkUrl(request, path, baseParams, new Dictionary<string, string?> { ["pageToken"] = this.nextPageToken })
                : null,
            Previous = this.previousPageToken != null
                ? BuildLinkUrl(request, path, baseParams, new Dictionary<string, string?> { ["pageToken"] = this.previousPageToken })
                : null,
            First = BuildLinkUrl(request, path, baseParams, new Dictionary<string, string?> { ["pageToken"] = null }),
            Last = null // Last link only available for offset pagination with known total count
        };

        this.links = linkBuilder;
        return this;
    }

    /// <summary>
    /// Automatically generates offset-based pagination links.
    /// </summary>
    /// <param name="request">The current HTTP request.</param>
    /// <param name="basePath">The base path for the resource.</param>
    /// <param name="offset">The current offset.</param>
    /// <param name="limit">The page size limit.</param>
    /// <param name="queryParameters">Optional query parameters to preserve in links.</param>
    /// <returns>The builder instance for chaining.</returns>
    /// <remarks>
    /// Generates links using offset and limit query parameters instead of page tokens.
    /// Useful for APIs that expose offset/limit directly rather than opaque tokens.
    /// </remarks>
    public PaginationBuilder<T> WithOffsetLinks(
        HttpRequest request,
        string? basePath,
        int offset,
        int limit,
        IDictionary<string, string?>? queryParameters = null)
    {
        Guard.NotNull(request);

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset must be non-negative.");
        }

        if (limit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be at least 1.");
        }

        var path = basePath ?? request.Path.Value ?? "/";
        var baseParams = queryParameters ?? new Dictionary<string, string?>();

        // Add limit to all links
        var paramsWithLimit = new Dictionary<string, string?>(baseParams)
        {
            ["limit"] = limit.ToString(CultureInfo.InvariantCulture)
        };

        var linkBuilder = new PaginationLinks
        {
            Self = BuildLinkUrl(request, path, paramsWithLimit, new Dictionary<string, string?> { ["offset"] = offset.ToString(CultureInfo.InvariantCulture) }),
            First = BuildLinkUrl(request, path, paramsWithLimit, new Dictionary<string, string?> { ["offset"] = "0" })
        };

        // Next link
        if (this.nextPageToken != null)
        {
            var nextOffset = offset + limit;
            linkBuilder = linkBuilder with
            {
                Next = BuildLinkUrl(request, path, paramsWithLimit, new Dictionary<string, string?> { ["offset"] = nextOffset.ToString(CultureInfo.InvariantCulture) })
            };
        }

        // Previous link
        if (offset > 0)
        {
            var prevOffset = Math.Max(0, offset - limit);
            linkBuilder = linkBuilder with
            {
                Previous = BuildLinkUrl(request, path, paramsWithLimit, new Dictionary<string, string?> { ["offset"] = prevOffset.ToString(CultureInfo.InvariantCulture) })
            };
        }

        // Last link (only if total count is known)
        if (this.totalCount.HasValue)
        {
            var lastOffset = Math.Max(0, ((this.totalCount.Value - 1) / limit) * limit);
            linkBuilder = linkBuilder with
            {
                Last = BuildLinkUrl(request, path, paramsWithLimit, new Dictionary<string, string?> { ["offset"] = lastOffset.ToString(CultureInfo.InvariantCulture) })
            };
        }

        this.links = linkBuilder;
        return this;
    }

    /// <summary>
    /// Builds the final PagedResponse object.
    /// </summary>
    /// <returns>A configured PagedResponse instance.</returns>
    public PagedResponse<T> Build()
    {
        return new PagedResponse<T>
        {
            Items = this.items,
            TotalCount = this.totalCount,
            NextPageToken = this.nextPageToken,
            PreviousPageToken = this.previousPageToken,
            Links = this.links
        };
    }

    /// <summary>
    /// Builds a URL for a pagination link by merging base parameters and overrides.
    /// </summary>
    private static string BuildLinkUrl(
        HttpRequest request,
        string path,
        IDictionary<string, string?> baseParams,
        IDictionary<string, string?>? overrides)
    {
        var mergedParams = new Dictionary<string, string?>(baseParams, StringComparer.OrdinalIgnoreCase);

        if (overrides != null)
        {
            foreach (var kvp in overrides)
            {
                if (kvp.Value == null)
                {
                    mergedParams.Remove(kvp.Key);
                }
                else
                {
                    mergedParams[kvp.Key] = kvp.Value;
                }
            }
        }

        return request.BuildAbsoluteUrl(path, mergedParams);
    }
}

/// <summary>
/// Static factory methods for creating pagination builders.
/// </summary>
public static class PaginationBuilder
{
    /// <summary>
    /// Creates a new pagination builder with the given items.
    /// </summary>
    /// <typeparam name="T">The type of items in the response.</typeparam>
    /// <param name="items">The items for the current page.</param>
    /// <returns>A new pagination builder instance.</returns>
    public static PaginationBuilder<T> Create<T>(IReadOnlyList<T> items)
    {
        return PaginationBuilder<T>.Create(items);
    }
}
