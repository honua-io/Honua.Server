// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Enterprise.ETL.Resilience;

/// <summary>
/// In-memory implementation of circuit breaker service
/// </summary>
public class InMemoryCircuitBreakerService : ICircuitBreakerService
{
    private readonly ConcurrentDictionary<string, CircuitBreakerNodeStats> _circuits = new();
    private readonly ILogger<InMemoryCircuitBreakerService> _logger;
    private readonly CircuitBreakerOptions _options;

    public InMemoryCircuitBreakerService(
        ILogger<InMemoryCircuitBreakerService> logger,
        IOptions<CircuitBreakerOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public Task RecordSuccessAsync(string nodeType)
    {
        var stats = _circuits.GetOrAdd(nodeType, _ => new CircuitBreakerNodeStats { NodeType = nodeType });

        lock (stats)
        {
            stats.TotalSuccesses++;
            stats.ConsecutiveFailures = 0;

            // Transition from half-open to closed on success
            if (stats.State == CircuitState.HalfOpen)
            {
                stats.State = CircuitState.Closed;
                stats.OpenedAt = null;
                stats.HalfOpenAt = null;
                _logger.LogInformation("Circuit breaker for {NodeType} transitioned to Closed after successful test", nodeType);
            }
        }

        return Task.CompletedTask;
    }

    public Task RecordFailureAsync(string nodeType, Exception exception)
    {
        var stats = _circuits.GetOrAdd(nodeType, _ => new CircuitBreakerNodeStats { NodeType = nodeType });

        lock (stats)
        {
            stats.TotalFailures++;
            stats.ConsecutiveFailures++;
            stats.LastFailureAt = DateTimeOffset.UtcNow;

            // Open circuit if threshold exceeded
            if (stats.State == CircuitState.Closed &&
                stats.ConsecutiveFailures >= _options.FailureThreshold)
            {
                stats.State = CircuitState.Open;
                stats.OpenedAt = DateTimeOffset.UtcNow;
                stats.HalfOpenAt = DateTimeOffset.UtcNow.AddSeconds(_options.TimeoutSeconds);

                _logger.LogWarning(
                    "Circuit breaker for {NodeType} opened after {Failures} consecutive failures. Exception: {Exception}",
                    nodeType,
                    stats.ConsecutiveFailures,
                    exception.Message);
            }
            // Failed during half-open, go back to open
            else if (stats.State == CircuitState.HalfOpen)
            {
                stats.State = CircuitState.Open;
                stats.OpenedAt = DateTimeOffset.UtcNow;
                stats.HalfOpenAt = DateTimeOffset.UtcNow.AddSeconds(_options.TimeoutSeconds);

                _logger.LogWarning(
                    "Circuit breaker for {NodeType} returned to Open state after failure during half-open test",
                    nodeType);
            }
        }

        return Task.CompletedTask;
    }

    public Task<bool> IsOpenAsync(string nodeType)
    {
        if (!_circuits.TryGetValue(nodeType, out var stats))
            return Task.FromResult(false);

        lock (stats)
        {
            // Check if we should transition to half-open
            if (stats.State == CircuitState.Open &&
                stats.HalfOpenAt.HasValue &&
                DateTimeOffset.UtcNow >= stats.HalfOpenAt.Value)
            {
                stats.State = CircuitState.HalfOpen;
                _logger.LogInformation(
                    "Circuit breaker for {NodeType} transitioned to HalfOpen for testing",
                    nodeType);
            }

            return Task.FromResult(stats.State == CircuitState.Open);
        }
    }

    public Task<CircuitState> GetStateAsync(string nodeType)
    {
        if (!_circuits.TryGetValue(nodeType, out var stats))
            return Task.FromResult(CircuitState.Closed);

        lock (stats)
        {
            // Check if we should transition to half-open
            if (stats.State == CircuitState.Open &&
                stats.HalfOpenAt.HasValue &&
                DateTimeOffset.UtcNow >= stats.HalfOpenAt.Value)
            {
                stats.State = CircuitState.HalfOpen;
            }

            return Task.FromResult(stats.State);
        }
    }

    public Task ResetAsync(string nodeType)
    {
        if (_circuits.TryGetValue(nodeType, out var stats))
        {
            lock (stats)
            {
                stats.State = CircuitState.Closed;
                stats.ConsecutiveFailures = 0;
                stats.OpenedAt = null;
                stats.HalfOpenAt = null;

                _logger.LogInformation("Circuit breaker for {NodeType} manually reset", nodeType);
            }
        }

        return Task.CompletedTask;
    }

    public Task<CircuitBreakerStats> GetStatsAsync()
    {
        var stats = new CircuitBreakerStats();

        foreach (var kvp in _circuits)
        {
            var nodeStats = kvp.Value;
            lock (nodeStats)
            {
                stats.NodeTypeStats[kvp.Key] = new CircuitBreakerNodeStats
                {
                    NodeType = nodeStats.NodeType,
                    State = nodeStats.State,
                    ConsecutiveFailures = nodeStats.ConsecutiveFailures,
                    TotalFailures = nodeStats.TotalFailures,
                    TotalSuccesses = nodeStats.TotalSuccesses,
                    LastFailureAt = nodeStats.LastFailureAt,
                    OpenedAt = nodeStats.OpenedAt,
                    HalfOpenAt = nodeStats.HalfOpenAt
                };
            }
        }

        return Task.FromResult(stats);
    }
}

/// <summary>
/// Circuit breaker configuration options
/// </summary>
public class CircuitBreakerOptions
{
    /// <summary>
    /// Number of consecutive failures before opening circuit
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Timeout in seconds before transitioning to half-open
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;
}
