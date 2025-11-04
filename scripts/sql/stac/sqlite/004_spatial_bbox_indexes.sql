-- Migration: Spatial BBOX Functional Indexes for STAC Items
-- Description: Adds functional indexes on bbox JSON extraction to accelerate spatial queries
-- Performance Target: 100-1000x faster spatial bbox queries on large datasets
-- Database: SQLite
-- Priority: P0 (Critical Performance Issue)
-- Related Issue: CATALOG_REVIEW_FINDINGS.md Section 2.1

-- SQLite supports indexes on JSON extraction expressions
-- These indexes enable the query planner to use indexes when filtering on bbox coordinates

-- Index 1: Minimum X coordinate (west/left boundary)
CREATE INDEX IF NOT EXISTS idx_stac_items_bbox_minx
ON stac_items(collection_id, CAST(json_extract(bbox_json, '$[0]') AS REAL))
WHERE bbox_json IS NOT NULL;

-- Index 2: Minimum Y coordinate (south/bottom boundary)
CREATE INDEX IF NOT EXISTS idx_stac_items_bbox_miny
ON stac_items(collection_id, CAST(json_extract(bbox_json, '$[1]') AS REAL))
WHERE bbox_json IS NOT NULL;

-- Index 3: Maximum X coordinate (east/right boundary)
CREATE INDEX IF NOT EXISTS idx_stac_items_bbox_maxx
ON stac_items(collection_id, CAST(json_extract(bbox_json, '$[2]') AS REAL))
WHERE bbox_json IS NOT NULL;

-- Index 4: Maximum Y coordinate (north/top boundary)
CREATE INDEX IF NOT EXISTS idx_stac_items_bbox_maxy
ON stac_items(collection_id, CAST(json_extract(bbox_json, '$[3]') AS REAL))
WHERE bbox_json IS NOT NULL;

-- Index 5: Raster dataset foreign key index
CREATE INDEX IF NOT EXISTS idx_stac_items_raster_dataset
ON stac_items(collection_id, raster_dataset_id)
WHERE raster_dataset_id IS NOT NULL;

-- SQLite automatically updates statistics on index creation
-- ANALYZE stac_items; -- Optional: explicitly update statistics

-- Performance notes:
-- 1. SQLite supports partial indexes with WHERE clauses to reduce index size
-- 2. These indexes reduce query time from seconds to milliseconds for spatial filters
-- 3. Expected improvement: 100-1000x faster spatial queries
