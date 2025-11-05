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
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddGeoEventServices(
        this IServiceCollection services,
        string connectionString,
        IConfiguration? configuration = null)
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
}
