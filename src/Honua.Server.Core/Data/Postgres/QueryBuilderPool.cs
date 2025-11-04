// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.ObjectPool;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data.Postgres;

/// <summary>
/// Thread-safe pool for PostgresFeatureQueryBuilder instances.
/// Uses ObjectPool internally with LRU eviction for unused builders.
/// Builders are keyed by service+layer combination.
/// </summary>
public sealed class QueryBuilderPool : DisposableBase
{
    private readonly ConcurrentDictionary<string, PoolEntry> _pools;
    private readonly QueryBuilderPoolMetrics _metrics;
    private readonly int _maxPoolsPerKey;
    private readonly int _maxTotalPools;
    private readonly LinkedList<string> _lruList;
    private readonly ReaderWriterLockSlim _lruLock;

    public QueryBuilderPool(int maxPoolsPerKey = 10, int maxTotalPools = 100)
    {
        if (maxPoolsPerKey <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxPoolsPerKey), "Must be greater than zero");
        if (maxTotalPools <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTotalPools), "Must be greater than zero");

        _maxPoolsPerKey = maxPoolsPerKey;
        _maxTotalPools = maxTotalPools;
        _pools = new ConcurrentDictionary<string, PoolEntry>(StringComparer.Ordinal);
        _metrics = new QueryBuilderPoolMetrics(maxTotalPools);
        _lruList = new LinkedList<string>();
        _lruLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
    }

    /// <summary>
    /// Gets a query builder from the pool or creates a new one.
    /// </summary>
    internal PostgresFeatureQueryBuilder Get(
        ServiceDefinition service,
        LayerDefinition layer,
        int storageSrid,
        int targetSrid)
    {
        ThrowIfDisposed();
        Guard.NotNull(service);
        Guard.NotNull(layer);

        var key = CreateKey(service.Id, layer.Id, storageSrid, targetSrid);
        var stopwatch = _metrics.StartGetTimer();

        try
        {
            var pool = GetOrCreatePool(key, service, layer, storageSrid, targetSrid);
            UpdateLruAccess(key);

            var builder = pool.Pool.Get();

            // Pool.Get() always returns a non-null builder due to our policy
            if (builder == null)
            {
                throw new InvalidOperationException("QueryBuilder pool returned null unexpectedly.");
            }

            // Record pool hit (always true since we created the pool with a valid policy)
            _metrics.RecordPoolHit(service.Id, layer.Id);

            return builder;
        }
        finally
        {
            _metrics.RecordGetDuration(stopwatch.Elapsed.TotalMilliseconds, service.Id, layer.Id);
        }
    }

    /// <summary>
    /// Returns a query builder to the pool.
    /// </summary>
    internal void Return(
        PostgresFeatureQueryBuilder builder,
        ServiceDefinition service,
        LayerDefinition layer,
        int storageSrid,
        int targetSrid)
    {
        if (IsDisposed || builder == null)
            return;

        Guard.NotNull(service);
        Guard.NotNull(layer);

        var key = CreateKey(service.Id, layer.Id, storageSrid, targetSrid);

        if (_pools.TryGetValue(key, out var pool))
        {
            pool.Pool.Return(builder);
            UpdateLruAccess(key);
            _metrics.RecordObjectReturned(service.Id, layer.Id);
        }
        else
        {
            _metrics.RecordObjectDiscarded(service.Id, layer.Id);
        }
    }

    /// <summary>
    /// Warms the cache for a specific service and layer combination.
    /// Pre-creates builders to avoid allocation during query execution.
    /// </summary>
    public void WarmCache(
        ServiceDefinition service,
        LayerDefinition layer,
        int storageSrid,
        int targetSrid,
        int count = 5)
    {
        ThrowIfDisposed();
        Guard.NotNull(service);
        Guard.NotNull(layer);

        if (count <= 0 || count > _maxPoolsPerKey)
            throw new ArgumentOutOfRangeException(nameof(count));

        var key = CreateKey(service.Id, layer.Id, storageSrid, targetSrid);
        var pool = GetOrCreatePool(key, service, layer, storageSrid, targetSrid);

        // Pre-create and return builders to warm the pool
        var builders = new List<PostgresFeatureQueryBuilder>(count);
        for (var i = 0; i < count; i++)
        {
            builders.Add(pool.Pool.Get());
        }

        foreach (var builder in builders)
        {
            pool.Pool.Return(builder);
        }
    }

    /// <summary>
    /// Gets pool statistics for monitoring.
    /// </summary>
    public PoolStatistics GetStatistics()
    {
        ThrowIfDisposed();

        _lruLock.EnterReadLock();
        try
        {
            return new PoolStatistics
            {
                TotalPools = _pools.Count,
                MaxTotalPools = _maxTotalPools,
                MaxPoolsPerKey = _maxPoolsPerKey,
                PoolKeys = _pools.Keys.ToList()
            };
        }
        finally
        {
            _lruLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Clears the pool, disposing all cached builders.
    /// </summary>
    public void Clear()
    {
        ThrowIfDisposed();

        _lruLock.EnterWriteLock();
        try
        {
            _pools.Clear();
            _lruList.Clear();
            _metrics.UpdatePoolSize(0);
        }
        finally
        {
            _lruLock.ExitWriteLock();
        }
    }

    private PoolEntry GetOrCreatePool(
        string key,
        ServiceDefinition service,
        LayerDefinition layer,
        int storageSrid,
        int targetSrid)
    {
        if (_pools.TryGetValue(key, out var existing))
        {
            return existing;
        }

        // Check if we need to evict before creating
        EnforceLruLimit();

        var policy = new QueryBuilderPoolPolicy(service, layer, storageSrid, targetSrid);
        var provider = new DefaultObjectPoolProvider
        {
            MaximumRetained = _maxPoolsPerKey
        };
        var pool = provider.Create(policy);

        var entry = new PoolEntry(pool, service.Id, layer.Id);

        if (_pools.TryAdd(key, entry))
        {
            _metrics.RecordObjectCreated(service.Id, layer.Id);
            _metrics.UpdatePoolSize(_pools.Count);
            return entry;
        }

        // Race condition: another thread created it first
        return _pools[key];
    }

    private void UpdateLruAccess(string key)
    {
        _lruLock.EnterWriteLock();
        try
        {
            // Remove from current position if it exists
            _lruList.Remove(key);

            // Add to front (most recently used)
            _lruList.AddFirst(key);
        }
        finally
        {
            _lruLock.ExitWriteLock();
        }
    }

    private void EnforceLruLimit()
    {
        if (_pools.Count < _maxTotalPools)
            return;

        _lruLock.EnterWriteLock();
        try
        {
            while (_pools.Count >= _maxTotalPools && _lruList.Count > 0)
            {
                // Remove least recently used (from the back)
                var lruKey = _lruList.Last?.Value;
                if (lruKey != null)
                {
                    _lruList.RemoveLast();

                    if (_pools.TryRemove(lruKey, out var removed))
                    {
                        _metrics.RecordObjectDiscarded(removed.ServiceId, removed.LayerId);
                    }
                }
            }

            _metrics.UpdatePoolSize(_pools.Count);
        }
        finally
        {
            _lruLock.ExitWriteLock();
        }
    }

    private static string CreateKey(string serviceId, string layerId, int storageSrid, int targetSrid)
    {
        return $"{serviceId}:{layerId}:{storageSrid}:{targetSrid}";
    }

    protected override void DisposeCore()
    {
        _lruLock.Dispose();
        _metrics.Dispose();
        _pools.Clear();
    }

    private sealed class PoolEntry
    {
        public ObjectPool<PostgresFeatureQueryBuilder> Pool { get; }
        public string ServiceId { get; }
        public string LayerId { get; }

        public PoolEntry(ObjectPool<PostgresFeatureQueryBuilder> pool, string serviceId, string layerId)
        {
            Pool = pool;
            ServiceId = serviceId;
            LayerId = layerId;
        }
    }
}

/// <summary>
/// Statistics about the query builder pool.
/// </summary>
public sealed class PoolStatistics
{
    public int TotalPools { get; init; }
    public int MaxTotalPools { get; init; }
    public int MaxPoolsPerKey { get; init; }
    public IReadOnlyList<string> PoolKeys { get; init; } = Array.Empty<string>();
}
