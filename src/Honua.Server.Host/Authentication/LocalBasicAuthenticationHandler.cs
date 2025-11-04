// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Security;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Host.Authentication;

public static class LocalBasicAuthenticationDefaults
{
    public const string Scheme = "HonuaBasic";
    public const string DisplayName = "Honua Basic";
}

/// <summary>
/// Authentication handler that validates HTTP Basic credentials against Honua's local authentication service.
/// </summary>
internal sealed class LocalBasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ILocalAuthenticationService _authenticationService;
    private readonly IOptionsMonitor<HonuaAuthenticationOptions> _options;
    private readonly ISecurityAuditLogger _auditLogger;

    public LocalBasicAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder,
        ISystemClock clock,
        ILocalAuthenticationService authenticationService,
        IOptionsMonitor<HonuaAuthenticationOptions> options,
        ISecurityAuditLogger auditLogger)
        : base(schemeOptions, loggerFactory, encoder, clock)
    {
        _authenticationService = Guard.NotNull(authenticationService);
        _options = Guard.NotNull(options);
        _auditLogger = Guard.NotNull(auditLogger);
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorizationHeader = Request.Headers.Authorization.ToString();
        if (authorizationHeader.IsNullOrWhiteSpace() ||
            !authorizationHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogDebug("No Basic authentication header provided from {RemoteIp}", Context.Connection.RemoteIpAddress);
            return AuthenticateResult.NoResult();
        }

        if (!IsHttps(Request))
        {
            Logger.LogWarning("Refusing to process Basic authentication over non-HTTPS request from {RemoteIp}", Context.Connection.RemoteIpAddress);
            return AuthenticateResult.Fail("Basic authentication requires HTTPS.");
        }

        var options = _options.CurrentValue;
        if (options.Mode != HonuaAuthenticationOptions.AuthenticationMode.Local)
        {
            Logger.LogWarning("Basic authentication attempted while local mode is disabled from {RemoteIp}", Context.Connection.RemoteIpAddress);
            return AuthenticateResult.Fail("Local authentication is not enabled.");
        }

        if (!TryDecodeCredentials(authorizationHeader, out var username, out var password))
        {
            Logger.LogWarning("Invalid Basic authentication header format from {RemoteIp}", Context.Connection.RemoteIpAddress);
            return AuthenticateResult.Fail("Invalid Basic authentication header.");
        }

        var ipAddress = Context.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        Logger.LogDebug("Attempting Basic authentication for user '{Username}' from {RemoteIp}", username, ipAddress);

        var result = await _authenticationService.AuthenticateAsync(username, password, Context.RequestAborted).ConfigureAwait(false);

        switch (result.Status)
        {
            case LocalAuthenticationStatus.Success:
                Logger.LogInformation("Successful Basic authentication for user '{Username}' from {RemoteIp}", username, ipAddress);
                _auditLogger.LogLoginSuccess(username, ipAddress, userAgent);
                return AuthenticateResult.Success(CreateTicket(result, username));

            case LocalAuthenticationStatus.PasswordExpiresSoon:
                Logger.LogWarning("Basic authentication successful for user '{Username}' from {RemoteIp} but password expires soon",
                    username, ipAddress);
                _auditLogger.LogLoginSuccess(username, ipAddress, userAgent);
                return AuthenticateResult.Success(CreateTicket(result, username));

            case LocalAuthenticationStatus.InvalidCredentials:
                Logger.LogWarning("Invalid credentials for user '{Username}' from {RemoteIp}", username, ipAddress);
                _auditLogger.LogLoginFailure(username, ipAddress, userAgent, "invalid_credentials");
                return AuthenticateResult.Fail("Invalid username or password.");

            case LocalAuthenticationStatus.LockedOut:
                Logger.LogWarning("Account locked for user '{Username}' from {RemoteIp}, locked until {LockedUntil}",
                    username, ipAddress, result.LockedUntil ?? DateTimeOffset.UtcNow);
                _auditLogger.LogAccountLockout(username, ipAddress, result.LockedUntil ?? DateTimeOffset.UtcNow);
                return AuthenticateResult.Fail("Account locked.");

            case LocalAuthenticationStatus.Disabled:
                Logger.LogWarning("Disabled account '{Username}' attempted authentication from {RemoteIp}", username, ipAddress);
                _auditLogger.LogLoginFailure(username, ipAddress, userAgent, "account_disabled");
                return AuthenticateResult.Fail("Account disabled.");

            case LocalAuthenticationStatus.PasswordExpired:
                Logger.LogWarning("Expired password for user '{Username}' attempted authentication from {RemoteIp}",
                    username, ipAddress);
                _auditLogger.LogLoginFailure(username, ipAddress, userAgent, "password_expired");
                return AuthenticateResult.Fail("Password expired.");

            case LocalAuthenticationStatus.NotConfigured:
                Logger.LogError("Local authentication not configured but was attempted from {RemoteIp}", ipAddress);
                return AuthenticateResult.Fail("Local authentication is not configured.");

            default:
                Logger.LogError("Unexpected authentication status {Status} for user '{Username}' from {RemoteIp}",
                    result.Status, username, ipAddress);
                return AuthenticateResult.Fail("Authentication failed.");
        }
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers["WWW-Authenticate"] = $"Basic realm=\"Honua\", charset=\"UTF-8\"";
        return base.HandleChallengeAsync(properties);
    }

    private AuthenticationTicket CreateTicket(LocalAuthenticationResult result, string username)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, result.UserId ?? username),
            new Claim(ClaimTypes.Name, username)
        };

        foreach (var role in result.Roles)
        {
            if (role.HasValue())
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
                claims.Add(new Claim(LocalAuthenticationDefaults.RoleClaimType, role));
            }
        }

        var identity = new ClaimsIdentity(claims, LocalBasicAuthenticationDefaults.Scheme);
        var principal = new ClaimsPrincipal(identity);
        return new AuthenticationTicket(principal, LocalBasicAuthenticationDefaults.Scheme);
    }

    private static bool TryDecodeCredentials(string authorizationHeader, out string username, out string password)
    {
        username = string.Empty;
        password = string.Empty;

        var encoded = authorizationHeader["Basic ".Length..].Trim();
        if (encoded.IsNullOrWhiteSpace())
        {
            return false;
        }

        byte[] decodedBytes;
        try
        {
            decodedBytes = Convert.FromBase64String(encoded);
        }
        catch (FormatException)
        {
            return false;
        }

        var credentialString = Encoding.UTF8.GetString(decodedBytes);
        var separatorIndex = credentialString.IndexOf(':');
        if (separatorIndex <= 0)
        {
            return false;
        }

        username = credentialString[..separatorIndex];
        password = credentialString[(separatorIndex + 1)..];
        return true;
    }

    /// <summary>
    /// Checks if the request is HTTPS, validating forwarded headers only from trusted proxies.
    /// SECURITY FIX: Prevents header spoofing attacks by validating the proxy source.
    /// </summary>
    private bool IsHttps(HttpRequest request)
    {
        if (request.IsHttps)
        {
            return true;
        }

        // SECURITY FIX: Only trust X-Forwarded-Proto from validated trusted proxies
        // Check if request comes from a trusted proxy before trusting forwarded headers
        var validator = Context.RequestServices.GetService<TrustedProxyValidator>();

        if (validator == null || !validator.IsEnabled)
        {
            // No trusted proxy configuration - do not trust forwarded headers
            var forwardedProto = request.Headers["X-Forwarded-Proto"].ToString();
            if (!forwardedProto.IsNullOrEmpty())
            {
                Logger.LogWarning(
                    "X-Forwarded-Proto header '{ForwardedProto}' received from untrusted IP {RemoteIP} is IGNORED. " +
                    "Configure TrustedProxies in appsettings.json if running behind a reverse proxy.",
                    forwardedProto,
                    Context.Connection.RemoteIpAddress);
            }
            return false;
        }

        var remoteIp = Context.Connection.RemoteIpAddress;
        if (!validator.IsTrustedProxy(remoteIp))
        {
            // Request not from trusted proxy - do not trust forwarded headers
            var forwardedProto = request.Headers["X-Forwarded-Proto"].ToString();
            if (!forwardedProto.IsNullOrEmpty())
            {
                Logger.LogWarning(
                    "SECURITY: X-Forwarded-Proto header '{ForwardedProto}' received from untrusted IP {RemoteIP} is IGNORED. " +
                    "This may indicate a header injection attack.",
                    forwardedProto,
                    remoteIp);
            }
            return false;
        }

        // Request from trusted proxy - safely check forwarded proto header
        if (request.Headers.TryGetValue("X-Forwarded-Proto", out var validatedForwardedProto) &&
            validatedForwardedProto.Count > 0 &&
            validatedForwardedProto[0].Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
