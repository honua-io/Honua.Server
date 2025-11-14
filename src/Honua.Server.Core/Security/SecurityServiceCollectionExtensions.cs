// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Honua.Server.Core.Security;

/// <summary>
/// Extension methods for registering security services in the dependency injection container.
/// </summary>
public static class SecurityServiceCollectionExtensions
{
    /// <summary>
    /// Adds comprehensive security services including user identity, audit logging, and admin authorization.
    /// This registration resolves TODOs 001 and 002 by providing proper security infrastructure.
    /// </summary>
    public static IServiceCollection AddHonuaSecurityServices(this IServiceCollection services)
    {
        // User identity service (TODO-002 resolution)
        services.TryAddScoped<IUserIdentityService, UserIdentityService>();

        // Tenant context service (TODO-003 resolution - when enabled)
        services.TryAddScoped<ITenantContextService, TenantContextService>();

        // Audit logging service (Compliance requirement)
        services.TryAddScoped<IAuditLoggingService, AuditLoggingService>();

        // Admin authorization handler (TODO-001 resolution)
        services.AddSingleton<IAuthorizationHandler, AdminPermissionHandler>();

        // HttpContextAccessor required by UserIdentityService
        services.AddHttpContextAccessor();

        return services;
    }

    /// <summary>
    /// Configures admin authorization policies.
    /// </summary>
    public static IServiceCollection AddHonuaAdminAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            AdminAuthorizationPolicies.ConfigureAdminPolicies(options);
        });

        return services;
    }

    /// <summary>
    /// Adds all security services and configurations in one call.
    /// </summary>
    public static IServiceCollection AddHonuaSecurity(this IServiceCollection services)
    {
        services.AddHonuaSecurityServices();
        services.AddHonuaAdminAuthorization();
        services.AddSecurityHeaders();

        return services;
    }
}
