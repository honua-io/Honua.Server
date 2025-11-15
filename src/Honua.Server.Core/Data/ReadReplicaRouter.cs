// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Data;

/// <summary>
/// Interface for routing data operations to appropriate data sources (primary or read replicas).
/// </summary>
public interface IDataSourceRouter
{
    /// <summary>
    /// Gets the appropriate data source provider for the operation.
    /// Routes to read replicas for read operations when enabled, falls back to primary otherwise.
    /// </summary>
    /// <param name="dataSource">The primary data source definition.</param>
    /// <param name="isReadOnly">Whether this is a read-only operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The data source provider to use for this operation.</returns>
    Task<DataSourceDefinition> RouteAsync(
        DataSourceDefinition dataSource,
        bool isReadOnly,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers read replicas for a primary data source.
    /// </summary>
    /// <param name="primaryDataSourceId">The primary data source identifier.</param>
    /// <param name="replicas">List of read replica data sources.</param>
    void RegisterReplicas(string primaryDataSourceId, IReadOnlyList<DataSourceDefinition> replicas);

    /// <summary>
    /// Reports a health status for a data source (used for circuit breaker pattern).
    /// </summary>
    /// <param name="dataSourceId">The data source identifier.</param>
    /// <param name="isHealthy">Whether the data source is healthy.</param>
    void ReportHealth(string dataSourceId, bool isHealthy);
}

/// <summary>
/// Implements read replica routing with round-robin selection, health checks, and fallback logic.
/// </summary>
public sealed class ReadReplicaRouter : IDataSourceRouter
{
    private readonly ReadReplicaOptions _options;
    private readonly ILogger<ReadReplicaRouter> _logger;
    private readonly ReadReplicaMetrics _metrics;

    // Maps primary data source ID to its read replicas
    private readonly ConcurrentDictionary<string, ReplicaGroup> _replicaGroups = new();

    // Tracks health status of each data source
    private readonly ConcurrentDictionary<string, DataSourceHealth> _healthStatus = new();

    public ReadReplicaRouter(
        IOptions<ReadReplicaOptions> options,
        ILogger<ReadReplicaRouter> logger,
        ReadReplicaMetrics metrics)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    public void RegisterReplicas(string primaryDataSourceId, IReadOnlyList<DataSourceDefinition> replicas)
    {
        if (string.IsNullOrWhiteSpace(primaryDataSourceId))
            throw new ArgumentException("Primary data source ID cannot be null or empty.", nameof(primaryDataSourceId));

        if (replicas == null || replicas.Count == 0)
            return;

        var replicaGroup = new ReplicaGroup(primaryDataSourceId, replicas);
        _replicaGroups[primaryDataSourceId] = replicaGroup;

        _logger.LogInformation(
            "Registered {ReplicaCount} read replicas for primary data source '{PrimaryId}': {ReplicaIds}",
            replicas.Count,
            primaryDataSourceId,
            string.Join(", ", replicas.Select(r => r.Id)));
    }

    public async Task<DataSourceDefinition> RouteAsync(
        DataSourceDefinition dataSource,
        bool isReadOnly,
        CancellationToken cancellationToken = default)
    {
        if (dataSource == null)
            throw new ArgumentNullException(nameof(dataSource));

        // Write operations always go to primary
        if (!isReadOnly)
        {
            _metrics.RecordRouting(dataSource.Id, "primary", "write_operation");
            return dataSource;
        }

        // If replica routing is disabled, use primary
        if (!_options.EnableReadReplicaRouting)
        {
            _metrics.RecordRouting(dataSource.Id, "primary", "routing_disabled");
            return dataSource;
        }

        // Check if we have replicas registered for this data source
        if (!_replicaGroups.TryGetValue(dataSource.Id, out var replicaGroup))
        {
            _metrics.RecordRouting(dataSource.Id, "primary", "no_replicas");
            return dataSource;
        }

        // Try to select a healthy replica
        var selectedReplica = await SelectHealthyReplicaAsync(replicaGroup, cancellationToken).ConfigureAwait(false);
        if (selectedReplica != null)
        {
            _metrics.RecordRouting(dataSource.Id, selectedReplica.Id, "replica_selected");

            if (_options.EnableDetailedLogging)
            {
                _logger.LogDebug(
                    "Routing read operation for '{PrimaryId}' to replica '{ReplicaId}'",
                    dataSource.Id,
                    selectedReplica.Id);
            }

            return selectedReplica;
        }

        // Fallback to primary if all replicas are unavailable
        if (_options.FallbackToPrimary)
        {
            _metrics.RecordRouting(dataSource.Id, "primary", "replica_fallback");
            _metrics.RecordFallback(dataSource.Id);

            _logger.LogWarning(
                "All read replicas unavailable for '{PrimaryId}', falling back to primary database",
                dataSource.Id);

            return dataSource;
        }

        // No fallback - throw exception
        _metrics.RecordRouting(dataSource.Id, "none", "all_replicas_unavailable");
        throw new InvalidOperationException(
            $"All read replicas for data source '{dataSource.Id}' are unavailable and fallback is disabled.");
    }

    public void ReportHealth(string dataSourceId, bool isHealthy)
    {
        if (string.IsNullOrWhiteSpace(dataSourceId))
            return;

        var health = _healthStatus.GetOrAdd(dataSourceId, _ => new DataSourceHealth());

        if (isHealthy)
        {
            health.RecordSuccess();
        }
        else
        {
            health.RecordFailure();

            if (health.IsUnhealthy(_options.MaxConsecutiveFailures))
            {
                _logger.LogWarning(
                    "Data source '{DataSourceId}' marked as unhealthy after {FailureCount} consecutive failures",
                    dataSourceId,
                    health.ConsecutiveFailures);

                _metrics.RecordUnhealthyReplica(dataSourceId);
            }
        }
    }

