// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Metadata;

/// <summary>
/// Metrics for metadata cache operations.
/// </summary>
public sealed class MetadataCacheMetrics
{
    private readonly Meter _meter;
    private readonly Counter<long> _cacheHits;
    private readonly Counter<long> _cacheMisses;
    private readonly Counter<long> _cacheErrors;
    private readonly Counter<long> _cacheInvalidationFailures;
    private readonly Counter<long> _cacheInvalidationRetries;
    private readonly Counter<long> _cacheInvalidationSuccesses;
    private readonly Histogram<double> _cacheOperationDuration;
    private readonly ObservableGauge<double> _cacheHitRate;

    private long _totalHits;
    private long _totalMisses;
    private long _totalInvalidationFailures;
    private long _totalInvalidationRetries;
    private long _totalInvalidationSuccesses;

    public MetadataCacheMetrics(IMeterFactory? meterFactory = null)
    {
        _meter = meterFactory?.Create("Honua.Metadata.Cache") ?? new Meter("Honua.Metadata.Cache");

        _cacheHits = _meter.CreateCounter<long>(
            "honua.metadata.cache.hits",
            description: "Total number of metadata cache hits");

        _cacheMisses = _meter.CreateCounter<long>(
            "honua.metadata.cache.misses",
            description: "Total number of metadata cache misses");

        _cacheErrors = _meter.CreateCounter<long>(
            "honua.metadata.cache.errors",
            description: "Total number of metadata cache errors");

        _cacheInvalidationFailures = _meter.CreateCounter<long>(
            "honua.metadata.cache.invalidation.failures",
            description: "Total number of cache invalidation failures");

        _cacheInvalidationRetries = _meter.CreateCounter<long>(
            "honua.metadata.cache.invalidation.retries",
            description: "Total number of cache invalidation retry attempts");

        _cacheInvalidationSuccesses = _meter.CreateCounter<long>(
            "honua.metadata.cache.invalidation.successes",
            description: "Total number of successful cache invalidations");

        _cacheOperationDuration = _meter.CreateHistogram<double>(
            "honua.metadata.cache.operation.duration",
            unit: "ms",
            description: "Duration of metadata cache operations");

        _cacheHitRate = _meter.CreateObservableGauge<double>(
            "honua.metadata.cache.hit_rate",
            observeValue: () => GetHitRate(),
            description: "Metadata cache hit rate (0-1)");
    }

    public void RecordCacheHit()
    {
        _cacheHits.Add(1);
        Interlocked.Increment(ref _totalHits);
    }

    public void RecordCacheMiss()
    {
        _cacheMisses.Add(1);
        Interlocked.Increment(ref _totalMisses);
    }

    public void RecordCacheError()
    {
        _cacheErrors.Add(1);
    }

    public void RecordOperationDuration(double durationMs, string operation)
    {
        _cacheOperationDuration.Record(durationMs, new KeyValuePair<string, object?>("operation", operation));
    }

    public double GetHitRate()
    {
        var hits = Interlocked.Read(ref _totalHits);
        var misses = Interlocked.Read(ref _totalMisses);
        var total = hits + misses;
        return total > 0 ? (double)hits / total : 0.0;
    }

    public void RecordInvalidationSuccess()
    {
        _cacheInvalidationSuccesses.Add(1);
        Interlocked.Increment(ref _totalInvalidationSuccesses);
    }

    public void RecordInvalidationFailure()
    {
        _cacheInvalidationFailures.Add(1);
        Interlocked.Increment(ref _totalInvalidationFailures);
    }

    public void RecordInvalidationRetry()
    {
        _cacheInvalidationRetries.Add(1);
        Interlocked.Increment(ref _totalInvalidationRetries);
    }

    public (long Hits, long Misses, double HitRate) GetStatistics()
    {
        var hits = Interlocked.Read(ref _totalHits);
        var misses = Interlocked.Read(ref _totalMisses);
        return (hits, misses, GetHitRate());
    }

    public (long Successes, long Failures, long Retries) GetInvalidationStatistics()
    {
        var successes = Interlocked.Read(ref _totalInvalidationSuccesses);
        var failures = Interlocked.Read(ref _totalInvalidationFailures);
        var retries = Interlocked.Read(ref _totalInvalidationRetries);
        return (successes, failures, retries);
    }
}
