// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Enterprise.Authentication;

/// <summary>
/// Extension methods for registering SAML SSO services
/// </summary>
public static class SamlServiceCollectionExtensions
{
    /// <summary>
    /// Adds SAML Single Sign-On (SSO) services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The application configuration</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddSamlSso(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Bind configuration
        var samlSection = configuration.GetSection("Saml");
        services.Configure<SamlSsoOptions>(samlSection);

        var samlOptions = samlSection.Get<SamlSsoOptions>();
        if (samlOptions == null || !samlOptions.Enabled)
        {
            // SAML is not enabled, skip registration
            return services;
        }

        // Validate required configuration
        if (string.IsNullOrEmpty(samlOptions.ServiceProvider.EntityId))
        {
            throw new InvalidOperationException(
                "Saml:ServiceProvider:EntityId configuration is required when SAML SSO is enabled.");
        }

        if (string.IsNullOrEmpty(samlOptions.ServiceProvider.BaseUrl))
        {
            throw new InvalidOperationException(
                "Saml:ServiceProvider:BaseUrl configuration is required when SAML SSO is enabled.");
        }

        // Get database connection string
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string is required for SAML SSO.");
        }

        // Register stores
        services.AddSingleton<ISamlIdentityProviderStore>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<PostgresSamlIdentityProviderStore>>();
            return new PostgresSamlIdentityProviderStore(connectionString, logger);
        });

        services.AddSingleton<ISamlSessionStore>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<PostgresSamlSessionStore>>();
            return new PostgresSamlSessionStore(connectionString, logger);
        });

        // Register core SAML service
        services.AddSingleton<ISamlService, SamlService>();

        // Register user provisioning service
        services.AddSingleton<ISamlUserProvisioningService>(sp =>
        {
            var idpStore = sp.GetRequiredService<ISamlIdentityProviderStore>();
            var logger = sp.GetRequiredService<ILogger<SamlUserProvisioningService>>();
            return new SamlUserProvisioningService(connectionString, idpStore, logger);
        });

        // Register background service for session cleanup
        services.AddHostedService<SamlSessionCleanupService>();

        return services;
    }
}

/// <summary>
/// Background service for cleaning up expired SAML sessions
/// </summary>
internal class SamlSessionCleanupService : Microsoft.Extensions.Hosting.BackgroundService
{
    private readonly ISamlSessionStore _sessionStore;
    private readonly ILogger<SamlSessionCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);

    public SamlSessionCleanupService(
        ISamlSessionStore sessionStore,
        ILogger<SamlSessionCleanupService> logger)
    {
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SAML session cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _sessionStore.CleanupExpiredSessionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired SAML sessions");
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }

        _logger.LogInformation("SAML session cleanup service stopped");
    }
}
