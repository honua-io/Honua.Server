// <copyright file="SqlAlertDeduplicator.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using System.Data;
using Dapper;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Honua.Server.AlertReceiver.Services;

public interface IAlertReceiverDbConnectionFactory
{
    IDbConnection CreateConnection();
}

public sealed class NpgsqlAlertReceiverDbConnectionFactory : IAlertReceiverDbConnectionFactory
{
    private readonly string connectionString;

    public NpgsqlAlertReceiverDbConnectionFactory(string connectionString)
    {
        this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public IDbConnection CreateConnection()
    {
        var connection = new NpgsqlConnection(this.connectionString);
        return connection;
    }
}

/// <summary>
/// Configuration options for alert deduplication reservation cache.
/// </summary>
public sealed class AlertDeduplicationCacheOptions
{
    public const string SectionName = "Alerts:Deduplication:Cache";

    /// <summary>
    /// Maximum number of reservation entries to keep in memory.
    /// Default: 10000 entries (sufficient for high-volume deployments).
    /// </summary>
    public int MaxEntries { get; set; } = 10000;

    /// <summary>
    /// Sliding expiration for reservation cache entries (in seconds).
    /// Default: 60 seconds (2x the reservation timeout for safety margin).
    /// </summary>
    public int SlidingExpirationSeconds { get; set; } = 60;

    /// <summary>
    /// Absolute expiration for reservation cache entries (in seconds).
    /// Default: 300 seconds (5 minutes max lifetime).
    /// </summary>
    public int AbsoluteExpirationSeconds { get; set; } = 300;
}

/// <summary>
/// Deduplicates alerts by persisting fingerprint/severity metadata in the alert history database.
/// Uses Dapper to keep dependencies lightweight.
/// RACE CONDITION FIX: Uses PostgreSQL advisory locks to ensure atomic check-and-reserve operations.
/// MEMORY LEAK FIX: Replaced unbounded static ConcurrentDictionary with MemoryCache with size limits and TTL.
/// </summary>
public sealed partial class SqlAlertDeduplicator : IAlertDeduplicator
{
    private const int MaxTimestampHistory = 100;
    private const int ReservationTimeoutSeconds = 30;

    private readonly IAlertReceiverDbConnectionFactory connectionFactory;
    private readonly IConfiguration configuration;
    private readonly ILogger<SqlAlertDeduplicator> logger;
    private readonly IAlertMetricsService metrics;
    private readonly IMemoryCache reservationCache;
    private readonly AlertDeduplicationCacheOptions cacheOptions;

    private static readonly object SchemaLock = new();
    private static volatile bool schemaInitialized;

    // MEMORY MANAGEMENT: Track cache size for monitoring
    // This is thread-safe because Interlocked operations are atomic
    private int currentCacheSize;

    // TOCTOU RACE CONDITION FIX: Secondary index to track active reservations by stateId
    // Allows quick lookup of completed reservations without scanning entire cache
    // Key: stateId (fingerprint:severity), Value: most recent reservationId for this state
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> stateToReservationIndex = new();

    public SqlAlertDeduplicator(
        IAlertReceiverDbConnectionFactory connectionFactory,
        IConfiguration configuration,
        ILogger<SqlAlertDeduplicator> logger,
        IAlertMetricsService metrics,
        IMemoryCache reservationCache,
        IOptions<AlertDeduplicationCacheOptions> cacheOptions)
    {
        this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        this.reservationCache = reservationCache ?? throw new ArgumentNullException(nameof(reservationCache));
        this.cacheOptions = cacheOptions?.Value ?? throw new ArgumentNullException(nameof(cacheOptions));

        this.logger.LogInformation(
            "Alert deduplication cache initialized with MaxEntries={MaxEntries}, " +
            "SlidingExpiration={SlidingExpiration}s, AbsoluteExpiration={AbsoluteExpiration}s",
            this.cacheOptions.MaxEntries,
            this.cacheOptions.SlidingExpirationSeconds,
            this.cacheOptions.AbsoluteExpirationSeconds);
    }

