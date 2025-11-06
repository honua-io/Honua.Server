// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.GeometryValidation;
using Honua.Server.Core.Security;
using Honua.Server.Host.Middleware;
using Honua.Server.Host.Observability;
using Honua.Server.Host.Wfs.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Extensions;

/// <summary>
/// Extension methods for configuring the web application middleware pipeline.
/// </summary>
internal static class WebApplicationExtensions
{
    /// <summary>
    /// Configures security-related middleware including HSTS, HTTPS redirection, and security headers.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for method chaining.</returns>
    public static WebApplication UseHonuaSecurity(this WebApplication app)
    {
        // Security headers - apply early in pipeline
        // TODO: Implement UseSecurityHeaders middleware extension method
        // app.UseSecurityHeaders();

        // HSTS and HTTPS redirection - enforce HTTPS in production
        if (!app.Environment.IsDevelopment())
        {
            // HSTS (HTTP Strict Transport Security) - MUST come before UseHttpsRedirection
            // Tells browsers to only use HTTPS for this domain
            app.UseHsts();

            // HTTPS redirection - redirects HTTP to HTTPS
            app.UseHttpsRedirection();
        }

        return app;
    }

    /// <summary>
    /// Configures exception handling middleware with environment-specific behavior.
    /// Uses .NET 8+ IExceptionHandler interface for modern exception handling.
    /// Provides RFC 7807 Problem Details responses for both controllers and minimal APIs.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for method chaining.</returns>
    public static WebApplication UseHonuaExceptionHandling(this WebApplication app)
    {
        // Use the new IExceptionHandler interface
        // This works for both MVC controllers AND minimal API endpoints
        // Must be called early in the pipeline to catch all exceptions
        app.UseExceptionHandler();

        return app;
    }

    /// <summary>
    /// Configures host filtering to prevent host header attacks.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for method chaining.</returns>
    public static WebApplication UseHonuaHostFiltering(this WebApplication app)
    {
        app.UseHostFiltering();

        return app;
    }

    /// <summary>
    /// Configures compression middleware.
    /// Note: Output caching must be called after UseRouting(), so it's separate.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for method chaining.</returns>
    public static WebApplication UseHonuaCompression(this WebApplication app)
    {
        app.UseResponseCompression();

        return app;
    }

    /// <summary>
    /// Configures request/response logging middleware for observability.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for method chaining.</returns>
    public static WebApplication UseHonuaRequestLogging(this WebApplication app)
    {
        // TODO: Implement UseRequestResponseLogging middleware extension method
        // app.UseRequestResponseLogging(options =>
        // {
        //     options.LogRequests = app.Configuration.GetValue("Observability:RequestLogging:Enabled", true);
        //     options.LogResponses = app.Configuration.GetValue("Observability:RequestLogging:Enabled", true);
        //     options.LogHeaders = app.Configuration.GetValue("Observability:RequestLogging:LogHeaders", false);
        //     options.SlowRequestThresholdMs = app.Configuration.GetValue("Observability:RequestLogging:SlowThresholdMs", 5000L);
        // });

        return app;
    }

    /// <summary>
    /// Configures input validation middleware to sanitize and validate requests.
    /// Uses in-app validation as fallback when YARP proxy isn't fronting the site.
    /// IMPORTANT: SecureInputValidationFilter enforces 100MB request size limits at action filter level.
    /// For production deployments with YARP, the proxy should also enforce limits.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for method chaining.</returns>
    public static WebApplication UseHonuaInputValidation(this WebApplication app)
    {
        // Issue #31 fix: Input validation is now handled by SecureInputValidationFilter
        // registered as a global action filter in MVC configuration.
        // This ensures request size limits and model validation are enforced
        // even without YARP fronting the application.
        // No middleware registration needed - the action filter runs on all MVC endpoints.

        return app;
    }

    /// <summary>
    /// Configures API metrics collection middleware.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for method chaining.</returns>
    public static WebApplication UseHonuaApiMetrics(this WebApplication app)
    {
        app.UseMiddleware<ApiMetricsMiddleware>();

        return app;
    }

    /// <summary>
    /// Configures CORS middleware with metadata-based policy provider.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for method chaining.</returns>
    public static WebApplication UseHonuaCorsMiddleware(this WebApplication app)
    {
        app.UseCors();

        return app;
    }

