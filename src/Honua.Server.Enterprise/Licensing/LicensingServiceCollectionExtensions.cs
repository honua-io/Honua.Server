// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Enterprise.Licensing.Models;
using Honua.Server.Enterprise.Licensing.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Honua.Server.Enterprise.Licensing;

/// <summary>
/// Extension methods for registering licensing services in the dependency injection container.
/// </summary>
public static class LicensingServiceCollectionExtensions
{
    /// <summary>
    /// Adds Honua licensing services to the service collection.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration containing licensing settings</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddHonuaLicensing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Register configuration
        services.Configure<LicenseOptions>(
            configuration.GetSection(LicenseOptions.SectionName));

        // Validate configuration
        services.AddSingleton<IValidateOptions<LicenseOptions>, LicenseOptionsValidator>();

        // Register stores
        services.AddSingleton<ILicenseStore, LicenseStore>();
        services.AddSingleton<ICredentialRevocationStore, CredentialRevocationStore>();

        // Register core services
        services.AddSingleton<ILicenseValidator, LicenseValidator>();
        services.AddSingleton<ILicenseManager, LicenseManager>();

        // Register feature flag service
        services.AddSingleton<Features.ILicenseFeatureFlagService, Features.LicenseFeatureFlagService>();

        // Note: ICredentialRevocationService implementation is registered in Core.Cloud
        // via AddCloudCredentialRevocation extension method

        // Register background service for license expiration monitoring
        services.AddHostedService<LicenseExpirationBackgroundService>();

        return services;
    }

    /// <summary>
    /// Adds Honua licensing services with custom options configuration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure license options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddHonuaLicensing(
        this IServiceCollection services,
        Action<LicenseOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        // Register configuration
        services.Configure(configureOptions);

        // Validate configuration
        services.AddSingleton<IValidateOptions<LicenseOptions>, LicenseOptionsValidator>();

        // Register stores
        services.AddSingleton<ILicenseStore, LicenseStore>();
        services.AddSingleton<ICredentialRevocationStore, CredentialRevocationStore>();

        // Register core services
        services.AddSingleton<ILicenseValidator, LicenseValidator>();
        services.AddSingleton<ILicenseManager, LicenseManager>();

        // Register feature flag service
        services.AddSingleton<Features.ILicenseFeatureFlagService, Features.LicenseFeatureFlagService>();

        // Note: ICredentialRevocationService implementation is registered in Core.Cloud
        // via AddCloudCredentialRevocation extension method

        // Register background service for license expiration monitoring
        services.AddHostedService<LicenseExpirationBackgroundService>();

        return services;
    }
}

/// <summary>
/// Validates LicenseOptions configuration at startup.
/// </summary>
internal sealed class LicenseOptionsValidator : IValidateOptions<LicenseOptions>
{
    public ValidateOptionsResult Validate(string? name, LicenseOptions options)
    {
        if (options == null)
        {
            return ValidateOptionsResult.Fail("LicenseOptions cannot be null");
        }

        var failures = new System.Collections.Generic.List<string>();

        // Validate signing key
        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            failures.Add("SigningKey is required");
        }
        else if (options.SigningKey.Length < 32)
        {
            failures.Add("SigningKey must be at least 32 characters (256 bits)");
        }

        // Validate issuer
        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("Issuer is required");
        }
        else if (!Uri.TryCreate(options.Issuer, UriKind.Absolute, out _))
        {
            failures.Add("Issuer must be a valid absolute URI");
        }

        // Validate audience
        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Audience is required");
        }

        // Validate connection string
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            failures.Add("ConnectionString is required");
        }

        // Validate provider
        var validProviders = new[] { "postgres", "postgresql", "mysql", "sqlite" };
        var provider = options.Provider?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(provider) || !Array.Exists(validProviders, p => p == provider))
        {
            failures.Add($"Provider must be one of: {string.Join(", ", validProviders)}");
        }

        // Validate expiration check interval
        if (options.ExpirationCheckInterval <= TimeSpan.Zero)
        {
            failures.Add("ExpirationCheckInterval must be greater than zero");
        }
        else if (options.ExpirationCheckInterval < TimeSpan.FromMinutes(5))
        {
            failures.Add("ExpirationCheckInterval should be at least 5 minutes to avoid excessive database queries");
        }

        // Validate warning threshold
        if (options.WarningThresholdDays <= 0)
        {
            failures.Add("WarningThresholdDays must be greater than zero");
        }
        else if (options.WarningThresholdDays > 90)
        {
            failures.Add("WarningThresholdDays should not exceed 90 days");
        }

        // Validate SMTP configuration if provided
        if (options.Smtp != null)
        {
            if (string.IsNullOrWhiteSpace(options.Smtp.Host))
            {
                failures.Add("Smtp.Host is required when SMTP is configured");
            }

            if (options.Smtp.Port <= 0 || options.Smtp.Port > 65535)
            {
                failures.Add("Smtp.Port must be between 1 and 65535");
            }

            if (string.IsNullOrWhiteSpace(options.Smtp.FromEmail))
            {
                failures.Add("Smtp.FromEmail is required when SMTP is configured");
            }
            else if (!IsValidEmail(options.Smtp.FromEmail))
            {
                failures.Add("Smtp.FromEmail must be a valid email address");
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
