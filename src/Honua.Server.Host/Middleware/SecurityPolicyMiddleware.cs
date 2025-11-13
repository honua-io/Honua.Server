// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Authorization;

namespace Honua.Server.Host.Middleware;

/// <summary>
/// Middleware that enforces authorization policies globally and detects potentially
/// unsafe endpoints that should have authorization but don't.
/// </summary>
/// <remarks>
/// <para>
/// This middleware acts as a security fail-safe by:
/// </para>
/// <list type="bullet">
/// <item><description>Detecting endpoints that require authorization but are missing the [Authorize] attribute</description></item>
/// <item><description>Logging warnings for suspicious endpoints (admin routes, mutation operations)</description></item>
/// <item><description>Returning 403 Forbidden for potentially unsafe endpoints without explicit [AllowAnonymous]</description></item>
/// <item><description>Allowing endpoints with explicit [AllowAnonymous] to function normally</description></item>
/// </list>
/// <para>
/// This implements a defense-in-depth security strategy where sensitive routes are
/// protected even if developers forget to add authorization attributes.
/// </para>
/// </remarks>
/// <example>
/// Register in the middleware pipeline in Program.cs:
/// <code>
/// app.UseRouting();
/// app.UseAuthentication();
/// app.UseAuthorization();
/// app.UseSecurityPolicy(); // After UseAuthorization
/// app.MapControllers();
/// </code>
/// </example>
public class SecurityPolicyMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<SecurityPolicyMiddleware> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityPolicyMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">Logger for recording security policy violations.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="next"/> or <paramref name="logger"/> is null.
    /// </exception>
    public SecurityPolicyMiddleware(RequestDelegate next, ILogger<SecurityPolicyMiddleware> logger)
    {
        this.next = Guard.NotNull(next);
        this.logger = Guard.NotNull(logger);
    }

    /// <summary>
    /// Invokes the middleware to check endpoint authorization requirements.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method:
    /// <list type="number">
    /// <item><description>Retrieves the endpoint from the current request</description></item>
    /// <item><description>Checks if the endpoint has [Authorize] or [AllowAnonymous] attributes</description></item>
    /// <item><description>Determines if the route is protected (admin routes, mutation operations)</description></item>
    /// <item><description>Logs warnings and returns 403 for unsafe endpoints without proper authorization</description></item>
    /// <item><description>Allows the request to proceed if authorization is properly configured</description></item>
    /// </list>
    /// </remarks>
    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();

        // No endpoint resolved, let the request proceed (will likely result in 404)
        if (endpoint == null)
        {
            await _next(context);
            return;
        }

        // Check for authorization metadata
        var hasAuthorize = endpoint.Metadata.GetMetadata<IAuthorizeData>() != null;
        var hasAllowAnonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() != null;

        // If endpoint has explicit authorization or explicit anonymous access, allow it
        if (hasAuthorize || hasAllowAnonymous)
        {
            await _next(context);
            return;
        }

        var request = context.Request;

        if (RequiresAuthorization(request))
        {
            this.logger.LogWarning(
                "Security Policy Violation: Endpoint {Path} (Method: {Method}) appears to be a protected route but lacks [Authorize] or [AllowAnonymous] attributes. " +
                "Denying access as a security fail-safe. DisplayName: {DisplayName}",
                request.Path,
                request.Method,
                endpoint.DisplayName ?? "(unknown)");

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                title = "Access Denied",
                status = 403,
                detail = "This endpoint requires authentication. Please provide valid credentials.",
                instance = request.Path.ToString(),
                traceId = context.TraceIdentifier
            });
            return;
        }

        if (ShouldWarnMissingAuthorization(request))
        {
            this.logger.LogWarning(
                "Security policy warning: endpoint {Path} (Method: {Method}) has no explicit authorization metadata. " +
                "The request is permitted because the operation is read-only; annotate intent explicitly.",
                request.Path,
                request.Method);
        }

        await _next(context);
    }

    private static bool RequiresAuthorization(HttpRequest request)
    {
        var method = request.Method;
        var segments = GetPathSegments(request.Path);

        if (IsAdminRoute(segments) || IsControlPlaneRoute(segments))
        {
            return true;
        }

        if (IsApiAdminRoute(segments))
        {
            return true;
        }

        if (!HttpMethods.IsGet(method) &&
            !HttpMethods.IsHead(method) &&
            !HttpMethods.IsOptions(method) &&
            !IsMutationAllowListed(segments))
        {
            return true;
        }

        return false;
    }

    private static bool ShouldWarnMissingAuthorization(HttpRequest request)
    {
        if (RequiresAuthorization(request))
        {
            return false;
        }

        if (!HttpMethods.IsGet(request.Method))
        {
            return false;
        }

        var segments = GetPathSegments(request.Path);
        return segments.Length > 0 &&
               string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase);
    }

    private static string[] GetPathSegments(PathString path)
    {
        var value = path.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool IsAdminRoute(string[] segments) =>
        segments.Length > 0 && string.Equals(segments[0], "admin", StringComparison.OrdinalIgnoreCase);

    private static bool IsApiAdminRoute(string[] segments) =>
        segments.Length > 1 &&
        string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(segments[1], "admin", StringComparison.OrdinalIgnoreCase);

    private static bool IsControlPlaneRoute(string[] segments) =>
        segments.Length > 0 && string.Equals(segments[0], "control-plane", StringComparison.OrdinalIgnoreCase);

    private static bool IsMutationAllowListed(string[] segments)
    {
        if (segments.Length == 0)
        {
            return false;
        }

        var first = segments[0];
        return first.Equals("stac", StringComparison.OrdinalIgnoreCase) ||
               first.Equals("records", StringComparison.OrdinalIgnoreCase) ||
               first.Equals("ogc", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Extension methods for adding the security policy middleware to the application pipeline.
/// </summary>
public static class SecurityPolicyMiddlewareExtensions
{
    /// <summary>
    /// Adds the security policy middleware to the application pipeline.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The application builder for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// This middleware should be placed after UseRouting(), UseAuthentication(), and UseAuthorization()
    /// but before endpoint execution (MapControllers, UseEndpoints, etc.).
    /// </para>
    /// <para>
    /// Correct order:
    /// </para>
    /// <code>
    /// app.UseRouting();
    /// app.UseAuthentication();
    /// app.UseAuthorization();
    /// app.UseSecurityPolicy(); // Place here
    /// app.MapControllers();
    /// </code>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> is null.
    /// </exception>
    public static IApplicationBuilder UseSecurityPolicy(this IApplicationBuilder builder)
    {
        Guard.NotNull(builder);
        return builder.UseMiddleware<SecurityPolicyMiddleware>();
    }
}
