// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.Utilities;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.Health;

/// <summary>
/// Health check that validates database connectivity by attempting simple queries.
/// Critical for production: Ensures databases are reachable before routing traffic.
/// </summary>
public sealed class DatabaseConnectivityHealthCheck : IHealthCheck
{
    private readonly IMetadataRegistry registry;
    private readonly IDataStoreProviderFactory providerFactory;
    private readonly ILogger<DatabaseConnectivityHealthCheck> logger;

    public DatabaseConnectivityHealthCheck(
        IMetadataRegistry registry,
        IDataStoreProviderFactory providerFactory,
        ILogger<DatabaseConnectivityHealthCheck> logger)
    {
        this.registry = Guard.NotNull(registry);
        this.providerFactory = Guard.NotNull(providerFactory);
        this.logger = Guard.NotNull(logger);
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Set aggressive timeout for health check
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);

            var snapshot = await this.registry.GetSnapshotAsync(linkedCts.Token).ConfigureAwait(false);

            var testedDataSources = new List<string>();
            var failedDataSources = new List<(string id, string error)>();

            foreach (var dataSource in snapshot.DataSources)
            {
                if (dataSource.ConnectionString.IsNullOrWhiteSpace())
                    continue;

                try
                {
                    var provider = this.providerFactory.Create(dataSource.Provider);
                    if (provider == null)
                    {
                        this.logger.LogWarning("No provider found for {Provider}", dataSource.Provider);
                        continue;
                    }

                    // Attempt simple connectivity test (e.g., SELECT 1)
                    // Most providers should have a lightweight connectivity check
                    await provider.TestConnectivityAsync(dataSource, linkedCts.Token).ConfigureAwait(false);

                    testedDataSources.Add(dataSource.Id);
                }
                catch (Exception ex)
                {
                    this.logger.LogExternalServiceFailure(ex, dataSource.Provider, "connectivity check", dataSource.Id);

                    failedDataSources.Add((dataSource.Id, ex.Message));
                }
            }

            var data = new Dictionary<string, object>
            {
                ["testedDataSources"] = testedDataSources.Count,
                ["failedDataSources"] = failedDataSources.Count
            };

            if (failedDataSources.Any())
            {
                data["failures"] = failedDataSources.Select(f => new { f.id, f.error }).ToArray();

                return HealthCheckResult.Unhealthy(
                    $"Database connectivity failed for {failedDataSources.Count} data source(s): " +
                    string.Join(", ", failedDataSources.Select(f => f.id)),
                    data: data);
            }

            return HealthCheckResult.Healthy(
                $"All {testedDataSources.Count} database(s) are reachable",
                data: data);
        }
        catch (OperationCanceledException)
        {
            this.logger.LogWarning("Database connectivity health check timed out");
            return HealthCheckResult.Degraded("Database connectivity check timed out");
        }
        catch (Exception ex)
        {
            this.logger.LogOperationFailure(ex, "Database connectivity health check");
            return HealthCheckResult.Unhealthy("Database connectivity check error", ex);
        }
    }
}
