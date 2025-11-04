// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Honua.Server.Core.Extensions;

#nullable enable

namespace Honua.Server.Host.Utilities;

/// <summary>
/// Shared helper for resolving user identity and authorization claims across different authentication contexts.
/// Provides consistent precedence: NameIdentifier > Name > fallback values.
/// </summary>
public static class UserIdentityHelper
{
    /// <summary>
    /// Resolves the user identifier from ClaimsPrincipal with fallback to "anonymous" for unauthenticated users.
    /// This is the preferred method for audit trails and logging where a non-null value is required.
    /// </summary>
    /// <param name="user">The claims principal (may be null).</param>
    /// <returns>User identifier string, never null. Returns "anonymous" for unauthenticated users.</returns>
    public static string GetUserIdentifier(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated == true)
        {
            // Prefer NameIdentifier (typically sub claim in JWT/OIDC)
            var nameIdentifier = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (nameIdentifier.HasValue())
            {
                return nameIdentifier;
            }

            // Fall back to Name (typically name claim)
            if (user.Identity.Name.HasValue())
            {
                return user.Identity.Name;
            }

            // User is authenticated but has no identifier claims
            return "authenticated-user";
        }

        return "anonymous";
    }

    /// <summary>
    /// Resolves the user identifier from ClaimsPrincipal, returning null for unauthenticated users.
    /// Use this when null is a meaningful value (e.g., optional user tracking).
    /// </summary>
    /// <param name="user">The claims principal (may be null).</param>
    /// <returns>User identifier string or null for unauthenticated users.</returns>
    public static string? GetUserIdentifierOrNull(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        // Prefer NameIdentifier (typically sub claim in JWT/OIDC)
        var nameIdentifier = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (nameIdentifier.HasValue())
        {
            return nameIdentifier;
        }

        // Fall back to Name (typically name claim)
        return user.Identity.Name;
    }

    /// <summary>
    /// Extracts user roles from ClaimsPrincipal claims.
    /// Checks both standard ClaimTypes.Role and lowercase "role" claim types (for JWT/OIDC compatibility).
    /// Returns distinct, non-empty role values with case-insensitive comparison.
    /// </summary>
    /// <param name="user">The claims principal (may be null).</param>
    /// <returns>Read-only list of role names, empty for unauthenticated users.</returns>
    public static IReadOnlyList<string> ExtractUserRoles(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return Array.Empty<string>();
        }

        var roles = user.Claims
            .Where(claim => claim.Type == ClaimTypes.Role || string.Equals(claim.Type, "role", StringComparison.OrdinalIgnoreCase))
            .Select(claim => claim.Value)
            .Where(value => value.HasValue())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return roles.Length == 0 ? Array.Empty<string>() : roles;
    }
}
