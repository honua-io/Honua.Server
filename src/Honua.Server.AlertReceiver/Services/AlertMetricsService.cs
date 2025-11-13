// <copyright file="AlertMetricsService.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

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
    private readonly Meter meter;
    private readonly Counter<long> alertsReceived;
    private readonly Counter<long> alertsSent;
    private readonly Counter<long> alertsSuppressed;
    private readonly Counter<long> alertErrors;
    private readonly Counter<long> persistenceFailures;
    private readonly Histogram<double> alertLatency;
    private readonly ObservableGauge<int> circuitBreakerStates;
    private readonly Counter<long> deduplicationCacheOperations;
    private readonly ObservableGauge<int> deduplicationCacheSize;
    private readonly Histogram<int> fingerprintLength;
    private readonly Counter<long> raceConditionsPrevented;

    // MEMORY LEAK FIX: Use bounded dictionary with LRU eviction to prevent unbounded memory growth
    // Max 1000 circuit breaker states (covers typical deployment scenarios with headroom)
    private readonly Dictionary<string, int> circuitStates = new();
    private readonly Queue<string> accessOrder = new();
    private readonly object stateLock = new();
    private const int MaxCircuitBreakerStates = 1000;

    private int currentCacheSize;
    private readonly object cacheSizeLock = new();

    public AlertMetricsService()
    {
        this.meter = new Meter("Honua.AlertReceiver", "1.0.0");

        this.alertsReceived = this.meter.CreateCounter<long>(
            "honua.alerts.received",
            unit: "{alert}",
            description: "Number of alerts received by source and severity");

        this.alertsSent = this.meter.CreateCounter<long>(
            "honua.alerts.sent",
            unit: "{alert}",
            description: "Number of alerts sent to providers");

        this.alertsSuppressed = this.meter.CreateCounter<long>(
            "honua.alerts.suppressed",
            unit: "{alert}",
            description: "Number of alerts suppressed by reason");

        this.alertErrors = this.meter.CreateCounter<long>(
            "honua.alerts.errors",
            unit: "{error}",
            description: "Number of alert delivery errors by provider");

        this.persistenceFailures = this.meter.CreateCounter<long>(
            "honua.alerts.persistence_failures",
            unit: "{failure}",
            description: "Number of alert persistence failures by operation");

        this.alertLatency = this.meter.CreateHistogram<double>(
            "honua.alerts.latency",
            unit: "ms",
            description: "Alert delivery latency by provider");

        this.circuitBreakerStates = this.meter.CreateObservableGauge<int>(
            "honua.alerts.circuit_breaker_state",
            () =>
            {
                lock (this.stateLock)
                {
                    return this.circuitStates.Select(kvp =>
                        new Measurement<int>(kvp.Value, new KeyValuePair<string, object?>("provider", kvp.Key)));
                }
            },
            unit: "{state}",
            description: "Circuit breaker state by provider (0=Closed, 1=Open, 2=HalfOpen)");

        this.deduplicationCacheOperations = this.meter.CreateCounter<long>(
            "honua.alerts.deduplication_cache_operations",
            unit: "{operation}",
            description: "Deduplication cache operations (add, get, remove, evict)");

        this.deduplicationCacheSize = this.meter.CreateObservableGauge<int>(
            "honua.alerts.deduplication_cache_size",
            () => new[] { new Measurement<int>(this.currentCacheSize) },
            unit: "{entry}",
            description: "Current number of entries in deduplication reservation cache");

        this.fingerprintLength = this.meter.CreateHistogram<int>(
            "honua.alerts.fingerprint_length",
            unit: "{character}",
            description: "Distribution of alert fingerprint lengths (helps identify truncation risks)");

        this.raceConditionsPrevented = this.meter.CreateCounter<long>(
            "honua.alerts.race_conditions_prevented",
            unit: "{race_condition}",
            description: "Number of race conditions prevented by atomic operations");
    }

    public void RecordAlertReceived(string source, string severity)
    {
        this.alertsReceived.Add(
            1,
            new("source", source),
            new("severity", severity));
    }

    public void RecordAlertSent(string provider, string severity, bool success)
    {
        if (success)
        {
            this.alertsSent.Add(
                1,
                new("provider", provider),
                new("severity", severity),
                new("success", "true"));
        }
        else
        {
            this.alertErrors.Add(
                1,
                new("provider", provider),
                new("severity", severity));
        }
    }

    public void RecordAlertSuppressed(string reason, string severity)
    {
        this.alertsSuppressed.Add(
            1,
            new("reason", reason),
            new("severity", severity));
    }

    public void RecordAlertLatency(string provider, TimeSpan duration)
    {
        this.alertLatency.Record(
            duration.TotalMilliseconds,
            new KeyValuePair<string, object?>("provider", provider));
    }

    public void RecordCircuitBreakerState(string provider, string state)
    {
        var stateValue = state.ToLowerInvariant() switch
        {
            "closed" => 0,
            "open" => 1,
            "halfopen" => 2,
            _ => -1,
        };

        // MEMORY LEAK FIX: Implement LRU eviction to prevent unbounded growth
        lock (this.stateLock)
        {
            // If provider already exists, remove it from access order to re-add at end
            if (this.circuitStates.ContainsKey(provider))
            {
                // Remove from middle of queue (O(n) but queue is small)
                var tempQueue = new Queue<string>(this.accessOrder.Count);
                foreach (var key in this.accessOrder)
                {
                    if (key != provider)
                    {
                        tempQueue.Enqueue(key);
                    }
                }
                this.accessOrder.Clear();
                while (tempQueue.Count > 0)
                {
                    this.accessOrder.Enqueue(tempQueue.Dequeue());
                }
            }

            // Add/update state
            this.circuitStates[provider] = stateValue;
            this.accessOrder.Enqueue(provider);

            // Evict oldest entry if we exceed max size
            while (this.circuitStates.Count > MaxCircuitBreakerStates && this.accessOrder.Count > 0)
            {
                var oldest = this.accessOrder.Dequeue();
                this.circuitStates.Remove(oldest);
            }
        }
    }

    public void RecordAlertPersistenceFailure(string operation)
    {
        this.persistenceFailures.Add(
            1,
            new KeyValuePair<string, object?>[] { new("operation", operation) });
    }

    public void RecordDeduplicationCacheOperation(string operation, bool hit)
    {
        this.deduplicationCacheOperations.Add(
            1,
            new("operation", operation),
            new("hit", hit.ToString().ToLowerInvariant()));
    }

    public void RecordDeduplicationCacheSize(int size)
    {
        lock (this.cacheSizeLock)
        {
            this.currentCacheSize = size;
        }
    }

    public void RecordFingerprintLength(int length)
    {
        this.fingerprintLength.Record(length);
    }

    public void RecordRaceConditionPrevented(string scenario)
    {
        this.raceConditionsPrevented.Add(
            1,
            new KeyValuePair<string, object?>("scenario", scenario));
    }

    public void Dispose()
    {
        this.meter.Dispose();
    }
}
