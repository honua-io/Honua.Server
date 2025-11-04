// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Host.Infrastructure;

/// <summary>
/// Detects whether the application is running behind a reverse proxy (YARP).
/// Used to determine if fallback rate limiting should be activated.
/// </summary>
/// <remarks>
/// <para>
/// Detection methods (in order of precedence):
/// </para>
/// <list type="number">
/// <item><description>Environment variable override (HONUA_BEHIND_REVERSE_PROXY)</description></item>
/// <item><description>Configuration setting (Honua:ReverseProxy:Enabled)</description></item>
/// <item><description>Runtime detection via X-Forwarded-* headers</description></item>
/// <item><description>Trusted proxy configuration check</description></item>
/// </list>
/// </remarks>
public sealed class ReverseProxyDetector
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReverseProxyDetector> _logger;
    private readonly bool? _explicitConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReverseProxyDetector"/> class.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public ReverseProxyDetector(IConfiguration configuration, ILogger<ReverseProxyDetector> logger)
    {
        _configuration = Guard.NotNull(configuration);
        _logger = Guard.NotNull(logger);

        // Check for explicit configuration at startup
        _explicitConfiguration = ResolveExplicitConfiguration();

        if (_explicitConfiguration.HasValue)
        {
            _logger.LogInformation(
                "Reverse proxy detection: Explicit configuration detected. Behind reverse proxy: {BehindProxy}",
                _explicitConfiguration.Value);
        }
    }

    /// <summary>
    /// Gets a value indicating whether explicit configuration has been set.
    /// </summary>
    public bool HasExplicitConfiguration => _explicitConfiguration.HasValue;

    /// <summary>
    /// Gets the explicitly configured reverse proxy state, if set.
    /// </summary>
    public bool? ExplicitConfiguration => _explicitConfiguration;

    /// <summary>
    /// Determines whether the application is running behind a reverse proxy.
    /// </summary>
    /// <param name="context">Optional HTTP context for runtime detection. If null, uses startup detection only.</param>
    /// <returns>True if behind a reverse proxy; otherwise, false.</returns>
    public bool IsBehindReverseProxy(HttpContext? context = null)
    {
        // Explicit configuration takes precedence
        if (_explicitConfiguration.HasValue)
        {
            return _explicitConfiguration.Value;
        }

        // Runtime detection based on headers
        if (context != null)
        {
            var hasForwardedHeaders = HasYarpHeaders(context);
            if (hasForwardedHeaders)
            {
                _logger.LogDebug("Reverse proxy detected via X-Forwarded-* headers");
                return true;
            }
        }

        // Check if trusted proxies are configured (implies reverse proxy setup)
        var hasTrustedProxies = HasTrustedProxiesConfigured();
        if (hasTrustedProxies)
        {
            _logger.LogDebug("Reverse proxy detected via trusted proxy configuration");
            return true;
        }

        _logger.LogDebug("No reverse proxy detected");
        return false;
    }

    /// <summary>
    /// Checks if the request contains YARP/proxy forwarding headers.
    /// </summary>
    /// <param name="context">HTTP context.</param>
    /// <returns>True if forwarding headers are present; otherwise, false.</returns>
    private static bool HasYarpHeaders(HttpContext context)
    {
        var headers = context.Request.Headers;

        // Check for standard forwarding headers used by YARP and other reverse proxies
        var hasXForwardedFor = headers.ContainsKey("X-Forwarded-For") && !string.IsNullOrWhiteSpace(headers["X-Forwarded-For"]);
        var hasXForwardedProto = headers.ContainsKey("X-Forwarded-Proto") && !string.IsNullOrWhiteSpace(headers["X-Forwarded-Proto"]);
        var hasXForwardedHost = headers.ContainsKey("X-Forwarded-Host") && !string.IsNullOrWhiteSpace(headers["X-Forwarded-Host"]);

        // Consider behind proxy if we have X-Forwarded-For plus at least one other forwarding header
        // This prevents false positives from clients manually setting headers
        return hasXForwardedFor && (hasXForwardedProto || hasXForwardedHost);
    }

    /// <summary>
    /// Checks if trusted proxies are configured in application settings.
    /// </summary>
    /// <returns>True if trusted proxies are configured; otherwise, false.</returns>
    private bool HasTrustedProxiesConfigured()
    {
        var trustedProxies = _configuration.GetSection("TrustedProxies").Get<string[]>();
        var trustedNetworks = _configuration.GetSection("TrustedProxyNetworks").Get<string[]>();

        return (trustedProxies?.Length > 0) || (trustedNetworks?.Length > 0);
    }

    /// <summary>
    /// Resolves explicit configuration from environment variables or appsettings.
    /// </summary>
    /// <returns>Explicit configuration value, or null if not configured.</returns>
    private bool? ResolveExplicitConfiguration()
    {
        // 1. Environment variable override (highest precedence)
        var envVar = Environment.GetEnvironmentVariable("HONUA_BEHIND_REVERSE_PROXY");
        if (!string.IsNullOrWhiteSpace(envVar))
        {
            if (bool.TryParse(envVar, out var envValue))
            {
                _logger.LogInformation(
                    "Reverse proxy configuration from environment variable: {Value}",
                    envValue);
                return envValue;
            }

            _logger.LogWarning(
                "Invalid HONUA_BEHIND_REVERSE_PROXY environment variable value: {Value}. Expected 'true' or 'false'.",
                envVar);
        }

        // 2. Configuration setting
        var configValue = _configuration.GetValue<bool?>("Honua:ReverseProxy:Enabled");
        if (configValue.HasValue)
        {
            _logger.LogInformation(
                "Reverse proxy configuration from appsettings: {Value}",
                configValue.Value);
            return configValue.Value;
        }

        // Alternative configuration path
        configValue = _configuration.GetValue<bool?>("ReverseProxy:Enabled");
        if (configValue.HasValue)
        {
            _logger.LogInformation(
                "Reverse proxy configuration from appsettings (ReverseProxy section): {Value}",
                configValue.Value);
            return configValue.Value;
        }

        // No explicit configuration
        return null;
    }
}
