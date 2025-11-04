-- MySQL Rollback Script for Migration 008
-- Purpose: Remove performance indexes added in 008_add_performance_indexes.sql
-- Note: Index drops are blocking operations in MySQL

-- ========================================
-- Rollback Alert History Indexes
-- ========================================

DROP INDEX IF EXISTS idx_alert_history_fingerprint_datetime ON alert_history;
DROP INDEX IF EXISTS idx_alert_history_severity_timestamp ON alert_history;
DROP INDEX IF EXISTS idx_alert_history_status_timestamp ON alert_history;
DROP INDEX IF EXISTS idx_alert_history_environment ON alert_history;

-- ========================================
-- Rollback STAC Collections Indexes
-- ========================================

DROP INDEX IF EXISTS idx_stac_collections_service_layer ON stac_collections;
DROP INDEX IF EXISTS idx_stac_collections_data_source ON stac_collections;

-- ========================================
-- Rollback STAC Items Indexes
-- ========================================

DROP INDEX IF EXISTS idx_stac_items_bbox_datetime ON stac_items;
DROP INDEX IF EXISTS idx_stac_items_collection_raster ON stac_items;
DROP INDEX IF EXISTS idx_stac_items_datetime_desc ON stac_items;

-- Drop spatial index if it exists
DROP INDEX IF EXISTS sidx_stac_items_geometry ON stac_items;

-- ========================================
-- Update Statistics
-- ========================================

ANALYZE TABLE alert_history;
ANALYZE TABLE stac_collections;
ANALYZE TABLE stac_items;

-- ========================================
-- Verification
-- ========================================

-- Verify indexes have been removed:
-- SELECT
--     table_schema,
--     table_name,
--     index_name
-- FROM information_schema.statistics
-- WHERE table_schema = DATABASE()
--   AND index_name IN (
--     'idx_alert_history_fingerprint_datetime',
--     'idx_alert_history_severity_timestamp',
--     'idx_alert_history_status_timestamp',
--     'idx_alert_history_environment',
--     'idx_stac_collections_service_layer',
--     'idx_stac_collections_data_source',
--     'idx_stac_items_bbox_datetime',
--     'idx_stac_items_collection_raster',
--     'idx_stac_items_datetime_desc',
--     'sidx_stac_items_geometry'
-- )
-- GROUP BY table_schema, table_name, index_name;
-- Expected: No rows returned
