// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Server.Host.Pagination;

/// <summary>
/// Represents a standardized paginated response following Microsoft Azure REST API Guidelines
/// and Google AIP-158 (List Pagination).
/// </summary>
/// <typeparam name="T">The type of items in the response.</typeparam>
/// <remarks>
/// <para><strong>Design Philosophy:</strong></para>
/// <para>
/// This response model provides a unified pagination interface across all REST APIs in Honua.Server,
/// supporting both cursor-based (recommended) and offset-based pagination strategies.
/// </para>
/// <para><strong>Pagination Strategy:</strong></para>
/// <list type="bullet">
///   <item>
///     <description>
///       <strong>Cursor-based (Recommended):</strong> Uses opaque page tokens for O(1) performance
///       regardless of page depth. Ideal for large datasets and real-time data.
///     </description>
///   </item>
///   <item>
///     <description>
///       <strong>Offset-based (Legacy):</strong> Uses numeric offsets. Performance degrades with
///       page depth but supports random access. Suitable for small datasets (&lt;10k records).
///     </description>
///   </item>
/// </list>
/// <para><strong>Protocol Compliance:</strong></para>
/// <list type="bullet">
///   <item>
///     <description>
///       Microsoft Azure REST API Guidelines: Supports nextLink and value fields for Azure-style APIs.
///     </description>
///   </item>
///   <item>
///     <description>
///       Google AIP-158: Supports next_page_token for Google Cloud-style APIs.
///     </description>
///   </item>
///   <item>
///     <description>
///       RFC 5988: Supports Link headers via PaginationLinks for hypermedia-driven APIs.
///     </description>
///   </item>
/// </list>
/// <para><strong>Token Format:</strong></para>
/// <para>
/// Page tokens are opaque, Base64-encoded strings that should never be parsed or constructed
/// by clients. The internal format may include:
/// </para>
/// <list type="bullet">
///   <item>Cursor values (e.g., last item ID, timestamp)</item>
///   <item>Sort order information</item>
///   <item>Filter criteria (if applicable)</item>
/// </list>
/// <para><strong>Token Expiration:</strong></para>
/// <para>
/// Page tokens are valid indefinitely for the same dataset state. However, if data is modified
/// (records added/deleted), tokens may return slightly different results. This is expected behavior
/// and provides more consistent results than offset-based pagination.
/// </para>
/// <para><strong>Maximum Page Size:</strong></para>
/// <para>
/// The maximum page size is protocol-dependent:
/// </para>
/// <list type="bullet">
///   <item>STAC API: 1000 items (StacConstants.MaxSearchLimit)</item>
///   <item>OGC API Features: 10000 items</item>
///   <item>Admin API: 1000 items</item>
///   <item>Default: 100 items when not specified</item>
/// </list>
/// <para><strong>Behavior When Page Size Changes:</strong></para>
/// <para>
/// If a client changes the page size (limit) while using a page token from a previous request
/// with a different limit, the server will:
/// </para>
/// <list type="number">
///   <item>Honor the new page size for the current request</item>
///   <item>Continue from the position indicated by the token</item>
///   <item>Return items starting after the token's cursor position</item>
/// </list>
/// <para>
/// Note: Some implementations may invalidate tokens if the page size changes to maintain
/// consistency. Check protocol-specific documentation.
/// </para>
/// </remarks>
/// <example>
/// <para>Example JSON response with cursor-based pagination:</para>
/// <code>
/// {
///   "items": [
///     { "id": "1", "name": "Item 1" },
///     { "id": "2", "name": "Item 2" }
///   ],
///   "totalCount": 1000,
///   "nextPageToken": "eyJpZCI6IjIiLCJ0aW1lc3RhbXAiOiIyMDI1LTAxLTE1VDEwOjMwOjAwWiJ9",
///   "links": {
///     "self": "https://api.example.com/items?limit=100",
///     "next": "https://api.example.com/items?limit=100&pageToken=eyJpZCI6IjIi...",
///     "first": "https://api.example.com/items?limit=100"
///   }
/// }
/// </code>
/// </example>
public sealed record PagedResponse<T>
{
    /// <summary>
    /// Gets the collection of items for the current page.
    /// </summary>
    /// <remarks>
    /// This property always contains a non-null collection, which may be empty if no items
    /// match the query criteria or if navigating beyond the last page.
    /// </remarks>
    [JsonPropertyName("items")]
    public IReadOnlyList<T> Items { get; init; } = new List<T>();