    /// <summary>
    /// Configures authentication and authorization middleware.
    /// Handles QuickStart mode validation and security checks.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for method chaining.</returns>
    public static WebApplication UseHonuaAuthenticationAndAuthorization(this WebApplication app)
    {
        var authOptions = app.Services.GetRequiredService<IOptions<HonuaAuthenticationOptions>>().Value;
        var quickStartActive = authOptions.Mode == HonuaAuthenticationOptions.AuthenticationMode.QuickStart && authOptions.QuickStart.Enabled;

        if (quickStartActive)
        {
            ValidateQuickStartMode(app, authOptions);
        }
        else
        {
            app.UseAuthentication();
        }

        // IMPORTANT: Always call UseAuthorization() even in QuickStart mode
        // Controllers may have [Authorize] attributes, and ASP.NET Core requires
        // the authorization middleware to be present to handle them properly.
        // QuickStart mode bypasses actual authentication, but authorization middleware
        // is still needed to process the authorization metadata.
        app.UseAuthorization();

        return app;
    }

    /// <summary>
    /// Configures CSRF protection middleware for state-changing operations.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for method chaining.</returns>
    public static WebApplication UseHonuaCsrfProtection(this WebApplication app)
    {
        // TODO: Implement UseCsrfValidation middleware extension method
        // app.UseCsrfValidation();
        return app;
    }

    /// <summary>
    /// Configures forwarded headers middleware with trusted proxy validation.
    /// SECURITY: Only trusts forwarded headers from validated proxies to prevent header injection attacks.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for method chaining.</returns>
    public static WebApplication UseHonuaForwardedHeaders(this WebApplication app)
    {
        // SECURITY FIX: Configure ForwardedHeadersOptions with trusted proxy validation
        var validator = app.Services.GetService<TrustedProxyValidator>();
        var configuration = app.Services.GetRequiredService<IConfiguration>();

        var forwardedHeadersOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
            // SECURITY: Require header symmetry - all forwarded headers must be present
            RequireHeaderSymmetry = true,
            ForwardLimit = 1  // Only process the first proxy in the chain
        };

        if (validator != null && validator.IsEnabled)
        {
            // Load trusted proxies from configuration
            var trustedProxiesConfig = configuration.GetSection("TrustedProxies").Get<string[]>() ?? Array.Empty<string>();
            var trustedNetworksConfig = configuration.GetSection("TrustedProxyNetworks").Get<string[]>() ?? Array.Empty<string>();

            // Parse and add individual IP addresses
            foreach (var proxyIp in trustedProxiesConfig)
            {
                if (!string.IsNullOrWhiteSpace(proxyIp) && System.Net.IPAddress.TryParse(proxyIp, out var ipAddress))
                {
                    forwardedHeadersOptions.KnownProxies.Add(ipAddress);
                }
            }

            // Parse and add CIDR networks
            foreach (var network in trustedNetworksConfig)
            {
                if (!string.IsNullOrWhiteSpace(network))
                {
                    var parts = network.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (parts.Length == 2 &&
                        System.Net.IPAddress.TryParse(parts[0], out var baseAddress) &&
                        int.TryParse(parts[1], out var prefixLength))
                    {
                        forwardedHeadersOptions.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(baseAddress, prefixLength));
                    }
                }
            }

            app.Logger.LogInformation(
                "Forwarded headers configured with {ProxyCount} trusted proxies and {NetworkCount} trusted networks",
                forwardedHeadersOptions.KnownProxies.Count,
                forwardedHeadersOptions.KnownNetworks.Count);

