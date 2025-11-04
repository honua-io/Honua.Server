// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data.Postgres;

/// <summary>
/// OpenTelemetry metrics for QueryBuilder pooling operations.
/// Tracks pool hits, misses, object creation, and pool statistics.
/// </summary>
internal sealed class QueryBuilderPoolMetrics : IDisposable
{
    private static readonly Meter Meter = new("Honua.Server.Postgres.QueryBuilderPool", "1.0.0");

    private readonly Counter<long> _poolHitsCounter;
    private readonly Counter<long> _poolMissesCounter;
    private readonly Counter<long> _objectsCreatedCounter;
    private readonly Counter<long> _objectsReturnedCounter;
    private readonly Counter<long> _objectsDiscardedCounter;
    private readonly Histogram<double> _poolGetDuration;
    private readonly ObservableGauge<int> _poolSizeGauge;
    private readonly ObservableGauge<int> _poolCapacityGauge;

    private int _currentPoolSize;
    private int _poolCapacity;

    public QueryBuilderPoolMetrics(int initialCapacity = 100)
    {
        _poolCapacity = initialCapacity;

        _poolHitsCounter = Meter.CreateCounter<long>(
            name: "honua.postgres.querybuilder_pool.hits",
            unit: "hits",
            description: "Number of successful query builder retrievals from the pool");

        _poolMissesCounter = Meter.CreateCounter<long>(
            name: "honua.postgres.querybuilder_pool.misses",
            unit: "misses",
            description: "Number of times a new query builder had to be created");

        _objectsCreatedCounter = Meter.CreateCounter<long>(
            name: "honua.postgres.querybuilder_pool.objects_created",
            unit: "objects",
            description: "Total number of query builder objects created");

        _objectsReturnedCounter = Meter.CreateCounter<long>(
            name: "honua.postgres.querybuilder_pool.objects_returned",
            unit: "objects",
            description: "Number of query builder objects returned to the pool");

        _objectsDiscardedCounter = Meter.CreateCounter<long>(
            name: "honua.postgres.querybuilder_pool.objects_discarded",
            unit: "objects",
            description: "Number of query builder objects discarded (not returned to pool)");

        _poolGetDuration = Meter.CreateHistogram<double>(
            name: "honua.postgres.querybuilder_pool.get_duration",
            unit: "ms",
            description: "Time taken to get a query builder from the pool");

        _poolSizeGauge = Meter.CreateObservableGauge(
            name: "honua.postgres.querybuilder_pool.size",
            observeValue: () => _currentPoolSize,
            unit: "objects",
            description: "Current number of query builders in the pool");

        _poolCapacityGauge = Meter.CreateObservableGauge(
            name: "honua.postgres.querybuilder_pool.capacity",
            observeValue: () => _poolCapacity,
            unit: "objects",
            description: "Maximum capacity of the query builder pool");
    }

    public void RecordPoolHit(string serviceId, string layerId)
    {
        _poolHitsCounter.Add(1, new("service", serviceId), new("layer", layerId));
    }

    public void RecordPoolMiss(string serviceId, string layerId)
    {
        _poolMissesCounter.Add(1, new("service", serviceId), new("layer", layerId));
    }

    public void RecordObjectCreated(string serviceId, string layerId)
    {
        _objectsCreatedCounter.Add(1, new("service", serviceId), new("layer", layerId));
        System.Threading.Interlocked.Increment(ref _currentPoolSize);
    }

    public void RecordObjectReturned(string serviceId, string layerId)
    {
        _objectsReturnedCounter.Add(1, new("service", serviceId), new("layer", layerId));
    }

    public void RecordObjectDiscarded(string serviceId, string layerId)
    {
        _objectsDiscardedCounter.Add(1, new("service", serviceId), new("layer", layerId));
        System.Threading.Interlocked.Decrement(ref _currentPoolSize);
    }

    public void RecordGetDuration(double milliseconds, string serviceId, string layerId)
    {
        _poolGetDuration.Record(milliseconds, new("service", serviceId), new("layer", layerId));
    }

    public void UpdatePoolSize(int size)
    {
        _currentPoolSize = size;
    }

    public void UpdatePoolCapacity(int capacity)
    {
        _poolCapacity = capacity;
    }

    public void Dispose()
    {
        // Meter disposal is handled by the application
    }

    public Stopwatch StartGetTimer() => Stopwatch.StartNew();
}
