// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Honua.Server.Core.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Middleware;

/// <summary>
/// Middleware that extracts and validates API version from request URLs.
/// </summary>
/// <remarks>
/// This middleware:
/// - Extracts version from URL path (e.g., /v1/ogc/collections)
/// - Validates the version is supported
/// - Stores the version in this.HttpContext.Items for downstream use
/// - Adds version to response headers
/// - Returns 400 Bad Request for unsupported versions
///
/// The middleware only processes requests that have a version in the path.
/// Requests without a version are passed through unchanged and handled by
/// the LegacyApiRedirectMiddleware.
/// </remarks>
public sealed class ApiVersionMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<ApiVersionMiddleware> logger;

    // Regex to match version pattern at the start of the path: /v1/, /v2/, etc.
    private static readonly Regex VersionPattern = new(@"^/(v\d+)(/|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Context item key for storing the API version.
    /// </summary>
    public const string ApiVersionContextKey = "ApiVersion";

    /// <summary>
    /// Response header name for API version.
    /// </summary>
    public const string ApiVersionHeaderName = "X-API-Version";

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiVersionMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger instance.</param>
    public ApiVersionMiddleware(RequestDelegate next, ILogger<ApiVersionMiddleware> logger)
    {
        this.next = next ?? throw new ArgumentNullException(nameof(next));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes the HTTP request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var path = context.Request.Path.Value;
        var version = ExtractVersion(path);

        // If a version is present in the path, validate it
        if (version != null)
        {
            if (!ApiVersioning.IsVersionSupported(version))
            {
                this.logger.LogWarning(
                    "Unsupported API version requested: {Version}. Path: {Path}",
                    version,
                    path);

                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Response.ContentType = "application/problem+json";

                await context.Response.WriteAsJsonAsync(new
                {
                    type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    title = "Unsupported API Version",
                    status = StatusCodes.Status400BadRequest,
                    detail = $"API version '{version}' is not supported. Supported versions: {string.Join(", ", ApiVersioning.SupportedVersions)}",
                    instance = path
                }).ConfigureAwait(false);

                return;
            }

            // Store version in HttpContext for later use by endpoints
            context.Items[ApiVersionContextKey] = version;

            this.logger.LogDebug("API version {Version} extracted from path: {Path}", version, path);
        }

        // Add version to response headers
        // Use the extracted version if present, otherwise use default version
        var responseVersion = version ?? ApiVersioning.DefaultVersion;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[ApiVersionHeaderName] = responseVersion;
            return Task.CompletedTask;
        });

        await _next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// Extracts the API version from a request path.
    /// </summary>
    /// <param name="path">The request path.</param>
    /// <returns>The version string if found (e.g., "v1"), otherwise null.</returns>
    private static string? ExtractVersion(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var match = VersionPattern.Match(path);
        return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
    }
}

/// <summary>
/// Extension methods for registering the ApiVersionMiddleware.
/// </summary>
public static class ApiVersionMiddlewareExtensions
{
    /// <summary>
    /// Adds the API version middleware to the pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for method chaining.</returns>
    public static IApplicationBuilder UseApiVersioning(this IApplicationBuilder app)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }

        return app.UseMiddleware<ApiVersionMiddleware>();
    }
}
