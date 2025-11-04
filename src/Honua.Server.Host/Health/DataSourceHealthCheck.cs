// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Honua.Server.Host.Utilities;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.Health;

public sealed class DataSourceHealthCheck : IHealthCheck
{
    private readonly IMetadataRegistry _registry;
    private readonly IReadOnlyDictionary<string, IDataSourceHealthContributor> _contributors;
    private readonly ILogger<DataSourceHealthCheck> _logger;

    public DataSourceHealthCheck(
        IMetadataRegistry registry,
        IEnumerable<IDataSourceHealthContributor> contributors,
        ILogger<DataSourceHealthCheck> logger)
    {
        _registry = Guard.NotNull(registry);
        _logger = Guard.NotNull(logger);
        _contributors = Guard.NotNull(contributors)
            .ToDictionary(c => c.Provider, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await _registry.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = await _registry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        if (snapshot.DataSources.Count == 0)
        {
            return HealthCheckResult.Healthy("No data sources configured.");
        }

        var details = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var overall = HealthStatus.Healthy;

        foreach (var dataSource in snapshot.DataSources)
        {
            var providerId = dataSource.Provider ?? string.Empty;
            if (!_contributors.TryGetValue(providerId, out var contributor))
            {
                details[dataSource.Id] = new Dictionary<string, object?>
                {
                    ["status"] = HealthStatus.Degraded.ToString(),
                    ["provider"] = dataSource.Provider,
                    ["description"] = $"No health contributor registered for provider '{dataSource.Provider}'."
                };

                overall = Combine(overall, HealthStatus.Degraded);
                continue;
            }

            HealthCheckResult result;
            try
            {
                result = await contributor.CheckAsync(dataSource, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Data source health check failed for {DataSourceId}.", dataSource.Id);
                result = HealthCheckResult.Unhealthy($"Data source '{dataSource.Id}' health check failed.", ex);
            }

            var entry = new Dictionary<string, object?>
            {
                ["status"] = result.Status.ToString(),
                ["provider"] = dataSource.Provider
            };

            if (result.Description.HasValue())
            {
                entry["description"] = result.Description;
            }

            foreach (var pair in result.Data)
            {
                entry[pair.Key] = pair.Value;
            }

            details[dataSource.Id] = entry;
            overall = Combine(overall, result.Status);
        }

        var description = overall switch
        {
            HealthStatus.Healthy => "All data sources are reachable.",
            HealthStatus.Degraded => "One or more data sources reported degraded status.",
            _ => "One or more data sources are unavailable."
        };

        return new HealthCheckResult(overall, description, data: details);
    }

    private static HealthStatus Combine(HealthStatus current, HealthStatus next)
    {
        if (next == HealthStatus.Unhealthy)
        {
            return HealthStatus.Unhealthy;
        }

        if (next == HealthStatus.Degraded && current == HealthStatus.Healthy)
        {
            return HealthStatus.Degraded;
        }

        return current;
    }
}
