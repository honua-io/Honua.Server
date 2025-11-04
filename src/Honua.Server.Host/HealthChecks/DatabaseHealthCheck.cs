// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.HealthChecks;

/// <summary>
/// Health check for database connectivity.
/// Tests connection to all configured data sources and verifies they are accessible.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly IDataStoreProviderFactory _dataStoreProviderFactory;
    private readonly IMetadataRegistry _metadataRegistry;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(
        IDataStoreProviderFactory dataStoreProviderFactory,
        IMetadataRegistry metadataRegistry,
        ILogger<DatabaseHealthCheck> logger)
    {
        _dataStoreProviderFactory = dataStoreProviderFactory;
        _metadataRegistry = metadataRegistry;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var unhealthyDataSources = new List<string>();
        var healthyDataSources = new List<string>();
        var data = new Dictionary<string, object>();

        try
        {
            // Get metadata snapshot
            var snapshot = await _metadataRegistry.GetSnapshotAsync(cancellationToken);
            var dataSources = snapshot.DataSources.ToList();

            if (dataSources.Count == 0)
            {
                // No data sources configured - this is degraded state
                _logger.LogWarning("No data sources configured");
                return HealthCheckResult.Degraded(
                    "No data sources configured",
                    data: new Dictionary<string, object>
                    {
                        ["dataSourceCount"] = 0
                    });
            }

            // Test connectivity to each data source
            foreach (var dataSource in dataSources)
            {
                try
                {
                    var provider = _dataStoreProviderFactory.Create(dataSource.Provider);

                    // Use TestConnectivityAsync method from IDataStoreProvider
                    await provider.TestConnectivityAsync(dataSource, cancellationToken);

                    healthyDataSources.Add(dataSource.Id);
                    _logger.LogDebug("Data source {DataSourceId} is healthy", dataSource.Id);
                }
                catch (Exception ex)
                {
                    unhealthyDataSources.Add(dataSource.Id);
                    _logger.LogError(ex, "Data source {DataSourceId} connectivity test failed", dataSource.Id);
                }
            }

            // Add diagnostic data
            data["totalDataSources"] = dataSources.Count;
            data["healthyDataSources"] = healthyDataSources.Count;
            data["unhealthyDataSources"] = unhealthyDataSources.Count;
            data["healthyDataSourceIds"] = healthyDataSources;

            if (unhealthyDataSources.Count > 0)
            {
                data["unhealthyDataSourceIds"] = unhealthyDataSources;
            }

            // Determine health status
            if (unhealthyDataSources.Count == 0)
            {
                return HealthCheckResult.Healthy(
                    $"All {healthyDataSources.Count} data source(s) are accessible",
                    data: data);
            }
            else if (healthyDataSources.Count > 0)
            {
                // Some data sources are healthy, some are not - degraded
                return HealthCheckResult.Degraded(
                    $"{unhealthyDataSources.Count} of {dataSources.Count} data source(s) are unavailable",
                    data: data);
            }
            else
            {
                // All data sources are unhealthy
                return HealthCheckResult.Unhealthy(
                    "All data sources are unavailable",
                    data: data);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy(
                "Database health check failed: " + ex.Message,
                exception: ex,
                data: data);
        }
    }
}
