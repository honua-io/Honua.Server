// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Extensions;

/// <summary>
/// Extension methods for configuring CSRF (Cross-Site Request Forgery) protection.
/// </summary>
internal static class CsrfProtectionExtensions
{
    /// <summary>
    /// Adds CSRF protection services with secure configuration.
    /// Configures antiforgery middleware with OWASP-recommended settings.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddHonuaCsrfProtection(
        this IServiceCollection services,
        IHostEnvironment environment)
    {
        services.AddAntiforgery(options =>
        {
            // Use custom header name for CSRF token
            // This allows JavaScript clients to easily include the token
            options.HeaderName = "X-CSRF-Token";

            // SECURITY: Cookie configuration following OWASP recommendations
            // The __Host- prefix provides additional security in production:
            // - Ensures cookie is set with Secure flag
            // - Ensures cookie is set from a secure origin
            // - Ensures cookie path is /
            // - Prevents cookie from being overridden by subdomains
            if (environment.IsProduction())
            {
                options.Cookie.Name = "__Host-X-CSRF-Token";
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            }
            else
            {
                // In development, use standard cookie name without __Host- prefix
                // since HTTPS is not always available locally
                options.Cookie.Name = "X-CSRF-Token";
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            }

            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.IsEssential = true;

            // Suppress auto-generation of form field
            // We use header-based tokens for API endpoints
            options.SuppressXFrameOptionsHeader = false;
        });

        // Register hosted service to log CSRF configuration on startup
        services.AddHostedService<CsrfConfigurationLoggingService>();

        return services;
    }
}

/// <summary>
/// Hosted service that logs CSRF configuration on startup.
/// This avoids the BuildServiceProvider() anti-pattern.
/// </summary>
internal sealed class CsrfConfigurationLoggingService : IHostedService
{
    private readonly ILogger<CsrfConfigurationLoggingService> _logger;
    private readonly IHostEnvironment _environment;

    public CsrfConfigurationLoggingService(
        ILogger<CsrfConfigurationLoggingService> logger,
        IHostEnvironment environment)
    {
        _logger = Guard.NotNull(logger);
        _environment = Guard.NotNull(environment);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var cookieName = _environment.IsProduction() ? "__Host-X-CSRF-Token" : "X-CSRF-Token";

        _logger.LogInformation(
            "CSRF protection enabled with cookie: {CookieName}, header: {HeaderName}, environment: {Environment}",
            cookieName,
            "X-CSRF-Token",
            _environment.EnvironmentName);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
