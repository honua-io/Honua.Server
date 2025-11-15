// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Configuration;
using Honua.Server.Host.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

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

        // Add OIDC authentication schemes if in OIDC mode
        var authOptions = configuration.GetSection(HonuaAuthenticationOptions.SectionName).Get<HonuaAuthenticationOptions>();
        if (authOptions?.Mode == HonuaAuthenticationOptions.AuthenticationMode.Oidc)
        {
            services.AddOidcAuthentication(configuration, authOptions);
        }

        // Register HttpContextAccessor if not already registered (required by UserContext)
        services.AddHttpContextAccessor();

        // Register User Context service for extracting user identity and session information
        // This service provides access to authenticated user details, session IDs, and request metadata
        // for audit logging and tracking throughout the application
        services.AddScoped<IUserContext, UserContext>();

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

            // Build list of authentication schemes including OIDC providers
            var schemes = new List<string> { JwtBearerDefaults.AuthenticationScheme, LocalBasicAuthenticationDefaults.Scheme };
            if (authConfig?.Oidc.Enabled == true) schemes.Add("oidc");
            if (authConfig?.AzureAd.Enabled == true) schemes.Add("azuread");
            if (authConfig?.Google.Enabled == true) schemes.Add("google");
            var authSchemes = schemes.ToArray();

            if (enforceAuth)
            {
                // When enforcement is enabled, require authenticated users with specific roles
                options.AddPolicy("RequireUser", policy =>
                {
                    policy.AddAuthenticationSchemes(authSchemes);
                    policy.RequireAuthenticatedUser();
                });
                options.AddPolicy("RequireAdministrator", policy =>
                {
                    policy.AddAuthenticationSchemes(authSchemes);
                    policy.RequireRole("administrator");
                });
                options.AddPolicy("RequireEditor", policy =>
                {
                    policy.AddAuthenticationSchemes(authSchemes);
                    policy.RequireRole("administrator", "editor");
                });
                options.AddPolicy("RequireDataPublisher", policy =>
                {
                    policy.AddAuthenticationSchemes(authSchemes);
                    policy.RequireRole("administrator", "datapublisher");
                });
                options.AddPolicy("RequireViewer", policy =>
                {
                    policy.AddAuthenticationSchemes(authSchemes);
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
                options.AddPolicy("RequireUser", policy =>
                {
                    policy.AddAuthenticationSchemes(authSchemes);
                    policy.RequireAssertion(context =>
                        context.User.Identity?.IsAuthenticated == true);
                });

                options.AddPolicy("RequireAdministrator", policy =>
                {
                    policy.AddAuthenticationSchemes(authSchemes);
                    policy.RequireAssertion(context =>
                        context.User.Identity?.IsAuthenticated != true ||
                        context.User.IsInRole("administrator"));
                });

                options.AddPolicy("RequireEditor", policy =>
                {
                    policy.AddAuthenticationSchemes(authSchemes);
                    policy.RequireAssertion(context =>
                        context.User.Identity?.IsAuthenticated != true ||
                        context.User.IsInRole("administrator") ||
                        context.User.IsInRole("editor"));
                });

                options.AddPolicy("RequireDataPublisher", policy =>
                {
                    policy.AddAuthenticationSchemes(authSchemes);
                    policy.RequireAssertion(context =>
                        context.User.Identity?.IsAuthenticated != true ||
                        context.User.IsInRole("administrator") ||
                        context.User.IsInRole("datapublisher"));
                });

                options.AddPolicy("RequireViewer", policy =>
                {
                    policy.AddAuthenticationSchemes(authSchemes);
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

    /// <summary>
    /// Adds OpenID Connect authentication providers (generic OIDC, Azure AD, Google).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="authOptions">The authentication options.</param>
    /// <returns>The service collection for method chaining.</returns>
    private static IServiceCollection AddOidcAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        HonuaAuthenticationOptions authOptions)
    {
        var authenticationBuilder = new AuthenticationBuilder(services);

        // Add generic OIDC provider
        if (authOptions.Oidc.Enabled)
        {
            authenticationBuilder.AddOpenIdConnect("oidc", options =>
            {
                options.Authority = authOptions.Oidc.Authority;
                options.ClientId = authOptions.Oidc.ClientId;
                options.ClientSecret = authOptions.Oidc.ClientSecret;
                options.ResponseType = authOptions.Oidc.ResponseType;
                options.SaveTokens = authOptions.Oidc.SaveTokens;
                options.GetClaimsFromUserInfoEndpoint = authOptions.Oidc.GetClaimsFromUserInfoEndpoint;
                options.RequireHttpsMetadata = authOptions.Oidc.RequireHttpsMetadata;
                options.CallbackPath = authOptions.Oidc.CallbackPath;
                options.SignedOutCallbackPath = authOptions.Oidc.SignedOutCallbackPath;
                options.UsePkce = authOptions.Oidc.UsePkce;

                // Configure scopes
                options.Scope.Clear();
                foreach (var scope in authOptions.Oidc.Scopes)
                {
                    options.Scope.Add(scope);
                }

                // Configure token validation
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    NameClaimType = "name",
                    RoleClaimType = authOptions.Oidc.RoleClaimType ?? "roles"
                };

                // Apply allowed issuers if configured
                if (authOptions.Oidc.AllowedIssuers != null && authOptions.Oidc.AllowedIssuers.Length > 0)
                {
                    options.TokenValidationParameters.ValidIssuers = authOptions.Oidc.AllowedIssuers;
                }
            });
        }

        // Add Azure AD provider
        if (authOptions.AzureAd.Enabled)
        {
            authenticationBuilder.AddOpenIdConnect("azuread", options =>
            {
                var authority = $"{authOptions.AzureAd.Instance.TrimEnd('/')}/{authOptions.AzureAd.TenantId}/v2.0";
                options.Authority = authority;
                options.ClientId = authOptions.AzureAd.ClientId;
                options.ClientSecret = authOptions.AzureAd.ClientSecret;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = false; // Azure AD includes claims in ID token
                options.RequireHttpsMetadata = true;
                options.CallbackPath = authOptions.AzureAd.CallbackPath;
                options.UsePkce = true;

                // Configure scopes
                options.Scope.Clear();
                foreach (var scope in authOptions.AzureAd.Scopes)
                {
                    options.Scope.Add(scope);
                }

                // Configure token validation
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = authOptions.AzureAd.ValidateIssuer,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    NameClaimType = "name",
                    RoleClaimType = authOptions.AzureAd.RoleClaimType
                };

                // Handle Azure AD specific claims mapping
                options.Events = new OpenIdConnectEvents
                {
                    OnTokenValidated = context =>
                    {
                        // Azure AD returns user roles in the "roles" claim
                        // Additional claims transformation can be done here
                        return Task.CompletedTask;
                    }
                };
            });
        }

        // Add Google provider
        if (authOptions.Google.Enabled)
        {
            authenticationBuilder.AddGoogle("google", options =>
            {
                options.ClientId = authOptions.Google.ClientId!;
                options.ClientSecret = authOptions.Google.ClientSecret!;
                options.CallbackPath = authOptions.Google.CallbackPath;
                options.SaveTokens = true;

                // Configure scopes
                options.Scope.Clear();
                foreach (var scope in authOptions.Google.Scopes)
                {
                    options.Scope.Add(scope);
                }

                // Apply additional authorization parameters if configured
                if (authOptions.Google.AuthorizationParameters != null)
                {
                    foreach (var param in authOptions.Google.AuthorizationParameters)
                    {
                        options.AuthorizationEndpoint += $"&{param.Key}={param.Value}";
                    }
                }
            });
        }

        // Register claims transformation service
        services.AddTransient<Microsoft.AspNetCore.Authentication.IClaimsTransformation, OidcClaimsTransformation>();

        return services;
    }
}
