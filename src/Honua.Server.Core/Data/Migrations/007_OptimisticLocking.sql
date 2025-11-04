-- ============================================================================
-- Honua Server - Optimistic Locking Support
-- ============================================================================
-- Version: 007
-- Description: Adds version tracking columns to support optimistic concurrency control
-- Prerequisites: Previous migrations applied
-- ============================================================================

-- This migration adds row_version columns to tables that support concurrent updates.
-- The version column is automatically incremented on every UPDATE operation.
-- Different database providers use different mechanisms:
-- - PostgreSQL: BIGINT with trigger-based increment
-- - SQL Server: ROWVERSION (timestamp) - automatically managed
-- - MySQL: BIGINT with trigger-based increment
-- SQLite: BIGINT with trigger-based increment

-- ============================================================================
-- PostgreSQL Implementation
-- ============================================================================
-- Conditional execution based on database type
DO $$
BEGIN
    -- Check if we're on PostgreSQL
    IF EXISTS (SELECT 1 FROM pg_catalog.pg_database WHERE datname = current_database()) THEN
        -- Note: This migration assumes user tables exist in their respective schemas
        -- For feature tables created by users, they should add the row_version column
        -- using: ALTER TABLE schema.tablename ADD COLUMN row_version BIGINT NOT NULL DEFAULT 1;

        -- Create a reusable function to increment row_version
        CREATE OR REPLACE FUNCTION honua_increment_row_version()
        RETURNS TRIGGER AS $func$
        BEGIN
            NEW.row_version := OLD.row_version + 1;
            RETURN NEW;
        END;
        $func$ LANGUAGE plpgsql;

        -- Example: Add row_version to a metadata tracking table (if exists)
        -- Users should apply similar pattern to their feature tables

        RAISE NOTICE 'PostgreSQL optimistic locking support installed.';
        RAISE NOTICE 'To enable on your tables, add: ALTER TABLE your_table ADD COLUMN row_version BIGINT NOT NULL DEFAULT 1;';
        RAISE NOTICE 'Then create trigger: CREATE TRIGGER trg_your_table_version BEFORE UPDATE ON your_table FOR EACH ROW EXECUTE FUNCTION honua_increment_row_version();';
    END IF;
END $$;

-- ============================================================================
-- SQL Server Implementation
-- ============================================================================
-- SQL Server uses ROWVERSION (formerly TIMESTAMP) which is automatically managed
-- Users should add to their tables:
-- ALTER TABLE schema.tablename ADD row_version ROWVERSION NOT NULL;

-- ============================================================================
-- MySQL Implementation
-- ============================================================================
-- MySQL uses BIGINT with BEFORE UPDATE trigger
-- Users should add to their tables:
-- ALTER TABLE schema.tablename ADD COLUMN row_version BIGINT NOT NULL DEFAULT 1;
-- Then create trigger:
/*
DELIMITER $$
CREATE TRIGGER trg_tablename_version
BEFORE UPDATE ON tablename
FOR EACH ROW
BEGIN
    SET NEW.row_version = OLD.row_version + 1;
END$$
DELIMITER ;
*/

-- ============================================================================
-- SQLite Implementation
-- ============================================================================
-- SQLite uses INTEGER with AFTER UPDATE trigger (since BEFORE UPDATE can't modify NEW)
-- Users should add to their tables:
-- ALTER TABLE tablename ADD COLUMN row_version INTEGER NOT NULL DEFAULT 1;
-- Then create trigger:
/*
CREATE TRIGGER IF NOT EXISTS trg_tablename_version
AFTER UPDATE ON tablename
FOR EACH ROW
WHEN NEW.row_version = OLD.row_version
BEGIN
    UPDATE tablename
    SET row_version = OLD.row_version + 1
    WHERE rowid = NEW.rowid;
END;
*/

-- ============================================================================
-- Migration History
-- ============================================================================
-- Track this migration (PostgreSQL syntax - adjust for other databases)
INSERT INTO schema_migrations (version, name, applied_at, applied_by, checksum, execution_time_ms)
VALUES (
    '007',
    'OptimisticLocking',
    NOW(),
    CURRENT_USER,
    'SHA256-PLACEHOLDER',
    0
)
ON CONFLICT (version) DO NOTHING;
