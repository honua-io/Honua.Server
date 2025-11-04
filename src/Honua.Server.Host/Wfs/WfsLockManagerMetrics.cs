// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics.Metrics;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.Wfs;

/// <summary>
/// Interface for WFS lock manager metrics.
/// </summary>
internal interface IWfsLockManagerMetrics
{
    void RecordLockAcquired(string serviceId, int targetCount, TimeSpan duration);
    void RecordLockAcquisitionFailed(string serviceId, string reason);
    void RecordLockValidated(string serviceId, bool success);
    void RecordLockReleased(string serviceId, int targetCount);
    void RecordCircuitOpened(string reason);
    void RecordCircuitClosed();
    void RecordCircuitHalfOpened();
    void RecordOperationLatency(string operation, TimeSpan duration);
}

/// <summary>
/// OpenTelemetry metrics for WFS lock manager operations.
/// </summary>
internal sealed class WfsLockManagerMetrics : IWfsLockManagerMetrics, IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _locksAcquired;
    private readonly Counter<long> _lockAcquisitionsFailed;
    private readonly Counter<long> _lockValidations;
    private readonly Counter<long> _locksReleased;
    private readonly Histogram<double> _lockDurationSeconds;
    private readonly Counter<long> _circuitOpened;
    private readonly Counter<long> _circuitClosed;
    private readonly Counter<long> _circuitHalfOpened;
    private readonly Histogram<double> _operationLatencyMs;

    public WfsLockManagerMetrics()
    {
        _meter = new Meter("Honua.Server.WfsLockManager");

        _locksAcquired = _meter.CreateCounter<long>(
            "honua.wfs.locks_acquired",
            description: "Number of WFS locks successfully acquired.");

        _lockAcquisitionsFailed = _meter.CreateCounter<long>(
            "honua.wfs.lock_acquisitions_failed",
            description: "Number of failed WFS lock acquisitions.");

        _lockValidations = _meter.CreateCounter<long>(
            "honua.wfs.lock_validations",
            description: "Number of WFS lock validations.");

        _locksReleased = _meter.CreateCounter<long>(
            "honua.wfs.locks_released",
            description: "Number of WFS locks released.");

        _lockDurationSeconds = _meter.CreateHistogram<double>(
            "honua.wfs.lock_duration_seconds",
            unit: "s",
            description: "Duration of WFS locks.");

        _circuitOpened = _meter.CreateCounter<long>(
            "honua.wfs.circuit_opened",
            description: "Number of times the circuit breaker opened.");

        _circuitClosed = _meter.CreateCounter<long>(
            "honua.wfs.circuit_closed",
            description: "Number of times the circuit breaker closed.");

        _circuitHalfOpened = _meter.CreateCounter<long>(
            "honua.wfs.circuit_half_opened",
            description: "Number of times the circuit breaker entered half-open state.");

        _operationLatencyMs = _meter.CreateHistogram<double>(
            "honua.wfs.operation_latency_ms",
            unit: "ms",
            description: "Latency of WFS lock manager operations.");
    }

    public void RecordLockAcquired(string serviceId, int targetCount, TimeSpan duration)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("service", NormalizeServiceId(serviceId)),
            new("target_count", targetCount)
        };
        _locksAcquired.Add(1, tags);

        var durationTags = new KeyValuePair<string, object?>[]
        {
            new("service", NormalizeServiceId(serviceId))
        };
        _lockDurationSeconds.Record(duration.TotalSeconds, durationTags);
    }

    public void RecordLockAcquisitionFailed(string serviceId, string reason)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("service", NormalizeServiceId(serviceId)),
            new("reason", reason)
        };
        _lockAcquisitionsFailed.Add(1, tags);
    }

    public void RecordLockValidated(string serviceId, bool success)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("service", NormalizeServiceId(serviceId)),
            new("success", success)
        };
        _lockValidations.Add(1, tags);
    }

    public void RecordLockReleased(string serviceId, int targetCount)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("service", NormalizeServiceId(serviceId)),
            new("target_count", targetCount)
        };
        _locksReleased.Add(1, tags);
    }

    public void RecordCircuitOpened(string reason)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("reason", reason)
        };
        _circuitOpened.Add(1, tags);
    }

    public void RecordCircuitClosed()
    {
        _circuitClosed.Add(1);
    }

    public void RecordCircuitHalfOpened()
    {
        _circuitHalfOpened.Add(1);
    }

    public void RecordOperationLatency(string operation, TimeSpan duration)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("operation", operation)
        };
        _operationLatencyMs.Record(duration.TotalMilliseconds, tags);
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    private static string NormalizeServiceId(string serviceId)
        => serviceId.IsNullOrWhiteSpace() ? "(unknown)" : serviceId;
}
