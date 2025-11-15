// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Observability;
using Honua.Server.Host.Ogc;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Ogc.Features.Handlers.Streaming;

/// <summary>
/// Format handler for streaming GeoJSON output.
/// This handler writes GeoJSON FeatureCollections incrementally as features are retrieved
/// from the repository, enabling memory-efficient processing of arbitrarily large datasets.
/// </summary>
/// <remarks>
/// <para>
/// Streaming GeoJSON differs from buffered GeoJSON by writing features to the HTTP response
/// stream as they are retrieved from the database, rather than loading all features into
/// memory first. This approach provides significant memory efficiency benefits for large
/// result sets.
/// </para>
/// <para>
/// <strong>Memory Efficiency Benefits:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Constant memory usage regardless of result set size</description></item>
/// <item><description>No need to buffer thousands of features in memory</description></item>
/// <item><description>Faster time-to-first-byte for clients</description></item>
/// <item><description>Supports datasets larger than available RAM</description></item>
/// </list>
/// <para>
/// <strong>Streaming Process:</strong>
/// </para>
/// <list type="number">
/// <item><description>Write GeoJSON FeatureCollection opening (metadata, opening brace, features array start)</description></item>
/// <item><description>Stream features one-by-one as they are retrieved from repository</description></item>
/// <item><description>Write GeoJSON FeatureCollection closing (closing array, metadata, closing brace)</description></item>
/// </list>
/// <para>
/// <strong>Limitations:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Not supported when attachments are exposed (requires buffering for N+1 fix)</description></item>
/// <item><description>Not supported for resultType=hits (requires count only, no features)</description></item>
/// </list>
/// </remarks>
public sealed class GeoJsonStreamingFormatHandler : StreamingFormatHandlerBase
{
    /// <inheritdoc/>
    /// <remarks>
    /// Returns <see cref="OgcResponseFormat.GeoJson"/>.
    /// </remarks>
    public override OgcResponseFormat Format => OgcResponseFormat.GeoJson;

