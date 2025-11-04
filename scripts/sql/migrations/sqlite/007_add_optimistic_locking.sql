-- ============================================================================
-- SQLite: Add Optimistic Locking Support
-- ============================================================================
-- This script adds row_version columns and triggers for optimistic concurrency control
-- Apply this to your feature tables for version tracking
--
-- Note: SQLite doesn't support BEFORE UPDATE triggers that modify NEW values
-- directly, so we use an AFTER UPDATE trigger pattern
-- ============================================================================

-- ============================================================================
-- Example: Apply to a feature table
-- ============================================================================
-- Replace 'your_table' with your actual table name

-- Step 1: Add row_version column to existing table
-- SQLite doesn't support ADD COLUMN IF NOT EXISTS, so check first
/*
-- Option 1: Add column (will fail if already exists)
ALTER TABLE your_table ADD COLUMN row_version INTEGER NOT NULL DEFAULT 1;

-- Option 2: Check before adding (using application logic)
-- PRAGMA table_info(your_table);
-- Check if row_version column exists in result
*/

-- Step 2: Create trigger to auto-increment version
-- This trigger only fires when row_version hasn't been explicitly updated
/*
CREATE TRIGGER IF NOT EXISTS trg_your_table_version
AFTER UPDATE ON your_table
FOR EACH ROW
WHEN NEW.row_version = OLD.row_version
BEGIN
    UPDATE your_table
    SET row_version = OLD.row_version + 1
    WHERE rowid = NEW.rowid;
END;
*/

-- Step 3: Create index on row_version for efficient lookups
/*
CREATE INDEX IF NOT EXISTS idx_your_table_row_version
ON your_table(row_version);
*/

-- ============================================================================
-- Complete Example: Create table with optimistic locking from scratch
-- ============================================================================
/*
-- Create table with row_version
CREATE TABLE IF NOT EXISTS features (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    geometry BLOB,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now')),
    row_version INTEGER NOT NULL DEFAULT 1
);

-- Create version increment trigger
CREATE TRIGGER IF NOT EXISTS trg_features_version
AFTER UPDATE ON features
FOR EACH ROW
WHEN NEW.row_version = OLD.row_version
BEGIN
    UPDATE features
    SET row_version = OLD.row_version + 1
    WHERE rowid = NEW.rowid;
END;

-- Create updated_at trigger
CREATE TRIGGER IF NOT EXISTS trg_features_updated_at
AFTER UPDATE ON features
FOR EACH ROW
BEGIN
    UPDATE features
    SET updated_at = datetime('now')
    WHERE rowid = NEW.rowid;
END;

-- Create indexes
CREATE INDEX IF NOT EXISTS idx_features_row_version ON features(row_version);
CREATE INDEX IF NOT EXISTS idx_features_name ON features(name);
*/

-- ============================================================================
-- Migration Script for Existing Tables
-- ============================================================================
-- SQLite doesn't support ADD COLUMN IF NOT EXISTS or ALTER COLUMN
-- So we need to recreate the table if row_version doesn't exist
--
-- This script template shows how to migrate an existing table:
/*
-- Check if migration needed
PRAGMA table_info(your_table);
-- If row_version not in output, run migration:

BEGIN TRANSACTION;

-- 1. Rename old table
ALTER TABLE your_table RENAME TO your_table_old;

-- 2. Create new table with row_version
CREATE TABLE your_table (
    id INTEGER PRIMARY KEY,
    -- Copy all your existing columns here
    name TEXT,
    geometry BLOB,
    -- ... other columns ...

    -- Add version tracking
    row_version INTEGER NOT NULL DEFAULT 1
);

-- 3. Copy data from old table
INSERT INTO your_table (id, name, geometry, ...)
SELECT id, name, geometry, ... FROM your_table_old;

-- 4. Create trigger
CREATE TRIGGER IF NOT EXISTS trg_your_table_version
AFTER UPDATE ON your_table
FOR EACH ROW
WHEN NEW.row_version = OLD.row_version
BEGIN
    UPDATE your_table
    SET row_version = OLD.row_version + 1
    WHERE rowid = NEW.rowid;
END;

-- 5. Create index
CREATE INDEX IF NOT EXISTS idx_your_table_row_version
ON your_table(row_version);

-- 6. Drop old table
DROP TABLE your_table_old;

COMMIT;
*/

-- ============================================================================
-- Usage Pattern for Applications
-- ============================================================================
-- 1. SELECT: Get current row_version value
--    SELECT id, name, ..., row_version FROM your_table WHERE id = ?
--
-- 2. UPDATE: Include row_version in WHERE clause
--    UPDATE your_table
--    SET name = ?, ...
--    WHERE id = ? AND row_version = ?
--
-- 3. CHECK: If changes() = 0, throw concurrency exception
--    SELECT changes(); -- Returns number of rows affected
--    -- If 0, concurrency conflict occurred

-- ============================================================================
-- Verification Queries
-- ============================================================================
-- Check table structure:
-- PRAGMA table_info(your_table);

-- Check triggers:
-- SELECT name, sql FROM sqlite_master WHERE type = 'trigger' AND tbl_name = 'your_table';

-- Check indexes:
-- SELECT name, sql FROM sqlite_master WHERE type = 'index' AND tbl_name = 'your_table';

-- Test version increment:
/*
-- Insert test record
INSERT INTO your_table (name) VALUES ('test');
SELECT row_version FROM your_table WHERE name = 'test'; -- Should be 1

-- Update and check version
UPDATE your_table SET name = 'test_updated' WHERE name = 'test';
SELECT row_version FROM your_table WHERE name = 'test_updated'; -- Should be 2
*/
