// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Security.Cryptography;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Middleware;

/// <summary>
/// Middleware that adds security headers to protect against common web vulnerabilities.
/// Implements OWASP security header recommendations with nonce-based CSP.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;

    /// <summary>
    /// HttpContext item key for CSP nonce value.
    /// Use this key to retrieve the nonce in views/templates: @Context.Items["csp-nonce"]
    /// </summary>
    public const string CspNonceKey = "csp-nonce";

    public SecurityHeadersMiddleware(
        RequestDelegate next,
        IWebHostEnvironment environment,
        ILogger<SecurityHeadersMiddleware> logger)
    {
        _next = Guard.NotNull(next);
        _environment = Guard.NotNull(environment);
        _logger = Guard.NotNull(logger);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        Guard.NotNull(context);

        try
        {
            // Generate cryptographically secure nonce for CSP
            var nonce = GenerateCspNonce();
            context.Items[CspNonceKey] = nonce;

            // Add security headers before processing the request
            AddSecurityHeaders(context, _environment, nonce);

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

    private static void AddSecurityHeaders(HttpContext context, IWebHostEnvironment environment, string cspNonce)
    {
        var headers = context.Response.Headers;
        var isProduction = environment.IsProduction();

        // Strict-Transport-Security (HSTS)
        // Forces HTTPS - use preload only in production
        if (context.Request.IsHttps)
        {
            headers["Strict-Transport-Security"] = isProduction
                ? "max-age=31536000; includeSubDomains; preload"
                : "max-age=86400; includeSubDomains";
        }

        // X-Content-Type-Options
        // Prevents MIME type sniffing
        headers["X-Content-Type-Options"] = "nosniff";

        // X-Frame-Options
        // Prevents clickjacking attacks
        headers["X-Frame-Options"] = "DENY";

        // X-XSS-Protection header removed (deprecated by modern browsers)
        // Use Content-Security-Policy instead for XSS protection
        // Modern browsers ignore this header and it can introduce security issues in older browsers

        // Referrer-Policy
        // Controls referrer information
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Content-Security-Policy with nonce-based script protection
        // Prevents XSS, clickjacking, and other code injection attacks
        // Nonce-based CSP provides strong XSS protection without unsafe-inline/unsafe-eval
        //
        // IMPORTANT: In views/templates, use the nonce for inline scripts:
        //   <script nonce="@Context.Items["csp-nonce"]">...</script>
        //
        // Production: Strict nonce-based policy with no unsafe directives
        // Development: Relaxed for Swagger/debugging tools (still uses nonce for additional security)
        headers["Content-Security-Policy"] = isProduction
            ? $"default-src 'self'; " +
              $"script-src 'nonce-{cspNonce}' 'self' 'strict-dynamic'; " +
              $"style-src 'self' 'unsafe-inline'; " +  // CSS can safely use unsafe-inline
              $"img-src 'self' data: https:; " +
              $"font-src 'self'; " +
              $"connect-src 'self'; " +
              $"object-src 'none'; " +
              $"base-uri 'self'; " +
              $"form-action 'self'; " +
              $"frame-ancestors 'none'; " +
              $"upgrade-insecure-requests"
            : $"default-src 'self'; " +
              $"script-src 'nonce-{cspNonce}' 'self' 'unsafe-inline' 'unsafe-eval'; " +  // Relaxed for Swagger
              $"style-src 'self' 'unsafe-inline'; " +
              $"img-src 'self' data: https:; " +
              $"font-src 'self' data:; " +
              $"connect-src 'self'; " +
              $"object-src 'none'; " +
              $"base-uri 'self'; " +
              $"form-action 'self'; " +
              $"frame-ancestors 'none'";

        // Permissions-Policy (formerly Feature-Policy)
        // Restricts browser features
        headers["Permissions-Policy"] =
            "accelerometer=(), " +
            "camera=(), " +
            "geolocation=(), " +
            "gyroscope=(), " +
            "magnetometer=(), " +
            "microphone=(), " +
            "payment=(), " +
            "usb=()";

        // X-Permitted-Cross-Domain-Policies
        // Restricts Adobe Flash and PDF cross-domain requests
        headers["X-Permitted-Cross-Domain-Policies"] = "none";

        // Remove server header to avoid information disclosure
        headers.Remove("Server");
        headers.Remove("X-Powered-By");
        headers.Remove("X-AspNet-Version");
        headers.Remove("X-AspNetMvc-Version");
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
