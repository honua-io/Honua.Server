// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics.Metrics;

namespace Honua.Server.Core.Observability;

/// <summary>
/// Circuit breaker state values for metric reporting.
/// </summary>
public enum CircuitState
{
    /// <summary>
    /// Circuit is closed - requests are flowing normally.
    /// </summary>
    Closed = 0,

    /// <summary>
    /// Circuit is open - all requests are rejected immediately.
    /// </summary>
    Open = 1,

    /// <summary>
    /// Circuit is half-open - testing if service has recovered.
    /// </summary>
    HalfOpen = 2
}

/// <summary>
/// OpenTelemetry metrics for circuit breaker state transitions and health.
/// Tracks circuit breaker state for external services (S3, Azure Blob, GCS, HTTP).
/// Also tracks hedging strategy metrics for latency-sensitive operations.
/// </summary>
public interface ICircuitBreakerMetrics
{
    /// <summary>
    /// Records a circuit breaker state transition.
    /// </summary>
    void RecordStateTransition(string serviceName, CircuitState fromState, CircuitState toState);

    /// <summary>
    /// Updates the current circuit state gauge.
    /// </summary>
    void UpdateCircuitState(string serviceName, CircuitState state);

    /// <summary>
    /// Records circuit breaker metrics on opened event.
    /// </summary>
    void RecordCircuitOpened(string serviceName, string? outcome);

    /// <summary>
    /// Records circuit breaker metrics on closed event.
    /// </summary>
    void RecordCircuitClosed(string serviceName);

    /// <summary>
    /// Records circuit breaker metrics on half-open event.
    /// </summary>
    void RecordCircuitHalfOpened(string serviceName);

    /// <summary>
    /// Records a hedging attempt (parallel request sent to reduce latency).
    /// </summary>
    void RecordHedgingAttempt(int attemptNumber, string statusCode, string exceptionType);

    /// <summary>
    /// Records a hedging timeout (all attempts failed or exceeded timeout).
    /// </summary>
    void RecordHedgingTimeout(double timeoutSeconds);
}

