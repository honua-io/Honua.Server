// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Configuration;

public sealed class HonuaAuthenticationOptions
{
    public const string SectionName = "honua:authentication";

    public AuthenticationMode Mode { get; set; } = AuthenticationMode.Local;

    public bool Enforce { get; set; }

    public QuickStartOptions QuickStart { get; set; } = new();

    public JwtOptions Jwt { get; set; } = new();

    public LocalOptions Local { get; set; } = new();

    public BootstrapOptions Bootstrap { get; set; } = new();

    public OidcProviderOptions Oidc { get; set; } = new();

    public AzureAdOptions AzureAd { get; set; } = new();

    public GoogleOptions Google { get; set; } = new();

    public enum AuthenticationMode
    {
        QuickStart,
        Oidc,
        Local
    }

    public sealed class QuickStartOptions
    {
        public bool Enabled { get; set; } = false;
    }

    public sealed class JwtOptions
    {
        public string? Authority { get; set; }

        public string? Audience { get; set; }

        public string? RoleClaimPath { get; set; }

        public bool RequireHttpsMetadata { get; set; } = true;
    }

    public sealed class LocalOptions
    {
        /// <summary>
        /// Local authentication provider.
        /// Supported values: sqlite, postgres, postgresql, mysql, sqlserver.
        /// </summary>
        public string Provider { get; set; } = "sqlite";

        public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromMinutes(30);

        public string StorePath { get; set; } = Path.Combine("data", "auth", "auth.db");

        /// <summary>
        /// Optional connection string for relational providers (Postgres, MySQL, SQL Server).
        /// </summary>
        public string? ConnectionString { get; set; }

        /// <summary>
        /// Name of the connection string entry to use. When specified, the value is resolved from ConnectionStrings.
        /// </summary>
        public string? ConnectionStringName { get; set; }

        /// <summary>
        /// Optional schema name for relational providers. Defaults to provider-specific system schema.
        /// </summary>
        public string? Schema { get; set; }

        public string? SigningKeyPath { get; set; }

        public int MaxFailedAttempts { get; set; } = 5;

        public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);

        public PasswordExpirationOptions PasswordExpiration { get; set; } = new();

        /// <summary>
        /// Clock skew tolerance for JWT token validation. Default is 5 minutes.
        /// This compensates for clock differences between token issuer and validator.
        /// </summary>
        public TimeSpan? ClockSkew { get; set; } = TimeSpan.FromMinutes(5);
    }

    public sealed class PasswordExpirationOptions
    {
        /// <summary>
        /// Enables password expiration policy. Default is false.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Password expiration period. Default is 90 days.
        /// </summary>
        public TimeSpan ExpirationPeriod { get; set; } = TimeSpan.FromDays(90);

        /// <summary>
        /// Grace period before password expires to show warnings. Default is 7 days.
        /// </summary>
        public TimeSpan WarningPeriod { get; set; } = TimeSpan.FromDays(7);

        /// <summary>
        /// Number of days before password expires to show first warning. Default is 7 days.
        /// </summary>
        public TimeSpan FirstWarningThreshold { get; set; } = TimeSpan.FromDays(7);

        /// <summary>
        /// Number of days before password expires to show urgent warning. Default is 1 day.
        /// </summary>
        public TimeSpan UrgentWarningThreshold { get; set; } = TimeSpan.FromDays(1);

        /// <summary>
        /// Grace period after expiration before account lockout. Default is 0 (immediate lockout).
        /// </summary>
        public TimeSpan GracePeriodAfterExpiration { get; set; } = TimeSpan.Zero;
    }

    public sealed class BootstrapOptions
    {
        public string? AdminUsername { get; set; }

        public string? AdminEmail { get; set; }

        public string? AdminPassword { get; set; }

        public string? AdminSubject { get; set; }

        public string? OutputPath { get; set; }
    }
}

public sealed class HonuaAuthenticationOptionsValidator : IValidateOptions<HonuaAuthenticationOptions>
{
    private readonly Microsoft.Extensions.Hosting.IHostEnvironment _environment;
    private readonly ConnectionStringOptions _connectionStrings;

