// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Core.Authentication;

/// <summary>
/// Provides access to the current user's identity and session information from the HTTP request context.
/// This service extracts user identity from authentication claims and maintains session tracking for audit trails.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="IUserContext"/> service is the primary mechanism for accessing user identity information
/// throughout the application. It provides a consistent interface for retrieving user details regardless of
/// the authentication mechanism (JWT, SAML, API Key, Local Auth, etc.).
/// </para>
/// <para>
/// <strong>Lifetime:</strong> This service must be registered as Scoped in the DI container to ensure
/// it's bound to a single HTTP request and can access HttpContext correctly.
/// </para>
/// <para>
/// <strong>Unauthenticated Scenarios:</strong> For system operations where no user is authenticated,
/// the service returns "system" as the user identifier and generates a unique session ID for tracking.
/// </para>
/// <para>
/// <strong>Session Management:</strong> Session IDs are extracted from the X-Session-Id request header
/// if present, otherwise a unique session ID is generated and maintained for the request lifetime.
/// This enables correlation of audit events within the same user session.
/// </para>
/// </remarks>
public interface IUserContext
{
    /// <summary>
    /// Gets the authenticated user's unique identifier.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This value is extracted from the ClaimTypes.NameIdentifier claim (typically the 'sub' claim in JWT/OIDC).
    /// </para>
    /// <para>
    /// For unauthenticated requests or system operations, this returns "system".
    /// </para>
    /// <para>
    /// This identifier should be used for audit logging, created_by/modified_by fields, and user tracking.
    /// </para>
    /// </remarks>
    /// <value>
    /// The user identifier (never null). Returns "system" for unauthenticated users or system operations.
    /// </value>
    string UserId { get; }

    /// <summary>
    /// Gets the authenticated user's display name or email address.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This value is extracted from claims in the following priority order:
    /// 1. Name claim (ClaimTypes.Name or "name")
    /// 2. Email claim (ClaimTypes.Email or "email")
    /// 3. Identity.Name
    /// </para>
    /// <para>
    /// For unauthenticated requests, this returns null.
    /// </para>
    /// </remarks>
    /// <value>
    /// The user's display name or email, or null if not authenticated.
    /// </value>
    string? UserName { get; }

    /// <summary>
    /// Gets the current session identifier for correlating related operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Session IDs are used to correlate multiple audit events and operations within a single user session.
    /// This enables tracing a user's actions across multiple API calls.
    /// </para>
    /// <para>
    /// The session ID is:
    /// - Extracted from the X-Session-Id request header if present
    /// - Generated as a new Guid if no header is present
    /// - Consistent for the duration of the HTTP request
    /// </para>
    /// </remarks>
    /// <value>
    /// A unique session identifier (never Guid.Empty).
    /// </value>
    Guid SessionId { get; }

    /// <summary>
    /// Gets the tenant identifier for the current request in multi-tenant scenarios.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This value is extracted from the "tenant_id" or "tenantId" claim if present.
    /// </para>
    /// <para>
    /// Returns null for single-tenant deployments or when no tenant context is available.
    /// </para>
    /// </remarks>
    /// <value>
    /// The tenant identifier, or null if not applicable or not available.
    /// </value>
    Guid? TenantId { get; }

    /// <summary>
    /// Gets a value indicating whether the current user is authenticated.
    /// </summary>
    /// <remarks>
    /// This is determined by checking HttpContext.User.Identity.IsAuthenticated.
    /// Returns false for anonymous requests or system operations.
    /// </remarks>
    /// <value>
    /// true if the user is authenticated; otherwise, false.
    /// </value>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the IP address of the client making the request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This value considers forwarded headers (X-Forwarded-For) for requests behind proxies or load balancers.
    /// </para>
    /// <para>
    /// Returns null if the IP address cannot be determined.
    /// </para>
    /// </remarks>
    /// <value>
    /// The client's IP address, or null if unavailable.
    /// </value>
    string? IpAddress { get; }

    /// <summary>
    /// Gets the User-Agent header from the request.
    /// </summary>
    /// <remarks>
    /// This can be used to identify the client application or browser making the request.
    /// Returns null if the User-Agent header is not present.
    /// </remarks>
    /// <value>
    /// The User-Agent string, or null if not provided.
    /// </value>
    string? UserAgent { get; }

    /// <summary>
    /// Gets the authentication method used for the current request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This indicates the authentication scheme used (e.g., "Bearer", "ApiKey", "SAML", "Cookies").
    /// </para>
    /// <para>
    /// Returns null for unauthenticated requests.
    /// </para>
    /// </remarks>
    /// <value>
    /// The authentication method/scheme, or null if not authenticated.
    /// </value>
    string? AuthenticationMethod { get; }
}
