// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Observability;

/// <summary>
/// Service for measuring and recording Service Level Indicators (SLIs).
/// </summary>
/// <remarks>
/// This service integrates with existing metrics infrastructure to track SLI measurements
/// and emit OpenTelemetry metrics for SLO compliance monitoring.
/// </remarks>
public interface ISliMetrics
{
    /// <summary>
    /// Records a latency measurement for SLI tracking.
    /// </summary>
    void RecordLatency(TimeSpan duration, string? endpoint = null, string? method = null);

    /// <summary>
    /// Records an availability measurement for SLI tracking.
    /// </summary>
    void RecordAvailability(int statusCode, string? endpoint = null, string? method = null);

    /// <summary>
    /// Records an error rate measurement for SLI tracking.
    /// </summary>
    void RecordError(int statusCode, string? endpoint = null, string? method = null);

    /// <summary>
    /// Records a health check result for SLI tracking.
    /// </summary>
    void RecordHealthCheck(bool isHealthy, string? checkName = null);

    /// <summary>
    /// Gets current SLI statistics for a specific SLO.
    /// </summary>
    SliStatistics? GetStatistics(string sloName, TimeSpan window);

    /// <summary>
    /// Gets all current SLI statistics.
    /// </summary>
    IReadOnlyList<SliStatistics> GetAllStatistics(TimeSpan window);
}

/// <summary>
/// Implementation of SLI metrics tracking.
/// </summary>
public sealed class SliMetrics : ISliMetrics, IDisposable
{
    private readonly ILogger<SliMetrics> _logger;
    private readonly SreOptions _options;
    private readonly Meter _meter;

    // OpenTelemetry metrics
    private readonly Histogram<double> _sliCompliance;
    private readonly Counter<long> _sliEvents;
    private readonly Counter<long> _sliGoodEvents;
    private readonly Counter<long> _sliBadEvents;

    // In-memory storage for recent measurements (used for statistics calculation)
    private readonly ConcurrentDictionary<string, ConcurrentQueue<SliMeasurement>> _measurements = new();
    private readonly TimeSpan _retentionWindow;

    public SliMetrics(
        IOptions<SreOptions> options,
        ILogger<SliMetrics> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _meter = new Meter("Honua.Server.SRE", "1.0.0");
        _retentionWindow = TimeSpan.FromDays(Math.Max(_options.RollingWindowDays, 1));

        // Create OpenTelemetry metrics
        _sliCompliance = _meter.CreateHistogram<double>(
            "honua.sli.compliance",
            unit: "{ratio}",
            description: "SLI compliance measurement (1.0 = good, 0.0 = bad) for calculating SLO achievement");

        _sliEvents = _meter.CreateCounter<long>(
            "honua.sli.events.total",
            unit: "{event}",
            description: "Total number of SLI events measured");

        _sliGoodEvents = _meter.CreateCounter<long>(
            "honua.sli.good_events.total",
            unit: "{event}",
            description: "Number of good SLI events (meeting the SLI criteria)");

        _sliBadEvents = _meter.CreateCounter<long>(
            "honua.sli.bad_events.total",
            unit: "{event}",
            description: "Number of bad SLI events (failing the SLI criteria)");
    }

