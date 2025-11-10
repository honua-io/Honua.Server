// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Enterprise.Events.Notifications;
using Honua.Server.Enterprise.Events.Repositories;
using Honua.Server.Enterprise.Events.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.Events;

/// <summary>
/// Extension methods for registering GeoEvent services
/// </summary>
public static class GeoEventServiceCollectionExtensions
{
    /// <summary>
    /// Add GeoEvent geofencing services with notification support
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="connectionString">PostgreSQL connection string</param>
    /// <param name="configuration">Application configuration for notifications</param>
    /// <param name="alertReceiverBaseUrl">Base URL for the alert receiver service (optional)</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddGeoEventServices(
        this IServiceCollection services,
        string connectionString,
        IConfiguration? configuration = null,
        string? alertReceiverBaseUrl = null)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
        }

        // Register repositories
        services.AddSingleton<IGeofenceRepository>(sp =>
            new PostgresGeofenceRepository(
                connectionString,
                sp.GetRequiredService<ILogger<PostgresGeofenceRepository>>()));

        services.AddSingleton<IEntityStateRepository>(sp =>
            new PostgresEntityStateRepository(
                connectionString,
                sp.GetRequiredService<ILogger<PostgresEntityStateRepository>>()));

        services.AddSingleton<IGeofenceEventRepository>(sp =>
            new PostgresGeofenceEventRepository(
                connectionString,
                sp.GetRequiredService<ILogger<PostgresGeofenceEventRepository>>()));

        // Register services
        services.AddSingleton<IGeofenceEvaluationService, GeofenceEvaluationService>();
        services.AddSingleton<IGeofenceManagementService, GeofenceManagementService>();

        // Register notification services if configuration provided
        if (configuration != null)
        {
            AddNotificationServices(services, configuration);
        }

        // Register alert integration services if alert receiver URL is provided
        if (!string.IsNullOrEmpty(alertReceiverBaseUrl))
        {
            AddAlertIntegrationServices(services, connectionString, alertReceiverBaseUrl);
        }

        return services;
    }

    /// <summary>
    /// Add notification services (webhooks, email, etc.)
    /// </summary>
    private static void AddNotificationServices(IServiceCollection services, IConfiguration configuration)
    {
        // Configure webhook notifier
        services.Configure<WebhookNotifierOptions>(
            configuration.GetSection(WebhookNotifierOptions.SectionName));

        // Configure email notifier
        services.Configure<EmailNotifierOptions>(
            configuration.GetSection(EmailNotifierOptions.SectionName));

        // Register HTTP client for webhook notifier
        services.AddHttpClient<IGeofenceEventNotifier, WebhookNotifier>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WebhookNotifierOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

            if (!string.IsNullOrWhiteSpace(options.AuthenticationHeader))
            {
                client.DefaultRequestHeaders.Add("Authorization", options.AuthenticationHeader);
            }
        });

        // Register email notifier
        services.AddSingleton<IGeofenceEventNotifier, EmailNotifier>();

        // Register notification orchestration service
        services.AddSingleton<IGeofenceEventNotificationService, GeofenceEventNotificationService>();
    }

    /// <summary>
    /// Add alert integration services for geofence-to-alert bridging
    /// </summary>
    private static void AddAlertIntegrationServices(IServiceCollection services, string connectionString, string alertReceiverBaseUrl)
    {
        // Register HTTP client for alert receiver
        services.AddHttpClient("AlertReceiver", client =>
        {
            client.BaseAddress = new Uri(alertReceiverBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Register geofence alert repository (using same connection as geofence services)
        services.AddScoped<Honua.Server.Core.Repositories.IGeofenceAlertRepository>(sp =>
        {
            var connection = new Npgsql.NpgsqlConnection(connectionString);
            return new Honua.Server.Core.Repositories.GeofenceAlertRepository(
                connection,
                sp.GetRequiredService<ILogger<Honua.Server.Core.Repositories.GeofenceAlertRepository>>());
        });

        // Register bridge service
        services.AddSingleton<IGeofenceToAlertBridgeService>(sp =>
            new GeofenceToAlertBridgeService(
                sp.GetRequiredService<Honua.Server.Core.Repositories.IGeofenceAlertRepository>(),
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<ILogger<GeofenceToAlertBridgeService>>(),
                alertReceiverBaseUrl));
    }
}
