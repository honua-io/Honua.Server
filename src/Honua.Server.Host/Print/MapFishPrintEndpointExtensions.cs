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
    public static IEndpointRouteBuilder MapMapFishPrint(this IEndpointRouteBuilder endpoints, string? prefix = null)
    {
        Guard.NotNull(endpoints);

        var endpointPrefix = string.IsNullOrEmpty(prefix) ? "" : $"{prefix}-";

        var group = endpoints.MapGroup("/print");
        group.RequireAuthorization("RequireViewer");

        group.MapGet("/apps.json", MapFishPrintHandlers.GetApplicationsAsync)
            .WithName($"{endpointPrefix}PrintGetApplications");
        group.MapGet("/apps/{appId}.json", MapFishPrintHandlers.GetApplicationCapabilitiesAsync)
            .WithName($"{endpointPrefix}PrintGetAppCapabilities");
        group.MapPost("/apps/{appId}/report.{format}", MapFishPrintHandlers.CreateReportAsync)
            .WithName($"{endpointPrefix}PrintCreateReport")
            .Accepts<MapFishPrintSpec>("application/json")
            .Produces<FileStreamHttpResult>(StatusCodes.Status200OK, "application/pdf");

        return endpoints;
    }
}
