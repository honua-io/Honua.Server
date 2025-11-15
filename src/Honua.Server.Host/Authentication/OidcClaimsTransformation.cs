// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Authentication;

/// <summary>
/// Transforms claims from OIDC providers to Honua's internal claim structure.
/// Maps provider-specific claims (Azure AD, Google, etc.) to standardized roles and claims.
/// </summary>
public sealed class OidcClaimsTransformation : IClaimsTransformation
{
    private readonly ILogger<OidcClaimsTransformation> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OidcClaimsTransformation"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public OidcClaimsTransformation(ILogger<OidcClaimsTransformation> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Transforms the claims principal by mapping OIDC provider claims to Honua's internal claims.
    /// </summary>
    /// <param name="principal">The claims principal to transform.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the transformed claims principal.</returns>
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return Task.FromResult(principal!);
        }

        var identity = principal.Identity as ClaimsIdentity;
        if (identity == null)
        {
            return Task.FromResult(principal);
        }

        // Only transform OIDC-authenticated users (skip JWT Bearer and Basic Auth)
        var authenticationType = identity.AuthenticationType;
        if (authenticationType == null ||
            !authenticationType.Contains("oidc", StringComparison.OrdinalIgnoreCase) &&
            !authenticationType.Contains("google", StringComparison.OrdinalIgnoreCase) &&
            !authenticationType.Contains("azuread", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(principal);
        }

        _logger.LogDebug("Transforming OIDC claims for authentication type: {AuthenticationType}", authenticationType);

        var claimsToAdd = new List<Claim>();

        // Map Azure AD roles
        // Azure AD provides roles in the "roles" claim when App Roles are configured
        MapAzureAdRoles(identity, claimsToAdd);

        // Map Google claims
        // Google doesn't provide roles by default, but we can map based on email domain or groups
        MapGoogleClaims(identity, claimsToAdd);

        // Map generic OIDC claims
        MapGenericOidcClaims(identity, claimsToAdd);

        // Ensure consistent role claim types
        NormalizeRoleClaims(identity, claimsToAdd);

        // Add all transformed claims to the identity
        if (claimsToAdd.Count > 0)
        {
            identity.AddClaims(claimsToAdd);
            _logger.LogDebug("Added {ClaimCount} transformed claims to identity", claimsToAdd.Count);
        }

        return Task.FromResult(principal);
    }

    /// <summary>
    /// Maps Azure AD specific claims to Honua roles.
    /// Azure AD App Roles appear in the "roles" claim.
    /// </summary>
    private void MapAzureAdRoles(ClaimsIdentity identity, List<Claim> claimsToAdd)
    {
        var rolesClaims = identity.FindAll("roles").ToList();
        if (rolesClaims.Count == 0)
        {
            return;
        }

        _logger.LogDebug("Found {RoleCount} Azure AD roles to map", rolesClaims.Count);

        foreach (var roleClaim in rolesClaims)
        {
            var roleValue = roleClaim.Value?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(roleValue))
            {
                continue;
            }

            // Map Azure AD roles to Honua roles
            var mappedRole = MapToHonuaRole(roleValue);
            if (mappedRole != null && !identity.HasClaim(ClaimTypes.Role, mappedRole))
            {
                claimsToAdd.Add(new Claim(ClaimTypes.Role, mappedRole));
                _logger.LogDebug("Mapped Azure AD role '{AzureRole}' to Honua role '{HonuaRole}'", roleValue, mappedRole);
            }
        }
    }

    /// <summary>
    /// Maps Google-specific claims to Honua roles.
    /// Google doesn't provide roles by default, but we can infer them from email domain or other claims.
    /// </summary>
    private void MapGoogleClaims(ClaimsIdentity identity, List<Claim> claimsToAdd)
    {
        var emailClaim = identity.FindFirst(ClaimTypes.Email) ?? identity.FindFirst("email");
        if (emailClaim == null)
        {
            return;
        }

        var email = emailClaim.Value;
        _logger.LogDebug("Processing Google email claim: {Email}", email);

        // Example: Grant viewer role to all authenticated Google users
        // In production, you would check against a whitelist, database, or external service
        if (!identity.HasClaim(ClaimTypes.Role, "viewer"))
        {
            claimsToAdd.Add(new Claim(ClaimTypes.Role, "viewer"));
            _logger.LogDebug("Granted default 'viewer' role to Google user: {Email}", email);
        }

        // Example: Grant administrator role to specific email domains
        // Uncomment and customize for your organization
        /*
        if (email.EndsWith("@yourdomain.com", StringComparison.OrdinalIgnoreCase))
        {
            if (!identity.HasClaim(ClaimTypes.Role, "administrator"))
            {
                claimsToAdd.Add(new Claim(ClaimTypes.Role, "administrator"));
                _logger.LogInformation("Granted 'administrator' role to domain user: {Email}", email);
            }
        }
        */
    }

    /// <summary>
    /// Maps generic OIDC provider claims to Honua roles.
    /// Handles common claim types from various OIDC providers.
    /// </summary>
    private void MapGenericOidcClaims(ClaimsIdentity identity, List<Claim> claimsToAdd)
    {
        // Check for "groups" claim (common in many OIDC providers)
        var groupsClaims = identity.FindAll("groups").ToList();
        foreach (var groupClaim in groupsClaims)
        {
            var groupValue = groupClaim.Value?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(groupValue))
            {
                continue;
            }

            var mappedRole = MapToHonuaRole(groupValue);
            if (mappedRole != null && !identity.HasClaim(ClaimTypes.Role, mappedRole))
            {
                claimsToAdd.Add(new Claim(ClaimTypes.Role, mappedRole));
                _logger.LogDebug("Mapped OIDC group '{Group}' to Honua role '{HonuaRole}'", groupValue, mappedRole);
            }
        }

        // Ensure user has a default role if no roles were assigned
        if (!identity.HasClaim(ClaimTypes.Role, "viewer") &&
            !identity.HasClaim(ClaimTypes.Role, "editor") &&
            !identity.HasClaim(ClaimTypes.Role, "administrator") &&
            !identity.HasClaim(ClaimTypes.Role, "datapublisher"))
        {
            // By default, grant viewer access to authenticated OIDC users
            // This can be disabled or customized based on your security requirements
            claimsToAdd.Add(new Claim(ClaimTypes.Role, "viewer"));
            _logger.LogDebug("Granted default 'viewer' role to OIDC user (no specific roles found)");
        }
    }

    /// <summary>
    /// Normalizes role claims to ensure consistency.
    /// Ensures both "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
    /// and custom role claim types are present.
    /// </summary>
    private void NormalizeRoleClaims(ClaimsIdentity identity, List<Claim> claimsToAdd)
    {
        // Ensure all roles are available under ClaimTypes.Role
        var customRoleClaims = identity.FindAll("role").ToList();
        foreach (var roleClaim in customRoleClaims)
        {
            if (!identity.HasClaim(ClaimTypes.Role, roleClaim.Value))
            {
                claimsToAdd.Add(new Claim(ClaimTypes.Role, roleClaim.Value));
            }
        }
    }

    /// <summary>
    /// Maps external role/group names to Honua's internal role names.
    /// Customize this method to match your organization's role naming conventions.
    /// </summary>
    /// <param name="externalRole">The external role or group name.</param>
    /// <returns>The mapped Honua role name, or null if no mapping exists.</returns>
    private string? MapToHonuaRole(string externalRole)
    {
        // Normalize the input
        var normalized = externalRole.Trim().ToLowerInvariant();

        // Map common role names
        return normalized switch
        {
            // Administrator mappings
            "administrator" or "admin" or "honua.admin" or "honua-admin" => "administrator",

            // Editor mappings
            "editor" or "honua.editor" or "honua-editor" => "editor",

            // Data Publisher mappings
            "datapublisher" or "data-publisher" or "honua.datapublisher" or "honua-datapublisher" => "datapublisher",

            // Viewer mappings
            "viewer" or "reader" or "honua.viewer" or "honua-viewer" => "viewer",

            // No mapping found
            _ => null
        };
    }
}
