// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Ogc;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Ogc.Features.Handlers.Buffered;

/// <summary>
/// Format handler for Well-Known Binary (WKB) format.
/// WKB is an OGC standard for representing vector geometries as compact binary data.
/// This handler buffers all features in memory before generating the binary WKB output.
/// </summary>
/// <remarks>
/// <para>
/// WKB format characteristics:
/// </para>
/// <list type="bullet">
/// <item><description>Output: Binary file with geometry data</description></item>
/// <item><description>MIME type: application/wkb</description></item>
/// <item><description>Buffering: Yes (all features loaded into memory)</description></item>
/// <item><description>Memory efficient: No (requires buffering)</description></item>
/// <item><description>Supports: Multiple CRS, standard OGC geometry types</description></item>
/// <item><description>Use case: Binary geometry transfer, compact representation</description></item>
/// <item><description>Format: [4-byte feature count][features...] with each feature as [4-byte length][WKB bytes]</description></item>
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
public sealed class WkbFormatHandler : BufferedFormatHandlerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WkbFormatHandler"/> class.
    /// </summary>
    public WkbFormatHandler()
    {
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Returns <see cref="OgcSharedHandlers.OgcResponseFormat.Wkb"/>.
    /// </remarks>
    public override OgcSharedHandlers.OgcResponseFormat Format => OgcSharedHandlers.OgcResponseFormat.Wkb;

    /// <inheritdoc/>
    /// <remarks>
    /// Generates a Well-Known Binary representation of the feature collection.
    /// The implementation:
    /// <list type="number">
    /// <item><description>Validates that buffered features are available</description></item>
    /// <item><description>Casts buffered features to FeatureRecord instances</description></item>
    /// <item><description>Delegates to <see cref="WkbFeatureFormatter.WriteFeatureCollection"/> to generate binary WKB data</description></item>
    /// <item><description>Returns the WKB binary as a downloadable file with appropriate headers and caching</description></item>
    /// </list>
    /// This method extracts the exact logic from OgcFeaturesHandlers.Items.cs lines 603-624.
    /// </remarks>
    /// <param name="request">The format request containing layer metadata, query parameters, and buffered features.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>
    /// A file result containing the WKB binary data with:
    /// <list type="bullet">
    /// <item><description>MIME type: application/wkb</description></item>
    /// <item><description>File name: {collection-id}.wkb</description></item>
    /// <item><description>Content-Crs header with the coordinate reference system</description></item>
    /// <item><description>Cache headers for feature resources</description></item>
    /// <item><description>ETag for cache validation</description></item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the request does not contain buffered features, or when WKB conversion fails.
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
                "WKB export requires buffered features. The FormatRequest.BufferedFeatures property must not be null.");
        }

        try
        {
            // Lines 607-618 from OgcFeaturesHandlers.Items.cs:
            // Cast buffered features to FeatureRecord instances
            var wkbFeatures = request.BufferedFeatures.Cast<FeatureRecord>().ToList();

            // Calculate number returned for the formatter
            var numberReturned = wkbFeatures.Count;
            var numberMatched = request.NumberMatched ?? numberReturned;

            // Generate WKB binary using the formatter
            var payload = WkbFeatureFormatter.WriteFeatureCollection(
                request.CollectionId,
                request.Layer,
                wkbFeatures,
                numberMatched,
                numberReturned);

            // Build the download file name
            var fileName = FileNameHelper.BuildDownloadFileName(request.CollectionId, null, "wkb");

            // Generate ETag for cache validation
            var wkbEtag = request.Dependencies.CacheHeaderService.GenerateETag(payload);

            // Create file result with WKB binary payload
            var wkbResult = Results.File(payload, OgcSharedHandlers.GetMimeType(this.Format), fileName);

            // Add Content-Crs header to indicate the coordinate reference system
            wkbResult = OgcSharedHandlers.WithContentCrsHeader(wkbResult, request.ContentCrs);

            // Return result with cache headers
            return Task.FromResult(
                wkbResult.WithFeatureCacheHeaders(request.Dependencies.CacheHeaderService, wkbEtag));
        }
        catch (InvalidOperationException)
        {
            // Lines 620-623 from OgcFeaturesHandlers.Items.cs:
            // Return error response if WKB conversion fails
            return Task.FromResult(
                Results.Problem(
                    "WKB conversion failed. Check server logs for details.",
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "WKB conversion failed"));
        }
    }
}
