// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Host.Wmts;

/// <summary>
/// Extension methods for registering WMTS endpoints.
/// </summary>
internal static class WmtsEndpointExtensions
{
    public static IEndpointRouteBuilder MapWmtsEndpoints(this IEndpointRouteBuilder endpoints, string? prefix = null)
    {
        var endpointPrefix = string.IsNullOrEmpty(prefix) ? "" : $"{prefix}-";

        var wmtsGroup = endpoints.MapGroup("/wmts")
            .WithTags("WMTS")
            .RequireRateLimiting("OgcApiPolicy");

        // Only require authorization if authentication is enforced
        var configuration = endpoints.ServiceProvider.GetRequiredService<IConfiguration>();
        var authEnforced = configuration.GetValue<bool>("honua:authentication:enforce", true);

        if (authEnforced)
        {
            wmtsGroup.RequireAuthorization("RequireViewer");
        }

        wmtsGroup.MapGet("", WmtsHandlers.HandleAsync)
            .WithName($"{endpointPrefix}WMTS-GET");
        wmtsGroup.MapPost("", WmtsHandlers.HandleAsync)
            .WithName($"{endpointPrefix}WMTS-POST");

        return endpoints;
    }
}
