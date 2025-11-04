// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Data;

/// <summary>
/// Abstract base class for in-memory store implementations using ConcurrentDictionary.
/// Provides thread-safe CRUD operations and query support with configurable size limits.
/// </summary>
/// <typeparam name="TEntity">The entity type stored in the collection.</typeparam>
/// <typeparam name="TKey">The type of the entity key. Must be non-nullable.</typeparam>
/// <remarks>
/// <para>
/// This base class eliminates duplicate in-memory storage patterns across the codebase.
/// It uses ConcurrentDictionary for thread-safe operations without explicit locking.
/// </para>
/// <para>
/// <strong>Size Limits (NEW):</strong>
/// To prevent OutOfMemoryException, stores now support optional size limits with LRU eviction.
/// Derived classes can set MaxSize to enforce limits. When limit is reached, oldest entries
/// are evicted automatically to make room for new entries.
/// </para>
/// <para>
/// Thread-Safety: All operations are thread-safe. ConcurrentDictionary provides lock-free
/// reads and fine-grained locking for writes. For operations requiring atomic updates
/// across multiple keys, derived classes should implement their own synchronization.
/// </para>
/// <para>
/// Async API: Methods are async for consistency with database-backed stores, enabling
/// easier swapping between in-memory and persistent implementations. In-memory operations
/// complete synchronously but return completed tasks.
/// </para>
/// </remarks>
public abstract class InMemoryStoreBase<TEntity, TKey>
    where TEntity : class
    where TKey : notnull
{
    /// <summary>
    /// Gets the underlying storage dictionary.
    /// Protected to allow derived classes to implement custom operations if needed.
    /// </summary>
    protected ConcurrentDictionary<TKey, TEntity> Storage { get; }

    /// <summary>
    /// Tracks access times for LRU eviction.
    /// </summary>
    protected ConcurrentDictionary<TKey, long> AccessTimes { get; }

    private long _accessCounter;
    private long _evictionCount;

    /// <summary>
    /// Maximum size of the store. 0 = unlimited.
    /// Derived classes can set this to enforce size limits.
    /// </summary>
    protected int MaxSize { get; set; } = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryStoreBase{TEntity, TKey}"/> class.
    /// </summary>
    protected InMemoryStoreBase()
    {
        Storage = new ConcurrentDictionary<TKey, TEntity>();
        AccessTimes = new ConcurrentDictionary<TKey, long>();
    }

    /// <summary>
    /// Initializes a new instance with a custom key comparer.
    /// </summary>
    /// <param name="comparer">The equality comparer to use for keys.</param>
    protected InMemoryStoreBase(IEqualityComparer<TKey> comparer)
    {
        Guard.NotNull(comparer);
        Storage = new ConcurrentDictionary<TKey, TEntity>(comparer);
        AccessTimes = new ConcurrentDictionary<TKey, long>(comparer);
    }

    /// <summary>
    /// Extracts the key from an entity.
    /// Derived classes must implement this to specify how to obtain the key.
    /// </summary>
    /// <param name="entity">The entity to extract the key from.</param>
    /// <returns>The key for the entity.</returns>
    protected abstract TKey GetKey(TEntity entity);

    /// <summary>
    /// Retrieves an entity by its key.
    /// </summary>
    /// <param name="key">The entity key.</param>
    /// <param name="cancellationToken">Cancellation token (unused for in-memory operations).</param>
    /// <returns>The entity if found, otherwise null.</returns>
    public virtual Task<TEntity?> GetAsync(TKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        // Update access time for LRU tracking
        if (Storage.TryGetValue(key, out var entity))
        {
            UpdateAccessTime(key);
        }

        return Task.FromResult(entity);
    }

    /// <summary>
    /// Retrieves all entities from the store.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token (unused for in-memory operations).</param>
    /// <returns>A read-only list of all entities.</returns>
    public virtual Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = Storage.Values.ToList();
        return Task.FromResult<IReadOnlyList<TEntity>>(list);
    }

    /// <summary>
    /// Checks if an entity with the specified key exists.
    /// </summary>
    /// <param name="key">The entity key.</param>
    /// <param name="cancellationToken">Cancellation token (unused for in-memory operations).</param>
    /// <returns>True if the entity exists, otherwise false.</returns>
    public virtual Task<bool> ExistsAsync(TKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        var exists = Storage.ContainsKey(key);
        return Task.FromResult(exists);
    }

    /// <summary>
    /// Stores or updates an entity.
    /// The key is automatically extracted from the entity using <see cref="GetKey"/>.
    /// </summary>
    /// <param name="entity">The entity to store.</param>
    /// <param name="cancellationToken">Cancellation token (unused for in-memory operations).</param>
    public virtual Task PutAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(entity);
        var key = GetKey(entity);

        // Check size limit and evict if necessary
        if (MaxSize > 0 && !Storage.ContainsKey(key) && Storage.Count >= MaxSize)
        {
            EvictLeastRecentlyUsed();
        }

        Storage[key] = entity;
        UpdateAccessTime(key);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stores or updates multiple entities in a single operation.
    /// </summary>
    /// <param name="entities">The entities to store.</param>
    /// <param name="cancellationToken">Cancellation token (unused for in-memory operations).</param>
    public virtual Task PutManyAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(entities);

        foreach (var entity in entities)
        {
            Guard.NotNull(entity);
            var key = GetKey(entity);
            Storage[key] = entity;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Deletes an entity by its key.
    /// </summary>
    /// <param name="key">The entity key.</param>
    /// <param name="cancellationToken">Cancellation token (unused for in-memory operations).</param>
    /// <returns>True if the entity was deleted, false if it didn't exist.</returns>
    public virtual Task<bool> DeleteAsync(TKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        var removed = Storage.TryRemove(key, out _);
        if (removed)
        {
            AccessTimes.TryRemove(key, out _);
        }
        return Task.FromResult(removed);
    }

    /// <summary>
    /// Removes all entities from the store.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token (unused for in-memory operations).</param>
    public virtual Task ClearAsync(CancellationToken cancellationToken = default)
    {
        Storage.Clear();
        AccessTimes.Clear();
        _accessCounter = 0;
        _evictionCount = 0;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the total number of entities in the store.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token (unused for in-memory operations).</param>
    /// <returns>The count of entities.</returns>
    public virtual Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var count = Storage.Count;
        return Task.FromResult(count);
    }

    /// <summary>
    /// Queries entities using a predicate function.
    /// </summary>
    /// <param name="predicate">The filter predicate to apply.</param>
    /// <param name="cancellationToken">Cancellation token (unused for in-memory operations).</param>
    /// <returns>A read-only list of entities matching the predicate.</returns>
    /// <remarks>
    /// This method materializes all entities into memory before filtering.
    /// For large datasets, consider implementing custom filtering in derived classes.
    /// </remarks>
    public virtual Task<IReadOnlyList<TEntity>> QueryAsync(
        Func<TEntity, bool> predicate,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(predicate);
        var results = Storage.Values.Where(predicate).ToList();
        return Task.FromResult<IReadOnlyList<TEntity>>(results);
    }

    /// <summary>
    /// Attempts to add an entity only if it doesn't already exist.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <param name="cancellationToken">Cancellation token (unused for in-memory operations).</param>
    /// <returns>True if the entity was added, false if it already existed.</returns>
    public virtual Task<bool> TryAddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(entity);
        var key = GetKey(entity);

        // Check size limit and evict if necessary
        if (MaxSize > 0 && Storage.Count >= MaxSize && !Storage.ContainsKey(key))
        {
            EvictLeastRecentlyUsed();
        }

        var added = Storage.TryAdd(key, entity);
        if (added)
        {
            UpdateAccessTime(key);
        }
        return Task.FromResult(added);
    }

    /// <summary>
    /// Updates an entity atomically using a update function.
    /// </summary>
    /// <param name="key">The entity key.</param>
    /// <param name="updateFactory">Function that receives the existing entity and returns the updated entity.</param>
    /// <param name="cancellationToken">Cancellation token (unused for in-memory operations).</param>
    /// <returns>The updated entity, or null if the key doesn't exist.</returns>
    /// <remarks>
    /// The update function may be called multiple times if there is contention.
    /// Ensure the function is safe to call multiple times with the same input.
    /// </remarks>
    public virtual Task<TEntity?> UpdateAsync(
        TKey key,
        Func<TEntity, TEntity> updateFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        Guard.NotNull(updateFactory);

        if (!Storage.TryGetValue(key, out var existing))
        {
            return Task.FromResult<TEntity?>(null);
        }

        var updated = Storage.AddOrUpdate(
            key,
            _ => throw new InvalidOperationException($"Entity with key '{key}' not found"),
            (_, current) => updateFactory(current));

        return Task.FromResult<TEntity?>(updated);
    }

    /// <summary>
    /// Gets all keys currently in the store.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token (unused for in-memory operations).</param>
    /// <returns>A read-only list of all keys.</returns>
    public virtual Task<IReadOnlyList<TKey>> GetKeysAsync(CancellationToken cancellationToken = default)
    {
        var keys = Storage.Keys.ToList();
        return Task.FromResult<IReadOnlyList<TKey>>(keys);
    }

    /// <summary>
    /// Gets store statistics including size and eviction count.
    /// </summary>
    public virtual InMemoryStoreStatistics GetStatistics()
    {
        return new InMemoryStoreStatistics
        {
            EntryCount = Storage.Count,
            MaxSize = MaxSize,
            EvictionCount = Interlocked.Read(ref _evictionCount),
            AccessCount = Interlocked.Read(ref _accessCounter)
        };
    }

    /// <summary>
    /// Updates the access time for LRU tracking.
    /// </summary>
    protected void UpdateAccessTime(TKey key)
    {
        var currentAccess = Interlocked.Increment(ref _accessCounter);
        AccessTimes.AddOrUpdate(key, currentAccess, (_, __) => currentAccess);
    }

    /// <summary>
    /// Evicts the least recently used entry to make room for new entries.
    /// Protected virtual to allow derived classes to customize eviction logic.
    /// </summary>
    protected virtual void EvictLeastRecentlyUsed()
    {
        if (Storage.IsEmpty)
        {
            return;
        }

        // Find the least recently used entry
        TKey? oldestKey = default;
        long oldestAccess = long.MaxValue;

        foreach (var kvp in AccessTimes)
        {
            if (kvp.Value < oldestAccess)
            {
                oldestAccess = kvp.Value;
                oldestKey = kvp.Key;
            }
        }

        // Remove the oldest entry
        if (oldestKey != null && !EqualityComparer<TKey>.Default.Equals(oldestKey, default))
        {
            Storage.TryRemove(oldestKey, out _);
            AccessTimes.TryRemove(oldestKey, out _);
            Interlocked.Increment(ref _evictionCount);

            OnEntryEvicted(oldestKey);
        }
    }

    /// <summary>
    /// Called when an entry is evicted. Override to add logging or metrics.
    /// </summary>
    /// <param name="key">The key of the evicted entry.</param>
    protected virtual void OnEntryEvicted(TKey key)
    {
        // Derived classes can override to add logging or metrics
    }
}

