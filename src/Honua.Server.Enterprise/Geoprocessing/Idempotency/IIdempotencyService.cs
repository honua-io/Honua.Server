// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Enterprise.Geoprocessing.Idempotency;

/// <summary>
/// Service for managing idempotency guarantees for geoprocessing jobs.
/// Prevents duplicate execution when workers crash and restart.
/// </summary>
public interface IIdempotencyService
{
    /// <summary>
    /// Computes the idempotency key for a job.
    /// Key is SHA256 hash of: job.Id + job.Inputs (serialized) + job.ProcessId
    /// </summary>
    /// <param name="job">The process run to compute key for</param>
    /// <returns>The idempotency key (SHA256 hash)</returns>
    string ComputeIdempotencyKey(ProcessRun job);

    /// <summary>
    /// Checks if a job has already been completed (idempotency cache hit).
    /// </summary>
    /// <param name="idempotencyKey">The idempotency key to check</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Cached result if found, null otherwise</returns>
    Task<IdempotencyCacheEntry?> GetCachedResultAsync(string idempotencyKey, CancellationToken ct = default);

    /// <summary>
    /// Stores a completed job result in the idempotency cache.
    /// </summary>
    /// <param name="idempotencyKey">The idempotency key</param>
    /// <param name="job">The completed job</param>
    /// <param name="result">The job result</param>
    /// <param name="ttl">Time-to-live for cache entry (default: 7 days)</param>
    /// <param name="ct">Cancellation token</param>
    Task StoreCachedResultAsync(
        string idempotencyKey,
        ProcessRun job,
        ProcessResult result,
        TimeSpan? ttl = null,
        CancellationToken ct = default);

    /// <summary>
    /// Removes expired idempotency entries.
    /// Should be called periodically by a background service.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Number of entries removed</returns>
    Task<int> CleanupExpiredEntriesAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets statistics about the idempotency cache.
    /// </summary>
    /// <param name="tenantId">Optional tenant ID to filter by</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Cache statistics</returns>
    Task<IdempotencyStatistics> GetStatisticsAsync(Guid? tenantId = null, CancellationToken ct = default);
}

/// <summary>
/// Represents a cached idempotency entry
/// </summary>
public class IdempotencyCacheEntry
{
    /// <summary>The idempotency key</summary>
    public required string IdempotencyKey { get; init; }

    /// <summary>Original job ID</summary>
    public required string JobId { get; init; }

    /// <summary>Cached result</summary>
    public required ProcessResult Result { get; init; }

    /// <summary>When the job completed</summary>
    public required DateTimeOffset CompletedAt { get; init; }

    /// <summary>When this cache entry expires</summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Result hash for integrity verification</summary>
    public required string ResultHash { get; init; }
}

/// <summary>
/// Statistics about the idempotency cache
/// </summary>
public class IdempotencyStatistics
{
    /// <summary>Total number of cached entries</summary>
    public long TotalEntries { get; init; }

    /// <summary>Number of entries that will expire in next 24 hours</summary>
    public long ExpiringIn24Hours { get; init; }

    /// <summary>Number of already-expired entries awaiting cleanup</summary>
    public long ExpiredEntries { get; init; }

    /// <summary>Total size of cached results in MB</summary>
    public decimal TotalSizeMB { get; init; }

    /// <summary>Oldest cache entry timestamp</summary>
    public DateTimeOffset? OldestEntry { get; init; }

    /// <summary>Newest cache entry timestamp</summary>
    public DateTimeOffset? NewestEntry { get; init; }
}
