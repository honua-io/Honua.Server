// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Honua.Server.Core.Security;

/// <summary>
/// Service for extracting user identity information from the authentication context.
/// This resolves TODO-002 by providing proper user attribution for audit trails and compliance.
/// </summary>
public interface IUserIdentityService
{
    /// <summary>
    /// Gets the current user's ID from the authentication context.
    /// </summary>
    /// <returns>The user ID, or null if not authenticated.</returns>
    string? GetCurrentUserId();

    /// <summary>
    /// Gets the current user's username/email from the authentication context.
    /// </summary>
    /// <returns>The username, or null if not authenticated.</returns>
    string? GetCurrentUsername();

    /// <summary>
    /// Gets the current user's email from the authentication context.
    /// </summary>
    /// <returns>The email, or null if not authenticated.</returns>
    string? GetCurrentUserEmail();

    /// <summary>
    /// Gets the current user's tenant ID from the authentication context.
    /// </summary>
    /// <returns>The tenant ID, or null if not authenticated or multi-tenancy not enabled.</returns>
    string? GetCurrentTenantId();

    /// <summary>
    /// Gets the current user's roles from the authentication context.
    /// </summary>
    /// <returns>An array of role names.</returns>
    string[] GetCurrentUserRoles();

    /// <summary>
    /// Gets a user identity object with all available information.
    /// </summary>
    /// <returns>A UserIdentity object, or null if not authenticated.</returns>
    UserIdentity? GetCurrentUserIdentity();

    /// <summary>
    /// Checks if the current user is authenticated.
    /// </summary>
    /// <returns>True if authenticated, false otherwise.</returns>
    bool IsAuthenticated();
}

/// <summary>
/// Represents user identity information extracted from the authentication context.
/// </summary>
public sealed record UserIdentity(
    string UserId,
    string? Username,
    string? Email,
    string? TenantId,
    string[] Roles);

/// <summary>
/// Default implementation of IUserIdentityService that extracts user information from HttpContext claims.
/// Supports multiple authentication modes: Local JWT, OIDC, SAML.
/// </summary>
public sealed class UserIdentityService : IUserIdentityService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserIdentityService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public string? GetCurrentUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        // Try standard claims in priority order:
        // 1. JWT 'sub' claim (most common for JWT tokens)
        // 2. Name Identifier claim (used by ASP.NET Identity)
        // 3. NameIdentifier (alternative claim type)
        return user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? user.FindFirst("nameid")?.Value;
    }

    public string? GetCurrentUsername()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        // Try to get username from various claim types
        return user.FindFirst(ClaimTypes.Name)?.Value
            ?? user.FindFirst("name")?.Value
            ?? user.FindFirst("preferred_username")?.Value
            ?? user.FindFirst(JwtRegisteredClaimNames.Name)?.Value
            ?? user.Identity.Name;
    }

    public string? GetCurrentUserEmail()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        // Try to get email from various claim types
        return user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.FindFirst("email")?.Value
            ?? user.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
    }

    public string? GetCurrentTenantId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        // Try to get tenant ID from various claim types
        // Common patterns: tenant_id, tid, tenantId, organization_id
        return user.FindFirst("tenant_id")?.Value
            ?? user.FindFirst("tid")?.Value
            ?? user.FindFirst("tenantId")?.Value
            ?? user.FindFirst("organization_id")?.Value
            ?? user.FindFirst("org_id")?.Value;
    }

    public string[] GetCurrentUserRoles()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return Array.Empty<string>();
        }

        // Get all role claims (there can be multiple)
        // Support various role claim types used by different auth providers
        return user.Claims
            .Where(c => c.Type == ClaimTypes.Role
                     || c.Type == "role"
                     || c.Type == "honua_role"
                     || c.Type == "roles")
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToArray();
    }

    public UserIdentity? GetCurrentUserIdentity()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return null;
        }

        return new UserIdentity(
            UserId: userId,
            Username: GetCurrentUsername(),
            Email: GetCurrentUserEmail(),
            TenantId: GetCurrentTenantId(),
            Roles: GetCurrentUserRoles());
    }

    public bool IsAuthenticated()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
    }
}
