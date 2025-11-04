// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Globalization;
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.Text.Json.Serialization;
using Honua.Server.Host.Middleware;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Authentication;

/// <summary>
/// ArcGIS-compatible token service endpoints used by ArcGIS Pro and other Esri desktop clients.
/// </summary>
internal static class ArcGisTokenEndpoints
{
    public static IEndpointRouteBuilder MapArcGisTokenEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tokens")
            .WithTags("Token Exchange")
            .WithDescription("Compatibility token endpoint for desktop GIS clients.");

        group.MapPost("/generate", GenerateTokenAsync)
            .AllowAnonymous()
            .WithName("GenerateToken")
            .WithSummary("Generates an ArcGIS-compatible access token.")
            .Produces<ArcGisTokenResponse>(StatusCodes.Status200OK)
            .Produces<ArcGisTokenErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ArcGisTokenErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ArcGisTokenErrorResponse>(StatusCodes.Status423Locked)
            .Produces<ArcGisTokenErrorResponse>(StatusCodes.Status429TooManyRequests)
            .Produces<ArcGisTokenErrorResponse>(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<IResult> GenerateTokenAsync(
        HttpContext context,
        [FromServices] ILocalAuthenticationService authenticationService,
        [FromServices] ILocalTokenService tokenService,
        [FromServices] IOptionsMonitor<HonuaAuthenticationOptions> authOptions,
        [FromServices] ILoggerFactory loggerFactory,
        [FromServices] ISecurityAuditLogger auditLogger,
        CancellationToken cancellationToken)
    {
        var options = authOptions.CurrentValue;
        var logger = loggerFactory.CreateLogger("ArcGisTokenService");

        if (options.Mode != HonuaAuthenticationOptions.AuthenticationMode.Local)
        {
            return BuildError(StatusCodes.Status503ServiceUnavailable, 499, "Token service is not enabled on this server.");
        }

        if (!context.Request.IsHttps)
        {
            logger.LogWarning("ArcGIS token request rejected: insecure transport from {RemoteIp}.", context.Connection.RemoteIpAddress);
            return BuildError(StatusCodes.Status400BadRequest, 499, "HTTPS is required for token requests.");
        }

        // SECURITY FIX: Reject credentials from query string to prevent URL logging exposure
        var (username, password, requestedMinutes, format, credentialsInQuery) = await ParseRequestAsync(context, cancellationToken).ConfigureAwait(false);

        if (credentialsInQuery)
        {
            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            logger.LogWarning(
                "ArcGIS token request rejected: credentials in query string from {RemoteIp}",
                ipAddress);
            auditLogger.LogSuspiciousActivity(
                "credentials_in_query_string",
                username.IsNullOrWhiteSpace() ? null : username,
                ipAddress,
                "Attempted to send credentials via query parameters instead of POST body");
            return BuildError(StatusCodes.Status400BadRequest, 498, "Credentials must be sent in the request body, not in query parameters.");
        }

        if (username.IsNullOrWhiteSpace() || password.IsNullOrWhiteSpace())
        {
            return BuildError(StatusCodes.Status400BadRequest, 498, "Username and password are required.");
        }

        var ipAddress2 = context.Connection.RemoteIpAddress?.ToString();
        var userAgent = context.Request.Headers.UserAgent.ToString();

        var result = await authenticationService.AuthenticateAsync(username, password, cancellationToken).ConfigureAwait(false);

        switch (result.Status)
        {
            case LocalAuthenticationStatus.InvalidCredentials:
                logger.LogInformation("ArcGIS token request failed for {Username}: invalid credentials.", username);
                auditLogger.LogLoginFailure(username, ipAddress2, userAgent, "invalid_credentials");
                return BuildAuthenticationFailure(StatusCodes.Status401Unauthorized);
            case LocalAuthenticationStatus.Disabled:
                logger.LogWarning("ArcGIS token request failed for {Username}: account disabled.", username);
                auditLogger.LogLoginFailure(username, ipAddress2, userAgent, "account_disabled");
                return BuildAuthenticationFailure(StatusCodes.Status401Unauthorized);
            case LocalAuthenticationStatus.NotConfigured:
                logger.LogWarning("ArcGIS token request attempted while local authentication is not configured.");
                return BuildError(StatusCodes.Status503ServiceUnavailable, 499, "Token service is temporarily unavailable.");
            case LocalAuthenticationStatus.LockedOut:
                var lockedUntil = result.LockedUntil?.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) ?? "unknown";
                logger.LogWarning("ArcGIS token request failed: account {Username} locked until {LockedUntil}.", username, lockedUntil);
                auditLogger.LogAccountLockout(username, ipAddress2, result.LockedUntil ?? DateTimeOffset.UtcNow);
                return BuildAuthenticationFailure(StatusCodes.Status423Locked);
            case LocalAuthenticationStatus.PasswordExpired:
                logger.LogWarning("ArcGIS token request failed for {Username}: password expired.", username);
                auditLogger.LogLoginFailure(username, ipAddress2, userAgent, "password_expired");
                return BuildAuthenticationFailure(StatusCodes.Status401Unauthorized);
        }

        if (result.UserId.IsNullOrEmpty())
        {
            logger.LogError("ArcGIS token request failed: authenticated result missing user identifier.");
            return BuildError(StatusCodes.Status500InternalServerError, 500, "Unable to generate token.");
        }

        var configuredLifetime = options.Local.SessionLifetime <= TimeSpan.Zero
            ? TimeSpan.FromMinutes(30)
            : options.Local.SessionLifetime;

        var requestedLifetime = ResolveRequestedLifetime(requestedMinutes, configuredLifetime);

        var token = await tokenService.CreateTokenAsync(
            result.UserId,
            result.Roles,
            requestedLifetime,
            cancellationToken).ConfigureAwait(false);

        var expiresAt = DateTimeOffset.UtcNow.Add(requestedLifetime);

        var response = new ArcGisTokenResponse(
            token,
            expiresAt.ToUnixTimeMilliseconds(),
            context.Request.IsHttps,
            result.Status == LocalAuthenticationStatus.PasswordExpiresSoon && result.PasswordExpiresAt.HasValue
                ? new ArcGisPasswordInfo(result.PasswordExpiresAt.Value.ToUniversalTime(), result.DaysUntilExpiration)
                : null);

        var jsonOptions = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = string.Equals(format, "pjson", StringComparison.OrdinalIgnoreCase)
        };

