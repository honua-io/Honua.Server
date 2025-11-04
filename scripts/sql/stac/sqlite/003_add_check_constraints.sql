-- Migration: Add CHECK Constraints to STAC Tables
-- Description: Recreates stac_items table with CHECK constraints for data validation
-- Database: SQLite
-- Date: 2025-10-18
-- Version: 1.0

-- Note: SQLite requires recreating tables to add CHECK constraints
-- This migration preserves all existing data

-- Step 1: Create new table with CHECK constraints
CREATE TABLE IF NOT EXISTS stac_items_new (
    collection_id TEXT NOT NULL,
    id TEXT NOT NULL,
    title TEXT,
    description TEXT,
    properties_json TEXT,
    assets_json TEXT NOT NULL,
    links_json TEXT NOT NULL,
    extensions_json TEXT NOT NULL,
    bbox_json TEXT,
    geometry_json TEXT,
    datetime TEXT,
    start_datetime TEXT,
    end_datetime TEXT,
    raster_dataset_id TEXT,
    etag TEXT,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    PRIMARY KEY (collection_id, id),
    FOREIGN KEY (collection_id) REFERENCES stac_collections(id) ON DELETE CASCADE,
    -- CHECK constraint: either datetime OR both start_datetime and end_datetime must be present
    CHECK (
        datetime IS NOT NULL OR
        (start_datetime IS NOT NULL AND end_datetime IS NOT NULL)
    ),
    -- CHECK constraint: start_datetime must be before or equal to end_datetime
    CHECK (start_datetime IS NULL OR end_datetime IS NULL OR start_datetime <= end_datetime)
);

-- Step 2: Copy data from old table to new table
INSERT INTO stac_items_new
    SELECT * FROM stac_items;

-- Step 3: Drop old table
DROP TABLE stac_items;

-- Step 4: Rename new table to original name
ALTER TABLE stac_items_new RENAME TO stac_items;

-- Step 5: Recreate indexes
CREATE INDEX IF NOT EXISTS idx_stac_items_collection ON stac_items(collection_id);

-- Optimized temporal indexes for range queries
CREATE INDEX IF NOT EXISTS idx_stac_items_temporal_start ON stac_items(collection_id, COALESCE(start_datetime, datetime));
CREATE INDEX IF NOT EXISTS idx_stac_items_temporal_end ON stac_items(collection_id, COALESCE(end_datetime, datetime));
CREATE INDEX IF NOT EXISTS idx_stac_items_temporal_range ON stac_items(collection_id, COALESCE(start_datetime, datetime), COALESCE(end_datetime, datetime), id);
CREATE INDEX IF NOT EXISTS idx_stac_items_datetime_point ON stac_items(collection_id, datetime) WHERE datetime IS NOT NULL AND start_datetime IS NULL AND end_datetime IS NULL;
CREATE INDEX IF NOT EXISTS idx_stac_items_datetime_range ON stac_items(collection_id, start_datetime, end_datetime) WHERE start_datetime IS NOT NULL AND end_datetime IS NOT NULL;
