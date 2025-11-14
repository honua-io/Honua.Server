// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Ogc.Features.Handlers;

/// <summary>
/// Base class for all OGC Items format handlers providing common validation logic.
/// </summary>
public abstract class FormatHandlerBase : IOgcItemsFormatHandler
{
    /// <inheritdoc/>
    public abstract OgcSharedHandlers.OgcResponseFormat Format { get; }

    /// <inheritdoc/>
    public virtual ValidationResult Validate(
        FeatureQuery query,
        string? requestedCrs,
        FormatContext context)
    {
        Guard.NotNull(query);
        Guard.NotNull(context);

        // Subclasses can override for format-specific validation
        return ValidationResult.Success;
    }

    /// <inheritdoc/>
    public abstract bool RequiresBuffering(FormatContext context);

    /// <inheritdoc/>
    public abstract Task<IResult> HandleAsync(
        FormatRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Validates that the query does not request resultType=hits, which is incompatible
    /// with export and some buffered formats.
    /// </summary>
    /// <param name="query">The feature query to validate.</param>
    /// <param name="formatName">The name of the format for error messaging.</param>
    /// <returns>Validation success or failure result.</returns>
    protected static ValidationResult ValidateNotHitsResultType(FeatureQuery query, string formatName)
    {
        if (query.ResultType == FeatureResultType.Hits)
        {
            return ValidationResult.Failure(
                $"{formatName} format does not support resultType=hits.",
                "resultType");
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Validates that the requested CRS is either null/empty or matches CRS84.
    /// Used by formats that only support WGS84 coordinates (KML, TopoJSON).
    /// </summary>
    /// <param name="requestedCrs">The requested coordinate reference system.</param>
    /// <param name="formatName">The name of the format for error messaging.</param>
    /// <returns>Validation success or failure result.</returns>
    protected static ValidationResult ValidateCrs84Only(string? requestedCrs, string formatName)
    {
        if (requestedCrs.HasValue() &&
            !string.Equals(
                CrsHelper.NormalizeIdentifier(requestedCrs),
                CrsHelper.DefaultCrsIdentifier,
                StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Failure(
                $"{formatName} output supports only CRS84.",
                "crs");
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Gets the effective CRS for the response, using the default if not specified.
    /// </summary>
    /// <param name="requestedCrs">The requested CRS, or null for default.</param>
    /// <param name="defaultCrs">The default CRS to use if none requested.</param>
    /// <returns>The effective CRS identifier.</returns>
    protected static string GetEffectiveCrs(string? requestedCrs, string? defaultCrs = null)
    {
        return requestedCrs.IsNullOrWhiteSpace()
            ? (defaultCrs ?? CrsHelper.DefaultCrsIdentifier)
            : requestedCrs;
    }
}

/// <summary>
/// Base class for export format handlers that generate downloadable files
/// (GeoPackage, Shapefile, FlatGeobuf, GeoArrow, CSV).
/// These formats stream directly from the repository to the export file without
/// buffering features in memory.
/// </summary>
public abstract class ExportFormatHandlerBase : FormatHandlerBase
{
    /// <inheritdoc/>
    /// <remarks>
    /// Export formats do not require buffering as they stream data directly
    /// through the exporter to the output file.
    /// </remarks>
    public override bool RequiresBuffering(FormatContext context) => false;

    /// <inheritdoc/>
    public override ValidationResult Validate(
        FeatureQuery query,
        string? requestedCrs,
        FormatContext context)
    {
        // Export formats don't support resultType=hits
        var baseValidation = base.Validate(query, requestedCrs, context);
        if (!baseValidation.IsValid)
        {
            return baseValidation;
        }

        return ValidateNotHitsResultType(query, this.GetFormatDisplayName());
    }

    /// <summary>
    /// Gets the display name of this format for error messages.
    /// </summary>
    /// <returns>The format display name (e.g., "GeoPackage", "Shapefile").</returns>
    protected abstract string GetFormatDisplayName();
}

/// <summary>
/// Base class for streaming format handlers that write responses incrementally
/// as features are retrieved (GeoJSON streaming, HTML streaming).
/// These formats do not buffer features in memory and can handle arbitrarily large datasets.
/// </summary>
public abstract class StreamingFormatHandlerBase : FormatHandlerBase
{
    /// <inheritdoc/>
    /// <remarks>
    /// Streaming formats do not require buffering as they write features
    /// incrementally as they are retrieved from the repository.
    /// </remarks>
    public override bool RequiresBuffering(FormatContext context) => false;

    /// <summary>
    /// Determines whether this request can use streaming based on context.
    /// Some conditions (like attachment exposure) may require buffering even for streaming formats.
    /// </summary>
    /// <param name="context">The format context.</param>
    /// <param name="query">The feature query.</param>
    /// <returns>True if streaming can be used; false if buffering is required.</returns>
    protected virtual bool CanUseStreaming(FormatContext context, FeatureQuery query)
    {
        // Streaming not supported with attachments (requires N+1 fix via buffering)
        if (context.ExposeAttachments)
        {
            return false;
        }

        // Hits result type requires count, not streaming
        if (query.ResultType == FeatureResultType.Hits)
        {
            return false;
        }

        return true;
    }
}

/// <summary>
/// Base class for buffered format handlers that must load all features into memory
/// before generating the response (KML, KMZ, TopoJSON, WKT, WKB, JSON-LD, GeoJSON-T).
/// These formats require complete feature collections for processing or special formatting.
/// </summary>
/// <remarks>
/// NOTE: Memory limitation - features are buffered in memory for in-memory response formats.
/// For large datasets, clients should use streaming export formats (GeoPackage, Shapefile,
/// FlatGeobuf, GeoArrow, CSV) which stream directly from the repository without buffering.
/// </remarks>
public abstract class BufferedFormatHandlerBase : FormatHandlerBase
{
    /// <inheritdoc/>
    /// <remarks>
    /// Buffered formats require all features to be loaded into memory before
    /// generating the response. This is necessary for formats that need to
    /// process the entire collection at once.
    /// </remarks>
    public override bool RequiresBuffering(FormatContext context) => true;
}

/// <summary>
/// Base class for format handlers that only support CRS84 (WGS84) coordinates
/// (KML, KMZ, TopoJSON). These formats enforce WGS84 in their specifications.
/// </summary>
public abstract class Crs84RequiredFormatHandlerBase : BufferedFormatHandlerBase
{
    /// <inheritdoc/>
    public override ValidationResult Validate(
        FeatureQuery query,
        string? requestedCrs,
        FormatContext context)
    {
        var baseValidation = base.Validate(query, requestedCrs, context);
        if (!baseValidation.IsValid)
        {
            return baseValidation;
        }

        return ValidateCrs84Only(requestedCrs, this.GetFormatDisplayName());
    }

    /// <summary>
    /// Gets the display name of this format for error messages.
    /// </summary>
    /// <returns>The format display name (e.g., "KML", "TopoJSON").</returns>
    protected abstract string GetFormatDisplayName();

    /// <summary>
    /// Creates a query with CRS84 enforced, regardless of the original CRS requested.
    /// </summary>
    /// <param name="originalQuery">The original feature query.</param>
    /// <returns>A new query with CRS set to CRS84.</returns>
    protected static FeatureQuery EnforceCrs84(FeatureQuery originalQuery)
    {
        return originalQuery with { Crs = CrsHelper.DefaultCrsIdentifier };
    }

    /// <summary>
    /// Gets the CRS84 identifier constant.
    /// </summary>
    /// <returns>The CRS84 identifier.</returns>
    protected static string GetCrs84Identifier()
    {
        return CrsHelper.DefaultCrsIdentifier;
    }
}

/// <summary>
/// Base class for HTML format handlers providing common HTML rendering utilities.
/// </summary>
public abstract class HtmlFormatHandlerBase : StreamingFormatHandlerBase
{
    /// <summary>
    /// Gets the HTML content type with UTF-8 charset.
    /// </summary>
    protected static string HtmlContentType => OgcSharedHandlers.HtmlContentType;

    /// <summary>
    /// Determines if this request should use streaming HTML rendering.
    /// </summary>
    /// <param name="context">The format context.</param>
    /// <param name="query">The feature query.</param>
    /// <returns>True if streaming should be used; false if buffering is required.</returns>
    protected override bool CanUseStreaming(FormatContext context, FeatureQuery query)
    {
        // HTML supports streaming unless attachments are exposed
        return !context.ExposeAttachments && query.ResultType != FeatureResultType.Hits;
    }
}

/// <summary>
/// Helper methods for format handlers.
/// </summary>
public static class FormatHandlerHelpers
{
    /// <summary>
    /// Builds OGC API links for a feature collection response.
    /// </summary>
    /// <param name="request">The format request containing HTTP request details.</param>
    /// <param name="query">The feature query with pagination parameters.</param>
    /// <param name="numberMatched">Total number of features matched, if known.</param>
    /// <returns>A list of OGC API links for pagination and navigation.</returns>
    public static IReadOnlyList<OgcLink> BuildItemsLinks(
        FormatRequest request,
        FeatureQuery query,
        long? numberMatched)
    {
        return OgcSharedHandlers.BuildItemsLinks(
            request.HttpRequest,
            request.CollectionId,
            query,
            numberMatched,
            OgcSharedHandlers.OgcResponseFormat.GeoJson, // Format is determined by handler
            request.ContentType);
    }

    /// <summary>
    /// Records metrics for features returned to the client.
    /// </summary>
    /// <param name="request">The format request containing dependencies.</param>
    /// <param name="featureCount">The number of features returned.</param>
    public static void RecordFeaturesReturned(FormatRequest request, int featureCount)
    {
        if (featureCount > 0)
        {
            request.Dependencies.ApiMetrics.RecordFeaturesReturned(
                "ogc-api-features",
                request.Service.Id,
                request.Layer.Id,
                featureCount);
        }
    }
}
