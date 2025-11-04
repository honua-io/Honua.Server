// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Diagnostics.Metrics;

namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Metrics for the alerting system itself (observability of alerting).
/// </summary>
public interface IAlertMetricsService
{
    void RecordAlertReceived(string source, string severity);
    void RecordAlertSent(string provider, string severity, bool success);
    void RecordAlertSuppressed(string reason, string severity);
    void RecordAlertLatency(string provider, TimeSpan duration);
    void RecordCircuitBreakerState(string provider, string state);
    void RecordAlertPersistenceFailure(string operation);
    void RecordDeduplicationCacheOperation(string operation, bool hit);
    void RecordDeduplicationCacheSize(int size);
    void RecordFingerprintLength(int length);
    void RecordRaceConditionPrevented(string scenario);
}

public sealed class AlertMetricsService : IAlertMetricsService, IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _alertsReceived;
    private readonly Counter<long> _alertsSent;
    private readonly Counter<long> _alertsSuppressed;
    private readonly Counter<long> _alertErrors;
    private readonly Counter<long> _persistenceFailures;
    private readonly Histogram<double> _alertLatency;
    private readonly ObservableGauge<int> _circuitBreakerStates;
    private readonly Counter<long> _deduplicationCacheOperations;
    private readonly ObservableGauge<int> _deduplicationCacheSize;
    private readonly Histogram<int> _fingerprintLength;
    private readonly Counter<long> _raceConditionsPrevented;

    // MEMORY LEAK FIX: Use bounded dictionary with LRU eviction to prevent unbounded memory growth
    // Max 1000 circuit breaker states (covers typical deployment scenarios with headroom)
    private readonly Dictionary<string, int> _circuitStates = new();
    private readonly Queue<string> _accessOrder = new();
    private readonly object _stateLock = new();
    private const int MaxCircuitBreakerStates = 1000;

    private int _currentCacheSize;
    private readonly object _cacheSizeLock = new();

    public AlertMetricsService()
    {
        _meter = new Meter("Honua.AlertReceiver", "1.0.0");

        _alertsReceived = _meter.CreateCounter<long>(
            "honua.alerts.received",
            unit: "{alert}",
            description: "Number of alerts received by source and severity");

        _alertsSent = _meter.CreateCounter<long>(
            "honua.alerts.sent",
            unit: "{alert}",
            description: "Number of alerts sent to providers");

        _alertsSuppressed = _meter.CreateCounter<long>(
            "honua.alerts.suppressed",
            unit: "{alert}",
            description: "Number of alerts suppressed by reason");

        _alertErrors = _meter.CreateCounter<long>(
            "honua.alerts.errors",
            unit: "{error}",
            description: "Number of alert delivery errors by provider");

        _persistenceFailures = _meter.CreateCounter<long>(
            "honua.alerts.persistence_failures",
            unit: "{failure}",
            description: "Number of alert persistence failures by operation");

        _alertLatency = _meter.CreateHistogram<double>(
            "honua.alerts.latency",
            unit: "ms",
            description: "Alert delivery latency by provider");

        _circuitBreakerStates = _meter.CreateObservableGauge<int>(
            "honua.alerts.circuit_breaker_state",
            () =>
            {
                lock (_stateLock)
                {
                    return _circuitStates.Select(kvp =>
                        new Measurement<int>(kvp.Value, new KeyValuePair<string, object?>("provider", kvp.Key)));
                }
            },
            unit: "{state}",
            description: "Circuit breaker state by provider (0=Closed, 1=Open, 2=HalfOpen)");

        _deduplicationCacheOperations = _meter.CreateCounter<long>(
            "honua.alerts.deduplication_cache_operations",
            unit: "{operation}",
            description: "Deduplication cache operations (add, get, remove, evict)");

        _deduplicationCacheSize = _meter.CreateObservableGauge<int>(
            "honua.alerts.deduplication_cache_size",
            () => new[] { new Measurement<int>(_currentCacheSize) },
            unit: "{entry}",
            description: "Current number of entries in deduplication reservation cache");

        _fingerprintLength = _meter.CreateHistogram<int>(
            "honua.alerts.fingerprint_length",
            unit: "{character}",
            description: "Distribution of alert fingerprint lengths (helps identify truncation risks)");

        _raceConditionsPrevented = _meter.CreateCounter<long>(
            "honua.alerts.race_conditions_prevented",
            unit: "{race_condition}",
            description: "Number of race conditions prevented by atomic operations");
    }

    public void RecordAlertReceived(string source, string severity)
    {
        _alertsReceived.Add(1,
            new("source", source),
            new("severity", severity));
    }

    public void RecordAlertSent(string provider, string severity, bool success)
    {
        if (success)
        {
            _alertsSent.Add(1,
                new("provider", provider),
                new("severity", severity),
                new("success", "true"));
        }
        else
        {
            _alertErrors.Add(1,
                new("provider", provider),
                new("severity", severity));
        }
    }

    public void RecordAlertSuppressed(string reason, string severity)
    {
        _alertsSuppressed.Add(1,
            new("reason", reason),
            new("severity", severity));
    }

    public void RecordAlertLatency(string provider, TimeSpan duration)
    {
        _alertLatency.Record(duration.TotalMilliseconds,
            new KeyValuePair<string, object?>("provider", provider));
    }

    public void RecordCircuitBreakerState(string provider, string state)
    {
        var stateValue = state.ToLowerInvariant() switch
        {
            "closed" => 0,
            "open" => 1,
            "halfopen" => 2,
            _ => -1
        };

        // MEMORY LEAK FIX: Implement LRU eviction to prevent unbounded growth
        lock (_stateLock)
        {
            // If provider already exists, remove it from access order to re-add at end
            if (_circuitStates.ContainsKey(provider))
            {
                // Remove from middle of queue (O(n) but queue is small)
                var tempQueue = new Queue<string>(_accessOrder.Count);
                foreach (var key in _accessOrder)
                {
                    if (key != provider)
                    {
                        tempQueue.Enqueue(key);
                    }
                }
                _accessOrder.Clear();
                while (tempQueue.Count > 0)
                {
                    _accessOrder.Enqueue(tempQueue.Dequeue());
                }
            }

            // Add/update state
            _circuitStates[provider] = stateValue;
            _accessOrder.Enqueue(provider);

            // Evict oldest entry if we exceed max size
            while (_circuitStates.Count > MaxCircuitBreakerStates && _accessOrder.Count > 0)
            {
                var oldest = _accessOrder.Dequeue();
                _circuitStates.Remove(oldest);
            }
        }
    }

    public void RecordAlertPersistenceFailure(string operation)
    {
        _persistenceFailures.Add(1, new KeyValuePair<string, object?>[] { new("operation", operation) });
    }

    public void RecordDeduplicationCacheOperation(string operation, bool hit)
    {
        _deduplicationCacheOperations.Add(1,
            new("operation", operation),
            new("hit", hit.ToString().ToLowerInvariant()));
    }

    public void RecordDeduplicationCacheSize(int size)
    {
        lock (_cacheSizeLock)
        {
            _currentCacheSize = size;
        }
    }

    public void RecordFingerprintLength(int length)
    {
        _fingerprintLength.Record(length);
    }

    public void RecordRaceConditionPrevented(string scenario)
    {
        _raceConditionsPrevented.Add(1, new KeyValuePair<string, object?>("scenario", scenario));
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
