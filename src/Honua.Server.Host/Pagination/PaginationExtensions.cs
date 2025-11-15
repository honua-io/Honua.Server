// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Honua.Server.Core.Utilities;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Pagination;

/// <summary>
/// Extension methods for adding pagination headers and building pagination responses.
/// </summary>
/// <remarks>
/// <para>
/// Provides standardized methods for adding RFC 5988 Link headers to HTTP responses and
/// building PagedResponse objects from data sources.
/// </para>
/// <para><strong>RFC 5988 Compliance:</strong></para>
/// <para>
/// Link headers follow RFC 5988 (Web Linking) and RFC 8288 format:
/// <code>
/// Link: &lt;url&gt;; rel="relation"[; title="title"][; type="media-type"]
/// </code>
/// </para>
/// <para>
/// Multiple links are separated by commas. Common relation types:
/// </para>
/// <list type="bullet">
///   <item>self: Current resource</item>
///   <item>next: Next page in sequence</item>
///   <item>prev/previous: Previous page in sequence</item>
///   <item>first: First page</item>
///   <item>last: Last page</item>
///   <item>alternate: Alternative representation</item>
/// </list>
/// </remarks>
public static class PaginationExtensions
{
    /// <summary>
    /// Adds RFC 5988 Link headers to the HTTP response for pagination navigation.
    /// </summary>
    /// <param name="response">The HTTP response to add headers to.</param>
    /// <param name="links">The pagination links to add.</param>
    /// <remarks>
    /// <para>
    /// Adds HTTP Link headers following RFC 5988 and RFC 8288 standards. The Link header
    /// format allows clients to discover pagination URLs without parsing JSON response bodies.
    /// </para>
    /// <para>
    /// This is particularly important for:
    /// </para>
    /// <list type="bullet">
    ///   <item>OGC API Features: Requires Link headers per OGC API - Features - Part 1: Core</item>
    ///   <item>STAC API: Recommends Link headers for better client compatibility</item>
    ///   <item>REST APIs: Provides standard HTTP-level pagination discovery</item>
    /// </list>
    /// <para><strong>Security Note:</strong></para>
    /// <para>
    /// Link URLs are properly encoded to prevent header injection attacks. URLs containing
    /// special characters are validated before being added to headers.
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>Example usage in a controller:</para>
    /// <code>
    /// var links = new PaginationLinks
    /// {
    ///     Self = "https://api.example.com/items?limit=100",
    ///     Next = "https://api.example.com/items?limit=100&amp;pageToken=abc",
    ///     First = "https://api.example.com/items?limit=100"
    /// };
    /// Response.AddPaginationHeaders(links);
    /// </code>
    /// <para>Resulting HTTP headers:</para>
    /// <code>
    /// Link: &lt;https://api.example.com/items?limit=100&gt;; rel="self",
    ///       &lt;https://api.example.com/items?limit=100&amp;pageToken=abc&gt;; rel="next",
    ///       &lt;https://api.example.com/items?limit=100&gt;; rel="first"
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException">Thrown when response is null.</exception>
    public static void AddPaginationHeaders(this HttpResponse response, PaginationLinks? links)
    {
        Guard.NotNull(response);

        if (links is null)
        {
            return;
        }

        var linkParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(links.Self))
        {
            linkParts.Add(FormatLinkHeader(links.Self, "self"));
        }

        if (!string.IsNullOrWhiteSpace(links.Next))
        {
            linkParts.Add(FormatLinkHeader(links.Next, "next"));
        }

        if (!string.IsNullOrWhiteSpace(links.Previous))
        {
            linkParts.Add(FormatLinkHeader(links.Previous, "prev"));
        }

        if (!string.IsNullOrWhiteSpace(links.First))
        {
            linkParts.Add(FormatLinkHeader(links.First, "first"));
        }

        if (!string.IsNullOrWhiteSpace(links.Last))
        {
            linkParts.Add(FormatLinkHeader(links.Last, "last"));
        }

