// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading.Tasks;

namespace Honua.Server.Enterprise.ETL.Resilience;

/// <summary>
/// Circuit breaker service to prevent cascading failures
/// </summary>
public interface ICircuitBreakerService
{
    /// <summary>
    /// Record a successful execution
    /// </summary>
    Task RecordSuccessAsync(string nodeType);

    /// <summary>
    /// Record a failed execution
    /// </summary>
    Task RecordFailureAsync(string nodeType, Exception exception);

    /// <summary>
    /// Check if circuit is open for a node type
    /// </summary>
    Task<bool> IsOpenAsync(string nodeType);

    /// <summary>
    /// Get circuit state for a node type
    /// </summary>
    Task<CircuitState> GetStateAsync(string nodeType);

    /// <summary>
    /// Manually reset a circuit breaker
    /// </summary>
    Task ResetAsync(string nodeType);

    /// <summary>
    /// Get all circuit breaker states
    /// </summary>
    Task<CircuitBreakerStats> GetStatsAsync();
}

/// <summary>
/// Circuit breaker state
/// </summary>
public enum CircuitState
{
    /// <summary>
    /// Circuit is closed, requests flow normally
    /// </summary>
    Closed,

    /// <summary>
    /// Circuit is open, requests are blocked
    /// </summary>
    Open,

    /// <summary>
    /// Circuit is half-open, testing if service has recovered
    /// </summary>
    HalfOpen
}

/// <summary>
/// Circuit breaker statistics
/// </summary>
public class CircuitBreakerStats
{
    /// <summary>
    /// Circuit states by node type
    /// </summary>
    public Dictionary<string, CircuitBreakerNodeStats> NodeTypeStats { get; set; } = new();
}

/// <summary>
/// Circuit breaker stats for a specific node type
/// </summary>
public class CircuitBreakerNodeStats
{
    /// <summary>
    /// Node type
    /// </summary>
    public string NodeType { get; set; } = string.Empty;

    /// <summary>
    /// Current state
    /// </summary>
    public CircuitState State { get; set; }

    /// <summary>
    /// Consecutive failure count
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// Total failure count
    /// </summary>
    public long TotalFailures { get; set; }

    /// <summary>
    /// Total success count
    /// </summary>
    public long TotalSuccesses { get; set; }

    /// <summary>
    /// Last failure time
    /// </summary>
    public DateTimeOffset? LastFailureAt { get; set; }

    /// <summary>
    /// When circuit opened
    /// </summary>
    public DateTimeOffset? OpenedAt { get; set; }

    /// <summary>
    /// When circuit will transition to half-open
    /// </summary>
    public DateTimeOffset? HalfOpenAt { get; set; }

    /// <summary>
    /// Failure rate (0.0 to 1.0)
    /// </summary>
    public double FailureRate => TotalSuccesses + TotalFailures > 0
        ? (double)TotalFailures / (TotalSuccesses + TotalFailures)
        : 0.0;
}
