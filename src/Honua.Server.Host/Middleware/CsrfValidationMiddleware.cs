// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.AspNetCore.Antiforgery;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Host.Middleware;

/// <summary>
/// Middleware that validates CSRF tokens for state-changing HTTP methods (POST, PUT, DELETE, PATCH).
/// Implements defense against Cross-Site Request Forgery (CSRF) attacks as recommended by OWASP.
/// </summary>
/// <remarks>
/// CSRF protection is automatically bypassed for:
/// - API key authenticated requests (X-API-Key header)
/// - Safe HTTP methods (GET, HEAD, OPTIONS, TRACE)
/// - Requests to excluded paths (e.g., /healthz, /metrics)
///
/// Browser clients should:
/// 1. Request a CSRF token from GET /api/csrf-token
/// 2. Include the token in X-CSRF-Token header for state-changing requests
/// 3. Ensure the CSRF cookie is sent with each request
/// </remarks>
public sealed class CsrfValidationMiddleware
{
    private readonly RequestDelegate next;
    private readonly IAntiforgery antiforgery;
    private readonly ILogger<CsrfValidationMiddleware> logger;
    private readonly ISecurityAuditLogger auditLogger;
    private readonly CsrfProtectionOptions options;

    // Safe HTTP methods that don't require CSRF protection (per RFC 7231)
    private static readonly string[] SafeMethods = { "GET", "HEAD", "OPTIONS", "TRACE" };

    public CsrfValidationMiddleware(
        RequestDelegate next,
        IAntiforgery antiforgery,
        ILogger<CsrfValidationMiddleware> logger,
        ISecurityAuditLogger auditLogger,
        CsrfProtectionOptions options)
    {
        this.next = Guard.NotNull(next);
        this.antiforgery = Guard.NotNull(antiforgery);
        this.logger = Guard.NotNull(logger);
        this.auditLogger = Guard.NotNull(auditLogger);
        this.options = Guard.NotNull(options);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        Guard.NotNull(context);

        // Skip CSRF validation if protection is disabled globally
        if (!this.options.Enabled)
        {
            await this.next(context).ConfigureAwait(false);
            return;
        }

        // Skip validation for safe HTTP methods
        if (IsSafeMethod(context.Request.Method))
        {
            await this.next(context).ConfigureAwait(false);
            return;
        }

        // Skip validation for excluded paths (health checks, metrics, etc.)
        if (IsExcludedPath(context.Request.Path))
        {
            await this.next(context).ConfigureAwait(false);
            return;
        }

        // Skip validation for API key authenticated requests (non-browser clients)
        if (IsApiKeyAuthenticated(context))
        {
            this.logger.LogDebug(
                "CSRF validation skipped for API key authenticated request to {Path}",
                context.Request.Path);
            await this.next(context).ConfigureAwait(false);
            return;
        }

        // Validate CSRF token for state-changing requests from browser clients
        try
        {
            await this.antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);

            this.logger.LogDebug(
                "CSRF token validated successfully for {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            await this.next(context).ConfigureAwait(false);
        }
        catch (AntiforgeryValidationException ex)
        {
            this.logger.LogWarning(
                ex,
                "CSRF token validation failed for {Method} {Path} from {RemoteIp}. User={User}",
                context.Request.Method,
                context.Request.Path,
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                context.User?.Identity?.Name ?? "anonymous");

            this.auditLogger.LogSuspiciousActivity(
                "csrf_validation_failure",
                context.User?.Identity?.Name,
                context.Connection.RemoteIpAddress?.ToString(),
                $"CSRF validation failed for {context.Request.Method} {context.Request.Path}");

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/problem+json";

            var problemDetails = new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                title = "CSRF Token Validation Failed",
                status = 403,
                detail = "The CSRF token is missing, invalid, or expired. Please refresh the page and try again.",
                instance = context.Request.Path.Value,
                traceId = context.TraceIdentifier
            };

            await context.Response.WriteAsJsonAsync(problemDetails).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this.logger.LogError(
                ex,
                "Unexpected error in CSRF validation middleware for {Method} {Path} from {RemoteIp}",
                context.Request.Method,
                context.Request.Path,
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown");

            // Re-throw to let the exception handling middleware deal with it
            throw;
        }
    }

    /// <summary>
    /// Determines if the HTTP method is safe and doesn't require CSRF protection.
    /// </summary>
    private static bool IsSafeMethod(string method)
    {
        return Array.Exists(SafeMethods, m => m.Equals(method, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Determines if the request is from an API key authenticated client.
    /// API key authentication indicates a non-browser client (e.g., service-to-service).
    /// </summary>
    /// <remarks>
    /// SECURITY: Only header-based API key authentication is supported.
    /// Query parameter API keys are insecure as they:
    /// - Appear in server logs, proxy logs, and browser history
    /// - Can be cached by intermediaries
    /// - Are visible in URLs shared or bookmarked by users
    /// - May be leaked via Referer headers
    /// </remarks>
    private static bool IsApiKeyAuthenticated(HttpContext context)
    {
        // SECURITY FIX: Only accept X-API-Key header (query parameters removed)
        // Query parameter API keys are a security vulnerability
        if (context.Request.Headers.ContainsKey("X-API-Key"))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines if the request path should be excluded from CSRF validation.
    /// </summary>
    private bool IsExcludedPath(PathString path)
    {
        foreach (var excludedPath in this.options.ExcludedPaths)
        {
            if (path.StartsWithSegments(excludedPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Configuration options for CSRF protection.
/// </summary>
public sealed class CsrfProtectionOptions
{
    /// <summary>
    /// Enables or disables CSRF protection globally.
    /// Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Paths that should be excluded from CSRF validation.
    /// Typically includes health checks, metrics, and other infrastructure endpoints.
    /// </summary>
    public string[] ExcludedPaths { get; set; } = new[]
    {
        "/healthz",
        "/livez",
        "/readyz",
        "/metrics",
        "/swagger",
        "/api-docs",
        "/stac",     // STAC API endpoints (public geospatial catalog API)
        "/v1/stac",  // Versioned STAC API endpoints
        "/ogc",      // OGC API endpoints (public geospatial standards)
        "/records"   // OGC Records API endpoints (public catalog API)
    };

    /// <summary>
    /// Creates default CSRF protection options.
    /// </summary>
    public static CsrfProtectionOptions Default => new();
}

/// <summary>
/// Extension methods for adding CSRF validation middleware.
/// </summary>
public static class CsrfValidationMiddlewareExtensions
{
    /// <summary>
    /// Adds CSRF validation middleware to the application pipeline.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="options">Optional CSRF protection options.</param>
    /// <returns>The application builder for method chaining.</returns>
    public static IApplicationBuilder UseCsrfValidation(
        this IApplicationBuilder builder,
        CsrfProtectionOptions? options = null)
    {
        options ??= CsrfProtectionOptions.Default;
        return builder.UseMiddleware<CsrfValidationMiddleware>(options);
    }
}
