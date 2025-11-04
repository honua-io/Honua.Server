// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Security;

/// <summary>
/// Endpoints for managing CSRF tokens.
/// Provides token generation for browser-based clients.
/// </summary>
public static class CsrfTokenEndpoints
{
    /// <summary>
    /// Maps CSRF token endpoints to the application.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The endpoint route builder for method chaining.</returns>
    public static IEndpointRouteBuilder MapCsrfTokenEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var csrfGroup = endpoints.MapGroup("/api/security")
            .WithTags("Security");

        // GET /api/security/csrf-token - Get a new CSRF token
        // SECURITY FIX: Requires authenticated session and validates origin to prevent token leakage
        csrfGroup.MapGet("/csrf-token", GetCsrfToken)
            .WithName("GetCsrfToken")
            .WithSummary("Get CSRF token for authenticated browser clients")
            .WithDescription(
                "Retrieves a CSRF token that must be included in the X-CSRF-Token header " +
                "for all state-changing requests (POST, PUT, DELETE, PATCH). " +
                "The token is automatically included in a cookie and should be sent with subsequent requests. " +
                "Requires an authenticated session and same-origin request.")
            .Produces<CsrfTokenResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .RequireAuthorization();

        return endpoints;
    }

    /// <summary>
    /// Generates and returns a CSRF token for the current request.
    /// SECURITY FIX: Validates origin/referer headers to prevent token leakage to malicious sites.
    /// </summary>
    private static IResult GetCsrfToken(
        HttpContext context,
        IAntiforgery antiforgery,
        ILogger<Program> logger)
    {
        try
        {
            // SECURITY FIX: Validate same-origin request to prevent cross-origin token leakage
            if (!ValidateSameOrigin(context, logger))
            {
                logger.LogWarning(
                    "CSRF token request rejected: Invalid origin/referer from {RemoteIp}, User={User}",
                    context.Connection.RemoteIpAddress,
                    context.User?.Identity?.Name ?? "anonymous");

                return Results.Problem(
                    title: "Invalid Origin",
                    detail: "CSRF tokens can only be issued to same-origin requests.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            // Generate CSRF token and set cookie
            var tokens = antiforgery.GetAndStoreTokens(context);

            if (tokens.RequestToken == null)
            {
                logger.LogError("Failed to generate CSRF token");
                return Results.Problem(
                    title: "CSRF Token Generation Failed",
                    detail: "Unable to generate CSRF token. Please try again.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            logger.LogDebug(
                "CSRF token generated for user {User} from {RemoteIp}",
                context.User?.Identity?.Name ?? "anonymous",
                context.Connection.RemoteIpAddress);

            // Return token in response body
            // The cookie is automatically set by the antiforgery middleware
            return Results.Ok(new CsrfTokenResponse
            {
                Token = tokens.RequestToken,
                HeaderName = "X-CSRF-Token",
                CookieName = "__Host-X-CSRF-Token",
                ExpiresIn = 3600 // 1 hour (approximate)
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating CSRF token");
            return Results.Problem(
                title: "CSRF Token Generation Error",
                detail: "An error occurred while generating the CSRF token.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Validates that the request comes from the same origin by checking Origin or Referer headers.
    /// Prevents CSRF token leakage to attacker-controlled domains.
    /// </summary>
    private static bool ValidateSameOrigin(HttpContext context, ILogger logger)
    {
        var request = context.Request;

        // BUG FIX #20: CSRF origin check ignores trusted proxy metadata
        // Use RequestLinkHelper to resolve effective scheme/host that respects X-Forwarded-* headers
        var expectedScheme = RequestLinkHelper.GetEffectiveScheme(request);
        var expectedHost = RequestLinkHelper.GetEffectiveHost(request);
        var expectedOrigin = $"{expectedScheme}://{expectedHost}";

        // Check Origin header first (preferred for CORS requests)
        if (request.Headers.TryGetValue("Origin", out var originHeader))
        {
            var origin = originHeader.ToString();

            if (string.IsNullOrWhiteSpace(origin))
            {
                logger.LogWarning("Empty Origin header received");
                return false;
            }

            // Compare origins (case-insensitive)
            if (origin.Equals(expectedOrigin, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            logger.LogWarning(
                "Origin mismatch: expected={Expected}, received={Received}",
                expectedOrigin,
                origin);
            return false;
        }

        // Fallback to Referer header if Origin is not present
        if (request.Headers.TryGetValue("Referer", out var refererHeader))
        {
            var referer = refererHeader.ToString();

            if (string.IsNullOrWhiteSpace(referer))
            {
                logger.LogWarning("Empty Referer header received");
                return false;
            }

            // Parse referer URL and validate host matches
            if (Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
            {
                var refererOrigin = $"{refererUri.Scheme}://{refererUri.Authority}";

                if (refererOrigin.Equals(expectedOrigin, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                logger.LogWarning(
                    "Referer origin mismatch: expected={Expected}, received={Received}",
                    expectedOrigin,
                    refererOrigin);
                return false;
            }

            logger.LogWarning("Invalid Referer URL format: {Referer}", referer);
            return false;
        }

        // Neither Origin nor Referer header present - reject for security
        logger.LogWarning("Neither Origin nor Referer header present in CSRF token request");
        return false;
    }
}

/// <summary>
/// Response model for CSRF token endpoint.
/// </summary>
public sealed class CsrfTokenResponse
{
    /// <summary>
    /// The CSRF token value that should be included in the X-CSRF-Token header.
    /// </summary>
    public required string Token { get; init; }

    /// <summary>
    /// The name of the header where the token should be included.
    /// </summary>
    public required string HeaderName { get; init; }

    /// <summary>
    /// The name of the cookie that stores the token.
    /// </summary>
    public required string CookieName { get; init; }

    /// <summary>
    /// Token expiration time in seconds.
    /// </summary>
    public int ExpiresIn { get; init; }
}
