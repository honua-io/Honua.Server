// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Raster.Caching;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Honua.Server.Host.Admin;

internal static class RasterTileCacheQuotaEndpoints
{
    public static IEndpointRouteBuilder MapRasterTileCacheQuota(this IEndpointRouteBuilder app)
    {
        Guard.NotNull(app);

        var group = app.MapGroup("/admin/raster-cache/quota")
            .RequireAuthorization("RequireAdministrator")
            .WithTags("Raster Tile Cache Quota");

        group.MapGet("", GetAllQuotas)
            .Produces<QuotaConfigurationsResponse>()
            .WithName("GetAllRasterCacheQuotas")
            .WithDescription("Get all dataset quota configurations");

        group.MapGet("/{datasetId}/status", GetQuotaStatus)
            .Produces<DatasetQuotaStatus>()
            .WithName("GetDatasetQuotaStatus")
            .WithDescription("Get quota status for a dataset");

        group.MapPut("/{datasetId}", UpdateQuota)
            .Accepts<DiskQuotaConfiguration>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithName("UpdateDatasetQuota")
            .WithDescription("Update quota configuration for a dataset");

        group.MapPost("/{datasetId}/enforce", EnforceQuota)
            .Produces<QuotaEnforcementResult>()
            .WithName("EnforceDatasetQuota")
            .WithDescription("Enforce quota by removing tiles if over quota");

        return app;
    }

    private static async Task<IResult> GetAllQuotas(
        [FromServices] IRasterTileCacheDiskQuotaService quotaService,
        CancellationToken cancellationToken)
    {
        var quotas = await quotaService.GetAllQuotasAsync(cancellationToken);
        return Results.Ok(new QuotaConfigurationsResponse(quotas));
    }

    private static async Task<IResult> GetQuotaStatus(
        string datasetId,
        [FromServices] IRasterTileCacheDiskQuotaService quotaService,
        CancellationToken cancellationToken)
    {
        var status = await quotaService.GetQuotaStatusAsync(datasetId, cancellationToken);
        return Results.Ok(status);
    }

    private static async Task<IResult> UpdateQuota(
        string datasetId,
        DiskQuotaConfiguration quota,
        [FromServices] IRasterTileCacheDiskQuotaService quotaService,
        CancellationToken cancellationToken)
    {
        try
        {
            await quotaService.UpdateQuotaAsync(datasetId, quota, cancellationToken);
            return Results.NoContent();
        }
        catch (ArgumentException ex)
        {
            return ApiErrorResponse.Json.BadRequestResult(ex.Message);
        }
    }

    private static async Task<IResult> EnforceQuota(
        string datasetId,
        [FromServices] IRasterTileCacheDiskQuotaService quotaService,
        CancellationToken cancellationToken)
    {
        var result = await quotaService.EnforceQuotaAsync(datasetId, cancellationToken);
        return Results.Ok(result);
    }

    private sealed record QuotaConfigurationsResponse(
        System.Collections.Generic.IReadOnlyDictionary<string, DiskQuotaConfiguration> Quotas);
}
