// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Ogc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Ogc.Features.Handlers.Buffered;

/// <summary>
/// Format handler for Well-Known Text (WKT) format.
/// WKT is an OGC standard for representing vector geometries as human-readable text strings.
/// This handler buffers all features in memory before generating the WKT output.
/// </summary>
/// <remarks>
/// <para>
/// WKT format characteristics:
/// </para>
/// <list type="bullet">
/// <item><description>Output: Plain text with one geometry per line</description></item>
/// <item><description>MIME type: text/plain</description></item>
/// <item><description>Buffering: Yes (all features loaded into memory)</description></item>
/// <item><description>Memory efficient: No (requires buffering)</description></item>
/// <item><description>Supports: Multiple CRS, standard OGC geometry types</description></item>
/// <item><description>Use case: Debugging, human-readable geometry inspection</description></item>
/// </list>
/// <para>
/// <strong>Memory Limitations:</strong>
/// </para>
/// <para>
/// Features are buffered in memory before generating the response. For large datasets,
/// clients should use streaming export formats (GeoPackage, Shapefile, FlatGeobuf,
/// GeoArrow, CSV) which stream directly from the repository without buffering.
/// </para>
/// </remarks>
public sealed class WktFormatHandler : BufferedFormatHandlerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WktFormatHandler"/> class.
    /// </summary>
    public WktFormatHandler()
    {
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Returns <see cref="OgcSharedHandlers.OgcResponseFormat.Wkt"/>.
    /// </remarks>
    public override OgcSharedHandlers.OgcResponseFormat Format => OgcSharedHandlers.OgcResponseFormat.Wkt;

    /// <inheritdoc/>
    /// <remarks>
    /// Generates a Well-Known Text representation of the feature collection.
    /// The implementation:
    /// <list type="number">
    /// <item><description>Validates that buffered features are available</description></item>
    /// <item><description>Casts buffered features to FeatureRecord instances</description></item>
    /// <item><description>Delegates to <see cref="WktFeatureFormatter.WriteFeatureCollection"/> to generate WKT text</description></item>
    /// <item><description>Returns the WKT content with appropriate headers and caching</description></item>
    /// </list>
    /// This method extracts the exact logic from OgcFeaturesHandlers.Items.cs lines 581-601.
    /// </remarks>
    /// <param name="request">The format request containing layer metadata, query parameters, and buffered features.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>
    /// A content result containing the WKT text with:
    /// <list type="bullet">
    /// <item><description>MIME type: text/plain</description></item>
    /// <item><description>Content-Crs header with the coordinate reference system</description></item>
    /// <item><description>Cache headers for feature resources</description></item>
    /// <item><description>ETag for cache validation</description></item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the request does not contain buffered features, or when WKT conversion fails.
    /// </exception>
    public override Task<IResult> HandleAsync(
        FormatRequest request,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);

        // Ensure we have buffered features to convert
        if (request.BufferedFeatures is null)
        {
            throw new InvalidOperationException(
                "WKT export requires buffered features. The FormatRequest.BufferedFeatures property must not be null.");
        }

        try
        {
            // Lines 585-595 from OgcFeaturesHandlers.Items.cs:
            // Cast buffered features to FeatureRecord instances
            var wktFeatures = request.BufferedFeatures.Cast<FeatureRecord>().ToList();

            // Calculate number returned for the formatter
            var numberReturned = wktFeatures.Count;
            var numberMatched = request.NumberMatched ?? numberReturned;

            // Generate WKT text using the formatter
            var payload = WktFeatureFormatter.WriteFeatureCollection(
                request.CollectionId,
                request.Layer,
                wktFeatures,
                numberMatched,
                numberReturned);

            // Create content result with WKT payload
            var wktResult = Results.Content(payload, OgcSharedHandlers.GetMimeType(this.Format));

            // Add Content-Crs header to indicate the coordinate reference system
            wktResult = OgcSharedHandlers.WithContentCrsHeader(wktResult, request.ContentCrs);

            // Generate ETag for cache validation
            var wktEtag = request.Dependencies.CacheHeaderService.GenerateETag(payload);

            // Return result with cache headers
            return Task.FromResult(
                wktResult.WithFeatureCacheHeaders(request.Dependencies.CacheHeaderService, wktEtag));
        }
        catch (InvalidOperationException)
        {
            // Lines 597-600 from OgcFeaturesHandlers.Items.cs:
            // Return error response if WKT conversion fails
            return Task.FromResult(
                Results.Problem(
                    "WKT conversion failed. Check server logs for details.",
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "WKT conversion failed"));
        }
    }
}
