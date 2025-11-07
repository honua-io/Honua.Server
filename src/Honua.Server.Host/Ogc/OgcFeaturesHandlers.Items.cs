// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Raster.Export;
using Honua.Server.Core.Data;
using Honua.Server.Core.Exceptions;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Query;
using Honua.Server.Core.Results;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Styling;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Observability;
using Honua.Server.Host.Utilities;
using Honua.Server.Host.Attachments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static Honua.Server.Core.Serialization.JsonLdFeatureFormatter;
using static Honua.Server.Core.Serialization.GeoJsonTFeatureFormatter;

namespace Honua.Server.Host.Ogc;

internal static partial class OgcFeaturesHandlers
{
    /// <summary>
    /// Gets items (features) from a collection.
    /// OGC API - Features /collections/{collectionId}/items endpoint.
    /// </summary>
    public static Task<IResult> GetCollectionItems(
        string collectionId,
        HttpRequest request,
        IFeatureContextResolver resolver,
        IFeatureRepository repository,
        IGeoPackageExporter geoPackageExporter,
        IShapefileExporter shapefileExporter,
        IFlatGeobufExporter flatGeobufExporter,
        [FromServices] IGeoArrowExporter geoArrowExporter,
        ICsvExporter csvExporter,
        IFeatureAttachmentOrchestrator attachmentOrchestrator,
        IMetadataRegistry metadataRegistry,
        IApiMetrics apiMetrics,
        OgcCacheHeaderService cacheHeaderService,
        Services.IOgcFeaturesAttachmentHandler attachmentHandler,
        CancellationToken cancellationToken)
        => ExecuteCollectionItemsAsync(
            collectionId,
            request,
            resolver,
            repository,
            geoPackageExporter,
            shapefileExporter,
            flatGeobufExporter,
            geoArrowExporter,
            csvExporter,
            attachmentOrchestrator,
            metadataRegistry,
            apiMetrics,
            cacheHeaderService,
            attachmentHandler,
            queryOverrides: null,
            cancellationToken);

    /// <summary>
    /// Core implementation for retrieving collection items with support for various formats.
    /// </summary>
    internal static async Task<IResult> ExecuteCollectionItemsAsync(
        string collectionId,
        HttpRequest request,
        IFeatureContextResolver resolver,
        IFeatureRepository repository,
        IGeoPackageExporter geoPackageExporter,
        IShapefileExporter shapefileExporter,
        IFlatGeobufExporter flatGeobufExporter,
        IGeoArrowExporter geoArrowExporter,
        ICsvExporter csvExporter,
        IFeatureAttachmentOrchestrator attachmentOrchestrator,
        IMetadataRegistry metadataRegistry,
        IApiMetrics apiMetrics,
        OgcCacheHeaderService cacheHeaderService,
        Services.IOgcFeaturesAttachmentHandler attachmentHandler,
        IQueryCollection? queryOverrides,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);
        Guard.NotNull(resolver);
        Guard.NotNull(repository);
        Guard.NotNull(geoPackageExporter);
        Guard.NotNull(shapefileExporter);
        Guard.NotNull(flatGeobufExporter);
        Guard.NotNull(geoArrowExporter);
        Guard.NotNull(csvExporter);
        Guard.NotNull(attachmentOrchestrator);
        Guard.NotNull(metadataRegistry);
        Guard.NotNull(apiMetrics);
        Guard.NotNull(cacheHeaderService);

        var (context, contextError) = await OgcSharedHandlers.TryResolveCollectionAsync(collectionId, resolver, cancellationToken).ConfigureAwait(false);
        if (contextError is not null)
        {
            return contextError;
        }
        var service = context.Service;
        var layer = context.Layer;

        var (format, contentType, formatError) = OgcSharedHandlers.ResolveResponseFormat(request, queryOverrides);
        if (formatError is not null)
        {
            return formatError;
        }

        var (query, contentCrs, includeCount, error) = OgcSharedHandlers.ParseItemsQuery(request, service, layer, queryOverrides);
        if (error is not null)
        {
            return error;
        }

        var requestedCrsSource = queryOverrides ?? request.Query;
        var requestedCrs = requestedCrsSource["crs"].ToString();
        var isKmlLike = format is OgcSharedHandlers.OgcResponseFormat.Kml or OgcSharedHandlers.OgcResponseFormat.Kmz;
        var isTopo = format == OgcSharedHandlers.OgcResponseFormat.TopoJson;
        var isHtml = format == OgcSharedHandlers.OgcResponseFormat.Html;
        var isWkt = format == OgcSharedHandlers.OgcResponseFormat.Wkt;
        var isWkb = format == OgcSharedHandlers.OgcResponseFormat.Wkb;
        StyleDefinition? kmlStyle = null;

