// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Raster.Cache;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Raster;

/// <summary>
/// REST API endpoints for Zarr time-series queries.
/// Provides access to temporal raster data with efficient chunk-based retrieval.
/// </summary>
public static class ZarrTimeSeriesEndpoints
{
    /// <summary>
    /// Map Zarr time-series endpoints to the application.
    /// </summary>
    public static IEndpointRouteBuilder MapZarrTimeSeriesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/raster/zarr/{datasetId}/timeseries")
            .WithTags("Zarr Time-Series");

        group.MapGet("/timesteps", GetTimeSteps)
            .WithName("GetZarrTimeSteps")
            .WithSummary("Get available timesteps from Zarr dataset")
            .WithDescription("Returns a list of all available timestamps in the Zarr dataset.")
            .Produces<TimeStepsResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapGet("/slice", GetTimeSlice)
            .WithName("GetZarrTimeSlice")
            .WithSummary("Get a single time slice from Zarr dataset")
            .WithDescription("Retrieves data for a specific timestamp with optional spatial subsetting.")
            .Produces<TimeSliceResponse>(StatusCodes.Status200OK, "application/json")
            .Produces<byte[]>(StatusCodes.Status200OK, "application/octet-stream")
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapGet("/range", GetTimeRange)
            .WithName("GetZarrTimeRange")
            .WithSummary("Get time range data from Zarr dataset")
            .WithDescription("Retrieves data for a time range with optional aggregation.")
            .Produces<TimeRangeResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    private static async Task<IResult> GetTimeSteps(
        [FromRoute] string datasetId,
        [FromQuery] string? variable,
        [FromServices] IZarrTimeSeriesService zarrService,
        [FromServices] ILogger<IZarrTimeSeriesService> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var variableName = variable ?? "data";
            logger.LogInformation("Getting timesteps for dataset {DatasetId}, variable {Variable}",
                datasetId, variableName);

            var timeSteps = await zarrService.GetTimeStepsAsync(datasetId, variableName, cancellationToken);

            var response = new TimeStepsResponse(
                datasetId,
                variableName,
                timeSteps.Count,
                timeSteps.Select(ts => ts.ToString("O")).ToList());

            return Results.Ok(response);
        }
        catch (FileNotFoundException ex)
        {
            logger.LogWarning(ex, "Dataset not found: {DatasetId}", datasetId);
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound,
                title: "Dataset Not Found");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting timesteps for dataset {DatasetId}", datasetId);
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error");
        }
    }

    private static async Task<IResult> GetTimeSlice(
        [FromRoute] string datasetId,
        [FromQuery] string timestamp,
        [FromQuery] string? variable,
        [FromQuery] string? bbox,
        [FromServices] IZarrTimeSeriesService zarrService,
        [FromServices] IZarrTimeSeriesMetrics? metrics,
        [FromServices] ILogger<IZarrTimeSeriesService> logger,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var variableName = variable ?? "data";

            // Parse timestamp
            if (!DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedTimestamp))
            {
                return Results.Problem(
                    detail: $"Invalid timestamp format: {timestamp}. Use ISO 8601 format.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Request");
            }

            // Parse bounding box if provided
            BoundingBox? boundingBox = null;
            if (!bbox.IsNullOrEmpty())
            {
                var parts = bbox.Split(',');
                if (parts.Length == 4 &&
                    parts[0].TryParseDoubleStrict(out var minX) &&
                    parts[1].TryParseDoubleStrict(out var minY) &&
                    parts[2].TryParseDoubleStrict(out var maxX) &&
                    parts[3].TryParseDoubleStrict(out var maxY))
                {
                    boundingBox = new BoundingBox(minX, minY, maxX, maxY);
                }
                else
                {
                    return Results.Problem(
                        detail: "Invalid bbox format. Use: minx,miny,maxx,maxy",
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Invalid Request");
                }
            }

            logger.LogInformation(
                "Getting time slice for dataset {DatasetId}, variable {Variable}, timestamp {Timestamp}",
                datasetId, variableName, parsedTimestamp);

            var startTime = DateTime.UtcNow;
            var slice = await zarrService.QueryTimeSliceAsync(datasetId, variableName, parsedTimestamp, boundingBox, cancellationToken);
            var duration = DateTime.UtcNow - startTime;

            // Record metrics
            metrics?.RecordTimeSliceQuery(datasetId, variableName, duration, slice.Data.Length);

            // Check Accept header for content negotiation
            var acceptHeader = context.Request.Headers.Accept.ToString();
            if (acceptHeader.Contains("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            {
                // Return binary data
                return Results.Bytes(slice.Data, "application/octet-stream");
            }
            else
            {
                // Return JSON response
                var response = new TimeSliceResponse(
                    datasetId,
                    variableName,
                    slice.Timestamp.ToString("O"),
                    slice.TimeIndex,
                    new MetadataInfo(
                        slice.Metadata.Shape,
                        slice.Metadata.Chunks,
                        slice.Metadata.DType),
                    new BoundingBoxInfo(
                        slice.SpatialExtent.MinX,
                        slice.SpatialExtent.MinY,
                        slice.SpatialExtent.MaxX,
                        slice.SpatialExtent.MaxY),
                    slice.Data.Length,
                    Convert.ToBase64String(slice.Data));

                return Results.Ok(response);
            }
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Invalid operation for dataset {DatasetId}", datasetId);
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting time slice for dataset {DatasetId}", datasetId);
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error");
        }
    }

    private static async Task<IResult> GetTimeRange(
        [FromRoute] string datasetId,
        [FromQuery] string start,
        [FromQuery] string end,
        [FromQuery] string? variable,
        [FromQuery] string? bbox,
        [FromQuery] string? aggregation,
        [FromServices] IZarrTimeSeriesService zarrService,
        [FromServices] IZarrTimeSeriesMetrics? metrics,
        [FromServices] ILogger<IZarrTimeSeriesService> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var variableName = variable ?? "data";

            // Parse timestamps
            if (!DateTimeOffset.TryParse(start, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var startTime))
            {
                return Results.Problem(
                    detail: $"Invalid start timestamp format: {start}. Use ISO 8601 format.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Request");
            }

            if (!DateTimeOffset.TryParse(end, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var endTime))
            {
                return Results.Problem(
                    detail: $"Invalid end timestamp format: {end}. Use ISO 8601 format.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Request");
            }

            // Parse bounding box if provided
            BoundingBox? boundingBox = null;
            if (!bbox.IsNullOrEmpty())
            {
                var parts = bbox.Split(',');
                if (parts.Length == 4 &&
                    parts[0].TryParseDoubleStrict(out var minX) &&
                    parts[1].TryParseDoubleStrict(out var minY) &&
                    parts[2].TryParseDoubleStrict(out var maxX) &&
                    parts[3].TryParseDoubleStrict(out var maxY))
                {
                    boundingBox = new BoundingBox(minX, minY, maxX, maxY);
                }
            }

            // Parse aggregation interval if provided
            TimeSpan? aggregationInterval = null;
            if (!aggregation.IsNullOrEmpty())
            {
                if (TimeSpan.TryParse(aggregation, CultureInfo.InvariantCulture, out var interval))
                {
                    aggregationInterval = interval;
                }
            }

            logger.LogInformation(
                "Getting time range for dataset {DatasetId}, variable {Variable}, {StartTime} to {EndTime}",
                datasetId, variableName, startTime, endTime);

            var queryStart = DateTime.UtcNow;
            var timeSeriesData = await zarrService.QueryTimeRangeAsync(
                datasetId, variableName, startTime, endTime, boundingBox, aggregationInterval, cancellationToken);
            var duration = DateTime.UtcNow - queryStart;

            // Record metrics
            var totalBytes = timeSeriesData.DataSlices.Sum(d => d.Length);
            metrics?.RecordTimeRangeQuery(datasetId, variableName, duration, timeSeriesData.Timestamps.Count, totalBytes);

            var response = new TimeRangeResponse(
                datasetId,
                variableName,
                startTime.ToString("O"),
                endTime.ToString("O"),
                timeSeriesData.Timestamps.Select(ts => ts.ToString("O")).ToList(),
                new MetadataInfo(
                    timeSeriesData.Metadata.Shape,
                    timeSeriesData.Metadata.Chunks,
                    timeSeriesData.Metadata.DType),
                timeSeriesData.AggregationMethod,
                totalBytes,
                timeSeriesData.DataSlices.Select(Convert.ToBase64String).ToList());

            return Results.Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Invalid operation for dataset {DatasetId}", datasetId);
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting time range for dataset {DatasetId}", datasetId);
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error");
        }
    }
}

/// <summary>
/// Response model for timesteps query.
/// </summary>
public sealed record TimeStepsResponse(
    string DatasetId,
    string Variable,
    int Count,
    IReadOnlyList<string> Timesteps);

/// <summary>
/// Response model for time slice query.
/// </summary>
public sealed record TimeSliceResponse(
    string DatasetId,
    string Variable,
    string Timestamp,
    int TimeIndex,
    MetadataInfo Metadata,
    BoundingBoxInfo SpatialExtent,
    int DataSizeBytes,
    string DataBase64);

/// <summary>
/// Response model for time range query.
/// </summary>
public sealed record TimeRangeResponse(
    string DatasetId,
    string Variable,
    string StartTime,
    string EndTime,
    IReadOnlyList<string> Timesteps,
    MetadataInfo Metadata,
    string AggregationMethod,
    int TotalDataSizeBytes,
    IReadOnlyList<string> DataSlicesBase64);

/// <summary>
/// Metadata information for response.
/// </summary>
public sealed record MetadataInfo(
    int[] Shape,
    int[] Chunks,
    string DType);

/// <summary>
/// Bounding box information for response.
/// </summary>
public sealed record BoundingBoxInfo(
    double MinX,
    double MinY,
    double MaxX,
    double MaxY);
