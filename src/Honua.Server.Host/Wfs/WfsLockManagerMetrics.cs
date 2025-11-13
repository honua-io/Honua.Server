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
    private readonly Meter meter;
    private readonly Counter<long> locksAcquired;
    private readonly Counter<long> lockAcquisitionsFailed;
    private readonly Counter<long> lockValidations;
    private readonly Counter<long> locksReleased;
    private readonly Histogram<double> lockDurationSeconds;
    private readonly Counter<long> circuitOpened;
    private readonly Counter<long> circuitClosed;
    private readonly Counter<long> circuitHalfOpened;
    private readonly Histogram<double> operationLatencyMs;

    public WfsLockManagerMetrics()
    {
        this.meter = new Meter("Honua.Server.WfsLockManager");

        this.locksAcquired = this.meter.CreateCounter<long>(
            "honua.wfs.locks_acquired",
            description: "Number of WFS locks successfully acquired.");

        this.lockAcquisitionsFailed = this.meter.CreateCounter<long>(
            "honua.wfs.lock_acquisitions_failed",
            description: "Number of failed WFS lock acquisitions.");

        this.lockValidations = this.meter.CreateCounter<long>(
            "honua.wfs.lock_validations",
            description: "Number of WFS lock validations.");

        this.locksReleased = this.meter.CreateCounter<long>(
            "honua.wfs.locks_released",
            description: "Number of WFS locks released.");

        this.lockDurationSeconds = this.meter.CreateHistogram<double>(
            "honua.wfs.lock_duration_seconds",
            unit: "s",
            description: "Duration of WFS locks.");

        this.circuitOpened = this.meter.CreateCounter<long>(
            "honua.wfs.circuit_opened",
            description: "Number of times the circuit breaker opened.");

        this.circuitClosed = this.meter.CreateCounter<long>(
            "honua.wfs.circuit_closed",
            description: "Number of times the circuit breaker closed.");

        this.circuitHalfOpened = this.meter.CreateCounter<long>(
            "honua.wfs.circuit_half_opened",
            description: "Number of times the circuit breaker entered half-open state.");

        this.operationLatencyMs = this.meter.CreateHistogram<double>(
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
        this.locksAcquired.Add(1, tags);

        var durationTags = new KeyValuePair<string, object?>[]
        {
            new("service", NormalizeServiceId(serviceId))
        };
        this.lockDurationSeconds.Record(duration.TotalSeconds, durationTags);
    }

    public void RecordLockAcquisitionFailed(string serviceId, string reason)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("service", NormalizeServiceId(serviceId)),
            new("reason", reason)
        };
        this.lockAcquisitionsFailed.Add(1, tags);
    }

    public void RecordLockValidated(string serviceId, bool success)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("service", NormalizeServiceId(serviceId)),
            new("success", success)
        };
        this.lockValidations.Add(1, tags);
    }

    public void RecordLockReleased(string serviceId, int targetCount)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("service", NormalizeServiceId(serviceId)),
            new("target_count", targetCount)
        };
        this.locksReleased.Add(1, tags);
    }

    public void RecordCircuitOpened(string reason)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("reason", reason)
        };
        this.circuitOpened.Add(1, tags);
    }

    public void RecordCircuitClosed()
    {
        this.circuitClosed.Add(1);
    }

    public void RecordCircuitHalfOpened()
    {
        this.circuitHalfOpened.Add(1);
    }

    public void RecordOperationLatency(string operation, TimeSpan duration)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("operation", operation)
        };
        this.operationLatencyMs.Record(duration.TotalMilliseconds, tags);
    }

    public void Dispose()
    {
        this.meter.Dispose();
    }

    private static string NormalizeServiceId(string serviceId)
        => serviceId.IsNullOrWhiteSpace() ? "(unknown)" : serviceId;
}
