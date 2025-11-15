// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Pagination;

/// <summary>
/// Builds RFC 5988 compliant Link headers for pagination in HTTP responses.
/// </summary>
/// <remarks>
/// <para>
/// The Link header field provides a means for serializing one or more links in HTTP headers.
/// It is semantically equivalent to the HTML &lt;link&gt; element, providing a way to indicate
/// relationships between the current resource and other resources.
/// </para>
/// <para>
/// RFC 5988 defines the Link header format as: Link: &lt;uri-reference&gt;; param1=value1; param2=value2
/// Common relation types for pagination include: first, last, prev, next, and self.
/// </para>
/// <para>
/// Reference: https://datatracker.ietf.org/doc/html/rfc5988
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Building a Link header with multiple relations
/// var builder = new LinkHeaderBuilder();
/// builder.AddSelf("https://api.honua.io/v1/maps?page=2&amp;limit=20")
///        .AddNext("https://api.honua.io/v1/maps?page=3&amp;limit=20")
///        .AddPrevious("https://api.honua.io/v1/maps?page=1&amp;limit=20")
///        .AddFirst("https://api.honua.io/v1/maps?page=1&amp;limit=20")
///        .AddLast("https://api.honua.io/v1/maps?page=10&amp;limit=20");
///
/// string linkHeader = builder.Build();
/// // Result: &lt;https://api.honua.io/v1/maps?page=2&amp;limit=20&gt;; rel="self",
/// //         &lt;https://api.honua.io/v1/maps?page=3&amp;limit=20&gt;; rel="next",
/// //         &lt;https://api.honua.io/v1/maps?page=1&amp;limit=20&gt;; rel="prev",
/// //         &lt;https://api.honua.io/v1/maps?page=1&amp;limit=20&gt;; rel="first",
/// //         &lt;https://api.honua.io/v1/maps?page=10&amp;limit=20&gt;; rel="last"
/// </code>
/// </example>
/// <example>
/// <code>
/// // Using with HttpResponse extension method
/// Response.AddLinkHeader(links => links
///     .AddSelf("https://api.honua.io/v1/datasets?page=5")
///     .AddNext("https://api.honua.io/v1/datasets?page=6")
///     .AddPrevious("https://api.honua.io/v1/datasets?page=4"));
/// </code>
/// </example>
public class LinkHeaderBuilder
{
    private readonly List<LinkHeaderValue> _links = new();

    /// <summary>
    /// Adds a link with the specified URL and relation type.
    /// </summary>
    /// <param name="url">The URL of the linked resource. Should be a fully-qualified absolute URL.</param>
    /// <param name="rel">
    /// The relation type (rel) parameter. Common values include: "next", "prev", "first", "last", "self".
    /// Per RFC 5988, relation types are case-insensitive but conventionally lowercase.
    /// </param>
    /// <returns>The current LinkHeaderBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when url or rel is null.</exception>
    /// <exception cref="ArgumentException">Thrown when url or rel is empty or whitespace.</exception>
    /// <remarks>
    /// URLs are not validated or encoded by this method. Callers should ensure URLs are properly
    /// formatted and encoded before passing them to this method. Query parameters should already
    /// be URL-encoded in the URL string.
    /// </remarks>
    /// <example>
    /// <code>
    /// var builder = new LinkHeaderBuilder();
    /// builder.AddLink("https://api.honua.io/v1/items?page=2", "next");
    /// builder.AddLink("https://api.honua.io/v1/items?page=1", "prev");
    /// </code>
    /// </example>
    public LinkHeaderBuilder AddLink(string url, string rel)
    {
        if (url == null)
        {
            throw new ArgumentNullException(nameof(url));
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL cannot be empty or whitespace.", nameof(url));
        }

        if (rel == null)
        {
            throw new ArgumentNullException(nameof(rel));
        }

        if (string.IsNullOrWhiteSpace(rel))
        {
            throw new ArgumentException("Relation type cannot be empty or whitespace.", nameof(rel));
        }

        _links.Add(new LinkHeaderValue(url, rel));
        return this;
    }

