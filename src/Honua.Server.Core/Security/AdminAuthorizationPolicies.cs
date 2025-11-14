// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Core.Security;

/// <summary>
/// Defines granular administrative authorization policies.
/// This resolves TODO-001 by providing role-based access control for admin functions.
/// </summary>
public static class AdminAuthorizationPolicies
{
    // Policy names
    public const string RequireAdministrator = "RequireAdministrator";
    public const string RequireSuperAdministrator = "RequireSuperAdministrator";
    public const string RequireAlertAdministrator = "RequireAlertAdministrator";
    public const string RequireMetadataAdministrator = "RequireMetadataAdministrator";
    public const string RequireUserAdministrator = "RequireUserAdministrator";
    public const string RequireFeatureFlagAdministrator = "RequireFeatureFlagAdministrator";
    public const string RequireServerAdministrator = "RequireServerAdministrator";

    // Role names
    public const string SuperAdminRole = "SuperAdmin";
    public const string AdminRole = "Admin";
    public const string AlertAdminRole = "AlertAdmin";
    public const string MetadataAdminRole = "MetadataAdmin";
    public const string UserAdminRole = "UserAdmin";
    public const string FeatureFlagAdminRole = "FeatureFlagAdmin";
    public const string ServerAdminRole = "ServerAdmin";

    /// <summary>
    /// Configures all administrative authorization policies.
    /// </summary>
    public static void ConfigureAdminPolicies(AuthorizationOptions options)
    {
        // General administrator - requires Admin or SuperAdmin role
        options.AddPolicy(RequireAdministrator, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole(AdminRole, SuperAdminRole);
        });

        // Super administrator - full system access
        options.AddPolicy(RequireSuperAdministrator, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole(SuperAdminRole);
        });

        // Alert administrator - manage alerts, rules, and channels
        options.AddPolicy(RequireAlertAdministrator, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole(SuperAdminRole, AdminRole, AlertAdminRole);
        });

        // Metadata administrator - manage data sources, layers, collections
        options.AddPolicy(RequireMetadataAdministrator, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole(SuperAdminRole, AdminRole, MetadataAdminRole);
        });

        // User administrator - manage users and permissions
        options.AddPolicy(RequireUserAdministrator, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole(SuperAdminRole, AdminRole, UserAdminRole);
        });

        // Feature flag administrator - manage feature flags
        options.AddPolicy(RequireFeatureFlagAdministrator, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole(SuperAdminRole, AdminRole, FeatureFlagAdminRole);
        });

        // Server administrator - manage server configuration and health
        options.AddPolicy(RequireServerAdministrator, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole(SuperAdminRole, AdminRole, ServerAdminRole);
        });
    }
}

/// <summary>
/// Authorization requirement that validates specific admin permissions.
/// </summary>
public class AdminPermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    public AdminPermissionRequirement(string permission)
    {
        Permission = permission ?? throw new ArgumentNullException(nameof(permission));
    }
}

/// <summary>
/// Authorization handler for admin permission requirements with audit logging.
/// </summary>
public class AdminPermissionHandler : AuthorizationHandler<AdminPermissionRequirement>
{
    private readonly IAuditLoggingService _auditLoggingService;

    public AdminPermissionHandler(IAuditLoggingService auditLoggingService)
    {
        _auditLoggingService = auditLoggingService ?? throw new ArgumentNullException(nameof(auditLoggingService));
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminPermissionRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await _auditLoggingService.LogAuthorizationDeniedAsync(
                action: $"AdminPermission.{requirement.Permission}",
                reason: "User not authenticated");
            return;
        }

        // SuperAdmin has all permissions
        if (context.User.IsInRole(AdminAuthorizationPolicies.SuperAdminRole))
        {
            context.Succeed(requirement);
            return;
        }

        // Check if user has the required permission
        // This is a simplified implementation - you can extend this with a more sophisticated
        // permission system (e.g., claims-based, database-driven, etc.)
        var hasPermission = context.User.HasClaim(c =>
            c.Type == "permission" && c.Value == requirement.Permission);

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
        else
        {
            await _auditLoggingService.LogAuthorizationDeniedAsync(
                action: $"AdminPermission.{requirement.Permission}",
                reason: $"User lacks required permission: {requirement.Permission}");
        }
    }
}

/// <summary>
/// Extension methods for admin authorization.
/// </summary>
public static class AdminAuthorizationExtensions
{
    /// <summary>
    /// Checks if the user has any of the specified admin roles.
    /// </summary>
    public static bool IsAdministrator(this System.Security.Claims.ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        return user.IsInRole(AdminAuthorizationPolicies.SuperAdminRole)
            || user.IsInRole(AdminAuthorizationPolicies.AdminRole);
    }

    /// <summary>
    /// Checks if the user has the SuperAdmin role.
    /// </summary>
    public static bool IsSuperAdministrator(this System.Security.Claims.ClaimsPrincipal user)
    {
        return user?.IsInRole(AdminAuthorizationPolicies.SuperAdminRole) == true;
    }

    /// <summary>
    /// Adds admin authorization services to the service collection.
    /// </summary>
    public static IServiceCollection AddAdminAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, AdminPermissionHandler>();
        return services;
    }
}
