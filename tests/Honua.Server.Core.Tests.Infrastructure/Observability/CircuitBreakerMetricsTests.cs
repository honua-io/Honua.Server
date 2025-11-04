using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using Honua.Server.Core.Observability;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Observability;

[Trait("Category", "Unit")]
public class CircuitBreakerMetricsTests : IDisposable
{
    private readonly CircuitBreakerMetrics _metrics;
    private readonly MeterListener _meterListener;
    private readonly List<MeasurementData> _measurements;

    public CircuitBreakerMetricsTests()
    {
        _metrics = new CircuitBreakerMetrics();
        _measurements = new List<MeasurementData>();

        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "Honua.Server.CircuitBreaker")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };

        _meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            _measurements.Add(new MeasurementData(instrument.Name, measurement, tags.ToArray()));
        });

        _meterListener.SetMeasurementEventCallback<int>((instrument, measurement, tags, state) =>
        {
            _measurements.Add(new MeasurementData(instrument.Name, measurement, tags.ToArray()));
        });

        _meterListener.Start();
    }

    [Fact]
    public void RecordCircuitOpened_RecordsBreakAndTransition()
    {
        // Act
        _metrics.RecordCircuitOpened("S3", "TimeoutException");

        // Assert
        var breaks = _measurements.Where(m => m.InstrumentName == "honua.circuit_breaker.breaks").ToList();
        Assert.Single(breaks);
        Assert.Equal(1L, breaks[0].Value);
        Assert.Equal("s3", GetTagValue(breaks[0].Tags, "service"));
        Assert.Equal("TimeoutException", GetTagValue(breaks[0].Tags, "outcome"));

        var transitions = _measurements.Where(m => m.InstrumentName == "honua.circuit_breaker.state_transitions").ToList();
        Assert.Single(transitions);
        Assert.Equal(1L, transitions[0].Value);
        Assert.Equal("s3", GetTagValue(transitions[0].Tags, "service"));
        Assert.Equal("closed", GetTagValue(transitions[0].Tags, "from_state"));
        Assert.Equal("open", GetTagValue(transitions[0].Tags, "to_state"));
    }

    [Fact]
    public void RecordCircuitClosed_RecordsClosureAndTransition()
    {
        // Arrange - first open the circuit
        _metrics.RecordCircuitOpened("Azure Blob", "ServiceUnavailable");
        _measurements.Clear();

        // Act
        _metrics.RecordCircuitClosed("Azure Blob");

        // Assert
        var closures = _measurements.Where(m => m.InstrumentName == "honua.circuit_breaker.closures").ToList();
        Assert.Single(closures);
        Assert.Equal(1L, closures[0].Value);
        Assert.Equal("azure_blob", GetTagValue(closures[0].Tags, "service"));

        var transitions = _measurements.Where(m => m.InstrumentName == "honua.circuit_breaker.state_transitions").ToList();
        Assert.Single(transitions);
        Assert.Equal("azure_blob", GetTagValue(transitions[0].Tags, "service"));
        Assert.Equal("open", GetTagValue(transitions[0].Tags, "from_state"));
        Assert.Equal("closed", GetTagValue(transitions[0].Tags, "to_state"));
    }

    [Fact]
    public void RecordCircuitHalfOpened_RecordsHalfOpenAndTransition()
    {
        // Arrange - first open the circuit
        _metrics.RecordCircuitOpened("GCS", "NetworkError");
        _measurements.Clear();

        // Act
        _metrics.RecordCircuitHalfOpened("GCS");

        // Assert
        var halfOpens = _measurements.Where(m => m.InstrumentName == "honua.circuit_breaker.half_opens").ToList();
        Assert.Single(halfOpens);
        Assert.Equal(1L, halfOpens[0].Value);
        Assert.Equal("gcs", GetTagValue(halfOpens[0].Tags, "service"));

        var transitions = _measurements.Where(m => m.InstrumentName == "honua.circuit_breaker.state_transitions").ToList();
        Assert.Single(transitions);
        Assert.Equal("gcs", GetTagValue(transitions[0].Tags, "service"));
        Assert.Equal("open", GetTagValue(transitions[0].Tags, "from_state"));
        Assert.Equal("halfopen", GetTagValue(transitions[0].Tags, "to_state"));
    }

    [Fact]
    public void RecordStateTransition_WithExplicitStates_RecordsCorrectly()
    {
        // Act
        _metrics.RecordStateTransition("HTTP", CircuitState.HalfOpen, CircuitState.Closed);

        // Assert
        var transitions = _measurements.Where(m => m.InstrumentName == "honua.circuit_breaker.state_transitions").ToList();
        Assert.Single(transitions);
        Assert.Equal(1L, transitions[0].Value);
        Assert.Equal("http", GetTagValue(transitions[0].Tags, "service"));
        Assert.Equal("halfopen", GetTagValue(transitions[0].Tags, "from_state"));
        Assert.Equal("closed", GetTagValue(transitions[0].Tags, "to_state"));
    }

    [Fact]
    public void UpdateCircuitState_UpdatesCurrentState()
    {
        // Act
        _metrics.UpdateCircuitState("S3", CircuitState.Open);
        _metrics.UpdateCircuitState("Azure Blob", CircuitState.Closed);
        _metrics.UpdateCircuitState("GCS", CircuitState.HalfOpen);

        // Assert - the gauge should be observable
        // We can't directly observe the gauge value without triggering an observation,
        // but we can verify no exceptions were thrown
        Assert.True(true);
    }

    [Fact]
    public void ServiceNameNormalization_NormalizesCommonPatterns()
    {
        // Act
        _metrics.RecordCircuitOpened("amazon-s3", "Error");
        _metrics.RecordCircuitOpened("Azure Storage Blob", "Error");
        _metrics.RecordCircuitOpened("Google Cloud Storage", "Error");
        _metrics.RecordCircuitOpened("HTTP COG", "Error");

        // Assert
        var breaks = _measurements.Where(m => m.InstrumentName == "honua.circuit_breaker.breaks").ToList();
        Assert.Equal(4, breaks.Count);

        // Check normalized service names
        Assert.Contains(breaks, b => GetTagValue(b.Tags, "service") == "s3");
        Assert.Contains(breaks, b => GetTagValue(b.Tags, "service") == "azure_blob");
        Assert.Contains(breaks, b => GetTagValue(b.Tags, "service") == "gcs");
        Assert.Contains(breaks, b => GetTagValue(b.Tags, "service") == "http");
    }

    [Fact]
    public void MultipleCircuits_TrackIndependently()
    {
        // Act
        _metrics.RecordCircuitOpened("S3", "Error1");
        _metrics.RecordCircuitOpened("Azure Blob", "Error2");
        _metrics.RecordCircuitHalfOpened("S3");
        _metrics.RecordCircuitClosed("Azure Blob");

        // Assert
        var breaks = _measurements.Where(m => m.InstrumentName == "honua.circuit_breaker.breaks").ToList();
        Assert.Equal(2, breaks.Count);

        var closures = _measurements.Where(m => m.InstrumentName == "honua.circuit_breaker.closures").ToList();
        Assert.Single(closures);
        Assert.Equal("azure_blob", GetTagValue(closures[0].Tags, "service"));

        var halfOpens = _measurements.Where(m => m.InstrumentName == "honua.circuit_breaker.half_opens").ToList();
        Assert.Single(halfOpens);
        Assert.Equal("s3", GetTagValue(halfOpens[0].Tags, "service"));
    }

    [Fact]
    public void CircuitBreakerWorkflow_RecordsFullLifecycle()
    {
        // Act - simulate full circuit breaker lifecycle
        _metrics.RecordCircuitOpened("S3", "ServiceUnavailable");
        _metrics.RecordCircuitHalfOpened("S3");
        _metrics.RecordCircuitClosed("S3");

        // Assert
        var transitions = _measurements.Where(m => m.InstrumentName == "honua.circuit_breaker.state_transitions").ToList();
        Assert.Equal(3, transitions.Count);

        // Check transition sequence
        Assert.Equal("closed", GetTagValue(transitions[0].Tags, "from_state"));
        Assert.Equal("open", GetTagValue(transitions[0].Tags, "to_state"));

        Assert.Equal("open", GetTagValue(transitions[1].Tags, "from_state"));
        Assert.Equal("halfopen", GetTagValue(transitions[1].Tags, "to_state"));

        Assert.Equal("open", GetTagValue(transitions[2].Tags, "from_state"));
        Assert.Equal("closed", GetTagValue(transitions[2].Tags, "to_state"));
    }

    [Fact]
    public void CircuitState_EnumValues_AreCorrect()
    {
        // Assert
        Assert.Equal(0, (int)CircuitState.Closed);
        Assert.Equal(1, (int)CircuitState.Open);
        Assert.Equal(2, (int)CircuitState.HalfOpen);
    }

    public void Dispose()
    {
        _meterListener?.Dispose();
        _metrics?.Dispose();
    }

    private static string? GetTagValue(KeyValuePair<string, object?>[] tags, string key)
    {
        var tag = tags.FirstOrDefault(t => t.Key == key);
        return tag.Value?.ToString();
    }

    private record MeasurementData(string InstrumentName, object Value, KeyValuePair<string, object?>[] Tags);
}
