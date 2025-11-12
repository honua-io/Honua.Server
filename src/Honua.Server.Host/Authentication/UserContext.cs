// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Security.Claims;
using Honua.Server.Core.Authentication;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Authentication;

/// <summary>
/// Implementation of <see cref="IUserContext"/> that extracts user identity and session information
/// from the current HTTP request context.
/// </summary>
/// <remarks>
/// <para>
/// This service uses <see cref="IHttpContextAccessor"/> to access the current HTTP request and extract:
/// - User identity from authentication claims
/// - Session ID from request headers or generates a new one
/// - IP address from connection or forwarded headers
/// - User agent from request headers
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> This service is thread-safe for the lifetime of a single HTTP request.
/// It should be registered as Scoped in the DI container.
/// </para>
/// <para>
/// <strong>Performance:</strong> Values are lazily computed and cached for the request lifetime to avoid
/// repeated claim lookups and header parsing.
/// </para>
/// </remarks>
public sealed class UserContext : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    // Cached values (computed once per request)
    private string? _userId;
    private string? _userName;
    private Guid? _sessionId;
    private Guid? _tenantId;
    private bool? _isAuthenticated;
    private string? _ipAddress;
    private string? _userAgent;
    private string? _authenticationMethod;
    private bool _valuesInitialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserContext"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor for retrieving request information.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpContextAccessor"/> is null.</exception>
    public UserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <inheritdoc />
    public string UserId
    {
        get
        {
            EnsureInitialized();
            return _userId!;
        }
    }

    /// <inheritdoc />
    public string? UserName
    {
        get
        {
            EnsureInitialized();
            return _userName;
        }
    }

    /// <inheritdoc />
    public Guid SessionId
    {
        get
        {
            EnsureInitialized();
            return _sessionId!.Value;
        }
    }

    /// <inheritdoc />
    public Guid? TenantId
    {
        get
        {
            EnsureInitialized();
            return _tenantId;
        }
    }

    /// <inheritdoc />
    public bool IsAuthenticated
    {
        get
        {
            EnsureInitialized();
            return _isAuthenticated!.Value;
        }
    }

    /// <inheritdoc />
    public string? IpAddress
    {
        get
        {
            EnsureInitialized();
            return _ipAddress;
        }
    }

    /// <inheritdoc />
    public string? UserAgent
    {
        get
        {
            EnsureInitialized();
            return _userAgent;
        }
    }

    /// <inheritdoc />
    public string? AuthenticationMethod
    {
        get
        {
            EnsureInitialized();
            return _authenticationMethod;
        }
    }

    /// <summary>
    /// Initializes all cached values from the HTTP context.
    /// This is called once per request on first property access.
    /// </summary>
    private void EnsureInitialized()
    {
        if (_valuesInitialized)
        {
            return;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext?.User;

        // Extract user identity
        _isAuthenticated = user?.Identity?.IsAuthenticated ?? false;

        if (_isAuthenticated.Value)
        {
            // Extract user ID using the existing helper
            _userId = UserIdentityHelper.GetUserIdentifier(user);

            // Extract user name from claims
            _userName = ExtractUserName(user);

            // Extract tenant ID if available
            _tenantId = ExtractTenantId(user);

            // Extract authentication method
            _authenticationMethod = user?.Identity?.AuthenticationType;
        }
        else
        {
            // System/unauthenticated operation
            _userId = "system";
            _userName = null;
            _tenantId = null;
            _authenticationMethod = null;
        }

        // Extract or generate session ID
        _sessionId = ExtractOrGenerateSessionId(httpContext);

        // Extract IP address (considering forwarded headers)
        _ipAddress = ExtractIpAddress(httpContext);

        // Extract user agent
        _userAgent = httpContext?.Request.Headers["User-Agent"].FirstOrDefault();

        _valuesInitialized = true;
    }

    /// <summary>
    /// Extracts the user name from claims.
    /// </summary>
    private static string? ExtractUserName(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        // Try multiple claim types in priority order
        return user.FindFirst(ClaimTypes.Name)?.Value
            ?? user.FindFirst("name")?.Value
            ?? user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.FindFirst("email")?.Value
            ?? user.Identity.Name;
    }

    /// <summary>
    /// Extracts the tenant ID from claims.
    /// </summary>
    private static Guid? ExtractTenantId(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        // Try both claim type variants
        var tenantIdClaim = user.FindFirst("tenant_id")?.Value
            ?? user.FindFirst("tenantId")?.Value
            ?? user.FindFirst("tid")?.Value; // Azure AD uses 'tid'

        if (string.IsNullOrWhiteSpace(tenantIdClaim))
        {
            return null;
        }

        return Guid.TryParse(tenantIdClaim, out var tenantGuid) ? tenantGuid : null;
    }

    /// <summary>
    /// Extracts session ID from request header or generates a new one.
    /// </summary>
    private static Guid ExtractOrGenerateSessionId(HttpContext? httpContext)
    {
        if (httpContext == null)
        {
            return Guid.NewGuid();
        }

        // Try to get session ID from header
        var sessionIdHeader = httpContext.Request.Headers["X-Session-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(sessionIdHeader) && Guid.TryParse(sessionIdHeader, out var sessionId))
        {
            return sessionId;
        }

        // Try to get from correlation ID (alternative header name)
        var correlationIdHeader = httpContext.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(correlationIdHeader) && Guid.TryParse(correlationIdHeader, out var correlationId))
        {
            return correlationId;
        }

        // Generate a new session ID for this request
        return Guid.NewGuid();
    }

    /// <summary>
    /// Extracts the client IP address, considering forwarded headers.
    /// </summary>
    private static string? ExtractIpAddress(HttpContext? httpContext)
    {
        if (httpContext == null)
        {
            return null;
        }

        // Check X-Forwarded-For header (for requests behind proxy/load balancer)
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            // X-Forwarded-For can contain multiple IPs (client, proxy1, proxy2, ...)
            // The first one is typically the original client
            var firstIp = forwardedFor.Split(',')[0].Trim();
            if (!string.IsNullOrWhiteSpace(firstIp))
            {
                return firstIp;
            }
        }

        // Check X-Real-IP header (common in nginx)
        var realIp = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(realIp))
        {
            return realIp;
        }

        // Fall back to direct connection IP
        return httpContext.Connection.RemoteIpAddress?.ToString();
    }
}
