// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Logging;

/// <summary>
/// Validates production security configuration and logs warnings for insecure settings.
/// </summary>
public sealed class ProductionSecurityValidator
{
    private readonly ILogger<ProductionSecurityValidator> _logger;

    public ProductionSecurityValidator(ILogger<ProductionSecurityValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void ValidateProductionSecurity(HonuaAuthenticationOptions authOptions, bool isProduction)
    {
        if (!isProduction)
        {
            return;
        }

        var warnings = 0;

        // Check authentication configuration - QuickStart is BLOCKED in production
        if (authOptions.Mode == HonuaAuthenticationOptions.AuthenticationMode.QuickStart)
        {
            throw new InvalidOperationException(
                "SECURITY ERROR: QuickStart authentication mode is NOT allowed in Production environment. " +
                "QuickStart disables JWT validation and is only safe for local development and testing. " +
                "Set HonuaAuthentication:Mode to 'Local' or 'Oidc' for production use. " +
                "This check prevents accidentally deploying with insecure authentication settings.");
        }

        if (authOptions.Enforce == false)
        {
            _logger.LogWarning(
                "SECURITY WARNING: Authentication enforcement is DISABLED in production. " +
                "Set Authentication:Enforce=true to require authentication for protected endpoints.");
            warnings++;
        }

        // Check Local mode settings
        if (authOptions.Mode == HonuaAuthenticationOptions.AuthenticationMode.Local && authOptions.Local != null)
        {
            // Check session lifetime
            if (authOptions.Local.SessionLifetime > TimeSpan.FromHours(24))
            {
                _logger.LogWarning(
                    "SECURITY WARNING: Session lifetime is {SessionLifetime}. " +
                    "Consider shorter sessions (< 24 hours) for production security.",
                    authOptions.Local.SessionLifetime);
                warnings++;
            }

            // Check lockout settings
            if (authOptions.Local.MaxFailedAttempts > 10)
            {
                _logger.LogWarning(
                    "SECURITY WARNING: Account lockout threshold is {MaxAttempts}. " +
                    "Consider a lower threshold (5-10 attempts) to prevent brute force attacks.",
                    authOptions.Local.MaxFailedAttempts);
                warnings++;
            }

            // Check signing key path is configured
            if (string.IsNullOrWhiteSpace(authOptions.Local.SigningKeyPath))
            {
                _logger.LogWarning(
                    "SECURITY WARNING: No signing key path configured in production. " +
                    "Set Local:SigningKeyPath to persist signing keys across restarts.");
                warnings++;
            }
        }

        // Summary
        if (warnings > 0)
        {
            _logger.LogWarning(
                "SECURITY SUMMARY: Found {WarningCount} security configuration warnings in production environment. " +
                "Review logs above and update configuration for production deployment.",
                warnings);
        }
        else
        {
            _logger.LogInformation("Production security configuration validated successfully - no warnings detected.");
        }
    }
}