    public HonuaAuthenticationOptionsValidator(
        Microsoft.Extensions.Hosting.IHostEnvironment environment,
        IOptions<ConnectionStringOptions> connectionStrings)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _connectionStrings = connectionStrings?.Value ?? new ConnectionStringOptions();
    }

    public ValidateOptionsResult Validate(string? name, HonuaAuthenticationOptions options)
    {
        var failures = new List<string>();

        switch (options.Mode)
        {
            case HonuaAuthenticationOptions.AuthenticationMode.Oidc:
                // Check if at least one OIDC provider is enabled
                var hasEnabledProvider = options.Oidc.Enabled || options.AzureAd.Enabled || options.Google.Enabled;
                if (!hasEnabledProvider)
                {
                    failures.Add("At least one OIDC provider must be enabled when authentication mode is Oidc. Enable Oidc, AzureAd, or Google provider.");
                }

                // Validate generic OIDC provider
                if (options.Oidc.Enabled)
                {
                    if (options.Oidc.Authority.IsNullOrWhiteSpace())
                    {
                        failures.Add("Oidc.Authority is required when Oidc provider is enabled.");
                    }
                    if (options.Oidc.ClientId.IsNullOrWhiteSpace())
                    {
                        failures.Add("Oidc.ClientId is required when Oidc provider is enabled.");
                    }
                    if (options.Oidc.ClientSecret.IsNullOrWhiteSpace())
                    {
                        failures.Add("Oidc.ClientSecret is required when Oidc provider is enabled.");
                    }
                }

                // Validate Azure AD provider
                if (options.AzureAd.Enabled)
                {
                    if (options.AzureAd.TenantId.IsNullOrWhiteSpace())
                    {
                        failures.Add("AzureAd.TenantId is required when Azure AD provider is enabled.");
                    }
                    if (options.AzureAd.ClientId.IsNullOrWhiteSpace())
                    {
                        failures.Add("AzureAd.ClientId is required when Azure AD provider is enabled.");
                    }
                    if (options.AzureAd.ClientSecret.IsNullOrWhiteSpace())
                    {
                        failures.Add("AzureAd.ClientSecret is required when Azure AD provider is enabled.");
                    }
                }

                // Validate Google provider
                if (options.Google.Enabled)
                {
                    if (options.Google.ClientId.IsNullOrWhiteSpace())
                    {
                        failures.Add("Google.ClientId is required when Google provider is enabled.");
                    }
                    if (options.Google.ClientSecret.IsNullOrWhiteSpace())
                    {
                        failures.Add("Google.ClientSecret is required when Google provider is enabled.");
                    }
                }

                // Legacy JWT-only validation (for backward compatibility)
                if (!hasEnabledProvider && options.Jwt.Authority.IsNullOrWhiteSpace())
                {
                    failures.Add("Jwt Authority must be provided when authentication mode is Oidc and no OIDC providers are enabled.");
                }

                if (!hasEnabledProvider && options.Jwt.Audience.IsNullOrWhiteSpace())
                {
                    failures.Add("Jwt Audience must be provided when authentication mode is Oidc and no OIDC providers are enabled.");
                }

                break;
            case HonuaAuthenticationOptions.AuthenticationMode.Local:
                var provider = (options.Local.Provider ?? "sqlite").Trim().ToLowerInvariant();

                if (provider is not ("sqlite" or "postgres" or "postgresql" or "mysql" or "sqlserver"))
                {
                    failures.Add($"Unsupported local authentication provider '{options.Local.Provider}'.");
                    break;
                }

                if (options.Local.SessionLifetime <= TimeSpan.Zero)
                {
                    failures.Add("Local SessionLifetime must be greater than zero.");
                }

                if (options.Local.MaxFailedAttempts <= 0)
                {
                    failures.Add("Local MaxFailedAttempts must be greater than zero.");
                }

                if (options.Local.LockoutDuration <= TimeSpan.Zero)
                {
                    failures.Add("Local LockoutDuration must be greater than zero.");
                }

                if (provider is "sqlite")
                {
                    if (options.Local.StorePath.IsNullOrWhiteSpace())
                    {
                        failures.Add("Local StorePath must be provided when using SQLite provider.");
                    }
                }
                else
                {
                    var resolved = ResolveConnectionString(options.Local.Provider ?? string.Empty, options.Local);
                    if (resolved.IsNullOrWhiteSpace())
                    {
                        failures.Add($"Connection string is required for local authentication provider '{options.Local.Provider}'. Set Local.ConnectionString, Local.ConnectionStringName, or ConnectionStrings__{options.Local.Provider}." );
                    }
                }

                // SECURITY: Validate AdminPassword in production
                if (_environment.IsProduction() && options.Bootstrap.AdminPassword.HasValue())
                {
                    failures.Add(
                        "SECURITY VIOLATION: AdminPassword must NOT be set in production configuration. " +
                        "Bootstrap.AdminPassword is intended for development/testing only. " +
                        "In production, create users through the admin interface or use OIDC authentication. " +
                        "Remove the 'HonuaAuthentication:Bootstrap:AdminPassword' setting from your production configuration.");
                }

                break;
            case HonuaAuthenticationOptions.AuthenticationMode.QuickStart:
                break;
            default:
                failures.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Unsupported authentication mode '{0}'.",
                        options.Mode));
                break;
        }

        if (options.Enforce && options.Mode == HonuaAuthenticationOptions.AuthenticationMode.QuickStart)
        {
            failures.Add("QuickStart mode cannot be enforced. Choose Oidc or Local when Enforce is true.");
        }

        if (options.Enforce && options.QuickStart.Enabled && options.Mode != HonuaAuthenticationOptions.AuthenticationMode.QuickStart)
        {
            failures.Add("QuickStart.Enabled must be false when Enforce is true for Oidc or Local mode.");
        }

        // SECURITY: Warn about AdminPassword in non-production environments
        if (!_environment.IsProduction() && options.Bootstrap.AdminPassword.HasValue())
        {
            // This is acceptable in development, but log a warning
            // The warning will be logged by the configuration system
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private string? ResolveConnectionString(string provider, HonuaAuthenticationOptions.LocalOptions localOptions)
    {
        if (localOptions.ConnectionString.HasValue())
        {
            return localOptions.ConnectionString;
        }

        if (localOptions.ConnectionStringName.HasValue())
        {
            return GetNamedConnectionString(localOptions.ConnectionStringName);
        }

        return provider.ToLowerInvariant() switch
        {
            "postgres" or "postgresql" => _connectionStrings.Postgres ?? _connectionStrings.DefaultConnection,
            "mysql" => _connectionStrings.MySql ?? _connectionStrings.DefaultConnection,
            "sqlserver" => _connectionStrings.SqlServer ?? _connectionStrings.DefaultConnection,
            _ => null
        };
    }

    private string? GetNamedConnectionString(string name)
    {
        if (name.IsNullOrWhiteSpace())
        {
            return null;
        }

        var normalized = name.Trim();

        if (normalized.Equals(nameof(ConnectionStringOptions.DefaultConnection), StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("DefaultConnection", StringComparison.OrdinalIgnoreCase))
        {
            return _connectionStrings.DefaultConnection;
        }

        if (normalized.Equals(nameof(ConnectionStringOptions.Postgres), StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Postgres", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            return _connectionStrings.Postgres;
        }

        if (normalized.Equals(nameof(ConnectionStringOptions.SqlServer), StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("SQLServer", StringComparison.OrdinalIgnoreCase))
        {
            return _connectionStrings.SqlServer;
        }

        if (normalized.Equals(nameof(ConnectionStringOptions.MySql), StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("MySql", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("MySQL", StringComparison.OrdinalIgnoreCase))
        {
            return _connectionStrings.MySql;
        }

        return null;
    }
}