    public void RecordLatency(TimeSpan duration, string? endpoint = null, string? method = null)
    {
        if (!_options.Enabled) return;

        var durationMs = duration.TotalMilliseconds;

        // Record for each configured latency SLO
        foreach (var (sloName, sloConfig) in _options.Slos)
        {
            if (!sloConfig.Enabled || sloConfig.Type != SliType.Latency || !sloConfig.ThresholdMs.HasValue)
                continue;

            if (!ShouldIncludeEndpoint(endpoint, sloConfig))
                continue;

            var thresholdMs = sloConfig.ThresholdMs.Value;
            var isGood = durationMs <= thresholdMs;

            RecordSliMeasurement(new SliMeasurement
            {
                Name = sloName,
                Type = SliType.Latency,
                Timestamp = DateTimeOffset.UtcNow,
                IsGood = isGood,
                Value = durationMs,
                Threshold = thresholdMs,
                Endpoint = endpoint,
                Method = method
            });

            // Emit OpenTelemetry metrics
            _sliCompliance.Record(isGood ? 1.0 : 0.0,
                new("slo.name", sloName),
                new("sli.type", "latency"),
                new("threshold.ms", thresholdMs),
                new("endpoint", endpoint ?? "all"),
                new("method", method ?? "all"));

            _sliEvents.Add(1,
                new("slo.name", sloName),
                new("sli.type", "latency"));

            if (isGood)
            {
                _sliGoodEvents.Add(1,
                    new("slo.name", sloName),
                    new("sli.type", "latency"));
            }
            else
            {
                _sliBadEvents.Add(1,
                    new("slo.name", sloName),
                    new("sli.type", "latency"));
            }
        }
    }

    public void RecordAvailability(int statusCode, string? endpoint = null, string? method = null)
    {
        if (!_options.Enabled) return;

        // Record for each configured availability SLO
        foreach (var (sloName, sloConfig) in _options.Slos)
        {
            if (!sloConfig.Enabled || sloConfig.Type != SliType.Availability)
                continue;

            if (!ShouldIncludeEndpoint(endpoint, sloConfig))
                continue;

            // Availability: any non-5xx response is considered "good"
            var isGood = statusCode < 500;

            RecordSliMeasurement(new SliMeasurement
            {
                Name = sloName,
                Type = SliType.Availability,
                Timestamp = DateTimeOffset.UtcNow,
                IsGood = isGood,
                Value = statusCode,
                Endpoint = endpoint,
                Method = method,
                StatusCode = statusCode
            });

            // Emit OpenTelemetry metrics
            _sliCompliance.Record(isGood ? 1.0 : 0.0,
                new("slo.name", sloName),
                new("sli.type", "availability"),
                new("endpoint", endpoint ?? "all"),
                new("method", method ?? "all"),
                new("status_code", statusCode.ToString()));

            _sliEvents.Add(1,
                new("slo.name", sloName),
                new("sli.type", "availability"));

            if (isGood)
            {
                _sliGoodEvents.Add(1,
                    new("slo.name", sloName),
                    new("sli.type", "availability"));
            }
            else
            {
                _sliBadEvents.Add(1,
                    new("slo.name", sloName),
                    new("sli.type", "availability"));
            }
        }
    }

    public void RecordError(int statusCode, string? endpoint = null, string? method = null)
    {
        if (!_options.Enabled) return;

        // Record for each configured error rate SLO
        foreach (var (sloName, sloConfig) in _options.Slos)
        {
            if (!sloConfig.Enabled || sloConfig.Type != SliType.ErrorRate)
                continue;

            if (!ShouldIncludeEndpoint(endpoint, sloConfig))
                continue;

            // Error rate: only 5xx errors count as "bad" (4xx are client errors, not service errors)
            var isGood = statusCode < 500;

            RecordSliMeasurement(new SliMeasurement
            {
                Name = sloName,
                Type = SliType.ErrorRate,
                Timestamp = DateTimeOffset.UtcNow,
                IsGood = isGood,
                Value = statusCode,
                Endpoint = endpoint,
                Method = method,
                StatusCode = statusCode
            });

            // Emit OpenTelemetry metrics
            _sliCompliance.Record(isGood ? 1.0 : 0.0,
                new("slo.name", sloName),
                new("sli.type", "error_rate"),
                new("endpoint", endpoint ?? "all"),
                new("method", method ?? "all"),
                new("status_code", statusCode.ToString()));

            _sliEvents.Add(1,
                new("slo.name", sloName),
                new("sli.type", "error_rate"));

            if (isGood)
            {
                _sliGoodEvents.Add(1,
                    new("slo.name", sloName),
                    new("sli.type", "error_rate"));
            }
            else
            {
                _sliBadEvents.Add(1,
                    new("slo.name", sloName),
                    new("sli.type", "error_rate"));
            }
        }
    }

