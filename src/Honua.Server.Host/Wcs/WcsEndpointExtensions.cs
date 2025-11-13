// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Rendering;
using Honua.Server.Host.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Honua.Server.Host.Wcs;

/// <summary>
/// Endpoint extensions for WCS 2.0.1 (Web Coverage Service).
/// </summary>
public static class WcsEndpointExtensions
{
    public static IEndpointRouteBuilder MapWcsEndpoints(this IEndpointRouteBuilder endpoints, string? prefix = null)
    {
        var endpointPrefix = string.IsNullOrEmpty(prefix) ? "" : $"{prefix}-";

        var wcsGroup = endpoints.MapGroup("/wcs")
            .WithTags("WCS")
            .WithMetadata(new EndpointNameMetadata("WCS 2.0.1"))
            .RequireAuthorization("RequireViewer");
            // Rate limiting moved to YARP reverse proxy

        wcsGroup.MapGet("", WcsHandlers.HandleAsync)
            .WithName($"{endpointPrefix}WCS-GET")
            .WithSummary("WCS 2.0.1 GET request handler")
            .Produces(200, contentType: "application/xml")
            .Produces(200, contentType: "image/tiff")
            .Produces(200, contentType: "image/png")
            .Produces(400, contentType: "application/xml");

        wcsGroup.MapPost("", WcsHandlers.HandleAsync)
            .WithName($"{endpointPrefix}WCS-POST")
            .WithSummary("WCS 2.0.1 POST request handler")
            .Produces(200, contentType: "application/xml")
            .Produces(200, contentType: "image/tiff")
            .Produces(200, contentType: "image/png")
            .Produces(400, contentType: "application/xml");

        return endpoints;
    }
}
