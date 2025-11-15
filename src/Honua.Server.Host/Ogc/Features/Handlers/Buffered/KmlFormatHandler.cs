// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Styling;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Ogc.Features.Handlers.Buffered;

/// <summary>
/// Format handler for KML (Keyhole Markup Language) output format.
/// KML is an XML-based format for representing geographic features with styling,
/// developed by Google for use in Google Earth and now an OGC standard.
/// This handler buffers all features in memory before generating the KML document
/// with styles and extended metadata.
/// </summary>
/// <remarks>
/// KML format characteristics:
/// - Output: XML document (.kml)
/// - MIME type: application/vnd.google-earth.kml+xml
/// - Streaming: No (requires buffering for complete document generation)
/// - Memory efficient: No (all features buffered)
/// - Supports: Styles, placemarks, extended data, descriptions
/// - CRS: Only supports CRS84 (WGS84) coordinates
/// - Limitations: Requires coordinate transformation to WGS84
///
/// KML Specification:
/// KML follows the OGC KML 2.3 specification with support for:
/// - Document-level styles from layer style definitions
/// - Feature placemarks with names and descriptions
/// - Extended data for feature properties
/// - Geometry conversion from GeoJSON to KML geometry types
/// - Collection metadata in document extended data
/// </remarks>
public sealed class KmlFormatHandler : Crs84RequiredFormatHandlerBase
{
    /// <inheritdoc/>
    /// <remarks>
    /// Returns <see cref="OgcResponseFormat.Kml"/>.
    /// </remarks>
    public override OgcResponseFormat Format => OgcResponseFormat.Kml;

    /// <inheritdoc/>
    /// <remarks>
    /// Returns "KML" for use in validation error messages.
    /// </remarks>
    protected override string GetFormatDisplayName() => "KML";

    /// <inheritdoc/>
    /// <remarks>
    /// Generates a KML document from buffered features with style information.
    /// The implementation follows these steps:
    /// <list type="number">
    /// <item><description>Resolves the layer's default style for KML styling</description></item>
    /// <item><description>Converts buffered feature records to KML feature content</description></item>
    /// <item><description>Generates the complete KML document with styles and metadata</description></item>
    /// <item><description>Returns the KML as a downloadable XML file with cache headers</description></item>
    /// </list>
    ///
    /// The handler expects <see cref="FormatRequest.BufferedFeatures"/> to contain
    /// <see cref="FeatureRecord"/> objects which are converted to KML placemarks.
    ///
    /// Style Resolution:
    /// The handler resolves styles in the following priority order:
    /// <list type="number">
    /// <item><description>Layer's default style ID if specified</description></item>
    /// <item><description>First style ID in the layer's style list</description></item>
    /// <item><description>Fallback to "default" style if no styles configured</description></item>
    /// </list>
    /// </remarks>
    /// <param name="request">The format request containing layer metadata, query parameters, and buffered features.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>
    /// A file result containing the KML document with:
    /// <list type="bullet">
    /// <item><description>MIME type: application/vnd.google-earth.kml+xml</description></item>
    /// <item><description>File name: {collection-id}.kml</description></item>
    /// <item><description>Cache headers for feature resources</description></item>
    /// <item><description>ETag computed from file content</description></item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when buffered features are null or when KML conversion fails.
    /// </exception>
    public override async Task<IResult> HandleAsync(
        FormatRequest request,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);

        // Ensure we have buffered features
        if (request.BufferedFeatures is null)
        {
            throw new InvalidOperationException(
                "KML format requires buffered features. The FormatRequest.BufferedFeatures property must not be null.");
        }

        // Resolve the layer's style for KML styling
        // Priority: DefaultStyleId > first StyleId > "default"
        var preferredStyleId = request.Layer.DefaultStyleId.HasValue()
            ? request.Layer.DefaultStyleId
            : request.Layer.StyleIds.Count > 0
                ? request.Layer.StyleIds[0]
                : "default";

        var kmlStyle = await OgcSharedHandlers.ResolveStyleDefinitionAsync(
            preferredStyleId!,
            request.Layer,
            request.Dependencies.MetadataRegistry,
            cancellationToken).ConfigureAwait(false);

        // Convert buffered feature records to KML feature content
        // This creates placemarks with geometry, names, and extended data
        var kmlFeatures = new List<KmlFeatureContent>();
        var query = EnforceCrs84(request.Query);

        foreach (var bufferedFeature in request.BufferedFeatures)
        {
            if (bufferedFeature is FeatureRecord record)
            {
                var kmlContent = FeatureComponentBuilder.CreateKmlContent(
                    request.Layer,
                    record,
                    query);
                kmlFeatures.Add(kmlContent);
            }
        }

        // Record metrics for features returned
        var numberReturned = kmlFeatures.Count;
        FormatHandlerHelpers.RecordFeaturesReturned(request, numberReturned);

        try
        {
            // Generate the complete KML document with styles and features
            var matchedValue = request.NumberMatched ?? numberReturned;
            var payload = KmlFeatureFormatter.WriteFeatureCollection(
                request.CollectionId,
                request.Layer,
                kmlFeatures,
                matchedValue,
                numberReturned,
                kmlStyle);

            // Convert to bytes and prepare file response
            var bytes = Encoding.UTF8.GetBytes(payload);
            var fileName = FileNameHelper.BuildDownloadFileName(request.CollectionId, null, "kml");
            var mimeType = OgcSharedHandlers.GetMimeType(Format);

            // Generate ETag for cache validation
            var etag = request.Dependencies.CacheHeaderService.GenerateETag(bytes);

            // Return the KML file with cache headers
            return Results.File(bytes, mimeType, fileName)
                .WithFeatureCacheHeaders(request.Dependencies.CacheHeaderService, etag);
        }
        catch (InvalidOperationException)
        {
            // KML conversion can fail if geometry types are unsupported or malformed
            return Results.Problem(
                "KML conversion failed. Check server logs for details.",
                statusCode: StatusCodes.Status500InternalServerError,
                title: "KML conversion failed");
        }
    }
}