        if (isKmlLike || isTopo)
        {
            if (requestedCrs.HasValue() &&
                !string.Equals(CrsHelper.NormalizeIdentifier(requestedCrs), CrsHelper.DefaultCrsIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                var label = isTopo ? "TopoJSON" : "KML";
                return OgcSharedHandlers.CreateValidationProblem($"{label} output supports only CRS84.", "crs");
            }

            var defaultCrs = CrsHelper.DefaultCrsIdentifier;
            query = query with { Crs = defaultCrs };
            contentCrs = defaultCrs;

            if (isKmlLike)
            {
                var preferredStyleId = layer.DefaultStyleId.HasValue()
                    ? layer.DefaultStyleId
                    : layer.StyleIds.Count > 0
                        ? layer.StyleIds[0]
                        : "default";

                kmlStyle = await OgcSharedHandlers.ResolveStyleDefinitionAsync(preferredStyleId!, layer, metadataRegistry, cancellationToken).ConfigureAwait(false);
            }
        }
        else if (format == OgcSharedHandlers.OgcResponseFormat.GeoPackage)
        {
            if (query.ResultType == FeatureResultType.Hits)
            {
                return OgcSharedHandlers.CreateValidationProblem("GeoPackage format does not support resultType=hits.", "resultType");
            }

            var exportResult = await geoPackageExporter.ExportAsync(
                layer,
                query,
                contentCrs,
                repository.QueryAsync(service.Id, layer.Id, query, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            return Results.File(exportResult.Content, OgcSharedHandlers.GetMimeType(format), exportResult.FileName)
                .WithFeatureCacheHeaders(cacheHeaderService);
        }
        else if (format == OgcSharedHandlers.OgcResponseFormat.Shapefile)
        {
            if (query.ResultType == FeatureResultType.Hits)
            {
                return OgcSharedHandlers.CreateValidationProblem("Shapefile format does not support resultType=hits.", "resultType");
            }

            var shapefileResult = await shapefileExporter.ExportAsync(
                layer,
                query,
                contentCrs,
                repository.QueryAsync(service.Id, layer.Id, query, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            return Results.File(shapefileResult.Content, OgcSharedHandlers.GetMimeType(format), shapefileResult.FileName)
                .WithFeatureCacheHeaders(cacheHeaderService);
        }
        else if (format == OgcSharedHandlers.OgcResponseFormat.FlatGeobuf)
        {
            if (query.ResultType == FeatureResultType.Hits)
            {
                return OgcSharedHandlers.CreateValidationProblem("FlatGeobuf format does not support resultType=hits.", "resultType");
            }

            var effectiveCrs = contentCrs.IsNullOrWhiteSpace() ? CrsHelper.DefaultCrsIdentifier : contentCrs;
            var exportResult = await flatGeobufExporter.ExportAsync(
                layer,
                query,
                effectiveCrs,
                repository.QueryAsync(service.Id, layer.Id, query, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            return Results.File(exportResult.Content, OgcSharedHandlers.GetMimeType(format), exportResult.FileName)
                .WithFeatureCacheHeaders(cacheHeaderService);
        }
        else if (format == OgcSharedHandlers.OgcResponseFormat.GeoArrow)
        {
            if (query.ResultType == FeatureResultType.Hits)
            {
                return OgcSharedHandlers.CreateValidationProblem("GeoArrow format does not support resultType=hits.", "resultType");
            }

            var effectiveCrs = contentCrs.IsNullOrWhiteSpace() ? CrsHelper.DefaultCrsIdentifier : contentCrs;
            var exportResult = await geoArrowExporter.ExportAsync(
                layer,
                query,
                effectiveCrs,
                repository.QueryAsync(service.Id, layer.Id, query, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            return Results.File(exportResult.Content, OgcSharedHandlers.GetMimeType(format), exportResult.FileName)
                .WithFeatureCacheHeaders(cacheHeaderService);
        }
        else if (format == OgcSharedHandlers.OgcResponseFormat.Csv)
        {
            if (query.ResultType == FeatureResultType.Hits)
            {
                return OgcSharedHandlers.CreateValidationProblem("CSV format does not support resultType=hits.", "resultType");
            }

            var csvResult = await csvExporter.ExportAsync(
                layer,
                query,
                repository.QueryAsync(service.Id, layer.Id, query, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            return Results.File(csvResult.Content, OgcSharedHandlers.GetMimeType(format), csvResult.FileName)
                .WithFeatureCacheHeaders(cacheHeaderService);
        }

        var shouldCount = includeCount || query.ResultType == FeatureResultType.Hits;
        long? numberMatched = null;
        if (shouldCount)
        {
            numberMatched = await repository.CountAsync(service.Id, layer.Id, query, cancellationToken).ConfigureAwait(false);
        }

        var exposeAttachments = !isKmlLike && !isTopo && !isWkt && !isWkb && attachmentHandler.ShouldExposeAttachmentLinks(service, layer);

        var useStreaming = format == OgcSharedHandlers.OgcResponseFormat.GeoJson
            && !exposeAttachments
            && !isHtml
            && !isKmlLike
            && !isTopo
            && !isWkt
            && !isWkb
            && query.ResultType != FeatureResultType.Hits;
        var useStreamingHtml = isHtml
            && !exposeAttachments
            && query.ResultType != FeatureResultType.Hits;

        if (useStreaming)
        {
            var streamingLinks = OgcSharedHandlers.BuildItemsLinks(request, collectionId, query, numberMatched, format, contentType);
            var styleIdsStreaming = OgcSharedHandlers.BuildOrderedStyleIds(layer);
            var streamingQuery = query with { Offset = null };
            var featuresAsync = repository.QueryAsync(service.Id, layer.Id, streamingQuery, cancellationToken);
            featuresAsync = ApplyPaginationWindow(featuresAsync, query.Offset ?? 0, query.Limit);

            IResult streamingResult = new StreamingFeatureCollectionResult(
                featuresAsync,
                service,
                layer,
                numberMatched,
                streamingLinks,
                layer.DefaultStyleId,
                styleIdsStreaming,
                layer.MinScale,
                layer.MaxScale,
                contentType,
                apiMetrics);

            streamingResult = OgcSharedHandlers.WithContentCrsHeader(streamingResult, contentCrs);
            streamingResult = streamingResult.WithFeatureCacheHeaders(cacheHeaderService);
            return streamingResult;
        }

        if (useStreamingHtml)
        {
            var streamingLinks = OgcSharedHandlers.BuildItemsLinks(request, collectionId, query, numberMatched, format, contentType);
            var featuresAsync = repository.QueryAsync(service.Id, layer.Id, query, cancellationToken);

            IResult streamingResult = new StreamingHtmlFeatureCollectionResult(
                featuresAsync,
                service,
                layer,
                query,
                collectionId,
                numberMatched,
                streamingLinks,
                contentCrs,
                apiMetrics);

            streamingResult = OgcSharedHandlers.WithContentCrsHeader(streamingResult, contentCrs);
            streamingResult = streamingResult.WithFeatureCacheHeaders(cacheHeaderService);
            return streamingResult;
        }

        // NOTE: Memory limitation - features are buffered in memory for in-memory response formats.
        // For large datasets, use streaming export formats (GeoPackage, Shapefile, FlatGeobuf, GeoArrow, CSV)
        // which stream directly from the repository without buffering. See lines 534-619 above.
        var kmlFeatures = isKmlLike ? new List<KmlFeatureContent>() : null;
        var topoFeatures = isTopo ? new List<TopoJsonFeatureContent>() : null;
        var wktFeatures = isWkt ? new List<FeatureRecord>() : null;
        var wkbFeatures = isWkb ? new List<FeatureRecord>() : null;
        var features = (!isKmlLike && !isTopo && !isWkt && !isWkb) ? new List<object>() : null;
        var htmlComponents = isHtml ? new List<FeatureComponents>() : null;

        // N+1 FIX: Batch-load attachments for all features in the page to avoid per-feature queries
        IReadOnlyDictionary<string, IReadOnlyList<AttachmentDescriptor>>? attachmentMap = null;
        if (query.ResultType != FeatureResultType.Hits && exposeAttachments)
        {
            // First pass: collect all feature IDs from the current page
            var featureRecords = new List<FeatureRecord>();
            await foreach (var record in repository.QueryAsync(service.Id, layer.Id, query, cancellationToken).ConfigureAwait(false))
            {
                featureRecords.Add(record);
            }

            // Extract feature IDs for batch loading
            var featureIds = new List<string>(featureRecords.Count);
            foreach (var record in featureRecords)
            {
                var components = FeatureComponentBuilder.BuildComponents(layer, record, query);
                if (!string.IsNullOrWhiteSpace(components.FeatureId))
                {
                    featureIds.Add(components.FeatureId);
                }
            }

            // Batch-load all attachments for these features (single query instead of N queries)
            if (featureIds.Count > 0)
            {
                attachmentMap = await attachmentOrchestrator.ListBatchAsync(
                    service.Id,
                    layer.Id,
                    featureIds,
                    cancellationToken).ConfigureAwait(false);
            }

            // Second pass: process features with pre-loaded attachments
            foreach (var record in featureRecords)
            {
                if (isKmlLike)
                {
                    kmlFeatures!.Add(FeatureComponentBuilder.CreateKmlContent(layer, record, query));
                }
                else if (isTopo)
                {
                    topoFeatures!.Add(FeatureComponentBuilder.CreateTopoContent(layer, record, query));
                }
                else if (isWkt)
                {
                    wktFeatures!.Add(record);
                }
                else if (isWkb)
                {
                    wkbFeatures!.Add(record);
                }
                else
                {
                    FeatureComponents? componentsOverride = null;
                    IReadOnlyList<OgcLink>? attachmentLinks = null;

                    if (isHtml || exposeAttachments)
                    {
                        componentsOverride = FeatureComponentBuilder.BuildComponents(layer, record, query);
                        if (isHtml)
                        {
                            htmlComponents!.Add(componentsOverride);
                        }
                    }

                    if (exposeAttachments && componentsOverride is not null)
                    {
                        if (attachmentMap is not null)
                        {
                            if (!attachmentMap.TryGetValue(componentsOverride.FeatureId, out var descriptorsForFeature))
                            {
                                descriptorsForFeature = Array.Empty<AttachmentDescriptor>();
                            }

                            attachmentLinks = await attachmentHandler.CreateAttachmentLinksAsync(
                                request,
                                service,
                                layer,
                                collectionId,
                                componentsOverride,
                                attachmentOrchestrator,
                                descriptorsForFeature,
                                cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            attachmentLinks = await attachmentHandler.CreateAttachmentLinksAsync(
                                request,
                                service,
                                layer,
                                collectionId,
                                componentsOverride,
                                attachmentOrchestrator,
                                cancellationToken).ConfigureAwait(false);
                        }

                        if (attachmentLinks.Count == 0)
                        {
                            attachmentLinks = null;
                        }
                    }

                    features!.Add(OgcSharedHandlers.ToFeature(request, collectionId, layer, record, query, componentsOverride, attachmentLinks));
                }
            }
        }
        else if (query.ResultType != FeatureResultType.Hits)
        {
            // No attachments needed - process features directly
            await foreach (var record in repository.QueryAsync(service.Id, layer.Id, query, cancellationToken).ConfigureAwait(false))
            {
                if (isKmlLike)
                {
                    kmlFeatures!.Add(FeatureComponentBuilder.CreateKmlContent(layer, record, query));
                }
                else if (isTopo)
                {
                    topoFeatures!.Add(FeatureComponentBuilder.CreateTopoContent(layer, record, query));
                }
                else if (isWkt)
                {
                    wktFeatures!.Add(record);
                }
                else if (isWkb)
                {
                    wkbFeatures!.Add(record);
                }
                else
                {
                    FeatureComponents? componentsOverride = null;

                    if (isHtml)
                    {
                        componentsOverride = FeatureComponentBuilder.BuildComponents(layer, record, query);
                        htmlComponents!.Add(componentsOverride);
                    }

                    features!.Add(OgcSharedHandlers.ToFeature(request, collectionId, layer, record, query, componentsOverride, null));
                }
            }
        }

        var numberReturned = query.ResultType == FeatureResultType.Hits
            ? 0
            : isKmlLike
                ? kmlFeatures!.Count
                : isTopo
                    ? topoFeatures!.Count
                    : isWkt
                        ? wktFeatures!.Count
                        : isWkb
                            ? wkbFeatures!.Count
                            : features!.Count;

        if (numberReturned > 0)
        {
            apiMetrics.RecordFeaturesReturned("ogc-api-features", service.Id, layer.Id, numberReturned);
        }

        if (isKmlLike)
        {
            try
            {
                var matchedValue = numberMatched ?? numberReturned;
                var payload = KmlFeatureFormatter.WriteFeatureCollection(
                    collectionId,
                    layer,
                    kmlFeatures!,
                    matchedValue,
                    numberReturned,
                    kmlStyle);

                if (format == OgcSharedHandlers.OgcResponseFormat.Kmz)
                {
                    var entryName = FileNameHelper.BuildArchiveEntryName(collectionId, null);
                    var archive = KmzArchiveBuilder.CreateArchive(payload, entryName);
                    var downloadName = FileNameHelper.BuildDownloadFileName(collectionId, null, "kmz");
                    var kmzEtag = cacheHeaderService.GenerateETag(archive);
                    return Results.File(archive, OgcSharedHandlers.GetMimeType(format), downloadName)
                        .WithFeatureCacheHeaders(cacheHeaderService, kmzEtag);
                }

                var bytes = Encoding.UTF8.GetBytes(payload);
                var fileName = FileNameHelper.BuildDownloadFileName(collectionId, null, "kml");
                var kmlEtag = cacheHeaderService.GenerateETag(bytes);
                return Results.File(bytes, OgcSharedHandlers.GetMimeType(format), fileName)
                    .WithFeatureCacheHeaders(cacheHeaderService, kmlEtag);
            }
            catch (InvalidOperationException)
            {
                return Results.Problem("KML conversion failed. Check server logs for details.", statusCode: StatusCodes.Status500InternalServerError, title: "KML conversion failed");
            }
        }

        if (isTopo)
        {
            try
            {
                var payload = TopoJsonFeatureFormatter.WriteFeatureCollection(
                    collectionId,
                    layer,
                    topoFeatures!,
                    numberMatched ?? numberReturned,
                    numberReturned);

                var topoResult = Results.Content(payload, OgcSharedHandlers.GetMimeType(format));
                topoResult = OgcSharedHandlers.WithContentCrsHeader(topoResult, contentCrs);
                var topoEtag = cacheHeaderService.GenerateETag(payload);
                return topoResult.WithFeatureCacheHeaders(cacheHeaderService, topoEtag);
            }
            catch (InvalidOperationException)
            {
                return Results.Problem("TopoJSON conversion failed. Check server logs for details.", statusCode: StatusCodes.Status500InternalServerError, title: "TopoJSON conversion failed");
            }
        }

        if (isWkt)
        {
            try
            {
                var payload = WktFeatureFormatter.WriteFeatureCollection(
                    collectionId,
                    layer,
                    wktFeatures!,
                    numberMatched ?? numberReturned,
                    numberReturned);

                var wktResult = Results.Content(payload, OgcSharedHandlers.GetMimeType(format));
                wktResult = OgcSharedHandlers.WithContentCrsHeader(wktResult, contentCrs);
                var wktEtag = cacheHeaderService.GenerateETag(payload);
                return wktResult.WithFeatureCacheHeaders(cacheHeaderService, wktEtag);
            }
            catch (InvalidOperationException)
            {
                return Results.Problem("WKT conversion failed. Check server logs for details.", statusCode: StatusCodes.Status500InternalServerError, title: "WKT conversion failed");
            }
        }

        if (isWkb)
        {
            try
            {
                var payload = WkbFeatureFormatter.WriteFeatureCollection(
                    collectionId,
                    layer,
                    wkbFeatures!,
                    numberMatched ?? numberReturned,
                    numberReturned);

                var fileName = FileNameHelper.BuildDownloadFileName(collectionId, null, "wkb");
                var wkbEtag = cacheHeaderService.GenerateETag(payload);
                var wkbResult = Results.File(payload, OgcSharedHandlers.GetMimeType(format), fileName);
                wkbResult = OgcSharedHandlers.WithContentCrsHeader(wkbResult, contentCrs);
                return wkbResult.WithFeatureCacheHeaders(cacheHeaderService, wkbEtag);
            }
            catch (InvalidOperationException)
            {
                return Results.Problem("WKB conversion failed. Check server logs for details.", statusCode: StatusCodes.Status500InternalServerError, title: "WKB conversion failed");
            }
        }

        var links = OgcSharedHandlers.BuildItemsLinks(request, collectionId, query, numberMatched, format, contentType);
        if (isHtml)
        {
            var htmlEntries = htmlComponents!
                .Select(components => new OgcSharedHandlers.HtmlFeatureEntry(collectionId, layer.Title ?? collectionId, components))
                .ToList();

            var html = OgcSharedHandlers.RenderFeatureCollectionHtml(
                layer.Title ?? collectionId,
                layer.Description,
                htmlEntries,
                numberMatched,
                numberReturned,
                contentCrs,
                links,
                query.ResultType == FeatureResultType.Hits);

            var htmlResult = Results.Content(html, OgcSharedHandlers.HtmlContentType);
            htmlResult = OgcSharedHandlers.WithContentCrsHeader(htmlResult, contentCrs);
            var etag = cacheHeaderService.GenerateETag(html);
            return htmlResult.WithFeatureCacheHeaders(cacheHeaderService, etag);
        }

        if (format == OgcSharedHandlers.OgcResponseFormat.JsonLd)
        {
            // BUG FIX #11: JSON-LD exporter ignores proxy-aware base URL
            // Use RequestLinkHelper to produce base URL with proper scheme/host/path normalization
            var baseUri = request.BuildAbsoluteUrl("/").TrimEnd('/');
            var jsonLd = JsonLdFeatureFormatter.ToJsonLdFeatureCollection(
                baseUri,
                collectionId,
                layer,
                features!,
                numberMatched ?? numberReturned,
                numberReturned,
                links);

            var serialized = JsonLdFeatureFormatter.Serialize(jsonLd);
            var jsonLdResult = Results.Content(serialized, contentType);
            jsonLdResult = OgcSharedHandlers.WithContentCrsHeader(jsonLdResult, contentCrs);
            var etag = cacheHeaderService.GenerateETag(serialized);
            return jsonLdResult.WithFeatureCacheHeaders(cacheHeaderService, etag);
        }

        if (format == OgcSharedHandlers.OgcResponseFormat.GeoJsonT)
        {
            var geoJsonT = GeoJsonTFeatureFormatter.ToGeoJsonTFeatureCollection(
                features!,
                numberMatched ?? numberReturned,
                numberReturned,
                layer.Temporal?.StartField,
                layer.Temporal?.EndField,
                null,
                links);

            var serialized = GeoJsonTFeatureFormatter.Serialize(geoJsonT);
            var geoJsonTResult = Results.Content(serialized, contentType);
            geoJsonTResult = OgcSharedHandlers.WithContentCrsHeader(geoJsonTResult, contentCrs);
            var etag = cacheHeaderService.GenerateETag(serialized);
            return geoJsonTResult.WithFeatureCacheHeaders(cacheHeaderService, etag);
        }

        var styleIds = OgcSharedHandlers.BuildOrderedStyleIds(layer);
        var effectiveMatched = numberMatched ?? numberReturned;
        var response = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "FeatureCollection",
            ["features"] = features,
            ["timeStamp"] = DateTimeOffset.UtcNow,
            ["numberReturned"] = numberReturned,
            ["numberMatched"] = effectiveMatched,
            ["links"] = links,
            ["styleIds"] = styleIds,
            ["minScale"] = layer.MinScale,
            ["maxScale"] = layer.MaxScale
        };

        if (layer.DefaultStyleId.HasValue())
        {
            response["defaultStyle"] = layer.DefaultStyleId;
        }

        var jsonResult = Results.Json(response, OgcSharedHandlers.GeoJsonSerializerOptions, contentType);
        jsonResult = OgcSharedHandlers.WithContentCrsHeader(jsonResult, contentCrs);
        var responseEtag = cacheHeaderService.GenerateETagForObject(response);
        return jsonResult.WithFeatureCacheHeaders(cacheHeaderService, responseEtag);
    }

    /// <summary>
    /// Gets a single item (feature) from a collection.
    /// OGC API - Features /collections/{collectionId}/items/{featureId} endpoint.
    /// </summary>
    public static async Task<IResult> GetCollectionItem(
        string collectionId,
        string featureId,
        HttpRequest request,
        IFeatureContextResolver resolver,
        IFeatureRepository repository,
        IFeatureAttachmentOrchestrator attachmentOrchestrator,
        IMetadataRegistry metadataRegistry,
        OgcCacheHeaderService cacheHeaderService,
        Services.IOgcFeaturesAttachmentHandler attachmentHandler,
        Services.IOgcFeaturesEditingHandler editingHandler,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(attachmentOrchestrator);
        Guard.NotNull(cacheHeaderService);
        var (context, contextError) = await OgcSharedHandlers.TryResolveCollectionAsync(collectionId, resolver, cancellationToken).ConfigureAwait(false);
        if (contextError is not null)
        {
            return contextError;
        }
        var service = context.Service;
        var layer = context.Layer;

        var (format, contentType, formatError) = OgcSharedHandlers.ResolveResponseFormat(request);
        if (formatError is not null)
        {
            return formatError;
        }

        if (format == OgcSharedHandlers.OgcResponseFormat.GeoPackage || format == OgcSharedHandlers.OgcResponseFormat.Shapefile)
        {
            var label = format == OgcSharedHandlers.OgcResponseFormat.GeoPackage ? "GeoPackage" : "Shapefile";
            return OgcSharedHandlers.CreateValidationProblem($"{label} format is only available for collection queries.", "f");
        }

        var supportedCrs = OgcSharedHandlers.ResolveSupportedCrs(service, layer);
        var (acceptCrs, acceptCrsError) = OgcSharedHandlers.ResolveAcceptCrs(request, supportedCrs);
        if (acceptCrsError is not null)
        {
            return acceptCrsError;
        }

        var requestedCrsRaw = request.Query["crs"].ToString();
        if (requestedCrsRaw.IsNullOrWhiteSpace() && acceptCrs.HasValue())
        {
            requestedCrsRaw = acceptCrs;
        }
        var isKmlLike = format is OgcSharedHandlers.OgcResponseFormat.Kml or OgcSharedHandlers.OgcResponseFormat.Kmz;
        var isTopo = format == OgcSharedHandlers.OgcResponseFormat.TopoJson;
        var isHtml = format == OgcSharedHandlers.OgcResponseFormat.Html;
        string contentCrs;
        StyleDefinition? kmlStyle = null;

        if (isKmlLike || isTopo)
        {
            if (requestedCrsRaw.HasValue() &&
                !string.Equals(CrsHelper.NormalizeIdentifier(requestedCrsRaw), CrsHelper.DefaultCrsIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                var label = isTopo ? "TopoJSON" : "KML";
                return OgcSharedHandlers.CreateValidationProblem($"{label} output supports only CRS84.", "crs");
            }

            contentCrs = CrsHelper.DefaultCrsIdentifier;
        }
        else
        {
            var (resolvedCrs, crsError) = OgcSharedHandlers.ResolveContentCrs(
                requestedCrsRaw.IsNullOrWhiteSpace() ? null : requestedCrsRaw,
                service,
                layer);
            if (crsError is not null)
            {
                return crsError;
            }

            contentCrs = resolvedCrs.IsNullOrWhiteSpace()
                ? OgcSharedHandlers.DetermineDefaultCrs(service, OgcSharedHandlers.ResolveSupportedCrs(service, layer))
                : resolvedCrs;
        }

        var featureQuery = new FeatureQuery(Crs: contentCrs);
        if (isKmlLike)
        {
            var preferredStyleId = layer.DefaultStyleId.HasValue()
                ? layer.DefaultStyleId
                : layer.StyleIds.Count > 0 ? layer.StyleIds[0] : "default";

            kmlStyle = await OgcSharedHandlers.ResolveStyleDefinitionAsync(preferredStyleId!, layer, metadataRegistry, cancellationToken).ConfigureAwait(false);
        }

        FeatureRecord? record;
        try
        {
            record = await repository.GetAsync(service.Id, layer.Id, featureId, featureQuery, cancellationToken).ConfigureAwait(false);
        }
        catch (FeatureNotFoundException)
        {
            return OgcSharedHandlers.CreateNotFoundProblem($"Feature '{featureId}' was not found in collection '{collectionId}'.");
        }
        catch (KeyNotFoundException)
        {
            return OgcSharedHandlers.CreateNotFoundProblem($"Feature '{featureId}' was not found in collection '{collectionId}'.");
        }
        if (record is null)
        {
            return OgcSharedHandlers.CreateNotFoundProblem($"Feature '{featureId}' was not found in collection '{collectionId}'.");
        }

        if (isKmlLike)
        {
            try
            {
                var content = FeatureComponentBuilder.CreateKmlContent(layer, record, featureQuery);
                var payload = KmlFeatureFormatter.WriteSingleFeature(collectionId, layer, content, kmlStyle);

                if (format == OgcSharedHandlers.OgcResponseFormat.Kmz)
                {
                    var entryName = FileNameHelper.BuildArchiveEntryName(collectionId, featureId);
                    var archive = KmzArchiveBuilder.CreateArchive(payload, entryName);
                    var downloadName = FileNameHelper.BuildDownloadFileName(collectionId, featureId, "kmz");
                    var kmzEtag = cacheHeaderService.GenerateETag(archive);
                    return Results.File(archive, OgcSharedHandlers.GetMimeType(format), downloadName)
                        .WithFeatureCacheHeaders(cacheHeaderService, kmzEtag);
                }

                var bytes = Encoding.UTF8.GetBytes(payload);
                var fileName = FileNameHelper.BuildDownloadFileName(collectionId, featureId, "kml");
                var kmlEtag = cacheHeaderService.GenerateETag(bytes);
                return Results.File(bytes, OgcSharedHandlers.GetMimeType(format), fileName)
                    .WithFeatureCacheHeaders(cacheHeaderService, kmlEtag);
            }
            catch (InvalidOperationException)
            {
                return Results.Problem("KML conversion failed. Check server logs for details.", statusCode: StatusCodes.Status500InternalServerError, title: "KML conversion failed");
            }
        }

        if (isTopo)
        {
            try
            {
                var content = FeatureComponentBuilder.CreateTopoContent(layer, record, featureQuery);
                var payload = TopoJsonFeatureFormatter.WriteSingleFeature(collectionId, layer, content);
                var topoResult = Results.Content(payload, contentType);
                topoResult = OgcSharedHandlers.WithContentCrsHeader(topoResult, contentCrs);
                var topoEtag = cacheHeaderService.GenerateETag(payload);
                return topoResult.WithFeatureCacheHeaders(cacheHeaderService, topoEtag);
            }
            catch (InvalidOperationException)
            {
                return Results.Problem("TopoJSON conversion failed. Check server logs for details.", statusCode: StatusCodes.Status500InternalServerError, title: "TopoJSON conversion failed");
            }
        }

        FeatureComponents? componentsOverride = isHtml ? FeatureComponentBuilder.BuildComponents(layer, record, featureQuery) : null;
        IReadOnlyList<OgcLink>? attachmentLinks = null;

        if (attachmentHandler.ShouldExposeAttachmentLinks(service, layer))
        {
            componentsOverride ??= FeatureComponentBuilder.BuildComponents(layer, record, featureQuery);
            try
            {
                var descriptors = await attachmentHandler.CreateAttachmentLinksAsync(
                    request,
                    service,
                    layer,
                    collectionId,
                    componentsOverride,
                    attachmentOrchestrator,
                    cancellationToken).ConfigureAwait(false);

                if (descriptors.Count > 0)
                {
                    attachmentLinks = descriptors;
                }
            }
            catch (FeatureNotFoundException)
            {
                attachmentLinks = Array.Empty<OgcLink>();
            }
            catch (KeyNotFoundException)
            {
                attachmentLinks = Array.Empty<OgcLink>();
            }
        }

        var feature = OgcSharedHandlers.ToFeature(request, collectionId, layer, record, featureQuery, componentsOverride, attachmentLinks);
        var etag = editingHandler.ComputeFeatureEtag(layer, record);
        if (isHtml)
        {
            var effectiveComponents = componentsOverride ?? FeatureComponentBuilder.BuildComponents(layer, record, featureQuery);
            var featureLinks = OgcSharedHandlers.BuildFeatureLinks(request, collectionId, layer, effectiveComponents, attachmentLinks);
            var html = OgcSharedHandlers.RenderFeatureHtml(
                layer.Title ?? collectionId,
                layer.Description,
                new OgcSharedHandlers.HtmlFeatureEntry(collectionId, layer.Title ?? collectionId, effectiveComponents),
                contentCrs,
                featureLinks);

            var htmlResult = Results.Content(html, OgcSharedHandlers.HtmlContentType);
            htmlResult = OgcSharedHandlers.WithContentCrsHeader(htmlResult, contentCrs);
            return htmlResult.WithFeatureCacheHeaders(cacheHeaderService, etag);
        }

        if (format == OgcSharedHandlers.OgcResponseFormat.JsonLd)
        {
            // BUG FIX #11: JSON-LD exporter ignores proxy-aware base URL
            // Use RequestLinkHelper to produce base URL with proper scheme/host/path normalization
            var baseUri = request.BuildAbsoluteUrl("/").TrimEnd('/');
            var jsonLd = JsonLdFeatureFormatter.ToJsonLdFeature(baseUri, collectionId, layer, feature);
            var serialized = JsonLdFeatureFormatter.Serialize(jsonLd);
            var jsonLdResult = Results.Content(serialized, contentType);
            jsonLdResult = OgcSharedHandlers.WithContentCrsHeader(jsonLdResult, contentCrs);
            return jsonLdResult.WithFeatureCacheHeaders(cacheHeaderService, etag);
        }

        if (format == OgcSharedHandlers.OgcResponseFormat.GeoJsonT)
        {
            var geoJsonT = GeoJsonTFeatureFormatter.ToGeoJsonTFeature(
                feature,
                layer.Temporal?.StartField,
                layer.Temporal?.EndField,
                null);
            var serialized = GeoJsonTFeatureFormatter.Serialize(geoJsonT);
            var geoJsonTResult = Results.Content(serialized, contentType);
            geoJsonTResult = OgcSharedHandlers.WithContentCrsHeader(geoJsonTResult, contentCrs);
            return geoJsonTResult.WithFeatureCacheHeaders(cacheHeaderService, etag);
        }

        var jsonResult = Results.Json(feature, OgcSharedHandlers.GeoJsonSerializerOptions, contentType);
        jsonResult = OgcSharedHandlers.WithContentCrsHeader(jsonResult, contentCrs);
        return jsonResult.WithFeatureCacheHeaders(cacheHeaderService, etag);
    }

    /// <summary>
    /// Applies skip/take pagination window to an async enumerable of feature records.
    /// </summary>
    private static IAsyncEnumerable<FeatureRecord> ApplyPaginationWindow(
        IAsyncEnumerable<FeatureRecord> source,
        int skip,
        int? take)
    {
        var enforceSkip = skip > 0;
        var enforceTake = take.HasValue && take.Value >= 0;
        if (!enforceSkip && !enforceTake)
        {
            return source;
        }

        return Window();

        async IAsyncEnumerable<FeatureRecord> Window()
        {
            var remainingSkip = enforceSkip ? Math.Max(0, skip) : 0;
            var remainingTake = enforceTake ? take!.Value : int.MaxValue;

            await foreach (var record in source)
            {
                if (enforceSkip && remainingSkip > 0)
                {
                    remainingSkip--;
                    continue;
                }
                if (enforceTake && remainingTake <= 0)
                {
                    yield break;
                }
                if (enforceTake) remainingTake--;

                yield return record;
            }
        }
    }

    /// <summary>
    /// Streaming result for GeoJSON feature collections.
    /// </summary>
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

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.ContentType = _contentType;
            var cancellationToken = httpContext.RequestAborted;

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

            if (count > 0)
            {
                _apiMetrics.RecordFeaturesReturned("ogc-api-features", _service.Id, _layer.Id, count);
            }
        }
    }

    /// <summary>
    /// Streaming result for HTML feature collections.
    /// </summary>
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

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.ContentType = OgcSharedHandlers.HtmlContentType;
            var cancellationToken = httpContext.RequestAborted;

            await using var writer = new StreamWriter(httpContext.Response.Body, Encoding.UTF8, bufferSize: 4096, leaveOpen: true);

            await WriteHeaderAsync(writer).ConfigureAwait(false);

            var returned = 0L;
            var hasFeatures = false;

            await foreach (var record in _features.WithCancellation(cancellationToken))
            {
                var components = FeatureComponentBuilder.BuildComponents(_layer, record, _query);
                await WriteFeatureAsync(writer, components).ConfigureAwait(false);
                hasFeatures = true;
                returned++;

                if (returned % 10 == 0)
                {
                    await writer.FlushAsync().ConfigureAwait(false);
                }
            }

            await WriteFooterAsync(writer, hasFeatures, returned).ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);

            if (returned > 0)
            {
                _apiMetrics.RecordFeaturesReturned("ogc-api-features", _service.Id, _layer.Id, returned);
            }
        }

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

        private static string HtmlEncode(string? value)
            => WebUtility.HtmlEncode(value ?? string.Empty);
    }
}
