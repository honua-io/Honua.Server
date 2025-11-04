// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Print.MapFish;
using Honua.Server.Core.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Print;

internal static class MapFishPrintEndpointExtensions
{
    public static IEndpointRouteBuilder MapMapFishPrint(this IEndpointRouteBuilder endpoints)
    {
        Guard.NotNull(endpoints);

        var group = endpoints.MapGroup("/print");
        group.RequireAuthorization("RequireViewer");

        group.MapGet("/apps.json", MapFishPrintHandlers.GetApplicationsAsync);
        group.MapGet("/apps/{appId}.json", MapFishPrintHandlers.GetApplicationCapabilitiesAsync);
        group.MapPost("/apps/{appId}/report.{format}", MapFishPrintHandlers.CreateReportAsync)
            .Accepts<MapFishPrintSpec>("application/json")
            .Produces<FileStreamHttpResult>(StatusCodes.Status200OK, "application/pdf");

        return endpoints;
    }
}