/// <summary>
/// Implementation of circuit breaker and hedging metrics using OpenTelemetry.
/// </summary>
public sealed class CircuitBreakerMetrics : ICircuitBreakerMetrics, IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _stateTransitions;
    private readonly Counter<long> _circuitBreaks;
    private readonly Counter<long> _circuitClosures;
    private readonly Counter<long> _circuitHalfOpens;
    private readonly ObservableGauge<int> _circuitState;

    // Hedging metrics
    private readonly Counter<long> _hedgingAttempts;
    private readonly Counter<long> _hedgingTimeouts;
    private readonly Histogram<double> _hedgingAttemptNumbers;

    // Track current state for each service for the observable gauge
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, CircuitState> _currentStates = new();

    public CircuitBreakerMetrics()
    {
        _meter = new Meter("Honua.Server.CircuitBreaker", "1.0.0");

        // Circuit breaker metrics
        _stateTransitions = _meter.CreateCounter<long>(
            "honua.circuit_breaker.state_transitions",
            unit: "{transition}",
            description: "Number of circuit breaker state transitions");

        _circuitBreaks = _meter.CreateCounter<long>(
            "honua.circuit_breaker.breaks",
            unit: "{break}",
            description: "Number of times circuit breaker opened");

        _circuitClosures = _meter.CreateCounter<long>(
            "honua.circuit_breaker.closures",
            unit: "{closure}",
            description: "Number of times circuit breaker closed");

        _circuitHalfOpens = _meter.CreateCounter<long>(
            "honua.circuit_breaker.half_opens",
            unit: "{half_open}",
            description: "Number of times circuit breaker entered half-open state");

        _circuitState = _meter.CreateObservableGauge<int>(
            "honua.circuit_breaker.state",
            () => GetCurrentStates(),
            unit: "{state}",
            description: "Current circuit breaker state (0=Closed, 1=Open, 2=HalfOpen)");

        // Hedging metrics
        _hedgingAttempts = _meter.CreateCounter<long>(
            "honua.hedging.attempts",
            unit: "{attempt}",
            description: "Number of hedging attempts (parallel requests sent to reduce latency)");

        _hedgingTimeouts = _meter.CreateCounter<long>(
            "honua.hedging.timeouts",
            unit: "{timeout}",
            description: "Number of hedging operations that timed out (all attempts failed or exceeded timeout)");

        _hedgingAttemptNumbers = _meter.CreateHistogram<double>(
            "honua.hedging.attempt_number",
            unit: "{attempt}",
            description: "Distribution of hedging attempt numbers (1=primary, 2=first hedge, etc.)");
    }

    public void RecordStateTransition(string serviceName, CircuitState fromState, CircuitState toState)
    {
        _stateTransitions.Add(1,
            new KeyValuePair<string, object?>[]
            {
                new("service", NormalizeServiceName(serviceName)),
                new("from_state", fromState.ToString().ToLowerInvariant()),
                new("to_state", toState.ToString().ToLowerInvariant())
            });
    }

    public void UpdateCircuitState(string serviceName, CircuitState state)
    {
        _currentStates[NormalizeServiceName(serviceName)] = state;
    }

    public void RecordCircuitOpened(string serviceName, string? outcome)
    {
        var normalizedService = NormalizeServiceName(serviceName);

        _circuitBreaks.Add(1,
            new KeyValuePair<string, object?>[]
            {
                new("service", normalizedService),
                new("outcome", outcome ?? "unknown")
            });

        RecordStateTransition(normalizedService, CircuitState.Closed, CircuitState.Open);
        UpdateCircuitState(normalizedService, CircuitState.Open);
    }

    public void RecordCircuitClosed(string serviceName)
    {
        var normalizedService = NormalizeServiceName(serviceName);

        _circuitClosures.Add(1,
            new KeyValuePair<string, object?>[] { new("service", normalizedService) });

        RecordStateTransition(normalizedService, CircuitState.Open, CircuitState.Closed);
        UpdateCircuitState(normalizedService, CircuitState.Closed);
    }

    public void RecordCircuitHalfOpened(string serviceName)
    {
        var normalizedService = NormalizeServiceName(serviceName);

        _circuitHalfOpens.Add(1,
            new KeyValuePair<string, object?>[] { new("service", normalizedService) });

        RecordStateTransition(normalizedService, CircuitState.Open, CircuitState.HalfOpen);
        UpdateCircuitState(normalizedService, CircuitState.HalfOpen);
    }

    public void RecordHedgingAttempt(int attemptNumber, string statusCode, string exceptionType)
    {
        _hedgingAttempts.Add(1,
            new KeyValuePair<string, object?>[]
            {
                new("attempt_number", attemptNumber),
                new("status_code", statusCode ?? "N/A"),
                new("exception_type", exceptionType ?? "None")
            });

        // Record attempt number distribution
        _hedgingAttemptNumbers.Record(attemptNumber,
            new KeyValuePair<string, object?>[]
            {
                new("status_code", statusCode ?? "N/A"),
                new("exception_type", exceptionType ?? "None")
            });
    }

    public void RecordHedgingTimeout(double timeoutSeconds)
    {
        _hedgingTimeouts.Add(1,
            new KeyValuePair<string, object?>[]
            {
                new("timeout_seconds", timeoutSeconds)
            });
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    private System.Collections.Generic.IEnumerable<Measurement<int>> GetCurrentStates()
    {
        foreach (var kvp in _currentStates)
        {
            yield return new Measurement<int>(
                (int)kvp.Value,
                new KeyValuePair<string, object?>[] { new("service", kvp.Key) });
        }
    }

    private static string NormalizeServiceName(string? serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            return "unknown";

        return serviceName.ToLowerInvariant() switch
        {
            var s when s.Contains("s3") || s.Contains("amazon") || s.Contains("aws") => "s3",
            var s when s.Contains("azure") || s.Contains("blob") => "azure_blob",
            var s when s.Contains("gcs") || s.Contains("google") || s.Contains("gcp") => "gcs",
            var s when s.Contains("http") || s.Contains("web") || s.Contains("cog") => "http",
            _ => serviceName.ToLowerInvariant().Replace(" ", "_")
        };
    }
}
