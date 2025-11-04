-- Migration: Optimized Composite Indexes for Temporal Queries in STAC
-- Description: Adds specialized indexes for temporal range queries using computed columns
-- Performance Target: 10x faster temporal queries
-- Database: MySQL/MariaDB

-- Drop the existing generic datetime index
DROP INDEX IF EXISTS idx_stac_items_datetime ON stac_items;

-- MySQL doesn't support function-based indexes or computed columns in the same way as PostgreSQL
-- However, MySQL 5.7+ supports generated columns which can be indexed

-- Add generated columns for temporal expressions (if they don't exist)
-- These are virtual columns that are computed on the fly but can still be indexed

-- Check if columns exist and add them (MySQL doesn't have IF NOT EXISTS for ALTER TABLE columns)
-- We use a procedure to handle this safely

DELIMITER //

CREATE PROCEDURE add_temporal_columns()
BEGIN
    -- Add computed_start_datetime if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
        AND TABLE_NAME = 'stac_items'
        AND COLUMN_NAME = 'computed_start_datetime'
    ) THEN
        ALTER TABLE stac_items
        ADD COLUMN computed_start_datetime DATETIME(6) GENERATED ALWAYS AS (COALESCE(start_datetime, datetime)) STORED;
    END IF;

    -- Add computed_end_datetime if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
        AND TABLE_NAME = 'stac_items'
        AND COLUMN_NAME = 'computed_end_datetime'
    ) THEN
        ALTER TABLE stac_items
        ADD COLUMN computed_end_datetime DATETIME(6) GENERATED ALWAYS AS (COALESCE(end_datetime, datetime)) STORED;
    END IF;
END//

DELIMITER ;

CALL add_temporal_columns();
DROP PROCEDURE add_temporal_columns;

-- Index 1: Optimized for temporal start queries
-- Supports queries filtering by end time
CREATE INDEX IF NOT EXISTS idx_stac_items_temporal_start
ON stac_items(collection_id, computed_start_datetime);

-- Index 2: Optimized for temporal end queries
-- Supports queries filtering by start time
CREATE INDEX IF NOT EXISTS idx_stac_items_temporal_end
ON stac_items(collection_id, computed_end_datetime);

-- Index 3: Combined temporal range index for full overlap queries
-- Includes id to enable covering index for common query patterns
CREATE INDEX IF NOT EXISTS idx_stac_items_temporal_range
ON stac_items(collection_id, computed_start_datetime, computed_end_datetime, id);

-- Index 4: Point-in-time queries (for items with only datetime, not ranges)
-- Note: MySQL doesn't support partial indexes with WHERE clauses like PostgreSQL
-- So we create a regular index, which is less efficient but still helps
CREATE INDEX IF NOT EXISTS idx_stac_items_datetime_point
ON stac_items(collection_id, datetime);

-- Index 5: Range-only queries (for items with start/end but no datetime)
CREATE INDEX IF NOT EXISTS idx_stac_items_datetime_range
ON stac_items(collection_id, start_datetime, end_datetime);

-- Analyze table to update statistics for query optimizer
ANALYZE TABLE stac_items;

-- Performance notes:
-- 1. computed_start_datetime and computed_end_datetime are STORED generated columns
--    that materialize COALESCE(start_datetime, datetime) at insert/update time
-- 2. idx_stac_items_temporal_start: Best for queries filtering by end date
-- 3. idx_stac_items_temporal_end: Best for queries filtering by start date
-- 4. idx_stac_items_temporal_range: Best for queries with both start AND end filters
-- 5. idx_stac_items_datetime_point: Fast lookups for point-in-time items
-- 6. idx_stac_items_datetime_range: Fast lookups for temporal range items
-- 7. Query optimizer will automatically choose the most efficient index based on query pattern
-- 8. MySQL doesn't support filtered indexes, so indexes 4 and 5 are less efficient than PostgreSQL