    public async Task<(bool shouldSend, string reservationId)> ShouldSendAlertAsync(
        string fingerprint,
        string severity,
        CancellationToken cancellationToken = default)
    {
        var stateId = BuildKey(fingerprint, severity);
        var reservationId = GenerateReservationId();

        // TOCTOU RACE CONDITION FIX: Check in-memory cache FIRST before database
        // This prevents the race where:
        // 1. Thread A checks DB (no active reservation)
        // 2. Thread B completes reservation (updates cache, clears DB reservation)
        // 3. Thread A creates new reservation (bypasses deduplication)
        // By checking the cache first, we catch recently-completed reservations that may have
        // cleared their DB reservation but are still within the deduplication window.
        if (this.TryGetCompletedReservationFromCache(stateId, out var cachedReservation))
        {
            this.logger.LogDebug(
                "Alert suppressed due to recently completed reservation in cache: {Fingerprint} (severity {Severity}), " +
                "completed {Elapsed:0.00}s ago.",
                fingerprint,
                severity,
                (DateTimeOffset.UtcNow - cachedReservation!.ExpiresAt).TotalSeconds);

            this.metrics.RecordAlertSuppressed("completed_reservation_cache", severity);
            this.metrics.RecordRaceConditionPrevented("toctou_cache_check");
            return (false, reservationId);
        }

        // RESOURCE LEAK FIX: Ensure connection is properly disposed in all error paths
        using var connection = this.connectionFactory.CreateConnection();
        IDbTransaction? transaction = null;

        try
        {
            // ASYNC FIX: Use OpenAsync for non-blocking I/O
            if (connection is NpgsqlConnection npgsqlConnection)
            {
                await npgsqlConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                connection.Open();
            }

            await this.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

            // RACE CONDITION FIX: Use advisory lock to serialize access per fingerprint+severity
            // Convert state ID to a 64-bit hash for pg_advisory_xact_lock
            var lockKey = ComputeLockKey(stateId);

            transaction = connection.BeginTransaction(IsolationLevel.Serializable);

            // Acquire advisory lock - automatically released on transaction commit/rollback
            await connection.ExecuteAsync("SELECT pg_advisory_xact_lock(@LockKey)", new { LockKey = lockKey }, transaction).ConfigureAwait(false);

            var state = await connection.QuerySingleOrDefaultAsync<AlertDeduplicationRecord>(SelectStateSql, new { Id = stateId }, transaction).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow;

            // Check if another request already has a valid reservation
            if (state?.ReservationId != null &&
                state.ReservationExpiresAt.HasValue &&
                state.ReservationExpiresAt.Value > now)
            {
                // DOUBLE-CHECK: Verify this reservation isn't already completed in cache
                // This catches the narrow race where a reservation completed between our cache check and DB query
                if (this.TryGetReservationFromCache(state.ReservationId, out var existingReservation) &&
                    existingReservation!.Completed)
                {
                    this.logger.LogWarning(
                        "RACE CONDITION DETECTED: Database shows active reservation {ReservationId} but cache shows it's completed. " +
                        "Allowing alert to proceed to prevent suppression.",
                        state.ReservationId);

                    this.metrics.RecordRaceConditionPrevented("completed_reservation_mismatch");

                    // Clear the stale reservation and proceed
                    await connection.ExecuteAsync(
                        @"
UPDATE alert_deduplication_state
SET reservation_id = NULL,
    reservation_expires_at = NULL,
    updated_at = @UpdatedAt
WHERE id = @Id",
                        new { Id = state.Id, UpdatedAt = now },
                        transaction).ConfigureAwait(false);
                }
                else
                {
                    // OPTIMISTIC LOCKING: Include row version in update
                    var suppressRowsAffected = await connection.ExecuteAsync(
                        UpdateSuppressedSql,
                        new { Id = state.Id, UpdatedAt = now, RowVersion = state.RowVersion },
                        transaction).ConfigureAwait(false);

                    if (suppressRowsAffected == 0)
                    {
                        // Concurrent modification detected - another process updated this row
                        this.logger.LogWarning(
                            "RACE CONDITION DETECTED: Optimistic locking failure when suppressing alert for {Fingerprint} (severity {Severity}). " +
                            "Another process modified the row.",
                            fingerprint,
                            severity);

                        this.metrics.RecordRaceConditionPrevented("optimistic_lock_failure_suppress");
                        transaction.Rollback();
                        return (false, reservationId);
                    }

                    transaction.Commit();

                    this.logger.LogDebug(
                        "Alert suppressed due to active reservation: {Fingerprint} (severity {Severity}), reservation expires in {Seconds:0.00}s.",
                        fingerprint,
                        severity,
                        (state.ReservationExpiresAt.Value - now).TotalSeconds);

                    this.metrics.RecordAlertSuppressed("active_reservation", severity);
                    return (false, reservationId);
                }
            }

            if (state is null)
            {
                state = new AlertDeduplicationRecord
                {
                    Id = stateId,
                    Fingerprint = fingerprint,
                    Severity = severity,
                    FirstSeen = now,
                    LastSent = DateTimeOffset.MinValue,
                    SentCount = 0,
                    SuppressedCount = 0,
                    SentTimestampsJson = "[]",
                    UpdatedAt = now,
                    ReservationId = null,
                    ReservationExpiresAt = null,
                };

                await connection.ExecuteAsync(InsertStateSql, state, transaction).ConfigureAwait(false);
            }

            var dedupWindow = this.GetDeduplicationWindow(severity);
            if (state.LastSent != DateTimeOffset.MinValue &&
                now - state.LastSent < dedupWindow)
            {
                // OPTIMISTIC LOCKING: Include row version in update
                var dedupRowsAffected = await connection.ExecuteAsync(
                    UpdateSuppressedSql,
                    new { Id = state.Id, UpdatedAt = now, RowVersion = state.RowVersion },
                    transaction).ConfigureAwait(false);

                if (dedupRowsAffected == 0)
                {
                    this.logger.LogWarning(
                        "RACE CONDITION DETECTED: Optimistic locking failure in deduplication window check for {Fingerprint} (severity {Severity}).",
                        fingerprint,
                        severity);

                    this.metrics.RecordRaceConditionPrevented("optimistic_lock_failure_dedup_window");
                    transaction.Rollback();
                    return (false, reservationId);
                }

                transaction.Commit();

                this.logger.LogDebug(
                    "Alert suppressed by deduplication window: {Fingerprint} (severity {Severity}) last sent {Elapsed:0.00}s ago.",
                    fingerprint,
                    severity,
                    (now - state.LastSent).TotalSeconds);

                this.metrics.RecordAlertSuppressed("deduplication_window", severity);
                return (false, reservationId);
            }

            var timestamps = DeserializeTimestamps(state.SentTimestampsJson);
            var recentCount = timestamps.Count(ts => ts > now.AddHours(-1));
            var hourlyLimit = this.GetRateLimit(severity);

            if (recentCount >= hourlyLimit)
            {
                // OPTIMISTIC LOCKING: Include row version in update
                var rateLimitRowsAffected = await connection.ExecuteAsync(
                    UpdateSuppressedSql,
                    new { Id = state.Id, UpdatedAt = now, RowVersion = state.RowVersion },
                    transaction).ConfigureAwait(false);

                if (rateLimitRowsAffected == 0)
                {
                    this.logger.LogWarning(
                        "RACE CONDITION DETECTED: Optimistic locking failure in rate limit check for {Fingerprint} (severity {Severity}).",
                        fingerprint,
                        severity);

                    this.metrics.RecordRaceConditionPrevented("optimistic_lock_failure_rate_limit");
                    transaction.Rollback();
                    return (false, reservationId);
                }

                transaction.Commit();

                this.logger.LogWarning(
                    "Alert rate limited: {Fingerprint} (severity {Severity}) exceeded {Limit} alerts/hour.",
                    fingerprint,
                    severity,
                    hourlyLimit);

                this.metrics.RecordAlertSuppressed("rate_limit", severity);
                return (false, reservationId);
            }

            // RACE CONDITION FIX: Create a reservation that expires in 30 seconds
            // This prevents concurrent alerts from bypassing deduplication
            // OPTIMISTIC LOCKING: Include row version to detect concurrent modifications
            var reservationExpiry = now.AddSeconds(ReservationTimeoutSeconds);
            var rowsAffected = await connection.ExecuteAsync(
                @"
UPDATE alert_deduplication_state
SET reservation_id = @ReservationId,
    reservation_expires_at = @ReservationExpiresAt,
    updated_at = @UpdatedAt,
    row_version = row_version + 1
WHERE id = @Id AND row_version = @RowVersion",
                new
                {
                    Id = state.Id,
                    ReservationId = reservationId,
                    ReservationExpiresAt = reservationExpiry,
                    UpdatedAt = now,
                    RowVersion = state.RowVersion,
                },
                transaction).ConfigureAwait(false);

            if (rowsAffected == 0)
            {
                // Concurrent modification - another process updated this row
                this.logger.LogWarning(
                    "RACE CONDITION DETECTED: Optimistic locking failure when creating reservation for {Fingerprint} (severity {Severity}). " +
                    "Suppressing alert to prevent duplicate sends.",
                    fingerprint,
                    severity);

                this.metrics.RecordRaceConditionPrevented("optimistic_lock_failure_create_reservation");
                transaction.Rollback();
                return (false, reservationId);
            }

            transaction.Commit();

            // MEMORY LEAK FIX: Store reservation in bounded MemoryCache instead of unbounded dictionary
            // Cache automatically evicts old entries based on size limit and TTL
            this.AddReservationToCache(reservationId, new ReservationState
            {
                StateId = stateId,
                Fingerprint = fingerprint,
                Severity = severity,
                ExpiresAt = reservationExpiry,
                Completed = false,
            });

            // TOCTOU RACE CONDITION FIX: Add to secondary index for quick lookup by stateId
            this.stateToReservationIndex[stateId] = reservationId;

            return (true, reservationId);
        }
        catch
        {
            // RESOURCE LEAK FIX: Explicitly rollback transaction on error
            try
            {
                transaction?.Rollback();
            }
            catch (Exception rollbackEx)
            {
                this.logger.LogWarning(rollbackEx, "Failed to rollback transaction during exception handling");
            }

            // Connection will be disposed by using statement
            throw;
        }
    }

