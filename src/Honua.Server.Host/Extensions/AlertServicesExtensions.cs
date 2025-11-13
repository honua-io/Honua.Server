// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.AlertReceiver.Services;
using Honua.Server.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;

namespace Honua.Server.Host.Extensions;

/// <summary>
/// Extension methods for registering alert management services.
/// </summary>
public static class AlertServicesExtensions
{
    /// <summary>
    /// Adds alert management services including alert rules, notification channels, and history.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddAlertManagementServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register HTTP clients for notification channel testing
        services.AddHttpClient("Slack");
        services.AddHttpClient("Teams");
        services.AddHttpClient("PagerDuty");
        services.AddHttpClient("Opsgenie");

        // Get PostgreSQL connection string for alerts
        var connectionString = configuration.GetConnectionString("Alerts")
            ?? configuration.GetConnectionString("Postgres")
            ?? configuration.GetConnectionString("DefaultConnection");

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            // Register connection factory for alert configuration
            services.AddSingleton<IAlertConfigurationDbConnectionFactory>(sp =>
                new PostgresAlertConfigurationDbConnectionFactory(connectionString));

            // Register alert configuration services
            services.AddScoped<IAlertConfigurationService, AlertConfigurationService>();
            services.AddScoped<INotificationChannelService, NotificationChannelService>();
            services.AddScoped<IAlertPublishingService, AlertPublishingService>();

            var logger = services.BuildServiceProvider().GetService<ILogger<PostgresAlertConfigurationDbConnectionFactory>>();
            logger?.LogInformation("Alert management services registered with PostgreSQL connection");
        }
        else
        {
            var logger = services.BuildServiceProvider().GetService<ILogger<PostgresAlertConfigurationDbConnectionFactory>>();
            logger?.LogWarning("Alert management services not registered - PostgreSQL connection string not configured");
        }

        return services;
    }
}

/// <summary>
/// PostgreSQL implementation of alert configuration database connection factory.
/// </summary>
public sealed class PostgresAlertConfigurationDbConnectionFactory : IAlertConfigurationDbConnectionFactory
{
    private readonly string connectionString;

    public PostgresAlertConfigurationDbConnectionFactory(string connectionString)
    {
        this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public IDbConnection CreateConnection()
    {
        return new NpgsqlConnection(this.connectionString);
    }
}
