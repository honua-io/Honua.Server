// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Ogc;
using Honua.Server.Host.Raster;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Admin;

/// <summary>
/// Extension methods for mapping raster tile cache administration endpoints.
/// </summary>
internal static class RasterTileCacheEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps all raster tile cache administration endpoints including job management and cache purging.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    /// <remarks>
    /// Provides endpoints for:
    /// - Creating preseed jobs to pre-generate tiles
    /// - Listing all preseed jobs and their status
    /// - Getting details of specific jobs
    /// - Cancelling running jobs
    /// - Purging cached tiles for specific datasets
    /// </remarks>
    /// <example>
    /// Example request to create a preseed job:
    /// <code>
    /// POST /admin/raster-cache/jobs
    /// {
    ///   "datasetIds": ["rainfall-2024"],
    ///   "tileMatrixSetId": "WorldWebMercatorQuad",
    ///   "minZoom": 0,
    ///   "maxZoom": 10,
    ///   "format": "image/png"
    /// }
    /// </code>
    /// </example>
    public static IEndpointRouteBuilder MapRasterTileCacheAdministration(this IEndpointRouteBuilder app)
    {
        Guard.NotNull(app);

        var group = app.MapGroup("/admin/raster-cache")
            .RequireAuthorization("RequireAdministrator")
            .WithTags("Raster Tile Cache");

        group.MapPost("/jobs", HandleCreateJob)
            .Accepts<RasterTilePreseedJobDto>("application/json")
            .Produces<RasterTilePreseedJobSnapshot>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithName("CreateRasterTilePreseedJob");

        group.MapGet("/jobs", async ([FromServices] IRasterTilePreseedService service, CancellationToken cancellationToken = default) =>
            Results.Ok(new { jobs = await service.ListJobsAsync(cancellationToken).ConfigureAwait(false) }))
            .Produces(StatusCodes.Status200OK)
            .WithName("ListRasterTilePreseedJobs");

        group.MapGet("/jobs/{jobId:guid}", async (Guid jobId, [FromServices] IRasterTilePreseedService service, CancellationToken cancellationToken = default) =>
        {
            var snapshot = await service.TryGetJobAsync(jobId, cancellationToken).ConfigureAwait(false);
            if (snapshot is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(new { job = snapshot });
        })
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .WithName("GetRasterTilePreseedJob");

        group.MapDelete("/jobs/{jobId:guid}", async (Guid jobId, [FromServices] IRasterTilePreseedService service) =>
        {
            var snapshot = await service.CancelAsync(jobId, "Cancelled via API").ConfigureAwait(false);
            if (snapshot is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(new { job = snapshot });
        })
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .WithName("CancelRasterTilePreseedJob");

        group.MapPost("/datasets/purge", HandlePurgeCache)
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithName("PurgeRasterTileCache");

        return app;
    }

    private static async Task<IResult> HandleCreateJob(
        HttpContext context,
        [FromServices] IRasterTilePreseedService service,
        [FromServices] ILogger<IRasterTilePreseedService> logger,
        CancellationToken cancellationToken)
    {
        var dto = await context.Request.ReadFromJsonAsync<RasterTilePreseedJobDto>(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (dto is null || dto.DatasetIds.Count == 0)
        {
            return ApiErrorResponse.Json.BadRequestResult("datasetIds array is required.");
        }

        try
        {
            var request = new RasterTilePreseedRequest(dto.DatasetIds)
            {
                TileMatrixSetId = dto.TileMatrixSetId.IsNullOrWhiteSpace() ? OgcTileMatrixHelper.WorldWebMercatorQuadId : dto.TileMatrixSetId!,
                MinZoom = dto.MinZoom,
                MaxZoom = dto.MaxZoom,
                StyleId = dto.StyleId.IsNullOrWhiteSpace() ? null : dto.StyleId,
                Transparent = dto.Transparent ?? true,
                Format = dto.Format.IsNullOrWhiteSpace() ? "image/png" : dto.Format!,
                Overwrite = dto.Overwrite ?? false,
                TileSize = dto.TileSize ?? 256
            };

            var snapshot = await service.EnqueueAsync(request, cancellationToken).ConfigureAwait(false);
            return Results.Accepted($"/admin/raster-cache/jobs/{snapshot.JobId}", new { job = snapshot });
        }
        catch (InvalidOperationException ex)
        {
            // These are typically validation errors (e.g., dataset not found, invalid zoom levels)
            logger.LogWarning(ex, "Raster tile preseed job creation failed validation for datasets: {DatasetIds}", string.Join(", ", dto.DatasetIds));
            return ApiErrorResponse.Json.BadRequestResult("The preseed request could not be processed. Please check your dataset IDs and parameters.");
        }
        catch (ArgumentException ex)
        {
            // These are parameter validation errors
            logger.LogWarning(ex, "Raster tile preseed job creation failed due to invalid arguments for datasets: {DatasetIds}", string.Join(", ", dto.DatasetIds));
            return ApiErrorResponse.Json.BadRequestResult("Invalid request parameters. Please verify your tile configuration.");
        }
        catch (Exception ex)
        {
            // Log the full exception details internally but return a generic error to clients
            // to prevent leaking sensitive information like stack traces or file paths
            logger.LogError(ex, "Raster tile preseed job creation failed unexpectedly for datasets: {DatasetIds}", string.Join(", ", dto.DatasetIds));
            return Results.Problem(
                detail: "An unexpected error occurred while creating the raster tile preseed job. Please try again or contact support if the problem persists.",
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Raster Tile Preseed Job Creation Failed");
        }
    }

    private static async Task<IResult> HandlePurgeCache(
        RasterTileCachePurgeRequest request,
        [FromServices] IRasterTilePreseedService service,
        [FromServices] ILogger<IRasterTilePreseedService> logger,
        CancellationToken cancellationToken)
    {
        if (request?.DatasetIds is null || request.DatasetIds.Count == 0)
        {
            return ApiErrorResponse.Json.BadRequestResult("datasetIds array is required.");
        }

        try
        {
            var result = await service.PurgeAsync(request.DatasetIds, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new { purged = result.PurgedDatasets, failed = result.FailedDatasets });
        }
        catch (InvalidOperationException ex)
        {
            // These are typically validation errors (e.g., dataset not found, cache not accessible)
            logger.LogWarning(ex, "Raster tile cache purge failed validation for datasets: {DatasetIds}", string.Join(", ", request.DatasetIds));
            return ApiErrorResponse.Json.BadRequestResult("The cache purge request could not be processed. Please check your dataset IDs.");
        }
        catch (Exception ex)
        {
            // Log the full exception details internally but return a generic error to clients
            // to prevent leaking sensitive information like stack traces, storage paths, or backend details
            logger.LogError(ex, "Raster tile cache purge failed unexpectedly for datasets: {DatasetIds}", string.Join(", ", request.DatasetIds));
            return Results.Problem(
                detail: "An unexpected error occurred while purging the raster tile cache. Please try again or contact support if the problem persists.",
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Raster Tile Cache Purge Failed");
        }
    }

    /// <summary>
    /// Request model for purging raster tile cache entries.
    /// </summary>
    private sealed record RasterTileCachePurgeRequest
    {
        /// <summary>
        /// Gets or initializes the list of dataset IDs to purge from cache.
        /// </summary>
        public List<string> DatasetIds { get; init; } = new();
    }

    /// <summary>
    /// Data transfer object for creating raster tile preseed jobs.
    /// </summary>
    private sealed record RasterTilePreseedJobDto
    {
        /// <summary>
        /// Gets or initializes the list of dataset IDs to preseed.
        /// </summary>
        public List<string> DatasetIds { get; init; } = new();

        /// <summary>
        /// Gets or initializes the tile matrix set identifier (defaults to WorldWebMercatorQuad).
        /// </summary>
        public string? TileMatrixSetId { get; init; }

        /// <summary>
        /// Gets or initializes the minimum zoom level to generate.
        /// </summary>
        public int? MinZoom { get; init; }

        /// <summary>
        /// Gets or initializes the maximum zoom level to generate.
        /// </summary>
        public int? MaxZoom { get; init; }

        /// <summary>
        /// Gets or initializes the style identifier to apply when rendering tiles.
        /// </summary>
        public string? StyleId { get; init; }

        /// <summary>
        /// Gets or initializes a value indicating whether tiles should have transparent backgrounds.
        /// </summary>
        public bool? Transparent { get; init; }

        /// <summary>
        /// Gets or initializes the output image format (e.g., "image/png", "image/jpeg").
        /// </summary>
        public string? Format { get; init; }

        /// <summary>
        /// Gets or initializes a value indicating whether to overwrite existing tiles.
        /// </summary>
        public bool? Overwrite { get; init; }

        /// <summary>
        /// Gets or initializes the tile size in pixels (typically 256 or 512).
        /// </summary>
        public int? TileSize { get; init; }
    }
}
