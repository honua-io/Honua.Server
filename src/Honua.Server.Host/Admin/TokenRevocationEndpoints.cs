// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Auth;
using Honua.Server.Core.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Admin;

/// <summary>
/// Provides administrative endpoints for JWT token revocation.
/// Allows administrators to revoke individual tokens or all tokens for a user.
/// </summary>
internal static class TokenRevocationEndpoints
{
    public static RouteGroupBuilder MapTokenRevocationEndpoints(this WebApplication app)
    {
        Guard.NotNull(app);

        var group = app.MapGroup("/admin/auth");
        return MapTokenRevocationEndpointsCore(group);
    }

    public static RouteGroupBuilder MapTokenRevocationEndpoints(this RouteGroupBuilder group)
    {
        Guard.NotNull(group);

        return MapTokenRevocationEndpointsCore(group.MapGroup("/admin/auth"));
    }

    private static RouteGroupBuilder MapTokenRevocationEndpointsCore(RouteGroupBuilder group)
    {
        group.RequireAuthorization("RequireAdministrator");

        // POST /admin/auth/revoke-token - Revoke a specific token
        group.MapPost("/revoke-token", RevokeTokenAsync)
            .WithName("RevokeToken")
            .WithSummary("Revoke a specific JWT token")
            .WithDescription("Adds a token to the revocation blacklist, preventing it from being used for authentication. " +
                             "The token must include a 'jti' (JWT ID) claim.");

        // POST /admin/auth/revoke-user-tokens/{userId} - Revoke all tokens for a user
        group.MapPost("/revoke-user-tokens/{userId}", RevokeAllUserTokensAsync)
            .WithName("RevokeAllUserTokens")
            .WithSummary("Revoke all tokens for a specific user")
            .WithDescription("Revokes all current and future tokens for a user until they authenticate again. " +
                             "Useful for emergency lockouts or password resets.");

        // POST /admin/auth/cleanup-revocations - Cleanup expired revocations
        group.MapPost("/cleanup-revocations", CleanupRevocationsAsync)
            .WithName("CleanupExpiredRevocations")
            .WithSummary("Cleanup expired token revocations")
            .WithDescription("Removes expired token revocations from storage. Redis handles this automatically via TTL, " +
                             "but this endpoint can be used for manual cleanup and metrics.");

        return group;
    }

    private static async Task<IResult> RevokeTokenAsync(
        RevokeTokenRequest request,
        [FromServices] ITokenRevocationService revocationService,
        [FromServices] ISecurityAuditLogger auditLogger,
        [FromServices] ILogger<ITokenRevocationService> logger,
        ClaimsPrincipal user,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        try
        {
            // Validate request
            if (request.Token.IsNullOrWhiteSpace())
            {
                return ApiErrorResponse.Json.BadRequestResult("Token is required");
            }

            // Parse the JWT to extract the jti claim
            var handler = new JwtSecurityTokenHandler();
            JwtSecurityToken? jwtToken;

            try
            {
                // We don't validate the signature here - we just need to extract the jti
                jwtToken = handler.ReadJwtToken(request.Token);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to parse JWT token for revocation");
                return ApiErrorResponse.Json.BadRequestResult("Invalid token format");
            }

            // Extract jti claim
            var jti = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
            if (jti.IsNullOrWhiteSpace())
            {
                return ApiErrorResponse.Json.BadRequestResult("Token does not contain a 'jti' claim");
            }

            // Extract expiration
            var exp = jwtToken.ValidTo;
            if (exp < DateTime.UtcNow)
            {
                return Results.BadRequest(new
                {
                    error = "Token is already expired",
                    expiredAt = exp.ToString("O")
                });
            }

            // Extract subject for audit logging
            var subject = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value ?? "unknown";

            // Revoke the token
            var reason = request.Reason ?? "Manual revocation by administrator";
            await revocationService.RevokeTokenAsync(jti, exp, reason, cancellationToken).ConfigureAwait(false);

            // Audit log the revocation
            var adminUsername = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? "unknown";
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

            auditLogger.LogAdminOperation(
                operation: "revoke_token",
                username: adminUsername,
                resourceType: "token",
                resourceId: subject,
                ipAddress: ipAddress);

            logger.LogInformation(
                "Token revoked by administrator - AdminUser={AdminUser}, TokenSubject={TokenSubject}, Reason={Reason}",
                adminUsername, subject, reason);

            return Results.Ok(new
            {
                status = "revoked",
                jti = jti[..Math.Min(8, jti.Length)] + "...", // Partial jti for confirmation
                subject,
                expiresAt = exp.ToString("O"),
                reason,
                message = "Token has been successfully revoked"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to revoke token");
            return Results.Problem(
                detail: "An error occurred while revoking the token",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> RevokeAllUserTokensAsync(
        string userId,
        RevokeUserTokensRequest request,
        [FromServices] ITokenRevocationService revocationService,
        [FromServices] ISecurityAuditLogger auditLogger,
        [FromServices] ILogger<ITokenRevocationService> logger,
        ClaimsPrincipal user,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        try
        {
            if (userId.IsNullOrWhiteSpace())
            {
                return ApiErrorResponse.Json.BadRequestResult("User ID is required");
            }

            var reason = request.Reason ?? "All user tokens revoked by administrator";

            // Revoke all tokens for the user
            await revocationService.RevokeAllUserTokensAsync(userId, reason, cancellationToken).ConfigureAwait(false);

            // Audit log the revocation
            var adminUsername = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? "unknown";
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

            auditLogger.LogAdminOperation(
                operation: "revoke_all_user_tokens",
                username: adminUsername,
                resourceType: "user",
                resourceId: userId,
                ipAddress: ipAddress);

            logger.LogWarning(
                "All user tokens revoked by administrator - AdminUser={AdminUser}, TargetUser={TargetUser}, Reason={Reason}",
                adminUsername, userId, reason);

            return Results.Ok(new
            {
                status = "revoked",
                userId,
                reason,
                message = $"All tokens for user '{userId}' have been successfully revoked"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to revoke all user tokens - UserId={UserId}", userId);
            return Results.Problem(
                detail: "An error occurred while revoking user tokens",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> CleanupRevocationsAsync(
        [FromServices] ITokenRevocationService revocationService,
        [FromServices] ILogger<ITokenRevocationService> logger,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        try
        {
            var adminUsername = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? "unknown";

            var cleanedCount = await revocationService.CleanupExpiredRevocationsAsync(cancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation(
                "Revocation cleanup completed by administrator - AdminUser={AdminUser}, CleanedCount={CleanedCount}",
                adminUsername, cleanedCount);

            return Results.Ok(new
            {
                status = "completed",
                cleanedCount,
                message = $"Cleaned up {cleanedCount} expired revocation(s). Note: Redis handles cleanup automatically via TTL."
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to cleanup expired revocations");
            return Results.Problem(
                detail: "An error occurred during revocation cleanup",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private sealed record RevokeTokenRequest
    {
        [Required]
        public string Token { get; init; } = string.Empty;

        public string? Reason { get; init; }
    }

    private sealed record RevokeUserTokensRequest
    {
        public string? Reason { get; init; }
    }
}
