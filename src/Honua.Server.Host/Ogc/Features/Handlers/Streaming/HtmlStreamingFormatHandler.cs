// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Query;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Ogc;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Ogc.Features.Handlers.Streaming;

/// <summary>
/// Format handler for streaming HTML output.
/// This handler writes HTML feature tables incrementally as features are retrieved
/// from the repository, enabling progressive rendering in browsers and memory-efficient
/// processing of large datasets.
/// </summary>
/// <remarks>
/// <para>
/// Streaming HTML provides an interactive table view of features with progressive rendering
/// capabilities. As features are retrieved from the database, they are immediately written
/// to the HTTP response, allowing browsers to render content before the complete response
/// is available.
/// </para>
/// <para>
/// <strong>Memory Efficiency Benefits:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Constant memory usage regardless of result set size</description></item>
/// <item><description>No need to buffer thousands of features in memory</description></item>
/// <item><description>Progressive rendering - users see content sooner</description></item>
/// <item><description>Periodic flushing ensures smooth streaming experience</description></item>
/// </list>
/// <para>
/// <strong>Streaming Process:</strong>
/// </para>
/// <list type="number">
/// <item><description>Write HTML header with title, styles, and metadata</description></item>
/// <item><description>Stream feature details sections one-by-one as retrieved</description></item>
/// <item><description>Flush output every 10 features for progressive rendering</description></item>
/// <item><description>Write HTML footer with feature counts and closing tags</description></item>
/// </list>
/// <para>
/// <strong>Limitations:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Not supported when attachments are exposed (requires buffering for N+1 fix)</description></item>
/// <item><description>Not supported for resultType=hits (requires count only, no features)</description></item>
/// </list>
/// </remarks>
public sealed class HtmlStreamingFormatHandler : HtmlFormatHandlerBase
{
    /// <inheritdoc/>
    /// <remarks>
    /// Returns <see cref="OgcSharedHandlers.OgcResponseFormat.Html"/>.
    /// </remarks>
    public override OgcSharedHandlers.OgcResponseFormat Format => OgcSharedHandlers.OgcResponseFormat.Html;

    /// <inheritdoc/>
    /// <remarks>
    /// Handles streaming HTML output by writing feature details incrementally to the
    /// HTTP response stream. This implementation extracts the streaming logic from
    /// OgcFeaturesHandlers.Items.cs lines 326-345.
    /// <para>
    /// The method:
    /// </para>
    /// <list type="number">
    /// <item><description>Builds OGC API links for navigation and pagination</description></item>
    /// <item><description>Queries features from the repository</description></item>
    /// <item><description>Returns a StreamingHtmlFeatureCollectionResult that writes incrementally</description></item>
    /// </list>
    /// </remarks>
    /// <param name="request">The format request containing layer metadata, query parameters, and dependencies.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>
    /// An HTTP result that streams the HTML table with:
    /// <list type="bullet">
    /// <item><description>MIME type: text/html; charset=utf-8</description></item>
    /// <item><description>Content-CRS header with the effective CRS</description></item>
    /// <item><description>Cache headers for feature resources</description></item>
    /// <item><description>Features written incrementally with periodic flushing</description></item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the request does not contain a feature stream required for streaming.
    /// </exception>
    public override async Task<IResult> HandleAsync(
        FormatRequest request,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);

        // Ensure we have features to stream
        if (request.Features is null)
        {
            throw new InvalidOperationException(
                "HTML streaming requires a feature stream. The FormatRequest.Features property must not be null.");
        }

        // Lines 328-329 from OgcFeaturesHandlers.Items.cs:
        // Build OGC API links for navigation and pagination
        var links = FormatHandlerHelpers.BuildItemsLinks(request, request.Query, request.NumberMatched);

        // Lines 331-340 from OgcFeaturesHandlers.Items.cs:
        // Create streaming result that writes the HTML table incrementally
        IResult streamingResult = new StreamingHtmlFeatureCollectionResult(
            request.Features,
            request.Service,
            request.Layer,
            request.Query,
            request.CollectionId,
            request.NumberMatched,
            links,
            request.ContentCrs,
            request.Dependencies.ApiMetrics);

        // Lines 342-344 from OgcFeaturesHandlers.Items.cs:
        // Add Content-CRS and cache headers
        streamingResult = OgcSharedHandlers.WithContentCrsHeader(streamingResult, request.ContentCrs);
        streamingResult = streamingResult.WithFeatureCacheHeaders(request.Dependencies.CacheHeaderService);

