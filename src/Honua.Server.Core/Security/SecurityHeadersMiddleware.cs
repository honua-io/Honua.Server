// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Security;

/// <summary>
/// Middleware that adds comprehensive security headers to HTTP responses.
/// Implements OWASP security best practices for web application security.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;
    private readonly SecurityHeadersOptions _options;

    public SecurityHeadersMiddleware(
        RequestDelegate next,
        ILogger<SecurityHeadersMiddleware> logger,
        SecurityHeadersOptions options)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers before processing the request
        AddSecurityHeaders(context);

        await _next(context);
    }

    private void AddSecurityHeaders(HttpContext context)
    {
        var headers = context.Response.Headers;

        // X-Content-Type-Options: Prevents MIME type sniffing
        if (_options.EnableXContentTypeOptions)
        {
            headers["X-Content-Type-Options"] = "nosniff";
        }

        // X-Frame-Options: Prevents clickjacking attacks
        if (_options.EnableXFrameOptions)
        {
            headers["X-Frame-Options"] = _options.XFrameOptionsValue;
        }

        // X-XSS-Protection: Enables browser XSS protection (legacy, but good defense-in-depth)
        if (_options.EnableXssProtection)
        {
            headers["X-XSS-Protection"] = "1; mode=block";
        }

        // Referrer-Policy: Controls referrer information
        if (_options.EnableReferrerPolicy)
        {
            headers["Referrer-Policy"] = _options.ReferrerPolicyValue;
        }

        // Content-Security-Policy: Prevents XSS, injection attacks
        if (_options.EnableContentSecurityPolicy && !string.IsNullOrEmpty(_options.ContentSecurityPolicyValue))
        {
            headers["Content-Security-Policy"] = _options.ContentSecurityPolicyValue;
        }

        // Strict-Transport-Security: Forces HTTPS (only add if connection is HTTPS)
        if (_options.EnableHsts && context.Request.IsHttps)
        {
            headers["Strict-Transport-Security"] = _options.HstsValue;
        }

        // Permissions-Policy: Controls browser features and APIs
        if (_options.EnablePermissionsPolicy && !string.IsNullOrEmpty(_options.PermissionsPolicyValue))
        {
            headers["Permissions-Policy"] = _options.PermissionsPolicyValue;
        }

        // Remove server header to avoid fingerprinting
        if (_options.RemoveServerHeader)
        {
            headers.Remove("Server");
        }

        // Remove X-Powered-By header to avoid technology disclosure
        if (_options.RemoveXPoweredByHeader)
        {
            headers.Remove("X-Powered-By");
        }
    }
}

/// <summary>
/// Configuration options for security headers.
/// </summary>
public class SecurityHeadersOptions
{
    /// <summary>
    /// Enable X-Content-Type-Options: nosniff header. Default: true.
    /// </summary>
    public bool EnableXContentTypeOptions { get; set; } = true;

    /// <summary>
    /// Enable X-Frame-Options header. Default: true.
    /// </summary>
    public bool EnableXFrameOptions { get; set; } = true;

    /// <summary>
    /// X-Frame-Options value. Default: DENY.
    /// Options: DENY, SAMEORIGIN, ALLOW-FROM uri
    /// </summary>
    public string XFrameOptionsValue { get; set; } = "DENY";

    /// <summary>
    /// Enable X-XSS-Protection header. Default: true.
    /// </summary>
    public bool EnableXssProtection { get; set; } = true;

    /// <summary>
    /// Enable Referrer-Policy header. Default: true.
    /// </summary>
    public bool EnableReferrerPolicy { get; set; } = true;

    /// <summary>
    /// Referrer-Policy value. Default: strict-origin-when-cross-origin.
    /// </summary>
    public string ReferrerPolicyValue { get; set; } = "strict-origin-when-cross-origin";

    /// <summary>
    /// Enable Content-Security-Policy header. Default: false (requires configuration).
    /// </summary>
    public bool EnableContentSecurityPolicy { get; set; } = false;

    /// <summary>
    /// Content-Security-Policy value. Must be configured based on application needs.
    /// Example: "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'"
    /// </summary>
    public string? ContentSecurityPolicyValue { get; set; }

    /// <summary>
    /// Enable HTTP Strict Transport Security (HSTS). Default: true.
    /// </summary>
    public bool EnableHsts { get; set; } = true;

    /// <summary>
    /// HSTS header value. Default: max-age=31536000; includeSubDomains (1 year).
    /// </summary>
    public string HstsValue { get; set; } = "max-age=31536000; includeSubDomains";

    /// <summary>
    /// Enable Permissions-Policy header. Default: true.
    /// </summary>
    public bool EnablePermissionsPolicy { get; set; } = true;

    /// <summary>
    /// Permissions-Policy value. Default: restrictive policy.
    /// </summary>
    public string PermissionsPolicyValue { get; set; } = "geolocation=(), microphone=(), camera=()";

    /// <summary>
    /// Remove Server header. Default: true.
    /// </summary>
    public bool RemoveServerHeader { get; set; } = true;

    /// <summary>
    /// Remove X-Powered-By header. Default: true.
    /// </summary>
    public bool RemoveXPoweredByHeader { get; set; } = true;
}

/// <summary>
/// Extension methods for adding security headers middleware.
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    /// <summary>
    /// Adds the security headers middleware to the application pipeline.
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(
        this IApplicationBuilder app,
        Action<SecurityHeadersOptions>? configureOptions = null)
    {
        var options = new SecurityHeadersOptions();
        configureOptions?.Invoke(options);

        return app.UseMiddleware<SecurityHeadersMiddleware>(options);
    }

    /// <summary>
    /// Adds security headers services to the service collection.
    /// </summary>
    public static IServiceCollection AddSecurityHeaders(
        this IServiceCollection services,
        Action<SecurityHeadersOptions>? configureOptions = null)
    {
        var options = new SecurityHeadersOptions();
        configureOptions?.Invoke(options);

        services.AddSingleton(options);
        return services;
    }
}
