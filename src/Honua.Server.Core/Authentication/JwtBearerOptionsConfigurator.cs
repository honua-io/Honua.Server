// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Honua.Server.Core.Auth;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Primitives;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Authentication;

public sealed class JwtBearerOptionsConfigurator : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly IOptionsMonitor<HonuaAuthenticationOptions> _authOptions;
    private readonly ILocalSigningKeyProvider _signingKeyProvider;
    private readonly IServiceProvider _serviceProvider;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<JwtBearerOptionsConfigurator> _logger;

    public JwtBearerOptionsConfigurator(
        IOptionsMonitor<HonuaAuthenticationOptions> authOptions,
        ILocalSigningKeyProvider signingKeyProvider,
        IServiceProvider serviceProvider,
        IWebHostEnvironment environment,
        ILogger<JwtBearerOptionsConfigurator> logger)
    {
        _authOptions = authOptions ?? throw new ArgumentNullException(nameof(authOptions));
        _signingKeyProvider = signingKeyProvider ?? throw new ArgumentNullException(nameof(signingKeyProvider));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Configure(string? name, JwtBearerOptions options)
    {
        Configure(options);
    }

    public void Configure(JwtBearerOptions options)
    {
        Guard.NotNull(options);

        var current = _authOptions.CurrentValue;

        switch (current.Mode)
        {
            case HonuaAuthenticationOptions.AuthenticationMode.Local:
                ConfigureLocalMode(options);
                return;
            case HonuaAuthenticationOptions.AuthenticationMode.Oidc:
                ConfigureOidcMode(options, current);
                return;
            default:
                ConfigureQuickStart(options);
                return;
        }
    }

    private void ConfigureQuickStart(JwtBearerOptions options)
    {
        // SECURITY: Block QuickStart mode in Production environment
        if (_environment.IsProduction())
        {
            throw new InvalidOperationException(
                "QuickStart authentication mode is not allowed in Production environment. " +
                "QuickStart disables JWT validation and should ONLY be used for local development and testing. " +
                "Set HonuaAuthentication:Mode to 'Local' or 'Oidc' for production use. " +
                $"Current environment: {_environment.EnvironmentName}");
        }

        // Log prominent warning for non-production environments
        _logger.LogWarning(
            "⚠️  ⚠️  ⚠️  WARNING: QuickStart authentication mode is ENABLED  ⚠️  ⚠️  ⚠️\n" +
            "This mode disables JWT validation and should ONLY be used for local development and testing.\n" +
            "DO NOT use QuickStart mode in production or staging environments with real data.\n" +
            "Environment: {Environment}\n" +
            "To disable: Set HonuaAuthentication:Mode to 'Local' or 'Oidc' in configuration",
            _environment.EnvironmentName);

        // QuickStart reuses the secure local authentication configuration but relaxes enforcement.
        // This keeps token validation enabled while allowing anonymous access through authorization policies.
        ConfigureLocalMode(options);

        // QuickStart is intended for local development, so HTTPS metadata is not required.
        options.RequireHttpsMetadata = false;
    }

    private void ConfigureLocalMode(JwtBearerOptions options)
    {
        options.Authority = null;
        options.Audience = LocalAuthenticationDefaults.Audience;

        // SECURITY: Only disable HTTPS metadata requirement in Development
        options.RequireHttpsMetadata = !_environment.IsDevelopment();

        if (!options.RequireHttpsMetadata && !_environment.IsDevelopment())
        {
            _logger.LogWarning(
                "SECURITY WARNING: RequireHttpsMetadata is disabled in non-development environment. " +
                "Environment: {Environment}. This should only occur in development or testing.",
                _environment.EnvironmentName);
        }

        options.MapInboundClaims = false;

        var signingKey = _signingKeyProvider.GetSigningKey();
        var localOptions = _authOptions.CurrentValue.Local;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = LocalAuthenticationDefaults.Issuer,
            ValidateAudience = true,
            ValidAudience = LocalAuthenticationDefaults.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(signingKey),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = LocalAuthenticationDefaults.RoleClaimType,
            ClockSkew = localOptions.ClockSkew ?? TimeSpan.FromMinutes(5)
        };

        options.Events = BuildJwtBearerEvents(options.Events);
    }

    private void ConfigureOidcMode(JwtBearerOptions options, HonuaAuthenticationOptions current)
    {
        options.Authority = current.Jwt.Authority;
        options.Audience = current.Jwt.Audience;
        options.RequireHttpsMetadata = current.Jwt.RequireHttpsMetadata;
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = current.Jwt.Authority.HasValue(),
            ValidateAudience = current.Jwt.Audience.HasValue(),
            ValidAudience = current.Jwt.Audience.IsNullOrWhiteSpace() ? null : current.Jwt.Audience,
            NameClaimType = JwtRegisteredClaimNames.Sub,
        };

        if (current.Jwt.RoleClaimPath.HasValue())
        {
            options.TokenValidationParameters.RoleClaimType = current.Jwt.RoleClaimPath;
        }

        options.Events = BuildJwtBearerEvents(options.Events);
    }

    private JwtBearerEvents BuildJwtBearerEvents(JwtBearerEvents? existing)
    {
        var events = existing ?? new JwtBearerEvents();

        var onMessageReceived = events.OnMessageReceived;
        events.OnMessageReceived = async context =>
        {
            if (onMessageReceived is not null)
            {
                await onMessageReceived(context).ConfigureAwait(false);
                if (!context.Token.IsNullOrEmpty())
                {
                    return;
                }
            }

            await OnMessageReceivedAsync(context).ConfigureAwait(false);
        };

        var onTokenValidated = events.OnTokenValidated;
        events.OnTokenValidated = async context =>
        {
            if (onTokenValidated is not null)
            {
                await onTokenValidated(context).ConfigureAwait(false);
            }

            await OnTokenValidatedAsync(context).ConfigureAwait(false);
        };

        return events;
    }

    /// <summary>
    /// Validates that the token has not been revoked (both individual token and user-level revocation).
    /// Called after JWT signature and expiration validation.
    /// </summary>
    /// <remarks>
    /// SECURITY: This method enforces two levels of token revocation:
    /// 1. Individual token revocation - specific token IDs (jti claim) that have been revoked
    /// 2. User-level revocation - all tokens for a user issued before a certain timestamp
    ///
    /// User-level revocation is critical for scenarios like:
    /// - Password reset (invalidate all existing sessions)
    /// - Account compromise (revoke all tokens immediately)
    /// - Permission changes (force re-authentication with new claims)
    /// - User deletion/suspension (block all access)
    /// </remarks>
    private async Task OnTokenValidatedAsync(TokenValidatedContext context)
    {
        ILogger<JwtBearerOptionsConfigurator>? logger = null;
        try
        {
            logger = _serviceProvider.GetRequiredService<ILogger<JwtBearerOptionsConfigurator>>();
            var auditLogger = _serviceProvider.GetRequiredService<ISecurityAuditLogger>();
            var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString();

            // Get the JWT ID (jti) and user ID (sub) claims
            var jti = context.Principal?.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
            var userId = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            if (jti.IsNullOrWhiteSpace())
            {
                // Token doesn't have a jti claim - this shouldn't happen for tokens we issue
                logger.LogWarning(
                    "SECURITY_AUDIT: JWT token missing 'jti' claim - cannot check revocation status. UserId={UserId}, IP={IPAddress}",
                    userId ?? "unknown",
                    ipAddress ?? "unknown");

                // Allow token without jti for backward compatibility with older tokens
                return;
            }

            // Check if revocation service is configured
            var revocationService = _serviceProvider.GetService<ITokenRevocationService>();
            if (revocationService == null)
            {
                // Revocation service not configured - allow authentication
                logger.LogDebug("Token revocation service not configured - skipping revocation check for UserId={UserId}", userId ?? "unknown");
                return;
            }

            // SECURITY FIX: Check individual token revocation
            var isTokenRevoked = await revocationService.IsTokenRevokedAsync(jti, context.HttpContext.RequestAborted)
                .ConfigureAwait(false);

            if (isTokenRevoked)
            {
                logger.LogWarning(
                    "SECURITY_AUDIT: Rejected individually revoked token - JtiHash={JtiHash}, UserId={UserId}, IP={IPAddress}",
                    HashForLogging(jti),
                    userId ?? "unknown",
                    ipAddress ?? "unknown");

                auditLogger.LogUnauthorizedAccess(userId ?? "unknown", context.Request.Path, ipAddress, "token_revoked");

                context.Fail("This token has been revoked.");
                return;
            }

            // SECURITY FIX: Check user-level revocation (all tokens for this user)
            // This is critical for password resets, account compromises, and permission changes
            if (userId.HasValue())
            {
                var isUserRevoked = await IsUserTokenRevokedAsync(
                        revocationService,
                        userId,
                        context.Principal,
                        context.SecurityToken,
                        context.HttpContext.RequestAborted)
                    .ConfigureAwait(false);

                if (isUserRevoked)
                {
                    logger.LogWarning(
                        "SECURITY_AUDIT: Rejected token for revoked user - UserId={UserId}, IP={IPAddress}, JtiHash={JtiHash}",
                        userId,
                        ipAddress ?? "unknown",
                        HashForLogging(jti));

                    auditLogger.LogUnauthorizedAccess(userId, context.Request.Path, ipAddress, "user_tokens_revoked");

                    context.Fail("All tokens for this user have been revoked. Please sign in again.");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            logger ??= _serviceProvider.GetRequiredService<ILogger<JwtBearerOptionsConfigurator>>();
            logger.LogError(
                ex,
                "Error checking token revocation status. IP={IPAddress}, Path={Path}. Failing authentication for safety.",
                context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                context.Request.Path.Value ?? "unknown");

            // SECURITY: Fail-closed on errors to prevent bypassing revocation checks
            context.Fail("Unable to validate token revocation status.");
        }
    }

    /// <summary>
    /// Extracts tokens supplied via alternate transports (query string or Esri-specific headers).
    /// </summary>
    private Task OnMessageReceivedAsync(MessageReceivedContext context)
    {
        if (!context.Token.IsNullOrEmpty())
        {
            return Task.CompletedTask;
        }

        if (TryResolveTokenFromQuery(context.Request.Query, out var token))
        {
            context.Token = token;
            return Task.CompletedTask;
        }

        if (context.Request.Headers.TryGetValue("X-Esri-Authorization", out var header) &&
            !StringValues.IsNullOrEmpty(header))
        {
            var value = header[0]!;
            if (value.HasValue())
            {
                const string bearerPrefix = "Bearer ";
                context.Token = value.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
                    ? value[bearerPrefix.Length..].Trim()
                    : value.Trim();
            }
        }

        return Task.CompletedTask;
    }

    private static bool TryResolveTokenFromQuery(IQueryCollection query, out string token)
    {
        if (TryGetFirst(query, "token", out token))
        {
            return true;
        }

        if (TryGetFirst(query, "access_token", out token))
        {
            return true;
        }

        token = string.Empty;
        return false;
    }

    private static bool TryGetFirst(IQueryCollection query, string key, out string value)
    {
        var raw = query[key];
        if (StringValues.IsNullOrEmpty(raw))
        {
            value = string.Empty;
            return false;
        }

        value = raw[0]!;
        return value.HasValue();
    }

    /// <summary>
    /// Checks if all tokens for a specific user have been revoked.
    /// Compares the token's issued-at time (iat claim) against the user revocation timestamp.
    /// </summary>
    /// <param name="revocationService">The token revocation service.</param>
    /// <param name="userId">The user ID (sub claim).</param>
    /// <param name="principal">The claims principal containing token claims.</param>
    /// <param name="securityToken">The security token being validated.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the token was issued before the user's revocation timestamp, false otherwise.</returns>
    /// <remarks>
    /// This method checks if there's a user-level revocation timestamp stored in the revocation service.
    /// If found, any token issued before that timestamp is considered revoked.
    ///
    /// Implementation: The revocation service stores a timestamp when RevokeAllUserTokensAsync is called.
    /// We check the token's "iat" (issued-at) claim against this timestamp.
    /// </remarks>
    private async Task<bool> IsUserTokenRevokedAsync(
        ITokenRevocationService revocationService,
        string userId,
        ClaimsPrincipal? principal,
        SecurityToken? securityToken,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get the token's issued-at timestamp (iat claim)
            var iatClaim = principal?.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Iat)?.Value;
            DateTimeOffset? tokenIssuedAt = null;

            if (iatClaim.HasValue() &&
                long.TryParse(iatClaim, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iatUnixSeconds))
            {
                tokenIssuedAt = DateTimeOffset.FromUnixTimeSeconds(iatUnixSeconds);
            }

            if (tokenIssuedAt is null && securityToken is not null)
            {
                var issued = securityToken.ValidFrom;
                if (issued != DateTime.MinValue)
                {
                    tokenIssuedAt = DateTime.SpecifyKind(issued, DateTimeKind.Utc);
                }
            }

            if (tokenIssuedAt is null)
            {
                var logger = _serviceProvider.GetRequiredService<ILogger<JwtBearerOptionsConfigurator>>();
                logger.LogWarning(
                    "SECURITY_AUDIT: Unable to determine issued-at timestamp for token belonging to UserId={UserId}. Treating token as revoked for safety.",
                    userId);
                return true;
            }

            // Check if there's a user-level revocation marker.
            // revocationCheckKey encodes the user identifier and issued-at timestamp so the
            // revocation service can compare against the stored "revoked_user:{userId}" marker.
            var revocationCheckKey = EncodeUserRevocationCheckKey(userId, tokenIssuedAt.Value);
            var isRevoked = await revocationService.IsTokenRevokedAsync(revocationCheckKey, cancellationToken)
                .ConfigureAwait(false);

            return isRevoked;
        }
        catch (Exception ex)
        {
            var logger = _serviceProvider.GetRequiredService<ILogger<JwtBearerOptionsConfigurator>>();
            logger.LogError(
                ex,
                "Error checking user-level token revocation. UserId={UserId}. Treating as not revoked (fail-open) since individual token check passed.",
                userId);

            // Fail-open for user revocation check failures (individual token check already passed)
            return false;
        }
    }

    /// <summary>
    /// Hashes a value for safe logging (prevents sensitive data leakage in logs).
    /// </summary>
    private static string HashForLogging(string value)
    {
        if (value.IsNullOrEmpty())
            return "null";

        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value));
        var hashHex = Convert.ToHexString(hashBytes);
        return hashHex[..Math.Min(8, hashHex.Length)];
    }

    private static string EncodeUserRevocationCheckKey(string userId, DateTimeOffset issuedAt)
    {
        var encodedUserId = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(userId));
        return $"user:{encodedUserId}|{issuedAt.ToUnixTimeSeconds()}";
    }
}