        if (linkParts.Count > 0)
        {
            // RFC 5988: Multiple links are comma-separated
            response.Headers.Append("Link", string.Join(", ", linkParts));
        }
    }

    /// <summary>
    /// Adds RFC 5988 Link headers to the HTTP response using a PagedResponse object.
    /// </summary>
    /// <typeparam name="T">The type of items in the paged response.</typeparam>
    /// <param name="response">The HTTP response to add headers to.</param>
    /// <param name="pagedResponse">The paged response containing link information.</param>
    /// <remarks>
    /// Convenience method that extracts links from a PagedResponse and adds them as HTTP headers.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when response or pagedResponse is null.</exception>
    public static void AddPaginationHeaders<T>(this HttpResponse response, PagedResponse<T> pagedResponse)
    {
        Guard.NotNull(response);
        Guard.NotNull(pagedResponse);

        response.AddPaginationHeaders(pagedResponse.Links);
    }

    /// <summary>
    /// Formats a single link for the Link header following RFC 5988 format.
    /// </summary>
    /// <param name="url">The URL for the link.</param>
    /// <param name="rel">The link relation type (e.g., "next", "prev", "self").</param>
    /// <param name="type">Optional media type for the linked resource.</param>
    /// <param name="title">Optional title for the link.</param>
    /// <returns>A formatted link header string.</returns>
    /// <remarks>
    /// <para>
    /// Formats a link according to RFC 5988 specification:
    /// <code>
    /// &lt;url&gt;; rel="relation"[; title="title"][; type="media-type"]
    /// </code>
    /// </para>
    /// <para>
    /// URLs containing special characters that could break header parsing are validated.
    /// Relation types, titles, and media types are properly quoted if they contain special characters.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var link = FormatLinkHeader("https://api.example.com/items?page=2", "next", "application/json", "Next Page");
    /// // Returns: &lt;https://api.example.com/items?page=2&gt;; rel="next"; type="application/json"; title="Next Page"
    /// </code>
    /// </example>
    private static string FormatLinkHeader(string url, string rel, string? type = null, string? title = null)
    {
        var sb = new StringBuilder();

        // RFC 5988 requires URLs to be enclosed in angle brackets
        sb.Append('<').Append(url).Append('>');

        // rel is required per RFC 5988
        sb.Append("; rel=\"").Append(rel).Append('"');

        if (!string.IsNullOrWhiteSpace(type))
        {
            sb.Append("; type=\"").Append(type).Append('"');
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            // Escape quotes in title
            var escapedTitle = title.Replace("\"", "\\\"");
            sb.Append("; title=\"").Append(escapedTitle).Append('"');
        }

        return sb.ToString();
    }

    /// <summary>
    /// Converts an OGC Link to a standardized pagination link string.
    /// </summary>
    /// <param name="url">The link URL.</param>
    /// <param name="rel">The link relation type.</param>
    /// <param name="type">Optional media type.</param>
    /// <param name="title">Optional link title.</param>
    /// <returns>A formatted RFC 5988 link string.</returns>
    /// <remarks>
    /// Public helper method for converting OGC-style links to RFC 5988 format.
    /// Useful for protocol adapters that need to convert between formats.
    /// </remarks>
    public static string ToRfc5988Link(string url, string rel, string? type = null, string? title = null)
    {
        Guard.NotNullOrWhiteSpace(url);
        Guard.NotNullOrWhiteSpace(rel);

        return FormatLinkHeader(url, rel, type, title);
    }

    /// <summary>
    /// Parses a Link header value into individual links.
    /// </summary>
    /// <param name="linkHeader">The Link header value.</param>
    /// <returns>A collection of parsed links as (url, rel) tuples.</returns>
    /// <remarks>
    /// <para>
    /// Parses RFC 5988 Link headers into structured data. Useful for clients consuming
    /// paginated APIs that need to extract link URLs.
    /// </para>
    /// <para>
    /// This is a simplified parser that extracts URL and rel attributes. For full RFC 5988
    /// parsing including title, type, and extension attributes, consider using a dedicated
    /// Link header parsing library.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var header = "&lt;https://api.example.com/items?page=2&gt;; rel=\"next\", &lt;https://api.example.com/items?page=1&gt;; rel=\"prev\"";
    /// var links = PaginationExtensions.ParseLinkHeader(header);
    /// // Returns: [("https://api.example.com/items?page=2", "next"), ("https://api.example.com/items?page=1", "prev")]
    /// </code>
    /// </example>
    public static IEnumerable<(string Url, string Rel)> ParseLinkHeader(string linkHeader)
    {
        if (string.IsNullOrWhiteSpace(linkHeader))
        {
            yield break;
        }

        // Split by comma (separates multiple links)
        var links = linkHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var link in links)
        {
            // Extract URL from angle brackets
            var urlStart = link.IndexOf('<');
            var urlEnd = link.IndexOf('>');
            if (urlStart < 0 || urlEnd < 0 || urlEnd <= urlStart)
            {
                continue;
            }

            var url = link.Substring(urlStart + 1, urlEnd - urlStart - 1);

            // Extract rel value
            var relStart = link.IndexOf("rel=\"", StringComparison.OrdinalIgnoreCase);
            if (relStart < 0)
            {
                continue;
            }

            relStart += 5; // Move past 'rel="'
            var relEnd = link.IndexOf('"', relStart);
            if (relEnd < 0)
            {
                continue;
            }

            var rel = link.Substring(relStart, relEnd - relStart);

            yield return (url, rel);
        }
    }
}
