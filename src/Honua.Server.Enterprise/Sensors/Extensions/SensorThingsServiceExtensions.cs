using Honua.Server.Enterprise.Sensors.Data;
using Honua.Server.Enterprise.Sensors.Data.Postgres;
using Honua.Server.Enterprise.Sensors.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Data;

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
            // Register IDbConnection for SensorThings
            services.AddScoped<IDbConnection>(sp => new NpgsqlConnection(connectionString));

            // Register repository
            services.AddScoped<ISensorThingsRepository, PostgresSensorThingsRepository>();
        }

        // Register services (when implemented)
        // services.AddSingleton<SensorThingsCacheService>();
        // services.AddSingleton<SensorThingsMetrics>();
        // services.AddHealthChecks().AddCheck<SensorThingsHealthCheck>("sensorthings");

        return services;
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
