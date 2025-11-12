// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Host.Health;
using Honua.Server.Host.HealthChecks;
using Honua.Server.Host.Raster;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Honua.Server.Host.Extensions;

/// <summary>
/// Extension methods for configuring health checks.
/// </summary>
public static class HealthCheckExtensions
{
    /// <summary>
    /// Adds Honua health checks including metadata, data sources, and schema validation.
    /// Configures startup, liveness, and readiness probes for Kubernetes-style deployments.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddHonuaHealthChecks(this IServiceCollection services)
    {
        services.AddSingleton<IDataSourceHealthContributor, SqliteDataSourceHealthContributor>();
        services.AddSingleton<IDataSourceHealthContributor, PostgresDataSourceHealthContributor>();

        services.AddHealthChecks()
            .AddCheck<MetadataHealthCheck>("metadata", failureStatus: HealthStatus.Unhealthy, tags: new[] { "startup", "ready" })
            // .AddCheck<CacheConsistencyHealthCheck>("cache_consistency", failureStatus: HealthStatus.Degraded, tags: new[] { "ready", "cache" }) // TODO: Fix CacheConsistencyHealthCheck implementation
            .AddCheck<DataSourceHealthCheck>("dataSources", failureStatus: HealthStatus.Unhealthy, tags: new[] { "ready" })
            .AddCheck<DatabaseConnectivityHealthCheck>("database_connectivity", failureStatus: HealthStatus.Unhealthy, tags: new[] { "ready", "database" })
            .AddCheck<SchemaHealthCheck>("schema", failureStatus: HealthStatus.Degraded, tags: new[] { "ready" })
            .AddCheck<CrsTransformationHealthCheck>("crs_transformation", failureStatus: HealthStatus.Degraded, tags: new[] { "ready" })
            .AddCheck<RedisStoresHealthCheck>("redisStores", failureStatus: HealthStatus.Degraded, tags: new[] { "ready", "distributed" })
            .AddCheck<OidcDiscoveryHealthCheck>("oidc", failureStatus: HealthStatus.Degraded, tags: new[] { "ready", "oidc" }, timeout: TimeSpan.FromSeconds(5))
            .AddCheck("self", () => HealthCheckResult.Healthy("Application is running."), tags: new[] { "live" })
            .ForwardToPrometheus(); // Export health check results as Prometheus metrics

        return services;
    }
}
