// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Data;

/// <summary>
/// Health check for read replica data sources.
/// Monitors replica availability and reports health to the router.
/// </summary>
public sealed class ReadReplicaHealthCheck : IHealthCheck
{
    private readonly IDataSourceRouter _router;
    private readonly IDataStoreProviderFactory _providerFactory;
    private readonly IMetadataRegistry _metadataRegistry;
    private readonly ReadReplicaOptions _options;
    private readonly ILogger<ReadReplicaHealthCheck> _logger;

    public ReadReplicaHealthCheck(
        IDataSourceRouter router,
        IDataStoreProviderFactory providerFactory,
        IMetadataRegistry metadataRegistry,
        IOptions<ReadReplicaOptions> options,
        ILogger<ReadReplicaHealthCheck> logger)
    {
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        _metadataRegistry = metadataRegistry ?? throw new ArgumentNullException(nameof(metadataRegistry));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableReadReplicaRouting)
        {
            return HealthCheckResult.Healthy("Read replica routing is disabled");
        }

        try
        {
            var snapshot = await _metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
            var readReplicas = snapshot.DataSources
                .Where(ds => ds.ReadOnly)
                .ToList();

            if (readReplicas.Count == 0)
            {
                return HealthCheckResult.Healthy("No read replicas configured");
            }

            var healthResults = new Dictionary<string, string>();
            var healthyCount = 0;
            var unhealthyCount = 0;

            foreach (var replica in readReplicas)
            {
                var isHealthy = await CheckReplicaHealthAsync(replica, cancellationToken).ConfigureAwait(false);

                _router.ReportHealth(replica.Id, isHealthy);

                if (isHealthy)
                {
                    healthyCount++;
                    healthResults[replica.Id] = "healthy";
                }
                else
                {
                    unhealthyCount++;
                    healthResults[replica.Id] = "unhealthy";
                }
            }

            var data = new Dictionary<string, object>
            {
                ["total_replicas"] = readReplicas.Count,
                ["healthy_replicas"] = healthyCount,
                ["unhealthy_replicas"] = unhealthyCount,
                ["replica_status"] = healthResults
            };

            if (unhealthyCount == readReplicas.Count)
            {
                return HealthCheckResult.Unhealthy(
                    $"All {readReplicas.Count} read replicas are unhealthy",
                    data: data);
            }

            if (unhealthyCount > 0)
            {
                return HealthCheckResult.Degraded(
                    $"{unhealthyCount} of {readReplicas.Count} read replicas are unhealthy",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                $"All {readReplicas.Count} read replicas are healthy",
                data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing read replica health check");
            return HealthCheckResult.Unhealthy(
                "Error performing read replica health check",
                exception: ex);
        }
    }

    private async Task<bool> CheckReplicaHealthAsync(
        DataSourceDefinition replica,
        CancellationToken cancellationToken)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var provider = _providerFactory.Create(replica.Provider);

            await provider.TestConnectivityAsync(replica, cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();

            if (_options.EnableDetailedLogging)
            {
                _logger.LogDebug(
                    "Replica '{ReplicaId}' health check passed in {ElapsedMs}ms",
                    replica.Id,
                    stopwatch.ElapsedMilliseconds);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Replica '{ReplicaId}' health check failed: {ErrorMessage}",
                replica.Id,
                ex.Message);

            return false;
        }
    }
}
