-- ============================================================================
-- PostgreSQL: Add Optimistic Locking Support
-- ============================================================================
-- This script adds row_version columns and triggers for optimistic concurrency control
-- Apply this to your feature tables for version tracking
-- ============================================================================

-- Create the reusable version increment function (idempotent)
CREATE OR REPLACE FUNCTION honua_increment_row_version()
RETURNS TRIGGER AS $$
BEGIN
    -- Increment version on update
    NEW.row_version := OLD.row_version + 1;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION honua_increment_row_version() IS
'Automatically increments row_version column on UPDATE operations for optimistic locking';

-- ============================================================================
-- Example: Apply to a feature table
-- ============================================================================
-- Replace 'your_schema.your_table' with your actual table names

-- Step 1: Add row_version column if it doesn't exist
-- DO $$
-- BEGIN
--     IF NOT EXISTS (
--         SELECT 1 FROM information_schema.columns
--         WHERE table_schema = 'your_schema'
--         AND table_name = 'your_table'
--         AND column_name = 'row_version'
--     ) THEN
--         ALTER TABLE your_schema.your_table
--         ADD COLUMN row_version BIGINT NOT NULL DEFAULT 1;
--
--         COMMENT ON COLUMN your_schema.your_table.row_version IS
--         'Version number for optimistic concurrency control - incremented on each update';
--     END IF;
-- END $$;

-- Step 2: Create trigger to auto-increment version
-- DROP TRIGGER IF EXISTS trg_your_table_version ON your_schema.your_table;
-- CREATE TRIGGER trg_your_table_version
--     BEFORE UPDATE ON your_schema.your_table
--     FOR EACH ROW
--     EXECUTE FUNCTION honua_increment_row_version();

-- Step 3: Create index on row_version for efficient lookups
-- CREATE INDEX IF NOT EXISTS idx_your_table_row_version
--     ON your_schema.your_table(row_version);

-- ============================================================================
-- PostgreSQL-specific: Using xmin for existing tables (alternative approach)
-- ============================================================================
-- PostgreSQL has a system column 'xmin' that tracks transaction IDs
-- You can use xmin as a version without adding a column:
--
-- Advantages:
-- - No schema changes required
-- - Automatically maintained by PostgreSQL
-- - Works with all existing tables
--
-- Disadvantages:
-- - xmin wraps around (32-bit counter)
-- - Not portable to other databases
-- - Can be confusing in multi-statement transactions
--
-- To use xmin, simply SELECT it: SELECT *, xmin FROM your_table WHERE id = $1
-- Then check on UPDATE: UPDATE your_table SET ... WHERE id = $1 AND xmin = $2

-- ============================================================================
-- Verification Query
-- ============================================================================
-- Check which tables have row_version columns:
-- SELECT
--     schemaname,
--     tablename,
--     column_name,
--     data_type
-- FROM information_schema.columns
-- WHERE column_name = 'row_version'
--     AND table_schema NOT IN ('pg_catalog', 'information_schema')
-- ORDER BY schemaname, tablename;
