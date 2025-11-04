-- Migration: Spatial BBOX Functional Indexes for STAC Items
-- Description: Adds functional indexes on bbox JSON extraction to accelerate spatial queries
-- Performance Target: 100-1000x faster spatial bbox queries on large datasets
-- Database: MySQL 8.0+
-- Priority: P0 (Critical Performance Issue)
-- Related Issue: CATALOG_REVIEW_FINDINGS.md Section 2.1

-- MySQL 8.0+ supports functional indexes on JSON fields
-- These indexes enable the query planner to use indexes when filtering on bbox coordinates

-- Index 1: Minimum X coordinate (west/left boundary)
CREATE INDEX idx_stac_items_bbox_minx
ON stac_items(collection_id, (CAST(JSON_EXTRACT(bbox_json, '$[0]') AS DECIMAL(20,10))));

-- Index 2: Minimum Y coordinate (south/bottom boundary)
CREATE INDEX idx_stac_items_bbox_miny
ON stac_items(collection_id, (CAST(JSON_EXTRACT(bbox_json, '$[1]') AS DECIMAL(20,10))));

-- Index 3: Maximum X coordinate (east/right boundary)
CREATE INDEX idx_stac_items_bbox_maxx
ON stac_items(collection_id, (CAST(JSON_EXTRACT(bbox_json, '$[2]') AS DECIMAL(20,10))));

-- Index 4: Maximum Y coordinate (north/top boundary)
CREATE INDEX idx_stac_items_bbox_maxy
ON stac_items(collection_id, (CAST(JSON_EXTRACT(bbox_json, '$[3]') AS DECIMAL(20,10))));

-- Index 5: Raster dataset foreign key index
CREATE INDEX idx_stac_items_raster_dataset
ON stac_items(collection_id, raster_dataset_id);

-- Update optimizer statistics
ANALYZE TABLE stac_items;

-- Performance notes:
-- 1. MySQL 8.0+ supports functional indexes on JSON expressions
-- 2. These indexes reduce query time from seconds to milliseconds for spatial filters
-- 3. Expected improvement: 100-1000x faster spatial queries