        return streamingResult;
    }

    /// <summary>
    /// Streaming result implementation for HTML feature collections.
    /// Writes an HTML table incrementally to the HTTP response stream.
    /// </summary>
    /// <remarks>
    /// This class is extracted from OgcFeaturesHandlers.Items.cs lines 1063-1274.
    /// It implements the IResult interface to integrate with ASP.NET Core's result execution pipeline.
    /// The HTML output uses collapsible details elements for each feature, with properties
    /// displayed in tables and geometry shown as formatted JSON.
    /// </remarks>
    private sealed class StreamingHtmlFeatureCollectionResult : IResult
    {
        private readonly IAsyncEnumerable<FeatureRecord> _features;
        private readonly ServiceDefinition _service;
        private readonly LayerDefinition _layer;
        private readonly FeatureQuery _query;
        private readonly string _collectionId;
        private readonly long? _numberMatched;
        private readonly IReadOnlyList<OgcLink> _links;
        private readonly string? _contentCrs;
        private readonly IApiMetrics _apiMetrics;

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamingHtmlFeatureCollectionResult"/> class.
        /// </summary>
        /// <param name="features">Async enumerable stream of features to write.</param>
        /// <param name="service">Service definition for metrics tracking.</param>
        /// <param name="layer">Layer definition containing metadata and schema.</param>
        /// <param name="query">Feature query with filters and pagination parameters.</param>
        /// <param name="collectionId">OGC API collection identifier.</param>
        /// <param name="numberMatched">Total number of features matching the query, if known.</param>
        /// <param name="links">OGC API links for navigation and pagination.</param>
        /// <param name="contentCrs">Coordinate reference system for response content.</param>
        /// <param name="apiMetrics">Metrics service for tracking features returned.</param>
        public StreamingHtmlFeatureCollectionResult(
            IAsyncEnumerable<FeatureRecord> features,
            ServiceDefinition service,
            LayerDefinition layer,
            FeatureQuery query,
            string collectionId,
            long? numberMatched,
            IReadOnlyList<OgcLink> links,
            string? contentCrs,
            IApiMetrics apiMetrics)
        {
            _features = features;
            _service = service;
            _layer = layer;
            _query = query;
            _collectionId = collectionId;
            _numberMatched = numberMatched;
            _links = links;
            _contentCrs = contentCrs;
            _apiMetrics = apiMetrics;
        }

        /// <summary>
        /// Executes the result by writing the HTML table to the HTTP response stream.
        /// </summary>
        /// <param name="httpContext">The HTTP context for the current request.</param>
        /// <returns>A task that completes when the response has been fully written.</returns>
        /// <remarks>
        /// This method writes the HTML in three phases:
        /// <list type="number">
        /// <item><description>Header: Document structure, title, styles, metadata, links</description></item>
        /// <item><description>Features: One details element per feature with properties and geometry</description></item>
        /// <item><description>Footer: Feature counts and closing tags</description></item>
        /// </list>
        /// The stream is flushed every 10 features to enable progressive rendering.
        /// </remarks>
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.ContentType = OgcSharedHandlers.HtmlContentType;
            var cancellationToken = httpContext.RequestAborted;

            await using var writer = new StreamWriter(httpContext.Response.Body, Encoding.UTF8, bufferSize: 4096, leaveOpen: true);

            // Write the HTML header
            await WriteHeaderAsync(writer).ConfigureAwait(false);

            var returned = 0L;
            var hasFeatures = false;

            // Stream features one-by-one
            await foreach (var record in _features.WithCancellation(cancellationToken))
            {
                var components = FeatureComponentBuilder.BuildComponents(_layer, record, _query);
                await WriteFeatureAsync(writer, components).ConfigureAwait(false);
                hasFeatures = true;
                returned++;

                // Flush every 10 features for progressive rendering
                if (returned % 10 == 0)
                {
                    await writer.FlushAsync().ConfigureAwait(false);
                }
            }

            // Write the HTML footer
            await WriteFooterAsync(writer, hasFeatures, returned).ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);

            // Record metrics for features returned
            if (returned > 0)
            {
                _apiMetrics.RecordFeaturesReturned("ogc-api-features", _service.Id, _layer.Id, returned);
            }
        }

        /// <summary>
        /// Writes the HTML header including title, styles, metadata, and links.
        /// </summary>
        /// <param name="writer">The text writer for the HTTP response stream.</param>
        /// <returns>A task that completes when the header has been written.</returns>
        private Task WriteHeaderAsync(TextWriter writer)
        {
            var title = _layer.Title ?? _collectionId;
            var builder = new StringBuilder();

            builder.AppendLine("<!DOCTYPE html>")
                .AppendLine("<html lang=\"en\"><head>")
                .AppendLine("<meta charset=\"utf-8\"/>")
                .Append("<title>").Append(HtmlEncode(title)).AppendLine("</title>")
                .AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;margin:1.5rem;}table{border-collapse:collapse;margin-bottom:1rem;}th,td{border:1px solid #ccc;padding:0.35rem 0.6rem;text-align:left;}details{margin-bottom:1rem;}summary{font-weight:600;cursor:pointer;}</style>")
                .AppendLine("</head><body>");

            builder.Append("<h1>").Append(HtmlEncode(title)).AppendLine("</h1>");

            if (_layer.Description.HasValue())
            {
                builder.Append("<p>").Append(HtmlEncode(_layer.Description)).AppendLine("</p>");
            }

            if (_contentCrs.HasValue())
            {
                builder.Append("<p><strong>Content CRS:</strong> ")
                    .Append(HtmlEncode(_contentCrs))
                    .AppendLine("</p>");
            }

            AppendLinksHtml(builder, _links);

            builder.AppendLine("<section id=\"features\">");

            return writer.WriteAsync(builder.ToString());
        }

        /// <summary>
        /// Writes a single feature as an HTML details element with properties table and geometry.
        /// </summary>
        /// <param name="writer">The text writer for the HTTP response stream.</param>
        /// <param name="components">The feature components to render.</param>
        /// <returns>A task that completes when the feature has been written.</returns>
        private static Task WriteFeatureAsync(TextWriter writer, FeatureComponents components)
        {
            var builder = new StringBuilder();
            var displayName = components.DisplayName ?? components.FeatureId ?? "Feature";

            builder.Append("<details open><summary>")
                .Append(HtmlEncode(displayName))
                .AppendLine("</summary>");

            if (components.FeatureId.HasValue())
            {
                builder.Append("<p><strong>Feature ID:</strong> ")
                    .Append(HtmlEncode(components.FeatureId))
                    .AppendLine("</p>");
            }

            AppendFeaturePropertiesTable(builder, components.Properties);
            AppendGeometrySection(builder, components.Geometry);

            builder.AppendLine("</details>");

            return writer.WriteAsync(builder.ToString());
        }

        /// <summary>
        /// Writes the HTML footer including feature counts and closing tags.
        /// </summary>
        /// <param name="writer">The text writer for the HTTP response stream.</param>
        /// <param name="hasFeatures">Whether any features were written.</param>
        /// <param name="returned">The number of features written.</param>
        /// <returns>A task that completes when the footer has been written.</returns>
        private Task WriteFooterAsync(TextWriter writer, bool hasFeatures, long returned)
        {
            var builder = new StringBuilder();

            if (!hasFeatures)
            {
                builder.AppendLine("<p>No features found.</p>");
            }

            var matchedDisplay = _numberMatched.HasValue
                ? _numberMatched.Value.ToString(CultureInfo.InvariantCulture)
                : "unknown";

            builder.Append("</section>")
                .Append("<p><strong>Number matched:</strong> ")
                .Append(HtmlEncode(matchedDisplay))
                .Append(" &nbsp; <strong>Number returned:</strong> ")
                .Append(HtmlEncode(returned.ToString(CultureInfo.InvariantCulture)))
                .AppendLine("</p>")
                .AppendLine("</body></html>");

            return writer.WriteAsync(builder.ToString());
        }

        /// <summary>
        /// Appends an HTML unordered list of OGC API links.
        /// </summary>
        /// <param name="builder">The string builder to append to.</param>
        /// <param name="links">The links to render.</param>
        private static void AppendLinksHtml(StringBuilder builder, IReadOnlyList<OgcLink> links)
        {
            if (links.Count == 0)
            {
                return;
            }

            builder.AppendLine("<h2>Links</h2><ul>");
            foreach (var link in links)
            {
                builder.Append("<li><a href=\"")
                    .Append(HtmlEncode(link.Href))
                    .Append("\">")
                    .Append(HtmlEncode(link.Title ?? link.Rel))
                    .Append("</a> <span>(")
                    .Append(HtmlEncode(link.Rel));

                if (link.Type.HasValue())
                {
                    builder.Append(", ").Append(HtmlEncode(link.Type));
                }

                builder.AppendLine(")</span></li>");
            }

            builder.AppendLine("</ul>");
        }

        /// <summary>
        /// Appends an HTML table of feature properties.
        /// </summary>
        /// <param name="builder">The string builder to append to.</param>
        /// <param name="properties">The feature properties to render.</param>
        private static void AppendFeaturePropertiesTable(StringBuilder builder, IReadOnlyDictionary<string, object?> properties)
        {
            builder.AppendLine("<h3>Properties</h3>");
            builder.AppendLine("<table><tbody>");

            foreach (var pair in properties.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            {
                builder.Append("<tr><th>")
                    .Append(HtmlEncode(pair.Key))
                    .Append("</th><td>")
                    .Append(HtmlEncode(OgcSharedHandlers.FormatPropertyValue(pair.Value)))
                    .AppendLine("</td></tr>");
            }

            builder.AppendLine("</tbody></table>");
        }

        /// <summary>
        /// Appends a collapsible details section with geometry JSON.
        /// </summary>
        /// <param name="builder">The string builder to append to.</param>
        /// <param name="geometry">The geometry object to render.</param>
        private static void AppendGeometrySection(StringBuilder builder, object? geometry)
        {
            var geometryText = OgcSharedHandlers.FormatGeometryValue(geometry);
            if (geometryText.IsNullOrWhiteSpace())
            {
                return;
            }

            builder.AppendLine("<details><summary>Geometry</summary>")
                .Append("<pre><code>")
                .Append(HtmlEncode(geometryText))
                .AppendLine("</code></pre>")
                .AppendLine("</details>");
        }

        /// <summary>
        /// HTML-encodes a string value for safe rendering in HTML content.
        /// </summary>
        /// <param name="value">The value to encode.</param>
        /// <returns>The HTML-encoded string.</returns>
        private static string HtmlEncode(string? value)
            => WebUtility.HtmlEncode(value ?? string.Empty);
    }
}
