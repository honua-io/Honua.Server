// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Honua.Server.Observability.HealthChecks;

/// <summary>
/// Health check for container registry connectivity and status.
/// </summary>
public class RegistryHealthCheck : IHealthCheck
{
    private readonly string connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegistryHealthCheck"/> class.
    /// </summary>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    public RegistryHealthCheck(string connectionString)
    {
        this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Checks the health of container registry connectivity and status.
    /// </summary>
    /// <param name="context">The health check context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous health check operation.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(this.connectionString);
            await connection.OpenAsync(cancellationToken);

            // Check if registry table exists
            var registryTableExists = await connection.QuerySingleAsync<bool>(
                @"SELECT EXISTS (
                    SELECT FROM information_schema.tables
                    WHERE table_schema = 'public'
                    AND table_name = 'registries'
                )",
                cancellationToken);

            if (!registryTableExists)
            {
                return HealthCheckResult.Degraded(
                    "Registry table does not exist",
                    data: new Dictionary<string, object>
                    {
                        { "table_exists", false },
                    });
            }

            // Get registry statistics
            var stats = await connection.QuerySingleAsync<RegistryStats>(
                @"SELECT
                    COUNT(*) as total_registries,
                    COUNT(*) FILTER (WHERE is_active = true) as active_registries,
                    COUNT(DISTINCT provider) as unique_providers
                  FROM registries",
                cancellationToken);

            // Check for credentials expiring soon (within 7 days)
            var credentialsExpiringSoon = await connection.QuerySingleAsync<int>(
                @"SELECT COUNT(*) FROM registries
                  WHERE is_active = true
                  AND credentials_expire_at IS NOT NULL
                  AND credentials_expire_at BETWEEN NOW() AND NOW() + INTERVAL '7 days'",
                cancellationToken);

            // Check for expired credentials
            var expiredCredentials = await connection.QuerySingleAsync<int>(
                @"SELECT COUNT(*) FROM registries
                  WHERE is_active = true
                  AND credentials_expire_at IS NOT NULL
                  AND credentials_expire_at <= NOW()",
                cancellationToken);

            var data = new Dictionary<string, object>
            {
                { "total_registries", stats.TotalRegistries },
                { "active_registries", stats.ActiveRegistries },
                { "unique_providers", stats.UniqueProviders },
                { "credentials_expiring_soon", credentialsExpiringSoon },
                { "expired_credentials", expiredCredentials },
            };

            // Determine health status
            if (expiredCredentials > 0)
            {
                return HealthCheckResult.Unhealthy(
                    $"{expiredCredentials} registry credential(s) have expired",
                    data: data);
            }

            if (credentialsExpiringSoon > 0)
            {
                return HealthCheckResult.Degraded(
                    $"{credentialsExpiringSoon} registry credential(s) expiring within 7 days",
                    data: data);
            }

            if (stats.ActiveRegistries == 0)
            {
                return HealthCheckResult.Degraded(
                    "No active registries configured",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                $"{stats.ActiveRegistries} active registr{(stats.ActiveRegistries == 1 ? "y" : "ies")} across {stats.UniqueProviders} provider{(stats.UniqueProviders == 1 ? "" : "s")}",
                data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Registry health check failed",
                ex);
        }
    }

    /// <summary>
    /// Registry statistics data transfer object.
    /// </summary>
    private class RegistryStats
    {
        /// <summary>
        /// Gets or sets the total number of registries.
        /// </summary>
        public int TotalRegistries { get; set; }

        /// <summary>
        /// Gets or sets the number of active registries.
        /// </summary>
        public int ActiveRegistries { get; set; }

        /// <summary>
        /// Gets or sets the number of unique providers.
        /// </summary>
        public int UniqueProviders { get; set; }
    }
}