    public void RecordHealthCheck(bool isHealthy, string? checkName = null)
    {
        if (!_options.Enabled) return;

        // Record for each configured health check SLO
        foreach (var (sloName, sloConfig) in _options.Slos)
        {
            if (!sloConfig.Enabled || sloConfig.Type != SliType.HealthCheckSuccess)
                continue;

            RecordSliMeasurement(new SliMeasurement
            {
                Name = sloName,
                Type = SliType.HealthCheckSuccess,
                Timestamp = DateTimeOffset.UtcNow,
                IsGood = isHealthy,
                Value = isHealthy ? 1.0 : 0.0,
                Metadata = checkName
            });

            // Emit OpenTelemetry metrics
            _sliCompliance.Record(isHealthy ? 1.0 : 0.0,
                new("slo.name", sloName),
                new("sli.type", "health_check"),
                new("check_name", checkName ?? "all"));

            _sliEvents.Add(1,
                new("slo.name", sloName),
                new("sli.type", "health_check"));

            if (isHealthy)
            {
                _sliGoodEvents.Add(1,
                    new("slo.name", sloName),
                    new("sli.type", "health_check"));
            }
            else
            {
                _sliBadEvents.Add(1,
                    new("slo.name", sloName),
                    new("sli.type", "health_check"));
            }
        }
    }

    public SliStatistics? GetStatistics(string sloName, TimeSpan window)
    {
        if (!_measurements.TryGetValue(sloName, out var queue))
            return null;

        var cutoff = DateTimeOffset.UtcNow - window;
        var relevantMeasurements = queue.Where(m => m.Timestamp >= cutoff).ToList();

        if (relevantMeasurements.Count == 0)
            return null;

        var first = relevantMeasurements.First();
        return new SliStatistics
        {
            Name = sloName,
            Type = first.Type,
            WindowStart = cutoff,
            WindowEnd = DateTimeOffset.UtcNow,
            TotalEvents = relevantMeasurements.Count,
            GoodEvents = relevantMeasurements.Count(m => m.IsGood),
            Threshold = first.Threshold
        };
    }

    public IReadOnlyList<SliStatistics> GetAllStatistics(TimeSpan window)
    {
        var stats = new List<SliStatistics>();
        var cutoff = DateTimeOffset.UtcNow - window;

        foreach (var (sloName, _) in _options.Slos)
        {
            var stat = GetStatistics(sloName, window);
            if (stat != null)
            {
                stats.Add(stat);
            }
        }

        return stats;
    }

    private void RecordSliMeasurement(SliMeasurement measurement)
    {
        var queue = _measurements.GetOrAdd(measurement.Name, _ => new ConcurrentQueue<SliMeasurement>());
        queue.Enqueue(measurement);

        // Clean up old measurements (simple approach - in production, consider a background cleanup task)
        var cutoff = DateTimeOffset.UtcNow - _retentionWindow;
        while (queue.TryPeek(out var oldest) && oldest.Timestamp < cutoff)
        {
            queue.TryDequeue(out _);
        }
    }

    private static bool ShouldIncludeEndpoint(string? endpoint, SloConfig config)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return true;

        // Check exclusions first
        if (config.ExcludeEndpoints != null && config.ExcludeEndpoints.Length > 0)
        {
            if (config.ExcludeEndpoints.Any(e => endpoint.Contains(e, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        // Check inclusions
        if (config.IncludeEndpoints != null && config.IncludeEndpoints.Length > 0)
        {
            return config.IncludeEndpoints.Any(e => endpoint.Contains(e, StringComparison.OrdinalIgnoreCase));
        }

        return true;
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
