// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Ogc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Ogc.Features.Handlers.Buffered;

/// <summary>
/// Format handler for TopoJSON format.
/// TopoJSON is an extension of GeoJSON that encodes topology and eliminates redundant coordinate data,
/// significantly reducing file size for datasets with shared boundaries.
/// This handler buffers all features in memory and requires WGS84 (CRS84) coordinates.
/// </summary>
/// <remarks>
/// <para>
/// TopoJSON format characteristics:
/// </para>
/// <list type="bullet">
/// <item><description>Output: JSON with topology encoding</description></item>
/// <item><description>MIME type: application/geo+json (topojson variant)</description></item>
/// <item><description>Buffering: Yes (all features loaded into memory)</description></item>
/// <item><description>Memory efficient: No (requires buffering)</description></item>
/// <item><description>Supports: Only CRS84 (WGS84) coordinates</description></item>
/// <item><description>CRS Requirement: MUST use WGS84 (CRS84)</description></item>
/// <item><description>Use case: Topology preservation, reduced file size for shared boundaries</description></item>
/// <item><description>Advantage: Eliminates redundant coordinates for adjacent features</description></item>
/// </list>
/// <para>
/// <strong>CRS Restriction:</strong>
/// </para>
/// <para>
/// TopoJSON only supports WGS84 (CRS84) coordinates. Requests for other coordinate
/// reference systems will be rejected during validation.
/// </para>
/// <para>
/// <strong>Memory Limitations:</strong>
/// </para>
/// <para>
/// Features are buffered in memory before generating the response. For large datasets,
/// clients should use streaming export formats (GeoPackage, Shapefile, FlatGeobuf,
/// GeoArrow, CSV) which stream directly from the repository without buffering.
/// </para>
/// </remarks>
public sealed class TopoJsonFormatHandler : Crs84RequiredFormatHandlerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TopoJsonFormatHandler"/> class.
    /// </summary>
    public TopoJsonFormatHandler()
    {
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Returns <see cref="OgcResponseFormat.TopoJson"/>.
    /// </remarks>
    public override OgcResponseFormat Format => OgcResponseFormat.TopoJson;

    /// <inheritdoc/>
    /// <remarks>
    /// Returns "TopoJSON" for use in validation error messages.
    /// </remarks>
    protected override string GetFormatDisplayName() => "TopoJSON";

    /// <inheritdoc/>
    /// <remarks>
    /// Generates a TopoJSON representation of the feature collection with topology encoding.
    /// The implementation:
    /// <list type="number">
    /// <item><description>Validates that buffered features are available</description></item>
    /// <item><description>Casts buffered features to TopoJsonFeatureContent instances</description></item>
    /// <item><description>Delegates to <see cref="TopoJsonFeatureFormatter.WriteFeatureCollection"/> to generate TopoJSON</description></item>
    /// <item><description>Returns the TopoJSON content with appropriate headers and caching</description></item>
    /// </list>
    /// This method extracts the exact logic from OgcFeaturesHandlers.Items.cs lines 559-579.
    /// </remarks>
    /// <param name="request">The format request containing layer metadata, query parameters, and buffered features.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>
    /// A content result containing the TopoJSON with:
    /// <list type="bullet">
    /// <item><description>MIME type: application/geo+json (topojson variant)</description></item>
    /// <item><description>Content-Crs header set to CRS84 (WGS84)</description></item>
    /// <item><description>Cache headers for feature resources</description></item>
    /// <item><description>ETag for cache validation</description></item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the request does not contain buffered features, or when TopoJSON conversion fails.
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
                "TopoJSON export requires buffered features. The FormatRequest.BufferedFeatures property must not be null.");
        }

        try
        {
            // Lines 563-573 from OgcFeaturesHandlers.Items.cs:
            // Cast buffered features to TopoJsonFeatureContent instances
            var topoFeatures = request.BufferedFeatures.Cast<TopoJsonFeatureContent>().ToList();

            // Calculate number returned for the formatter
            var numberReturned = topoFeatures.Count;
            var numberMatched = request.NumberMatched ?? numberReturned;

            // Generate TopoJSON using the formatter
            var payload = TopoJsonFeatureFormatter.WriteFeatureCollection(
                request.CollectionId,
                request.Layer,
                topoFeatures,
                numberMatched,
                numberReturned);

            // Create content result with TopoJSON payload
            var topoResult = Results.Content(payload, OgcSharedHandlers.GetMimeType(this.Format));

            // Add Content-Crs header to indicate CRS84 (WGS84)
            topoResult = OgcSharedHandlers.WithContentCrsHeader(topoResult, request.ContentCrs);

            // Generate ETag for cache validation
            var topoEtag = request.Dependencies.CacheHeaderService.GenerateETag(payload);

            // Return result with cache headers
            return Task.FromResult(
                topoResult.WithFeatureCacheHeaders(request.Dependencies.CacheHeaderService, topoEtag));
        }
        catch (InvalidOperationException)
        {
            // Lines 575-578 from OgcFeaturesHandlers.Items.cs:
            // Return error response if TopoJSON conversion fails
            return Task.FromResult(
                Results.Problem(
                    "TopoJSON conversion failed. Check server logs for details.",
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "TopoJSON conversion failed"));
        }
    }
}
