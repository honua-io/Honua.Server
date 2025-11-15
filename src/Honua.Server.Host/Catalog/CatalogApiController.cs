// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Asp.Versioning;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Configuration;
using Honua.Server.Host.Middleware;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Catalog;

[ApiController]
[ApiVersion("1.0")]
[Authorize(Policy = "RequireViewer")]
[Route("api/v{version:apiVersion}/catalogs")]
public sealed class CatalogApiController : ControllerBase
{
    private readonly ICatalogProjectionService catalog;
    private readonly ILogger<CatalogApiController> logger;

    public CatalogApiController(ICatalogProjectionService catalog, ILogger<CatalogApiController> logger)
    {
        this.catalog = Guard.NotNull(catalog);
        this.logger = Guard.NotNull(logger);
    }

    /// <summary>
    /// Searches catalog records with optional filtering and pagination.
    /// </summary>
    /// <param name="query">Search query terms to match against titles, summaries, and keywords.</param>
    /// <param name="group">Filter results by group/folder ID.</param>
    /// <param name="limit">Maximum number of results to return (default: 100, max: 1000).</param>
    /// <param name="offset">Number of results to skip for pagination (default: 0).</param>
    /// <returns>A paginated collection of catalog records with total count information.</returns>
    /// <response code="200">Returns the paginated catalog records</response>
    /// <response code="400">Invalid limit or offset parameter</response>
    [HttpGet]
    [ProducesResponseType(typeof(CatalogCollectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [OutputCache(PolicyName = OutputCachePolicies.CatalogCollections)]
    public ActionResult<CatalogCollectionResponse> Get(
        [FromQuery(Name = "q")] string? query,
        [FromQuery(Name = "group")] string? group,
        [FromQuery(Name = "limit")] int? limit,
        [FromQuery(Name = "offset")] int? offset)
    {
        var sw = Stopwatch.StartNew();
        var effectiveLimit = limit ?? 100;
        var effectiveOffset = offset ?? 0;

        return ActivityScope.Execute<ActionResult<CatalogCollectionResponse>>(
            HonuaTelemetry.Metadata,
            "CatalogApiSearch",
            [
                ("catalog.operation", "Search"),
                ("catalog.query", query ?? "(none)"),
                ("catalog.group", group ?? "(all)"),
                ("catalog.limit", effectiveLimit),
                ("catalog.offset", effectiveOffset)
            ],
            activity =>
            {
                this.logger.LogInformation("Catalog API search request: query={Query}, group={Group}, limit={Limit}, offset={Offset}",
                    query ?? "(none)", group ?? "(all)", effectiveLimit, effectiveOffset);

                if (effectiveLimit <= 0 || effectiveLimit > ApiLimitsAndConstants.MaxCatalogLimit)
                {
                    this.logger.LogWarning("Invalid limit parameter in catalog search: {Limit}", effectiveLimit);
                    return this.BadRequest(new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Invalid limit parameter",
                        Detail = "Limit must be between 1 and 1000."
                    });
                }

                if (effectiveOffset < 0)
                {
                    this.logger.LogWarning("Invalid offset parameter in catalog search: {Offset}", effectiveOffset);
                    return this.BadRequest(new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Invalid offset parameter",
                        Detail = "Offset must be non-negative."
                    });
                }

                try
                {
                    // Get paginated results directly
                    var records = this.catalog.Search(query, group, effectiveLimit, effectiveOffset);

                    // For total count, we need to make a separate call with offset 0 and limit 1000
                    // This is a known limitation - for large catalogs (>1000 matching records),
                    // the total count will be capped at 1000
                    var countingResults = this.catalog.Search(query, group, limit: 1000, offset: 0);
                    var totalCount = countingResults.Count;

                    activity.AddTag("catalog.result_count", records.Count);
                    activity.AddTag("catalog.total_count", totalCount);

                    var response = new CatalogCollectionResponse
                    {
                        Query = query.IsNullOrWhiteSpace() ? null : query,
                        Group = group.IsNullOrWhiteSpace() ? null : group,
                        Limit = effectiveLimit,
                        Offset = effectiveOffset,
                        Count = records.Count,
                        TotalCount = totalCount,
                        Records = records.Select(MapRecord).ToArray()
                    };

                    this.logger.LogInformation("Catalog API search completed: query={Query}, group={Group}, returned={Count}, total={TotalCount}, duration={Duration}ms",
                        query ?? "(none)", group ?? "(all)", records.Count, totalCount, sw.Elapsed.TotalMilliseconds);

                    return this.Ok(response);
                }
                catch (ArgumentException ex)
                {
                    this.logger.LogError(ex, "Invalid search parameters in catalog API: {Error}", ex.Message);
                    return this.BadRequest(new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Invalid search parameters",
                        Detail = ex.Message
                    });
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Catalog API search failed: query={Query}, group={Group}, error={Error}",
                        query ?? "(none)", group ?? "(all)", ex.Message);
                    throw;
                }
            });
    }

    /// <summary>
    /// Retrieves a specific catalog record by service and layer identifier.
    /// </summary>
    /// <param name="serviceId">The unique identifier of the service containing the layer.</param>
    /// <param name="layerId">The unique identifier of the layer within the service.</param>
    /// <returns>The detailed catalog record including metadata, links, and spatial/temporal extent information.</returns>
    /// <response code="200">Catalog record retrieved successfully</response>
    /// <response code="404">Catalog record not found for the specified service and layer combination</response>
    /// <remarks>
    /// This endpoint provides comprehensive metadata about a specific layer including:
    /// - Layer title and description
    /// - Service information
    /// - Spatial and temporal extents
    /// - Related links (self, alternate HTML view, ESRI service)
    /// - Keywords and themes for discovery
    ///
    /// Results are cached according to the CatalogRecord output cache policy.
    /// </remarks>
    [HttpGet("{serviceId}/{layerId}")]
    [ProducesResponseType(typeof(CatalogRecordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [OutputCache(PolicyName = OutputCachePolicies.CatalogRecord)]
    public ActionResult<CatalogRecordResponse> GetRecord(string serviceId, string layerId)
    {
        var sw = Stopwatch.StartNew();
        var recordId = $"{serviceId}:{layerId}";

        return ActivityScope.Execute<ActionResult<CatalogRecordResponse>>(
            HonuaTelemetry.Metadata,
            "CatalogApiGetRecord",
            [
                ("catalog.operation", "GetRecord"),
                ("catalog.service_id", serviceId),
                ("catalog.layer_id", layerId)
            ],
            activity =>
            {
                this.logger.LogInformation("Catalog API get record request: recordId={RecordId}", recordId);

                try
                {
                    var record = this.catalog.GetRecord(recordId);
                    if (record is null)
                    {
                        this.logger.LogInformation("Catalog record not found: recordId={RecordId}, duration={Duration}ms",
                            recordId, sw.Elapsed.TotalMilliseconds);
                        activity.AddTag("catalog.found", false);
                        return this.NotFound();
                    }

                    activity.AddTag("catalog.found", true);
                    this.logger.LogInformation("Catalog record retrieved: recordId={RecordId}, title={Title}, duration={Duration}ms",
                        recordId, record.Title, sw.Elapsed.TotalMilliseconds);

                    return this.Ok(MapRecord(record));
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Error retrieving catalog record: recordId={RecordId}, error={Error}",
                        recordId, ex.Message);
                    throw;
                }
            });
    }

    private CatalogRecordResponse MapRecord(CatalogDiscoveryRecord record)
    {
        var hrefBase = HttpContext?.Request?.PathBase.Value ?? string.Empty;
        var links = new List<CatalogLinkResponse>();

        foreach (var link in record.Links)
        {
            links.Add(new CatalogLinkResponse
            {
                Rel = link.Rel,
                Href = link.Href,
                Type = link.Type,
                Title = link.Title
            });
        }

        links.Add(new CatalogLinkResponse
        {
            Rel = "self",
            Href = CombinePath(hrefBase, $"/api/v1.0/catalogs/{record.ServiceId}/{record.LayerId}"),
            Type = "application/json",
            Title = record.Title
        });

        links.Add(new CatalogLinkResponse
        {
            Rel = "alternate",
            Href = CombinePath(hrefBase, $"/catalog/{record.ServiceId}/{record.LayerId}"),
            Type = "text/html",
            Title = $"{record.Title} details"
        });

        links.Add(new CatalogLinkResponse
        {
            Rel = "esri:service",
            Href = CombinePath(hrefBase, $"/rest/services/{record.GroupId}/{record.ServiceId}/FeatureServer"),
            Type = "application/json",
            Title = $"{record.ServiceTitle} FeatureServer"
        });

        var extent = MapExtent(record.SpatialExtent, record.TemporalExtent);

        return new CatalogRecordResponse
        {
            Id = record.Id,
            Title = record.Title,
            Summary = record.Summary,
            GroupId = record.GroupId,
            GroupTitle = record.GroupTitle,
            ServiceId = record.ServiceId,
            ServiceTitle = record.ServiceTitle,
            ServiceType = record.ServiceType,
            Keywords = record.Keywords,
            Themes = record.Themes,
            Extent = extent,
            Links = links,
            Thumbnail = record.Thumbnail,
            Ordering = record.Ordering
        };
    }

    private static CatalogExtentResponse? MapExtent(CatalogSpatialExtentDefinition? spatial, CatalogTemporalExtentDefinition? temporal)
    {
        if (spatial is null && temporal is null)
        {
            return null;
        }

        var spa = spatial is null
            ? null
            : new CatalogSpatialExtentResponse
            {
                Bbox = spatial.Bbox,
                Crs = spatial.Crs
            };

        var tmp = temporal is null
            ? null
            : new CatalogTemporalExtentResponse
            {
                Start = temporal.Start?.ToString("o", CultureInfo.InvariantCulture),
                End = temporal.End?.ToString("o", CultureInfo.InvariantCulture)
            };

        return new CatalogExtentResponse
        {
            Spatial = spa,
            Temporal = tmp
        };
    }

    private static string CombinePath(string? basePath, string relative)
    {
        if (basePath.IsNullOrEmpty() || basePath == "/")
        {
            return relative;
        }

        return basePath!.EndsWith("/", StringComparison.Ordinal)
            ? basePath + relative.TrimStart('/')
            : basePath + relative;
    }
}

public sealed record CatalogCollectionResponse
{
    public string? Query { get; init; }
    public string? Group { get; init; }
    public int Limit { get; init; }
    public int Offset { get; init; }
    public int Count { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<CatalogRecordResponse> Records { get; init; } = Array.Empty<CatalogRecordResponse>();
}

public sealed record CatalogRecordResponse
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string GroupId { get; init; } = string.Empty;
    public string GroupTitle { get; init; } = string.Empty;
    public string ServiceId { get; init; } = string.Empty;
    public string ServiceTitle { get; init; } = string.Empty;
    public string ServiceType { get; init; } = string.Empty;
    public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Themes { get; init; } = Array.Empty<string>();
    public CatalogExtentResponse? Extent { get; init; }
    public IReadOnlyList<CatalogLinkResponse> Links { get; init; } = Array.Empty<CatalogLinkResponse>();
    public string? Thumbnail { get; init; }
    public int? Ordering { get; init; }
}

public sealed record CatalogExtentResponse
{
    public CatalogSpatialExtentResponse? Spatial { get; init; }
    public CatalogTemporalExtentResponse? Temporal { get; init; }
}

public sealed record CatalogSpatialExtentResponse
{
    public IReadOnlyList<double[]> Bbox { get; init; } = Array.Empty<double[]>();
    public string? Crs { get; init; }
}

public sealed record CatalogTemporalExtentResponse
{
    public string? Start { get; init; }
    public string? End { get; init; }
}

public sealed record CatalogLinkResponse
{
    public string? Rel { get; init; }
    public string? Href { get; init; }
    public string? Type { get; init; }
    public string? Title { get; init; }
}

