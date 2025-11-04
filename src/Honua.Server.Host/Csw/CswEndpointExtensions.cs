// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Honua.Server.Host.Csw;

/// <summary>
/// Endpoint extensions for CSW 2.0.2 (Catalog Service for the Web).
/// </summary>
public static class CswEndpointExtensions
{
    public static IEndpointRouteBuilder MapCswEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var cswGroup = endpoints.MapGroup("/csw")
            .WithTags("CSW")
            .WithMetadata(new EndpointNameMetadata("CSW 2.0.2"))
            .RequireAuthorization("RequireViewer");
            // Rate limiting moved to YARP reverse proxy

        cswGroup.MapGet("", CswHandlers.HandleAsync)
            .WithName("CSW-GET")
            .WithSummary("CSW 2.0.2 GET request handler")
            .Produces(200, contentType: "application/xml")
            .Produces(400, contentType: "application/xml");

        cswGroup.MapPost("", CswHandlers.HandleAsync)
            .WithName("CSW-POST")
            .WithSummary("CSW 2.0.2 POST request handler")
            .Produces(200, contentType: "application/xml")
            .Produces(400, contentType: "application/xml");

        return endpoints;
    }
}
