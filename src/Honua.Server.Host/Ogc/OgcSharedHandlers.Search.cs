// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

// This file contains search execution methods for OGC API features.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Data;
using Honua.Server.Core.Editing;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Export;
using Honua.Server.Core.Results;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Styling;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Raster;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Host.Ogc;

internal static partial class OgcSharedHandlers
{
    internal static async Task<IResult> ExecuteSearchAsync(
        HttpRequest request,
        IReadOnlyList<string> collections,
        IQueryCollection queryParameters,
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
        CancellationToken cancellationToken)
    {
        static QueryCollection RemoveCollectionsParameter(IQueryCollection source)
        {
            var dictionary = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in source)
            {
                if (string.Equals(pair.Key, "collections", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                dictionary[pair.Key] = pair.Value;
            }

            return new QueryCollection(dictionary);
        }

        if (collections.Count == 0)
        {
            return CreateValidationProblem("At least one collection must be specified.", "collections");
        }

        var resolutions = new List<SearchCollectionContext>(collections.Count);
        foreach (var collectionId in collections)
        {
            var resolution = await ResolveCollectionAsync(collectionId, resolver, cancellationToken).ConfigureAwait(false);
            if (resolution.IsFailure)
            {
                return MapCollectionResolutionError(resolution.Error!, collectionId);
            }

            resolutions.Add(new SearchCollectionContext(collectionId, resolution.Value));
        }

        var (format, contentType, formatError) = ResolveResponseFormat(request, queryParameters);
        if (formatError is not null)
        {
            return formatError;
        }

        var supportsAggregation = format is OgcResponseFormat.GeoJson or OgcResponseFormat.Html;
        if (!supportsAggregation)
        {
            if (collections.Count != 1)
            {
                return CreateValidationProblem("The requested format requires a single collection.", "collections");
            }

            var sanitized = RemoveCollectionsParameter(queryParameters);
            return await OgcFeaturesHandlers.ExecuteCollectionItemsAsync(
                collections[0],
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
                new Core.Elevation.DefaultElevationService(),
                sanitized,
                cancellationToken).ConfigureAwait(false);
        }

        var isHtml = format == OgcResponseFormat.Html;
        var preparedQueries = new List<(SearchCollectionContext Context, FeatureQuery Query, string ContentCrs)>(resolutions.Count);
        FeatureQuery? baseQuery = null;
        FeatureResultType resultType = FeatureResultType.Results;
        string? globalContentCrs = null;
        var includeCount = false;

        foreach (var context in resolutions)
        {
            var (query, contentCrs, includeCountFlag, error) = ParseItemsQuery(request, context.FeatureContext.Service, context.FeatureContext.Layer, queryParameters);
            if (error is not null)
            {
                return error;
            }

            includeCount |= includeCountFlag;

            if (baseQuery is null)
            {
                baseQuery = query;
                resultType = query.ResultType;
            }
            else if (query.ResultType != resultType)
            {
                return CreateValidationProblem("Mixed resultType values are not supported across collections.", "resultType");
            }

            if (globalContentCrs is null)
            {
                globalContentCrs = contentCrs;
            }
            else if (!string.Equals(globalContentCrs, contentCrs, StringComparison.OrdinalIgnoreCase))
            {
                return CreateValidationProblem("All collections in a search must share a common response CRS.", "crs");
            }

            preparedQueries.Add((context, query, contentCrs));
        }

        if (baseQuery is null)
        {
            baseQuery = new FeatureQuery(ResultType: FeatureResultType.Results);
        }

        if (resultType == FeatureResultType.Hits)
        {
            includeCount = true;
        }
        var needsOffsetDistribution = (baseQuery.Offset ?? 0) > 0;

        var initialLimit = baseQuery.Limit.HasValue ? Math.Max(1, (long)baseQuery.Limit.Value) : long.MaxValue;
        var initialOffset = baseQuery.Offset ?? 0;
        if (!needsOffsetDistribution)
        {
            initialOffset = 0;
        }

        if (isHtml)
        {
            var htmlEntries = new List<HtmlFeatureEntry>();

            var iterationResult = await EnumerateSearchAsync(
                preparedQueries,
                resultType,
                includeCount,
                initialLimit,
                initialOffset,
                repository,
                (context, layer, query, record, components) =>
                {
                    htmlEntries.Add(new HtmlFeatureEntry(context.CollectionId, layer.Title ?? context.CollectionId, components));
                    return ValueTask.FromResult(true);
                },
                cancellationToken).ConfigureAwait(false);

            var links = BuildSearchLinks(request, collections, baseQuery, iterationResult.NumberMatched, contentType);
            var htmlDescription = collections.Count == 1
                ? $"Collection: {collections[0]}"
                : $"Collections: {string.Join(", ", collections)}";

            var html = RenderFeatureCollectionHtml(
                "Search results",
                htmlDescription,
                htmlEntries,
                iterationResult.NumberMatched,
                iterationResult.NumberReturned,
                globalContentCrs,
                links,
                resultType == FeatureResultType.Hits);

            var htmlResult = Results.Content(html, HtmlContentType);
            return WithContentCrsHeader(htmlResult, globalContentCrs);
        }

        var geoJsonResult = Results.Stream(async stream =>
        {
            await WriteGeoJsonSearchResponseAsync(
                stream,
                request,
                collections,
                contentType,
                baseQuery,
                preparedQueries,
                includeCount,
                resultType,
                repository,
                initialLimit,
                initialOffset,
                cancellationToken).ConfigureAwait(false);
        }, contentType);

        return WithContentCrsHeader(geoJsonResult, globalContentCrs);
    }
    private static async Task WriteGeoJsonSearchResponseAsync(
        Stream outputStream,
        HttpRequest request,
        IReadOnlyList<string> collections,
        string contentType,
        FeatureQuery baseQuery,
        IReadOnlyList<(SearchCollectionContext Context, FeatureQuery Query, string ContentCrs)> preparedQueries,
        bool includeCount,
        FeatureResultType resultType,
        IFeatureRepository repository,
        long initialLimit,
        long initialOffset,
        CancellationToken cancellationToken)
    {
        await using var writer = new Utf8JsonWriter(outputStream, new JsonWriterOptions { SkipValidation = false });

        writer.WriteStartObject();
        writer.WriteString("type", "FeatureCollection");
        writer.WriteString("timeStamp", DateTimeOffset.UtcNow);
        writer.WritePropertyName("features");
        writer.WriteStartArray();
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

        var iterationResult = await EnumerateSearchAsync(
            preparedQueries,
            resultType,
            includeCount,
            initialLimit,
            initialOffset,
            repository,
            async (context, layer, query, record, components) =>
            {
                var feature = ToFeature(request, context.CollectionId, layer, record, query, components);
                JsonSerializer.Serialize(writer, feature, GeoJsonSerializerOptions);

                if (writer.BytesPending > 8192)
                {
                    await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                return true;
            },
            cancellationToken).ConfigureAwait(false);

        writer.WriteEndArray();

        var matched = iterationResult.NumberMatched ?? iterationResult.NumberReturned;
        writer.WriteNumber("numberMatched", matched);
        writer.WriteNumber("numberReturned", iterationResult.NumberReturned);

        var links = BuildSearchLinks(request, collections, baseQuery, iterationResult.NumberMatched, contentType);
        writer.WritePropertyName("links");
        JsonSerializer.Serialize(writer, links, GeoJsonSerializerOptions);

        writer.WriteEndObject();
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<SearchIterationResult> EnumerateSearchAsync(
        IReadOnlyList<(SearchCollectionContext Context, FeatureQuery Query, string ContentCrs)> preparedQueries,
        FeatureResultType resultType,
        bool includeCount,
        long initialLimit,
        long initialOffset,
        IFeatureRepository repository,
        Func<SearchCollectionContext, LayerDefinition, FeatureQuery, FeatureRecord, FeatureComponents, ValueTask<bool>> onFeature,
        CancellationToken cancellationToken)
    {
        long? numberMatchedTotal = includeCount ? 0L : null;
        long numberReturnedTotal = 0;
        var remainingLimit = initialLimit;
        var remainingOffset = initialOffset;
        var enforceLimit = remainingLimit != long.MaxValue;

        if (resultType == FeatureResultType.Hits)
        {
            foreach (var prepared in preparedQueries)
            {
                var service = prepared.Context.FeatureContext.Service;
                var layer = prepared.Context.FeatureContext.Layer;
                var query = prepared.Query;
                var matched = await repository.CountAsync(service.Id, layer.Id, query, cancellationToken).ConfigureAwait(false);
                numberMatchedTotal += matched;
            }

            return new SearchIterationResult(numberMatchedTotal, 0);
        }

        foreach (var prepared in preparedQueries)
        {
            var service = prepared.Context.FeatureContext.Service;
            var layer = prepared.Context.FeatureContext.Layer;
            var query = prepared.Query;

            if (includeCount)
            {
                var matched = await repository.CountAsync(service.Id, layer.Id, query, cancellationToken).ConfigureAwait(false);
                numberMatchedTotal += matched;

                long skip = Math.Min(remainingOffset, matched);
                remainingOffset -= skip;

                var available = matched - skip;
                if (available <= 0)
                {
                    continue;
                }

                var allowed = enforceLimit ? Math.Min(available, remainingLimit) : available;
                if (allowed <= 0)
                {
                    continue;
                }

                var adjustedQuery = query with
                {
                    Offset = (int)Math.Min(skip, int.MaxValue),
                    Limit = (int)Math.Min(allowed, int.MaxValue)
                };

                await foreach (var record in repository.QueryAsync(service.Id, layer.Id, adjustedQuery, cancellationToken).ConfigureAwait(false))
                {
                    var components = FeatureComponentBuilder.BuildComponents(layer, record, adjustedQuery);
                    var shouldContinue = await onFeature(prepared.Context, layer, adjustedQuery, record, components).ConfigureAwait(false);
                    numberReturnedTotal++;

                    if (enforceLimit)
                    {
                        remainingLimit--;
                        if (remainingLimit <= 0)
                        {
                            return new SearchIterationResult(numberMatchedTotal, numberReturnedTotal);
                        }
                    }

                    if (!shouldContinue)
                    {
                        return new SearchIterationResult(numberMatchedTotal, numberReturnedTotal);
                    }
                }

                continue;
            }

            var streamingQuery = query with
            {
                Offset = null,
                Limit = query.Limit
            };

            await foreach (var record in repository.QueryAsync(service.Id, layer.Id, streamingQuery, cancellationToken).ConfigureAwait(false))
            {
                if (remainingOffset > 0)
                {
                    remainingOffset--;
                    continue;
                }

                var components = FeatureComponentBuilder.BuildComponents(layer, record, streamingQuery);
                var shouldContinue = await onFeature(prepared.Context, layer, streamingQuery, record, components).ConfigureAwait(false);
                numberReturnedTotal++;

                if (enforceLimit)
                {
                    remainingLimit--;
                    if (remainingLimit <= 0)
                    {
                        return new SearchIterationResult(numberMatchedTotal, numberReturnedTotal);
                    }
                }

                if (!shouldContinue)
                {
                    return new SearchIterationResult(numberMatchedTotal, numberReturnedTotal);
                }
            }
        }

        return new SearchIterationResult(numberMatchedTotal, numberReturnedTotal);
    }
}