    public async Task RecordAlertAsync(
        string fingerprint,
        string severity,
        string reservationId,
        CancellationToken cancellationToken = default)
    {
        // RACE CONDITION FIX: Verify this is a valid reservation
        // MEMORY LEAK FIX: Use MemoryCache instead of static dictionary
        if (!this.TryGetReservationFromCache(reservationId, out var reservation))
        {
            this.logger.LogWarning(
                "Attempted to record alert with unknown reservation ID: {ReservationId}",
                reservationId);
            this.metrics.RecordDeduplicationCacheOperation("get", hit: false);
            return;
        }

        this.metrics.RecordDeduplicationCacheOperation("get", hit: true);

        // Check if already completed (idempotency)
        if (reservation!.Completed)
        {
            this.logger.LogDebug(
                "Reservation {ReservationId} already completed, skipping duplicate record",
                reservationId);
            return;
        }

        var stateId = BuildKey(fingerprint, severity);

        // RESOURCE LEAK FIX: Ensure connection is properly disposed in all error paths
        using var connection = this.connectionFactory.CreateConnection();
        IDbTransaction? transaction = null;

        try
        {
            // ASYNC FIX: Use OpenAsync for non-blocking I/O
            if (connection is NpgsqlConnection npgsqlConnection)
            {
                await npgsqlConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                connection.Open();
            }

            await this.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

            // RACE CONDITION FIX: Use same advisory lock as ShouldSendAlert
            var lockKey = ComputeLockKey(stateId);

            transaction = connection.BeginTransaction(IsolationLevel.Serializable);

            // Acquire advisory lock
            await connection.ExecuteAsync("SELECT pg_advisory_xact_lock(@LockKey)", new { LockKey = lockKey }, transaction).ConfigureAwait(false);

            var state = await connection.QuerySingleOrDefaultAsync<AlertDeduplicationRecord>(SelectStateSql, new { Id = stateId }, transaction).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow;

            if (state is null)
            {
                this.logger.LogWarning(
                    "Alert state not found for reservation {ReservationId}, fingerprint {Fingerprint}",
                    reservationId,
                    fingerprint);
                transaction.Rollback();
                return;
            }

            // RACE CONDITION FIX: Verify the reservation matches
            if (state.ReservationId != reservationId)
            {
                this.logger.LogWarning(
                    "Reservation ID mismatch: expected {Expected}, got {Actual}",
                    state.ReservationId,
                    reservationId);
                transaction.Rollback();
                return;
            }

            var timestamps = DeserializeTimestamps(state.SentTimestampsJson);
            timestamps.Add(now);

            if (timestamps.Count > MaxTimestampHistory)
            {
                timestamps.RemoveRange(0, timestamps.Count - MaxTimestampHistory);
            }

            var updatedPayload = SerializeTimestamps(timestamps);

            // RACE CONDITION FIX: Clear reservation and update sent timestamp atomically
            // OPTIMISTIC LOCKING: Include row version to detect concurrent modifications
            var rowsAffected = await connection.ExecuteAsync(
                @"
UPDATE alert_deduplication_state
SET last_sent = @LastSent,
    sent_count = sent_count + 1,
    sent_timestamps = @SentTimestampsJson,
    reservation_id = NULL,
    reservation_expires_at = NULL,
    updated_at = @UpdatedAt,
    row_version = row_version + 1
WHERE id = @Id AND row_version = @RowVersion",
                new
                {
                    Id = state.Id,
                    LastSent = now,
                    SentTimestampsJson = updatedPayload,
                    UpdatedAt = now,
                    RowVersion = state.RowVersion,
                },
                transaction).ConfigureAwait(false);

            if (rowsAffected == 0)
            {
                // Concurrent modification detected
                this.logger.LogWarning(
                    "RACE CONDITION DETECTED: Optimistic locking failure when recording alert for {Fingerprint} (severity {Severity}). " +
                    "Another process modified the row.",
                    fingerprint,
                    severity);

                this.metrics.RecordRaceConditionPrevented("optimistic_lock_failure_record_alert");
                transaction.Rollback();
                return;
            }

            transaction.Commit();

            // TOCTOU RACE CONDITION FIX: Mark reservation as completed BEFORE clearing from index
            // This ensures that TryGetCompletedReservationFromCache will find it
            reservation.Completed = true;

            // MEMORY LEAK FIX: Cache entry will be automatically removed by MemoryCache TTL
            // No manual cleanup needed - MemoryCache handles eviction automatically
            // Note: We keep the entry in stateToReservationIndex until cache eviction
            // This allows quick detection of recently-completed reservations
        }
        catch
        {
            // RESOURCE LEAK FIX: Explicitly rollback transaction on error
            try
            {
                transaction?.Rollback();
            }
            catch (Exception rollbackEx)
            {
                this.logger.LogWarning(rollbackEx, "Failed to rollback transaction during exception handling");
            }

            // Connection will be disposed by using statement
            throw;
        }
    }

