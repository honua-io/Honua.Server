// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.ComponentModel.DataAnnotations;

namespace Honua.Server.Host.Configuration;

/// <summary>
/// Configuration options for security headers middleware.
/// Controls OWASP-recommended security headers for protecting against common web vulnerabilities.
/// </summary>
public sealed class SecurityHeadersOptions
{
    /// <summary>
    /// Gets or sets whether security headers middleware is enabled.
    /// Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the Content-Security-Policy header value.
    /// Prevents XSS, clickjacking, and code injection attacks.
    /// Use {nonce} placeholder for dynamic nonce injection.
    /// Default: Production-safe nonce-based policy.
    /// </summary>
    public string? ContentSecurityPolicy { get; set; }

    /// <summary>
    /// Gets or sets the Strict-Transport-Security (HSTS) header value.
    /// Forces HTTPS connections for the specified duration.
    /// Only applied when request is HTTPS.
    /// Default: "max-age=31536000; includeSubDomains; preload" in Production,
    ///          "max-age=86400; includeSubDomains" in Development.
    /// </summary>
    public string? StrictTransportSecurity { get; set; }

    /// <summary>
    /// Gets or sets the Referrer-Policy header value.
    /// Controls how much referrer information is sent with requests.
    /// Default: "strict-origin-when-cross-origin".
    /// </summary>
    public string? ReferrerPolicy { get; set; }

    /// <summary>
    /// Gets or sets the Permissions-Policy header value.
    /// Restricts browser features like camera, microphone, geolocation.
    /// Default: Denies all sensitive features.
    /// </summary>
    public string? PermissionsPolicy { get; set; }

    /// <summary>
    /// Gets or sets the X-Frame-Options header value.
    /// Prevents clickjacking attacks.
    /// Default: "DENY".
    /// </summary>
    public string? XFrameOptions { get; set; }

    /// <summary>
    /// Gets or sets the X-Content-Type-Options header value.
    /// Prevents MIME type sniffing.
    /// Default: "nosniff".
    /// </summary>
    public string? XContentTypeOptions { get; set; }

    /// <summary>
    /// Gets or sets the X-Permitted-Cross-Domain-Policies header value.
    /// Restricts Adobe Flash and PDF cross-domain requests.
    /// Default: "none".
    /// </summary>
    public string? XPermittedCrossDomainPolicies { get; set; }

    /// <summary>
    /// Gets or sets the Cross-Origin-Embedder-Policy header value.
    /// Controls whether the document can load cross-origin resources.
    /// Provides defense-in-depth against Spectre/Meltdown-class attacks.
    /// Recommended: "require-corp" for enhanced security.
    /// </summary>
    public string? CrossOriginEmbedderPolicy { get; set; }

    /// <summary>
    /// Gets or sets the Cross-Origin-Opener-Policy header value.
    /// Controls whether a document can open a cross-origin window.
    /// Provides isolation against cross-origin attacks.
    /// Recommended: "same-origin" for enhanced security.
    /// </summary>
    public string? CrossOriginOpenerPolicy { get; set; }

    /// <summary>
    /// Gets or sets the Cross-Origin-Resource-Policy header value.
    /// Controls whether a resource can be loaded by cross-origin requests.
    /// Provides protection against cross-origin attacks.
    /// Recommended: "same-origin" for enhanced security.
    /// </summary>
    public string? CrossOriginResourcePolicy { get; set; }

    /// <summary>
    /// Gets or sets whether to remove server identification headers.
    /// Removes: Server, X-Powered-By, X-AspNet-Version, X-AspNetMvc-Version.
    /// Default: true.
    /// </summary>
    public bool RemoveServerHeaders { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to allow unsafe-inline for scripts in Development.
    /// Required for Swagger and debugging tools.
    /// Ignored in Production (always uses strict nonce-based CSP).
    /// Default: true.
    /// </summary>
    public bool AllowUnsafeInlineInDevelopment { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to allow unsafe-eval for scripts in Development.
    /// Required for some development tools.
    /// Ignored in Production (always uses strict nonce-based CSP).
    /// Default: true.
    /// </summary>
    public bool AllowUnsafeEvalInDevelopment { get; set; } = true;

    /// <summary>
    /// Gets or sets whether HSTS is enabled.
    /// When false, HSTS header is not added even on HTTPS requests.
    /// Default: true.
    /// </summary>
    public bool EnableHsts { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to only apply HSTS in Production.
    /// When true, HSTS is only enabled when environment is Production.
    /// Default: false (HSTS enabled in all environments when EnableHsts is true).
    /// </summary>
    public bool HstsProductionOnly { get; set; } = false;

    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "SecurityHeaders";
}
