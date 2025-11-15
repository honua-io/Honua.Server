// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Ogc.Features.Handlers.Buffered;

/// <summary>
/// Format handler for JSON-LD (JSON for Linked Data) output format.
/// JSON-LD is a method of encoding linked data using JSON, adding semantic web
/// annotations to standard GeoJSON features through @context and @type properties.
/// This handler buffers all features in memory before adding semantic annotations
/// and generating the complete JSON-LD document.
/// </summary>
/// <remarks>
/// JSON-LD format characteristics:
/// - Output: JSON document with semantic annotations
/// - MIME type: application/ld+json (or application/geo+json;profile=ld)
/// - Streaming: No (requires buffering for collection-level context)
/// - Memory efficient: No (all features buffered)
/// - Supports: GeoJSON features with semantic web annotations
/// - CRS: Supports multiple CRS (inherits from layer configuration)
/// - Context: Includes @context with vocabulary mappings
/// - Limitations: Buffered in memory, not suitable for very large datasets
///
/// JSON-LD Specification:
/// JSON-LD follows the JSON-LD 1.1 specification with OGC Features extensions:
/// - @context defines vocabulary mappings (GeoSPARQL, Schema.org, Dublin Core)
/// - @type annotates entity types for semantic interoperability
/// - @id provides stable URIs for features and collections
/// - Preserves full GeoJSON compatibility while adding semantic layer
///
/// Semantic Vocabularies:
/// The handler includes context mappings for:
/// - GeoSPARQL: Spatial properties and relationships
/// - Schema.org: General-purpose vocabulary for structured data
/// - Dublin Core Terms: Metadata vocabulary for resources
/// - Layer-specific field mappings with XSD type annotations
///
/// Use Cases:
/// JSON-LD is particularly useful for:
/// - Semantic web applications consuming linked data
/// - Knowledge graphs integrating geospatial features
/// - Data catalogs requiring rich metadata annotations
/// - Interoperability with RDF and SPARQL-based systems
/// </remarks>
public sealed class JsonLdFormatHandler : BufferedFormatHandlerBase
{
    /// <inheritdoc/>
    /// <remarks>
    /// Returns <see cref="OgcResponseFormat.JsonLd"/>.
    /// </remarks>
    public override OgcResponseFormat Format => OgcResponseFormat.JsonLd;

    /// <inheritdoc/>
    /// <remarks>
    /// Generates a JSON-LD document from buffered features with semantic annotations.
    /// The implementation follows these steps:
    /// <list type="number">
    /// <item><description>Builds the base URL for generating stable feature URIs</description></item>
    /// <item><description>Converts buffered features to JSON-LD with @context annotations</description></item>
    /// <item><description>Builds OGC API pagination links for the collection</description></item>
    /// <item><description>Serializes the complete JSON-LD document</description></item>
    /// <item><description>Returns JSON-LD with Content-Crs header and cache headers</description></item>
    /// </list>
    ///
    /// The handler expects <see cref="FormatRequest.BufferedFeatures"/> to contain
    /// feature objects (typically GeoJSON features) which will be enhanced with
    /// JSON-LD semantic annotations.
    ///
    /// URI Generation:
    /// The handler uses proxy-aware base URL generation to ensure feature URIs
    /// are correct when the server is behind a reverse proxy or load balancer.
    /// This addresses a bug fix where JSON-LD URIs were not respecting the
    /// X-Forwarded-* headers.
    ///
    /// Context Generation:
    /// The @context is generated from the layer definition and includes:
    /// <list type="bullet">
    /// <item><description>Standard vocabulary prefixes (geosparql, dcterms, schema)</description></item>
    /// <item><description>Field-specific type mappings (string, number, date, etc.)</description></item>
    /// <item><description>Geometry and properties relationship mappings</description></item>
    /// </list>
    /// </remarks>
    /// <param name="request">The format request containing layer metadata, query parameters, and buffered features.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>
    /// A content result containing the JSON-LD document with:
    /// <list type="bullet">
    /// <item><description>Content-Type header with the specified MIME type</description></item>
    /// <item><description>Content-Crs header indicating the coordinate reference system</description></item>
    /// <item><description>Cache headers for feature resources</description></item>
    /// <item><description>ETag computed from serialized content</description></item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when buffered features are null or when JSON-LD serialization fails.
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
                "JSON-LD format requires buffered features. The FormatRequest.BufferedFeatures property must not be null.");
        }

        // Build proxy-aware base URL for generating stable feature URIs
        // BUG FIX #11: JSON-LD exporter now properly respects X-Forwarded-* headers
        // Use RequestLinkHelper to produce base URL with proper scheme/host/path normalization
        var baseUri = request.HttpRequest.BuildAbsoluteUrl("/").TrimEnd('/');

        // Build OGC API links for pagination and navigation
        var numberReturned = request.BufferedFeatures.Count;
        var links = FormatHandlerHelpers.BuildItemsLinks(
            request,
            request.Query,
            request.NumberMatched);

        // Convert features to JSON-LD with semantic annotations
        // Adds @context, @type, and @id to create linked data from GeoJSON
        var jsonLd = JsonLdFeatureFormatter.ToJsonLdFeatureCollection(
            baseUri,
            request.CollectionId,
            request.Layer,
            request.BufferedFeatures,
            request.NumberMatched ?? numberReturned,
            numberReturned,
            links);

        // Serialize the JSON-LD document to string
        var serialized = JsonLdFeatureFormatter.Serialize(jsonLd);

        // Record metrics for features returned
        FormatHandlerHelpers.RecordFeaturesReturned(request, numberReturned);

        // Create content result with appropriate MIME type
        var jsonLdResult = Results.Content(serialized, request.ContentType);

        // Add Content-Crs header to indicate coordinate reference system
        jsonLdResult = OgcSharedHandlers.WithContentCrsHeader(jsonLdResult, request.ContentCrs);

        // Generate ETag for cache validation
        var etag = request.Dependencies.CacheHeaderService.GenerateETag(serialized);

        // Return JSON-LD with cache headers
        return Task.FromResult(
            jsonLdResult.WithFeatureCacheHeaders(request.Dependencies.CacheHeaderService, etag));
    }
}
