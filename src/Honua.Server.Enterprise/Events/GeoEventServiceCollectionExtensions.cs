using Honua.Server.Enterprise.Events.Repositories;
using Honua.Server.Enterprise.Events.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.Events;

/// <summary>
/// Extension methods for registering GeoEvent services
/// </summary>
public static class GeoEventServiceCollectionExtensions
{
    /// <summary>
    /// Add GeoEvent geofencing services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="connectionString">PostgreSQL connection string</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddGeoEventServices(
        this IServiceCollection services,
        string connectionString)
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

        return services;
    }
}