    /// <summary>
    /// Gets the total number of items across all pages, if known.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This field may be null for performance reasons, especially for large datasets where
    /// executing a COUNT query would be expensive. Protocols that require total counts
    /// (e.g., OGC API Features) will always populate this field.
    /// </para>
    /// <para>
    /// When using cursor-based pagination, this value represents the count at the time
    /// of the first request and may not reflect real-time changes to the dataset.
    /// </para>
    /// </remarks>
    [JsonPropertyName("totalCount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TotalCount { get; init; }

    /// <summary>
    /// Gets the opaque token for retrieving the next page of results.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This token should be passed as the 'pageToken' or 'token' query parameter in the
    /// next request. The token is opaque and must not be parsed or constructed by clients.
    /// </para>
    /// <para>
    /// If null, there are no more pages available (current page is the last page).
    /// </para>
    /// <para>
    /// Token format examples:
    /// </para>
    /// <list type="bullet">
    ///   <item>STAC: Base64-encoded "collectionId:itemId"</item>
    ///   <item>Admin: Base64-encoded cursor position</item>
    ///   <item>OGC: Base64-encoded offset and limit (legacy)</item>
    /// </list>
    /// </remarks>
    [JsonPropertyName("nextPageToken")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NextPageToken { get; init; }

    /// <summary>
    /// Gets the opaque token for retrieving the previous page of results.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This token should be passed as the 'pageToken' or 'token' query parameter along with
    /// 'direction=backward' to navigate to the previous page. Optional field that may not be
    /// supported by all implementations.
    /// </para>
    /// <para>
    /// If null, there is no previous page (current page is the first page or backward navigation
    /// is not supported).
    /// </para>
    /// </remarks>
    [JsonPropertyName("previousPageToken")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PreviousPageToken { get; init; }

    /// <summary>
    /// Gets the RFC 5988 Web Linking (HATEOAS) links for pagination and related resources.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides hypermedia links following RFC 5988 standard. These links enable clients to
    /// navigate the API without hardcoding URLs. Common link relations include:
    /// </para>
    /// <list type="bullet">
    ///   <item>self: Current page URL</item>
    ///   <item>next: Next page URL</item>
    ///   <item>prev/previous: Previous page URL</item>
    ///   <item>first: First page URL</item>
    ///   <item>last: Last page URL (if total count is known)</item>
    /// </list>
    /// <para>
    /// Some protocols (OGC API, STAC) prefer Link headers over embedded links. Use
    /// <see cref="PaginationExtensions.AddPaginationHeaders"/> to add HTTP Link headers.
    /// </para>
    /// </remarks>
    [JsonPropertyName("links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PaginationLinks? Links { get; init; }
}

/// <summary>
/// Represents RFC 5988 Web Linking links for pagination navigation.
/// </summary>
/// <remarks>
/// <para>
/// Implements RFC 5988 (Web Linking) and RFC 8288 (Link header) standards for HATEOAS-style
/// navigation. Links can be embedded in JSON responses or sent as HTTP Link headers.
/// </para>
/// <para>
/// Link relation types follow IANA Link Relations registry:
/// https://www.iana.org/assignments/link-relations/link-relations.xhtml
/// </para>
/// </remarks>
/// <example>
/// <para>Example JSON representation:</para>
/// <code>
/// {
///   "self": "https://api.example.com/items?limit=100&amp;pageToken=abc",
///   "next": "https://api.example.com/items?limit=100&amp;pageToken=def",
///   "previous": "https://api.example.com/items?limit=100&amp;pageToken=ghi",
///   "first": "https://api.example.com/items?limit=100",
///   "last": "https://api.example.com/items?limit=100&amp;pageToken=xyz"
/// }
/// </code>
/// <para>Example HTTP Link header (RFC 5988):</para>
/// <code>
/// Link: &lt;https://api.example.com/items?limit=100&amp;pageToken=def&gt;; rel="next",
///       &lt;https://api.example.com/items?limit=100&amp;pageToken=ghi&gt;; rel="prev",
///       &lt;https://api.example.com/items?limit=100&gt;; rel="first"
/// </code>
/// </example>
public sealed record PaginationLinks
{
    /// <summary>
    /// Gets the URL of the current page (rel="self").
    /// </summary>
    /// <remarks>
    /// Canonical URL for the current resource. Useful for bookmarking, caching, and identifying
    /// the current request.
    /// </remarks>
    [JsonPropertyName("self")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Self { get; init; }

    /// <summary>
    /// Gets the URL of the next page (rel="next").
    /// </summary>
    /// <remarks>
    /// If null, the current page is the last page or there are no more results available.
    /// </remarks>
    [JsonPropertyName("next")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Next { get; init; }

    /// <summary>
    /// Gets the URL of the previous page (rel="prev" or rel="previous").
    /// </summary>
    /// <remarks>
    /// If null, the current page is the first page or backward navigation is not supported.
    /// Note: Both "prev" and "previous" are valid per RFC 5988, but "prev" is more common.
    /// </remarks>
    [JsonPropertyName("previous")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Previous { get; init; }

    /// <summary>
    /// Gets the URL of the first page (rel="first").
    /// </summary>
    /// <remarks>
    /// Allows clients to reset pagination to the beginning. Always available unless the
    /// implementation doesn't support first page navigation.
    /// </remarks>
    [JsonPropertyName("first")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? First { get; init; }

    /// <summary>
    /// Gets the URL of the last page (rel="last").
    /// </summary>
    /// <remarks>
    /// Only available when total count is known and the implementation supports direct navigation
    /// to the last page. Not available for cursor-based pagination without total count.
    /// </remarks>
    [JsonPropertyName("last")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Last { get; init; }
}
