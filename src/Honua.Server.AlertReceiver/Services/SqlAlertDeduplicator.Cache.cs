// <copyright file="SqlAlertDeduplicator.Cache.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using Microsoft.Extensions.Caching.Memory;

namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Cache operations partial class for SqlAlertDeduplicator.
/// Contains all memory cache management methods for reservation tracking.
/// MEMORY LEAK FIX: Uses bounded MemoryCache with size limits and TTL instead of unbounded static dictionary.
/// </summary>
public sealed partial class SqlAlertDeduplicator
{
    /// <summary>
    /// MEMORY MANAGEMENT: Adds a reservation to the bounded MemoryCache with automatic eviction.
    /// The cache is configured with:
    /// - Size limit: Prevents unbounded memory growth
    /// - Sliding expiration: Evicts inactive entries
    /// - Absolute expiration: Ensures entries don't live forever
    /// - Eviction callback: Tracks cache size for monitoring
    /// </summary>
    private void AddReservationToCache(string reservationId, ReservationState reservation)
    {
        var cacheEntryOptions = new MemoryCacheEntryOptions()
            // Each entry has a size of 1 for simple counting
            .SetSize(1)
            // Sliding expiration: Reset TTL on each access (prevents premature eviction of active reservations)
            .SetSlidingExpiration(TimeSpan.FromSeconds(this.cacheOptions.SlidingExpirationSeconds))
            // Absolute expiration: Maximum lifetime regardless of access (prevents zombie entries)
            .SetAbsoluteExpiration(TimeSpan.FromSeconds(this.cacheOptions.AbsoluteExpirationSeconds))
            // Eviction callback: Track cache size and log evictions for monitoring
            .RegisterPostEvictionCallback((key, value, reason, state) =>
            {
                var size = Interlocked.Decrement(ref this.currentCacheSize);
                this.metrics.RecordDeduplicationCacheSize(size);
                this.metrics.RecordDeduplicationCacheOperation("evict", hit: false);

                if (reason != EvictionReason.Removed && reason != EvictionReason.Replaced)
                {
                    this.logger.LogDebug(
                        "Reservation {ReservationId} evicted from cache, reason: {Reason}, remaining entries: {Size}",
                        key,
                        reason,
                        size);
                }
            });

        this.reservationCache.Set(reservationId, reservation, cacheEntryOptions);

        var newSize = Interlocked.Increment(ref this.currentCacheSize);
        this.metrics.RecordDeduplicationCacheSize(newSize);
        this.metrics.RecordDeduplicationCacheOperation("add", hit: true);
    }

    /// <summary>
    /// MEMORY MANAGEMENT: Retrieves a reservation from the cache.
    /// Returns false if not found (cache miss).
    /// </summary>
    private bool TryGetReservationFromCache(string reservationId, out ReservationState? reservation)
    {
        return this.reservationCache.TryGetValue(reservationId, out reservation);
    }

    /// <summary>
    /// TOCTOU RACE CONDITION FIX: Checks if a completed reservation exists in cache for this state.
    /// This prevents the race where a reservation is completed (cache updated, DB cleared) between
    /// our cache check and DB query. We need to search by stateId, not reservationId.
    ///
    /// Uses a secondary index (stateToReservationIndex) to quickly find the most recent reservation
    /// for a given stateId without scanning the entire cache.
    /// </summary>
    private bool TryGetCompletedReservationFromCache(string stateId, out ReservationState? reservation)
    {
        reservation = null;

        // PERFORMANCE OPTIMIZATION: Use secondary index to look up reservation by stateId
        // This is O(1) instead of O(n) cache scan
        if (!this.stateToReservationIndex.TryGetValue(stateId, out var reservationId))
        {
            // No reservation found for this stateId
            return false;
        }

        // Look up the reservation in the cache
        if (!this.TryGetReservationFromCache(reservationId, out reservation))
        {
            // Reservation was evicted from cache, clean up the index
            this.stateToReservationIndex.TryRemove(stateId, out _);
            return false;
        }

        // Only return true if the reservation is completed
        // This catches the race where:
        // 1. ShouldSendAlert creates reservation and adds to cache
        // 2. RecordAlert completes reservation (sets Completed=true, clears DB reservation)
        // 3. Another ShouldSendAlert checks DB (sees no reservation) before cache check
        // By checking Completed flag, we prevent the second alert from proceeding
        if (reservation!.Completed)
        {
            this.metrics.RecordDeduplicationCacheOperation("get_completed", hit: true);
            return true;
        }

        return false;
    }

    /// <summary>
    /// MEMORY MANAGEMENT: Removes a reservation from the cache.
    /// This triggers the eviction callback to update metrics.
    /// </summary>
    private void RemoveReservationFromCache(string reservationId)
    {
        this.reservationCache.Remove(reservationId);
        this.metrics.RecordDeduplicationCacheOperation("remove", hit: true);
    }

    /// <summary>
    /// In-memory reservation state tracking.
    /// Stores metadata about active reservations for deduplication.
    /// </summary>
    private sealed class ReservationState
    {
        public required string StateId { get; init; }

        public required string Fingerprint { get; init; }

        public required string Severity { get; init; }

        public required DateTimeOffset ExpiresAt { get; init; }

        public bool Completed { get; set; }
    }
}
