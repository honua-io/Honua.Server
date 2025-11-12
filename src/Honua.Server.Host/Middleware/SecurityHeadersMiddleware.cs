// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Security.Cryptography;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Configuration;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Host.Middleware;

/// <summary>
/// Middleware that adds security headers to protect against common web vulnerabilities.
/// Implements OWASP security header recommendations with nonce-based CSP.
/// Configurable via appsettings.json SecurityHeaders section.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;
    private readonly SecurityHeadersOptions _options;

    /// <summary>
    /// HttpContext item key for CSP nonce value.
    /// Use this key to retrieve the nonce in views/templates: @Context.Items["csp-nonce"]
    /// </summary>
    public const string CspNonceKey = "csp-nonce";

    public SecurityHeadersMiddleware(
        RequestDelegate next,
        IWebHostEnvironment environment,
        ILogger<SecurityHeadersMiddleware> logger,
        IOptions<SecurityHeadersOptions> options)
    {
        _next = Guard.NotNull(next);
        _environment = Guard.NotNull(environment);
        _logger = Guard.NotNull(logger);
        _options = Guard.NotNull(options).Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        Guard.NotNull(context);

        // Skip if middleware is disabled
        if (!_options.Enabled)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        try
        {
            // Generate cryptographically secure nonce for CSP
            var nonce = GenerateCspNonce();
            context.Items[CspNonceKey] = nonce;

            // Add security headers before processing the request
            AddSecurityHeaders(context, _environment, nonce, _options);

            await _next(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error in SecurityHeadersMiddleware for {Path} from {RemoteIp}",
                context.Request.Path,
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
            throw;
        }
    }

    /// <summary>
    /// Generates a cryptographically secure random nonce for Content Security Policy.
    /// </summary>
    /// <returns>Base64-encoded random nonce.</returns>
    private static string GenerateCspNonce()
    {
        Span<byte> nonceBytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(nonceBytes);
        return Convert.ToBase64String(nonceBytes);
    }

    private static void AddSecurityHeaders(HttpContext context, IWebHostEnvironment environment, string cspNonce, SecurityHeadersOptions options)
    {
        var headers = context.Response.Headers;
        var isProduction = environment.IsProduction();

        // Strict-Transport-Security (HSTS)
        // Forces HTTPS - use preload only in production
        if (options.EnableHsts && context.Request.IsHttps)
        {
            // Skip HSTS if configured for production-only and not in production
            if (!options.HstsProductionOnly || isProduction)
            {
                var hstsValue = options.StrictTransportSecurity;
                if (string.IsNullOrWhiteSpace(hstsValue))
                {
                    // Default HSTS values
                    hstsValue = isProduction
                        ? "max-age=31536000; includeSubDomains; preload"
                        : "max-age=86400; includeSubDomains";
                }

                if (!headers.ContainsKey("Strict-Transport-Security"))
                {
                    headers["Strict-Transport-Security"] = hstsValue;
                }
            }
        }

        // X-Content-Type-Options
        // Prevents MIME type sniffing
        var xContentTypeOptions = options.XContentTypeOptions ?? "nosniff";
        if (!string.IsNullOrWhiteSpace(xContentTypeOptions) && !headers.ContainsKey("X-Content-Type-Options"))
        {
            headers["X-Content-Type-Options"] = xContentTypeOptions;
        }

        // X-Frame-Options
        // Prevents clickjacking attacks
        var xFrameOptions = options.XFrameOptions ?? "DENY";
        if (!string.IsNullOrWhiteSpace(xFrameOptions) && !headers.ContainsKey("X-Frame-Options"))
        {
            headers["X-Frame-Options"] = xFrameOptions;
        }

        // X-XSS-Protection header removed (deprecated by modern browsers)
        // Use Content-Security-Policy instead for XSS protection
        // Modern browsers ignore this header and it can introduce security issues in older browsers

        // Referrer-Policy
        // Controls referrer information
        var referrerPolicy = options.ReferrerPolicy ?? "strict-origin-when-cross-origin";
        if (!string.IsNullOrWhiteSpace(referrerPolicy) && !headers.ContainsKey("Referrer-Policy"))
        {
            headers["Referrer-Policy"] = referrerPolicy;
        }

        // Content-Security-Policy with nonce-based script protection
        // Prevents XSS, clickjacking, and other code injection attacks
        // Nonce-based CSP provides strong XSS protection without unsafe-inline/unsafe-eval
        //
        // IMPORTANT: In views/templates, use the nonce for inline scripts:
        //   <script nonce="@Context.Items["csp-nonce"]">...</script>
        //
        // Production: Strict nonce-based policy with no unsafe directives
        // Development: Relaxed for Swagger/debugging tools (still uses nonce for additional security)
        var cspValue = options.ContentSecurityPolicy;
        if (string.IsNullOrWhiteSpace(cspValue))
        {
            // Build default CSP based on environment
            var scriptSrc = isProduction
                ? $"'nonce-{cspNonce}' 'self' 'strict-dynamic'"
                : $"'nonce-{cspNonce}' 'self'" +
                  (options.AllowUnsafeInlineInDevelopment ? " 'unsafe-inline'" : "") +
                  (options.AllowUnsafeEvalInDevelopment ? " 'unsafe-eval'" : "");

            var fontSrc = isProduction ? "'self'" : "'self' data:";

            cspValue = $"default-src 'self'; " +
                      $"script-src {scriptSrc}; " +
                      $"style-src 'self' 'unsafe-inline'; " +  // CSS can safely use unsafe-inline
                      $"img-src 'self' data: https:; " +
                      $"font-src {fontSrc}; " +
                      $"connect-src 'self'; " +
                      $"object-src 'none'; " +
                      $"base-uri 'self'; " +
                      $"form-action 'self'; " +
                      $"frame-ancestors 'none'";

            if (isProduction)
            {
                cspValue += "; upgrade-insecure-requests";
            }
        }
        else
        {
            // Replace {nonce} placeholder with actual nonce
            cspValue = cspValue.Replace("{nonce}", cspNonce);
        }

        if (!headers.ContainsKey("Content-Security-Policy"))
        {
            headers["Content-Security-Policy"] = cspValue;
        }

        // Permissions-Policy (formerly Feature-Policy)
        // Restricts browser features
        var permissionsPolicy = options.PermissionsPolicy;
        if (string.IsNullOrWhiteSpace(permissionsPolicy))
        {
            permissionsPolicy =
                "accelerometer=(), " +
                "camera=(), " +
                "geolocation=(), " +
                "gyroscope=(), " +
                "magnetometer=(), " +
                "microphone=(), " +
                "payment=(), " +
                "usb=()";
        }

        if (!headers.ContainsKey("Permissions-Policy"))
        {
            headers["Permissions-Policy"] = permissionsPolicy;
        }

        // X-Permitted-Cross-Domain-Policies
        // Restricts Adobe Flash and PDF cross-domain requests
        var xPermittedCrossDomainPolicies = options.XPermittedCrossDomainPolicies ?? "none";
        if (!string.IsNullOrWhiteSpace(xPermittedCrossDomainPolicies) && !headers.ContainsKey("X-Permitted-Cross-Domain-Policies"))
        {
            headers["X-Permitted-Cross-Domain-Policies"] = xPermittedCrossDomainPolicies;
        }

        // Cross-Origin-Embedder-Policy (COEP)
        // Prevents a document from loading cross-origin resources that don't explicitly grant permission
        // Provides defense-in-depth against Spectre/Meltdown-class attacks
        if (!string.IsNullOrEmpty(options.CrossOriginEmbedderPolicy) && !headers.ContainsKey("Cross-Origin-Embedder-Policy"))
        {
            headers["Cross-Origin-Embedder-Policy"] = options.CrossOriginEmbedderPolicy;
        }

        // Cross-Origin-Opener-Policy (COOP)
        // Prevents cross-origin documents from being able to open a window and interact with it
        // Provides isolation for your browsing context group
        if (!string.IsNullOrEmpty(options.CrossOriginOpenerPolicy) && !headers.ContainsKey("Cross-Origin-Opener-Policy"))
        {
            headers["Cross-Origin-Opener-Policy"] = options.CrossOriginOpenerPolicy;
        }

        // Cross-Origin-Resource-Policy (CORP)
        // Controls which origins can load a given resource
        // Protects against cross-origin attacks
        if (!string.IsNullOrEmpty(options.CrossOriginResourcePolicy) && !headers.ContainsKey("Cross-Origin-Resource-Policy"))
        {
            headers["Cross-Origin-Resource-Policy"] = options.CrossOriginResourcePolicy;
        }

        // Remove server header to avoid information disclosure
        if (options.RemoveServerHeaders)
        {
            headers.Remove("Server");
            headers.Remove("X-Powered-By");
            headers.Remove("X-AspNet-Version");
            headers.Remove("X-AspNetMvc-Version");
        }
    }
}

/// <summary>
/// Extension methods for adding security headers middleware.
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
