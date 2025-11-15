// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Data;
using Honua.Server.Core.Export;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Query;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Raster.Export;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Ogc.Services;

/// <summary>
/// Service for handling OGC API Features query operations including search, parsing, and queryables.
/// Extracted from OgcSharedHandlers to enable dependency injection and testability.
/// </summary>
internal sealed class OgcFeaturesQueryHandler : IOgcFeaturesQueryHandler
{
    private readonly IOgcCollectionResolver collectionResolver;
    private readonly IOgcFeaturesGeoJsonHandler geoJsonHandler;
    private readonly FilterParsingCacheService filterCache;
    private readonly FilterParsingCacheOptions cacheOptions;
    private readonly ILogger<OgcFeaturesQueryHandler> logger;

    public OgcFeaturesQueryHandler(
        IOgcCollectionResolver collectionResolver,
        IOgcFeaturesGeoJsonHandler geoJsonHandler,
        FilterParsingCacheService filterCache,
        Microsoft.Extensions.Options.IOptions<FilterParsingCacheOptions> cacheOptions,
        ILogger<OgcFeaturesQueryHandler> logger)
    {
        this.collectionResolver = collectionResolver ?? throw new ArgumentNullException(nameof(collectionResolver));
        this.geoJsonHandler = geoJsonHandler ?? throw new ArgumentNullException(nameof(geoJsonHandler));
        this.filterCache = filterCache ?? throw new ArgumentNullException(nameof(filterCache));
        this.cacheOptions = cacheOptions?.Value ?? throw new ArgumentNullException(nameof(cacheOptions));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public (FeatureQuery Query, string ContentCrs, bool IncludeCount, IResult? Error) ParseItemsQuery(
        HttpRequest request,
        ServiceDefinition service,
        LayerDefinition layer,
        IQueryCollection? overrideQuery = null)
    {
        var allowedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "limit",
            "offset",
            "bbox",
            "bbox-crs",
            "datetime",
            "resultType",
            "properties",
            "crs",
            "count",
            "f",
            "filter",
            "filter-lang",
            "filter-crs",
            "ids",
            "sortby"
        };

        var queryCollection = overrideQuery ?? request.Query;

        if (overrideQuery is not null)
        {
            allowedKeys.Add("collections");
        }

        foreach (var key in queryCollection.Keys)
        {
            if (!allowedKeys.Contains(key))
            {
                return (default!, string.Empty, false, OgcSharedHandlers.CreateValidationProblem($"Unknown query parameter '{key}'.", key));
            }
        }

        // Parse limit using shared helper (handles clamping automatically)
        var serviceLimit = service.Ogc.ItemLimit;
        var layerLimit = layer.Query?.MaxRecordCount;
        var (limitValue, limitError) = QueryParameterHelper.ParseLimit(
            queryCollection["limit"].ToString(),
            serviceLimit,
            layerLimit,
            fallback: 10); // OGC API default page size is 10
        if (limitError is not null)
        {
            return (default!, string.Empty, false, OgcSharedHandlers.CreateValidationProblem(limitError, "limit"));
        }
        // Fallback should ensure non-null, but add safety check
        if (!limitValue.HasValue)
        {
            return (default!, string.Empty, false, OgcSharedHandlers.CreateValidationProblem("Limit parameter is required.", "limit"));
        }

        // Parse offset using shared helper
        var offsetRaw = queryCollection["offset"].ToString();
        var (offsetValue, offsetError) = QueryParameterHelper.ParseOffset(offsetRaw);
        if (offsetError is not null)
        {
            return (default!, string.Empty, false, OgcSharedHandlers.CreateValidationProblem(offsetError, "offset"));
        }

        var bboxParse = ParseBoundingBox(queryCollection["bbox"]);
        if (bboxParse.Error is not null)
        {
            return (default!, string.Empty, false, bboxParse.Error);
        }

        var timeParse = ParseTemporal(queryCollection["datetime"]);
        if (timeParse.Error is not null)
        {
            return (default!, string.Empty, false, timeParse.Error);
        }

        var resultTypeParse = ParseResultType(queryCollection["resultType"]);
        if (resultTypeParse.Error is not null)
        {
            return (default!, string.Empty, false, resultTypeParse.Error);
        }
        var resultType = resultTypeParse.Value;

        var (sortOrdersExplicit, sortError) = OgcSharedHandlers.ParseSortOrders(queryCollection["sortby"], layer);
        if (sortError is not null)
        {
            return (default!, string.Empty, false, sortError);
        }

        var supportedCrs = OgcSharedHandlers.ResolveSupportedCrs(service, layer);
        var defaultCrs = OgcSharedHandlers.DetermineDefaultCrs(service, supportedCrs);

        var (acceptCrs, acceptCrsError) = OgcSharedHandlers.ResolveAcceptCrs(request, supportedCrs);
        if (acceptCrsError is not null)
        {
            return (default!, string.Empty, false, acceptCrsError);
        }

        var filterLangRaw = queryCollection["filter-lang"].ToString();
        string? filterLangNormalized = null;
        if (filterLangRaw.HasValue())
        {
            filterLangNormalized = filterLangRaw.Trim().ToLowerInvariant();
            if (filterLangNormalized != "cql-text" &&
                filterLangNormalized != "cql2-json")
            {
                return (default!, string.Empty, false, OgcSharedHandlers.CreateValidationProblem($"filter-lang '{filterLangRaw}' is not supported. Supported values: cql-text, cql2-json.", "filter-lang"));
            }
        }

        var (normalizedFilterCrs, filterCrsError) = QueryParameterHelper.ParseCrs(
            queryCollection["filter-crs"].ToString(),
            supportedCrs,
            defaultCrs: null);
        if (filterCrsError is not null)
        {
            return (default!, string.Empty, false, OgcSharedHandlers.CreateValidationProblem(filterCrsError, "filter-crs"));
        }

        QueryFilter? combinedFilter = null;
        var filterValues = queryCollection["filter"];
        var rawFilter = filterValues.ToString();
        if (rawFilter.HasValue())
        {
            var treatAsJsonFilter = string.Equals(filterLangNormalized, "cql2-json", StringComparison.Ordinal) ||
                                    (filterLangNormalized is null && OgcSharedHandlers.LooksLikeJson(rawFilter));

            // Determine filter language for caching
            var effectiveFilterLanguage = treatAsJsonFilter ? "cql2-json" : "cql-text";
            if (filterLangNormalized is not null)
            {
                effectiveFilterLanguage = filterLangNormalized;
            }

            try
            {
                // Use cache if enabled, otherwise parse directly
                if (this.cacheOptions.Enabled)
                {
                    combinedFilter = this.filterCache.GetOrParse(
                        rawFilter,
                        effectiveFilterLanguage,
                        layer,
                        normalizedFilterCrs,
                        () =>
                        {
                            // Parse function - only called on cache miss
                            if (treatAsJsonFilter)
                            {
                                return Cql2JsonParser.Parse(rawFilter, layer, normalizedFilterCrs);
                            }
                            else
                            {
                                return CqlFilterParser.Parse(rawFilter, layer);
                            }
                        });
                }
                else
                {
                    // Cache disabled - parse directly
                    if (treatAsJsonFilter)
                    {
                        combinedFilter = Cql2JsonParser.Parse(rawFilter, layer, normalizedFilterCrs);
                    }
                    else
                    {
                        combinedFilter = CqlFilterParser.Parse(rawFilter, layer);
                    }
                }

                // Update filter language for consistency
                if (treatAsJsonFilter)
                {
                    filterLangNormalized ??= "cql2-json";
                }
            }
            catch (Exception ex)
            {
                return (default!, string.Empty, false, OgcSharedHandlers.CreateValidationProblem($"Invalid filter expression. {ex.Message}", "filter"));
            }
        }
        else if (string.Equals(filterLangNormalized, "cql2-json", StringComparison.Ordinal))
        {
            return (default!, string.Empty, false, OgcSharedHandlers.CreateValidationProblem("filter parameter is required when filter-lang=cql2-json.", "filter"));
        }

        var rawIds = ParseList(queryCollection["ids"]);
        if (rawIds.Count > 0)
        {
            var (idsFilter, idsError) = BuildIdsFilter(layer, rawIds);
            if (idsError is not null)
            {
                return (default!, string.Empty, false, idsError);
            }

            combinedFilter = CombineFilters(combinedFilter, idsFilter);
        }

        var requestedCrsRaw = queryCollection["crs"].ToString();
        if (requestedCrsRaw.IsNullOrWhiteSpace() && acceptCrs.HasValue())
        {
            requestedCrsRaw = acceptCrs;
        }

        var (servedCrsCandidate, servedCrsError) = QueryParameterHelper.ParseCrs(
            requestedCrsRaw,
            supportedCrs,
            defaultCrs);
        if (servedCrsError is not null)
        {
            return (default!, string.Empty, false, OgcSharedHandlers.CreateValidationProblem(servedCrsError, "crs"));
        }

        var servedCrs = servedCrsCandidate ?? defaultCrs;

        var storageCrs = OgcSharedHandlers.DetermineStorageCrs(layer);
        var (bboxCrsCandidate, bboxCrsError) = QueryParameterHelper.ParseCrs(
            queryCollection["bbox-crs"].ToString(),
            supportedCrs,
            storageCrs);
        if (bboxCrsError is not null)
        {
            return (default!, string.Empty, false, OgcSharedHandlers.CreateValidationProblem(bboxCrsError, "bbox-crs"));
        }

        var bboxCrs = bboxCrsCandidate ?? storageCrs;

        // QueryParameterHelper already clamped limit to service/layer max
        var effectiveLimit = limitValue.Value; // Already validated as non-null above
        var effectiveOffset = offsetValue ?? 0;
        var rawProperties = ParseList(queryCollection["properties"]);
        IReadOnlyList<string>? propertyNames = rawProperties.Count == 0 ? null : rawProperties;

        var bbox = bboxParse.Value;
        if (bbox is not null)
        {
            bbox = bbox with { Crs = bboxCrs };
        }

        IReadOnlyList<FeatureSortOrder>? sortOrders = sortOrdersExplicit;
        if (sortOrders is null && layer.IdField.HasValue())
        {
            sortOrders = new[] { new FeatureSortOrder(layer.IdField, FeatureSortDirection.Ascending) };
        }

        var query = new FeatureQuery(
            Limit: effectiveLimit,
            Offset: effectiveOffset,
            Bbox: bbox,
            Temporal: timeParse.Value,
            ResultType: resultType,
            PropertyNames: propertyNames,
            SortOrders: sortOrders,
            Filter: combinedFilter,
            Crs: servedCrs);

        var (includeCount, countError) = QueryParameterHelper.ParseBoolean(
            queryCollection["count"].ToString(),
            defaultValue: resultType == FeatureResultType.Hits);
        // Note: Boolean parsing errors are not critical, just use default
        if (resultType == FeatureResultType.Hits)
        {
            includeCount = true;
        }

        return (query, servedCrs, includeCount, null);
    }

