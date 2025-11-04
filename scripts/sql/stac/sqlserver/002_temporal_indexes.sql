-- Migration: Optimized Composite Indexes for Temporal Queries in STAC
-- Description: Adds specialized indexes for temporal range queries using COALESCE expressions
-- Performance Target: 10x faster temporal queries
-- Database: SQL Server

-- Drop the existing generic datetime index
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_stac_items_datetime' AND object_id = OBJECT_ID('stac_items'))
BEGIN
    DROP INDEX idx_stac_items_datetime ON stac_items;
END;
GO

-- Index 1: Optimized for start_datetime-based queries (ascending temporal order)
-- Supports queries filtering by end time: WHERE COALESCE(start_datetime, datetime) <= @end
-- Note: SQL Server doesn't support function-based indexes directly, but computed columns can be indexed
-- We'll use a workaround with persisted computed columns for best performance

-- Add computed columns for temporal expressions (if they don't exist)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('stac_items') AND name = 'computed_start_datetime')
BEGIN
    ALTER TABLE stac_items
    ADD computed_start_datetime AS COALESCE(start_datetime, datetime) PERSISTED;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('stac_items') AND name = 'computed_end_datetime')
BEGIN
    ALTER TABLE stac_items
    ADD computed_end_datetime AS COALESCE(end_datetime, datetime) PERSISTED;
END;
GO

-- Index 1: Optimized for temporal start queries
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_stac_items_temporal_start' AND object_id = OBJECT_ID('stac_items'))
BEGIN
    CREATE INDEX idx_stac_items_temporal_start
    ON stac_items(collection_id, computed_start_datetime);
END;
GO

-- Index 2: Optimized for temporal end queries
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_stac_items_temporal_end' AND object_id = OBJECT_ID('stac_items'))
BEGIN
    CREATE INDEX idx_stac_items_temporal_end
    ON stac_items(collection_id, computed_end_datetime);
END;
GO

-- Index 3: Combined temporal range index for full overlap queries
-- Includes id to enable index-only scans for common query patterns
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_stac_items_temporal_range' AND object_id = OBJECT_ID('stac_items'))
BEGIN
    CREATE INDEX idx_stac_items_temporal_range
    ON stac_items(collection_id, computed_start_datetime, computed_end_datetime)
    INCLUDE (id);
END;
GO

-- Index 4: Point-in-time queries (for items with only datetime, not ranges)
-- Optimized for exact datetime lookups
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_stac_items_datetime_point' AND object_id = OBJECT_ID('stac_items'))
BEGIN
    CREATE INDEX idx_stac_items_datetime_point
    ON stac_items(collection_id, datetime)
    WHERE datetime IS NOT NULL AND start_datetime IS NULL AND end_datetime IS NULL;
END;
GO

-- Index 5: Range-only queries (for items with start/end but no datetime)
-- Optimized for temporal range items
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_stac_items_datetime_range' AND object_id = OBJECT_ID('stac_items'))
BEGIN
    CREATE INDEX idx_stac_items_datetime_range
    ON stac_items(collection_id, start_datetime, end_datetime)
    WHERE start_datetime IS NOT NULL AND end_datetime IS NOT NULL;
END;
GO

-- Update statistics to help query optimizer
UPDATE STATISTICS stac_items;
GO

-- Performance notes:
-- 1. computed_start_datetime and computed_end_datetime are persisted computed columns
--    that materialize COALESCE(start_datetime, datetime) at insert/update time
-- 2. idx_stac_items_temporal_start: Best for queries filtering by end date
-- 3. idx_stac_items_temporal_end: Best for queries filtering by start date
-- 4. idx_stac_items_temporal_range: Best for queries with both start AND end filters
-- 5. idx_stac_items_datetime_point: Fast lookups for point-in-time items
-- 6. idx_stac_items_datetime_range: Fast lookups for temporal range items
-- 7. Query optimizer will automatically choose the most efficient index based on query pattern
