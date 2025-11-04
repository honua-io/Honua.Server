// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Stac;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Middleware;
using Honua.Server.Host.Stac.Services;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Stac;

/// <summary>
/// API controller for STAC (SpatioTemporal Asset Catalog) search operations.
/// </summary>
/// <remarks>
/// Provides endpoints for searching across STAC collections using spatial, temporal, and attribute filters.
/// Supports both GET and POST search operations as defined in the STAC API specification.
/// </remarks>
[ApiController]
[Authorize(Policy = "RequireViewer")]
[Route("stac/search")]
public sealed class StacSearchController : ControllerBase
{
    private readonly IStacCatalogStore _store;
    private readonly StacControllerHelper _helper;
    private readonly ILogger<StacSearchController> _logger;
    private readonly StacMetrics _metrics;

    public StacSearchController(IStacCatalogStore store, StacControllerHelper helper, ILogger<StacSearchController> logger, StacMetrics metrics)
    {
        _store = Guard.NotNull(store);
        _helper = Guard.NotNull(helper);
        _logger = Guard.NotNull(logger);
        _metrics = Guard.NotNull(metrics);
    }

    /// <summary>
    /// Searches for STAC items across collections using GET method with query parameters.
    /// </summary>
    /// <param name="collections">Comma-separated list of collection IDs to search (optional, searches all if not specified).</param>
    /// <param name="ids">Comma-separated list of item IDs to filter (optional).</param>
    /// <param name="bbox">Bounding box as comma-separated values: minX,minY,maxX,maxY (optional).</param>
    /// <param name="datetime">Date-time filter in RFC 3339 format or interval (e.g., "2020-01-01T00:00:00Z/2020-12-31T23:59:59Z") (optional).</param>
    /// <param name="limit">Maximum number of results to return (default: 10, max: 1000).</param>
    /// <param name="token">Pagination token from a previous search response.</param>
    /// <param name="sortby">Comma-separated sort fields with optional +/- direction prefix (e.g., "-datetime,+id").</param>
    /// <param name="fields">Comma-separated list of fields to include (or exclude with '-' prefix) in the response (e.g., "id,geometry,properties.datetime" or "-assets,-links").</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A GeoJSON FeatureCollection containing matching STAC items.</returns>
    /// <response code="200">Returns the search results as a GeoJSON FeatureCollection</response>
    /// <response code="400">Invalid bbox or datetime parameter</response>
    /// <response code="404">STAC is not enabled or no matching collections found</response>
    [AllowAnonymous]
    [HttpGet]
    [Produces("application/geo+json")]
    [ProducesResponseType(typeof(StacItemCollectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [OutputCache(PolicyName = OutputCachePolicies.StacSearch)]
    public async Task<ActionResult<StacItemCollectionResponse>> GetSearchAsync(
        [FromQuery(Name = "collections")] string? collections,
        [FromQuery(Name = "ids")] string? ids,
        [FromQuery(Name = "bbox")] string? bbox,
        [FromQuery(Name = "datetime")] string? datetime,
        [FromQuery(Name = "limit")] int? limit,
        [FromQuery(Name = "token")] string? token,
        [FromQuery(Name = "sortby")] string? sortby,
        [FromQuery(Name = "fields")] string? fields,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("STAC search GET request received with collections: {Collections}, bbox: {Bbox}, datetime: {Datetime}, limit: {Limit}",
            collections ?? "all", bbox ?? "none", datetime ?? "none", limit);

        // Validate collections and ids counts
        var collectionsList = Split(collections);
        var idsList = Split(ids);

        var (isValid, validationError) = _helper.ValidateCollectionsAndIds(collectionsList, idsList, StacConstants.MaxCollectionsCount, StacConstants.MaxIdsCount);
        if (!isValid)
        {
            _logger.LogRequestRejected("STAC search GET request", "collections or ids count exceeds maximum");
            return validationError!;
        }

        var (parsedBbox, bboxError) = ParseBbox(bbox);
        if (bboxError is not null)
        {
            _logger.LogValidationFailure("bbox", "Invalid format", bbox);
            return bboxError;
        }

        var (datetimeRange, datetimeError) = ParseDatetimeRange(datetime);
        if (datetimeError is not null)
        {
            _logger.LogValidationFailure("datetime", "Invalid format", datetime);
            return datetimeError;
        }

        var (sortFields, sortError) = StacSortParser.ParseGetSortBy(sortby);
        if (sortError is not null)
        {
            _logger.LogValidationFailure("sortby", sortError, sortby);
            return BadRequest(_helper.CreateInvalidParameterProblem("sortby", sortError));
        }

        var fieldsSpec = FieldsParser.ParseGetFields(fields);

        JsonObject? filterObject = null;
        if (Request.Query.TryGetValue("filter", out var filterValues))
        {
            var filterRaw = filterValues.ToString();
            if (!filterRaw.IsNullOrWhiteSpace())
            {
                try
                {
                    var parsed = JsonNode.Parse(filterRaw);
                    if (parsed is JsonObject jsonObject)
                    {
                        filterObject = jsonObject;
                    }
                    else
                    {
                        _logger.LogValidationFailure("filter", "Filter parameter must be a JSON object", filterRaw);
                        return BadRequest(_helper.CreateInvalidParameterProblem("filter", "Filter parameter must be a JSON object."));
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogValidationFailure("filter", "Invalid JSON", filterRaw);
                    return BadRequest(_helper.CreateInvalidParameterProblem("filter", $"Invalid JSON payload: {ex.Message}"));
                }
            }
        }

        var filterLangValue = Request.Query.TryGetValue("filter-lang", out var filterLangValues)
            ? filterLangValues.ToString()
            : null;

        JsonNode? intersectsNode = null;
        if (Request.Query.TryGetValue("intersects", out var intersectsValues))
        {
            var intersectsRaw = intersectsValues.ToString();
            if (!intersectsRaw.IsNullOrWhiteSpace())
            {
                try
                {
                    intersectsNode = JsonNode.Parse(intersectsRaw);
                }
                catch (JsonException ex)
                {
                    _logger.LogValidationFailure("intersects", "Invalid JSON", intersectsRaw);
                    return BadRequest(_helper.CreateInvalidParameterProblem("intersects", $"Invalid GeoJSON geometry: {ex.Message}"));
                }
            }
        }

        var (parsedGeometry, intersectsError) = ParseIntersectsGeometry(intersectsNode, "STAC GET search");
        if (intersectsError is not null)
        {
            return intersectsError;
        }

        var request = new StacSearchRequest
        {
            Collections = collectionsList,
            Ids = idsList,
            Bbox = parsedBbox,
            Datetime = datetime,
            Limit = limit,
            Token = token,
            SortBy = sortFields != null ? ConvertToSortFieldDtos(sortFields) : null,
            Filter = filterObject,
            FilterLang = filterLangValue,
            Intersects = intersectsNode,
            Fields = fieldsSpec.IsEmpty ? null : new StacSearchFieldsRequest
            {
                Include = fieldsSpec.Include?.ToList(),
                Exclude = fieldsSpec.Exclude?.ToList()
            }
        };

        return await SearchInternalAsync(request, datetimeRange, sortFields, fieldsSpec, parsedGeometry, cancellationToken);
    }

    /// <summary>
    /// Searches for STAC items across collections using POST method with JSON body.
    /// </summary>
    /// <param name="request">The search request containing filter criteria (collections, ids, bbox, datetime, limit).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A GeoJSON FeatureCollection containing matching STAC items.</returns>
    /// <response code="200">Returns the search results as a GeoJSON FeatureCollection</response>
    /// <response code="400">Invalid datetime parameter</response>
    /// <response code="404">STAC is not enabled or no matching collections found</response>
    /// <remarks>
    /// POST method allows for more complex search criteria and larger payloads compared to GET.
    /// Supports the same filters as GET: collections, ids, bbox, datetime, and limit.
    /// SECURITY: POST requests are cached with shorter TTL (30 seconds) varying by user and query params.
    /// </remarks>
    [AllowAnonymous]
    [HttpPost]
    [Produces("application/geo+json")]
    [ProducesResponseType(typeof(StacItemCollectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [OutputCache(PolicyName = OutputCachePolicies.StacSearchPost)]
    public async Task<ActionResult<StacItemCollectionResponse>> PostSearchAsync(
        [FromBody] StacSearchRequest request,
        CancellationToken cancellationToken)
    {
        // Validate model state before processing
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("STAC search POST request validation failed: {ValidationErrors}",
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return BadRequest(ModelState);
        }

        // Additional validation for collections and ids count
        var (isValid, validationError) = _helper.ValidateCollectionsAndIds(request.Collections, request.Ids, StacConstants.MaxCollectionsCount, StacConstants.MaxIdsCount);
        if (!isValid)
        {
            _logger.LogWarning("STAC search POST request validation failed: collections or ids count exceeds maximum");
            return validationError!;
        }

        _logger.LogInformation("STAC search POST request received with collections: {Collections}, bbox: {HasBbox}, datetime: {Datetime}, limit: {Limit}",
            request.Collections is not null ? string.Join(",", request.Collections) : "all",
            request.Bbox is not null,
            request.Datetime ?? "none",
            request.Limit);

        var (datetimeRange, datetimeError) = ParseDatetimeRange(request.Datetime);
        if (datetimeError is not null)
        {
            _logger.LogValidationFailure("datetime", "Invalid format in POST request", request.Datetime);
            return datetimeError;
        }

        // Validate bbox and intersects mutual exclusivity
        if (request.Bbox is not null && request.Intersects is not null)
        {
            _logger.LogWarning("STAC search POST request contains both bbox and intersects");
            return BadRequest(_helper.CreateBadRequestProblem("Invalid spatial parameters", "Cannot specify both 'bbox' and 'intersects' parameters. Use only one spatial filter."));
        }

        // Parse and validate intersects geometry if provided
        var (parsedGeometry, geometryError) = ParseIntersectsGeometry(request.Intersects, "STAC POST search");
        if (geometryError is not null)
        {
            return geometryError;
        }

        // Validate and convert sort fields from POST body
        IReadOnlyList<StacSortField>? sortFields = null;
        if (request.SortBy is not null && request.SortBy.Count > 0)
        {
            var (convertedFields, conversionError) = ConvertPostSortFields(request.SortBy);
            if (conversionError is not null)
            {
                _logger.LogWarning("Invalid sortby parameter in STAC POST search: {Error}", conversionError);
                return BadRequest(_helper.CreateInvalidParameterProblem("sortby", conversionError));
            }
            sortFields = convertedFields;
        }

        // Parse fields from POST body
        var fieldsSpec = request.Fields is not null
            ? FieldsParser.ParsePostFields(request.Fields.Include, request.Fields.Exclude)
            : new FieldsSpecification();

        return await SearchInternalAsync(request, datetimeRange, sortFields, fieldsSpec, parsedGeometry, cancellationToken);
    }

    private async Task<ActionResult<StacItemCollectionResponse>> SearchInternalAsync(
        StacSearchRequest request,
        (DateTimeOffset? Start, DateTimeOffset? End) datetimeRange,
        IReadOnlyList<StacSortField>? sortFields,
        FieldsSpecification fieldsSpec,
        ParsedGeometry? parsedGeometry,
        CancellationToken cancellationToken)
    {
        var stacEnabledError = _helper.EnsureStacEnabledOrNotFound();
        if (stacEnabledError is not null)
        {
            _logger.LogFeatureDisabled("STAC");
            return stacEnabledError;
        }

        var collectionCount = request.Collections?.Count ?? 0;
        var hasBbox = request.Bbox != null;
        var hasDatetime = !string.IsNullOrWhiteSpace(request.Datetime);

        return await OperationInstrumentation.Create<ActionResult<StacItemCollectionResponse>>("STAC Search")
            .WithActivitySource(HonuaTelemetry.Stac)
            .WithLogger(_logger)
            .WithLogLevels(LogLevel.Information, LogLevel.Error)
            .WithTag("stac.operation", "Search")
            .WithTag("stac.collections", request.Collections is not null ? string.Join(",", request.Collections) : "all")
            .WithTag("stac.collection_count", collectionCount)
            .WithTag("stac.has_bbox", hasBbox)
            .WithTag("stac.has_datetime", hasDatetime)
            .WithTag("stac.limit", NormalizeLimit(request.Limit))
            .ExecuteAsync(async activity =>
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    await _store.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

                    var baseUri = _helper.BuildBaseUri(Request);

                    // Optimize collection fetching: only fetch requested collections if specified
                    IReadOnlyList<StacCollectionRecord> requestedCollections;
                    if (request.Collections is not null && request.Collections.Count > 0)
                    {
                        // Use batch fetching to avoid N+1 query problem
                        // This fetches all requested collections in a single database query
                        requestedCollections = await _store.GetCollectionsAsync(request.Collections, cancellationToken).ConfigureAwait(false);

                        if (requestedCollections.Count == 0)
                        {
                            _logger.LogWarning("STAC search: no matching collections found for requested collections: {RequestedCollections}",
                                string.Join(",", request.Collections));
                            return NotFound();
                        }

                        // Log if some collections were not found
                        if (requestedCollections.Count < request.Collections.Count)
                        {
                            var foundIds = requestedCollections.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
                            var missingIds = request.Collections.Where(id => !foundIds.Contains(id)).ToList();
                            _logger.LogDebug("STAC search: {MissingCount} collections not found: {MissingCollections}",
                                missingIds.Count, string.Join(",", missingIds));
                        }
                    }
                    else
                    {
                        // No specific collections requested - search all collections
                        var allCollections = await _store.ListCollectionsAsync(cancellationToken).ConfigureAwait(false);

                        if (allCollections.Count == 0)
                        {
                            _logger.LogInformation("STAC search completed: no collections available, returning empty result");
                            activity?.SetTag("stac.result_count", 0);

                            // Record metrics for empty search
                            _metrics.RecordSearch(sw.Elapsed.TotalMilliseconds, 0, collectionCount, hasBbox, hasDatetime);

                            return Ok(StacApiMapper.BuildSearchCollection(Array.Empty<StacItemRecord>(), baseUri, matched: 0, nextToken: null, limit: null, fieldsSpec));
                        }

                        requestedCollections = allCollections
                            .OrderBy(c => c.Id, StringComparer.Ordinal)
                            .ToList();
                    }

                    var limit = NormalizeLimit(request.Limit);
                    var (start, end) = datetimeRange;

                    _logger.LogDebug("STAC search executing: collections={Collections}, limit={Limit}, hasBbox={HasBbox}, hasDatetime={HasDatetime}, hasIds={HasIds}",
                        string.Join(",", requestedCollections.Select(c => c.Id)),
                        limit,
                        request.Bbox is not null,
                        !string.IsNullOrWhiteSpace(request.Datetime),
                        request.Ids is not null);

                    activity?.SetTag("stac.datetime", request.Datetime);
                    if (request.Ids is not null)
                    {
                        activity?.SetTag("stac.has_ids", true);
                    }

                    var parameters = new StacSearchParameters
                    {
                        Collections = requestedCollections.Select(c => c.Id).ToList(),
                        Ids = request.Ids,
                        Bbox = request.Bbox,
                        Intersects = parsedGeometry,
                        Start = start,
                        End = end,
                        Limit = limit,
                        Token = request.Token,
                        SortBy = sortFields,
                        Filter = request.Filter?.ToJsonString(),
                        FilterLang = request.FilterLang
                    };

                    var result = await _store.SearchAsync(parameters, cancellationToken).ConfigureAwait(false);
                    activity?.SetTag("stac.result_count", result.Items.Count);
                    activity?.SetTag("stac.matched", result.Matched);

                    _logger.LogInformation("STAC search completed successfully: returned {ReturnedCount} items, matched {MatchedCount} total",
                        result.Items.Count, result.Matched);

                    // Record search metrics
                    _metrics.RecordSearch(sw.Elapsed.TotalMilliseconds, result.Items.Count, collectionCount, hasBbox, hasDatetime);

                    var response = StacApiMapper.BuildSearchCollection(result.Items, baseUri, result.Matched, result.NextToken, limit, fieldsSpec);
                    return Ok(response);
                }
                catch (Exception ex)
                {
                    _logger.LogOperationFailure(ex, "STAC search");

                    // Record failed search metrics
                    _metrics.RecordSearch(sw.Elapsed.TotalMilliseconds, 0, collectionCount, hasBbox, hasDatetime);

                    throw;
                }
            });
    }



    private ((DateTimeOffset? Start, DateTimeOffset? End) Range, ActionResult? Error) ParseDatetimeRange(string? value)
    {
        var (temporalRange, error) = QueryParsingHelpers.ParseTemporalRange(value, "datetime");
        if (error is not null)
        {
            return ((null, null), BadRequest(_helper.CreateInvalidParameterProblem("datetime", QueryParsingHelpers.ExtractProblemMessage(error, "Invalid datetime format."))));
        }

        if (temporalRange is null)
        {
            return ((null, null), null);
        }

        return ((temporalRange.Value.Start, temporalRange.Value.End), null);
    }


    private static IReadOnlyList<string>? Split(string? value)
    {
        var tokens = QueryParsingHelpers.ParseCsv(value);
        return tokens.Count == 0 ? null : tokens;
    }

    private (double[]? Bbox, ActionResult? Error) ParseBbox(string? value)
    {
        var (bbox, error) = QueryParsingHelpers.ParseBoundingBox(value, "bbox", allowAltitude: true);
        if (error is not null)
        {
            return (null, BadRequest(_helper.CreateInvalidParameterProblem("bbox", QueryParsingHelpers.ExtractProblemMessage(error, "Invalid bounding box format."))));
        }

        return (bbox, null);
    }

    private static int NormalizeLimit(int? limit)
    {
        const int defaultLimit = 10;
        const int maxLimit = 1000;
        if (!limit.HasValue || limit.Value <= 0)
        {
            return defaultLimit;
        }

        return Math.Min(limit.Value, maxLimit);
    }

    private static (IReadOnlyList<StacSortField>? SortFields, string? Error) ConvertPostSortFields(IReadOnlyList<StacSortFieldDto> dtos)
    {
        var validationError = StacSortParser.ValidatePostSortBy(ConvertDtosToSortFields(dtos));
        if (validationError != null)
        {
            return (null, validationError);
        }

        return (ConvertDtosToSortFields(dtos), null);
    }

    private static IReadOnlyList<StacSortField> ConvertDtosToSortFields(IReadOnlyList<StacSortFieldDto> dtos)
    {
        var fields = new List<StacSortField>(dtos.Count);
        foreach (var dto in dtos)
        {
            var direction = dto.Direction.EqualsIgnoreCase("desc")
                ? StacSortDirection.Descending
                : StacSortDirection.Ascending;

            fields.Add(new StacSortField
            {
                Field = dto.Field,
                Direction = direction
            });
        }
        return fields;
    }

    private static IReadOnlyList<StacSortFieldDto> ConvertToSortFieldDtos(IReadOnlyList<StacSortField> fields)
    {
        var dtos = new List<StacSortFieldDto>(fields.Count);
        foreach (var field in fields)
        {
            dtos.Add(new StacSortFieldDto
            {
                Field = field.Field,
                Direction = field.Direction == StacSortDirection.Descending ? "desc" : "asc"
            });
        }
        return dtos;
    }

    /// <summary>
    /// Parses and validates an intersects geometry from GeoJSON.
    /// Returns the parsed geometry or an error response if parsing fails.
    /// </summary>
    private (ParsedGeometry? Geometry, ActionResult? Error) ParseIntersectsGeometry(JsonNode? intersects, string context)
    {
        if (intersects is null)
        {
            return (null, null);
        }

        try
        {
            var parsedGeometry = GeometryParser.Parse(intersects);
            _logger.LogInformation("Parsed intersects geometry in {Context}: type={GeometryType}, vertices={VertexCount}",
                context, parsedGeometry.Type, parsedGeometry.VertexCount);
            return (parsedGeometry, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid intersects geometry in {Context}", context);
            return (null, BadRequest(_helper.CreateInvalidParameterProblem("intersects", $"Invalid GeoJSON geometry: {ex.Message}")));
        }
    }
}

public sealed record StacSearchRequest
{
    [JsonPropertyName("collections")]
    [MaxLength(100)]
    public IReadOnlyList<string>? Collections { get; init; }

    [JsonPropertyName("ids")]
    [MaxLength(100)]
    public IReadOnlyList<string>? Ids { get; init; }

    [JsonPropertyName("bbox")]
    [MinLength(4)]
    [MaxLength(6)]
    public double[]? Bbox { get; init; }

    [JsonPropertyName("intersects")]
    public JsonNode? Intersects { get; init; }

    [JsonPropertyName("datetime")]
    [StringLength(100)]
    public string? Datetime { get; init; }

    [JsonPropertyName("limit")]
    [Range(1, 10000)]
    public int? Limit { get; init; }

    [JsonPropertyName("token")]
    [StringLength(256)]
    public string? Token { get; init; }

    [JsonPropertyName("sortby")]
    [MaxLength(10)]
    public IReadOnlyList<StacSortFieldDto>? SortBy { get; init; }

    [JsonPropertyName("fields")]
    public StacSearchFieldsRequest? Fields { get; init; }

    [JsonPropertyName("filter")]
    public System.Text.Json.Nodes.JsonObject? Filter { get; init; }

    [JsonPropertyName("filter-lang")]
    [StringLength(50)]
    public string? FilterLang { get; init; }
}

public sealed record StacSortFieldDto
{
    [JsonPropertyName("field")]
    [StringLength(100)]
    public required string Field { get; init; }

    [JsonPropertyName("direction")]
    [StringLength(10)]
    public string Direction { get; init; } = "asc";
}

public sealed record StacSearchFieldsRequest
{
    [JsonPropertyName("include")]
    public IReadOnlyList<string>? Include { get; init; }

    [JsonPropertyName("exclude")]
    public IReadOnlyList<string>? Exclude { get; init; }
}