            // BUG FIX #41: Only call UseForwardedHeaders when proxies are actually configured
            // This ensures HTTPS link generation works correctly when behind a reverse proxy
            app.UseForwardedHeaders(forwardedHeadersOptions);
        }
        else
        {
            var requireTrustedProxies = configuration.GetValue("honua:security:requireTrustedProxies", false);

            if (requireTrustedProxies)
            {
                const string message = "SECURITY ERROR: honua:security:requireTrustedProxies=true but no trusted proxies are configured. " +
                    "Configure TrustedProxies or TrustedProxyNetworks before running behind a reverse proxy.";
                app.Logger.LogCritical(message);
                throw new InvalidOperationException(message);
            }

            // No trusted proxies configured - clear default options to prevent trusting any forwarded headers
            forwardedHeadersOptions.KnownProxies.Clear();
            forwardedHeadersOptions.KnownNetworks.Clear();
            app.Logger.LogWarning(
                "SECURITY: No trusted proxies configured. Forwarded headers will NOT be processed. " +
                "Configure TrustedProxies or TrustedProxyNetworks in appsettings.json if running behind a reverse proxy.");
        }

        return app;
    }

    /// <summary>
    /// Validates that QuickStart mode is properly configured and not used in production.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <param name="authOptions">The authentication options.</param>
    private static void ValidateQuickStartMode(WebApplication app, HonuaAuthenticationOptions authOptions)
    {
        // BUG FIX #42: Only validate when QuickStart is actually enabled
        // CI/staging environments flagged as "Production" should not crash when QuickStart is disabled
        if (!authOptions.QuickStart.Enabled)
        {
            return;
        }

        // QuickStart mode should NEVER be used outside development profiles
        if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("QuickStart"))
        {
            const string message = "SECURITY ERROR: QuickStart authentication mode is only supported in Development/QuickStart environments. " +
                                  "Configure Local or OIDC authentication before deploying to staged or production environments.";
            app.Logger.LogCritical(message);
            throw new InvalidOperationException(message);
        }

        var allowQuickStart = app.Configuration.GetValue<bool?>("honua:authentication:allowQuickStart") ?? false;
        if (!allowQuickStart)
        {
            var envFlag = Environment.GetEnvironmentVariable("HONUA_ALLOW_QUICKSTART");
            allowQuickStart = string.Equals(envFlag, "true", StringComparison.OrdinalIgnoreCase);
        }

        if (!allowQuickStart)
        {
            const string message = "QuickStart authentication mode is disabled. Set HONUA_ALLOW_QUICKSTART=true or configure honua:authentication:allowQuickStart for test profiles.";
            app.Logger.LogCritical(message);
            throw new InvalidOperationException(message);
        }

        // Log warning about QuickStart mode being active
        app.Logger.LogWarning("QuickStart authentication mode is ACTIVE. This mode bypasses authentication and should ONLY be used for development/testing.");
    }

    /// <summary>
    /// Configures request localization middleware for multi-language support.
    /// Uses Accept-Language header, query string (?culture=fr-FR), or cookie to determine culture.
    /// Adds Content-Language response header for OGC compliance.
    ///
    /// IMPORTANT: This middleware enforces InvariantCulture for CurrentCulture to ensure
    /// geospatial data formatting is always culture-invariant (coordinates use period as decimal
    /// separator, dates use ISO 8601). Only CurrentUICulture is set from the request to localize
    /// error messages and user-facing strings.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for method chaining.</returns>
    public static WebApplication UseHonuaLocalization(this WebApplication app)
    {
        var localizationOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
        app.UseRequestLocalization(localizationOptions.Value);

        // CRITICAL: Override CurrentCulture to InvariantCulture for API data formatting
        // This ensures all geospatial data (coordinates, dates, numbers) use culture-invariant
        // formatting, preventing bugs like:
        // - French locale formatting 52.5 as "52,5" (breaks GeoJSON)
        // - German locale formatting dates incorrectly (breaks ISO 8601)
        // - Turkish locale "I".ToLower() = "ı" instead of "i" (breaks identifiers)
        app.Use(async (context, next) =>
        {
            // Force CurrentCulture to InvariantCulture for all data formatting operations
            // This affects: ToString(), string.Format(), Parse(), etc.
            System.Globalization.CultureInfo.CurrentCulture =
                System.Globalization.CultureInfo.InvariantCulture;

            // CurrentUICulture is already set by RequestLocalizationMiddleware above
            // This affects: IStringLocalizer resource lookups (error messages, UI strings)

            await next();

            // Add Content-Language header if not already set (OGC compliance)
            // Only set if response hasn't started (headers are still writable)
            if (!context.Response.HasStarted && !context.Response.Headers.ContainsKey("Content-Language"))
            {
                // Use CurrentUICulture (not CurrentCulture) for language header
                var culture = System.Globalization.CultureInfo.CurrentUICulture;
                context.Response.Headers.ContentLanguage = culture.TwoLetterISOLanguageName;
            }
        });

        return app;
    }

    /// <summary>
    /// Configures the complete Honua middleware pipeline in the correct order.
    /// Following Microsoft's recommended middleware order for ASP.NET Core.
    /// See: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/#middleware-order
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for method chaining.</returns>
    public static WebApplication UseHonuaMiddlewarePipeline(this WebApplication app)
    {
        // 1. Exception handling (MUST be first to catch all exceptions)
        app.UseHonuaExceptionHandling();

        // 2. Forwarded headers (MUST be early to process X-Forwarded-* headers from proxies/load balancers)
        // SECURITY FIX: Configure with trusted proxy validation
        app.UseHonuaForwardedHeaders();

        // 3. API Documentation (available in all environments)
        app.UseHonuaApiDocumentation();

        // 4. Security (headers, HTTPS/HSTS)
        app.UseHonuaSecurity();

        // 5. Host filtering
        app.UseHonuaHostFiltering();

        // 6. Response compression (before routing)
        app.UseHonuaCompression();

        // 7. Request/response logging
        app.UseHonuaRequestLogging();

        // 8. Legacy API redirect (BEFORE routing to intercept non-versioned URLs)
        app.UseLegacyApiRedirect();

        // 9. Routing (required before CORS, output cache, and rate limiting)
        app.UseRouting();

        // 10. Request localization (AFTER routing, sets culture from Accept-Language header or query string)
        app.UseHonuaLocalization();

        // 11. API versioning (AFTER routing to access route values)
        // TODO: Implement UseApiVersioning middleware extension method
        // app.UseApiVersioning();

        // 12. Deprecation warnings (AFTER API versioning)
        // TODO: Implement UseDeprecationWarnings middleware extension method
        // app.UseDeprecationWarnings();

        // 13. Output caching (MUST be after UseRouting() per Microsoft docs)
        // TODO: Implement UseHonuaCaching middleware extension method
        // app.UseHonuaCaching();

        // 14. CORS (after routing, before authentication)
        app.UseHonuaCorsMiddleware();

        // 15. Rate limiting - Handled by YARP gateway

        // 16. Input validation
        app.UseHonuaInputValidation();

        // 17. API metrics
        app.UseHonuaApiMetrics();

        // 18. Authentication and authorization
        app.UseHonuaAuthenticationAndAuthorization();

        // 19. CSRF protection (after authentication)
        app.UseHonuaCsrfProtection();

        // 20. Security policy enforcement (after authorization, before endpoint execution)
        // TODO: Implement UseSecurityPolicy middleware extension method
        // app.UseSecurityPolicy();

        // 21. Initialize geometry complexity validator for WFS/GML parsing (DOS protection)
        InitializeGeometryComplexityValidator(app);

        return app;
    }

    /// <summary>
    /// Initializes the geometry complexity validator for GML geometry parsing.
    /// This provides DOS protection by validating geometry complexity before processing.
    /// </summary>
    private static void InitializeGeometryComplexityValidator(WebApplication app)
    {
        var validator = app.Services.GetService<GeometryComplexityValidator>();
        if (validator != null)
        {
            GmlGeometryParser.SetComplexityValidator(validator);
            app.Logger.LogInformation(
                "Geometry complexity validation enabled: MaxVertices={MaxVertices}, MaxRings={MaxRings}, MaxDepth={MaxDepth}",
                validator.Options.MaxVertexCount,
                validator.Options.MaxRingCount,
                validator.Options.MaxNestingDepth);
        }
        else
        {
            app.Logger.LogWarning("Geometry complexity validator not registered. DOS protection disabled.");
        }
    }

    /// <summary>
    /// Configures health check endpoints for Kubernetes liveness and readiness probes.
    ///
    /// Endpoints:
    /// - /health: Comprehensive health status (all checks)
    /// - /health/ready: Readiness probe (database, cache, storage)
    /// - /health/live: Liveness probe (returns 200 if app is running)
    ///
    /// Returns RFC-compliant JSON responses with health status and diagnostic data.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for method chaining.</returns>
    public static WebApplication UseHonuaHealthChecks(this WebApplication app)
    {
        var healthCheckOptions = app.Configuration
            .GetSection(HealthChecks.HealthCheckOptions.SectionName)
            .Get<HealthChecks.HealthCheckOptions>() ?? new HealthChecks.HealthCheckOptions();

        // /health - Comprehensive health check (all checks)
        // Returns detailed status of all health checks
        app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            ResponseWriter = WriteDetailedHealthCheckResponse,
            AllowCachingResponses = false
        });

        // /health/ready - Readiness probe for Kubernetes
        // Returns healthy only if all critical services (database, cache, storage) are operational
        // Use this for Kubernetes readiness probe to ensure traffic only goes to ready instances
        app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteDetailedHealthCheckResponse,
            AllowCachingResponses = false
        });

        // /health/live - Liveness probe for Kubernetes
        // Returns 200 OK if the application is running (no actual checks performed)
        // Use this for Kubernetes liveness probe to detect deadlocks/infinite loops
        app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = _ => false, // No checks - just returns 200 if app is running
            ResponseWriter = WriteSimpleHealthCheckResponse,
            AllowCachingResponses = false
        });

        // Add Health Checks UI if enabled
        if (healthCheckOptions.EnableUI)
        {
            app.MapHealthChecksUI(setup =>
            {
                setup.UIPath = healthCheckOptions.UIPath;
                setup.ApiPath = $"{healthCheckOptions.UIPath}/api";
            });

            app.Logger.LogInformation(
                "Health Checks UI enabled at {UIPath}",
                healthCheckOptions.UIPath);
        }

        app.Logger.LogInformation(
            "Health check endpoints configured: /health, /health/ready, /health/live");

        return app;
    }

    /// <summary>
    /// Writes a detailed health check response with diagnostic data.
    /// </summary>
    private static Task WriteDetailedHealthCheckResponse(
        HttpContext context,
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            entries = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.TotalMilliseconds,
                description = e.Value.Description,
                data = e.Value.Data,
                tags = e.Value.Tags
            })
        }, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        return context.Response.WriteAsync(result);
    }

    /// <summary>
    /// Writes a simple health check response without detailed diagnostic data.
    /// Used when EnableDetailedErrors is false (typically in production).
    /// </summary>
    private static Task WriteSimpleHealthCheckResponse(
        HttpContext context,
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds
        });

        return context.Response.WriteAsync(result);
    }
}
