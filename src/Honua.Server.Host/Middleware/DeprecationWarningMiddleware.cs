// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Middleware;

/// <summary>
/// Middleware that adds deprecation headers to responses for deprecated API versions.
/// </summary>
/// <remarks>
/// This middleware implements RFC 8594 (The Sunset HTTP Header Field) and adds
/// deprecation information to responses for API versions that are being phased out.
///
/// Headers added for deprecated versions:
/// - Deprecation: true (indicates the resource is deprecated)
/// - Sunset: [date] (RFC 7231 date when the version will be removed)
/// - Link: [url]; rel="deprecation" (link to deprecation documentation)
///
/// Configuration example in appsettings.json:
/// {
///   "ApiVersioning": {
///     "DeprecationWarnings": {
///       "v1": "2026-12-31T23:59:59Z",
///       "v2": null
///     },
///     "DeprecationDocumentationUrl": "https://docs.honua.io/api/deprecation"
///   }
/// }
/// </remarks>
public sealed class DeprecationWarningMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DeprecationWarningMiddleware> _logger;
    private readonly IReadOnlyDictionary<string, string> _deprecations;
    private readonly string? _deprecationDocUrl;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeprecationWarningMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public DeprecationWarningMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<DeprecationWarningMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        // Load deprecation configuration
        var deprecationConfig = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var deprecationSection = configuration.GetSection("ApiVersioning:DeprecationWarnings");

        if (deprecationSection.Exists())
        {
            foreach (var child in deprecationSection.GetChildren())
            {
                var sunsetDate = child.Value;
                if (!string.IsNullOrWhiteSpace(sunsetDate))
                {
                    deprecationConfig[child.Key] = sunsetDate;
                }
            }
        }

        _deprecations = deprecationConfig;
        _deprecationDocUrl = configuration.GetValue<string>("ApiVersioning:DeprecationDocumentationUrl");

        if (_deprecations.Count > 0)
        {
            _logger.LogInformation(
                "Deprecation warning middleware configured with {Count} deprecated version(s): {Versions}",
                _deprecations.Count,
                string.Join(", ", _deprecations.Keys));
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

        // Extract version from context (set by ApiVersionMiddleware)
        var version = context.Items[ApiVersionMiddleware.ApiVersionContextKey] as string;

        // Check if this version is deprecated
        if (version != null && _deprecations.TryGetValue(version, out var sunsetDate))
        {
            // Add deprecation headers before the response starts
            context.Response.OnStarting(() =>
            {
                // RFC 8594: Deprecation header
                context.Response.Headers["Deprecation"] = "true";

                // RFC 7231: Sunset header (date when the version will be removed)
                if (!string.IsNullOrWhiteSpace(sunsetDate))
                {
                    context.Response.Headers["Sunset"] = sunsetDate;
                }

                // RFC 8288: Link header with deprecation documentation
                if (!string.IsNullOrWhiteSpace(_deprecationDocUrl))
                {
                    context.Response.Headers["Link"] = $"<{_deprecationDocUrl}>; rel=\"deprecation\"";
                }

                _logger.LogWarning(
                    "Deprecated API version {Version} used. Path: {Path}. Sunset date: {SunsetDate}",
                    version,
                    context.Request.Path,
                    sunsetDate ?? "not set");

                return Task.CompletedTask;
            });
        }

        await _next(context).ConfigureAwait(false);
    }
}

/// <summary>
/// Extension methods for registering the DeprecationWarningMiddleware.
/// </summary>
public static class DeprecationWarningMiddlewareExtensions
{
    /// <summary>
    /// Adds the deprecation warning middleware to the pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for method chaining.</returns>
    /// <remarks>
    /// This middleware should be registered after ApiVersionMiddleware
    /// since it depends on the version being extracted and stored in HttpContext.Items.
    /// </remarks>
    public static IApplicationBuilder UseDeprecationWarnings(this IApplicationBuilder app)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }

        return app.UseMiddleware<DeprecationWarningMiddleware>();
    }
}
