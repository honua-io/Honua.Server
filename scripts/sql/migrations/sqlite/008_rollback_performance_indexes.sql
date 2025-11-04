-- SQLite Rollback Script for Migration 008
-- Purpose: Remove performance indexes added in 008_add_performance_indexes.sql
-- Note: Index drops are fast and non-blocking in SQLite

-- ========================================
-- Rollback Alert History Indexes
-- ========================================

DROP INDEX IF EXISTS idx_alert_history_fingerprint_timestamp;
DROP INDEX IF EXISTS idx_alert_history_severity_timestamp;
DROP INDEX IF EXISTS idx_alert_history_status_timestamp;
DROP INDEX IF EXISTS idx_alert_history_environment_timestamp;

-- ========================================
-- Rollback STAC Collections Indexes
-- ========================================

DROP INDEX IF EXISTS idx_stac_collections_service_layer;
DROP INDEX IF EXISTS idx_stac_collections_data_source;
DROP INDEX IF EXISTS idx_stac_collections_updated_at;

-- ========================================
-- Rollback STAC Items Indexes
-- ========================================

DROP INDEX IF EXISTS idx_stac_items_bbox_datetime;
DROP INDEX IF EXISTS idx_stac_items_collection_raster;
DROP INDEX IF EXISTS idx_stac_items_datetime_desc;
DROP INDEX IF EXISTS idx_stac_items_collection_updated;

-- ========================================
-- Update Statistics
-- ========================================

ANALYZE alert_history;
ANALYZE stac_collections;
ANALYZE stac_items;

-- Optional: Vacuum to reclaim space
-- VACUUM;

-- ========================================
-- Verification
-- ========================================

-- Verify indexes have been removed:
-- SELECT
--     name AS index_name,
--     tbl_name AS table_name
-- FROM sqlite_master
-- WHERE type = 'index'
--   AND name IN (
--     'idx_alert_history_fingerprint_timestamp',
--     'idx_alert_history_severity_timestamp',
--     'idx_alert_history_status_timestamp',
--     'idx_alert_history_environment_timestamp',
--     'idx_stac_collections_service_layer',
--     'idx_stac_collections_data_source',
--     'idx_stac_collections_updated_at',
--     'idx_stac_items_bbox_datetime',
--     'idx_stac_items_collection_raster',
--     'idx_stac_items_datetime_desc',
--     'idx_stac_items_collection_updated'
-- );
-- Expected: No rows returned