    private async Task<DataSourceDefinition?> SelectHealthyReplicaAsync(
        ReplicaGroup replicaGroup,
        CancellationToken cancellationToken)
    {
        var replicas = replicaGroup.Replicas;
        var startIndex = replicaGroup.GetNextIndex();

        // Try each replica in round-robin order
        for (int i = 0; i < replicas.Count; i++)
        {
            var index = (startIndex + i) % replicas.Count;
            var replica = replicas[index];

            // Check health status
            var health = _healthStatus.GetOrAdd(replica.Id, _ => new DataSourceHealth());

            if (health.IsUnhealthy(_options.MaxConsecutiveFailures))
            {
                // Check if enough time has passed to retry
                if (!health.ShouldRetry(TimeSpan.FromSeconds(_options.UnhealthyRetryIntervalSeconds)))
                {
                    if (_options.EnableDetailedLogging)
                    {
                        _logger.LogDebug(
                            "Skipping unhealthy replica '{ReplicaId}' (will retry after {RetryInterval}s)",
                            replica.Id,
                            _options.UnhealthyRetryIntervalSeconds);
                    }
                    continue;
                }

                _logger.LogInformation(
                    "Retrying previously unhealthy replica '{ReplicaId}'",
                    replica.Id);
            }

            // Check replication lag if configured
            if (await IsReplicationLagAcceptableAsync(replica, cancellationToken).ConfigureAwait(false))
            {
                // Update round-robin index for next call
                replicaGroup.IncrementIndex();
                return replica;
            }

            _logger.LogWarning(
                "Replica '{ReplicaId}' has excessive replication lag, skipping",
                replica.Id);
        }

        return null;
    }

    private async Task<bool> IsReplicationLagAcceptableAsync(
        DataSourceDefinition replica,
        CancellationToken cancellationToken)
    {
        // Determine max lag threshold (replica-specific or global)
        var maxLag = replica.MaxReplicationLagSeconds ?? _options.MaxReplicationLagSeconds;

        if (!maxLag.HasValue)
        {
            // No lag checking configured
            return true;
        }

        // TODO: Implement actual replication lag checking
        // This would query pg_stat_replication or similar provider-specific views
        // For now, assume lag is acceptable
        await Task.CompletedTask.ConfigureAwait(false);
        return true;
    }

    private sealed class ReplicaGroup
    {
        private int _currentIndex = 0;

        public string PrimaryId { get; }
        public IReadOnlyList<DataSourceDefinition> Replicas { get; }

        public ReplicaGroup(string primaryId, IReadOnlyList<DataSourceDefinition> replicas)
        {
            PrimaryId = primaryId;
            Replicas = replicas;
        }

        public int GetNextIndex() => Volatile.Read(ref _currentIndex);

        public void IncrementIndex()
        {
            var current = _currentIndex;
            var next = (current + 1) % Replicas.Count;
            Interlocked.CompareExchange(ref _currentIndex, next, current);
        }
    }

    private sealed class DataSourceHealth
    {
        private int _consecutiveFailures = 0;
        private DateTime? _lastFailureTime;
        private readonly object _lock = new();

        public int ConsecutiveFailures => Volatile.Read(ref _consecutiveFailures);

        public void RecordSuccess()
        {
            lock (_lock)
            {
                Volatile.Write(ref _consecutiveFailures, 0);
                _lastFailureTime = null;
            }
        }

        public void RecordFailure()
        {
            lock (_lock)
            {
                Interlocked.Increment(ref _consecutiveFailures);
                _lastFailureTime = DateTime.UtcNow;
            }
        }

        public bool IsUnhealthy(int threshold)
        {
            return Volatile.Read(ref _consecutiveFailures) >= threshold;
        }

        public bool ShouldRetry(TimeSpan retryInterval)
        {
            lock (_lock)
            {
                if (_lastFailureTime == null)
                    return true;

                return DateTime.UtcNow - _lastFailureTime.Value >= retryInterval;
            }
        }
    }
}

/// <summary>
/// Metrics for read replica routing operations.
/// </summary>
public sealed class ReadReplicaMetrics
{
    private readonly ILogger<ReadReplicaMetrics> _logger;

    public ReadReplicaMetrics(ILogger<ReadReplicaMetrics> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void RecordRouting(string primaryId, string targetId, string reason)
    {
        using var activity = HonuaTelemetry.Database.StartActivity("ReadReplica.Route");
        activity?.SetTag("primary.id", primaryId);
        activity?.SetTag("target.id", targetId);
        activity?.SetTag("routing.reason", reason);

        // TODO: Add actual metrics collection (counters, histograms)
        // This would integrate with OpenTelemetry metrics or custom metric provider
    }

    public void RecordFallback(string primaryId)
    {
        _logger.LogWarning(
            "Read replica fallback occurred for primary '{PrimaryId}'",
            primaryId);

        using var activity = HonuaTelemetry.Database.StartActivity("ReadReplica.Fallback");
        activity?.SetTag("primary.id", primaryId);
    }

    public void RecordUnhealthyReplica(string replicaId)
    {
        _logger.LogWarning(
            "Replica '{ReplicaId}' marked as unhealthy",
            replicaId);

        using var activity = HonuaTelemetry.Database.StartActivity("ReadReplica.Unhealthy");
        activity?.SetTag("replica.id", replicaId);
    }
}
