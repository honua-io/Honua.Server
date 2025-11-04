// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Analytics;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Honua.Server.Host.Raster;

internal static class RasterAnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapRasterAnalyticsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var analyticsGroup = endpoints.MapGroup("/raster/analytics")
            .WithTags("Raster Analytics")
            .RequireRateLimiting("OgcApiPolicy");

        analyticsGroup.MapGet("/capabilities", GetCapabilities)
            .WithName("GetRasterAnalyticsCapabilities")
            .WithSummary("Get raster analytics capabilities")
            .WithDescription("Get raster analytics capabilities");

        analyticsGroup.MapPost("/statistics", CalculateStatistics)
            .WithName("CalculateRasterStatistics")
            .WithSummary("Calculate raster statistics")
            .WithDescription("Calculate statistics for a raster dataset");

        analyticsGroup.MapPost("/algebra", CalculateAlgebra)
            .WithName("CalculateRasterAlgebra")
            .WithSummary("Perform raster algebra")
            .WithDescription("Perform algebra operations on raster datasets");

        analyticsGroup.MapPost("/extract", ExtractValues)
            .WithName("ExtractRasterValues")
            .WithSummary("Extract raster values")
            .WithDescription("Extract raster values at specific points");

        analyticsGroup.MapPost("/histogram", CalculateHistogram)
            .WithName("CalculateRasterHistogram")
            .WithSummary("Calculate raster histogram")
            .WithDescription("Calculate histogram for a raster dataset");

        analyticsGroup.MapPost("/zonal-statistics", CalculateZonalStatistics)
            .WithName("CalculateZonalStatistics")
            .WithSummary("Calculate zonal statistics")
            .WithDescription("Calculate zonal statistics for polygons");

        analyticsGroup.MapPost("/terrain", CalculateTerrain)
            .WithName("CalculateTerrainAnalysis")
            .WithSummary("Calculate terrain analysis")
            .WithDescription("Calculate terrain analysis (hillshade, slope, aspect, curvature, roughness)");

        return endpoints;
    }

    private static IResult GetCapabilities([FromServices] IRasterAnalyticsService analyticsService)
    {
        var capabilities = analyticsService.GetCapabilities();
        return Results.Ok(capabilities);
    }

    private static async Task<IResult> CalculateStatistics(
        [FromBody] RasterStatisticsRequestDto request,
        [FromServices] IRasterAnalyticsService analyticsService,
        [FromServices] IRasterDatasetRegistry rasterRegistry,
        CancellationToken cancellationToken)
    {
        var dataset = await rasterRegistry.FindAsync(request.DatasetId, cancellationToken).ConfigureAwait(false);
        if (dataset == null)
        {
            return Results.NotFound($"Dataset {request.DatasetId} not found");
        }

        var statsRequest = new RasterStatisticsRequest(dataset, request.BoundingBox, request.BandIndex);
        var result = await analyticsService.CalculateStatisticsAsync(statsRequest, cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> CalculateAlgebra(
        [FromBody] RasterAlgebraRequestDto request,
        [FromServices] IRasterAnalyticsService analyticsService,
        [FromServices] IRasterDatasetRegistry rasterRegistry,
        CancellationToken cancellationToken)
    {
        if (request.DatasetIds.IsNullOrEmpty())
        {
            return Results.BadRequest("At least one dataset ID is required");
        }

        if (string.IsNullOrWhiteSpace(request.Expression))
        {
            return Results.BadRequest("Expression is required");
        }

        if (request.BoundingBox == null || request.BoundingBox.Length != 4)
        {
            return Results.BadRequest("Bounding box must have 4 values [minX, minY, maxX, maxY]");
        }

        var datasets = new List<Core.Metadata.RasterDatasetDefinition>();
        foreach (var datasetId in request.DatasetIds)
        {
            var dataset = await rasterRegistry.FindAsync(datasetId, cancellationToken).ConfigureAwait(false);
            if (dataset == null)
            {
                return Results.NotFound($"Dataset {datasetId} not found");
            }
            datasets.Add(dataset);
        }

        var algebraRequest = new RasterAlgebraRequest(
            datasets,
            request.Expression,
            request.BoundingBox,
            request.Width ?? 1024,
            request.Height ?? 1024,
            request.Format ?? "png");

        var result = await analyticsService.CalculateAlgebraAsync(algebraRequest, cancellationToken);

        return Results.File(result.Data, result.ContentType);
    }

    private static async Task<IResult> ExtractValues(
        [FromBody] RasterValueExtractionRequestDto request,
        [FromServices] IRasterAnalyticsService analyticsService,
        [FromServices] IRasterDatasetRegistry rasterRegistry,
        CancellationToken cancellationToken)
    {
        var dataset = await rasterRegistry.FindAsync(request.DatasetId, cancellationToken).ConfigureAwait(false);
        if (dataset == null)
        {
            return Results.NotFound($"Dataset {request.DatasetId} not found");
        }

        if (request.Points.IsNullOrEmpty())
        {
            return Results.BadRequest("At least one point is required");
        }

        var points = request.Points.Select(p => new Point(p.X, p.Y)).ToList();
        var extractRequest = new RasterValueExtractionRequest(dataset, points, request.BandIndex);
        var result = await analyticsService.ExtractValuesAsync(extractRequest, cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> CalculateHistogram(
        [FromBody] RasterHistogramRequestDto request,
        [FromServices] IRasterAnalyticsService analyticsService,
        [FromServices] IRasterDatasetRegistry rasterRegistry,
        CancellationToken cancellationToken)
    {
        var dataset = await rasterRegistry.FindAsync(request.DatasetId, cancellationToken).ConfigureAwait(false);
        if (dataset == null)
        {
            return Results.NotFound($"Dataset {request.DatasetId} not found");
        }

        var histRequest = new RasterHistogramRequest(dataset, request.BinCount ?? 256, request.BoundingBox, request.BandIndex);
        var result = await analyticsService.CalculateHistogramAsync(histRequest, cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> CalculateZonalStatistics(
        [FromBody] ZonalStatisticsRequestDto request,
        [FromServices] IRasterAnalyticsService analyticsService,
        [FromServices] IRasterDatasetRegistry rasterRegistry,
        CancellationToken cancellationToken)
    {
        var dataset = await rasterRegistry.FindAsync(request.DatasetId, cancellationToken).ConfigureAwait(false);
        if (dataset == null)
        {
            return Results.NotFound($"Dataset {request.DatasetId} not found");
        }

        if (request.Zones.IsNullOrEmpty())
        {
            return Results.BadRequest("At least one zone is required");
        }

        var zones = request.Zones.Select(z => new Polygon(
            z.Coordinates.Select(p => new Point(p.X, p.Y)).ToList(),
            z.ZoneId,
            z.Properties)).ToList();

        var zonalRequest = new ZonalStatisticsRequest(dataset, zones, request.BandIndex, request.Statistics);
        var result = await analyticsService.CalculateZonalStatisticsAsync(zonalRequest, cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> CalculateTerrain(
        [FromBody] TerrainAnalysisRequestDto request,
        [FromServices] IRasterAnalyticsService analyticsService,
        [FromServices] IRasterDatasetRegistry rasterRegistry,
        CancellationToken cancellationToken)
    {
        var dataset = await rasterRegistry.FindAsync(request.ElevationDatasetId, cancellationToken).ConfigureAwait(false);
        if (dataset == null)
        {
            return Results.NotFound($"Dataset {request.ElevationDatasetId} not found");
        }

        var terrainRequest = new TerrainAnalysisRequest(
            dataset,
            request.AnalysisType,
            request.BoundingBox,
            request.Width ?? 512,
            request.Height ?? 512,
            request.Format ?? "png",
            request.ZFactor ?? 1.0,
            request.Azimuth ?? 315.0,
            request.Altitude ?? 45.0);

        var result = await analyticsService.CalculateTerrainAsync(terrainRequest, cancellationToken);

        return Results.File(result.Data, result.ContentType);
    }
}

public sealed record RasterStatisticsRequestDto(
    [Required][StringLength(256)] string DatasetId,
    [MinLength(4)][MaxLength(6)] double[]? BoundingBox = null,
    [Range(0, int.MaxValue)] int? BandIndex = null);

public sealed record RasterAlgebraRequestDto(
    [Required][MinLength(1)][MaxLength(10)] System.Collections.Generic.IReadOnlyList<string> DatasetIds,
    [Required][StringLength(1000, MinimumLength = 1)] string Expression,
    [Required][MinLength(4)][MaxLength(4)] double[] BoundingBox,
    [Range(1, 8192)] int? Width = null,
    [Range(1, 8192)] int? Height = null,
    [StringLength(10)] string? Format = null);

public sealed record RasterValueExtractionRequestDto(
    [Required][StringLength(256)] string DatasetId,
    [Required][MinLength(1)][MaxLength(1000)] System.Collections.Generic.IReadOnlyList<PointDto> Points,
    [Range(0, int.MaxValue)] int? BandIndex = null);

public sealed record PointDto(
    [Range(-180, 180)] double X,
    [Range(-90, 90)] double Y);

public sealed record RasterHistogramRequestDto(
    [Required][StringLength(256)] string DatasetId,
    [Range(2, 1024)] int? BinCount = null,
    [MinLength(4)][MaxLength(6)] double[]? BoundingBox = null,
    [Range(0, int.MaxValue)] int? BandIndex = null);

public sealed record ZonalStatisticsRequestDto(
    [Required][StringLength(256)] string DatasetId,
    [Required][MinLength(1)][MaxLength(1000)] System.Collections.Generic.IReadOnlyList<PolygonDto> Zones,
    [Range(0, int.MaxValue)] int? BandIndex = null,
    [MaxLength(20)] System.Collections.Generic.IReadOnlyList<string>? Statistics = null);

public sealed record PolygonDto(
    [Required][MinLength(3)][MaxLength(10000)] System.Collections.Generic.IReadOnlyList<PointDto> Coordinates,
    [StringLength(256)] string? ZoneId = null,
    System.Collections.Generic.Dictionary<string, object>? Properties = null);

public sealed record TerrainAnalysisRequestDto(
    [Required][StringLength(256)] string ElevationDatasetId,
    [Required] TerrainAnalysisType AnalysisType,
    [MinLength(4)][MaxLength(6)] double[]? BoundingBox = null,
    [Range(1, 4096)] int? Width = null,
    [Range(1, 4096)] int? Height = null,
    [StringLength(10)] string? Format = null,
    [Range(0.001, 1000)] double? ZFactor = null,
    [Range(0, 360)] double? Azimuth = null,
    [Range(0, 90)] double? Altitude = null);