        // SECURITY FIX: Log successful authentication for audit trail
        auditLogger.LogLoginSuccess(username, ipAddress2, userAgent);
        logger.LogInformation("ArcGIS token issued for {Username}; expires at {ExpiresAt:o}.", username, expiresAt);
        return Results.Json(response, jsonOptions);
    }

    private static async Task<(string Username, string Password, double? ExpirationMinutes, string Format, bool CredentialsInQuery)> ParseRequestAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        IFormCollection? form = null;
        bool credentialsInQuery = false;

        if (context.Request.HasFormContentType)
        {
            form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        }

        string GetValue(string key, bool isCredential = false)
        {
            if (form is not null)
            {
                var formValue = form[key];
                if (!StringValues.IsNullOrEmpty(formValue))
                {
                    return formValue[0] ?? string.Empty;
                }
            }

            // SECURITY FIX: Check if credentials are in query string
            var queryValue = context.Request.Query[key];
            if (!StringValues.IsNullOrEmpty(queryValue))
            {
                if (isCredential)
                {
                    credentialsInQuery = true;
                }
                return queryValue[0] ?? string.Empty;
            }

            return string.Empty;
        }

        var username = GetValue("username", isCredential: true);
        var password = GetValue("password", isCredential: true);
        var format = GetValue("f");

        double? expiration = null;
        var rawExpiration = GetValue("expiration");
        if (rawExpiration.HasValue() &&
            rawExpiration.TryParseDoubleStrict(out var minutes))
        {
            expiration = minutes;
        }

        return (username, password, expiration, format, credentialsInQuery);
    }

    private static TimeSpan ResolveRequestedLifetime(double? requestedMinutes, TimeSpan configuredLifetime)
    {
        if (!requestedMinutes.HasValue || requestedMinutes.Value <= 0)
        {
            return configuredLifetime;
        }

        var minimum = TimeSpan.FromMinutes(1);
        var requested = TimeSpan.FromMinutes(requestedMinutes.Value);

        if (requested < minimum)
        {
            requested = minimum;
        }

        if (configuredLifetime > TimeSpan.Zero && requested > configuredLifetime)
        {
            requested = configuredLifetime;
        }

        return requested;
    }

    private static IResult BuildAuthenticationFailure(int statusCode)
    {
        return BuildError(statusCode, 498, "Authentication failed.");
    }

    private static IResult BuildError(int httpStatusCode, int code, string message, params string[] details)
    {
        var payload = new ArcGisTokenErrorResponse(new ArcGisTokenError(code, message, details ?? Array.Empty<string>()));
        return Results.Json(payload, statusCode: httpStatusCode);
    }

    private sealed record ArcGisTokenResponse(
        [property: JsonPropertyName("token")] string Token,
        [property: JsonPropertyName("expires")] long Expires,
        [property: JsonPropertyName("ssl")] bool Ssl,
        [property: JsonPropertyName("passwordInfo")] ArcGisPasswordInfo? PasswordInfo);

    private sealed record ArcGisPasswordInfo(
        [property: JsonPropertyName("expiresAt")] DateTimeOffset ExpiresAt,
        [property: JsonPropertyName("daysRemaining")] int? DaysRemaining);

    private sealed record ArcGisTokenErrorResponse([property: JsonPropertyName("error")] ArcGisTokenError Error);

    private sealed record ArcGisTokenError(
        [property: JsonPropertyName("code")] int Code,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("details")] IReadOnlyCollection<string> Details);
}