    /// <inheritdoc/>
    /// <remarks>
    /// Handles streaming GeoJSON output by writing the FeatureCollection incrementally
    /// to the HTTP response stream. This implementation extracts the streaming logic from
    /// OgcFeaturesHandlers.Items.cs lines 300-324.
    /// <para>
    /// The method:
    /// </para>
    /// <list type="number">
    /// <item><description>Builds OGC API links for navigation and pagination</description></item>
    /// <item><description>Retrieves style metadata for the layer</description></item>
    /// <item><description>Queries features with pagination applied via streaming window</description></item>
    /// <item><description>Returns a StreamingFeatureCollectionResult that writes incrementally</description></item>
    /// </list>
    /// </remarks>
    /// <param name="request">The format request containing layer metadata, query parameters, and dependencies.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>
    /// An HTTP result that streams the GeoJSON FeatureCollection with:
    /// <list type="bullet">
    /// <item><description>MIME type: application/geo+json</description></item>
    /// <item><description>Content-CRS header with the effective CRS</description></item>
    /// <item><description>Cache headers for feature resources</description></item>
    /// <item><description>Features written incrementally as they are retrieved</description></item>
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
                "GeoJSON streaming requires a feature stream. The FormatRequest.Features property must not be null.");
        }

        // Lines 302-303 from OgcFeaturesHandlers.Items.cs:
        // Build OGC API links for pagination and navigation
        var links = FormatHandlerHelpers.BuildItemsLinks(request, request.Query, request.NumberMatched);

        // Line 303 from OgcFeaturesHandlers.Items.cs:
        // Build ordered style IDs for the layer
        var styleIds = OgcSharedHandlers.BuildOrderedStyleIds(request.Layer);

        // Lines 308-319 from OgcFeaturesHandlers.Items.cs:
        // Create streaming result that writes the FeatureCollection incrementally
        IResult streamingResult = new StreamingFeatureCollectionResult(
            request.Features,
            request.Service,
            request.Layer,
            request.NumberMatched,
            links,
            request.Layer.DefaultStyleId,
            styleIds,
            request.Layer.MinScale,
            request.Layer.MaxScale,
            request.ContentType,
            request.Dependencies.ApiMetrics);

        // Lines 321-323 from OgcFeaturesHandlers.Items.cs:
        // Add Content-CRS and cache headers
        streamingResult = OgcSharedHandlers.WithContentCrsHeader(streamingResult, request.ContentCrs);
        streamingResult = streamingResult.WithFeatureCacheHeaders(request.Dependencies.CacheHeaderService);

        return streamingResult;
    }

    /// <summary>
    /// Streaming result implementation for GeoJSON feature collections.
    /// Writes the FeatureCollection incrementally to the HTTP response stream.
    /// </summary>
    /// <remarks>
    /// This class is extracted from OgcFeaturesHandlers.Items.cs lines 995-1058.
    /// It implements the IResult interface to integrate with ASP.NET Core's result execution pipeline.
    /// </remarks>
    private sealed class StreamingFeatureCollectionResult : IResult
    {
        private readonly IAsyncEnumerable<FeatureRecord> _features;
        private readonly ServiceDefinition _service;
        private readonly LayerDefinition _layer;
        private readonly long? _numberMatched;
        private readonly IReadOnlyList<OgcLink> _links;
        private readonly string? _defaultStyle;
        private readonly IReadOnlyList<string>? _styleIds;
        private readonly double? _minScale;
        private readonly double? _maxScale;
        private readonly string _contentType;
        private readonly IApiMetrics _apiMetrics;

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamingFeatureCollectionResult"/> class.
        /// </summary>
        /// <param name="features">Async enumerable stream of features to write.</param>
        /// <param name="service">Service definition for metrics tracking.</param>
        /// <param name="layer">Layer definition containing metadata and schema.</param>
        /// <param name="numberMatched">Total number of features matching the query, if known.</param>
        /// <param name="links">OGC API links for navigation and pagination.</param>
        /// <param name="defaultStyle">Default style ID for the layer, if any.</param>
        /// <param name="styleIds">Ordered list of available style IDs.</param>
        /// <param name="minScale">Minimum scale denominator for layer visibility.</param>
        /// <param name="maxScale">Maximum scale denominator for layer visibility.</param>
        /// <param name="contentType">MIME type for the response (application/geo+json).</param>
        /// <param name="apiMetrics">Metrics service for tracking features returned.</param>
        public StreamingFeatureCollectionResult(
            IAsyncEnumerable<FeatureRecord> features,
            ServiceDefinition service,
            LayerDefinition layer,
            long? numberMatched,
            IReadOnlyList<OgcLink> links,
            string? defaultStyle,
            IReadOnlyList<string>? styleIds,
            double? minScale,
            double? maxScale,
            string contentType,
            IApiMetrics apiMetrics)
        {
            _features = features;
            _service = service;
            _layer = layer;
            _numberMatched = numberMatched;
            _links = links;
            _defaultStyle = defaultStyle;
            _styleIds = styleIds;
            _minScale = minScale;
            _maxScale = maxScale;
            _contentType = contentType;
            _apiMetrics = apiMetrics;
        }

        /// <summary>
        /// Executes the result by writing the GeoJSON FeatureCollection to the HTTP response stream.
        /// </summary>
        /// <param name="httpContext">The HTTP context for the current request.</param>
        /// <returns>A task that completes when the response has been fully written.</returns>
        /// <remarks>
        /// This method delegates to <see cref="OgcFeatureCollectionWriter.WriteFeatureCollectionAsync"/>
        /// which handles the incremental writing of the FeatureCollection structure.
        /// After writing completes, it records metrics for the number of features returned.
        /// </remarks>
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.ContentType = _contentType;
            var cancellationToken = httpContext.RequestAborted;

            // Write the FeatureCollection incrementally to the response stream
            var count = await OgcFeatureCollectionWriter.WriteFeatureCollectionAsync(
                httpContext.Response.Body,
                _features,
                _layer,
                _numberMatched,
                null,
                _links,
                _defaultStyle,
                _styleIds,
                _minScale,
                _maxScale,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // Record metrics for features returned
            if (count > 0)
            {
                _apiMetrics.RecordFeaturesReturned("ogc-api-features", _service.Id, _layer.Id, count);
            }
        }
    }
}