    /// <summary>
    /// Adds a "next" relation link pointing to the next page of results.
    /// </summary>
    /// <param name="url">The URL of the next page. Should be a fully-qualified absolute URL.</param>
    /// <returns>The current LinkHeaderBuilder instance for method chaining.</returns>
    /// <remarks>
    /// The "next" relation indicates that the link's context is a part of a series, and that
    /// the next in the series is the link target.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.AddNext("https://api.honua.io/v1/maps?page=3&amp;limit=20");
    /// </code>
    /// </example>
    public LinkHeaderBuilder AddNext(string url) => AddLink(url, "next");

    /// <summary>
    /// Adds a "prev" relation link pointing to the previous page of results.
    /// </summary>
    /// <param name="url">The URL of the previous page. Should be a fully-qualified absolute URL.</param>
    /// <returns>The current LinkHeaderBuilder instance for method chaining.</returns>
    /// <remarks>
    /// The "prev" relation indicates that the link's context is a part of a series, and that
    /// the previous in the series is the link target. Note: RFC 5988 uses "prev" not "previous".
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.AddPrevious("https://api.honua.io/v1/maps?page=1&amp;limit=20");
    /// </code>
    /// </example>
    public LinkHeaderBuilder AddPrevious(string url) => AddLink(url, "prev");

    /// <summary>
    /// Adds a "first" relation link pointing to the first page of results.
    /// </summary>
    /// <param name="url">The URL of the first page. Should be a fully-qualified absolute URL.</param>
    /// <returns>The current LinkHeaderBuilder instance for method chaining.</returns>
    /// <remarks>
    /// The "first" relation indicates that the link's context is a part of a series, and that
    /// the first in the series is the link target.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.AddFirst("https://api.honua.io/v1/maps?page=1&amp;limit=20");
    /// </code>
    /// </example>
    public LinkHeaderBuilder AddFirst(string url) => AddLink(url, "first");

    /// <summary>
    /// Adds a "last" relation link pointing to the last page of results.
    /// </summary>
    /// <param name="url">The URL of the last page. Should be a fully-qualified absolute URL.</param>
    /// <returns>The current LinkHeaderBuilder instance for method chaining.</returns>
    /// <remarks>
    /// The "last" relation indicates that the link's context is a part of a series, and that
    /// the last in the series is the link target.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.AddLast("https://api.honua.io/v1/maps?page=10&amp;limit=20");
    /// </code>
    /// </example>
    public LinkHeaderBuilder AddLast(string url) => AddLink(url, "last");

    /// <summary>
    /// Adds a "self" relation link pointing to the current page/resource.
    /// </summary>
    /// <param name="url">The URL of the current resource. Should be a fully-qualified absolute URL.</param>
    /// <returns>The current LinkHeaderBuilder instance for method chaining.</returns>
    /// <remarks>
    /// The "self" relation indicates that the link's context is the link target itself.
    /// This is useful for providing a canonical URL for the current page.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.AddSelf("https://api.honua.io/v1/maps?page=2&amp;limit=20");
    /// </code>
    /// </example>
    public LinkHeaderBuilder AddSelf(string url) => AddLink(url, "self");

    /// <summary>
    /// Builds the RFC 5988 compliant Link header string from all added links.
    /// </summary>
    /// <returns>
    /// A formatted Link header string with all links joined by commas.
    /// Returns an empty string if no links have been added.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The format of each link follows RFC 5988: &lt;url&gt;; rel="relation"
    /// Multiple links are separated by commas and spaces for readability.
    /// </para>
    /// <para>
    /// Example output:
    /// &lt;https://api.honua.io/v1/maps?page=2&gt;; rel="self", &lt;https://api.honua.io/v1/maps?page=3&gt;; rel="next"
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var builder = new LinkHeaderBuilder();
    /// builder.AddSelf("https://api.honua.io/v1/maps?page=2")
    ///        .AddNext("https://api.honua.io/v1/maps?page=3");
    ///
    /// string header = builder.Build();
    /// // Result: &lt;https://api.honua.io/v1/maps?page=2&gt;; rel="self", &lt;https://api.honua.io/v1/maps?page=3&gt;; rel="next"
    /// </code>
    /// </example>
    public string Build()
    {
        return string.Join(", ", _links.Select(l => $"<{l.Url}>; rel=\"{l.Rel}\""));
    }

    /// <summary>
    /// Gets the number of links currently added to the builder.
    /// </summary>
    /// <returns>The count of links.</returns>
    public int Count => _links.Count;

