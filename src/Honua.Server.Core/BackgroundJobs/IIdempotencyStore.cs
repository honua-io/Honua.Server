// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.BackgroundJobs;

/// <summary>
/// Store for idempotency tracking to prevent duplicate job processing.
/// Typically implemented using Redis for fast lookups and automatic expiry.
/// </summary>
/// <remarks>
/// Idempotency guarantees:
///
/// 1. At-most-once processing: Each job is processed successfully at most once
/// 2. Duplicate detection: Same job submitted multiple times uses cached result
/// 3. Crash recovery: Worker crash-restart doesn't cause duplicate execution
/// 4. TTL-based cleanup: Results expire after configured period (default 7 days)
///
/// Implementation considerations:
/// - Redis: Fast, automatic expiry via TTL, supports atomic operations
/// - PostgreSQL: Slower but no external dependencies, manual cleanup needed
/// - In-memory: Testing only, not suitable for production
/// </remarks>
public interface IIdempotencyStore
{
    /// <summary>
    /// Gets a cached result for the given idempotency key.
    /// Returns null if not found or expired.
    /// </summary>
    /// <typeparam name="T">Result type</typeparam>
    /// <param name="idempotencyKey">Unique idempotency key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached result if found, null otherwise</returns>
    Task<T?> GetAsync<T>(string idempotencyKey, CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>
    /// Stores a result with the given idempotency key.
    /// Overwrites existing entry if present.
    /// </summary>
    /// <typeparam name="T">Result type</typeparam>
    /// <param name="idempotencyKey">Unique idempotency key</param>
    /// <param name="result">Result to store</param>
    /// <param name="ttl">Time-to-live for the entry (default: 7 days)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StoreAsync<T>(
        string idempotencyKey,
        T result,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>
    /// Checks if an idempotency key exists (without retrieving the value).
    /// </summary>
    /// <param name="idempotencyKey">Unique idempotency key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if key exists and not expired, false otherwise</returns>
    Task<bool> ExistsAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an idempotency entry.
    /// </summary>
    /// <param name="idempotencyKey">Unique idempotency key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about the idempotency store.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Store statistics</returns>
    Task<IdempotencyStoreStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about the idempotency store
/// </summary>
public class IdempotencyStoreStatistics
{
    /// <summary>Total number of cached entries</summary>
    public long TotalEntries { get; init; }

    /// <summary>Approximate memory usage in MB (if applicable)</summary>
    public decimal? MemoryUsageMB { get; init; }

    /// <summary>Hit rate (percentage of lookups that found a cached value)</summary>
    public decimal? HitRate { get; init; }

    /// <summary>Miss rate (percentage of lookups that didn't find a cached value)</summary>
    public decimal? MissRate { get; init; }
}
