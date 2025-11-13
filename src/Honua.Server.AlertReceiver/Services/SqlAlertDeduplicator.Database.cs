// <copyright file="SqlAlertDeduplicator.Database.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using System.Data;
using Dapper;
using Npgsql;

namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Database operations partial class for SqlAlertDeduplicator.
/// Contains schema initialization, SQL constants, and database-related methods.
/// </summary>
public sealed partial class SqlAlertDeduplicator
{
    private const string EnsureSchemaSql = @"
CREATE TABLE IF NOT EXISTS alert_deduplication_state (
    id TEXT PRIMARY KEY,
    fingerprint TEXT NOT NULL,
    severity TEXT NOT NULL,
    first_seen TIMESTAMPTZ NOT NULL,
    last_sent TIMESTAMPTZ NOT NULL,
    sent_count INTEGER NOT NULL,
    suppressed_count INTEGER NOT NULL,
    sent_timestamps TEXT NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    reservation_id TEXT NULL,
    reservation_expires_at TIMESTAMPTZ NULL,
    row_version INTEGER NOT NULL DEFAULT 1
);
CREATE INDEX IF NOT EXISTS idx_alert_deduplication_state_fingerprint_severity
ON alert_deduplication_state(fingerprint, severity);

-- RACE CONDITION FIX: Unique constraint to prevent duplicate active reservations
-- Partial index: only enforces uniqueness when reservation_id IS NOT NULL
-- This prevents two concurrent requests from creating reservations for the same fingerprint+severity
CREATE UNIQUE INDEX IF NOT EXISTS idx_alert_deduplication_unique_active_reservation
ON alert_deduplication_state(fingerprint, severity, reservation_id)
WHERE reservation_id IS NOT NULL;

-- MIGRATION: Add row_version column to existing tables if not present
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'alert_deduplication_state'
        AND column_name = 'row_version'
    ) THEN
        ALTER TABLE alert_deduplication_state
        ADD COLUMN row_version INTEGER NOT NULL DEFAULT 1;
    END IF;
END $$;
";

    private const string SelectStateSql = @"
SELECT id, fingerprint, severity, first_seen AS FirstSeen, last_sent AS LastSent,
       sent_count AS SentCount, suppressed_count AS SuppressedCount,
       sent_timestamps AS SentTimestampsJson, updated_at AS UpdatedAt,
       reservation_id AS ReservationId, reservation_expires_at AS ReservationExpiresAt,
       row_version AS RowVersion
FROM alert_deduplication_state
WHERE id = @Id
FOR UPDATE";

    private const string InsertStateSql = @"
INSERT INTO alert_deduplication_state
    (id, fingerprint, severity, first_seen, last_sent, sent_count, suppressed_count, sent_timestamps, updated_at, reservation_id, reservation_expires_at, row_version)
VALUES
    (@Id, @Fingerprint, @Severity, @FirstSeen, @LastSent, @SentCount, @SuppressedCount, @SentTimestampsJson, @UpdatedAt, @ReservationId, @ReservationExpiresAt, 1);";

    private const string UpdateSuppressedSql = @"
UPDATE alert_deduplication_state
SET suppressed_count = suppressed_count + 1,
    updated_at = @UpdatedAt,
    row_version = row_version + 1
WHERE id = @Id AND row_version = @RowVersion;";

    private const string UpdateSentSql = @"
UPDATE alert_deduplication_state
SET last_sent = @LastSent,
    sent_count = sent_count + 1,
    sent_timestamps = @SentTimestampsJson,
    updated_at = @UpdatedAt,
    row_version = row_version + 1
WHERE id = @Id AND row_version = @RowVersion;";

    /// <summary>
    /// Ensures the database schema is initialized using double-checked locking pattern.
    /// Thread-safe and only executes once per application lifetime.
    /// </summary>
    private Task EnsureSchemaAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        if (schemaInitialized)
        {
            return Task.CompletedTask;
        }

        lock (SchemaLock)
        {
            if (schemaInitialized)
            {
                return Task.CompletedTask;
            }

            // Execute synchronously within the lock to prevent race conditions
            connection.Execute(EnsureSchemaSql);

            schemaInitialized = true;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Database record class representing alert deduplication state.
    /// Maps to the alert_deduplication_state table.
    /// </summary>
    private sealed class AlertDeduplicationRecord
    {
        public string Id { get; set; } = string.Empty;

        public string Fingerprint { get; set; } = string.Empty;

        public string Severity { get; set; } = string.Empty;

        public DateTimeOffset FirstSeen { get; set; }

        public DateTimeOffset LastSent { get; set; }

        public int SentCount { get; set; }

        public int SuppressedCount { get; set; }

        public string SentTimestampsJson { get; set; } = "[]";

        public DateTimeOffset UpdatedAt { get; set; }

        public string? ReservationId { get; set; }

        public DateTimeOffset? ReservationExpiresAt { get; set; }

        /// <summary>
        /// OPTIMISTIC LOCKING: Row version for detecting concurrent modifications.
        /// Incremented on every update. Updates will fail if version doesn't match.
        /// </summary>
        public int RowVersion { get; set; }
    }
}