    /// <summary>
    /// Clears all links from the builder.
    /// </summary>
    /// <returns>The current LinkHeaderBuilder instance for method chaining.</returns>
    /// <remarks>
    /// This method is useful when you want to reuse a LinkHeaderBuilder instance
    /// for building multiple different Link headers.
    /// </remarks>
    public LinkHeaderBuilder Clear()
    {
        _links.Clear();
        return this;
    }
}

/// <summary>
/// Represents a single link in a Link header with its URL and relation type.
/// </summary>
/// <param name="Url">The URL of the linked resource.</param>
/// <param name="Rel">The relation type describing the relationship to the current resource.</param>
/// <remarks>
/// This is an internal implementation detail of LinkHeaderBuilder.
/// Relation types are defined in RFC 5988 and the IANA Link Relations registry.
/// Common relation types for pagination: first, last, prev, next, self.
/// </remarks>
internal record LinkHeaderValue(string Url, string Rel);

/// <summary>
/// Provides extension methods for adding RFC 5988 Link headers to HTTP responses.
/// </summary>
/// <remarks>
/// These extension methods simplify the process of adding pagination Link headers
/// to ASP.NET Core HTTP responses, ensuring RFC 5988 compliance.
/// </remarks>
public static class LinkHeaderExtensions
{
    /// <summary>
    /// Adds a pre-built Link header string to the HTTP response.
    /// </summary>
    /// <param name="response">The HTTP response to add the header to.</param>
    /// <param name="linkHeader">The formatted Link header string.</param>
    /// <exception cref="ArgumentNullException">Thrown when response or linkHeader is null.</exception>
    /// <remarks>
    /// This method appends the Link header to the response headers. If a Link header
    /// already exists, this will add an additional Link header (HTTP allows multiple
    /// headers with the same name).
    /// </remarks>
    /// <example>
    /// <code>
    /// var linkHeader = "&lt;https://api.honua.io/v1/maps?page=2&gt;; rel=\"next\"";
    /// Response.AddLinkHeader(linkHeader);
    /// </code>
    /// </example>
    public static void AddLinkHeader(this HttpResponse response, string linkHeader)
    {
        if (response == null)
        {
            throw new ArgumentNullException(nameof(response));
        }

        if (linkHeader == null)
        {
            throw new ArgumentNullException(nameof(linkHeader));
        }

        response.Headers.Append("Link", linkHeader);
    }

    /// <summary>
    /// Adds a Link header to the HTTP response using a fluent builder configuration.
    /// </summary>
    /// <param name="response">The HTTP response to add the header to.</param>
    /// <param name="configure">
    /// An action that configures the LinkHeaderBuilder with the desired links.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when response or configure is null.</exception>
    /// <remarks>
    /// <para>
    /// This method provides a fluent API for building Link headers directly on the response.
    /// It creates a new LinkHeaderBuilder, passes it to the configuration action, builds
    /// the header string, and appends it to the response.
    /// </para>
    /// <para>
    /// This is the recommended approach for adding pagination links to API responses.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // In a controller action
    /// Response.AddLinkHeader(links => links
    ///     .AddSelf($"https://api.honua.io/v1/maps?page={currentPage}")
    ///     .AddNext($"https://api.honua.io/v1/maps?page={currentPage + 1}")
    ///     .AddPrevious($"https://api.honua.io/v1/maps?page={currentPage - 1}")
    ///     .AddFirst("https://api.honua.io/v1/maps?page=1")
    ///     .AddLast($"https://api.honua.io/v1/maps?page={totalPages}"));
    /// </code>
    /// </example>
    /// <example>
    /// <code>
    /// // With conditional links (only add prev if not on first page)
    /// Response.AddLinkHeader(links =>
    /// {
    ///     links.AddSelf($"https://api.honua.io/v1/datasets?page={page}");
    ///
    ///     if (hasNextPage)
    ///         links.AddNext($"https://api.honua.io/v1/datasets?page={page + 1}");
    ///
    ///     if (page > 1)
    ///         links.AddPrevious($"https://api.honua.io/v1/datasets?page={page - 1}");
    /// });
    /// </code>
    /// </example>
    public static void AddLinkHeader(this HttpResponse response, Action<LinkHeaderBuilder> configure)
    {
        if (response == null)
        {
            throw new ArgumentNullException(nameof(response));
        }

        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var builder = new LinkHeaderBuilder();
        configure(builder);
        response.AddLinkHeader(builder.Build());
    }
}