/// <summary>
/// Statistics for in-memory store monitoring.
/// </summary>
public sealed class InMemoryStoreStatistics
{
    public int EntryCount { get; init; }
    public int MaxSize { get; init; }
    public long EvictionCount { get; init; }
    public long AccessCount { get; init; }
    public double UtilizationRate => MaxSize > 0 ? (double)EntryCount / MaxSize : 0;
}

/// <summary>
/// Abstract base class for in-memory stores using string keys.
/// This is a convenience class for the most common key type.
/// </summary>
/// <typeparam name="TEntity">The entity type stored in the collection.</typeparam>
/// <remarks>
/// Uses case-sensitive string comparison by default. Override the constructor
/// in derived classes to provide a custom string comparer (e.g., case-insensitive).
/// </remarks>
public abstract class InMemoryStoreBase<TEntity> : InMemoryStoreBase<TEntity, string>
    where TEntity : class
{
    /// <summary>
    /// Initializes a new instance with case-sensitive string comparison.
    /// </summary>
    protected InMemoryStoreBase()
        : base()
    {
    }

    /// <summary>
    /// Initializes a new instance with a custom string comparer.
    /// </summary>
    /// <param name="comparer">The string comparer to use for keys.</param>
    protected InMemoryStoreBase(StringComparer comparer)
        : base(comparer)
    {
    }
}
