// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Raster.Caching;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.GeoservicesREST;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Honua.Server.Host.Admin;

internal static class RasterTileCacheStatisticsEndpoints
{
    public static IEndpointRouteBuilder MapRasterTileCacheStatistics(this IEndpointRouteBuilder app)
    {
        Guard.NotNull(app);

        var group = app.MapGroup("/admin/raster-cache/statistics")
            .RequireAuthorization("RequireViewer")
            .WithTags("Raster Tile Cache Statistics");

        group.MapGet("", GetOverallStatistics)
            .Produces<CacheStatistics>()
            .WithName("GetRasterCacheStatistics")
            .WithDescription("Get overall cache statistics");

        group.MapGet("/datasets", GetAllDatasetStatistics)
            .Produces<DatasetCacheStatistics[]>()
            .WithName("GetAllDatasetCacheStatistics")
            .WithDescription("Get cache statistics for all datasets");

        group.MapGet("/datasets/{datasetId}", GetDatasetStatistics)
            .Produces<DatasetCacheStatistics>()
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetDatasetCacheStatistics")
            .WithDescription("Get cache statistics for a specific dataset");

        group.MapPost("/reset", ResetStatistics)
            .RequireAuthorization("RequireAdministrator")
            .Produces(StatusCodes.Status204NoContent)
            .WithName("ResetCacheStatistics")
            .WithDescription("Reset cache statistics");

        return app;
    }

    private static async Task<IResult> GetOverallStatistics(
        [FromServices] IRasterTileCacheStatisticsService statisticsService,
        CancellationToken cancellationToken)
    {
        var stats = await statisticsService.GetStatisticsAsync(cancellationToken);
        return Results.Ok(stats);
    }

    private static async Task<IResult> GetAllDatasetStatistics(
        [FromServices] IRasterTileCacheStatisticsService statisticsService,
        CancellationToken cancellationToken)
    {
        var stats = await statisticsService.GetAllDatasetStatisticsAsync(cancellationToken);
        return Results.Ok(stats);
    }

    private static async Task<IResult> GetDatasetStatistics(
        string datasetId,
        [FromServices] IRasterTileCacheStatisticsService statisticsService,
        CancellationToken cancellationToken)
    {
        var stats = await statisticsService.GetDatasetStatisticsAsync(datasetId, cancellationToken);
        if (stats == null)
        {
            return GeoservicesRESTErrorHelper.NotFoundWithMessage($"No cache statistics found for dataset '{datasetId}'");
        }

        return Results.Ok(stats);
    }

    private static async Task<IResult> ResetStatistics(
        [FromServices] IRasterTileCacheStatisticsService statisticsService,
        CancellationToken cancellationToken)
    {
        await statisticsService.ResetStatisticsAsync(cancellationToken);
        return Results.NoContent();
    }
}