    /// <inheritdoc />
    public async Task<IResult> ExecuteSearchAsync(
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
        IOgcFeaturesAttachmentHandler attachmentHandler,
        CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Executing OGC search across {CollectionCount} collections: {Collections}",
            collections.Count, string.Join(", ", collections));
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
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
            return OgcSharedHandlers.CreateValidationProblem("At least one collection must be specified.", "collections");
        }

        var resolutions = new List<SearchCollectionContext>(collections.Count);
        foreach (var collectionId in collections)
        {
            var resolution = await this.collectionResolver.ResolveCollectionAsync(collectionId, resolver, cancellationToken).ConfigureAwait(false);
            if (resolution.IsFailure)
            {
                return this.collectionResolver.MapCollectionResolutionError(resolution.Error!, collectionId);
            }

            resolutions.Add(new SearchCollectionContext(collectionId, resolution.Value!));
        }

        var (format, contentType, formatError) = OgcSharedHandlers.ResolveResponseFormat(request, queryParameters);
        if (formatError is not null)
        {
            return formatError;
        }

        var supportsAggregation = format is OgcSharedHandlers.OgcResponseFormat.GeoJson or OgcSharedHandlers.OgcResponseFormat.Html;
        if (!supportsAggregation)
        {
            if (collections.Count != 1)
            {
                return OgcSharedHandlers.CreateValidationProblem("The requested format requires a single collection.", "collections");
            }

            var sanitized = RemoveCollectionsParameter(queryParameters);
            return await OgcFeaturesHandlers.ExecuteCollectionItemsAsync(
                collections[0],
                new OgcFeaturesRequestContext
                {
                    Request = request,
                    QueryOverrides = sanitized
                },
                resolver,
                repository,
                metadataRegistry,
                new OgcFeatureExportServices
                {
                    GeoPackage = geoPackageExporter,
                    Shapefile = shapefileExporter,
                    FlatGeobuf = flatGeobufExporter,
                    GeoArrow = geoArrowExporter,
                    Csv = csvExporter
                },
                new OgcFeatureAttachmentServices
                {
                    Orchestrator = attachmentOrchestrator,
                    Handler = attachmentHandler
                },
                new OgcFeatureEnrichmentServices
                {
                    Elevation = new Core.Elevation.DefaultElevationService()
                },
                new OgcFeatureObservabilityServices
                {
                    Metrics = apiMetrics,
                    CacheHeaders = cacheHeaderService,
                    Logger = this.logger
                },
                cancellationToken).ConfigureAwait(false);
        }

        var isHtml = format == OgcSharedHandlers.OgcResponseFormat.Html;
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
                return OgcSharedHandlers.CreateValidationProblem("Mixed resultType values are not supported across collections.", "resultType");
            }

            if (globalContentCrs is null)
            {
                globalContentCrs = contentCrs;
            }
            else if (!string.Equals(globalContentCrs, contentCrs, StringComparison.OrdinalIgnoreCase))
            {
                return OgcSharedHandlers.CreateValidationProblem("All collections in a search must share a common response CRS.", "crs");
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
            var htmlEntries = new List<OgcSharedHandlers.HtmlFeatureEntry>();

            var iterationResult = await EnumerateSearchAsync(
                preparedQueries,
                resultType,
                includeCount,
                initialLimit,
                initialOffset,
                repository,
                (context, layer, query, record, components) =>
                {
                    htmlEntries.Add(new OgcSharedHandlers.HtmlFeatureEntry(context.CollectionId, layer.Title ?? context.CollectionId, components));
                    return ValueTask.FromResult(true);
                },
                cancellationToken).ConfigureAwait(false);

            var links = OgcSharedHandlers.BuildSearchLinks(request, collections, baseQuery, iterationResult.NumberMatched, contentType);
            var htmlDescription = collections.Count == 1
                ? $"Collection: {collections[0]}"
                : $"Collections: {string.Join(", ", collections)}";

            var html = OgcSharedHandlers.RenderFeatureCollectionHtml(
                "Search results",
                htmlDescription,
                htmlEntries,
                iterationResult.NumberMatched,
                iterationResult.NumberReturned,
                globalContentCrs,
                links,
                resultType == FeatureResultType.Hits);

            var htmlResult = Results.Content(html, OgcSharedHandlers.HtmlContentType);
            return OgcSharedHandlers.WithContentCrsHeader(htmlResult, globalContentCrs);
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

        stopwatch.Stop();
        this.logger.LogInformation("OGC search completed for {Collections} in {ElapsedMs}ms",
            string.Join(", ", collections), stopwatch.ElapsedMilliseconds);

        return OgcSharedHandlers.WithContentCrsHeader(geoJsonResult, globalContentCrs);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            this.logger.LogError(ex, "OGC search failed for collections {Collections} after {ElapsedMs}ms",
                string.Join(", ", collections), stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    /// <inheritdoc />
    public object BuildQueryablesSchema(LayerDefinition layer)
    {
        var properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var required = new List<string>();

        var fields = FieldMetadataResolver.ResolveFields(layer, includeGeometry: false, includeIdField: true);
        foreach (var field in fields)
        {
            var schema = CreateQueryablesPropertySchema(field);
            if (schema is null)
            {
                continue;
            }

            properties[field.Name] = schema;
            if (!field.Nullable)
            {
                required.Add(field.Name);
            }
        }

        if (!properties.ContainsKey(layer.GeometryField))
        {
            properties[layer.GeometryField] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["content"] = new Dictionary<string, object>
                {
                    ["application/geo+json"] = new { }
                }
            };
        }

        var result = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["title"] = layer.Title.IsNullOrWhiteSpace() ? layer.Id : layer.Title,
            ["properties"] = properties,
            ["additionalProperties"] = false
        };

        if (required.Count > 0)
        {
            result["required"] = required;
        }

        return result;
    }

    /// <inheritdoc />
    public object? ConvertExtent(LayerExtentDefinition? extent)
    {
        if (extent is null)
        {
            return null;
        }

        object? spatial = null;
        if (extent.Bbox.Count > 0 || extent.Crs.HasValue())
        {
            spatial = new
            {
                bbox = extent.Bbox,
                crs = extent.Crs.IsNullOrWhiteSpace()
                    ? CrsHelper.DefaultCrsIdentifier
                    : CrsHelper.NormalizeIdentifier(extent.Crs)
            };
        }

        var hasIntervals = extent.Temporal.Count > 0;
        var intervals = hasIntervals
            ? extent.Temporal
                .Select(t => new[] { t.Start?.ToString("O"), t.End?.ToString("O") })
                .ToArray()
            : Array.Empty<string?[]>();

        object? temporal = null;
        if (hasIntervals || extent.TemporalReferenceSystem.HasValue())
        {
            temporal = new
            {
                interval = intervals,
                trs = extent.TemporalReferenceSystem.IsNullOrWhiteSpace()
                    ? "http://www.opengis.net/def/uom/ISO-8601/0/Gregorian"
                    : extent.TemporalReferenceSystem
            };
        }

        if (spatial is null && temporal is null)
        {
            return null;
        }

        return new
        {
            spatial,
            temporal
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<string> BuildOrderedStyleIds(LayerDefinition layer)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<string>();

        if (layer.DefaultStyleId.HasValue() && seen.Add(layer.DefaultStyleId))
        {
            results.Add(layer.DefaultStyleId);
        }

        foreach (var styleId in layer.StyleIds)
        {
            if (styleId.HasValue() && seen.Add(styleId))
            {
                results.Add(styleId);
            }
        }

        return results.Count == 0 ? Array.Empty<string>() : results;
    }

    #region Private Helper Methods

    private static QueryFilter? CombineFilters(QueryFilter? first, QueryFilter? second)
    {
        if (first?.Expression is null)
        {
            return second;
        }

        if (second?.Expression is null)
        {
            return first;
        }

        var combined = new QueryBinaryExpression(first.Expression, QueryBinaryOperator.And, second.Expression);
        return new QueryFilter(combined);
    }

    private static (BoundingBox? Value, IResult? Error) ParseBoundingBox(string? raw)
    {
        // Note: bbox-crs is parsed separately in ParseItemsQuery and set on the BoundingBox later
        var (bbox, error) = QueryParameterHelper.ParseBoundingBox(raw, crs: null);
        if (error is not null)
        {
            return (null, OgcSharedHandlers.CreateValidationProblem(error, "bbox"));
        }

        return (bbox, null);
    }

    private static (TemporalInterval? Value, IResult? Error) ParseTemporal(string? raw)
    {
        var (interval, error) = QueryParameterHelper.ParseTemporalRange(raw);
        if (error is not null)
        {
            return (null, OgcSharedHandlers.CreateValidationProblem(error, "datetime"));
        }

        return (interval, null);
    }

    private static (FeatureResultType Value, IResult? Error) ParseResultType(string? raw)
    {
        var (resultType, error) = QueryParameterHelper.ParseResultType(raw, FeatureResultType.Results);
        if (error is not null)
        {
            return (FeatureResultType.Results, OgcSharedHandlers.CreateValidationProblem(error, "resultType"));
        }

        return (resultType, null);
    }

    private static IReadOnlyList<string> ParseList(string? raw)
    {
        if (raw.IsNullOrWhiteSpace())
        {
            return Array.Empty<string>();
        }

        var values = QueryParsingHelpers.ParseCsv(raw);
        return values.Count == 0 ? Array.Empty<string>() : values;
    }

    private static (QueryFilter? Filter, IResult? Error) BuildIdsFilter(LayerDefinition layer, IReadOnlyList<string> ids)
    {
        if (ids.Count == 0)
        {
            return (null, null);
        }

        // Limit IDs to prevent unbounded OR expressions that hammer the database
        const int MaxIds = 1000;
        if (ids.Count > MaxIds)
        {
            return (null, OgcSharedHandlers.CreateValidationProblem($"ids parameter exceeds maximum limit of {MaxIds} identifiers.", "ids"));
        }

        QueryExpression? expression = null;
        (string FieldName, string? FieldType) resolved;

        try
        {
            resolved = CqlFilterParserUtils.ResolveField(layer, layer.IdField);
        }
        catch (Exception ex)
        {
            return (null, OgcSharedHandlers.CreateValidationProblem(ex.Message, "ids"));
        }

        foreach (var rawId in ids)
        {
            if (rawId.IsNullOrWhiteSpace())
            {
                continue;
            }

            var typedValue = CqlFilterParserUtils.ConvertToFieldValue(resolved.FieldType, rawId);
            var comparison = new QueryBinaryExpression(
                new QueryFieldReference(resolved.FieldName),
                QueryBinaryOperator.Equal,
                new QueryConstant(typedValue));

            expression = expression is null
                ? comparison
                : new QueryBinaryExpression(expression, QueryBinaryOperator.Or, comparison);
        }

        if (expression is null)
        {
            return (null, OgcSharedHandlers.CreateValidationProblem("ids parameter must include at least one non-empty value.", "ids"));
        }

        return (new QueryFilter(expression), null);
    }

    private static object? CreateQueryablesPropertySchema(FieldDefinition field)
    {
        var kind = (field.DataType ?? field.StorageType ?? "string").Trim().ToLowerInvariant();

        if (string.Equals(kind, "geometry", StringComparison.OrdinalIgnoreCase))
        {
            return new Dictionary<string, object>
            {
                ["type"] = "object",
                ["content"] = new Dictionary<string, object>
                {
                    ["application/geo+json"] = new { }
                }
            };
        }

        var schema = new Dictionary<string, object>();

        switch (kind)
        {
            case "int":
            case "integer":
            case "int16":
            case "int32":
            case "short":
            case "smallint":
                schema["type"] = "integer";
                break;
            case "int64":
            case "long":
            case "bigint":
                schema["type"] = "integer";
                schema["format"] = "int64";
                break;
            case "double":
            case "float":
            case "single":
            case "real":
            case "decimal":
            case "numeric":
                schema["type"] = "number";
                break;
            case "date":
            case "datetime":
            case "datetimeoffset":
            case "time":
                schema["type"] = "string";
                schema["format"] = "date-time";
                break;
            case "bool":
            case "boolean":
                schema["type"] = "boolean";
                break;
            case "uuid":
            case "guid":
            case "uniqueidentifier":
                schema["type"] = "string";
                schema["format"] = "uuid";
                break;
            default:
                schema["type"] = "string";
                break;
        }

        if (field.MaxLength.HasValue && field.MaxLength.Value > 0 && schema.TryGetValue("type", out var typeValue) &&
            string.Equals(typeValue as string, "string", StringComparison.OrdinalIgnoreCase))
        {
            schema["maxLength"] = field.MaxLength.Value;
        }

        return schema;
    }

    private async Task WriteGeoJsonSearchResponseAsync(
        System.IO.Stream outputStream,
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
                var feature = this.geoJsonHandler.ToFeature(request, context.CollectionId, layer, record, query, components);
                JsonSerializer.Serialize(writer, feature, OgcSharedHandlers.GeoJsonSerializerOptions);

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

        var links = OgcSharedHandlers.BuildSearchLinks(request, collections, baseQuery, iterationResult.NumberMatched, contentType);
        writer.WritePropertyName("links");
        JsonSerializer.Serialize(writer, links, OgcSharedHandlers.GeoJsonSerializerOptions);

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

    private sealed record SearchIterationResult(long? NumberMatched, long NumberReturned);

    private sealed record SearchCollectionContext(string CollectionId, FeatureContext FeatureContext);

    #endregion
}
