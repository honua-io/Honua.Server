// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Mosaic;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Honua.Server.Host.Raster;

internal static class RasterMosaicEndpoints
{
    public static IEndpointRouteBuilder MapRasterMosaicEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var mosaicGroup = endpoints.MapGroup("/raster/mosaic")
            .WithTags("Raster Mosaic")
            .RequireRateLimiting("OgcApiPolicy");

        mosaicGroup.MapGet("/capabilities", GetCapabilities)
            .WithName("GetRasterMosaicCapabilities")
            .WithSummary("Get raster mosaic capabilities")
            .WithDescription("Get raster mosaic capabilities");

        mosaicGroup.MapPost("", CreateMosaic)
            .WithName("CreateRasterMosaic")
            .WithSummary("Create raster mosaic")
            .WithDescription("Create a raster mosaic from multiple datasets");

        return endpoints;
    }

    private static IResult GetCapabilities([FromServices] IRasterMosaicService mosaicService)
    {
        var capabilities = mosaicService.GetCapabilities();
        return Results.Ok(capabilities);
    }

    private static async Task<IResult> CreateMosaic(
        [FromBody] RasterMosaicRequestDto request,
        [FromServices] IRasterMosaicService mosaicService,
        [FromServices] IRasterDatasetRegistry rasterRegistry,
        CancellationToken cancellationToken)
    {
        if (request.DatasetIds.IsNullOrEmpty())
        {
            return Results.BadRequest("At least one dataset ID is required");
        }

        if (request.BoundingBox == null || request.BoundingBox.Length != 4)
        {
            return Results.BadRequest("Bounding box must have 4 values [minX, minY, maxX, maxY]");
        }

        // Load datasets
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

        var mosaicRequest = new RasterMosaicRequest(
            datasets,
            request.BoundingBox,
            request.Width ?? 1024,
            request.Height ?? 1024,
            request.SourceCrs ?? "EPSG:4326",
            request.TargetCrs ?? "EPSG:4326",
            request.Format ?? "png",
            request.Transparent ?? true,
            request.Method ?? RasterMosaicMethod.First,
            request.Resampling ?? RasterResamplingMethod.Bilinear,
            request.StyleId);

        var result = await mosaicService.CreateMosaicAsync(mosaicRequest, cancellationToken);

        return Results.File(result.Data, result.ContentType);
    }
}

public sealed record RasterMosaicRequestDto(
    [Required][MinLength(1)][MaxLength(100)] System.Collections.Generic.IReadOnlyList<string> DatasetIds,
    [Required][MinLength(4)][MaxLength(4)] double[] BoundingBox,
    [Range(1, 8192)] int? Width = null,
    [Range(1, 8192)] int? Height = null,
    [StringLength(50)] string? SourceCrs = null,
    [StringLength(50)] string? TargetCrs = null,
    [StringLength(10)] string? Format = null,
    bool? Transparent = null,
    RasterMosaicMethod? Method = null,
    RasterResamplingMethod? Resampling = null,
    [StringLength(256)] string? StyleId = null);
