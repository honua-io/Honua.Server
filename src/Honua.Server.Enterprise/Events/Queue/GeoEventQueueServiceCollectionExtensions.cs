// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Enterprise.Events.Queue.Repositories;
using Honua.Server.Enterprise.Events.Queue.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.Events.Queue;

/// <summary>
/// Extension methods for registering GeoEvent Queue services
/// </summary>
public static class GeoEventQueueServiceCollectionExtensions
{
    /// <summary>
    /// Add durable event queue services for guaranteed delivery
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="connectionString">PostgreSQL connection string</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddGeoEventQueue(
        this IServiceCollection services,
        string connectionString,
        IConfiguration configuration)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
        }

        // Configure options
        services.Configure<GeoEventQueueOptions>(
            configuration.GetSection(GeoEventQueueOptions.SectionName));

        // Register queue repository
        services.AddSingleton<IGeofenceEventQueueRepository>(sp =>
            new PostgresGeofenceEventQueueRepository(
                connectionString,
                sp.GetRequiredService<ILogger<PostgresGeofenceEventQueueRepository>>()));

        // Register durable event publisher
        services.AddSingleton<IDurableEventPublisher, DurableEventPublisher>();

        // Register background consumer service
        services.AddHostedService<GeofenceEventQueueConsumerService>();

        // Register optional Azure Service Bus publisher
        services.AddSingleton<AzureServiceBusEventPublisher>();

        return services;
    }

    /// <summary>
    /// Add durable event queue services with default configuration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="connectionString">PostgreSQL connection string</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddGeoEventQueue(
        this IServiceCollection services,
        string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
        }

        // Configure with default options
        services.Configure<GeoEventQueueOptions>(options =>
        {
            options.PollingIntervalSeconds = 5;
            options.BatchSize = 10;
            options.RetentionDays = 30;
            options.EnableServiceBus = false;
        });

        // Register queue repository
        services.AddSingleton<IGeofenceEventQueueRepository>(sp =>
            new PostgresGeofenceEventQueueRepository(
                connectionString,
                sp.GetRequiredService<ILogger<PostgresGeofenceEventQueueRepository>>()));

        // Register durable event publisher
        services.AddSingleton<IDurableEventPublisher, DurableEventPublisher>();

        // Register background consumer service
        services.AddHostedService<GeofenceEventQueueConsumerService>();

        return services;
    }
}
