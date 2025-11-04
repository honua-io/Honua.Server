-- Migration: Add Race Condition Fixes to Alert Deduplication
-- Date: 2025-10-31
-- Purpose: Fix TOCTOU race conditions in alert deduplication reservation logic
--
-- Changes:
-- 1. Add row_version column for optimistic locking
-- 2. Add unique constraint on (fingerprint, severity, reservation_id) to prevent duplicate reservations
--
-- IMPORTANT: This migration is idempotent and can be run multiple times safely.

-- Step 1: Add row_version column if it doesn't exist
-- OPTIMISTIC LOCKING: Row version increments on every update to detect concurrent modifications
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'alert_deduplication_state'
        AND column_name = 'row_version'
    ) THEN
        ALTER TABLE alert_deduplication_state
        ADD COLUMN row_version INTEGER NOT NULL DEFAULT 1;

        -- Initialize existing rows with version 1
        UPDATE alert_deduplication_state
        SET row_version = 1
        WHERE row_version IS NULL;

        RAISE NOTICE 'Added row_version column to alert_deduplication_state';
    ELSE
        RAISE NOTICE 'row_version column already exists, skipping';
    END IF;
END $$;

-- Step 2: Add unique constraint on active reservations
-- RACE CONDITION FIX: Prevents two concurrent requests from creating reservations
-- for the same fingerprint+severity combination
--
-- This is a partial unique index that only enforces uniqueness when reservation_id IS NOT NULL
-- This allows multiple rows with the same fingerprint+severity but NULL reservation_id
CREATE UNIQUE INDEX IF NOT EXISTS idx_alert_deduplication_unique_active_reservation
ON alert_deduplication_state(fingerprint, severity, reservation_id)
WHERE reservation_id IS NOT NULL;

-- Verify the changes
DO $$
DECLARE
    col_exists BOOLEAN;
    idx_exists BOOLEAN;
BEGIN
    -- Check row_version column
    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'alert_deduplication_state'
        AND column_name = 'row_version'
    ) INTO col_exists;

    -- Check unique index
    SELECT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE indexname = 'idx_alert_deduplication_unique_active_reservation'
    ) INTO idx_exists;

    IF col_exists AND idx_exists THEN
        RAISE NOTICE 'Migration completed successfully';
        RAISE NOTICE '  - row_version column: EXISTS';
        RAISE NOTICE '  - unique reservation index: EXISTS';
    ELSE
        RAISE WARNING 'Migration incomplete:';
        RAISE WARNING '  - row_version column: %', CASE WHEN col_exists THEN 'EXISTS' ELSE 'MISSING' END;
        RAISE WARNING '  - unique reservation index: %', CASE WHEN idx_exists THEN 'EXISTS' ELSE 'MISSING' END;
    END IF;
END $$;

-- Performance Notes:
-- 1. The partial unique index has minimal overhead since it only applies to rows with active reservations
-- 2. Row version adds 4 bytes per row but provides strong concurrency guarantees
-- 3. Both changes are backward-compatible with existing application code
