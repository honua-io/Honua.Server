// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Features.Strategies;

/// <summary>
/// Adaptive metrics service that degrades from real-time to in-memory to log-only.
/// </summary>
public sealed class AdaptiveMetricsService
{
    private readonly AdaptiveFeatureService _adaptiveFeature;
    private readonly Meter? _meter;
    private readonly ILogger<AdaptiveMetricsService> _logger;
    private readonly ConcurrentDictionary<string, long> _inMemoryCounters = new();

    public AdaptiveMetricsService(
        AdaptiveFeatureService adaptiveFeature,
        ILogger<AdaptiveMetricsService> logger,
        Meter? meter = null)
    {
        _adaptiveFeature = adaptiveFeature ?? throw new ArgumentNullException(nameof(adaptiveFeature));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _meter = meter;
    }

    /// <summary>
    /// Records a counter metric.
    /// </summary>
    public async Task RecordCounterAsync(
        string name,
        long value = 1,
        params KeyValuePair<string, object?>[] tags)
    {
        var mode = await _adaptiveFeature.GetMetricsModeAsync();

        switch (mode)
        {
            case MetricsMode.RealTime:
                if (_meter != null)
                {
                    var counter = _meter.CreateCounter<long>(name);
                    counter.Add(value, tags);
                }
                break;

            case MetricsMode.InMemory:
                _inMemoryCounters.AddOrUpdate(name, value, (_, current) => current + value);
                break;

            case MetricsMode.LogOnly:
                _logger.LogDebug(
                    "Metric {MetricName}: {Value} (log-only mode)",
                    name,
                    value);
                break;
        }
    }

    /// <summary>
    /// Records a histogram metric.
    /// </summary>
    public async Task RecordHistogramAsync(
        string name,
        double value,
        params KeyValuePair<string, object?>[] tags)
    {
        var mode = await _adaptiveFeature.GetMetricsModeAsync();

        switch (mode)
        {
            case MetricsMode.RealTime:
                if (_meter != null)
                {
                    var histogram = _meter.CreateHistogram<double>(name);
                    histogram.Record(value, tags);
                }
                break;

            case MetricsMode.InMemory:
                // For in-memory mode, just track count
                var counterName = $"{name}_count";
                _inMemoryCounters.AddOrUpdate(counterName, 1, (_, current) => current + 1);
                break;

            case MetricsMode.LogOnly:
                _logger.LogDebug(
                    "Metric {MetricName}: {Value} (log-only mode)",
                    name,
                    value);
                break;
        }
    }

    /// <summary>
    /// Gets in-memory counter value (for debugging/monitoring during degradation).
    /// </summary>
    public long GetInMemoryCounter(string name)
    {
        return _inMemoryCounters.TryGetValue(name, out var value) ? value : 0;
    }

    /// <summary>
    /// Resets in-memory counters.
    /// </summary>
    public void ResetInMemoryCounters()
    {
        _inMemoryCounters.Clear();
    }
}

/// <summary>
/// Helper for recording common metrics with degradation support.
/// </summary>
public sealed class DegradableMetrics
{
    private readonly AdaptiveMetricsService _metrics;

    public DegradableMetrics(AdaptiveMetricsService metrics)
    {
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    public Task RecordRequestAsync(string endpoint, int statusCode)
    {
        return _metrics.RecordCounterAsync(
            "http_requests_total",
            1,
            new KeyValuePair<string, object?>("endpoint", endpoint),
            new KeyValuePair<string, object?>("status", statusCode));
    }

    public Task RecordRequestDurationAsync(string endpoint, double durationMs)
    {
        return _metrics.RecordHistogramAsync(
            "http_request_duration_ms",
            durationMs,
            new KeyValuePair<string, object?>("endpoint", endpoint));
    }

    public Task RecordCacheHitAsync(string cacheType, bool hit)
    {
        return _metrics.RecordCounterAsync(
            "cache_operations_total",
            1,
            new KeyValuePair<string, object?>("type", cacheType),
            new KeyValuePair<string, object?>("result", hit ? "hit" : "miss"));
    }

    public Task RecordDatabaseQueryAsync(string operation, double durationMs)
    {
        return _metrics.RecordHistogramAsync(
            "database_query_duration_ms",
            durationMs,
            new KeyValuePair<string, object?>("operation", operation));
    }

    public Task RecordFeatureDegradationAsync(string featureName, string state)
    {
        return _metrics.RecordCounterAsync(
            "feature_degradation_events_total",
            1,
            new KeyValuePair<string, object?>("feature", featureName),
            new KeyValuePair<string, object?>("state", state));
    }
}
