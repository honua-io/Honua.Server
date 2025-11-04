// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data;

/// <summary>
/// Thread-safe cache for prepared SQL statements to improve query performance.
/// Prepared statements are parsed once and reused, reducing database round trips.
/// Uses LRU eviction policy with a maximum of 1000 cached statements.
/// </summary>
public sealed class PreparedStatementCache : DisposableBase
{
    private const int MaxCacheSize = 1000;
    private readonly ConcurrentDictionary<string, object> _cache = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _lruList = new();
    private readonly ReaderWriterLockSlim _lruLock = new();

    // Metrics for observability
    private static readonly Meter Meter = new("Honua.Server.Database.PreparedStatements", "1.0.0");
    private static readonly Counter<long> CacheHitsCounter = Meter.CreateCounter<long>(
        "prepared_statement_cache_hits_total", "hits", "Total number of cache hits");
    private static readonly Counter<long> CacheMissesCounter = Meter.CreateCounter<long>(
        "prepared_statement_cache_misses_total", "misses", "Total number of cache misses");
    private static readonly Counter<long> CacheEvictionsCounter = Meter.CreateCounter<long>(
        "prepared_statement_cache_evictions_total", "evictions", "Total number of cache evictions");
    // Note: For multi-instance scenarios, this gauge will only track one instance
    // Consider using instance-specific metrics if multiple caches exist
    private ObservableGauge<int>? _cacheSizeGauge;

    public PreparedStatementCache()
    {
        // Create observable gauge for this instance's cache size
        _cacheSizeGauge = Meter.CreateObservableGauge(
            "prepared_statement_cache_size",
            () => new Measurement<int>(_cache.Count),
            "statements",
            "Current number of cached prepared statements");
    }

    /// <summary>
    /// Gets or creates a cached prepared statement.
    /// </summary>
    /// <typeparam name="T">The type of the prepared statement (e.g., NpgsqlCommand, MySqlCommand).</typeparam>
    /// <param name="key">Unique cache key (typically connection string + SQL hash).</param>
    /// <param name="factory">Factory function to create and prepare the statement if not cached.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached or newly created prepared statement.</returns>
    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        CancellationToken cancellationToken = default) where T : class
    {
        ThrowIfDisposed();
        Guard.NotNullOrWhiteSpace(key);
        Guard.NotNull(factory);

        // Check if already cached
        if (_cache.TryGetValue(key, out var cached))
        {
            // Record cache hit
            CacheHitsCounter.Add(1);

            // Update LRU position on cache hit
            UpdateLruList(key);
            return (T)cached;
        }

        // Record cache miss
        CacheMissesCounter.Add(1);

        // Enforce LRU limit before adding new entry
        EnforceLruLimit();

        // Create new prepared statement
        var command = await factory().ConfigureAwait(false);

        // Add to cache (thread-safe)
        // If another thread added it concurrently, use their version instead
        var added = _cache.GetOrAdd(key, command);

        // If we didn't win the race, dispose our command and use the cached one
        if (!ReferenceEquals(added, command) && command is IDisposable disposable)
        {
            disposable.Dispose();
        }
        else
        {
            // We won the race, update LRU list
            UpdateLruList(key);
        }

        return (T)added;
    }

    private void UpdateLruList(string key)
    {
        _lruLock.EnterWriteLock();
        try
        {
            // Remove existing entry if present
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
        if (_cache.Count < MaxCacheSize)
        {
            return;
        }

        _lruLock.EnterWriteLock();
        try
        {
            while (_cache.Count >= MaxCacheSize && _lruList.Count > 0)
            {
                // Remove least recently used (from the back)
                var oldest = _lruList.Last!.Value;
                _lruList.RemoveLast();

                if (_cache.TryRemove(oldest, out var removed))
                {
                    // Record cache eviction
                    CacheEvictionsCounter.Add(1);

                    // Dispose the statement if it implements IDisposable
                    if (removed is IDisposable disposable)
                    {
                        try
                        {
                            disposable.Dispose();
                        }
                        catch
                        {
                            // Ignore disposal errors
                        }
                    }
                }
            }
        }
        finally
        {
            _lruLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Clears all cached prepared statements.
    /// </summary>
    public void Clear()
    {
        ThrowIfDisposed();

        _lruLock.EnterWriteLock();
        try
        {
            // Dispose all cached commands if they implement IDisposable
            foreach (var item in _cache.Values)
            {
                if (item is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch
                    {
                        // Ignore disposal errors
                    }
                }
            }

            _cache.Clear();
            _lruList.Clear();
        }
        finally
        {
            _lruLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets the number of cached prepared statements.
    /// </summary>
    public int Count => _cache.Count;

    protected override void DisposeCore()
    {
        Clear();
        _lruLock.Dispose();
    }
}
