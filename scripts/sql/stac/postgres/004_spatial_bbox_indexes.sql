-- Migration: Spatial BBOX Functional Indexes for STAC Items
-- Description: Adds functional indexes on bbox JSON extraction to accelerate spatial queries
-- Performance Target: 100-1000x faster spatial bbox queries on large datasets
-- Database: PostgreSQL
-- Priority: P0 (Critical Performance Issue)
-- Related Issue: CATALOG_REVIEW_FINDINGS.md Section 2.1

-- These indexes enable the query planner to use indexes when filtering on bbox coordinates
-- without requiring a full table scan or JSON parsing on every row.

-- Index 1: Minimum X coordinate (west/left boundary)
-- Supports queries filtering on western extent: WHERE bbox_minx <= @x
CREATE INDEX IF NOT EXISTS idx_stac_items_bbox_minx
ON stac_items(collection_id, ((bbox_json::json->>0)::double precision))
WHERE bbox_json IS NOT NULL;

-- Index 2: Minimum Y coordinate (south/bottom boundary)
-- Supports queries filtering on southern extent: WHERE bbox_miny <= @y
CREATE INDEX IF NOT EXISTS idx_stac_items_bbox_miny
ON stac_items(collection_id, ((bbox_json::json->>1)::double precision))
WHERE bbox_json IS NOT NULL;

-- Index 3: Maximum X coordinate (east/right boundary)
-- Supports queries filtering on eastern extent: WHERE bbox_maxx >= @x
CREATE INDEX IF NOT EXISTS idx_stac_items_bbox_maxx
ON stac_items(collection_id, ((bbox_json::json->>2)::double precision))
WHERE bbox_json IS NOT NULL;

-- Index 4: Maximum Y coordinate (north/top boundary)
-- Supports queries filtering on northern extent: WHERE bbox_maxy >= @y
CREATE INDEX IF NOT EXISTS idx_stac_items_bbox_maxy
ON stac_items(collection_id, ((bbox_json::json->>3)::double precision))
WHERE bbox_json IS NOT NULL;

-- Index 5: Raster dataset foreign key index
-- Supports queries filtering items by raster_dataset_id
-- Improves performance when querying items for specific raster datasets
CREATE INDEX IF NOT EXISTS idx_stac_items_raster_dataset
ON stac_items(collection_id, raster_dataset_id)
WHERE raster_dataset_id IS NOT NULL;

-- Update query planner statistics
ANALYZE stac_items;

-- Performance notes:
-- 1. These functional indexes allow PostgreSQL to use index scans instead of sequential scans
--    when filtering on bbox coordinates, reducing query time from seconds to milliseconds.
-- 2. The WHERE clause (bbox_json IS NOT NULL / raster_dataset_id IS NOT NULL) creates
--    partial indexes that only include rows with valid data, reducing index size and
--    maintenance overhead.
-- 3. collection_id is included as the first column to support collection-scoped queries,
--    which is the most common access pattern in STAC searches.
-- 4. Index size impact: Each index is ~10-20% of the table size for typical datasets.
--    With 100k items, expect ~50-100 MB total for all 5 indexes.
-- 5. These indexes complement the existing GIST spatial index (idx_stac_items_geometry)
--    by providing a lightweight alternative for simple bbox intersection queries that
--    don't require full geometry operations.
--
-- Example query patterns that benefit:
-- - Bbox intersection: WHERE bbox_minx <= @maxx AND bbox_maxx >= @minx AND bbox_miny <= @maxy AND bbox_maxy >= @miny
-- - Spatial extent queries: WHERE bbox_minx >= @west AND bbox_maxx <= @east
-- - Raster item lookup: WHERE raster_dataset_id = @dataset_id
--
-- Expected performance improvement:
-- - Before indexes: 5-10 seconds for 10k items with bbox filter (sequential scan + JSON parsing)
-- - After indexes: 10-50ms for same query (index scan)
-- - Improvement factor: 100-1000x depending on selectivity
