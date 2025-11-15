// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Enterprise.Sensors.Data;
using Honua.Server.Enterprise.Sensors.Data.Postgres;
using Honua.Server.Enterprise.Sensors.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Enterprise.Sensors.Extensions;

/// <summary>
/// Extension methods for registering SensorThings API services.
/// </summary>
public static class SensorThingsServiceExtensions
{
    /// <summary>
    /// Adds SensorThings API services to the dependency injection container.
    /// </summary>
    public static IServiceCollection AddSensorThings(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        var config = configuration.GetSection("SensorThings").Get<SensorThingsServiceDefinition>()
            ?? new SensorThingsServiceDefinition();

        services.AddSingleton(config);

        // Only register repository if connection string is available
        var connectionString = configuration.GetConnectionString("SensorThingsDb");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            // Register connection factory as Singleton (factory doesn't hold resources)
            services.AddSingleton<ISensorThingsDbConnectionFactory>(
                new NpgsqlSensorThingsDbConnectionFactory(connectionString));

            // Register repository
            services.AddScoped<ISensorThingsRepository, PostgresSensorThingsRepository>();
        }

        // Register WebSocket streaming if enabled
        if (config.WebSocketStreamingEnabled)
        {
            AddSensorThingsStreaming(services, configuration);
        }

        // Register services (when implemented)
        // services.AddSingleton<SensorThingsCacheService>();
        // services.AddSingleton<SensorThingsMetrics>();
        // services.AddHealthChecks().AddCheck<SensorThingsHealthCheck>("sensorthings");

        return services;
    }

    /// <summary>
    /// Adds SensorThings real-time streaming services (SignalR).
    /// </summary>
    private static void AddSensorThingsStreaming(
        IServiceCollection services,
        IConfiguration configuration)
    {
        // Note: The actual SignalR hub and broadcaster types are in Honua.Server.Host
        // We register the streaming options here so they can be injected

        // Bind streaming options from configuration
        var streamingOptions = configuration
            .GetSection("SensorThings:Streaming")
            .Get<SensorObservationStreamingOptions>()
            ?? new SensorObservationStreamingOptions { Enabled = true };

        services.AddSingleton(streamingOptions);
    }

    /// <summary>
    /// Adds SensorThings API services with explicit configuration.
    /// </summary>
    public static IServiceCollection AddSensorThings(
        this IServiceCollection services,
        SensorThingsServiceDefinition config)
    {
        services.AddSingleton(config);
        services.AddScoped<ISensorThingsRepository, PostgresSensorThingsRepository>();

        return services;
    }
}
