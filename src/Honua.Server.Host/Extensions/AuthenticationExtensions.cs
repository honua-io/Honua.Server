// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Configuration;
using Honua.Server.Host.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Honua.Server.Host.Extensions;

/// <summary>
/// Extension methods for configuring authentication and authorization.
/// </summary>
public static class AuthenticationExtensions
{
    /// <summary>
    /// Adds authentication services to the service collection.
    /// Configures JWT Bearer authentication with support for QuickStart, Local, and OIDC modes.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddHonuaAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Note: HonuaAuthenticationOptionsValidator is registered in the Core assembly
        var optionsBuilder = services.AddOptions<HonuaAuthenticationOptions>()
            .Bind(configuration.GetSection(HonuaAuthenticationOptions.SectionName));
        _ = Microsoft.Extensions.DependencyInjection.OptionsBuilderExtensions.ValidateOnStart(optionsBuilder);

        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, JwtBearerOptionsConfigurator>();

        var authenticationBuilder = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        });

        authenticationBuilder.AddJwtBearer();
        authenticationBuilder.AddScheme<AuthenticationSchemeOptions, LocalBasicAuthenticationHandler>(
            LocalBasicAuthenticationDefaults.Scheme,
            _ => { });

        return services;
    }

    /// <summary>
    /// Adds authorization policies to the service collection.
    /// Supports both enforced authentication (production) and permissive mode (QuickStart).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddHonuaAuthorization(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthorization(options =>
        {
            // Get authentication configuration to determine if enforcement is enabled
            var authConfig = configuration.GetSection(HonuaAuthenticationOptions.SectionName).Get<HonuaAuthenticationOptions>();
            var enforceAuth = authConfig?.Enforce ?? false;

            if (enforceAuth)
            {
                // When enforcement is enabled, require authenticated users with specific roles
                options.AddPolicy("RequireAdministrator", policy =>
                {
                    policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, LocalBasicAuthenticationDefaults.Scheme);
                    policy.RequireRole("administrator");
                });
                options.AddPolicy("RequireDataPublisher", policy =>
                {
                    policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, LocalBasicAuthenticationDefaults.Scheme);
                    policy.RequireRole("administrator", "datapublisher");
                });
                options.AddPolicy("RequireViewer", policy =>
                {
                    policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, LocalBasicAuthenticationDefaults.Scheme);
                    policy.RequireRole("administrator", "datapublisher", "viewer");
                });
            }
            else
            {
                // SECURITY: When enforcement is disabled (e.g., QuickStart mode), allow anonymous access
                // but still check roles if the user IS authenticated
                //
                // Logic explanation:
                // - If user is NOT authenticated: Allow access (return true)
                // - If user IS authenticated: Check if they have the required role
                //
                // Note: "context.User.Identity?.IsAuthenticated != true" means:
                //   - True when IsAuthenticated is false (not authenticated)
                //   - True when IsAuthenticated is null (not authenticated)
                //   - False when IsAuthenticated is true (authenticated)
                options.AddPolicy("RequireAdministrator", policy =>
                {
                    policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, LocalBasicAuthenticationDefaults.Scheme);
                    policy.RequireAssertion(context =>
                        context.User.Identity?.IsAuthenticated != true ||
                        context.User.IsInRole("administrator"));
                });

                options.AddPolicy("RequireDataPublisher", policy =>
                {
                    policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, LocalBasicAuthenticationDefaults.Scheme);
                    policy.RequireAssertion(context =>
                        context.User.Identity?.IsAuthenticated != true ||
                        context.User.IsInRole("administrator") ||
                        context.User.IsInRole("datapublisher"));
                });

                options.AddPolicy("RequireViewer", policy =>
                {
                    policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, LocalBasicAuthenticationDefaults.Scheme);
                    policy.RequireAssertion(context =>
                        context.User.Identity?.IsAuthenticated != true ||
                        context.User.IsInRole("administrator") ||
                        context.User.IsInRole("datapublisher") ||
                        context.User.IsInRole("viewer"));
                });
            }
        });

        return services;
    }
}
