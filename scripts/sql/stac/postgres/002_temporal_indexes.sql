-- Migration: Optimized Composite Indexes for Temporal Queries in STAC
-- Description: Adds specialized indexes for temporal range queries using COALESCE expressions
-- Performance Target: 10x faster temporal queries
-- Database: PostgreSQL

-- Drop the existing generic datetime index
DROP INDEX IF EXISTS idx_stac_items_datetime;

-- Index 1: Optimized for start_datetime-based queries (ascending temporal order)
-- Supports queries filtering by end time: WHERE COALESCE(start_datetime, datetime) <= @end
CREATE INDEX IF NOT EXISTS idx_stac_items_temporal_start
ON stac_items(collection_id, COALESCE(start_datetime, datetime));

-- Index 2: Optimized for end_datetime-based queries (descending temporal order)
-- Supports queries filtering by start time: WHERE COALESCE(end_datetime, datetime) >= @start
CREATE INDEX IF NOT EXISTS idx_stac_items_temporal_end
ON stac_items(collection_id, COALESCE(end_datetime, datetime));

-- Index 3: Combined temporal range index for full overlap queries
-- Supports queries with both start and end filters for temporal intersection
-- Includes id to enable index-only scans for common query patterns
CREATE INDEX IF NOT EXISTS idx_stac_items_temporal_range
ON stac_items(
    collection_id,
    COALESCE(start_datetime, datetime),
    COALESCE(end_datetime, datetime),
    id
);

-- Index 4: Point-in-time queries (for items with only datetime, not ranges)
-- Optimized for exact datetime lookups
CREATE INDEX IF NOT EXISTS idx_stac_items_datetime_point
ON stac_items(collection_id, datetime)
WHERE datetime IS NOT NULL AND start_datetime IS NULL AND end_datetime IS NULL;

-- Index 5: Range-only queries (for items with start/end but no datetime)
-- Optimized for temporal range items
CREATE INDEX IF NOT EXISTS idx_stac_items_datetime_range
ON stac_items(collection_id, start_datetime, end_datetime)
WHERE start_datetime IS NOT NULL AND end_datetime IS NOT NULL;

-- Create statistics to help query planner
ANALYZE stac_items;

-- Performance notes:
-- 1. idx_stac_items_temporal_start: Best for queries filtering by end date
-- 2. idx_stac_items_temporal_end: Best for queries filtering by start date
-- 3. idx_stac_items_temporal_range: Best for queries with both start AND end filters
-- 4. idx_stac_items_datetime_point: Fast lookups for point-in-time items
-- 5. idx_stac_items_datetime_range: Fast lookups for temporal range items
--
-- Query planner will automatically choose the most efficient index based on the query pattern
