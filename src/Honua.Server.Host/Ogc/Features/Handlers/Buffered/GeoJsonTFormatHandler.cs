// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Ogc.Features.Handlers.Buffered;

/// <summary>
/// Format handler for GeoJSON-T (GeoJSON with Temporal properties) output format.
/// GeoJSON-T extends standard GeoJSON with temporal coordinate support and explicit
/// temporal properties ("when" property) for representing time-varying geospatial features.
/// This handler buffers all features in memory before adding temporal annotations
/// and generating the complete GeoJSON-T document.
/// </summary>
/// <remarks>
/// GeoJSON-T format characteristics:
/// - Output: GeoJSON with temporal extensions
/// - MIME type: application/geo+json (with temporal properties)
/// - Streaming: No (requires buffering for collection-level temporal metadata)
/// - Memory efficient: No (all features buffered)
/// - Supports: Standard GeoJSON features with temporal annotations
/// - CRS: Supports multiple CRS (inherits from layer configuration)
/// - Temporal: Adds "when" property with start/end time ranges
/// - Limitations: Buffered in memory, not suitable for very large datasets
///
/// GeoJSON-T Specification:
/// GeoJSON-T follows the GeoJSON-T draft specification with temporal extensions:
/// - "when" property: Feature-level temporal information (instant or interval)
/// - Temporal coordinates: Optional 4th dimension in coordinate arrays (M values)
/// - Start/end time fields: Extracted from layer temporal configuration
/// - Backward compatible: Valid GeoJSON readers can parse GeoJSON-T
///
/// Temporal Property Extraction:
/// The handler uses the layer's temporal configuration to extract time information:
/// - StartField: Field containing the start time of a temporal interval
/// - EndField: Field containing the end time of a temporal interval
/// - If both are present, creates an interval representation
/// - If only start is present, represents an instant in time
/// - Temporal fields are read from <see cref="LayerDefinition.Temporal"/>
///
/// Use Cases:
/// GeoJSON-T is particularly useful for:
/// - Time-series geospatial data (moving objects, historical events)
/// - Temporal analysis and visualization in mapping applications
/// - Change detection and temporal queries
/// - Integration with temporal GIS systems
/// - Animated map visualizations showing change over time
/// </remarks>
public sealed class GeoJsonTFormatHandler : BufferedFormatHandlerBase
{
    /// <inheritdoc/>
    /// <remarks>
    /// Returns <see cref="OgcSharedHandlers.OgcResponseFormat.GeoJsonT"/>.
    /// </remarks>
    public override OgcSharedHandlers.OgcResponseFormat Format => OgcSharedHandlers.OgcResponseFormat.GeoJsonT;

    /// <inheritdoc/>
    /// <remarks>
    /// Generates a GeoJSON-T document from buffered features with temporal annotations.
    /// The implementation follows these steps:
    /// <list type="number">
    /// <item><description>Extracts temporal field names from layer configuration</description></item>
    /// <item><description>Converts buffered features to GeoJSON-T with "when" properties</description></item>
    /// <item><description>Builds OGC API pagination links for the collection</description></item>
    /// <item><description>Serializes the complete GeoJSON-T document</description></item>
    /// <item><description>Returns GeoJSON-T with Content-Crs header and cache headers</description></item>
    /// </list>
    ///
    /// The handler expects <see cref="FormatRequest.BufferedFeatures"/> to contain
    /// feature objects (typically GeoJSON features) which will be enhanced with
    /// temporal properties based on the layer's temporal configuration.
    ///
    /// Temporal Field Configuration:
    /// The handler reads temporal field names from the layer definition:
    /// <list type="bullet">
    /// <item><description><see cref="LayerDefinition.Temporal.StartField"/>: Field containing start time</description></item>
    /// <item><description><see cref="LayerDefinition.Temporal.EndField"/>: Field containing end time</description></item>
    /// </list>
    /// If the layer has no temporal configuration, features are still converted
    /// to GeoJSON-T format but without "when" properties.
    ///
    /// "When" Property Structure:
    /// The "when" property structure depends on available temporal fields:
    /// <list type="bullet">
    /// <item><description>Both start and end: { "start": "ISO8601", "end": "ISO8601" }</description></item>
    /// <item><description>Start only: { "instant": "ISO8601" }</description></item>
    /// <item><description>No temporal fields: "when" property is omitted</description></item>
    /// </list>
    /// </remarks>
    /// <param name="request">The format request containing layer metadata, query parameters, and buffered features.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>
    /// A content result containing the GeoJSON-T document with:
    /// <list type="bullet">
    /// <item><description>Content-Type header with the specified MIME type</description></item>
    /// <item><description>Content-Crs header indicating the coordinate reference system</description></item>
    /// <item><description>Cache headers for feature resources</description></item>
    /// <item><description>ETag computed from serialized content</description></item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when buffered features are null or when GeoJSON-T serialization fails.
    /// </exception>
    public override Task<IResult> HandleAsync(
        FormatRequest request,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);

        // Ensure we have buffered features
        if (request.BufferedFeatures is null)
        {
            throw new InvalidOperationException(
                "GeoJSON-T format requires buffered features. The FormatRequest.BufferedFeatures property must not be null.");
        }

        // Build OGC API links for pagination and navigation
        var numberReturned = request.BufferedFeatures.Count;
        var links = FormatHandlerHelpers.BuildItemsLinks(
            request,
            request.Query,
            request.NumberMatched);

        // Extract temporal field names from layer configuration
        // These fields identify which properties contain temporal information
        var startTimeField = request.Layer.Temporal?.StartField;
        var endTimeField = request.Layer.Temporal?.EndField;

        // Convert features to GeoJSON-T with temporal properties
        // Adds "when" property to features based on temporal field configuration
        // The third parameter (timeField) is set to null as we use start/end fields
        var geoJsonT = GeoJsonTFeatureFormatter.ToGeoJsonTFeatureCollection(
            request.BufferedFeatures,
            request.NumberMatched ?? numberReturned,
            numberReturned,
            startTimeField,
            endTimeField,
            null, // timeField - not used when start/end fields are specified
            links);

        // Serialize the GeoJSON-T document to string
        var serialized = GeoJsonTFeatureFormatter.Serialize(geoJsonT);

        // Record metrics for features returned
        FormatHandlerHelpers.RecordFeaturesReturned(request, numberReturned);

        // Create content result with appropriate MIME type
        var geoJsonTResult = Results.Content(serialized, request.ContentType);

        // Add Content-Crs header to indicate coordinate reference system
        geoJsonTResult = OgcSharedHandlers.WithContentCrsHeader(geoJsonTResult, request.ContentCrs);

        // Generate ETag for cache validation
        var etag = request.Dependencies.CacheHeaderService.GenerateETag(serialized);

        // Return GeoJSON-T with cache headers
        return Task.FromResult(
            geoJsonTResult.WithFeatureCacheHeaders(request.Dependencies.CacheHeaderService, etag));
    }
}
