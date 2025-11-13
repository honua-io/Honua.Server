// <copyright file="WebhookSignatureMiddlewareExtensions.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.AlertReceiver.Middleware;

public static class WebhookSignatureMiddlewareExtensions
{
    /// <summary>
    /// Adds webhook signature validation middleware to the pipeline.
    /// Should be added before authorization and controller mapping.
    /// </summary>
    public static IApplicationBuilder UseWebhookSignatureValidation(
        this IApplicationBuilder app,
        PathString pathPrefix = default)
    {
        // If a path prefix is specified, only apply to those paths
        if (pathPrefix.HasValue)
        {
            return app.MapWhen(
                context => context.Request.Path.StartsWithSegments(pathPrefix),
                branch => branch.UseMiddleware<WebhookSignatureMiddleware>());
        }

        return app.UseMiddleware<WebhookSignatureMiddleware>();
    }
}
