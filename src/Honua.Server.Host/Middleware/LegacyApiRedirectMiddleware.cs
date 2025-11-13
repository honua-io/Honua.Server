// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Honua.Server.Core.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Middleware;

/// <summary>
/// Middleware that redirects legacy (non-versioned) API URLs to their versioned equivalents.
/// </summary>
/// <remarks>
/// This middleware provides backward compatibility for existing clients using non-versioned URLs.
/// For example:
/// - /ogc/collections → /v1/ogc/collections (308 Permanent Redirect)
/// - /stac → /v1/stac (308 Permanent Redirect)
/// - /api/admin/ingestion → /v1/api/admin/ingestion (308 Permanent Redirect)
///
/// The middleware can be enabled/disabled via configuration for gradual migration:
/// - Phase 1: Enable with 308 redirects (current)
/// - Phase 2: Add deprecation warnings
/// - Phase 3: Disable to require versioned URLs
///
/// Only processes requests to known API paths that lack version prefixes.
/// Other requests (like health checks, root, etc.) are passed through unchanged.
/// </remarks>
public sealed class LegacyApiRedirectMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<LegacyApiRedirectMiddleware> logger;
    private readonly bool enabled;
    private readonly string targetVersion;

    // Regex to check if a path already has a version: /v1/, /v2/, etc.
    private static readonly Regex HasVersionPattern = new(@"^/v\d+(/|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Known API path prefixes that should be versioned
    private static readonly string[] KnownApiPaths = new[]
    {
        "/ogc",
        "/stac",
        "/api",
        "/records",
        "/carto",
        "/wms",
        "/wfs",
        "/wmts",
        "/wcs",
        "/csw",
        "/print",
        "/raster",
        "/openrosa",
        "/admin"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="LegacyApiRedirectMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public LegacyApiRedirectMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<LegacyApiRedirectMiddleware> logger)
    {
        this.next = next ?? throw new ArgumentNullException(nameof(next));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        // Check if legacy URL support is enabled (default: true for backward compatibility)
        this.enabled = configuration.GetValue("ApiVersioning:AllowLegacyUrls", true);

        // Get the target version for redirects (default: v1)
        this.targetVersion = configuration.GetValue("ApiVersioning:LegacyRedirectVersion", ApiVersioning.DefaultVersion)
            ?? ApiVersioning.DefaultVersion;

        if (_enabled)
        {
            this.logger.LogInformation(
                "Legacy API redirect middleware enabled. Non-versioned URLs will redirect to {Version}",
                _targetVersion);
        }
        else
        {
            this.logger.LogInformation("Legacy API redirect middleware disabled. Non-versioned URLs will return 404.");
        }
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

        // Skip if middleware is disabled
        if (!_enabled)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var path = context.Request.Path.Value;

        // Skip if path is null/empty or already has a version
        if (string.IsNullOrEmpty(path) || HasVersion(path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // Skip if this is not a known API path
        if (!IsKnownApiPath(path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // Build the versioned URL
        var newPath = $"/{_targetVersion}{path}";
        var queryString = context.Request.QueryString.ToUriComponent();
        var newLocation = newPath + queryString;

        this.logger.LogInformation(
            "Redirecting legacy API URL {OriginalPath} to versioned URL {NewPath}",
            path,
            newPath);

        // Return 308 Permanent Redirect with RFC 7807 Problem Details
        context.Response.StatusCode = StatusCodes.Status308PermanentRedirect;
        context.Response.Headers["Location"] = newLocation;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc7538",
            title = "API Endpoint Moved Permanently",
            status = StatusCodes.Status308PermanentRedirect,
            detail = $"This endpoint has moved to {newPath}. Please update your client to use versioned URLs. " +
                     "Non-versioned URLs are deprecated and will be removed in a future release.",
            instance = path,
            newLocation,
            deprecationInfo = "https://docs.honua.io/api/versioning"
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if a path already has a version prefix.
    /// </summary>
    /// <param name="path">The request path.</param>
    /// <returns>True if the path has a version, false otherwise.</returns>
    private static bool HasVersion(string path)
    {
        return HasVersionPattern.IsMatch(path);
    }

    /// <summary>
    /// Checks if a path is a known API path that should be versioned.
    /// </summary>
    /// <param name="path">The request path.</param>
    /// <returns>True if the path is a known API path, false otherwise.</returns>
    private static bool IsKnownApiPath(string path)
    {
        foreach (var knownPath in KnownApiPaths)
        {
            if (path.StartsWith(knownPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Extension methods for registering the LegacyApiRedirectMiddleware.
/// </summary>
public static class LegacyApiRedirectMiddlewareExtensions
{
    /// <summary>
    /// Adds the legacy API redirect middleware to the pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for method chaining.</returns>
    /// <remarks>
    /// This middleware should be registered early in the pipeline, before routing,
    /// to intercept requests before they reach the endpoint routing system.
    /// </remarks>
    public static IApplicationBuilder UseLegacyApiRedirect(this IApplicationBuilder app)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }

        return app.UseMiddleware<LegacyApiRedirectMiddleware>();
    }
}