    public async Task ReleaseReservationAsync(
        string reservationId,
        CancellationToken cancellationToken = default)
    {
        // RACE CONDITION FIX: Release reservation if alert publishing failed
        // MEMORY LEAK FIX: Use MemoryCache instead of static dictionary
        if (!this.TryGetReservationFromCache(reservationId, out var reservation))
        {
            this.metrics.RecordDeduplicationCacheOperation("get", hit: false);
            return;
        }

        this.metrics.RecordDeduplicationCacheOperation("get", hit: true);

        if (reservation!.Completed)
        {
            // Already recorded, nothing to release
            return;
        }

        var stateId = reservation.StateId;

        // RESOURCE LEAK FIX: Ensure connection is properly disposed in all error paths
        try
        {
            using var connection = this.connectionFactory.CreateConnection();
            IDbTransaction? transaction = null;

            try
            {
                // ASYNC FIX: Use OpenAsync for non-blocking I/O
                if (connection is NpgsqlConnection npgsqlConnection)
                {
                    await npgsqlConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    connection.Open();
                }

                var lockKey = ComputeLockKey(stateId);

                transaction = connection.BeginTransaction(IsolationLevel.Serializable);

                // Acquire advisory lock
                await connection.ExecuteAsync("SELECT pg_advisory_xact_lock(@LockKey)", new { LockKey = lockKey }, transaction).ConfigureAwait(false);

                // RACE CONDITION FIX: Clear the reservation in the database
                // Note: We don't need row_version here because we're checking reservation_id
                // which provides sufficient protection (reservation_id is unique per state)
                await connection.ExecuteAsync(
                    @"
UPDATE alert_deduplication_state
SET reservation_id = NULL,
    reservation_expires_at = NULL,
    updated_at = @UpdatedAt,
    row_version = row_version + 1
WHERE id = @Id AND reservation_id = @ReservationId",
                    new
                    {
                        Id = stateId,
                        ReservationId = reservationId,
                        UpdatedAt = DateTimeOffset.UtcNow,
                    },
                    transaction).ConfigureAwait(false);

                transaction.Commit();

                // MEMORY LEAK FIX: Remove from cache (will be evicted automatically, but remove now for consistency)
                this.RemoveReservationFromCache(reservationId);

                // TOCTOU RACE CONDITION FIX: Clean up secondary index
                this.stateToReservationIndex.TryRemove(stateId, out _);

                this.logger.LogDebug("Released reservation {ReservationId}", reservationId);
            }
            catch (Exception ex)
            {
                // RESOURCE LEAK FIX: Explicitly rollback transaction on error
                try
                {
                    transaction?.Rollback();
                }
                catch (Exception rollbackEx)
                {
                    this.logger.LogWarning(rollbackEx, "Failed to rollback transaction during exception handling");
                }

                this.logger.LogWarning(ex, "Failed to release reservation {ReservationId}", reservationId);
                // Connection will be disposed by using statement
            }
        }
        catch (Exception ex)
        {
            // RESOURCE LEAK FIX: Log factory-level errors
            this.logger.LogWarning(ex, "Failed to create connection for releasing reservation {ReservationId}", reservationId);
        }
    }
}