/*
 * UNIT TEST EXAMPLES
 * ==================
 *
 * These examples demonstrate how to test the LinkHeaderBuilder class.
 * Add these to your test project (e.g., Honua.Server.Host.Tests/Pagination/LinkHeaderBuilderTests.cs)
 *
 * using Xunit;
 * using Honua.Server.Host.Pagination;
 *
 * namespace Honua.Server.Host.Tests.Pagination;
 *
 * public class LinkHeaderBuilderTests
 * {
 *     [Fact]
 *     public void Build_SingleLink_ReturnsCorrectFormat()
 *     {
 *         // Arrange
 *         var builder = new LinkHeaderBuilder();
 *         builder.AddNext("https://api.honua.io/v1/maps?page=2");
 *
 *         // Act
 *         var result = builder.Build();
 *
 *         // Assert
 *         Assert.Equal("<https://api.honua.io/v1/maps?page=2>; rel=\"next\"", result);
 *     }
 *
 *     [Fact]
 *     public void Build_MultipleLinks_ReturnsCommaSeparatedFormat()
 *     {
 *         // Arrange
 *         var builder = new LinkHeaderBuilder();
 *         builder.AddSelf("https://api.honua.io/v1/maps?page=2")
 *                .AddNext("https://api.honua.io/v1/maps?page=3")
 *                .AddPrevious("https://api.honua.io/v1/maps?page=1");
 *
 *         // Act
 *         var result = builder.Build();
 *
 *         // Assert
 *         var expected = "<https://api.honua.io/v1/maps?page=2>; rel=\"self\", " +
 *                       "<https://api.honua.io/v1/maps?page=3>; rel=\"next\", " +
 *                       "<https://api.honua.io/v1/maps?page=1>; rel=\"prev\"";
 *         Assert.Equal(expected, result);
 *     }
 *
 *     [Fact]
 *     public void Build_UrlWithMultipleQueryParameters_PreservesParameters()
 *     {
 *         // Arrange
 *         var builder = new LinkHeaderBuilder();
 *         var url = "https://api.honua.io/v1/maps?page=2&limit=20&sort=name&order=asc";
 *         builder.AddNext(url);
 *
 *         // Act
 *         var result = builder.Build();
 *
 *         // Assert
 *         Assert.Equal($"<{url}>; rel=\"next\"", result);
 *     }
 *
 *     [Fact]
 *     public void Build_AllPaginationLinks_ReturnsCompleteHeader()
 *     {
 *         // Arrange
 *         var builder = new LinkHeaderBuilder();
 *         builder.AddSelf("https://api.honua.io/v1/maps?page=5")
 *                .AddNext("https://api.honua.io/v1/maps?page=6")
 *                .AddPrevious("https://api.honua.io/v1/maps?page=4")
 *                .AddFirst("https://api.honua.io/v1/maps?page=1")
 *                .AddLast("https://api.honua.io/v1/maps?page=10");
 *
 *         // Act
 *         var result = builder.Build();
 *
 *         // Assert
 *         Assert.Contains("rel=\"self\"", result);
 *         Assert.Contains("rel=\"next\"", result);
 *         Assert.Contains("rel=\"prev\"", result);
 *         Assert.Contains("rel=\"first\"", result);
 *         Assert.Contains("rel=\"last\"", result);
 *     }
 *
 *     [Fact]
 *     public void Build_NoLinks_ReturnsEmptyString()
 *     {
 *         // Arrange
 *         var builder = new LinkHeaderBuilder();
 *
 *         // Act
 *         var result = builder.Build();
 *
 *         // Assert
 *         Assert.Equal(string.Empty, result);
 *     }
 *
 *     [Fact]
 *     public void AddLink_NullUrl_ThrowsArgumentNullException()
 *     {
 *         // Arrange
 *         var builder = new LinkHeaderBuilder();
 *
 *         // Act & Assert
 *         Assert.Throws<ArgumentNullException>(() => builder.AddLink(null, "next"));
 *     }
 *
 *     [Fact]
 *     public void AddLink_EmptyUrl_ThrowsArgumentException()
 *     {
 *         // Arrange
 *         var builder = new LinkHeaderBuilder();
 *
 *         // Act & Assert
 *         Assert.Throws<ArgumentException>(() => builder.AddLink("", "next"));
 *     }
 *
 *     [Fact]
 *     public void AddLink_NullRel_ThrowsArgumentNullException()
 *     {
 *         // Arrange
 *         var builder = new LinkHeaderBuilder();
 *
 *         // Act & Assert
 *         Assert.Throws<ArgumentNullException>(() =>
 *             builder.AddLink("https://api.honua.io/v1/maps?page=2", null));
 *     }
 *
 *     [Fact]
 *     public void Clear_RemovesAllLinks()
 *     {
 *         // Arrange
 *         var builder = new LinkHeaderBuilder();
 *         builder.AddNext("https://api.honua.io/v1/maps?page=2")
 *                .AddPrevious("https://api.honua.io/v1/maps?page=1");
 *
 *         // Act
 *         builder.Clear();
 *         var result = builder.Build();
 *
 *         // Assert
 *         Assert.Equal(string.Empty, result);
 *         Assert.Equal(0, builder.Count);
 *     }
 *
 *     [Fact]
 *     public void Count_ReturnsCorrectNumberOfLinks()
 *     {
 *         // Arrange
 *         var builder = new LinkHeaderBuilder();
 *
 *         // Act & Assert
 *         Assert.Equal(0, builder.Count);
 *
 *         builder.AddNext("https://api.honua.io/v1/maps?page=2");
 *         Assert.Equal(1, builder.Count);
 *
 *         builder.AddPrevious("https://api.honua.io/v1/maps?page=1");
 *         Assert.Equal(2, builder.Count);
 *     }
 *
 *     [Fact]
 *     public void Build_UrlWithEncodedParameters_PreservesEncoding()
 *     {
 *         // Arrange
 *         var builder = new LinkHeaderBuilder();
 *         var url = "https://api.honua.io/v1/maps?filter=name%20eq%20'test'&page=2";
 *         builder.AddNext(url);
 *
 *         // Act
 *         var result = builder.Build();
 *
 *         // Assert
 *         Assert.Equal($"<{url}>; rel=\"next\"", result);
 *         Assert.Contains("%20", result);
 *     }
 * }
 *
 * public class LinkHeaderExtensionsTests
 * {
 *     [Fact]
 *     public void AddLinkHeader_WithString_AddsHeaderToResponse()
 *     {
 *         // Arrange
 *         var context = new DefaultHttpContext();
 *         var linkHeader = "<https://api.honua.io/v1/maps?page=2>; rel=\"next\"";
 *
 *         // Act
 *         context.Response.AddLinkHeader(linkHeader);
 *
 *         // Assert
 *         Assert.True(context.Response.Headers.ContainsKey("Link"));
 *         Assert.Equal(linkHeader, context.Response.Headers["Link"].ToString());
 *     }
 *
 *     [Fact]
 *     public void AddLinkHeader_WithBuilder_AddsHeaderToResponse()
 *     {
 *         // Arrange
 *         var context = new DefaultHttpContext();
 *
 *         // Act
 *         context.Response.AddLinkHeader(links => links
 *             .AddNext("https://api.honua.io/v1/maps?page=2")
 *             .AddPrevious("https://api.honua.io/v1/maps?page=1"));
 *
 *         // Assert
 *         Assert.True(context.Response.Headers.ContainsKey("Link"));
 *         var header = context.Response.Headers["Link"].ToString();
 *         Assert.Contains("rel=\"next\"", header);
 *         Assert.Contains("rel=\"prev\"", header);
 *     }
 *
 *     [Fact]
 *     public void AddLinkHeader_NullResponse_ThrowsArgumentNullException()
 *     {
 *         // Arrange
 *         HttpResponse response = null;
 *
 *         // Act & Assert
 *         Assert.Throws<ArgumentNullException>(() =>
 *             response.AddLinkHeader("<url>; rel=\"next\""));
 *     }
 *
 *     [Fact]
 *     public void AddLinkHeader_NullConfigure_ThrowsArgumentNullException()
 *     {
 *         // Arrange
 *         var context = new DefaultHttpContext();
 *
 *         // Act & Assert
 *         Assert.Throws<ArgumentNullException>(() =>
 *             context.Response.AddLinkHeader((Action<LinkHeaderBuilder>)null));
 *     }
 * }
 */
